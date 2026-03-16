// MediaInfo.cs — Probes media files to extract duration, bitrate, and stream information.
// Uses FFmpeg's avformat_open_input and avformat_find_stream_info to read container
// metadata without decoding any frames.

using System.Runtime.InteropServices;
using Loxifi.FFmpeg.Helpers;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Native.Types;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Contains metadata about a media file: total duration, bitrate, and per-stream
/// information (codec, resolution, sample rate, etc.). Use <see cref="Probe"/> to
/// analyze a file.
/// </summary>
public class MediaInfo
{
    /// <summary>Total duration of the media file.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Total bitrate in bits/second. May be 0 if unknown.</summary>
    public long BitRate { get; init; }

    /// <summary>List of all streams found in the container.</summary>
    public IReadOnlyList<StreamInfo> Streams { get; init; } = [];

    /// <summary>The first video stream, or null if none exists.</summary>
    public StreamInfo? VideoStream => Streams.FirstOrDefault(s => s.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO);

    /// <summary>The first audio stream, or null if none exists.</summary>
    public StreamInfo? AudioStream => Streams.FirstOrDefault(s => s.MediaType == AVMediaType.AVMEDIA_TYPE_AUDIO);

    /// <summary>
    /// Probes a media file and returns its metadata without decoding any frames.
    /// Opens the file, reads the container header and stream info, then closes it.
    /// </summary>
    /// <param name="filePath">Path to the media file to probe.</param>
    /// <returns>A <see cref="MediaInfo"/> instance with the file's metadata.</returns>
    /// <exception cref="FFmpegException">Thrown if the file cannot be opened or probed.</exception>
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

            AVStream** streamsPtr = (AVStream**)fmtCtx->Streams;

            for (uint i = 0; i < fmtCtx->NbStreams; i++)
            {
                AVStream* stream = streamsPtr[i];
                AVCodecParameters* codecpar = stream->Codecpar;

                // Calculate stream duration from the stream's own duration and timebase
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

            // Container-level duration is in AV_TIME_BASE (microseconds)
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
