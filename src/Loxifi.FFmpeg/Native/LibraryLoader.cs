// LibraryLoader.cs — Custom native library resolver for FFmpeg shared libraries.
// FFmpeg libraries have complex interdependencies (e.g., avformat depends on avcodec,
// which depends on avutil). On Linux/Android, they must be loaded with RTLD_GLOBAL so
// that symbols are visible across shared objects. The standard .NET NativeLibrary.Load
// uses RTLD_LOCAL, which causes unresolved symbol errors at runtime. This class solves
// that by using dlopen(RTLD_NOW | RTLD_GLOBAL) directly via a function pointer.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native;

/// <summary>
/// Resolves and loads FFmpeg native libraries at runtime using a custom DllImportResolver.
/// Handles platform-specific library naming (e.g., .so.61, .dylib, .dll), dependency ordering,
/// and the critical RTLD_GLOBAL requirement on Linux/Android.
/// </summary>
/// <remarks>
/// This class is automatically initialized via <see cref="ModuleInitializerAttribute"/> when
/// any type in this assembly is first accessed. It registers a <see cref="NativeLibrary.SetDllImportResolver"/>
/// callback that intercepts all P/Invoke loads for the five FFmpeg libraries.
/// </remarks>
public static class LibraryLoader
{
    /// <summary>
    /// Maps logical library names (used in <c>[LibraryImport]</c> attributes) to platform-specific
    /// file name candidates. FFmpeg libraries use soname versioning (e.g., libavcodec.so.61),
    /// and the version numbers correspond to FFmpeg 7.1 ABI versions.
    /// </summary>
    private static readonly Dictionary<string, string[]> LibraryNameMap = new()
    {
        ["avformat"] = GetPlatformNames("avformat", 61),
        ["avcodec"] = GetPlatformNames("avcodec", 61),
        ["avutil"] = GetPlatformNames("avutil", 59),
        ["swscale"] = GetPlatformNames("swscale", 8),
        ["swresample"] = GetPlatformNames("swresample", 5),
    };

    /// <summary>
    /// Dependency graph for FFmpeg libraries. Each library must have its dependencies loaded
    /// before it can be loaded, because FFmpeg uses cross-library symbol references.
    /// For example, avformat calls functions in avcodec, which calls functions in avutil.
    /// </summary>
    private static readonly Dictionary<string, string[]> Dependencies = new()
    {
        ["avutil"] = [],
        ["swresample"] = ["avutil"],
        ["avcodec"] = ["avutil", "swresample"],
        ["swscale"] = ["avutil"],
        ["avformat"] = ["avutil", "swresample", "avcodec"],
    };

    /// <summary>Cache of loaded library handles, keyed by logical name.</summary>
    private static readonly Dictionary<string, nint> _handles = new();

    /// <summary>Resolved list of directories to search for native libraries.</summary>
    private static string[]? _resolvedSearchDirs;

    /// <summary>Function pointer to the native dlopen function, resolved once at startup.</summary>
    private static nint _dlopenFn;

    /// <summary>Function pointer to the native dlerror function, for diagnostic purposes.</summary>
    private static nint _dlerrorFn;

    /// <summary>Whether we have attempted to resolve dlopen (even if it failed).</summary>
    private static bool _dlopenResolved;

    /// <summary>
    /// Optional custom path to search for FFmpeg native libraries. Set this before any
    /// P/Invoke call if the libraries are in a non-standard location.
    /// </summary>
    public static string? CustomSearchPath { get; set; }

    private static bool _initialized;

    /// <summary>
    /// Registers the custom DllImportResolver for this assembly. Called automatically
    /// by the .NET runtime when the module is first loaded, thanks to <see cref="ModuleInitializerAttribute"/>.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255")]
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        NativeLibrary.SetDllImportResolver(typeof(LibraryLoader).Assembly, ResolveLibrary);
    }

    /// <summary>
    /// DllImportResolver callback. Invoked by the .NET runtime whenever a [LibraryImport] or
    /// [DllImport] in this assembly needs to resolve a native library. Returns IntPtr.Zero for
    /// libraries we don't manage, letting the default resolver handle them.
    /// </summary>
    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!LibraryNameMap.ContainsKey(libraryName))
            return nint.Zero;

        // Resolve search directories once on first call
        _resolvedSearchDirs ??= GetSearchDirs(assembly);

        // Resolve the dlopen function pointer once. On Linux/Android, we need dlopen with
        // RTLD_GLOBAL to make symbols visible across FFmpeg's shared objects. On Windows,
        // LoadLibrary always exports symbols globally, so this is unnecessary.
        if (!_dlopenResolved)
        {
            _dlopenResolved = true;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Try multiple possible locations for dlopen: libdl.so.2 (glibc),
                // libdl.so (older systems), or libc.so.6 (musl/Android where dlopen is in libc)
                if (NativeLibrary.TryLoad("libdl.so.2", out nint libdl) ||
                    NativeLibrary.TryLoad("libdl.so", out libdl) ||
                    NativeLibrary.TryLoad("libc.so.6", out libdl))
                {
                    NativeLibrary.TryGetExport(libdl, "dlopen", out _dlopenFn);
                    NativeLibrary.TryGetExport(libdl, "dlerror", out _dlerrorFn);
                }
            }
        }

        // Load all dependencies before this library. This is not truly recursive because
        // the dependency graph is a DAG with a maximum depth of 3.
        if (Dependencies.TryGetValue(libraryName, out string[]? deps))
        {
            foreach (string dep in deps)
            {
                EnsureLoaded(dep);
            }
        }

        return EnsureLoaded(libraryName);
    }

    /// <summary>
    /// Loads a library by logical name if not already loaded, searching configured directories
    /// first, then falling back to system paths.
    /// </summary>
    /// <param name="libraryName">Logical FFmpeg library name (e.g., "avutil").</param>
    /// <returns>The native library handle, or <see cref="nint.Zero"/> if not found.</returns>
    private static nint EnsureLoaded(string libraryName)
    {
        if (_handles.TryGetValue(libraryName, out nint cached))
            return cached;

        if (!LibraryNameMap.TryGetValue(libraryName, out string[]? candidates))
            return nint.Zero;

        nint handle = nint.Zero;

        // First pass: search in configured directories (NuGet runtime dirs, app base, custom path)
        if (_resolvedSearchDirs is not null)
        {
            foreach (string dir in _resolvedSearchDirs)
            {
                if (handle != nint.Zero) break;
                if (!Directory.Exists(dir)) continue;

                foreach (string candidate in candidates)
                {
                    string fullPath = Path.Combine(dir, candidate);
                    if (!File.Exists(fullPath)) continue;

                    handle = LoadWithGlobal(fullPath);
                    if (handle != nint.Zero) break;
                }
            }
        }

        // Second pass: fall back to system library paths (LD_LIBRARY_PATH, /usr/lib, etc.)
        if (handle == nint.Zero)
        {
            foreach (string candidate in candidates)
            {
                handle = LoadWithGlobal(candidate);
                if (handle == nint.Zero)
                    NativeLibrary.TryLoad(candidate, out handle);
                if (handle != nint.Zero) break;
            }
        }

        if (handle != nint.Zero)
            _handles[libraryName] = handle;

        return handle;
    }

    /// <summary>
    /// Loads a native library with RTLD_GLOBAL visibility if dlopen is available,
    /// otherwise falls back to NativeLibrary.TryLoad (which uses RTLD_LOCAL).
    /// </summary>
    /// <param name="path">Absolute or relative path to the native library.</param>
    /// <returns>The native library handle, or <see cref="nint.Zero"/> on failure.</returns>
    private static nint LoadWithGlobal(string path)
    {
        if (_dlopenFn != nint.Zero)
            return CallDlopen(_dlopenFn, path);

        NativeLibrary.TryLoad(path, out nint h);
        return h;
    }

    /// <summary>
    /// Calls the native dlopen function directly via an unmanaged function pointer.
    /// Uses RTLD_NOW | RTLD_GLOBAL flags so that all symbols are resolved immediately
    /// and made visible to subsequently loaded libraries. This is essential because
    /// FFmpeg libraries reference each other's symbols (e.g., avformat calls avcodec functions).
    /// Without RTLD_GLOBAL, symbol lookup fails at load time with "undefined symbol" errors.
    /// </summary>
    /// <param name="dlopenFn">Function pointer to the native dlopen.</param>
    /// <param name="path">Path to the shared library to load.</param>
    /// <returns>The library handle, or <see cref="nint.Zero"/> on failure.</returns>
    private static unsafe nint CallDlopen(nint dlopenFn, string path)
    {
        // RTLD_NOW: resolve all symbols immediately (fail fast if any are missing)
        const int RTLD_NOW = 0x2;
        // RTLD_GLOBAL: make symbols available for subsequently loaded shared objects
        const int RTLD_GLOBAL = 0x100;

        nint pathPtr = Marshal.StringToHGlobalAnsi(path);
        try
        {
            // Cast the function pointer to the dlopen signature: void* dlopen(const char*, int)
            var fn = (delegate* unmanaged[Cdecl]<nint, int, nint>)dlopenFn;
            nint result = fn(pathPtr, RTLD_NOW | RTLD_GLOBAL);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(pathPtr);
        }
    }

    /// <summary>
    /// Builds the ordered list of directories to search for native libraries.
    /// Checks NuGet runtime-specific directories first, then the assembly and app base directories.
    /// </summary>
    private static string[] GetSearchDirs(Assembly assembly)
    {
        string assemblyDir = Path.GetDirectoryName(assembly.Location) ?? string.Empty;
        string baseDir = AppContext.BaseDirectory;
        string rid = RuntimeInformation.RuntimeIdentifier;

        var dirs = new List<string>();
        if (CustomSearchPath is not null) dirs.Add(CustomSearchPath);
        // NuGet runtime packages place native libs in runtimes/{rid}/native/
        dirs.Add(Path.Combine(assemblyDir, "runtimes", rid, "native"));
        dirs.Add(Path.Combine(baseDir, "runtimes", rid, "native"));
        dirs.Add(assemblyDir);
        dirs.Add(baseDir);
        return dirs.Distinct().ToArray();
    }

    /// <summary>
    /// Generates platform-specific library file name candidates for a given FFmpeg library.
    /// Each platform uses different naming conventions:
    /// Windows: avformat-61.dll, Linux: libavformat.so.61, macOS: libavformat.61.dylib.
    /// The versioned name is tried first (more specific), then the unversioned fallback.
    /// </summary>
    /// <param name="baseName">The FFmpeg library base name (e.g., "avformat").</param>
    /// <param name="soVersion">The soname version number from the FFmpeg 7.1 ABI.</param>
    /// <returns>Array of candidate file names in priority order.</returns>
    private static string[] GetPlatformNames(string baseName, int soVersion)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return [$"{baseName}-{soVersion}.dll", $"{baseName}.dll"];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || OperatingSystem.IsAndroid())
            return [$"lib{baseName}.so.{soVersion}", $"lib{baseName}.so"];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return [$"lib{baseName}.{soVersion}.dylib", $"lib{baseName}.dylib"];

        return [$"lib{baseName}.so", $"{baseName}"];
    }
}
