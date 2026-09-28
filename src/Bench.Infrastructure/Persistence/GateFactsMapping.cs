using Bench.Domain.Gate;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Persistence;

/// <summary>A settled session's facts ↔ the cell's fact columns. <i>Not captured</i> is a flag beside every count,
/// never a zero: a zero stored for "nobody counted" reads back as "none", which is how a gap in the ledger becomes
/// a claim about a reviewer.</summary>
internal static class GateFactsMapping
{
    /// <summary>Writes a settlement's facts onto a TRACKED row. The failure sentence is the one text written.</summary>
    public static void Apply(GateCellRow row, GateSettlement settlement)
    {
        var (facts, settingsHash, promptHash) = settlement switch
        {
            GateSettlement.Completed completed => (completed.Facts, completed.SettingsHash, completed.PromptHash),
            GateSettlement.Failed failed => (GateRunFacts.NotProduced(failed.Cause), string.Empty, string.Empty),
            _ => throw new InvalidOperationException("unreachable"),
        };

        row.OutcomeKind = settlement.Kind;
        row.FactsRecorded = true;
        row.SettingsHash = settingsHash;
        row.PromptHash = promptHash;
        row.ServerVersion = settlement.Notes.ServerVersion;
        row.ReferencesHash = settlement.Notes.ReferencesHash;
        row.SettingsChecked = settlement.Notes.SettingsChecked;
        row.SettingsMismatches = settlement.Notes.SettingsMismatches;
        ApplyReply(row, facts);
        ApplyTokens(row, facts);
        ApplyTimes(row, facts);
    }

    public static GateRunFacts Facts(GateCellRow row) => new(
        row.Valid,
        row.Verdict,
        row.ReplyParsed,
        Count(row.FindingsCaptured, row.Findings),
        row.Turns,
        row.HttpCalls,
        [.. row.FinishReasons],
        [.. row.Statuses],
        Count(row.TokensInCaptured, row.TokensIn),
        Count(row.TokensOutCaptured, row.TokensOut),
        Count(row.TokensCachedCaptured, row.TokensCached),
        Count(row.TokensReasoningCaptured, row.TokensReasoning),
        row.SecondsTotal,
        row.ReviewSeconds,
        [.. row.SecondsPerTurn],
        [.. row.CachedPerCall.Select((value, i) => Count(i < row.CachedPerCallCaptured.Count && row.CachedPerCallCaptured[i], value))],
        row.CostCaptured ? CapturedUsd.Amount(row.CostUsd) : CapturedUsd.Unavailable(GateRowMapping.StoredNotCaptured),
        row.Served,
        row.Refused,
        new FailureCause(row.FailureKind, row.FailureText))
    {
        TurnFactsCaptured = row.TurnFactsCaptured,
    };

    private static void ApplyReply(GateCellRow row, GateRunFacts facts)
    {
        row.Valid = facts.Valid;
        row.Verdict = facts.Verdict;
        row.ReplyParsed = facts.ReplyParsed;
        row.FindingsCaptured = facts.Findings.WasCaptured;
        row.Findings = facts.Findings.Value;
        row.Turns = facts.Turns;
        row.HttpCalls = facts.HttpCalls;
        row.TurnFactsCaptured = facts.TurnFactsCaptured;
        row.FinishReasons = [.. facts.FinishReasons];
        row.Statuses = [.. facts.Statuses];
        row.Served = facts.Served;
        row.Refused = facts.Refused;
        row.FailureKind = facts.Failure.Kind;
        row.FailureText = facts.Failure.Text;
    }

    private static void ApplyTokens(GateCellRow row, GateRunFacts facts)
    {
        (row.TokensInCaptured, row.TokensIn) = (facts.TokensIn.WasCaptured, facts.TokensIn.Value);
        (row.TokensOutCaptured, row.TokensOut) = (facts.TokensOut.WasCaptured, facts.TokensOut.Value);
        (row.TokensCachedCaptured, row.TokensCached) = (facts.TokensCached.WasCaptured, facts.TokensCached.Value);
        (row.TokensReasoningCaptured, row.TokensReasoning) = (facts.TokensReasoning.WasCaptured, facts.TokensReasoning.Value);
        (row.CostCaptured, row.CostUsd) = (facts.CostUsd.WasCaptured, facts.CostUsd.Value);
        row.CachedPerCallCaptured = [.. facts.CachedPerCall.Select(c => c.WasCaptured)];
        row.CachedPerCall = [.. facts.CachedPerCall.Select(c => c.Value)];
    }

    private static void ApplyTimes(GateCellRow row, GateRunFacts facts)
    {
        row.SecondsTotal = facts.SecondsTotal;
        row.ReviewSeconds = facts.ReviewSeconds;
        row.SecondsPerTurn = [.. facts.SecondsPerTurn];
    }

    private static CapturedCount Count(bool captured, long value) =>
        captured ? CapturedCount.Number(value) : CapturedCount.Unavailable(GateRowMapping.StoredNotCaptured);
}
