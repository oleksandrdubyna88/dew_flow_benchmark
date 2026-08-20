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
/// <param name="Exhausted">The ceiling ended it, not the model. Settles as a CAP, never as a wrong
/// answer.</param>
public sealed record ToolLoopResult(
    ModelAnswer Answer,
    IReadOnlyList<ModelTurn> Transcript,
    IReadOnlyList<TurnCall> Calls,
    int TurnsSpent,
    bool Exhausted);

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
public sealed class ToolLoopRunner(IModelRuntime runtime, ILogger<ToolLoopRunner> logger)
{
    public async Task<Outcome<ToolLoopResult>> RunAsync(
        ModelEndpoint endpoint,
        Sampling sampling,
        string doctrine,
        string prompt,
        IReadOnlyList<Budget> budgets,
        ToolSurface.Looping surface,
        CancellationToken cancellationToken)
    {
        var transcript = new List<ModelTurn>();
        var calls = new List<TurnCall>();

        for (var turn = 1; turn <= surface.MaxTurns; turn++)
        {
            // A SNAPSHOT, not the list itself. The request is a value describing what was sent, and handing
            // over the growing list would let it change after the send — so anything that records a request
            // (the operator's "show me the prompts", first of all) would render the whole conversation as
            // if every turn had carried it. Found by its own test.
            var asked = await runtime.AskAsync(
                ModelRequest.OfTurn(endpoint, sampling, doctrine, prompt, budgets, surface.Tools, [.. transcript]),
                cancellationToken);

            if (asked is Outcome<ModelAnswer>.Fail failed)
            {
                return Outcome<ToolLoopResult>.Failure(failed.Reason);
            }

            var answer = ((Outcome<ModelAnswer>.Ok)asked).Value;

            if (answer.IsFinal)
            {
                return Outcome<ToolLoopResult>.Success(
                    new ToolLoopResult(answer, transcript, calls, turn, Exhausted: false));
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
                    new ToolLoopResult(answer, transcript, calls, turn, Exhausted: true));
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
            new Bench.Domain.Trace.ToolCall(
                requested.Name,
                requested.ArgumentsJson,
                answer.WasRefused,
                answer is ToolAnswer.Failed failure ? failure.Message : string.Empty,
                elapsed));

        return (record, new ModelTurn.ToolResult(requested.Id, requested.Name, answer.Text, answer.WasRefused));
    }
}
