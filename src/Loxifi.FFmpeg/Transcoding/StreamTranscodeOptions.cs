// StreamTranscodeOptions.cs — Configuration for stream-based transcoding operations.
// Similar to TranscodeOptions but uses .NET Streams instead of file paths, and requires
// an explicit output format since there is no file extension to infer it from.

using Loxifi.FFmpeg.Transcoding.Codecs;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Configuration for a stream-based transcoding operation. Uses .NET <see cref="Stream"/>
/// objects for input and output instead of file paths. The output format must be specified
/// explicitly since there is no file extension to infer it from.
/// </summary>
public class StreamTranscodeOptions
{
    /// <summary>Input stream containing the source media data.</summary>
    public required Stream InputStream { get; init; }

    /// <summary>Output stream to write the transcoded media to.</summary>
    public required Stream OutputStream { get; init; }

    /// <summary>
    /// Output container format. Required for stream-based output because there is
    /// no file extension to infer the format from.
    /// </summary>
    public required ContainerFormat OutputFormat { get; init; }

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

    /// <summary>
    /// Converts this stream-based options object to a file-based <see cref="TranscodeOptions"/>
    /// with empty paths (the actual I/O is handled via <see cref="StreamIOContext"/>).
    /// </summary>
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
