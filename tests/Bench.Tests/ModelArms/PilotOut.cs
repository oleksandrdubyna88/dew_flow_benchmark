using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace Bench.Tests.ModelArms;

/// <summary>The ONE place the attended pilots' scaffolds learn where they may write (row E6-l of <c>todo/PLAN_test_leftovers_E6.md</c>): <c>BENCH_PILOT_OUT</c>, a folder the OPERATOR names. What a pilot makes must OUTLIVE the run, so it is never made in the temp root (a WSL restart empties it; no run's sweep may collect it) and never under the prefix of
/// <c>common.test-leftovers</c>: the output is unprefixed, named by a manifest the scaffold writes first, collected by no sweep, and removed only by the pilot's own teardown.
/// <para>
/// <see cref="Resolve"/> refuses, in this order and each time with a sentence naming <c>BENCH_PILOT_OUT</c>: unset; empty; relative; not an existing folder; at or under the temp root (both resolved through links); inside a git checkout (<c>init</c> refuses a root there, <c>ExperimentFile.cs:70</c>). Otherwise the folder's full path.
/// The refusal texts follow <c>BENCH_ARTIFACT_ROOT</c> (<c>hosts/Cli/ProbesInputs.cs</c>).
/// </para>
/// <para>
/// What this does NOT prove: that the folder is writable, or that nothing else uses it (the operator names it); a <c>BENCH_PILOT_OUT</c> under <c>/tmp</c> itself when <c>TMPDIR</c> points elsewhere (the temp root compared with is the EFFECTIVE one).
/// </para></summary>
internal static class PilotOut
{
    internal const string Variable = "BENCH_PILOT_OUT";

    private const string Advice = "name an existing folder outside every git checkout and outside the temp root";

    /// <summary>The folder the scaffold writes under, or why it may not: <paramref name="value"/> is the variable's value (null when unset), <paramref name="tempRoot"/> the temp root it must not be at or under, <paramref name="gitTopOf"/> the nearest checkout above a path (empty for none). Each step is its own method.</summary>
    internal static Outcome<string> Resolve(string? value, string tempRoot, Func<string, string> gitTopOf) =>
        (Unset(value) ?? NotAFolder(value!)) is { } problem ? Refused(problem) : Placed(value!, tempRoot, gitTopOf);

    /// <summary>The folder from the variable, or <see cref="InvalidOperationException"/> carrying <see cref="Resolve"/>'s reason: what a scaffold calls before anything is made. Reads the variable, the effective temp root (<see cref="TempDir.Root"/>, a path only read for the comparison) and <see cref="RootFacts.GitTopOf"/>.</summary>
    internal static string Required() =>
        Resolve(System.Environment.GetEnvironmentVariable(Variable), TempDir.Root, RootFacts.GitTopOf).Match(
            folder => folder,
            reason => throw new InvalidOperationException(reason));

    private static string? Unset(string? value) =>
        value switch
        {
            null => $"{Variable} is not set — {Advice}",
            "" => $"{Variable} is empty — {Advice}",
            _ => null,
        };

    private static string? NotAFolder(string value) =>
        !Path.IsPathFullyQualified(value) ? $"{Variable} is '{value}', not an absolute path — {Advice}"
        : !Directory.Exists(value) ? $"{Variable} is '{value}', which is not an existing folder — make it first: {Advice}"
        : null;

    /// <summary>The folder placed where it really is, then judged against the temp root and the checkouts.</summary>
    private static Outcome<string> Placed(string value, string tempRoot, Func<string, string> gitTopOf)
    {
        var folder = Path.GetFullPath(value);
        return RealOf(folder).Match(
            real => Below(value, folder, real, tempRoot, gitTopOf),
            reason => Refused($"{Variable} is '{value}', which cannot be resolved ({reason})"));
    }

    private static Outcome<string> Below(string value, string folder, string real, string tempRoot, Func<string, string> gitTopOf) =>
        RealOf(tempRoot).Match(
            root => Under(real, root) ? Refused($"{Variable} is '{value}', at or under the temp root {root}, which a restart empties and a sweep may collect — {Advice}") : NotInCheckout(value, folder, gitTopOf),
            reason => Refused($"{Variable}: the temp root {tempRoot} cannot be resolved ({reason})"));

    private static Outcome<string> NotInCheckout(string value, string folder, Func<string, string> gitTopOf) =>
        gitTopOf(folder) is { Length: > 0 } checkout
            ? Refused($"{Variable} is '{value}', inside the git checkout {checkout} — {Advice}")
            : Outcome<string>.Success(folder);

    private static Outcome<string> RealOf(string path) => ExecutableResolution.RealPathClimbing(Path.GetFullPath(path), RootFacts.Host);

    /// <summary>True when <paramref name="real"/> is <paramref name="root"/> or below it, compared segment by segment (<c>/tmpx</c> is not under <c>/tmp</c>).</summary>
    private static bool Under(string real, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var top = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(real.TrimEnd(Path.DirectorySeparatorChar), top, comparison)
            || real.StartsWith(top + Path.DirectorySeparatorChar, comparison);
    }

    private static Outcome<string> Refused(string reason) => Outcome<string>.Failure(reason);
}
