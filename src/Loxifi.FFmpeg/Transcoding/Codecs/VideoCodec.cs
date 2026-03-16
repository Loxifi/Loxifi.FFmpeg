// VideoCodec.cs — Strongly-typed video encoder codec identifier.

namespace Loxifi.FFmpeg.Transcoding.Codecs;

/// <summary>
/// A video encoder codec. Instances are only available via <see cref="Codecs.LGPL.Video"/>
/// or <see cref="Codecs.GPL.Video"/>.
/// </summary>
public sealed class VideoCodec : ICodec
{
    public string Name { get; }

    internal VideoCodec(string name)
    {
        Name = name;
    }

    public override string ToString() => Name;

    /// <summary>
    /// Implicit conversion to string for backward compatibility with string-based APIs.
    /// </summary>
    public static implicit operator string(VideoCodec codec) => codec.Name;
}
