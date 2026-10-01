using Bench.Domain.Runs;

namespace Bench.Domain.Probes;

/// <summary>One planned execution: a probe, a subject, a repeat, the slot it belongs to and the position the subject runs
/// in within that slot — <c>GateMatrixCell</c>'s shape over the probe axes.</summary>
public sealed record ProbeMatrixCell(ProbeKind Probe, ProbeSubjectId Subject, int Repeat, int Slot, int Position);

/// <summary>A (probe, subject) pair the planner left out, and why — a dropped pair is NAMED, never silently absent, so the
/// write-up's "not measured" list is read off the plan rather than guessed.</summary>
public sealed record DroppedPair(ProbeKind Probe, ProbeSubjectId Subject, string Reason)
{
    public string Describe => $"{ProbeWord.Of(Probe)} × {Subject}: {Reason}";
}

public sealed record ProbePlan(IReadOnlyList<ProbeMatrixCell> Cells, IReadOnlyList<DroppedPair> Dropped);

/// <summary>Which probes a subject's runtime can be measured on. The table lives in the domain so the planner and the
/// write-up agree on what was NOT measured, and it says exactly what <c>CliArgv</c> can spell (S2, measured 2026-10-01):
/// every CLI takes a directory grant (<c>--add-dir</c> on claude, codex AND agy 1.2.14 — the S1 guess that antigravity had
/// none is withdrawn), while antigravity has neither a tool deny-list nor a flag that turns the web OFF. <c>read-denied</c>
/// is defined as web OFF with the file tools denied, so on antigravity it cannot be launched as itself — that pair is
/// dropped BY NAME rather than run with the web on and recorded as a measurement of something else. <c>web-confined</c>
/// still runs there as is (web ON, nothing to deny): it answers whether the CLI reads the disk at all.</summary>
public static class ProbeApplicability
{
    /// <summary>Empty when the pair applies; otherwise the reason it is dropped.</summary>
    public static string Reason(ProbeKind probe, ProbeRuntime runtime) =>
        (ProbeTraits.IsApi(probe), runtime == ProbeRuntime.Api, ProbeTraits.NeedsWebOff(probe) && runtime == ProbeRuntime.Antigravity) switch
        {
            (true, false, _) => "api-reachable runs through the product's api path, not a CLI",
            (false, true, _) => "an api subject runs no CLI probe — the product answers over HTTP, there is no process to confine",
            (_, _, true) => "read-denied is web OFF with the file tools denied, and antigravity has a flag for neither (1.2.14, measured 2026-10-01) — "
                            + "the pair is dropped by name rather than measured with the web on",
            _ => string.Empty,
        };
}

/// <summary>Materialising a probe run as probe × subject × repeat, <b>repeats outermost</b> and the subjects rotated on
/// the global slot counter (<see cref="SlotRotation"/>, the same function both other matrices call), with the pairs the
/// runtime cannot honour dropped and named.
/// <para>
/// Outermost for the gate matrix's reason: the three repeats of one pair are hours apart, so a vendor's cache and its
/// rate limit measure the model rather than the previous minute. A slot no subject can run takes no slot number, so the
/// rotation stays balanced over the slots that exist. Repeats are numbered from ONE.
/// </para></summary>
public static class ProbeMatrix
{
    public static Outcome<ProbePlan> Plan(IReadOnlyList<ProbeKind> probes, IReadOnlyList<ProbeSubject> subjects, int repeats)
    {
        var refusal = Validate(probes, subjects, repeats);
        if (refusal.Length > 0)
        {
            return Outcome<ProbePlan>.Failure(refusal);
        }

        var dropped = Dropped(probes, subjects);
        var slots = Slots(probes, subjects, repeats);

        return slots.Count == 0
            ? Outcome<ProbePlan>.Failure($"every pair was dropped, nothing is left to measure: {string.Join("; ", dropped.Select(d => d.Describe))}")
            : Outcome<ProbePlan>.Success(new ProbePlan(Cells(slots), dropped));
    }

    private static IReadOnlyList<DroppedPair> Dropped(IReadOnlyList<ProbeKind> probes, IReadOnlyList<ProbeSubject> subjects) =>
        [.. probes.SelectMany(probe => subjects
            .Select(subject => new DroppedPair(probe, subject.Id, ProbeApplicability.Reason(probe, subject.Runtime)))
            .Where(d => d.Reason.Length > 0))];

    /// <summary>Repeats OUTERMOST: every probe once, then every probe again; inside a slot, the subjects that can run it.</summary>
    private static IReadOnlyList<(ProbeKind Probe, int Repeat, IReadOnlyList<ProbeSubjectId> Subjects)> Slots(
        IReadOnlyList<ProbeKind> probes, IReadOnlyList<ProbeSubject> subjects, int repeats) =>
        [.. Enumerable.Range(1, repeats)
            .SelectMany(repeat => probes.Select(probe => (probe, repeat, Applicable(probe, subjects))))
            .Where(slot => slot.Item3.Count > 0)];

    private static IReadOnlyList<ProbeSubjectId> Applicable(ProbeKind probe, IReadOnlyList<ProbeSubject> subjects) =>
        [.. subjects.Where(s => ProbeApplicability.Reason(probe, s.Runtime).Length == 0).Select(s => s.Id)];

    private static IReadOnlyList<ProbeMatrixCell> Cells(IReadOnlyList<(ProbeKind Probe, int Repeat, IReadOnlyList<ProbeSubjectId> Subjects)> slots) =>
        [.. slots.SelectMany((slot, slotIndex) => SlotRotation.Rotated(slot.Subjects, slotIndex)
            .Select((subject, position) => new ProbeMatrixCell(slot.Probe, subject, slot.Repeat, slotIndex, position)))];

    private static string Validate(IReadOnlyList<ProbeKind> probes, IReadOnlyList<ProbeSubject> subjects, int repeats) =>
        (probes.Count, subjects.Count, repeats) switch
        {
            (0, _, _) => "a probe matrix needs at least one probe",
            (_, 0, _) => "a probe matrix needs at least one subject",
            (_, _, < 1) => $"repeats must be at least 1, got {repeats}",
            _ => Distinct(probes, subjects),
        };

    private static string Distinct(IReadOnlyList<ProbeKind> probes, IReadOnlyList<ProbeSubject> subjects)
    {
        var probe = probes.GroupBy(p => p).FirstOrDefault(g => g.Count() > 1);
        var subject = subjects.GroupBy(s => s.Id.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        return (probe, subject) switch
        {
            (not null, _) => $"probe '{ProbeWord.Of(probe.Key)}' is listed twice — the same question twice is one fact counted as two",
            (_, not null) => $"subject '{subject.Key}' is listed twice — the same subject twice is one CLI measured as two",
            _ => string.Empty,
        };
    }
}
