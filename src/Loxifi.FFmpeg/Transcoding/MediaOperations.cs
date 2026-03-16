// MediaOperations.cs — High-level media operations built on top of MediaTranscoder.
// Provides convenient methods for common tasks: muxing separate audio/video into one file,
// resizing video to a target file size, and converting GIF to MP4. All methods support
// both file paths and .NET Streams.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Helpers;
using Loxifi.FFmpeg.Transcoding.Codecs;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Provides high-level media operations such as muxing, resizing, and format conversion.
/// All methods automatically select the best available codec at runtime based on which
/// FFmpeg encoders are compiled into the loaded libraries (GPL vs LGPL builds).
/// </summary>
public static unsafe class MediaOperations
{
    /// <summary>
    /// Muxes a video file and an audio file into a single output file.
    /// Both streams are copied without re-encoding (fast, lossless).
    /// Typical use case: combining DASH video-only and audio-only segments (e.g., Reddit video downloads).
    /// </summary>
    /// <param name="videoPath">Path to the video-only input file.</param>
    /// <param name="audioPath">Path to the audio-only input file.</param>
    /// <param name="outputPath">Path for the combined output file.</param>
    /// <param name="outputFormat">Optional container format override; if null, inferred from output file extension.</param>
    /// <param name="ct">Cancellation token checked between each packet.</param>
    public static void Mux(string videoPath, string audioPath, string outputPath,
        ContainerFormat? outputFormat = null, CancellationToken ct = default)
    {
        AVFormatContext* videoCtx = null;
        AVFormatContext* audioCtx = null;
        AVFormatContext* outputCtx = null;

        try
        {
            OpenInput(videoPath, &videoCtx);
            OpenInput(audioPath, &audioCtx);

            // Find the best video and audio streams from each input
            int videoStreamIdx = AVFormat.av_find_best_stream(videoCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (videoStreamIdx < 0) throw new FFmpegException(videoStreamIdx, "No video stream found");

            int audioStreamIdx = AVFormat.av_find_best_stream(audioCtx, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            if (audioStreamIdx < 0) throw new FFmpegException(audioStreamIdx, "No audio stream found");

            AVStream* inVideoStream = videoCtx->Streams[videoStreamIdx];
            AVStream* inAudioStream = audioCtx->Streams[audioStreamIdx];

            AllocOutput(outputPath, outputFormat, &outputCtx);

            // Create output streams and copy codec parameters (stream copy, no re-encoding)
            AVStream* outVideoStream = AVFormat.avformat_new_stream(outputCtx, nint.Zero);
            if (outVideoStream == null) throw new FFmpegException(-1, "Failed to create video output stream");
            FFmpegException.ThrowIfError(
                AVCodec.avcodec_parameters_copy(outVideoStream->Codecpar, inVideoStream->Codecpar),
                "Failed to copy video parameters");
            outVideoStream->Codecpar->CodecTag = 0;

            AVStream* outAudioStream = AVFormat.avformat_new_stream(outputCtx, nint.Zero);
            if (outAudioStream == null) throw new FFmpegException(-1, "Failed to create audio output stream");
            FFmpegException.ThrowIfError(
                AVCodec.avcodec_parameters_copy(outAudioStream->Codecpar, inAudioStream->Codecpar),
                "Failed to copy audio parameters");
            outAudioStream->Codecpar->CodecTag = 0;

            OpenOutputFile(outputCtx, outputPath);
            FFmpegException.ThrowIfError(
                AVFormat.avformat_write_header(outputCtx, null),
                "Failed to write header");

            // Copy packets from both inputs to the output. Video packets are written first,
            // then audio. av_interleaved_write_frame handles proper interleaving in the output.
            AVPacket* packet = AVCodec.av_packet_alloc();
            try
            {
                // Write all video packets (stream index 0 in output)
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int ret = AVFormat.av_read_frame(videoCtx, packet);
                    if (ret == AVErrors.AVERROR_EOF) break;
                    FFmpegException.ThrowIfError(ret, "Error reading video frame");

                    if (packet->StreamIndex == videoStreamIdx)
                    {
                        packet->StreamIndex = 0;
                        AVCodec.av_packet_rescale_ts(packet, inVideoStream->TimeBase, outVideoStream->TimeBase);
                        packet->Pos = -1;
                        AVFormat.av_interleaved_write_frame(outputCtx, packet);
                    }
                    AVCodec.av_packet_unref(packet);
                }

                // Write all audio packets (stream index 1 in output)
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int ret = AVFormat.av_read_frame(audioCtx, packet);
                    if (ret == AVErrors.AVERROR_EOF) break;
                    FFmpegException.ThrowIfError(ret, "Error reading audio frame");

                    if (packet->StreamIndex == audioStreamIdx)
                    {
                        packet->StreamIndex = 1;
                        AVCodec.av_packet_rescale_ts(packet, inAudioStream->TimeBase, outAudioStream->TimeBase);
                        packet->Pos = -1;
                        AVFormat.av_interleaved_write_frame(outputCtx, packet);
                    }
                    AVCodec.av_packet_unref(packet);
                }
            }
            finally
            {
                AVCodec.av_packet_free(&packet);
            }

            FFmpegException.ThrowIfError(
                AVFormat.av_write_trailer(outputCtx),
                "Error writing trailer");
        }
        finally
        {
            CloseInput(&videoCtx);
            CloseInput(&audioCtx);
            CloseOutput(&outputCtx);
        }
    }

    /// <summary>
    /// Muxes video and audio files asynchronously on a thread pool thread.
    /// </summary>
    /// <inheritdoc cref="Mux(string, string, string, ContainerFormat?, CancellationToken)"/>
    public static Task MuxAsync(string videoPath, string audioPath, string outputPath,
        ContainerFormat? outputFormat = null, CancellationToken ct = default)
    {
        return Task.Run(() => Mux(videoPath, audioPath, outputPath, outputFormat, ct), ct);
    }

    /// <summary>
    /// Re-encodes a video to fit within a target file size in bytes.
    /// Calculates the required video bitrate from the target size and input duration,
    /// reserving 128kbps for the audio stream.
    /// </summary>
    /// <param name="inputPath">Path to the input video file.</param>
    /// <param name="outputPath">Path for the output video file.</param>
    /// <param name="targetSizeBytes">Target output file size in bytes.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public static void ResizeToFileSize(string inputPath, string outputPath, long targetSizeBytes,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        MediaInfo info = MediaInfo.Probe(inputPath);
        double durationSeconds = info.Duration.TotalSeconds;
        if (durationSeconds <= 0) throw new ArgumentException("Cannot determine input duration");

        // Calculate bitrate budget: target_size_bits / duration_seconds = total_bitrate.
        // Reserve 128kbps for audio, allocate the rest to video. Floor at 64kbps minimum.
        long audioBitRate = 128_000;
        long totalBitRate = (long)(targetSizeBytes * 8 / durationSeconds);
        long videoBitRate = Math.Max(totalBitRate - audioBitRate, 64_000);

        using var transcoder = new MediaTranscoder();
        transcoder.Transcode(new TranscodeOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            VideoCodec = BestVideoCodec,
            AudioCodec = BestAudioCodec,
            VideoBitRate = videoBitRate,
            AudioBitRate = audioBitRate,
        }, progress, ct);
    }

    /// <summary>
    /// Re-encodes a video to fit within a target file size asynchronously.
    /// </summary>
    /// <inheritdoc cref="ResizeToFileSize(string, string, long, IProgress{TranscodeProgress}?, CancellationToken)"/>
    public static Task ResizeToFileSizeAsync(string inputPath, string outputPath, long targetSizeBytes,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        return Task.Run(() => ResizeToFileSize(inputPath, outputPath, targetSizeBytes, progress, ct), ct);
    }

    /// <summary>
    /// Re-encodes a video stream to fit within a target file size.
    /// The input stream must be seekable because it is first probed for duration
    /// (by copying to a temporary file), then seeked back to position 0 for transcoding.
    /// </summary>
    /// <param name="input">Seekable input stream containing the source video.</param>
    /// <param name="output">Output stream for the re-encoded video.</param>
    /// <param name="targetSizeBytes">Target output size in bytes.</param>
    /// <param name="outputFormat">Output container format (default: MP4).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public static void ResizeToFileSize(Stream input, Stream output, long targetSizeBytes,
        ContainerFormat outputFormat = ContainerFormat.Mp4,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        if (!input.CanSeek) throw new ArgumentException("Input stream must be seekable to probe duration", nameof(input));

        // MediaInfo.Probe requires a file path, so we copy the stream to a temp file
        // for probing, then seek the original stream back to the beginning for transcoding.
        string tempInput = Path.Combine(Path.GetTempPath(), $"ffmpeg_probe_{Guid.NewGuid()}.tmp");
        try
        {
            using (var tempFile = File.Create(tempInput))
            {
                input.CopyTo(tempFile);
            }
            input.Position = 0;

            MediaInfo info = MediaInfo.Probe(tempInput);
            double durationSeconds = info.Duration.TotalSeconds;
            if (durationSeconds <= 0) throw new ArgumentException("Cannot determine input duration");

            long audioBitRate = 128_000;
            long totalBitRate = (long)(targetSizeBytes * 8 / durationSeconds);
            long videoBitRate = Math.Max(totalBitRate - audioBitRate, 64_000);

            using var transcoder = new MediaTranscoder();
            transcoder.Transcode(new StreamTranscodeOptions
            {
                InputStream = input,
                OutputStream = output,
                OutputFormat = outputFormat,
                VideoCodec = BestVideoCodec,
                AudioCodec = Codecs.LGPL.Audio.Aac,
                VideoBitRate = videoBitRate,
                AudioBitRate = audioBitRate,
            }, progress, ct);
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
        }
    }

    /// <summary>
    /// Re-encodes a video stream to fit within a target file size asynchronously.
    /// </summary>
    /// <inheritdoc cref="ResizeToFileSize(Stream, Stream, long, ContainerFormat, IProgress{TranscodeProgress}?, CancellationToken)"/>
    public static Task ResizeToFileSizeAsync(Stream input, Stream output, long targetSizeBytes,
        ContainerFormat outputFormat = ContainerFormat.Mp4,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        return Task.Run(() => ResizeToFileSize(input, output, targetSizeBytes, outputFormat, progress, ct), ct);
    }

    /// <summary>
    /// Converts a GIF file to an MP4 video (H.264 or best available codec, no audio).
    /// GIF frames are decoded and re-encoded because GIF uses a palette-based pixel format
    /// (pal8) that is incompatible with video containers without re-encoding.
    /// </summary>
    /// <param name="inputPath">Path to the input GIF file.</param>
    /// <param name="outputPath">Path for the output MP4 file.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public static void GifToMp4(string inputPath, string outputPath,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        using var transcoder = new MediaTranscoder();
        transcoder.Transcode(new TranscodeOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            OutputFormat = ContainerFormat.Mp4,
            VideoCodec = BestVideoCodec,
        }, progress, ct);
    }

    /// <summary>
    /// Converts a GIF file to MP4 asynchronously.
    /// </summary>
    /// <inheritdoc cref="GifToMp4(string, string, IProgress{TranscodeProgress}?, CancellationToken)"/>
    public static Task GifToMp4Async(string inputPath, string outputPath,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        return Task.Run(() => GifToMp4(inputPath, outputPath, progress, ct), ct);
    }

    /// <summary>
    /// Converts a GIF stream to an MP4 stream.
    /// </summary>
    /// <param name="input">Input stream containing GIF data.</param>
    /// <param name="output">Output stream for the MP4 result.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public static void GifToMp4(Stream input, Stream output,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        using var transcoder = new MediaTranscoder();
        transcoder.Transcode(new StreamTranscodeOptions
        {
            InputStream = input,
            OutputStream = output,
            OutputFormat = ContainerFormat.Mp4,
            VideoCodec = BestVideoCodec,
        }, progress, ct);
    }

    /// <summary>
    /// Converts a GIF stream to an MP4 stream asynchronously.
    /// </summary>
    /// <inheritdoc cref="GifToMp4(Stream, Stream, IProgress{TranscodeProgress}?, CancellationToken)"/>
    public static Task GifToMp4Async(Stream input, Stream output,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        return Task.Run(() => GifToMp4(input, output, progress, ct), ct);
    }

    /// <summary>
    /// Muxes a video stream and an audio stream into an output stream without re-encoding.
    /// Uses custom AVIOContexts backed by the .NET streams.
    /// </summary>
    /// <param name="videoInput">Input stream containing the video.</param>
    /// <param name="audioInput">Input stream containing the audio.</param>
    /// <param name="output">Output stream for the muxed result.</param>
    /// <param name="outputFormat">Output container format (default: MP4).</param>
    /// <param name="ct">Cancellation token.</param>
    public static void Mux(Stream videoInput, Stream audioInput, Stream output,
        ContainerFormat outputFormat = ContainerFormat.Mp4, CancellationToken ct = default)
    {
        AVFormatContext* videoCtx = null;
        AVFormatContext* audioCtx = null;
        AVFormatContext* outputCtx = null;

        using var videoIO = StreamIOContext.ForReading(videoInput);
        using var audioIO = StreamIOContext.ForReading(audioInput);
        using var outputIO = StreamIOContext.ForWriting(output);

        try
        {
            OpenInputStream(videoIO, &videoCtx);
            OpenInputStream(audioIO, &audioCtx);

            int videoStreamIdx = AVFormat.av_find_best_stream(videoCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (videoStreamIdx < 0) throw new FFmpegException(videoStreamIdx, "No video stream found");

            int audioStreamIdx = AVFormat.av_find_best_stream(audioCtx, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            if (audioStreamIdx < 0) throw new FFmpegException(audioStreamIdx, "No audio stream found");

            AVStream* inVideoStream = videoCtx->Streams[videoStreamIdx];
            AVStream* inAudioStream = audioCtx->Streams[audioStreamIdx];

            AllocOutputForStream(outputFormat, &outputCtx);
            outputCtx->Pb = outputIO.Context;

            AVStream* outVideoStream = AVFormat.avformat_new_stream(outputCtx, nint.Zero);
            if (outVideoStream == null) throw new FFmpegException(-1, "Failed to create video output stream");
            FFmpegException.ThrowIfError(
                AVCodec.avcodec_parameters_copy(outVideoStream->Codecpar, inVideoStream->Codecpar),
                "Failed to copy video parameters");
            outVideoStream->Codecpar->CodecTag = 0;

            AVStream* outAudioStream = AVFormat.avformat_new_stream(outputCtx, nint.Zero);
            if (outAudioStream == null) throw new FFmpegException(-1, "Failed to create audio output stream");
            FFmpegException.ThrowIfError(
                AVCodec.avcodec_parameters_copy(outAudioStream->Codecpar, inAudioStream->Codecpar),
                "Failed to copy audio parameters");
            outAudioStream->Codecpar->CodecTag = 0;

            FFmpegException.ThrowIfError(
                AVFormat.avformat_write_header(outputCtx, null),
                "Failed to write header");

            AVPacket* packet = AVCodec.av_packet_alloc();
            try
            {
                // Write video packets (output stream index 0)
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int ret = AVFormat.av_read_frame(videoCtx, packet);
                    if (ret == AVErrors.AVERROR_EOF) break;
                    FFmpegException.ThrowIfError(ret, "Error reading video frame");

                    if (packet->StreamIndex == videoStreamIdx)
                    {
                        packet->StreamIndex = 0;
                        AVCodec.av_packet_rescale_ts(packet, inVideoStream->TimeBase, outVideoStream->TimeBase);
                        packet->Pos = -1;
                        AVFormat.av_interleaved_write_frame(outputCtx, packet);
                    }
                    AVCodec.av_packet_unref(packet);
                }

                // Write audio packets (output stream index 1)
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int ret = AVFormat.av_read_frame(audioCtx, packet);
                    if (ret == AVErrors.AVERROR_EOF) break;
                    FFmpegException.ThrowIfError(ret, "Error reading audio frame");

                    if (packet->StreamIndex == audioStreamIdx)
                    {
                        packet->StreamIndex = 1;
                        AVCodec.av_packet_rescale_ts(packet, inAudioStream->TimeBase, outAudioStream->TimeBase);
                        packet->Pos = -1;
                        AVFormat.av_interleaved_write_frame(outputCtx, packet);
                    }
                    AVCodec.av_packet_unref(packet);
                }
            }
            finally
            {
                AVCodec.av_packet_free(&packet);
            }

            FFmpegException.ThrowIfError(
                AVFormat.av_write_trailer(outputCtx),
                "Error writing trailer");
        }
        finally
        {
            CloseInput(&videoCtx);
            CloseInput(&audioCtx);
            // Don't call avio_closep — the StreamIOContext owns the pb and will free it on Dispose
            if (outputCtx != null)
            {
                outputCtx->Pb = null;
                AVFormat.avformat_free_context(outputCtx);
            }
        }
    }

    /// <summary>
    /// Muxes video and audio streams asynchronously on a thread pool thread.
    /// </summary>
    /// <inheritdoc cref="Mux(Stream, Stream, Stream, ContainerFormat, CancellationToken)"/>
    public static Task MuxAsync(Stream videoInput, Stream audioInput, Stream output,
        ContainerFormat outputFormat = ContainerFormat.Mp4, CancellationToken ct = default)
    {
        return Task.Run(() => Mux(videoInput, audioInput, output, outputFormat, ct), ct);
    }

    // ── Codec selection ──
    // These properties probe the loaded FFmpeg libraries at runtime to determine which
    // encoders are available. GPL builds include libx264 (best H.264 encoder), while
    // LGPL-only builds fall back to SVT-AV1 or the built-in mpeg4 encoder.

    /// <summary>
    /// Selects the best available video encoder at runtime by probing the loaded FFmpeg libraries.
    /// Preference order: libx264 (GPL, best quality/speed) > libsvtav1 (LGPL, AV1) > mpeg4 (fallback).
    /// </summary>
    private static VideoCodec BestVideoCodec
    {
        get
        {
            if (AVCodec.avcodec_find_encoder_by_name("libx264") != nint.Zero)
                return GPL.Video.X264;
            if (AVCodec.avcodec_find_encoder_by_name("libsvtav1") != nint.Zero)
                return LGPL.Video.SvtAv1;
            return LGPL.Video.Mpeg4;
        }
    }

    /// <summary>
    /// Selects the best available audio encoder. AAC is universally available (built-in to FFmpeg)
    /// and compatible with all common containers.
    /// </summary>
    private static AudioCodec BestAudioCodec
    {
        get
        {
            return LGPL.Audio.Aac;
        }
    }

    // ── Helper methods for format context lifecycle ──

    /// <summary>
    /// Opens a .NET stream as an FFmpeg input using a custom AVIOContext.
    /// </summary>
    private static void OpenInputStream(StreamIOContext io, AVFormatContext** ctx)
    {
        *ctx = AVFormat.avformat_alloc_context();
        if (*ctx == null) throw new FFmpegException(-1, "Failed to allocate format context");

        (*ctx)->Pb = io.Context;

        FFmpegException.ThrowIfError(
            AVFormat.avformat_open_input(ctx, null, nint.Zero, null),
            "Failed to open input stream");

        FFmpegException.ThrowIfError(
            AVFormat.avformat_find_stream_info(*ctx, null),
            "Failed to find stream info");
    }

    /// <summary>
    /// Allocates an output format context for stream-based output with an explicit format name.
    /// </summary>
    private static void AllocOutputForStream(ContainerFormat format, AVFormatContext** ctx)
    {
        nint fmtPtr = Marshal.StringToHGlobalAnsi(format.ToFFmpegName());
        try
        {
            FFmpegException.ThrowIfError(
                AVFormat.avformat_alloc_output_context2(ctx, nint.Zero, (byte*)fmtPtr, null),
                "Failed to allocate output context");
        }
        finally
        {
            Marshal.FreeHGlobal(fmtPtr);
        }
    }

    /// <summary>
    /// Opens a file path as an FFmpeg input, probing stream info.
    /// </summary>
    private static void OpenInput(string path, AVFormatContext** ctx)
    {
        nint urlPtr = Marshal.StringToHGlobalAnsi(path);
        try
        {
            FFmpegException.ThrowIfError(
                AVFormat.avformat_open_input(ctx, (byte*)urlPtr, nint.Zero, null),
                $"Failed to open input '{path}'");
        }
        finally
        {
            Marshal.FreeHGlobal(urlPtr);
        }

        FFmpegException.ThrowIfError(
            AVFormat.avformat_find_stream_info(*ctx, null),
            "Failed to find stream info");
    }

    /// <summary>
    /// Allocates an output format context for file-based output, optionally with an explicit format.
    /// </summary>
    private static void AllocOutput(string path, ContainerFormat? format, AVFormatContext** ctx)
    {
        nint fmtPtr = format is not null ? Marshal.StringToHGlobalAnsi(format.Value.ToFFmpegName()) : nint.Zero;
        nint pathPtr = Marshal.StringToHGlobalAnsi(path);
        try
        {
            FFmpegException.ThrowIfError(
                AVFormat.avformat_alloc_output_context2(
                    ctx, nint.Zero,
                    fmtPtr != nint.Zero ? (byte*)fmtPtr : null,
                    (byte*)pathPtr),
                "Failed to allocate output context");
        }
        finally
        {
            if (fmtPtr != nint.Zero) Marshal.FreeHGlobal(fmtPtr);
            Marshal.FreeHGlobal(pathPtr);
        }
    }

    /// <summary>
    /// Opens the output file for writing via avio_open, unless the format specifies AVFMT_NOFILE.
    /// </summary>
    private static void OpenOutputFile(AVFormatContext* ctx, string path)
    {
        AVOutputFormat* oformat = (AVOutputFormat*)ctx->Oformat;
        if ((oformat->Flags & (int)AVFormatFlags.AVFMT_NOFILE) == 0)
        {
            nint pathPtr = Marshal.StringToHGlobalAnsi(path);
            try
            {
                FFmpegException.ThrowIfError(
                    AVFormat.avio_open(&ctx->Pb, (byte*)pathPtr, (int)AVIOFlags.AVIO_FLAG_WRITE),
                    "Failed to open output file");
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }
        }
    }

    /// <summary>
    /// Closes an input format context, freeing all associated resources.
    /// </summary>
    private static void CloseInput(AVFormatContext** ctx)
    {
        if (*ctx != null)
        {
            AVFormat.avformat_close_input(ctx);
        }
    }

    /// <summary>
    /// Closes an output format context, closing the AVIO file handle and freeing resources.
    /// </summary>
    private static void CloseOutput(AVFormatContext** ctx)
    {
        if (*ctx != null)
        {
            AVOutputFormat* oformat = (AVOutputFormat*)(*ctx)->Oformat;
            if (oformat != null && (*ctx)->Pb != null &&
                (oformat->Flags & (int)AVFormatFlags.AVFMT_NOFILE) == 0)
            {
                AVFormat.avio_closep(&(*ctx)->Pb);
            }
            AVFormat.avformat_free_context(*ctx);
            *ctx = null;
        }
    }
}
