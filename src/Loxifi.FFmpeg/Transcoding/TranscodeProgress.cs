// TranscodeProgress.cs — Progress reporting data for transcoding operations.

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Reports the progress of a transcoding operation, including wall-clock time elapsed,
/// the current position within the media, the total duration, and a completion percentage.
/// </summary>
/// <param name="Elapsed">Wall-clock time elapsed since transcoding started.</param>
/// <param name="Position">Current position within the media being transcoded.</param>
/// <param name="Duration">Total duration of the input media.</param>
/// <param name="Percent">Completion percentage (0-100), clamped to valid range.</param>
public readonly record struct TranscodeProgress(
    TimeSpan Elapsed,
    TimeSpan Position,
    TimeSpan Duration,
    double Percent);
