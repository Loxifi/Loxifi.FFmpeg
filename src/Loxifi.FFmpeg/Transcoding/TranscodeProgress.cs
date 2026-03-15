namespace Loxifi.FFmpeg.Transcoding;

public readonly record struct TranscodeProgress(
    TimeSpan Elapsed,
    TimeSpan Position,
    TimeSpan Duration,
    double Percent);
