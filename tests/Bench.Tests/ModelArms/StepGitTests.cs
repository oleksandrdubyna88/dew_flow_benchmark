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
        bench.Git = bench.ShimScript(bench.Combine("bin", "git"), $"echo \"$0 $*\" >> '{marker}'\nenv | grep -E '^(GIT_|GITHUB_TOKEN)' >> '{marker}'\nexec '{ExperimentRoot.RealGit}' \"$@\"\n");
        bench.Parent["GIT_DIR"] = "/nowhere";
        bench.Parent["GITHUB_TOKEN"] = "ghp_value";
        bench.Replays("result-success.jsonl");

        var outcome = await bench.Runner().RunAsync(bench.Context("CR", 1), Ct);

        outcome.IsDone();
        var runs = File.Exists(marker) ? File.ReadAllLines(marker) : [];
        runs.Count(line => line.StartsWith(bench.Git + " ", StringComparison.Ordinal)).Should().BeGreaterThanOrEqualTo(3, "the shim should record at least three git runs: the .before, the heads and the diff");
        var head = File.ReadAllText(bench.Paths.Before("2-hreview")).Trim();
        const string Hardened = " -c core.fsmonitor=false -c core.hooksPath= -c submodule.recurse=false";
        runs.Should().Contain(bench.Git + Hardened + " rev-parse HEAD").And.Contain(bench.Git + Hardened + " rev-parse --short HEAD")
            .And.Contain(bench.Git + Hardened + $" -c diff.external= diff --no-ext-diff --no-textconv --submodule=short {head} HEAD");
        var gitNames = runs.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal)).Select(line => line.Split('=')[0]).Distinct().ToList();
        gitNames.Should().Contain("GIT_NO_LAZY_FETCH", "git runs with the harness's own switch (no lazy fetch from a promisor remote)").And.Contain("GIT_ALLOW_PROTOCOL", "and with the allow-list that refuses every transport (set, empty)");
        gitNames.Except(["GIT_NO_LAZY_FETCH", "GIT_ALLOW_PROTOCOL"]).Where(name => !System.Text.RegularExpressions.Regex.IsMatch(name, @"^GIT_CONFIG_(COUNT|KEY_\d+|VALUE_\d+)$")).Should().BeEmpty("no GIT_* of the parent's reaches git; the rest are the harness's own driver neutralisation");
        runs.Where(line => line.StartsWith("GITHUB_TOKEN", StringComparison.Ordinal)).Should().BeEmpty("no secret of the parent's reaches a program git might still run: the class reduces the environment itself");
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

        var again = await bench.Runner().RunAsync(bench.Context("CR", 1), Ct);

        again.IsStopped(5);
        bench.Fake.Calls().Should().HaveCount(1, "a relaunch does not cure a snapshot git cannot diff from: that stop is permanent until the operator acts");
    }

    /// <summary>The model under test writes the clone, including its <c>.git/config</c> and <c>.gitattributes</c>: a program they name must not run in the harness's git, and the measured <c>.diff</c> must be the plain patch.</summary>
    [Theory]
    [InlineData("diff.external")]
    [InlineData("textconv")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_diff_is_made_without_running_a_program_the_clone_names(string plant)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the planted programs are shell scripts");
        using var bench = new StepBench();
        var marker = bench.Combine("markers", "planted");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        var external = bench.ShimScript(bench.Combine("bin", "ext"), $"echo ext >> '{marker}'\necho PLANTED\n");
        var convert = bench.ShimScript(bench.Combine("bin", "conv"), $"echo conv >> '{marker}'\necho PLANTED-TEXTCONV\n");
        await bench.CommitFileAsync(".gitattributes", "* diff=planted\n", "attributes");
        var config = plant == "textconv" ? $"[diff \"planted\"]\n\ttextconv = {convert}\n" : $"[diff]\n\texternal = {external}\n";
        await File.AppendAllTextAsync(Path.Combine(bench.Paths.CloneFolder, ".git", "config"), config, Ct);
        bench.Replays("result-success.jsonl");
        var work = new ProbeAt(StepPoint.Launched, () => bench.CommitFileAsync("work.txt", "hello\n", "the arm's work").GetAwaiter().GetResult());

        var outcome = await bench.Runner(work).RunAsync(bench.Context("CR", 1), Ct);

        outcome.IsDone();
        (File.Exists(marker) ? File.ReadAllText(marker) : string.Empty).Should().BeEmpty("a program the clone's config or attributes name never ran in the harness's git");
        var diff = File.ReadAllText(bench.Paths.Diff("2-hreview"));
        diff.Should().Contain("+hello").And.NotContain("PLANTED", "the .diff is the plain patch");
    }

    /// <summary>A partial clone names a promisor remote and a program to run for it (<c>remote.origin.uploadpack</c>); a blob the model deleted makes git fetch it lazily — and run that program — unless lazy fetching is switched off.</summary>
    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_diff_over_a_missing_blob_runs_no_program_the_clones_promisor_remote_names_and_stops_resumable_without_a_done_marker()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the planted program is a shell script");
        using var bench = new StepBench();
        var marker = bench.Combine("markers", "promisor");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        var upload = bench.ShimScript(bench.Combine("bin", "upload"), $"echo fetched >> '{marker}'\nexit 1\n");
        bench.Replays("result-success.jsonl");
        var plant = new ProbeAt(StepPoint.Launched, () => PlantMissingBlobAsync(bench, upload).GetAwaiter().GetResult());

        var outcome = await bench.Runner(plant).RunAsync(bench.Context("CR", 1), Ct);

        (File.Exists(marker) ? File.ReadAllText(marker) : string.Empty).Should().BeEmpty("git did not fetch the blob, so it ran no program the clone's config names");
        outcome.IsStopped(5).Why.Should().Contain("2-hreview");
        File.Exists(bench.Paths.Done("2-hreview")).Should().BeFalse();
        File.Exists(bench.Paths.Diff("2-hreview")).Should().BeFalse();
    }

    private static async Task PlantMissingBlobAsync(StepBench bench, string upload)
    {
        await bench.CommitFileAsync("work.txt", "hello\n", "the arm's work");
        var blob = await bench.RevParseAsync("HEAD:work.txt");
        File.Delete(Path.Combine(bench.Paths.CloneFolder, ".git", "objects", blob[..2], blob[2..]));
        await File.AppendAllTextAsync(
            Path.Combine(bench.Paths.CloneFolder, ".git", "config"),
            $"[core]\n\trepositoryformatversion = 1\n[extensions]\n\tpartialClone = origin\n[remote \"origin\"]\n\turl = {bench.Paths.CloneFolder}\n\tpromisor = true\n\tuploadpack = {upload}\n");
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

        // the stop is resumable: the next entry LAUNCHES — the session resumed as attempt 2 at the floor budget — and the verdict is written from its result
        bench.Replays("result-success.jsonl");
        var again = await bench.Runner().RunAsync(bench.Context("C", 1), Ct);

        again.IsDone().Should().Match<StepOutcome.Done>(done => !done.Skipped && !done.Recovered, "the step finished by a new attempt, not by a recovery");
        var calls = bench.Fake.Calls();
        calls.Should().HaveCount(2, "the second entry launched");
        calls[1].After("--resume").Should().Be(calls[0].After("--session-id"));
        bench.Timing().OfType<TimingLine.Start>().Select(start => (start.Attempt, start.How)).Should().Equal([(1, "fresh"), (2, "resume")]);
        File.ReadAllText(bench.NotesFile("notes/review.json")).Should().Be("""{"verdict":"ready","findings":[]}""");
        File.Exists(bench.Paths.Done("2-review")).Should().BeTrue();
    }

    [Fact]
    public async Task A_review_that_finished_with_no_text_stops_resumable_and_the_next_entry_resumes_it()
    {
        using var bench = new StepBench();
        bench.Script(new System.Text.Json.Nodes.JsonObject { ["stdoutFile"] = bench.Write("""{"type":"result","subtype":"success","is_error":false,"result":""}""" + "\n", "blank.jsonl") });

        var first = await bench.Runner().RunAsync(bench.Context("C", 1), Ct);

        first.IsStopped(5).Why.Should().Contain("no verdict to read");
        File.Exists(bench.Paths.Done("2-review")).Should().BeFalse();
        bench.Log.ToString().Should().NotContain("finished without a verdict", "the first entry has nothing to resume");
        bench.Replays("result-success.jsonl");

        var second = await bench.Runner().RunAsync(bench.Context("C", 1), Ct);

        second.IsDone().Should().Match<StepOutcome.Done>(done => !done.Skipped && !done.Recovered);
        bench.Fake.Calls().Should().HaveCount(2, "a review that said nothing is asked again, not recovered as done");
        bench.Log.ToString().Should().Contain("2-review: attempt 1 finished without a verdict — resuming it", "the relaunch costs a paid attempt and says why");
        bench.Fake.Calls()[1].After("--resume").Should().Be(bench.Fake.Calls()[0].After("--session-id"));
        File.ReadAllText(bench.NotesFile("notes/review.json")).Should().Be("""{"verdict":"ready","findings":[]}""");
    }

    [Fact]
    public async Task The_coder_outputs_of_a_context_that_names_no_task_are_refused_and_write_nothing()
    {
        using var bench = new StepBench();
        var context = bench.Context("C", 0);
        var files = new ArmFiles(bench.Paths);
        var git = new ArmGit(bench.Git, bench.Paths.CloneFolder, bench.Parent);

        var outcome = await DerivedOutputs.WriteAsync(context, [DerivedOutput.CoderHead, DerivedOutput.CoderDiff], files, git, Ct);

        outcome.Should().BeOfType<Outcome<IReadOnlyList<DerivedOutput>>.Fail>().Which.Reason.Should().Contain("1-code: the coder's context names no task", "E4.S3 writes them for a coder (StepRestartTests); a context with no task has no file to name");
        Directory.Exists(bench.Paths.Out).Should().BeFalse("nothing was written for it");
    }
}
