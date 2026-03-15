using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVFrame
{
    // uint8_t *data[8] — 8 pointers
    public nint Data0;
    public nint Data1;
    public nint Data2;
    public nint Data3;
    public nint Data4;
    public nint Data5;
    public nint Data6;
    public nint Data7;

    // int linesize[8]
    public fixed int Linesize[8];

    public byte** ExtendedData;
    public int Width;
    public int Height;
    public int NbSamples;
    public int Format;              // AVPixelFormat or AVSampleFormat

    // key_frame is deprecated but still present in FFmpeg 7.1 (FF_API_FRAME_KEY)
    public int KeyFrame;

    public int PictType;            // enum AVPictureType
    public AVRational SampleAspectRatio;
    public long Pts;
    public long PktDts;
    // Remaining fields omitted — not accessed directly past this point.

    public nint* DataPtrs
    {
        get
        {
            fixed (nint* p = &Data0)
                return p;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct AVChannelLayout
{
    public AVChannelOrder Order;
    public int NbChannels;
    public ulong U;               // union { uint64_t mask; ... }
    public nint Opaque;
}
