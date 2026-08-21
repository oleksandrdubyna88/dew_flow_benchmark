using Bench.Domain.Trace;

namespace Bench.Domain.Runs;

/// <summary>Who saw a tool call happen.
///
/// <para>The two are <b>never blended</b> — the same rule the bench trace and the server telemetry already
/// follow, one level down. A report may show them side by side and may not average them together, because
/// they answer different questions: one records what the harness drove, the other what a server was asked
/// for by a loop nobody here could see inside.</para>
/// </summary>
public enum ToolCallSource
{
    /// <summary>The harness drove every turn — <c>ToolLoopRunner</c> saw the call, its arguments, its
    /// outcome and the turn it happened on.</summary>
    Observed,

    /// <summary>Recovered afterwards from the MCP server's spool, joined by correlation. Carries arguments,
    /// outcome and server time, but <b>no ordering relative to the model's thinking</b>: a CLI agent runs
    /// its own loop and the harness is outside it.</summary>
    Reconstructed,
}

/// <summary>One tool call as it will be read back — the trace record, plus where it sat in the leg.
/// </summary>
/// <param name="Ordinal">Position in the whole leg, from 0. The doctrine under test is an ORDERING claim
/// ("locate before you read"), so a ledger that cannot say what came first cannot falsify it.</param>
/// <param name="Turn">Which model turn asked for it. Several calls can share a turn — a model may request
/// three tools at once — which is exactly why turn and ordinal are separate numbers.</param>
/// <param name="Phase">The leg phase it happened in. A fix leg investigates and then fixes; averaging a
/// call made while diagnosing with one made while patching would describe neither.</param>
public sealed record LedgerEntry(int Ordinal, int Turn, PhaseKind Phase, ToolCall Call);

/// <summary>
/// Every tool call one leg made, in order.
///
/// <para><b><c>Offered</c> is not <c>Entries.Count > 0</c>.</b> A lane that offered four tools to a subject
/// that reached for none is a real, interesting zero — it is evidence about the tool DESCRIPTIONS — and it
/// must never read as the floor lane, which could not have called anything. That is the same distinction
/// <c>ToolUsageObservation</c> keeps for the metric, and it is kept twice on purpose: a stored artefact that
/// loses it cannot be re-read into the metric later.</para>
///
/// <para><b>One source per ledger.</b> The flag lives here rather than only on each row because it is an
/// invariant, not a label: a leg is either one the harness drove or one it reconstructed afterwards, and a
/// ledger that could hold both would be the blending the rule forbids.</para>
/// </summary>
public sealed record ToolLedger(bool Offered, ToolCallSource Source, IReadOnlyList<LedgerEntry> Entries)
{
    /// <summary>The floor: this lane offered no tools at all. Not "it called nothing".</summary>
    public static ToolLedger NotOffered { get; } = new(false, ToolCallSource.Observed, []);

    /// <summary>What the harness itself drove, turn by turn.</summary>
    public static ToolLedger Watched(IReadOnlyList<LedgerEntry> entries) =>
        new(true, ToolCallSource.Observed, entries);

    /// <summary>What a later pass recovered from a server's spool for a loop the harness never saw.</summary>
    public static ToolLedger Recovered(IReadOnlyList<LedgerEntry> entries) =>
        new(true, ToolCallSource.Reconstructed, entries);

    /// <summary>Tool names in call order, repeats included.
    /// <para>
    /// Repeats are kept because the ordering claim is about the SEQUENCE — "search, read, read" and
    /// "read, search, read" are the difference between a doctrine followed and a doctrine ignored, and a
    /// deduplicated set cannot tell them apart.
    /// </para></summary>
    public IReadOnlyList<string> Sequence => [.. Entries.OrderBy(e => e.Ordinal).Select(e => e.Call.Name)];

    /// <summary>How many calls the engine refused. A refused call still shows the subject SELECTED the tool,
    /// which is what a description is measured on — this is the separate question of whether they worked.</summary>
    public int Refused => Entries.Count(e => e.Call.Refused);
}
