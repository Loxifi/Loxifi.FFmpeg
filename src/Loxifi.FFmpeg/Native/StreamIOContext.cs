using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// Wraps a .NET Stream as an FFmpeg AVIOContext for custom I/O.
/// Allows reading from or writing to in-memory streams instead of files.
/// </summary>
public sealed unsafe class StreamIOContext : IDisposable
{
    private const int BufferSize = 32768; // 32KB — typical AVIO buffer

    private readonly Stream _stream;
    private readonly bool _writable;
    private GCHandle _gcHandle;
    private AVIOContext* _avioCtx;
    private byte* _buffer;
    private bool _disposed;

    // Must be kept as fields to prevent GC from collecting the delegates
    private readonly ReadPacketDelegate? _readDelegate;
    private readonly WritePacketDelegate? _writeDelegate;
    private readonly SeekDelegate? _seekDelegate;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ReadPacketDelegate(nint opaque, byte* buf, int bufSize);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int WritePacketDelegate(nint opaque, byte* buf, int bufSize);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long SeekDelegate(nint opaque, long offset, int whence);

    private StreamIOContext(Stream stream, bool writable)
    {
        _stream = stream;
        _writable = writable;

        // Pin this object so FFmpeg can call back via the opaque pointer
        _gcHandle = GCHandle.Alloc(this);

        // Allocate buffer with av_malloc (required by FFmpeg — it may realloc/free it)
        _buffer = (byte*)AVUtil.av_malloc(BufferSize);
        if (_buffer == null)
            throw new OutOfMemoryException("Failed to allocate AVIO buffer");

        // Create delegates and get function pointers
        nint readPtr = nint.Zero;
        nint writePtr = nint.Zero;
        nint seekPtr = nint.Zero;

        if (!writable)
        {
            _readDelegate = ReadPacket;
            readPtr = Marshal.GetFunctionPointerForDelegate(_readDelegate);
        }
        else
        {
            _writeDelegate = WritePacket;
            writePtr = Marshal.GetFunctionPointerForDelegate(_writeDelegate);
        }

        if (stream.CanSeek)
        {
            _seekDelegate = Seek;
            seekPtr = Marshal.GetFunctionPointerForDelegate(_seekDelegate);
        }

        _avioCtx = AVFormat.avio_alloc_context(
            _buffer,
            BufferSize,
            writable ? 1 : 0,
            GCHandle.ToIntPtr(_gcHandle),
            readPtr,
            writePtr,
            seekPtr);

        if (_avioCtx == null)
            throw new InvalidOperationException("Failed to allocate AVIOContext");
    }

    /// <summary>
    /// Create a read-only AVIO context from a stream.
    /// </summary>
    public static StreamIOContext ForReading(Stream stream)
    {
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable", nameof(stream));
        return new StreamIOContext(stream, false);
    }

    /// <summary>
    /// Create a write-only AVIO context from a stream.
    /// </summary>
    public static StreamIOContext ForWriting(Stream stream)
    {
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable", nameof(stream));
        return new StreamIOContext(stream, true);
    }

    /// <summary>
    /// The underlying AVIOContext pointer. Assign to AVFormatContext.Pb.
    /// </summary>
    public AVIOContext* Context => _avioCtx;

    // ── Callbacks ──

    private static int ReadPacket(nint opaque, byte* buf, int bufSize)
    {
        var self = (StreamIOContext)GCHandle.FromIntPtr(opaque).Target!;
        try
        {
            var span = new Span<byte>(buf, bufSize);
            int bytesRead = self._stream.Read(span);
            return bytesRead == 0 ? -541478725 /* AVERROR_EOF */ : bytesRead;
        }
        catch
        {
            return -1;
        }
    }

    private static int WritePacket(nint opaque, byte* buf, int bufSize)
    {
        var self = (StreamIOContext)GCHandle.FromIntPtr(opaque).Target!;
        try
        {
            var span = new ReadOnlySpan<byte>(buf, bufSize);
            self._stream.Write(span);
            return bufSize;
        }
        catch
        {
            return -1;
        }
    }

    private static long Seek(nint opaque, long offset, int whence)
    {
        var self = (StreamIOContext)GCHandle.FromIntPtr(opaque).Target!;
        const int AVSEEK_SIZE = 0x10000;

        try
        {
            if (whence == AVSEEK_SIZE)
            {
                return self._stream.CanSeek ? self._stream.Length : -1;
            }

            var origin = whence switch
            {
                0 => SeekOrigin.Begin,
                1 => SeekOrigin.Current,
                2 => SeekOrigin.End,
                _ => SeekOrigin.Begin,
            };

            return self._stream.Seek(offset, origin);
        }
        catch
        {
            return -1;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_avioCtx != null)
        {
            // Flush if writable
            if (_writable)
            {
                // avio_flush is just writing remaining buffer
            }

            AVIOContext* ctx = _avioCtx;
            AVFormat.avio_context_free(&ctx);
            _avioCtx = null;
            // avio_context_free frees the buffer too, so don't av_free it
            _buffer = null;
        }

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }
    }
}
