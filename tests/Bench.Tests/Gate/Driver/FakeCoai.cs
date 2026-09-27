using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Infrastructure.Gate;

namespace Bench.Tests.Gate.Driver;

/// <summary>The fake coai-mcp (<c>tests/FakeCoai</c>), located in its OWN build output and driven through the real
/// <see cref="McpStdioClient"/>. A test writes a script, points <c>FAKE_COAI_SCRIPT</c> at it, and reads back the
/// event log the fake writes into <c>FAKE_COAI_EVENTS</c>.</summary>
internal sealed class FakeCoai : IDisposable
{
    public FakeCoai(JsonObject? script = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "bench-gate-fake", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        EventsDir = Path.Combine(Root, "events");
        Directory.CreateDirectory(EventsDir);
        ScriptPath = Path.Combine(Root, "script.json");
        File.WriteAllText(ScriptPath, (script ?? []).ToJsonString());
    }

    public string Root { get; }

    public string EventsDir { get; }

    public string ScriptPath { get; }

    /// <summary>The fake's apphost, in <c>tests/FakeCoai/bin/&lt;configuration&gt;/net10.0/</c> — the configuration read
    /// off the test assembly's own folder, so a Debug test run launches a Debug fake.</summary>
    public static string Executable
    {
        get
        {
            var testBin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var framework = testBin.Name;
            var configuration = testBin.Parent!.Name;
            var tests = testBin.Parent.Parent!.Parent!.Parent!.FullName;
            var name = OperatingSystem.IsWindows() ? "fake-coai-mcp.exe" : "fake-coai-mcp";

            return Path.Combine(tests, "FakeCoai", "bin", configuration, framework, name);
        }
    }

    /// <summary>The parent's environment without any <c>COAI_*</c>, plus the fake's two variables and whatever the
    /// test adds — the shape <c>CoaiEnvironment</c> produces for the real product.</summary>
    public IReadOnlyDictionary<string, string> Environment(IReadOnlyDictionary<string, string>? extra = null)
    {
        var env = System.Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(e => (Name: (string)e.Key, Value: e.Value as string ?? string.Empty))
            .Where(e => !e.Name.StartsWith("COAI_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);
        env["FAKE_COAI_SCRIPT"] = ScriptPath;
        env["FAKE_COAI_EVENTS"] = EventsDir;

        foreach (var (name, value) in extra ?? new Dictionary<string, string>())
        {
            env[name] = value;
        }

        return env;
    }

    public McpLaunch Launch(string stderrName = "stderr.txt", IReadOnlyDictionary<string, string>? extra = null) =>
        new(Executable, [], Root, Environment(extra), Path.Combine(Root, Guid.NewGuid().ToString("N") + "-" + stderrName), TimeSpan.FromSeconds(30));

    public Task<Bench.Domain.Outcome<IMcpSession>> OpenAsync(IReadOnlyDictionary<string, string>? extra = null) =>
        new McpStdioSessionFactory().OpenAsync(Launch(extra: extra), Xunit.TestContext.Current.CancellationToken);

    /// <summary>Every event every fake process logged, in time order: (timestamp, pid, text).</summary>
    public IReadOnlyList<(long At, int Pid, string Text)> Events() =>
        [.. Directory.EnumerateFiles(EventsDir, "events-*.log")
            .SelectMany(File.ReadAllLines)
            .Select(Parse)
            .OrderBy(e => e.At)];

    /// <summary>The largest number of intervals open at once, where an interval opens at an event named
    /// <paramref name="open"/> and closes at <paramref name="close"/>, per process — optionally keyed by the rest of
    /// the event's text (the endpoint of a review).</summary>
    public IReadOnlyDictionary<string, int> MaxConcurrent(string open, string close, bool byKey)
    {
        var current = new Dictionary<string, int>(StringComparer.Ordinal);
        var max = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (_, _, text) in Events())
        {
            var (verb, key) = Split(text, byKey);
            var delta = verb == open ? 1 : verb == close ? -1 : 0;
            current[key] = current.GetValueOrDefault(key) + delta;
            max[key] = Math.Max(max.GetValueOrDefault(key), current[key]);
        }

        return max;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A child still closing a file; the temp folder is the operating system's to clean.
        }
    }

    private static (string Verb, string Key) Split(string text, bool byKey)
    {
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        var verb = space < 0 ? text : text[..space];
        return (verb, byKey && space >= 0 ? text[(space + 1)..] : "*");
    }

    private static (long At, int Pid, string Text) Parse(string line)
    {
        var parts = line.Split(' ', 3);
        return (long.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), parts.Length > 2 ? parts[2] : string.Empty);
    }
}
