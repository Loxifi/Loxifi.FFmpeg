// FFmpegLog.cs — Captures FFmpeg/codec log output via a custom av_log callback so the
// real reason behind a failure can be attached to the thrown exception. FFmpeg reports
// many failures as a bare error code (e.g. avcodec_open2 returning AVERROR_EXTERNAL,
// "Generic error in an external library", when libx264's x264_encoder_open refuses the
// configuration). The actual explanation is only ever printed through av_log and would
// otherwise be lost — especially on Android, where native stderr isn't surfaced.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Native;

namespace Loxifi.FFmpeg.Helpers;

/// <summary>
/// Installs a global FFmpeg log callback and retains the most recent warning/error lines
/// per-thread, so <see cref="FFmpegException"/> can include the underlying diagnostic that
/// FFmpeg (or a codec such as libx264) emitted right before returning an error code.
/// </summary>
public static unsafe class FFmpegLog
{
    /// <summary>AV_LOG_WARNING. Capture warnings and everything more severe (error/fatal/panic);
    /// lower numeric values are more severe in FFmpeg's scheme.</summary>
    private const int CaptureThreshold = 24;

    /// <summary>Maximum lines retained per thread before the oldest is dropped.</summary>
    private const int MaxLines = 16;

    // The delegate must stay reachable for the whole process: FFmpeg keeps the raw function
    // pointer, so if the delegate were collected the thunk would dangle.
    private static readonly LogCallback _callback = OnLog;
    private static readonly object _installLock = new();
    private static bool _installed;

    // Recent captured lines for the current thread. The codec that fails logs synchronously
    // on the same thread that calls into FFmpeg (e.g. avcodec_open2), so per-thread capture
    // reliably ties the diagnostic to the operation that then throws.
    [ThreadStatic] private static List<string>? _recent;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogCallback(nint avcl, int level, nint fmt, nint vl);

    /// <summary>
    /// Installs the capture callback once. Idempotent and exception-safe: if the loaded
    /// FFmpeg build lacks the required entry points, logging is simply left untouched.
    /// </summary>
    public static void EnsureInstalled()
    {
        if (_installed) return;
        lock (_installLock)
        {
            if (_installed) return;
            try
            {
                AVUtil.av_log_set_callback(Marshal.GetFunctionPointerForDelegate(_callback));
            }
            catch
            {
                // Best-effort: leave FFmpeg's default callback in place.
            }
            _installed = true;
        }
    }

    /// <summary>Discards any lines captured on the current thread. Call at the start of an
    /// operation so a later failure isn't attributed stale diagnostics from a reused thread.</summary>
    public static void Reset() => _recent?.Clear();

    /// <summary>
    /// Returns the warning/error lines captured on the current thread (joined into one string)
    /// and clears them, or <c>null</c> if nothing was captured.
    /// </summary>
    public static string? Consume()
    {
        List<string>? lines = _recent;
        if (lines is null || lines.Count == 0) return null;
        string joined = string.Join(" | ", lines);
        lines.Clear();
        return joined;
    }

    private static void OnLog(nint avcl, int level, nint fmt, nint vl)
    {
        try
        {
            // A custom callback receives every message regardless of av_log_set_level, so we
            // filter here and keep only what's useful for explaining a failure.
            if (level > CaptureThreshold) return;

            byte* buffer = stackalloc byte[1024];
            int printPrefix = 1;
            AVUtil.av_log_format_line2(avcl, level, (byte*)fmt, vl, buffer, 1024, &printPrefix);

            string? msg = Marshal.PtrToStringUTF8((nint)buffer)?.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            List<string> lines = _recent ??= new List<string>(MaxLines);
            if (lines.Count >= MaxLines) lines.RemoveAt(0);
            lines.Add(msg);
        }
        catch
        {
            // A log callback invoked from native code must never let an exception propagate.
        }
    }
}
