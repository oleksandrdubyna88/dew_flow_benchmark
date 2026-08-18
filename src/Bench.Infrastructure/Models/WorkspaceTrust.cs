using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;

namespace Bench.Infrastructure.Models;

public enum TrustOutcome
{
    /// <summary>The config already trusted every key this working directory needs.</summary>
    AlreadyTrusted,

    /// <summary>The config was edited. Says which keys were added.</summary>
    Granted,

    /// <summary>Nothing was written, and the reason names what stopped it. Never an exception: a launch that
    /// cannot be pre-trusted still runs, it just risks the gate.</summary>
    Refused,
}

/// <param name="Keys">The config keys this directory needs trusted, in the spelling the CLI itself uses.</param>
public sealed record TrustResult(TrustOutcome Outcome, IReadOnlyList<string> Keys, string Reason)
{
    public string Describe => Outcome switch
    {
        TrustOutcome.AlreadyTrusted => $"already trusted ({Keys.Count} key(s))",
        TrustOutcome.Granted => $"trusted {string.Join(", ", Keys)}",
        _ => $"not pre-trusted — {Reason}",
    };
}

/// <summary>Pre-trusting the checkout an agent is about to be launched in.
/// <para>
/// <b>Measured, 2026-08-18.</b> Two of four authoring groups produced nothing and burned their full 900-second
/// wall each — half an hour for nothing — because the Claude CLI printed <i>"Ignoring 5 permissions.allow
/// entries: this workspace has not been trusted"</i> and then waited on a dialog no headless run can answer.
/// The wall is what ended it, which is the wall working correctly and the run failing anyway. The other two
/// groups succeeded with the same warning printed, so the warning is not the failure — the blocked tool call
/// behind it is.
/// </para>
/// <para>
/// <b>The scope is the guard.</b> Only a directory UNDER the benchmark's own checkout root can be trusted here.
/// Trusting an arbitrary path on the operator's behalf would hand any repository this benchmark is pointed at
/// the permissions of a trusted workspace; trusting a tree this harness created itself, at a commit it pinned,
/// is the same decision the operator already made by running the benchmark.
/// </para>
/// <para>
/// It edits the real, live <c>~/.claude.json</c>, so: only one boolean per key is touched, every sibling field
/// is preserved, a <c>projects</c> node that is not an object refuses rather than being replaced, and the write
/// goes through a temporary file with a backup — this file holds an operator's whole CLI state, and a truncated
/// one would be a bad afternoon.
/// </para></summary>
public sealed class WorkspaceTrust(string configPath, string allowedRoot)
{
    private const string TrustField = "hasTrustDialogAccepted";

    public static string DefaultConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    /// <summary>Every config key this working directory needs trusted: the directory itself, and — when it is a
    /// git worktree — the bare repository its <c>.git</c> file points at.
    /// <para>
    /// Both, because the CLI's own message names the BARE path (<c>projects["…/bare/xxx.git"]</c>) while the
    /// directory it was launched in is the worktree. One more entry costs nothing and removes a fifteen-minute
    /// failure mode from being possible at all.
    /// </para></summary>
    public static IReadOnlyList<string> KeysFor(string workingDirectory)
    {
        var keys = new List<string> { Key(workingDirectory) };
        var pointer = Path.Combine(workingDirectory, ".git");

        if (File.Exists(pointer) && Bare(File.ReadAllText(pointer)) is { Length: > 0 } bare)
        {
            keys.Add(Key(bare));
        }

        return [.. keys.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The bare repository a worktree's <c>.git</c> file points at, or empty when the text is not one.
    /// <para>
    /// Pure, so the parsing is testable without a repository. A worktree's pointer reads
    /// <c>gitdir: C:/…/bare/x.git/worktrees/abc</c>, and the trusted key is the part before
    /// <c>/worktrees/</c> — a plain checkout has a <c>.git</c> DIRECTORY and never reaches here.
    /// </para></summary>
    public static string Bare(string gitFileText)
    {
        var text = gitFileText.Trim();

        if (!text.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var gitDir = text["gitdir:".Length..].Trim().Replace('\\', '/');
        var worktrees = gitDir.LastIndexOf("/worktrees/", StringComparison.OrdinalIgnoreCase);

        return worktrees > 0 ? gitDir[..worktrees] : gitDir;
    }

    public Outcome<TrustResult> Ensure(string workingDirectory)
    {
        var keys = KeysFor(workingDirectory);
        var outside = keys.Where(k => !Under(k)).ToList();

        if (outside.Count > 0)
        {
            return Outcome<TrustResult>.Success(new TrustResult(
                TrustOutcome.Refused,
                keys,
                $"{string.Join(", ", outside)} is not under this benchmark's checkout root ({Key(allowedRoot)}), "
                + "and trusting a tree the harness did not create is the operator's decision, not this run's"));
        }

        if (!File.Exists(configPath))
        {
            // Creating one would be inventing a CLI configuration on a machine where that CLI has never run.
            return Outcome<TrustResult>.Success(new TrustResult(
                TrustOutcome.Refused, keys, $"there is no '{configPath}' — run the agent's CLI once so it writes its own config"));
        }

        return Apply(keys);
    }

    private Outcome<TrustResult> Apply(IReadOnlyList<string> keys)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(configPath)) is not JsonObject root)
            {
                return Outcome<TrustResult>.Failure($"'{configPath}' is not a JSON object");
            }

            if (root["projects"] is JsonNode existing and not JsonObject)
            {
                return Outcome<TrustResult>.Failure(
                    $"'projects' in '{configPath}' is a {existing.GetValueKind()} rather than an object — refusing "
                    + "to replace it, because whatever is there is the operator's");
            }

            var projects = root["projects"] as JsonObject ?? Added(root);
            var granted = keys.Where(key => Trust(projects, key)).ToList();

            return granted.Count == 0
                ? Outcome<TrustResult>.Success(new TrustResult(TrustOutcome.AlreadyTrusted, keys, string.Empty))
                : Write(root, granted);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Outcome<TrustResult>.Failure($"'{configPath}' could not be read or written: {ex.Message}");
        }
    }

    private static JsonObject Added(JsonObject root)
    {
        var projects = new JsonObject();
        root["projects"] = projects;

        return projects;
    }

    /// <summary>Sets the one boolean, preserving every sibling field of an entry that already exists. Returns
    /// whether anything actually changed.</summary>
    private static bool Trust(JsonObject projects, string key)
    {
        if (projects[key] is JsonObject entry)
        {
            if (entry[TrustField]?.GetValue<bool>() == true)
            {
                return false;
            }

            entry[TrustField] = true;
            return true;
        }

        projects[key] = new JsonObject { [TrustField] = true };
        return true;
    }

    /// <summary>Through a temporary file with a backup, because this is the operator's live CLI state and a
    /// half-written one would cost more than this whole feature saves.</summary>
    private Outcome<TrustResult> Write(JsonObject root, IReadOnlyList<string> granted)
    {
        var staged = configPath + ".bench-staged";
        File.WriteAllText(staged, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Replace(staged, configPath, configPath + ".bench-backup");

        return Outcome<TrustResult>.Success(new TrustResult(TrustOutcome.Granted, granted, string.Empty));
    }

    private bool Under(string key) =>
        key.StartsWith(Key(allowedRoot), StringComparison.OrdinalIgnoreCase);

    /// <summary>A path in the spelling the CLI writes into its own config: absolute, forward slashes. Taken from
    /// the CLI's own warning text (<c>C:/Users/…/bare/x.git</c>) rather than invented, because the lookup is by
    /// string and a differently-spelled key would be a new entry nothing reads.</summary>
    private static string Key(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
}
