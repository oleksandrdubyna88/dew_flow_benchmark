using System.Security.Cryptography;
using System.Text;
using Bench.Application.Gate;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Infrastructure.Scouts;

/// <param name="PluginCache">The plugin's cached folder — what pins the agent type (<c>~/.claude/plugins/cache/claude-plugins-official/feature-dev</c>).</param>
/// <param name="SettingsFiles">The operator's settings files whose bytes are the settings pin — effort levels included, because they are the
/// real condition the rule will run under (§3.4).</param>
public sealed record ScoutPinSources(string PluginCache, IReadOnlyList<string> SettingsFiles)
{
    public static ScoutPinSources Machine
    {
        get
        {
            var claude = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return new(Path.Combine(claude, "plugins", "cache", "claude-plugins-official", "feature-dev"), [Path.Combine(claude, "settings.json")]);
        }
    }
}

/// <summary>The pins this machine can read before a claim without a paid call (§3.4): the CLI binary's <see cref="ProductPin"/> (its version
/// text's first token is the CLI-version pin — a pin is one token), the plugin cache hash (SHA-256 over every file's relative path and bytes'
/// SHA-256, sorted — a plugin update moves it) and the settings hash (SHA-256 over each settings file's name and bytes; an absent file hashes as
/// absent, so creating one moves it). A plugin cache that is not there is a refusal: the agent under test would be "not found".</summary>
public sealed class ScoutPinReader(IProductPinReader binaries, ScoutPinSources sources) : IScoutPinReader
{
    public async Task<Outcome<ScoutLocalPins>> ReadAsync(string executable, CancellationToken cancellationToken)
    {
        var binary = await binaries.ReadAsync(executable, cancellationToken);

        return (binary, Directory.Exists(sources.PluginCache)) switch
        {
            (Outcome<ProductPin>.Fail fail, _) => Outcome<ScoutLocalPins>.Failure(fail.Reason),
            (_, false) => Outcome<ScoutLocalPins>.Failure($"the plugin cache {sources.PluginCache} is not there — the scouts' agent type would be \"not found\""),
            (Outcome<ProductPin>.Ok ok, _) => Outcome<ScoutLocalPins>.Success(new ScoutLocalPins(ok.Value, VersionToken(ok.Value.VersionText), FolderHash(sources.PluginCache), SettingsHash(sources.SettingsFiles))),
            _ => Outcome<ScoutLocalPins>.Failure("unreachable"),
        };
    }

    /// <summary><c>2.1.289 (Claude Code)</c> → <c>2.1.289</c>.</summary>
    public static string VersionToken(string versionText) =>
        versionText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(string.Empty);

    public static string FolderHash(string folder)
    {
        var manifest = new StringBuilder();

        foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
                     .Select(f => (Full: f, Relative: Path.GetRelativePath(folder, f).Replace('\\', '/')))
                     .OrderBy(f => f.Relative, StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(file.Full);
            manifest.Append(file.Relative).Append('\n').Append(Convert.ToHexStringLower(SHA256.HashData(stream))).Append('\n');
        }

        return StableHash.Of(manifest.ToString());
    }

    public static string SettingsHash(IReadOnlyList<string> files) =>
        StableHash.Of(string.Concat(files.Select(f => $"{Path.GetFileName(f)}\n{(File.Exists(f) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))) : "absent")}\n")));
}
