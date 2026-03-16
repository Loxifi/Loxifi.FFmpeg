namespace Loxifi.FFmpeg.Transcoding.Codecs;

/// <summary>
/// Represents an FFmpeg encoder codec.
/// </summary>
public interface ICodec
{
    /// <summary>
    /// The FFmpeg encoder name (e.g., "libx264", "aac").
    /// </summary>
    string Name { get; }
}
