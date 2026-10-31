using System.Text.RegularExpressions;
using Bench.Cli;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed partial class TaskWorktreesTests
{
    private const string AllPins = "-c core.fsmonitor=false -c core.hooksPath= -c submodule.recurse=false ";

    /// <summary>The whole life of a task through the recording git, under a parent environment that carries what must not reach git: Enter → a commit → PickBack → Drop.</summary>
    private static async Task<(WorktreeKit Kit, IReadOnlyList<(string Call, string Cwd, IReadOnlyList<string> Env)> Blocks)> RecordedFlowAsync()
    {
        var kit = await KitAsync();
        try
        {
            var environment = new Dictionary<string, string>(kit.Root.Environment, StringComparer.Ordinal)
            {
                ["GIT_DIR"] = "/nonexistent",
                ["GITHUB_TOKEN"] = "ghp_secret",
                ["PGPASSWORD"] = "hunter2",
                ["SSH_AUTH_SOCK"] = "/tmp/agent.sock",
            };
            var sut = kit.Sut(kit.Recording(environment));
            await kit.ReadyAsync(sut, "t1", "t2");
            await kit.BeforeAsync("t1");
            await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
            kit.Set("t1", new StepEntry.Skip());
            (await sut.PickBackAsync("t1", Ct)).Should().BeNull();
            (await sut.DropAsync("t1", Ct)).Should().BeNull();
            return (kit, Sentinel.Blocks(kit.Marker));
        }
        catch
        {
            kit.Dispose();
            throw;
        }
    }

    /// <summary>The <c>GIT_CONFIG_*</c> lines a neutralised question must carry on THIS host: four filter pairs per driver git reads, then (for a merging question) one empty pair per merge driver key and for <c>merge.default</c> — derived from the host's own enumeration, never copied from the code under test.</summary>
    private static async Task<IReadOnlyList<string>> HostPairsAsync(WorktreeKit kit, bool merges)
    {
        async Task<string[]> Names(string pattern)
        {
            var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["config", "--name-only", "-z", "--get-regexp", pattern], kit.Clone, TimeSpan.FromSeconds(60), string.Empty, GitEnvironment.Of(kit.Root.Environment), Ct);
            var done = attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>(attempt.Describe).Subject;
            return [.. done.Result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries)];
        }

        string[] variables = ["clean", "smudge", "process", "required"];
        string[] values = ["", "", "", "false"];
        var pairs = (await Names(@"^filter\.")).Select(name => name[7..name.LastIndexOf('.')]).Distinct(StringComparer.Ordinal)
            .SelectMany(driver => variables.Select((variable, index) => ($"filter.{driver}.{variable}", values[index]))).ToList();
        if (merges)
        {
            pairs.AddRange((await Names(@"^merge\.")).Where(name => name == "merge.default" || name.EndsWith(".driver", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Select(name => (name, string.Empty)));
        }

        return pairs.Count == 0 ? [] : [$"GIT_CONFIG_COUNT={pairs.Count}", .. pairs.SelectMany((pair, index) => new[] { $"GIT_CONFIG_KEY_{index}={pair.Item1}", $"GIT_CONFIG_VALUE_{index}={pair.Item2}" })];
    }

    [Fact]
    public async Task Every_git_of_the_task_pipeline_is_the_resolved_one_hardened_with_no_inherited_GIT_variable_but_the_harness_guards()
    {
        var (kit, blocks) = await RecordedFlowAsync();
        using var _ = kit;

        blocks.Should().HaveCountGreaterThan(12, "an AllSatisfy over an empty recording is green");
        var calls = blocks.Select(block => block.Call.StartsWith(AllPins, StringComparison.Ordinal) ? block.Call[AllPins.Length..] : block.Call).ToList();
        AssertInOrder(
            [.. calls.Where(call => !WorktreeKit.IsEnumeration(call))],
            "worktree list --porcelain",
            "rev-parse --verify -q refs/heads/tmp/t1",
            $"worktree add -q {kit.Wt("t1")} -b tmp/t1 HEAD",
            "submodule update --init --checkout -q -- .agents/conventions",
            "rev-parse HEAD",
            "-c commit.gpgsign=false cherry-pick --no-edit --allow-empty --keep-redundant-commits",
            "rev-parse HEAD",
            $"worktree remove --force --force {kit.Wt("t1")}",
            "worktree prune",
            "branch -D tmp/t1");
        blocks.Should().OnlyContain(block => block.Call.StartsWith(AllPins, StringComparison.Ordinal), "every question is hardened");
        var filters = await HostPairsAsync(kit, merges: false);
        var withMerges = await HostPairsAsync(kit, merges: true);
        foreach (var (call, _, env) in blocks)
        {
            var git = env.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal)).ToList();
            IEnumerable<string> expected;
            if (WorktreeKit.IsEnumeration(call.Replace(AllPins, string.Empty, StringComparison.Ordinal)))
            {
                expected = ["GIT_ALLOW_PROTOCOL=", "GIT_NO_LAZY_FETCH=1"];
            }
            else if (call.Contains(" submodule update ", StringComparison.Ordinal))
            {
                expected = ["GIT_ALLOW_PROTOCOL=file", "GIT_NO_LAZY_FETCH=1", .. filters];
            }
            else if (call.Contains(" cherry-pick --no-edit", StringComparison.Ordinal))
            {
                expected = ["GIT_ALLOW_PROTOCOL=", "GIT_NO_LAZY_FETCH=1", "GIT_COMMITTER_NAME=model-arms", "GIT_COMMITTER_EMAIL=model-arms@model-arms.invalid", .. withMerges];
            }
            else
            {
                expected = ["GIT_ALLOW_PROTOCOL=", "GIT_NO_LAZY_FETCH=1", .. filters];
            }

            git.Should().BeEquivalentTo(expected, call);
            env.Should().NotContain(line => line.StartsWith("GIT_DIR=", StringComparison.Ordinal) || line.StartsWith("GITHUB_TOKEN=", StringComparison.Ordinal) || line.StartsWith("PGPASSWORD=", StringComparison.Ordinal) || line.StartsWith("SSH_AUTH_SOCK=", StringComparison.Ordinal), call);
        }

        calls.Should().Contain(call => call.StartsWith("-c commit.gpgsign=false cherry-pick --no-edit --allow-empty --keep-redundant-commits ", StringComparison.Ordinal), "the editor rows' only break-it is the argv (M-j): --no-edit stays");
    }

    [Fact]
    public async Task The_arms_clone_never_reaches_the_base_forms_of_detach_and_submodule_update()
    {
        var root = Cli.Repository.Root;
        var adapter = File.ReadAllText(Path.Combine(root, "tools", "model-arms", "ModelArms", "Adapters", "TaskWorktrees.cs"));
        var baseForms = File.ReadAllText(Path.Combine(root, "tools", "model-arms", "ModelArms", "Adapters", "BaseCheckout.cs"));
        var detach = new Regex(@"(?<![A-Za-z])DetachAsync\(", RegexOptions.CultureInvariant);
        var update = new Regex(@"(?<![A-Za-z])SubmoduleUpdateAsync\(", RegexOptions.CultureInvariant);
        detach.IsMatch(baseForms).Should().BeTrue("the scan is alive: the base's checkout calls both");
        update.IsMatch(baseForms).Should().BeTrue();
        detach.IsMatch(adapter).Should().BeFalse("nothing in E4 moves the arm clone's HEAD by a checkout");
        update.IsMatch(adapter).Should().BeFalse("the base's submodule update has no allow-list narrower than 'any' and no --checkout");

        var (kit, blocks) = await RecordedFlowAsync();
        using var _ = kit;
        var updates = blocks.Where(block => block.Call.Contains(" submodule update ", StringComparison.Ordinal)).ToList();
        updates.Should().HaveCount(2, "one per worktree — the count first, so the clause cannot pass over none");
        updates.Should().OnlyContain(block => block.Call.Contains(" --checkout ", StringComparison.Ordinal) && block.Env.Contains("GIT_ALLOW_PROTOCOL=file"));
        blocks.Should().NotContain(block => block.Call.Contains(" --detach ", StringComparison.Ordinal));
        blocks.Should().NotContain(block => block.Call.Contains("protocol.file.allow", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("entry")]
    [InlineData("pick")]
    [InlineData("stranded")]
    public async Task A_stale_index_lock_stops_the_pipeline_with_exit_3_and_is_never_removed(string where)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        var lockFile = Path.Combine(kit.Clone, ".git", "index.lock");
        if (where == "entry")
        {
            await kit.ConflictInCloneAsync(two: false);
            await File.WriteAllTextAsync(lockFile, string.Empty, Ct);
            kit.ForgetCalls();

            var entry = await sut.EnterAsync(["t1", "t2"], kit.Recover, Ct);

            var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
            stopped.ExitCode.Should().Be(3);
            stopped.Why.Should().Contain(lockFile).And.Contain("remove it once none does");
            File.Exists(kit.Marker).Should().BeFalse("not one git call was made");
            File.Exists(lockFile).Should().BeTrue();
        }
        else if (where == "pick")
        {
            await DoneWithCommitsAsync(kit, sut, "c1.txt");
            await File.WriteAllTextAsync(lockFile, string.Empty, Ct);
            kit.ForgetCalls();

            var stopped = await sut.PickBackAsync("t1", Ct);

            stopped.Should().NotBeNull();
            stopped!.ExitCode.Should().Be(3);
            stopped.Why.Should().Contain(lockFile);
            kit.Calls().Should().NotContain(call => call.Contains("cherry-pick", StringComparison.Ordinal));
            File.Exists(lockFile).Should().BeTrue();
        }
        else
        {
            await kit.ReadyAsync(sut, "t1", "t2");
            await File.WriteAllTextAsync(Path.Combine(kit.Clone, ".git", "worktrees", "t1", "index.lock"), string.Empty, Ct);

            await kit.ReadyAsync(sut, "t1", "t2");

            File.Exists(Path.Combine(kit.Wt("t1"), ".git")).Should().BeTrue("a worktree's own lock blocks nothing the pipeline does (M-d)");
        }
    }
}
