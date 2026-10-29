namespace ModelArms.Core.Experiment;

/// <summary>The one answer to "is this path equal to or under that root". It compares the normalised text, ignoring case, each with a
/// trailing separator, so <c>/a/bc</c> is not under <c>/a/b</c>. Ignoring case fails closed: on a case-sensitive host it can only refuse
/// a path that differs from a forbidden root by case alone, never admit one a case-insensitive host would resolve into it. Only absolute paths are compared: normalising a relative one would
/// ask the process for its working directory, and Core asks the world nothing.</summary>
internal static class PathScope
{
    public static bool IsEqualOrUnder(string path, string root)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(root))
        {
            return false;
        }

        return WithSeparator(Path.GetFullPath(path)).StartsWith(WithSeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase);
    }

    private static string WithSeparator(string full) => Path.TrimEndingDirectorySeparator(full) + Path.DirectorySeparatorChar;
}
