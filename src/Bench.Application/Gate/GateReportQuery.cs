using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>Why a gate read did not answer, in the three shapes a caller acts on differently: the request was malformed
/// (400 · exit 4), the thing it named is not here (404 · exit 4), or it exists and a prerequisite is missing — the suite's
/// tasks were never recorded (409 · exit 3).</summary>
public enum GateRefusalKind
{
    BadRequest,
    NotFound,
    Conflict,
}

/// <summary>A gate read's answer, or its refusal by kind.</summary>
public abstract record GateAnswer<T>
{
    private GateAnswer()
    {
    }

    public sealed record Answered(T Value) : GateAnswer<T>;

    public sealed record Refused(GateRefusalKind Kind, string Reason) : GateAnswer<T>;

    public static GateAnswer<T> Of(T value) => new Answered(value);

    public static GateAnswer<T> Refuse(GateRefusalKind kind, string reason) => new Refused(kind, reason);
}

/// <summary>The gate report's use cases — scopes, one scope's per-model table under ONE rubric, one scope's run list, one
/// run — over <see cref="IGateReads"/>, for the CLI and the API alike. Every decision about what a figure is over is
/// <see cref="GateReport"/>'s; this decides only WHICH scope and rubric were asked for, and refuses in words.</summary>
public static class GateReportQuery
{
    public const string NoScopeNamed =
        "name a scope — ?scope=<id> (or --scope), one of the ids the scope list gives; a gate report over every scope at once would put two products in one column";

    public const string NoRubricNamed =
        "name a rubric — ?rubric=<id> (or --rubric), one this scope's verdicts carry; strict and lenient verdicts are two populations, so there is no default";

    public static async Task<GateAnswer<IReadOnlyList<GateScopeDto>>> ScopesAsync(IGateReads reads, string gateWord, CancellationToken cancellationToken)
    {
        var gates = (gateWord.Length, GateWord.Parse(gateWord)) switch
        {
            (0, _) => Outcome<IReadOnlyList<GateKind>>.Success(Enum.GetValues<GateKind>()),
            (_, Outcome<GateKind>.Ok one) => Outcome<IReadOnlyList<GateKind>>.Success([one.Value]),
            (_, Outcome<GateKind>.Fail bad) => Outcome<IReadOnlyList<GateKind>>.Failure(bad.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

        return gates switch
        {
            Outcome<IReadOnlyList<GateKind>>.Ok wanted => GateAnswer<IReadOnlyList<GateScopeDto>>.Of(await ScopesOfAsync(reads, wanted.Value, cancellationToken)),
            Outcome<IReadOnlyList<GateKind>>.Fail bad => GateAnswer<IReadOnlyList<GateScopeDto>>.Refuse(GateRefusalKind.BadRequest, bad.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    public static async Task<GateAnswer<GateModelTableDto>> ModelsAsync(
        IGateReads reads, string gateWord, string scopeId, string rubric, CancellationToken cancellationToken)
    {
        return Asked(gateWord, scopeId, rubric.Length > 0 ? string.Empty : NoRubricNamed) switch
        {
            GateAnswer<GateKind>.Answered gate => await TableAsync(reads, gate.Value, scopeId, rubric, cancellationToken),
            GateAnswer<GateKind>.Refused refused => GateAnswer<GateModelTableDto>.Refuse(refused.Kind, refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    public static async Task<GateAnswer<IReadOnlyList<GateRunSummaryDto>>> RunsAsync(
        IGateReads reads, string gateWord, string scopeId, CancellationToken cancellationToken)
    {
        return Asked(gateWord, scopeId, string.Empty) switch
        {
            GateAnswer<GateKind>.Answered gate => await RunListAsync(reads, gate.Value, scopeId, cancellationToken),
            GateAnswer<GateKind>.Refused refused => GateAnswer<IReadOnlyList<GateRunSummaryDto>>.Refuse(refused.Kind, refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static async Task<IReadOnlyList<GateScopeDto>> ScopesOfAsync(IGateReads reads, IReadOnlyList<GateKind> gates, CancellationToken cancellationToken)
    {
        var snapshot = await GateSnapshot.ReadAsync(reads, cancellationToken);

        return [.. snapshot.Scopes().Where(s => gates.Contains(s.Gate)).Select(snapshot.ScopeDto)];
    }

    private static async Task<GateAnswer<IReadOnlyList<GateRunSummaryDto>>> RunListAsync(
        IGateReads reads, GateKind gate, string scopeId, CancellationToken cancellationToken)
    {
        var snapshot = await GateSnapshot.ReadAsync(reads, cancellationToken);

        return snapshot.Find(gate, scopeId) switch
        {
            Outcome<GateScope>.Ok found => GateAnswer<IReadOnlyList<GateRunSummaryDto>>.Of(await snapshot.RunListAsync(found.Value, cancellationToken)),
            Outcome<GateScope>.Fail missing => GateAnswer<IReadOnlyList<GateRunSummaryDto>>.Refuse(GateRefusalKind.NotFound, missing.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    public static async Task<GateAnswer<GateRunDetailDto>> RunAsync(IGateReads reads, Guid runId, CancellationToken cancellationToken)
    {
        var snapshot = await GateSnapshot.ReadAsync(reads, cancellationToken);
        var record = snapshot.Records.FirstOrDefault(r => r.RunId == runId);

        return record is null
            ? GateAnswer<GateRunDetailDto>.Refuse(GateRefusalKind.NotFound, $"no gate run {runId} in this database")
            : GateAnswer<GateRunDetailDto>.Of(await snapshot.DetailAsync(record, cancellationToken));
    }

    private static async Task<GateAnswer<GateModelTableDto>> TableAsync(
        IGateReads reads, GateKind gate, string scopeId, string rubric, CancellationToken cancellationToken)
    {
        var snapshot = await GateSnapshot.ReadAsync(reads, cancellationToken);

        return snapshot.Find(gate, scopeId) switch
        {
            Outcome<GateScope>.Ok found => await snapshot.TableAsync(found.Value, rubric, cancellationToken),
            Outcome<GateScope>.Fail missing => GateAnswer<GateModelTableDto>.Refuse(GateRefusalKind.NotFound, missing.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>The scope a CLI's <c>--scope</c> names — its id, or a suite STAMP that spans exactly one scope of the gate. A
    /// stamp spanning several (two products, two settings hashes) is refused listing each, because picking one would report
    /// on a population nobody chose; nothing named lists what there is.</summary>
    public static GateAnswer<GateScopeDto> ResolveScope(IReadOnlyList<GateScopeDto> scopes, string asked)
    {
        var byId = scopes.Where(s => string.Equals(s.Id, asked, StringComparison.OrdinalIgnoreCase)).ToList();
        var byStamp = scopes.Where(s => string.Equals(s.SuiteStamp, asked, StringComparison.Ordinal)).ToList();
        var matches = byId.Count > 0 ? byId : byStamp;

        return (asked.Length, matches.Count) switch
        {
            (0, _) => GateAnswer<GateScopeDto>.Refuse(GateRefusalKind.BadRequest, $"{NoScopeNamed} — {Listing(scopes)}"),
            (_, 1) => GateAnswer<GateScopeDto>.Of(matches[0]),
            (_, 0) => GateAnswer<GateScopeDto>.Refuse(GateRefusalKind.NotFound, $"no scope or suite stamp '{asked}' — {Listing(scopes)}"),
            _ => GateAnswer<GateScopeDto>.Refuse(
                GateRefusalKind.BadRequest, $"suite {asked} spans {matches.Count} scopes — never one table: name one by id — {Listing(matches)}"),
        };
    }

    private static string Listing(IReadOnlyList<GateScopeDto> scopes) =>
        scopes.Count == 0
            ? "this gate has no runs yet"
            : "the scopes are " + string.Join("; ", scopes.Select(s =>
                $"{s.Id} (suite {s.SuiteStamp}, product {s.ProductVersion}, settings {HashText.Short(s.SettingsHash)}, {s.Runs} run(s))"));

    /// <summary>The gate asked for, or the first thing wrong with how the request was ASKED.</summary>
    private static GateAnswer<GateKind> Asked(string gateWord, string scopeId, string rubricRefusal) =>
        (GateWord.Parse(gateWord), scopeId.Length, rubricRefusal.Length) switch
        {
            (Outcome<GateKind>.Fail bad, _, _) => GateAnswer<GateKind>.Refuse(GateRefusalKind.BadRequest, bad.Reason),
            (_, 0, _) => GateAnswer<GateKind>.Refuse(GateRefusalKind.BadRequest, NoScopeNamed),
            (_, _, > 0) => GateAnswer<GateKind>.Refuse(GateRefusalKind.BadRequest, rubricRefusal),
            (Outcome<GateKind>.Ok gate, _, _) => GateAnswer<GateKind>.Of(gate.Value),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
