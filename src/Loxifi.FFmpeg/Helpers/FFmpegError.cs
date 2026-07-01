// FFmpegError.cs — Exception type for FFmpeg errors with human-readable messages.
// Wraps FFmpeg's integer error codes (negative values) into .NET exceptions,
// using av_strerror to convert the error code to a descriptive string.

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Helpers;

/// <summary>
/// Exception thrown when an FFmpeg function returns a negative error code.
/// Converts the error code to a human-readable message using FFmpeg's <c>av_strerror</c>.
/// </summary>
public partial class FFmpegException : Exception
{
    /// <summary>The FFmpeg error code (negative integer) that caused this exception.</summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Creates an exception from an FFmpeg error code.
    /// </summary>
    /// <param name="errorCode">The negative FFmpeg error code.</param>
    public FFmpegException(int errorCode)
        : base(Compose(null, errorCode))
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates an exception from an FFmpeg error code with additional context.
    /// </summary>
    /// <param name="errorCode">The negative FFmpeg error code.</param>
    /// <param name="context">Description of the operation that failed (e.g., "Failed to open input").</param>
    public FFmpegException(int errorCode, string context)
        : base(Compose(context, errorCode))
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Builds the exception message: the (optional) context, FFmpeg's error-code string, and —
    /// when available — the underlying diagnostic captured from FFmpeg's log. The log is what
    /// turns an opaque code like AVERROR_EXTERNAL ("Generic error in an external library") into
    /// the codec's actual complaint (e.g. libx264's reason for refusing to open).
    /// </summary>
    private static string Compose(string? context, int errorCode)
    {
        string baseMsg = context is not null
            ? $"{context}: {GetErrorMessage(errorCode)}"
            : GetErrorMessage(errorCode);

        string? log = FFmpegLog.Consume();
        return log is null ? baseMsg : $"{baseMsg} [ffmpeg: {log}]";
    }

    /// <summary>
    /// Converts an FFmpeg error code to a human-readable string using <c>av_strerror</c>.
    /// </summary>
    /// <param name="errorCode">The negative FFmpeg error code.</param>
    /// <returns>A descriptive error message, or a fallback string if av_strerror fails.</returns>
    public static unsafe string GetErrorMessage(int errorCode)
    {
        byte* buffer = stackalloc byte[1024];
        _ = av_strerror(errorCode, buffer, 1024);
        return Marshal.PtrToStringUTF8((nint)buffer) ?? $"Unknown error {errorCode}";
    }

    /// <summary>
    /// Throws an <see cref="FFmpegException"/> if <paramref name="result"/> is negative (error).
    /// This is the primary error-checking pattern used throughout the library.
    /// </summary>
    /// <param name="result">The return value from an FFmpeg function.</param>
    /// <param name="context">Optional description of the operation for the error message.</param>
    public static void ThrowIfError(int result, string? context = null)
    {
        if (result < 0)
        {
            throw context is not null
                ? new FFmpegException(result, context)
                : new FFmpegException(result);
        }
    }

    /// <summary>
    /// P/Invoke for av_strerror. Declared here (rather than in AVUtil.cs) to keep the
    /// error handling self-contained and avoid circular initialization issues.
    /// </summary>
    [LibraryImport("avutil", EntryPoint = "av_strerror")]
    private static unsafe partial int av_strerror(int errnum, byte* errbuf, nuint errbuf_size);
}
