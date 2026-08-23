namespace Bench.Domain.Trace;

/// <summary>Whether the runs behind one comparison came off one machine.
/// <para>
/// Four states rather than a boolean, because "they agree" and "nobody looked" are opposite pieces of news
/// and a flag can only carry one of them. This is <see cref="MachineFacts"/>'s own three-state promise —
/// same machine · different machine · not recorded — plus the state a real database actually produces: some
/// runs probed and some stored before the probe existed.
/// </para></summary>
public enum MachineConsensus
{
    /// <summary>No run here recorded a machine. Nothing is known, which is not the same as agreement.</summary>
    NotRecorded,

    /// <summary>Every run that recorded a machine names the same one — but others recorded none, so the
    /// agreement covers only part of the population.</summary>
    PartlyRecorded,

    /// <summary>Every run recorded a machine and it is the same one. The only state under which a difference
    /// between arms is attributable to the arm.</summary>
    OneMachine,

    /// <summary>The runs came off more than one machine. Not a refusal — hardware changes, and a benchmark
    /// unable to span a driver update would be useless — but the comparison is confounded and must say so.</summary>
    SeveralMachines,
}

/// <summary>Whether a set of runs shares a machine, and the sentence that says it.
///
/// <para><b>Why this is a guard and not a footnote.</b> A compute arm is <em>host/provider/device</em>
/// recorded on the RUN, so comparing two arms means comparing two runs — and two runs on two machines differ
/// for a reason that has nothing to do with the backend. Nothing recorded which machine produced a row until
/// <c>MachineFacts</c> existed, so two machines' results merged silently; folding them now without saying so
/// would keep the defect and add a number to it.</para>
///
/// <para><b>It never refuses.</b> <see cref="MachineConsensus.SeveralMachines"/> is reported, never blocked:
/// refusing would make the harness unable to compare anything across a hardware change, which is the ordinary
/// life of a benchmark that runs for months. The rule is the one <see cref="MachineFacts"/> already states —
/// a fingerprint is a fingerprint, not a gate.</para>
///
/// <para>Pure, and shared: the run report and the arm comparison ask the same question, and a second copy of
/// a rule about confounded comparisons is the last thing this repository can afford to let drift.</para>
/// </summary>
/// <param name="Machines">How many DISTINCT machines the recorded runs name. Zero when none recorded one.</param>
/// <param name="Unrecorded">How many runs recorded no machine at all. Beside the count rather than folded
/// into it, because a run nobody probed is not evidence of a second machine — nor of the first.</param>
public sealed record MachineAgreement(MachineConsensus State, int Machines, int Unrecorded)
{
    /// <summary>Nothing was recorded and nothing asked. What a comparison over pre-probe runs is.</summary>
    public static MachineAgreement Nothing { get; } = new(MachineConsensus.NotRecorded, 0, 0);

    /// <summary>Folds the machines behind a population of runs into one reading.</summary>
    public static MachineAgreement Of(IEnumerable<MachineFacts> facts)
    {
        var all = facts.ToList();
        var fingerprints = all.Where(f => f.Recorded).Select(f => f.Fingerprint).Distinct(StringComparer.Ordinal).Count();
        var unrecorded = all.Count(f => !f.Recorded);

        return new MachineAgreement(Consensus(fingerprints, unrecorded), fingerprints, unrecorded);
    }

    /// <summary>True only where a difference between arms may be read as a property of the arm. Both unknown
    /// states answer false: an unverified comparison and a confounded one are equally unsafe to rank on, and
    /// a caller that had to distinguish them would re-implement this rule.</summary>
    public bool OnOneMachine => State is MachineConsensus.OneMachine;

    /// <summary>The actionable half. Never empty — a comparison always says what it knows about its
    /// hardware, because silence here reads as agreement.</summary>
    public string Describe => State switch
    {
        MachineConsensus.OneMachine =>
            "all of these runs were measured on one machine.",
        MachineConsensus.SeveralMachines =>
            $"these runs were measured on {Machines} DIFFERENT machines — a gap between arms is not "
            + "attributable to the backend alone, because the hardware under them also differs.",
        MachineConsensus.PartlyRecorded =>
            $"the runs that recorded a machine all name the same one, but {Unrecorded} recorded none — "
            + "so nothing here can vouch that the whole comparison ran on one machine.",
        _ =>
            "no run here recorded the machine it measured on, so nothing can say whether these numbers "
            + "came off one — a run stored before the machine probe existed cannot be attributed to hardware.",
    };

    private static MachineConsensus Consensus(int fingerprints, int unrecorded) =>
        (fingerprints, unrecorded) switch
        {
            (0, _) => MachineConsensus.NotRecorded,
            ( > 1, _) => MachineConsensus.SeveralMachines,
            (_, > 0) => MachineConsensus.PartlyRecorded,
            _ => MachineConsensus.OneMachine,
        };
}
