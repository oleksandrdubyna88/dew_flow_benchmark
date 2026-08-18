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
/// <b>All three are now measured</b> (2026-08-18), each by piping a one-word prompt to the real CLI and reading
/// what came back on stdout. The three headless forms are NOT the same shape, and the one that was guessed from
/// documentation was wrong: <c>gemini -p</c> exits 1 and prints its help, because <c>-p</c> demands a value —
/// the prompt must arrive on stdin with no flag at all. That is exactly the failure this switch exists to make
/// impossible to guess at twice.
/// </para></summary>
public static class CliArgv
{
    /// <summary>The argv for a kind, PINNED to a model, or a refusal for a kind that is not a CLI at all.
    /// <para>
    /// <b>The model is pinned, never left to the CLI's default.</b> Measured 2026-08-18 and it is not a
    /// preference: Gemini's default is <c>Auto</c>, which routes per call — a one-word prompt went to
    /// <c>gemini-3.1-flash-lite</c> — so a batch left on Auto would be written by whichever model the router
    /// picked, question by question, while the bank recorded one fixed string. And Codex's real default here is
    /// <c>gpt-5.6-terra</c>, not the id this registry row first guessed. <c>AuthorModel</c> is the fact the whole
    /// authoring design rests on — a set's ceiling becomes its author's ceiling, and the eligibility rule
    /// compares that id — so it must name what actually answered.
    /// </para></summary>
    public static Outcome<IReadOnlyList<string>> For(ModelRuntimeKind runtime, string modelId) =>
        runtime switch
        {
            // `-p` is Claude Code's print mode: it answers once and exits, reading the prompt from stdin when
            // none is given as an argument. Verified against 2.1.216.
            ModelRuntimeKind.CliClaude => Outcome<IReadOnlyList<string>>.Success(["-p", "--model", modelId]),

            // `exec` is Codex's non-interactive subcommand and `-` means "read the prompt from stdin". Verified
            // 2026-08-18: exit 0, `ready` on stdout, wrapped in a preamble the JSON extractor already handles.
            ModelRuntimeKind.CliCodex => Outcome<IReadOnlyList<string>>.Success(["exec", "-m", modelId, "-"]),

            // No prompt FLAG: Gemini reads a piped prompt from stdin and answers once, while its `-p` takes the
            // prompt as a VALUE — passing it bare exits 1 with the help text. Verified 2026-08-18; stdout is
            // exactly the answer and its "true color"/"ripgrep" warnings go to stderr, which this runtime ignores.
            //
            // `--skip-trust` is the second thing this CLI needs and the second one measured rather than guessed:
            // without it every launch in a checked-out worktree exits 55 with "Gemini CLI is not running in a
            // trusted directory", and it wrote 0 questions in all four groups of the first three-author batch.
            // Gemini has its OWN trust gate, so the `~/.claude.json` pre-trust that fixed the Claude CLI does
            // nothing for it. Skipping is the right answer here for the same reason pre-trusting is: the tree is
            // one THIS harness created, at a commit it pinned, from a repository the operator named.
            ModelRuntimeKind.CliGemini => Outcome<IReadOnlyList<string>>.Success(["-m", modelId, "--skip-trust"]),

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

        var argv = CliArgv.For(ask.Runtime, ask.ModelId);

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

            ProcessAttempt.Completed done when done.Result.StandardOutput.Length == 0 =>
                Outcome<AgentAnswer>.Failure(
                    $"the {ask.Runtime} agent exited 0 and printed nothing on stdout — an empty answer stored as "
                    + "a candidate would be a question nobody wrote"
                    + (done.Result.Output.Length > 0 ? $". It did say: {Short(done.Result.Output)}" : string.Empty)),

            // STDOUT alone, never the merged text. Found live: the Claude CLI prints a workspace-trust warning
            // beside its answer, and a merged reading therefore begins with prose — which the JSON parser then
            // refuses, correctly, having been handed something that is not the agent's answer.
            ProcessAttempt.Completed done => Outcome<AgentAnswer>.Success(new AgentAnswer(
                done.Result.StandardOutput,
                elapsed,
                System.Text.Encoding.UTF8.GetByteCount(done.Result.StandardOutput))),

            _ => Outcome<AgentAnswer>.Failure($"the {ask.Runtime} agent produced an attempt this build cannot read"),
        };

    private static string Short(string text) =>
        text.Length <= 300 ? text.Trim() : text[..300].Trim() + "…";
}
