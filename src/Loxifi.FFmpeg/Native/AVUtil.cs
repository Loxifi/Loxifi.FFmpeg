// AVUtil.cs — P/Invoke declarations for FFmpeg's libavutil library.
// libavutil is the foundational utility library used by all other FFmpeg libraries.
// It provides memory allocation (av_malloc/av_free), frame management, error strings,
// timestamp rescaling, image utilities, channel layout handling, and dictionary (options) support.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// P/Invoke bindings for FFmpeg's <c>libavutil</c> library, which provides utility
/// functions used across all FFmpeg libraries: memory management, frame allocation,
/// error handling, timestamp math, and channel layout operations.
/// </summary>
public static unsafe partial class AVUtil
{
    /// <summary>Logical library name resolved by <see cref="LibraryLoader"/> at runtime.</summary>
    private const string LibName = "avutil";

    /// <summary>Returns the libavutil version as a packed integer.</summary>
    [LibraryImport(LibName, EntryPoint = "avutil_version")]
    public static partial uint avutil_version();

    /// <summary>
    /// Sets the global FFmpeg log level. Common values: AV_LOG_QUIET (-8),
    /// AV_LOG_ERROR (16), AV_LOG_WARNING (24), AV_LOG_INFO (32), AV_LOG_DEBUG (48).
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_log_set_level")]
    public static partial void av_log_set_level(int level);

    /// <summary>Allocates an AVFrame. Must be freed with <see cref="av_frame_free"/>.</summary>
    [LibraryImport(LibName, EntryPoint = "av_frame_alloc")]
    public static partial AVFrame* av_frame_alloc();

    /// <summary>Frees an AVFrame and sets the pointer to null.</summary>
    [LibraryImport(LibName, EntryPoint = "av_frame_free")]
    public static partial void av_frame_free(AVFrame** frame);

    /// <summary>
    /// Unreferences (releases) the data buffers owned by a frame, resetting it for reuse.
    /// Does not free the AVFrame struct itself.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_frame_unref")]
    public static partial void av_frame_unref(AVFrame* frame);

    /// <summary>
    /// Allocates data buffers for a frame based on its width, height, and format.
    /// The frame's width, height, and format fields must be set before calling this.
    /// </summary>
    /// <param name="frame">The frame to allocate buffers for.</param>
    /// <param name="align">Buffer alignment (0 for default, typically 32 bytes).</param>
    [LibraryImport(LibName, EntryPoint = "av_frame_get_buffer")]
    public static partial int av_frame_get_buffer(AVFrame* frame, int align);

    /// <summary>
    /// Converts an FFmpeg error code to a human-readable string.
    /// Used by <see cref="Helpers.FFmpegException"/> for error messages.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_strerror")]
    public static partial int av_strerror(int errnum, byte* errbuf, nuint errbuf_size);

    /// <summary>
    /// Rescales a timestamp from one timebase to another using integer arithmetic
    /// with rounding to avoid floating-point drift. Essential for timestamp conversion
    /// between decoder output timebase and encoder input timebase.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_rescale_q")]
    public static partial long av_rescale_q(long a, AVRational bq, AVRational cq);

    /// <summary>
    /// Returns the size in bytes of an image with the given pixel format, dimensions, and alignment.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_image_get_buffer_size")]
    public static partial int av_image_get_buffer_size(AVPixelFormat pix_fmt, int width, int height, int align);

    /// <summary>
    /// Fills a channel layout struct with the default layout for the given number of channels
    /// (e.g., 2 channels = stereo).
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_channel_layout_default")]
    public static partial void av_channel_layout_default(AVChannelLayout* ch_layout, int nb_channels);

    /// <summary>Copies a channel layout from source to destination.</summary>
    [LibraryImport(LibName, EntryPoint = "av_channel_layout_copy")]
    public static partial int av_channel_layout_copy(AVChannelLayout* dst, AVChannelLayout* src);

    /// <summary>
    /// Allocates memory using FFmpeg's allocator. Memory allocated with this function
    /// must be freed with <see cref="av_free"/>. Used for AVIO buffers which FFmpeg
    /// may reallocate internally.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_malloc")]
    public static partial nint av_malloc(nuint size);

    /// <summary>Frees memory allocated with <see cref="av_malloc"/>.</summary>
    [LibraryImport(LibName, EntryPoint = "av_free")]
    public static partial void av_free(nint ptr);

    /// <summary>Sets a key-value pair in an AVDictionary (FFmpeg's options/metadata container).</summary>
    [LibraryImport(LibName, EntryPoint = "av_dict_set")]
    public static partial int av_dict_set(nint* pm, byte* key, byte* value, int flags);

    /// <summary>Frees an AVDictionary and all its entries.</summary>
    [LibraryImport(LibName, EntryPoint = "av_dict_free")]
    public static partial void av_dict_free(nint* m);
}
