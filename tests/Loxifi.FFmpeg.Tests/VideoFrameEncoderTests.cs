// VideoFrameEncoderTests.cs — Tests for encoding a video from frames held in memory.
// Every test here produces a real file and probes it back, because "it did not throw" says nothing
// about whether a muxer wrote something a player can open.

using System.Runtime.CompilerServices;
using Loxifi.FFmpeg.Helpers;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;
using Loxifi.FFmpeg.Transcoding;
using Loxifi.FFmpeg.Transcoding.Codecs;
using Xunit;

namespace Loxifi.FFmpeg.Tests;

/// <summary>
/// Tests for <see cref="VideoFrameEncoder"/>.
///
/// <para>These use MPEG-4 Part 2 rather than H.264 for everything that is not specifically about H.264:
/// <c>mpeg4</c> is built into FFmpeg itself, so it is present in the LGPL runtime the test project
/// references by default. libx264 needs <c>-p:UseGPL=true</c>, and the one test that requires it skips
/// itself when the encoder is absent rather than failing on a build that never claimed to have it.</para>
/// </summary>
public class VideoFrameEncoderTests
{
    static VideoFrameEncoderTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(LibraryLoader).Module.ModuleHandle);
    }

    /// <summary>An encoder present in every build, so the tests exercise the class rather than the packaging.</summary>
    private static VideoCodec PortableCodec => LGPL.Video.Mpeg4;

    private static bool HasEncoder(VideoCodec codec) =>
        AVCodec.avcodec_find_encoder_by_name(codec.Name) != nint.Zero;

    /// <summary>
    /// Set <c>LOXIFI_REQUIRE_GPL=1</c> to turn "this build has no libx264, so skip" into a failure.
    ///
    /// <para>Without it there is no way to tell a GPL test that ran from one that quietly returned —
    /// both report as passed, and a run of ten green tests can contain zero H.264 encodes. CI sets it on
    /// the GPL leg so the codec the library exists to reach is actually exercised.</para>
    /// </summary>
    private static bool GplRequired =>
        Environment.GetEnvironmentVariable("LOXIFI_REQUIRE_GPL") is "1" or "true";

    /// <summary>True if the GPL test body should run; throws instead of skipping when GPL was demanded.</summary>
    private static bool ShouldRunGpl()
    {
        if (HasEncoder(GPL.Video.X264)) return true;
        Assert.False(GplRequired,
            "LOXIFI_REQUIRE_GPL is set but libx264 is not in the loaded FFmpeg build — " +
            "the test project was not built with -p:UseGPL=true.");
        return false;
    }

    /// <summary>A frame whose colour varies with the index, so encoded output is not a constant image.</summary>
    private static byte[] Frame(int width, int height, int index)
    {
        var buffer = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            buffer[i * 4 + 0] = (byte)((i + index * 7) % 256);   // R
            buffer[i * 4 + 1] = (byte)((index * 13) % 256);      // G
            buffer[i * 4 + 2] = (byte)((i / Math.Max(1, width)) % 256); // B
            buffer[i * 4 + 3] = 255;                             // A
        }
        return buffer;
    }

    private static string EncodeToFile(FrameEncodeOptions options, int frames)
    {
        string path = Path.Combine(Path.GetTempPath(), $"loxifi-frameenc-{Guid.NewGuid():N}.mp4");
        using (var file = File.Create(path))
        using (var encoder = new VideoFrameEncoder(file, options))
        {
            for (int i = 0; i < frames; i++)
                encoder.WriteFrame(Frame(options.Width, options.Height, i));
            encoder.Complete();
        }
        return path;
    }

    [Fact]
    public void Encodes_frames_into_a_file_that_probes_back_correctly()
    {
        string path = EncodeToFile(new FrameEncodeOptions
        {
            Width = 64,
            Height = 48,
            FrameRate = 24,
            VideoCodec = PortableCodec,
        }, frames: 48);

        try
        {
            Assert.True(new FileInfo(path).Length > 0, "encoder produced an empty file");

            MediaInfo info = MediaInfo.Probe(path);
            StreamInfo? video = info.VideoStream;
            Assert.NotNull(video);
            Assert.Equal(64, video!.Width);
            Assert.Equal(48, video.Height);

            // 48 frames at 24fps is two seconds. Containers round, so allow a frame either way.
            Assert.InRange(info.Duration.TotalSeconds, 1.9, 2.1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The reason <see cref="FrameEncodeOptions.Fragmented"/> exists. A normal MP4 muxer seeks back at
    /// the end to write the <c>moov</c> atom; against a stream that cannot seek it either throws or
    /// silently produces a file no player will open. Fragmented output must never seek.
    /// </summary>
    [Fact]
    public void Fragmented_output_never_seeks_the_destination()
    {
        using var destination = new NonSeekableStream();

        using (var encoder = new VideoFrameEncoder(destination, new FrameEncodeOptions
        {
            Width = 32,
            Height = 32,
            FrameRate = 12,
            VideoCodec = PortableCodec,
            Fragmented = true,
        }))
        {
            for (int i = 0; i < 12; i++) encoder.WriteFrame(Frame(32, 32, i));
            encoder.Complete();
        }

        Assert.True(destination.Written > 0, "nothing was written");
        Assert.False(destination.SeekAttempted, "the muxer seeked despite Fragmented = true");
    }

    /// <summary>
    /// YUV420P halves chroma in both directions, so an odd dimension cannot be represented and encoders
    /// refuse to open rather than rounding. The encoder rounds down and tells the scaler, instead of
    /// surfacing an error that does not mention the size.
    /// </summary>
    [Fact]
    public void Odd_dimensions_are_rounded_down_to_even()
    {
        string path = EncodeToFile(new FrameEncodeOptions
        {
            Width = 101,
            Height = 77,
            FrameRate = 10,
            VideoCodec = PortableCodec,
        }, frames: 5);

        try
        {
            StreamInfo? video = MediaInfo.Probe(path).VideoStream;
            Assert.NotNull(video);
            Assert.Equal(100, video!.Width);
            Assert.Equal(76, video.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_frame_of_the_wrong_size_is_rejected()
    {
        using var sink = new MemoryStream();
        using var encoder = new VideoFrameEncoder(sink, new FrameEncodeOptions
        {
            Width = 16,
            Height = 16,
            FrameRate = 10,
            VideoCodec = PortableCodec,
        });

        // 16*16*4 = 1024 is correct; anything else is a caller bug worth naming immediately, because a
        // short buffer would otherwise be read past the end.
        var ex = Assert.Throws<ArgumentException>(() => encoder.WriteFrame(new byte[1023]));
        Assert.Contains("1024", ex.Message);
    }

    [Fact]
    public void Writing_after_Complete_is_rejected()
    {
        using var sink = new MemoryStream();
        using var encoder = new VideoFrameEncoder(sink, new FrameEncodeOptions
        {
            Width = 16,
            Height = 16,
            FrameRate = 10,
            VideoCodec = PortableCodec,
        });

        encoder.WriteFrame(Frame(16, 16, 0));
        encoder.Complete();

        Assert.Throws<InvalidOperationException>(() => encoder.WriteFrame(Frame(16, 16, 1)));
    }

    [Fact]
    public void Complete_is_idempotent()
    {
        using var sink = new MemoryStream();
        using var encoder = new VideoFrameEncoder(sink, new FrameEncodeOptions
        {
            Width = 16,
            Height = 16,
            FrameRate = 10,
            VideoCodec = PortableCodec,
        });

        encoder.WriteFrame(Frame(16, 16, 0));
        encoder.Complete();
        encoder.Complete();   // must not write a second trailer
    }

    [Fact]
    public void FrameCount_tracks_what_was_written()
    {
        using var sink = new MemoryStream();
        using var encoder = new VideoFrameEncoder(sink, new FrameEncodeOptions
        {
            Width = 16,
            Height = 16,
            FrameRate = 10,
            VideoCodec = PortableCodec,
        });

        Assert.Equal(0, encoder.FrameCount);
        for (int i = 0; i < 7; i++) encoder.WriteFrame(Frame(16, 16, i));
        Assert.Equal(7, encoder.FrameCount);
    }

    /// <summary>A planar format cannot be described by one tightly-packed span, and saying so beats a crash.</summary>
    [Fact]
    public void A_planar_source_format_is_rejected_by_name()
    {
        using var sink = new MemoryStream();
        var ex = Assert.Throws<NotSupportedException>(() => new VideoFrameEncoder(sink, new FrameEncodeOptions
        {
            Width = 16,
            Height = 16,
            FrameRate = 10,
            SourcePixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P,
            VideoCodec = PortableCodec,
        }));
        Assert.Contains("RGBA", ex.Message);
    }

    /// <summary>An encoder the build does not carry must say so, rather than failing as "could not open".</summary>
    [Fact]
    public void A_missing_encoder_names_itself_and_the_GPL_package()
    {
        if (HasEncoder(GPL.Video.X264)) return;   // GPL build — the encoder exists, nothing to assert here

        using var sink = new MemoryStream();
        var ex = Assert.Throws<FFmpegException>(() => new VideoFrameEncoder(sink, new FrameEncodeOptions
        {
            Width = 16,
            Height = 16,
            FrameRate = 10,
            VideoCodec = GPL.Video.X264,
        }));
        Assert.Contains("libx264", ex.Message);
        Assert.Contains("GPL", ex.Message);
    }

    /// <summary>The configuration ImageGen actually uses: H.264, CRF, a preset, fragmented MP4.</summary>
    [Fact]
    public void H264_with_crf_and_preset_produces_a_fragmented_mp4()
    {
        if (!ShouldRunGpl()) return;   // LGPL build — run with -p:UseGPL=true, or set LOXIFI_REQUIRE_GPL=1

        string path = EncodeToFile(new FrameEncodeOptions
        {
            Width = 64,
            Height = 64,
            FrameRate = 24,
            VideoCodec = GPL.Video.X264,
            Quality = 20,
            Preset = "veryfast",
            Fragmented = true,
        }, frames: 24);

        try
        {
            StreamInfo? video = MediaInfo.Probe(path).VideoStream;
            Assert.NotNull(video);
            Assert.Equal(AVCodecID.AV_CODEC_ID_H264, video!.CodecId);
            Assert.Equal(64, video.Width);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A write-only, non-seekable sink — what a network response or a pipe looks like.</summary>
    private sealed class NonSeekableStream : Stream
    {
        public long Written { get; private set; }
        public bool SeekAttempted { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => Written;
            set { SeekAttempted = true; throw new NotSupportedException(); }
        }

        public override void Write(byte[] buffer, int offset, int count) => Written += count;
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
        {
            SeekAttempted = true;
            throw new NotSupportedException();
        }

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
