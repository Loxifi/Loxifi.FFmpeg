// AVPacket.cs — Struct mapping for FFmpeg's AVPacket.
// AVPacket represents a single compressed data unit (one video frame, one audio frame,
// or one subtitle entry). It carries timestamps (PTS/DTS), the compressed data, and
// metadata about which stream it belongs to. Fields are mapped in exact C ABI order.

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Mapping of FFmpeg's <c>AVPacket</c> struct. Represents a single unit of compressed
/// data read from a demuxer or produced by an encoder. Fields are mapped in exact
/// C ABI order; all fields are included as the struct is small.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVPacket
{
    /// <summary>Reference-counted buffer backing the packet data.</summary>
    public nint Buf;

    /// <summary>
    /// Presentation timestamp — when this packet should be displayed.
    /// In stream timebase units. May be AV_NOPTS_VALUE if unknown.
    /// </summary>
    public long Pts;

    /// <summary>
    /// Decompression timestamp — when this packet should be decoded.
    /// Differs from PTS for codecs with B-frames (reordering).
    /// </summary>
    public long Dts;

    /// <summary>Pointer to the compressed data.</summary>
    public byte* Data;

    /// <summary>Size of the compressed data in bytes.</summary>
    public int Size;

    /// <summary>
    /// Index of the stream this packet belongs to. Must be remapped when
    /// copying packets between containers with different stream ordering.
    /// </summary>
    public int StreamIndex;

    /// <summary>Packet flags (e.g., AV_PKT_FLAG_KEY for keyframes).</summary>
    public int Flags;

    /// <summary>Additional side data (not used by this library).</summary>
    public nint SideData;

    /// <summary>Number of side data elements.</summary>
    public int SideDataElems;

    /// <summary>Duration of this packet in stream timebase units.</summary>
    public long Duration;

    /// <summary>
    /// Byte position in the input file. Set to -1 when writing output packets
    /// since the position is meaningless in the new file.
    /// </summary>
    public long Pos;

    /// <summary>User-defined opaque data (not used by this library).</summary>
    public nint Opaque;

    /// <summary>Reference-counted opaque data (not used by this library).</summary>
    public nint OpaqueRef;

    /// <summary>Packet timebase (FFmpeg 7.x addition; packets carry their own timebase).</summary>
    public AVRational TimeBase;
}
