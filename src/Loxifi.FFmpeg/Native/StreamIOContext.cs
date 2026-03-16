// StreamIOContext.cs — Bridges .NET Streams to FFmpeg's AVIOContext for custom I/O.
// FFmpeg normally reads/writes files via its own I/O layer (avio_open). This class
// creates a custom AVIOContext backed by a .NET Stream, enabling in-memory transcoding
// without touching the filesystem. It uses GCHandle pinning and unmanaged delegate
// callbacks to allow FFmpeg's C code to call back into managed .NET methods.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// Wraps a .NET <see cref="Stream"/> as an FFmpeg <c>AVIOContext</c>, enabling
/// custom I/O for reading from or writing to arbitrary streams (memory, network, etc.)
/// instead of filesystem paths.
/// </summary>
/// <remarks>
/// <para>
/// This class pins itself via <see cref="GCHandle"/> so FFmpeg can store a stable pointer
/// to it as the "opaque" parameter in callback functions. The callbacks (read, write, seek)
/// are static methods that recover the <c>StreamIOContext</c> instance from the opaque pointer.
/// </para>
/// <para>
/// The delegate fields (<c>_readDelegate</c>, etc.) must be kept alive as instance fields to
/// prevent the garbage collector from collecting them while FFmpeg holds unmanaged function
/// pointers to them.
/// </para>
/// <para>
/// The internal buffer is allocated with <c>av_malloc</c> because FFmpeg may reallocate or
/// free it internally. Using managed memory or <c>Marshal.AllocHGlobal</c> would cause
/// heap corruption when FFmpeg calls <c>av_free</c> on the buffer.
/// </para>
/// </remarks>
public sealed unsafe class StreamIOContext : IDisposable
{
    /// <summary>Internal buffer size for AVIO reads/writes. 32KB is the typical default.</summary>
    private const int BufferSize = 32768;

    /// <summary>The underlying .NET stream that FFmpeg reads from or writes to.</summary>
    private readonly Stream _stream;

    /// <summary>Whether this context is for writing (true) or reading (false).</summary>
    private readonly bool _writable;

    /// <summary>
    /// GCHandle pinning this instance so the opaque pointer passed to FFmpeg remains valid.
    /// Without this, the GC could relocate this object, invalidating the pointer.
    /// </summary>
    private GCHandle _gcHandle;

    /// <summary>The FFmpeg AVIOContext created by avio_alloc_context.</summary>
    private AVIOContext* _avioCtx;

    /// <summary>
    /// Buffer allocated with av_malloc. Owned by the AVIOContext after creation;
    /// freed automatically by avio_context_free.
    /// </summary>
    private byte* _buffer;

    private bool _disposed;

    // These delegate fields prevent the GC from collecting the delegates while FFmpeg
    // holds unmanaged function pointers to them. If these were local variables, the GC
    // could collect them and the callback would crash with an access violation.
    private readonly ReadPacketDelegate? _readDelegate;
    private readonly WritePacketDelegate? _writeDelegate;
    private readonly SeekDelegate? _seekDelegate;

    /// <summary>FFmpeg read_packet callback signature: int read_packet(void* opaque, uint8_t* buf, int buf_size).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ReadPacketDelegate(nint opaque, byte* buf, int bufSize);

    /// <summary>FFmpeg write_packet callback signature: int write_packet(void* opaque, const uint8_t* buf, int buf_size).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int WritePacketDelegate(nint opaque, byte* buf, int bufSize);

    /// <summary>FFmpeg seek callback signature: int64_t seek(void* opaque, int64_t offset, int whence).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long SeekDelegate(nint opaque, long offset, int whence);

    private StreamIOContext(Stream stream, bool writable)
    {
        _stream = stream;
        _writable = writable;

        // Pin this object so FFmpeg can safely call back via the opaque pointer
        // for the entire lifetime of the AVIOContext.
        _gcHandle = GCHandle.Alloc(this);

        // Allocate the I/O buffer with av_malloc. This is required because FFmpeg's
        // avio_context_free will call av_free on this buffer, so it must come from
        // FFmpeg's allocator, not from .NET's heap or Marshal.AllocHGlobal.
        _buffer = (byte*)AVUtil.av_malloc(BufferSize);
        if (_buffer == null)
            throw new OutOfMemoryException("Failed to allocate AVIO buffer");

        // Create managed delegates and convert to unmanaged function pointers.
        // Only create the relevant callback (read for input, write for output).
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

        // avio_alloc_context takes ownership of the buffer. The opaque pointer
        // (GCHandle.ToIntPtr) is passed to every callback as the first argument.
        _avioCtx = AVFormat.avio_alloc_context(
            _buffer,
            BufferSize,
            writable ? 1 : 0,          // write_flag: 1 for output, 0 for input
            GCHandle.ToIntPtr(_gcHandle), // opaque: passed to all callbacks
            readPtr,
            writePtr,
            seekPtr);

        if (_avioCtx == null)
            throw new InvalidOperationException("Failed to allocate AVIOContext");
    }

    /// <summary>
    /// Creates a read-only AVIO context backed by the given stream.
    /// </summary>
    /// <param name="stream">A readable .NET stream to use as the input source.</param>
    /// <returns>A new <see cref="StreamIOContext"/> configured for reading.</returns>
    /// <exception cref="ArgumentException">Thrown if the stream is not readable.</exception>
    public static StreamIOContext ForReading(Stream stream)
    {
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable", nameof(stream));
        return new StreamIOContext(stream, false);
    }

    /// <summary>
    /// Creates a write-only AVIO context backed by the given stream.
    /// </summary>
    /// <param name="stream">A writable .NET stream to use as the output destination.</param>
    /// <returns>A new <see cref="StreamIOContext"/> configured for writing.</returns>
    /// <exception cref="ArgumentException">Thrown if the stream is not writable.</exception>
    public static StreamIOContext ForWriting(Stream stream)
    {
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable", nameof(stream));
        return new StreamIOContext(stream, true);
    }

    /// <summary>
    /// The underlying <c>AVIOContext*</c> pointer. Assign this to
    /// <c>AVFormatContext.Pb</c> before calling <c>avformat_open_input</c>
    /// or <c>avformat_write_header</c>.
    /// </summary>
    public AVIOContext* Context => _avioCtx;

    // ── Callbacks ──
    // These are static methods that recover the StreamIOContext instance from the
    // opaque pointer (GCHandle) passed by FFmpeg. They catch all exceptions because
    // allowing a managed exception to propagate into FFmpeg's C code would crash the process.

    /// <summary>
    /// Read callback invoked by FFmpeg when it needs more data from the input stream.
    /// Returns the number of bytes read, or AVERROR_EOF (-541478725) at end of stream.
    /// </summary>
    private static int ReadPacket(nint opaque, byte* buf, int bufSize)
    {
        var self = (StreamIOContext)GCHandle.FromIntPtr(opaque).Target!;
        try
        {
            var span = new Span<byte>(buf, bufSize);
            int bytesRead = self._stream.Read(span);
            // FFmpeg expects AVERROR_EOF when the stream is exhausted, not 0
            return bytesRead == 0 ? -541478725 /* AVERROR_EOF */ : bytesRead;
        }
        catch
        {
            // Return -1 (generic I/O error) rather than letting the exception propagate
            // into FFmpeg's unmanaged code, which would crash the process.
            return -1;
        }
    }

    /// <summary>
    /// Write callback invoked by FFmpeg when it has data to write to the output stream.
    /// Returns the number of bytes written (always bufSize on success).
    /// </summary>
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

    /// <summary>
    /// Seek callback invoked by FFmpeg to seek within the stream or query its size.
    /// Handles the special AVSEEK_SIZE whence value (0x10000) which asks for total stream length.
    /// </summary>
    private static long Seek(nint opaque, long offset, int whence)
    {
        var self = (StreamIOContext)GCHandle.FromIntPtr(opaque).Target!;

        // FFmpeg uses this non-standard whence value to query the total stream size
        const int AVSEEK_SIZE = 0x10000;

        try
        {
            if (whence == AVSEEK_SIZE)
            {
                return self._stream.CanSeek ? self._stream.Length : -1;
            }

            // Map FFmpeg's whence values (same as POSIX) to .NET SeekOrigin
            var origin = whence switch
            {
                0 => SeekOrigin.Begin,    // SEEK_SET
                1 => SeekOrigin.Current,  // SEEK_CUR
                2 => SeekOrigin.End,      // SEEK_END
                _ => SeekOrigin.Begin,
            };

            return self._stream.Seek(offset, origin);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Releases the AVIOContext, its internal buffer, and the GCHandle.
    /// After disposal, the <see cref="Context"/> pointer is invalid.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_avioCtx != null)
        {
            AVIOContext* ctx = _avioCtx;
            AVFormat.avio_context_free(&ctx);
            _avioCtx = null;
            // avio_context_free also frees the buffer (allocated with av_malloc),
            // so we must not call av_free on _buffer separately.
            _buffer = null;
        }

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }
    }
}
