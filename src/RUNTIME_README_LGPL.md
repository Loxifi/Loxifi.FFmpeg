# Loxifi.FFmpeg Runtime (LGPL)

FFmpeg 7.1 native shared libraries for use with the [Loxifi.FFmpeg](https://www.nuget.org/packages/Loxifi.FFmpeg) P/Invoke library.

## License

LGPL v3 — safe for proprietary/closed-source apps using dynamic linking.

## Bundled Codecs

Video: libaom-av1, libsvtav1, libvpx, libvpx-vp9, libopenh264, libtheora, libwebp, mpeg4
Audio: libmp3lame, libopus, libvorbis, aac
Utility: libdav1d, libopencore-amr, libopenjpeg, libzimg

For H.264 (libx264) and H.265 (libx265) support, use the `.GPL` variant of this package instead.

## Usage

Install this package alongside `Loxifi.FFmpeg`. The native libraries are automatically resolved at runtime.

See the [main project README](https://github.com/Loxifi/Loxifi.FFmpeg) for API documentation.
