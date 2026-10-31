using System.Globalization;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What an earlier <c>prep-base</c> attempt left in the root, put out of the way of the next one (plan E3.S2 step 2): a half-made <c>base/</c> is removed whole; the attempt's logs and results folder are RENAMED into <c>prep-base.prev-&lt;utc stamp&gt;/</c> — kept, because a log
/// is evidence of why the last attempt failed — so the next attempt's exclusive creates succeed (plan gate F1). A second move within the same second takes <c>prep-base.prev-&lt;stamp&gt;-2</c>, a third <c>-3</c>: the kernel's exclusive <c>mkdir</c> decides which name is free, never a look first (E3 §8 item 49 (c)). Everything is by NAME: the names are <see cref="RootPaths"/>'s, the root is never listed, and nothing here follows a link — a link is removed or renamed as a link, and
/// what it points at is never touched.
/// <para>
/// What this does not do: tell a half-made <c>base/</c> from one an operator built by hand — the gate has refused a root that holds a baseline, and a root without one that holds a <c>base/</c> is, by D18, the leftover of a crashed <c>prep-base</c>.
/// </para></summary>
internal static class BaseLeftovers
{
    /// <summary>Removes <c>base/</c> whole when there is anything at the name. <c>Directory.Delete(recursive)</c> removes a link inside the tree as a link and never enters it; a link AT the name is unlinked. The answer is whether something was removed.</summary>
    public static Outcome<bool> RemoveBase(string folder) =>
        KernelFacts.Present(folder).Match(present => present ? Removed(folder) : Outcome<bool>.Success(false), Outcome<bool>.Failure);

    /// <summary>The most folders one stamp can take (<c>prep-base.prev-&lt;stamp&gt;</c>, then <c>-2</c> … <c>-N</c>): a bound, so a host whose <c>mkdir</c> answers "there" for every name ends the verb instead of spinning.</summary>
    internal const int SameSecondMoves = 999;

    /// <summary>Renames what the root holds of the last attempt into <c>prep-base.prev-&lt;stamp&gt;</c> — or <c>-2</c>, <c>-3</c> … when that name is taken — made exclusively; the answer is the folder and the names moved (none: nothing was there and no folder was made, the folder is empty text). <paramref name="npmLogs"/> is how many <c>base-npm-N.log</c> this configuration
    /// will write; the earlier attempt's are probed by name from 1 until one is absent, and at least that many.</summary>
    public static Outcome<MovedAside> MoveAside(RootPaths root, int npmLogs, string stamp, PlatformRoad road)
    {
        var present = Names(root, npmLogs).Select(path => (Path: path, Is: KernelFacts.Present(path))).ToList();
        if (present.Select(entry => entry.Is).OfType<Outcome<bool>.Fail>().FirstOrDefault() is { } unknown)
        {
            return Outcome<MovedAside>.Failure(unknown.Reason);
        }

        var left = present.Where(entry => entry.Is is Outcome<bool>.Ok { Value: true }).Select(entry => entry.Path).ToList();
        return left.Count == 0 ? Outcome<MovedAside>.Success(new MovedAside(string.Empty, [])) : Moved(left, root, stamp, 1, road);
    }

    /// <summary>The folder name of the <paramref name="n"/>th move of one stamp: the stamp itself, then <c>&lt;stamp&gt;-2</c>, <c>-3</c> …</summary>
    private static string Numbered(string stamp, int n) => n == 1 ? stamp : $"{stamp}-{n.ToString(CultureInfo.InvariantCulture)}";

    private static Outcome<MovedAside> Moved(IReadOnlyList<string> left, RootPaths root, string stamp, int n, PlatformRoad road)
    {
        var previous = root.PrepBasePrevious(Numbered(stamp, n));
        return ExclusiveFolder.Make(previous, road) switch
        {
            FolderMade.Made => Moved(left, previous).Map(names => new MovedAside(previous, names)),
            FolderMade.AlreadyThere when n < SameSecondMoves => Moved(left, root, stamp, n + 1, road),
            FolderMade.AlreadyThere => Outcome<MovedAside>.Failure($"{previous} and the {(SameSecondMoves - 1).ToString(CultureInfo.InvariantCulture)} numbered folders after it already exist — run again in a second"),
            FolderMade.Failed failed => Outcome<MovedAside>.Failure($"cannot create {previous}: {failed.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static Outcome<IReadOnlyList<string>> Moved(IReadOnlyList<string> left, string previous)
    {
        try
        {
            foreach (var path in left)
            {
                Rename(path, Path.Combine(previous, Path.GetFileName(path)));
            }

            return Outcome<IReadOnlyList<string>>.Success([.. left.Select(Path.GetFileName).Cast<string>()]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<IReadOnlyList<string>>.Failure($"cannot move the last attempt's files into {previous}: {ex.Message}");
        }
    }

    /// <summary>The names the last attempt may have made, in the order it made them; the npm logs by number.</summary>
    private static IEnumerable<string> Names(RootPaths root, int npmLogs)
    {
        yield return root.PrepBaseLog;
        yield return root.BaseBuildLog;
        for (var n = 1; n <= npmLogs || KernelFacts.Present(root.BaseNpmLog(n)) is Outcome<bool>.Ok { Value: true }; n++)
        {
            yield return root.BaseNpmLog(n);
        }

        yield return root.BaseSuiteLog;
        yield return root.BaseExtensionLog;
        yield return root.BaseResults;
    }

    private static Outcome<bool> Removed(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.Directory) && !attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.Delete(path);
            }

            return Outcome<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<bool>.Failure($"cannot remove {path}: {ex.Message}");
        }
    }

    /// <summary>One rename, of a file, a link (live or dangling) or a folder — never a copy, never through a link. The runtime reports a link to a folder as a folder, and renames it as the link it is.</summary>
    internal static void Rename(string from, string to)
    {
        if (File.GetAttributes(from).HasFlag(FileAttributes.Directory))
        {
            Directory.Move(from, to);
        }
        else
        {
            File.Move(from, to);
        }
    }

    /// <summary>The UTC stamp of a moved-aside attempt, like <c>20261030T101500Z</c> (<see cref="RootPaths.PrepBasePrevious"/>).</summary>
    public static string Stamp(DateTimeOffset now) => now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
}

/// <summary>What <see cref="BaseLeftovers.MoveAside"/> did: the folder the last attempt's files went into (empty text when nothing was there) and the names moved.</summary>
internal sealed record MovedAside(string Folder, IReadOnlyList<string> Names);
