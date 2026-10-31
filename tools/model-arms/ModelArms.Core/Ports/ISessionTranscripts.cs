namespace ModelArms.Core.Ports;

/// <summary>Whether claude left a transcript for a session in a working directory (plan C7, D7): the one fact recovery needs to choose between resuming a session and starting a new
/// one. The adapter (E2.S4) probes the file <c>SessionTranscripts.PathOf</c> spells; Core opens nothing.</summary>
public interface ISessionTranscripts
{
    /// <summary>True when the transcript of <paramref name="session"/>, started with <paramref name="workingDirectory"/> as its cwd, exists.</summary>
    bool Exists(string workingDirectory, Guid session);
}
