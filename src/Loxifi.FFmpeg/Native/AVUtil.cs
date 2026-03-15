using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

public static unsafe partial class AVUtil
{
    private const string LibName = "avutil";

    [LibraryImport(LibName, EntryPoint = "avutil_version")]
    public static partial uint avutil_version();

    [LibraryImport(LibName, EntryPoint = "av_log_set_level")]
    public static partial void av_log_set_level(int level);

    [LibraryImport(LibName, EntryPoint = "av_frame_alloc")]
    public static partial AVFrame* av_frame_alloc();

    [LibraryImport(LibName, EntryPoint = "av_frame_free")]
    public static partial void av_frame_free(AVFrame** frame);

    [LibraryImport(LibName, EntryPoint = "av_frame_unref")]
    public static partial void av_frame_unref(AVFrame* frame);

    [LibraryImport(LibName, EntryPoint = "av_frame_get_buffer")]
    public static partial int av_frame_get_buffer(AVFrame* frame, int align);

    [LibraryImport(LibName, EntryPoint = "av_strerror")]
    public static partial int av_strerror(int errnum, byte* errbuf, nuint errbuf_size);

    [LibraryImport(LibName, EntryPoint = "av_rescale_q")]
    public static partial long av_rescale_q(long a, AVRational bq, AVRational cq);

    [LibraryImport(LibName, EntryPoint = "av_image_get_buffer_size")]
    public static partial int av_image_get_buffer_size(AVPixelFormat pix_fmt, int width, int height, int align);

    [LibraryImport(LibName, EntryPoint = "av_channel_layout_default")]
    public static partial void av_channel_layout_default(AVChannelLayout* ch_layout, int nb_channels);

    [LibraryImport(LibName, EntryPoint = "av_channel_layout_copy")]
    public static partial int av_channel_layout_copy(AVChannelLayout* dst, AVChannelLayout* src);

    [LibraryImport(LibName, EntryPoint = "av_dict_set")]
    public static partial int av_dict_set(nint* pm, byte* key, byte* value, int flags);

    [LibraryImport(LibName, EntryPoint = "av_dict_free")]
    public static partial void av_dict_free(nint* m);

    // Packet functions are in libavcodec in FFmpeg 7.x, accessed via AVCodec class.
    // av_packet_alloc, av_packet_free, av_packet_unref, av_packet_rescale_ts
    // are all declared in AVCodec.cs.
}
