using Bench.Domain.Scouts;

namespace Bench.Infrastructure.Scouts;

/// <summary>Where the detail of a PRIVATE task's refusal goes when a public writer may not carry it (<see cref="ScoutPublication.Refusal"/>):
/// appended to <c>&lt;trees root&gt;/.scout-trees/&lt;label&gt;.withheld.txt</c> — the tree builder's own folder under a root that is outside every
/// checkout or git-ignored by rule — and the public sentence names the label, the harness's own summary and that file. The detail is git's
/// or the filesystem's words, which quote urls and paths; the summary is ours.</summary>
public static class ScoutWithheld
{
    public static string Refusal(string treesRoot, ScoutTaskPrivacy privacy, string label, string summary, string detail)
    {
        if (privacy == ScoutTaskPrivacy.Private)
        {
            Write(treesRoot, label, summary, detail);
        }

        return ScoutPublication.Refusal(privacy, label, summary, detail);
    }

    private static void Write(string treesRoot, string label, string summary, string detail)
    {
        var path = Path.Combine(Path.GetFullPath(treesRoot), ScoutPublication.WithheldFile(label).Replace('/', Path.DirectorySeparatorChar));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {summary}\n{detail}\n\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The detail could not be kept; the public sentence still says where it would have been, and the verb's exit code says it failed.
        }
    }
}
