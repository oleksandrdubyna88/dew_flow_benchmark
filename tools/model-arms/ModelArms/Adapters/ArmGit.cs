using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Tasks;

namespace ModelArms.Adapters;

/// <summary>The harness's own git for one folder (plan D17 d, E2 §8 item 25, E3 C14): launched by the REAL path the verb resolved, with the working directory the folder it is asked about and an environment with every <c>GIT_*</c> of the
/// parent taken out (and every secret-named one: <see cref="TargetEnvironment"/>), through the shared launcher's replacing overload and a ceiling of 60 s a question (<see cref="TargetWalls.Git"/>; the clone and the submodule's, <see cref="TargetWalls.Clone"/>). One class for every git the module starts: E3 widens it, never duplicates it. The arm's claude keeps its own environment (D19): two processes, two environments, on purpose. Nothing here names the module's
/// bare-name git helper — the architecture scan is red the moment it does.
/// <para>
/// <b>What git is kept from running</b> (measured on git 2.53.0, each by a planted program writing a sentinel; the tests keep observing it on the host's git). Every question: <c>-c core.fsmonitor=false</c>; <c>-c core.hooksPath=</c> (<c>status</c> runs <c>post-index-change</c> and <c>checkout -b</c> runs
/// <c>post-checkout</c>, from the clone's hooks folder or an arm-written <c>core.hooksPath</c>; the empty path stopped both); <c>GIT_NO_LAZY_FETCH=1</c> (a promisor remote's program run by a lazy fetch of a blob the model deleted); <c>-c submodule.recurse=false</c> (with <c>submodule.recurse=true</c> in the clone's config, a <c>checkout -q -b x &lt;start&gt;</c> that MOVES a submodule's gitlink ran the submodule's own clean filter — a planted sentinel ran; with the pin it did not. The submodule's config is
/// the one the enumeration below never sees. <c>checkout --detach</c> moves a gitlink today, <c>NewBranchAsync</c> has no start point yet, and E4's checkouts will); <c>GIT_ALLOW_PROTOCOL=</c> (set, empty) on every question except <c>clone</c> and <c>submodule update</c>: git treats the variable, when set, as <c>protocol.allow=never</c> with nothing allowed, overriding every <c>protocol.allow</c> and <c>protocol.&lt;name&gt;.allow</c> of every scope (repo, global, an included file) — a <c>protocol.&lt;name&gt;.allow=always</c> the clone writes has priority over <c>-c protocol.allow=never</c>
/// (measured on git 2.53.0: <c>protocol.ssh.allow=always</c> ran a <c>core.sshCommand</c> sentinel, <c>protocol.ext.allow=always</c> with an <c>ext::</c> url ran it, <c>remote.origin.vcs=foo</c> with <c>protocol.foo.allow=always</c> ran a planted <c>git-remote-foo</c>, an <c>insteadOf</c> to <c>ext::</c> ran it; under the variable each is <c>fatal: transport '&lt;name&gt;' not allowed</c>, exit 128, at once). The variable is in git since 2.3.10/2.6.1 (release notes; not measured on an old git), the pin only since 2.12 and inert beside the variable, so the pin is gone.
/// The two exempt questions transport from the operator's own source, which D17 trusts; <c>checkout --detach</c> is not exempt. <c>GIT_NO_LAZY_FETCH=1</c> stays beside it (git 2.44 and later): the switch stops the fetch, the allow-list stops the transport — the test that drops the switch (<see cref="ArmGitSeams"/>) observes the allow-list hold alone. <b>A git floor is enforced</b> at every verb's admission and by <c>validate</c> (D23, E4.S0, <see cref="GitFloor"/>): 2.45.0, or 2.39.4 / 2.40.2 / 2.41.1 / 2.42.2 / 2.43.4 / 2.44.1 on their lines — a NUMBER read from <c>--version</c> (<see cref="VersionAsync"/>), not an observation: a vendor build carrying the number without the fix would pass (parent §9); <c>GIT_ALLOW_PROTOCOL</c> (2.6.1) and the <c>GIT_CONFIG_COUNT</c> neutralisation (2.31) are implied by it; and the allow-list is UNOBSERVED on Windows — its tests are shell-script plants that skip there, so whether git for Windows reads an empty variable as set is not measured;
/// and, except for <c>clone</c>, <c>checkout --detach</c> and <c>submodule update</c>
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
/// uncommitted count, the changed <c>.cs</c> names, <c>numstat</c>, <c>shortstat</c> and the commit count since a base (every diff <c>-c diff.external= … --no-ext-diff --no-textconv</c>, the base a hex id) and the one <c>grep -l -F</c> that asks whether a text is named under some globs. <c>--version</c> is the floor's question (E4.S0): pinned, sealed, one launch, no repository read.
/// </para>
/// <para>
/// <b>What E4.S2 adds</b> (the task pipeline's worktrees and pick-back; measured on git 2.53.0 with planted programs, M-h…M-w of the story, each proven live with the host's git first by <c>TaskWorktreesTests</c>): every WRITE of the arm's clone — <c>worktree add</c> (a <c>filter.&lt;x&gt;.smudge</c> and a <c>post-checkout</c> hook ran on it), the attach, the arm's submodule update, the cherry-pick and its abort — is
/// neutralised like every other question (the base's three forms, <c>clone</c>, <c>checkout --detach</c> and <see cref="SubmoduleUpdateAsync"/>, keep their argv and are the BASE's only, <c>prep-base</c>); <see cref="ArmSubmoduleUpdateAsync"/> allows <c>GIT_ALLOW_PROTOCOL=file</c> alone and carries <c>--checkout</c> (a <c>submodule.&lt;name&gt;.update=!&lt;program&gt;</c> ran on a plain update); the cherry-pick alone also empties every
/// <c>merge.&lt;name&gt;.driver</c> (the key verbatim, a name holding <c>=</c> included) and <c>merge.default</c> through the same <c>GIT_CONFIG_*</c> pairs (an emptied driver is the conflict stop), pins <c>-c commit.gpgsign=false</c> (stops <c>gpg.program</c>, <c>gpg.ssh.program</c> and <c>gpg.x509.program</c>), asks for <c>--no-edit --allow-empty --keep-redundant-commits</c> (an empty or a redundant source becomes ONE empty commit) and names the
/// committer (<c>GIT_COMMITTER_NAME=model-arms</c>), so a host with no identity never fails a pick. The patch-ids come from ONE <c>diff-tree --stdin -p</c> under the diff pins piped into ONE <c>patch-id --stable</c> (a porcelain <c>diff</c> ran a <c>textconv</c> and made two commits' ids equal). An enumeration that cannot run is the question's failure (<see cref="EnumerationFailure"/>), never a question asked with fewer pairs.
/// <b>Residuals:</b> the editor pins are requests git 2.53 never needed for a one-commit non-interactive pick (<c>core.editor</c> ran only for <c>cherry-pick --edit</c>, <c>sequence.editor</c> only for <c>rebase -i</c>): their only break-it is the argv; a <c>--textconv</c> default a later git might grow; LFS worktrees are pointer files (filters are neutralised) and unobserved.
/// </para></summary>
public sealed partial class ArmGit
{
    private readonly string _git;
    private readonly string _workingDirectory;

    private static readonly TimeSpan Budget = TargetWalls.Git;

    private const string Filter = "filter.";

    /// <summary>The base environment git runs under: the one it was given, reduced here (<c>GitEnvironment.Of(TargetEnvironment.Of(…))</c>: no <c>GIT_*</c>, no secret-named variable, no agent socket), and <c>GIT_NO_LAZY_FETCH=1</c>. A partial clone names a promisor remote and a program to run for it; a blob the model deleted would make git fetch it — and run that program — on the harness's behalf. With this set the
    /// read fails ("unable to read") and the step stops. The environment variable, not <c>--no-lazy-fetch</c>, which older gits refuse.</summary>
    private readonly IReadOnlyDictionary<string, string> _environment;

    /// <summary><see cref="_environment"/> plus <c>GIT_ALLOW_PROTOCOL=</c> (set, EMPTY): the environment of every question that does not transport.</summary>
    private readonly IReadOnlyDictionary<string, string> _sealed;

    /// <summary><see cref="_environment"/> plus <c>GIT_ALLOW_PROTOCOL=file</c>: the narrowest value git honours that admits the local clone an arm's submodule update makes (measured 2.53.0, 2.45.0 and 2.39.4: empty refuses it, <c>file</c> admits it without <c>-c protocol.file.allow=always</c>, <c>ext</c> refuses).</summary>
    private readonly IReadOnlyDictionary<string, string> _fileOnly;

    private const string AllowProtocol = "GIT_ALLOW_PROTOCOL";

    /// <summary>The prefix of the failure of an enumeration of the clone's config (filters, merge drivers) that could not run: the question was NOT asked, never asked with fewer pairs. The pipeline reads it as the host's (exit 3) before any write of the clone.</summary>
    public const string EnumerationFailure = "cannot list the clone's ";

    /// <summary>The transport a question is allowed (E4.S2): <see cref="None"/> is <c>GIT_ALLOW_PROTOCOL=</c> (set, empty — every question that does not transport), <see cref="Any"/> sets no variable (the base's <c>clone</c> and <c>submodule update</c>, from the operator's own source), <see cref="File"/> is <c>GIT_ALLOW_PROTOCOL=file</c>
    /// (an arm's submodule update from its own checkout).</summary>
    private enum Transport
    {
        None,
        Any,
        File,
    }

    public ArmGit(string git, string workingDirectory, IReadOnlyDictionary<string, string> environment)
        : this(git, workingDirectory, environment, ArmGitSeams.Real)
    {
    }

    internal ArmGit(string git, string workingDirectory, IReadOnlyDictionary<string, string> environment, ArmGitSeams seams)
    {
        _git = git;
        _workingDirectory = workingDirectory;
        var reduced = new Dictionary<string, string>(GitEnvironment.Of(TargetEnvironment.Of(environment)), StringComparer.Ordinal);
        _environment = seams.LazyFetchSwitch ? new Dictionary<string, string>(reduced, StringComparer.Ordinal) { ["GIT_NO_LAZY_FETCH"] = "1" } : reduced;
        _sealed = new Dictionary<string, string>(_environment, StringComparer.Ordinal) { [AllowProtocol] = string.Empty };
        _fileOnly = new Dictionary<string, string>(_environment, StringComparer.Ordinal) { [AllowProtocol] = "file" };
    }

    /// <summary>The pins of every question: <c>-c core.fsmonitor=false -c core.hooksPath=</c> (the first pair first: <c>TargetBench.HarnessGit</c> and the tests key on it) and <c>-c submodule.recurse=false</c>: the clone is written by the model under test, and its own config and hooks folder must not name a program git runs on the harness's behalf, nor may a
    /// <c>submodule.recurse=true</c> of its config (or of the operator's global one) make a checkout that moves a gitlink run the submodule's own clean filter.</summary>
    private static readonly string[] Pinned = ["-c", "core.fsmonitor=false", "-c", "core.hooksPath=", "-c", "submodule.recurse=false"];

    public async Task<Outcome<string>> HeadAsync(CancellationToken ct) => Trimmed(await AskAsync([.. Pinned, "rev-parse", "HEAD"], ct));

    /// <summary><c>--version</c>, the floor's question (E4.S0, D23): the first line of stdout, trimmed. Pinned like every question, sealed (<c>GIT_ALLOW_PROTOCOL=</c>, <c>GIT_NO_LAZY_FETCH=1</c>, no secret-named variable), under the 60 s wall, and ONE launch: it opens no repository, so no filter driver is enumerated first — <c>smudges: true</c> is the flag that
    /// skips the enumeration (the three questions that write the base's worktree set it, and this read shares it). A wrapper that prints more than git is cut to its first line, <c>"  git version 2.44.1  \n"</c> reads as <c>git version 2.44.1</c>: the cut comes BEFORE the trim, in this one statement.</summary>
    public async Task<Outcome<string>> VersionAsync(CancellationToken ct) =>
        (await AskAsync([.. Pinned, "--version"], ct, Budget, smudges: true)).Match(stdout => Outcome<string>.Success(stdout.Split('\n')[0].Trim()), Outcome<string>.Failure);

    public async Task<Outcome<string>> HeadShortAsync(CancellationToken ct) => Trimmed(await AskAsync([.. Pinned, "rev-parse", "--short", "HEAD"], ct));

    /// <summary><c>diff --no-ext-diff --no-textconv &lt;from&gt; HEAD</c>, stdout verbatim — the plain patch: a <c>diff.external</c> or a <c>textconv</c> driver the clone's config or <c>.gitattributes</c> names would run a program and its output would BE the measured diff. <paramref name="from"/> must be a commit id in hex: it came from a file, and a file's text is never handed to git as an option.</summary>
    public async Task<Outcome<string>> DiffAsync(string from, CancellationToken ct) =>
        CommitId().IsMatch(from) ? await AskAsync([.. Pinned, "-c", "diff.external=", "diff", "--no-ext-diff", "--no-textconv", "--submodule=short", from, "HEAD"], ct) : Outcome<string>.Failure($"'{from}' is not a commit id — the snapshot file does not hold one");

    /// <summary><c>clone -q --branch &lt;branch&gt; -- &lt;source&gt; &lt;destination&gt;</c> (<c>prep-base.sh:7</c>), under the clone's wall; the folder this git runs in must exist and <paramref name="destination"/> must not.</summary>
    public async Task<Outcome<string>> CloneAsync(string source, string branch, string destination, CancellationToken ct) =>
        Refusal(branch) is { Length: > 0 } refused
            ? Outcome<string>.Failure(refused)
            : await AskAsync([.. Pinned, "clone", "-q", "--branch", branch, "--", source, destination], ct, TargetWalls.Clone, smudges: true, transport: Transport.Any);

    /// <summary><c>checkout -q --detach &lt;sha&gt;</c>: <paramref name="sha"/> is a commit id in hex — it came from a file, and a file's text is never handed to git as an option.</summary>
    public async Task<Outcome<string>> DetachAsync(string sha, CancellationToken ct) =>
        CommitId().IsMatch(sha) ? await AskAsync([.. Pinned, "checkout", "-q", "--detach", sha], ct, smudges: true) : Outcome<string>.Failure(NotACommitId(sha));

    /// <summary><c>config -f .gitmodules --get-regexp ^submodule\..*\.path$</c> (<c>prep-base.sh:9</c>): its lines, or the empty text when the folder has no <c>.gitmodules</c> or none holds a path (git's exit 1).</summary>
    public async Task<Outcome<string>> SubmodulePathsAsync(CancellationToken ct) =>
        File.Exists(Path.Combine(_workingDirectory, ".gitmodules"))
            ? await AskAsync([.. Pinned, "config", "-f", ".gitmodules", "--get-regexp", @"^submodule\..*\.path$"], ct, Budget, noAnswer: 1)
            : Outcome<string>.Success(string.Empty);

    public async Task<Outcome<string>> SetSubmoduleUrlAsync(string name, string url, CancellationToken ct) =>
        await AskAsync([.. Pinned, "config", $"submodule.{name}.url", url], ct);

    /// <summary><c>-c protocol.file.allow=always submodule update --init -q -- &lt;path&gt;</c> (<c>prep-base.sh:11</c>), under the clone's wall: the file protocol is allowed for this one command and this one path, whose url the verb has just set to a folder of the operator's own.</summary>
    public async Task<Outcome<string>> SubmoduleUpdateAsync(string path, CancellationToken ct) =>
        await AskAsync([.. Pinned, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q", "--", path], ct, TargetWalls.Clone, smudges: true, transport: Transport.Any);

    /// <summary><c>checkout -q -b &lt;branch&gt;</c> in the folder; a name that starts with <c>-</c> or is empty is refused before git is asked.</summary>
    public async Task<Outcome<string>> NewBranchAsync(string branch, CancellationToken ct) =>
        Refusal(branch) is { Length: > 0 } refused ? Outcome<string>.Failure(refused) : await AskAsync([.. Pinned, "checkout", "-q", "-b", branch], ct);

    /// <summary>The number of lines <c>status --porcelain</c> prints (<c>objective-checks.sh:13</c>).</summary>
    public async Task<Outcome<int>> UncommittedCountAsync(CancellationToken ct) =>
        Lines(await AskAsync([.. Pinned, "status", "--porcelain", "--ignore-submodules=dirty"], ct)).Match(lines => Outcome<int>.Success(lines.Count), Outcome<int>.Failure);

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
            : (await AskAsync([.. Pinned, "rev-list", "--count", $"{baseSha}..HEAD"], ct)).Match(
                text => long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? Outcome<long>.Success(count) : Outcome<long>.Failure($"git counted '{text.Trim()}' commits"),
                Outcome<long>.Failure);

    /// <summary>Whether <paramref name="text"/> is named, as fixed text, by a file under <paramref name="globs"/> (<c>git grep -l -F -e &lt;text&gt; -- &lt;globs&gt;</c>, <c>objective-checks.sh:37</c>): exit 0 yes, 1 no, anything else git failing to answer.</summary>
    public async Task<Outcome<bool>> MentionsAsync(string text, IReadOnlyList<string> globs, CancellationToken ct) =>
        (await LaunchAsync([.. Pinned, "grep", "-l", "-F", "-e", text, "--", .. globs], ct, Budget)).Match(
            attempt => attempt switch
            {
                ProcessAttempt.Completed { Result.ExitCode: 0 } => Outcome<bool>.Success(true),
                ProcessAttempt.Completed { Result.ExitCode: 1 } => Outcome<bool>.Success(false),
                var other => Outcome<bool>.Failure(Said(other)),
            },
            Outcome<bool>.Failure);

    // ---- E4.S2: the questions the task pipeline asks of the arm's clone and its worktrees. Every write of the clone is neutralised (smudges: false); the cherry-pick alone also carries the merge neutralisation and the committer pin. ----

    /// <summary><c>worktree add -q &lt;path&gt; -b &lt;branch&gt; HEAD</c>, under the clone's wall: a new worktree on a new branch from the clone's HEAD. A path or a branch that starts with <c>-</c> is refused before git is asked.</summary>
    public async Task<Outcome<string>> WorktreeAddAsync(string path, string branch, CancellationToken ct) =>
        PathRefusal(path) is { Length: > 0 } pathRefused ? Outcome<string>.Failure(pathRefused)
        : BranchRefusal(branch) is { Length: > 0 } refused ? Outcome<string>.Failure(refused)
        : await AskAsync([.. Pinned, "worktree", "add", "-q", path, "-b", branch, "HEAD"], ct, TargetWalls.Clone);

    /// <summary><c>worktree add -q &lt;path&gt; &lt;branch&gt;</c>: a folder attached to the EXISTING branch, HEAD on it (M-t) — only once the registration is clean (a dead one is pruned first; <c>-f</c> is never used, it overrides a live registration too).</summary>
    public async Task<Outcome<string>> WorktreeAttachAsync(string path, string branch, CancellationToken ct) =>
        PathRefusal(path) is { Length: > 0 } pathRefused ? Outcome<string>.Failure(pathRefused)
        : BranchRefusal(branch) is { Length: > 0 } refused ? Outcome<string>.Failure(refused)
        : await AskAsync([.. Pinned, "worktree", "add", "-q", path, branch], ct, TargetWalls.Clone);

    /// <summary><c>worktree remove --force --force &lt;path&gt;</c>: the second <c>--force</c> removes a worktree the model LOCKED (M-t); a registered worktree whose folder is gone is removed with its registration (exit 0), an unregistered path is git's 128.</summary>
    public async Task<Outcome<string>> WorktreeRemoveAsync(string path, CancellationToken ct) =>
        PathRefusal(path) is { Length: > 0 } refused ? Outcome<string>.Failure(refused) : await AskAsync([.. Pinned, "worktree", "remove", "--force", "--force", path], ct);

    /// <summary><c>worktree prune</c>: removes registrations whose folder is gone, and nothing else.</summary>
    public async Task<Outcome<string>> WorktreePruneAsync(CancellationToken ct) => await AskAsync([.. Pinned, "worktree", "prune"], ct);

    /// <summary><c>branch -D &lt;branch&gt;</c>.</summary>
    public async Task<Outcome<string>> DeleteBranchAsync(string branch, CancellationToken ct) =>
        BranchRefusal(branch) is { Length: > 0 } refused ? Outcome<string>.Failure(refused) : await AskAsync([.. Pinned, "branch", "-D", branch], ct);

    /// <summary>The commit <c>refs/heads/&lt;branch&gt;</c> names, or the empty text when there is no such branch (<c>rev-parse --verify -q</c> exit 1). Spelled in full: a TAG of the same name is never read.</summary>
    public async Task<Outcome<string>> BranchTipAsync(string branch, CancellationToken ct) =>
        BranchRefusal(branch) is { Length: > 0 } refused ? Outcome<string>.Failure(refused) : Trimmed(await AskAsync([.. Pinned, "rev-parse", "--verify", "-q", $"refs/heads/{branch}^{{commit}}"], ct, Budget, noAnswer: 1));

    /// <summary>The branch HEAD is on (<c>symbolic-ref -q --short HEAD</c>), or the empty text when HEAD is detached (exit 1).</summary>
    public async Task<Outcome<string>> CurrentBranchAsync(CancellationToken ct) => Trimmed(await AskAsync([.. Pinned, "symbolic-ref", "-q", "--short", "HEAD"], ct, Budget, noAnswer: 1));

    /// <summary><c>rev-list --reverse &lt;from&gt;..HEAD</c>: the commits since <paramref name="fromSha"/>, oldest first. <paramref name="fromSha"/> is hex: it came from a file.</summary>
    public async Task<Outcome<IReadOnlyList<string>>> RevListAsync(string fromSha, CancellationToken ct) =>
        CommitId().IsMatch(fromSha) ? Lines(await AskAsync([.. Pinned, "rev-list", "--reverse", $"{fromSha}..HEAD"], ct)) : Outcome<IReadOnlyList<string>>.Failure(NotACommitId(fromSha));

    /// <summary><c>rev-list --reverse &lt;from&gt;..refs/heads/&lt;branch&gt;</c>: the same up to a branch, spelled in full.</summary>
    public async Task<Outcome<IReadOnlyList<string>>> RevListAsync(string fromSha, string branch, CancellationToken ct) =>
        !CommitId().IsMatch(fromSha) ? Outcome<IReadOnlyList<string>>.Failure(NotACommitId(fromSha))
        : BranchRefusal(branch) is { Length: > 0 } refused ? Outcome<IReadOnlyList<string>>.Failure(refused)
        : Lines(await AskAsync([.. Pinned, "rev-list", "--reverse", $"{fromSha}..refs/heads/{branch}"], ct));

    /// <summary>The patch-id of each commit: ONE <c>diff-tree --stdin -p</c> fed the shas, its stdout piped into ONE <c>patch-id --stable</c> — two launches whatever the count. The diff carries the pins of <see cref="DiffAsync"/> (<c>-c diff.external= --no-ext-diff --no-textconv --submodule=short</c>): the plumbing ran neither a
    /// textconv nor an external diff by default (M-l), and the porcelain ran both — a <c>textconv</c> printing a constant made two different commits' ids EQUAL, the silent skip D22 describes. A commit without a patch (empty, or a merge commit) has no line (M-n) and its <see cref="PickSource.PatchId"/> is empty. Every sha is hex.</summary>
    public async Task<Outcome<IReadOnlyList<PickSource>>> PatchIdsAsync(IReadOnlyList<string> shas, CancellationToken ct)
    {
        if (shas.FirstOrDefault(sha => !CommitId().IsMatch(sha)) is { } bad)
        {
            return Outcome<IReadOnlyList<PickSource>>.Failure(NotACommitId(bad));
        }

        if (shas.Count == 0)
        {
            return Outcome<IReadOnlyList<PickSource>>.Success([]);
        }

        var patches = await AskAsync([.. Pinned, "-c", "diff.external=", "diff-tree", "--stdin", "-p", "--no-ext-diff", "--no-textconv", "--submodule=short"], ct, input: string.Join('\n', shas) + "\n");
        if (patches is Outcome<string>.Fail diffFailed)
        {
            return Outcome<IReadOnlyList<PickSource>>.Failure(diffFailed.Reason);
        }

        if (((Outcome<string>.Ok)patches).Value.Trim().Length == 0)
        {
            // No commit has a patch (all empty or merge commits): nothing to hash, and a patch-id fed no input would wait on a stdin nobody closes.
            return Outcome<IReadOnlyList<PickSource>>.Success([.. shas.Select(sha => new PickSource(sha, string.Empty))]);
        }

        var ids = await AskAsync([.. Pinned, "patch-id", "--stable"], ct, input: ((Outcome<string>.Ok)patches).Value);
        if (ids is Outcome<string>.Fail idsFailed)
        {
            return Outcome<IReadOnlyList<PickSource>>.Failure(idsFailed.Reason);
        }

        var byCommit = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in ((Outcome<string>.Ok)ids).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                byCommit[parts[1]] = parts[0];
            }
        }

        return Outcome<IReadOnlyList<PickSource>>.Success([.. shas.Select(sha => new PickSource(sha, byCommit.GetValueOrDefault(sha, string.Empty)))]);
    }

    /// <summary>The committer a pick is made under: the harness made the commit, so the harness is named (M-p) — it beats <c>committer.*</c> and <c>user.*</c> of the clone's config the model may have written, keeps the source commit's AUTHOR, and a host with no identity never fails a pick.</summary>
    private static readonly IReadOnlyDictionary<string, string> Committer = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GIT_COMMITTER_NAME"] = "model-arms",
        ["GIT_COMMITTER_EMAIL"] = "model-arms@model-arms.invalid",
    };

    /// <summary><c>-c commit.gpgsign=false cherry-pick --no-edit --allow-empty --keep-redundant-commits &lt;sha&gt;</c>: ONE commit per call, so an abort undoes at most the commit in flight (D22). <c>commit.gpgsign=false</c> alone stops <c>gpg.program</c>, <c>gpg.ssh.program</c> and <c>gpg.x509.program</c> (M-i); <c>--no-edit</c> is a request a one-commit non-interactive pick never needs on git 2.53 (M-j); the
    /// two flags make an empty source (M-o) and a redundant one each ONE empty commit instead of a stop, so the record stays one line per source (M-v). Neutralised for filters AND merge drivers (<c>merges</c>); an emptied driver is the conflict stop, exit 4 (M-k). The answer's failure reason is git's WHOLE output, stdout then stderr (the shell's <c>&gt;&gt; pick-&lt;id&gt;.log 2&gt;&amp;1</c>; the <c>CONFLICT</c> line is on stdout, and this is the one question for which the reason is more than a line: the pipeline writes it to <c>pick-&lt;id&gt;.log</c>).</summary>
    public async Task<Outcome<string>> CherryPickAsync(string sha, CancellationToken ct) =>
        CommitId().IsMatch(sha)
            ? await AskAsync([.. Pinned, "-c", "commit.gpgsign=false", "cherry-pick", "--no-edit", "--allow-empty", "--keep-redundant-commits", sha], ct, merges: true, extra: Committer, wholeOutput: true)
            : Outcome<string>.Failure(NotACommitId(sha));

    /// <summary><c>cherry-pick --abort</c>: asked only when <c>CHERRY_PICK_HEAD</c> or <c>sequencer/</c> is there (with nothing in progress it is 128; with a lone <c>REVERT_HEAD</c> it would abort the revert, M-s).</summary>
    public async Task<Outcome<string>> CherryPickAbortAsync(CancellationToken ct) => await AskAsync([.. Pinned, "cherry-pick", "--abort"], ct);

    /// <summary>The number of changes <c>status --porcelain --untracked-files=all --ignore-submodules=dirty</c> lists outside <paramref name="excluded"/> (relative paths: <c>t-lib.sh:29</c> grepped <c>node_modules</c> out).</summary>
    public async Task<Outcome<int>> LeftBehindAsync(IReadOnlyList<string> excluded, CancellationToken ct) =>
        Lines(await AskAsync([.. Pinned, "status", "--porcelain", "--untracked-files=all", "--ignore-submodules=dirty", "--", ".", .. excluded.Select(path => $":(exclude){path}")], ct))
            .Match(lines => Outcome<int>.Success(lines.Count), Outcome<int>.Failure);

    /// <summary><c>submodule update --init --checkout -q -- &lt;path&gt;</c>, the arm's form: <c>GIT_ALLOW_PROTOCOL=file</c> (the narrowest value that admits the local clone, M-a), neutralised like every write of the arm's clone. <c>--checkout</c> on the command line stops <c>submodule.&lt;name&gt;.update=!&lt;program&gt;</c> whatever the name holds (M-b, M-q); a
    /// <c>-c submodule.&lt;name&gt;.update=checkout</c> would split at the name's first <c>=</c>. The base's <see cref="SubmoduleUpdateAsync"/> is the base's form (<c>prep-base</c> only) and is never used in an arm.</summary>
    public async Task<Outcome<string>> ArmSubmoduleUpdateAsync(string path, CancellationToken ct) =>
        await AskAsync([.. Pinned, "submodule", "update", "--init", "--checkout", "-q", "--", path], ct, TargetWalls.Clone, transport: Transport.File);

    /// <summary>The names of the filter drivers the clone's config names (the enumeration every neutralised question runs): what the LFS warning reads.</summary>
    public async Task<Outcome<IReadOnlyList<string>>> FilterDriversAsync(CancellationToken ct) =>
        (await ListAsync(@"^filter\.", "filter drivers", ct)).Match(names => Outcome<IReadOnlyList<string>>.Success(DriversOf(names)), Outcome<IReadOnlyList<string>>.Failure);

    /// <summary><c>worktree list --porcelain</c>: one entry per registered worktree, its path, the branch ref it is on (<c>""</c> when detached) and whether it is prunable (its folder is gone). A prunable entry still names its branch, read from the admin folder (M-t).</summary>
    public async Task<Outcome<IReadOnlyList<WorktreeEntry>>> WorktreeListAsync(CancellationToken ct) =>
        (await AskAsync([.. Pinned, "worktree", "list", "--porcelain"], ct)).Match(text => Outcome<IReadOnlyList<WorktreeEntry>>.Success(WorktreeEntries(text)), Outcome<IReadOnlyList<WorktreeEntry>>.Failure);

    /// <summary>The commit <paramref name="sha"/> names in the clone, or the empty text when the clone does not know it (<c>rev-parse --verify -q &lt;sha&gt;^{commit}</c> exit 1).</summary>
    public async Task<Outcome<string>> CommitPresentAsync(string sha, CancellationToken ct) =>
        CommitId().IsMatch(sha) ? Trimmed(await AskAsync([.. Pinned, "rev-parse", "--verify", "-q", $"{sha}^{{commit}}"], ct, Budget, noAnswer: 1)) : Outcome<string>.Failure(NotACommitId(sha));

    /// <summary>Whether <paramref name="sha"/> is an ancestor of <c>refs/heads/&lt;branch&gt;</c> (<c>merge-base --is-ancestor</c>: 0 yes, 1 no). Asked only of a sha <see cref="CommitPresentAsync"/> knew: an unknown one is git's 128, a failure, never a "no".</summary>
    public async Task<Outcome<bool>> IsAncestorAsync(string sha, string branch, CancellationToken ct)
    {
        if (!CommitId().IsMatch(sha))
        {
            return Outcome<bool>.Failure(NotACommitId(sha));
        }

        if (BranchRefusal(branch) is { Length: > 0 } refused)
        {
            return Outcome<bool>.Failure(refused);
        }

        return (await LaunchAsync([.. Pinned, "merge-base", "--is-ancestor", sha, $"refs/heads/{branch}"], ct, Budget)).Match(
            attempt => attempt switch
            {
                ProcessAttempt.Completed { Result.ExitCode: 0 } => Outcome<bool>.Success(true),
                ProcessAttempt.Completed { Result.ExitCode: 1 } => Outcome<bool>.Success(false),
                var other => Outcome<bool>.Failure(Said(other)),
            },
            Outcome<bool>.Failure);
    }

    private static IReadOnlyList<WorktreeEntry> WorktreeEntries(string text)
    {
        var entries = new List<WorktreeEntry>();
        foreach (var block in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string path = string.Empty, branch = string.Empty;
            bool detached = false, prunable = false;
            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("worktree ", StringComparison.Ordinal))
                {
                    path = line["worktree ".Length..];
                }
                else if (line.StartsWith("branch ", StringComparison.Ordinal))
                {
                    branch = line["branch ".Length..].Trim();
                }
                else if (line.Trim() == "detached")
                {
                    detached = true;
                }
                else if (line.StartsWith("prunable", StringComparison.Ordinal))
                {
                    prunable = true;
                }
            }

            if (path.Length > 0)
            {
                entries.Add(new WorktreeEntry(path, branch, detached, prunable));
            }
        }

        return entries;
    }

    /// <summary>A branch name for the E4 questions: not empty, no leading <c>-</c>, and none of the characters git reads as revision syntax or refuses in a ref (the name is built from a task id, so this is the second line).</summary>
    private static string BranchRefusal(string branch) =>
        Refusal(branch) is { Length: > 0 } refused ? refused
        : branch.IndexOfAny([' ', '~', '^', ':', '?', '*', '[', '\\', '@', '{', '\n', '\0']) >= 0 || branch.Contains("..", StringComparison.Ordinal)
            ? $"'{branch}' is not a branch name the pipeline hands git: it holds revision syntax"
            : string.Empty;

    private static string PathRefusal(string path) => path.Length == 0 || path.StartsWith('-') ? $"'{path}' is not a path git may be given: it is empty or starts with '-'" : string.Empty;

    /// <summary>A commit-to-commit diff with the harness's three guards: no external diff program, no textconv, the plain patch — the clone's config and attributes are the model's.</summary>
    private Task<Outcome<string>> DiffOf(string baseSha, string[] options, string[] pathspec, CancellationToken ct) =>
        CommitId().IsMatch(baseSha)
            ? AskAsync([.. Pinned, "-c", "diff.external=", "diff", "--no-ext-diff", "--no-textconv", "--submodule=short", .. options, $"{baseSha}..HEAD", .. pathspec], ct)
            : Task.FromResult(Outcome<string>.Failure(NotACommitId(baseSha)));

    private static string NotACommitId(string text) => $"'{text}' is not a commit id — a file holds the id, and a file's text is never handed to git as an option";

    private static string Refusal(string branch) =>
        branch.Length == 0 || branch.StartsWith('-') ? $"'{branch}' is not a branch name git may be given: it is empty or starts with '-'" : string.Empty;

    private static Outcome<IReadOnlyList<string>> LinesOf(string text) => Outcome<IReadOnlyList<string>>.Success([.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)]);

    private static Outcome<IReadOnlyList<string>> Lines(Outcome<string> answer) => answer.Match(LinesOf, Outcome<IReadOnlyList<string>>.Failure);

    private static Outcome<string> Trimmed(Outcome<string> answer) => answer.Match(text => Outcome<string>.Success(text.Trim()), Outcome<string>.Failure);

    private async Task<Outcome<string>> AskAsync(
        string[] arguments, CancellationToken ct, TimeSpan? budget = null, int? noAnswer = null, bool smudges = false, Transport transport = Transport.None, bool merges = false, IReadOnlyDictionary<string, string>? extra = null, string input = "", bool wholeOutput = false) =>
        (await LaunchAsync(arguments, ct, budget ?? Budget, smudges, transport, merges, extra, input)).Match(
            attempt => attempt switch
            {
                ProcessAttempt.Completed { Result.ExitCode: 0 } done => Outcome<string>.Success(done.Result.StandardOutput),
                ProcessAttempt.Completed done when done.Result.ExitCode == noAnswer => Outcome<string>.Success(string.Empty),
                ProcessAttempt.Completed done when wholeOutput && (done.Result.StandardOutput + done.Result.StandardError).Trim() is { Length: > 0 } whole => Outcome<string>.Failure(whole),
                _ => Outcome<string>.Failure(Said(attempt)),
            },
            Outcome<string>.Failure);

    /// <summary>One launch of the question. Unless <paramref name="smudges"/> — which only the three questions that write the base's worktree from the operator's objects set — the clone's filter drivers are neutralised first (<see cref="EnvironmentForAsync"/>), and for a question that
    /// merges (<paramref name="merges"/>) its merge drivers too; a clone whose config cannot be listed is the question's failure, never a question asked unguarded.</summary>
    private async Task<Outcome<ProcessAttempt>> LaunchAsync(
        string[] arguments, CancellationToken ct, TimeSpan budget, bool smudges = false, Transport transport = Transport.None, bool merges = false, IReadOnlyDictionary<string, string>? extra = null, string input = "") =>
        await (await EnvironmentForAsync(smudges, transport, merges, ct)).Match(
            launchEnvironment => LaunchUnderAsync(arguments, extra is null ? launchEnvironment : With(launchEnvironment, extra), ct, budget, input),
            reason => Task.FromResult(Outcome<ProcessAttempt>.Failure(reason)));

    private async Task<Outcome<ProcessAttempt>> LaunchUnderAsync(string[] arguments, IReadOnlyDictionary<string, string> launchEnvironment, CancellationToken ct, TimeSpan budget, string input = "") =>
        Outcome<ProcessAttempt>.Success(await ProcessRunner.RunAsync(_git, arguments, _workingDirectory, budget, input, launchEnvironment, ct));

    private static IReadOnlyDictionary<string, string> With(IReadOnlyDictionary<string, string> basis, IReadOnlyDictionary<string, string> extra)
    {
        var merged = new Dictionary<string, string>(basis, StringComparer.Ordinal);
        foreach (var (name, value) in extra)
        {
            merged[name] = value;
        }

        return merged;
    }

    /// <summary>The environment of one question: <see cref="_environment"/> (with the question's transport), plus — unless the question writes the base's worktree — one <c>GIT_CONFIG_*</c> quadruple per filter driver git would read, found by <c>config --name-only -z --get-regexp ^filter\.</c> in the folder the
    /// question is asked of (every scope git reads, includes too; exit 1 is none), and for a question that <paramref name="merges"/> one more pair per merge driver key (<c>merge.&lt;name&gt;.driver</c>, the key verbatim) and for <c>merge.default</c>, emptied, numbered after the filter pairs. The enumerations go through
    /// the basis directly, never through this method. A failure of either — a timeout, exit 128 — is the question's failure: fail closed.</summary>
    private async Task<Outcome<IReadOnlyDictionary<string, string>>> EnvironmentForAsync(bool smudges, Transport transport, bool merges, CancellationToken ct)
    {
        var basis = transport switch { Transport.Any => _environment, Transport.File => _fileOnly, _ => _sealed };
        if (smudges)
        {
            return Outcome<IReadOnlyDictionary<string, string>>.Success(basis);
        }

        var listed = await ListAsync(@"^filter\.", "filter drivers", ct);
        if (listed is Outcome<string>.Fail failed)
        {
            return Outcome<IReadOnlyDictionary<string, string>>.Failure(failed.Reason);
        }

        var pairs = FilterPairs(((Outcome<string>.Ok)listed).Value);
        if (merges)
        {
            var mergeKeys = await ListAsync(@"^merge\.", "merge drivers", ct);
            if (mergeKeys is Outcome<string>.Fail mergeFailed)
            {
                return Outcome<IReadOnlyDictionary<string, string>>.Failure(mergeFailed.Reason);
            }

            pairs.AddRange(MergePairs(((Outcome<string>.Ok)mergeKeys).Value));
        }

        return Outcome<IReadOnlyDictionary<string, string>>.Success(Neutralised(pairs, basis));
    }

    /// <summary><c>config --name-only -z --get-regexp &lt;pattern&gt;</c> through the basis environment: the NUL-separated key names, or the empty text for exit 1 (none).</summary>
    private async Task<Outcome<string>> ListAsync(string pattern, string what, CancellationToken ct)
    {
        var listed = await ProcessRunner.RunAsync(_git, [.. Pinned, "config", "--name-only", "-z", "--get-regexp", pattern], _workingDirectory, Budget, string.Empty, _sealed, ct);
        return listed switch
        {
            ProcessAttempt.Completed { Result.ExitCode: 0 } done => Outcome<string>.Success(done.Result.StandardOutput),
            ProcessAttempt.Completed { Result.ExitCode: 1 } => Outcome<string>.Success(string.Empty),
            _ => Outcome<string>.Failure($"{EnumerationFailure}{what}: {Said(listed)}"),
        };
    }

    /// <summary>The drivers named by <c>filter.&lt;driver&gt;.&lt;variable&gt;</c> keys; the driver may be EMPTY (<c>[filter ""]</c> is the key <c>filter..clean</c>) and may hold dots, <c>=</c>, quotes or spaces — the key is used verbatim.</summary>
    private static List<string> DriversOf(string names) =>
        [.. names.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.LastIndexOf('.') is var last && last >= Filter.Length ? name[Filter.Length..last] : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)];

    /// <summary>For each driver: <c>clean</c>, <c>smudge</c> and <c>process</c> empty and <c>required</c> false.</summary>
    private static List<(string Key, string Value)> FilterPairs(string names)
    {
        string[] variables = ["clean", "smudge", "process", "required"];
        string[] values = [string.Empty, string.Empty, string.Empty, "false"];
        return [.. DriversOf(names).SelectMany(driver => variables.Select((variable, index) => ($"filter.{driver}.{variable}", values[index])))];
    }

    /// <summary>Every <c>merge.&lt;name&gt;.driver</c> (the name may hold dots or <c>=</c>: the key is its own variable, so the <c>=</c> splits nothing) and <c>merge.default</c>, emptied. An emptied driver is "cannot run" and the pick stops at its conflict (M-k, M-v); <c>merge=union|binary|text</c> are git's built-ins and run nothing.</summary>
    private static List<(string Key, string Value)> MergePairs(string names) =>
        [.. names.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(name => name == "merge.default" || (name.StartsWith("merge.", StringComparison.Ordinal) && name.EndsWith(".driver", StringComparison.Ordinal) && name.Length >= "merge..driver".Length))
            .Distinct(StringComparer.Ordinal)
            .Select(name => (name, string.Empty))];

    /// <summary>The pairs as <c>GIT_CONFIG_KEY_n</c>/<c>VALUE_n</c> counted by <c>GIT_CONFIG_COUNT</c>. Environment variables, not <c>-c</c>: <c>-c</c> splits at the first <c>=</c>, so a driver named <c>a=b</c> slips past it.</summary>
    private static IReadOnlyDictionary<string, string> Neutralised(List<(string Key, string Value)> pairs, IReadOnlyDictionary<string, string> basis)
    {
        var launched = new Dictionary<string, string>(basis, StringComparer.Ordinal);
        if (pairs.Count == 0)
        {
            return launched;
        }

        for (var n = 0; n < pairs.Count; n++)
        {
            launched[$"GIT_CONFIG_KEY_{n.ToString(CultureInfo.InvariantCulture)}"] = pairs[n].Key;
            launched[$"GIT_CONFIG_VALUE_{n.ToString(CultureInfo.InvariantCulture)}"] = pairs[n].Value;
        }

        launched["GIT_CONFIG_COUNT"] = pairs.Count.ToString(CultureInfo.InvariantCulture);
        return launched;
    }

    private static string Said(ProcessAttempt attempt) =>
        attempt is ProcessAttempt.Completed done && FirstLine(done.Result.StandardError) is { Length: > 0 } line ? line : attempt.Describe;

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;

    [GeneratedRegex(@"^[0-9a-f]{4,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommitId();
}

/// <summary>One registration of <c>worktree list --porcelain</c>: <paramref name="Branch"/> is the full ref (<c>refs/heads/tmp/t1</c>) or empty; <paramref name="Prunable"/> says the folder is gone.</summary>
public sealed record WorktreeEntry(string Path, string Branch, bool Detached, bool Prunable);
