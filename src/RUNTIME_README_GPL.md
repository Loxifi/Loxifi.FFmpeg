# Loxifi.FFmpeg Runtime (GPL)

FFmpeg 7.1 native shared libraries for use with the [Loxifi.FFmpeg](https://www.nuget.org/packages/Loxifi.FFmpeg) P/Invoke library.

## License

GPL v3 — includes all LGPL codecs **plus** libx264, libx265, and libxvid. If you distribute an app using this package, it must be GPL-licensed.

## Bundled Codecs

All LGPL codecs plus:
- **libx264** — H.264/AVC encoder (industry standard, excellent quality)
- **libx265** — H.265/HEVC encoder (~30% better compression than H.264)
- **libxvid** — MPEG-4 ASP encoder

## Usage

Install this package alongside `Loxifi.FFmpeg` **instead of** the LGPL runtime package. The native libraries are automatically resolved at runtime.

See the [main project README](https://github.com/Loxifi/Loxifi.FFmpeg) for API documentation.
