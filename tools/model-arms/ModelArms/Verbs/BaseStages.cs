using System.Globalization;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>What <c>prep-base</c> hands on to the baseline: how each suite ended and the text its log holds (read back through the handle the verb made it with), or why the log could not be read.</summary>
internal sealed record Measured(ToolRun Suite, Outcome<string> SuiteLog, ToolRun Extension, Outcome<string> ExtensionLog);

/// <summary>The stages of <c>prep-base</c> (<c>prep-base.sh:12-17</c>, E3.S2 step 5), in the shell's order: the rules submodule's <c>npm ci</c> (scripts ignored), the build, every other <c>npm ci</c>, the main suite, the extension's tests. Each tool's log is created exclusively BEFORE the tool starts and a line
/// per stage is appended to <c>prep-base.log</c> in the shell's shape (<c>build rc=0 s=65 head=8b1eef7a</c>), the last being <c>DONE</c>. A tool that did not start, an <c>npm</c> or a build that did not exit 0, stops the walk — exit 3 — and nothing after it runs. A suite that exits non-zero is not a stop: its failures
/// are what the baseline is made of; whether it RAN TO COMPLETION is the baseline's question.</summary>
internal sealed class BaseStages(PrepBaseRun run, ITargetTools tools, ToolLog stages, string head, CancellationToken ct)
{
    private const string Name = "model-arms prep-base";
    private const int LogLimit = 64 * 1024 * 1024;

    private int _npm;

    private sealed record StageRun(ToolRun Run, Outcome<string> Log);

    public Task<Passage<Measured>> RunAsync()
    {
        var clone = run.Root.Base;
        var target = run.Target;
        return InOrderAsync(TargetArgv.RulesNpmCi(target, run.Tools, clone))
            .ThenAsync(_ => OneAsync("build", TargetArgv.Build(target, run.Tools, clone), run.Root.BaseBuildLog, mustExitZero: true))
            .ThenAsync(_ => InOrderAsync(TargetArgv.OtherNpmCi(target, run.Tools, clone)))
            .Then(_ => ResultsFolder())
            .ThenAsync(_ => OneAsync("mcp tests", TargetArgv.Suite(target, run.Tools, clone, run.Root.BaseResults), run.Root.BaseSuiteLog, mustExitZero: false))
            .ThenAsync(ExtensionAsync);
    }

    private async Task<Passage<Measured>> ExtensionAsync(StageRun suite)
    {
        var extension = await OneAsync("ts tests", TargetArgv.Extension(run.Target, run.Tools, run.Root.Base), run.Root.BaseExtensionLog, mustExitZero: false);
        return extension.Map(done =>
        {
            stages.WriteLine("DONE");
            return new Measured(suite.Run, suite.Log, done.Run, done.Log);
        });
    }

    /// <summary>The <c>npm ci</c> commands one after the other; the first that fails ends the walk. Each takes the next number of <c>base-npm-N.log</c>, the rules submodule's first.</summary>
    private async Task<Passage<bool>> InOrderAsync(IReadOnlyList<ToolCommand> commands)
    {
        foreach (var command in commands)
        {
            var stage = await OneAsync("npm ci", command, run.Root.BaseNpmLog(++_npm), mustExitZero: true);
            if (stage is Passage<StageRun>.Stopped stopped)
            {
                return new Passage<bool>.Stopped(stopped.ExitCode, stopped.Lines);
            }
        }

        return new Passage<bool>.Through(true);
    }

    /// <summary>The folder the suite writes its results into, made exclusively: one that is already there is not adopted.</summary>
    private Passage<bool> ResultsFolder() =>
        ExclusiveFolder.Make(run.Root.BaseResults, PlatformFactsReader.CurrentRoad) switch
        {
            FolderMade.Made => new Passage<bool>.Through(true),
            FolderMade.AlreadyThere => Passage<bool>.Stop(ExitCodes.Environment, $"{Name}: {run.Root.BaseResults} already exists — it is not adopted; move it away and run again"),
            FolderMade.Failed failed => Passage<bool>.Stop(ExitCodes.Environment, $"{Name}: cannot create {run.Root.BaseResults}: {failed.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private async Task<Passage<StageRun>> OneAsync(string stage, ToolCommand command, string logPath, bool mustExitZero)
    {
        ct.ThrowIfCancellationRequested();
        var made = ToolLog.Create(logPath);
        if (made is not Outcome<ToolLog>.Ok created)
        {
            return Passage<StageRun>.Stop(ExitCodes.Environment, $"{Name}: {((Outcome<ToolLog>.Fail)made).Reason}");
        }

        using var log = created.Value;
        var ran = await tools.RunAsync(run.Seams.Walled(command), log.Stream, ct);
        stages.WriteLine(Line(stage, ran));
        if (!ran.Started)
        {
            return Passage<StageRun>.Stop(ExitCodes.Environment, $"{Name}: {command.Label} could not be started — {command.Executable} — see {logPath}");
        }

        return mustExitZero && !(ran.Exit.WasCaptured && ran.Exit.Value == 0)
            ? Passage<StageRun>.Stop(ExitCodes.Environment, $"{Name}: the base does not build at {run.Target.Base.Value} — {command.Label} {How(ran)} — see {logPath}; no baseline")
            : new Passage<StageRun>.Through(new StageRun(ran, mustExitZero ? Outcome<string>.Success(string.Empty) : log.ReadBack(LogLimit)));
    }

    /// <summary>The shell's line: <c>build rc=1 s=65 head=8b1eef7a</c>; <c>killed</c> and <c>not-started</c> stand where an exit code is not captured.</summary>
    private string Line(string stage, ToolRun ran) =>
        $"{stage} rc={Rc(ran)} s={((long)ran.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture)}{(stage == "build" ? $" head={head}" : string.Empty)}";

    private static string Rc(ToolRun ran) =>
        ran.Exit.WasCaptured ? ran.Exit.Value.ToString(CultureInfo.InvariantCulture) : ran.KilledAtWall ? "killed" : "not-started";

    private static string How(ToolRun ran) => ran.KilledAtWall ? "was killed at its wall" : $"exited {Rc(ran)}";
}
