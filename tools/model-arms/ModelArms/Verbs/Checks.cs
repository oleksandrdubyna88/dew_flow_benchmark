using System.Text;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Checks;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>What <c>checks</c> is handed that production never changes: the clock every stamp and every stage's duration comes from, a wall that REPLACES the suite's (<see cref="TargetWalls"/>) — so a test whose suite never ends is bounded in seconds, not in the hours the real wall allows; the other tools keep their real walls — and a call
/// made when the report is on disk and the marker is not yet, which is how a test sees the marker LAST. Internal, so only the tests can hand in another.</summary>
internal sealed record ChecksSeams(TimeProvider Clock, TimeSpan? SuiteWall, Action? AfterReport = null)
{
    public static ChecksSeams Real { get; } = new(TimeProvider.System, null);

    /// <summary>The command with the seam's wall, when it is the suite's and there is one.</summary>
    public ToolCommand Walled(ToolCommand command) => command.Label == "suite" && SuiteWall is { } wall ? command with { Wall = wall } : command;
}

/// <summary><c>checks &lt;arm&gt; [--parse-only]</c> (plan §4.9, D17, D26; <c>objective-checks.sh</c>): the objective report of one made arm — the arm's own code (its build, its tests, its npm scripts) run by the target's tools, then what that code could have written READ back — <c>out/objective.json</c> in the shell's shape plus
/// <c>stepsOnDeadline</c>, the summary line, and the marker <c>chain/objective-&lt;arm&gt;</c> LAST. Admission is <see cref="ChecksAdmission"/> (the scheme, the token, the PATH and the two resolved-executable lists, a runner that is not alive, the baseline and the scenarios — each refusal ends the verb before a program starts); the stages are
/// <see cref="ObjectiveRun"/>; the reading and the writing are <see cref="ChecksReport"/>. Exit 0 the report is written (a failing build or suite is a RESULT, not a failure of the verb); 3 the host or the target could not be measured (a tool that did not start, git that cannot answer, a folder replaced while the arm's code ran, a file that cannot be written);
/// 4 a wrong configuration or a refused file; 5 the operator's stop (Program's mapping) — the same command redoes it.
/// <para>
/// <b>The trust boundary (D17, D26; E3 §8 item 17).</b> The arm's code runs with the operator's rights — that IS the measurement — and the harness's guarantees are about ITS executables (resolved, printed, launched by the real path under <see cref="TargetEnvironment"/>), ITS files (created exclusively, held, or staged and renamed over their name) and the model's files it READS (bounded, no link followed,
/// no FIFO waited on). <b>The report is not authenticated against that code:</b> with the operator's rights it can rewrite <c>objective.json</c> after this verb ends, and the CTRF is its suite's own output by nature. The exclusive logs, the held-stream read-back and the folder's identity check stop the harness from FOLLOWING or CONSUMING a substitution while it measures; they are not a claim that the numbers are
/// true. <b>Residuals:</b> the launcher keeps every output line in memory (item 18); a total is the FIRST <c>total:</c> anywhere in the log, so a failing test's message can set it (item 27 e); an uncommitted edit inside a submodule's worktree is not counted (item 40); a program the arm's code left running keeps its rights after the verb ends.
/// </para></summary>
public static class ChecksVerb
{
    private const string Name = "model-arms checks";

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, ChecksSeams.Real);

    internal static async Task<int> RunAsync(ExperimentInput input, ChecksSeams seams)
    {
        var done = await ChecksAdmission.AdmitAsync(input).ThenAsync(plan => MeasureAsync(plan, seams));
        return done.Match(code => code, stop => stop.Print(input.Verb.Error));
    }

    private static Task<Passage<int>> MeasureAsync(ChecksPlan plan, ChecksSeams seams)
    {
        var git = new ArmGit(plan.Tools.Git, plan.Paths.CloneFolder, plan.Input.Verb.Environment);
        return plan.ParseOnly ? ParseOnlyAsync(plan, seams, git) : FullAsync(plan, seams, git);
    }

    /// <summary>Full mode (step 5, then 6–9): the folder made and the logs held, the four stages, git's facts, the folder's identity, <c>stages.json</c>, the held logs read back, the report. A stop travels up as the cancellation it is; every other failure is exit 3 and no report.</summary>
    private static async Task<Passage<int>> FullAsync(ChecksPlan plan, ChecksSeams seams, ArmGit git)
    {
        var ct = plan.Input.Verb.Stopping;
        var stamp = BaseLeftovers.Stamp(seams.Clock.GetUtcNow());
        var began = ObjectiveRun.Begin(plan.Paths, stamp, PlatformFactsReader.CurrentRoad);
        if (began is not Outcome<ObjectiveRun>.Ok { Value: var run })
        {
            return Stopped(plan, ((Outcome<ObjectiveRun>.Fail)began).Reason);
        }

        using (run)
        {
            if (run.KeptAside.Length > 0)
            {
                plan.Say($"{Name} {plan.Arm}: the earlier out/objective is kept as {run.KeptAside}");
            }

            var target = plan.Input.Experiment.Target;
            var tools = new TargetTools(TargetEnvironment.Of(plan.Input.Verb.Environment), seams.Clock);
            var stages = new ObjectiveStages(tools, target, plan.Tools, plan.Paths.CloneFolder, plan.Paths.ObjectiveFolder, seams.Walled, token => git.ChangedCsAsync(target.Base.Value, token));
            var ran = await run.RunAsync(stages, ct);
            if (ran is not Outcome<StageRuns>.Ok { Value: var runs })
            {
                return Stopped(plan, ((Outcome<StageRuns>.Fail)ran).Reason);
            }

            var facts = await ChecksReport.GitAsync(plan, git, ct);
            if (facts is not Outcome<GitFacts>.Ok { Value: var gathered })
            {
                return Stopped(plan, ((Outcome<GitFacts>.Fail)facts).Reason);
            }

            return Replaced(plan, run) is { Length: > 0 } changed
                ? Stopped(plan, $"out/objective was replaced while the arm's code ran — no report ({changed})", reportWord: false)
                : Published(plan, seams, run, runs, gathered);
        }
    }

    /// <summary>Everything after the folder is known to be the one made: <c>stages.json</c> staged and renamed over its name, the two logs read back from the handles, the report.</summary>
    private static Passage<int> Published(ChecksPlan plan, ChecksSeams seams, ObjectiveRun run, StageRuns runs, GitFacts git)
    {
        if (run.Record(runs) is Outcome<bool>.Fail failed)
        {
            return Stopped(plan, failed.Reason);
        }

        var suite = ChecksReport.LogText(plan, "mcp-tests.log", run.SuiteText());
        var extension = ChecksReport.LogText(plan, "ts-tests.log", run.ExtensionText());
        return ChecksReport.Publish(plan, ChecksReport.Facts(plan, runs, git, suite, extension), seams);
    }

    private static string Replaced(ChecksPlan plan, ObjectiveRun run) => run.StillTheFolderMade();

    /// <summary><c>--parse-only</c>: no stage runs; the exits are <c>stages.json</c>'s (absent: four exits nobody recorded; refused: exit 4), the four logs and the CTRF are read by their paths through the bounded road, and git is asked as in a full run.</summary>
    private static async Task<Passage<int>> ParseOnlyAsync(ChecksPlan plan, ChecksSeams seams, ArmGit git)
    {
        var recorded = StagesOf(plan);
        if (recorded is not Passage<StageRuns>.Through { Value: var runs })
        {
            return new Passage<int>.Stopped(((Passage<StageRuns>.Stopped)recorded).ExitCode, ((Passage<StageRuns>.Stopped)recorded).Lines);
        }

        var facts = await ChecksReport.GitAsync(plan, git, plan.Input.Verb.Stopping);
        if (facts is not Outcome<GitFacts>.Ok { Value: var gathered })
        {
            return Stopped(plan, ((Outcome<GitFacts>.Fail)facts).Reason);
        }

        var suite = ChecksReport.LogByPath(plan, plan.Paths.ObjectiveSuiteLog);
        var extension = ChecksReport.LogByPath(plan, plan.Paths.ObjectiveExtensionLog);
        return ChecksReport.Publish(plan, ChecksReport.Facts(plan, runs, gathered, suite, extension), seams);
    }

    private const int StagesLimit = 64 * 1024;

    private static Passage<StageRuns> StagesOf(ChecksPlan plan) =>
        BoundedFile.Read(plan.Paths.ObjectiveStages, StagesLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Absent => new Passage<StageRuns>.Through(StageRecord.Unrecorded),
            BoundedRead.Refused refused => Passage<StageRuns>.Stop(ExitCodes.Configuration, $"{Name} {plan.Arm}: stages.json is refused: {refused.Reason}"),
            BoundedRead.Bytes bytes => StageRecord.Read(Encoding.UTF8.GetString(bytes.Value)).Match(
                read => new Passage<StageRuns>.Through(read),
                reason => Passage<StageRuns>.Stop(ExitCodes.Configuration, $"{Name} {plan.Arm}: {reason}")),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static Passage<int> Stopped(ChecksPlan plan, string reason, bool reportWord = true) =>
        Passage<int>.Stop(ExitCodes.Environment, $"{Name} {plan.Arm}: {reason}{(reportWord ? " — no report" : string.Empty)}");
}
