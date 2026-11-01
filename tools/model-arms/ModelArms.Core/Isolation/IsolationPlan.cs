using System.Security.Cryptography;
using System.Text;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Isolation;

/// <summary>What a path is closed to: <see cref="Closed"/> is mode 000 (nothing can be listed, read or traversed), <see cref="Unlisted"/> is mode 300 (traversable and creatable by NAME, never listable — the part-6 harness ran nine arms for hours under it, F0).</summary>
public enum IsolationKind { Closed, Unlisted }

/// <summary>One path of the plan: fully qualified, normalised, no trailing separator. <see cref="Source"/> is the configuration entry that named it (<c>isolation.unlisted[3]</c>), so a refusal can say where to look; it is not part of the digest.</summary>
public sealed record IsolationEntry(string Path, IsolationKind Kind, string Source = "");

/// <summary>The ordered closed-and-unlisted paths one run is locked with (D11, D27) and the digest of that list. An empty plan is <i>no isolation configured</i>.</summary>
public sealed record IsolationPlan(string Run, string Digest, IReadOnlyList<IsolationEntry> Entries)
{
    public bool IsEmpty => Entries.Count == 0;
}

/// <summary>D11's <c>IsolationPlan.From</c>, pure: the configured closed paths, the run's <c>closedArms</c> resolved through the map and its <c>closed</c> paths, then the unlisted ones (the shell's order, <c>lock.sh:12-14</c>, with the run's set between). Every refusal names the configuration entry it
/// is about, and the first one ends the plan — a snapshot is never made of a plan that would fail half-way (the shell's defect). What it cannot know — whether a path exists, what kind it is, whether a link leads to it — is the adapter's, read before anything is written.</summary>
public static class IsolationPlans
{
    /// <summary>The mode a <see cref="IsolationKind.Closed"/> path is locked to.</summary>
    private const int ClosedMode = 0;

    /// <summary>The mode an <see cref="IsolationKind.Unlisted"/> path is locked to: <c>-wx</c> for the owner.</summary>
    private const int UnlistedMode = 0xC0; // 0o300

    public static int LockedMode(IsolationKind kind) => kind == IsolationKind.Closed ? ClosedMode : UnlistedMode;

    public static Outcome<IsolationPlan> From(IsolationFacts config, RunSpec run, ArmMap map, string root, IReadOnlyList<string> neverClosed)
    {
        return Named(config, run, map, root).Match(
            entries => Problem(entries, neverClosed, root) is { Length: > 0 } problem
                ? Outcome<IsolationPlan>.Failure(problem)
                : Outcome<IsolationPlan>.Success(new IsolationPlan(run.Name, DigestOf(entries), entries)),
            Outcome<IsolationPlan>.Failure);
    }

    /// <summary>SHA-256 over the UTF-8 bytes of one line per entry in plan order, <c>&lt;kind&gt;\t&lt;path&gt;\t&lt;source&gt;\n</c>, kind <c>closed</c> or <c>unlisted</c>, source the configuration entry that named it; 64 lower-case hex characters. The snapshot and the applied file carry it, and the runner and the chain compare it ordinally.</summary>
    public static string DigestOf(IReadOnlyList<IsolationEntry> entries)
    {
        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            text.Append(entry.Kind == IsolationKind.Closed ? "closed" : "unlisted").Append('\t').Append(entry.Path).Append('\t').Append(entry.Source).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>Whether <c>unlock</c> may set a mode on <paramref name="path"/> at all (finding 11): the root, anything under it, anything under <c>&lt;home&gt;/.claude</c>, or a path today's <c>experiment.json</c> names (or something under one). A same-user process can write
    /// <c>.locked-modes</c> by name under a root at 300, so a forged entry must not steer this user's <c>chmod</c> anywhere else.</summary>
    public static bool InBound(string path, string root, string home, IReadOnlyList<string> configured) =>
        (root.Length > 0 && IsUnder(path, root)) || (home.Length > 0 && IsUnder(path, Path.Combine(home, ".claude"))) || configured.Any(each => each.Length > 0 && IsUnder(path, each));

    /// <summary>The path is the folder or inside it, compared ordinally and by whole segments.</summary>
    public static bool IsUnder(string path, string folder)
    {
        var normal = Normal(path);
        var parent = Normal(folder);
        return string.Equals(normal, parent, StringComparison.Ordinal) || normal.StartsWith(parent.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string Normal(string path) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));

    private static Outcome<IReadOnlyList<IsolationEntry>> Named(IsolationFacts config, RunSpec run, ArmMap map, string root)
    {
        var entries = new List<IsolationEntry>();
        entries.AddRange(config.Closed.Select((path, index) => new IsolationEntry(path, IsolationKind.Closed, $"isolation.closed[{index}]")));
        for (var index = 0; index < run.ClosedArms.Count; index++)
        {
            var arm = run.ClosedArms[index];
            var token = map.TokenOf(arm);
            if (token is Outcome<string>.Fail)
            {
                return Outcome<IReadOnlyList<IsolationEntry>>.Failure(
                    $"runs.{run.Name}.closedArms[{index}] names '{arm}', which arms.map.json does not hold — it was never made; make it, or remove it from closedArms");
            }

            entries.Add(new IsolationEntry(new RootPaths(root).Arm(((Outcome<string>.Ok)token).Value).Folder, IsolationKind.Closed, $"runs.{run.Name}.closedArms[{index}]"));
        }

        entries.AddRange(run.Closed.Select((path, index) => new IsolationEntry(path, IsolationKind.Closed, $"runs.{run.Name}.closed[{index}]")));
        entries.AddRange(config.Unlisted.Select((path, index) => new IsolationEntry(path, IsolationKind.Unlisted, $"isolation.unlisted[{index}]")));
        return Normalised(entries);
    }

    private static Outcome<IReadOnlyList<IsolationEntry>> Normalised(List<IsolationEntry> entries)
    {
        var nul = entries.FirstOrDefault(entry => entry.Path.Contains('\0', StringComparison.Ordinal));
        return nul is not null
            ? Outcome<IReadOnlyList<IsolationEntry>>.Failure($"{nul.Source} holds a NUL character — refused")
            : Outcome<IReadOnlyList<IsolationEntry>>.Success([.. entries.Select(entry => entry with { Path = Normal(entry.Path) })]);
    }

    /// <summary>The first problem of the whole list, or the empty string: a path named twice; an entry inside a closed one (whichever is written first — a child of a 000 folder cannot have its mode set, so the pair's second chmod would fail half-way and unlock could not restore it); a closed entry that
    /// is, or contains, a path the run itself stands on. A child of an UNLISTED entry is fine (<c>&lt;root&gt;</c>, <c>&lt;root&gt;/r</c>, <c>&lt;root&gt;/pids</c> is the shell's own set).</summary>
    private static string Problem(IReadOnlyList<IsolationEntry> entries, IReadOnlyList<string> neverClosed, string root) =>
        entries.Select((entry, index) => new[] { Duplicate(entries, index), Nested(entries, index), Forbidden(entry, neverClosed, root) }.FirstOrDefault(found => found.Length > 0) ?? string.Empty).FirstOrDefault(found => found.Length > 0) ?? string.Empty;

    private static string Duplicate(IReadOnlyList<IsolationEntry> entries, int index) =>
        entries.Take(index).FirstOrDefault(earlier => string.Equals(earlier.Path, entries[index].Path, StringComparison.Ordinal)) is { } first
            ? $"{entries[index].Source} {entries[index].Path} is named twice (first by {first.Source})"
            : string.Empty;

    private static string Nested(IReadOnlyList<IsolationEntry> entries, int index) =>
        entries.Where((other, at) => at != index && other.Kind == IsolationKind.Closed && !string.Equals(other.Path, entries[index].Path, StringComparison.Ordinal) && IsUnder(entries[index].Path, other.Path)).FirstOrDefault() is { } outer
            ? $"{entries[index].Source} {entries[index].Path} is inside closed {outer.Path} ({outer.Source}) — a child of a closed folder cannot have its mode set"
            : string.Empty;

    private static string Forbidden(IsolationEntry entry, IReadOnlyList<string> neverClosed, string root)
    {
        if (entry.Kind != IsolationKind.Closed)
        {
            return string.Empty;
        }

        var stood = neverClosed.Select(Normal).FirstOrDefault(path => IsUnder(path, entry.Path));
        return stood is null
            ? string.Empty
            : $"{entry.Source} {entry.Path} is, or contains, {(string.Equals(stood, Normal(root), StringComparison.Ordinal) ? "the experiment root" : stood)} — it cannot be closed";
    }
}
