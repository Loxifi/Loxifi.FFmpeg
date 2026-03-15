using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVStream
{
    public nint AvClass;              // const AVClass*
    public int Index;
    public int Id;
    public AVCodecParameters* Codecpar;
    public nint PrivData;            // void*
    public AVRational TimeBase;
    public long StartTime;
    public long Duration;
    public long NbFrames;
    public int Disposition;
    public int Discard;              // enum AVDiscard
    public AVRational SampleAspectRatio;
    public nint Metadata;            // AVDictionary*
    public AVRational AvgFrameRate;
    // Remaining fields omitted — not accessed directly
}
