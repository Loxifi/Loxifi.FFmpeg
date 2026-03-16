// SWScale.cs — P/Invoke declarations for FFmpeg's libswscale library.
// libswscale provides image scaling and pixel format conversion. Used during transcoding
// when the input pixel format or resolution differs from the encoder's requirements
// (e.g., converting GIF's pal8 format to YUV420P for H.264 encoding).

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// P/Invoke bindings for FFmpeg's <c>libswscale</c> library, which provides
/// video frame scaling and pixel format conversion.
/// </summary>
public static unsafe partial class SWScale
{
    /// <summary>Logical library name resolved by <see cref="LibraryLoader"/> at runtime.</summary>
    private const string LibName = "swscale";

    /// <summary>Returns the libswscale version as a packed integer.</summary>
    [LibraryImport(LibName, EntryPoint = "swscale_version")]
    public static partial uint swscale_version();

    /// <summary>
    /// Creates a scaling/conversion context for transforming frames from one
    /// resolution and pixel format to another. The context is reusable across
    /// multiple frames with the same parameters.
    /// </summary>
    /// <param name="srcW">Source width in pixels.</param>
    /// <param name="srcH">Source height in pixels.</param>
    /// <param name="srcFormat">Source pixel format.</param>
    /// <param name="dstW">Destination width in pixels.</param>
    /// <param name="dstH">Destination height in pixels.</param>
    /// <param name="dstFormat">Destination pixel format.</param>
    /// <param name="flags">Scaling algorithm flags (e.g., <see cref="SwsFlags.SWS_BILINEAR"/>).</param>
    /// <param name="srcFilter">Source filter (typically IntPtr.Zero).</param>
    /// <param name="dstFilter">Destination filter (typically IntPtr.Zero).</param>
    /// <param name="param">Algorithm-specific parameters (typically IntPtr.Zero).</param>
    /// <returns>An opaque SwsContext pointer, or IntPtr.Zero on failure.</returns>
    [LibraryImport(LibName, EntryPoint = "sws_getContext")]
    public static partial nint sws_getContext(
        int srcW, int srcH, AVPixelFormat srcFormat,
        int dstW, int dstH, AVPixelFormat dstFormat,
        int flags,
        nint srcFilter,
        nint dstFilter,
        nint param);

    /// <summary>
    /// Scales/converts image data from source planes to destination planes.
    /// Handles both resolution scaling and pixel format conversion in a single pass.
    /// </summary>
    /// <param name="c">The SwsContext from <see cref="sws_getContext"/>.</param>
    /// <param name="srcSlice">Array of 8 source plane pointers (Y, U, V, ... for planar formats).</param>
    /// <param name="srcStride">Array of 8 source line sizes (byte stride per plane, may include padding).</param>
    /// <param name="srcSliceY">Starting row in the source image (typically 0).</param>
    /// <param name="srcSliceH">Number of rows to process (typically the full source height).</param>
    /// <param name="dst">Array of 8 destination plane pointers.</param>
    /// <param name="dstStride">Array of 8 destination line sizes.</param>
    /// <returns>The height of the output slice.</returns>
    [LibraryImport(LibName, EntryPoint = "sws_scale")]
    public static partial int sws_scale(
        nint c,
        byte** srcSlice,
        int* srcStride,
        int srcSliceY,
        int srcSliceH,
        byte** dst,
        int* dstStride);

    /// <summary>Frees a scaling context created by <see cref="sws_getContext"/>.</summary>
    [LibraryImport(LibName, EntryPoint = "sws_freeContext")]
    public static partial void sws_freeContext(nint swsContext);
}
