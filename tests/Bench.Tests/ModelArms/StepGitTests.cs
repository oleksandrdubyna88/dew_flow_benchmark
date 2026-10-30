using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S4 — the git the step launches (D17 d, E2 §8 item 25): the real path it was handed, a <c>GIT_*</c>-free environment and the arm's clone as the working directory; and the derived outputs it
/// writes (E2 §8 item 52). Real git, real folders.
/// <para>
/// What this does NOT prove: that a git other than the host's answers the same; that an environment variable git reads without a <c>GIT_</c> prefix (<c>HOME</c>'s global config) cannot redirect it — <c>HOME</c> passes as judged, D17.
/// </para></summary>
public sealed class StepGitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_ambient_GIT_DIR_in_the_parent_environment_does_not_change_which_head_the_before_snapshot_reads()
    {
        using var bench = new StepBench();
        var other = await bench.NewRepositoryAsync("other");
        var otherHead = await bench.HeadAsync(other);
        var cloneHead = await bench.HeadAsync();
        otherHead.Should().NotBe(cloneHead, "the two repositories must differ, or the test cannot tell them apart");
        bench.Parent["GIT_DIR"] = Path.Combine(other, ".git");
        bench.Parent["GIT_WORK_TREE"] = other;
        bench.Replays("result-success.jsonl");

        var outcome = await bench.Runner().RunAsync(bench.Context("CR", 1), Ct);

        outcome.IsDone().End.Should().BeOfType<StepEnd.Finished>();
        File.Exists(bench.Paths.Before("2-hreview")).Should().BeTrue("the snapshot is written before the first attempt");
        File.ReadAllText(bench.Paths.Before("2-hreview")).Trim().Should().Be(cloneHead, "the clone's HEAD, never the other repository's");
        File.ReadAllText(bench.Paths.Diff("2-hreview")).Should().BeEmpty();
        bench.Fake.Calls().Single().EnvironmentNames.Should().Contain("GIT_DIR", "the arm's claude gets its environment untouched (D19): the harness's git does not");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_step_launches_the_git_it_was_handed_by_its_real_path_and_no_other()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the shim is a shell script");
        using var bench = new StepBench();
        var marker = bench.Combine("markers", "git");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        bench.Git = bench.ShimScript(bench.Combine("bin", "git"), $"echo \"$0 $*\" >> '{marker}'\nenv | grep '^GIT_' >> '{marker}'\nexec '{ExperimentRoot.RealGit}' \"$@\"\n");
        bench.Parent["GIT_DIR"] = "/nowhere";
        bench.Replays("result-success.jsonl");

        var outcome = await bench.Runner().RunAsync(bench.Context("CR", 1), Ct);

        outcome.IsDone();
        var runs = File.Exists(marker) ? File.ReadAllLines(marker) : [];
        runs.Count(line => line.StartsWith(bench.Git + " ", StringComparison.Ordinal)).Should().BeGreaterThanOrEqualTo(3, "the shim should record at least three git runs: the .before, the heads and the diff");
        var head = File.ReadAllText(bench.Paths.Before("2-hreview")).Trim();
        runs.Should().Contain(bench.Git + " rev-parse HEAD").And.Contain(bench.Git + " rev-parse --short HEAD").And.Contain(bench.Git + $" diff {head} HEAD");
        runs.Should().NotContain(line => line.StartsWith("GIT_", StringComparison.Ordinal), "git runs without a GIT_* variable of the parent's");
        head.Should().Be(await bench.HeadAsync());
    }

    [Fact]
    public async Task A_git_that_cannot_answer_stops_the_before_snapshot_resumable_and_launches_nothing()
    {
        using var bench = new StepBench();
        bench.Git = bench.Combine("no-such-git");
        bench.Replays("result-success.jsonl");

        var outcome = await bench.Runner().RunAsync(bench.Context("CR", 1), Ct);

        outcome.IsStopped(5).Why.Should().Contain("2-hreview.before");
        File.Exists(bench.Paths.Before("2-hreview")).Should().BeFalse("a snapshot with no commit in it is no snapshot");
        bench.Fake.Calls().Should().BeEmpty();
        bench.LastStopped().Exit.Should().Be(5);
    }

    [Fact]
    public async Task A_head_git_cannot_read_is_recorded_as_empty_and_logged_and_the_step_goes_on()
    {
        using var bench = new StepBench();
        bench.Git = bench.Combine("no-such-git");
        bench.Replays("result-success.jsonl");

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        outcome.IsDone().End.Should().BeOfType<StepEnd.Finished>("a step with no snapshot does not need git to run");
        bench.Timing().OfType<TimingLine.Start>().Should().ContainSingle().Which.Head.Should().BeEmpty();
        bench.Log.ToString().Should().Contain("could not read HEAD");
    }

    [Fact]
    public async Task A_diff_git_cannot_make_stops_the_step_resumable_with_no_done_marker()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        Directory.CreateDirectory(bench.Paths.Out);
        await File.WriteAllTextAsync(bench.Paths.Before("2-hreview"), new string('0', 40) + "\n", Ct);

        var outcome = await bench.Runner().RunAsync(bench.Context("CR", 1), Ct);

        outcome.IsStopped(5).Why.Should().Contain("2-hreview");
        File.Exists(bench.Paths.Done("2-hreview")).Should().BeFalse("a step whose diff could not be made is not done");
        File.Exists(bench.Paths.Diff("2-hreview")).Should().BeFalse();
    }

    [Fact]
    public async Task A_review_whose_last_result_is_a_limit_stops_resumable_and_writes_no_file()
    {
        using var bench = new StepBench();
        var context = bench.Context("C", 1);
        bench.Paths.Out.Should().NotBeNull();
        Directory.CreateDirectory(bench.Paths.Out);
        File.Copy(StepBench.Replay("result-429-usage-limit-reached.jsonl"), bench.Paths.Stream("2-review", 1));
        var files = new ArmFiles(bench.Paths);
        var git = new ArmGit(bench.Git, bench.Paths.CloneFolder, bench.Parent);

        var outcome = await DerivedOutputs.WriteAsync(context, [DerivedOutput.Verdict], files, git, Ct);

        outcome.Should().BeOfType<Outcome<IReadOnlyList<DerivedOutput>>.Fail>().Which.Reason.Should().Contain("2-review", "the reason names the step").And.Contain("no review.json is written");
        File.Exists(bench.NotesFile("notes/review.json")).Should().BeFalse("a limit is never mistaken for a review that found nothing");
    }

    [Fact]
    public async Task A_review_cut_by_its_wall_without_a_result_stops_resumable_and_writes_no_file()
    {
        using var bench = new StepBench();
        bench.SpendWall("2-review", 59);
        bench.Replays("stream-no-result.jsonl", new System.Text.Json.Nodes.JsonObject { ["holdAfterLine"] = 1 });

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 1), Ct);

        outcome.IsStopped(5).Why.Should().Contain("no verdict to read");
        File.Exists(bench.Paths.Done("2-review")).Should().BeFalse("D21 over D8: a review with no result is resumable, not done with a hole");
        File.Exists(bench.NotesFile("notes/review.json")).Should().BeFalse();
        bench.LastStopped().Exit.Should().Be(5);
    }

    [Fact]
    public async Task The_coder_outputs_of_the_task_pipeline_are_not_written_here()
    {
        using var bench = new StepBench();
        var context = bench.Context("C", 0);
        var files = new ArmFiles(bench.Paths);
        var git = new ArmGit(bench.Git, bench.Paths.CloneFolder, bench.Parent);

        var outcome = await DerivedOutputs.WriteAsync(context, [DerivedOutput.CoderHead, DerivedOutput.CoderDiff], files, git, Ct);

        outcome.Should().BeOfType<Outcome<IReadOnlyList<DerivedOutput>>.Fail>().Which.Reason.Should().Contain("task outputs are written by the task pipeline (E4.S3)");
    }
}
