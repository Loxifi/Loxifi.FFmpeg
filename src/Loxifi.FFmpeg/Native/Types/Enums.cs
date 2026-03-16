// Enums.cs — FFmpeg enumeration types used across the P/Invoke layer.
// These values must match their C counterparts exactly. They are defined in various
// FFmpeg headers (libavutil/avutil.h, libavcodec/codec_id.h, libavutil/pixfmt.h, etc.).

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Media stream type classification. Matches FFmpeg's <c>enum AVMediaType</c>.
/// </summary>
public enum AVMediaType
{
    /// <summary>Unknown or unsupported media type.</summary>
    AVMEDIA_TYPE_UNKNOWN = -1,

    /// <summary>Video stream.</summary>
    AVMEDIA_TYPE_VIDEO = 0,

    /// <summary>Audio stream.</summary>
    AVMEDIA_TYPE_AUDIO = 1,

    /// <summary>Opaque data stream (timecodes, etc.).</summary>
    AVMEDIA_TYPE_DATA = 2,

    /// <summary>Subtitle stream.</summary>
    AVMEDIA_TYPE_SUBTITLE = 3,

    /// <summary>Attachment stream (fonts, cover art, etc.).</summary>
    AVMEDIA_TYPE_ATTACHMENT = 4,

    /// <summary>Number of media types (not a valid type).</summary>
    AVMEDIA_TYPE_NB
}

/// <summary>
/// Codec identifiers. Values must match FFmpeg's <c>enum AVCodecID</c>.
/// Only commonly used codecs are included; the full enum has hundreds of entries.
/// </summary>
public enum AVCodecID
{
    /// <summary>No codec specified.</summary>
    AV_CODEC_ID_NONE = 0,

    // Video codecs
    /// <summary>MPEG-2 Video.</summary>
    AV_CODEC_ID_MPEG2VIDEO = 2,

    /// <summary>MPEG-4 Part 2 (DivX/Xvid era).</summary>
    AV_CODEC_ID_MPEG4 = 12,

    /// <summary>H.264/AVC — the most widely used video codec.</summary>
    AV_CODEC_ID_H264 = 27,

    /// <summary>VP8 — Google's open video codec.</summary>
    AV_CODEC_ID_VP8 = 139,

    /// <summary>VP9 — successor to VP8, used by YouTube.</summary>
    AV_CODEC_ID_VP9 = 167,

    /// <summary>H.265/HEVC — ~30% better compression than H.264.</summary>
    AV_CODEC_ID_HEVC = 173,

    /// <summary>AV1 — royalty-free next-gen codec from Alliance for Open Media.</summary>
    AV_CODEC_ID_AV1 = 226,

    // Audio codecs — IDs start at 0x10000 (65536) in FFmpeg's numbering scheme
    /// <summary>MP3 (MPEG Layer 3).</summary>
    AV_CODEC_ID_MP3 = 86017,

    /// <summary>AAC (Advanced Audio Coding) — dominant lossy audio codec.</summary>
    AV_CODEC_ID_AAC = 86018,

    /// <summary>AC-3 (Dolby Digital).</summary>
    AV_CODEC_ID_AC3 = 86019,

    /// <summary>Vorbis — open lossy audio codec.</summary>
    AV_CODEC_ID_VORBIS = 86021,

    /// <summary>FLAC — open lossless audio codec.</summary>
    AV_CODEC_ID_FLAC = 86028,

    /// <summary>Opus — low-latency lossy codec, excellent for voice and music.</summary>
    AV_CODEC_ID_OPUS = 86076,

    /// <summary>PCM signed 16-bit little-endian (uncompressed audio).</summary>
    AV_CODEC_ID_PCM_S16LE = 65536,
}

/// <summary>
/// Pixel format identifiers. Values must match FFmpeg's <c>enum AVPixelFormat</c>.
/// Only commonly used formats are included; FFmpeg supports over 200 pixel formats.
/// </summary>
public enum AVPixelFormat
{
    /// <summary>No pixel format specified.</summary>
    AV_PIX_FMT_NONE = -1,

    /// <summary>Planar YUV 4:2:0, 12bpp — the standard format for H.264/H.265 encoding.</summary>
    AV_PIX_FMT_YUV420P = 0,

    /// <summary>Packed YUV 4:2:2, 16bpp.</summary>
    AV_PIX_FMT_YUYV422 = 1,

    /// <summary>Packed RGB, 24bpp (8 bits per channel).</summary>
    AV_PIX_FMT_RGB24 = 2,

    /// <summary>Packed BGR, 24bpp (8 bits per channel).</summary>
    AV_PIX_FMT_BGR24 = 3,

    /// <summary>Planar YUV 4:2:2, 16bpp.</summary>
    AV_PIX_FMT_YUV422P = 4,

    /// <summary>Planar YUV 4:4:4, 24bpp.</summary>
    AV_PIX_FMT_YUV444P = 5,

    /// <summary>Planar YUV 4:1:0, 9bpp.</summary>
    AV_PIX_FMT_YUV410P = 6,

    /// <summary>Planar YUV 4:1:1, 12bpp.</summary>
    AV_PIX_FMT_YUV411P = 7,

    /// <summary>Semi-planar YUV 4:2:0, 12bpp — common hardware decode output format.</summary>
    AV_PIX_FMT_NV12 = 23,

    /// <summary>Semi-planar YUV 4:2:0 with reversed chroma planes.</summary>
    AV_PIX_FMT_NV21 = 24,

    /// <summary>Packed RGBA, 32bpp (8 bits per channel + alpha).</summary>
    AV_PIX_FMT_RGBA = 26,

    /// <summary>Packed BGRA, 32bpp (8 bits per channel + alpha).</summary>
    AV_PIX_FMT_BGRA = 28,
}

/// <summary>
/// Audio sample format identifiers. Values must match FFmpeg's <c>enum AVSampleFormat</c>.
/// Formats ending in 'P' are planar (separate buffer per channel); others are interleaved.
/// </summary>
public enum AVSampleFormat
{
    /// <summary>No sample format specified.</summary>
    AV_SAMPLE_FMT_NONE = -1,

    /// <summary>Unsigned 8-bit interleaved.</summary>
    AV_SAMPLE_FMT_U8 = 0,

    /// <summary>Signed 16-bit interleaved (CD quality).</summary>
    AV_SAMPLE_FMT_S16 = 1,

    /// <summary>Signed 32-bit interleaved.</summary>
    AV_SAMPLE_FMT_S32 = 2,

    /// <summary>32-bit float interleaved.</summary>
    AV_SAMPLE_FMT_FLT = 3,

    /// <summary>64-bit double interleaved.</summary>
    AV_SAMPLE_FMT_DBL = 4,

    /// <summary>Unsigned 8-bit planar.</summary>
    AV_SAMPLE_FMT_U8P = 5,

    /// <summary>Signed 16-bit planar.</summary>
    AV_SAMPLE_FMT_S16P = 6,

    /// <summary>Signed 32-bit planar.</summary>
    AV_SAMPLE_FMT_S32P = 7,

    /// <summary>32-bit float planar — most common internal format for audio codecs.</summary>
    AV_SAMPLE_FMT_FLTP = 8,

    /// <summary>64-bit double planar.</summary>
    AV_SAMPLE_FMT_DBLP = 9,

    /// <summary>Signed 64-bit interleaved.</summary>
    AV_SAMPLE_FMT_S64 = 10,

    /// <summary>Signed 64-bit planar.</summary>
    AV_SAMPLE_FMT_S64P = 11,
}

/// <summary>
/// AVIO flags controlling the direction of I/O operations.
/// </summary>
[Flags]
public enum AVIOFlags
{
    /// <summary>Open for reading.</summary>
    AVIO_FLAG_READ = 1,

    /// <summary>Open for writing.</summary>
    AVIO_FLAG_WRITE = 2,

    /// <summary>Open for both reading and writing.</summary>
    AVIO_FLAG_READ_WRITE = AVIO_FLAG_READ | AVIO_FLAG_WRITE,
}

/// <summary>
/// Audio channel ordering scheme. Matches FFmpeg's <c>enum AVChannelOrder</c>.
/// </summary>
public enum AVChannelOrder
{
    /// <summary>Channel order is unspecified.</summary>
    AV_CHANNEL_ORDER_UNSPEC = 0,

    /// <summary>Native channel order (bitmask-based, e.g., FL|FR for stereo).</summary>
    AV_CHANNEL_ORDER_NATIVE = 1,

    /// <summary>Custom channel order with per-channel identifiers.</summary>
    AV_CHANNEL_ORDER_CUSTOM = 2,

    /// <summary>Ambisonic channel order.</summary>
    AV_CHANNEL_ORDER_AMBISONIC = 3,
}

/// <summary>
/// Output format flags from AVOutputFormat.flags. Only the flags used by this library are included.
/// </summary>
[Flags]
public enum AVFormatFlags
{
    /// <summary>The format does not require a file (e.g., null muxer). Skip avio_open calls.</summary>
    AVFMT_NOFILE = 0x0001,

    /// <summary>The format requires codec extradata in the global header rather than inline.</summary>
    AVFMT_GLOBALHEADER = 0x0040,
}

/// <summary>
/// Codec context flags from AVCodecContext.flags. Only the flags used by this library are included.
/// </summary>
[Flags]
public enum AVCodecFlags
{
    /// <summary>
    /// Tells the encoder to place SPS/PPS (for H.264) or equivalent data in the
    /// codec extradata rather than prepending it to each keyframe. Required when
    /// the container format has AVFMT_GLOBALHEADER set.
    /// </summary>
    AV_CODEC_FLAG_GLOBAL_HEADER = 1 << 22,
}

/// <summary>
/// Scaling algorithm flags for libswscale's <c>sws_getContext</c>.
/// </summary>
public static class SwsFlags
{
    /// <summary>Bilinear interpolation — fast, acceptable quality for downscaling.</summary>
    public const int SWS_BILINEAR = 2;

    /// <summary>Bicubic interpolation — better quality, slower.</summary>
    public const int SWS_BICUBIC = 4;

    /// <summary>Lanczos interpolation — highest quality, slowest.</summary>
    public const int SWS_LANCZOS = 0x200;
}

/// <summary>
/// FFmpeg error codes used for control flow (EOF, EAGAIN).
/// FFmpeg encodes errors as negative values; AVERROR_EOF and AVERROR_EAGAIN are
/// the two most commonly checked in the decode/encode loop.
/// </summary>
public static class AVErrors
{
    /// <summary>
    /// End of file reached. Returned by av_read_frame when all packets have been read,
    /// and by avcodec_receive_frame/avcodec_receive_packet when the codec is fully flushed.
    /// Computed as FFERRTAG('E','O','F',' ') negated.
    /// </summary>
    public const int AVERROR_EOF = -('E' | ('O' << 8) | ('F' << 16) | (' ' << 24));

    /// <summary>
    /// Resource temporarily unavailable (POSIX EAGAIN). Returned by the codec
    /// send/receive API when the codec needs more input before producing output.
    /// </summary>
    public const int AVERROR_EAGAIN = -11;

    /// <summary>Converts a POSIX errno to an FFmpeg error code (negation).</summary>
    public static int AVERROR(int e) => -e;
}
