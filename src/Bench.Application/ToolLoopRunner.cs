using System.Diagnostics;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Microsoft.Extensions.Logging;

namespace Bench.Application;

/// <summary>
/// What a tool-calling leg produced: the last answer, everything that happened on the way, and whether it
/// ran out of turns.
/// </summary>
/// <param name="Answer">The turn that ended the loop — or the last one taken, when the ceiling ended it
/// instead.</param>
/// <param name="Transcript">Every turn, in order. <b>Kept rather than discarded</b>, because the operator's
/// requirement is to see what was actually sent, and the middle of a loop exists nowhere else: the user
/// prompt is on the result, the doctrine is in the lane, the advertised tools are in the surface
/// fingerprint, and each turn's own messages are only here.</param>
/// <param name="Calls">One record per invocation, with its OUTCOME rather than its size. "A refused call and
/// an executed one were indistinguishable" is the defect that let a false read-only guarantee stand for
/// months upstream, because all the ledger recorded was a result's length.</param>
/// <param name="TurnsSpent">How many times the model was asked.</param>
/// <param name="End">Why the loop stopped. Two states rather than a bool, so a reader of the record does not
/// have to know which way <c>true</c> pointed. The leg's WALL is not a third state here: it ends the loop as
/// a FAILURE, because the caller already knows how to settle one — <c>UnansweredAsync</c> checks the
/// deadline and caps it — and inventing an answer to carry back would put a turn in the ledger that never
/// happened.</param>
public sealed record ToolLoopResult(
    ModelAnswer Answer,
    IReadOnlyList<ModelTurn> Transcript,
    IReadOnlyList<TurnCall> Calls,
    int TurnsSpent,
    LoopEnd End);

/// <summary>Why a tool loop stopped.</summary>
public enum LoopEnd
{
    /// <summary>The model answered. The only ending that is scored.</summary>
    Answered,

    /// <summary>The turn ceiling ran out while the model was still working.</summary>
    TurnsSpent,

}

/// <summary>One call and the turn it happened on.
/// <para>The turn is carried here because <see cref="Bench.Domain.Trace.ToolCall"/> has no field for it and
/// the stored ledger needs one — and it cannot be re-derived later: a leg that called two tools on turn 3
/// and one on turn 4 is indistinguishable, from an ordered list alone, from one that called three on a
/// single turn.</para></summary>
public sealed record TurnCall(int Turn, Bench.Domain.Trace.ToolCall Call);

/// <summary>
/// The loop the domain has been describing since <c>BudgetKind.Turns</c> was declared.
///
/// <para><b>The turn budget is confirmed HERE, and the runtime is right to keep refusing it.</b>
/// <c>OpenAiCompatibleRuntime</c> answers a turn ceiling with "one completion has no turns — a turn ceiling
/// belongs to an agentic loop, not to this runtime". This class is the component that refusal names. A
/// budget nobody confirmed is a budget that does not exist: upstream, a context ceiling was configured,
/// believed and reasoned from for a whole series while reaching none of the arms it was supposed to
/// bound.</para>
///
/// <para>One collaborator, deliberately. It asks, invokes, appends and asks again; it scores nothing,
/// persists nothing and settles nothing, so <c>LegRunner</c> stays the assembly it already is.</para>
/// </summary>
public sealed class ToolLoopRunner(IModelRuntime runtime, TimeProvider clock, ILogger<ToolLoopRunner> logger)
{
    /// <param name="deadline">The leg's wall, not a frozen budget list — and that distinction is the whole
    /// reason this parameter is shaped this way. <c>LegDeadline.ForCall</c> narrows the wall to what REMAINS,
    /// and its own comment says why: it "is what makes twenty-five turns share one ceiling instead of each
    /// starting a fresh one". Computed once outside the loop, every turn would be handed the remainder as it
    /// stood at turn one, and a leg could outrun its wall by a factor of its turn ceiling. It is recomputed
    /// per turn here, and checked between turns.</param>
    public async Task<Outcome<ToolLoopResult>> RunAsync(
        ModelEndpoint endpoint,
        Sampling sampling,
        string doctrine,
        string prompt,
        LegDeadline deadline,
        ToolSurface.Looping surface,
        CancellationToken cancellationToken)
    {
        var transcript = new List<ModelTurn>();
        var calls = new List<TurnCall>();

        for (var turn = 1; turn <= surface.MaxTurns; turn++)
        {
            // BETWEEN turns, before spending another completion. A leg whose wall went while a tool was
            // working has nothing left to think with, and asking anyway buys an answer generated under no
            // budget — measuring the ceiling and calling it a score. It is the same check the single-shot
            // path already makes between retrieval and the ask, arriving where it matters most.
            if (deadline.Exhausted(clock.GetUtcNow()))
            {
                return Outcome<ToolLoopResult>.Failure(
                    $"the leg spent its {deadline.Describe} wall budget after {turn - 1} turn(s) "
                    + $"and {calls.Count} tool call(s)");
            }

            // A SNAPSHOT, not the list itself. The request is a value describing what was sent, and handing
            // over the growing list would let it change after the send — so anything that records a request
            // (the operator's "show me the prompts", first of all) would render the whole conversation as
            // if every turn had carried it. Found by its own test.
            var asked = await runtime.AskAsync(
                ModelRequest.OfTurn(
                    endpoint, sampling, doctrine, prompt,
                    deadline.ForCall(clock.GetUtcNow()),
                    surface.Tools,
                    [.. transcript]),
                cancellationToken);

            if (asked is Outcome<ModelAnswer>.Fail failed)
            {
                return Outcome<ToolLoopResult>.Failure(failed.Reason);
            }

            var answer = ((Outcome<ModelAnswer>.Ok)asked).Value;

            if (answer.IsFinal)
            {
                return Outcome<ToolLoopResult>.Success(
                    new ToolLoopResult(answer, transcript, calls, turn, LoopEnd.Answered));
            }

            transcript.Add(new ModelTurn.Assistant(answer.Text.WasCaptured ? answer.Text.Value : string.Empty, answer.ToolCalls));

            foreach (var requested in answer.ToolCalls)
            {
                var (record, result) = await InvokeAsync(surface.Engine, requested, turn, cancellationToken);
                calls.Add(record);
                transcript.Add(result);
            }

            // The ceiling is checked AFTER the calls of the last permitted turn, not before them: a model
            // that asked for a tool on its final turn should have that call recorded, because "it was still
            // working when the ceiling arrived" is exactly what the cap is reporting.
            if (turn == surface.MaxTurns)
            {
                logger.LogInformation(
                    "A leg spent its {Turns}-turn ceiling with {Calls} tool call(s) made", surface.MaxTurns, calls.Count);

                return Outcome<ToolLoopResult>.Success(
                    new ToolLoopResult(answer, transcript, calls, turn, LoopEnd.TurnsSpent));
            }
        }

        // Unreachable: MaxTurns is refused below 1 by the lane definition, so the loop always runs at least
        // once and always returns from inside it. Stated rather than left to a default value.
        return Outcome<ToolLoopResult>.Failure(
            $"a surface with {surface.MaxTurns} turns ran no turn at all");
    }

    /// <summary>
    /// One invocation, recorded by its outcome.
    ///
    /// <para>The engine's own contract makes a refusal a VALUE, so nothing here catches an expected failure.
    /// What it does do is keep the two facts apart: the model sees the reason as content — it has to, to
    /// correct itself — while the ledger records whether the tool refused, tried and could not, or
    /// answered.</para>
    /// </summary>
    private static async Task<(TurnCall Record, ModelTurn.ToolResult Result)> InvokeAsync(
        IEngine engine, RequestedToolCall requested, int turn, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var answer = await engine.InvokeAsync(requested.Name, requested.ArgumentsJson, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(started);

        var record = new TurnCall(
            turn,
            Bench.Domain.Trace.ToolCall.Of(requested.Name, requested.ArgumentsJson, answer, elapsed));

        return (record, new ModelTurn.ToolResult(requested.Id, requested.Name, answer.Text, answer.WasRefused));
    }


}
