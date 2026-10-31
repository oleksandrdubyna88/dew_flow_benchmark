using ModelArms.Core.Ports;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>Whether claude left a transcript for a session (plan D7): the file <see cref="SessionTranscripts.PathOf"/> spells exists. An empty claude home has no path, so no transcript — a fresh start, the safe side.</summary>
public sealed class ClaudeSessionTranscripts(string claudeHome) : ISessionTranscripts
{
    public bool Exists(string workingDirectory, Guid session) =>
        SessionTranscripts.PathOf(claudeHome, workingDirectory, session) is { Length: > 0 } path && File.Exists(path);
}
