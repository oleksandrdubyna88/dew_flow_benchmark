using Bench.Cli;
using Bench.Domain;
using Bench.Infrastructure.Gate;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;

namespace ModelArms.Verbs;

/// <summary><c>lock &lt;run&gt;</c> (D11, D27; E5.S1): closes the run's folders — the configured <c>isolation</c> block, the run's <c>closedArms</c> through the map and its <c>closed</c> paths — in D11's crash-safe order, and OBSERVES that it worked before it says so. Only <c>lock</c> and <c>unlock</c> ever change a mode, each under the EXCLUSIVE <c>.isolation.lock</c>
/// (the runners hold it shared for their life); neither writes a chain marker. In order: the run's plan (<see cref="PlanOf"/>; an empty plan is <i>no isolation configured</i>, exit 0, and Windows is the same), <c>root</c> refused, the exclusive lock, the two files read, <see cref="LockDecision"/>, and then the adapter's steps — pin every entry, write the snapshot (staged, flushed, renamed,
/// BEFORE the first mode), apply, verify every entry, write the applied file. An apply or verify failure rolls back through the held descriptors and moves the snapshot aside ONLY when every entry was restored; a restore that fails for any entry KEEPS <c>.locked-modes</c> (state <i>applying</i>: <c>lock</c> recovers it, <c>chain</c> refuses it, <c>unlock</c> retries it) and exits 3.
/// <para>
/// Exit 0 locked, already locked (re-verified), no isolation configured; 3 the host (root, a mode or a verification that failed, the lock not free within 60 s); 4 configuration (an unknown run, another run's or another set's snapshot, an inconsistent one, a path refused, a runner alive); 5 the operator's stop. Logged once in <c>lock.log</c>.
/// </para></summary>
internal static class LockVerb
{
    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, IsolationSeams.Real);

    internal static async Task<int> RunAsync(ExperimentInput input, IsolationSeams seams)
    {
        var run = input.Verb.Positionals[0];
        var outcome = await LockAsync(input, run, input.Verb.Stopping, seams);
        return Reported.Print(input, $"lock {run}", outcome switch
        {
            LockOutcome.Applied applied => new Reported(0, applied.Line),
            LockOutcome.AlreadyLocked already => new Reported(0, already.Line),
            LockOutcome.NoIsolation none => new Reported(0, none.Line),
            LockOutcome.Stopped stopped => new Reported(stopped.ExitCode, stopped.Why),
            _ => new Reported(ExitCodes.Environment, "unknown outcome"),
        }, seams);
    }

    public static Task<LockOutcome> LockAsync(ExperimentInput input, string run, CancellationToken stopping) => LockAsync(input, run, stopping, IsolationSeams.Real);

    internal static async Task<LockOutcome> LockAsync(ExperimentInput input, string run, CancellationToken stopping, IsolationSeams seams)
    {
        if (stopping.IsCancellationRequested)
        {
            return new LockOutcome.Stopped(ExitCodes.NoReport, "stopped before anything was changed — run the same command again");
        }

        var planned = PlanOf(input, run);
        if (planned is Passage<IsolationPlan?>.Stopped stop)
        {
            return new LockOutcome.Stopped(stop.ExitCode, string.Join(' ', stop.Lines));
        }

        if (((Passage<IsolationPlan?>.Through)planned).Value is not { } plan)
        {
            return new LockOutcome.NoIsolation("no isolation configured — nothing to lock");
        }

        using var isolation = FolderIsolations.For(seams.Road, seams.Hands, seams.Events);
        return !isolation.Applies ? new LockOutcome.NoIsolation(isolation.Describe[(isolation.Describe.IndexOf("— ", StringComparison.Ordinal) + 2)..])
            : isolation.IsRoot ? new LockOutcome.Stopped(ExitCodes.Environment, "running as root (uid 0): isolation by file modes does nothing for root — run as the operator")
            : await LockedAsync(input, plan, isolation, seams, stopping);
    }

    /// <summary>The run's plan, or why there is none: 4 for a run that is not in <c>experiment.json</c> and for a plan that cannot be made (an arm the map lacks, a path named twice, one the run stands on); <c>null</c> for no isolation at all (no block, and the run closes nothing). <c>&lt;root&gt;/pids</c> is made by name when the plan names it (question 1).</summary>
    internal static Passage<IsolationPlan?> PlanOf(ExperimentInput input, string run)
    {
        var verb = $"model-arms {input.Verb.Verb}";
        if (input.Experiment.Runs.FirstOrDefault(each => each.Name == run) is not { } spec)
        {
            return Passage<IsolationPlan?>.Stop(ExitCodes.Configuration, $"{verb}: there is no run {ExecutableRule.Printable(run)} — the runs are {string.Join(", ", input.Experiment.Runs.Select(each => each.Name))}");
        }

        var mapped = IsolationLocking.MapOf(input, PlatformFactsReader.CurrentRoad);
        if (mapped is Passage<ArmMap>.Stopped refused)
        {
            return Passage<IsolationPlan?>.Stop(refused.ExitCode, [.. refused.Lines.Select(line => $"{verb}: {line}")]);
        }

        return PlanFor(input, spec, ((Passage<ArmMap>.Through)mapped).Value) switch
        {
            Passage<IsolationPlan?>.Through { Value: { } plan } => PidsMade(input, plan),
            var other => other,
        };
    }

    /// <summary>The plan of <paramref name="spec"/> over <paramref name="map"/>, created nothing: what the runner's warning (question 20) and <see cref="PlanOf"/> both ask. Stops with 4 for a plan that cannot be made; <c>null</c> for no isolation at all.</summary>
    internal static Passage<IsolationPlan?> PlanFor(ExperimentInput input, RunSpec spec, ArmMap map)
    {
        var standsOn = new List<string> { input.Root.Root, ExperimentGate.Home(input.Verb.Environment), input.Experiment.Target.Source };
        standsOn.AddRange(spec.Rounds.SelectMany(round => round).Select(arm => map.TokenOf(arm)).OfType<Outcome<string>.Ok>().Select(token => input.Root.Arm(token.Value).Folder));
        return IsolationPlans.From(input.Experiment.Isolation, spec, map, input.Root.Root, [.. standsOn.Where(path => path.Length > 0)]).Match(
            plan => (Passage<IsolationPlan?>)new Passage<IsolationPlan?>.Through(plan.IsEmpty ? null : plan),
            reason => Passage<IsolationPlan?>.Stop(ExitCodes.Configuration, $"model-arms {input.Verb.Verb}: {reason}"));
    }

    private static Passage<IsolationPlan?> PidsMade(ExperimentInput input, IsolationPlan plan) =>
        plan.Entries.Any(entry => string.Equals(entry.Path, input.Root.Pids, StringComparison.Ordinal))
            ? RootFolders.Pids(input.Root).Match(_ => (Passage<IsolationPlan?>)new Passage<IsolationPlan?>.Through(plan), stop => Passage<IsolationPlan?>.Stop(stop.ExitCode, [.. stop.Lines.Select(line => $"model-arms {input.Verb.Verb}: {line}")]))
            : new Passage<IsolationPlan?>.Through(plan);

    private static async Task<LockOutcome> LockedAsync(ExperimentInput input, IsolationPlan plan, IFolderIsolation isolation, IsolationSeams seams, CancellationToken stopping)
    {
        var taken = await IsolationLocking.TakeExclusiveAsync(input, seams, stopping);
        if (taken is Passage<IDisposable>.Stopped stop)
        {
            return new LockOutcome.Stopped(stop.ExitCode, string.Join(' ', stop.Lines));
        }

        using var handle = ((Passage<IDisposable>.Through)taken).Value;
        var read = IsolationFiles.Read(input.Root, seams.Road);
        return LockDecision.Of(read.State, plan, read.Snapshot) switch
        {
            LockStep.Fresh => Fresh(input, plan, isolation, seams, said: false),
            LockStep.Recover recover => Recovered(input, plan, isolation, seams, read, recover.Old),
            LockStep.AlreadyLocked already => AlreadyLocked(input, plan, isolation, read, already.Since),
            LockStep.RefuseOtherSet other => new LockOutcome.Stopped(ExitCodes.Configuration, $"locked for {ExecutableRule.Printable(other.LockedRun)} with a different closed set — unlock first"),
            LockStep.RefuseInconsistent inconsistent => new LockOutcome.Stopped(ExitCodes.Configuration, $"isolation snapshot inconsistent — {ExecutableRule.Printable(inconsistent.Reason)} — run unlock (when .locked-modes does not parse, restore the modes by hand and move it aside)"),
            _ => new LockOutcome.Stopped(ExitCodes.Environment, "the isolation state is one this build does not know"),
        };
    }

    /// <summary>An applied snapshot of the same run and set is re-VERIFIED, never claimed: every entry is tried again; an entry found open is re-pinned (its identity must be the saved one), the locked mode set again through the descriptor, and verified again — under the standing snapshot, which stays the authority (no new snapshot, no new applied file).</summary>
    private static LockOutcome AlreadyLocked(ExperimentInput input, IsolationPlan plan, IFolderIsolation isolation, IsolationRead read, string since)
    {
        var said = $"already locked for {ExecutableRule.Printable(plan.Run)} since {ExecutableRule.Printable(since)}";
        var open = isolation.Verify(plan);
        if (open.Count == 0)
        {
            return new LockOutcome.AlreadyLocked(said);
        }

        var saved = read.Snapshot is Read<string>.Present text ? LockSnapshots.Saved(text.Value) : Outcome<SavedSnapshot>.Failure(".locked-modes could not be read");
        if (saved is Outcome<SavedSnapshot>.Fail unreadable)
        {
            return new LockOutcome.Stopped(ExitCodes.Configuration, $"{said}, but {string.Join("; ", open)} — and the snapshot cannot be used to re-apply: {unreadable.Reason}");
        }

        IReadOnlyList<SavedMode> entries = [.. ((Outcome<SavedSnapshot>.Ok)saved).Value.Modes.Where(mode => open.Any(sentence => sentence.StartsWith(mode.Path + " ", StringComparison.Ordinal)))];
        var again = isolation.ReApply(entries, plan);
        if (again is Outcome<string>.Fail replaced)
        {
            return new LockOutcome.Stopped(ExitCodes.Configuration, $"{said}, but {replaced.Reason} — run unlock, then lock {ExecutableRule.Printable(plan.Run)}");
        }

        var still = isolation.Verify(plan);
        return still.Count == 0
            ? new LockOutcome.AlreadyLocked($"{said} — re-applied {entries.Count} entries that were open ({string.Join(", ", entries.Select(entry => entry.Path))})")
            : new LockOutcome.Stopped(ExitCodes.Environment, $"{said}, but {string.Join("; ", still)} — re-applied and still not verified");
    }

    /// <summary>A lock was interrupted: its modes are restored PINNED ANEW (the killed lock's descriptors died with it), and only when no entry failed and none is the operator's is the old snapshot moved aside and the lock made afresh; else the snapshot is kept and nothing is written over it.</summary>
    private static LockOutcome Recovered(ExperimentInput input, IsolationPlan plan, IFolderIsolation isolation, IsolationSeams seams, IsolationRead read, SavedSnapshot old)
    {
        var line = isolation.NotVerifiedLine;
        if (line.Length > 0)
        {
            input.Verb.Output.WriteLine($"model-arms lock {plan.Run}: {line}");
        }

        var mapped = IsolationLocking.MapOf(input, seams.Road);
        var map = mapped is Passage<ArmMap>.Through through ? through.Value : ArmMap.Empty;
        var report = isolation.Restore(IsolationRestoreOrder.Of(old.Modes), IsolationLocking.BoundOf(input, map));
        var interrupted = $"a lock was interrupted (snapshot of {ExecutableRule.Printable(old.Run)} since {ExecutableRule.Printable(old.Since)})";
        var trouble = report.Failed.Concat(report.Unresolved).ToList();
        if (trouble.Count > 0)
        {
            return new LockOutcome.Stopped(ExitCodes.Environment, $"{interrupted} — restored {report.Restored.Count} of {old.Modes.Count} — {string.Join("; ", trouble)} — the snapshot is kept; restore by hand, or run lock {ExecutableRule.Printable(plan.Run)} again");
        }

        var skipped = report.Skipped.Count > 0 ? $" (skipped: {string.Join("; ", report.Skipped)})" : string.Empty;
        input.Verb.Output.WriteLine($"model-arms lock {plan.Run}: {interrupted} — restored {report.Restored.Count} of {old.Modes.Count}{skipped}, now locking afresh");
        return Aside(input, seams, appliedToo: read.Applied is not Read<string>.Absent) is { } stopped ? stopped : Fresh(input, plan, isolation, seams, said: line.Length > 0);
    }

    private static LockOutcome? Aside(ExperimentInput input, IsolationSeams seams, bool appliedToo)
    {
        var moved = SnapshotAside.Snapshot(input.Root, seams.Clock);
        if (moved is Outcome<string>.Fail failed)
        {
            return new LockOutcome.Stopped(ExitCodes.Environment, failed.Reason);
        }

        seams.Events?.Add("aside " + Path.GetFileName(((Outcome<string>.Ok)moved).Value));
        if (!appliedToo)
        {
            return null;
        }

        var applied = SnapshotAside.Applied(input.Root, seams.Clock);
        if (applied is Outcome<string>.Fail appliedFailed)
        {
            return new LockOutcome.Stopped(ExitCodes.Environment, appliedFailed.Reason);
        }

        seams.Events?.Add("aside " + Path.GetFileName(((Outcome<string>.Ok)applied).Value));
        return null;
    }

    /// <summary>Nothing stands: pin every entry, write the snapshot, apply, verify, write the applied file.</summary>
    private static LockOutcome Fresh(ExperimentInput input, IsolationPlan plan, IFolderIsolation isolation, IsolationSeams seams, bool said)
    {
        if (!said && isolation.NotVerifiedLine.Length > 0)
        {
            input.Verb.Output.WriteLine($"model-arms lock {plan.Run}: {isolation.NotVerifiedLine}");
        }

        var pinned = isolation.Pin(plan);
        if (pinned is Outcome<Pinned>.Fail refused)
        {
            return new LockOutcome.Stopped(ExitCodes.Configuration, refused.Reason);
        }

        var held = ((Outcome<Pinned>.Ok)pinned).Value;
        var since = seams.Clock.GetUtcNow().ToString(SnapshotFiles.Utc, System.Globalization.CultureInfo.InvariantCulture);
        var written = Written(input.Root.LockedModes, SnapshotFiles.Snapshot(new SavedSnapshot(plan.Run, plan.Digest, since, held.Modes)));
        if (written.Length > 0)
        {
            return new LockOutcome.Stopped(ExitCodes.Configuration, $".locked-modes appeared while the lock was being made — nothing was overwritten: {written}");
        }

        seams.Events?.Add("snapshot");
        return Applied(input, plan, isolation, seams, held);
    }

    private static LockOutcome Applied(ExperimentInput input, IsolationPlan plan, IFolderIsolation isolation, IsolationSeams seams, Pinned held)
    {
        var applied = isolation.Apply(held, plan);
        if (applied is Outcome<string>.Fail failed)
        {
            return RolledBack(input, plan, isolation, seams, held, $"the lock could not be applied — {failed.Reason}", "the modes were restored");
        }

        var open = isolation.Verify(plan);
        if (open.Count > 0)
        {
            return RolledBack(input, plan, isolation, seams, held, $"the lock could not be verified — {string.Join("; ", open)}", "the modes were restored (running as root, or a file system that ignores modes, such as a Windows drive under WSL?)");
        }

        var at = seams.Clock.GetUtcNow().ToString(SnapshotFiles.Utc, System.Globalization.CultureInfo.InvariantCulture);
        var recorded = Written(input.Root.LockedModesApplied, SnapshotFiles.Applied(plan.Run, plan.Digest, at));
        return recorded.Length > 0
            ? new LockOutcome.Stopped(ExitCodes.Environment, $"the modes are applied but .locked-modes.applied could not be written: {recorded} — run lock {ExecutableRule.Printable(plan.Run)} again")
            : new LockOutcome.Applied($"locked — closed {plan.Entries.Count(entry => entry.Kind == IsolationKind.Closed)}, unlisted {plan.Entries.Count(entry => entry.Kind == IsolationKind.Unlisted)}, verified at {at}");
    }

    /// <summary>The apply or the verification failed: every entry back through the HELD descriptors; the snapshot is moved aside only when none failed to restore, else KEPT (the state reads <i>applying</i>, which the next <c>lock</c> recovers).</summary>
    private static LockOutcome RolledBack(ExperimentInput input, IsolationPlan plan, IFolderIsolation isolation, IsolationSeams seams, Pinned held, string what, string restored)
    {
        var report = isolation.RollBack(held);
        if (report.Failed.Count > 0)
        {
            return new LockOutcome.Stopped(ExitCodes.Environment, $"{what} — restored {report.Restored.Count} of {held.Modes.Count} — {string.Join("; ", report.Failed)} — the snapshot is kept; restore by hand, or run lock {ExecutableRule.Printable(plan.Run)} again");
        }

        return Aside(input, seams, appliedToo: false) is { } stopped ? stopped : new LockOutcome.Stopped(ExitCodes.Environment, $"{what} — {restored}");
    }

    /// <summary>The bytes staged, flushed and renamed in (create-new): the empty string, or why a file that appeared first was not overwritten.</summary>
    private static string Written(string path, string text)
    {
        try
        {
            ArtifactCommit.Stage(path, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
            return string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }
}

/// <summary>What a verb prints once it knows: an exit code and ONE line, written to stdout on 0 and stderr otherwise, prefixed with the verb's name, and appended to <c>lock.log</c> (<c>&lt;utc&gt; lock &lt;run&gt;: …</c>, a refusal as <c>refused (exit n) — …</c>).</summary>
internal sealed record Reported(int ExitCode, string Line)
{
    public static int Print(ExperimentInput input, string label, Reported reported, IsolationSeams seams)
    {
        var line = $"model-arms {label}: {reported.Line}";
        (reported.ExitCode == 0 ? input.Verb.Output : input.Verb.Error).WriteLine(line);
        LockLog.Append(input.Root, seams.Clock, reported.ExitCode == 0 ? $"{label}: {reported.Line}" : $"{label}: refused (exit {reported.ExitCode}) — {reported.Line}", input.Verb.Error);
        return reported.ExitCode;
    }
}
