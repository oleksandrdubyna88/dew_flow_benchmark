using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>How making one arm ended (C15): <see cref="Made"/> — the marker is written and the arm can run — or <see cref="Stopped"/> with the exit code of the verb that asked and the sentence that says why. The sentence has passed <see cref="ArmMap.Scrub"/>: no token.</summary>
public abstract record MakeOutcome
{
    private MakeOutcome() { }

    public sealed record Made : MakeOutcome;

    public sealed record Stopped(int ExitCode, string Why) : MakeOutcome;
}

/// <summary>Makes ONE arm (C15, E3.S3; <c>after6.sh:26-37</c>): the unit <c>make</c> repeats per arm and <c>chain</c>'s <c>Make(arm)</c> (E5.S4) calls — so there is one copy of the code that deletes and copies. In order: (a) a folder that is there without the marker is judged by <see cref="ArmLeftovers"/> and removed only when the arm never ran; (b) <c>r/&lt;token&gt;</c> made
/// exclusively at <c>0700</c>, then <c>out</c>, <c>notes</c>, <c>work</c>, <c>tmp</c> and <c>coai-data/&lt;side&gt;</c>; (c) <see cref="BaseCopy"/> of <c>base/</c> into <c>arm/</c>; (d) the one branch (<see cref="ArmGit"/>); (e) the warm build, lowered, into <c>out/prep-build.log</c> created exclusively — not exit 0 is a stop; (f) with a coai gate, the settings file's bytes and
/// <c>mcp-coai.json</c> by <see cref="ArmFiles.ReplacePrivate(string, string)"/>; (g) <c>mcp-none.json</c> likewise; (h) <c>chain/made-&lt;arm&gt;</c> LAST — so an arm with a marker is whole, and an arm without one is redone whole.
/// <para>
/// <b>Trust, D26 (a)–(f).</b> (a) the kinds and the presence of what it deletes and copies are the kernel's (<see cref="KernelFacts"/>, <see cref="BoundedFile"/>); (b) every child — <c>git</c>, the node that runs the target's wrapper, the build — goes by the REAL path its caller resolved and printed (<see cref="ToolPaths"/>), under <see cref="TargetEnvironment"/> (the tools' <see cref="ITargetTools"/> holds it) and
/// <see cref="ArmGit"/>'s own reduced environment, from the arm's clone; (c) PATH, <c>HOME</c> and <c>XDG_CONFIG_HOME</c> are judged by the verb (<see cref="Verbs.LaunchAdmission"/>) before this is built; (d) <c>&lt;fill me&gt;</c> is the experiment reader's; (e) every folder is made exclusively, every log and file created new or staged and renamed, nothing written through a link; (f) the gate's.
/// </para>
/// <para>
/// <b>The vault key.</b> <c>mcp-coai.json</c> may carry <c>COAI_CREDS_KEY</c> (<see cref="McpConfig"/>) and is created <c>0600</c>: that keeps the key from OTHER users. The arm's own code runs as the same user and can read the file — as the shell's could, and the coai server it launches needs the key; secrecy from the arm would need another identity, which this module does not have.
/// </para>
/// <para>
/// What this does not do: build worktrees (E4), change a mode (E5), or tell an arm folder that is somebody else's from the arm's own.
/// </para></summary>
public sealed class ArmMaker(Experiment experiment, RootPaths root, ArmMap map, ToolPaths tools, ITargetTools targets, IReadOnlyDictionary<string, string> environment, PlatformRoad road, Action<string> say)
{
    private const int SettingsLimit = 1024 * 1024;
    private static readonly UnixFileMode OwnerFolder = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public async Task<MakeOutcome> MakeAsync(string arm, CancellationToken ct)
    {
        var passage = await Task.FromResult(Placed(arm))
            .Then(paths => Cleared(arm, paths))
            .Then(paths => Folders(arm, paths))
            .Then(paths => Copied(arm, paths))
            .ThenAsync(paths => BranchedAsync(arm, paths, ct))
            .ThenAsync(paths => BuiltAsync(arm, paths, ct))
            .Then(paths => Configured(arm, paths))
            .Then(paths => Marked(arm, paths));
        return passage.Match<MakeOutcome>(
            _ =>
            {
                say(map.Scrub($"arm {arm} made and built"));
                return new MakeOutcome.Made();
            },
            stop => new MakeOutcome.Stopped(stop.ExitCode, string.Join('\n', stop.Lines)));
    }

    private Passage<T> Stop<T>(int exitCode, string text) => Passage<T>.Stop(exitCode, map.Scrub(text));

    /// <summary>The arm's paths by its token — and the marker: an arm that has it is MADE, and nothing below runs on it (a caller that asks anyway is stopped before the one deletion in the class).</summary>
    private Passage<ArmPaths> Placed(string arm) =>
        map.TokenOf(arm).Match(
            token => MarkerCheck(arm, root.Arm(token)),
            reason => Stop<ArmPaths>(4, reason));

    private Passage<ArmPaths> MarkerCheck(string arm, ArmPaths paths) =>
        KernelFacts.Present(root.Made(arm)).Match(
            made => made ? Stop<ArmPaths>(4, $"{arm} is made already (chain/made-{arm}) — make never remakes an arm") : new Passage<ArmPaths>.Through(paths),
            reason => Stop<ArmPaths>(3, $"cannot tell whether {arm} is made: {reason}"));

    /// <summary>(a) A folder without the marker is removed only when <see cref="ArmLeftovers"/> says the arm never ran; the removal is said.</summary>
    private Passage<ArmPaths> Cleared(string arm, ArmPaths paths) =>
        ArmLeftovers.Judge(arm, paths, road) switch
        {
            ArmLeftovers.Verdict.Gone => new Passage<ArmPaths>.Through(paths),
            ArmLeftovers.Verdict.NeverRan => Removed(arm, paths),
            ArmLeftovers.Verdict.Stop stop => Stop<ArmPaths>(stop.ExitCode, stop.Why),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private Passage<ArmPaths> Removed(string arm, ArmPaths paths)
    {
        if (ArmLeftovers.Remove(paths) is ArmLeftovers.Verdict.Stop stop)
        {
            return Stop<ArmPaths>(stop.ExitCode, stop.Why);
        }

        say(map.Scrub($"arm {arm}: removed its half-made folder {paths.Folder} (no made marker, and no sign that it ever ran) — it is made again"));
        return new Passage<ArmPaths>.Through(paths);
    }

    /// <summary>(b) <c>r/&lt;token&gt;</c> exclusively — an entry already at the name is never adopted — then the folders the arm writes under, all owner-only.</summary>
    private Passage<ArmPaths> Folders(string arm, ArmPaths paths)
    {
        try
        {
            OwnerOnly(root.Arms);
            return ExclusiveFolder.Make(paths.Folder, road) switch
            {
                FolderMade.Made => Inside(paths),
                FolderMade.AlreadyThere => Stop<ArmPaths>(4, $"{paths.Folder} appeared while {arm} was being made — it is not adopted; run make again"),
                FolderMade.Failed failed => Stop<ArmPaths>(3, $"cannot create {paths.Folder}: {failed.Reason}"),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Stop<ArmPaths>(3, $"cannot make the folders of {arm}: {ex.Message}");
        }
    }

    private Passage<ArmPaths> Inside(ArmPaths paths)
    {
        OwnerOnly(paths.Folder);
        foreach (var folder in new[] { paths.Out, paths.Notes, paths.Work, paths.Tmp })
        {
            OwnerOnly(folder);
        }

        if (experiment.Coai.Configured)
        {
            OwnerOnly(paths.CoaiData);
            OwnerOnly(Path.GetDirectoryName(paths.CoaiSettings(experiment.Coai.Side))!);
        }

        return new Passage<ArmPaths>.Through(paths);
    }

    /// <summary>The folder made if it is not there, at owner-only mode set explicitly (the umask may have narrowed what mkdir was asked for). A folder that IS there keeps the mode it has: changing a mode is <c>lock</c>'s and <c>unlock</c>'s alone (D11), and <c>r/</c> or <c>chain/</c> may be at the isolation's.</summary>
    private static void OwnerOnly(string folder)
    {
        if (Directory.Exists(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(folder, OwnerFolder);
        }
    }

    /// <summary>(c) <c>base/</c> into <c>arm/</c>.</summary>
    private Passage<ArmPaths> Copied(string arm, ArmPaths paths) =>
        BaseCopy.Copy(root.Base, paths.CloneFolder, road, [root.Root]).Match(
            _ => new Passage<ArmPaths>.Through(paths),
            reason => Stop<ArmPaths>(3, $"cannot copy base/ into {arm}'s clone: {reason}"));

    /// <summary>(d) the one branch, from <c>base</c>'s commit.</summary>
    private async Task<Passage<ArmPaths>> BranchedAsync(string arm, ArmPaths paths, CancellationToken ct) =>
        (await new ArmGit(tools.Git, paths.CloneFolder, environment).NewBranchAsync(experiment.Target.Branch, ct)).Match(
            _ => new Passage<ArmPaths>.Through(paths),
            reason => Stop<ArmPaths>(3, $"cannot branch {arm}'s clone onto {experiment.Target.Branch}: {reason}"));

    /// <summary>(e) the warm build, lowered, into <c>out/prep-build.log</c> created NEW; anything but exit 0 is a stop and no marker.</summary>
    private async Task<Passage<ArmPaths>> BuiltAsync(string arm, ArmPaths paths, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var made = ToolLog.Create(paths.PrepBuildLog);
        if (made is not Outcome<ToolLog>.Ok created)
        {
            return Stop<ArmPaths>(3, ((Outcome<ToolLog>.Fail)made).Reason);
        }

        using var log = created.Value;
        var ran = await targets.RunAsync(TargetArgv.Build(experiment.Target, tools, paths.CloneFolder), log.Stream, ct);
        return ran.Started && ran.Exit.WasCaptured && ran.Exit.Value == 0
            ? new Passage<ArmPaths>.Through(paths)
            : Stop<ArmPaths>(3, $"warm build of {arm} failed ({How(ran)}) — see its out/prep-build.log");
    }

    private static string How(ToolRun ran) =>
        !ran.Started ? "it could not be started" : ran.KilledAtWall ? "killed at its wall" : $"exit {ran.Exit.Value}";

    /// <summary>(f), (g): the coai settings and <c>mcp-coai.json</c> when a gate is configured, <c>mcp-none.json</c> always — each created private.</summary>
    private Passage<ArmPaths> Configured(string arm, ArmPaths paths)
    {
        try
        {
            var coai = experiment.Coai.Configured ? Coai(paths) : new Passage<ArmPaths>.Through(paths);
            if (coai is Passage<ArmPaths>.Through)
            {
                ArmFiles.ReplacePrivate(paths.McpNone, McpConfig.None() + "\n");
            }

            return coai;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Stop<ArmPaths>(3, $"cannot write the MCP files of {arm}: {ex.Message}");
        }
    }

    private Passage<ArmPaths> Coai(ArmPaths paths)
    {
        var coai = experiment.Coai;
        switch (BoundedFile.Read(coai.Settings, SettingsLimit, road))
        {
            case BoundedRead.Bytes bytes:
                ArmFiles.ReplacePrivate(paths.CoaiSettings(coai.Side), bytes.Value);
                ArmFiles.ReplacePrivate(paths.McpCoai, McpConfig.Coai(coai, paths, environment) + "\n");
                return new Passage<ArmPaths>.Through(paths);
            case BoundedRead.Refused refused:
                return Stop<ArmPaths>(4, $"coai.settings is not read: {refused.Reason}");
            default:
                return Stop<ArmPaths>(4, $"coai.settings {coai.Settings} does not exist");
        }
    }

    /// <summary>(h) the marker, LAST: staged and renamed, an existing file or link at its name refused.</summary>
    private Passage<ArmPaths> Marked(string arm, ArmPaths paths)
    {
        try
        {
            OwnerOnly(root.Chain);
            ArmFiles.WriteFileOnce(root.Made(arm), "made\n");
            return new Passage<ArmPaths>.Through(paths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Stop<ArmPaths>(3, $"cannot write chain/made-{arm}: {ex.Message}");
        }
    }
}
