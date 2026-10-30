using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><see cref="ArmGit"/> as E3 widened it (C14): the questions <c>prep-base</c> asks of the base clone and the ones <c>make</c> and <c>checks</c> ask of an arm's, each against a REAL git over real folders, each started by the real path it was handed with <c>-c core.fsmonitor=false</c> —
/// the class is the one place the module asks git, so one test per answer the later verbs read.
/// <para>
/// What this does NOT prove: Windows (the shims are shell scripts); a git other than the host's (every plant is observed on the host's git first, so a git that runs a program these flags do not name turns a test red rather than passing silently); <c>smudge=</c> is neutralised by the same mechanism as <c>clean=</c>
/// but no question here writes a worktree — E4's does, and its test must observe it.
/// </para></summary>
public sealed class ArmGitWideningTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempFolder _temp = new();

    private IReadOnlyDictionary<string, string> Environment => new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = _temp.Folder("home"), ["PATH"] = "/usr/bin:/bin" };

    private ArmGit In(string folder, string? git = null) => new(git ?? ExperimentRoot.RealGit, folder, Environment);

    public void Dispose() => _temp.Dispose();

    private int _dirtied;

    private string Sentinel => _temp.Combine("sentinel.log");

    private bool Ran => File.Exists(Sentinel);

    /// <summary>A program a clone's config or hooks folder can name: it appends a line to the sentinel and, as a filter must, passes its input through.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private string PlantProgram(string name, string? body = null)
    {
        var path = _temp.File($"#!/bin/sh\necho \"$0 ran\" >> '{Sentinel}'\n{body ?? "cat"}\n", "plant", name);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    /// <summary>Makes <paramref name="file"/> stat-dirty without changing a byte: a distinct, older modification time every call — a repeated one would let a later "no program ran" prove nothing, because git would find the index already refreshed.</summary>
    private void Redirty(string file) => File.SetLastWriteTimeUtc(file, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(++_dirtied));

    /// <summary>The host's git, EXIT CODE IGNORED, under the scratch environment: for proving a plant live (a plant that makes git fail still ran its program).</summary>
    private async Task HostGitAsync(string folder, params string[] arguments)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, arguments, folder, TimeSpan.FromSeconds(60), string.Empty, Environment, Ct);
        attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>(attempt.Describe);
    }

    /// <summary>The plant is LIVE: the host's own git, asked the same kind of question over a stat-dirty file, ran the program. Then the sentinel is deleted and the file made dirty again, so the question under test starts from the same state.</summary>
    private async Task ProveLiveAsync(string folder, string dirtyFile, params string[] hostQuestion)
    {
        Redirty(dirtyFile);
        await HostGitAsync(folder, hostQuestion);
        Ran.Should().BeTrue("the plant must be proven live with the host's git first, or 'no program ran' proves nothing");
        File.Delete(Sentinel);
        Redirty(dirtyFile);
    }

    private async Task<string> ClonedAsync()
    {
        var (source, _, _) = await SourceAsync();
        var clone = Path.Combine(_temp.Path, "clone");
        (await In(_temp.Path).CloneAsync(source, "plan", clone, Ct)).Ok();
        return clone;
    }

    /// <summary>A clone holding a submodule <c>sub</c> (a file <c>s.txt</c>) at the superproject's HEAD.</summary>
    private async Task<(string Clone, string Sub, string ModuleFolder)> WithSubmoduleAsync()
    {
        var clone = await ClonedAsync();
        var sub = _temp.Folder("subsrc");
        await TargetBench.GitAsync(sub, "init", "--quiet", "--initial-branch", "main");
        _temp.File("one\n", "subsrc", "s.txt");
        await CommitAllAsync(sub, "sub first");
        await TargetBench.GitAsync(clone, "-c", "protocol.file.allow=always", "submodule", "add", "--quiet", sub, "sub");
        await CommitAllAsync(clone, "add the submodule");
        return (clone, Path.Combine(clone, "sub"), Path.Combine(clone, ".git", "modules", "sub"));
    }

    private static async Task CommitAllAsync(string folder, string message)
    {
        await TargetBench.GitAsync(folder, "add", "-A");
        await TargetBench.GitAsync(folder, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message);
    }

    /// <summary>A source of two commits: <c>a.txt</c>, then <c>src/B.cs</c> and <c>a.txt</c> changed.</summary>
    private async Task<(string Source, string First, string Second)> SourceAsync()
    {
        var source = _temp.Folder("source");
        await TargetBench.GitAsync(source, "init", "--quiet", "--initial-branch", "plan");
        _temp.File("one\n", "source", "a.txt");
        await CommitAllAsync(source, "first");
        var first = (await TargetBench.GitAsync(source, "rev-parse", "HEAD")).Trim();
        _temp.File("class B { }\n", "source", "src", "B.cs");
        _temp.File("one\ntwo\n", "source", "a.txt");
        await CommitAllAsync(source, "second");
        return (source, first, (await TargetBench.GitAsync(source, "rev-parse", "HEAD")).Trim());
    }

    [Fact]
    public async Task A_clone_is_made_detached_at_a_commit_and_an_arm_branches_from_there()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the recording shim is a shell script");
        var (source, first, second) = await SourceAsync();
        var clone = Path.Combine(_temp.Path, "clone");

        (await In(_temp.Path).CloneAsync(source, "plan", clone, Ct)).Ok();
        var inClone = In(clone);

        (await inClone.HeadAsync(Ct)).Ok().Should().Be(second, "a clone is at the branch's head");
        (await inClone.DetachAsync(first, Ct)).Ok();
        (await inClone.HeadAsync(Ct)).Ok().Should().Be(first, "the detach moves it to the configured commit");
        (await inClone.NewBranchAsync("feat/x", Ct)).Ok();
        (await TargetBench.GitAsync(clone, "rev-parse", "--abbrev-ref", "HEAD")).Trim().Should().Be("feat/x");
        (await inClone.CountSinceAsync(first, Ct)).Ok().Should().Be(0);
    }

    [Theory]
    [InlineData("-x")]
    [InlineData("--detach")]
    [InlineData("")]
    public async Task A_branch_name_that_could_be_an_option_is_refused_before_git_is_asked(string name)
    {
        var shim = Path.Combine(_temp.Path, "git-never");

        var branch = await In(_temp.Path, shim).NewBranchAsync(name, Ct);
        var clone = await In(_temp.Path, shim).CloneAsync(_temp.Path, name, Path.Combine(_temp.Path, "x"), Ct);

        branch.Failed().Should().BeTrue();
        clone.Failed().Should().BeTrue();
        branch.Reason().Should().Contain("not a branch name");
    }

    [Theory]
    [InlineData("--output=/tmp/x")]
    [InlineData("HEAD")]
    [InlineData("zz")]
    public async Task A_base_that_is_not_a_hex_id_is_refused_by_every_question_that_takes_one(string text)
    {
        var git = In(_temp.Path, Path.Combine(_temp.Path, "git-never"));

        (await git.DetachAsync(text, Ct)).Failed().Should().BeTrue();
        (await git.ChangedCsAsync(text, Ct)).Failed().Should().BeTrue();
        (await git.NumstatAsync(text, Ct)).Failed().Should().BeTrue();
        (await git.ShortstatAsync(text, Ct)).Failed().Should().BeTrue();
        (await git.CountSinceAsync(text, Ct)).Failed().Should().BeTrue();
        (await git.NumstatAsync(text, Ct)).Reason().Should().Contain("is not a commit id");
    }

    [Fact]
    public async Task The_arms_questions_are_answered_from_its_clone()
    {
        var (source, first, second) = await SourceAsync();
        var clone = Path.Combine(_temp.Path, "clone");
        (await In(_temp.Path).CloneAsync(source, "plan", clone, Ct)).Ok();
        var git = In(clone);

        (await git.UncommittedCountAsync(Ct)).Ok().Should().Be(0);
        _temp.File("x\n", "clone", "new one.cs");
        _temp.File("y\n", "clone", "notes.md");
        (await git.UncommittedCountAsync(Ct)).Ok().Should().Be(2, "two untracked files");
        await CommitAllAsync(clone, "the arm's work");

        (await git.ChangedCsAsync(first, Ct)).Ok().Should().BeEquivalentTo(["src/B.cs", "new one.cs"], "added or modified .cs files, a name with a space as ONE name");
        (await git.NumstatAsync(first, Ct)).Ok().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Contain("1\t0\tsrc/B.cs").And.Contain("1\t0\tnew one.cs").And.Contain("1\t0\tnotes.md").And.Contain("1\t0\ta.txt");
        (await git.ShortstatAsync(first, Ct)).Ok().Should().Contain("4 files changed");
        (await git.CountSinceAsync(first, Ct)).Ok().Should().Be(2);
        (await git.CountSinceAsync(second, Ct)).Ok().Should().Be(1);
        (await git.MentionsAsync("class B", ["src"], Ct)).Ok().Should().BeTrue();
        (await git.MentionsAsync("class B", ["docs"], Ct)).Ok().Should().BeFalse("exit 1 is no, not a failure");
        (await git.MentionsAsync("a.*b", ["src"], Ct)).Ok().Should().BeFalse("the text is fixed text, never a pattern");
        (await git.MentionsAsync("-e", ["src"], Ct)).Ok().Should().BeFalse("a text that looks like an option is the text");
    }

    [Fact]
    public async Task The_rules_submodule_is_found_by_the_gitmodules_the_clone_holds()
    {
        var (source, _, _) = await SourceAsync();
        var clone = Path.Combine(_temp.Path, "clone");
        (await In(_temp.Path).CloneAsync(source, "plan", clone, Ct)).Ok();
        var git = In(clone);

        (await git.SubmodulePathsAsync(Ct)).Ok().Should().BeEmpty("a repository with no .gitmodules has no submodule: that is an answer, not a failure");

        _temp.File("[submodule \"rules\"]\n\tpath = .agents/conventions\n\turl = ../rules\n[submodule \"web\"]\n\tpath = web\n\turl = ../web\n", "clone", ".gitmodules");
        var paths = (await git.SubmodulePathsAsync(Ct)).Ok();

        GitModules.NameOf(paths, ".agents/conventions").Should().Be("rules");
        GitModules.NameOf(paths, "web").Should().Be("web");
        GitModules.NameOf(paths, "nowhere").Should().BeEmpty();
        (await git.SetSubmoduleUrlAsync("rules", "/some/where", Ct)).Ok();
        (await TargetBench.GitAsync(clone, "config", "--get", "submodule.rules.url")).Trim().Should().Be("/some/where");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Every_question_is_started_hardened_by_the_path_it_was_handed_under_an_environment_with_no_GIT_variable_but_the_lazy_fetch_switch()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the recording shim is a shell script");
        var (source, first, _) = await SourceAsync();
        var marker = Path.Combine(_temp.Path, "git-calls");
        var shim = _temp.File($"#!/bin/sh\necho \"$*\" >> '{marker}'\n/usr/bin/env | while IFS= read -r l; do case \"$l\" in GIT_*) echo \"$l\" >> '{marker}';; esac; done\nexec '{ExperimentRoot.RealGit}' \"$@\"\n", "bin", "git");
        File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var clone = Path.Combine(_temp.Path, "clone");
        var environment = GitEnvironment.Of(new Dictionary<string, string>(Environment, StringComparer.Ordinal) { ["GIT_DIR"] = "/nonexistent" });
        var git = new ArmGit(shim, _temp.Path, environment);

        (await git.CloneAsync(source, "plan", clone, Ct)).Ok();
        var inClone = new ArmGit(shim, clone, environment);
        (await inClone.DetachAsync(first, Ct)).Ok();
        (await inClone.NewBranchAsync("feat/x", Ct)).Ok();
        await inClone.UncommittedCountAsync(Ct);
        await inClone.ChangedCsAsync(first, Ct);
        await inClone.NumstatAsync(first, Ct);
        await inClone.ShortstatAsync(first, Ct);
        await inClone.CountSinceAsync(first, Ct);
        await inClone.MentionsAsync("x", ["src"], Ct);
        await inClone.SubmoduleUpdateAsync("nowhere", Ct);

        var lines = File.ReadAllLines(marker);
        var asked = lines.Where(line => !line.StartsWith("GIT_", StringComparison.Ordinal)).ToList();
        asked.Should().HaveCountGreaterThanOrEqualTo(10).And.OnlyContain(line => line.StartsWith("-c core.fsmonitor=false ", StringComparison.Ordinal), "every question the class asks is hardened");
        asked.Where(line => line.Contains("diff", StringComparison.Ordinal)).Should().OnlyContain(line => line.Contains("-c diff.external= ", StringComparison.Ordinal) && line.Contains("--no-ext-diff --no-textconv", StringComparison.Ordinal), "every diff is the plain patch");
        asked.Where(line => line.Contains("protocol.file.allow", StringComparison.Ordinal)).Should().ContainSingle("the file protocol is allowed for the one submodule update only");
        lines.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal)).Distinct().Should().Equal(["GIT_NO_LAZY_FETCH=1"], "no GIT_* of the parent's reaches git; the one it is given is the harness's own");
    }

    private const string Attributes = "info/attributes";

    private void AppendConfig(string folder, string text) => File.AppendAllText(Path.Combine(folder, "config"), text);

    private void AppendAttributes(string gitFolder, string text)
    {
        Directory.CreateDirectory(Path.Combine(gitFolder, "info"));
        File.AppendAllText(Path.Combine(gitFolder, Attributes), text);
    }

    /// <summary>The recording shim: one block per call — <c>CALL &lt;argv&gt;</c>, then <c>ENV name=value</c> for every <c>GIT_*</c> and the names the tests care about — appended to <paramref name="marker"/>, then the host's git.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private string RecordingShim(string marker)
    {
        var shim = _temp.File(
            $"#!/bin/sh\necho \"CALL $*\" >> '{marker}'\n/usr/bin/env | while IFS= read -r l; do case \"$l\" in GIT_*|GITHUB_TOKEN=*|CLAUDE_CODE_OAUTH_TOKEN=*|BENCH_DB=*|SSH_AUTH_SOCK=*|KEEP_ME=*) echo \"ENV ${{l%%=*}}=${{l#*=}}\" >> '{marker}';; esac; done\nexec '{ExperimentRoot.RealGit}' \"$@\"\n",
            "bin", "recording-git");
        File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return shim;
    }

    private static IReadOnlyList<(string Call, IReadOnlyList<string> Env)> Blocks(string marker)
    {
        var blocks = new List<(string, List<string>)>();
        foreach (var line in File.ReadAllLines(marker))
        {
            if (line.StartsWith("CALL ", StringComparison.Ordinal))
            {
                blocks.Add((line[5..], []));
            }
            else if (line.StartsWith("ENV ", StringComparison.Ordinal))
            {
                blocks[^1].Item2.Add(line[4..]);
            }
        }

        return [.. blocks.Select(block => (block.Item1, (IReadOnlyList<string>)block.Item2))];
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Every_question_runs_under_the_targets_environment_whatever_the_caller_handed_in()
    {
        var (source, _, _) = await SourceAsync();
        var marker = _temp.Combine("calls.log");
        var parent = new Dictionary<string, string>(Environment, StringComparer.Ordinal)
        {
            ["GITHUB_TOKEN"] = "ghp_value", ["CLAUDE_CODE_OAUTH_TOKEN"] = "oauth-value", ["BENCH_DB"] = "Host=db;Password=x", ["SSH_AUTH_SOCK"] = "/run/user/1000/keyring/ssh", ["KEEP_ME"] = "yes", ["GIT_DIR"] = "/nonexistent",
        };

        _ = await new ArmGit(RecordingShim(marker), source, parent).HeadAsync(Ct); // the answer is not the point: what the call was started with is

        var calls = Blocks(marker);
        calls.Should().NotBeEmpty();
        foreach (var (call, env) in calls)
        {
            var names = env.Select(line => line.Split('=')[0]).ToList();
            names.Should().Contain("KEEP_ME", $"{call}: a variable no rule names passes").And.NotContain(["GITHUB_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "BENCH_DB", "SSH_AUTH_SOCK", "GIT_DIR"], $"{call}: the class reduces whatever it is handed, so no secret reaches a program git might still run");
            env.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal)).Should().Equal(["GIT_NO_LAZY_FETCH=1"], call);
        }
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_hook_of_the_clone_never_runs_for_a_harness_question()
    {
        var clone = await ClonedAsync();
        var hooks = _temp.Folder("clone", "myhooks");
        foreach (var hook in new[] { "post-checkout", "post-index-change" })
        {
            File.Copy(PlantProgram("hook-" + hook, "exit 0"), Path.Combine(hooks, hook));
        }

        File.Copy(PlantProgram("hook-dot-git", "exit 0"), Path.Combine(clone, ".git", "hooks", "post-index-change"));
        AppendConfig(Path.Combine(clone, ".git"), $"[core]\n\thooksPath = {hooks}\n");
        File.AppendAllText(Path.Combine(clone, ".git", "info", "exclude"), "myhooks/\n");
        var a = Path.Combine(clone, "a.txt");
        await ProveLiveAsync(clone, a, "status", "--porcelain");
        await HostGitAsync(clone, "checkout", "-q", "-b", "probe");
        Ran.Should().BeTrue("post-checkout is live too");
        await HostGitAsync(clone, "checkout", "-q", "plan");
        File.Delete(Sentinel);
        Redirty(a);

        (await In(clone).UncommittedCountAsync(Ct)).Ok().Should().Be(0);
        Ran.Should().BeFalse("status ran post-index-change from the clone's hooks folder");
        (await In(clone).NewBranchAsync("feat/h", Ct)).Ok();
        Ran.Should().BeFalse("checkout -b ran post-checkout");
    }

    private const string CleanOnly = "clean";
    private const string WithProcessAndRequired = "process";
    private const string NamedWithEquals = "equals";

    /// <summary>Plants a filter driver the clone names, proves it live with the host's git, and leaves the dirty file ready.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private async Task<string> PlantFilterAsync(string plant)
    {
        var clone = await ClonedAsync();
        var program = PlantProgram("filter");
        var driver = plant == NamedWithEquals ? "a=b" : "x";
        AppendConfig(Path.Combine(clone, ".git"), $"[filter \"{driver}\"]\n\tclean = {program}\n" + (plant == WithProcessAndRequired ? $"\tprocess = {program}\n\trequired = true\n" : string.Empty));
        AppendAttributes(Path.Combine(clone, ".git"), $"* filter={driver}\n");
        await ProveLiveAsync(clone, Path.Combine(clone, "a.txt"), "status", "--porcelain");
        return clone;
    }

    [Theory]
    [InlineData(CleanOnly)]
    [InlineData(WithProcessAndRequired)]
    [InlineData(NamedWithEquals)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_filter_driver_the_clone_names_never_runs_for_the_count(string plant)
    {
        var clone = await PlantFilterAsync(plant);

        var count = await In(clone).UncommittedCountAsync(Ct);

        Ran.Should().BeFalse("status hashes a stat-dirty file and ran its clean filter");
        count.Ok().Should().Be(0, "the file's bytes are the same: only its time changed");
    }

    [Theory]
    [InlineData(CleanOnly)]
    [InlineData(WithProcessAndRequired)]
    [InlineData(NamedWithEquals)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_filter_driver_the_clone_names_never_runs_for_a_new_branch(string plant)
    {
        var clone = await PlantFilterAsync(plant);

        var branch = await In(clone).NewBranchAsync("feat/f", Ct);

        Ran.Should().BeFalse("checkout -b over a stat-dirty index hashed the file through its clean filter");
        branch.Ok();
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_count_names_every_filter_driver_git_would_read_and_nothing_when_there_is_none()
    {
        var clone = await ClonedAsync();
        var marker = _temp.Combine("calls.log");
        var gitconfig = Path.Combine(Environment["HOME"], ".gitconfig");
        File.WriteAllText(gitconfig, "[filter \"lfs\"]\n\tclean = /bin/false\n");

        (await In(clone, RecordingShim(marker)).UncommittedCountAsync(Ct)).Ok();

        var status = Blocks(marker).Single(block => block.Call.Contains(" status ", StringComparison.Ordinal)).Env.ToList();
        status.Select(line => line.Split('=')[0]).Should().Contain(["GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_0", "GIT_CONFIG_KEY_1", "GIT_CONFIG_KEY_2", "GIT_CONFIG_KEY_3", "GIT_CONFIG_VALUE_0", "GIT_CONFIG_VALUE_1", "GIT_CONFIG_VALUE_2", "GIT_CONFIG_VALUE_3"], "a driver of the GLOBAL config is read by git too");
        status.Should().Contain(["GIT_CONFIG_COUNT=4", "GIT_CONFIG_KEY_0=filter.lfs.clean", "GIT_CONFIG_VALUE_0=", "GIT_CONFIG_KEY_3=filter.lfs.required", "GIT_CONFIG_VALUE_3=false"]);

        File.Delete(gitconfig);
        File.Delete(marker);
        (await In(clone, RecordingShim(marker)).UncommittedCountAsync(Ct)).Ok();

        Blocks(marker).Single(block => block.Call.Contains(" status ", StringComparison.Ordinal)).Env.Should().NotContain(line => line.StartsWith("GIT_CONFIG_", StringComparison.Ordinal), "no driver, nothing to neutralise");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_submodules_own_config_is_never_consulted_and_its_commit_change_is_still_counted()
    {
        var (clone, sub, module) = await WithSubmoduleAsync();
        AppendConfig(module, $"[filter \"y\"]\n\tclean = {PlantProgram("sub-filter")}\n");
        AppendAttributes(module, "* filter=y\n");
        var file = Path.Combine(sub, "s.txt");
        await ProveLiveAsync(clone, file, "status", "--porcelain");

        (await In(clone).UncommittedCountAsync(Ct)).Ok().Should().Be(0, "a stat-dirty file inside the submodule");
        Ran.Should().BeFalse("status spawned a status inside the submodule and ran its clean filter");

        File.WriteAllText(file, "two\n");
        (await In(clone).UncommittedCountAsync(Ct)).Ok().Should().Be(0, "an uncommitted edit inside a submodule is not a line (the count's deviation from the shell, E3 §8 item 40)");

        await TargetBench.GitAsync(sub, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-a", "-m", "sub second");
        File.Delete(Sentinel); // the host's own commit just ran the plant over the staged file: the next question starts clean
        (await In(clone).UncommittedCountAsync(Ct)).Ok().Should().Be(1, "a changed submodule COMMIT is still a line");
        Ran.Should().BeFalse();
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_diff_driver_of_a_submodule_never_runs_for_the_gates_patch()
    {
        var (clone, sub, module) = await WithSubmoduleAsync();
        var before = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        AppendConfig(Path.Combine(clone, ".git"), "[diff]\n\tsubmodule = diff\n");
        AppendConfig(module, $"[diff \"z\"]\n\tcommand = {PlantProgram("sub-diff", "exit 0")}\n");
        AppendAttributes(module, "* diff=z\n");
        File.WriteAllText(Path.Combine(sub, "s.txt"), "two\n");
        await TargetBench.GitAsync(sub, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-a", "-m", "sub second");
        await CommitAllAsync(clone, "bump the gitlink");
        await HostGitAsync(clone, "diff", before, "HEAD");
        Ran.Should().BeTrue("the plant must be proven live with the host's git first: under diff.submodule=diff git spawns a diff in the submodule and runs its diff.z.command");
        File.Delete(Sentinel);

        var patch = (await In(clone).DiffAsync(before, Ct)).Ok();

        Ran.Should().BeFalse("the gate's snapshot diff ran a program a submodule's config names");
        patch.Should().MatchRegex("Subproject commit|160000");
    }
}
