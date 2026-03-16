// AVCodecContext.cs — Struct mapping for FFmpeg's AVCodecContext.
// AVCodecContext is the central struct for codec operations (both encoding and decoding).
// Fields are mapped in exact C ABI order up through PixFmt. Audio-specific fields beyond
// PixFmt are not mapped because audio re-encoding is not implemented; audio codec
// parameters are set via avcodec_parameters_to_context/from_context instead.

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Partial mapping of FFmpeg's <c>AVCodecContext</c> struct. Contains codec configuration
/// and state for encoding or decoding. Fields are mapped in exact C ABI order through
/// <c>PixFmt</c>; remaining fields (including audio-specific ones) are omitted.
/// </summary>
/// <remarks>
/// <para>
/// The field order is dictated by FFmpeg's C struct layout. Every field must be the
/// correct size and type; a single misalignment would corrupt all subsequent fields.
/// </para>
/// <para>
/// Some deprecated fields (like <c>TicksPerFrame</c>) are still present in FFmpeg 7.1's
/// ABI under <c>FF_API_TICKS_PER_FRAME</c> and must be included to maintain correct offsets
/// even though they are not used.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVCodecContext
{
    /// <summary>Pointer to AVClass for logging and option handling.</summary>
    public nint AvClass;

    /// <summary>Log level offset for this context.</summary>
    public int LogLevelOffset;

    /// <summary>Media type (video/audio) this context handles.</summary>
    public AVMediaType CodecType;

    /// <summary>The codec this context is configured for (opaque pointer).</summary>
    public nint Codec;

    /// <summary>Codec identifier.</summary>
    public AVCodecID CodecId;

    /// <summary>FourCC codec tag.</summary>
    public uint CodecTag;

    /// <summary>Codec-specific private data (opaque).</summary>
    public nint PrivData;

    /// <summary>Internal codec state (opaque).</summary>
    public nint Internal;

    /// <summary>User-defined opaque data.</summary>
    public nint Opaque;

    /// <summary>
    /// Target bitrate in bits/second. For encoders, this controls the output quality/size.
    /// Set to 0 for codec-default behavior.
    /// </summary>
    public long BitRate;

    /// <summary>Codec flags (e.g., <see cref="AVCodecFlags.AV_CODEC_FLAG_GLOBAL_HEADER"/>).</summary>
    public int Flags;

    /// <summary>Additional codec flags (AV_CODEC_FLAG2_* constants).</summary>
    public int Flags2;

    /// <summary>Codec extradata (SPS/PPS for H.264, etc.).</summary>
    public byte* Extradata;

    /// <summary>Size of extradata in bytes.</summary>
    public int ExtradataSize;

    /// <summary>
    /// Timebase for this codec context. For encoders, this defines the unit of PTS values
    /// on input frames. Typically set to the inverse of the frame rate (1/fps).
    /// </summary>
    public AVRational TimeBase;

    /// <summary>Packet timebase (set by the demuxer for decoders).</summary>
    public AVRational PktTimebase;

    /// <summary>Frame rate for video encoders. Used to derive <see cref="TimeBase"/> as its inverse.</summary>
    public AVRational FrameRate;

    /// <summary>
    /// Deprecated but still present in FFmpeg 7.1 ABI (under FF_API_TICKS_PER_FRAME).
    /// Must be included to maintain correct field offsets for subsequent fields.
    /// </summary>
    public int TicksPerFrame;

    /// <summary>Codec delay (number of frames the codec buffers before producing output).</summary>
    public int Delay;

    /// <summary>Video frame width in pixels.</summary>
    public int Width;

    /// <summary>Video frame height in pixels.</summary>
    public int Height;

    /// <summary>Coded video width (may differ from display width due to alignment).</summary>
    public int CodedWidth;

    /// <summary>Coded video height (may differ from display height due to alignment).</summary>
    public int CodedHeight;

    /// <summary>Sample (pixel) aspect ratio.</summary>
    public AVRational SampleAspectRatio;

    /// <summary>
    /// Pixel format for video encoding/decoding. Most encoders (especially libx264)
    /// require <see cref="AVPixelFormat.AV_PIX_FMT_YUV420P"/>. If the decoded format
    /// differs, a SwsContext is used to convert.
    /// </summary>
    public AVPixelFormat PixFmt;
    // Remaining fields omitted — audio codec context fields (sample_fmt, ch_layout, etc.)
    // are beyond this point. Audio parameters are set via avcodec_parameters_to_context
    // and avcodec_parameters_from_context rather than direct field access.
}

// Note: The AVCodec struct itself is treated as opaque (nint) in all P/Invoke calls.
// The static class Loxifi.FFmpeg.Native.AVCodec contains the P/Invoke declarations;
// the struct type is never instantiated in managed code.
