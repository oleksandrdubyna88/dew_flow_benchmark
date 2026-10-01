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
    /// <item><b>the probes</b> (S2 of the question-consultant plan, flags measured on this machine 2026-10-01): web search
    /// ON is codex's TOP-LEVEL <c>--search</c> — <c>codex exec --search</c> exits 2 — placed BEFORE <c>exec</c>; on claude it
    /// is the two web tools NOT in the deny list, and OFF puts <c>WebSearch WebFetch</c> there; agy as it is for ON and has no
    /// flag for OFF. A directory grant is <c>--add-dir</c> on all three. JSON events are claude <c>--output-format json</c>,
    /// codex <c>--json</c>, agy <c>--output-format stream-json</c>. agy's read-only launch is <c>--mode plan</c>.</item>
    /// </list>
    /// An option the CLI has no flag for is REFUSED by name — claude has no output schema and no last-message file,
    /// codex has no tool deny-list and no turn ceiling, agy has neither a deny-list nor a web-off switch, gemini has none of
    /// the probe options: a guarantee silently not applied is one the caller goes on believing.</summary>
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
                ModelRuntimeKind.CliAntigravity => Outcome<IReadOnlyList<string>>.Success(Antigravity(modelId, options)),
                _ => Plain(runtime, modelId),
            };
    }

    private static IReadOnlyList<string> Codex(string modelId, AgentAskOptions options, IReadOnlyList<string> servers) =>
    [
        // Top-level, BEFORE the subcommand: `codex exec --search` exits 2 (codex-cli 0.156.1, 2026-10-01).
        .. options.WebSearch == AgentWebSearch.On ? ["--search"] : Array.Empty<string>(),
        "exec",
        .. options.Sandbox == AgentSandbox.ReadOnly ? ["-s", "read-only"] : Array.Empty<string>(),
        "--skip-git-repo-check", "--color", "never",
        .. options.JsonEvents ? ["--json"] : Array.Empty<string>(),
        .. Grants(options),
        .. options.OutputSchemaFile.Length > 0 ? ["--output-schema", options.OutputSchemaFile] : Array.Empty<string>(),
        .. options.LastMessageFile.Length > 0 ? ["-o", options.LastMessageFile] : Array.Empty<string>(),
        "-m", modelId,
        .. options.McpServersOff ? servers.SelectMany(name => new[] { "-c", $"mcp_servers.{name}.enabled=false" }) : [],
        "-",
    ];

    private static IReadOnlyList<string> Claude(string modelId, AgentAskOptions options) =>
    [
        "-p", "--model", modelId,
        .. options.JsonEvents ? ["--output-format", "json"] : Array.Empty<string>(),
        .. options.Sandbox == AgentSandbox.ReadOnly ? ["--permission-mode", "plan"] : Array.Empty<string>(),
        .. Grants(options),
        .. ClaudeDenied(options) is { Count: > 0 } denied ? ["--disallowedTools", .. denied] : Array.Empty<string>(),
        .. options.MaxTurns > 0 ? ["--max-turns", options.MaxTurns.ToString(System.Globalization.CultureInfo.InvariantCulture)] : Array.Empty<string>(),
        .. options.McpServersOff ? ["--strict-mcp-config"] : Array.Empty<string>(),
    ];

    /// <summary>agy 1.2.14: <c>--print</c> answers once from stdin; <c>--output-format stream-json</c> is its event transcript;
    /// <c>--mode plan</c> is its no-edits mode; <c>--add-dir</c> is repeatable. Measured as flags that EXIST (2026-10-01); what
    /// each does on a live run is S5's first-cell hand-check.</summary>
    private static IReadOnlyList<string> Antigravity(string modelId, AgentAskOptions options) =>
    [
        "--print", "--model", modelId,
        .. options.JsonEvents ? ["--output-format", "stream-json"] : Array.Empty<string>(),
        .. options.Sandbox == AgentSandbox.ReadOnly ? ["--mode", "plan"] : Array.Empty<string>(),
        .. Grants(options),
    ];

    /// <summary>Claude's deny list: the caller's tools, then — for web OFF — the two web tools, each once.</summary>
    private static IReadOnlyList<string> ClaudeDenied(AgentAskOptions options) =>
        [.. options.DisallowedTools.Concat(options.WebSearch == AgentWebSearch.Off ? ["WebSearch", "WebFetch"] : []).Distinct(StringComparer.Ordinal)];

    private static IEnumerable<string> Grants(AgentAskOptions options) => options.AddDirectories.SelectMany(dir => new[] { "--add-dir", dir });

    private static string Unsupported(ModelRuntimeKind runtime, AgentAskOptions o)
    {
        ModelRuntimeKind[] probeClis = [ModelRuntimeKind.CliCodex, ModelRuntimeKind.CliClaude, ModelRuntimeKind.CliAntigravity];

        var asked = new (bool Asked, string Name, ModelRuntimeKind[] Honoured)[]
        {
            (o.Sandbox == AgentSandbox.ReadOnly, "a read-only sandbox", probeClis),
            (o.OutputSchemaFile.Length > 0, "an output schema", [ModelRuntimeKind.CliCodex]),
            (o.LastMessageFile.Length > 0, "a last-message file", [ModelRuntimeKind.CliCodex]),
            (o.DisallowedTools.Count > 0, "a tool deny-list", [ModelRuntimeKind.CliClaude]),
            (o.MaxTurns > 0, "a turn ceiling", [ModelRuntimeKind.CliClaude]),
            (o.McpServersOff, "MCP servers off", [ModelRuntimeKind.CliCodex, ModelRuntimeKind.CliClaude]),
            (o.WebSearch == AgentWebSearch.On, "web search on", probeClis),
            (o.WebSearch == AgentWebSearch.Off, "web search off", [ModelRuntimeKind.CliCodex, ModelRuntimeKind.CliClaude]),
            (o.AddDirectories.Count > 0, "a directory grant", probeClis),
            (o.JsonEvents, "JSON events", probeClis),
        };

        var unhonoured = asked.Where(a => a.Asked && !a.Honoured.Contains(runtime)).Select(a => a.Name).ToList();

        return unhonoured.Count == 0 || !probeClis.Contains(runtime) && runtime != ModelRuntimeKind.CliGemini
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

            // `--print` is agy's print mode (its `-p`), the prompt on stdin; `--model` pins the model. agy 1.2.14's
            // flags measured 2026-10-01; whether the pipe answers is S5's first live cell.
            ModelRuntimeKind.CliAntigravity => Outcome<IReadOnlyList<string>>.Success(["--print", "--model", modelId]),

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
public sealed class CliAgentRuntime(ILogger<CliAgentRuntime> logger) : ICliAgentRuntime, ICliAgentTranscripts
{
    public async Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken)
    {
        var launched = await LaunchAsync(ask, cancellationToken);

        if (launched is Outcome<Launched>.Fail refused)
        {
            return Outcome<AgentAnswer>.Failure(refused.Reason);
        }

        var (attempt, elapsed) = ((Outcome<Launched>.Ok)launched).Value;
        var answer = Read(attempt, ask, elapsed, LastMessage(ask));

        answer.Match(
            ok => 0,
            reason =>
            {
                logger.LogWarning("The {Runtime} agent did not answer: {Reason}", ask.Runtime, reason);
                return 0;
            });

        return answer;
    }

    /// <summary>The probes' reading (S2): the same launch, handed back whole — see <see cref="Transcript"/>.</summary>
    public async Task<Outcome<AgentTranscript>> TranscriptAsync(AgentAsk ask, CancellationToken cancellationToken) =>
        (await LaunchAsync(ask, cancellationToken)).Match(launched => Transcript(launched.Attempt, launched.Elapsed, ask), Outcome<AgentTranscript>.Failure);

    private sealed record Launched(ProcessAttempt Attempt, TimeSpan Elapsed);

    /// <summary>What both readings share: the prompt check, the argv, the one launcher, the clock. A refusal here is one that
    /// stopped the launch — nothing was spent.</summary>
    private static async Task<Outcome<Launched>> LaunchAsync(AgentAsk ask, CancellationToken cancellationToken)
    {
        if (ask.Prompt.Trim().Length == 0)
        {
            return Outcome<Launched>.Failure(
                "an agent was asked an empty prompt — a launch that cannot produce an answer must not cost one");
        }

        var servers = ask.Runtime == ModelRuntimeKind.CliCodex && ask.Options.McpServersOff
            ? CodexMcpServers.Declared()
            : Outcome<IReadOnlyList<string>>.Success([]);

        if (servers is Outcome<IReadOnlyList<string>>.Fail unreadable)
        {
            return Outcome<Launched>.Failure(unreadable.Reason);
        }

        var argv = CliArgv.For(ask.Runtime, ask.ModelId, ask.Options, ((Outcome<IReadOnlyList<string>>.Ok)servers).Value);

        if (argv is Outcome<IReadOnlyList<string>>.Fail wrongKind)
        {
            return Outcome<Launched>.Failure(wrongKind.Reason);
        }

        var clock = Stopwatch.StartNew();

        var attempt = await ProcessRunner.RunAsync(
            ask.Executable,
            ((Outcome<IReadOnlyList<string>>.Ok)argv).Value,
            ask.WorkingDirectory,
            ask.Wall,
            ask.Prompt,
            cancellationToken);

        return Outcome<Launched>.Success(new Launched(attempt, clock.Elapsed));
    }

    /// <summary>One <see cref="ProcessAttempt"/> as a transcript: a completed run keeps its exit code and both pipes; a run
    /// the wall ended keeps both pipes and no exit code; an executable that is not there is the one refusal — a configuration
    /// fact, as <see cref="Read(ProcessAttempt, AgentAsk, TimeSpan)"/> reads it.</summary>
    public static Outcome<AgentTranscript> Transcript(ProcessAttempt attempt, TimeSpan elapsed, AgentAsk ask) =>
        attempt switch
        {
            ProcessAttempt.NotFound missing => Outcome<AgentTranscript>.Failure(
                $"'{missing.Executable}' is not installed on this machine — the registry's reference resolved to "
                + "a path nothing is at, which is a configuration fact rather than an agent's failure"),

            ProcessAttempt.TimedOut cap => Outcome<AgentTranscript>.Success(new AgentTranscript(
                Bench.Domain.Trace.CapturedCount.Unavailable($"the wall of {cap.Budget.TotalSeconds:0.#}s ended the process"), true, cap.StandardOutput, cap.StandardError, elapsed)),

            ProcessAttempt.Completed done => Outcome<AgentTranscript>.Success(new AgentTranscript(
                Bench.Domain.Trace.CapturedCount.Number(done.Result.ExitCode), false, done.Result.StandardOutput, done.Result.StandardError, elapsed)),

            _ => Outcome<AgentTranscript>.Failure($"the {ask.Runtime} agent produced an attempt this build cannot read"),
        };

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
