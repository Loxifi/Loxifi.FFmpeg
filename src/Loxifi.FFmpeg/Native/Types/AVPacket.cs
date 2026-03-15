using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVPacket
{
    public nint Buf;                 // AVBufferRef*
    public long Pts;
    public long Dts;
    public byte* Data;
    public int Size;
    public int StreamIndex;
    public int Flags;
    public nint SideData;            // AVPacketSideData*
    public int SideDataElems;
    public long Duration;
    public long Pos;
    public nint Opaque;              // void*
    public nint OpaqueRef;           // AVBufferRef*
    public AVRational TimeBase;
}
