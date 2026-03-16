// AudioCodec.cs — Strongly-typed audio encoder codec identifier.

namespace Loxifi.FFmpeg.Transcoding.Codecs;

/// <summary>
/// An audio encoder codec. Instances are only available via <see cref="Codecs.LGPL.Audio"/>
/// or <see cref="Codecs.GPL.Audio"/>.
/// </summary>
public sealed class AudioCodec : ICodec
{
    public string Name { get; }

    internal AudioCodec(string name)
    {
        Name = name;
    }

    public override string ToString() => Name;

    /// <summary>
    /// Implicit conversion to string for backward compatibility with string-based APIs.
    /// </summary>
    public static implicit operator string(AudioCodec codec) => codec.Name;
}
