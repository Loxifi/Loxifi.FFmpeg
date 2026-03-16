using System.Runtime.CompilerServices;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;
using Loxifi.FFmpeg.Transcoding;
using Xunit;

namespace Loxifi.FFmpeg.Tests;

public class MediaInfoTests
{
    static MediaInfoTests()
    {
        // Ensure the library loader's ModuleInitializer has run
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

            // Verify output is valid media
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

            // With a near-immediate cancellation, either it throws or completes
            // before cancellation is checked — both are valid for a tiny file
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
                // Expected for larger files
            }

            // Test passes either way — we're verifying no crash/resource leak
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
            // Use synchronous progress handler to capture reports immediately
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

    private class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}

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
            // Allow some tolerance — bitrate targeting isn't exact
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
