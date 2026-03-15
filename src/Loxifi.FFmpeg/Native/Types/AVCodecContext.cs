using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVCodecContext
{
    public nint AvClass;              // const AVClass*
    public int LogLevelOffset;
    public AVMediaType CodecType;
    public nint Codec;               // const AVCodec*
    public AVCodecID CodecId;
    public uint CodecTag;
    public nint PrivData;            // void*
    public nint Internal;            // AVCodecInternal*
    public nint Opaque;              // void*
    public long BitRate;
    public int Flags;
    public int Flags2;
    public byte* Extradata;
    public int ExtradataSize;
    public AVRational TimeBase;
    public AVRational PktTimebase;
    public AVRational FrameRate;
    // ticks_per_frame is deprecated but still present in FFmpeg 7.1 (FF_API_TICKS_PER_FRAME)
    public int TicksPerFrame;
    public int Delay;
    public int Width;
    public int Height;
    public int CodedWidth;
    public int CodedHeight;
    public AVRational SampleAspectRatio;
    public AVPixelFormat PixFmt;
    // Remaining fields omitted — not accessed directly.
    // For audio codec context fields, use avcodec_parameters_to_context/from_context.
}

// Note: The AVCodec struct is treated as opaque (nint) in P/Invoke calls.
// The static class Loxifi.FFmpeg.Native.AVCodec contains the P/Invoke declarations.
