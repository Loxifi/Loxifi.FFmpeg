// SwrContext.cs — Documentation placeholder for FFmpeg's SwrContext.
// SwrContext is a fully opaque struct — FFmpeg provides no public fields. It is used
// exclusively as a pointer (nint or nint*) in all swr_* P/Invoke calls. This file
// exists to document the type's role: SwrContext handles audio resampling, sample
// format conversion, and channel layout remapping. Currently not actively used because
// audio streams are stream-copied rather than re-encoded.
