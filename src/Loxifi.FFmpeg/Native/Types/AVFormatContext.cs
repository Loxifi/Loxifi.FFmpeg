// AVFormatContext.cs — Struct mappings for FFmpeg's AVFormatContext and AVOutputFormat.
// These structs must match the exact field order and sizes of their C counterparts
// because they are accessed directly via unsafe pointers. Fields are mapped up to the
// last one we need; remaining fields are omitted with a comment. Adding or reordering
// fields would break the ABI and cause memory corruption.

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Partial mapping of FFmpeg's <c>AVFormatContext</c> struct. This is the central struct
/// for container-level operations (demuxing and muxing). Fields are mapped in exact C ABI
/// order up through <c>Flags</c>; remaining fields are omitted as they are not accessed.
/// </summary>
/// <remarks>
/// Field order is critical for correct P/Invoke marshalling. Each field must be the
/// correct size and at the correct offset. Adding fields out of order or with wrong
/// sizes would cause all subsequent fields to be read at incorrect offsets.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVFormatContext
{
    /// <summary>Pointer to AVClass for logging and option handling.</summary>
    public nint AvClass;

    /// <summary>The input format (demuxer). Null for output contexts.</summary>
    public nint Iformat;

    /// <summary>The output format (muxer). Null for input contexts. Cast to <see cref="AVOutputFormat"/>*.</summary>
    public nint Oformat;

    /// <summary>Format-specific private data (opaque).</summary>
    public nint PrivData;

    /// <summary>
    /// I/O context pointer. For file-based I/O, set by avio_open. For custom I/O
    /// (e.g., <see cref="StreamIOContext"/>), set manually before avformat_open_input.
    /// </summary>
    public AVIOContext* Pb;

    /// <summary>Context flags (AVFMTCTX_* constants).</summary>
    public int CtxFlags;

    /// <summary>Number of streams in the <see cref="Streams"/> array.</summary>
    public uint NbStreams;

    /// <summary>Array of stream pointers (<c>AVStream**</c>).</summary>
    public AVStream** Streams;

    /// <summary>Number of stream groups (FFmpeg 7.x feature).</summary>
    public uint NbStreamGroups;

    /// <summary>Array of stream group pointers (not used by this library).</summary>
    public nint StreamGroups;

    /// <summary>Number of chapters.</summary>
    public uint NbChapters;

    /// <summary>Array of chapter pointers (not used by this library).</summary>
    public nint Chapters;

    /// <summary>
    /// URL of the input/output. In FFmpeg 7.x this is a <c>char*</c> (heap-allocated),
    /// not a fixed-size array like in older versions.
    /// </summary>
    public nint Url;

    /// <summary>Start time of the stream in AV_TIME_BASE (microsecond) units.</summary>
    public long StartTime;

    /// <summary>Duration of the stream in AV_TIME_BASE (microsecond) units.</summary>
    public long Duration;

    /// <summary>Total bitrate in bits/second. May be 0 if unknown.</summary>
    public long BitRate;

    /// <summary>Packet size (0 = default).</summary>
    public uint PacketSize;

    /// <summary>Maximum mux delay in AV_TIME_BASE units.</summary>
    public int MaxDelay;

    /// <summary>Format flags (AVFMT_FLAG_* constants).</summary>
    public int Flags;
    // Remaining fields omitted — not accessed directly by this library.
}

/// <summary>
/// Partial mapping of FFmpeg's <c>AVOutputFormat</c> struct. Describes an output
/// container format (muxer) including its name, default codecs, and capability flags.
/// Fields are mapped in exact C ABI order through <c>PrivClass</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AVOutputFormat
{
    /// <summary>Short format name (e.g., "mp4", "matroska").</summary>
    public nint Name;

    /// <summary>Long descriptive format name.</summary>
    public nint LongName;

    /// <summary>MIME type (e.g., "video/mp4").</summary>
    public nint MimeType;

    /// <summary>Comma-separated file extensions (e.g., "mp4,m4v").</summary>
    public nint Extensions;

    /// <summary>Default audio codec for this format.</summary>
    public AVCodecID AudioCodec;

    /// <summary>Default video codec for this format.</summary>
    public AVCodecID VideoCodec;

    /// <summary>Default subtitle codec for this format.</summary>
    public AVCodecID SubtitleCodec;

    /// <summary>Format capability flags (AVFMT_NOFILE, AVFMT_GLOBALHEADER, etc.).</summary>
    public int Flags;

    /// <summary>Codec tag tables (not used by this library).</summary>
    public nint CodecTag;

    /// <summary>AVClass for format-specific options.</summary>
    public nint PrivClass;
}
