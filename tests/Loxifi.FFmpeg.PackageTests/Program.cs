using System.Runtime.CompilerServices;
using Loxifi.FFmpeg.Native;
using Loxifi.FFmpeg.Transcoding;

RuntimeHelpers.RunModuleConstructor(typeof(LibraryLoader).Module.ModuleHandle);

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

// Test 1: Library loading
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
