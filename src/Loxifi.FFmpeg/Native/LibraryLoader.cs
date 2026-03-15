using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native;

public static class LibraryLoader
{
    // FFmpeg 7.1 soname versions
    private static readonly Dictionary<string, string[]> LibraryNameMap = new()
    {
        ["avformat"] = GetPlatformNames("avformat", 61),
        ["avcodec"] = GetPlatformNames("avcodec", 61),
        ["avutil"] = GetPlatformNames("avutil", 59),
        ["swscale"] = GetPlatformNames("swscale", 8),
        ["swresample"] = GetPlatformNames("swresample", 5),
    };

    // Dependencies: each library lists what must be loaded before it
    private static readonly Dictionary<string, string[]> Dependencies = new()
    {
        ["avutil"] = [],
        ["swresample"] = ["avutil"],
        ["avcodec"] = ["avutil", "swresample"],
        ["swscale"] = ["avutil"],
        ["avformat"] = ["avutil", "swresample", "avcodec"],
    };

    private static readonly Dictionary<string, nint> _handles = new();
    private static string[]? _resolvedSearchDirs;
    private static nint _dlopenFn;
    private static bool _dlopenResolved;

    public static string? CustomSearchPath { get; set; }

    private static bool _initialized;

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255")]
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        NativeLibrary.SetDllImportResolver(typeof(LibraryLoader).Assembly, ResolveLibrary);
    }

    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {

        if (!LibraryNameMap.ContainsKey(libraryName))
            return nint.Zero;

        // Resolve search directories once
        _resolvedSearchDirs ??= GetSearchDirs(assembly);

        // Resolve dlopen function once (for RTLD_GLOBAL on Linux)
        if (!_dlopenResolved)
        {
            _dlopenResolved = true;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (NativeLibrary.TryLoad("libdl.so.2", out nint libdl) ||
                    NativeLibrary.TryLoad("libdl.so", out libdl) ||
                    NativeLibrary.TryLoad("libc.so.6", out libdl))
                {
                    NativeLibrary.TryGetExport(libdl, "dlopen", out _dlopenFn);
                    NativeLibrary.TryGetExport(libdl, "dlerror", out _dlerrorFn);
                }
            }
        }

        // Ensure dependencies are loaded first (recursive but finite)
        if (Dependencies.TryGetValue(libraryName, out string[]? deps))
        {
            foreach (string dep in deps)
            {
                EnsureLoaded(dep);
            }
        }

        return EnsureLoaded(libraryName);
    }

    private static nint EnsureLoaded(string libraryName)
    {
        if (_handles.TryGetValue(libraryName, out nint cached))
            return cached;

        if (!LibraryNameMap.TryGetValue(libraryName, out string[]? candidates))
            return nint.Zero;

        nint handle = nint.Zero;

        // Search directories
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

        // System paths fallback
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

    private static nint LoadWithGlobal(string path)
    {
        if (_dlopenFn != nint.Zero)
            return CallDlopen(_dlopenFn, path);

        NativeLibrary.TryLoad(path, out nint h);
        return h;
    }

    private static nint _dlerrorFn;

    private static unsafe nint CallDlopen(nint dlopenFn, string path)
    {
        const int RTLD_NOW = 0x2;
        const int RTLD_GLOBAL = 0x100;

        nint pathPtr = Marshal.StringToHGlobalAnsi(path);
        try
        {
            var fn = (delegate* unmanaged[Cdecl]<nint, int, nint>)dlopenFn;
            nint result = fn(pathPtr, RTLD_NOW | RTLD_GLOBAL);

            if (result == nint.Zero && _dlerrorFn != nint.Zero)
            {
                var errFn = (delegate* unmanaged[Cdecl]<nint>)_dlerrorFn;
                nint errPtr = errFn();
                if (errPtr != nint.Zero)
                {
                    string? err = Marshal.PtrToStringUTF8(errPtr);
                }
            }

            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(pathPtr);
        }
    }

    private static string[] GetSearchDirs(Assembly assembly)
    {
        string assemblyDir = Path.GetDirectoryName(assembly.Location) ?? string.Empty;
        string rid = RuntimeInformation.RuntimeIdentifier;
        string runtimesDir = Path.Combine(assemblyDir, "runtimes", rid, "native");

        return CustomSearchPath is not null
            ? [CustomSearchPath, runtimesDir, assemblyDir]
            : [runtimesDir, assemblyDir];
    }

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
