namespace ModelArms.Core.Steps;

/// <summary>Where claude keeps a session's transcript.</summary>
public static class SessionTranscripts
{
    public static string ClaudeHome(IReadOnlyDictionary<string, string> environment) => string.Empty;

    public static string PathOf(string claudeHome, string workingDirectory, Guid session) => string.Empty;
}
