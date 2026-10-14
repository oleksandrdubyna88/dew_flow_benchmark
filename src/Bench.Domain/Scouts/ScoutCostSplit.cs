using System.Globalization;

namespace Bench.Domain.Scouts;

/// <summary>One kind of token in a team's cost: how many, and what they cost at the published price.</summary>
public sealed record ScoutCostPart(string Kind, double Tokens, decimal Usd);

/// <summary>A team's tokens split by kind with their dollars (the operator, 2026-10-14: "write input and output tokens, and their prices,
/// separately") — so a reader sees WHERE a team's money goes: the pilot's Sonnet teams spent most of theirs writing what they read into the
/// cache, not on output. Dollars at the published price, rounded to the micro-dollar.</summary>
public static class ScoutCostSplit
{
    public static IReadOnlyList<ScoutCostPart> Of(double input, double output, double cacheRead, double cacheWrite5m, double cacheWrite1h, ModelPrice price) =>
    [
        Part("input", input, price.InputPerMtok),
        Part("output", output, price.OutputPerMtok),
        Part("cache read", cacheRead, price.CacheReadPerMtok),
        Part("cache write 5m", cacheWrite5m, price.CacheWrite5mPerMtok),
        Part("cache write 1h", cacheWrite1h, price.CacheWrite1hPerMtok),
    ];

    /// <summary>The split as one report line: <c>kind N tok $x</c>, joined by a middle dot.</summary>
    public static string Line(IReadOnlyList<ScoutCostPart> parts) =>
        string.Join(" · ", parts.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Kind} {p.Tokens:0.##} tok ${p.Usd:0.######}")));

    private static ScoutCostPart Part(string kind, double tokens, decimal perMtok) =>
        new(kind, tokens, decimal.Round((decimal)tokens * perMtok / 1_000_000m, 6, MidpointRounding.AwayFromZero));
}
