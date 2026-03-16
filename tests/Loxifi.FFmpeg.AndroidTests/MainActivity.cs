// MainActivity.cs — Android test runner activity.
// Discovers and runs all [Fact]-attributed test methods using reflection, displays
// results in a TextView, writes xUnit-compatible XML results to external storage,
// and logs completion status for CI tooling to detect.

using Android.App;
using Android.OS;
using Android.Widget;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Loxifi.FFmpeg.AndroidTests;

/// <summary>
/// Main activity that runs all xUnit [Fact] tests on a background thread
/// and displays results in a simple text view.
/// </summary>
[Activity(Label = "FFmpeg Tests", MainLauncher = true)]
public class MainActivity : Activity
{
    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var textView = new TextView(this) { TextSize = 12 };
        SetContentView(textView);
        textView.Text = "Running tests...";

        // Run tests on a background thread to avoid blocking the UI
        _ = Task.Run(() =>
        {
            var results = RunTests(typeof(NativeLibraryTests).Assembly);

            // Write xUnit-compatible XML results to external storage for CI extraction
            string xml = results.ToXml();
            string resultsDir = Application.Context.GetExternalFilesDir(null)?.AbsolutePath
                ?? Application.Context.FilesDir!.AbsolutePath;
            string resultsFile = Path.Combine(resultsDir, "TestResults.xml");
            File.WriteAllText(resultsFile, xml);

            Android.Util.Log.Info("FFmpegTests", $"TEST_RESULTS_PATH: {resultsFile}");
            Android.Util.Log.Info("FFmpegTests", xml);

            RunOnUiThread(() => textView.Text = results.Summary + "\n\n" + results.Details);

            // Log a sentinel message for CI tooling to detect test completion
            if (results.Failed == 0)
                Android.Util.Log.Info("FFmpegTests", $"TEST_RUN_COMPLETE: SUCCESS ({results.Passed} passed)");
            else
                Android.Util.Log.Error("FFmpegTests", $"TEST_RUN_COMPLETE: FAILED ({results.Failed} failed, {results.Passed} passed)");
        });
    }

    /// <summary>
    /// Discovers and runs all [Fact] test methods in the given assembly using reflection.
    /// </summary>
    private static TestRunResults RunTests(Assembly assembly)
    {
        var results = new TestRunResults();

        var testClasses = assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && t.GetMethods()
                .Any(m => m.GetCustomAttribute<Xunit.FactAttribute>() != null));

        foreach (var testClass in testClasses)
        {
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(testClass);
            }
            catch (Exception ex)
            {
                results.AddFailure(testClass.Name, ".ctor", ex);
                continue;
            }

            var testMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<Xunit.FactAttribute>() != null);

            foreach (var method in testMethods)
            {
                try
                {
                    object? result = method.Invoke(instance, null);

                    // Handle async tests by synchronously waiting for completion
                    if (result is Task task)
                    {
                        task.GetAwaiter().GetResult();
                    }

                    results.AddPass($"{testClass.Name}.{method.Name}");
                }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    results.AddFailure(testClass.Name, method.Name, tie.InnerException);
                }
                catch (Exception ex)
                {
                    results.AddFailure(testClass.Name, method.Name, ex);
                }
            }

            (instance as IDisposable)?.Dispose();
        }

        return results;
    }
}

/// <summary>
/// Accumulates test results and generates summary text and xUnit-compatible XML output.
/// </summary>
internal class TestRunResults
{
    private readonly List<(string Name, bool Passed, string? Error)> _results = [];

    /// <summary>Number of passing tests.</summary>
    public int Passed => _results.Count(r => r.Passed);

    /// <summary>Number of failing tests.</summary>
    public int Failed => _results.Count(r => !r.Passed);

    /// <summary>Total number of tests run.</summary>
    public int Total => _results.Count;

    /// <summary>Records a passing test.</summary>
    public void AddPass(string name) => _results.Add((name, true, null));

    /// <summary>Records a failing test with its exception details.</summary>
    public void AddFailure(string className, string methodName, Exception ex)
    {
        _results.Add(($"{className}.{methodName}", false, ex.ToString()));
    }

    /// <summary>One-line summary of results.</summary>
    public string Summary =>
        $"Tests: {Total}, Passed: {Passed}, Failed: {Failed}";

    /// <summary>Detailed per-test results with PASS/FAIL markers.</summary>
    public string Details
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var (name, passed, error) in _results)
            {
                sb.AppendLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
                if (error != null)
                {
                    sb.AppendLine($"  {error[..Math.Min(error.Length, 500)]}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>Generates xUnit-compatible XML output for CI tooling.</summary>
    public string ToXml()
    {
        var assemblies = new XElement("assemblies",
            new XElement("assembly",
                new XAttribute("name", "Loxifi.FFmpeg.AndroidTests"),
                new XAttribute("total", Total),
                new XAttribute("passed", Passed),
                new XAttribute("failed", Failed),
                _results.Select(r => new XElement("test",
                    new XAttribute("name", r.Name),
                    new XAttribute("result", r.Passed ? "Pass" : "Fail"),
                    r.Error != null ? new XElement("failure", new XElement("message", r.Error)) : null))));

        return new XDocument(assemblies).ToString();
    }
}
