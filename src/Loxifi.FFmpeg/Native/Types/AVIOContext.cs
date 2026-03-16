// AVIOContext.cs — Minimal struct mapping for FFmpeg's AVIOContext.
// AVIOContext is FFmpeg's I/O abstraction layer. We only map the first field (AvClass)
// because this struct is used exclusively as a pointer type in our P/Invoke calls.
// The full struct has dozens of fields, but we never access them directly.

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Minimal representation of FFmpeg's <c>AVIOContext</c> struct.
/// This is treated as an opaque type — we only use pointers to it (<c>AVIOContext*</c>)
/// and never access fields directly. The struct exists so we can declare typed pointers
/// in <see cref="AVFormatContext.Pb"/> and in <see cref="StreamIOContext"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVIOContext
{
    /// <summary>Pointer to the AVClass for logging (first field in all FFmpeg structs).</summary>
    public nint AvClass;
}
