// TranscodeOptions.cs — Configuration for file-based transcoding operations.
// Specifies input/output paths, codec selection, bitrate targets, and resolution overrides.

using Loxifi.FFmpeg.Transcoding.Codecs;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Configuration for a file-based transcoding operation. Specifies the input and output
/// file paths, codec selection (null = stream copy), and optional bitrate/resolution overrides.
/// </summary>
public class TranscodeOptions
{
    /// <summary>Path to the input media file.</summary>
    public required string InputPath { get; init; }

    /// <summary>Path for the output media file.</summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// Output container format. If null, inferred from the output file extension.
    /// </summary>
    public ContainerFormat? OutputFormat { get; init; }

    /// <summary>
    /// Video codec for re-encoding. Use <see cref="LGPL.Video"/> or <see cref="GPL.Video"/> instances.
    /// Null means stream copy (passthrough without re-encoding).
    /// </summary>
    public VideoCodec? VideoCodec { get; init; }

    /// <summary>
    /// Audio codec for re-encoding. Use <see cref="LGPL.Audio"/> or <see cref="GPL.Audio"/> instances.
    /// Null means stream copy (passthrough without re-encoding).
    /// </summary>
    public AudioCodec? AudioCodec { get; init; }

    /// <summary>Target video bitrate in bits per second. 0 uses the codec's default.</summary>
    public long VideoBitRate { get; init; }

    /// <summary>Target audio bitrate in bits per second. 0 uses the codec's default.</summary>
    public long AudioBitRate { get; init; }

    /// <summary>Output video width in pixels. 0 keeps the input resolution.</summary>
    public int Width { get; init; }

    /// <summary>Output video height in pixels. 0 keeps the input resolution.</summary>
    public int Height { get; init; }

    /// <summary>Output audio sample rate in Hz. 0 keeps the input sample rate.</summary>
    public int SampleRate { get; init; }

    /// <summary>Output audio channel count. 0 keeps the input channel count.</summary>
    public int AudioChannels { get; init; }

    /// <summary>The video codec name string for FFmpeg, or null for stream copy.</summary>
    internal string? VideoCodecName => VideoCodec?.Name;

    /// <summary>The audio codec name string for FFmpeg, or null for stream copy.</summary>
    internal string? AudioCodecName => AudioCodec?.Name;

    /// <summary>The output format name string for FFmpeg, or null for auto-detection.</summary>
    internal string? OutputFormatName => OutputFormat?.ToFFmpegName();
}
