// SWResample.cs — P/Invoke declarations for FFmpeg's libswresample library.
// libswresample provides audio resampling, sample format conversion, and channel
// layout remapping. Currently declared for completeness but not actively used
// because audio streams are stream-copied rather than re-encoded.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// P/Invoke bindings for FFmpeg's <c>libswresample</c> library, which provides
/// audio resampling and sample format conversion. These bindings are declared for
/// completeness but are not actively used because the transcoder stream-copies audio.
/// </summary>
public static unsafe partial class SWResample
{
    /// <summary>Logical library name resolved by <see cref="LibraryLoader"/> at runtime.</summary>
    private const string LibName = "swresample";

    /// <summary>Returns the libswresample version as a packed integer.</summary>
    [LibraryImport(LibName, EntryPoint = "swresample_version")]
    public static partial uint swresample_version();

    /// <summary>
    /// Allocates and configures a resampler context with the given input/output parameters.
    /// Must be followed by <see cref="swr_init"/> before use.
    /// </summary>
    /// <param name="ps">Output pointer to the allocated SwrContext.</param>
    /// <param name="out_ch_layout">Output channel layout.</param>
    /// <param name="out_sample_fmt">Output sample format.</param>
    /// <param name="out_sample_rate">Output sample rate in Hz.</param>
    /// <param name="in_ch_layout">Input channel layout.</param>
    /// <param name="in_sample_fmt">Input sample format.</param>
    /// <param name="in_sample_rate">Input sample rate in Hz.</param>
    /// <param name="log_offset">Log level offset (typically 0).</param>
    /// <param name="log_ctx">Log context (typically IntPtr.Zero).</param>
    [LibraryImport(LibName, EntryPoint = "swr_alloc_set_opts2")]
    public static partial int swr_alloc_set_opts2(
        nint* ps,
        AVChannelLayout* out_ch_layout,
        AVSampleFormat out_sample_fmt,
        int out_sample_rate,
        AVChannelLayout* in_ch_layout,
        AVSampleFormat in_sample_fmt,
        int in_sample_rate,
        int log_offset,
        nint log_ctx);

    /// <summary>Initializes a resampler context after configuration.</summary>
    [LibraryImport(LibName, EntryPoint = "swr_init")]
    public static partial int swr_init(nint s);

    /// <summary>
    /// Converts audio data from an input frame to an output frame, handling
    /// sample format conversion, resampling, and channel remapping.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "swr_convert_frame")]
    public static partial int swr_convert_frame(
        nint s,
        AVFrame* output,
        AVFrame* input);

    /// <summary>Frees a resampler context and sets the pointer to null.</summary>
    [LibraryImport(LibName, EntryPoint = "swr_free")]
    public static partial void swr_free(nint* s);
}
