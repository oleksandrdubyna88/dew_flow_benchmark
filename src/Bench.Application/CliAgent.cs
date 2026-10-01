using Bench.Domain;
using Bench.Domain.Registry;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>One question put to a CLI agent.</summary>
/// <param name="Runtime">Which CLI this is, so the adapter can build the argv that puts THAT one in headless
/// mode. <c>claude -p</c> is not <c>codex exec</c> is not <c>gemini -p</c>, and a single flag string in
/// configuration would be a knob nobody can validate.</param>
/// <param name="Executable">Resolved on THIS machine from the registry's reference — the registry stores the
/// name of an environment variable, never a path, because its database is published unedited.</param>
/// <param name="Prompt">Sent on STDIN. A CLI agent's prompt runs to kilobytes and an argument list has a
/// platform maximum, so argv would fail on the machine with the biggest target repository.</param>
/// <param name="Wall">Every wait has a ceiling. An agent that hangs must cost one recorded refusal rather
/// than an authoring run of six groups.</param>
/// <param name="ModelId">Which model to pin the CLI to. Never left to the CLI's default: Gemini's default is
/// <c>Auto</c>, which routes per call, so a batch left on it would be written by whichever model the router
/// picked question by question while the bank recorded one fixed string — and <c>AuthorModel</c> is the fact the
/// whole authoring design rests on.</param>
public sealed record AgentAsk(
    ModelRuntimeKind Runtime,
    string Executable,
    string Prompt,
    string WorkingDirectory,
    TimeSpan Wall,
    string ModelId = "")
{
    /// <summary>What else the launch must guarantee — a read-only sandbox, an output schema, tools taken away, MCP
    /// servers off. <see cref="AgentAskOptions.None"/> for every caller that asked for none, so their argv is unchanged.</summary>
    public AgentAskOptions Options { get; init; } = AgentAskOptions.None;
}

/// <summary>How far a CLI agent's own sandbox is opened.</summary>
public enum AgentSandbox
{
    /// <summary>Whatever the CLI does by default — the authoring and review passes' launch.</summary>
    Default,

    /// <summary>It may read, and it may not write, anywhere: the blinded assessor's launch.</summary>
    ReadOnly,
}

/// <summary>The guarantees a launch asks the CLI for, beyond "answer once": the blinded assessor's needs, spelled per
/// CLI by <c>CliArgv</c>. An option a CLI cannot honour is REFUSED there by name, never dropped — a sandbox that is
/// silently not applied is a sandbox the caller believes in.</summary>
public sealed record AgentAskOptions
{
    public static AgentAskOptions None { get; } = new();

    public AgentSandbox Sandbox { get; init; } = AgentSandbox.Default;

    /// <summary>A JSON Schema file the CLI enforces on its final message (codex <c>--output-schema</c>).</summary>
    public string OutputSchemaFile { get; init; } = string.Empty;

    /// <summary>Where the CLI writes its final message (codex <c>-o</c>); the answer is then read from this file.</summary>
    public string LastMessageFile { get; init; } = string.Empty;

    /// <summary>Tools the agent may not call (claude <c>--disallowedTools</c>).</summary>
    public IReadOnlyList<string> DisallowedTools { get; init; } = [];

    /// <summary>Every MCP server the CLI would load is switched off for this launch.</summary>
    public bool McpServersOff { get; init; }

    /// <summary>A ceiling on agent turns (claude <c>--max-turns</c>); zero is none.</summary>
    public int MaxTurns { get; init; }

    /// <summary>Whether the CLI may search the web — left as the CLI has it, switched ON, or switched OFF (the probes, S2).</summary>
    public AgentWebSearch WebSearch { get; init; } = AgentWebSearch.Default;

    /// <summary>Directories the CLI is granted beyond its working directory (<c>--add-dir</c> on claude, codex and agy).</summary>
    public IReadOnlyList<string> AddDirectories { get; init; } = [];

    /// <summary>The CLI prints its machine-readable transcript — claude <c>--output-format stream-json --verbose</c> (the <c>json</c>
    /// envelope is blind to the tools, S2b), codex <c>--json</c>, agy <c>--output-format stream-json</c> — so tool evidence can be read off it.</summary>
    public bool JsonEvents { get; init; }

    /// <summary>The ONLY built-in tools the CLI may offer (claude <c>--tools</c>; a named-but-empty list is <c>--tools ""</c>, nothing
    /// offered). An allow-list is a confinement in itself: web OFF under one is the web tools' absence, not a deny entry (S2b).</summary>
    public AgentToolAllowlist AllowedTools { get; init; } = AgentToolAllowlist.NotAsked;

    /// <summary>claude <c>--restricted</c>: the code-running tools and WebFetch removed unless <see cref="AllowedTools"/> names them, the
    /// file tools confined to the working directories, the user's settings files ignored (S2b).</summary>
    public bool Restricted { get; init; }

    /// <summary>Nothing asked beyond "answer once" — the launch every caller before the assessor made.</summary>
    public bool IsNone =>
        Sandbox == AgentSandbox.Default && OutputSchemaFile.Length == 0 && LastMessageFile.Length == 0
        && DisallowedTools.Count == 0 && !McpServersOff && MaxTurns == 0
        && WebSearch == AgentWebSearch.Default && AddDirectories.Count == 0 && !JsonEvents
        && !AllowedTools.Asked && !Restricted;
}

/// <summary>A tool allow-list as a launch option: not asked (the CLI's default set), or ONLY these names — where an empty list is
/// a real request ("offer nothing"), which is why this is not a bare list. Equal by content, so two launches built the same way
/// compare equal.</summary>
public sealed record AgentToolAllowlist
{
    private AgentToolAllowlist(bool asked, IReadOnlyList<string> names)
    {
        Asked = asked;
        Names = names;
    }

    public static AgentToolAllowlist NotAsked { get; } = new(false, []);

    public static AgentToolAllowlist Only(IReadOnlyList<string> names) => new(true, [.. names]);

    public bool Asked { get; }

    public IReadOnlyList<string> Names { get; }

    public bool Equals(AgentToolAllowlist? other) =>
        other is not null && Asked == other.Asked && Names.SequenceEqual(other.Names, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Asked, Names.Count);

    public override string ToString() => Asked ? $"only [{string.Join(' ', Names)}]" : "not asked";
}

/// <summary>Whether a launch may reach the web. <see cref="Default"/> asks nothing — every caller before the probes.</summary>
public enum AgentWebSearch
{
    Default,

    /// <summary>The CLI may search: codex <c>--search</c> (top-level, before <c>exec</c>); claude with <c>WebSearch</c>/<c>WebFetch</c>
    /// NOT in its deny list; agy as it is.</summary>
    On,

    /// <summary>The CLI may not: claude with <c>WebSearch</c> and <c>WebFetch</c> denied; codex without <c>--search</c> (its default);
    /// agy has no flag for it and is refused.</summary>
    Off,
}

/// <param name="ResponseBytes">What the agent actually printed. Recorded for the same reason a leg records
/// its response size: it is the only honest per-call measure of what a batch cost to produce.</param>
public sealed record AgentAnswer(string Text, TimeSpan Elapsed, long ResponseBytes);

/// <summary>Everything one CLI launch produced, for a caller that reads the TRANSCRIPT rather than an answer (the probes,
/// S2): the exit code — <i>not captured</i> when the wall ended the process —, whether it did, stdout and stderr APART (a
/// quota marker and a usage error arrive on stderr; a grammar reads stdout and must not meet them), and the time it took.</summary>
public sealed record AgentTranscript(CapturedCount ExitCode, bool TimedOut, string Stdout, string Stderr, TimeSpan Elapsed)
{
    /// <summary>The exact argv the CLI was launched with (S2b, finding 6) — the launcher's own record, committed as <c>argv.json</c>
    /// beside the transcript before anything is parsed, so a faulted reading can still be replayed by hand.</summary>
    public IReadOnlyList<string> Argv { get; init; } = [];
}

/// <summary>The same launch as <see cref="ICliAgentRuntime"/> — one argv, the prompt on stdin, the wall — read whole rather
/// than as an answer. A refusal here is only what stopped the LAUNCH: an empty prompt, an option the CLI has no flag for, a
/// runtime that is no CLI, an executable that is not there. Everything the CLI then did — a usage error, a crash, a quota
/// marker, a timeout — comes back as a transcript, because for a probe those are the facts being measured.</summary>
public interface ICliAgentTranscripts
{
    Task<Outcome<AgentTranscript>> TranscriptAsync(AgentAsk ask, CancellationToken cancellationToken);
}

/// <summary>A CLI coding agent, asked once and read once.
/// <para>
/// <b>Not <see cref="IModelRuntime"/>, and not a subject.</b> That port is a completion endpoint measured as
/// a subject; this one launches a process to do WORK FOR the harness — authoring questions, reviewing them.
/// The same executable will later be measured as a subject through a different port with turn ceilings and
/// per-leg telemetry (`todo/PLAN_tool_benchmark.md` step 11), and conflating the two would make the
/// measurement of an agent indistinguishable from the harness's own use of one.
/// </para>
/// <para>
/// Every failure is a VALUE. An executable that is not installed, a CLI that exits non-zero, an answer that
/// outlives its wall: all facts a batch records and continues past. One agent's bad afternoon must not end a
/// run over six groups of a hundred questions.
/// </para></summary>
public interface ICliAgentRuntime
{
    Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken);
}
