namespace ModelArms.Core.Experiment;

/// <summary>D17's static half: which executables a command may name. A harness never runs a file an arm could write, so an executable
/// is a bare name resolved from PATH, or an absolute path outside every forbidden root. A file under the target may only be an
/// argument of an executable resolved elsewhere, which is why the suite runs as <c>dotnet &lt;tests&gt;.dll</c>.</summary>
public static class ExecutableRule
{
    private static readonly char[] PathMarks = ['/', '\\', ':'];

    /// <summary>Why this executable is refused, or the empty string when it is fine. A name holding a separator or a colon is a PATH:
    /// <c>C:x.exe</c> is drive-relative on Windows, so it is never a bare name.
    /// <para>
    /// This is a static, textual rule. It does not follow symbolic links: a link outside every forbidden root that points inside one
    /// passes here, and E1.S8's executable resolution is what resolves the final target and checks it against the same roots.
    /// </para></summary>
    public static string Refusal(string executable, IReadOnlyList<string> forbiddenRoots) =>
        executable switch
        {
            _ when executable.Trim().Length == 0 => "the executable is empty — name an executable from PATH or an absolute path outside the target (D17)",
            _ when executable.IndexOfAny(PathMarks) < 0 => string.Empty,
            _ when !Path.IsPathFullyQualified(executable) => Relative(executable),
            _ => UnderForbiddenRoot(executable, forbiddenRoots),
        };

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
