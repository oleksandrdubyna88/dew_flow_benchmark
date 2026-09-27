using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>The product's verdict words, as an enum so a row stores a NAME the page can group by. A word this
/// build does not know is <see cref="Unknown"/> — a state, counted apart, never silently one of the others.</summary>
public enum GateVerdictWord
{
    Proceed,
    Revise,
    GoodEnough,
    ContinueAnyway,
    CallHuman,
    Escalated,
    Unknown,
}

public static class GateVerdictWords
{
    public static GateVerdictWord Parse(string? word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "proceed" => GateVerdictWord.Proceed,
        "revise" => GateVerdictWord.Revise,
        "good_enough" => GateVerdictWord.GoodEnough,
        "continue_anyway" => GateVerdictWord.ContinueAnyway,
        "call_human" => GateVerdictWord.CallHuman,
        "escalated" => GateVerdictWord.Escalated,
        _ => GateVerdictWord.Unknown,
    };

    /// <summary>The two verdicts a run may be VALID under — the other harness's rule, kept verbatim: a
    /// <c>call_human</c> is the product stopping, not the reviewer answering.</summary>
    public static bool IsPassing(GateVerdictWord word) => word is GateVerdictWord.Proceed or GateVerdictWord.Revise;
}

/// <summary>What the tool answered, once parsed — or the fact that it could not be.</summary>
public abstract record GateReply
{
    private GateReply()
    {
    }

    /// <param name="Findings"><i>Not captured</i> when the reply carried no findings LIST — a reply whose
    /// findings field is missing or malformed is a different fact from one that found nothing.</param>
    public sealed record Answered(GateVerdictWord Verdict, CapturedCount Findings) : GateReply;

    /// <summary>The tool's text was not JSON. The size is kept; the text goes to the artefact store.</summary>
    public sealed record NotJson(int Chars) : GateReply;
}

/// <summary>One row of the product's usage ledger — one reviewer turn.</summary>
/// <param name="Outcome">The product's own word for how the turn ended; <c>ok</c> is the one that counts.</param>
public sealed record LedgerTurn(
    double Seconds,
    CapturedCount TokensIn,
    CapturedCount TokensOut,
    CapturedCount TokensCached,
    CapturedCount TokensReasoning,
    CapturedUsd CostUsd,
    string Outcome)
{
    public bool IsOk => string.Equals(Outcome, "ok", StringComparison.Ordinal);
}

/// <summary>One HTTP exchange the tap recorded in front of an <c>api</c> reviewer.</summary>
/// <param name="FinishReason">The vendor's own word; <c>length</c> is the one that names a cut-off answer.</param>
public sealed record HttpCallFacts(
    int Status,
    string FinishReason,
    CapturedCount CachedTokens,
    CapturedCount ReasoningTokens,
    double WallSeconds,
    int ContentChars);

public enum FailureKind
{
    None,
    NonJsonReply,
    HttpError,
    LengthCut,
    EmptyContent,
    VerdictNotPassing,
    NoTurns,
    TurnFailed,
    NoFindingsList,
    ProcessDied,
    Interrupted,
    Unexplained,
}

/// <summary>Why a run was not valid — a KIND the page groups by, and a sentence that names the call, the
/// status, the verdict. The sentence is the one free-text column the gate stores, and it passes the
/// publication redaction before it does.</summary>
public sealed record FailureCause(FailureKind Kind, string Text)
{
    public static FailureCause None { get; } = new(FailureKind.None, string.Empty);

    public bool IsFailure => Kind != FailureKind.None;
}

/// <summary>The one-line facts of a run, from three sources — the tool's answer, the product's ledger, the
/// tap — and the <c>valid</c> rule over them. A port of the other harness's <c>summarise</c>, field for field,
/// so an imported row and a native one are computed by one function.</summary>
public sealed record GateRunFacts(
    bool Valid,
    GateVerdictWord Verdict,
    bool ReplyParsed,
    CapturedCount Findings,
    int Turns,
    int HttpCalls,
    IReadOnlyList<string> FinishReasons,
    IReadOnlyList<int> Statuses,
    CapturedCount TokensIn,
    CapturedCount TokensOut,
    CapturedCount TokensCached,
    CapturedCount TokensReasoning,
    double SecondsTotal,
    double ReviewSeconds,
    IReadOnlyList<double> SecondsPerTurn,
    IReadOnlyList<CapturedCount> CachedPerCall,
    CapturedUsd CostUsd,
    int Served,
    int Refused,
    FailureCause Failure)
{
    /// <summary>HTTP calls beyond the turns — a repair (the product re-asking a turn whose answer was not the
    /// schema) or a retry. The count proves an extra call, not its cause.</summary>
    public int ExtraCalls => Math.Max(0, HttpCalls - Turns);

    /// <summary>A turn-1 cache read above this many tokens is a WARM run — the prompt was served from a
    /// vendor cache rather than read afresh — and warm runs are reported apart.</summary>
    public const long WarmTurnOneCachedTokens = 4096;

    public bool IsWarm => CachedPerCall.Count > 0 && CachedPerCall[0].WasCaptured && CachedPerCall[0].Value > WarmTurnOneCachedTokens;

    public static GateRunFacts From(
        GateReply reply, IReadOnlyList<LedgerTurn> ledger, IReadOnlyList<HttpCallFacts> calls, double wallSeconds, int served, int refused)
    {
        var (verdict, findings, parsed) = reply switch
        {
            GateReply.Answered a => (a.Verdict, a.Findings, true),
            _ => (GateVerdictWord.Unknown, CapturedCount.Unavailable("the reply was not JSON"), false),
        };

        var valid = parsed
            && GateVerdictWords.IsPassing(verdict)
            && ledger.Count > 0
            && ledger.All(t => t.IsOk)
            && findings.WasCaptured;

        var facts = new GateRunFacts(
            valid,
            verdict,
            parsed,
            findings,
            ledger.Count,
            calls.Count,
            [.. calls.Select(c => c.FinishReason)],
            [.. calls.Select(c => c.Status)],
            Sum(ledger, t => t.TokensIn, "no ledger turn"),
            Sum(ledger, t => t.TokensOut, "no ledger turn"),
            Sum(ledger, t => t.TokensCached, "no ledger turn"),
            Sum(ledger, t => t.TokensReasoning, "no ledger turn reported reasoning tokens"),
            wallSeconds,
            PythonRound.Of(ledger.Sum(t => t.Seconds), 1),
            [.. ledger.Select(t => PythonRound.Of(t.Seconds, 1))],
            [.. calls.Select(c => c.CachedTokens)],
            Cost(ledger),
            served,
            refused,
            FailureCause.None);

        return facts with { Failure = FailureCauses.Of(facts, reply, ledger, calls) };
    }

    /// <summary>The facts of a session that never reached an end — a process that died, a cell abandoned after its
    /// attempts. Invalid, with nothing captured and the cause kept: a failed run stays in every denominator, so it
    /// needs facts, and every one of them is <i>not captured</i> rather than a zero that reads as "none".</summary>
    public static GateRunFacts NotProduced(FailureCause cause) => new(
        Valid: false,
        GateVerdictWord.Unknown,
        ReplyParsed: false,
        CapturedCount.Unavailable(NotProducedReason),
        Turns: 0,
        HttpCalls: 0,
        [],
        [],
        CapturedCount.Unavailable(NotProducedReason),
        CapturedCount.Unavailable(NotProducedReason),
        CapturedCount.Unavailable(NotProducedReason),
        CapturedCount.Unavailable(NotProducedReason),
        SecondsTotal: 0,
        ReviewSeconds: 0,
        [],
        [],
        CapturedUsd.Unavailable(NotProducedReason),
        Served: 0,
        Refused: 0,
        cause);

    /// <summary>The one reason every field of a session that never ended carries.</summary>
    public const string NotProducedReason = "the session never reached an end";

    /// <summary>The cost the ledger metered, or <i>unknown</i> when no turn carried one — never free.</summary>
    private static CapturedUsd Cost(IReadOnlyList<LedgerTurn> ledger)
    {
        var metered = ledger.Where(t => t.CostUsd.WasCaptured).ToList();

        return metered.Count > 0
            ? CapturedUsd.Amount(metered.Sum(t => t.CostUsd.Value))
            : CapturedUsd.Unavailable(ledger.Count == 0 ? "no ledger turn" : "no ledger turn carried a cost");
    }

    /// <summary>The sum of what was captured, or <i>not captured</i> when nothing was — never a zero that
    /// reads as "none".</summary>
    private static CapturedCount Sum(IReadOnlyList<LedgerTurn> ledger, Func<LedgerTurn, CapturedCount> field, string reason)
    {
        var captured = ledger.Select(field).Where(c => c.WasCaptured).ToList();

        return captured.Count > 0
            ? CapturedCount.Number(captured.Sum(c => c.Value))
            : CapturedCount.Unavailable(reason);
    }
}

/// <summary>A port of the other harness's <c>failure_cause</c>: every reason it could name, in the order it
/// named them, joined into one sentence, with the FIRST as the kind the page groups by.</summary>
public static class FailureCauses
{
    public static FailureCause Of(GateRunFacts facts, GateReply reply, IReadOnlyList<LedgerTurn> ledger, IReadOnlyList<HttpCallFacts> calls)
    {
        if (facts.Valid)
        {
            return FailureCause.None;
        }

        if (reply is GateReply.NotJson nonJson)
        {
            return new FailureCause(FailureKind.NonJsonReply, $"tool answered non-JSON ({nonJson.Chars} chars)");
        }

        var reasons = CallReasons(calls).Concat(ReplyReasons(facts, ledger)).ToList();

        return reasons.Count > 0
            ? new FailureCause(reasons[0].Kind, string.Join(" | ", reasons.Select(r => r.Text)))
            : new FailureCause(FailureKind.Unexplained, "no call, turn or verdict names the cause — see the server's stderr for this run");
    }

    private static IEnumerable<(FailureKind Kind, string Text)> CallReasons(IReadOnlyList<HttpCallFacts> calls) =>
        calls.Select((call, index) => CallReason(call, index + 1)).Where(r => r.Kind != FailureKind.None);

    private static (FailureKind Kind, string Text) CallReason(HttpCallFacts call, int number) =>
        (call.Status, call.FinishReason, call.ContentChars) switch
        {
            (not 200, _, _) => (FailureKind.HttpError, $"call {number}: HTTP {call.Status}"),
            (_, "length", _) => (FailureKind.LengthCut,
                $"call {number}: finish_reason=length (reasoning={Reasoning(call)}, content={call.ContentChars} chars)"),
            (_, _, 0) => (FailureKind.EmptyContent, $"call {number}: empty content (finish={call.FinishReason}, reasoning={Reasoning(call)})"),
            _ => (FailureKind.None, string.Empty),
        };

    private static IEnumerable<(FailureKind Kind, string Text)> ReplyReasons(GateRunFacts facts, IReadOnlyList<LedgerTurn> ledger)
    {
        if (!GateVerdictWords.IsPassing(facts.Verdict))
        {
            yield return (FailureKind.VerdictNotPassing, $"verdict {facts.Verdict} — the product stopped rather than the reviewer answering");
        }

        if (ledger.Count == 0)
        {
            yield return (FailureKind.NoTurns, "the ledger holds no turn — no reviewer was ever asked");
        }

        foreach (var (turn, index) in ledger.Select((t, i) => (t, i)).Where(p => !p.t.IsOk))
        {
            yield return (FailureKind.TurnFailed, $"turn {index + 1}: {turn.Outcome}");
        }

        if (facts.ReplyParsed && !facts.Findings.WasCaptured)
        {
            yield return (FailureKind.NoFindingsList, "the reply carried no findings list");
        }
    }

    private static string Reasoning(HttpCallFacts call) =>
        call.ReasoningTokens.WasCaptured ? call.ReasoningTokens.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "not captured";
}
