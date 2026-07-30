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

    /// <summary>
    /// Installs a custom global log callback. The pointer must match the C signature
    /// <c>void (*)(void* avcl, int level, const char* fmt, va_list vl)</c> using the
    /// C calling convention. Passing <see cref="nint.Zero"/> restores FFmpeg's default.
    /// Used by <see cref="Helpers.FFmpegLog"/> to capture codec diagnostics.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_log_set_callback")]
    public static partial void av_log_set_callback(nint callback);

    /// <summary>
    /// Renders a single log entry — expanding the <c>va_list</c> against <paramref name="fmt"/> —
    /// into <paramref name="line"/>. Called from a custom log callback to turn FFmpeg's
    /// printf-style arguments into a finished string without touching the va_list directly.
    /// </summary>
    /// <param name="print_prefix">In/out flag controlling whether the line prefix is emitted.</param>
    [LibraryImport(LibName, EntryPoint = "av_log_format_line2")]
    public static partial int av_log_format_line2(nint avcl, int level, byte* fmt, nint vl, byte* line, int line_size, int* print_prefix);

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
    /// Ensures the frame's data is writable, copying the buffer only if something else still references
    /// it. Required before overwriting a frame that has already been sent to an encoder: the encoder may
    /// still hold it for lookahead or B-frame reordering, and writing into it regardless corrupts frames
    /// it has not finished with.
    /// </summary>
    /// <param name="frame">The frame to make writable.</param>
    [LibraryImport(LibName, EntryPoint = "av_frame_make_writable")]
    public static partial int av_frame_make_writable(AVFrame* frame);

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
