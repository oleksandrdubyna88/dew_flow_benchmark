using System.Collections;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Process;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelArms.Adapters;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>A clock a test sets by hand: the instant the runner stamps and the zone it writes a deadline in. High-resolution timestamps are the system's, so a wall is measured in real time.</summary>
internal sealed class BenchClock(DateTimeOffset start, TimeZoneInfo zone) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => zone;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>The wait the runner is given: it records every span and returns at once, or does what <see cref="OnWait"/> says.</summary>
internal sealed class RecordingWaits
{
    public List<TimeSpan> Recorded { get; } = [];

    public Func<TimeSpan, CancellationToken, Task>? OnWait { get; set; }

    public Task Wait(TimeSpan span, CancellationToken cancellationToken)
    {
        Recorded.Add(span);
        return OnWait?.Invoke(span, cancellationToken) ?? Task.CompletedTask;
    }
}

/// <summary>Whether claude left a transcript, as a test says it did.</summary>
internal sealed class SettableTranscripts : ISessionTranscripts
{
    public bool Answer { get; set; } = true;

    public bool Exists(string workingDirectory, Guid session) => Answer;
}

/// <summary>A probe that does something at one point.</summary>
internal sealed class ProbeAt(StepPoint point, Action action) : StepProbe
{
    public override void Reached(StepPoint reached)
    {
        if (reached == point)
        {
            action();
        }
    }
}

/// <summary>One arm of the worked example over real folders (E2.S4): a scratch root named <c>testsToDelete-modelarms-…</c> (common.test-leftovers) holding the arm's folder, with its clone a git repository of one commit; the
/// example's <c>steps.*.model</c> pointing at the fake CLI, one minute a step and the limits a test can wait out; a fake CLI; the parent's environment; and a clock, a wait and a log the test reads. The runner under test is
/// the real one, launching the fake through the shared launcher and the host's git by its real path — no port stands in for a file.
/// <para>
/// What this does NOT prove: anything about a real claude — the fake replays recorded streams; or about a git other than the host's.
/// </para></summary>
internal sealed class StepBench : IDisposable
{
    public const string Arm = "C1";

    private static readonly TimeSpan GitBudget = TimeSpan.FromSeconds(30);

    private readonly TempFolder _temp = new();

    public StepBench(Action<JsonObject>? edit = null)
    {
        Fake = new FakeCli();
        Root = _temp.Path;
        Home = _temp.Folder("home");
        Map = ArmMap.Extend(ArmMap.Empty, [Arm], () => [0, 0, 0, 0, 0, 0, 0, 0xc1]);
        Token = Map.TokenOf(Arm).Ok();
        Paths = new ArmPaths(Root, Token);
        Experiment = ExperimentFile.Read(Edited(Fake.Model, edit), new ExperimentLocation(Root, "/x/home", string.Empty, "/x/repo")).Ok();
        Parent = ParentVariables(Home, Fake.Home);
        Clock = new BenchClock(new DateTimeOffset(2026, 10, 28, 21, 32, 38, TimeSpan.Zero), Zone());
        Directory.CreateDirectory(Paths.CloneFolder);
        InitAsync(Paths.CloneFolder, "the first commit").GetAwaiter().GetResult();
    }

    public string Root { get; }

    public string Home { get; }

    public string Token { get; }

    public ArmMap Map { get; }

    public ArmPaths Paths { get; }

    public Experiment Experiment { get; }

    public FakeCli Fake { get; }

    /// <summary>The git every launch of the bench goes by: the host's, by its real path. A test may replace it with a shim.</summary>
    public string Git { get; set; } = ExperimentRoot.RealGit;

    public Dictionary<string, string> Parent { get; }

    public SettableTranscripts Transcripts { get; } = new();

    public BenchClock Clock { get; }

    public RecordingWaits Waits { get; } = new();

    public StringWriter Log { get; } = new();

    public IReadOnlyList<string> LogLines => [.. Log.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>The recorded fixture <paramref name="name"/>'s path.</summary>
    public static string Replay(string name) => ModelArmsFixtures.Path(name);

    private static string Edited(string model, Action<JsonObject>? edit) =>
        BriefExample.Edited(root =>
        {
            foreach (var step in root["steps"]!.AsObject())
            {
                step.Value!["model"] = model;
                step.Value["minutes"] = 1;
            }

            root["limits"] = JsonNode.Parse("""{"waitSeconds":900,"tries":2,"gateWaitSeconds":1800,"gateTries":2,"minimumRemainingSeconds":1,"killGraceSeconds":1}""");
            edit?.Invoke(root);
        });

    private static TimeZoneInfo Zone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Paris", out var paris) ? paris : TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    private static Dictionary<string, string> ParentVariables(string home, string fakeHome)
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home,
            ["PATH"] = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            ["FAKE_CLI_HOME"] = fakeHome,
            ["BENCH_DB"] = "x",
            ["SUBAGENT_MODEL_GUARD"] = "off",
            ["CLAUDE_CODE_DISABLE_BACKGROUND_TASKS"] = "1",
        };

        foreach (var name in new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64" }.Where(name => !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(name))))
        {
            parent[name] = System.Environment.GetEnvironmentVariable(name)!;
        }

        return parent;
    }

    /// <summary>The runner over the bench's seams, launching the fake through the shared launcher.</summary>
    public StepRunner Runner(StepProbe? probe = null) =>
        new(new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance), Transcripts, Clock, Waits.Wait, Log, Map, probe ?? StepProbe.None);

    /// <summary>The context of the step at <paramref name="position"/> of <paramref name="scheme"/>: <c>("C", 0)</c> is <c>1-code</c>, <c>("CR", 1)</c> is <c>2-hreview</c>, <c>("C", 3)</c> is <c>4-gate</c>.</summary>
    public StepContext Context(string scheme, int position) =>
        new(
            Arm,
            Paths,
            Experiment.Schemes.Single(s => s.Name == scheme),
            position,
            Experiment,
            FakeCli.Executable,
            Git,
            Paths.CloneFolder,
            Parent,
            new BriefTemplateFiles(BriefExample.ShippedFolder, _temp.Folder("briefs")),
            new BriefLeakGuard(Map, Arm, [.. Experiment.Schemes.Select(s => s.Name)], Root),
            ScenarioCount: 1,
            TaskFacts.None,
            Problems: string.Empty);

    /// <summary>Replaces the fake's script.</summary>
    public void Script(JsonObject script) => Fake.Rewrite(script);

    /// <summary>A script that replays one recorded stream on every call.</summary>
    public void Replays(string fixture, JsonObject? more = null)
    {
        var script = more ?? [];
        script["stdoutFile"] = Replay(fixture);
        Fake.Rewrite(script);
    }

    public string OutFile(string name) => Path.Combine(Paths.Out, name);

    public string ReadOut(string name) => File.ReadAllText(OutFile(name));

    public IReadOnlyList<TimingLine> Timing() => File.Exists(Paths.Timing) ? TimingLines.Read(File.ReadAllText(Paths.Timing)) : [];

    public string NotesFile(string relative) => Path.Combine(Paths.Folder, relative);

    /// <summary>The id <c>.session</c> holds for <paramref name="step"/>.</summary>
    public string SessionOf(string step) => File.ReadAllText(Paths.Session(step)).Trim();

    /// <summary>The clone's HEAD, read by the host's git.</summary>
    public async Task<string> HeadAsync(string? folder = null) => (await GitAsync(folder ?? Paths.CloneFolder, "rev-parse", "HEAD")).Trim();

    /// <summary>Commits a file in the clone (or in <paramref name="folder"/>): what an arm's work looks like to the diff.</summary>
    public async Task CommitFileAsync(string relative, string text, string message, string? folder = null)
    {
        var target = folder ?? Paths.CloneFolder;
        await File.WriteAllTextAsync(Path.Combine(target, relative), text, TestContext.Current.CancellationToken);
        await GitAsync(target, "add", relative);
        await GitAsync(target, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message);
    }

    /// <summary>The object name <paramref name="revision"/> resolves to in the clone.</summary>
    public async Task<string> RevParseAsync(string revision) => (await GitAsync(Paths.CloneFolder, "rev-parse", revision)).Trim();

    /// <summary>A second repository of one commit, its HEAD different from the clone's.</summary>
    public async Task<string> NewRepositoryAsync(string name)
    {
        var folder = _temp.Folder(name);
        await InitAsync(folder, "another repository's commit");
        return folder;
    }

    /// <summary>A <c>0755</c> shell script with the given body after the shebang.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public string ShimScript(string path, string body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(path, (UnixFileMode)Convert.ToInt32("755", 8));
        return path;
    }

    public string Combine(params string[] parts) => _temp.Combine(parts);

    public string Write(string text, params string[] parts) => _temp.File(text, parts);

    private static async Task InitAsync(string folder, string message)
    {
        await GitAsync(folder, "init", "--quiet", "--initial-branch", "main");
        await GitAsync(folder, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "--allow-empty", "-m", message);
    }

    private static async Task<string> GitAsync(string folder, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync(ExperimentRoot.RealGit, arguments, folder, GitBudget, string.Empty, null, TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>().Subject;
        done.Result.ExitCode.Should().Be(0, $"git {arguments[0]} builds the bench's repository: {done.Result.Output}");
        return done.Result.StandardOutput;
    }

    public void Dispose()
    {
        Fake.Dispose();
        _temp.Dispose();
    }
}

/// <summary>What the step runner's tests read off an outcome, a call and the timing — said once.</summary>
internal static class StepAsserts
{
    public static StepOutcome.Done IsDone(this StepOutcome outcome) => outcome.Should().BeOfType<StepOutcome.Done>($"the step should be done, not {outcome}").Subject;

    public static StepOutcome.Stopped IsStopped(this StepOutcome outcome, int exit)
    {
        var stopped = outcome.Should().BeOfType<StepOutcome.Stopped>($"the step should have stopped with {exit}, not {outcome}").Subject;
        stopped.ExitCode.Should().Be(exit, stopped.Why);
        return stopped;
    }

    /// <summary>The value after <paramref name="flag"/> in the call's argv.</summary>
    public static string After(this FakeCliCall call, string flag)
    {
        var at = call.Argv.ToList().IndexOf(flag);
        at.Should().BeGreaterThanOrEqualTo(0, $"call {call.Call}'s argv should carry {flag}: {string.Join(' ', call.Argv)}");
        return call.Argv[at + 1];
    }

    public static TimingLine.Stopped LastStopped(this StepBench bench) => bench.Timing().Last().Should().BeOfType<TimingLine.Stopped>("the last timing line is the stop").Subject;

    /// <summary>An unlimited end line of <paramref name="seconds"/> of wall for attempt 1 of <paramref name="step"/>, so the budget the next attempt gets is what is left.</summary>
    public static void SpendWall(this StepBench bench, string step, int seconds)
    {
        Directory.CreateDirectory(bench.Paths.Out);
        var end = new TimingLine.End(StepBench.Arm, step, 1, bench.Clock.GetUtcNow(), Bench.Domain.Trace.CapturedCount.Number(seconds), Bench.Domain.Trace.CapturedCount.Number(0), true, false, "abc1234", "finished", false);
        File.AppendAllText(bench.Paths.Timing, TimingLines.Write(end) + "\n");
    }
}
