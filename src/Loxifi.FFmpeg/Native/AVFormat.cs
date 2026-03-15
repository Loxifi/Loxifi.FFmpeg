using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

public static unsafe partial class AVFormat
{
    private const string LibName = "avformat";

    [LibraryImport(LibName, EntryPoint = "avformat_version")]
    public static partial uint avformat_version();

    [LibraryImport(LibName, EntryPoint = "avformat_open_input")]
    public static partial int avformat_open_input(
        AVFormatContext** ps,
        byte* url,
        nint fmt,          // const AVInputFormat*
        nint* options);    // AVDictionary**

    [LibraryImport(LibName, EntryPoint = "avformat_find_stream_info")]
    public static partial int avformat_find_stream_info(
        AVFormatContext* ic,
        nint* options);    // AVDictionary**

    [LibraryImport(LibName, EntryPoint = "avformat_close_input")]
    public static partial void avformat_close_input(AVFormatContext** s);

    [LibraryImport(LibName, EntryPoint = "avformat_alloc_output_context2")]
    public static partial int avformat_alloc_output_context2(
        AVFormatContext** ctx,
        nint oformat,      // const AVOutputFormat*
        byte* format_name,
        byte* filename);

    [LibraryImport(LibName, EntryPoint = "avformat_free_context")]
    public static partial void avformat_free_context(AVFormatContext* s);

    [LibraryImport(LibName, EntryPoint = "avformat_new_stream")]
    public static partial AVStream* avformat_new_stream(
        AVFormatContext* s,
        nint c);           // const AVCodec*

    [LibraryImport(LibName, EntryPoint = "avformat_write_header")]
    public static partial int avformat_write_header(
        AVFormatContext* s,
        nint* options);    // AVDictionary**

    [LibraryImport(LibName, EntryPoint = "av_read_frame")]
    public static partial int av_read_frame(AVFormatContext* s, AVPacket* pkt);

    [LibraryImport(LibName, EntryPoint = "av_interleaved_write_frame")]
    public static partial int av_interleaved_write_frame(AVFormatContext* s, AVPacket* pkt);

    [LibraryImport(LibName, EntryPoint = "av_write_trailer")]
    public static partial int av_write_trailer(AVFormatContext* s);

    [LibraryImport(LibName, EntryPoint = "avio_open")]
    public static partial int avio_open(
        AVIOContext** s,
        byte* url,
        int flags);

    [LibraryImport(LibName, EntryPoint = "avio_closep")]
    public static partial int avio_closep(AVIOContext** s);

    [LibraryImport(LibName, EntryPoint = "av_find_best_stream")]
    public static partial int av_find_best_stream(
        AVFormatContext* ic,
        AVMediaType type,
        int wanted_stream_nb,
        int related_stream,
        nint* decoder_ret, // const AVCodec**
        int flags);
}
