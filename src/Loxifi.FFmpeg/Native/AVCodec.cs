// AVCodec.cs — P/Invoke declarations for FFmpeg's libavcodec library.
// libavcodec provides encoding and decoding functionality. In FFmpeg 7.x, packet
// allocation/free functions (av_packet_alloc, av_packet_free, etc.) also live in
// libavcodec rather than libavutil.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// P/Invoke bindings for FFmpeg's <c>libavcodec</c> library, which provides
/// codec (encoder/decoder) operations including finding codecs, allocating contexts,
/// and the send/receive packet/frame API for encoding and decoding.
/// </summary>
public static unsafe partial class AVCodec
{
    /// <summary>Logical library name resolved by <see cref="LibraryLoader"/> at runtime.</summary>
    private const string LibName = "avcodec";

    /// <summary>Returns the libavcodec version as a packed integer.</summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_version")]
    public static partial uint avcodec_version();

    /// <summary>Finds a decoder by its codec ID. Returns an opaque AVCodec pointer, or IntPtr.Zero if not found.</summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_find_decoder")]
    public static partial nint avcodec_find_decoder(AVCodecID id);

    /// <summary>Finds an encoder by its codec ID. Returns an opaque AVCodec pointer, or IntPtr.Zero if not found.</summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_find_encoder")]
    public static partial nint avcodec_find_encoder(AVCodecID id);

    /// <summary>Finds a decoder by name (e.g., "h264"). Returns IntPtr.Zero if not found.</summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_find_decoder_by_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint avcodec_find_decoder_by_name(string name);

    /// <summary>
    /// Finds an encoder by name (e.g., "libx264", "aac"). Returns IntPtr.Zero if the
    /// encoder is not compiled into the loaded FFmpeg build. Used at runtime to detect
    /// whether GPL codecs (like libx264) are available.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_find_encoder_by_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint avcodec_find_encoder_by_name(string name);

    /// <summary>
    /// Allocates an AVCodecContext for the given codec. The codec pointer comes from
    /// avcodec_find_decoder or avcodec_find_encoder.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_alloc_context3")]
    public static partial AVCodecContext* avcodec_alloc_context3(nint codec);

    /// <summary>Frees an AVCodecContext and sets the pointer to null.</summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_free_context")]
    public static partial void avcodec_free_context(AVCodecContext** avctx);

    /// <summary>
    /// Copies codec parameters from an AVCodecParameters to an AVCodecContext.
    /// Used to initialize a decoder context from stream parameters.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_parameters_to_context")]
    public static partial int avcodec_parameters_to_context(
        AVCodecContext* codec,
        AVCodecParameters* par);

    /// <summary>
    /// Copies codec parameters from an AVCodecContext to an AVCodecParameters.
    /// Used to propagate encoder settings to the output stream.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_parameters_from_context")]
    public static partial int avcodec_parameters_from_context(
        AVCodecParameters* par,
        AVCodecContext* codec);

    /// <summary>
    /// Copies codec parameters between AVCodecParameters structs.
    /// Used for stream copy (remuxing) where no decoding/encoding occurs.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_parameters_copy")]
    public static partial int avcodec_parameters_copy(
        AVCodecParameters* dst,
        AVCodecParameters* src);

    /// <summary>
    /// Opens a codec context for use. Must be called after setting all parameters
    /// and before sending packets (decoder) or frames (encoder).
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_open2")]
    public static partial int avcodec_open2(
        AVCodecContext* avctx,
        nint codec,        // const AVCodec*
        nint* options);    // AVDictionary** — codec-specific options

    /// <summary>
    /// Sends a compressed packet to the decoder. Part of the send/receive API:
    /// call this, then call avcodec_receive_frame in a loop to get decoded frames.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_send_packet")]
    public static partial int avcodec_send_packet(AVCodecContext* avctx, AVPacket* avpkt);

    /// <summary>
    /// Receives a decoded frame from the decoder. Returns AVERROR(EAGAIN) when no
    /// frame is available yet, or AVERROR_EOF when the decoder is fully flushed.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_receive_frame")]
    public static partial int avcodec_receive_frame(AVCodecContext* avctx, AVFrame* frame);

    /// <summary>
    /// Sends a raw frame to the encoder. Send a null frame to flush the encoder.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_send_frame")]
    public static partial int avcodec_send_frame(AVCodecContext* avctx, AVFrame* frame);

    /// <summary>
    /// Receives an encoded packet from the encoder. Returns AVERROR(EAGAIN) when no
    /// packet is available yet, or AVERROR_EOF when the encoder is fully flushed.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avcodec_receive_packet")]
    public static partial int avcodec_receive_packet(AVCodecContext* avctx, AVPacket* avpkt);

    /// <summary>
    /// Allocates a new AVPacket. In FFmpeg 7.x, packet functions live in libavcodec.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_packet_alloc")]
    public static partial AVPacket* av_packet_alloc();

    /// <summary>Frees an AVPacket and sets the pointer to null.</summary>
    [LibraryImport(LibName, EntryPoint = "av_packet_free")]
    public static partial void av_packet_free(AVPacket** pkt);

    /// <summary>Unreferences (releases) the data owned by a packet, resetting it for reuse.</summary>
    [LibraryImport(LibName, EntryPoint = "av_packet_unref")]
    public static partial void av_packet_unref(AVPacket* pkt);

    /// <summary>
    /// Rescales packet timestamps (pts, dts, duration) from one timebase to another.
    /// Essential when remuxing between containers that use different timebases.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_packet_rescale_ts")]
    public static partial void av_packet_rescale_ts(AVPacket* pkt, AVRational tb_src, AVRational tb_dst);
}
