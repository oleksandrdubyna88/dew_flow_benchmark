using System.Diagnostics;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Registry;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Models;

/// <summary>What argv puts each CLI agent into headless mode.
/// <para>
/// A switch over the runtime kind rather than a string in configuration, and the difference is that a switch
/// FAILS AT A COMPILER ERROR when a kind is added. A configured flag string would be a knob nobody can
/// validate: wrong, it produces an interactive session that waits on a terminal nobody is watching, and the
/// symptom is a timeout rather than a message.
/// </para>
/// <para>
/// <b>Only <c>claude</c> has been exercised</b> (operator decision 2026-08-17: three authors are the design,
/// one is the first measurement). The other two argv shapes are written from their documented headless flags
/// and are <b>unverified</b> — stated here rather than discovered by somebody trusting them, and the live
/// test that would verify each one is the same test, pointed at a different reference.
/// </para></summary>
public static class CliArgv
{
    /// <summary>The argv for a kind, or a refusal for a kind that is not a CLI at all.</summary>
    public static Outcome<IReadOnlyList<string>> For(ModelRuntimeKind runtime) =>
        runtime switch
        {
            // `-p` is Claude Code's print mode: it answers once and exits, reading the prompt from stdin when
            // none is given as an argument. Verified against 2.1.216.
            ModelRuntimeKind.CliClaude => Outcome<IReadOnlyList<string>>.Success(["-p"]),

            // UNVERIFIED — from documented headless usage, never run here.
            ModelRuntimeKind.CliCodex => Outcome<IReadOnlyList<string>>.Success(["exec", "-"]),
            ModelRuntimeKind.CliGemini => Outcome<IReadOnlyList<string>>.Success(["-p"]),

            _ => Outcome<IReadOnlyList<string>>.Failure(
                $"{runtime} is not a CLI agent — it is answered over HTTP, and asking it to author a question "
                + "by launching a process would launch nothing"),
        };
}

/// <summary>A CLI coding agent, launched once per question.
/// <para>
/// Over <see cref="ProcessRunner"/>, which is the family's one sanctioned launcher: exe + argv, never a shell
/// string. That matters more here than anywhere else in this repository — this pipeline is handed repository
/// paths and question text, and text concatenated into a shell command is arbitrary code execution wearing a
/// prompt.
/// </para></summary>
public sealed class CliAgentRuntime(ILogger<CliAgentRuntime> logger) : ICliAgentRuntime
{
    public async Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken)
    {
        if (ask.Prompt.Trim().Length == 0)
        {
            return Outcome<AgentAnswer>.Failure(
                "an agent was asked an empty prompt — a launch that cannot produce an answer must not cost one");
        }

        var argv = CliArgv.For(ask.Runtime);

        if (argv is Outcome<IReadOnlyList<string>>.Fail wrongKind)
        {
            return Outcome<AgentAnswer>.Failure(wrongKind.Reason);
        }

        var clock = Stopwatch.StartNew();

        var attempt = await ProcessRunner.RunAsync(
            ask.Executable,
            ((Outcome<IReadOnlyList<string>>.Ok)argv).Value,
            ask.WorkingDirectory,
            ask.Wall,
            ask.Prompt,
            cancellationToken);

        var answer = Read(attempt, ask, clock.Elapsed);

        answer.Match(
            ok => 0,
            reason =>
            {
                logger.LogWarning("The {Runtime} agent did not answer: {Reason}", ask.Runtime, reason);
                return 0;
            });

        return answer;
    }

    /// <summary>One <see cref="ProcessAttempt"/> as an answer or a named refusal.
    /// <para>
    /// Pure, and separate from the launch, because these four readings are the whole logic worth asserting and
    /// a test of them needs no process at all. The one that matters is the last: an agent that exits ZERO and
    /// prints nothing is a refusal, not an empty answer — an empty answer stored as a candidate would be a
    /// question nobody wrote.
    /// </para></summary>
    public static Outcome<AgentAnswer> Read(ProcessAttempt attempt, AgentAsk ask, TimeSpan elapsed) =>
        attempt switch
        {
            ProcessAttempt.NotFound missing => Outcome<AgentAnswer>.Failure(
                $"'{missing.Executable}' is not installed on this machine — the registry's reference resolved to "
                + "a path nothing is at, which is a configuration fact rather than an agent's failure"),

            ProcessAttempt.TimedOut cap => Outcome<AgentAnswer>.Failure(
                $"the {ask.Runtime} agent did not answer within {cap.Budget.TotalSeconds:0.#}s"
                + (cap.Output.Length > 0 ? $" — it had printed: {Short(cap.Output)}" : " and printed nothing")),

            ProcessAttempt.Completed { Result.Ok: false } failed => Outcome<AgentAnswer>.Failure(
                $"the {ask.Runtime} agent exited {failed.Result.ExitCode}: {Short(failed.Result.Output)}"),

            ProcessAttempt.Completed done when done.Result.Output.Trim().Length == 0 =>
                Outcome<AgentAnswer>.Failure(
                    $"the {ask.Runtime} agent exited 0 and printed nothing — an empty answer stored as a "
                    + "candidate would be a question nobody wrote"),

            ProcessAttempt.Completed done => Outcome<AgentAnswer>.Success(new AgentAnswer(
                done.Result.Output.Trim(),
                elapsed,
                System.Text.Encoding.UTF8.GetByteCount(done.Result.Output))),

            _ => Outcome<AgentAnswer>.Failure($"the {ask.Runtime} agent produced an attempt this build cannot read"),
        };

    private static string Short(string text) =>
        text.Length <= 300 ? text.Trim() : text[..300].Trim() + "…";
}
