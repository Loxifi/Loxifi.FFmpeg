# Loxifi.FFmpeg

Cross-platform .NET 8.0 FFmpeg P/Invoke library for media transcoding. Targets Windows (x64), Linux (x64), and Android (arm64).

Ships in two license variants:
- **LGPL** — safe for proprietary/closed-source apps (dynamic linking)
- **GPL** — adds libx264, libx265, libxvid (requires your app to be GPL if distributed)

## Usage

### Mux audio and video together (fixing DASH files)

When you have separate video-only and audio-only files (e.g. from DASH streams), combine them into a single file. This is a stream copy operation — no re-encoding, so it's fast.

```csharp
using Loxifi.FFmpeg.Transcoding;

// Combine separate video and audio files into one MP4
MediaOperations.Mux("video_only.mp4", "audio_only.mp4", "output.mp4");

// Async version
await MediaOperations.MuxAsync("video_only.mp4", "audio_only.mp4", "output.mp4");

// In-memory using streams (no disk I/O — ideal for DASH on Android/mobile)
using var videoStream = GetVideoStream();   // any System.IO.Stream
using var audioStream = GetAudioStream();
using var output = new MemoryStream();
MediaOperations.Mux(videoStream, audioStream, output);
```

### Resize video to target file size (Discord upload limit)

Re-encodes the video to fit within a target file size in bytes. Audio is stream-copied (not re-encoded). The video bitrate is calculated automatically from the target size and duration.

```csharp
using Loxifi.FFmpeg.Transcoding;

// Resize to fit within 25MB (Discord Nitro limit)
long targetSize = 25 * 1024 * 1024; // 25MB in bytes
MediaOperations.ResizeToFileSize("input.mp4", "output.mp4", targetSize);

// With progress reporting
var progress = new Progress<TranscodeProgress>(p =>
    Console.WriteLine($"{p.Percent:F1}% ({p.Position}/{p.Duration})"));

await MediaOperations.ResizeToFileSizeAsync("input.mp4", "output.mp4", targetSize, progress);

// Stream-based (no disk I/O)
using var input = File.OpenRead("input.mp4");
using var output = new MemoryStream();
MediaOperations.ResizeToFileSize(input, output, targetSize);
```

### Convert GIF to MP4

Converts an animated GIF to an MP4 video. MP4 is dramatically smaller and plays better everywhere.

```csharp
using Loxifi.FFmpeg.Transcoding;

MediaOperations.GifToMp4("funny.gif", "funny.mp4");

// Stream-based
using var gif = File.OpenRead("funny.gif");
using var mp4 = File.Create("funny.mp4");
MediaOperations.GifToMp4(gif, mp4);
```

### Codec selection (no magic strings)

All codecs and container formats are strongly typed. Use `LGPL.Video.*` / `LGPL.Audio.*` for LGPL builds, or `GPL.Video.*` / `GPL.Audio.*` for GPL builds:

```csharp
using Loxifi.FFmpeg.Transcoding;
using Loxifi.FFmpeg.Transcoding.Codecs;

// LGPL build — available codecs are discoverable via intellisense
using var transcoder = new MediaTranscoder();
transcoder.Transcode(new TranscodeOptions
{
    InputPath = "input.mkv",
    OutputPath = "output.webm",
    OutputFormat = ContainerFormat.WebM,
    VideoCodec = LGPL.Video.Vp9,
    AudioCodec = LGPL.Audio.Opus,
});

// GPL build — includes all LGPL codecs plus GPL-only ones
transcoder.Transcode(new TranscodeOptions
{
    InputPath = "input.mkv",
    OutputPath = "output.mp4",
    OutputFormat = ContainerFormat.Mp4,
    VideoCodec = GPL.Video.X264,      // H.264 (GPL only)
    AudioCodec = GPL.Audio.Aac,
});

// Stream-based transcoding (no disk I/O)
transcoder.Transcode(new StreamTranscodeOptions
{
    InputStream = inputStream,
    OutputStream = outputStream,
    OutputFormat = ContainerFormat.Mp4,
    VideoCodec = LGPL.Video.Mpeg4,
});
```

### Low-level: Stream copy (remux without re-encoding)

```csharp
using Loxifi.FFmpeg.Transcoding;

using var transcoder = new MediaTranscoder();
transcoder.Transcode(new TranscodeOptions
{
    InputPath = "input.mkv",
    OutputPath = "output.mp4",
    // VideoCodec = null (default) means stream copy
    // AudioCodec = null (default) means stream copy
});
```

### Low-level: Probe file metadata

```csharp
using Loxifi.FFmpeg.Transcoding;

MediaInfo info = MediaInfo.Probe("video.mp4");
Console.WriteLine($"Duration: {info.Duration}");
Console.WriteLine($"Video: {info.VideoStream?.Width}x{info.VideoStream?.Height}");
Console.WriteLine($"Audio: {info.AudioStream?.SampleRate}Hz, {info.AudioStream?.Channels}ch");
```

## NuGet Packages

| Package | License | Contents |
|---------|---------|----------|
| `Loxifi.FFmpeg` | MIT | Core library (P/Invoke + managed API) |
| `Loxifi.FFmpeg.Runtime.linux-x64` | LGPL | Linux x64 native binaries |
| `Loxifi.FFmpeg.Runtime.win-x64` | LGPL | Windows x64 native binaries |
| `Loxifi.FFmpeg.Runtime.android-arm64` | LGPL | Android arm64 native binaries |
| `Loxifi.FFmpeg.Runtime.linux-x64.GPL` | GPL | Linux x64 native binaries (+ libx264, libx265) |
| `Loxifi.FFmpeg.Runtime.win-x64.GPL` | GPL | Windows x64 native binaries (+ libx264, libx265) |
| `Loxifi.FFmpeg.Runtime.android-arm64.GPL` | GPL | Android arm64 native binaries (+ libx264, libx265) |

All runtime packages include the same 14 codec libraries (17 for GPL). See below for the full list.

## Bundled Codecs

All platforms are built from source with identical configuration for full parity.

| Codec | Type | License | Description |
|-------|------|---------|-------------|
| libaom-av1 | Video | BSD | AV1 (reference encoder) |
| libsvtav1 | Video | BSD | AV1 (fast encoder) |
| libvpx / libvpx-vp9 | Video | BSD | VP8 / VP9 |
| libopenh264 | Video | BSD | H.264 (Cisco) |
| libtheora | Video | BSD | Theora |
| libwebp | Video | BSD | WebP |
| mpeg4 | Video | built-in | MPEG-4 Part 2 |
| libmp3lame | Audio | LGPL | MP3 |
| libopus | Audio | BSD | Opus |
| libvorbis | Audio | BSD | Vorbis |
| aac | Audio | built-in | AAC |
| **libx264** | Video | **GPL** | H.264/AVC (industry standard) |
| **libx265** | Video | **GPL** | H.265/HEVC |
| **libxvid** | Video | **GPL** | MPEG-4 ASP |

Additional utility libraries: libdav1d (AV1 decoder), libopencore-amr (AMR voice), libopenjpeg (JPEG 2000), libzimg (image scaling).

## Building Native Libraries

All platforms are built from source using `scripts/build-ffmpeg.sh`:

```bash
# Build all 6 combinations (LGPL + GPL × 3 platforms)
./scripts/build-ffmpeg.sh both all

# Build specific license/platform
./scripts/build-ffmpeg.sh gpl linux-x64
./scripts/build-ffmpeg.sh lgpl android-arm64
```

**Prerequisites:**
- Android NDK r27c: `sdkmanager "ndk;27.2.12479018"`
- mingw-w64 for Windows cross-compile: `apt install gcc-mingw-w64-x86-64 g++-mingw-w64-x86-64`
- Host tools: `gcc g++ make cmake nasm pkg-config autoconf automake libtool git meson ninja-build`

The script cross-compiles all 17 codec dependencies from source for each platform, then builds FFmpeg against them. This ensures identical codec support across all platforms.

### Soname version mapping (FFmpeg 7.1)

| Library | Linux/Windows soname | Android soname |
|---------|---------------------|----------------|
| libavformat | 61 | unversioned |
| libavcodec | 61 | unversioned |
| libavutil | 59 | unversioned |
| libswscale | 8 | unversioned |
| libswresample | 5 | unversioned |

These version numbers are coded into `LibraryLoader.cs` and must be updated when changing FFmpeg major versions.

### Updating to a new FFmpeg version

1. Update `FFMPEG_VERSION` in `scripts/build-ffmpeg.sh`
2. Run `./scripts/build-ffmpeg.sh both all`
3. Update soname versions in `src/Loxifi.FFmpeg/Native/LibraryLoader.cs` if changed
4. Check for struct layout changes in the FFmpeg headers (`src/Loxifi.FFmpeg/Native/Types/`)
5. Run `./test.sh` to verify all 46 tests pass

## Building the .NET Projects

```bash
./build.sh           # Release build (default)
./build.sh Debug     # Debug build
```

Set `ANDROID_SDK_DIR` and `JAVA_HOME` environment variables for Android test project builds.

## Testing

Run all 46 tests across both license variants and both platforms:

```bash
./test.sh
```

This runs:
- Desktop LGPL (15 tests)
- Desktop GPL (15 tests)
- Android LGPL (8 tests) — requires connected ARM64 device
- Android GPL (8 tests) — requires connected ARM64 device

To run desktop tests only:
```bash
dotnet test tests/Loxifi.FFmpeg.Tests/ -c Release                     # LGPL
dotnet test tests/Loxifi.FFmpeg.Tests/ -c Release -p:UseGPL=true      # GPL
```

## License

The core `Loxifi.FFmpeg` library is MIT-licensed. The native FFmpeg binaries are available in two license variants:

- **LGPL v3** — `Loxifi.FFmpeg.Runtime.{platform}` packages. Safe for proprietary apps using dynamic linking.
- **GPL v3** — `Loxifi.FFmpeg.Runtime.{platform}.GPL` packages. Adds libx264/libx265/libxvid. Requires your distributed app to be GPL-licensed.

Choose the runtime package that matches your licensing requirements.
