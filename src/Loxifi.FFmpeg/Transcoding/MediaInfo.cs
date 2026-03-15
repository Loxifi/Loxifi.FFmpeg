using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Helpers;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Transcoding;

public class MediaInfo
{
    public TimeSpan Duration { get; init; }
    public long BitRate { get; init; }
    public IReadOnlyList<StreamInfo> Streams { get; init; } = [];

    public StreamInfo? VideoStream => Streams.FirstOrDefault(s => s.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO);
    public StreamInfo? AudioStream => Streams.FirstOrDefault(s => s.MediaType == AVMediaType.AVMEDIA_TYPE_AUDIO);

    public static unsafe MediaInfo Probe(string filePath)
    {
        AVFormatContext* fmtCtx = null;

        try
        {
            nint urlPtr = Marshal.StringToHGlobalAnsi(filePath);
            try
            {
                int ret = AVFormat.avformat_open_input(&fmtCtx, (byte*)urlPtr, nint.Zero, null);
                FFmpegException.ThrowIfError(ret, "Failed to open input");
            }
            finally
            {
                Marshal.FreeHGlobal(urlPtr);
            }

            FFmpegException.ThrowIfError(
                AVFormat.avformat_find_stream_info(fmtCtx, null),
                "Failed to find stream info");

            List<StreamInfo> streams = new((int)fmtCtx->NbStreams);

            // Streams is AVStream** stored as nint; dereference to get AVStream*[]
            AVStream** streamsPtr = (AVStream**)fmtCtx->Streams;

            for (uint i = 0; i < fmtCtx->NbStreams; i++)
            {
                AVStream* stream = streamsPtr[i];
                AVCodecParameters* codecpar = stream->Codecpar;

                TimeSpan streamDuration = stream->Duration > 0
                    ? TimeSpan.FromSeconds(stream->Duration * stream->TimeBase.ToDouble())
                    : TimeSpan.Zero;

                streams.Add(new StreamInfo
                {
                    Index = stream->Index,
                    MediaType = codecpar->CodecType,
                    CodecId = codecpar->CodecId,
                    BitRate = codecpar->BitRate,
                    Width = codecpar->Width,
                    Height = codecpar->Height,
                    PixelFormat = (AVPixelFormat)codecpar->Format,
                    FrameRate = stream->AvgFrameRate.ToDouble(),
                    SampleRate = codecpar->SampleRate,
                    Channels = codecpar->ChLayout.NbChannels,
                    SampleFormat = (AVSampleFormat)codecpar->Format,
                    Duration = streamDuration,
                    TimeBase = stream->TimeBase,
                });
            }

            TimeSpan duration = fmtCtx->Duration > 0
                ? TimeSpan.FromMicroseconds(fmtCtx->Duration)
                : TimeSpan.Zero;

            return new MediaInfo
            {
                Duration = duration,
                BitRate = fmtCtx->BitRate,
                Streams = streams,
            };
        }
        finally
        {
            if (fmtCtx != null)
            {
                AVFormat.avformat_close_input(&fmtCtx);
            }
        }
    }
}
