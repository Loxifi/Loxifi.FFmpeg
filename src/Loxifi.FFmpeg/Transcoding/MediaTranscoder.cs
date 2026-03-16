// MediaTranscoder.cs — Core transcoding engine that reads packets from an input,
// optionally decodes/re-encodes them (with pixel format conversion and scaling via
// libswscale), and writes the result to an output container. Supports both file paths
// and .NET Streams as input/output via StreamIOContext.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Helpers;
using Loxifi.FFmpeg.Transcoding.Codecs;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Transcodes media files or streams, supporting both stream copy (remuxing) and
/// re-encoding with codec/resolution/bitrate changes. Manages the full lifecycle of
/// FFmpeg contexts (format, codec, scaler, resampler) and ensures proper cleanup.
/// </summary>
/// <remarks>
/// <para>
/// The transcoder uses FFmpeg's "push" API: packets are read from the demuxer, sent to
/// the decoder, decoded frames are optionally scaled/converted, then sent to the encoder,
/// and the resulting encoded packets are written to the muxer.
/// </para>
/// <para>
/// Audio re-encoding is intentionally not implemented — audio streams are always stream-copied.
/// This is because configuring audio encoders requires setting <c>sample_fmt</c> and
/// <c>ch_layout</c> fields deep in AVCodecContext that are beyond our mapped struct fields.
/// Stream copy is sufficient for the primary use case (video resize to target file size).
/// </para>
/// </remarks>
public sealed unsafe class MediaTranscoder : IDisposable
{
    private AVFormatContext* _inputCtx;
    private AVFormatContext* _outputCtx;

    /// <summary>Array of decoder contexts, one per input stream. Null entries mean stream copy.</summary>
    private AVCodecContext** _decoderCtxs;

    /// <summary>Array of encoder contexts, one per input stream. Null entries mean stream copy.</summary>
    private AVCodecContext** _encoderCtxs;

    /// <summary>Array of SwsContext pointers for video scaling/pixel format conversion, one per input stream.</summary>
    private nint* _swsCtxs;

    /// <summary>Array of SwrContext pointers for audio resampling, one per input stream (currently unused).</summary>
    private nint* _swrCtxs;

    /// <summary>
    /// Maps input stream indices to output stream indices. A value of -1 means the
    /// input stream is skipped (e.g., subtitle or data streams).
    /// </summary>
    private int* _streamMap;

    private int _nbInputStreams;
    private bool _disposed;

    /// <summary>Custom I/O context for stream-based input (null for file-based input).</summary>
    private StreamIOContext? _inputIO;

    /// <summary>Custom I/O context for stream-based output (null for file-based output).</summary>
    private StreamIOContext? _outputIO;

    /// <summary>
    /// Transcodes a media file from the input path to the output path.
    /// </summary>
    /// <param name="options">Transcoding configuration (paths, codecs, bitrates, resolution).</param>
    /// <param name="progress">Optional progress reporter receiving percentage and timing updates.</param>
    /// <param name="ct">Cancellation token checked between each packet.</param>
    public void Transcode(
        TranscodeOptions options,
        IProgress<TranscodeProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            OpenInput(options.InputPath);
            SetupOutput(options);
            TranscodeLoop(options, progress, ct);
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Transcodes a media file asynchronously on a thread pool thread.
    /// </summary>
    /// <param name="options">Transcoding configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when transcoding finishes.</returns>
    public Task TranscodeAsync(
        TranscodeOptions options,
        IProgress<TranscodeProgress>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run(() => Transcode(options, progress, ct), ct);
    }

    /// <summary>
    /// Transcodes media from an input stream to an output stream.
    /// </summary>
    /// <param name="options">Stream-based transcoding configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public void Transcode(
        StreamTranscodeOptions options,
        IProgress<TranscodeProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            OpenInput(options.InputStream);
            SetupOutput(options);
            TranscodeLoop(options.ToFileOptions(), progress, ct);
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Transcodes media from streams asynchronously on a thread pool thread.
    /// </summary>
    /// <param name="options">Stream-based transcoding configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when transcoding finishes.</returns>
    public Task TranscodeAsync(
        StreamTranscodeOptions options,
        IProgress<TranscodeProgress>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run(() => Transcode(options, progress, ct), ct);
    }

    /// <summary>Convenience accessor for input stream array.</summary>
    private AVStream** InputStreams => _inputCtx->Streams;

    /// <summary>Convenience accessor for output stream array.</summary>
    private AVStream** OutputStreams => (AVStream**)_outputCtx->Streams;

    /// <summary>
    /// Opens a .NET stream as input by creating a custom AVIOContext and
    /// attaching it to the format context before opening.
    /// </summary>
    private void OpenInput(Stream inputStream)
    {
        _inputIO = StreamIOContext.ForReading(inputStream);

        _inputCtx = AVFormat.avformat_alloc_context();
        if (_inputCtx == null) throw new FFmpegException(-1, "Failed to allocate format context");

        // Attach custom I/O before avformat_open_input so FFmpeg reads from our stream
        _inputCtx->Pb = _inputIO.Context;

        fixed (AVFormatContext** pInputCtx = &_inputCtx)
        {
            FFmpegException.ThrowIfError(
                AVFormat.avformat_open_input(pInputCtx, null, nint.Zero, null),
                "Failed to open input stream");
        }

        FFmpegException.ThrowIfError(
            AVFormat.avformat_find_stream_info(_inputCtx, null),
            "Failed to find stream info");

        InitStreamArrays();
    }

    /// <summary>
    /// Opens a file path as input using FFmpeg's built-in file I/O.
    /// </summary>
    private void OpenInput(string inputPath)
    {
        fixed (AVFormatContext** pInputCtx = &_inputCtx)
        {
            nint urlPtr = Marshal.StringToHGlobalAnsi(inputPath);
            try
            {
                FFmpegException.ThrowIfError(
                    AVFormat.avformat_open_input(pInputCtx, (byte*)urlPtr, nint.Zero, null),
                    "Failed to open input");
            }
            finally
            {
                Marshal.FreeHGlobal(urlPtr);
            }
        }

        FFmpegException.ThrowIfError(
            AVFormat.avformat_find_stream_info(_inputCtx, null),
            "Failed to find stream info");

        InitStreamArrays();
    }

    /// <summary>
    /// Allocates per-stream arrays for decoders, encoders, scalers, resamplers, and the
    /// stream index mapping. All entries are zero-initialized; the stream map defaults to -1 (skip).
    /// </summary>
    private void InitStreamArrays()
    {
        _nbInputStreams = (int)_inputCtx->NbStreams;
        _decoderCtxs = (AVCodecContext**)NativeMemory.AllocZeroed((nuint)_nbInputStreams, (nuint)sizeof(nint));
        _encoderCtxs = (AVCodecContext**)NativeMemory.AllocZeroed((nuint)_nbInputStreams, (nuint)sizeof(nint));
        _swsCtxs = (nint*)NativeMemory.AllocZeroed((nuint)_nbInputStreams, (nuint)sizeof(nint));
        _swrCtxs = (nint*)NativeMemory.AllocZeroed((nuint)_nbInputStreams, (nuint)sizeof(nint));
        _streamMap = (int*)NativeMemory.Alloc((nuint)_nbInputStreams, (nuint)sizeof(int));

        // Initialize all stream mappings to -1 (skip) until configured in SetupOutputStreams
        for (int i = 0; i < _nbInputStreams; i++)
        {
            _streamMap[i] = -1;
        }
    }

    /// <summary>
    /// Sets up the output context for stream-based output using a custom AVIOContext.
    /// </summary>
    private void SetupOutput(StreamTranscodeOptions streamOptions)
    {
        var options = streamOptions.ToFileOptions();
        _outputIO = StreamIOContext.ForWriting(streamOptions.OutputStream);

        nint formatNamePtr = Marshal.StringToHGlobalAnsi(streamOptions.OutputFormat.ToFFmpegName());
        try
        {
            fixed (AVFormatContext** pOutputCtx = &_outputCtx)
            {
                FFmpegException.ThrowIfError(
                    AVFormat.avformat_alloc_output_context2(
                        pOutputCtx, nint.Zero, (byte*)formatNamePtr, null),
                    "Failed to allocate output context");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(formatNamePtr);
        }

        // Attach custom I/O for stream-based writing
        _outputCtx->Pb = _outputIO.Context;

        SetupOutputStreams(options);

        FFmpegException.ThrowIfError(
            AVFormat.avformat_write_header(_outputCtx, null),
            "Failed to write header");
    }

    /// <summary>
    /// Sets up the output context for file-based output, opening the file for writing.
    /// </summary>
    private void SetupOutput(TranscodeOptions options)
    {
        nint formatNamePtr = options.OutputFormatName is not null
            ? Marshal.StringToHGlobalAnsi(options.OutputFormatName)
            : nint.Zero;
        nint fileNamePtr = Marshal.StringToHGlobalAnsi(options.OutputPath);

        try
        {
            fixed (AVFormatContext** pOutputCtx = &_outputCtx)
            {
                FFmpegException.ThrowIfError(
                    AVFormat.avformat_alloc_output_context2(
                        pOutputCtx,
                        nint.Zero,
                        formatNamePtr != nint.Zero ? (byte*)formatNamePtr : null,
                        (byte*)fileNamePtr),
                    "Failed to allocate output context");
            }
        }
        finally
        {
            if (formatNamePtr != nint.Zero) Marshal.FreeHGlobal(formatNamePtr);
            Marshal.FreeHGlobal(fileNamePtr);
        }

        SetupOutputStreams(options);

        // Open output file (unless the format is "NOFILE", e.g., for raw codecs)
        AVOutputFormat* oformat = (AVOutputFormat*)_outputCtx->Oformat;
        if ((oformat->Flags & (int)AVFormatFlags.AVFMT_NOFILE) == 0)
        {
            nint outputPathPtr = Marshal.StringToHGlobalAnsi(options.OutputPath);
            try
            {
                FFmpegException.ThrowIfError(
                    AVFormat.avio_open(&_outputCtx->Pb, (byte*)outputPathPtr, (int)AVIOFlags.AVIO_FLAG_WRITE),
                    "Failed to open output file");
            }
            finally
            {
                Marshal.FreeHGlobal(outputPathPtr);
            }
        }

        FFmpegException.ThrowIfError(
            AVFormat.avformat_write_header(_outputCtx, null),
            "Failed to write header");
    }

    /// <summary>
    /// Iterates input streams and configures each one for either stream copy or re-encoding.
    /// Only video and audio streams are carried over; subtitle, data, and attachment streams
    /// are skipped (streamMap entry stays -1).
    /// </summary>
    private void SetupOutputStreams(TranscodeOptions options)
    {
        int outputStreamIndex = 0;

        for (int i = 0; i < _nbInputStreams; i++)
        {
            AVStream* inStream = InputStreams[i];
            AVCodecParameters* codecpar = inStream->Codecpar;

            // Skip non-audio/video streams (subtitles, data, attachments)
            if (codecpar->CodecType != AVMediaType.AVMEDIA_TYPE_VIDEO &&
                codecpar->CodecType != AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                continue;
            }

            _streamMap[i] = outputStreamIndex++;

            bool isVideo = codecpar->CodecType == AVMediaType.AVMEDIA_TYPE_VIDEO;
            string? codecName = isVideo ? options.VideoCodecName : options.AudioCodecName;
            // If a codec is specified, re-encode; otherwise stream copy (passthrough)
            bool reencode = codecName is not null;

            if (reencode)
            {
                SetupReencode(i, inStream, codecpar, codecName!, options);
            }
            else
            {
                SetupStreamCopy(inStream, codecpar);
            }
        }
    }

    /// <summary>
    /// Configures a stream for pass-through (stream copy). Copies codec parameters from
    /// input to output without setting up any decoder or encoder. This is the fastest mode
    /// since no decoding/encoding occurs.
    /// </summary>
    private void SetupStreamCopy(AVStream* inStream, AVCodecParameters* codecpar)
    {
        AVStream* outStream = AVFormat.avformat_new_stream(_outputCtx, nint.Zero);
        if (outStream == null) throw new FFmpegException(-1, "Failed to create output stream");

        FFmpegException.ThrowIfError(
            AVCodec.avcodec_parameters_copy(outStream->Codecpar, codecpar),
            "Failed to copy codec parameters");

        // Clear codec tag so the muxer can assign the correct one for the output container
        outStream->Codecpar->CodecTag = 0;
    }

    /// <summary>
    /// Configures a stream for re-encoding: sets up both decoder and encoder contexts,
    /// and optionally a pixel format converter / scaler (SwsContext).
    /// </summary>
    /// <remarks>
    /// For audio streams, this method falls back to stream copy because configuring audio
    /// encoders requires setting sample_fmt and ch_layout in AVCodecContext, which are
    /// fields beyond our partial struct mapping.
    /// </remarks>
    private void SetupReencode(int streamIndex, AVStream* inStream, AVCodecParameters* codecpar, string codecName, TranscodeOptions options)
    {
        // --- Decoder setup ---
        nint decoder = AVCodec.avcodec_find_decoder(codecpar->CodecId);
        if (decoder == nint.Zero) throw new FFmpegException(-1, "Decoder not found");

        AVCodecContext* decCtx = AVCodec.avcodec_alloc_context3(decoder);
        FFmpegException.ThrowIfError(
            AVCodec.avcodec_parameters_to_context(decCtx, codecpar),
            "Failed to copy decoder parameters");
        decCtx->TimeBase = inStream->TimeBase;

        FFmpegException.ThrowIfError(
            AVCodec.avcodec_open2(decCtx, decoder, null),
            "Failed to open decoder");
        _decoderCtxs[streamIndex] = decCtx;

        // --- Encoder setup ---
        nint encoder = AVCodec.avcodec_find_encoder_by_name(codecName);
        if (encoder == nint.Zero) throw new FFmpegException(-1, $"Encoder '{codecName}' not found");

        AVCodecContext* encCtx = AVCodec.avcodec_alloc_context3(encoder);
        _encoderCtxs[streamIndex] = encCtx;

        AVStream* outStream = AVFormat.avformat_new_stream(_outputCtx, nint.Zero);
        if (outStream == null) throw new FFmpegException(-1, "Failed to create output stream");

        if (codecpar->CodecType == AVMediaType.AVMEDIA_TYPE_VIDEO)
        {
            // Use requested dimensions, or keep original if not specified
            encCtx->Width = options.Width > 0 ? options.Width : codecpar->Width;
            encCtx->Height = options.Height > 0 ? options.Height : codecpar->Height;

            // Most encoders (especially libx264) require YUV420P pixel format.
            // If the input is in a different format (e.g., RGB from GIF, NV12 from
            // hardware decode), the SwsContext will handle the conversion.
            AVPixelFormat inputFmt = (AVPixelFormat)codecpar->Format;
            encCtx->PixFmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

            // Derive encoder timebase from framerate (timebase = 1/fps).
            // Fall back to 25fps if the input doesn't have a known frame rate.
            encCtx->FrameRate = inStream->AvgFrameRate.Denominator > 0
                ? inStream->AvgFrameRate
                : new AVRational(25, 1);
            encCtx->TimeBase = new AVRational(encCtx->FrameRate.Denominator, encCtx->FrameRate.Numerator);

            if (options.VideoBitRate > 0) encCtx->BitRate = options.VideoBitRate;

            // Create a scaler if the resolution or pixel format differs between input and output.
            // sws_scale handles both scaling and pixel format conversion in a single pass.
            if (encCtx->Width != codecpar->Width ||
                encCtx->Height != codecpar->Height ||
                encCtx->PixFmt != inputFmt)
            {
                _swsCtxs[streamIndex] = SWScale.sws_getContext(
                    codecpar->Width, codecpar->Height, inputFmt,
                    encCtx->Width, encCtx->Height, encCtx->PixFmt,
                    SwsFlags.SWS_BILINEAR,
                    nint.Zero, nint.Zero, nint.Zero);

                if (_swsCtxs[streamIndex] == nint.Zero)
                    throw new FFmpegException(-1, "Failed to create sws context");
            }
        }
        else // Audio — fall back to stream copy
        {
            // Audio re-encoding is not supported in this transcoder because configuring
            // audio encoders requires setting sample_fmt and ch_layout deep in AVCodecContext,
            // which are beyond our partial struct mapping. Stream copy is sufficient for
            // the resize use case since video bitrate dominates file size.
            AVCodec.avcodec_free_context(&decCtx);
            _decoderCtxs[streamIndex] = null;
            AVCodec.avcodec_free_context(&encCtx);
            _encoderCtxs[streamIndex] = null;
            FFmpegException.ThrowIfError(
                AVCodec.avcodec_parameters_copy(outStream->Codecpar, codecpar),
                "Failed to copy audio codec parameters");
            outStream->Codecpar->CodecTag = 0;
            return;
        }

        // Some containers (e.g., MP4) require codec extradata in the file header rather
        // than inline in the stream. Setting GLOBAL_HEADER tells the encoder to write
        // SPS/PPS (for H.264) to extradata instead of prepending to each keyframe.
        AVOutputFormat* oformat = (AVOutputFormat*)_outputCtx->Oformat;
        if ((oformat->Flags & (int)AVFormatFlags.AVFMT_GLOBALHEADER) != 0)
        {
            encCtx->Flags |= (int)AVCodecFlags.AV_CODEC_FLAG_GLOBAL_HEADER;
        }

        FFmpegException.ThrowIfError(
            AVCodec.avcodec_open2(encCtx, encoder, null),
            "Failed to open encoder");

        // Copy encoder parameters (codec ID, extradata, pixel format, etc.) to the
        // output stream so the muxer knows how to write the container headers.
        FFmpegException.ThrowIfError(
            AVCodec.avcodec_parameters_from_context(outStream->Codecpar, encCtx),
            "Failed to copy encoder parameters to output stream");
    }

    /// <summary>
    /// Main transcoding loop. Reads packets from the demuxer and either stream-copies them
    /// or decodes/re-encodes them, writing the result to the output. Reports progress based
    /// on packet timestamps relative to the total input duration.
    /// </summary>
    private void TranscodeLoop(
        TranscodeOptions options,
        IProgress<TranscodeProgress>? progress,
        CancellationToken ct)
    {
        AVPacket* packet = AVCodec.av_packet_alloc();
        AVFrame* frame = AVUtil.av_frame_alloc();
        AVFrame* scaledFrame = AVUtil.av_frame_alloc();

        if (packet == null || frame == null || scaledFrame == null)
            throw new FFmpegException(-1, "Failed to allocate packet/frame");

        Stopwatch sw = Stopwatch.StartNew();
        // Duration is in AV_TIME_BASE units (microseconds)
        long totalDuration = _inputCtx->Duration;

        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int ret = AVFormat.av_read_frame(_inputCtx, packet);
                if (ret == AVErrors.AVERROR_EOF) break;
                FFmpegException.ThrowIfError(ret, "Error reading frame");

                int inputStreamIndex = packet->StreamIndex;

                // Skip packets from unmapped streams (subtitles, data, etc.)
                if (inputStreamIndex >= _nbInputStreams || _streamMap[inputStreamIndex] < 0)
                {
                    AVCodec.av_packet_unref(packet);
                    continue;
                }

                int outputStreamIndex = _streamMap[inputStreamIndex];

                if (_decoderCtxs[inputStreamIndex] != null)
                {
                    // Re-encode path: decode -> optionally scale -> encode -> write
                    ProcessReencode(packet, frame, scaledFrame, inputStreamIndex, outputStreamIndex);
                }
                else
                {
                    // Stream copy path: rescale timestamps and pass through unchanged
                    AVStream* inStream = InputStreams[inputStreamIndex];
                    AVStream* outStream = OutputStreams[outputStreamIndex];

                    packet->StreamIndex = outputStreamIndex;
                    // Convert packet timestamps from input stream timebase to output stream timebase
                    AVCodec.av_packet_rescale_ts(packet, inStream->TimeBase, outStream->TimeBase);
                    // Reset file position since it's meaningless in the output file
                    packet->Pos = -1;

                    FFmpegException.ThrowIfError(
                        AVFormat.av_interleaved_write_frame(_outputCtx, packet),
                        "Error writing packet");
                }

                // Report progress based on the current packet's presentation timestamp
                if (progress != null && totalDuration > 0 && totalDuration < long.MaxValue / 2)
                {
                    long pos = packet->Pts >= 0 ? packet->Pts : packet->Dts;
                    if (pos >= 0)
                    {
                        AVStream* inStream = _inputCtx->Streams[inputStreamIndex];
                        double posSeconds = pos * inStream->TimeBase.ToDouble();
                        double durationSeconds = totalDuration / 1_000_000.0; // Convert microseconds to seconds
                        // Guard against nonsensical values from corrupt metadata
                        if (posSeconds >= 0 && posSeconds < 1e9 && durationSeconds > 0 && durationSeconds < 1e9)
                        {
                            double percent = Math.Clamp(posSeconds / durationSeconds * 100.0, 0, 100);

                            progress.Report(new TranscodeProgress(
                                sw.Elapsed,
                                TimeSpan.FromSeconds(posSeconds),
                                TimeSpan.FromSeconds(durationSeconds),
                                percent));
                        }
                    }
                }

                AVCodec.av_packet_unref(packet);
            }

            // Flush all encoders by sending a null frame, which tells the encoder to
            // output any buffered frames (e.g., B-frames waiting for reordering).
            for (int i = 0; i < _nbInputStreams; i++)
            {
                if (_encoderCtxs[i] != null)
                {
                    FlushEncoder(i, _streamMap[i]);
                }
            }

            // Write the container trailer (e.g., moov atom for MP4)
            FFmpegException.ThrowIfError(
                AVFormat.av_write_trailer(_outputCtx),
                "Error writing trailer");
        }
        finally
        {
            AVCodec.av_packet_free(&packet);
            AVUtil.av_frame_free(&frame);
            AVUtil.av_frame_free(&scaledFrame);
        }
    }

    /// <summary>
    /// Processes a single packet through the decode -> scale -> encode pipeline.
    /// Uses FFmpeg's "push/pull" codec API: send_packet pushes data in, receive_frame
    /// pulls decoded frames out, and the same pattern is used for encoding.
    /// </summary>
    private void ProcessReencode(AVPacket* packet, AVFrame* frame, AVFrame* scaledFrame, int inputIndex, int outputIndex)
    {
        AVCodecContext* decCtx = _decoderCtxs[inputIndex];
        AVCodecContext* encCtx = _encoderCtxs[inputIndex];

        // Send the compressed packet to the decoder
        int ret = AVCodec.avcodec_send_packet(decCtx, packet);
        // EAGAIN means the decoder's internal buffer is full; we need to drain frames first
        if (ret < 0 && ret != AVErrors.AVERROR_EAGAIN) return;

        // Pull all available decoded frames from the decoder
        while (true)
        {
            ret = AVCodec.avcodec_receive_frame(decCtx, frame);
            if (ret == AVErrors.AVERROR_EAGAIN || ret == AVErrors.AVERROR_EOF) break;
            FFmpegException.ThrowIfError(ret, "Error decoding frame");

            AVFrame* frameToEncode = frame;

            // Rescale frame PTS from the decoder's timebase (typically the input stream's
            // timebase) to the encoder's timebase (typically 1/fps). This ensures the
            // encoder stamps output packets with correct timestamps.
            if (frame->Pts >= 0)
            {
                AVStream* inStream = InputStreams[inputIndex];
                frame->Pts = AVUtil.av_rescale_q(frame->Pts, inStream->TimeBase, encCtx->TimeBase);
            }

            // Apply video scaling and/or pixel format conversion if a SwsContext was created.
            // sws_scale converts the frame in-place from source format/resolution to target.
            if (_swsCtxs[inputIndex] != nint.Zero)
            {
                AVUtil.av_frame_unref(scaledFrame);
                scaledFrame->Width = encCtx->Width;
                scaledFrame->Height = encCtx->Height;
                scaledFrame->Format = (int)encCtx->PixFmt;
                FFmpegException.ThrowIfError(
                    AVUtil.av_frame_get_buffer(scaledFrame, 0),
                    "Failed to allocate scaled frame buffer");

                // AVFrame stores data pointers and linesizes as fixed-size arrays.
                // We extract them into managed arrays for the pinned pointer call.
                // Data pointers point to each plane (Y, U, V for YUV420P; R, G, B for RGB).
                // Linesize is the byte stride of each plane (may include padding for alignment).
                byte*[] srcData = [
                    (byte*)frame->Data0, (byte*)frame->Data1,
                    (byte*)frame->Data2, (byte*)frame->Data3,
                    (byte*)frame->Data4, (byte*)frame->Data5,
                    (byte*)frame->Data6, (byte*)frame->Data7
                ];
                byte*[] dstData = [
                    (byte*)scaledFrame->Data0, (byte*)scaledFrame->Data1,
                    (byte*)scaledFrame->Data2, (byte*)scaledFrame->Data3,
                    (byte*)scaledFrame->Data4, (byte*)scaledFrame->Data5,
                    (byte*)scaledFrame->Data6, (byte*)scaledFrame->Data7
                ];
                int[] srcLinesize = new int[8];
                int[] dstLinesize = new int[8];
                for (int j = 0; j < 8; j++)
                {
                    srcLinesize[j] = frame->Linesize[j];
                    dstLinesize[j] = scaledFrame->Linesize[j];
                }

                // Pin the managed arrays and call sws_scale, which performs the actual
                // pixel format conversion and/or resolution scaling in a single pass.
                fixed (byte** srcPtr = srcData)
                fixed (byte** dstPtr = dstData)
                fixed (int* srcStridePtr = srcLinesize)
                fixed (int* dstStridePtr = dstLinesize)
                {
                    SWScale.sws_scale(
                        _swsCtxs[inputIndex],
                        srcPtr, srcStridePtr,
                        0, frame->Height, // Process all rows starting from row 0
                        dstPtr, dstStridePtr);
                }

                // Carry over the presentation timestamp to the scaled frame
                scaledFrame->Pts = frame->Pts;
                frameToEncode = scaledFrame;
            }

            // Apply audio resampling if a SwrContext was created (currently unused)
            if (_swrCtxs[inputIndex] != nint.Zero)
            {
                SWResample.swr_convert_frame(_swrCtxs[inputIndex], scaledFrame, frame);
                scaledFrame->Pts = frame->Pts;
                frameToEncode = scaledFrame;
            }

            EncodeAndWrite(encCtx, frameToEncode, outputIndex);

            AVUtil.av_frame_unref(frame);
            if (frameToEncode == scaledFrame) AVUtil.av_frame_unref(scaledFrame);
        }
    }

    /// <summary>
    /// Sends a frame to the encoder and writes all resulting packets to the output.
    /// Pass a null frame to flush the encoder (drain buffered frames).
    /// </summary>
    private void EncodeAndWrite(AVCodecContext* encCtx, AVFrame* frame, int outputStreamIndex)
    {
        int ret = AVCodec.avcodec_send_frame(encCtx, frame);
        if (ret < 0 && ret != AVErrors.AVERROR_EAGAIN) return;

        AVPacket* outPkt = AVCodec.av_packet_alloc();
        try
        {
            while (true)
            {
                ret = AVCodec.avcodec_receive_packet(encCtx, outPkt);
                if (ret == AVErrors.AVERROR_EAGAIN || ret == AVErrors.AVERROR_EOF) break;
                FFmpegException.ThrowIfError(ret, "Error encoding frame");

                outPkt->StreamIndex = outputStreamIndex;
                AVStream* outStream = OutputStreams[outputStreamIndex];
                // Convert packet timestamps from encoder timebase to output stream timebase
                AVCodec.av_packet_rescale_ts(outPkt, encCtx->TimeBase, outStream->TimeBase);

                FFmpegException.ThrowIfError(
                    AVFormat.av_interleaved_write_frame(_outputCtx, outPkt),
                    "Error writing encoded packet");
            }
        }
        finally
        {
            AVCodec.av_packet_free(&outPkt);
        }
    }

    /// <summary>
    /// Flushes the encoder for a given stream by sending a null frame, which tells
    /// the encoder to output any remaining buffered frames (e.g., B-frames).
    /// </summary>
    private void FlushEncoder(int inputIndex, int outputIndex)
    {
        AVCodecContext* encCtx = _encoderCtxs[inputIndex];
        EncodeAndWrite(encCtx, null, outputIndex);
    }

    /// <summary>
    /// Releases all FFmpeg resources: codec contexts, scalers, resamplers, format contexts,
    /// and StreamIOContext wrappers. Carefully handles the distinction between file-based
    /// and stream-based I/O to avoid double-freeing AVIO contexts.
    /// </summary>
    private void Cleanup()
    {
        // Free per-stream resources (decoders, encoders, scalers, resamplers)
        if (_decoderCtxs != null)
        {
            for (int i = 0; i < _nbInputStreams; i++)
            {
                if (_decoderCtxs[i] != null)
                {
                    AVCodecContext* ctx = _decoderCtxs[i];
                    AVCodec.avcodec_free_context(&ctx);
                }
                if (_encoderCtxs[i] != null)
                {
                    AVCodecContext* ctx = _encoderCtxs[i];
                    AVCodec.avcodec_free_context(&ctx);
                }
                if (_swsCtxs[i] != nint.Zero)
                {
                    SWScale.sws_freeContext(_swsCtxs[i]);
                }
                if (_swrCtxs[i] != nint.Zero)
                {
                    nint ctx = _swrCtxs[i];
                    SWResample.swr_free(&ctx);
                }
            }
            NativeMemory.Free(_decoderCtxs); _decoderCtxs = null;
            NativeMemory.Free(_encoderCtxs); _encoderCtxs = null;
            NativeMemory.Free(_swsCtxs); _swsCtxs = null;
            NativeMemory.Free(_swrCtxs); _swrCtxs = null;
        }

        if (_streamMap != null)
        {
            NativeMemory.Free(_streamMap);
            _streamMap = null;
        }

        // Free output context. Must handle file-based vs stream-based I/O differently:
        // - File-based: we opened the AVIO via avio_open, so we must close it via avio_closep.
        // - Stream-based: the StreamIOContext owns the pb; we just null it out to prevent double-free.
        if (_outputCtx != null)
        {
            if (_outputIO == null)
            {
                AVOutputFormat* oformat = (AVOutputFormat*)_outputCtx->Oformat;
                if (oformat != null && (_outputCtx->Pb != null) &&
                    (oformat->Flags & (int)AVFormatFlags.AVFMT_NOFILE) == 0)
                {
                    AVFormat.avio_closep(&_outputCtx->Pb);
                }
            }
            else
            {
                _outputCtx->Pb = null;
            }
            AVFormat.avformat_free_context(_outputCtx);
            _outputCtx = null;
        }

        // Free input context. For stream-based input, detach pb before close to prevent
        // avformat_close_input from freeing the StreamIOContext-owned AVIO.
        if (_inputCtx != null)
        {
            if (_inputIO != null)
            {
                _inputCtx->Pb = null;
            }
            fixed (AVFormatContext** p = &_inputCtx)
            {
                AVFormat.avformat_close_input(p);
            }
        }

        // Dispose StreamIOContext wrappers (frees GCHandle and AVIOContext)
        _inputIO?.Dispose();
        _inputIO = null;
        _outputIO?.Dispose();
        _outputIO = null;
    }

    /// <summary>
    /// Disposes the transcoder, releasing all native resources.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
    }
}
