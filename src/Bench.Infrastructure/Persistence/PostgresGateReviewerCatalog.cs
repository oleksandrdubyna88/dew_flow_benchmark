using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The reviewer catalog over <c>gate_reviewers</c> — the variant catalog's shape: a row is added and retired,
/// never edited, and read back through the same parsers that admitted it, so a hand-edited row is refused by name
/// instead of served.</summary>
public sealed class PostgresGateReviewerCatalog(BenchDbContext db) : IGateReviewerCatalog
{
    public async Task<Outcome<GateReviewer>> AddAsync(GateReviewer reviewer, CancellationToken cancellationToken)
    {
        if (await db.GateReviewers.AnyAsync(r => r.Id == reviewer.Id.Value, cancellationToken))
        {
            return Outcome<GateReviewer>.Failure(
                $"reviewer '{reviewer.Id}' already exists — a catalog row is never edited; retire it and add the new configuration under a new id");
        }

        db.GateReviewers.Add(ToRow(reviewer));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return Outcome<GateReviewer>.Success(reviewer);
    }

    public async Task<IReadOnlyList<GateReviewer>> ListAsync(bool includeRetired, CancellationToken cancellationToken)
    {
        var rows = await db.GateReviewers.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);

        return [.. rows.Select(ToDomain).OfType<Outcome<GateReviewer>.Ok>().Select(o => o.Value).Where(r => includeRetired || r.IsActive)];
    }

    public async Task<Outcome<IReadOnlyList<GateReviewer>>> GetAsync(IReadOnlyList<GateReviewerId> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Select(i => i.Value).ToList();
        var rows = await db.GateReviewers.AsNoTracking().Where(r => wanted.Contains(r.Id)).ToListAsync(cancellationToken);
        var missing = wanted.Where(w => rows.All(r => r.Id != w)).ToList();

        if (missing.Count > 0)
        {
            return Outcome<IReadOnlyList<GateReviewer>>.Failure(
                $"no reviewer called {string.Join(", ", missing)} is in the catalog — an id is not a vendor; add it with `bench gate reviewers add`");
        }

        var parsed = wanted.Select(w => ToDomain(rows.Single(r => r.Id == w))).ToList();
        var bad = parsed.OfType<Outcome<GateReviewer>.Fail>().FirstOrDefault();

        return bad is not null
            ? Outcome<IReadOnlyList<GateReviewer>>.Failure(bad.Reason)
            : Outcome<IReadOnlyList<GateReviewer>>.Success([.. parsed.OfType<Outcome<GateReviewer>.Ok>().Select(o => o.Value)]);
    }

    public async Task<Outcome<GateReviewer>> RetireAsync(GateReviewerId id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await db.GateReviewers.SingleOrDefaultAsync(r => r.Id == id.Value, cancellationToken);

        if (row is null)
        {
            return Outcome<GateReviewer>.Failure($"no reviewer called {id} is in the catalog");
        }

        var retired = ToDomain(row).Match(r => r.Retire(now), Outcome<GateReviewer>.Failure);

        if (retired is Outcome<GateReviewer>.Ok ok)
        {
            row.RetiredAt = ok.Value.RetiredAt;
            await db.SaveChangesAsync(cancellationToken);
        }

        db.ChangeTracker.Clear();
        return retired;
    }

    internal static GateReviewerRow ToRow(GateReviewer reviewer)
    {
        var d = reviewer.Definition;
        var t = d.Transport;
        var p = d.Prices;

        return new GateReviewerRow
        {
            Id = reviewer.Id.Value,
            Hash = reviewer.Hash,
            Runtime = d.Runtime,
            Model = d.Model,
            EndpointUrl = d.Endpoint is ReviewerEndpoint.Value v ? v.Url : string.Empty,
            EndpointRef = d.Endpoint is ReviewerEndpoint.Reference r ? r.Name : string.Empty,
            KeyName = d.KeyName,
            CredsKeyRef = d.CredsKeyRef,
            ExecutableRef = d.ExecutableRef,
            RemoteVendor = d.RemoteVendor,
            Dialect = t.Dialect,
            ReasoningEffort = t.ReasoningEffort,
            MaxTokens = t.MaxTokens,
            TimeoutMinutes = t.TimeoutMinutes,
            FollowUps = t.FollowUps,
            ReviewMinutesCap = t.ReviewMinutesCap,
            Thinking = t.Thinking switch { ThinkingSetting.On => true, ThinkingSetting.Off => false, _ => null },
            PricesKnown = p.Known,
            InPerMTok = p.InPerMTok,
            CachedPerMTok = p.CachedPerMTok,
            OutPerMTok = p.OutPerMTok,
            TierFromTokens = p.TierFromTokens,
            TierIn = p.TierIn,
            TierCached = p.TierCached,
            TierOut = p.TierOut,
            GatePlan = d.Gates.Plan,
            GateCode = d.Gates.Code,
            GateFeature = d.Gates.Feature,
            AddedAt = reviewer.AddedAt,
            RetiredAt = reviewer.RetiredAt,
        };
    }

    /// <summary>A row back through the parsers — and refused when its stored hash no longer matches the definition it
    /// spells, because a row edited in place has relabelled every run measured against it.</summary>
    internal static Outcome<GateReviewer> ToDomain(GateReviewerRow row)
    {
        var definition = Definition(row);

        if (definition is Outcome<ReviewerDefinition>.Fail fail)
        {
            return Outcome<GateReviewer>.Failure($"reviewer row '{row.Id}' does not read back — {fail.Reason}");
        }

        var parsed = ((Outcome<ReviewerDefinition>.Ok)definition).Value;

        return parsed.Hash == row.Hash
            ? GateReviewer.Rehydrate(row.Id, parsed, row.AddedAt, row.RetiredAt)
            : Outcome<GateReviewer>.Failure($"reviewer row '{row.Id}' was edited in place — its stored hash no longer matches its definition");
    }

    private static Outcome<ReviewerDefinition> Definition(GateReviewerRow row)
    {
        var endpoint = ReviewerEndpoint.Parse(row.EndpointUrl.Length > 0 ? row.EndpointUrl : row.EndpointRef);
        var thinking = row.Thinking switch { true => ThinkingSetting.On, false => ThinkingSetting.Off, null => ThinkingSetting.VendorDefault };
        var transport = ReviewerTransport.Parse(row.Dialect, row.ReasoningEffort, row.MaxTokens, row.TimeoutMinutes, row.FollowUps, row.ReviewMinutesCap, thinking);
        var prices = row.PricesKnown
            ? ReviewerPrices.Of(row.InPerMTok, row.CachedPerMTok, row.OutPerMTok, row.TierFromTokens, row.TierIn, row.TierCached, row.TierOut)
            : Outcome<ReviewerPrices>.Success(ReviewerPrices.Unknown);
        var gates = HostedGates.Of(Gates(row));

        return (endpoint, transport, prices, gates) switch
        {
            (Outcome<ReviewerEndpoint>.Fail f, _, _, _) => Outcome<ReviewerDefinition>.Failure(f.Reason),
            (_, Outcome<ReviewerTransport>.Fail f, _, _) => Outcome<ReviewerDefinition>.Failure(f.Reason),
            (_, _, Outcome<ReviewerPrices>.Fail f, _) => Outcome<ReviewerDefinition>.Failure(f.Reason),
            (_, _, _, Outcome<HostedGates>.Fail f) => Outcome<ReviewerDefinition>.Failure(f.Reason),
            (Outcome<ReviewerEndpoint>.Ok e, Outcome<ReviewerTransport>.Ok t, Outcome<ReviewerPrices>.Ok p, Outcome<HostedGates>.Ok g) =>
                ReviewerDefinition.Parse(row.Runtime, row.Model, e.Value, row.KeyName, row.CredsKeyRef, row.ExecutableRef, row.RemoteVendor, t.Value, p.Value, g.Value),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static IReadOnlyList<GateKind> Gates(GateReviewerRow row) =>
        [.. new[] { (row.GatePlan, GateKind.Plan), (row.GateCode, GateKind.Code), (row.GateFeature, GateKind.Feature) }.Where(g => g.Item1).Select(g => g.Item2)];
}
