namespace Bench.Domain.Sessions;

/// <summary>What a detector found, and where. Never a verdict on the agent — a pattern, its price, and the
/// call ordinals that prove it, so a reader can go and look rather than trust a label.</summary>
public enum FindingKind
{
    /// <summary>The same target read again and again with nothing changed in between.</summary>
    ReResearchLoop,

    /// <summary>One question asked several ways: consecutive searches whose words mostly agree.</summary>
    SearchVariantChain,

    /// <summary>A tool this system counted as harmless moved the working tree. A defect in the taxonomy,
    /// found by the taxonomy's own ground truth.</summary>
    MutationDisagreement,

    /// <summary>A shell command conservatively counted as a write that changed nothing, repeatedly. Not a
    /// finding about the agent at all — it is this system's own allowlist telling us what to add to it.</summary>
    AllowlistCandidate,
}

/// <param name="Ordinals">The calls that make the case. A finding a reader cannot go and check is an
/// assertion, and this whole family exists because assertions were being made without them.</param>
public sealed record SessionFinding(
    FindingKind Kind,
    string Subject,
    int Occurrences,
    IReadOnlyList<int> Ordinals,
    string Detail);

/// <param name="Elapsed">Wall time between this phase's calls and the next one's. Attributed from the call
/// TIMESTAMPS rather than summed from durations, because the gaps between calls — the model thinking — are
/// most of a session and belong to the phase they happen in.</param>
public sealed record PhaseTotals(SessionPhase Phase, int Calls, TimeSpan Elapsed);

/// <param name="UnknownTools">Calls whose tool this build's taxonomy does not know. The instrument's own
/// error bar: every phase number below is a number about the calls it COULD classify, and a report that
/// hid this would be quoting a denominator it had not stated.</param>
public sealed record SessionEconomics(
    IReadOnlyList<PhaseTotals> Phases,
    int Switches,
    int CompileFailures,
    int UnknownTools,
    IReadOnlyList<SessionFinding> Findings);

/// <summary>The detectors — pure, deterministic, and with no model anywhere.
/// <para>
/// Same trace in, same findings out, every run. That is not a nicety: the whole thesis of this family is
/// that deterministic code can do work a model is currently doing, and an analyzer that asked a model
/// would be a demonstration of the opposite.
/// </para></summary>
public static class SessionAnalysis
{
    /// <summary>How many reads of one target, with nothing changed between them, stop being diligence.
    /// <para>
    /// Three, and the window is what matters rather than the number: the counter resets at every change to
    /// the tree. <b>The draft rule — "three or more reads after an execution phase" — is refused here</b>,
    /// because a read after an edit is how a careful agent verifies itself, and a detector that punished it
    /// would push the corpus toward exactly the behaviour nobody wants.
    /// </para></summary>
    public const int RepeatedReadsThreshold = 3;

    /// <summary>How many near-identical searches in a row are a chain. Three, for the same reason: two is
    /// a refinement, and refining a search is the search working.</summary>
    public const int VariantChainThreshold = 3;

    /// <summary>How alike two consecutive searches must be to count as the same question re-asked. Half
    /// their words — a number this build chose and which the first measured series is expected to move.
    /// It is stated as a constant rather than buried in the comparison so that moving it is a decision
    /// somebody makes, not an edit somebody notices later.</summary>
    public const double VariantOverlap = 0.5;

    public static SessionEconomics Of(IReadOnlyList<SessionToolCall> calls, ToolTaxonomy taxonomy) => new(
        Phases(calls),
        Switches(calls),
        calls.Count(c => c.CompileFailure),
        calls.Count(c => c.Class == ToolClass.Unknown),
        [.. Loops(calls), .. Chains(calls, taxonomy), .. Disagreements(calls), .. Candidates(calls, taxonomy)]);

    /// <summary>Time and calls per phase. Every phase that occurred, none that did not — a zero row for a
    /// phase a session never entered reads as "it verified nothing" when the truth is "it never got
    /// there".</summary>
    public static IReadOnlyList<PhaseTotals> Phases(IReadOnlyList<SessionToolCall> calls) =>
        [.. calls
            .Select((call, index) => (call, span: Span(calls, index)))
            .GroupBy(x => x.call.Phase)
            .Select(g => new PhaseTotals(g.Key, g.Count(), Sum(g.Select(x => x.span))))
            .OrderBy(p => p.Phase)];

    /// <summary>How often the phase changed. The number the replacement map reads as churn: a session that
    /// switched forty times did not do four things, it did one thing badly.</summary>
    public static int Switches(IReadOnlyList<SessionToolCall> calls) =>
        calls.Where((call, index) => index > 0 && call.Phase != calls[index - 1].Phase).Count();

    /// <summary>Targets read three times or more without an intervening change.</summary>
    public static IReadOnlyList<SessionFinding> Loops(IReadOnlyList<SessionToolCall> calls)
    {
        var found = new List<SessionFinding>();
        var window = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var call in calls)
        {
            if (call.Phase == SessionPhase.Execution)
            {
                // The tree moved: everything read before it may legitimately need reading again.
                Flush(window, found);
                continue;
            }

            Remember(window, call);
        }

        Flush(window, found);

        return found;
    }

    /// <summary>Runs of consecutive searches asking one question in rearranged words.</summary>
    public static IReadOnlyList<SessionFinding> Chains(
        IReadOnlyList<SessionToolCall> calls, ToolTaxonomy taxonomy)
    {
        var found = new List<SessionFinding>();
        var run = new List<SessionToolCall>();

        foreach (var call in calls)
        {
            var continues = taxonomy.IsSearch(call.ToolName) && Similar(run, call);
            run = continues ? [.. run, call] : Restart(found, run, taxonomy, call);
        }

        Close(found, run);

        return found;
    }

    /// <summary>Calls where the working tree contradicted the taxonomy. One row each — these are defects
    /// in this system, and aggregating them would make it easy to leave them aggregated.</summary>
    public static IReadOnlyList<SessionFinding> Disagreements(IReadOnlyList<SessionToolCall> calls) =>
        [.. calls.Where(c => c.Disagrees).Select(c => new SessionFinding(
            FindingKind.MutationDisagreement,
            c.Target.Length > 0 ? c.Target : c.ToolName,
            1,
            [c.Ordinal],
            $"{c.ToolName} is classified {c.Class} and the working tree changed across it"))];

    /// <summary>Shell commands counted as writes that never moved the tree. The allowlist's own to-do
    /// list, produced by measurement rather than by guessing which commands look safe.</summary>
    public static IReadOnlyList<SessionFinding> Candidates(
        IReadOnlyList<SessionToolCall> calls, ToolTaxonomy taxonomy) =>
        [.. calls
            .Where(c => c.Class == ToolClass.Write
                && c.Mutation == MutationEvidence.Unchanged
                && taxonomy.Shell.Contains(ToolTaxonomy.Strip(c.ToolName))
                && c.Target.Length > 0)
            .GroupBy(c => c.Target, StringComparer.Ordinal)
            .Select(g => new SessionFinding(
                FindingKind.AllowlistCandidate,
                g.Key,
                g.Count(),
                [.. g.Select(c => c.Ordinal)],
                $"'{g.Key}' was counted as a write {g.Count()} time(s) and changed nothing"))];

    // ---- the machinery the detectors above are written in terms of -------------------------------------

    private static void Remember(Dictionary<string, List<int>> window, SessionToolCall call)
    {
        if (call.Class != ToolClass.Read || call.Target.Length == 0)
        {
            return;
        }

        if (!window.TryGetValue(call.Target, out var ordinals))
        {
            window[call.Target] = ordinals = [];
        }

        ordinals.Add(call.Ordinal);
    }

    private static void Flush(Dictionary<string, List<int>> window, List<SessionFinding> found)
    {
        found.AddRange(window
            .Where(entry => entry.Value.Count >= RepeatedReadsThreshold)
            .Select(entry => new SessionFinding(
                FindingKind.ReResearchLoop,
                entry.Key,
                entry.Value.Count,
                entry.Value,
                $"read {entry.Value.Count} times with no change to the tree in between")));

        window.Clear();
    }

    /// <summary>Whether this search continues the run — alike enough to be the same question, and not
    /// IDENTICAL, which is a repeated read and belongs to the other detector rather than to both.</summary>
    private static bool Similar(List<SessionToolCall> run, SessionToolCall call)
    {
        if (run.Count == 0)
        {
            return true;
        }

        var previous = run[^1];
        var overlap = ToolTarget.Overlap(ToolTarget.Words(previous.Target), ToolTarget.Words(call.Target));

        return overlap >= VariantOverlap && !string.Equals(previous.Target, call.Target, StringComparison.Ordinal);
    }

    private static List<SessionToolCall> Restart(
        List<SessionFinding> found, List<SessionToolCall> run, ToolTaxonomy taxonomy, SessionToolCall call)
    {
        Close(found, run);

        return taxonomy.IsSearch(call.ToolName) ? [call] : [];
    }

    private static void Close(List<SessionFinding> found, List<SessionToolCall> run)
    {
        if (run.Count < VariantChainThreshold)
        {
            return;
        }

        found.Add(new SessionFinding(
            FindingKind.SearchVariantChain,
            run[0].Target,
            run.Count,
            [.. run.Select(c => c.Ordinal)],
            $"{run.Count} searches in a row re-asked one question: {string.Join(" · ", run.Select(c => c.Target))}"));
    }

    /// <summary>What one call occupied: the gap until the next one. The LAST call has no next, so it is
    /// credited with its own measured duration where one was captured and with nothing where none was —
    /// a session's tail is not evidence about how long its final act took.</summary>
    private static TimeSpan Span(IReadOnlyList<SessionToolCall> calls, int index) =>
        index + 1 < calls.Count
            ? Positive(calls[index + 1].At - calls[index].At)
            : Tail(calls[index]);

    private static TimeSpan Tail(SessionToolCall call) =>
        call.DurationMs.WasCaptured ? TimeSpan.FromMilliseconds(call.DurationMs.Value) : TimeSpan.Zero;

    /// <summary>Clocks go backwards — a resumed session, a machine that re-synchronised. A negative span
    /// would silently subtract from a phase total, so it contributes nothing instead.</summary>
    private static TimeSpan Positive(TimeSpan span) => span < TimeSpan.Zero ? TimeSpan.Zero : span;

    private static TimeSpan Sum(IEnumerable<TimeSpan> spans) =>
        spans.Aggregate(TimeSpan.Zero, (total, span) => total + span);
}
