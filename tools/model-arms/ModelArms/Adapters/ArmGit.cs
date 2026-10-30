using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;

namespace ModelArms.Adapters;

/// <summary>The harness's own git for one folder (plan D17 d, E2 §8 item 25, E3 C14): launched by the REAL path the verb resolved, with the working directory the folder it is asked about and an environment with every <c>GIT_*</c> of the
/// parent taken out (and every secret-named one: <see cref="TargetEnvironment"/>), through the shared launcher's replacing overload and a ceiling of 60 s a question (<see cref="TargetWalls.Git"/>; the clone and the submodule's, <see cref="TargetWalls.Clone"/>). One class for every git the module starts: E3 widens it, never duplicates it. The arm's claude keeps its own environment (D19): two processes, two environments, on purpose. Nothing here names the module's
/// bare-name git helper — the architecture scan is red the moment it does.
/// <para>
/// <b>What git is kept from running</b> (measured on git 2.53.0, each by a planted program writing a sentinel; the tests keep observing it on the host's git). Every question: <c>-c core.fsmonitor=false</c>; <c>-c core.hooksPath=</c> (<c>status</c> runs <c>post-index-change</c> and <c>checkout -b</c> runs
/// <c>post-checkout</c>, from the clone's hooks folder or an arm-written <c>core.hooksPath</c>; the empty path stopped both); <c>GIT_NO_LAZY_FETCH=1</c> (a promisor remote's program run by a lazy fetch of a blob the model deleted); and, except for <c>clone</c>, <c>checkout --detach</c> and <c>submodule update</c>
/// (they write the base's worktree from the operator's objects — an LFS target's smudge must run there), every filter driver in any scope git itself reads (<c>config --name-only -z --get-regexp ^filter\.</c>: local, worktree, global, system, includes) is neutralised through <c>GIT_CONFIG_COUNT</c>/<c>KEY_n</c>/<c>VALUE_n</c> —
/// <c>clean</c>, <c>smudge</c>, <c>process</c> empty, <c>required</c> false — because <c>status</c> and <c>checkout -b</c> hash a stat-dirty file and ran its <c>clean</c> and its <c>process</c>. By the variables, not <c>-c</c>: <c>-c</c> splits at the first <c>=</c>, and a driver named <c>a=b</c> passed <c>-c</c> (the program ran)
/// and was stopped by the variable. <c>status</c> carries <c>--ignore-submodules=dirty</c>: a submodule is a second repository whose config the enumeration does not see, and git spawned a status in it that ran its driver — a changed submodule commit is still a line, an uncommitted edit inside one is not (E3 §8 item 40).
/// Every diff carries <c>--submodule=short</c> beside <c>-c diff.external= --no-ext-diff --no-textconv</c>: under <c>diff.submodule=diff</c> git spawned <c>git diff</c> in the submodule and ran a <c>diff.&lt;driver&gt;.command</c> of its config past <c>--no-ext-diff</c>, which the child does not inherit. The environment is
/// <c>GitEnvironment.Of(TargetEnvironment.Of(parent))</c>, built HERE whatever the caller hands in: no secret-named variable reaches a program git might still run. Measured to run nothing: <c>grep -l -F</c>, <c>rev-parse</c>, <c>rev-list --count</c>, <c>config</c>, commit-to-commit <c>--numstat</c>/<c>--shortstat</c> under <c>diff.submodule=diff</c>, a pager for a piped
/// stdout with <c>core.pager</c> and <c>pager.status</c> planted. <b>Residuals:</b> a key a later git adds; a program the arm's code left RUNNING between the enumeration and the question (E3 §8 item 17); one more launch per question, under the same budget — an <c>include.path</c> to a FIFO hangs it until the budget kills it (fail closed).
/// </para>
/// <para>
/// A question git cannot answer is a <see cref="Outcome{T}"/> failure carrying git's first stderr line, or how the attempt ended (<c>exit 128</c>, <c>timed out</c>, <c>not found</c>).
/// </para>
/// <para>
/// <b>The questions E3 added</b> (<c>prep-base</c> asks the first five, <c>make</c> and <c>checks</c> the rest), each started <c>-c core.fsmonitor=false</c>: <c>clone</c> into a destination the verb names and <c>checkout --detach</c> of a hex id, the
/// <c>.gitmodules</c> path query, the submodule's url and <c>submodule update</c> (the one place <c>protocol.file.allow=always</c> is given, for the one path asked); a new branch (a name starting with <c>-</c> is refused); and, over an arm's clone, the
/// uncommitted count, the changed <c>.cs</c> names, <c>numstat</c>, <c>shortstat</c> and the commit count since a base (every diff <c>-c diff.external= … --no-ext-diff --no-textconv</c>, the base a hex id) and the one <c>grep -l -F</c> that asks whether a text is named under some globs.
/// </para></summary>
public sealed partial class ArmGit(string git, string workingDirectory, IReadOnlyDictionary<string, string> environment)
{
    private static readonly TimeSpan Budget = TargetWalls.Git;

    private const string Filter = "filter.";

    /// <summary>The environment git runs under: the one it was given, reduced here (<c>GitEnvironment.Of(TargetEnvironment.Of(…))</c>: no <c>GIT_*</c>, no secret-named variable, no agent socket), and <c>GIT_NO_LAZY_FETCH=1</c>. A partial clone names a promisor remote and a program to run for it; a blob the model deleted would make git fetch it — and run that program — on the harness's behalf. With this set the
    /// read fails ("unable to read") and the step stops. The environment variable, not <c>--no-lazy-fetch</c>, which older gits refuse.</summary>
    private readonly IReadOnlyDictionary<string, string> _environment = new Dictionary<string, string>(GitEnvironment.Of(TargetEnvironment.Of(environment)), StringComparer.Ordinal) { ["GIT_NO_LAZY_FETCH"] = "1" };

    /// <summary>Every question starts <c>-c core.fsmonitor=false -c core.hooksPath=</c> (the first pair first: tests key on it): the clone is written by the model under test, and its own config and hooks folder must not name a program git runs on the harness's behalf.</summary>
    private static readonly string[] Hardened = ["-c", "core.fsmonitor=false", "-c", "core.hooksPath="];

    public async Task<Outcome<string>> HeadAsync(CancellationToken ct) => Trimmed(await AskAsync([.. Hardened, "rev-parse", "HEAD"], ct));

    public async Task<Outcome<string>> HeadShortAsync(CancellationToken ct) => Trimmed(await AskAsync([.. Hardened, "rev-parse", "--short", "HEAD"], ct));

    /// <summary><c>diff --no-ext-diff --no-textconv &lt;from&gt; HEAD</c>, stdout verbatim — the plain patch: a <c>diff.external</c> or a <c>textconv</c> driver the clone's config or <c>.gitattributes</c> names would run a program and its output would BE the measured diff. <paramref name="from"/> must be a commit id in hex: it came from a file, and a file's text is never handed to git as an option.</summary>
    public async Task<Outcome<string>> DiffAsync(string from, CancellationToken ct) =>
        CommitId().IsMatch(from) ? await AskAsync([.. Hardened, "-c", "diff.external=", "diff", "--no-ext-diff", "--no-textconv", "--submodule=short", from, "HEAD"], ct) : Outcome<string>.Failure($"'{from}' is not a commit id — the snapshot file does not hold one");

    /// <summary><c>clone -q --branch &lt;branch&gt; -- &lt;source&gt; &lt;destination&gt;</c> (<c>prep-base.sh:7</c>), under the clone's wall; the folder this git runs in must exist and <paramref name="destination"/> must not.</summary>
    public async Task<Outcome<string>> CloneAsync(string source, string branch, string destination, CancellationToken ct) =>
        Refusal(branch) is { Length: > 0 } refused
            ? Outcome<string>.Failure(refused)
            : await AskAsync([.. Hardened, "clone", "-q", "--branch", branch, "--", source, destination], ct, TargetWalls.Clone, smudges: true);

    /// <summary><c>checkout -q --detach &lt;sha&gt;</c>: <paramref name="sha"/> is a commit id in hex — it came from a file, and a file's text is never handed to git as an option.</summary>
    public async Task<Outcome<string>> DetachAsync(string sha, CancellationToken ct) =>
        CommitId().IsMatch(sha) ? await AskAsync([.. Hardened, "checkout", "-q", "--detach", sha], ct, smudges: true) : Outcome<string>.Failure(NotACommitId(sha));

    /// <summary><c>config -f .gitmodules --get-regexp ^submodule\..*\.path$</c> (<c>prep-base.sh:9</c>): its lines, or the empty text when the folder has no <c>.gitmodules</c> or none holds a path (git's exit 1).</summary>
    public async Task<Outcome<string>> SubmodulePathsAsync(CancellationToken ct) =>
        File.Exists(Path.Combine(workingDirectory, ".gitmodules"))
            ? await AskAsync([.. Hardened, "config", "-f", ".gitmodules", "--get-regexp", @"^submodule\..*\.path$"], ct, Budget, noAnswer: 1)
            : Outcome<string>.Success(string.Empty);

    public async Task<Outcome<string>> SetSubmoduleUrlAsync(string name, string url, CancellationToken ct) =>
        await AskAsync([.. Hardened, "config", $"submodule.{name}.url", url], ct);

    /// <summary><c>-c protocol.file.allow=always submodule update --init -q -- &lt;path&gt;</c> (<c>prep-base.sh:11</c>), under the clone's wall: the file protocol is allowed for this one command and this one path, whose url the verb has just set to a folder of the operator's own.</summary>
    public async Task<Outcome<string>> SubmoduleUpdateAsync(string path, CancellationToken ct) =>
        await AskAsync([.. Hardened, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q", "--", path], ct, TargetWalls.Clone, smudges: true);

    /// <summary><c>checkout -q -b &lt;branch&gt;</c> in the folder; a name that starts with <c>-</c> or is empty is refused before git is asked.</summary>
    public async Task<Outcome<string>> NewBranchAsync(string branch, CancellationToken ct) =>
        Refusal(branch) is { Length: > 0 } refused ? Outcome<string>.Failure(refused) : await AskAsync([.. Hardened, "checkout", "-q", "-b", branch], ct);

    /// <summary>The number of lines <c>status --porcelain</c> prints (<c>objective-checks.sh:13</c>).</summary>
    public async Task<Outcome<int>> UncommittedCountAsync(CancellationToken ct) =>
        Lines(await AskAsync([.. Hardened, "status", "--porcelain", "--ignore-submodules=dirty"], ct)).Match(lines => Outcome<int>.Success(lines.Count), Outcome<int>.Failure);

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
        (await LaunchAsync([.. Hardened, "grep", "-l", "-F", "-e", text, "--", .. globs], ct, Budget)).Match(
            attempt => attempt switch
            {
                ProcessAttempt.Completed { Result.ExitCode: 0 } => Outcome<bool>.Success(true),
                ProcessAttempt.Completed { Result.ExitCode: 1 } => Outcome<bool>.Success(false),
                var other => Outcome<bool>.Failure(Said(other)),
            },
            Outcome<bool>.Failure);

    /// <summary>A commit-to-commit diff with the harness's three guards: no external diff program, no textconv, the plain patch — the clone's config and attributes are the model's.</summary>
    private Task<Outcome<string>> DiffOf(string baseSha, string[] options, string[] pathspec, CancellationToken ct) =>
        CommitId().IsMatch(baseSha)
            ? AskAsync([.. Hardened, "-c", "diff.external=", "diff", "--no-ext-diff", "--no-textconv", "--submodule=short", .. options, $"{baseSha}..HEAD", .. pathspec], ct)
            : Task.FromResult(Outcome<string>.Failure(NotACommitId(baseSha)));

    private static string NotACommitId(string text) => $"'{text}' is not a commit id — a file holds the id, and a file's text is never handed to git as an option";

    private static string Refusal(string branch) =>
        branch.Length == 0 || branch.StartsWith('-') ? $"'{branch}' is not a branch name git may be given: it is empty or starts with '-'" : string.Empty;

    private static Outcome<IReadOnlyList<string>> LinesOf(string text) => Outcome<IReadOnlyList<string>>.Success([.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)]);

    private static Outcome<IReadOnlyList<string>> Lines(Outcome<string> answer) => answer.Match(LinesOf, Outcome<IReadOnlyList<string>>.Failure);

    private static Outcome<string> Trimmed(Outcome<string> answer) => answer.Match(text => Outcome<string>.Success(text.Trim()), Outcome<string>.Failure);

    private async Task<Outcome<string>> AskAsync(string[] arguments, CancellationToken ct, TimeSpan? budget = null, int? noAnswer = null, bool smudges = false) =>
        (await LaunchAsync(arguments, ct, budget ?? Budget, smudges)).Match(
            attempt => attempt switch
            {
                ProcessAttempt.Completed { Result.ExitCode: 0 } done => Outcome<string>.Success(done.Result.StandardOutput),
                ProcessAttempt.Completed done when done.Result.ExitCode == noAnswer => Outcome<string>.Success(string.Empty),
                _ => Outcome<string>.Failure(Said(attempt)),
            },
            Outcome<string>.Failure);

    /// <summary>One launch of the question. Unless <paramref name="smudges"/> — which only the three questions that write the base's worktree from the operator's objects set — the clone's filter drivers are neutralised first (<see cref="EnvironmentForAsync"/>); a clone whose drivers cannot be
    /// listed is the question's failure, never a question asked unguarded.</summary>
    private async Task<Outcome<ProcessAttempt>> LaunchAsync(string[] arguments, CancellationToken ct, TimeSpan budget, bool smudges = false) =>
        await (await EnvironmentForAsync(smudges, ct)).Match(
            launchEnvironment => LaunchUnderAsync(arguments, launchEnvironment, ct, budget),
            reason => Task.FromResult(Outcome<ProcessAttempt>.Failure(reason)));

    private async Task<Outcome<ProcessAttempt>> LaunchUnderAsync(string[] arguments, IReadOnlyDictionary<string, string> launchEnvironment, CancellationToken ct, TimeSpan budget) =>
        Outcome<ProcessAttempt>.Success(await ProcessRunner.RunAsync(git, arguments, workingDirectory, budget, string.Empty, launchEnvironment, ct));

    /// <summary>The environment of one question: <see cref="_environment"/>, plus — unless the question writes the base's worktree — one <c>GIT_CONFIG_*</c> quadruple per filter driver git would read, found by <c>config --name-only -z --get-regexp ^filter\.</c> in the folder the question is asked of (every scope git reads,
    /// includes too; exit 1 is none). The enumeration goes through <see cref="_environment"/> directly, never through this method. A failure of it — a timeout, exit 128 — is the question's failure: fail closed.</summary>
    private async Task<Outcome<IReadOnlyDictionary<string, string>>> EnvironmentForAsync(bool smudges, CancellationToken ct)
    {
        if (smudges)
        {
            return Outcome<IReadOnlyDictionary<string, string>>.Success(_environment);
        }

        var listed = await ProcessRunner.RunAsync(git, [.. Hardened, "config", "--name-only", "-z", "--get-regexp", @"^filter\."], workingDirectory, Budget, string.Empty, _environment, ct);
        return listed switch
        {
            ProcessAttempt.Completed { Result.ExitCode: 0 } done => Outcome<IReadOnlyDictionary<string, string>>.Success(Neutralised(done.Result.StandardOutput)),
            ProcessAttempt.Completed { Result.ExitCode: 1 } => Outcome<IReadOnlyDictionary<string, string>>.Success(_environment),
            _ => Outcome<IReadOnlyDictionary<string, string>>.Failure($"cannot list the clone's filter drivers: {Said(listed)}"),
        };
    }

    /// <summary>For each filter driver named by a key (<c>filter.&lt;driver&gt;.&lt;variable&gt;</c>; the driver may hold dots, <c>=</c>, quotes or spaces — the key is used verbatim): <c>clean</c>, <c>smudge</c> and <c>process</c> empty and <c>required</c> false, as four
    /// <c>GIT_CONFIG_KEY_n</c>/<c>VALUE_n</c> pairs counted by <c>GIT_CONFIG_COUNT</c>. Environment variables, not <c>-c</c>: <c>-c</c> splits at the first <c>=</c>, so a driver named <c>a=b</c> slips past it.</summary>
    private IReadOnlyDictionary<string, string> Neutralised(string names)
    {
        var drivers = names.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.LastIndexOf('.') is var last && last > Filter.Length ? name[Filter.Length..last] : string.Empty)
            .Where(driver => driver.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var launched = new Dictionary<string, string>(_environment, StringComparer.Ordinal);
        if (drivers.Count == 0)
        {
            return launched;
        }

        string[] variables = ["clean", "smudge", "process", "required"];
        string[] values = [string.Empty, string.Empty, string.Empty, "false"];
        for (var driver = 0; driver < drivers.Count; driver++)
        {
            for (var variable = 0; variable < variables.Length; variable++)
            {
                var n = (driver * variables.Length) + variable;
                launched[$"GIT_CONFIG_KEY_{n.ToString(CultureInfo.InvariantCulture)}"] = $"filter.{drivers[driver]}.{variables[variable]}";
                launched[$"GIT_CONFIG_VALUE_{n.ToString(CultureInfo.InvariantCulture)}"] = values[variable];
            }
        }

        launched["GIT_CONFIG_COUNT"] = (drivers.Count * variables.Length).ToString(CultureInfo.InvariantCulture);
        return launched;
    }

    private static string Said(ProcessAttempt attempt) =>
        attempt is ProcessAttempt.Completed done && FirstLine(done.Result.StandardError) is { Length: > 0 } line ? line : attempt.Describe;

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;

    [GeneratedRegex(@"^[0-9a-f]{4,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommitId();
}
