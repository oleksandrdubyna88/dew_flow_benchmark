namespace Bench.Domain.Gate;

/// <summary>Where a gate run stands. Forward-only: <see cref="Planned"/> → <see cref="Running"/> → one of the two
/// terminal states. A terminal run's cells are never swept — a finished campaign is a record, and handing one
/// of its claims back would reopen a measurement nobody is driving any more.</summary>
public enum GateRunStatus
{
    Planned,
    Running,
    Finished,
    Failed,
}

/// <summary>Whether every cell of a run gets a product data directory of its own or all of them share one.
/// Stored on the run, so a resumed campaign cannot flip it.</summary>
public enum DataDirMode
{
    Isolated,
    Shared,
}

/// <summary>One <c>bench gate run</c> invocation — the campaign its cells belong to, and the id every artefact
/// path starts with (<c>runs/&lt;runId&gt;/…</c>).
/// <para>
/// A product session is ONE CELL of a run, not the run: a run is a matrix of task × reviewer × repeat, and each
/// cell's settled attempt is the session the report reads (<see cref="GateRunRecord"/> carries the run as its
/// campaign and the cell as its run id). Only one attempt of a cell ever settles — a settled cell is terminal —
/// so the cell is where that session's facts live.
/// </para></summary>
/// <param name="SuiteStamp">The frozen suite's <c>id#hash12</c> — never its path or its private names.</param>
/// <param name="Source">Where the run came from: <c>native</c>, or the other harness it was imported from.</param>
public sealed record GateRun(
    Guid Id,
    GateKind Gate,
    string SuiteStamp,
    DataDirMode Mode,
    GateRunStatus Status,
    RunSource Source,
    DateTimeOffset CreatedAt)
{
    /// <summary>A run that ended — whole or failed. Its cells are never swept and never claimed again.</summary>
    public bool IsTerminal => Status is GateRunStatus.Finished or GateRunStatus.Failed;

    public static GateRun Planned(Guid id, GateKind gate, string suiteStamp, DataDirMode mode, DateTimeOffset now) =>
        new(id, gate, suiteStamp, mode, GateRunStatus.Planned, new RunSource.Native(), now);
}

/// <summary>How a claimed cell ended, as the store records it — a closed hierarchy, because the two ends carry
/// different payloads and a caller has to say which one it has.</summary>
public abstract record GateSettlement
{
    private GateSettlement()
    {
    }

    /// <summary>The product session ran to its end: its facts, its findings (hashes only), and the hashes of
    /// what it was sent. Whether the run was VALID is a fact inside <see cref="Facts"/>, not about the cell.</summary>
    public sealed record Completed(
        GateRunFacts Facts,
        IReadOnlyList<GateFinding> Findings,
        string SettingsHash,
        string PromptHash) : GateSettlement;

    /// <summary>The session could not be driven to an end. The cause is the one free-text column a cell stores,
    /// and it is redacted (<see cref="FailureRedaction"/>) before it reaches the store.</summary>
    public sealed record Failed(FailureCause Cause) : GateSettlement;

    public GateCellOutcomeKind Kind => this is Completed ? GateCellOutcomeKind.Completed : GateCellOutcomeKind.Failed;
}
