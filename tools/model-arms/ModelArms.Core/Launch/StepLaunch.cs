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
    /// <summary>The options of a step: stream-json events, MCP servers off and the arm's notes and work folders granted, whatever the mode;
    /// then the session; then the mode's permission mode, permission list, deny list and MCP config.</summary>
    public static AgentAskOptions Options(StepMode mode, ToolSets tools, ArmPaths paths, StepSession session) =>
        Session(ForMode(mode, tools, paths), session);

    /// <summary>The question put to claude for one attempt of a step.</summary>
    public static AgentAsk Ask(StepLaunchRequest request) =>
        new(ModelRuntimeKind.CliClaude, request.ClaudeExecutable, request.Prompt, request.WorkingDirectory, request.Wall, request.Spec.Model)
        {
            Options = Options(request.Spec.Mode, request.Tools, request.Paths, request.Session),
            Environment = AgentEnvironment.Only(ArmChildEnvironment.Of(request.ParentEnvironment, request.Paths)),
        };

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

    private static AgentAskOptions Session(AgentAskOptions options, StepSession session) =>
        session switch
        {
            StepSession.Fresh fresh => options with { SessionId = fresh.Id },
            StepSession.Resume resume => options with { ResumeSessionId = resume.Id },
            _ => throw new ArgumentOutOfRangeException(nameof(session), session, "not a step session this build launches (known: Fresh, Resume)"),
        };
}
