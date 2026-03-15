using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVIOContext
{
    public nint AvClass;            // const AVClass*
    // This is an opaque struct — we only use pointers to it, never access fields directly.
    // The full layout is not needed for P/Invoke usage.
}
