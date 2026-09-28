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

/// <summary>What a resume ASKS for: nothing (the stored mode), or one of the two explicitly — which must then be the
/// stored one.</summary>
public enum RequestedDataDir
{
    AsStored,
    Isolated,
    Shared,
}

public static class GateRunResume
{
    /// <summary>A resume never flips the data-directory mode: a run planned isolated and resumed shared would put its
    /// later cells in one directory and its earlier ones in their own, and the report could not tell which population
    /// a number came from. Refused naming the stored mode.</summary>
    public static Outcome<GateRun> Resume(GateRun run, RequestedDataDir requested) =>
        (run.IsTerminal, requested, run.Mode) switch
        {
            (true, _, _) => Outcome<GateRun>.Failure($"gate run {run.Id} is {run.Status} — a finished run is a record, and it is not resumed"),
            (_, RequestedDataDir.Shared, DataDirMode.Isolated) or (_, RequestedDataDir.Isolated, DataDirMode.Shared) =>
                Outcome<GateRun>.Failure(
                    $"gate run {run.Id} was planned with {run.Mode.ToString().ToLowerInvariant()} data directories and is resumed with the mode it "
                    + $"was planned with — asking for {requested.ToString().ToLowerInvariant()} would mix two populations in one run"),
            _ => Outcome<GateRun>.Success(run),
        };
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

    /// <summary>SHA-256 of the prediction written before the run (measurement rule 4). The TEXT is free text, so it lives
    /// in the artefact root (<c>runs/&lt;id&gt;/prediction.txt</c>); the database holds its hash. Empty when none was given.</summary>
    public string PredictionHash { get; init; } = string.Empty;

    /// <summary>Whether this run was started with <c>--allow-product-change</c>: a product that moves during it is claimed
    /// under the new pin (a new scope) instead of stopping the campaign.</summary>
    public bool AllowProductChange { get; init; }

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

    /// <summary>What the attempt learned about the product session beside its facts: the handshake's version, the hash of
    /// the reviewer's resolved references, and the settings check as counts.</summary>
    public GateSessionNotes Notes { get; init; } = GateSessionNotes.None;
}

/// <param name="ServerVersion"><c>serverInfo.version</c> from the MCP handshake — beside the pin, because a rebuild can
/// print one version from different bytes and the other way round.</param>
/// <param name="ReferencesHash">SHA-256 over the VALUES the reviewer's references resolved to on this machine (a referenced
/// endpoint's url, a CLI's path). The row hashes the names; this is what makes a re-pointed reference visible.</param>
/// <param name="SettingsChecked">How many asked-for settings the session file could show.</param>
/// <param name="SettingsMismatches">How many of those it showed differently — a setting accepted and ignored.</param>
public sealed record GateSessionNotes(string ServerVersion, string ReferencesHash, int SettingsChecked, int SettingsMismatches)
{
    public static GateSessionNotes None { get; } = new(string.Empty, string.Empty, 0, 0);
}
