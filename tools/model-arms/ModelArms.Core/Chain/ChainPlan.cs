using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;

namespace ModelArms.Core.Chain;

/// <summary>What the chain knows about one arm of its run, read by name (C25). <see cref="Made"/> = <c>chain/made-&lt;arm&gt;</c>; <see cref="Done"/> = <see cref="Steps.TimingLines.IsArmDone"/> of its timing; <see cref="Alive"/> = its runner holds <c>pids/&lt;arm&gt;.lock</c>; <see cref="Ran"/> = <c>pids/&lt;arm&gt;.pid</c> is there (a runner of the arm held its lock and wrote it, E5.S1); <see cref="Checked"/> = <c>chain/objective-&lt;arm&gt;</c>.</summary>
public sealed record ArmFacts(string Arm, bool Made, bool Done, bool Alive, bool Ran, bool Checked);

/// <summary>Everything <see cref="ChainPlan.Next"/> decides from (C25). <see cref="Arms"/> are this run's, in round order; <see cref="OthersAlive"/> the map's other arms whose runner is alive; the three markers are the run's own (<c>chain/&lt;run&gt;/…</c>); <see cref="Digest"/> is <see cref="IsolationPlan.Digest"/> of this run, <c>null</c> for no isolation.
/// The spans are measured by the chain's clock from instants it recorded: <see cref="StartingFor"/> since its last <c>Start</c> while an arm it started is neither alive, done nor ran (else zero); <see cref="AwaitedFor"/> how long <see cref="ChainAction.AwaitExit"/> has stood (else zero); <see cref="WaitedFor"/> since its first action in the current round; <see cref="WaitCeiling"/> is <see cref="ChainWaitCeiling.OfRound"/> of that round.</summary>
public sealed record ChainFacts(
    RunSpec Run,
    IReadOnlyList<ArmFacts> Arms,
    IReadOnlyList<string> OthersAlive,
    bool Locked,
    bool Unlocked,
    bool ArmsDone,
    LockState Snapshot,
    string? Digest,
    IReadOnlySet<string> StartedHere,
    TimeSpan StartingFor,
    TimeSpan AwaitedFor,
    TimeSpan Grace,
    TimeSpan WaitedFor,
    TimeSpan WaitCeiling);

/// <summary>The one thing the chain does next (C25), closed. <see cref="ExitCode"/> is <c>null</c> for an action the chain carries out and goes on from, else the code the chain ends with: 5 stopped and resumable (an arm died, a bounded wait expired, an arm of another run is alive), 4 configuration (a marker over a snapshot it does not match), 0 finished.</summary>
public abstract record ChainAction
{
    private ChainAction()
    {
    }

    public virtual int? ExitCode => null;

    public sealed record Make(string Arm) : ChainAction;

    public sealed record Lock : ChainAction;

    public sealed record Start(IReadOnlyList<string> Arms) : ChainAction;

    /// <summary>An arm of the round whose runner held its lock, wrote its pid, and is neither alive nor done: it stopped without finishing. Nothing new is started while an arm waits for the operator (§8 item 14).</summary>
    public sealed record StopArmDied(string Arm) : ChainAction
    {
        public override int? ExitCode => 5;
    }

    /// <summary>Arms started by this chain whose runner has not yet taken its lock (§8 item 46): polled, never read as died; expired past <see cref="ChainWaitCeiling.StartupCeiling"/>.</summary>
    public sealed record AwaitStart(IReadOnlyList<string> Arms, bool Expired) : ChainAction
    {
        public override int? ExitCode => Expired ? 5 : null;
    }

    public sealed record WaitFor(string Arm, bool Expired) : ChainAction
    {
        public override int? ExitCode => Expired ? 5 : null;
    }

    /// <summary>Runners of this run that wrote <c>arm-done</c> and still hold their lock (§8 item 47), waited for before every <c>Start</c> and before <c>Unlock</c>; expired at <see cref="ChainFacts.Grace"/>.</summary>
    public sealed record AwaitExit(IReadOnlyList<string> Arms, bool Expired) : ChainAction
    {
        public override int? ExitCode => Expired ? 5 : null;
    }

    public sealed record StopOtherRunAlive(string Arm) : ChainAction
    {
        public override int? ExitCode => 5;
    }

    /// <summary>A stage marker over a state it does not match (parent §4.7): never a skip. <paramref name="State"/> is the words that name the state.</summary>
    public sealed record RefuseMarker(RunStage Stage, string State) : ChainAction
    {
        public override int? ExitCode => 4;
    }

    public sealed record Unlock : ChainAction;

    public sealed record Check(string Arm) : ChainAction;

    public sealed record Finished : ChainAction
    {
        public override int? ExitCode => 0;
    }
}

/// <summary>The chain's decision (C25, parent §4.7), pure: the FIRST rule that holds answers, one action at a time, from facts the chain just read — a marker is never proof of the current state, so the chain is stateless between its own actions and a restarted one decides afresh. In order: (1) <c>arms-done</c> is finished; (2) <c>unlocked</c> goes on to the checks, which run under any snapshot; (3) every arm of the run is made, in round
/// order; (4) the isolation is locked — <c>lock</c> is the one place a half-applied lock is recovered; (5) <b>the reconciliation</b>: <c>locked</c> stands only over an applied snapshot of THIS run and THIS digest (or none under no isolation), else <see cref="ChainAction.RefuseMarker"/>; (6) the rounds, at the first one with an arm not done: an arm that ran and is gone stops the chain (nothing new is started while an arm
/// waits for the operator), an arm just started is awaited (§8 item 46), the arms not yet started are started — after the finished runners of this run have exited (§8 item 47) — and else the round is waited for under its ceiling (§8 item 49); (7) every arm done, a runner of the run still exiting is awaited; (8) an arm of another run alive stops the chain; (9) unlock.</summary>
public static class ChainPlan
{
    public static ChainAction Next(ChainFacts facts) =>
        Finishing(facts) ?? Checking(facts) ?? Making(facts) ?? Locking(facts) ?? Reconciled(facts) ?? Rounds(facts) ?? Closing(facts);

    private static ChainAction? Finishing(ChainFacts facts) => facts.ArmsDone ? new ChainAction.Finished() : null;

    /// <summary>Rule 2: after <c>unlocked</c> the checks, the first arm not checked; none left is the end. <c>unlocked</c> is not reconciled — the checks run under any snapshot and print its line (§8 item 12).</summary>
    private static ChainAction? Checking(ChainFacts facts) =>
        !facts.Unlocked ? null
        : ArmsOf(facts).FirstOrDefault(arm => !arm.Checked) is { } unchecked_ ? new ChainAction.Check(unchecked_.Arm)
        : new ChainAction.Finished();

    private static ChainAction? Making(ChainFacts facts) =>
        ArmsOf(facts).FirstOrDefault(arm => !arm.Made) is { } unmade ? new ChainAction.Make(unmade.Arm) : null;

    private static ChainAction? Locking(ChainFacts facts) => facts.Locked ? null : new ChainAction.Lock();

    /// <summary>Rule 5: <c>locked</c> stands only over what it claims. Under no isolation (<see cref="ChainFacts.Digest"/> null) only over no snapshot — a stale one is refused, never run under.</summary>
    private static ChainAction? Reconciled(ChainFacts facts) =>
        Stands(facts) ? null : new ChainAction.RefuseMarker(RunStage.Locked, StateWords(facts));

    private static bool Stands(ChainFacts facts) =>
        facts.Digest is null
            ? facts.Snapshot is LockState.None
            : facts.Snapshot is LockState.Applied applied && string.Equals(applied.Run, facts.Run.Name, StringComparison.Ordinal) && string.Equals(applied.Digest, facts.Digest, StringComparison.Ordinal);

    private static string StateWords(ChainFacts facts) =>
        IsolationLine.Of(facts.Snapshot)
        + (facts.Snapshot is LockState.Applied applied && string.Equals(applied.Run, facts.Run.Name, StringComparison.Ordinal) ? " with another closed set" : string.Empty);

    /// <summary>Rule 6, at the first round with an arm not done; null when every round is done.</summary>
    private static ChainAction? Rounds(ChainFacts facts) =>
        CurrentRound(facts) is { } round ? InRound(facts, round) : null;

    /// <summary>The facts of the arms of the first round that has one not done, in round order; null when none has.</summary>
    internal static IReadOnlyList<ArmFacts>? CurrentRound(ChainFacts facts) =>
        facts.Run.Rounds.Select(round => round.Select(arm => FactsOf(facts, arm)).ToList()).FirstOrDefault(round => round.Any(arm => !arm.Done));

    private static ChainAction InRound(ChainFacts facts, IReadOnlyList<ArmFacts> round) =>
        Died(round) ?? Starting(facts, round) ?? Starts(facts, round) ?? Waiting(facts, round);

    private static ChainAction? Died(IReadOnlyList<ArmFacts> round) =>
        round.FirstOrDefault(arm => !arm.Done && !arm.Alive && arm.Ran) is { } died ? new ChainAction.StopArmDied(died.Arm) : null;

    /// <summary>6 (b): started here, not yet holding the runner's lock — a runner that has not taken <c>pids/&lt;arm&gt;.lock</c> is not dead.</summary>
    private static ChainAction? Starting(ChainFacts facts, IReadOnlyList<ArmFacts> round) =>
        round.Where(arm => facts.StartedHere.Contains(arm.Arm) && Unstarted(arm)).Select(arm => arm.Arm).ToList() is { Count: > 0 } starting
            ? new ChainAction.AwaitStart(starting, facts.StartingFor >= ChainWaitCeiling.StartupCeiling)
            : null;

    /// <summary>6 (c): arms nobody has started — after the finished runners of this run have gone, because <c>run-all</c> counts an exiting runner alive and its queue refusal would trip.</summary>
    private static ChainAction? Starts(ChainFacts facts, IReadOnlyList<ArmFacts> round)
    {
        var toStart = round.Where(arm => !facts.StartedHere.Contains(arm.Arm) && Unstarted(arm)).Select(arm => arm.Arm).ToList();
        return toStart.Count == 0 ? null : Exiting(facts) ?? new ChainAction.Start(toStart);
    }

    /// <summary>Neither done, alive nor ran.</summary>
    private static bool Unstarted(ArmFacts arm) => !arm.Done && !arm.Alive && !arm.Ran;

    private static ChainAction Waiting(ChainFacts facts, IReadOnlyList<ArmFacts> round) =>
        new ChainAction.WaitFor(round.First(arm => arm.Alive && !arm.Done).Arm, facts.WaitedFor >= facts.WaitCeiling);

    /// <summary>Rules 7 to 9, every arm done.</summary>
    private static ChainAction Closing(ChainFacts facts) =>
        Exiting(facts) ?? (facts.OthersAlive.Count > 0 ? new ChainAction.StopOtherRunAlive(facts.OthersAlive[0]) : new ChainAction.Unlock());

    /// <summary>This run's runners that wrote <c>arm-done</c> and still hold their lock, awaited at most the kill grace.</summary>
    private static ChainAction? Exiting(ChainFacts facts) =>
        ArmsOf(facts).Where(arm => arm.Done && arm.Alive).Select(arm => arm.Arm).ToList() is { Count: > 0 } exiting
            ? new ChainAction.AwaitExit(exiting, facts.AwaitedFor >= facts.Grace)
            : null;

    private static IEnumerable<ArmFacts> ArmsOf(ChainFacts facts) =>
        facts.Run.Rounds.SelectMany(round => round).Distinct(StringComparer.Ordinal).Select(arm => FactsOf(facts, arm));

    private static ArmFacts FactsOf(ChainFacts facts, string arm) =>
        facts.Arms.FirstOrDefault(each => string.Equals(each.Arm, arm, StringComparison.Ordinal)) ?? new ArmFacts(arm, false, false, false, false, false);
}
