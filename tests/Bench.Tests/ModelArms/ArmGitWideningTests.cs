using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><see cref="ArmGit"/> as E3 widened it (C14): the questions <c>prep-base</c> asks of the base clone and the ones <c>make</c> and <c>checks</c> ask of an arm's, each against a REAL git over real folders, each started by the real path it was handed with <c>-c core.fsmonitor=false</c> —
/// the class is the one place the module asks git, so one test per answer the later verbs read.
/// <para>
/// What this does NOT prove: that a program the clone's config names is not run by <c>status</c> (a clean filter is — the class's doc says so); that a git other than the host's answers the same; Windows (the recording shim is a shell script).
/// </para></summary>
public sealed class ArmGitWideningTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempFolder _temp = new();

    private IReadOnlyDictionary<string, string> Environment => new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = _temp.Folder("home"), ["PATH"] = "/usr/bin:/bin" };

    private ArmGit In(string folder, string? git = null) => new(git ?? ExperimentRoot.RealGit, folder, Environment);

    public void Dispose() => _temp.Dispose();

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
}
