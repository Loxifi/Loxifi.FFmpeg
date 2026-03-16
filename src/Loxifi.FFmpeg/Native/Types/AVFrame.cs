// AVFrame.cs — Struct mappings for FFmpeg's AVFrame and AVChannelLayout.
// AVFrame represents a decoded video frame or audio sample buffer. The data pointer
// and linesize arrays are the core fields — they point to pixel/sample planes and
// describe byte strides. Fields are mapped in exact C ABI order through PktDts.

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Partial mapping of FFmpeg's <c>AVFrame</c> struct. Represents a decoded video frame
/// or audio sample buffer. Fields are mapped in exact C ABI order through <c>PktDts</c>;
/// remaining fields are omitted.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Data0..Data7</c> fields correspond to FFmpeg's <c>uint8_t *data[8]</c> array.
/// They are declared as individual <c>nint</c> fields because C# cannot have fixed-size
/// arrays of pointer types. For planar video (e.g., YUV420P), Data0=Y, Data1=U, Data2=V.
/// </para>
/// <para>
/// The <c>Linesize</c> array contains the byte stride of each plane. This may be larger
/// than <c>Width * bytes_per_pixel</c> due to alignment padding (typically 32 or 64 bytes).
/// Always use <c>Linesize</c> for pointer arithmetic, never compute stride from width alone.
/// </para>
/// <para>
/// Some deprecated fields (like <c>KeyFrame</c>) are still present in FFmpeg 7.1's ABI
/// and must be included to maintain correct field offsets.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVFrame
{
    // uint8_t *data[8] — pointers to pixel/sample planes.
    // For YUV420P video: Data0=Y plane, Data1=U plane, Data2=V plane, Data3..7=unused.
    // For interleaved audio: Data0=sample buffer, Data1..7=unused.
    // For planar audio: Data0=ch0, Data1=ch1, etc.

    /// <summary>Pointer to data plane 0 (Y for YUV, or the sole plane for packed formats).</summary>
    public nint Data0;
    /// <summary>Pointer to data plane 1 (U/Cb for YUV).</summary>
    public nint Data1;
    /// <summary>Pointer to data plane 2 (V/Cr for YUV).</summary>
    public nint Data2;
    /// <summary>Pointer to data plane 3.</summary>
    public nint Data3;
    /// <summary>Pointer to data plane 4.</summary>
    public nint Data4;
    /// <summary>Pointer to data plane 5.</summary>
    public nint Data5;
    /// <summary>Pointer to data plane 6.</summary>
    public nint Data6;
    /// <summary>Pointer to data plane 7.</summary>
    public nint Data7;

    /// <summary>
    /// Byte stride (line size) for each of the 8 data planes. Includes alignment padding.
    /// For video, Linesize[0] is the Y plane stride, Linesize[1] is U, Linesize[2] is V.
    /// </summary>
    public fixed int Linesize[8];

    /// <summary>Extended data pointer for audio with more than 8 channels.</summary>
    public byte** ExtendedData;

    /// <summary>Video frame width in pixels.</summary>
    public int Width;

    /// <summary>Video frame height in pixels.</summary>
    public int Height;

    /// <summary>Number of audio samples per channel in this frame.</summary>
    public int NbSamples;

    /// <summary>
    /// Frame format: <see cref="AVPixelFormat"/> for video, <see cref="AVSampleFormat"/> for audio.
    /// Cast to the appropriate enum based on the codec type.
    /// </summary>
    public int Format;

    /// <summary>
    /// Deprecated keyframe flag. Still present in FFmpeg 7.1 ABI (under FF_API_FRAME_KEY).
    /// Must be included to maintain correct field offsets.
    /// </summary>
    public int KeyFrame;

    /// <summary>Picture type (I, P, B frame — enum AVPictureType).</summary>
    public int PictType;

    /// <summary>Sample aspect ratio.</summary>
    public AVRational SampleAspectRatio;

    /// <summary>
    /// Presentation timestamp — when this frame should be displayed.
    /// In the timebase of the stream or codec context.
    /// </summary>
    public long Pts;

    /// <summary>DTS copied from the packet that produced this frame.</summary>
    public long PktDts;
    // Remaining fields omitted — not accessed directly past this point.
}

/// <summary>
/// Mapping of FFmpeg's <c>AVChannelLayout</c> struct (FFmpeg 7.x).
/// Replaces the old <c>uint64_t channel_layout</c> bitmask with a more flexible
/// representation supporting native, custom, and ambisonic channel orders.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AVChannelLayout
{
    /// <summary>Channel ordering scheme (native bitmask, custom, ambisonic, etc.).</summary>
    public AVChannelOrder Order;

    /// <summary>Number of audio channels.</summary>
    public int NbChannels;

    /// <summary>
    /// Union field: for <see cref="AVChannelOrder.AV_CHANNEL_ORDER_NATIVE"/>, this is
    /// a bitmask of channel positions (e.g., FL|FR for stereo = 0x3).
    /// </summary>
    public ulong U;

    /// <summary>Opaque pointer for custom channel order data.</summary>
    public nint Opaque;
}
