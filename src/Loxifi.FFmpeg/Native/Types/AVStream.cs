// AVStream.cs — Struct mapping for FFmpeg's AVStream.
// AVStream represents a single elementary stream (video, audio, subtitle, etc.) within
// a container. Fields are mapped in exact C ABI order. The struct contains timing
// information (timebase, duration), codec parameters, and frame rate metadata.

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Partial mapping of FFmpeg's <c>AVStream</c> struct. Represents a single stream
/// (video, audio, subtitle, etc.) within a container. Fields are mapped in exact
/// C ABI order through <c>AvgFrameRate</c>; remaining fields are omitted.
/// </summary>
/// <remarks>
/// The field order is dictated by FFmpeg's C struct layout and must not be changed.
/// Each field's size and alignment must match the C counterpart exactly.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVStream
{
    /// <summary>Pointer to AVClass for logging.</summary>
    public nint AvClass;

    /// <summary>Stream index within the container (0-based).</summary>
    public int Index;

    /// <summary>Format-specific stream ID.</summary>
    public int Id;

    /// <summary>
    /// Codec parameters describing the stream's codec, dimensions, sample rate, etc.
    /// This replaces the deprecated AVStream.codec field from older FFmpeg versions.
    /// </summary>
    public AVCodecParameters* Codecpar;

    /// <summary>Format-specific private data (opaque).</summary>
    public nint PrivData;

    /// <summary>
    /// Stream timebase — the fundamental unit of time for this stream's timestamps.
    /// All PTS/DTS values in packets from this stream are in this timebase.
    /// For example, a timebase of {1, 90000} means each tick is 1/90000th of a second.
    /// </summary>
    public AVRational TimeBase;

    /// <summary>Stream start time in <see cref="TimeBase"/> units.</summary>
    public long StartTime;

    /// <summary>Stream duration in <see cref="TimeBase"/> units. May be AV_NOPTS_VALUE if unknown.</summary>
    public long Duration;

    /// <summary>Number of frames in the stream (0 if unknown).</summary>
    public long NbFrames;

    /// <summary>Stream disposition flags (AV_DISPOSITION_* constants).</summary>
    public int Disposition;

    /// <summary>Stream discard level (enum AVDiscard).</summary>
    public int Discard;

    /// <summary>Sample aspect ratio (pixel aspect ratio). {0,1} means square pixels.</summary>
    public AVRational SampleAspectRatio;

    /// <summary>Stream metadata dictionary (title, language, etc.).</summary>
    public nint Metadata;

    /// <summary>
    /// Average frame rate as a rational number. More reliable than r_frame_rate for
    /// determining the actual playback frame rate. Used to set the encoder timebase.
    /// </summary>
    public AVRational AvgFrameRate;
    // Remaining fields omitted — not accessed directly by this library.
}
