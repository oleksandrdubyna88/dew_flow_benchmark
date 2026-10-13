using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Tests.Probes.Driver;

namespace Bench.Tests.Scouts;

/// <summary>The fake CLI (<c>tests/FakeCli</c>) as a scout campaign sees it: every scout cell launches <c>--model opus</c>, so the fake's
/// per-MODEL home would be shared by every test that runs one — this gives it a TEST-OWNED home that does not depend on the model, handed to
/// it as <c>FAKE_CLI_HOME</c> through the runner's allow-listed child environment (tests only), and replays each cell's own stream by the
/// SHA-256 of the prompt it receives (<c>stdoutByPrompt</c>).</summary>
internal sealed class ScoutFake : IDisposable
{
    private readonly JsonObject _byPrompt = [];
    private JsonObject _extra = [];

    public ScoutFake(string home)
    {
        Home = home;
        Directory.CreateDirectory(Path.Combine(Home, "streams"));
        Write();
    }

    public string Home { get; }

    public static string Executable => FakeCli.Executable;

    public string EventsDir => Path.Combine(Home, "events");

    /// <summary>What the runner adds to the child's environment in a test — and nothing else.</summary>
    public IReadOnlyDictionary<string, string> ChildVariables => new Dictionary<string, string>(StringComparer.Ordinal) { ["FAKE_CLI_HOME"] = Home };

    /// <summary>The fake answers <paramref name="prompt"/> with <paramref name="stream"/>, verbatim but for <c>__CWD__</c>.</summary>
    public void Answer(string prompt, string stream)
    {
        var file = Path.Combine(Home, "streams", StableHash.Of(prompt)[..16] + ".ndjson");
        File.WriteAllText(file, stream);
        _byPrompt[StableHash.Of(prompt)] = file;
        Write();
    }

    /// <summary>Extra script fields — <c>holdAfterLine</c>, <c>lineDelayMs</c>, <c>mode</c>, <c>stderr</c>.</summary>
    public void Script(JsonObject extra)
    {
        _extra = extra;
        Write();
    }

    public IReadOnlyList<FakeCliCall> Calls() =>
        Directory.Exists(EventsDir)
            ? [.. Directory.EnumerateFiles(EventsDir, "call-*.json").Select(Read).OrderBy(c => c.Call)]
            : [];

    /// <summary>The pid of a fake that HELD (<c>holdAfterLine</c>), once it did.</summary>
    public IReadOnlyList<int> Held() =>
        [.. Directory.EnumerateFiles(Home, "held-*").Select(f => int.Parse(Path.GetFileName(f)["held-".Length..], System.Globalization.CultureInfo.InvariantCulture))];

    private void Write()
    {
        var script = (JsonObject)_extra.DeepClone();
        script["stdoutByPrompt"] = _byPrompt.DeepClone();
        File.WriteAllText(Path.Combine(Home, "script.json"), script.ToJsonString());
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
