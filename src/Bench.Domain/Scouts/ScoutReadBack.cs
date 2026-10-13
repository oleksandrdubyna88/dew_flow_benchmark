using Bench.Domain.Probes;

namespace Bench.Domain.Scouts;

/// <summary>What one cell was launched to do: the arm (its count, model alias and agent type), the frozen prompts' SHA-256s (one per
/// scout), the resolved model id every child must have answered with, and the tree the children were confined to.</summary>
public sealed record ReadBackExpectation(ScoutArm Arm, IReadOnlyList<string> PromptSha256s, string ResolvedModel, string TreeRoot);

/// <summary>The read-back's answer for one cell: the void reasons, the stop reasons, and the path audit with its recorded outside
/// attempts. The reasons are <see cref="ScoutReason"/> words — no sentence reaches the database.</summary>
public sealed record ReadBackVerdict(IReadOnlyList<ScoutReason> Voids, IReadOnlyList<ScoutReason> Stops, IReadOnlyList<AuditedCall> Audit)
{
    public bool IsValid => Voids.Count == 0 && Stops.Count == 0;

    /// <summary>A stop outranks a void: a broken boundary ends the campaign whatever else the cell got wrong.</summary>
    public ScoutCellOutcome Outcome => (Stops.Count > 0, Voids.Count > 0) switch
    {
        (true, _) => ScoutCellOutcome.Stopped,
        (_, true) => ScoutCellOutcome.Void,
        _ => ScoutCellOutcome.Scored,
    };

    /// <summary>The one reason a settled cell stores — the first stop, else the first void, else none.</summary>
    public ScoutReason Reason => Stops.Count > 0 ? Stops[0] : Voids.Count > 0 ? Voids[0] : ScoutReason.None;

    /// <summary>Outside attempts the CLI refused, and outside searches that came back explicitly empty — recorded, never a stop.</summary>
    public int RecordedOutsideAttempts => Audit.Count(c => c.Finding is PathFinding.OutsideDenied or PathFinding.OutsideEmpty);
}

/// <summary>The read-back of §3.5, on EVERY cell: exactly N Agent calls; each call's <c>input.model</c>, agent type and prompt hash
/// equal the frozen ones; each child's <c>resolvedModel</c> and every <c>message.model</c> it answered with are the arm's resolved id;
/// no child tool outside {Read, Glob, Grep, LS}; the main session calls nothing but Agent; <c>subagent_stats.completed = N</c>; and
/// every child read path inside the tree (§3.2's fail-stop audit, <see cref="PathAudit"/>). Any miss voids the cell with its reason;
/// a path-audit stop stops the campaign.</summary>
public static class ScoutReadBack
{
    public static readonly IReadOnlySet<string> ReadOnlyTools = PathAudit.AuditedTools;

    public static ReadBackVerdict Check(SubagentTranscript transcript, ReadBackExpectation expected, LinkTargets links)
    {
        var audit = PathAudit.Audit(transcript, expected.TreeRoot, links);

        return new ReadBackVerdict(
            [.. VoidChecks(transcript, expected).Where(check => check.Failed).Select(check => check.Reason)],
            [.. audit.Select(c => PathAudit.StopReason(c.Finding)).Where(r => r != ScoutReason.None).Distinct()],
            audit);
    }

    private static IEnumerable<(bool Failed, ScoutReason Reason)> VoidChecks(SubagentTranscript t, ReadBackExpectation e)
    {
        yield return (!t.Complete, ScoutReason.StreamIncomplete);
        yield return (t.Agents.Count != e.Arm.Count, ScoutReason.WrongCallCount);
        yield return (t.Agents.Any(a => a.InputModel != e.Arm.ModelAlias), ScoutReason.InputModelMismatch);
        yield return (t.Agents.Any(a => a.SubagentType != e.Arm.AgentType), ScoutReason.AgentTypeMismatch);
        yield return (!SameHashes(t.Agents.Select(a => a.PromptSha256), e.PromptSha256s), ScoutReason.PromptHashMismatch);
        yield return (t.Agents.Any(a => !a.Result.Captured || a.Result.IsError), ScoutReason.AgentCallFailed);
        yield return (t.Agents.Any(a => a.Result.Captured && !a.Result.IsError && a.Result.ResolvedModel.Length == 0), ScoutReason.ChildModelNotCaptured);
        yield return (t.Agents.Any(a => WrongModel(a, e.ResolvedModel)), ScoutReason.ChildModelMismatch);
        yield return (t.Agents.SelectMany(a => a.Child.Tools).Any(c => !ReadOnlyTools.Contains(c.Name)), ScoutReason.ChildToolOutsideReadOnlySet);
        yield return (t.MainCalls.Any(c => c.Name != ClaudeSubagents.AgentTool), ScoutReason.MainSessionNonAgentCall);
        yield return (!t.Envelope.Stats.Captured || t.Envelope.Stats.Completed != e.Arm.Count, ScoutReason.SubagentsNotCompleted);
    }

    /// <summary>A child answered with another model: its <c>resolvedModel</c> (when there is one — a missing one is its own reason) or any
    /// message's <c>message.model</c> is not the arm's resolved id.</summary>
    private static bool WrongModel(SubagentCall call, string resolvedModel) =>
        call.Result.ResolvedModel.Length > 0 && !string.Equals(call.Result.ResolvedModel, resolvedModel, StringComparison.Ordinal)
        || call.Child.MessageModels.Any(m => !string.Equals(m, resolvedModel, StringComparison.Ordinal));

    /// <summary>The calls' prompts are exactly the frozen ones as a MULTISET — order inside the one message is not part of the contract,
    /// a prompt sent twice and another never is.</summary>
    private static bool SameHashes(IEnumerable<string> actual, IReadOnlyList<string> expected) =>
        actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
