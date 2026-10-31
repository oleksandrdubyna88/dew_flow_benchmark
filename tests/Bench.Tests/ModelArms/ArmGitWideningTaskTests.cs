using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The questions E4.S2 added to <see cref="ArmGit"/> for the task pipeline (the arm's worktrees, the pick-back and the re-entry), each against a REAL git over real folders. Every write of the arm's clone is neutralised; the cherry-pick alone also carries the merge neutralisation and the committer pin; the arm's submodule update allows the
/// file transport alone. The sentinel rows (a program the clone names that must not run) are in <c>TaskWorktreesTests</c>, over the pipeline; here each question's answer is observed against a real clone.
/// <para>
/// What this does NOT prove: Windows (shell shims and plants); a git other than the host's (the pilot's log).
/// </para></summary>
public sealed partial class ArmGitWideningTests
{
    private static string Worktree(string clone, string name) => Path.Combine(Path.GetDirectoryName(clone)!, name);

    private async Task<string> CommitInAsync(string folder, string file, string text, string message)
    {
        File.WriteAllText(Path.Combine(folder, file), text);
        await TargetBench.GitAsync(folder, "add", "--", file);
        await CommitAllAsync(folder, message);
        return (await TargetBench.GitAsync(folder, "rev-parse", "HEAD")).Trim();
    }

    [Fact]
    public async Task Worktree_add_attach_remove_prune_and_branch_delete_answer_against_real_git_and_a_dash_name_is_refused()
    {
        var clone = await ClonedAsync();
        var git = In(clone);
        var one = Worktree(clone, "wt-one");

        (await git.WorktreeAddAsync(one, "tmp/t1", Ct)).Ok();

        (await TargetBench.GitAsync(one, "symbolic-ref", "--short", "HEAD")).Trim().Should().Be("tmp/t1");
        (await git.WorktreeRemoveAsync(one, Ct)).Ok();
        Directory.Exists(one).Should().BeFalse();
        (await git.WorktreeAddAsync(one, "tmp/t1", Ct)).Failed().Should().BeTrue("the branch exists: add -b over it is git's 255");
        (await git.WorktreeAttachAsync(one, "tmp/t1", Ct)).Ok();
        (await TargetBench.GitAsync(one, "symbolic-ref", "--short", "HEAD")).Trim().Should().Be("tmp/t1", "attached to the existing branch");
        await TargetBench.GitAsync(clone, "worktree", "lock", one);
        (await git.WorktreeRemoveAsync(one, Ct)).Ok();
        Directory.Exists(one).Should().BeFalse("the second --force removes a worktree the model locked");
        (await git.WorktreePruneAsync(Ct)).Ok();
        (await git.DeleteBranchAsync("tmp/t1", Ct)).Ok();
        (await TargetBench.GitAsync(clone, "branch", "--list", "tmp/t1")).Trim().Should().BeEmpty();
        foreach (var bad in new[] { "-x", "--force", string.Empty })
        {
            var never = In(clone, Path.Combine(_temp.Path, "git-never"));
            (await never.WorktreeAddAsync(Worktree(clone, "wt-two"), bad, Ct)).Failed().Should().BeTrue();
            (await never.WorktreeAttachAsync(Worktree(clone, "wt-two"), bad, Ct)).Failed().Should().BeTrue();
            (await never.DeleteBranchAsync(bad, Ct)).Failed().Should().BeTrue();
            (await never.BranchTipAsync(bad, Ct)).Failed().Should().BeTrue();
        }

        (await In(clone, Path.Combine(_temp.Path, "git-never")).WorktreeAddAsync("-evil", "tmp/t2", Ct)).Failed().Should().BeTrue("a path starting with '-' is refused the same way");
    }

    [Fact]
    public async Task Branch_tip_is_empty_for_a_missing_branch_the_sha_for_a_present_one_and_a_tag_of_the_same_name_is_not_read()
    {
        var clone = await ClonedAsync();
        var head = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        var git = In(clone);

        (await git.BranchTipAsync("tmp/t1", Ct)).Ok().Should().BeEmpty();
        await TargetBench.GitAsync(clone, "tag", "tmp/t1", head);
        (await git.BranchTipAsync("tmp/t1", Ct)).Ok().Should().BeEmpty("refs/heads/ is spelled in full: a tag of the same name is no branch");
        await TargetBench.GitAsync(clone, "branch", "tmp/t1");
        (await git.BranchTipAsync("tmp/t1", Ct)).Ok().Should().Be(head);
        (await git.BranchTipAsync("tmp/t1^{tree}", Ct)).Failed().Should().BeTrue("revision syntax is no branch name");
    }

    [Fact]
    public async Task Rev_list_lists_the_range_oldest_first_and_refuses_a_from_that_is_not_hex()
    {
        var clone = await ClonedAsync();
        var start = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        var one = await CommitInAsync(clone, "one.txt", "1\n", "one");
        var two = await CommitInAsync(clone, "two.txt", "2\n", "two");
        await TargetBench.GitAsync(clone, "branch", "tmp/t1");
        var three = await CommitInAsync(clone, "three.txt", "3\n", "three");
        var git = In(clone);

        (await git.RevListAsync(start, Ct)).Ok().Should().Equal(one, two, three);
        (await git.RevListAsync(start, "tmp/t1", Ct)).Ok().Should().Equal(one, two);
        (await git.RevListAsync("--output=/tmp/x", Ct)).Failed().Should().BeTrue();
        (await git.RevListAsync("HEAD", Ct)).Failed().Should().BeTrue();
        (await git.RevListAsync(start, "-x", Ct)).Failed().Should().BeTrue();
        three.Should().NotBeEmpty();
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Patch_ids_come_from_one_diff_tree_and_one_patch_id_launch_and_an_empty_or_merge_commit_has_none()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the recording shim is a shell script");
        var clone = await ClonedAsync();
        var start = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        var one = await CommitInAsync(clone, "one.txt", "1\n", "one");
        var two = await CommitInAsync(clone, "two.txt", "2\n", "two");
        await TargetBench.GitAsync(clone, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "--allow-empty", "-m", "empty");
        var empty = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        await TargetBench.GitAsync(clone, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "merge", "--no-ff", "--quiet", "-m", "merge", start);
        var merge = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        var marker = _temp.Combine("calls.log");

        var ids = (await In(clone, Sentinel.RecordingShim(_temp, marker)).PatchIdsAsync([one, two, empty, merge], Ct)).Ok();

        ids.Select(id => id.Sha).Should().Equal(one, two, empty, merge);
        ids[0].PatchId.Should().NotBeEmpty().And.NotBe(ids[1].PatchId);
        ids[2].PatchId.Should().BeEmpty("an empty commit has no patch (M-n)");
        ids[3].PatchId.Should().BeEmpty("a merge commit has no patch without -m (M-n)");
        var calls = Sentinel.Blocks(marker).Select(block => block.Call).Where(call => !IsEnumeration(call)).ToList();
        calls.Should().HaveCount(2, "ONE diff-tree and ONE patch-id launch whatever the commit count");
        calls[0].Should().EndWith(" -c diff.external= diff-tree --stdin -p --no-ext-diff --no-textconv --submodule=short");
        calls[1].Should().EndWith(" patch-id --stable");
        (await In(clone).PatchIdsAsync(["zz"], Ct)).Failed().Should().BeTrue("every sha is hex");
        (await In(clone).PatchIdsAsync([empty], Ct)).Ok().Single().PatchId.Should().BeEmpty("no patch anywhere: no patch-id launch waits on an unclosed stdin");
    }

    [Fact]
    public async Task Cherry_pick_makes_one_commit_per_source_keeps_the_author_and_pins_the_committer_on_a_host_without_an_identity()
    {
        var clone = await ClonedAsync();
        var start = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        await TargetBench.GitAsync(clone, "branch", "side");
        await CommitInAsync(clone, "arm.txt", "arm\n", "the arm");
        await TargetBench.GitAsync(clone, "checkout", "-q", "side");
        var source = await CommitInAsync(clone, "side.txt", "side\n", "the side");
        await TargetBench.GitAsync(clone, "checkout", "-q", "-");
        var armHead = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        Directory.CreateDirectory(Environment["HOME"]);

        var picked = await In(clone).CherryPickAsync(source, Ct);

        picked.Ok();
        (await TargetBench.GitAsync(clone, "rev-list", "--count", $"{armHead}..HEAD")).Trim().Should().Be("1");
        (await TargetBench.GitAsync(clone, "rev-parse", "HEAD^")).Trim().Should().Be(armHead);
        (await TargetBench.GitAsync(clone, "log", "-1", "--format=%an|%ae|%cn|%ce")).Trim().Should().Be("test|test@example.invalid|model-arms|model-arms@model-arms.invalid", "the author stays the source's, the committer is the harness's whatever user.* the clone's config holds");
        (await In(clone).CherryPickAsync("zz", Ct)).Failed().Should().BeTrue();
        start.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Cherry_pick_abort_clears_a_real_conflict_and_fails_when_nothing_is_in_progress()
    {
        var clone = await ClonedAsync();
        await TargetBench.GitAsync(clone, "branch", "side");
        await CommitInAsync(clone, "a.txt", "arm\n", "the arm");
        var armHead = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        await TargetBench.GitAsync(clone, "checkout", "-q", "side");
        var source = await CommitInAsync(clone, "a.txt", "side\n", "the side");
        await TargetBench.GitAsync(clone, "checkout", "-q", "-");
        var git = In(clone);
        (await git.CherryPickAbortAsync(Ct)).Failed().Should().BeTrue("nothing in progress: git's 128");

        var conflict = await git.CherryPickAsync(source, Ct);

        conflict.Failed().Should().BeTrue();
        conflict.Reason().Should().Contain("CONFLICT", "the whole output, the CONFLICT line (stdout) included");
        File.Exists(Path.Combine(clone, ".git", "CHERRY_PICK_HEAD")).Should().BeTrue();
        (await git.CherryPickAbortAsync(Ct)).Ok();
        File.Exists(Path.Combine(clone, ".git", "CHERRY_PICK_HEAD")).Should().BeFalse();
        (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim().Should().Be(armHead);
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_merge_keys_the_clone_names_are_emptied_on_the_cherry_pick_only_and_merge_default_among_them()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the recording shim is a shell script");
        var clone = await ClonedAsync();
        AppendConfig(Path.Combine(clone, ".git"), "[merge \"m\"]\n\tdriver = /bin/true\n\tname = a name, not a driver\n[merge]\n\tdefault = m\n");
        var head = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        var marker = _temp.Combine("calls.log");
        var git = In(clone, Sentinel.RecordingShim(_temp, marker));

        _ = await git.HeadAsync(Ct);
        _ = await git.CherryPickAsync(head, Ct);

        var blocks = Sentinel.Blocks(marker).Where(block => !IsEnumeration(block.Call)).ToList();
        var plain = blocks.Single(block => block.Call.EndsWith(" rev-parse HEAD", StringComparison.Ordinal));
        var pick = blocks.Single(block => block.Call.Contains(" cherry-pick ", StringComparison.Ordinal));
        plain.Env.Should().NotContain(line => line.Contains("merge.", StringComparison.Ordinal), "only the cherry-pick merges");
        var drivers = (await HostKeysAsync(clone, @"^filter\.")).Count;
        pick.Env.Should().Contain($"GIT_CONFIG_COUNT={(drivers * 4) + 2}");
        pick.Env.Should().Contain($"GIT_CONFIG_KEY_{drivers * 4}=merge.m.driver").And.Contain($"GIT_CONFIG_VALUE_{drivers * 4}=");
        pick.Env.Should().Contain($"GIT_CONFIG_KEY_{(drivers * 4) + 1}=merge.default").And.Contain($"GIT_CONFIG_VALUE_{(drivers * 4) + 1}=");
        pick.Env.Should().NotContain(line => line.Contains("merge.m.name", StringComparison.Ordinal), "a name is no program");
        pick.Env.Should().Contain(["GIT_COMMITTER_NAME=model-arms", "GIT_COMMITTER_EMAIL=model-arms@model-arms.invalid"]);
        plain.Env.Should().NotContain(line => line.StartsWith("GIT_COMMITTER", StringComparison.Ordinal));
    }

    private async Task<IReadOnlyList<string>> HostKeysAsync(string folder, string pattern)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["config", "--name-only", "-z", "--get-regexp", pattern], folder, TimeSpan.FromSeconds(60), string.Empty, GitEnvironment.Of(Environment), Ct);
        var done = attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>(attempt.Describe).Subject;
        return [.. done.Result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(name => name[7..name.LastIndexOf('.')]).Distinct(StringComparer.Ordinal)];
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_merge_enumeration_lists_an_invented_name_holding_an_equals_sign_verbatim()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the recording shim is a shell script");
        var clone = await ClonedAsync();
        await TargetBench.GitAsync(clone, "config", "--", "merge.a=b.driver", "/bin/true");
        var listed = await TargetBench.GitAsync(clone, "config", "--name-only", "-z", "--get-regexp", @"^merge\.");
        listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Should().Contain("merge.a=b.driver", "the live proof: git lists the invented name with its '='");
        var head = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        var marker = _temp.Combine("calls.log");

        _ = await In(clone, Sentinel.RecordingShim(_temp, marker)).CherryPickAsync(head, Ct);

        var pick = Sentinel.Blocks(marker).Single(block => block.Call.Contains(" cherry-pick ", StringComparison.Ordinal));
        var drivers = (await HostKeysAsync(clone, @"^filter\.")).Count;
        pick.Env.Should().Contain($"GIT_CONFIG_KEY_{drivers * 4}=merge.a=b.driver", "the key is verbatim in its own variable: the '=' splits nothing").And.Contain($"GIT_CONFIG_VALUE_{drivers * 4}=");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_arms_submodule_update_allows_the_file_transport_alone_and_an_ext_url_is_refused()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "plants are shell scripts");
        var (clone, _, _) = await WithSubmoduleAsync();
        var plain = Worktree(clone, "wt-plain");
        await TargetBench.GitAsync(clone, "worktree", "add", "-q", plain, "-b", "tmp/plain", "HEAD");

        (await In(plain).ArmSubmoduleUpdateAsync("sub", Ct)).Ok();

        File.Exists(Path.Combine(plain, "sub", "s.txt")).Should().BeTrue("the file transport is allowed for the arm's own checkout, with no -c protocol.file.allow=always");
        var hostile = Worktree(clone, "wt-hostile");
        await TargetBench.GitAsync(clone, "worktree", "add", "-q", hostile, "-b", "tmp/hostile", "HEAD");
        var program = PlantProgram("ext-transport", "exit 1");
        await TargetBench.GitAsync(clone, "config", "submodule.sub.url", $"ext::{program} %S");
        Ran.Should().BeFalse();
        await HostGitAsync(hostile, "-c", "protocol.ext.allow=always", "submodule", "update", "--init", "-q", "--", "sub");
        Ran.Should().BeTrue("the plant is live: the host's git, told to allow ext, ran the program the submodule's url names (an insteadOf did not rewrite a submodule's clone url on 2.53: the url itself is the model-writable lever)");
        File.Delete(SentinelFile);
        await TargetBench.GitAsync(clone, "worktree", "remove", "--force", "--force", hostile);
        await TargetBench.GitAsync(clone, "worktree", "add", "-q", hostile, "-b", "tmp/hostile2", "HEAD");

        var refused = await In(hostile).ArmSubmoduleUpdateAsync("sub", Ct);

        refused.Failed().Should().BeTrue();
        refused.Reason().Should().Contain("not allowed");
        Ran.Should().BeFalse("GIT_ALLOW_PROTOCOL=file refuses ext:: whatever the clone's config says");
    }

    [Fact]
    public async Task Left_behind_counts_untracked_files_but_not_the_excluded_links()
    {
        var clone = await ClonedAsync();
        var git = In(clone);
        (await git.LeftBehindAsync(["node_modules"], Ct)).Ok().Should().Be(0);
        Directory.CreateDirectory(Path.Combine(clone, "node_modules"));
        File.WriteAllText(Path.Combine(clone, "node_modules", "x.js"), "x\n");
        Directory.CreateDirectory(Path.Combine(clone, "deep", "node_modules"));
        File.WriteAllText(Path.Combine(clone, "deep", "node_modules", "y.js"), "y\n");
        File.WriteAllText(Path.Combine(clone, "left.txt"), "left\n");
        File.WriteAllText(Path.Combine(clone, "a.txt"), "edited\n");

        (await git.LeftBehindAsync(["node_modules", "deep/node_modules"], Ct)).Ok().Should().Be(2, "left.txt and the edit; both node_modules folders are excluded");
        (await git.LeftBehindAsync([], Ct)).Ok().Should().Be(4);
    }

    [Fact]
    public async Task Current_branch_is_empty_when_detached()
    {
        var clone = await ClonedAsync();
        var git = In(clone);
        (await git.CurrentBranchAsync(Ct)).Ok().Should().Be("plan");
        await TargetBench.GitAsync(clone, "checkout", "-q", "--detach");
        (await git.CurrentBranchAsync(Ct)).Ok().Should().BeEmpty();
    }

    [Fact]
    public async Task Filter_drivers_names_what_the_enumeration_neutralises()
    {
        var clone = await ClonedAsync();
        var git = In(clone);
        var before = (await git.FilterDriversAsync(Ct)).Ok();
        AppendConfig(Path.Combine(clone, ".git"), "[filter \"lfs\"]\n\tclean = x\n[filter \"a=b\"]\n\tsmudge = y\n");

        var after = (await git.FilterDriversAsync(Ct)).Ok();

        after.Except(before).Should().BeEquivalentTo(["lfs", "a=b"]);
    }

    [Fact]
    public async Task Worktree_list_names_each_registration_its_branch_and_whether_it_is_prunable()
    {
        var clone = await ClonedAsync();
        var live = Worktree(clone, "wt-live");
        var detached = Worktree(clone, "wt-detached");
        var gone = Worktree(clone, "wt-gone");
        await TargetBench.GitAsync(clone, "worktree", "add", "-q", live, "-b", "tmp/live", "HEAD");
        await TargetBench.GitAsync(clone, "worktree", "add", "-q", detached, "-b", "tmp/detached", "HEAD");
        await TargetBench.GitAsync(detached, "checkout", "-q", "--detach");
        await TargetBench.GitAsync(clone, "worktree", "add", "-q", gone, "-b", "tmp/gone", "HEAD");
        Directory.Delete(gone, recursive: true);

        var entries = (await In(clone).WorktreeListAsync(Ct)).Ok();

        entries.Should().HaveCount(4);
        entries.Single(entry => entry.Path == live).Should().Be(new WorktreeEntry(live, "refs/heads/tmp/live", false, false));
        entries.Single(entry => entry.Path == detached).Should().Be(new WorktreeEntry(detached, string.Empty, true, false));
        entries.Single(entry => entry.Path == gone).Should().Be(new WorktreeEntry(gone, "refs/heads/tmp/gone", false, true), "a prunable entry still names its branch (M-t)");
    }

    [Fact]
    public async Task Commit_present_is_empty_for_a_sha_the_clone_does_not_know_and_is_ancestor_answers_descent()
    {
        var clone = await ClonedAsync();
        var start = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        await TargetBench.GitAsync(clone, "branch", "tmp/t1");
        await TargetBench.GitAsync(clone, "checkout", "-q", "tmp/t1");
        var tip = await CommitInAsync(clone, "t.txt", "t\n", "t");
        await TargetBench.GitAsync(clone, "checkout", "-q", "plan");
        var other = await CommitInAsync(clone, "o.txt", "o\n", "o");
        var git = In(clone);

        (await git.CommitPresentAsync(tip, Ct)).Ok().Should().Be(tip);
        (await git.CommitPresentAsync(new string('a', 40), Ct)).Ok().Should().BeEmpty("a sha the clone does not know is no failure");
        (await git.CommitPresentAsync("--output=x", Ct)).Failed().Should().BeTrue();
        (await git.IsAncestorAsync(start, "tmp/t1", Ct)).Ok().Should().BeTrue();
        (await git.IsAncestorAsync(other, "tmp/t1", Ct)).Ok().Should().BeFalse("a commit the tip does not descend from is a 'no', not a failure");
        (await git.IsAncestorAsync(new string('a', 40), "tmp/t1", Ct)).Failed().Should().BeTrue("an unknown sha is git's 128, a failure: the caller asks CommitPresent first");
        (await git.IsAncestorAsync(start, "-x", Ct)).Failed().Should().BeTrue();
    }

    /// <summary>One row per new question: each is started by the path it was handed with the three pins first, and its <c>GIT_*</c> are the sealed ones plus the host's own filter neutralisation — the arm's submodule update beside <c>GIT_ALLOW_PROTOCOL=file</c>, the cherry-pick beside the committer and the merge pairs.</summary>
    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Every_task_question_is_started_hardened_by_the_path_it_was_handed_with_the_sealed_environment()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the recording shim is a shell script");
        var (clone, _, _) = await WithSubmoduleAsync();
        var head = (await TargetBench.GitAsync(clone, "rev-parse", "HEAD")).Trim();
        var wt = Worktree(clone, "wt-hardened");
        var marker = _temp.Combine("calls.log");
        var parent = new Dictionary<string, string>(Environment, StringComparer.Ordinal) { ["GIT_DIR"] = "/nonexistent", ["PGPASSWORD"] = "hunter2" };
        var git = new ArmGit(Sentinel.RecordingShim(_temp, marker), clone, parent);

        _ = await git.WorktreeAddAsync(wt, "tmp/h", Ct);
        _ = await new ArmGit(Sentinel.RecordingShim(_temp, marker), wt, parent).ArmSubmoduleUpdateAsync("sub", Ct);
        _ = await git.WorktreeAttachAsync(wt, "tmp/h", Ct);
        _ = await git.BranchTipAsync("tmp/h", Ct);
        _ = await git.CurrentBranchAsync(Ct);
        _ = await git.RevListAsync(head, Ct);
        _ = await git.RevListAsync(head, "tmp/h", Ct);
        _ = await git.PatchIdsAsync([head], Ct);
        _ = await git.CherryPickAsync(head, Ct);
        _ = await git.CherryPickAbortAsync(Ct);
        _ = await git.LeftBehindAsync(["sub"], Ct);
        _ = await git.FilterDriversAsync(Ct);
        _ = await git.WorktreeListAsync(Ct);
        _ = await git.CommitPresentAsync(head, Ct);
        _ = await git.IsAncestorAsync(head, "tmp/h", Ct);
        _ = await git.WorktreeRemoveAsync(wt, Ct);
        _ = await git.WorktreePruneAsync(Ct);
        _ = await git.DeleteBranchAsync("tmp/h", Ct);

        var blocks = Sentinel.Blocks(marker);
        blocks.Should().HaveCountGreaterThan(18, "an assertion over an empty recording is green");
        blocks.Should().OnlyContain(block => block.Call.StartsWith("-c core.fsmonitor=false -c core.hooksPath= -c submodule.recurse=false ", StringComparison.Ordinal));
        var filters = Neutralisation(await HostDriversAsync(clone, Environment));
        foreach (var block in blocks)
        {
            var expected = IsEnumeration(block.Call) ? new[] { "GIT_ALLOW_PROTOCOL=", "GIT_NO_LAZY_FETCH=1" }
                : block.Call.Contains(" submodule update ", StringComparison.Ordinal) ? ["GIT_ALLOW_PROTOCOL=file", "GIT_NO_LAZY_FETCH=1", .. filters]
                : block.Call.Contains(" cherry-pick --no-edit", StringComparison.Ordinal) ? ["GIT_ALLOW_PROTOCOL=", "GIT_NO_LAZY_FETCH=1", "GIT_COMMITTER_NAME=model-arms", "GIT_COMMITTER_EMAIL=model-arms@model-arms.invalid", .. filters]
                : ["GIT_ALLOW_PROTOCOL=", "GIT_NO_LAZY_FETCH=1", .. filters];
            block.Env.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal)).Should().BeEquivalentTo(expected, block.Call);
            block.Env.Should().NotContain(line => line.StartsWith("PGPASSWORD=", StringComparison.Ordinal), block.Call);
        }
    }
}
