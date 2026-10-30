using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>What one <c>prep-base</c> run knows once validate's checks have passed: the input, the host's facts (whose resolved executables are the ONLY paths launched — never looked up again), the paths built from them, the seams and the environment the target's tools run under.</summary>
internal sealed record PrepBaseRun(ExperimentInput Input, HostFacts Facts, ToolPaths Tools, PrepBaseSeams Seams, IReadOnlyDictionary<string, string> ToolEnvironment)
{
    public TargetFacts Target => Input.Experiment.Target;

    public RootPaths Root => Input.Root;

    /// <summary>The environment git runs under: the target's, with no <c>GIT_*</c> either way.</summary>
    public IReadOnlyDictionary<string, string> GitEnvironment => Core.Experiment.GitEnvironment.Of(ToolEnvironment);

    public void Say(string text) => Input.Verb.Output.WriteLine($"model-arms prep-base: {text}");
}

/// <summary><c>prep-base</c> (plan §4.1, D18, D24; <c>prep-base.sh</c>): the baseline — the failures of both of the target's suites at <c>target.base</c>, before any arm touched it — written once as <c>baseline.json</c>. It crosses the module's trust boundary: four executables of the target are launched
/// (<c>git</c>, the node wrapper's <c>node</c>, <c>dotnet</c> or the suite's own, <c>npm</c>), so each goes by the REAL path validate's checks resolved and printed (D17 c, d), under <see cref="TargetEnvironment"/>, from a folder the verb names, and every log is created exclusively.
/// <para>
/// In order: (1) validate's checks — any problem ends the verb with validate's exit and launches nothing; (2) a half-made <c>base/</c> is removed whole, an earlier attempt's logs and results are moved into <c>prep-base.prev-&lt;stamp&gt;</c>; (3) the plan branch cloned and detached at <c>target.base</c>, HEAD asserted;
/// (4) the rules submodule from the local source; (5) the stages (<see cref="BaseStages"/>); (6) the baseline composed from the two logs — <b>never from a suite that did not run to completion</b> (no summary captured, killed at its wall, an exit with no name: <see cref="BaselineFile.Compose"/> refuses it) — and written. Exit 0 the baseline is written; 3 the host or the
/// target could not be measured (a tool not started, a base that does not build, a suite that did not complete); 4 a wrong configuration (the rules submodule is not one).
/// </para>
/// <para>
/// What this does not do: bound a tool's output in memory (E3 §8 item 18), or judge the target's own code, which runs with the operator's rights by design (item 17) — the baseline is measured, not authenticated.
/// </para></summary>
public static class PrepBaseVerb
{
    private const string Name = "model-arms prep-base";

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, GateSeams.Real, PrepBaseSeams.Real);

    /// <summary>The same with the host's reads and the clock and wall handed in.</summary>
    internal static Task<int> RunAsync(ExperimentInput input, GateSeams gate, PrepBaseSeams seams) =>
        ValidateVerb.CheckedAsync(input, gate, facts => MeasureAsync(input, facts, seams));

    private static async Task<int> MeasureAsync(ExperimentInput input, HostFacts facts, PrepBaseSeams seams)
    {
        var environment = TargetEnvironment.Of(input.Verb.Environment);
        var tools = ToolPaths.Of(input.Experiment.Target, name => RealPathOf(facts, name));
        var run = new PrepBaseRun(input, facts, tools, seams, environment);
        var passage = await Task.FromResult(Unresolved(run))
            .Then(_ => Cleaned(run))
            .ThenAsync(_ => BaseCheckout.MakeAsync(run.Target, input.Root.Root, input.Root.Base, tools.Git, run.GitEnvironment, input.Verb.Stopping))
            .ThenAsync(head => StagedAsync(run, head));
        return passage.Match(done => done, stop => stop.Print(input.Verb.Error));
    }

    /// <summary>The path <see cref="HostChecks"/> resolved for a name, as the real path printed — or the empty text when it did not resolve (validate's checks have refused that already, so this is the second line of a defence nobody expects to need).</summary>
    private static string RealPathOf(HostFacts facts, string name) =>
        facts.Executables.FirstOrDefault(executable => executable.Needed.Name == name)?.Answer is ExecutableAnswer.Found found ? found.Real : string.Empty;

    private static Passage<bool> Unresolved(PrepBaseRun run) =>
        run.Tools.Missing(run.Target) is { Count: > 0 } missing
            ? Passage<bool>.Stop(ExitCodes.Environment, $"{Name}: no executable was resolved for {string.Join(", ", missing)} — nothing is launched")
            : new Passage<bool>.Through(true);

    /// <summary>Step 2: a <c>base/</c> with no baseline beside it is a crashed run's, and goes whole; the last attempt's logs and results are moved aside so this attempt's exclusive creates succeed (plan gate F1).</summary>
    private static Passage<bool> Cleaned(PrepBaseRun run)
    {
        var removed = BaseLeftovers.RemoveBase(run.Root.Base);
        if (removed is Outcome<bool>.Fail failed)
        {
            return Passage<bool>.Stop(ExitCodes.Environment, $"{Name}: {failed.Reason}");
        }

        if (removed is Outcome<bool>.Ok { Value: true })
        {
            run.Say($"removed {run.Root.Base}, a half-made base of a run that did not finish; it is made again");
        }

        var stamp = BaseLeftovers.Stamp(run.Seams.Clock.GetUtcNow());
        var moved = BaseLeftovers.MoveAside(run.Root, run.Target.NodeModules.Count, stamp, PlatformFactsReader.CurrentRoad);
        return moved.Match(
            names =>
            {
                if (names.Count > 0)
                {
                    run.Say($"moved the last attempt's {string.Join(", ", names)} into {run.Root.PrepBasePrevious(stamp)}");
                }

                return (Passage<bool>)new Passage<bool>.Through(true);
            },
            reason => Passage<bool>.Stop(ExitCodes.Environment, $"{Name}: {reason}"));
    }

    private static async Task<Passage<int>> StagedAsync(PrepBaseRun run, string head)
    {
        var made = ToolLog.Create(run.Root.PrepBaseLog);
        if (made is not Outcome<ToolLog>.Ok created)
        {
            return Passage<int>.Stop(ExitCodes.Environment, $"{Name}: {((Outcome<ToolLog>.Fail)made).Reason}");
        }

        using var stages = created.Value;
        var tools = new TargetTools(run.ToolEnvironment, run.Seams.Clock);
        var measured = await new BaseStages(run, tools, stages, head, run.Input.Verb.Stopping).RunAsync();
        return measured.Bind(done => Baselined(run, done));
    }

    /// <summary>Step 6: each suite is a <see cref="SuiteRun"/> that COMPLETED only when its tool exited and its log carries the total — the baseline is composed from two completed runs or not at all.</summary>
    private static Passage<int> Baselined(PrepBaseRun run, Measured measured)
    {
        var mtp = TestLogSummary.Read(Text(run, measured.SuiteLog), run.Target.Suite.NamePrefix);
        var suite = SuiteOf(measured.Suite, measured.SuiteLog, [.. mtp.Failed.Distinct(StringComparer.Ordinal)], mtp.Total);
        var node = TsTestLog.Read(Text(run, measured.ExtensionLog));
        var extension = SuiteOf(measured.Extension, measured.ExtensionLog, node.Failed, node.Tests);
        var host = $"{Environment.MachineName} ({run.Facts.Host})";
        return BaselineFile.Compose(run.Target.Base, run.Seams.Clock.GetUtcNow(), host, suite, extension, string.Empty).Match(
            baseline => Written(run, baseline),
            reason => Passage<int>.Stop(ExitCodes.Environment, [.. reason.Split('\n').Select(sentence => $"{Name}: {sentence} — no baseline is written")]));
    }

    private static SuiteRun SuiteOf(ToolRun ran, Outcome<string> log, IReadOnlyList<string> failed, Bench.Domain.Trace.CapturedCount total) =>
        new(ran.Exit.WasCaptured && total.WasCaptured && log is Outcome<string>.Ok, ran.Exit.WasCaptured ? (int)ran.Exit.Value : -1, failed, total.WasCaptured ? (int)Math.Min(total.Value, int.MaxValue) : 0);

    private static string Text(PrepBaseRun run, Outcome<string> log)
    {
        if (log is Outcome<string>.Fail fail)
        {
            run.Input.Verb.Error.WriteLine($"{Name}: {fail.Reason} — it is not read");
        }

        return log is Outcome<string>.Ok ok ? ok.Value : string.Empty;
    }

    private static Passage<int> Written(PrepBaseRun run, Baseline baseline) =>
        run.Root.Of(run.Input.Experiment.BaselineFile).Match(
            path => Written(run, baseline, path),
            reason => Passage<int>.Stop(ExitCodes.Configuration, $"{Name}: {reason}"));

    private static Passage<int> Written(PrepBaseRun run, Baseline baseline, string path)
    {
        try
        {
            ArmFiles.WriteFileOnce(path, BaselineFile.Write(baseline) + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<int>.Stop(ExitCodes.Environment, $"{Name}: {path} cannot be written: {ex.Message}");
        }

        run.Say($"baseline written to {path}: base {baseline.Base.Value}, the main suite {baseline.SuiteTotal} tests with {baseline.McpFailed.Count} failing, the extension {baseline.ExtensionTotal} tests with {baseline.TsFailed.Count} failing");
        return new Passage<int>.Through(ExitCodes.Pass);
    }
}
