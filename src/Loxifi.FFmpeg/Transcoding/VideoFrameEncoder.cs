// VideoFrameEncoder.cs — Encode a video from raw frames supplied by the caller.
// The counterpart to MediaTranscoder: there is no demuxer and no decoder, because the frames do not
// come from a file. Pixels in, container out.

using System.Runtime.InteropServices;
using System.Text;
using Loxifi.FFmpeg.Helpers;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;
using Loxifi.FFmpeg.Transcoding.Codecs;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Encodes a video from raw pixel buffers pushed in one frame at a time, writing the container to a
/// <see cref="Stream"/>.
///
/// <para>Use this when the frames cannot be handed to FFmpeg as a file. The case this was written for is
/// an animated WebP: FFmpeg's WebP decoder returns only the FIRST frame, so the animation has to be
/// decoded elsewhere and the frames pushed through here.</para>
///
/// <para>Usage — write every frame, then <see cref="Complete"/>. Disposing without completing abandons
/// the output, which is the right behaviour for an abandoned encode; a partially written MP4 is not a
/// file anyone wants written to disk.</para>
/// <code>
/// using var encoder = new VideoFrameEncoder(output, new FrameEncodeOptions
/// {
///     Width = 640, Height = 480, FrameRate = 24, Quality = 20, Preset = "veryfast", Fragmented = true,
/// });
/// foreach (var frame in frames) encoder.WriteFrame(frame);   // packed RGBA, Width * Height * 4 bytes
/// encoder.Complete();
/// </code>
/// </summary>
public sealed unsafe class VideoFrameEncoder : IDisposable
{
    private readonly FrameEncodeOptions _options;
    private readonly int _sourceStride;
    private readonly int _sourceBytes;

    private AVFormatContext* _outputCtx;
    private AVCodecContext* _encoderCtx;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private nint _swsCtx;
    private StreamIOContext? _outputIO;

    private long _nextPts;
    private bool _headerWritten;
    private bool _completed;
    private bool _disposed;

    /// <summary>Number of frames written so far.</summary>
    public long FrameCount => _nextPts;

    /// <summary>
    /// Creates an encoder writing to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Destination stream. Not disposed by this class.</param>
    /// <param name="options">Frame geometry, codec and quality settings.</param>
    /// <exception cref="ArgumentOutOfRangeException">Width, height or frame rate is not positive.</exception>
    /// <exception cref="FFmpegException">The encoder or muxer could not be initialised.</exception>
    public VideoFrameEncoder(Stream output, FrameEncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Width <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Width must be positive.");
        if (options.Height <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Height must be positive.");
        if (!(options.FrameRate > 0)) throw new ArgumentOutOfRangeException(nameof(options), "FrameRate must be positive.");

        _options = options;
        _sourceStride = options.Width * BytesPerPixel(options.SourcePixelFormat);
        _sourceBytes = checked(_sourceStride * options.Height);

        try
        {
            Initialise(output);
        }
        catch
        {
            // A half-built encoder owns native allocations that nothing else will ever free.
            Cleanup();
            throw;
        }
    }

    /// <summary>
    /// Encodes one frame. The buffer must hold exactly <c>Width * Height * bytes-per-pixel</c> bytes in
    /// <see cref="FrameEncodeOptions.SourcePixelFormat"/>, tightly packed.
    /// </summary>
    /// <param name="pixels">The frame's pixel data.</param>
    /// <exception cref="ArgumentException">The buffer is not exactly one frame.</exception>
    /// <exception cref="InvalidOperationException">Called after <see cref="Complete"/>.</exception>
    public void WriteFrame(ReadOnlySpan<byte> pixels)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) throw new InvalidOperationException("Cannot write frames after Complete().");
        if (pixels.Length != _sourceBytes)
            throw new ArgumentException(
                $"Expected exactly {_sourceBytes} bytes for a {_options.Width}x{_options.Height} frame, got {pixels.Length}.",
                nameof(pixels));

        // The frame buffer is shared with the encoder, which may still hold a reference to the previous
        // frame. Asking for writability copies it only when that is actually the case.
        FFmpegException.ThrowIfError(AVUtil.av_frame_make_writable(_frame), "Frame is not writable");

        fixed (byte* src = pixels)
        {
            byte** srcPlanes = stackalloc byte*[8];
            int* srcStrides = stackalloc int[8];
            for (int i = 0; i < 8; i++) { srcPlanes[i] = null; srcStrides[i] = 0; }
            srcPlanes[0] = src;
            srcStrides[0] = _sourceStride;

            byte** dstPlanes = stackalloc byte*[8];
            int* dstStrides = stackalloc int[8];
            dstPlanes[0] = (byte*)_frame->Data0;
            dstPlanes[1] = (byte*)_frame->Data1;
            dstPlanes[2] = (byte*)_frame->Data2;
            dstPlanes[3] = (byte*)_frame->Data3;
            for (int i = 4; i < 8; i++) dstPlanes[i] = null;
            for (int i = 0; i < 8; i++) dstStrides[i] = _frame->Linesize[i];

            int scaled = SWScale.sws_scale(_swsCtx, srcPlanes, srcStrides, 0, _options.Height, dstPlanes, dstStrides);
            if (scaled <= 0) throw new FFmpegException(scaled, "Failed to convert frame to the encoder's pixel format");
        }

        _frame->Pts = _nextPts++;
        Drain(_frame);
    }

    /// <summary>
    /// Flushes the encoder and finalises the container. Must be called for the output to be a valid
    /// file. Safe to call once; a second call does nothing.
    /// </summary>
    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) return;

        // A null frame tells the encoder to emit whatever it still holds (B-frames, lookahead).
        Drain(null);
        FFmpegException.ThrowIfError(AVFormat.av_write_trailer(_outputCtx), "Failed to write container trailer");
        _completed = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        Cleanup();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void Initialise(Stream output)
    {
        // --- Muxer ---
        byte[] formatName = Encoding.UTF8.GetBytes(_options.OutputFormat.ToFFmpegName() + "\0");
        fixed (byte* fmt = formatName)
        {
            AVFormatContext* ctx = null;
            FFmpegException.ThrowIfError(
                AVFormat.avformat_alloc_output_context2(&ctx, nint.Zero, fmt, null),
                "Failed to allocate output context");
            _outputCtx = ctx;
        }

        _outputIO = StreamIOContext.ForWriting(output);
        _outputCtx->Pb = _outputIO.Context;

        // --- Encoder ---
        string codecName = _options.VideoCodec.Name;
        nint encoder = AVCodec.avcodec_find_encoder_by_name(codecName);
        if (encoder == nint.Zero)
            throw new FFmpegException(-1,
                $"Encoder '{codecName}' is not present in the loaded FFmpeg build. " +
                "libx264 and libx265 need one of the .GPL runtime packages.");

        _encoderCtx = AVCodec.avcodec_alloc_context3(encoder);
        if (_encoderCtx == null) throw new FFmpegException(-1, "Failed to allocate encoder context");

        // YUV420P subsamples chroma by two, so an odd dimension has no representation. libx264 refuses
        // to open rather than rounding, and the error does not mention the size -- so round here, where
        // the scaler that follows can be told about it.
        _encoderCtx->Width = Math.Max(2, _options.Width & ~1);
        _encoderCtx->Height = Math.Max(2, _options.Height & ~1);
        _encoderCtx->PixFmt = _options.EncoderPixelFormat;

        AVRational rate = ToRational(_options.FrameRate);
        _encoderCtx->FrameRate = rate;
        _encoderCtx->TimeBase = new AVRational(rate.Denominator, rate.Numerator);
        if (_options.BitRate > 0) _encoderCtx->BitRate = _options.BitRate;

        // MP4 keeps codec extradata (H.264 SPS/PPS) in the container header rather than inline before
        // each keyframe. Without this flag the muxer has nothing to write there.
        AVOutputFormat* oformat = (AVOutputFormat*)_outputCtx->Oformat;
        if ((oformat->Flags & (int)AVFormatFlags.AVFMT_GLOBALHEADER) != 0)
            _encoderCtx->Flags |= (int)AVCodecFlags.AV_CODEC_FLAG_GLOBAL_HEADER;

        nint codecOpts = nint.Zero;
        try
        {
            if (_options.Quality is int crf) DictSet(&codecOpts, "crf", crf.ToString());
            if (!string.IsNullOrWhiteSpace(_options.Preset)) DictSet(&codecOpts, "preset", _options.Preset!);
            if (_options.CodecOptions is { } extra)
                foreach (var (key, value) in extra) DictSet(&codecOpts, key, value);

            FFmpegException.ThrowIfError(
                AVCodec.avcodec_open2(_encoderCtx, encoder, &codecOpts),
                $"Failed to open encoder '{codecName}'");
        }
        finally
        {
            if (codecOpts != nint.Zero) AVUtil.av_dict_free(&codecOpts);
        }

        AVStream* stream = AVFormat.avformat_new_stream(_outputCtx, nint.Zero);
        if (stream == null) throw new FFmpegException(-1, "Failed to create output stream");
        FFmpegException.ThrowIfError(
            AVCodec.avcodec_parameters_from_context(stream->Codecpar, _encoderCtx),
            "Failed to copy encoder parameters to the output stream");
        stream->TimeBase = _encoderCtx->TimeBase;

        // --- Header ---
        nint muxerOpts = nint.Zero;
        try
        {
            if (_options.Fragmented)
                DictSet(&muxerOpts, "movflags", "+frag_keyframe+empty_moov+default_base_moof");
            if (_options.MuxerOptions is { } extra)
                foreach (var (key, value) in extra) DictSet(&muxerOpts, key, value);

            FFmpegException.ThrowIfError(
                AVFormat.avformat_write_header(_outputCtx, &muxerOpts),
                "Failed to write container header");
            _headerWritten = true;
        }
        finally
        {
            if (muxerOpts != nint.Zero) AVUtil.av_dict_free(&muxerOpts);
        }

        // --- Reusable frame + scaler ---
        _frame = AVUtil.av_frame_alloc();
        if (_frame == null) throw new FFmpegException(-1, "Failed to allocate frame");
        _frame->Width = _encoderCtx->Width;
        _frame->Height = _encoderCtx->Height;
        _frame->Format = (int)_encoderCtx->PixFmt;
        FFmpegException.ThrowIfError(AVUtil.av_frame_get_buffer(_frame, 32), "Failed to allocate frame buffer");

        _packet = AVCodec.av_packet_alloc();
        if (_packet == null) throw new FFmpegException(-1, "Failed to allocate packet");

        // Always created, even when nothing appears to change: the source is packed and the encoder's
        // format is planar, so there is a conversion to do in every realistic configuration.
        _swsCtx = SWScale.sws_getContext(
            _options.Width, _options.Height, _options.SourcePixelFormat,
            _encoderCtx->Width, _encoderCtx->Height, _encoderCtx->PixFmt,
            SwsFlags.SWS_BILINEAR, nint.Zero, nint.Zero, nint.Zero);
        if (_swsCtx == nint.Zero) throw new FFmpegException(-1, "Failed to create pixel format converter");
    }

    /// <summary>Sends a frame (or null to flush) and writes every packet the encoder returns.</summary>
    private void Drain(AVFrame* frame)
    {
        int ret = AVCodec.avcodec_send_frame(_encoderCtx, frame);
        if (ret < 0 && ret != AVErrors.AVERROR_EAGAIN)
            throw new FFmpegException(ret, "Failed to send frame to the encoder");

        while (true)
        {
            ret = AVCodec.avcodec_receive_packet(_encoderCtx, _packet);
            if (ret == AVErrors.AVERROR_EAGAIN || ret == AVErrors.AVERROR_EOF) break;
            FFmpegException.ThrowIfError(ret, "Failed to encode frame");

            try
            {
                _packet->StreamIndex = 0;
                AVStream* stream = ((AVStream**)_outputCtx->Streams)[0];
                AVCodec.av_packet_rescale_ts(_packet, _encoderCtx->TimeBase, stream->TimeBase);
                FFmpegException.ThrowIfError(
                    AVFormat.av_interleaved_write_frame(_outputCtx, _packet),
                    "Failed to write encoded packet");
            }
            finally
            {
                AVCodec.av_packet_unref(_packet);
            }
        }
    }

    /// <summary>Converts a decimal frame rate to a rational, keeping NTSC rates exact.</summary>
    private static AVRational ToRational(double fps)
    {
        // 23.976/29.97/59.94 are 24000/1001 and friends. Rounding them to 24/30/60 drifts audio-video
        // sync on long clips, and the exact form costs nothing.
        foreach (int baseRate in new[] { 24, 30, 60, 120 })
        {
            if (Math.Abs(fps - (baseRate * 1000.0 / 1001.0)) < 0.001)
                return new AVRational(baseRate * 1000, 1001);
        }

        if (Math.Abs(fps - Math.Round(fps)) < 1e-9 && fps <= int.MaxValue)
            return new AVRational((int)Math.Round(fps), 1);

        // Otherwise keep three decimal places, which is finer than any frame delay a container records.
        return new AVRational((int)Math.Round(fps * 1000), 1000);
    }

    private static int BytesPerPixel(AVPixelFormat format) => format switch
    {
        AVPixelFormat.AV_PIX_FMT_RGBA or AVPixelFormat.AV_PIX_FMT_BGRA => 4,
        AVPixelFormat.AV_PIX_FMT_RGB24 or AVPixelFormat.AV_PIX_FMT_BGR24 => 3,
        // Planar and subsampled formats have a per-plane layout that a single tightly-packed span
        // cannot express; they would need av_image_fill_arrays and a different WriteFrame signature.
        _ => throw new NotSupportedException(
            $"{format} is not a packed format frames can be supplied in. Use RGBA, BGRA, RGB24 or BGR24."),
    };

    private static void DictSet(nint* dict, string key, string value)
    {
        byte[] k = Encoding.UTF8.GetBytes(key + "\0");
        byte[] v = Encoding.UTF8.GetBytes(value + "\0");
        fixed (byte* kp = k)
        fixed (byte* vp = v)
        {
            FFmpegException.ThrowIfError(AVUtil.av_dict_set(dict, kp, vp, 0), $"Failed to set option '{key}'");
        }
    }

    private void Cleanup()
    {
        if (_swsCtx != nint.Zero) { SWScale.sws_freeContext(_swsCtx); _swsCtx = nint.Zero; }
        if (_packet != null) { AVPacket* p = _packet; AVCodec.av_packet_free(&p); _packet = null; }
        if (_frame != null) { AVFrame* f = _frame; AVUtil.av_frame_free(&f); _frame = null; }
        if (_encoderCtx != null) { AVCodecContext* c = _encoderCtx; AVCodec.avcodec_free_context(&c); _encoderCtx = null; }

        if (_outputCtx != null)
        {
            // Pb points at the StreamIOContext's buffer, which that class owns and frees. Clearing it
            // first stops avformat_free_context from taking it too.
            _outputCtx->Pb = null;
            AVFormat.avformat_free_context(_outputCtx);
            _outputCtx = null;
        }

        _outputIO?.Dispose();
        _outputIO = null;
        _ = _headerWritten;
    }
}
