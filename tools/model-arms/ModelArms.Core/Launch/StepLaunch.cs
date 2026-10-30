using Bench.Application;
using Bench.Domain.Registry;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Launch;

/// <summary>Whether a step starts a session under an id the runner minted, or resumes the one it started (plan D9). A closed pair, so a
/// launch is never both.</summary>
public abstract record StepSession
{
    private StepSession()
    {
    }

    /// <summary>The attempt starts a session under this id (<c>--session-id</c>).</summary>
    public sealed record Fresh(Guid Id) : StepSession;

    /// <summary>The attempt continues the session started under this id (<c>--resume</c>).</summary>
    public sealed record Resume(Guid Id) : StepSession;
}

/// <summary>Everything one launch of a step needs that a decision can know: the executable the console resolved, the step's facts, the
/// arm's paths, the session, the working directory, the filled brief (or the resume notice), the wall this attempt may take and the
/// environment of the harness itself, which Core never reads.</summary>
public sealed record StepLaunchRequest(
    string ClaudeExecutable,
    StepSpec Spec,
    ToolSets Tools,
    ArmPaths Paths,
    StepSession Session,
    string WorkingDirectory,
    string Prompt,
    TimeSpan Wall,
    IReadOnlyDictionary<string, string> ParentEnvironment);

/// <summary>The launch boundary of a step (plan §4.4): the three modes' options and the ask that carries them to the one launcher. Pure — it
/// builds the values <c>CliArgv</c> and <c>ProcessRunner</c> consume and starts nothing. Both sinks stay <see cref="AgentOutputSink.None"/>:
/// the runner sets them (E2.S4) with <c>with</c>, because a sink is a file and Core opens none.</summary>
public static class StepLaunch
{
    private const string PwdVariable = "PWD";

    /// <summary>The tool that starts a subagent, under both names the CLI has used.</summary>
    public static readonly IReadOnlyList<string> SubagentTools = ["Agent", "Task"];

    /// <summary>The options of a step: stream-json events, MCP servers off and the arm's notes and work folders granted, whatever the mode;
    /// then the session; then the mode's permission mode, permission list, deny list and MCP config.</summary>
    public static AgentAskOptions Options(StepSpec spec, ToolSets tools, ArmPaths paths, StepSession session) =>
        Session(Delegation(ForMode(spec.Mode, tools, paths), spec.Subagents), session);

    /// <summary>The question put to claude for one attempt of a step.</summary>
    public static AgentAsk Ask(StepLaunchRequest request) =>
        new(ModelRuntimeKind.CliClaude, request.ClaudeExecutable, request.Prompt, request.WorkingDirectory, request.Wall, request.Spec.Model)
        {
            Options = Options(request.Spec, request.Tools, request.Paths, request.Session),
            Environment = AgentEnvironment.Only(ChildVariables(request)),
        };

    /// <summary><see cref="ArmChildEnvironment.Of"/> plus the one thing only a launch knows: <c>PWD</c> is the working directory. The shell ran
    /// claude as <c>(cd "$DIR" &amp;&amp; … claude …)</c>, so its child saw its own folder as <c>PWD</c> and no <c>OLDPWD</c>; a process started
    /// with a working directory does not update the variable, so the runner's own <c>PWD</c> and <c>OLDPWD</c> would otherwise ride along
    /// and show where the experiment was started from. This is the single named exception to "nothing else is added".</summary>
    private static IReadOnlyDictionary<string, string> ChildVariables(StepLaunchRequest request) =>
        new Dictionary<string, string>(
            ArmChildEnvironment.Of(request.ParentEnvironment, request.Paths).Where(variable => !IsShellsFolderTracking(variable.Key)),
            StringComparer.Ordinal)
        {
            [PwdVariable] = request.WorkingDirectory,
        };

    private static bool IsShellsFolderTracking(string name) =>
        name.Equals(PwdVariable, StringComparison.OrdinalIgnoreCase) || name.Equals("OLDPWD", StringComparison.OrdinalIgnoreCase);

    private static AgentAskOptions ForMode(StepMode mode, ToolSets tools, ArmPaths paths)
    {
        var common = new AgentAskOptions { JsonEvents = true, McpServersOff = true, AddDirectories = [paths.Notes, paths.Work] };

        return mode switch
        {
            StepMode.Write => common with
            {
                PermissionMode = AgentPermissionMode.AcceptEdits,
                PermittedTools = tools.Write,
                DisallowedTools = tools.Deny,
                McpConfigFile = paths.McpNone,
            },
            StepMode.Read => common with
            {
                PermissionMode = AgentPermissionMode.Default,
                PermittedTools = tools.Read,
                DisallowedTools = ["Edit", "Write", "NotebookEdit", .. tools.Deny],
                McpConfigFile = paths.McpNone,
            },
            StepMode.Gate => common with
            {
                PermissionMode = AgentPermissionMode.AcceptEdits,
                PermittedTools = [.. tools.Write, .. tools.Gate],
                DisallowedTools = tools.Deny,
                McpConfigFile = paths.McpCoai,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "not a step mode this build launches (known: Write, Read, Gate)"),
        };
    }

    /// <summary>The delegation policy (E2.S0): unless the step's spec says so, no step starts a subagent. <see cref="SubagentTools"/> lead the deny list and leave the allow list, ahead of
    /// the mode's own lists, so the policy holds whatever the file's <c>tools</c> say. It is one function of the spec, applied to every launch — a fresh one and a resume alike — so a
    /// resume is never wider than the launch it continues. With <c>subagents: true</c> it adds nothing and removes nothing. What this sends is a request: whether the CLI honours it
    /// is measured in the module doc (<i>The delegation policy</i>).</summary>
    private static AgentAskOptions Delegation(AgentAskOptions options, bool subagents) =>
        subagents
            ? options
            : options with
            {
                DisallowedTools = [.. SubagentTools, .. options.DisallowedTools],
                PermittedTools = [.. options.PermittedTools.Where(tool => !SubagentTools.Contains(tool, StringComparer.Ordinal))],
            };

    private static AgentAskOptions Session(AgentAskOptions options, StepSession session) =>
        session switch
        {
            StepSession.Fresh fresh => options with { SessionId = fresh.Id },
            StepSession.Resume resume => options with { ResumeSessionId = resume.Id },
            _ => throw new ArgumentOutOfRangeException(nameof(session), session, "not a step session this build launches (known: Fresh, Resume)"),
        };
}
