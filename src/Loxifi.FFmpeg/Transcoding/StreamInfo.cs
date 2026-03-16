// StreamInfo.cs — Describes a single stream within a media container.
// Populated by MediaInfo.Probe with codec, format, and timing information
// extracted from FFmpeg's AVStream and AVCodecParameters.

using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Describes a single stream (video, audio, subtitle, etc.) within a media container.
/// Populated by <see cref="MediaInfo.Probe"/> with information extracted from FFmpeg's
/// AVStream and AVCodecParameters.
/// </summary>
public class StreamInfo
{
    /// <summary>Stream index within the container (0-based).</summary>
    public int Index { get; init; }

    /// <summary>Media type (video, audio, subtitle, etc.).</summary>
    public AVMediaType MediaType { get; init; }

    /// <summary>Codec identifier (e.g., H264, AAC).</summary>
    public AVCodecID CodecId { get; init; }

    /// <summary>Average bitrate in bits/second. May be 0 if unknown.</summary>
    public long BitRate { get; init; }

    // Video properties

    /// <summary>Video frame width in pixels. 0 for non-video streams.</summary>
    public int Width { get; init; }

    /// <summary>Video frame height in pixels. 0 for non-video streams.</summary>
    public int Height { get; init; }

    /// <summary>Video pixel format. Meaningless for audio streams.</summary>
    public AVPixelFormat PixelFormat { get; init; }

    /// <summary>Average video frame rate in frames/second. 0 for non-video streams.</summary>
    public double FrameRate { get; init; }

    // Audio properties

    /// <summary>Audio sample rate in Hz. 0 for non-audio streams.</summary>
    public int SampleRate { get; init; }

    /// <summary>Number of audio channels. 0 for non-audio streams.</summary>
    public int Channels { get; init; }

    /// <summary>Audio sample format. Meaningless for video streams.</summary>
    public AVSampleFormat SampleFormat { get; init; }

    /// <summary>Stream duration. May be <see cref="TimeSpan.Zero"/> if unknown.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Stream timebase used for timestamp calculations.</summary>
    public AVRational TimeBase { get; init; }
}
