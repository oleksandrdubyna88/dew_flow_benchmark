using System.Text.Json.Nodes;

namespace Bench.Tests.Probes.Driver;

/// <summary>The fake CLI (<c>tests/FakeCli</c>) as one probe SUBJECT sees it: a fresh model id per instance, which is the key the
/// fake finds its script and writes its call log under (<c>%TEMP%/bench-fake-cli/&lt;model&gt;/</c>), so two subjects of one
/// campaign — and two tests in parallel — never read each other's script. The probe runner launches it with the harness's own
/// environment, as the real CLIs are launched, so nothing here sets a process-wide variable.</summary>
internal sealed class FakeCli : IDisposable
{
    public FakeCli(JsonObject? script = null)
    {
        Model = $"fake-{Guid.NewGuid():N}";
        Home = Path.Combine(Path.GetTempPath(), "bench-fake-cli", Model);
        Directory.CreateDirectory(Home);
        Rewrite(script);
    }

    /// <summary>The model id a subject pins the fake to — the fake's script key.</summary>
    public string Model { get; }

    public string Home { get; }

    public string ScriptPath => Path.Combine(Home, "script.json");

    public string EventsDir => Path.Combine(Home, "events");

    /// <summary>The fake's apphost, in <c>tests/FakeCli/bin/&lt;configuration&gt;/net10.0/</c> — the configuration read off the test
    /// assembly's own folder, as <see cref="Gate.Driver.FakeCoai.Executable"/> is.</summary>
    public static string Executable
    {
        get
        {
            var testBin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var framework = testBin.Name;
            var configuration = testBin.Parent!.Name;
            var tests = testBin.Parent.Parent!.Parent!.Parent!.FullName;
            var name = OperatingSystem.IsWindows() ? "fake-cli.exe" : "fake-cli";

            return Path.Combine(tests, "FakeCli", "bin", configuration, framework, name);
        }
    }

    /// <summary>Replaces the script — how a test puts an account back between two campaigns.</summary>
    public void Rewrite(JsonObject? script) => File.WriteAllText(ScriptPath, (script ?? []).ToJsonString());

    /// <summary>Every call the fake logged, in call order.</summary>
    public IReadOnlyList<FakeCliCall> Calls() =>
        Directory.Exists(EventsDir)
            ? [.. Directory.EnumerateFiles(EventsDir, "call-*.json").Select(Read).OrderBy(c => c.Call)]
            : [];

    private static FakeCliCall Read(string path)
    {
        var o = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];

        return new FakeCliCall(
            o["call"]?.GetValue<int>() ?? 0,
            o["pid"]?.GetValue<int>() ?? 0,
            [.. (o["argv"] as JsonArray ?? []).Select(a => a?.GetValue<string>() ?? string.Empty)],
            o["cwd"]?.GetValue<string>() ?? string.Empty,
            o["prompt"]?.GetValue<string>() ?? string.Empty);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Home, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A child still closing a file; the temp folder is the operating system's to clean.
        }
    }
}

internal sealed record FakeCliCall(int Call, int Pid, IReadOnlyList<string> Argv, string Cwd, string Prompt);
