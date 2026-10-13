using System.Security.Cryptography;
using System.Text;
using Bench.Application.Gate;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Scouts;

namespace Bench.Infrastructure.Scouts;

/// <param name="PluginCache">The plugin's cache folder — <c>~/.claude/plugins/cache/claude-plugins-official/feature-dev</c>, every version ever
/// installed under it; the ONE version claude loads for the user is resolved from <see cref="PluginRegistry"/> (S5), and that folder is what
/// is hashed and passed as <c>--plugin-dir</c>. A folder that is itself a plugin (it holds <c>.claude-plugin/plugin.json</c> — a test's) is
/// taken as it is.</param>
/// <param name="SettingsFiles">The operator's settings files — what the grant guard reads, the campaign key's placement is checked against, and
/// the pinned settings file is projected FROM (S5). Since S5 their bytes are no longer the settings pin: the pinned file's are.</param>
public sealed record ScoutPinSources(string PluginCache, IReadOnlyList<string> SettingsFiles)
{
    /// <summary><c>~/.claude/plugins/installed_plugins.json</c> — which version folder under the cache the operator's sessions load.</summary>
    public string PluginRegistry { get; init; } = string.Empty;

    /// <summary>The pinned settings file (<c>&lt;artefact root&gt;/scouts/pinned-settings.json</c>) — the settings PIN and the file the launch's
    /// <c>--settings</c> names; empty until a verb knows its artefact root (<c>tasks</c> reads no pin).</summary>
    public string PinnedSettingsFile { get; init; } = string.Empty;

    public static ScoutPinSources Machine
    {
        get
        {
            var claude = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return new(Path.Combine(claude, "plugins", "cache", "claude-plugins-official", "feature-dev"), [Path.Combine(claude, "settings.json")])
            {
                PluginRegistry = Path.Combine(claude, "plugins", "installed_plugins.json"),
            };
        }
    }
}

/// <summary>The one plugin directory the scouts launch with (S5): the cache itself when it is a plugin folder, else the version folder the
/// registry's user-scope entry names — which must lie under the cache and hold a manifest. Fourteen version folders sat under the cache on
/// 2026-10-13; naming the cache would pin fourteen plugins and launch none.</summary>
public static class ScoutPluginDirectory
{
    public static bool IsPlugin(string folder) => File.Exists(Path.Combine(folder, ".claude-plugin", "plugin.json"));

    public static Outcome<string> Resolve(ScoutPinSources sources)
    {
        var cache = sources.PluginCache;

        return (Directory.Exists(cache), IsPlugin(cache), File.Exists(sources.PluginRegistry)) switch
        {
            (false, _, _) => Outcome<string>.Failure($"the plugin cache {cache} is not there — the scouts' agent type would be \"not found\""),
            (_, true, _) => Outcome<string>.Success(Path.GetFullPath(cache)),
            (_, _, false) => Outcome<string>.Failure(
                $"the plugin cache {cache} holds {Directory.EnumerateDirectories(cache).Count()} version folder(s) and the plugin registry {sources.PluginRegistry} is not there to say which one claude loads — pass --plugin-registry"),
            _ => Registered(cache, sources.PluginRegistry),
        };
    }

    private static Outcome<string> Registered(string cache, string registry) =>
        ScoutPluginRegistry.InstallPath(File.ReadAllText(registry), ScoutPluginRegistry.FeatureDev).Match(
            path => (ClaudeGrants.Within(path, cache), IsPlugin(path)) switch
            {
                (false, _) => Outcome<string>.Failure($"the plugin registry's user-scope install of {ScoutPluginRegistry.FeatureDev} lies outside the plugin cache {cache} — the pin reads one cache; pass the cache that holds it"),
                (_, false) => Outcome<string>.Failure($"the plugin registry's user-scope install of {ScoutPluginRegistry.FeatureDev} holds no .claude-plugin/plugin.json — reinstall the plugin"),
                _ => Outcome<string>.Success(Path.GetFullPath(path)),
            },
            reason => Outcome<string>.Failure($"{registry}: {reason}"));
}

/// <summary>The pins this machine can read before a claim without a paid call (§3.4): the CLI binary's <see cref="ProductPin"/> (its version
/// text's first token is the CLI-version pin — a pin is one token), the plugin cache hash (SHA-256 over every file's relative path and bytes'
/// SHA-256 under the PINNED plugin directory, sorted — a plugin update moves it; claude's own <c>.in_use/&lt;pid&gt;</c> markers and
/// <c>.orphaned_at</c> are skipped, because a session starting on this machine is not a plugin change) and the settings hash — since S5 the
/// SHA-256 of the pinned settings file, which must be there and carry only its two keys. A plugin directory that cannot be resolved is a
/// refusal: the agent under test would be "not found".</summary>
public sealed class ScoutPinReader(IProductPinReader binaries, ScoutPinSources sources) : IScoutPinReader
{
    public async Task<Outcome<ScoutLocalPins>> ReadAsync(string executable, CancellationToken cancellationToken)
    {
        var binary = await binaries.ReadAsync(executable, cancellationToken);
        var plugin = ScoutPluginDirectory.Resolve(sources);
        var pinned = PinnedSettings(sources.PinnedSettingsFile);

        return (binary, plugin, pinned) switch
        {
            (Outcome<ProductPin>.Fail fail, _, _) => Outcome<ScoutLocalPins>.Failure(fail.Reason),
            (_, Outcome<string>.Fail fail, _) => Outcome<ScoutLocalPins>.Failure(fail.Reason),
            (_, _, Outcome<string>.Fail fail) => Outcome<ScoutLocalPins>.Failure(fail.Reason),
            (Outcome<ProductPin>.Ok ok, Outcome<string>.Ok dir, Outcome<string>.Ok file) => Outcome<ScoutLocalPins>.Success(
                new ScoutLocalPins(ok.Value, VersionToken(ok.Value.VersionText), FolderHash(dir.Value), SettingsHash([file.Value])) { PluginDirectory = dir.Value }),
            _ => Outcome<ScoutLocalPins>.Failure("unreachable"),
        };
    }

    /// <summary>The pinned settings file: named, there, and holding only its two keys — a key beside them would reach every scout.</summary>
    public static Outcome<string> PinnedSettings(string file) =>
        (file.Length, File.Exists(file)) switch
        {
            (0, _) => Outcome<string>.Failure("no pinned settings file is named — the verb was loaded without an artefact root"),
            (_, false) => Outcome<string>.Failure($"the pinned settings file {file} is not there — bench scouts run writes it once from the user settings; run (not resume) a run, or an alias-check, to pin it"),
            _ => ScoutPinnedSettings.Verify(File.ReadAllText(file)).Match(_ => Outcome<string>.Success(file), reason => Outcome<string>.Failure($"{file}: {reason}")),
        };

    /// <summary><c>2.1.289 (Claude Code)</c> → <c>2.1.289</c>.</summary>
    public static string VersionToken(string versionText) =>
        versionText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(string.Empty);

    public static string FolderHash(string folder)
    {
        var manifest = new StringBuilder();

        foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
                     .Select(f => (Full: f, Relative: Path.GetRelativePath(folder, f).Replace('\\', '/')))
                     .Where(f => !Transient(f.Relative))
                     .OrderBy(f => f.Relative, StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(file.Full);
            manifest.Append(file.Relative).Append('\n').Append(Convert.ToHexStringLower(SHA256.HashData(stream))).Append('\n');
        }

        return StableHash.Of(manifest.ToString());
    }

    /// <summary>Claude's own bookkeeping inside a plugin folder — a <c>.in_use/&lt;pid&gt;</c> marker per running session, an <c>.orphaned_at</c>
    /// stamp — which moves whenever a session starts or ends and says nothing about the plugin.</summary>
    public static bool Transient(string relative) =>
        relative.StartsWith(".in_use/", StringComparison.Ordinal) || relative == ".in_use" || relative == ".orphaned_at";

    public static string SettingsHash(IReadOnlyList<string> files) =>
        StableHash.Of(string.Concat(files.Select(f => $"{Path.GetFileName(f)}\n{(File.Exists(f) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))) : "absent")}\n")));
}
