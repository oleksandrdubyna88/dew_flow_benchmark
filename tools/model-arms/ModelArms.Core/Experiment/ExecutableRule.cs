namespace ModelArms.Core.Experiment;

/// <summary>D17's static half: which executables a command may name. A harness never runs a file an arm could write, so an executable
/// is a bare name resolved from PATH, or an absolute path outside every forbidden root. A file under the target may only be an
/// argument of an executable resolved elsewhere, which is why the suite runs as <c>dotnet &lt;tests&gt;.dll</c>.</summary>
public static class ExecutableRule
{
    /// <summary>Why this executable is refused, or the empty string when it is fine.</summary>
    public static string Refusal(string executable, IReadOnlyList<string> forbiddenRoots)
    {
        if (executable.Trim().Length == 0)
        {
            return "the executable is empty — name an executable from PATH or an absolute path outside the target (D17)";
        }

        if (!executable.Contains('/') && !executable.Contains('\\'))
        {
            return string.Empty;
        }

        return Path.IsPathFullyQualified(executable) ? UnderForbiddenRoot(executable, forbiddenRoots) : Relative(executable);
    }

    private static string Relative(string executable) =>
        $"the executable '{executable}' is a relative path, resolved against the target — use a name from PATH or an absolute path outside the target (D17)";

    private static string UnderForbiddenRoot(string executable, IReadOnlyList<string> forbiddenRoots)
    {
        var root = forbiddenRoots.FirstOrDefault(r => PathScope.IsEqualOrUnder(executable, r));
        return root is null
            ? string.Empty
            : $"the executable '{executable}' is under '{root}' — a harness never runs a file an arm could write; pass it as an argument of an executable resolved outside (D17)";
    }
}
