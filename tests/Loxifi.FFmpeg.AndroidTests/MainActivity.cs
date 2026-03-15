using Android.App;
using Android.OS;
using Android.Widget;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Loxifi.FFmpeg.AndroidTests;

[Activity(Label = "FFmpeg Tests", MainLauncher = true)]
public class MainActivity : Activity
{
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
            string xml = results.ToXml();

            string resultsDir = Application.Context.GetExternalFilesDir(null)?.AbsolutePath
                ?? Application.Context.FilesDir!.AbsolutePath;
            string resultsFile = Path.Combine(resultsDir, "TestResults.xml");
            File.WriteAllText(resultsFile, xml);

            Android.Util.Log.Info("FFmpegTests", $"TEST_RESULTS_PATH: {resultsFile}");
            Android.Util.Log.Info("FFmpegTests", xml);

            RunOnUiThread(() => textView.Text = results.Summary + "\n\n" + results.Details);

            if (results.Failed == 0)
                Android.Util.Log.Info("FFmpegTests", $"TEST_RUN_COMPLETE: SUCCESS ({results.Passed} passed)");
            else
                Android.Util.Log.Error("FFmpegTests", $"TEST_RUN_COMPLETE: FAILED ({results.Failed} failed, {results.Passed} passed)");
        });
    }

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
                string testName = $"{testClass.Name}.{method.Name}";
                try
                {
                    object? result = method.Invoke(instance, null);

                    // Handle async tests
                    if (result is Task task)
                    {
                        task.GetAwaiter().GetResult();
                    }

                    results.AddPass(testName);
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

internal class TestRunResults
{
    private readonly List<(string Name, bool Passed, string? Error)> _results = [];

    public int Passed => _results.Count(r => r.Passed);
    public int Failed => _results.Count(r => !r.Passed);
    public int Total => _results.Count;

    public void AddPass(string name) => _results.Add((name, true, null));

    public void AddFailure(string className, string methodName, Exception ex)
    {
        _results.Add(($"{className}.{methodName}", false, ex.ToString()));
    }

    public string Summary =>
        $"Tests: {Total}, Passed: {Passed}, Failed: {Failed}";

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
