using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Domain.Probes;

/// <summary>One unit of work in a probe run: a probe, a subject, a repeat, a GENERATION, and the slot and position it runs in.
/// <para>
/// Composes <see cref="Claimable"/> exactly as <see cref="GateCell"/> does — one claim/settle/sweep lifecycle, the
/// three-attempt abandonment included — and is claimed UNDER a <see cref="ProductPin"/> (D9): the answers are facts about a
/// CLI build, and claude moved 2.1.284 → 2.1.286 under VS Code on the day the plan was written.
/// </para>
/// <para>
/// What is its own is <see cref="Generation"/> (D2): a settled cell is terminal, so a re-run never reopens it — it appends a
/// new Pending cell with the next generation for the same (run, probe, subject, repeat), and the history stays — and
/// <see cref="UnmeasuredAttempts"/> (S2c): how many of its attempts were handed back UNMEASURED (a quota stop, an unwritable
/// artefact root). Those keep their number — their directory exists — but never count toward the abandonment (D8: "a quota stop
/// is never a step toward Abandoned"), so the sweep abandons on <see cref="MeasuredAttempts"/>.
/// </para></summary>
public sealed record ProbeCell(
    Guid Id,
    Guid RunId,
    ProbeKind Probe,
    ProbeSubjectId Subject,
    int Repeat,
    int Generation,
    int Slot,
    int Position,
    Claimable Claim,
    ProductPin Pin,
    ProbeFacts Facts,
    IReadOnlyList<ProbeArtifact> Artifacts,
    ProbeReason Reason,
    int UnmeasuredAttempts = 0)
{
    public const int FirstGeneration = 1;

    /// <summary>A cell before anyone claimed it. The id is the CALLER's, as for the gate cell: a factory that minted one would
    /// read a clock, and the same plan could never produce the same cells twice.</summary>
    public static ProbeCell Pending(Guid id, Guid runId, ProbeMatrixCell cell) => new(
        id,
        runId,
        cell.Probe,
        cell.Subject,
        cell.Repeat,
        FirstGeneration,
        cell.Slot,
        cell.Position,
        Claimable.Fresh,
        ProductPin.None,
        ProbeFacts.None,
        [],
        ProbeReason.None);

    public CellState State => Claim.State;

    public int Attempts => Claim.Attempts;

    /// <summary>The attempts that count toward <see cref="Claimable.MaxAttempts"/>: every attempt minus the unmeasured ones.</summary>
    public int MeasuredAttempts => Attempts - UnmeasuredAttempts;

    public WorkerIdentity Owner => Claim.Owner;

    public DateTimeOffset ClaimedAt => Claim.ClaimedAt;

    public bool IsTerminal => Claim.IsTerminal;

    /// <summary>How the cell names itself in a refusal — <c>probe cell 0199…</c>.</summary>
    public string Label => $"probe cell {Id}";

    /// <summary>The identity a generation belongs to — what <c>rerun</c> appends a generation of.</summary>
    public (Guid RunId, ProbeKind Probe, string Subject, int Repeat) Lineage => (RunId, Probe, Subject.Value, Repeat);
}

/// <summary>The claim/settle/sweep transitions over a <see cref="ProbeCell"/>, as pure functions — <see cref="GateCellLifecycle"/>'s
/// shape over the same <see cref="Claimable"/>. What is decided here is only what an abandonment and an unmeasured attempt mean
/// for the probe cell's own columns, that a claim carries a pin, and how a NEXT GENERATION is made.</summary>
public static class ProbeCellLifecycle
{
    /// <summary>Takes the cell for <paramref name="owner"/> under <paramref name="pin"/>. Refused without a pin: a fact about an
    /// unknown build is a fact about nothing.</summary>
    public static Outcome<ProbeCell> Claim(ProbeCell cell, WorkerIdentity owner, DateTimeOffset now, ProductPin pin)
    {
        if (!pin.IsPinned)
        {
            return Outcome<ProbeCell>.Failure($"{cell.Label} is claimed under a product pin, and none was given — the pin is stored per cell at claim time");
        }

        return Claimable.Claim(cell.Claim, owner, now, cell.Label).Match(
            claim => Outcome<ProbeCell>.Success(cell with { Claim = claim, Pin = pin }),
            Outcome<ProbeCell>.Failure);
    }

    /// <summary>Records how a claimed cell ended. An attempt of kind <see cref="ProbeAttemptKind.Unmeasured"/> is never settled —
    /// it is handed back (D8) — and a settlement with no kind says nothing happened.</summary>
    public static Outcome<ProbeCell> Settle(ProbeCell cell, ProbeSettlement settlement)
    {
        var refusal = settlement.Facts.Kind switch
        {
            ProbeAttemptKind.None => $"{cell.Label} cannot settle with no attempt kind — say Answered, LaunchRefused or TimedOut",
            ProbeAttemptKind.Unmeasured => $"{cell.Label}: an unmeasured attempt is handed back, never settled — a quota stop is not a measurement",
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Outcome<ProbeCell>.Failure(refusal);
        }

        return Claimable.Settle(cell.Claim, cell.Label).Match(
            claim => Outcome<ProbeCell>.Success(cell with { Claim = claim, Facts = settlement.Facts, Artifacts = settlement.Artifacts, Reason = ProbeReason.None }),
            Outcome<ProbeCell>.Failure);
    }

    /// <summary>The attempt ran and measured nothing the bench may use (D8): back to Pending, the attempt STAYS counted (its
    /// directory exists) but is counted UNMEASURED (S2c), the kind says <i>unmeasured</i> and the reason says why. Never a step
    /// toward Abandoned — <see cref="Reclaim"/> forgives every unmeasured attempt.</summary>
    public static Outcome<ProbeCell> HandBackUnmeasured(ProbeCell cell, ProbeReason reason) =>
        (cell.State, reason) switch
        {
            (not CellState.Claimed, _) => Outcome<ProbeCell>.Failure($"{cell.Label} is {cell.State}, not Claimed — only a claimed cell is handed back"),
            (_, ProbeReason.None) => Outcome<ProbeCell>.Failure($"{cell.Label}: a hand-back says why — an account out, never nothing"),
            _ => Outcome<ProbeCell>.Success(cell with
            {
                Claim = cell.Claim with { State = CellState.Pending, Owner = WorkerIdentity.Nobody, ClaimedAt = default },
                Facts = ProbeFacts.NothingCaptured(ProbeAttemptKind.Unmeasured, cell.Facts.ExitCode),
                Reason = reason,
                UnmeasuredAttempts = cell.UnmeasuredAttempts + 1,
            }),
        };

    /// <summary>The sweep decision for one cell, on its MEASURED attempts (S2c). The pin stays: the next claim overwrites it with the build
    /// THEN. A requeue clears the reason — the store's sweep writes <see cref="ProbeReason.None"/> on a requeue, and a cell handed back by a
    /// crash is not "account out" because its previous attempt was.</summary>
    public static ProbeCell Reclaim(ProbeCell cell) =>
        Claimable.Reclaim(cell.Claim, cell.UnmeasuredAttempts) switch
        {
            ReclaimDecision.Requeued requeued => cell with { Claim = requeued.Claim, Reason = ProbeReason.None },
            ReclaimDecision.Abandoned abandoned => cell with { Claim = abandoned.Claim, Reason = ProbeReason.Abandoned },
            _ => cell,
        };

    public static bool IsStale(ProbeCell cell, DateTimeOffset now, TimeSpan staleAfter) => Claimable.IsStale(cell.Claim, now, staleAfter);

    /// <summary>The next generation of ONE lineage (D2), given every generation it has so far: a fresh Pending cell numbered
    /// max + 1, at the lineage's slot and position. Refused while any generation is still Pending or Claimed — that one is
    /// still being measured — and for an empty or a mixed list.</summary>
    public static Outcome<ProbeCell> NextGeneration(IReadOnlyList<ProbeCell> generations, Guid newId)
    {
        var refusal = NextGenerationRefusal(generations);

        return refusal.Length > 0
            ? Outcome<ProbeCell>.Failure(refusal)
            : Outcome<ProbeCell>.Success(generations.MaxBy(g => g.Generation)! with
            {
                Id = newId,
                Generation = ProbeGenerations.Highest(generations) + 1,
                Claim = Claimable.Fresh,
                Pin = ProductPin.None,
                Facts = ProbeFacts.None,
                Artifacts = [],
                Reason = ProbeReason.None,
            });
    }

    private static string NextGenerationRefusal(IReadOnlyList<ProbeCell> generations) =>
        (generations.Count, generations.Select(g => g.Lineage).Distinct().Count(), generations.FirstOrDefault(g => !g.IsTerminal)) switch
        {
            (0, _, _) => "a next generation needs the lineage it follows — got no generation at all",
            (_, > 1, _) => "the cells span two lineages — a generation follows ONE (run, probe, subject, repeat)",
            (_, _, { } open) => $"generation {open.Generation} of {open.Label} is {open.State} — it is still being measured; a re-run waits for it",
            _ => string.Empty,
        };
}
