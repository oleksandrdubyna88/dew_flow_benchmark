using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain;
using Bench.Infrastructure.Process;
using ModelArms.Core.Launch;

namespace ModelArms.Adapters;

/// <summary>The harness's own git for one folder (plan D17 d, E2 §8 item 25, E3 C14): launched by the REAL path the verb resolved, with the working directory the folder it is asked about and an environment with every <c>GIT_*</c> of the
/// parent taken out, through the shared launcher's replacing overload and a ceiling of 60 s a question (<see cref="TargetWalls.Git"/>; the clone and the submodule's, <see cref="TargetWalls.Clone"/>). One class for every git the module starts: E3 widens it, never duplicates it. The arm's claude keeps its own environment (D19): two processes, two environments, on purpose. Nothing here names the module's
/// bare-name git helper — the architecture scan is red the moment it does.
/// <para>
/// <b>Residual.</b> Git still READS the clone's config, which the model under test writes. Checked and closed: <c>diff.external</c> and <c>diff.&lt;driver&gt;.command</c> (<c>-c diff.external=</c>, <c>--no-ext-diff</c>), <c>diff.&lt;driver&gt;.textconv</c>
/// (<c>--no-textconv</c>) and <c>core.fsmonitor</c> (<c>-c core.fsmonitor=false</c>, on every question), and a promisor remote's program run by a lazy fetch of a blob the model deleted (<c>GIT_NO_LAZY_FETCH=1</c> in the environment: planted, observed); a pager is not started for a piped stdout; a commit-to-commit diff runs no clean or smudge filter. Not checked: any other key that can name a program
/// on a path these three questions do not take (they are the only ones asked), or a key a later git version adds — the first line of defence is the arm's clone being the model's own, the second that nothing here asks git for more than a hash and a diff.
/// </para>
/// <para>
/// A question git cannot answer is a <see cref="Outcome{T}"/> failure carrying git's first stderr line, or how the attempt ended (<c>exit 128</c>, <c>timed out</c>, <c>not found</c>).
/// </para>
/// <para>
/// <b>The questions E3 added</b> (<c>prep-base</c> asks the first five, <c>make</c> and <c>checks</c> the rest), each started <c>-c core.fsmonitor=false</c>: <c>clone</c> into a destination the verb names and <c>checkout --detach</c> of a hex id, the
/// <c>.gitmodules</c> path query, the submodule's url and <c>submodule update</c> (the one place <c>protocol.file.allow=always</c> is given, for the one path asked); a new branch (a name starting with <c>-</c> is refused); and, over an arm's clone, the
/// uncommitted count, the changed <c>.cs</c> names, <c>numstat</c>, <c>shortstat</c> and the commit count since a base (every diff <c>-c diff.external= … --no-ext-diff --no-textconv</c>, the base a hex id) and the one <c>grep -l -F</c> that asks whether a text is named under some globs.
/// <b>Not closed, and said:</b> <c>status</c> hashes a file whose stat is dirty and so applies the <c>clean</c> filter a <c>.gitattributes</c> of the clone and its config name — the arm wrote both. The first line of defence is that the clone is the model's own and the harness runs it
/// with the operator's rights by design (E3 §8 item 17); the second is that nothing here asks more of it than counts and names.
/// </para></summary>
public sealed partial class ArmGit(string git, string workingDirectory, IReadOnlyDictionary<string, string> environment)
{
    private static readonly TimeSpan Budget = TargetWalls.Git;

    /// <summary>The environment git runs under: the one it was given and <c>GIT_NO_LAZY_FETCH=1</c>. A partial clone names a promisor remote and a program to run for it; a blob the model deleted would make git fetch it — and run that program — on the harness's behalf. With this set the
    /// read fails ("unable to read") and the step stops. The environment variable, not <c>--no-lazy-fetch</c>, which older gits refuse.</summary>
    private readonly IReadOnlyDictionary<string, string> _environment = new Dictionary<string, string>(environment, StringComparer.Ordinal) { ["GIT_NO_LAZY_FETCH"] = "1" };

    /// <summary>Every question starts <c>-c core.fsmonitor=false</c>: the clone is written by the model under test, and its own config must not name a program git runs on the harness's behalf.</summary>
    private static readonly string[] Hardened = ["-c", "core.fsmonitor=false"];

    public async Task<Outcome<string>> HeadAsync(CancellationToken ct) => Trimmed(await AskAsync([.. Hardened, "rev-parse", "HEAD"], ct));

    public async Task<Outcome<string>> HeadShortAsync(CancellationToken ct) => Trimmed(await AskAsync([.. Hardened, "rev-parse", "--short", "HEAD"], ct));

    /// <summary><c>diff --no-ext-diff --no-textconv &lt;from&gt; HEAD</c>, stdout verbatim — the plain patch: a <c>diff.external</c> or a <c>textconv</c> driver the clone's config or <c>.gitattributes</c> names would run a program and its output would BE the measured diff. <paramref name="from"/> must be a commit id in hex: it came from a file, and a file's text is never handed to git as an option.</summary>
    public async Task<Outcome<string>> DiffAsync(string from, CancellationToken ct) =>
        CommitId().IsMatch(from) ? await AskAsync([.. Hardened, "-c", "diff.external=", "diff", "--no-ext-diff", "--no-textconv", from, "HEAD"], ct) : Outcome<string>.Failure($"'{from}' is not a commit id — the snapshot file does not hold one");

    /// <summary><c>clone -q --branch &lt;branch&gt; -- &lt;source&gt; &lt;destination&gt;</c> (<c>prep-base.sh:7</c>), under the clone's wall; the folder this git runs in must exist and <paramref name="destination"/> must not.</summary>
    public async Task<Outcome<string>> CloneAsync(string source, string branch, string destination, CancellationToken ct) =>
        Refusal(branch) is { Length: > 0 } refused
            ? Outcome<string>.Failure(refused)
            : await AskAsync([.. Hardened, "clone", "-q", "--branch", branch, "--", source, destination], ct, TargetWalls.Clone);

    /// <summary><c>checkout -q --detach &lt;sha&gt;</c>: <paramref name="sha"/> is a commit id in hex — it came from a file, and a file's text is never handed to git as an option.</summary>
    public async Task<Outcome<string>> DetachAsync(string sha, CancellationToken ct) =>
        CommitId().IsMatch(sha) ? await AskAsync([.. Hardened, "checkout", "-q", "--detach", sha], ct) : Outcome<string>.Failure(NotACommitId(sha));

    /// <summary><c>config -f .gitmodules --get-regexp ^submodule\..*\.path$</c> (<c>prep-base.sh:9</c>): its lines, or the empty text when the folder has no <c>.gitmodules</c> or none holds a path (git's exit 1).</summary>
    public async Task<Outcome<string>> SubmodulePathsAsync(CancellationToken ct) =>
        File.Exists(Path.Combine(workingDirectory, ".gitmodules"))
            ? await AskAsync([.. Hardened, "config", "-f", ".gitmodules", "--get-regexp", @"^submodule\..*\.path$"], ct, Budget, noAnswer: 1)
            : Outcome<string>.Success(string.Empty);

    public async Task<Outcome<string>> SetSubmoduleUrlAsync(string name, string url, CancellationToken ct) =>
        await AskAsync([.. Hardened, "config", $"submodule.{name}.url", url], ct);

    /// <summary><c>-c protocol.file.allow=always submodule update --init -q -- &lt;path&gt;</c> (<c>prep-base.sh:11</c>), under the clone's wall: the file protocol is allowed for this one command and this one path, whose url the verb has just set to a folder of the operator's own.</summary>
    public async Task<Outcome<string>> SubmoduleUpdateAsync(string path, CancellationToken ct) =>
        await AskAsync([.. Hardened, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q", "--", path], ct, TargetWalls.Clone);

    /// <summary><c>checkout -q -b &lt;branch&gt;</c> in the folder; a name that starts with <c>-</c> or is empty is refused before git is asked.</summary>
    public async Task<Outcome<string>> NewBranchAsync(string branch, CancellationToken ct) =>
        Refusal(branch) is { Length: > 0 } refused ? Outcome<string>.Failure(refused) : await AskAsync([.. Hardened, "checkout", "-q", "-b", branch], ct);

    /// <summary>The number of lines <c>status --porcelain</c> prints (<c>objective-checks.sh:13</c>).</summary>
    public async Task<Outcome<int>> UncommittedCountAsync(CancellationToken ct) =>
        Lines(await AskAsync([.. Hardened, "status", "--porcelain"], ct)).Match(lines => Outcome<int>.Success(lines.Count), Outcome<int>.Failure);

    /// <summary>The files added or modified between <paramref name="baseSha"/> and <c>HEAD</c> that end in <c>.cs</c> (<c>objective-checks.sh:19</c>), each name as git has it: read NUL-separated, so a name with a newline or a quote is one name.</summary>
    public async Task<Outcome<IReadOnlyList<string>>> ChangedCsAsync(string baseSha, CancellationToken ct) =>
        (await DiffOf(baseSha, ["--name-only", "--diff-filter=AM", "-z"], ["--", "*.cs"], ct)).Match(
            text => Outcome<IReadOnlyList<string>>.Success([.. text.Split('\0', StringSplitOptions.RemoveEmptyEntries)]),
            Outcome<IReadOnlyList<string>>.Failure);

    /// <summary><c>diff --numstat &lt;base&gt;..HEAD</c>, stdout verbatim (<c>objective-checks.sh:51</c>).</summary>
    public Task<Outcome<string>> NumstatAsync(string baseSha, CancellationToken ct) => DiffOf(baseSha, ["--numstat"], [], ct);

    /// <summary><c>diff --shortstat &lt;base&gt;..HEAD</c>, stdout verbatim.</summary>
    public Task<Outcome<string>> ShortstatAsync(string baseSha, CancellationToken ct) => DiffOf(baseSha, ["--shortstat"], [], ct);

    /// <summary><c>rev-list --count &lt;base&gt;..HEAD</c> (<c>objective-checks.sh:55</c>).</summary>
    public async Task<Outcome<long>> CountSinceAsync(string baseSha, CancellationToken ct) =>
        !CommitId().IsMatch(baseSha)
            ? Outcome<long>.Failure(NotACommitId(baseSha))
            : (await AskAsync([.. Hardened, "rev-list", "--count", $"{baseSha}..HEAD"], ct)).Match(
                text => long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? Outcome<long>.Success(count) : Outcome<long>.Failure($"git counted '{text.Trim()}' commits"),
                Outcome<long>.Failure);

    /// <summary>Whether <paramref name="text"/> is named, as fixed text, by a file under <paramref name="globs"/> (<c>git grep -l -F -e &lt;text&gt; -- &lt;globs&gt;</c>, <c>objective-checks.sh:37</c>): exit 0 yes, 1 no, anything else git failing to answer.</summary>
    public async Task<Outcome<bool>> MentionsAsync(string text, IReadOnlyList<string> globs, CancellationToken ct) =>
        (await LaunchAsync([.. Hardened, "grep", "-l", "-F", "-e", text, "--", .. globs], ct, Budget)) switch
        {
            ProcessAttempt.Completed { Result.ExitCode: 0 } => Outcome<bool>.Success(true),
            ProcessAttempt.Completed { Result.ExitCode: 1 } => Outcome<bool>.Success(false),
            var other => Outcome<bool>.Failure(Said(other)),
        };

    /// <summary>A commit-to-commit diff with the harness's three guards: no external diff program, no textconv, the plain patch — the clone's config and attributes are the model's.</summary>
    private Task<Outcome<string>> DiffOf(string baseSha, string[] options, string[] pathspec, CancellationToken ct) =>
        CommitId().IsMatch(baseSha)
            ? AskAsync([.. Hardened, "-c", "diff.external=", "diff", "--no-ext-diff", "--no-textconv", .. options, $"{baseSha}..HEAD", .. pathspec], ct)
            : Task.FromResult(Outcome<string>.Failure(NotACommitId(baseSha)));

    private static string NotACommitId(string text) => $"'{text}' is not a commit id — a file holds the id, and a file's text is never handed to git as an option";

    private static string Refusal(string branch) =>
        branch.Length == 0 || branch.StartsWith('-') ? $"'{branch}' is not a branch name git may be given: it is empty or starts with '-'" : string.Empty;

    private static Outcome<IReadOnlyList<string>> LinesOf(string text) => Outcome<IReadOnlyList<string>>.Success([.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)]);

    private static Outcome<IReadOnlyList<string>> Lines(Outcome<string> answer) => answer.Match(LinesOf, Outcome<IReadOnlyList<string>>.Failure);

    private static Outcome<string> Trimmed(Outcome<string> answer) => answer.Match(text => Outcome<string>.Success(text.Trim()), Outcome<string>.Failure);

    private async Task<Outcome<string>> AskAsync(string[] arguments, CancellationToken ct, TimeSpan? budget = null, int? noAnswer = null)
    {
        var attempt = await LaunchAsync(arguments, ct, budget ?? Budget);
        return attempt switch
        {
            ProcessAttempt.Completed { Result.ExitCode: 0 } done => Outcome<string>.Success(done.Result.StandardOutput),
            ProcessAttempt.Completed done when done.Result.ExitCode == noAnswer => Outcome<string>.Success(string.Empty),
            _ => Outcome<string>.Failure(Said(attempt)),
        };
    }

    private Task<ProcessAttempt> LaunchAsync(string[] arguments, CancellationToken ct, TimeSpan budget) =>
        ProcessRunner.RunAsync(git, arguments, workingDirectory, budget, string.Empty, _environment, ct);

    private static string Said(ProcessAttempt attempt) =>
        attempt is ProcessAttempt.Completed done && FirstLine(done.Result.StandardError) is { Length: > 0 } line ? line : attempt.Describe;

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;

    [GeneratedRegex(@"^[0-9a-f]{4,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommitId();
}
