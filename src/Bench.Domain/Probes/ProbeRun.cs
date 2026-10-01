using System.Text.RegularExpressions;
using Bench.Domain.Runs;

namespace Bench.Domain.Probes;

/// <summary>Where the web oracle's value came from: the registry, read once at <c>run</c> before anything was planned, or
/// the operator's <c>--oracle-version</c> (D7).</summary>
public enum OracleSource
{
    Registry,
    Manual,
}

/// <summary>The frozen web oracle — the version of <c>@openai/codex</c> the <c>web-search</c> answer is compared with. Stored on
/// the run so <c>resume</c> and <c>rerun</c> read it from the run and never fetch again.</summary>
public sealed partial record ProbeOracle
{
    private ProbeOracle(string version, OracleSource source)
    {
        Version = version;
        Source = source;
    }

    public string Version { get; }

    public OracleSource Source { get; }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")]
    private static partial Regex Semver { get; }

    public static Outcome<ProbeOracle> Parse(string? version, OracleSource source)
    {
        var trimmed = (version ?? string.Empty).Trim().TrimStart('v', 'V');

        return Semver.IsMatch(trimmed)
            ? Outcome<ProbeOracle>.Success(new ProbeOracle(trimmed, source))
            : Outcome<ProbeOracle>.Failure($"'{trimmed}' is not a version — the oracle is a semver (1.2.3, optionally -pre), and a run without one is not planned");
    }
}

/// <summary>One <c>bench probes run</c> invocation — the campaign its cells belong to.
/// <para>
/// <b>No stored status</b> (D3): open or finished is derived from the cells (<see cref="ProbeRunProgress"/>), because the
/// gate's forward-only <c>Finished</c> is exactly what makes a re-run impossible there. The subjects are FROZEN here (D4),
/// references not values, so nothing a later verb does depends on a file that may have changed.
/// </para></summary>
public sealed record ProbeRun(Guid Id, ProbeOracle Oracle, IReadOnlyList<ProbeSubject> Subjects, int Repeats, DateTimeOffset CreatedAt)
{
    /// <summary>Set by <c>bench probes prune --run</c>: the artefacts are gone and the verdicts are no longer auditable from disk.</summary>
    public bool ArtifactsPruned { get; init; }

    /// <summary>The probes this run was ASKED for (<c>--probes</c>, default all seven), frozen so the planner's dropped pairs can be
    /// recomputed from them and the frozen subjects (<see cref="ProbeMatrix.DroppedPairs"/>) — a probe every subject dropped plans no
    /// cell, so the cells alone cannot name it. Empty on a run that recorded none; the report then falls back to the probes its cells
    /// name.</summary>
    public IReadOnlyList<ProbeKind> Probes { get; init; } = [];

    public static Outcome<ProbeRun> Planned(Guid id, ProbeOracle oracle, IReadOnlyList<ProbeSubject> subjects, int repeats, DateTimeOffset now)
    {
        var refusal = PlannedRefusal(subjects, repeats);

        return refusal.Length > 0
            ? Outcome<ProbeRun>.Failure(refusal)
            : Outcome<ProbeRun>.Success(new ProbeRun(id, oracle, [.. subjects], repeats, now));
    }

    private static string PlannedRefusal(IReadOnlyList<ProbeSubject> subjects, int repeats) =>
        (subjects.Count, repeats, subjects.GroupBy(s => s.Id.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1)) switch
        {
            (0, _, _) => "a probe run needs at least one subject",
            (_, < 1, _) => $"repeats must be at least 1, got {repeats}",
            (_, _, { } twice) => $"subject '{twice.Key}' is listed twice — the same subject twice is one CLI measured as two",
            _ => string.Empty,
        };

    /// <summary>The frozen subject a cell names, or a refusal — a cell naming a subject its run does not hold is a hand-edited row.</summary>
    public Outcome<ProbeSubject> Subject(ProbeSubjectId id) =>
        Subjects.FirstOrDefault(s => s.Id == id) is { } subject
            ? Outcome<ProbeSubject>.Success(subject)
            : Outcome<ProbeSubject>.Failure($"probe run {Id} holds no subject '{id}'");
}

/// <summary>Where a run stands, read off its cells — the run has no status column of its own (D3).</summary>
public sealed record ProbeRunProgress(int Pending, int Claimed, int Settled, int Abandoned)
{
    /// <summary>Something is still to be measured or being measured — <c>prune</c> refuses, the page keeps polling.</summary>
    public bool IsOpen => Pending + Claimed > 0;

    public static ProbeRunProgress Of(IEnumerable<ProbeCell> cells)
    {
        var states = cells.Select(c => c.State).ToList();

        return new ProbeRunProgress(
            states.Count(s => s == CellState.Pending),
            states.Count(s => s == CellState.Claimed),
            states.Count(s => s == CellState.Settled),
            states.Count(s => s == CellState.Abandoned));
    }
}
