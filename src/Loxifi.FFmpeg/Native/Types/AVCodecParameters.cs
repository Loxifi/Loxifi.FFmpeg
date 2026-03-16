// AVCodecParameters.cs — Struct mapping for FFmpeg's AVCodecParameters.
// AVCodecParameters holds stream-level codec information without requiring an open
// codec context. It replaced the deprecated AVStream.codec in FFmpeg 3.x+. Fields
// are mapped in exact C ABI order and cover both video properties (width, height,
// pixel format) and audio properties (sample rate, channel layout).

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Partial mapping of FFmpeg's <c>AVCodecParameters</c> struct. Contains codec
/// information for a stream, shared between demuxer and muxer without opening a
/// codec context. Fields are mapped in exact C ABI order through <c>SeekPreroll</c>.
/// </summary>
/// <remarks>
/// This struct is used for stream copy (avcodec_parameters_copy) and for initializing
/// decoder/encoder contexts (avcodec_parameters_to_context / avcodec_parameters_from_context).
/// The field order must match FFmpeg's C struct exactly for correct unsafe pointer access.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVCodecParameters
{
    /// <summary>General media type (video, audio, subtitle, etc.).</summary>
    public AVMediaType CodecType;

    /// <summary>Specific codec identifier.</summary>
    public AVCodecID CodecId;

    /// <summary>
    /// FourCC codec tag. Set to 0 when copying between containers to let the
    /// muxer assign the correct tag for the output format.
    /// </summary>
    public uint CodecTag;

    /// <summary>Codec extradata (SPS/PPS for H.264, etc.).</summary>
    public byte* Extradata;

    /// <summary>Size of the extradata buffer in bytes.</summary>
    public int ExtradataSize;

    /// <summary>Coded side data (not used by this library).</summary>
    public nint CodedSideData;

    /// <summary>Number of coded side data entries.</summary>
    public int NbCodedSideData;

    /// <summary>
    /// Pixel format for video (<see cref="AVPixelFormat"/>), or sample format for
    /// audio (<see cref="AVSampleFormat"/>). Cast to the appropriate enum based on
    /// <see cref="CodecType"/>.
    /// </summary>
    public int Format;

    /// <summary>Average bitrate in bits/second. May be 0 if unknown.</summary>
    public long BitRate;

    /// <summary>Bits per coded sample (relevant for PCM audio).</summary>
    public int BitsPerCodedSample;

    /// <summary>Bits per raw sample (relevant for high bit-depth video).</summary>
    public int BitsPerRawSample;

    /// <summary>Codec profile (e.g., H.264 High, AAC LC).</summary>
    public int Profile;

    /// <summary>Codec level (e.g., H.264 Level 4.1).</summary>
    public int Level;

    /// <summary>Video width in pixels.</summary>
    public int Width;

    /// <summary>Video height in pixels.</summary>
    public int Height;

    /// <summary>Sample (pixel) aspect ratio.</summary>
    public AVRational SampleAspectRatio;

    /// <summary>Video frame rate as a rational number.</summary>
    public AVRational Framerate;

    /// <summary>Field ordering for interlaced video (enum AVFieldOrder).</summary>
    public int FieldOrder;

    /// <summary>Video color range (limited vs full).</summary>
    public int ColorRange;

    /// <summary>Color primaries (BT.709, BT.2020, etc.).</summary>
    public int ColorPrimaries;

    /// <summary>Color transfer characteristic (gamma curve).</summary>
    public int ColorTrc;

    /// <summary>Color space (YCbCr matrix coefficients).</summary>
    public int ColorSpace;

    /// <summary>Chroma sample location.</summary>
    public int ChromaLocation;

    /// <summary>Number of frames of delay for video codecs (B-frames).</summary>
    public int VideoDelay;

    /// <summary>Audio channel layout (FFmpeg 7.x <c>AVChannelLayout</c> struct, not a bitmask).</summary>
    public AVChannelLayout ChLayout;

    /// <summary>Audio sample rate in Hz.</summary>
    public int SampleRate;

    /// <summary>Audio block alignment (bytes per frame for PCM-like codecs).</summary>
    public int BlockAlign;

    /// <summary>Audio frame size (number of samples per frame).</summary>
    public int FrameSize;

    /// <summary>Number of padding samples at the start of the stream (encoder delay).</summary>
    public int InitialPadding;

    /// <summary>Number of padding samples at the end of the stream.</summary>
    public int TrailingPadding;

    /// <summary>Number of samples to discard after a seek for gapless playback.</summary>
    public int SeekPreroll;
}
