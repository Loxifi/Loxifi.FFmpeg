using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Transcoding;

public class StreamInfo
{
    public int Index { get; init; }
    public AVMediaType MediaType { get; init; }
    public AVCodecID CodecId { get; init; }
    public long BitRate { get; init; }

    // Video properties
    public int Width { get; init; }
    public int Height { get; init; }
    public AVPixelFormat PixelFormat { get; init; }
    public double FrameRate { get; init; }

    // Audio properties
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public AVSampleFormat SampleFormat { get; init; }

    public TimeSpan Duration { get; init; }
    public AVRational TimeBase { get; init; }
}
