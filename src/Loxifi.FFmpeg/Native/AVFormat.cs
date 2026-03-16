// AVFormat.cs — P/Invoke declarations for FFmpeg's libavformat library.
// libavformat handles container-level I/O: opening/closing media files, reading/writing
// packets, and managing the muxer/demuxer lifecycle. All functions use the logical
// library name "avformat" which is resolved at runtime by LibraryLoader.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// P/Invoke bindings for FFmpeg's <c>libavformat</c> library, which provides
/// container format (muxer/demuxer) operations including opening files, reading
/// packets, writing output, and managing AVIO contexts for custom I/O.
/// </summary>
public static unsafe partial class AVFormat
{
    /// <summary>Logical library name resolved by <see cref="LibraryLoader"/> at runtime.</summary>
    private const string LibName = "avformat";

    /// <summary>Returns the libavformat version as a packed integer (major &lt;&lt; 16 | minor &lt;&lt; 8 | micro).</summary>
    [LibraryImport(LibName, EntryPoint = "avformat_version")]
    public static partial uint avformat_version();

    /// <summary>
    /// Opens an input media file/stream and reads the container header.
    /// The <paramref name="ps"/> pointer is allocated internally if null, or reused if pre-allocated
    /// (required for custom AVIO contexts where Pb must be set before calling this).
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avformat_open_input")]
    public static partial int avformat_open_input(
        AVFormatContext** ps,
        byte* url,
        nint fmt,          // const AVInputFormat* — null for auto-detect
        nint* options);    // AVDictionary** — null for defaults

    /// <summary>
    /// Probes the input and populates stream information (codec, duration, bitrate, etc.).
    /// Must be called after <see cref="avformat_open_input"/> before reading packets.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avformat_find_stream_info")]
    public static partial int avformat_find_stream_info(
        AVFormatContext* ic,
        nint* options);    // AVDictionary** — per-stream options, null for defaults

    /// <summary>
    /// Closes the input and frees the format context. Sets the pointer to null.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avformat_close_input")]
    public static partial void avformat_close_input(AVFormatContext** s);

    /// <summary>
    /// Allocates an empty AVFormatContext. Used when setting up custom I/O (Pb)
    /// before calling <see cref="avformat_open_input"/>.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avformat_alloc_context")]
    public static partial AVFormatContext* avformat_alloc_context();

    /// <summary>
    /// Allocates an output format context. The format can be specified by name, or
    /// inferred from the filename extension if <paramref name="format_name"/> is null.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avformat_alloc_output_context2")]
    public static partial int avformat_alloc_output_context2(
        AVFormatContext** ctx,
        nint oformat,      // const AVOutputFormat* — null to auto-detect
        byte* format_name, // e.g., "mp4", "matroska" — null to infer from filename
        byte* filename);   // output filename for format inference

    /// <summary>Frees a format context (does not close the AVIO handle).</summary>
    [LibraryImport(LibName, EntryPoint = "avformat_free_context")]
    public static partial void avformat_free_context(AVFormatContext* s);

    /// <summary>
    /// Creates a new stream in the output format context. The codec parameter is
    /// typically null; codec parameters are set separately via avcodec_parameters_copy.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avformat_new_stream")]
    public static partial AVStream* avformat_new_stream(
        AVFormatContext* s,
        nint c);           // const AVCodec* — usually null

    /// <summary>
    /// Writes the container header to the output. Must be called after all streams
    /// are configured and before writing any packets.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avformat_write_header")]
    public static partial int avformat_write_header(
        AVFormatContext* s,
        nint* options);    // AVDictionary** — muxer options, null for defaults

    /// <summary>
    /// Reads the next packet from the input. Returns AVERROR_EOF at end of file.
    /// The caller must call av_packet_unref after processing each packet.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_read_frame")]
    public static partial int av_read_frame(AVFormatContext* s, AVPacket* pkt);

    /// <summary>
    /// Writes a packet to the output with proper interleaving. The muxer may buffer
    /// packets internally to ensure correct ordering in the output file.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_interleaved_write_frame")]
    public static partial int av_interleaved_write_frame(AVFormatContext* s, AVPacket* pkt);

    /// <summary>
    /// Writes the container trailer (e.g., MP4 moov atom) and flushes all buffered packets.
    /// Must be the last write operation before closing the output.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_write_trailer")]
    public static partial int av_write_trailer(AVFormatContext* s);

    /// <summary>
    /// Opens a file for I/O and creates an AVIOContext for it.
    /// Used for file-based output; stream-based output uses <see cref="avio_alloc_context"/> instead.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avio_open")]
    public static partial int avio_open(
        AVIOContext** s,
        byte* url,
        int flags);        // AVIOFlags (AVIO_FLAG_READ, AVIO_FLAG_WRITE, etc.)

    /// <summary>
    /// Closes an AVIO context opened with <see cref="avio_open"/> and sets the pointer to null.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avio_closep")]
    public static partial int avio_closep(AVIOContext** s);

    /// <summary>
    /// Allocates a custom AVIOContext with caller-provided read/write/seek callbacks.
    /// The buffer must be allocated with av_malloc because FFmpeg may realloc or free it.
    /// Used by <see cref="StreamIOContext"/> to bridge .NET Streams to FFmpeg I/O.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avio_alloc_context")]
    public static partial AVIOContext* avio_alloc_context(
        byte* buffer,       // av_malloc'd buffer, ownership transferred to FFmpeg
        int buffer_size,
        int write_flag,     // 1 for output, 0 for input
        nint opaque,        // user data passed to all callbacks
        nint read_packet,   // int (*)(void*, uint8_t*, int) — null for write-only
        nint write_packet,  // int (*)(void*, const uint8_t*, int) — null for read-only
        nint seek);         // int64_t (*)(void*, int64_t, int) — null if not seekable

    /// <summary>
    /// Frees an AVIOContext and its internal buffer. Sets the pointer to null.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "avio_context_free")]
    public static partial void avio_context_free(AVIOContext** s);

    /// <summary>
    /// Finds the "best" stream of a given type (video, audio, etc.) in the input.
    /// Returns the stream index, or a negative AVERROR if not found.
    /// </summary>
    [LibraryImport(LibName, EntryPoint = "av_find_best_stream")]
    public static partial int av_find_best_stream(
        AVFormatContext* ic,
        AVMediaType type,
        int wanted_stream_nb, // preferred stream index, or -1 for auto
        int related_stream,   // related stream for preference, or -1
        nint* decoder_ret,    // const AVCodec** — optional output for the decoder
        int flags);           // reserved, pass 0
}
