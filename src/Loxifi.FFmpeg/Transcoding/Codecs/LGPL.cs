// LGPL.cs — Codec definitions available in LGPL-licensed FFmpeg builds.
// These codecs can be used without GPL licensing obligations.

namespace Loxifi.FFmpeg.Transcoding.Codecs;

/// <summary>
/// Codecs available in LGPL-licensed FFmpeg builds.
/// </summary>
public static class LGPL
{
    /// <summary>
    /// Video encoders available in LGPL builds.
    /// </summary>
    public static class Video
    {
        /// <summary>AV1 encoder (libaom). High quality, slow encoding.</summary>
        public static readonly VideoCodec AomAv1 = new("libaom-av1");

        /// <summary>AV1 encoder (SVT-AV1). Good balance of speed and quality.</summary>
        public static readonly VideoCodec SvtAv1 = new("libsvtav1");

        /// <summary>VP8 encoder.</summary>
        public static readonly VideoCodec Vpx = new("libvpx");

        /// <summary>VP9 encoder. Good compression, widely supported.</summary>
        public static readonly VideoCodec Vp9 = new("libvpx-vp9");

        /// <summary>H.264 encoder (Cisco OpenH264). BSD-licensed, moderate quality.</summary>
        public static readonly VideoCodec OpenH264 = new("libopenh264");

        /// <summary>Theora encoder. Open format, legacy.</summary>
        public static readonly VideoCodec Theora = new("libtheora");

        /// <summary>WebP encoder. For animated WebP output.</summary>
        public static readonly VideoCodec WebP = new("libwebp");

        /// <summary>MPEG-4 Part 2 encoder. Built-in, no external deps. Low quality at low bitrates.</summary>
        public static readonly VideoCodec Mpeg4 = new("mpeg4");
    }

    /// <summary>
    /// Audio encoders available in LGPL builds.
    /// </summary>
    public static class Audio
    {
        /// <summary>MP3 encoder (LAME).</summary>
        public static readonly AudioCodec Mp3Lame = new("libmp3lame");

        /// <summary>Opus encoder. Best quality-per-bit for voice and music.</summary>
        public static readonly AudioCodec Opus = new("libopus");

        /// <summary>Vorbis encoder. Open format, good quality.</summary>
        public static readonly AudioCodec Vorbis = new("libvorbis");

        /// <summary>AAC encoder (built-in). Widely compatible.</summary>
        public static readonly AudioCodec Aac = new("aac");
    }
}
