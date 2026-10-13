using System.Text.Json;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>Everything one cell's reading needs besides its stream: what it was launched to do (the read-back expectation), the
/// run's frozen pins, the price table, the task's G and the tree's file list, and the CLI's stderr (a quota marker arrives there).</summary>
public sealed record ScoutCellContext(
    ReadBackExpectation Expectation, ScoutPins Pins, PriceTable Prices, IReadOnlyList<string> GroundTruth, IReadOnlyList<string> TreeFiles, string Stderr);

/// <summary>How one cell reads: the settlement it stores, the per-scout records, and what the CLI said it cost — the money the ledger
/// settles the attempt with, whatever the cell's outcome.</summary>
public sealed record ScoutCellVerdict(ScoutSettlement Settlement, IReadOnlyList<ScoutRecord> Scouts, CapturedUsd ReportedUsd, ReadBackVerdict ReadBack, CostCheck Cost);

/// <summary>A cell's stream read into a settlement — pure, so every rule below is a table test with no process behind it:
/// <list type="number">
/// <item><b>Stops</b> outrank everything: the path audit's (§3.2), then a moved pin (§3.4, <see cref="ScoutPinDrift"/>) — a safeguard
/// fallback in the main session or any child (<see cref="ScoutReason.ModelRefusalFallback"/>, E2 S8), else a main session or child that
/// answered with a resolved id other than the one frozen on the run (<see cref="ScoutReason.PinMoved"/>).</item>
/// <item><b>aborted:spend</b> next (§3.10): a quota marker on stderr or in the result, the CLI's own budget cap reached, or a subagent
/// refused for budget — never scored.</item>
/// <item><b>The cost check</b> (§3.6): computed main + scouts + internal cost against <c>total_cost_usd</c> within 2 %; a larger gap — or a
/// stream whose tokens cannot be priced at all — STOPS (<see cref="ScoutReason.CostMismatch"/>). No envelope, no check: the read-back
/// has already voided that stream as incomplete.</item>
/// <item><b>Voids</b> (§3.5), else <b>scored</b> — and only a scored cell carries recall.</item>
/// </list></summary>
public static class ScoutCellReading
{
    public static ScoutCellVerdict Read(SubagentTranscript transcript, string stdout, ScoutCellContext context, LinkTargets links)
    {
        var readBack = ScoutReadBack.Check(transcript, context.Expectation, links);
        var cost = CostOf(transcript, context.Prices);
        var (outcome, reason) = Decide(transcript, readBack, cost, Aborted(transcript, stdout, context.Stderr), context.Pins);
        var scouts = Records(transcript, context);

        return new ScoutCellVerdict(
            new ScoutSettlement(outcome, reason, Facts(transcript, outcome, cost, scouts, context)),
            scouts,
            transcript.Envelope.TotalCostUsd,
            readBack,
            cost);
    }

    private static (ScoutCellOutcome, ScoutReason) Decide(SubagentTranscript t, ReadBackVerdict readBack, CostCheck cost, bool aborted, ScoutPins pins) =>
        (readBack.Stops.Count > 0, Drift(t, pins), aborted, cost is CostCheck.Disagrees) switch
        {
            (true, _, _, _) => (ScoutCellOutcome.Stopped, readBack.Stops[0]),
            (_, var moved and not ScoutReason.None, _, _) => (ScoutCellOutcome.Stopped, moved),
            (_, _, true, _) => (ScoutCellOutcome.Aborted, ScoutReason.SpendLimit),
            (_, _, _, true) => (ScoutCellOutcome.Stopped, ScoutReason.CostMismatch),
            _ => (readBack.Outcome, readBack.Reason),
        };

    /// <summary>A moved pin, and which: a safeguard fallback names its own event (S8) — it is the more specific word, and a fallback model
    /// often answers under a pinned-looking init, so it is checked first — else a moved alias.</summary>
    private static ScoutReason Drift(SubagentTranscript t, ScoutPins pins) =>
        (ScoutPinDrift.FellBack(t), ScoutPinDrift.Moved(t, pins)) switch
        {
            (true, _) => ScoutReason.ModelRefusalFallback,
            (_, true) => ScoutReason.PinMoved,
            _ => ScoutReason.None,
        };

    /// <summary>The cost check; a stream with an envelope whose tokens cannot be attributed or priced DISAGREES — the price table or the
    /// token sum is wrong, and every cost after it would be too.</summary>
    public static CostCheck CostOf(SubagentTranscript transcript, PriceTable prices)
    {
        if (!transcript.Envelope.Captured)
        {
            return new CostCheck.Unchecked("no result envelope");
        }

        var computed = ScoutTokens.Attribute(transcript).Match(usage => ScoutCost.Of(usage, prices), Outcome<CellCost>.Failure);

        return computed is Outcome<CellCost>.Ok ok
            ? ScoutCost.Check(ok.Value.TotalUsd, transcript.Envelope.TotalCostUsd)
            : new CostCheck.Disagrees(0m, transcript.Envelope.TotalCostUsd.WasCaptured ? transcript.Envelope.TotalCostUsd.Value : 0m, 1m);
    }

    /// <summary>A spend limit (§3.10): the account's quota (stderr, or the result's own text), the CLI's <c>--max-budget-usd</c> reached
    /// (a <c>result</c> subtype naming the budget), or a subagent the CLI refused for budget.</summary>
    public static bool Aborted(SubagentTranscript transcript, string stdout, string stderr) =>
        ReviewerAccountOut.CliReason(stderr + "\n" + ResultText(stdout)).Length > 0
        || transcript.Envelope.Subtype.Contains("budget", StringComparison.OrdinalIgnoreCase)
        || transcript.Envelope.Stats.RefusedBudget > 0;

    /// <summary>The final <c>result</c> event's own <c>result</c> text — the CLI's voice, where an account-out sentence lands.</summary>
    public static string ResultText(string stdout)
    {
        var documents = ProbeJson.Lines(stdout);

        try
        {
            return documents.Select(d => d.RootElement).LastOrDefault(e => ProbeJson.Text(e, "type") == "result") is { ValueKind: JsonValueKind.Object } result
                ? ProbeJson.Text(result, "result")
                : string.Empty;
        }
        finally
        {
            ProbeJson.Dispose(documents);
        }
    }

    private static ScoutCellFacts Facts(SubagentTranscript t, ScoutCellOutcome outcome, CostCheck cost, IReadOnlyList<ScoutRecord> scouts, ScoutCellContext context)
    {
        var recall = RecallMatcher.Match([.. scouts.SelectMany(s => s.KeyFiles)], context.GroundTruth, context.TreeFiles, context.Expectation.TreeRoot);
        var scored = outcome == ScoutCellOutcome.Scored;
        var team = Team(t, context.Prices);

        return new ScoutCellFacts(
            scored ? CapturedCount.Number(recall.Found.Count) : CapturedCount.Unavailable($"a {outcome} cell has no recall"),
            scored ? CapturedCount.Number(recall.Total) : CapturedCount.Unavailable($"a {outcome} cell has no recall"),
            t.TeamMs,
            t.Envelope.DurationMs,
            cost switch
            {
                CostCheck.Agrees a => CapturedUsd.Amount(a.ComputedUsd),
                CostCheck.Disagrees { ComputedUsd: > 0m } d => CapturedUsd.Amount(d.ComputedUsd),
                _ => CapturedUsd.Unavailable("the cost could not be computed"),
            },
            t.Envelope.TotalCostUsd)
        {
            TeamTokens = team.Tokens,
            TeamUsd = team.Usd,
        };
    }

    /// <summary>One record per Agent call whose prompt is one of the frozen prompts — its ordinal and focus are that prompt's place in the
    /// arm (F1..F5). A call with another prompt has no focus to record; the read-back has voided its cell.</summary>
    private static IReadOnlyList<ScoutRecord> Records(SubagentTranscript t, ScoutCellContext context)
    {
        var usage = ScoutTokens.Attribute(t).Match(u => u.Scouts, _ => []);
        var costs = ScoutTokens.Attribute(t).Match(u => ScoutCost.Of(u, context.Prices).Match(c => c.Scouts, _ => []), _ => []);

        return [.. Reports(t, context.Expectation.PromptSha256s).Select(r => Record(r, usage, costs, context))];
    }

    /// <summary>Each scout's REPORT, by its ordinal: the Agent calls whose prompt is one of the frozen prompts, matched to that prompt's place
    /// in the arm (a prompt matched once), in ordinal order — what the records are built from at settlement, and what <c>score</c>,
    /// <c>assess</c> and the hand-check re-read from the stream later, so all three read the same scout as the same ordinal.</summary>
    public static IReadOnlyList<ScoutCallReport> Reports(SubagentTranscript t, IReadOnlyList<string> promptSha256s)
    {
        var taken = new HashSet<int>();
        var reports = new List<ScoutCallReport>();

        foreach (var call in t.Agents)
        {
            var ordinal = Enumerable.Range(0, promptSha256s.Count).FirstOrDefault(i => promptSha256s[i] == call.PromptSha256 && !taken.Contains(i), -1);

            if (ordinal >= 0 && taken.Add(ordinal))
            {
                reports.Add(new ScoutCallReport(ordinal + 1, call, call.Result.FinalText.Length > 0 ? call.Result.FinalText : call.Child.LastText));
            }
        }

        return [.. reports.OrderBy(r => r.Ordinal)];
    }

    private static ScoutRecord Record(ScoutCallReport report, IReadOnlyList<ScoutUsage> usage, IReadOnlyList<ScoutCostLine> costs, ScoutCellContext context)
    {
        var (call, ordinal) = (report.Call, report.Ordinal);
        var keyFiles = ScoutOutput.KeyFiles(report.Text);
        var claims = ScoutOutput.Claims(report.Text);
        var mine = usage.Where(u => u.CallId == call.ToolUseId).ToList();

        return new ScoutRecord(
            ordinal,
            (ScoutFocus)ordinal,
            call.InputModel,
            call.PromptSha256,
            call.Result.ResolvedModel,
            call.Result.TotalDurationMs,
            mine.Count > 0 ? mine[0].Usage : call.Child.StreamUsage,
            mine.Count > 0 && mine[0].OutputSeparable,
            costs.Where(c => c.CallId == call.ToolUseId).Select(c => c.Usd).DefaultIfEmpty(CapturedUsd.Unavailable("the cell's tokens could not be priced")).First(),
            KeyFiles(keyFiles.Paths, context.TreeFiles, context.Expectation.TreeRoot),
            keyFiles.Unparseable,
            keyFiles.Extra,
            claims.Silent,
            claims.Claims.Count,
            claims.Unparseable,
            claims.Extra);
    }

    /// <summary>KEY FILES as the matcher resolves them in the tree; an entry that is not repository-relative is not stored.</summary>
    public static IReadOnlyList<string> KeyFiles(IReadOnlyList<string> paths, IReadOnlyList<string> treeFiles, string treeRoot)
    {
        var tree = treeFiles.Select(RecallMatcher.Normalise).ToList();

        return [.. paths.Select(p => RecallMatcher.Normalise(p, treeRoot)).Where(p => p.Length > 0)
            .SelectMany(p => RecallMatcher.Resolve(p, tree))
            .Where(p => RepositoryRelative.Refusal(p).Length == 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The scout TEAM's tokens and cost (§3.6): the attributed teams' tokens summed, priced — the main session and the internal
    /// calls excluded. Not captured when the stream's tokens cannot be attributed or priced.</summary>
    private static (CapturedTokens Tokens, CapturedUsd Usd) Team(SubagentTranscript t, PriceTable prices) =>
        ScoutTokens.Attribute(t) is Outcome<CellUsage>.Ok { Value: var usage }
            ? (CapturedTokens.Of(usage.Teams.Aggregate(TokenUsage.Zero, (sum, team) => sum.Plus(team.Usage))),
               ScoutCost.Of(usage, prices).Match(cost => CapturedUsd.Amount(cost.ScoutsUsd), reason => CapturedUsd.Unavailable(reason)))
            : (CapturedTokens.NotCaptured, CapturedUsd.Unavailable("the stream's tokens could not be attributed"));
}

/// <summary>One scout's report as the stream carries it: its ordinal (its focus area), the Agent call, and the text its blocks are read from.</summary>
public sealed record ScoutCallReport(int Ordinal, SubagentCall Call, string Text);

/// <summary>A moved alias, read off a cell's own stream (§3.4: a change of any pin mid-run stops the run). The resolved ids cannot be read
/// before a claim without a paid call, so they are checked on every cell AFTER it ran: the main session's <c>init.model</c> must be the run's
/// resolved Opus id, and each Agent result's <c>resolvedModel</c> the run's resolved id for the alias that call named. Since E2 S8 a
/// <c>model_refusal_fallback</c> event anywhere in the stream is a moved pin of its own (<see cref="FellBack"/>): the CLI keeps the session
/// going on another model and <c>init.model</c> still names the original.</summary>
public static class ScoutPinDrift
{
    public static bool Moved(SubagentTranscript transcript, ScoutPins pins) =>
        transcript.MainModel.Length > 0 && !string.Equals(transcript.MainModel, pins.ResolvedOpus, StringComparison.Ordinal)
        || transcript.Agents.Any(call => ChildMoved(call, pins));

    /// <summary>E2 S8: the main session or any child continued on another model after a safeguard refusal (a <c>model_refusal_fallback</c>
    /// event). <c>init.model</c> and a child's <c>resolvedModel</c> can still name the pinned ids, so this is read on its own — the cell was not
    /// measured on the models the run froze, whichever side fell back.</summary>
    public static bool FellBack(SubagentTranscript transcript) => transcript.Fallbacks.Count > 0;

    private static bool ChildMoved(SubagentCall call, ScoutPins pins) =>
        call.Result.ResolvedModel.Length > 0
        && Expected(call.InputModel, pins) is { Length: > 0 } expected
        && !string.Equals(call.Result.ResolvedModel, expected, StringComparison.Ordinal);

    private static string Expected(string alias, ScoutPins pins) => alias switch
    {
        "opus" => pins.ResolvedOpus,
        "sonnet" => pins.ResolvedSonnet,
        _ => string.Empty,
    };
}
