namespace Loxifi.FFmpeg.Transcoding;

public class StreamTranscodeOptions
{
    public required Stream InputStream { get; init; }
    public required Stream OutputStream { get; init; }

    /// <summary>
    /// Output container format (e.g., "mp4", "webm", "mkv"). Required for stream output.
    /// </summary>
    public required string OutputFormat { get; init; }

    /// <summary>
    /// Video codec name (e.g., "libx264", "libvpx-vp9").
    /// Null means stream copy (passthrough).
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Audio codec name (e.g., "aac", "libopus").
    /// Null means stream copy (passthrough).
    /// </summary>
    public string? AudioCodec { get; init; }

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

    internal TranscodeOptions ToFileOptions() => new()
    {
        InputPath = "",
        OutputPath = "",
        OutputFormat = OutputFormat,
        VideoCodec = VideoCodec,
        AudioCodec = AudioCodec,
        VideoBitRate = VideoBitRate,
        AudioBitRate = AudioBitRate,
        Width = Width,
        Height = Height,
        SampleRate = SampleRate,
        AudioChannels = AudioChannels,
    };
}
