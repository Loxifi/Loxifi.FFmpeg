using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;
using Xunit;

namespace Loxifi.FFmpeg.AndroidTests;

public class NativeLibraryTests
{
    [Fact]
    public void AVUtil_Loads_Successfully()
    {
        uint version = AVUtil.avutil_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void AVFormat_Loads_Successfully()
    {
        uint version = AVFormat.avformat_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void AVCodec_Loads_Successfully()
    {
        uint version = AVCodec.avcodec_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void SWScale_Loads_Successfully()
    {
        uint version = SWScale.swscale_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void SWResample_Loads_Successfully()
    {
        uint version = SWResample.swresample_version();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public unsafe void AVCodec_FrameAlloc_And_Free_Works()
    {
        AVFrame* frame = AVUtil.av_frame_alloc();
        Assert.True(frame != null, "av_frame_alloc should return non-null");
        AVUtil.av_frame_free(&frame);
        Assert.True(frame == null, "av_frame_free should set pointer to null");
    }

    [Fact]
    public unsafe void AVCodec_PacketAlloc_And_Free_Works()
    {
        AVPacket* packet = AVCodec.av_packet_alloc();
        Assert.True(packet != null, "av_packet_alloc should return non-null");
        AVCodec.av_packet_free(&packet);
        Assert.True(packet == null, "av_packet_free should set pointer to null");
    }

    [Fact]
    public void AVCodec_FindDecoder_H264_ReturnsNonZero()
    {
        nint decoder = AVCodec.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
        Assert.NotEqual(nint.Zero, decoder);
    }
}
