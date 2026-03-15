using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

public static unsafe partial class SWScale
{
    private const string LibName = "swscale";

    [LibraryImport(LibName, EntryPoint = "swscale_version")]
    public static partial uint swscale_version();

    [LibraryImport(LibName, EntryPoint = "sws_getContext")]
    public static partial nint sws_getContext(
        int srcW, int srcH, AVPixelFormat srcFormat,
        int dstW, int dstH, AVPixelFormat dstFormat,
        int flags,
        nint srcFilter,    // SwsFilter*
        nint dstFilter,    // SwsFilter*
        nint param);       // const double*

    [LibraryImport(LibName, EntryPoint = "sws_scale")]
    public static partial int sws_scale(
        nint c,            // SwsContext*
        byte** srcSlice,
        int* srcStride,
        int srcSliceY,
        int srcSliceH,
        byte** dst,
        int* dstStride);

    [LibraryImport(LibName, EntryPoint = "sws_freeContext")]
    public static partial void sws_freeContext(nint swsContext);
}
