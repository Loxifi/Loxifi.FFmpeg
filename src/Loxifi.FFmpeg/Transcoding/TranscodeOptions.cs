using Loxifi.FFmpeg.Transcoding.Codecs;

namespace Loxifi.FFmpeg.Transcoding;

public class TranscodeOptions
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }

    /// <summary>
    /// Output container format. If null, inferred from output file extension.
    /// </summary>
    public ContainerFormat? OutputFormat { get; init; }

    /// <summary>
    /// Video codec. Use <see cref="LGPL.Video"/> or <see cref="GPL.Video"/> instances.
    /// Null means stream copy (passthrough).
    /// </summary>
    public VideoCodec? VideoCodec { get; init; }

    /// <summary>
    /// Audio codec. Use <see cref="LGPL.Audio"/> or <see cref="GPL.Audio"/> instances.
    /// Null means stream copy (passthrough).
    /// </summary>
    public AudioCodec? AudioCodec { get; init; }

    /// <summary>Video bitrate in bits per second. 0 = codec default.</summary>
    public long VideoBitRate { get; init; }

    /// <summary>Audio bitrate in bits per second. 0 = codec default.</summary>
    public long AudioBitRate { get; init; }

    /// <summary>Output video width. 0 = same as input.</summary>
    public int Width { get; init; }

    /// <summary>Output video height. 0 = same as input.</summary>
    public int Height { get; init; }

    /// <summary>Output audio sample rate. 0 = same as input.</summary>
    public int SampleRate { get; init; }

    /// <summary>Output audio channel count. 0 = same as input.</summary>
    public int AudioChannels { get; init; }

    internal string? VideoCodecName => VideoCodec?.Name;
    internal string? AudioCodecName => AudioCodec?.Name;
    internal string? OutputFormatName => OutputFormat?.ToFFmpegName();
}
