# Loxifi.FFmpeg

Cross-platform .NET 8.0 FFmpeg P/Invoke library for media transcoding. Targets Windows (x64), Linux (x64), and Android (arm64).

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
```

> **Note:** `ResizeToFileSize` uses the `mpeg4` encoder (LGPL) by default. For better quality with a GPL build, use the low-level API with `GPL.Video.X264`.

### Codec selection (no magic strings)

All codecs are strongly typed. Use `LGPL.Video.*` / `LGPL.Audio.*` for LGPL builds, or `GPL.Video.*` / `GPL.Audio.*` for GPL builds:

```csharp
using Loxifi.FFmpeg.Transcoding;
using Loxifi.FFmpeg.Transcoding.Codecs;

// LGPL build — available codecs are discoverable via intellisense
using var transcoder = new MediaTranscoder();
transcoder.Transcode(new TranscodeOptions
{
    InputPath = "input.mkv",
    OutputPath = "output.mp4",
    VideoCodec = LGPL.Video.SvtAv1,   // AV1 encoder
    AudioCodec = LGPL.Audio.Opus,     // Opus audio
});

// GPL build — includes all LGPL codecs plus GPL-only ones
transcoder.Transcode(new TranscodeOptions
{
    InputPath = "input.mkv",
    OutputPath = "output.mp4",
    VideoCodec = GPL.Video.X264,      // H.264 (GPL only)
    AudioCodec = GPL.Audio.Aac,       // AAC
});
```

### Convert GIF to MP4

Converts an animated GIF to an MP4 video. Useful because MP4 is dramatically smaller and plays better everywhere.

```csharp
using Loxifi.FFmpeg.Transcoding;

MediaOperations.GifToMp4("funny.gif", "funny.mp4");

// Async version
await MediaOperations.GifToMp4Async("funny.gif", "funny.mp4");
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

## Native Library Sources

This project bundles FFmpeg 7.1 shared libraries. Below are the exact sources and steps to reproduce each platform's binaries.

### Linux x64 and Windows x64

**Source:** Pre-built from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds)

**Version:** FFmpeg n7.1 LGPL shared

**Downloads:**
- Linux x64: `ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1.tar.xz`
- Windows x64: `ffmpeg-n7.1-latest-win64-lgpl-shared-7.1.zip`

**Direct URLs:**
- https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1.tar.xz
- https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n7.1-latest-win64-lgpl-shared-7.1.zip

**Steps to update Linux x64:**
```bash
curl -L -o linux64.tar.xz "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1.tar.xz"
tar xf linux64.tar.xz
# Copy versioned .so files (the real files, not symlinks) into the runtime package:
cp -L ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1/lib/libavformat.so.61 src/Loxifi.FFmpeg.Runtime.linux-x64/runtimes/linux-x64/native/
cp -L ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1/lib/libavcodec.so.61  src/Loxifi.FFmpeg.Runtime.linux-x64/runtimes/linux-x64/native/
cp -L ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1/lib/libavutil.so.59   src/Loxifi.FFmpeg.Runtime.linux-x64/runtimes/linux-x64/native/
cp -L ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1/lib/libswscale.so.8   src/Loxifi.FFmpeg.Runtime.linux-x64/runtimes/linux-x64/native/
cp -L ffmpeg-n7.1-latest-linux64-lgpl-shared-7.1/lib/libswresample.so.5 src/Loxifi.FFmpeg.Runtime.linux-x64/runtimes/linux-x64/native/
```

**Steps to update Windows x64:**
```bash
curl -L -o win64.zip "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n7.1-latest-win64-lgpl-shared-7.1.zip"
unzip win64.zip
# Copy versioned DLLs into the runtime package:
cp ffmpeg-n7.1-latest-win64-lgpl-shared-7.1/bin/avformat-61.dll   src/Loxifi.FFmpeg.Runtime.win-x64/runtimes/win-x64/native/
cp ffmpeg-n7.1-latest-win64-lgpl-shared-7.1/bin/avcodec-61.dll    src/Loxifi.FFmpeg.Runtime.win-x64/runtimes/win-x64/native/
cp ffmpeg-n7.1-latest-win64-lgpl-shared-7.1/bin/avutil-59.dll     src/Loxifi.FFmpeg.Runtime.win-x64/runtimes/win-x64/native/
cp ffmpeg-n7.1-latest-win64-lgpl-shared-7.1/bin/swscale-8.dll     src/Loxifi.FFmpeg.Runtime.win-x64/runtimes/win-x64/native/
cp ffmpeg-n7.1-latest-win64-lgpl-shared-7.1/bin/swresample-5.dll  src/Loxifi.FFmpeg.Runtime.win-x64/runtimes/win-x64/native/
```

### Android arm64

**Source:** Cross-compiled from [FFmpeg 7.1 source](https://ffmpeg.org/releases/ffmpeg-7.1.tar.xz) using Android NDK r27c

**License:** LGPL v2.1 or later (no `--enable-gpl` or `--enable-nonfree` flags)

**Prerequisites:**
- Android NDK r27c (install via `sdkmanager "ndk;27.2.12479018"`)
- GCC (host compiler for FFmpeg build tools)

**Steps to build from source:**

```bash
# 1. Download FFmpeg 7.1 source
curl -L -o ffmpeg-7.1.tar.xz "https://ffmpeg.org/releases/ffmpeg-7.1.tar.xz"
tar xf ffmpeg-7.1.tar.xz
cd ffmpeg-7.1

# 2. Set up NDK toolchain symlinks
#    The NDK clang wrappers use relative paths internally, so we need
#    symlinks that include the actual clang binary alongside the wrappers.
NDK=/path/to/android-sdk/ndk/27.2.12479018
TOOLCHAIN=$NDK/toolchains/llvm/prebuilt/linux-x86_64
mkdir -p /tmp/ndk-bin
ln -sf $TOOLCHAIN/bin/aarch64-linux-android21-clang   /tmp/ndk-bin/aarch64-clang
ln -sf $TOOLCHAIN/bin/aarch64-linux-android21-clang++ /tmp/ndk-bin/aarch64-clang++
ln -sf $TOOLCHAIN/bin/clang                           /tmp/ndk-bin/clang
ln -sf $TOOLCHAIN/bin/clang++                         /tmp/ndk-bin/clang++
ln -sf $TOOLCHAIN/bin/llvm-nm                         /tmp/ndk-bin/
ln -sf $TOOLCHAIN/bin/llvm-ar                         /tmp/ndk-bin/
ln -sf $TOOLCHAIN/bin/llvm-ranlib                     /tmp/ndk-bin/
ln -sf $TOOLCHAIN/bin/llvm-strip                      /tmp/ndk-bin/

# 3. Configure (LGPL, shared, no programs/docs/filters)
./configure \
    --prefix=/tmp/ffmpeg-android-out \
    --enable-shared \
    --disable-static \
    --disable-programs \
    --disable-doc \
    --disable-avdevice \
    --disable-postproc \
    --disable-avfilter \
    --enable-cross-compile \
    --target-os=android \
    --arch=aarch64 \
    --cpu=armv8-a \
    --cc=/tmp/ndk-bin/aarch64-clang \
    --cxx=/tmp/ndk-bin/aarch64-clang++ \
    --nm=/tmp/ndk-bin/llvm-nm \
    --ar=/tmp/ndk-bin/llvm-ar \
    --ranlib=/tmp/ndk-bin/llvm-ranlib \
    --strip=/tmp/ndk-bin/llvm-strip \
    --host-cc=gcc \
    --sysroot=$TOOLCHAIN/sysroot \
    --extra-cflags="-O2 -fPIC" \
    --extra-ldflags="-lm"

# 4. Build and install
make -j$(nproc)
make install

# 5. Copy into runtime package (unversioned sonames on Android)
cp /tmp/ffmpeg-android-out/lib/libavformat.so   src/Loxifi.FFmpeg.Runtime.android-arm64/runtimes/android-arm64/native/
cp /tmp/ffmpeg-android-out/lib/libavcodec.so    src/Loxifi.FFmpeg.Runtime.android-arm64/runtimes/android-arm64/native/
cp /tmp/ffmpeg-android-out/lib/libavutil.so     src/Loxifi.FFmpeg.Runtime.android-arm64/runtimes/android-arm64/native/
cp /tmp/ffmpeg-android-out/lib/libswscale.so    src/Loxifi.FFmpeg.Runtime.android-arm64/runtimes/android-arm64/native/
cp /tmp/ffmpeg-android-out/lib/libswresample.so src/Loxifi.FFmpeg.Runtime.android-arm64/runtimes/android-arm64/native/
```

### Soname version mapping (FFmpeg 7.1)

| Library      | Linux/Windows soname version | Android soname |
|-------------|------------------------------|----------------|
| libavformat | 61                           | unversioned    |
| libavcodec  | 61                           | unversioned    |
| libavutil   | 59                           | unversioned    |
| libswscale  | 8                            | unversioned    |
| libswresample | 5                          | unversioned    |

These version numbers are coded into `LibraryLoader.cs` and must be updated when changing FFmpeg major versions.

### Updating to a new FFmpeg version

1. Update the binaries following the steps above for each platform
2. Update soname versions in `src/Loxifi.FFmpeg/Native/LibraryLoader.cs` (`LibraryNameMap`)
3. Check for struct layout changes in the FFmpeg headers (see `src/Loxifi.FFmpeg/Native/Types/`)
   - Headers are at `include/` in the BtbN builds or in the source tree
   - Key files: `libavformat/avformat.h`, `libavcodec/codec_par.h`, `libavcodec/packet.h`, `libavutil/frame.h`
4. Run tests to verify: `dotnet test tests/Loxifi.FFmpeg.Tests/`

## Building

```bash
./build.sh           # Release build (default)
./build.sh Debug     # Debug build
```

Set `ANDROID_SDK_DIR` and `JAVA_HOME` environment variables for Android test project builds.

## Testing

Desktop tests run automatically and use the bundled FFmpeg libraries:
```bash
dotnet test tests/Loxifi.FFmpeg.Tests/ -c Release
```

Android tests require an ARM64 device or emulator with XHarness:
```bash
xharness android test \
    --app=tests/Loxifi.FFmpeg.AndroidTests/bin/Release/net9.0-android/com.loxifi.ffmpeg.tests-Signed.apk \
    --package-name=com.loxifi.ffmpeg.tests \
    --output-directory=./test-results
```

## License

FFmpeg libraries are licensed under LGPL v2.1 or later. All builds use LGPL configuration (no `--enable-gpl` or `--enable-nonfree`).
