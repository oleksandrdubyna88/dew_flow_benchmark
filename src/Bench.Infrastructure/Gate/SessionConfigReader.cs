using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Infrastructure.Gate;

/// <summary>Reads <c>state.config</c> of THIS run's session out of the product's data directory, without asking the
/// product — a port of <c>coai-bench</c>'s <c>OnDisk</c> + <c>Sessions.Owner</c>. A session file names its repo and branch in
/// <c>state.repoPath</c> / <c>state.branch</c> (the file name is a hash); the repo path is compared with its separators,
/// a trailing separator and its case normalised, the branch exactly. A neighbour's session in a shared directory is
/// never read.</summary>
public static class SessionConfigReader
{
    /// <summary>The config JSON of the newest matching session, or empty when there is none.</summary>
    public static string Read(string dataDir, string repoPath, string branch)
    {
        var sessions = Path.Combine(dataDir, "sessions");

        if (!Directory.Exists(sessions))
        {
            return string.Empty;
        }

        return Directory.EnumerateFiles(sessions, "session-*.json")
            .Select(f => (File: f, State: StateOf(f)))
            .Where(s => s.State is not null && Mine(s.State, repoPath, branch))
            .OrderByDescending(s => File.GetLastWriteTimeUtc(s.File))
            .Select(s => (s.State!["config"] as JsonObject)?.ToJsonString() ?? string.Empty)
            .FirstOrDefault() ?? string.Empty;
    }

    private static bool Mine(JsonObject state, string repoPath, string branch) =>
        string.Equals(Normal(Text(state, "repoPath")), Normal(repoPath), StringComparison.OrdinalIgnoreCase)
        && (branch.Length == 0 || string.Equals(Text(state, "branch"), branch, StringComparison.Ordinal));

    private static string Normal(string path) => path.Replace('\\', '/').TrimEnd('/');

    private static string Text(JsonObject state, string name) =>
        state[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    /// <summary>Shared and permitting deletion: the product may be replacing this very file.</summary>
    private static JsonObject? StateOf(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return (JsonNode.Parse(stream) as JsonObject)?["state"] as JsonObject;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
