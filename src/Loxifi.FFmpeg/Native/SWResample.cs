using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

public static unsafe partial class SWResample
{
    private const string LibName = "swresample";

    [LibraryImport(LibName, EntryPoint = "swresample_version")]
    public static partial uint swresample_version();

    [LibraryImport(LibName, EntryPoint = "swr_alloc_set_opts2")]
    public static partial int swr_alloc_set_opts2(
        nint* ps,                    // SwrContext**
        AVChannelLayout* out_ch_layout,
        AVSampleFormat out_sample_fmt,
        int out_sample_rate,
        AVChannelLayout* in_ch_layout,
        AVSampleFormat in_sample_fmt,
        int in_sample_rate,
        int log_offset,
        nint log_ctx);

    [LibraryImport(LibName, EntryPoint = "swr_init")]
    public static partial int swr_init(nint s);

    [LibraryImport(LibName, EntryPoint = "swr_convert_frame")]
    public static partial int swr_convert_frame(
        nint s,            // SwrContext*
        AVFrame* output,
        AVFrame* input);

    [LibraryImport(LibName, EntryPoint = "swr_free")]
    public static partial void swr_free(nint* s);
}
