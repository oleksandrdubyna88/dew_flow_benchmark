using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Cli;

/// <summary>`bench sessions install` — teaching one repository to record what an agent does in it.
/// <para>
/// The hooks go into <c>.claude/settings.local.json</c> rather than the shared <c>settings.json</c>, and
/// that is not a detail: the command line holds an ABSOLUTE path to this machine's <c>bench-hook</c>
/// binary, and committing it would point every one of a team's checkouts at a path only one person has.
/// </para>
/// <para>
/// The merge preserves everything it did not write. An operator's own hooks are somebody's working setup,
/// and an installer that replaced the file wholesale would be a tool people install once and then undo.
/// </para></summary>
public static class SessionHooks
{
    /// <summary>Which moments are recorded.
    /// <para>
    /// The three tool events are the measurement. <c>PostToolUseFailure</c> is not an extra — a tool that
    /// FAILED fires it INSTEAD of <c>PostToolUse</c>, so leaving it out strands every failed call open in
    /// the ledger. Measured on a real session, 2026-08-23: an agent guessed a path that did not exist, and
    /// the read sat unfinished forever, indistinguishable from an interrupted session. Failed calls are
    /// precisely the work a replacement map is hunting.
    /// </para>
    /// <para>
    /// The two session events are worth their cost for one reason each: <c>SessionStart</c> creates the row
    /// before the first tool call, so a dashboard shows a session the moment it opens rather than after its
    /// first read; <c>SessionEnd</c> stamps a last-seen time that is the agent's own rather than a
    /// timeout's.
    /// </para></summary>
    private static readonly string[] Events =
        ["SessionStart", "PreToolUse", "PostToolUse", "PostToolUseFailure", "SessionEnd"];

    /// <summary>Seconds a hook is allowed before the agent gives up on it. Generous next to the client's
    /// own budgets (1.5 s to post, 1.5 s for git) precisely so that this ceiling is never the one that
    /// fires — if it does, something is wrong that a longer wait would not fix.</summary>
    private const int HookTimeoutSeconds = 5;

    public static int Install(CommandLine command, TextWriter output, TextWriter error)
    {
        var repo = command.Value("repo");

        if (repo.Length == 0 || !Directory.Exists(repo))
        {
            return SessionsCommand.Fail(
                error, $"--repo <path> must name an existing directory, got '{repo}'", ExitCodes.Configuration);
        }

        var hook = Resolve(command.Value("hook"));

        if (hook.Length == 0)
        {
            return SessionsCommand.Fail(
                error,
                "could not find bench-hook — pass --hook <path to bench-hook.exe>. "
                + "Build it with `dotnet build dew_flow_benchmark.slnx -c Release`",
                ExitCodes.Environment);
        }

        var settings = Path.Combine(repo, ".claude", "settings.local.json");
        var collector = command.Value("collector", Bench.Contracts.SessionCollector.DefaultUrl);

        Write(settings, hook, collector);

        output.WriteLine($"hooks installed  {settings}");
        output.WriteLine($"hook binary      {hook}");
        output.WriteLine($"collector        {collector}");
        output.WriteLine();
        output.WriteLine("open a terminal in that repository with BENCH_TASK_ID / BENCH_TASK_NAME / BENCH_PLAN_PATH set,");
        output.WriteLine("or let the VS Code extension open it for you, and every tool call lands in the bench database.");

        return ExitCodes.Pass;
    }

    private static void Write(string settingsPath, string hook, string collector)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        var root = Load(settingsPath);
        var hooks = Child(root, "hooks");

        foreach (var name in Events)
        {
            hooks[name] = Merge(hooks[name] as JsonArray, Entry(hook, name));
        }

        // The collector's address travels in the settings' own env block, so a terminal opened by hand —
        // without the extension — still reaches it. The task variables deliberately do NOT live here: they
        // describe one SESSION, and a value pinned per repository would label every session the same.
        Child(root, "env")[Bench.Contracts.SessionCollector.UrlVariable] = collector;

        File.WriteAllText(settingsPath, root.ToJsonString(Indented));
    }

    /// <summary>Keeps every entry that is not ours, and replaces the one that is.
    /// <para>
    /// Matched by the binary's NAME rather than by its full path, so re-installing from a different build
    /// directory updates the entry instead of leaving a stale one beside it pointing at a binary that has
    /// since been deleted.
    /// </para></summary>
    private static JsonArray Merge(JsonArray? existing, JsonNode ours)
    {
        var kept = existing?
            .Where(entry => entry is not null && !entry.ToJsonString().Contains("bench-hook", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry!.DeepClone())
            ?? [];

        return [.. kept, ours];
    }

    private static JsonNode Entry(string hook, string eventName) => new JsonObject
    {
        ["matcher"] = "*",
        ["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command",
            // Quoted: this path routinely contains spaces, and an unquoted one becomes a command whose
            // first argument is half a directory name.
            ["command"] = $"\"{hook}\" {eventName}",
            ["timeout"] = HookTimeoutSeconds,
        }),
    };

    private static JsonObject Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            // A settings file this cannot read is somebody's file with a syntax error in it. Starting from
            // an empty object would silently delete their configuration; starting from empty and writing to
            // a NEW name would leave the agent reading the broken one. So: keep nothing, and let the write
            // below replace a file that was not valid JSON anyway.
            return [];
        }
    }

    private static JsonObject Child(JsonObject root, string name)
    {
        if (root[name] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        root[name] = created;

        return created;
    }

    /// <summary>Where the hook binary is. Beside this CLI first — that is how a published pair sits — then
    /// at the sibling build output, which is how a developer's checkout sits.</summary>
    private static string Resolve(string given)
    {
        if (given.Length > 0)
        {
            return File.Exists(given) ? Path.GetFullPath(given) : string.Empty;
        }

        return Candidates().FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static IEnumerable<string> Candidates()
    {
        var name = OperatingSystem.IsWindows() ? "bench-hook.exe" : "bench-hook";
        var here = AppContext.BaseDirectory;

        yield return Path.Combine(here, name);

        // …/hosts/Cli/bin/<config>/net10.0/  →  …/hosts/Hook/bin/<config>/net10.0/
        var configuration = new DirectoryInfo(here).Parent?.Name ?? "Release";
        var hosts = new DirectoryInfo(here).Parent?.Parent?.Parent?.Parent?.FullName;

        if (hosts is not null)
        {
            yield return Path.Combine(hosts, "Hook", "bin", configuration, "net10.0", name);
        }
    }

    /// <summary>Indented, and with the default escaper relaxed.
    /// <para>
    /// The strict encoder writes every quote in a command line as <c>"</c>. That is valid JSON and
    /// every parser reads it — but this file is one an operator opens to check what was installed, and a
    /// command line rendered as escape sequences is one nobody can check at a glance. The relaxed encoder
    /// is safe here for the reason the name warns about: nothing embeds this file in HTML.
    /// </para></summary>
    private static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
