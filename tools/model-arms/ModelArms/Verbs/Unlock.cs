using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;

namespace ModelArms.Verbs;

/// <summary><c>unlock [--force]</c> (D11; E5.S1): restores the modes of the standing snapshot in reverse and moves both files aside — whatever <c>experiment.json</c> says now (a block removed after a lock must not strand the modes), within <see cref="RestoreBound"/> (a forged <c>.locked-modes</c> must not steer this user's <c>chmod</c>). Under the EXCLUSIVE <c>.isolation.lock</c>, then
/// the liveness probe of every arm of the map, so no runner can start between the probe and the restore (a runner alive holds the lock shared, which makes the take itself the refusal, at once). Each entry is pinned ANEW and judged (<see cref="RestoreJudgement"/>): gone, a link now, another kind or replaced are skipped and said; an entry whose mode is neither the saved nor the locked one is the operator's own and is reported
/// <i>unresolved</i>, left as it is. A restore that fails for any entry KEEPS <c>.locked-modes</c> (the snapshot is the one authority for modes; the state reads <i>applying</i>), moves the applied file aside and exits 3; <c>unlock</c> again retries.
/// <para>
/// <b><c>--force</c></b> (D11's meaning): it skips BOTH the exclusive take and the liveness probe, restores, and prints ONE loud <c>WARNING</c> first, on stderr and in <c>lock.log</c> — an arm may still be running and lose its isolation mid-run; the race is the operator's choice. Nothing else changes: the decision, the bound, the judgement per entry and the partial-restore rule stand. A <c>.locked-modes</c> that does not parse is
/// refused (4) whatever the flag says.
/// </para>
/// <para>
/// Exit 0 unlocked / nothing locked / nothing to restore; 3 a restore that failed for an entry, the host could not say; 4 an arm is alive, a snapshot that cannot be used; 5 the operator's stop.
/// </para></summary>
internal static class UnlockVerb
{
    private const string Warning = "WARNING: --force skipped the isolation lock and the liveness check — an arm may still be running and lose its isolation mid-run; this race is your choice";

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, IsolationSeams.Real);

    internal static async Task<int> RunAsync(ExperimentInput input, IsolationSeams seams)
    {
        var outcome = await UnlockAsync(input, input.Verb.Flags.Has("force"), input.Verb.Stopping, seams);
        return Reported.Print(input, "unlock", outcome switch
        {
            UnlockOutcome.Restored restored => new Reported(0, restored.Line),
            UnlockOutcome.NothingLocked nothing => new Reported(0, nothing.Line),
            UnlockOutcome.NoIsolation none => new Reported(0, none.Line),
            UnlockOutcome.RefusedAlive alive => new Reported(ExitCodes.Configuration, alive.Why),
            UnlockOutcome.Stopped stopped => new Reported(stopped.ExitCode, stopped.Why),
            _ => new Reported(ExitCodes.Environment, "unknown outcome"),
        }, seams);
    }

    public static Task<UnlockOutcome> UnlockAsync(ExperimentInput input, bool force, CancellationToken stopping) => UnlockAsync(input, force, stopping, IsolationSeams.Real);

    internal static async Task<UnlockOutcome> UnlockAsync(ExperimentInput input, bool force, CancellationToken stopping, IsolationSeams seams)
    {
        if (stopping.IsCancellationRequested)
        {
            return new UnlockOutcome.Stopped(ExitCodes.NoReport, "stopped before anything was changed — run the same command again");
        }

        using var isolation = FolderIsolations.For(seams.Road, seams.Hands, seams.Events);
        if (force)
        {
            input.Verb.Error.WriteLine($"model-arms unlock: {Warning}");
            LockLog.Append(input.Root, seams.Clock, $"unlock: {Warning}", input.Verb.Error);
            return Decided(input, force, isolation, seams);
        }

        var taken = await IsolationLocking.TakeExclusiveAsync(input, seams, stopping);
        if (taken is Passage<IDisposable>.Stopped stop)
        {
            return new UnlockOutcome.Stopped(stop.ExitCode, string.Join(' ', stop.Lines));
        }

        using var handle = ((Passage<IDisposable>.Through)taken).Value;
        return Decided(input, force, isolation, seams);
    }

    private static UnlockOutcome Decided(ExperimentInput input, bool force, IFolderIsolation isolation, IsolationSeams seams)
    {
        var read = IsolationFiles.Read(input.Root, seams.Road);
        if (!isolation.Applies)
        {
            return read.State is LockState.None
                ? new UnlockOutcome.NoIsolation("isolation by file modes is POSIX only — nothing to unlock on Windows")
                : new UnlockOutcome.Stopped(ExitCodes.Configuration, $"a snapshot stands on a host without isolation by file modes ({IsolationLine.Of(read.State)}) — nothing here can restore it");
        }

        return UnlockDecision.Of(read.State, read.Snapshot, read.Applied) switch
        {
            UnlockStep.Nothing => new UnlockOutcome.NothingLocked(NothingLine(input)),
            UnlockStep.Refuse refuse => new UnlockOutcome.Stopped(ExitCodes.Configuration, $"{ExecutableRule.Printable(refuse.Reason)} — .locked-modes cannot be used, both files are kept; restore the modes by hand and move them aside"),
            UnlockStep.RenameAppliedAlone => AppliedAlone(input, seams),
            UnlockStep.Restore restore => Restored(input, force, isolation, seams, restore),
            _ => new UnlockOutcome.Stopped(ExitCodes.Environment, "the isolation state is one this build does not know"),
        };
    }

    /// <summary><i>no isolation configured</i> only when nothing in <c>experiment.json</c> could ever have been locked; otherwise <i>nothing locked</i>.</summary>
    private static string NothingLine(ExperimentInput input) =>
        !input.Experiment.Isolation.Configured && input.Experiment.Runs.All(run => run.ClosedArms.Count + run.Closed.Count == 0)
            ? "no isolation configured — nothing to unlock"
            : "nothing locked";

    private static UnlockOutcome AppliedAlone(ExperimentInput input, IsolationSeams seams)
    {
        var moved = SnapshotAside.Applied(input.Root, seams.Clock);
        if (moved is Outcome<string>.Fail failed)
        {
            return new UnlockOutcome.Stopped(ExitCodes.Environment, failed.Reason);
        }

        seams.Events?.Add("aside " + Path.GetFileName(((Outcome<string>.Ok)moved).Value));
        return new UnlockOutcome.Restored(0, "nothing to restore — .locked-modes.applied stood alone, moved aside");
    }

    private static UnlockOutcome Restored(ExperimentInput input, bool force, IFolderIsolation isolation, IsolationSeams seams, UnlockStep.Restore restore)
    {
        var mapped = IsolationLocking.MapOf(input, seams.Road);
        if (mapped is Passage<ArmMap>.Stopped refusedMap)
        {
            return new UnlockOutcome.Stopped(refusedMap.ExitCode, string.Join(' ', refusedMap.Lines));
        }

        var map = ((Passage<ArmMap>.Through)mapped).Value;
        if (!force && Alive(input, seams, map) is var probed and not UnlockOutcome.Clear)
        {
            return probed;
        }

        if (isolation.NotVerifiedLine.Length > 0)
        {
            input.Verb.Output.WriteLine($"model-arms unlock: {isolation.NotVerifiedLine}");
        }

        var report = isolation.Restore(IsolationRestoreOrder.Of(restore.Saved.Modes), IsolationLocking.BoundOf(input, map, restore.Saved));
        return report.Failed.Count == 0 ? Unlocked(input, seams, restore, report) : Failed(input, seams, restore, report);
    }

    /// <summary>The first arm of the map that is alive, in map order, as the refusal — or the refusal of a lock the root will not let be probed; <see cref="UnlockOutcome.Clear"/> when none is.</summary>
    private static UnlockOutcome Alive(ExperimentInput input, IsolationSeams seams, ArmMap map)
    {
        var portable = RunnerLiveness.PortableLine(seams.Road);
        if (portable.Length > 0 && map.Entries.Count > 0)
        {
            input.Verb.Output.WriteLine(portable);
        }

        foreach (var arm in map.Entries.Select(entry => entry.Key))
        {
            switch (seams.Liveness(input.Root, arm, seams.Road))
            {
                case RunnerState.Alive running:
                    return new UnlockOutcome.RefusedAlive(arm, $"refused — {arm} is alive ({running.Holder}); unlock once no arm is alive, or --force");
                case RunnerState.Refused refused:
                    return new UnlockOutcome.Stopped(ExitCodes.Configuration, refused.Reason);
                case RunnerState.Unread unread:
                    return new UnlockOutcome.Stopped(ExitCodes.Environment, unread.Reason);
            }
        }

        return new UnlockOutcome.Clear();
    }

    /// <summary>Nothing failed: both files moved aside in D11's order — the snapshot, then the applied file (a crash between the two leaves the applied file alone, which the next <c>unlock</c> moves aside). The line says how many entries needed no call, and where the record of what was locked now is.</summary>
    private static UnlockOutcome Unlocked(ExperimentInput input, IsolationSeams seams, UnlockStep.Restore restore, RestoreReport report)
    {
        var snapshot = SnapshotAside.Snapshot(input.Root, seams.Clock);
        if (snapshot is Outcome<string>.Fail failed)
        {
            return new UnlockOutcome.Stopped(ExitCodes.Environment, $"everything was restored, but {failed.Reason}");
        }

        var archive = Path.GetFileName(((Outcome<string>.Ok)snapshot).Value);
        seams.Events?.Add("aside " + archive);
        if (restore.AppliedPresent && SnapshotAside.Applied(input.Root, seams.Clock) is var applied)
        {
            if (applied is Outcome<string>.Fail appliedFailed)
            {
                return new UnlockOutcome.Stopped(ExitCodes.Environment, $"everything was restored and the snapshot moved aside, but {appliedFailed.Reason} — the next unlock moves it");
            }

            seams.Events?.Add("aside " + Path.GetFileName(((Outcome<string>.Ok)applied).Value));
        }

        var line = $"unlocked — restored {report.Settled} of {restore.Saved.Modes.Count}{report.HarmlessWords}";
        line += report.Skipped.Count > 0 ? $" (skipped: {string.Join("; ", report.Skipped)}; the record of what was locked is kept in {archive})" : string.Empty;
        line += report.Unresolved.Count > 0 ? $" (unresolved: {string.Join("; ", report.Unresolved)})" : string.Empty;
        return new UnlockOutcome.Restored(report.Settled, line);
    }

    /// <summary>An entry failed: the applied file is moved aside (when present) and <c>.locked-modes</c> is KEPT — the one authority for the modes, retried by the next <c>unlock</c> and recovered by <c>lock</c>; the state reads <i>applying</i>. The line says whether the applied file really moved.</summary>
    private static UnlockOutcome Failed(ExperimentInput input, IsolationSeams seams, UnlockStep.Restore restore, RestoreReport report)
    {
        var aside = restore.AppliedPresent ? SnapshotAside.Applied(input.Root, seams.Clock) : Outcome<string>.Success(string.Empty);
        if (aside is Outcome<string>.Ok { Value.Length: > 0 } moved)
        {
            seams.Events?.Add("aside " + Path.GetFileName(moved.Value));
        }

        var applied = aside switch
        {
            Outcome<string>.Fail failed => $"the applied file could not be moved aside ({failed.Reason})",
            _ when restore.AppliedPresent => "the applied file was moved aside",
            _ => "there was no applied file",
        };
        return new UnlockOutcome.Stopped(ExitCodes.Environment, $"restored {report.Settled} of {restore.Saved.Modes.Count}{report.HarmlessWords} — {string.Join("; ", report.Failed)} — {applied}; run unlock again, or lock {ExecutableRule.Printable(restore.Saved.Run)}");
    }
}
