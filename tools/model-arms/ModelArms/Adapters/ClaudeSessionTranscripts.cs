using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>Whether claude left a transcript (plan D7). STUB.</summary>
public sealed class ClaudeSessionTranscripts(string claudeHome) : ISessionTranscripts
{
    private readonly string _home = claudeHome;

    public bool Exists(string workingDirectory, Guid session) => false;
}
