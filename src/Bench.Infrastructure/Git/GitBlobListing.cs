using Bench.Domain;

namespace Bench.Infrastructure.Git;

/// <summary>The paths and blob ids a git listing names, parsed off its <c>-z</c> output: <see cref="RawDiff"/> (<c>git diff --raw -z
/// --no-abbrev</c> — the index or a commit range; a rename or copy is read under its NEW path, a gitlink (mode 160000, a submodule's commit) is
/// not a blob and is skipped) and <see cref="LsTree"/> (<c>git ls-tree -r -z</c> — a whole tree, blobs only). What <c>GitBlobBatch</c> is fed.</summary>
public static class GitBlobListing
{
    private const string GitlinkMode = "160000";

    private const string Blob = "blob";

    /// <summary>One raw entry's header: the destination mode and object id, and how many paths follow (two for a rename or copy).</summary>
    private sealed record RawHeader(string DestinationMode, string ObjectId, int Paths);

    public static Outcome<IReadOnlyList<GitBlobRef>> RawDiff(string text)
    {
        var tokens = text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var listed = new List<GitBlobRef>();
        var at = 0;

        while (at < tokens.Length)
        {
            var step = RawStep(tokens, at);

            if (step is Outcome<(IReadOnlyList<GitBlobRef> Blobs, int Next)>.Fail fail)
            {
                return Outcome<IReadOnlyList<GitBlobRef>>.Failure(fail.Reason);
            }

            var (blobs, next) = ((Outcome<(IReadOnlyList<GitBlobRef> Blobs, int Next)>.Ok)step).Value;
            listed.AddRange(blobs);
            at = next;
        }

        return Outcome<IReadOnlyList<GitBlobRef>>.Success(listed);
    }

    /// <summary>The entry at <paramref name="at"/>: its header, then its path — the last of two for a rename or copy — and where the next entry starts.</summary>
    private static Outcome<(IReadOnlyList<GitBlobRef> Blobs, int Next)> RawStep(string[] tokens, int at) =>
        Header(tokens[at]).Match(
            header =>
            {
                if (at + header.Paths >= tokens.Length)
                {
                    return Outcome<(IReadOnlyList<GitBlobRef> Blobs, int Next)>.Failure($"raw diff entry '{tokens[at]}' names {header.Paths} path(s) the listing does not carry");
                }

                IReadOnlyList<GitBlobRef> blobs = header.DestinationMode == GitlinkMode ? [] : [new GitBlobRef(tokens[at + header.Paths], header.ObjectId)];
                return Outcome<(IReadOnlyList<GitBlobRef> Blobs, int Next)>.Success((blobs, at + header.Paths + 1));
            },
            Outcome<(IReadOnlyList<GitBlobRef> Blobs, int Next)>.Failure);

    /// <summary><c>:&lt;src mode&gt; &lt;dst mode&gt; &lt;src id&gt; &lt;dst id&gt; &lt;status&gt;</c> — the status letter says whether one path or two follow.</summary>
    private static Outcome<RawHeader> Header(string token) =>
        (token.StartsWith(':') ? token[1..].Split(' ') : []) switch
        {
            [_, var mode, _, var id, { Length: > 0 } status] => Outcome<RawHeader>.Success(new RawHeader(mode, id, status[0] is 'R' or 'C' ? 2 : 1)),
            _ => Outcome<RawHeader>.Failure($"raw diff entry '{token}' is not ':<mode> <mode> <id> <id> <status>'"),
        };

    public static Outcome<IReadOnlyList<GitBlobRef>> LsTree(string text)
    {
        var entries = text.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(TreeEntry).ToList();

        return entries.OfType<Outcome<IReadOnlyList<GitBlobRef>>.Fail>().FirstOrDefault() is { } fail
            ? Outcome<IReadOnlyList<GitBlobRef>>.Failure(fail.Reason)
            : Outcome<IReadOnlyList<GitBlobRef>>.Success([.. entries.OfType<Outcome<IReadOnlyList<GitBlobRef>>.Ok>().SelectMany(ok => ok.Value)]);
    }

    /// <summary><c>&lt;mode&gt; &lt;type&gt; &lt;id&gt;TAB&lt;path&gt;</c>: a blob is listed; a commit (a submodule) or a tree is not.</summary>
    private static Outcome<IReadOnlyList<GitBlobRef>> TreeEntry(string entry) =>
        entry.Split('\t', 2) is [var meta, var path] && meta.Split(' ') is [_, var type, var id]
            ? Outcome<IReadOnlyList<GitBlobRef>>.Success(type == Blob ? [new GitBlobRef(path, id)] : [])
            : Outcome<IReadOnlyList<GitBlobRef>>.Failure($"ls-tree entry '{entry}' is not '<mode> <type> <id>\\t<path>'");
}
