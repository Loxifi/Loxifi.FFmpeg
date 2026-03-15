namespace Loxifi.FFmpeg.Native.Types;

public enum AVMediaType
{
    AVMEDIA_TYPE_UNKNOWN = -1,
    AVMEDIA_TYPE_VIDEO = 0,
    AVMEDIA_TYPE_AUDIO = 1,
    AVMEDIA_TYPE_DATA = 2,
    AVMEDIA_TYPE_SUBTITLE = 3,
    AVMEDIA_TYPE_ATTACHMENT = 4,
    AVMEDIA_TYPE_NB
}

public enum AVCodecID
{
    AV_CODEC_ID_NONE = 0,

    // Video codecs
    AV_CODEC_ID_H264 = 27,
    AV_CODEC_ID_HEVC = 173,
    AV_CODEC_ID_VP8 = 139,
    AV_CODEC_ID_VP9 = 167,
    AV_CODEC_ID_AV1 = 226,
    AV_CODEC_ID_MPEG4 = 12,
    AV_CODEC_ID_MPEG2VIDEO = 2,

    // Audio codecs
    AV_CODEC_ID_AAC = 86018,
    AV_CODEC_ID_MP3 = 86017,
    AV_CODEC_ID_OPUS = 86076,
    AV_CODEC_ID_VORBIS = 86021,
    AV_CODEC_ID_FLAC = 86028,
    AV_CODEC_ID_AC3 = 86019,
    AV_CODEC_ID_PCM_S16LE = 65536,
}

public enum AVPixelFormat
{
    AV_PIX_FMT_NONE = -1,
    AV_PIX_FMT_YUV420P = 0,
    AV_PIX_FMT_YUYV422 = 1,
    AV_PIX_FMT_RGB24 = 2,
    AV_PIX_FMT_BGR24 = 3,
    AV_PIX_FMT_YUV422P = 4,
    AV_PIX_FMT_YUV444P = 5,
    AV_PIX_FMT_YUV410P = 6,
    AV_PIX_FMT_YUV411P = 7,
    AV_PIX_FMT_NV12 = 23,
    AV_PIX_FMT_NV21 = 24,
    AV_PIX_FMT_RGBA = 26,
    AV_PIX_FMT_BGRA = 28,
}

public enum AVSampleFormat
{
    AV_SAMPLE_FMT_NONE = -1,
    AV_SAMPLE_FMT_U8 = 0,
    AV_SAMPLE_FMT_S16 = 1,
    AV_SAMPLE_FMT_S32 = 2,
    AV_SAMPLE_FMT_FLT = 3,
    AV_SAMPLE_FMT_DBL = 4,
    AV_SAMPLE_FMT_U8P = 5,
    AV_SAMPLE_FMT_S16P = 6,
    AV_SAMPLE_FMT_S32P = 7,
    AV_SAMPLE_FMT_FLTP = 8,
    AV_SAMPLE_FMT_DBLP = 9,
    AV_SAMPLE_FMT_S64 = 10,
    AV_SAMPLE_FMT_S64P = 11,
}

[Flags]
public enum AVIOFlags
{
    AVIO_FLAG_READ = 1,
    AVIO_FLAG_WRITE = 2,
    AVIO_FLAG_READ_WRITE = AVIO_FLAG_READ | AVIO_FLAG_WRITE,
}

public enum AVChannelOrder
{
    AV_CHANNEL_ORDER_UNSPEC = 0,
    AV_CHANNEL_ORDER_NATIVE = 1,
    AV_CHANNEL_ORDER_CUSTOM = 2,
    AV_CHANNEL_ORDER_AMBISONIC = 3,
}

[Flags]
public enum AVFormatFlags
{
    AVFMT_NOFILE = 0x0001,
    AVFMT_GLOBALHEADER = 0x0040,
}

[Flags]
public enum AVCodecFlags
{
    AV_CODEC_FLAG_GLOBAL_HEADER = 1 << 22,
}

public static class SwsFlags
{
    public const int SWS_BILINEAR = 2;
    public const int SWS_BICUBIC = 4;
    public const int SWS_LANCZOS = 0x200;
}

public static class AVErrors
{
    public const int AVERROR_EOF = -('E' | ('O' << 8) | ('F' << 16) | (' ' << 24));
    public const int AVERROR_EAGAIN = -11; // POSIX EAGAIN

    public static int AVERROR(int e) => -e;
}
