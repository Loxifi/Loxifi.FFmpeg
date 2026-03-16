namespace Loxifi.FFmpeg.Transcoding.Codecs;

/// <summary>
/// Output container format for muxing.
/// </summary>
public enum ContainerFormat
{
    /// <summary>MP4 (MPEG-4 Part 14). Most widely compatible container.</summary>
    Mp4,

    /// <summary>Matroska (MKV). Supports virtually all codecs.</summary>
    Matroska,

    /// <summary>WebM. VP8/VP9/AV1 + Vorbis/Opus. Web-native.</summary>
    WebM,

    /// <summary>AVI. Legacy Microsoft container.</summary>
    Avi,

    /// <summary>QuickTime MOV. Apple ecosystem.</summary>
    Mov,

    /// <summary>Flash Video. Legacy streaming format.</summary>
    Flv,

    /// <summary>MPEG Transport Stream. Broadcast/streaming.</summary>
    MpegTs,

    /// <summary>Ogg container. Open format for Vorbis/Theora/Opus.</summary>
    Ogg,

    /// <summary>GIF. Animated images.</summary>
    Gif,

    /// <summary>MP3 file (audio only).</summary>
    Mp3,

    /// <summary>WAV (audio only). Uncompressed PCM.</summary>
    Wav,

    /// <summary>FLAC (audio only). Lossless compression.</summary>
    Flac,

    /// <summary>AAC ADTS stream (audio only).</summary>
    Adts,
}

public static class ContainerFormatExtensions
{
    public static string ToFFmpegName(this ContainerFormat format) => format switch
    {
        ContainerFormat.Mp4 => "mp4",
        ContainerFormat.Matroska => "matroska",
        ContainerFormat.WebM => "webm",
        ContainerFormat.Avi => "avi",
        ContainerFormat.Mov => "mov",
        ContainerFormat.Flv => "flv",
        ContainerFormat.MpegTs => "mpegts",
        ContainerFormat.Ogg => "ogg",
        ContainerFormat.Gif => "gif",
        ContainerFormat.Mp3 => "mp3",
        ContainerFormat.Wav => "wav",
        ContainerFormat.Flac => "flac",
        ContainerFormat.Adts => "adts",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown container format"),
    };
}
