using System.Text.RegularExpressions;
using Bench.Domain;
using Bench.Tests.Leftovers;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scratch folder the 9p tests may use, named by the operator in <c>MODEL_ARMS_DRVFS_SCRATCH</c> and checked before anything is written: an absolute, existing folder BELOW a mount of type <c>9p</c> (the Windows drive under WSL), judged by its REAL path (links followed), that holds
/// NOTHING but owner-form entries of this repository and their claim files — what a killed run of these tests leaves, which the test's start sweep then judges. Anything else refuses, naming it: nothing foreign is ever touched. A refusal is a failure, not a skip: the operator named the folder.
/// <para>
/// What this does NOT prove: the 9p mount's own behaviour (the real run does, once); a folder that changes between the check and the test.
/// </para></summary>
internal static class DrvfsScratch
{
    internal const string Variable = "MODEL_ARMS_DRVFS_SCRATCH";

    /// <summary>The REAL path of the folder when it may be used, or why not. <paramref name="mountLines"/> are <c>/proc/mounts</c> lines (the mount point is escaped there: a space is <c>\040</c>, a tab <c>\011</c>, a backslash <c>\134</c>); <paramref name="realPath"/> places a path where it really is.</summary>
    internal static Outcome<string> Check(string? value, IEnumerable<string> mountLines, Func<string, Outcome<string>> realPath)
    {
        if (string.IsNullOrEmpty(value) || !Path.IsPathFullyQualified(value) || !Directory.Exists(value))
        {
            return Outcome<string>.Failure($"{Variable} is '{value}': name an absolute path of an existing folder below a 9p mount");
        }

        return realPath(value).Match(real => Judged(real, mountLines, realPath), reason => Outcome<string>.Failure($"{Variable}: {value} cannot be resolved ({reason})"));
    }

    /// <summary>The check for a skip or a failure in a test: unset or empty skips with the operator's opt-in sentence; a refusal fails naming why. Returns the real folder.</summary>
    internal static string Required()
    {
        var value = System.Environment.GetEnvironmentVariable(Variable) ?? string.Empty;
        Assert.SkipWhen(value.Length == 0, $"{Variable} names no writable folder on a 9p mount (the Windows drive under WSL) — no test writes the operator's Windows drive on its own");
        var answer = Check(value, File.ReadLines("/proc/mounts"), RootFacts.RealPathOf);
        Assert.False(answer.Failed(), answer.Failed() ? answer.Reason() : string.Empty);
        return answer.Ok();
    }

    private static Outcome<string> Judged(string real, IEnumerable<string> mountLines, Func<string, Outcome<string>> realPath)
    {
        var mounts = mountLines.Select(line => line.Split(' ')).Where(fields => fields.Length > 2).Select(fields => (Point: Unescaped(fields[1]), Type: fields[2])).ToList();
        string? type = null;
        var best = -1;
        foreach (var (point, kind) in mounts)
        {
            var found = realPath(point);
            if (found is Outcome<string>.Fail fail && AtOrAbove(point, real))
            {
                return Outcome<string>.Failure($"{Variable}: the mount point {point} above {real} cannot be resolved ({fail.Reason})");
            }

            if (found is Outcome<string>.Ok { Value: var top } && AtOrAbove(top, real) && top.TrimEnd('/').Length >= best)
            {
                (best, type) = (top.TrimEnd('/').Length, kind);
            }
        }

        if (type != "9p")
        {
            return Outcome<string>.Failure($"{Variable}: {real} is below a mount of type {type ?? "(none found)"}, not 9p");
        }

        var foreign = Directory.EnumerateFileSystemEntries(real).Select(Path.GetFileName).FirstOrDefault(name => !OwnerForm(name!));
        return foreign is null
            ? Outcome<string>.Success(real)
            : Outcome<string>.Failure($"{Variable}: {real} holds {foreign}, which is not an owner-form entry of this repository or its claim — name a folder that holds nothing else");
    }

    /// <summary>True when <paramref name="path"/> is directly inside <paramref name="folder"/> (its parent is exactly that folder).</summary>
    internal static bool IsDirectChild(string folder, string path) =>
        string.Equals(Path.GetDirectoryName(path), folder.TrimEnd('/'), StringComparison.Ordinal);

    private static bool OwnerForm(string name) =>
        Owned.FullName().IsMatch(name) || (name.EndsWith(".owner", StringComparison.Ordinal) && Owned.FullName().IsMatch(name[..^6]));

    /// <summary>True when <paramref name="ancestor"/> is <paramref name="path"/> or a folder above it, compared segment by segment (<c>/mnt/c</c> is not above <c>/mnt/cx/f</c>).</summary>
    private static bool AtOrAbove(string ancestor, string path) =>
        ancestor.TrimEnd('/') is var top && (top.Length == 0 || path == top || path.StartsWith(top + "/", StringComparison.Ordinal));

    private static string Unescaped(string field) =>
        Regex.Replace(field, @"\\([0-7]{3})", match => ((char)Convert.ToInt32(match.Groups[1].Value, 8)).ToString());
}
