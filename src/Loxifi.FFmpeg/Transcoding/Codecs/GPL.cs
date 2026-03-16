// GPL.cs — Codec definitions available in GPL-licensed FFmpeg builds.
// Includes all LGPL codecs plus GPL-only codecs like libx264 and libx265.

namespace Loxifi.FFmpeg.Transcoding.Codecs;

/// <summary>
/// Codecs available in GPL-licensed FFmpeg builds.
/// Includes all LGPL codecs plus GPL-only codecs.
/// </summary>
public static class GPL
{
    /// <summary>
    /// Video encoders available in GPL builds.
    /// </summary>
    public static class Video
    {
        // ── LGPL-compatible (shared with LGPL builds) ──

        /// <summary>AV1 encoder (libaom). High quality, slow encoding.</summary>
        public static readonly VideoCodec AomAv1 = LGPL.Video.AomAv1;

        /// <summary>AV1 encoder (SVT-AV1). Good balance of speed and quality.</summary>
        public static readonly VideoCodec SvtAv1 = LGPL.Video.SvtAv1;

        /// <summary>VP8 encoder.</summary>
        public static readonly VideoCodec Vpx = LGPL.Video.Vpx;

        /// <summary>VP9 encoder. Good compression, widely supported.</summary>
        public static readonly VideoCodec Vp9 = LGPL.Video.Vp9;

        /// <summary>H.264 encoder (Cisco OpenH264). BSD-licensed, moderate quality.</summary>
        public static readonly VideoCodec OpenH264 = LGPL.Video.OpenH264;

        /// <summary>Theora encoder. Open format, legacy.</summary>
        public static readonly VideoCodec Theora = LGPL.Video.Theora;

        /// <summary>WebP encoder. For animated WebP output.</summary>
        public static readonly VideoCodec WebP = LGPL.Video.WebP;

        /// <summary>MPEG-4 Part 2 encoder. Built-in, no external deps.</summary>
        public static readonly VideoCodec Mpeg4 = LGPL.Video.Mpeg4;

        // ── GPL-only ──

        /// <summary>H.264/AVC encoder (x264). Industry standard, excellent quality. GPL only.</summary>
        public static readonly VideoCodec X264 = new("libx264");

        /// <summary>H.265/HEVC encoder (x265). ~30% better compression than H.264. GPL only.</summary>
        public static readonly VideoCodec X265 = new("libx265");

        /// <summary>MPEG-4 ASP encoder (Xvid). Legacy format. GPL only.</summary>
        public static readonly VideoCodec Xvid = new("libxvid");
    }

    /// <summary>
    /// Audio encoders available in GPL builds.
    /// (Currently identical to LGPL — no GPL-only audio encoders.)
    /// </summary>
    public static class Audio
    {
        /// <summary>MP3 encoder (LAME).</summary>
        public static readonly AudioCodec Mp3Lame = LGPL.Audio.Mp3Lame;

        /// <summary>Opus encoder. Best quality-per-bit for voice and music.</summary>
        public static readonly AudioCodec Opus = LGPL.Audio.Opus;

        /// <summary>Vorbis encoder. Open format, good quality.</summary>
        public static readonly AudioCodec Vorbis = LGPL.Audio.Vorbis;

        /// <summary>AAC encoder (built-in). Widely compatible.</summary>
        public static readonly AudioCodec Aac = LGPL.Audio.Aac;
    }
}
