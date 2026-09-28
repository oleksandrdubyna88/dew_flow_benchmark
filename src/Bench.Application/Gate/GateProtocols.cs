using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>One tool call of a cell's session and, for a review, the resolve that followed it: what was sent, what
/// came back, and what the resolve refused — kept, because "can these findings be acted on" is answered there.</summary>
public sealed record ProtocolStage(string Name, string Tool, JsonObject Arguments, string Reply, double Seconds, string Resolve, string ResolveRefusal);

/// <summary>A cell's session, driven to its end or to the point it broke.</summary>
/// <param name="Measured">The stage whose reply IS the measurement — the plan round, the code round, the feature
/// review; <see cref="ProtocolStage"/>'s name is empty when the session broke before one answered.</param>
/// <param name="Broken">Why the session could not be driven on (a call that timed out and took the process, a product
/// that exited), or empty. A broken session is a FAILED cell, not an invalid run.</param>
/// <param name="PlanLoopPassed">For the code gate: whether a plan round reached a passing verdict before the code round.</param>
public sealed record ProtocolRun(IReadOnlyList<ProtocolStage> Stages, ProtocolStage Measured, string Broken, bool PlanLoopPassed)
{
    public static ProtocolStage NoStage { get; } = new(string.Empty, string.Empty, [], string.Empty, 0, string.Empty, string.Empty);

    public bool HasMeasurement => Measured.Name.Length > 0;
}

/// <summary>What a protocol is called with: the checkout the product reviews, the run's own ref, the task, the plan
/// text, and the cell's ABSOLUTE deadline — every call's timeout is what is left of it.</summary>
public sealed record ProtocolInputs(string RepoPath, string Branch, GateTask Task, string PlanText, DateTimeOffset Deadline, TimeProvider Clock)
{
    /// <summary>The harness is the caller, and says so — the other harness said <c>calib-harness</c>.</summary>
    public const string CallerModel = "bench-gate";

    /// <summary>The plan stage is asked at most this many times before a code cell gives up on reaching the code stage.
    /// The product's own budget is what actually stops it; this only keeps a bench from looping.</summary>
    public const int MaxPlanRounds = 4;

    public TimeSpan Remaining => Deadline - Clock.GetUtcNow();
}

/// <summary>The steps the three protocols share: a call under what is left of the deadline, and a review followed by
/// its accept-all resolve.</summary>
public static class GateProtocolSteps
{
    public static async Task<Outcome<ProtocolStage>> CallAsync(
        IMcpSession session, string name, string tool, JsonObject arguments, ProtocolInputs inputs, CancellationToken cancellationToken)
    {
        if (inputs.Remaining <= TimeSpan.Zero)
        {
            return Outcome<ProtocolStage>.Failure($"the cell's deadline passed before {tool} — the session was ended");
        }

        var answered = await session.CallToolAsync(tool, arguments, inputs.Remaining, cancellationToken);

        return answered.Match(
            a => Outcome<ProtocolStage>.Success(new ProtocolStage(name, tool, arguments, a.Text, a.Seconds, string.Empty, string.Empty)),
            Outcome<ProtocolStage>.Failure);
    }

    /// <summary>A review, then — when it answered a verdict — the resolve that accepts every finding. A round that did not
    /// answer one (non-JSON, a refusal) is not resolved: there is nothing to decide.</summary>
    public static async Task<Outcome<ProtocolStage>> ReviewAsync(
        IMcpSession session, string name, string tool, JsonObject arguments, ProtocolInputs inputs, CancellationToken cancellationToken)
    {
        var review = await CallAsync(session, name, tool, arguments, inputs, cancellationToken);

        if (review is not Outcome<ProtocolStage>.Ok { Value: var stage } || GateReplyParser.Parse(stage.Reply).Reply is not GateReply.Answered)
        {
            return review;
        }

        var decisions = GateReplyParser.AcceptAll(GateReplyParser.Parse(stage.Reply).Findings.Count);
        var resolve = await CallAsync(
            session, name + "-resolve", "resolve",
            new JsonObject { ["repoPath"] = inputs.RepoPath, ["branch"] = inputs.Branch, ["decisions"] = decisions },
            inputs, cancellationToken);

        return resolve.Match(
            r => Outcome<ProtocolStage>.Success(stage with { Resolve = r.Reply, ResolveRefusal = GateReplyParser.RefusalIn(r.Reply) }),
            Outcome<ProtocolStage>.Failure);
    }

    public static JsonObject Open(ProtocolInputs inputs) =>
        new() { ["repoPath"] = inputs.RepoPath, ["branch"] = inputs.Branch, ["callerModel"] = ProtocolInputs.CallerModel };

    public static JsonObject Plan(ProtocolInputs inputs) =>
        new() { ["repoPath"] = inputs.RepoPath, ["branch"] = inputs.Branch, ["planText"] = inputs.PlanText };

    public static ProtocolRun Broken(IReadOnlyList<ProtocolStage> stages, string reason) => new(stages, ProtocolRun.NoStage, reason, false);
}

/// <summary>The plan gate: <c>open → review_plan → resolve accept-all</c>. ONE round: the bench never revises the plan,
/// so a second round would re-review the same text; the reply of that round is the measurement.</summary>
public static class PlanGateProtocol
{
    public static async Task<ProtocolRun> RunAsync(IMcpSession session, ProtocolInputs inputs, CancellationToken cancellationToken)
    {
        var open = await GateProtocolSteps.CallAsync(session, "open", "open", GateProtocolSteps.Open(inputs), inputs, cancellationToken);

        if (open is Outcome<ProtocolStage>.Fail openFail)
        {
            return GateProtocolSteps.Broken([], openFail.Reason);
        }

        var opened = ((Outcome<ProtocolStage>.Ok)open).Value;
        var plan = await GateProtocolSteps.ReviewAsync(session, "plan-1", "review_plan", GateProtocolSteps.Plan(inputs), inputs, cancellationToken);

        return plan.Match(
            stage => new ProtocolRun([opened, stage], stage, string.Empty, GateReplyParser.Passed(GateReplyParser.Parse(stage.Reply).Verdict)),
            reason => GateProtocolSteps.Broken([opened], reason));
    }
}

/// <summary>The code gate: <c>open → plan loop (≤ 4 rounds, accept-all, until proceed | good_enough | continue_anyway)
/// → review_code → resolve accept-all</c>, on the run's own ref. The product refuses <c>review_code</c> until a plan round
/// was RESOLVED at a passing verdict, which is why the loop resolves every round. <c>again</c> is never sent: every cell
/// attempt is a fresh session. A plan loop that never passed is recorded — the code round is never called.</summary>
public static class CodeGateProtocol
{
    public static async Task<ProtocolRun> RunAsync(IMcpSession session, ProtocolInputs inputs, CancellationToken cancellationToken)
    {
        var open = await GateProtocolSteps.CallAsync(session, "open", "open", GateProtocolSteps.Open(inputs), inputs, cancellationToken);

        if (open is Outcome<ProtocolStage>.Fail openFail)
        {
            return GateProtocolSteps.Broken([], openFail.Reason);
        }

        var stages = new List<ProtocolStage> { ((Outcome<ProtocolStage>.Ok)open).Value };
        var (passed, broken) = await PlanLoopAsync(session, inputs, stages, cancellationToken);

        if (broken.Length > 0)
        {
            return GateProtocolSteps.Broken(stages, broken);
        }

        if (!passed)
        {
            return new ProtocolRun(stages, stages[^1], string.Empty, PlanLoopPassed: false);
        }

        var code = await GateProtocolSteps.ReviewAsync(session, "code", "review_code", Code(inputs), inputs, cancellationToken);

        return code.Match(
            stage => new ProtocolRun([.. stages, stage], stage, string.Empty, PlanLoopPassed: true),
            reason => GateProtocolSteps.Broken(stages, reason));
    }

    private static async Task<(bool Passed, string Broken)> PlanLoopAsync(
        IMcpSession session, ProtocolInputs inputs, List<ProtocolStage> stages, CancellationToken cancellationToken)
    {
        for (var round = 1; round <= ProtocolInputs.MaxPlanRounds; round++)
        {
            var plan = await GateProtocolSteps.ReviewAsync(session, $"plan-{round}", "review_plan", GateProtocolSteps.Plan(inputs), inputs, cancellationToken);

            if (plan is Outcome<ProtocolStage>.Fail fail)
            {
                return (false, fail.Reason);
            }

            var stage = ((Outcome<ProtocolStage>.Ok)plan).Value;
            stages.Add(stage);
            var parsed = GateReplyParser.Parse(stage.Reply);

            if (GateReplyParser.Passed(parsed.Verdict) || parsed.Reply is not GateReply.Answered)
            {
                return (GateReplyParser.Passed(parsed.Verdict), string.Empty); // passed, or a round that did not run: another measures nothing
            }
        }

        return (false, string.Empty);
    }

    private static JsonObject Code(ProtocolInputs inputs) => new()
    {
        ["repoPath"] = inputs.RepoPath,
        ["branch"] = inputs.Branch,
        ["baseRef"] = inputs.Task.Case.Base.Value,
        ["planText"] = inputs.PlanText,
        ["again"] = false,
    };
}

/// <summary>The feature gate: <c>review_feature(repoPath, planPath, baseRef, epics, lessons)</c> with the suite's inputs,
/// from the clone at the variant head — its own session, no <c>open</c>, and (as the calibration harness did) no resolve.</summary>
public static class FeatureGateProtocol
{
    public static async Task<ProtocolRun> RunAsync(IMcpSession session, ProtocolInputs inputs, CancellationToken cancellationToken)
    {
        var arguments = new JsonObject
        {
            ["repoPath"] = inputs.RepoPath,
            ["planPath"] = inputs.Task.Case.PlanPath,
            ["baseRef"] = inputs.Task.Case.Base.Value,
            ["epics"] = inputs.Task.Case.Epics,
            ["lessons"] = inputs.Task.Case.Lessons,
            ["again"] = false,
            ["callerModel"] = ProtocolInputs.CallerModel,
        };

        var review = await GateProtocolSteps.CallAsync(session, "feature", "review_feature", arguments, inputs, cancellationToken);

        return review.Match(
            stage => new ProtocolRun([stage], stage, string.Empty, PlanLoopPassed: false),
            reason => GateProtocolSteps.Broken([], reason));
    }
}
