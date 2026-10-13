using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;

namespace Bench.Infrastructure.Scouts;

/// <summary>Where the campaign key may be, and the key read from there or created there once (plan §4 S4). It COMPOSES the gate's
/// <see cref="FileHashKeyFile"/> — 32 random bytes from the operating system, owner-only at creation, checked owner-only on every read, a
/// value (<see cref="FileHashKey"/>) whose <c>ToString</c> is redacted — and adds the one rule the scouts need: the file's resolved path,
/// junctions and links followed, must lie OUTSIDE every git checkout, the trees root, the mirror root and every directory the operator's
/// Claude settings grant. A key inside a checkout is one <c>git add</c> from publishing every commitment's preimage; one inside the trees or
/// the mirrors is readable from a scout's session; one in a granted directory is readable from any tree.
/// <para>
/// A missing key is REFUSED by every verb that needs one (<see cref="Read"/>): <c>bench scouts key init</c> creates it exactly once, and a
/// later verb never regenerates it — a second key would make every commitment published so far unverifiable, silently. The bytes never
/// enter an argv, a child's environment or a prompt: the key is read into memory here and handed to the committer as a value.
/// </para></summary>
public static class ScoutCampaignKey
{
    public const string Flag = "--campaign-key";

    public const string Variable = "BENCH_SCOUT_CAMPAIGN_KEY";

    public const string InitVerb = "bench scouts key init";

    /// <summary>The path the key is named by: <paramref name="flagValue"/>, else the variable; empty when neither names one.</summary>
    public static string PathOf(string flagValue) =>
        flagValue.Length > 0 ? flagValue : Environment.GetEnvironmentVariable(Variable) ?? string.Empty;

    /// <summary>Why <paramref name="path"/> may not hold the key, or empty. The checks run on the RESOLVED path (<see cref="ArtifactContainment"/>:
    /// every existing component asked whether it is a link), so a junction into a checkout is seen as the checkout. A refusal names the rule
    /// and the roots, never the key.</summary>
    public static string Refusal(string path, string treesRoot, string mirrorRoot, IReadOnlyList<string> userSettingsFiles)
    {
        if (path.Trim().Length == 0)
        {
            return $"the scouts need the campaign key — pass {Flag} <file> or set {Variable}; a private task's commitments are made under it";
        }

        var resolved = ArtifactContainment.Resolve(path);

        return resolved.Match(
            full => Placed(full, Path.GetFullPath(treesRoot), Path.GetFullPath(mirrorRoot), userSettingsFiles),
            reason => $"the campaign key's path cannot be resolved — {reason}");
    }

    private static string Placed(string full, string treesRoot, string mirrorRoot, IReadOnlyList<string> userSettingsFiles)
    {
        var folder = Path.GetDirectoryName(full) ?? full;
        var checkout = FileSystemGateArtifactStore.GitCheckoutAbove(folder);

        return (checkout.Length > 0, ClaudeGrants.Within(full, treesRoot), ClaudeGrants.Within(full, mirrorRoot)) switch
        {
            (true, _, _) => $"the campaign key {full} lies inside the git checkout at {checkout} — one 'git add' would publish every commitment's preimage; keep it outside every repository",
            (_, true, _) => $"the campaign key {full} lies under the trees root {treesRoot} — a scout's session could read it; keep it outside",
            (_, _, true) => $"the campaign key {full} lies under the mirror root {mirrorRoot} — a scout's session could read it; keep it outside",
            _ => Granted(full, userSettingsFiles),
        };
    }

    private static string Granted(string full, IReadOnlyList<string> userSettingsFiles) =>
        ClaudeGrants.Read(userSettingsFiles, Path.GetDirectoryName(full) ?? full) switch
        {
            Outcome<IReadOnlyList<string>>.Fail fail => $"the Claude user settings could not be read — {fail.Reason}; a grant nobody could read is a grant nobody checked",
            Outcome<IReadOnlyList<string>>.Ok ok when ok.Value.FirstOrDefault(g => ClaudeGrants.Within(full, g)) is { } grant =>
                $"the campaign key {full} lies inside {grant}, a directory the Claude user settings grant (permissions.additionalDirectories) — readable from any tree; keep it outside",
            _ => string.Empty,
        };

    /// <summary>The key at <paramref name="path"/>, or the refusal: missing (never created here), unreadable, or readable by another user.</summary>
    public static Outcome<FileHashKey> Read(string path) =>
        FileHashKeyFile.ReadFile(Path.GetFullPath(path)) switch
        {
            FileHashKeyRead.Present present => Outcome<FileHashKey>.Success(present.Key),
            FileHashKeyRead.Missing => Outcome<FileHashKey>.Failure(
                $"no campaign key at {Path.GetFullPath(path)} — '{InitVerb}' creates it once; a verb never regenerates one, because a new key would make every commitment published so far unverifiable"),
            FileHashKeyRead.Unusable unusable => Outcome<FileHashKey>.Failure($"the campaign key at {Path.GetFullPath(path)} cannot be used — {unusable.Reason}"),
            _ => Outcome<FileHashKey>.Failure("unreachable"),
        };

    /// <summary><c>bench scouts key init</c>: the key created once at <paramref name="path"/> (its folder made), refused when one is there already.</summary>
    public static Outcome<FileHashKey> Create(string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? full);

        return FileHashKeyFile.CreateFile(full).Match(Outcome<FileHashKey>.Success, reason => Outcome<FileHashKey>.Failure($"the campaign key at {full}: {reason}"));
    }
}
