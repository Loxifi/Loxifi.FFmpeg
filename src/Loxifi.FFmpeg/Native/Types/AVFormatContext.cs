using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVFormatContext
{
    public nint AvClass;              // const AVClass*
    public nint Iformat;             // const AVInputFormat*
    public nint Oformat;             // const AVOutputFormat*
    public nint PrivData;            // void*
    public AVIOContext* Pb;          // AVIOContext*
    public int CtxFlags;
    public uint NbStreams;
    public AVStream** Streams;
    public uint NbStreamGroups;
    public nint StreamGroups;        // AVStreamGroup**
    public uint NbChapters;
    public nint Chapters;            // AVChapter**
    public nint Url;                 // char* (NOT fixed array in FFmpeg 7.x)
    public long StartTime;
    public long Duration;
    public long BitRate;
    public uint PacketSize;
    public int MaxDelay;
    public int Flags;
    // Remaining fields omitted — not accessed directly
}

[StructLayout(LayoutKind.Sequential)]
public struct AVOutputFormat
{
    public nint Name;                // const char*
    public nint LongName;           // const char*
    public nint MimeType;           // const char*
    public nint Extensions;         // const char*
    public AVCodecID AudioCodec;
    public AVCodecID VideoCodec;
    public AVCodecID SubtitleCodec;
    public int Flags;
    public nint CodecTag;           // const AVCodecTag* const*
    public nint PrivClass;          // const AVClass*
}
