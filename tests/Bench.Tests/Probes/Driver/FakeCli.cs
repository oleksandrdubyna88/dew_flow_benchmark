using System.Text.Json.Nodes;
using Bench.Tests.Leftovers;

namespace Bench.Tests.Probes.Driver;

/// <summary>The fake CLI (<c>tests/FakeCli</c>) as one probe SUBJECT sees it: a fresh model id per instance, which is the key the
/// fake finds its script and writes its call log under, so two subjects of one campaign — and two tests in parallel — never read each
/// other's script. The id is the NAME of this instance's owned folder (<c>testsToDelete-&lt;owner&gt;-bench-fakecli-&lt;id&gt;</c>, claimed first and removed
/// with the instance), and the fake reads an id of that form as <c>&lt;TEMP&gt;/&lt;id&gt;</c>: the home is the owned folder itself, made here and never by the
/// fake. The probe runner launches it under a MINIMAL replaced environment (S2c — <c>TEMP</c> passes, nothing the harness owns does), as the
/// real CLIs are launched, so nothing here sets a process-wide variable and the model id in the argv is the one thing every launch carries.</summary>
internal sealed class FakeCli : IDisposable
{
    private readonly OwnedDir _owned = OwnedDir.Create("fakecli");

    public FakeCli(JsonObject? script = null)
    {
        Model = Path.GetFileName(_owned.Path);
        Home = _owned.Path;
        Rewrite(script);
    }

    /// <summary>The model id a subject pins the fake to — the fake's script key, and the owned folder's own name.</summary>
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
    public IReadOnlyList<FakeCliCall> Calls() => CallsIn(Home);

    /// <summary>Every call the fake logged under <paramref name="home"/> (the <c>FAKE_CLI_HOME</c> a subject was given), in call order: how a check that holds only the
    /// subject's environment, and not the fake, reads the same log.</summary>
    public static IReadOnlyList<FakeCliCall> CallsIn(string home)
    {
        var events = Path.Combine(home, "events");

        return Directory.Exists(events)
            ? [.. Directory.EnumerateFiles(events, "call-*.json").Select(Read).OrderBy(c => c.Call)]
            : [];
    }

    private static FakeCliCall Read(string path)
    {
        var o = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];

        return new FakeCliCall(
            o["call"]?.GetValue<int>() ?? 0,
            o["pid"]?.GetValue<int>() ?? 0,
            [.. (o["argv"] as JsonArray ?? []).Select(a => a?.GetValue<string>() ?? string.Empty)],
            o["cwd"]?.GetValue<string>() ?? string.Empty,
            o["prompt"]?.GetValue<string>() ?? string.Empty,
            [.. (o["env"] as JsonArray ?? []).Select(a => a?.GetValue<string>() ?? string.Empty)],
            o["subagentModelGuard"]?.GetValue<string>() ?? string.Empty);
    }

    public void Dispose() => _owned.Dispose();
}

/// <param name="EnvironmentNames">The names of the variables the fake saw — what the launcher let through (S2c), never their values.</param>
internal sealed record FakeCliCall(int Call, int Pid, IReadOnlyList<string> Argv, string Cwd, string Prompt, IReadOnlyList<string> EnvironmentNames, string SubagentModelGuard = "");
