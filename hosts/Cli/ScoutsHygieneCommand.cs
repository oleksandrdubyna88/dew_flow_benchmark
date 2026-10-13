using System.Globalization;
using System.Text;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Git;

namespace Bench.Cli;

/// <summary><c>bench scouts hygiene --staged | --range &lt;a..b&gt; | --tree &lt;rev&gt; [--repo &lt;dir&gt;] [--deny-list &lt;file&gt;]</c> (E2 S4, §3.13) — the
/// SECOND line of the public-repository hygiene, behind the projection: every staged blob, or every blob a push would carry, read WHOLE from
/// git — never the working tree, which is not what is committed — through ONE <c>git cat-file --batch</c> (<see cref="GitBlobBatch"/>, each
/// announced as <c>[i/n]</c> as it arrives), and scanned against the operator's local deny-list (<c>--deny-list</c>, else
/// <c>BENCH_SCOUT_DENYLIST</c>): a text blob with <see cref="ScoutScrubber.Leftovers"/> (home paths, e-mails, the deny-list), a BINARY blob — a
/// NUL in its first 8 KB — by its bytes (<see cref="ScoutScrubber.BinaryLeftovers"/>: every entry as UTF-8 and UTF-16LE, ASCII case folded);
/// every file NAME is scanned too. A leftover is named by rule, never by value, and the path that carries a private name is printed with the
/// name redacted.
/// <para>
/// It REFUSES rather than scans less: no deny-list named (4 — a scan without the list it exists for would pass forever), a named list that
/// does not load (4), git failing to list or hand over a blob (3). A hit exits 1 — the scan ran and the answer is bad. <c>.githooks/pre-commit</c>
/// and <c>.githooks/pre-push</c> call it; they are opt-in (<c>git config core.hooksPath .githooks</c>), never installed by a verb.
/// </para></summary>
public static class ScoutsHygieneCommand
{
    private static readonly TimeSpan ListWall = TimeSpan.FromSeconds(60);

    /// <summary>The wall of the one batch read — every blob of a whole tree (<c>--tree</c>) on one pipe, scanned in-process as git streams it.</summary>
    private static readonly TimeSpan BatchWall = TimeSpan.FromMinutes(10);

    private const string CannotScan = "scouts hygiene cannot scan — ";

    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var repository = Path.GetFullPath(command.Value("repo", Directory.GetCurrentDirectory()));
        var modes = new[] { command.Has("staged"), command.Has("range"), command.Has("tree") }.Count(m => m);

        if (modes != 1)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts hygiene scans exactly one of --staged, --range <a..b> or --tree <rev>");
        }

        if (!ScoutsInputs.DenyListNamed(command))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration,
                $"scouts hygiene needs the deny-list — pass --deny-list <file> or set {ScoutsInputs.DenyListVariable}; a scan without the list of private names would pass forever");
        }

        return await ScoutsInputs.DenyList(command).Match(
            denyList => ScanAsync(command, repository, denyList, output, error, cancellationToken),
            reason => Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, reason)));
    }

    private static async Task<int> ScanAsync(CommandLine command, string repository, ScrubDenyList denyList, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var names = PrivateNames.Of(denyList.Entries);
        var listed = await ListAsync(command, repository, cancellationToken);

        if (listed is Outcome<IReadOnlyList<GitBlobRef>>.Fail cannot)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, CannotScan + FailureRedaction.Redact(cannot.Reason, names));
        }

        var blobs = ((Outcome<IReadOnlyList<GitBlobRef>>.Ok)listed).Value;
        var scanned = await GitBlobBatch.ReadAsync(
            repository, [.. blobs.Select(b => b.ObjectId)], BatchWall, (at, bytes) => Scanned(blobs[at], at, blobs.Count, bytes.Span, denyList, names, output), cancellationToken);

        return scanned.Match(
            leftovers => Report(blobs.Count, [.. leftovers.SelectMany(l => l)], output, error),
            reason => GateRunCommand.Refuse(error, ExitCodes.Environment, CannotScan + FailureRedaction.Redact(reason, names)));
    }

    /// <summary>One blob as it arrives off the batch: announced with its place (the path redacted), then what it leaves behind.</summary>
    private static IReadOnlyList<string> Scanned(GitBlobRef blob, int at, int count, ReadOnlySpan<byte> bytes, ScrubDenyList denyList, PrivateNames names, TextWriter output)
    {
        var shown = FailureRedaction.Redact(blob.Path, names);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"scanning       [{at + 1}/{count}] {shown}"));
        return Leftovers(blob.Path, shown, bytes, denyList, names);
    }

    /// <summary>What one file leaves behind: a deny-list entry in its NAME, then every leftover rule in its content — a text blob's as text, a
    /// BINARY blob's by its bytes (E2 code round: a binary blob used to be scanned by name only).</summary>
    private static IReadOnlyList<string> Leftovers(string path, string shown, ReadOnlySpan<byte> bytes, ScrubDenyList denyList, PrivateNames names)
    {
        IReadOnlyList<string> inName = names.FirstIn(path) is var entry and > 0 ? [$"{shown}: the file name carries deny-list entry #{entry}"] : [];
        var inContent = ScoutScrubber.IsBinary(bytes) ? ScoutScrubber.BinaryLeftovers(bytes, denyList) : ScoutScrubber.Leftovers(Encoding.UTF8.GetString(bytes), denyList);

        return [.. inName, .. inContent.Select(rule => $"{shown}: {rule}")];
    }

    private static int Report(int files, IReadOnlyList<string> leftovers, TextWriter output, TextWriter error)
    {
        foreach (var leftover in leftovers)
        {
            output.WriteLine($"leftover       {leftover}");
        }

        output.WriteLine($"hygiene        {files} file(s) scanned whole from git, {leftovers.Count} leftover(s)");

        return leftovers.Count == 0
            ? ExitCodes.Pass
            : GateRunCommand.Refuse(error, ExitCodes.Regression, $"{leftovers.Count} leftover(s) would publish a home path, an e-mail or a deny-list entry — fix them before committing or pushing");
    }

    /// <summary>The blobs of the chosen mode — the index (<c>diff --cached --raw</c>), a commit range (read at its end), or one revision's whole
    /// tree (<c>ls-tree -r</c>) — ordered by path, each with the id the batch reads it by.</summary>
    private static Task<Outcome<IReadOnlyList<GitBlobRef>>> ListAsync(CommandLine command, string repository, CancellationToken cancellationToken) =>
        (command.Has("staged"), command.Has("range")) switch
        {
            (true, _) => ListedAsync(repository, GitBlobListing.RawDiff, cancellationToken, "diff", "--cached", "--raw", "-z", "--no-abbrev", "--diff-filter=ACMR"),
            (_, true) => RangeAsync(command.Value("range"), repository, cancellationToken),
            _ => ListedAsync(repository, GitBlobListing.LsTree, cancellationToken, "ls-tree", "-r", "-z", command.Value("tree")),
        };

    private static Task<Outcome<IReadOnlyList<GitBlobRef>>> RangeAsync(string range, string repository, CancellationToken cancellationToken)
    {
        var ends = range.Split("..", 2, StringSplitOptions.TrimEntries);

        return ends.Length == 2 && ends.All(e => e.Length > 0)
            ? ListedAsync(repository, GitBlobListing.RawDiff, cancellationToken, "diff", "--raw", "-z", "--no-abbrev", "--diff-filter=ACMR", $"{ends[0]}..{ends[1]}")
            : Task.FromResult(Outcome<IReadOnlyList<GitBlobRef>>.Failure($"--range takes <from>..<to>, got '{range}'"));
    }

    private static async Task<Outcome<IReadOnlyList<GitBlobRef>>> ListedAsync(
        string repository, Func<string, Outcome<IReadOnlyList<GitBlobRef>>> parse, CancellationToken cancellationToken, params string[] listArgv)
    {
        if (!Directory.Exists(repository))
        {
            return Outcome<IReadOnlyList<GitBlobRef>>.Failure($"{repository} is not a directory");
        }

        var listed = await GitCommand.ReadAsync(repository, ListWall, cancellationToken, listArgv);

        return listed.Match(
            text => parse(text).Match(
                blobs => Outcome<IReadOnlyList<GitBlobRef>>.Success([.. blobs.OrderBy(b => b.Path, StringComparer.Ordinal)]),
                Outcome<IReadOnlyList<GitBlobRef>>.Failure),
            Outcome<IReadOnlyList<GitBlobRef>>.Failure);
    }
}
