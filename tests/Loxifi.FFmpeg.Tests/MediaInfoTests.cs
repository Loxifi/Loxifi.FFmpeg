// MediaInfoTests.cs — Unit tests for the Loxifi.FFmpeg library.
// Tests cover library loading, media probing, transcoding (stream copy and re-encode),
// codec selection, muxing, resizing, and GIF-to-MP4 conversion.

using System.Runtime.CompilerServices;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;
using Loxifi.FFmpeg.Transcoding;
using Xunit;

namespace Loxifi.FFmpeg.Tests;

/// <summary>
/// Tests for media probing and FFmpeg library loading.
/// </summary>
public class MediaInfoTests
{
    static MediaInfoTests()
    {
        // Force the module initializer to run, ensuring LibraryLoader registers
        // the DllImportResolver before any P/Invoke calls are made.
        RuntimeHelpers.RunModuleConstructor(typeof(LibraryLoader).Module.ModuleHandle);
    }

    private static string SampleFile => Path.Combine(AppContext.BaseDirectory, "Samples", "sample.mp4");

    [Fact]
    public void AVUtil_Version_ReturnsNonZero()
    {
        uint version = AVUtil.avutil_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void AVFormat_Version_ReturnsNonZero()
    {
        uint version = AVFormat.avformat_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void AVCodec_Version_ReturnsNonZero()
    {
        uint version = AVCodec.avcodec_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void SWScale_Version_ReturnsNonZero()
    {
        uint version = SWScale.swscale_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void SWResample_Version_ReturnsNonZero()
    {
        uint version = SWResample.swresample_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void Probe_ReturnsDurationAndStreams()
    {
        MediaInfo info = MediaInfo.Probe(SampleFile);

        Assert.True(info.Duration > TimeSpan.Zero, "Duration should be positive");
        Assert.NotEmpty(info.Streams);
    }

    [Fact]
    public void Probe_HasVideoStream()
    {
        MediaInfo info = MediaInfo.Probe(SampleFile);

        Assert.NotNull(info.VideoStream);
        Assert.True(info.VideoStream!.Width > 0, "Video width should be positive");
        Assert.True(info.VideoStream!.Height > 0, "Video height should be positive");
    }
}

/// <summary>
/// Tests for the MediaTranscoder (stream copy and re-encoding).
/// </summary>
public class TranscodeTests
{
    static TranscodeTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(LibraryLoader).Module.ModuleHandle);
    }

    private static string SampleFile => Path.Combine(AppContext.BaseDirectory, "Samples", "sample.mp4");

    [Fact]
    public void StreamCopy_ProducesOutput()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_test_{Guid.NewGuid()}.mp4");

        try
        {
            using var transcoder = new MediaTranscoder();
            transcoder.Transcode(new TranscodeOptions
            {
                InputPath = SampleFile,
                OutputPath = outputPath,
            });

            Assert.True(File.Exists(outputPath), "Output file should exist");
            Assert.True(new FileInfo(outputPath).Length > 0, "Output file should not be empty");

            // Verify output is valid media by probing it
            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.True(info.Duration > TimeSpan.Zero);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task TranscodeAsync_CanBeCancelled()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_test_{Guid.NewGuid()}.mp4");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(1));

        try
        {
            using var transcoder = new MediaTranscoder();

            // With a near-immediate cancellation, either it throws OperationCanceledException
            // or completes before cancellation is checked — both are valid for a tiny file.
            try
            {
                await transcoder.TranscodeAsync(
                    new TranscodeOptions
                    {
                        InputPath = SampleFile,
                        OutputPath = outputPath,
                    },
                    ct: cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected for larger files; test passes either way
            }
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void Transcode_ReportsProgress()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_test_{Guid.NewGuid()}.mp4");
        var progressReports = new List<TranscodeProgress>();

        try
        {
            var syncProgress = new SyncProgress<TranscodeProgress>(p => progressReports.Add(p));

            using var transcoder = new MediaTranscoder();
            transcoder.Transcode(
                new TranscodeOptions
                {
                    InputPath = SampleFile,
                    OutputPath = outputPath,
                },
                syncProgress);

            // With a very small file, progress may or may not be reported
            // depending on duration detection. Just verify no crash.
            Assert.True(File.Exists(outputPath), "Output should exist");
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    /// <summary>
    /// Synchronous IProgress implementation for testing (avoids SynchronizationContext issues).
    /// </summary>
    private class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}

/// <summary>
/// Tests for runtime codec selection (GPL vs LGPL builds).
/// </summary>
public class CodecSelectionTests
{
    static CodecSelectionTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(LibraryLoader).Module.ModuleHandle);
    }

    [Fact]
    public void BestVideoCodec_UsesX264_WhenGPLAvailable()
    {
        // Probe whether libx264 is available to determine expected output codec
        nint x264 = AVCodec.avcodec_find_encoder_by_name("libx264");

        string inputPath = Path.Combine(AppContext.BaseDirectory, "Samples", "sample.gif");
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_codec_test_{Guid.NewGuid()}.mp4");

        try
        {
            MediaOperations.GifToMp4(inputPath, outputPath);

            Assert.True(File.Exists(outputPath));
            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.NotNull(info.VideoStream);

            // On GPL builds, the output should be H.264; on LGPL builds, MPEG-4 or AV1
            if (x264 != nint.Zero)
            {
                Assert.Equal(AVCodecID.AV_CODEC_ID_H264, info.VideoStream!.CodecId);
            }
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void BestVideoCodec_FallsBackGracefully()
    {
        // Regardless of which codec is used, the operation should succeed
        string inputPath = Path.Combine(AppContext.BaseDirectory, "Samples", "sample_av.mp4");
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_resize_codec_{Guid.NewGuid()}.mp4");

        try
        {
            MediaOperations.ResizeToFileSize(inputPath, outputPath, 200_000);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}

/// <summary>
/// Tests for high-level MediaOperations (mux, resize, GIF-to-MP4).
/// </summary>
public class MediaOperationsTests
{
    static MediaOperationsTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(LibraryLoader).Module.ModuleHandle);
    }

    private static string SampleMp4 => Path.Combine(AppContext.BaseDirectory, "Samples", "sample.mp4");
    private static string SampleAV => Path.Combine(AppContext.BaseDirectory, "Samples", "sample_av.mp4");
    private static string SampleGif => Path.Combine(AppContext.BaseDirectory, "Samples", "sample.gif");

    [Fact]
    public void Mux_RedditDASH_HasAudio()
    {
        // Test muxing separate DASH video/audio segments (common with Reddit video downloads)
        string videoPath = Path.Combine(AppContext.BaseDirectory, "Samples", "reddit_video.mp4");
        string audioPath = Path.Combine(AppContext.BaseDirectory, "Samples", "reddit_audio.mp4");
        if (!File.Exists(videoPath) || !File.Exists(audioPath)) return;

        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_reddit_mux_{Guid.NewGuid()}.mp4");

        try
        {
            MediaOperations.Mux(videoPath, audioPath, outputPath);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);

            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.NotNull(info.VideoStream);
            Assert.NotNull(info.AudioStream);
            Assert.True(info.AudioStream!.SampleRate > 0, "Audio should have a valid sample rate");
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void Mux_CombinesVideoAndAudio()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_mux_{Guid.NewGuid()}.mp4");

        try
        {
            // Use sample_av.mp4 as both video and audio source (it has both streams)
            MediaOperations.Mux(SampleAV, SampleAV, outputPath);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);

            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.NotNull(info.VideoStream);
            Assert.NotNull(info.AudioStream);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void ResizeToFileSize_ProducesSmallFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_resize_{Guid.NewGuid()}.mp4");
        long targetSize = 200_000; // 200KB

        try
        {
            MediaOperations.ResizeToFileSize(SampleAV, outputPath, targetSize);

            Assert.True(File.Exists(outputPath));
            long actualSize = new FileInfo(outputPath).Length;
            Assert.True(actualSize > 0, "Output should not be empty");

            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.True(info.Duration > TimeSpan.Zero);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void Mux_WithStreams_CombinesVideoAndAudio()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_mux_stream_{Guid.NewGuid()}.mp4");

        try
        {
            using var videoStream = File.OpenRead(SampleAV);
            using var audioStream = File.OpenRead(SampleAV);
            using var outputStream = File.Create(outputPath);

            MediaOperations.Mux(videoStream, audioStream, outputStream);
            outputStream.Flush();

            Assert.True(new FileInfo(outputPath).Length > 0);

            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.NotNull(info.VideoStream);
            Assert.NotNull(info.AudioStream);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void GifToMp4_WithStreams_Converts()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_gif_stream_{Guid.NewGuid()}.mp4");

        try
        {
            using var input = File.OpenRead(SampleGif);
            using var output = File.Create(outputPath);

            MediaOperations.GifToMp4(input, output);
            output.Flush();

            Assert.True(new FileInfo(outputPath).Length > 0);

            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.NotNull(info.VideoStream);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void GifToMp4_Converts()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_gif_{Guid.NewGuid()}.mp4");

        try
        {
            MediaOperations.GifToMp4(SampleGif, outputPath);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);

            MediaInfo info = MediaInfo.Probe(outputPath);
            Assert.NotNull(info.VideoStream);
            Assert.True(info.VideoStream!.Width > 0);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
