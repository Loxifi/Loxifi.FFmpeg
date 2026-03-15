using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Helpers;

public partial class FFmpegException : Exception
{
    public int ErrorCode { get; }

    public FFmpegException(int errorCode)
        : base(GetErrorMessage(errorCode))
    {
        ErrorCode = errorCode;
    }

    public FFmpegException(int errorCode, string context)
        : base($"{context}: {GetErrorMessage(errorCode)}")
    {
        ErrorCode = errorCode;
    }

    public static unsafe string GetErrorMessage(int errorCode)
    {
        byte* buffer = stackalloc byte[1024];
        _ = av_strerror(errorCode, buffer, 1024);
        return Marshal.PtrToStringUTF8((nint)buffer) ?? $"Unknown error {errorCode}";
    }

    public static void ThrowIfError(int result, string? context = null)
    {
        if (result < 0)
        {
            throw context is not null
                ? new FFmpegException(result, context)
                : new FFmpegException(result);
        }
    }

    [LibraryImport("avutil", EntryPoint = "av_strerror")]
    private static unsafe partial int av_strerror(int errnum, byte* errbuf, nuint errbuf_size);
}
