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
        var snapshot = await GateSnapshot.ReadAsync(reads, gates, cancellationToken);

        return [.. snapshot.Scopes().Select(snapshot.ScopeDto)];
    }

    private static async Task<GateAnswer<IReadOnlyList<GateRunSummaryDto>>> RunListAsync(
        IGateReads reads, GateKind gate, string scopeId, CancellationToken cancellationToken)
    {
        var snapshot = await GateSnapshot.ReadAsync(reads, [gate], cancellationToken);

        return snapshot.Find(gate, scopeId) switch
        {
            Outcome<GateScope>.Ok found => GateAnswer<IReadOnlyList<GateRunSummaryDto>>.Of(await snapshot.RunListAsync(found.Value, cancellationToken)),
            Outcome<GateScope>.Fail missing => GateAnswer<IReadOnlyList<GateRunSummaryDto>>.Refuse(GateRefusalKind.NotFound, missing.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>One run — read through ITS gate's history only (its scope's runs and verdicts are what its detail shows).</summary>
    public static async Task<GateAnswer<GateRunDetailDto>> RunAsync(IGateReads reads, Guid runId, CancellationToken cancellationToken) =>
        await reads.GateOfRunAsync(runId, cancellationToken) switch
        {
            Outcome<GateKind>.Ok gate => await DetailAsync(await GateSnapshot.ReadAsync(reads, [gate.Value], cancellationToken), runId, cancellationToken),
            Outcome<GateKind>.Fail missing => GateAnswer<GateRunDetailDto>.Refuse(GateRefusalKind.NotFound, missing.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>The CLI's ask — a scope id or a suite stamp, and a rubric — answered from ONE read of the gate: the scope is
    /// resolved and the table computed over the same snapshot (code round: resolving first and then asking for the table read
    /// the gate's whole history twice).</summary>
    public static Task<GateAnswer<GateModelTableDto>> ReportAsync(
        IGateReads reads, string gateWord, string scopeAsked, string rubric, CancellationToken cancellationToken) =>
        ReportAsync(reads, gateWord, scopeAsked, rubric, [], cancellationToken);

    /// <summary>The same, narrowed to <paramref name="campaigns"/> when any are named — S7.3's code scope held a campaign a
    /// harness defect had voided beside the real one, and only a person can say which of a scope's runs measure the models.
    /// A campaign that is not one of the gate's is refused by name, never read as an empty table.</summary>
    public static async Task<GateAnswer<GateModelTableDto>> ReportAsync(
        IGateReads reads, string gateWord, string scopeAsked, string rubric, IReadOnlyList<Guid> campaigns, CancellationToken cancellationToken) =>
        GateWord.Parse(gateWord) switch
        {
            Outcome<GateKind>.Ok gate => Narrowed(await GateSnapshot.ReadAsync(reads, [gate.Value], cancellationToken), gate.Value, campaigns) switch
            {
                Outcome<GateSnapshot>.Ok snapshot => await ReportOfAsync(snapshot.Value, gate.Value, scopeAsked, rubric, cancellationToken),
                Outcome<GateSnapshot>.Fail unknown => GateAnswer<GateModelTableDto>.Refuse(GateRefusalKind.NotFound, unknown.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            },
            Outcome<GateKind>.Fail bad => GateAnswer<GateModelTableDto>.Refuse(GateRefusalKind.BadRequest, bad.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static Outcome<GateSnapshot> Narrowed(GateSnapshot snapshot, GateKind gate, IReadOnlyList<Guid> campaigns)
    {
        var unknown = campaigns.Where(c => !snapshot.Records.Any(r => r.CampaignId == c)).Select(c => c.ToString("D")).FirstOrDefault() ?? string.Empty;

        return (campaigns.Count, unknown.Length) switch
        {
            (0, _) => Outcome<GateSnapshot>.Success(snapshot),
            (_, > 0) => Outcome<GateSnapshot>.Failure($"{unknown} is not a {GateWord.Of(gate)}-gate run in this database"),
            _ => Outcome<GateSnapshot>.Success(snapshot.Only(campaigns)),
        };
    }

    private static async Task<GateAnswer<GateModelTableDto>> ReportOfAsync(
        GateSnapshot snapshot, GateKind gate, string scopeAsked, string rubric, CancellationToken cancellationToken) =>
        (ResolveScope([.. snapshot.Scopes().Select(snapshot.ScopeDto)], scopeAsked), rubric.Length) switch
        {
            (GateAnswer<GateScopeDto>.Refused refused, _) => GateAnswer<GateModelTableDto>.Refuse(refused.Kind, refused.Reason),
            (GateAnswer<GateScopeDto>.Answered scope, 0) =>
                GateAnswer<GateModelTableDto>.Refuse(GateRefusalKind.BadRequest, $"{NoRubricNamed} — {Carried(scope.Value)}"),
            (GateAnswer<GateScopeDto>.Answered scope, _) => await snapshot.TableAsync(
                snapshot.Find(gate, scope.Value.Id).Match(s => s, reason => throw new InvalidOperationException(reason)), rubric, cancellationToken),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static async Task<GateAnswer<GateRunDetailDto>> DetailAsync(GateSnapshot snapshot, Guid runId, CancellationToken cancellationToken) =>
        snapshot.Records.FirstOrDefault(r => r.RunId == runId) is { } record
            ? GateAnswer<GateRunDetailDto>.Of(await snapshot.DetailAsync(record, cancellationToken))
            : GateAnswer<GateRunDetailDto>.Refuse(GateRefusalKind.NotFound, $"no gate run {runId} in this database");

    private static string Carried(GateScopeDto scope) =>
        scope.Rubrics.Count == 0
            ? "nothing in this scope was assessed yet (bench gate assess)"
            : "this scope's verdicts carry " + string.Join(", ", scope.Rubrics.Select(r => $"{r.Stamp} ({r.Kind}, {r.Verdicts} verdict(s))"));

    private static async Task<GateAnswer<GateModelTableDto>> TableAsync(
        IGateReads reads, GateKind gate, string scopeId, string rubric, CancellationToken cancellationToken)
    {
        var snapshot = await GateSnapshot.ReadAsync(reads, [gate], cancellationToken);

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
