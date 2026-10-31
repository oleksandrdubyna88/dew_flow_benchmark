namespace ModelArms.Core.Experiment;

/// <summary>What <c>git config -f .gitmodules --get-regexp ^submodule\..*\.path$</c> printed, asked for the submodule at one path (<c>prep-base.sh:9</c>'s awk): a line is <c>submodule.&lt;name&gt;.path &lt;path&gt;</c>, and the name — which may itself
/// hold dots — is what stands between the first <c>submodule.</c> and the last <c>.path</c>.</summary>
public static class GitModules
{
    private const string Prefix = "submodule.";

    /// <summary>The name of the submodule whose path is <paramref name="path"/> exactly, or the empty text when there is none.</summary>
    public static string NameOf(string getRegexpOutput, string path)
    {
        var tail = ".path " + path;
        return getRegexpOutput.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith(Prefix, StringComparison.Ordinal) && line.EndsWith(tail, StringComparison.Ordinal) && line.Length > Prefix.Length + tail.Length)
            .Select(line => line[Prefix.Length..^tail.Length])
            .FirstOrDefault() ?? string.Empty;
    }
}
