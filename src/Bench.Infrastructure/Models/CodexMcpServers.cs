using System.Text.RegularExpressions;
using Bench.Domain;

namespace Bench.Infrastructure.Models;

/// <summary>The MCP servers this machine's Codex CLI would load — the table names under <c>[mcp_servers.&lt;name&gt;]</c> in
/// <c>$CODEX_HOME/config.toml</c> (default <c>~/.codex</c>) — so an assessor launch can switch each one off with
/// <c>-c mcp_servers.&lt;name&gt;.enabled=false</c>. A port of the calibration's <c>codex_mcp_servers</c>: a blinded
/// assessor with the review gate's own MCP server loaded could reach the very product it is judging.</summary>
public static partial class CodexMcpServers
{
    [GeneratedRegex(@"^\[mcp_servers\.([A-Za-z0-9_-]+)\]", RegexOptions.Multiline)]
    private static partial Regex Table { get; }

    public static string DefaultConfig =>
        Path.Combine(
            Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } home
                ? home
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"),
            "config.toml");

    public static Outcome<IReadOnlyList<string>> Declared() => Read(DefaultConfig);

    /// <summary>The declared names, sorted and distinct; none when the file is absent — a codex with no config loads no
    /// server. A config that exists and cannot be read is a REFUSAL: its servers could not be switched off, and a launch
    /// that promised "MCP servers off" would load them.</summary>
    public static Outcome<IReadOnlyList<string>> Read(string configFile)
    {
        try
        {
            return Outcome<IReadOnlyList<string>>.Success(File.Exists(configFile) ? Parse(File.ReadAllText(configFile)) : []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<IReadOnlyList<string>>.Failure(
                $"the codex config {Path.GetFileName(configFile)} exists and could not be read ({ex.GetType().Name}) — its MCP servers cannot be switched off");
        }
    }

    public static IReadOnlyList<string> Parse(string toml) =>
        [.. Table.Matches(toml).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
