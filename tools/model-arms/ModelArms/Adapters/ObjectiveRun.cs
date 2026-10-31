using Bench.Domain;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>What the stages of one objective run are handed: the target's tools (launched by the real paths in <see cref="ToolPaths"/>, under the environment <see cref="ITargetTools"/> was built with), the target's facts, the arm's clone, the folder the suite writes its results into, the verb's wall for a command (the production walls, but for a test's)
/// and the question "which <c>.cs</c> files changed since the base" — asked of git between the suite and the formatter, as the shell asked it (<c>objective-checks.sh:19</c>).</summary>
internal sealed record ObjectiveStages(
    ITargetTools Tools,
    TargetFacts Target,
    ToolPaths Paths,
    string CloneFolder,
    string Results,
    Func<ToolCommand, ToolCommand> Walled,
    Func<CancellationToken, Task<Outcome<IReadOnlyList<string>>>> ChangedCs);

/// <summary>The objective run of one arm (plan §4.9, E3.S5 steps 5 and 7, C17): the folder <c>out/objective</c> made EXCLUSIVELY (an earlier one is renamed aside, kept), its four logs created exclusively and HELD before the first child starts, the four stages run in the shell's order — every stage whatever the one before did — and
/// <c>stages.json</c> written afterwards. The arm's own code runs in the stages with the operator's rights and can do to this folder what it likes, so the run claims only what it can keep: it never FOLLOWS (every log is a <c>CreateNew</c> handle, a link planted at a name is never opened) and it never CONSUMES a substitution (a suite log is read back from the handle it holds, not
/// by its path; a folder swapped for a link or another folder is told by <see cref="StillTheFolderMade"/> before anything is read from or written into it).
/// <para>
/// What this does NOT do: authenticate what the arm's code printed — a log the arm's code wrote INTO (open and append, no unlink) is what it holds, the CTRF is its suite's own output by nature, and the report is not authenticated against that code (E3 §8 item 17); bound a tool's output in memory (item 18).
/// </para></summary>
internal sealed class ObjectiveRun : IDisposable
{
    private const int LogLimit = 64 * 1024 * 1024;

    private readonly ArmPaths _paths;
    private readonly PlatformRoad _road;
    private readonly FolderIdentity _made;
    private readonly ToolLog _build;
    private readonly ToolLog _suite;
    private readonly ToolLog _format;
    private readonly ToolLog _extension;

    private ObjectiveRun(ArmPaths paths, PlatformRoad road, FolderIdentity made, string keptAside, ToolLog[] logs)
    {
        _paths = paths;
        _road = road;
        _made = made;
        KeptAside = keptAside;
        (_build, _suite, _format, _extension) = (logs[0], logs[1], logs[2], logs[3]);
    }

    /// <summary>Where the earlier <c>out/objective</c> was renamed to; the empty text when there was none.</summary>
    public string KeptAside { get; }

    /// <summary>Step 5's beginning: the earlier folder aside, the new one made exclusively and its identity taken, the four logs created. Any failure leaves nothing held.</summary>
    public static Outcome<ObjectiveRun> Begin(ArmPaths paths, string stamp, PlatformRoad road) =>
        Aside(paths, stamp).Bind(kept => Made(paths, road).Bind(identity => Logs(paths).Map(logs => new ObjectiveRun(paths, road, identity, kept, logs))));

    /// <summary>The four stages in the shell's order. A tool that did not START ends the run with the sentence saying so; a tool that exits non-zero, or is killed at its wall, is a RESULT. A stop travels up as the cancellation it is.</summary>
    public Task<Outcome<StageRuns>> RunAsync(ObjectiveStages stages, CancellationToken ct) =>
        OneAsync(stages, TargetArgv.Build(stages.Target, stages.Paths, stages.CloneFolder), _build, ct).BindAsync(built =>
            OneAsync(stages, TargetArgv.Suite(stages.Target, stages.Paths, stages.CloneFolder, stages.Results), _suite, ct).BindAsync(tested =>
                FormatAsync(stages, ct).BindAsync(formatted =>
                    OneAsync(stages, TargetArgv.Extension(stages.Target, stages.Paths, stages.CloneFolder), _extension, ct).MapAsync(extended => new StageRuns(built, tested, formatted, extended)))));

    /// <summary>Step 7: the empty text while <c>out/objective</c> is still the folder made; otherwise the sentence saying what it is now.</summary>
    public string StillTheFolderMade() => ObjectiveFolder.Changed(_paths.ObjectiveFolder, _made, _road);

    /// <summary><c>stages.json</c>, staged and renamed over its name (a link planted there is replaced, never written through). Call only after <see cref="StillTheFolderMade"/> answered the empty text.</summary>
    public Outcome<bool> Record(StageRuns runs)
    {
        try
        {
            new ArmFiles(_paths).Replace(_paths.ObjectiveStages, StageRecord.Write(runs));
            return Outcome<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<bool>.Failure($"{_paths.ObjectiveStages} cannot be written: {ex.Message}");
        }
    }

    /// <summary>The suite's log as the handle this run made it with holds it (seek 0, at most 64 MiB); a log past the limit is a failure naming it.</summary>
    public Outcome<string> SuiteText() => _suite.ReadBack(LogLimit);

    public Outcome<string> ExtensionText() => _extension.ReadBack(LogLimit);

    public void Dispose()
    {
        foreach (var log in new[] { _build, _suite, _format, _extension })
        {
            log.Dispose();
        }
    }

    /// <summary>An earlier <c>out/objective</c> — a folder, a file or a link, a dangling one too — renamed to <c>objective.prev-&lt;stamp&gt;</c>, as what it is; the empty text when there was none.</summary>
    private static Outcome<string> Aside(ArmPaths paths, string stamp) =>
        KernelFacts.Present(paths.ObjectiveFolder).Bind(present => present ? Moved(paths, stamp) : Outcome<string>.Success(string.Empty));

    private static Outcome<string> Moved(ArmPaths paths, string stamp)
    {
        var kept = paths.ObjectivePrevious(stamp);
        if (KernelFacts.Present(kept) is not Outcome<bool>.Ok { Value: false })
        {
            return Outcome<string>.Failure($"{kept} already exists, or the host cannot say — an earlier objective folder was moved aside a moment ago; run again");
        }

        try
        {
            BaseLeftovers.Rename(paths.ObjectiveFolder, kept);
            return Outcome<string>.Success(kept);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<string>.Failure($"cannot move {paths.ObjectiveFolder} aside to {kept}: {ex.Message}");
        }
    }

    private static Outcome<FolderIdentity> Made(ArmPaths paths, PlatformRoad road) =>
        ExclusiveFolder.Make(paths.ObjectiveFolder, road) switch
        {
            FolderMade.Made => ObjectiveFolder.Take(paths.ObjectiveFolder, road),
            FolderMade.AlreadyThere => Outcome<FolderIdentity>.Failure($"{paths.ObjectiveFolder} appeared while it was being made — it is not adopted; run again"),
            FolderMade.Failed failed => Outcome<FolderIdentity>.Failure($"cannot create {paths.ObjectiveFolder}: {failed.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>The four logs, each created exclusively (<c>CreateNew</c>: nothing at the name is followed or adopted); the ones already made are closed when a later one cannot be.</summary>
    private static Outcome<ToolLog[]> Logs(ArmPaths paths)
    {
        var made = new List<ToolLog>();
        foreach (var path in new[] { paths.ObjectiveBuildLog, paths.ObjectiveSuiteLog, paths.ObjectiveFormatLog, paths.ObjectiveExtensionLog })
        {
            if (ToolLog.Create(path) is Outcome<ToolLog>.Ok created)
            {
                made.Add(created.Value);
                continue;
            }

            made.ForEach(log => log.Dispose());
            return Outcome<ToolLog[]>.Failure($"{path} cannot be created: something is already at that name");
        }

        return Outcome<ToolLog[]>.Success([.. made]);
    }

    private static async Task<Outcome<StageRun>> OneAsync(ObjectiveStages stages, ToolCommand command, ToolLog log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ran = await stages.Tools.RunAsync(stages.Walled(command), log.Stream, ct);
        return ran.Started
            ? Outcome<StageRun>.Success(Stage(ran))
            : Outcome<StageRun>.Failure($"{command.Label} could not be started — {command.Executable} — see {log.Path}");
    }

    /// <summary>The formatter's verbs one after the other over the changed <c>.cs</c> files; the first that does not exit 0 ends the stage — the shell's <c>&amp;&amp;</c>. None changed: nothing runs and the stage is exit 0 (the shell's pass).</summary>
    private async Task<Outcome<StageRun>> FormatAsync(ObjectiveStages stages, CancellationToken ct)
    {
        var changed = await stages.ChangedCs(ct);
        return await changed.Match(
            files => FormatVerbsAsync(stages, TargetArgv.Format(stages.Target, stages.Paths, stages.CloneFolder, files), ct),
            reason => Task.FromResult(Outcome<StageRun>.Failure($"git cannot name the .cs files changed since the base: {reason}")));
    }

    private async Task<Outcome<StageRun>> FormatVerbsAsync(ObjectiveStages stages, IReadOnlyList<ToolCommand> commands, CancellationToken ct)
    {
        var last = new StageRun(Bench.Domain.Trace.CapturedCount.Number(0), 0, Killed: false);
        foreach (var command in commands)
        {
            var one = await OneAsync(stages, command, _format, ct);
            if (one is not Outcome<StageRun>.Ok { Value: var ran })
            {
                return one;
            }

            last = new StageRun(ran.Exit, last.Seconds + ran.Seconds, ran.Killed);
            if (!ran.Exit.WasCaptured || ran.Exit.Value != 0)
            {
                break;
            }
        }

        return Outcome<StageRun>.Success(last);
    }

    private static StageRun Stage(ToolRun ran) => new(ran.Exit, (long)ran.Elapsed.TotalSeconds, ran.KilledAtWall);
}
