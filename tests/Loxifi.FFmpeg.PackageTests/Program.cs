// Program.cs — Standalone smoke test for the Loxifi.FFmpeg NuGet package.
// Runs outside the xUnit framework to verify that native library resolution works
// correctly when the package is consumed as a NuGet dependency. Prints diagnostic
// information about the runtime environment and checks that all five FFmpeg libraries
// load and return valid version numbers.

using System.Runtime.CompilerServices;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Transcoding;

// Force the module initializer to run
RuntimeHelpers.RunModuleConstructor(typeof(LibraryLoader).Module.ModuleHandle);

// Print runtime diagnostics for troubleshooting native library resolution
Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
Console.WriteLine($"RID: {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
Console.WriteLine($"BaseDir: {AppContext.BaseDirectory}");
Console.WriteLine($"Assembly: {typeof(LibraryLoader).Assembly.Location}");

// Check if native files exist in expected locations
string baseDir = AppContext.BaseDirectory;
foreach (string f in new[] { "avutil-59.dll", "libavutil.so.59", "avutil.dll", "libavutil.so" })
{
    string path = Path.Combine(baseDir, f);
    Console.WriteLine($"  {f}: {(File.Exists(path) ? "EXISTS" : "missing")}");
}
string rtDir = Path.Combine(baseDir, "runtimes");
if (Directory.Exists(rtDir))
    Console.WriteLine($"  runtimes/: {string.Join(", ", Directory.GetDirectories(rtDir).Select(Path.GetFileName))}");
else
    Console.WriteLine("  runtimes/: missing");
Console.WriteLine();

int passed = 0;
int failed = 0;

void Assert(bool condition, string name)
{
    if (condition)
    {
        Console.WriteLine($"  [PASS] {name}");
        passed++;
    }
    else
    {
        Console.WriteLine($"  [FAIL] {name}");
        failed++;
    }
}

// Test each FFmpeg library loads and returns a valid version
try
{
    uint ver = AVUtil.avutil_version();
    Assert(ver != 0, "AVUtil loads and returns version");
}
catch (Exception ex)
{
    Console.WriteLine($"  [FAIL] AVUtil loads: {ex.GetType().Name}: {ex.Message}");
    failed++;
}

try
{
    uint ver = AVFormat.avformat_version();
    Assert(ver != 0, "AVFormat loads and returns version");
}
catch (Exception ex)
{
    Console.WriteLine($"  [FAIL] AVFormat loads: {ex.GetType().Name}: {ex.Message}");
    failed++;
}

try
{
    uint ver = AVCodec.avcodec_version();
    Assert(ver != 0, "AVCodec loads and returns version");
}
catch (Exception ex)
{
    Console.WriteLine($"  [FAIL] AVCodec loads: {ex.GetType().Name}: {ex.Message}");
    failed++;
}

try
{
    uint ver = SWScale.swscale_version();
    Assert(ver != 0, "SWScale loads and returns version");
}
catch (Exception ex)
{
    Console.WriteLine($"  [FAIL] SWScale loads: {ex.GetType().Name}: {ex.Message}");
    failed++;
}

try
{
    uint ver = SWResample.swresample_version();
    Assert(ver != 0, "SWResample loads and returns version");
}
catch (Exception ex)
{
    Console.WriteLine($"  [FAIL] SWResample loads: {ex.GetType().Name}: {ex.Message}");
    failed++;
}

Console.WriteLine($"\nResults: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
