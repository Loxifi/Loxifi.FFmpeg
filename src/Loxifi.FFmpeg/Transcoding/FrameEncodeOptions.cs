// FrameEncodeOptions.cs — Configuration for encoding a video from frames held in memory.
// Unlike TranscodeOptions, there is no input file: the caller supplies raw pixel buffers.

using Loxifi.FFmpeg.Native.Types;
using Loxifi.FFmpeg.Transcoding.Codecs;

namespace Loxifi.FFmpeg.Transcoding;

/// <summary>
/// Configuration for <see cref="VideoFrameEncoder"/> — encoding a video from raw frames the caller
/// already holds, rather than from an input file.
///
/// <para>This exists because some sources cannot be handed to FFmpeg as a file at all. The motivating
/// case is an animated WebP: FFmpeg's WebP decoder reads only the FIRST frame, so the frames have to be
/// decoded by something else and pushed in one at a time.</para>
/// </summary>
public class FrameEncodeOptions
{
    /// <summary>Width of the frames the caller will supply, in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Height of the frames the caller will supply, in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>
    /// Frames per second. Used as the encoder timebase (1/fps), so it also determines playback speed —
    /// a value that does not match the source re-times the output.
    /// </summary>
    public required double FrameRate { get; init; }

    /// <summary>
    /// Pixel format of the buffers passed to <see cref="VideoFrameEncoder.WriteFrame"/>. Defaults to
    /// packed 8-bit RGBA. Converted to the encoder's format by libswscale.
    /// </summary>
    public AVPixelFormat SourcePixelFormat { get; init; } = AVPixelFormat.AV_PIX_FMT_RGBA;

    /// <summary>
    /// Video encoder. Defaults to H.264 via x264, which needs a GPL runtime package; use
    /// <see cref="LGPL.Video.OpenH264"/> or <see cref="LGPL.Video.Vp9"/> on an LGPL build.
    /// </summary>
    public VideoCodec VideoCodec { get; init; } = GPL.Video.X264;

    /// <summary>
    /// Pixel format to encode in. Defaults to YUV420P, which is what H.264 decoders in browsers
    /// universally accept.
    /// </summary>
    public AVPixelFormat EncoderPixelFormat { get; init; } = AVPixelFormat.AV_PIX_FMT_YUV420P;

    /// <summary>Output container. Defaults to MP4.</summary>
    public ContainerFormat OutputFormat { get; init; } = ContainerFormat.Mp4;

    /// <summary>Target bitrate in bits per second. 0 leaves it to the codec, or to <see cref="Quality"/>.</summary>
    public long BitRate { get; init; }

    /// <summary>
    /// Constant rate factor — the quality knob for x264/x265, roughly 0 (lossless) to 51 (worst),
    /// with 18-23 the usual range. Null leaves the encoder's default. Ignored by encoders with no
    /// <c>crf</c> option.
    /// </summary>
    public int? Quality { get; init; }

    /// <summary>
    /// Encoder speed preset (x264/x265: <c>ultrafast</c> … <c>veryslow</c>). Null leaves the default.
    /// </summary>
    public string? Preset { get; init; }

    /// <summary>
    /// Write a FRAGMENTED container. For MP4 this sets
    /// <c>movflags=+frag_keyframe+empty_moov+default_base_moof</c>, which is what lets the result be
    /// written to a non-seekable stream and played before it is complete. Without it the muxer must
    /// seek back to fill in the <c>moov</c> atom, so encoding straight to a network stream produces a
    /// file no player will open.
    /// </summary>
    public bool Fragmented { get; init; }

    /// <summary>
    /// Extra private options passed to the encoder (FFmpeg's <c>-opt value</c>), applied after
    /// <see cref="Quality"/> and <see cref="Preset"/> so they can override them. Example:
    /// <c>{ ["tune"] = "animation" }</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CodecOptions { get; init; }

    /// <summary>
    /// Extra options passed to the muxer, applied after <see cref="Fragmented"/> so they can override
    /// it. Example: <c>{ ["movflags"] = "+faststart" }</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? MuxerOptions { get; init; }
}
