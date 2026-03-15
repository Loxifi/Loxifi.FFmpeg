using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

public static unsafe partial class AVCodec
{
    private const string LibName = "avcodec";

    [LibraryImport(LibName, EntryPoint = "avcodec_version")]
    public static partial uint avcodec_version();

    [LibraryImport(LibName, EntryPoint = "avcodec_find_decoder")]
    public static partial nint avcodec_find_decoder(AVCodecID id);

    [LibraryImport(LibName, EntryPoint = "avcodec_find_encoder")]
    public static partial nint avcodec_find_encoder(AVCodecID id);

    [LibraryImport(LibName, EntryPoint = "avcodec_find_decoder_by_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint avcodec_find_decoder_by_name(string name);

    [LibraryImport(LibName, EntryPoint = "avcodec_find_encoder_by_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint avcodec_find_encoder_by_name(string name);

    [LibraryImport(LibName, EntryPoint = "avcodec_alloc_context3")]
    public static partial AVCodecContext* avcodec_alloc_context3(nint codec);

    [LibraryImport(LibName, EntryPoint = "avcodec_free_context")]
    public static partial void avcodec_free_context(AVCodecContext** avctx);

    [LibraryImport(LibName, EntryPoint = "avcodec_parameters_to_context")]
    public static partial int avcodec_parameters_to_context(
        AVCodecContext* codec,
        AVCodecParameters* par);

    [LibraryImport(LibName, EntryPoint = "avcodec_parameters_from_context")]
    public static partial int avcodec_parameters_from_context(
        AVCodecParameters* par,
        AVCodecContext* codec);

    [LibraryImport(LibName, EntryPoint = "avcodec_parameters_copy")]
    public static partial int avcodec_parameters_copy(
        AVCodecParameters* dst,
        AVCodecParameters* src);

    [LibraryImport(LibName, EntryPoint = "avcodec_open2")]
    public static partial int avcodec_open2(
        AVCodecContext* avctx,
        nint codec,        // const AVCodec*
        nint* options);    // AVDictionary**

    [LibraryImport(LibName, EntryPoint = "avcodec_send_packet")]
    public static partial int avcodec_send_packet(AVCodecContext* avctx, AVPacket* avpkt);

    [LibraryImport(LibName, EntryPoint = "avcodec_receive_frame")]
    public static partial int avcodec_receive_frame(AVCodecContext* avctx, AVFrame* frame);

    [LibraryImport(LibName, EntryPoint = "avcodec_send_frame")]
    public static partial int avcodec_send_frame(AVCodecContext* avctx, AVFrame* frame);

    [LibraryImport(LibName, EntryPoint = "avcodec_receive_packet")]
    public static partial int avcodec_receive_packet(AVCodecContext* avctx, AVPacket* avpkt);

    // Packet allocation/free functions live in libavcodec in FFmpeg 7.x
    [LibraryImport(LibName, EntryPoint = "av_packet_alloc")]
    public static partial AVPacket* av_packet_alloc();

    [LibraryImport(LibName, EntryPoint = "av_packet_free")]
    public static partial void av_packet_free(AVPacket** pkt);

    [LibraryImport(LibName, EntryPoint = "av_packet_unref")]
    public static partial void av_packet_unref(AVPacket* pkt);

    [LibraryImport(LibName, EntryPoint = "av_packet_rescale_ts")]
    public static partial void av_packet_rescale_ts(AVPacket* pkt, AVRational tb_src, AVRational tb_dst);
}
