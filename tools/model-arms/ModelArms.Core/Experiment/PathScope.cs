namespace ModelArms.Core.Experiment;

/// <summary>The one answer to "is this path equal to or under that root". It compares the normalised text, ordinally, each with a
/// trailing separator, so <c>/a/bc</c> is not under <c>/a/b</c>. Only absolute paths are compared: normalising a relative one would
/// ask the process for its working directory, and Core asks the world nothing.</summary>
internal static class PathScope
{
    public static bool IsEqualOrUnder(string path, string root)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(root))
        {
            return false;
        }

        return WithSeparator(Path.GetFullPath(path)).StartsWith(WithSeparator(Path.GetFullPath(root)), StringComparison.Ordinal);
    }

    private static string WithSeparator(string full) => Path.TrimEndingDirectorySeparator(full) + Path.DirectorySeparatorChar;
}
