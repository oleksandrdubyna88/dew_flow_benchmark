using Bench.Domain.Probes;
using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>One model's prices in USD per million tokens — input, output, cache read, and the cache write by its TTL.</summary>
public sealed record ModelPrice(string ModelId, decimal InputPerMtok, decimal OutputPerMtok, decimal CacheReadPerMtok, decimal CacheWrite5mPerMtok, decimal CacheWrite1hPerMtok)
{
    public static Outcome<ModelPrice> Of(string modelId, decimal input, decimal output, decimal cacheRead, decimal cacheWrite5m, decimal cacheWrite1h) =>
        (modelId.Trim().Length > 0, new[] { input, output, cacheRead, cacheWrite5m, cacheWrite1h }.All(p => p >= 0m)) switch
        {
            (false, _) => Outcome<ModelPrice>.Failure("a price names the exact model id it is for"),
            (_, false) => Outcome<ModelPrice>.Failure($"a price for {modelId} is negative"),
            _ => Outcome<ModelPrice>.Success(new ModelPrice(modelId.Trim(), input, output, cacheRead, cacheWrite5m, cacheWrite1h)),
        };

    /// <summary>What <paramref name="usage"/> costs at these prices.</summary>
    public decimal Cost(TokenUsage usage) =>
        (usage.Input * InputPerMtok + usage.Output * OutputPerMtok + usage.CacheRead * CacheReadPerMtok
         + usage.CacheWrite5m * CacheWrite5mPerMtok + usage.CacheWrite1h * CacheWrite1hPerMtok) / 1_000_000m;
}

/// <summary>The vendor price table read on the run date (§3.6): where it was read, when, and a price per exact model id.</summary>
public sealed record PriceTable(string SourceUrl, DateOnly ReadOn, IReadOnlyList<ModelPrice> Models)
{
    public static Outcome<PriceTable> Of(string sourceUrl, DateOnly readOn, IReadOnlyList<ModelPrice> models) =>
        (sourceUrl.StartsWith("https://", StringComparison.Ordinal), models.Count > 0, models.Select(m => m.ModelId).Distinct(StringComparer.Ordinal).Count() == models.Count) switch
        {
            (false, _, _) => Outcome<PriceTable>.Failure($"'{sourceUrl}' is not where a price table was read — an https url"),
            (_, false, _) => Outcome<PriceTable>.Failure("a price table prices at least one model"),
            (_, _, false) => Outcome<PriceTable>.Failure("a price table prices each model id once"),
            _ => Outcome<PriceTable>.Success(new PriceTable(sourceUrl, readOn, [.. models])),
        };

    public Outcome<ModelPrice> For(string modelId) =>
        Models.FirstOrDefault(m => string.Equals(m.ModelId, modelId, StringComparison.Ordinal)) is { } price
            ? Outcome<ModelPrice>.Success(price)
            : Outcome<ModelPrice>.Failure($"the price table has no price for '{modelId}' — it prices {string.Join(", ", Models.Select(m => m.ModelId))}");
}

/// <summary>One scout's tokens. Input and cache tokens come from its own messages and are exact; its OUTPUT tokens are exact only when it
/// is the one scout on its model (<see cref="OutputSeparable"/>) — see <see cref="ScoutTokens"/>.</summary>
public sealed record ScoutUsage(string CallId, string Model, TokenUsage Usage, bool OutputSeparable);

/// <summary>A team's tokens on one model — exact, outputs included.</summary>
public sealed record TeamUsage(string Model, TokenUsage Usage);

/// <summary>A cell's tokens, attributed: the main session, each scout, each scout model's team total, and the models neither the main
/// session nor a scout answered with (the CLI's internal calls — a Haiku line in every 2026-10-12 probe).</summary>
public sealed record CellUsage(string MainModel, TokenUsage Main, IReadOnlyList<ScoutUsage> Scouts, IReadOnlyList<TeamUsage> Teams, IReadOnlyList<TeamUsage> Internal);

/// <summary>Who spent which tokens in one cell.
/// <para>
/// <b>Measured 2026-10-12, and not what the plan assumed:</b> deduplicating a child's messages by id makes its INPUT and CACHE tokens
/// exact (they are known when a message starts, and match <c>modelUsage</c> to the token), but not its OUTPUT tokens: each assistant
/// event carries the usage as of the message's start, so a message that called a tool streams a placeholder (a Sonnet child streamed
/// 16 + 22 and was billed 118; an Opus child streamed 35 and was billed 1,442). The exact sources are the envelope's: <c>modelUsage</c> per
/// model, and <c>usage</c> for the main session alone. So a team's output on model m is <c>modelUsage[m]</c> minus the main session's when
/// the main session is on m too — exact — and one scout's output is exact only when it is the only scout on its model. Two scouts on one
/// model share an output total the stream cannot split; their per-scout cost is then <i>not captured</i>, never guessed.
/// </para></summary>
public static class ScoutTokens
{
    public static Outcome<CellUsage> Attribute(SubagentTranscript transcript)
    {
        var models = transcript.Agents.Select(a => (Call: a, Model: ModelOf(a))).ToList();
        var refusal = Refusal(transcript, models.FirstOrDefault(m => m.Model.Length == 0).Call);

        return refusal.Length > 0 ? Outcome<CellUsage>.Failure(refusal) : Teams(transcript, models);
    }

    private static string Refusal(SubagentTranscript transcript, SubagentCall? unpriceable) =>
        (transcript.Envelope.Captured, transcript.MainModel.Length > 0, unpriceable) switch
        {
            (false, _, _) => "the stream has no result envelope — its tokens cannot be attributed",
            (_, false, _) => "the stream names no main model (init.model)",
            (_, _, { } call) => $"child {call.ToolUseId} answered with no model — it cannot be priced",
            _ => string.Empty,
        };

    private static Outcome<CellUsage> Teams(SubagentTranscript transcript, IReadOnlyList<(SubagentCall Call, string Model)> models)
    {
        var teams = new List<TeamUsage>();
        var scouts = new List<ScoutUsage>();

        foreach (var team in models.GroupBy(m => m.Model, StringComparer.Ordinal))
        {
            var output = TeamOutput(transcript, team.Key);

            if (output is Outcome<long>.Fail fail)
            {
                return Outcome<CellUsage>.Failure(fail.Reason);
            }

            var exact = ((Outcome<long>.Ok)output).Value;
            var members = team.Select(m => m.Call).ToList();
            teams.Add(new TeamUsage(team.Key, members.Aggregate(TokenUsage.Zero, (sum, m) => sum.Plus(m.Child.StreamUsage)) with { Output = exact }));
            scouts.AddRange(Members(team.Key, members, exact));
        }

        var known = models.Select(m => m.Model).Append(transcript.MainModel).ToHashSet(StringComparer.Ordinal);
        var internalCalls = transcript.Envelope.ModelUsage.Where(m => !known.Contains(m.Model)).Select(m => new TeamUsage(m.Model, new TokenUsage(m.Input, m.Output, m.CacheRead, m.CacheCreation, 0))).ToList();

        return Outcome<CellUsage>.Success(new CellUsage(transcript.MainModel, transcript.Envelope.MainUsage, [.. scouts.OrderBy(s => s.CallId, StringComparer.Ordinal)], teams, internalCalls));
    }

    /// <summary>Each scout of one model's team: its own input and cache tokens, and the team's exact output only when it is the team.</summary>
    private static IEnumerable<ScoutUsage> Members(string model, IReadOnlyList<SubagentCall> members, long teamOutput)
    {
        var separable = members.Count == 1;

        return members.Select(m => new ScoutUsage(m.ToolUseId, model, m.Child.StreamUsage with { Output = separable ? teamOutput : 0 }, separable));
    }

    /// <summary>The scouts' exact output on <paramref name="model"/>: its <c>modelUsage</c> line minus the main session's own output when
    /// the main session answered with the same model.</summary>
    private static Outcome<long> TeamOutput(SubagentTranscript transcript, string model)
    {
        var line = transcript.Envelope.ModelUsage.FirstOrDefault(m => string.Equals(m.Model, model, StringComparison.Ordinal));
        var output = (line?.Output ?? 0) - (model == transcript.MainModel ? transcript.Envelope.MainUsage.Output : 0);

        return (line is not null, output >= 0) switch
        {
            (false, _) => Outcome<long>.Failure($"modelUsage has no line for '{model}', which a scout answered with"),
            (_, false) => Outcome<long>.Failure($"modelUsage's output for '{model}' is smaller than the main session's own — the envelope disagrees with itself"),
            _ => Outcome<long>.Success(output),
        };
    }

    private static string ModelOf(SubagentCall call) =>
        call.Result.ResolvedModel.Length > 0 ? call.Result.ResolvedModel : call.Child.MessageModels.FirstOrDefault() ?? string.Empty;
}

/// <summary>One scout's cost — captured only when its output tokens are its own.</summary>
public sealed record ScoutCostLine(string CallId, CapturedUsd Usd);

/// <summary>One scout model's team cost.</summary>
public sealed record TeamCostLine(string Model, decimal Usd);

/// <summary>A cell's cost: the main session, each scout (when its output is separable), each scout model's team, the internal calls.</summary>
public sealed record CellCost(decimal MainUsd, IReadOnlyList<ScoutCostLine> Scouts, IReadOnlyList<TeamCostLine> Teams, decimal InternalUsd)
{
    public decimal ScoutsUsd => Teams.Sum(t => t.Usd);

    public decimal TotalUsd => MainUsd + ScoutsUsd + InternalUsd;
}

/// <summary>Whether the computed cost and the CLI's <c>total_cost_usd</c> agree within the tolerance (§3.6) — a larger gap STOPS the run:
/// the price table or the token sum is wrong, and every cost after it would be too.</summary>
public abstract record CostCheck
{
    private CostCheck()
    {
    }

    public sealed record Agrees(decimal ComputedUsd, decimal ReportedUsd, decimal Gap) : CostCheck;

    public sealed record Disagrees(decimal ComputedUsd, decimal ReportedUsd, decimal Gap) : CostCheck;

    /// <summary>No reported cost to compare with — the stream has no envelope, which the read-back voids on its own.</summary>
    public sealed record Unchecked(string Reason) : CostCheck;
}

/// <summary>The cost calculator (§3.6): attributed tokens × the price table, and the 2 % comparison.</summary>
public static class ScoutCost
{
    /// <summary>The largest relative gap between computed and reported cost that still agrees.</summary>
    public const decimal Tolerance = 0.02m;

    public static Outcome<CellCost> Of(CellUsage usage, PriceTable prices)
    {
        var models = usage.Teams.Concat(usage.Internal).Select(t => t.Model).Append(usage.MainModel).Distinct(StringComparer.Ordinal).ToList();
        var missing = models.Select(prices.For).OfType<Outcome<ModelPrice>.Fail>().FirstOrDefault();

        return missing is not null ? Outcome<CellCost>.Failure(missing.Reason) : Outcome<CellCost>.Success(Priced(usage, prices));
    }

    private static CellCost Priced(CellUsage usage, PriceTable prices)
    {
        decimal Price(string model, TokenUsage tokens) => ((Outcome<ModelPrice>.Ok)prices.For(model)).Value.Cost(tokens);

        return new CellCost(
            Price(usage.MainModel, usage.Main),
            [.. usage.Scouts.Select(s => new ScoutCostLine(s.CallId, s.OutputSeparable ? CapturedUsd.Amount(Price(s.Model, s.Usage)) : CapturedUsd.Unavailable("two scouts share this model's output tokens")))],
            [.. usage.Teams.Select(t => new TeamCostLine(t.Model, Price(t.Model, t.Usage)))],
            usage.Internal.Sum(i => Price(i.Model, i.Usage)));
    }

    public static CostCheck Check(decimal computedUsd, CapturedUsd reported) => Check(computedUsd, reported, Tolerance);

    public static CostCheck Check(decimal computedUsd, CapturedUsd reported, decimal tolerance) =>
        reported.WasCaptured && reported.Value > 0m
            ? Compared(computedUsd, reported.Value, Math.Abs(computedUsd - reported.Value) / reported.Value, tolerance)
            : new CostCheck.Unchecked(reported.WasCaptured ? "the CLI reported no cost" : reported.Reason);

    private static CostCheck Compared(decimal computedUsd, decimal reportedUsd, decimal gap, decimal tolerance) =>
        gap <= tolerance ? new CostCheck.Agrees(computedUsd, reportedUsd, gap) : new CostCheck.Disagrees(computedUsd, reportedUsd, gap);
}
