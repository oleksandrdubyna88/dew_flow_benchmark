using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Process;
using FluentAssertions;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scenarios of the oracle target: the shell's two kinds, with every outcome <c>passing</c> can say — passed, failed, mixed, absent — and a node scenario of each.</summary>
internal static class ObjectiveScenarios
{
    public static readonly IReadOnlyList<Scenario> All =
    [
        new("S1", "cs", "TheSpentVerdict_IsHopeless"),
        new("S2", "cs", "TheLadder_Climbs"),
        new("S3", "cs", "TheMixedCase_ReadsBoth"),
        new("S4", "cs", "TheMissingCase_IsNeverRun"),
        new("S5", "node", "a nested node scenario"),
        new("S6", "node", "a failing node scenario"),
        new("S7", "node", "a node scenario nobody ran"),
    ];
}

/// <summary>Defaults for an <see cref="ObjectiveFacts"/> a test fills in by hand, and the readers of the report's text. The defaults are a finished, passing arm; a test names only what it changes.</summary>
internal static class ObjectiveFactsKit
{
    public const string Prefix = "CoaiMcp.Tests.";

    private static readonly CommitSha Base = CommitSha.Parse(new string('a', 40)).Ok();

    public static Baseline EmptyBaseline() => BaselineFile.Compose(Base, DateTimeOffset.UnixEpoch, "test-host", new SuiteRun(true, 0, [], 10), new SuiteRun(true, 0, [], 10), string.Empty).Ok();

    public static Baseline BaselineOf(IReadOnlyList<string> mcpFailed, IReadOnlyList<string> tsFailed) =>
        EmptyBaseline() with { McpFailed = mcpFailed, TsFailed = tsFailed };

    public static StageExits Exits(long build = 0, long suite = 0, long format = 0, long extension = 0) =>
        new(CapturedCount.Number(build), CapturedCount.Number(suite), CapturedCount.Number(format), CapturedCount.Number(extension));

    public static StageExits UnknownExits() =>
        new(CapturedCount.Unavailable("x"), CapturedCount.Unavailable("x"), CapturedCount.Unavailable("x"), CapturedCount.Unavailable("x"));

    public static ObjectiveFacts Facts() =>
        new(
            "C1",
            "f8a595ab",
            CapturedCount.Number(14),
            0,
            Exits(),
            string.Empty,
            Outcome<string>.Failure("no report was written"),
            string.Empty,
            [],
            new Dictionary<string, bool>(),
            EmptyBaseline(),
            Prefix,
            string.Empty,
            string.Empty,
            []);

    public static JsonElement Parse(string text) => JsonDocument.Parse(text).RootElement.Clone();

    /// <summary>The strings of the array at <paramref name="path"/>; empty when a member on the way is missing, so a stub that writes nothing fails the test's assertion and not a lookup.</summary>
    public static IReadOnlyList<string> Strings(JsonElement report, params string[] path) =>
        Walk(report, path) is { ValueKind: JsonValueKind.Array } array ? [.. array.EnumerateArray().Select(e => e.GetString()!)] : [];

    /// <summary>The value at <paramref name="path"/> as text — a string as it is, a number, <c>true</c>, <c>false</c> or <c>null</c> as JSON spells it — and <c>&lt;missing&gt;</c> when a member on the way is absent, so a stub fails on the value and not on a lookup.</summary>
    public static string Value(JsonElement report, params string[] path) =>
        Walk(report, path) is { } found ? (found.ValueKind == JsonValueKind.String ? found.GetString()! : found.GetRawText()) : "<missing>";

    public static JsonElement? Walk(JsonElement report, params string[] path)
    {
        JsonElement? at = report;
        foreach (var name in path)
        {
            at = at is { ValueKind: JsonValueKind.Object } here && here.TryGetProperty(name, out var next) ? next : null;
        }

        return at;
    }

    /// <summary>Every member path of a report: its name and nesting in the order written, and the JSON kind of its value. The scenario map's entries count once, as <c>scenarios.*</c>; an array is one path and its items are not
    /// read (a list's length is a fact, not the shape).</summary>
    public static IReadOnlyList<string> Paths(JsonElement report) => [.. Paths(report, string.Empty)];

    private static IEnumerable<string> Paths(JsonElement element, string prefix)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var member in element.EnumerateObject())
        {
            var name = prefix.Length == 0 ? member.Name : $"{prefix}.{member.Name}";
            yield return $"{name}: {Kind(member.Value)}";
            var children = name == "scenarios" ? FirstEntryOf(member.Value, name) : Paths(member.Value, name);
            foreach (var child in children)
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<string> FirstEntryOf(JsonElement map, string prefix) =>
        map.EnumerateObject().Take(1).SelectMany(entry => Paths(entry.Value, $"{prefix}.*"));

    private static string Kind(JsonElement value) =>
        value.ValueKind is JsonValueKind.True or JsonValueKind.False ? "boolean" : value.ValueKind.ToString().ToLowerInvariant();
}

/// <summary>What the shell's own reader (<c>objective-checks.sh:27-67</c>, the heredoc, verbatim) wrote and said over one set of inputs.</summary>
internal sealed record OracleRun(string Report, string SummaryLine);

/// <summary>The oracle's world: a repository laid out as the coai target (tests under <c>src_mcp/tests</c> and <c>src_vs_code/src/test</c>, a source file, a document, a binary and a tool; a base commit, then two), the
/// root's <c>baseline.json</c> and <c>scenarios.json</c>, and an arm's <c>out/objective/</c> holding the three recorded fixtures under the shell's names. The shell's reader is run over it as <c>node - …</c> with the script on
/// stdin, the way the shell ran it; the facts the port is given are gathered here with the real git, the way <c>checks</c> will (E3.S5). Nothing here is production code: this is the measuring stick.
/// <para>
/// What this does NOT prove: that a reader of another target's layout (the shell's three <c>changedLines</c> rules are the coai target's), or of logs the recorded fixtures do not hold, agrees with the shell's.
/// </para></summary>
internal sealed class ObjectiveBench : IDisposable
{
    private const string Arm = "C1";
    private const string McpFailedInBaseline = "OtherTests.ASlowCase_TimesOut";
    private const string TsFailedInBaseline = "an old flaky node test";
    private const string ShellReaderSha256 = "285d01135cf31bad3220faf255d7be5e94b663c18f94b669662c0cdbf6e7d42b";

    private readonly TempFolder _temp = new();

    private ObjectiveBench()
    {
    }

    /// <summary>The commit the arm's work is measured against, a 40-hex sha.</summary>
    public string Base { get; private set; } = string.Empty;

    public Baseline Baseline { get; private set; } = ObjectiveFactsKit.EmptyBaseline();

    public string Repository => _temp.Combine("repo");

    public string Root => _temp.Combine("root");

    public string Objective => _temp.Combine("root", "r", "x", "out", "objective");

    public string ReportFile => _temp.Combine("root", "r", "x", "out", "objective.json");

    public string Log(string name) => File.ReadAllText(Path.Combine(Objective, name));

    public static async Task<ObjectiveBench> CreateAsync()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the oracle runs the shell's reader with git and node on a POSIX PATH");
        Assert.False(PinnedConsumer.NodePath.Length == 0, "a node must resolve on the test process's PATH to run the shell's reader");
        var bench = new ObjectiveBench();
        try
        {
            await bench.BuildAsync();
            return bench;
        }
        catch
        {
            bench.Dispose();
            throw;
        }
    }

    private async Task BuildAsync()
    {
        File.CreateSymbolicLink(Path.Combine(_temp.Folder("bin"), "git"), ExperimentRoot.RealGit);
        var repo = _temp.Folder("repo");
        await GitAsync(repo, "init", "--quiet", "--initial-branch", "main");
        _temp.File("class A {\n}\n", "repo", "src_mcp", "src", "A.cs");
        _temp.File("# notes\n", "repo", "research", "x.md");
        Base = await CommitAsync(repo, "base");

        _temp.File("class A {\n  int B;\n  int C;\n  int D;\n  int E;\n}\n", "repo", "src_mcp", "src", "A.cs");
        _temp.File("// TheSpentVerdict_IsHopeless\n// TheLadder_Climbs\n// TheMixedCase_ReadsBoth\n", "repo", "src_mcp", "tests", "ATests.cs");
        _temp.File("// a nested node scenario\n// a failing node scenario\n", "repo", "src_vs_code", "src", "test", "a.test.ts");
        File.WriteAllBytes(Path.Combine(_temp.Folder("repo", "src_vs_code"), "icon.png"), [0x89, 0x50, 0x4e, 0x47, 0x00, 0x00, 0x01, 0x02]);
        await CommitAsync(repo, "tests and a source file");

        _temp.File("# notes\none\ntwo\nthree\n", "repo", "research", "x.md");
        _temp.File("export {};\n", "repo", "tools", "b.mjs");
        await CommitAsync(repo, "a document and a tool");

        Baseline = BaselineFile.Compose(
            CommitSha.Parse(Base).Ok(),
            DateTimeOffset.UnixEpoch,
            "test-host",
            new SuiteRun(true, 1, [McpFailedInBaseline], 100),
            new SuiteRun(true, 1, [TsFailedInBaseline], 50),
            "the oracle's baseline").Ok();
        _temp.File(BaselineFile.Write(Baseline), "root", "baseline.json");
        _temp.File(ScenariosJson(), "root", "scenarios.json");
        foreach (var (fixture, name) in new[] { ("mtp.log", "mcp-tests.log"), ("node.log", "ts-tests.log"), ("ctrf.json", "mcp.ctrf.json") })
        {
            _temp.File(ModelArmsFixtures.Read("objective/" + fixture), "root", "r", "x", "out", "objective", name);
        }
    }

    private static string ScenariosJson() =>
        JsonSerializer.Serialize(ObjectiveScenarios.All.Select(s => new { id = s.Id, kind = s.Kind, name = s.Name }));

    /// <summary>The facts <c>checks</c> will gather (E3.S5), gathered with the real git over the oracle's repository: the arm exited as the oracle is told (<c>mcp</c> 1, the rest 0), the logs are the fixtures.</summary>
    public async Task<ObjectiveFacts> FactsAsync()
    {
        var present = new Dictionary<string, bool>();
        foreach (var scenario in ObjectiveScenarios.All)
        {
            var found = await GitAsync(Repository, "grep", "-l", "-F", scenario.Name, "--", "src_mcp/tests", "src_vs_code/src/test");
            present[scenario.Id] = found.Length > 0;
        }

        return new ObjectiveFacts(
            Arm,
            await GitAsync(Repository, "rev-parse", "--short", "HEAD"),
            CapturedCount.Number(long.Parse(await GitAsync(Repository, "rev-list", "--count", $"{Base}..HEAD"), CultureInfo.InvariantCulture)),
            0,
            ObjectiveFactsKit.Exits(suite: 1),
            Log("mcp-tests.log"),
            Outcome<string>.Success(Log("mcp.ctrf.json")),
            Log("ts-tests.log"),
            ObjectiveScenarios.All,
            present,
            Baseline,
            ObjectiveFactsKit.Prefix,
            await GitAsync(Repository, "diff", "--numstat", $"{Base}..HEAD"),
            await GitAsync(Repository, "diff", "--shortstat", $"{Base}..HEAD"),
            []);
    }

    /// <summary>The shell's reader, run as the shell ran it: <c>node - &lt;O&gt; &lt;E&gt; &lt;arm&gt; &lt;BASE&gt; 0 0 1 0 0</c> with the script on stdin, in the repository, node by its resolved path, <c>git</c> found on a PATH holding only the host's git.</summary>
    public async Task<OracleRun> RunOracleAsync(CancellationToken cancellationToken)
    {
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ModelArmsFixtures.Path("objective/objective-checks-reader.mjs")))).Should().Be(ShellReaderSha256, "the oracle is the shell's heredoc, byte for byte");
        var environment = new Dictionary<string, string> { ["PATH"] = Path.Combine(_temp.Path, "bin"), ["HOME"] = _temp.Path };
        string[] arguments = ["-", Objective, Root, Arm, Base, "0", "0", "1", "0", "0"];
        var attempt = await ProcessRunner.RunAsync(PinnedConsumer.NodePath, arguments, Repository, TimeSpan.FromSeconds(120), ModelArmsFixtures.Read("objective/objective-checks-reader.mjs"), environment, cancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        done.ExitCode.Should().Be(0, done.Output);
        return new OracleRun(File.ReadAllText(ReportFile), done.StandardOutput.TrimEnd('\n'));
    }

    private async Task<string> CommitAsync(string repo, string message)
    {
        await GitAsync(repo, "add", "-A");
        await GitAsync(repo, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message);
        return await GitAsync(repo, "rev-parse", "HEAD");
    }

    private async Task<string> GitAsync(string repo, params string[] arguments)
    {
        var environment = new Dictionary<string, string> { ["PATH"] = Path.Combine(_temp.Path, "bin"), ["HOME"] = _temp.Path };
        var attempt = await ProcessRunner.RunAsync(ExperimentRoot.RealGit, arguments, repo, TimeSpan.FromMinutes(1), string.Empty, environment, CancellationToken.None);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        // `git grep` answers 1 for "nothing found", which is an answer; every other non-zero exit is the fixture failing.
        return done.ExitCode == 0 || (arguments[0] == "grep" && done.ExitCode == 1)
            ? done.StandardOutput.Trim()
            : throw new InvalidOperationException($"git {arguments[0]} failed in the oracle's repository: {done.Output}");
    }

    public void Dispose() => _temp.Dispose();
}
