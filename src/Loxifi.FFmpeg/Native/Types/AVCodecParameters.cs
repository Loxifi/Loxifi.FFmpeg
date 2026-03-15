using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVCodecParameters
{
    public AVMediaType CodecType;
    public AVCodecID CodecId;
    public uint CodecTag;
    public byte* Extradata;
    public int ExtradataSize;
    public nint CodedSideData;       // AVPacketSideData*
    public int NbCodedSideData;
    public int Format;               // AVPixelFormat for video, AVSampleFormat for audio
    public long BitRate;
    public int BitsPerCodedSample;
    public int BitsPerRawSample;
    public int Profile;
    public int Level;
    public int Width;
    public int Height;
    public AVRational SampleAspectRatio;
    public AVRational Framerate;
    public int FieldOrder;           // enum AVFieldOrder
    public int ColorRange;
    public int ColorPrimaries;
    public int ColorTrc;
    public int ColorSpace;
    public int ChromaLocation;
    public int VideoDelay;
    public AVChannelLayout ChLayout;
    public int SampleRate;
    public int BlockAlign;
    public int FrameSize;
    public int InitialPadding;
    public int TrailingPadding;
    public int SeekPreroll;
}
