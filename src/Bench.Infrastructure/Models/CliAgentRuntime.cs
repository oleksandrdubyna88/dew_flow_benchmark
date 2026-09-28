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
        For(runtime, modelId, AgentAskOptions.None, []);

    /// <summary>The same argv, widened by <paramref name="options"/> — the blinded assessor's launch (E4). With
    /// <see cref="AgentAskOptions.None"/> it is exactly the argv above, so no existing caller's launch changes.
    /// <list type="bullet">
    /// <item><b>codex</b>: <c>exec -s read-only --skip-git-repo-check --color never --output-schema &lt;schema&gt; -o &lt;out&gt;
    /// -m &lt;model&gt; [-c mcp_servers.&lt;name&gt;.enabled=false …] -</c> — the calibration's <c>assess.py</c> launch;
    /// <paramref name="codexMcpServers"/> are the server names this machine's codex config declares.</item>
    /// <item><b>claude</b>: <c>-p --model &lt;model&gt; --permission-mode plan --disallowedTools Edit Write NotebookEdit
    /// --max-turns 1 [--strict-mcp-config]</c> — print mode, plan mode (no edits), the write tools taken away, and with no
    /// <c>--mcp-config</c> beside it, <c>--strict-mcp-config</c> loads no MCP server at all.</item>
    /// </list>
    /// An option the CLI has no flag for is REFUSED by name — claude has no output schema and no last-message file,
    /// codex has no tool deny-list and no turn ceiling, gemini has none of them: a guarantee silently not applied is one
    /// the caller goes on believing.</summary>
    public static Outcome<IReadOnlyList<string>> For(
        ModelRuntimeKind runtime, string modelId, AgentAskOptions options, IReadOnlyList<string> codexMcpServers)
    {
        var refusal = Unsupported(runtime, options);

        if (refusal.Length > 0)
        {
            return Outcome<IReadOnlyList<string>>.Failure(refusal);
        }

        return options.IsNone
            ? Plain(runtime, modelId)
            : runtime switch
            {
                ModelRuntimeKind.CliCodex => Outcome<IReadOnlyList<string>>.Success(Codex(modelId, options, codexMcpServers)),
                ModelRuntimeKind.CliClaude => Outcome<IReadOnlyList<string>>.Success(Claude(modelId, options)),
                _ => Plain(runtime, modelId),
            };
    }

    private static IReadOnlyList<string> Codex(string modelId, AgentAskOptions options, IReadOnlyList<string> servers) =>
    [
        "exec",
        .. options.Sandbox == AgentSandbox.ReadOnly ? ["-s", "read-only"] : Array.Empty<string>(),
        "--skip-git-repo-check", "--color", "never",
        .. options.OutputSchemaFile.Length > 0 ? ["--output-schema", options.OutputSchemaFile] : Array.Empty<string>(),
        .. options.LastMessageFile.Length > 0 ? ["-o", options.LastMessageFile] : Array.Empty<string>(),
        "-m", modelId,
        .. options.McpServersOff ? servers.SelectMany(name => new[] { "-c", $"mcp_servers.{name}.enabled=false" }) : [],
        "-",
    ];

    private static IReadOnlyList<string> Claude(string modelId, AgentAskOptions options) =>
    [
        "-p", "--model", modelId,
        .. options.Sandbox == AgentSandbox.ReadOnly ? ["--permission-mode", "plan"] : Array.Empty<string>(),
        .. options.DisallowedTools.Count > 0 ? ["--disallowedTools", .. options.DisallowedTools] : Array.Empty<string>(),
        .. options.MaxTurns > 0 ? ["--max-turns", options.MaxTurns.ToString(System.Globalization.CultureInfo.InvariantCulture)] : Array.Empty<string>(),
        .. options.McpServersOff ? ["--strict-mcp-config"] : Array.Empty<string>(),
    ];

    private static string Unsupported(ModelRuntimeKind runtime, AgentAskOptions o)
    {
        var asked = new (bool Asked, string Name, ModelRuntimeKind[] Honoured)[]
        {
            (o.Sandbox == AgentSandbox.ReadOnly, "a read-only sandbox", [ModelRuntimeKind.CliCodex, ModelRuntimeKind.CliClaude]),
            (o.OutputSchemaFile.Length > 0, "an output schema", [ModelRuntimeKind.CliCodex]),
            (o.LastMessageFile.Length > 0, "a last-message file", [ModelRuntimeKind.CliCodex]),
            (o.DisallowedTools.Count > 0, "a tool deny-list", [ModelRuntimeKind.CliClaude]),
            (o.MaxTurns > 0, "a turn ceiling", [ModelRuntimeKind.CliClaude]),
            (o.McpServersOff, "MCP servers off", [ModelRuntimeKind.CliCodex, ModelRuntimeKind.CliClaude]),
        };

        var unhonoured = asked.Where(a => a.Asked && !a.Honoured.Contains(runtime)).Select(a => a.Name).ToList();

        return unhonoured.Count == 0
            ? string.Empty
            : $"{runtime} has no flag for {string.Join(", ", unhonoured)} — refused rather than launched without it, because a guarantee "
              + "silently not applied is one the caller goes on believing";
    }

    private static Outcome<IReadOnlyList<string>> Plain(ModelRuntimeKind runtime, string modelId) =>
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

        var servers = ask.Runtime == ModelRuntimeKind.CliCodex && ask.Options.McpServersOff
            ? CodexMcpServers.Declared()
            : Outcome<IReadOnlyList<string>>.Success([]);

        if (servers is Outcome<IReadOnlyList<string>>.Fail unreadable)
        {
            return Outcome<AgentAnswer>.Failure(unreadable.Reason);
        }

        var argv = CliArgv.For(ask.Runtime, ask.ModelId, ask.Options, ((Outcome<IReadOnlyList<string>>.Ok)servers).Value);

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

        var answer = Read(attempt, ask, clock.Elapsed, LastMessage(ask));

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
        Read(attempt, ask, elapsed, string.Empty);

    /// <summary>The same reading, for a launch that asked the CLI to write its final message to a FILE
    /// (<see cref="AgentAskOptions.LastMessageFile"/>, codex <c>-o</c>): a clean exit is then answered by the file, and a
    /// clean exit that left the file empty or absent is a refusal naming it — whatever stdout carried, because with
    /// <c>-o</c> stdout is progress, not the answer.</summary>
    public static Outcome<AgentAnswer> Read(ProcessAttempt attempt, AgentAsk ask, TimeSpan elapsed, string lastMessage) =>
        attempt switch
        {
            ProcessAttempt.Completed { Result.Ok: true } when ask.Options.LastMessageFile.Length > 0 => lastMessage.Trim().Length > 0
                ? Outcome<AgentAnswer>.Success(new AgentAnswer(lastMessage, elapsed, System.Text.Encoding.UTF8.GetByteCount(lastMessage)))
                : Outcome<AgentAnswer>.Failure(
                    $"the {ask.Runtime} agent exited 0 and wrote no final message to {Path.GetFileName(ask.Options.LastMessageFile)}"),

            ProcessAttempt.NotFound missing => Outcome<AgentAnswer>.Failure(
                $"'{missing.Executable}' is not installed on this machine — the registry's reference resolved to "
                + "a path nothing is at, which is a configuration fact rather than an agent's failure"),

            ProcessAttempt.TimedOut cap => Outcome<AgentAnswer>.Failure(
                $"the {ask.Runtime} agent did not answer within {cap.Budget.TotalSeconds:0.#}s"
                + (cap.Output.Length > 0 ? $" — it had printed: {Last(cap.Output)}" : " and printed nothing")),

            // The TAIL, not the head. A CLI's output opens with banners — "True color (24-bit) support not
            // detected", a workspace notice, a version line — and the reason it failed is the last thing it said.
            // Read from the front, a gemini failure reported nothing but a colour warning three times.
            ProcessAttempt.Completed { Result.Ok: false } failed => Outcome<AgentAnswer>.Failure(
                $"the {ask.Runtime} agent exited {failed.Result.ExitCode}: {Last(failed.Result.Output)}"),

            ProcessAttempt.Completed done when done.Result.StandardOutput.Length == 0 =>
                Outcome<AgentAnswer>.Failure(
                    $"the {ask.Runtime} agent exited 0 and printed nothing on stdout — an empty answer stored as "
                    + "a candidate would be a question nobody wrote"
                    + (done.Result.Output.Length > 0 ? $". It did say: {Last(done.Result.Output)}" : string.Empty)),

            // STDOUT alone, never the merged text. Found live: the Claude CLI prints a workspace-trust warning
            // beside its answer, and a merged reading therefore begins with prose — which the JSON parser then
            // refuses, correctly, having been handed something that is not the agent's answer.
            ProcessAttempt.Completed done => Outcome<AgentAnswer>.Success(new AgentAnswer(
                done.Result.StandardOutput,
                elapsed,
                System.Text.Encoding.UTF8.GetByteCount(done.Result.StandardOutput))),

            _ => Outcome<AgentAnswer>.Failure($"the {ask.Runtime} agent produced an attempt this build cannot read"),
        };

    /// <summary>The final message a launch asked to be written to a file, or empty when none was asked or none was written.</summary>
    private static string LastMessage(AgentAsk ask)
    {
        var file = ask.Options.LastMessageFile;

        try
        {
            return file.Length > 0 && File.Exists(file) ? File.ReadAllText(file) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>The END of what a process said, which is where the reason lives — the beginning is banners.
    /// <para>
    /// ONE helper, for all three "what did it say" sites. There used to be a second that quoted the FIRST 300
    /// characters, and a gemini failure reported nothing but "True color (24-bit) support not detected" three
    /// runs in a row while its real reason sat at the end. A timeout has the same shape and had kept the head
    /// reading: what a hung agent printed last is what says where it hung.
    /// </para></summary>
    private static string Last(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= 400 ? trimmed : "…" + trimmed[^400..];
    }
}
