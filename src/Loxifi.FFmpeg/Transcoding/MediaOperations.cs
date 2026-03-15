using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Helpers;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Transcoding;

public static unsafe class MediaOperations
{
    /// <summary>
    /// Mux a video file and an audio file into a single output file.
    /// Both streams are copied without re-encoding (fast).
    /// Typical use: combining DASH video-only and audio-only segments.
    /// </summary>
    public static void Mux(string videoPath, string audioPath, string outputPath,
        string? outputFormat = null, CancellationToken ct = default)
    {
        AVFormatContext* videoCtx = null;
        AVFormatContext* audioCtx = null;
        AVFormatContext* outputCtx = null;

        try
        {
            // Open video input
            OpenInput(videoPath, &videoCtx);
            // Open audio input
            OpenInput(audioPath, &audioCtx);

            // Find best streams
            int videoStreamIdx = AVFormat.av_find_best_stream(videoCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (videoStreamIdx < 0) throw new FFmpegException(videoStreamIdx, "No video stream found");

            int audioStreamIdx = AVFormat.av_find_best_stream(audioCtx, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            if (audioStreamIdx < 0) throw new FFmpegException(audioStreamIdx, "No audio stream found");

            AVStream* inVideoStream = videoCtx->Streams[videoStreamIdx];
            AVStream* inAudioStream = audioCtx->Streams[audioStreamIdx];

            // Create output
            AllocOutput(outputPath, outputFormat, &outputCtx);

            // Add video stream
            AVStream* outVideoStream = AVFormat.avformat_new_stream(outputCtx, nint.Zero);
            if (outVideoStream == null) throw new FFmpegException(-1, "Failed to create video output stream");
            FFmpegException.ThrowIfError(
                AVCodec.avcodec_parameters_copy(outVideoStream->Codecpar, inVideoStream->Codecpar),
                "Failed to copy video parameters");
            outVideoStream->Codecpar->CodecTag = 0;

            // Add audio stream
            AVStream* outAudioStream = AVFormat.avformat_new_stream(outputCtx, nint.Zero);
            if (outAudioStream == null) throw new FFmpegException(-1, "Failed to create audio output stream");
            FFmpegException.ThrowIfError(
                AVCodec.avcodec_parameters_copy(outAudioStream->Codecpar, inAudioStream->Codecpar),
                "Failed to copy audio parameters");
            outAudioStream->Codecpar->CodecTag = 0;

            // Open output file and write header
            OpenOutputFile(outputCtx, outputPath);
            FFmpegException.ThrowIfError(
                AVFormat.avformat_write_header(outputCtx, null),
                "Failed to write header");

            // Interleave packets from both inputs
            AVPacket* packet = AVCodec.av_packet_alloc();
            try
            {
                // Write video packets
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int ret = AVFormat.av_read_frame(videoCtx, packet);
                    if (ret == AVErrors.AVERROR_EOF) break;
                    FFmpegException.ThrowIfError(ret, "Error reading video frame");

                    if (packet->StreamIndex == videoStreamIdx)
                    {
                        packet->StreamIndex = 0; // video is output stream 0
                        AVCodec.av_packet_rescale_ts(packet, inVideoStream->TimeBase, outVideoStream->TimeBase);
                        packet->Pos = -1;
                        AVFormat.av_interleaved_write_frame(outputCtx, packet);
                    }
                    AVCodec.av_packet_unref(packet);
                }

                // Write audio packets
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int ret = AVFormat.av_read_frame(audioCtx, packet);
                    if (ret == AVErrors.AVERROR_EOF) break;
                    FFmpegException.ThrowIfError(ret, "Error reading audio frame");

                    if (packet->StreamIndex == audioStreamIdx)
                    {
                        packet->StreamIndex = 1; // audio is output stream 1
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
    /// Mux video and audio asynchronously.
    /// </summary>
    public static Task MuxAsync(string videoPath, string audioPath, string outputPath,
        string? outputFormat = null, CancellationToken ct = default)
    {
        return Task.Run(() => Mux(videoPath, audioPath, outputPath, outputFormat, ct), ct);
    }

    /// <summary>
    /// Resize a video to fit within the target file size (in bytes).
    /// Re-encodes video with H.264 and audio with AAC.
    /// The video bitrate is calculated from the target size and duration.
    /// </summary>
    public static void ResizeToFileSize(string inputPath, string outputPath, long targetSizeBytes,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        MediaInfo info = MediaInfo.Probe(inputPath);
        double durationSeconds = info.Duration.TotalSeconds;
        if (durationSeconds <= 0) throw new ArgumentException("Cannot determine input duration");

        // Reserve 128kbps for audio, rest for video
        long audioBitRate = 128_000;
        long totalBitRate = (long)(targetSizeBytes * 8 / durationSeconds);
        long videoBitRate = Math.Max(totalBitRate - audioBitRate, 64_000);

        using var transcoder = new MediaTranscoder();
        transcoder.Transcode(new TranscodeOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            VideoCodec = "mpeg4",
            AudioCodec = "aac",
            VideoBitRate = videoBitRate,
            AudioBitRate = audioBitRate,
        }, progress, ct);
    }

    /// <summary>
    /// Resize a video to fit within the target file size asynchronously.
    /// </summary>
    public static Task ResizeToFileSizeAsync(string inputPath, string outputPath, long targetSizeBytes,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        return Task.Run(() => ResizeToFileSize(inputPath, outputPath, targetSizeBytes, progress, ct), ct);
    }

    /// <summary>
    /// Convert a GIF to MP4 (H.264 video, no audio).
    /// </summary>
    public static void GifToMp4(string inputPath, string outputPath,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        using var transcoder = new MediaTranscoder();
        transcoder.Transcode(new TranscodeOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            OutputFormat = "mp4",
            VideoCodec = "mpeg4",
        }, progress, ct);
    }

    /// <summary>
    /// Convert a GIF to MP4 asynchronously.
    /// </summary>
    public static Task GifToMp4Async(string inputPath, string outputPath,
        IProgress<TranscodeProgress>? progress = null, CancellationToken ct = default)
    {
        return Task.Run(() => GifToMp4(inputPath, outputPath, progress, ct), ct);
    }

    // ── Helpers ──

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

    private static void AllocOutput(string path, string? format, AVFormatContext** ctx)
    {
        nint fmtPtr = format is not null ? Marshal.StringToHGlobalAnsi(format) : nint.Zero;
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

    private static void CloseInput(AVFormatContext** ctx)
    {
        if (*ctx != null)
        {
            AVFormat.avformat_close_input(ctx);
        }
    }

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
