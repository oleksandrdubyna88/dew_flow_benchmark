using System.Diagnostics;
using System.Text.Json.Nodes;
using Bench.Application.Probes;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Probes;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Probes.Driver;

/// <summary>S2 acceptance 4 — one attempt end to end against the fake CLI through the real launcher: the canary read off the
/// answer and the readers applied; a usage-error exit settling <i>launch refused</i> with every fact <i>not captured</i>; the wall
/// killing the process tree and settling <i>timed out</i>; a quota marker handing the attempt back unmeasured; the artefacts
/// committed before the settlement and referenced by hash; the fixture gone whatever happened.</summary>
public sealed class CliProbeRunnerTests : IDisposable
{
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();
    private readonly List<FakeCli> _fakes = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string WorkRoot => _root.Sibling("work");

    [Fact]
    public async Task A_cli_that_reads_the_file_settles_canary_yes_with_three_artefacts_committed_and_its_fixture_deleted()
    {
        var (runner, run, subject) = Rig("claude", new JsonObject { ["readInside"] = true });
        var cell = Claimed(run, subject, ProbeKind.ReadInside);

        var result = await runner.RunAsync(run, subject, cell, Ct);

        var settlement = result.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement;
        settlement.Facts.Kind.Should().Be(ProbeAttemptKind.Answered);
        settlement.Facts.CanaryRead.Should().Be(ProbeFact.Yes, "the IN token is in the answer");
        settlement.Facts.ExitCode.Value.Should().Be(0);
        settlement.Artifacts.Select(a => a.Kind).Should().BeEquivalentTo([ProbeArtifactKind.Answer, ProbeArtifactKind.Stdout, ProbeArtifactKind.Stderr]);
        foreach (var artifact in settlement.Artifacts)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine([_root.Path, .. artifact.Path.Segments]), Ct);
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)).Should().Be(artifact.Sha256, $"{artifact.Kind} is referenced by the hash of the bytes on disk");
            bytes.LongLength.Should().Be(artifact.Length);
        }

        var call = _fakes[0].Calls().Should().ContainSingle().Subject;
        call.Argv.Should().StartWith(["-p", "--model", subject.ModelId, "--output-format", "json", "--permission-mode", "plan"]);
        call.Argv.Should().ContainInConsecutiveOrder("--disallowedTools", "Edit", "Write", "NotebookEdit", "Bash", "Task", "Agent", "WebSearch", "WebFetch");
        call.Prompt.Should().Contain("inside.txt");
        Directory.Exists(call.Cwd).Should().BeFalse("the fixture is deleted after the attempt");
        Directory.Exists(Path.Combine(WorkRoot, "probes", run.Id.ToString("D"), cell.Id.ToString("D"))).Should().BeFalse();
    }

    [Fact]
    public async Task A_web_search_with_the_oracles_version_and_tool_evidence_settles_current_and_codex_gets_search_before_exec()
    {
        var (runner, run, subject) = Rig("codex", new JsonObject { ["webSearch"] = true, ["version"] = "0.52.0" });

        var result = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.WebSearch), Ct);

        var facts = result.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;
        facts.AnswerCurrent.Should().Be(ProbeFact.Yes);
        facts.ToolEvidence.Should().Be(ProbeFact.Yes, "a codex web_search item in a completed turn");
        facts.CanaryRead.Should().Be(ProbeFact.NotCaptured, "the web search asks no file question");
        var argv = _fakes[0].Calls().Single().Argv;
        argv.Should().StartWith(["--search", "exec"]);
        argv.Should().Contain("--json").And.ContainInConsecutiveOrder("-m", subject.ModelId);
    }

    [Fact]
    public async Task A_confined_row_that_was_denied_and_tried_settles_confined_and_the_control_gets_the_same_denial()
    {
        var (runner, run, subject) = Rig("claude", new JsonObject { ["webSearch"] = true, ["readAttempted"] = true, ["readOutside"] = false });

        var confined = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.WebConfined), Ct);
        var control = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.ReadDenied), Ct);

        var row = confined.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;
        row.CanaryRead.Should().Be(ProbeFact.No, "no canary in the answer AND the read was tried — confined");
        row.ReadAttempted.Should().Be(ProbeFact.Yes);
        row.AnswerCurrent.Should().Be(ProbeFact.Yes);
        control.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts.ReadAttempted.Should().Be(ProbeFact.Yes);
        var calls = _fakes[0].Calls();
        calls.Should().HaveCount(2);
        var denied = calls.Select(c => c.Argv.SkipWhile(a => a != "--disallowedTools").Skip(1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList()).ToList();
        denied[0].Should().Contain(["Read", "Glob", "Grep"]).And.NotContain("WebSearch", "web ON: the two web tools are not denied");
        denied[1].Should().Equal([.. denied[0], "WebSearch", "WebFetch"], "the control is the row's launch with web OFF — the same denial list");
    }

    [Fact]
    public async Task A_usage_error_exit_settles_launch_refused_with_every_fact_not_captured_and_the_exit_code_kept()
    {
        var (runner, run, subject) = Rig("codex", new JsonObject { ["refuseArgvContaining"] = "--search", ["refuseExit"] = 2 });

        var result = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.WebSearch), Ct);

        var facts = result.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;
        facts.Kind.Should().Be(ProbeAttemptKind.LaunchRefused, "a refusal is a launch fact about the build, never 'codex cannot search'");
        facts.ExitCode.Value.Should().Be(2);
        new[] { facts.CanaryRead, facts.ReadAttempted, facts.AnswerCurrent, facts.ToolEvidence, facts.Reachable, facts.AccountOut }.Should().OnlyContain(f => f == ProbeFact.NotCaptured);
        var stderr = result.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Artifacts.Single(a => a.Kind == ProbeArtifactKind.Stderr);
        (await File.ReadAllTextAsync(Path.Combine([_root.Path, .. stderr.Path.Segments]), Ct)).Should().Contain("unexpected argument '--search'");
    }

    [Fact]
    public async Task The_wall_kills_the_process_tree_and_settles_timed_out()
    {
        var (runner, run, subject) = Rig("claude", new JsonObject { ["mode"] = "hang" }, wall: TimeSpan.FromSeconds(2));

        var clock = Stopwatch.StartNew();
        var result = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.ReadInside), Ct);

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
        var facts = result.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;
        facts.Kind.Should().Be(ProbeAttemptKind.TimedOut);
        facts.ExitCode.WasCaptured.Should().BeFalse("the wall ended it; there is no exit code");
        facts.CanaryRead.Should().Be(ProbeFact.NotCaptured);
        var call = _fakes[0].Calls().Single();
        await AssertGoneAsync(call.Pid);
        Directory.Exists(call.Cwd).Should().BeFalse("the fixture is deleted after a timeout too");
        Directory.EnumerateDirectories(Path.Combine(WorkRoot, "probes", run.Id.ToString("D"))).Should().BeEmpty("and leaves no husk under the run's root");
    }

    [Theory]
    [InlineData("claude", "Claude AI usage limit reached|1759340000")]
    [InlineData("codex", "You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase more credits.")]
    [InlineData("antigravity", "Error: 429 RESOURCE_EXHAUSTED: Quota exceeded for quota metric 'Generate Content API requests per day'")]
    public async Task A_quota_marker_hands_the_attempt_back_unmeasured_with_the_stderr_artefact_kept(string runtime, string marker)
    {
        var (runner, run, subject) = Rig(runtime, new JsonObject { ["mode"] = "quota", ["quotaText"] = marker });

        var result = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.ReadInside), Ct);

        result.Should().BeOfType<ProbeAttemptResult.Unmeasured>().Subject.Reason.Should().Be(ProbeReason.AccountOut, "a quota stop is not a measurement (D8)");
        var stderr = Directory.EnumerateFiles(Path.Combine(_root.Path, "probes"), "stderr.txt", SearchOption.AllDirectories).Should().ContainSingle().Subject;
        (await File.ReadAllTextAsync(stderr, Ct)).Should().Contain(marker.Split(':')[0], "what the CLI said is kept for the write-up");
    }

    [Fact]
    public async Task A_plain_rate_limit_on_stderr_does_not_bench_and_a_clean_exit_that_said_nothing_settles_failed()
    {
        var (runner, run, subject) = Rig("claude", new JsonObject { ["stderr"] = "HTTP 429 Too Many Requests — rate limited, retrying", ["readInside"] = true });
        var (silent, silentRun, silentSubject) = Rig("claude", new JsonObject { ["mode"] = "silent" });

        var answered = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.ReadInside), Ct);
        var nothing = await silent.RunAsync(silentRun, silentSubject, Claimed(silentRun, silentSubject, ProbeKind.ReadInside), Ct);

        answered.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts.CanaryRead.Should().Be(ProbeFact.Yes, "a transient 429 beside an answer is not an empty account");
        var facts = nothing.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;
        facts.Kind.Should().Be(ProbeAttemptKind.Failed, "an empty answer would read every canary as 'no'");
        facts.CanaryRead.Should().Be(ProbeFact.NotCaptured);
        facts.ExitCode.Value.Should().Be(0);
    }

    private (CliProbeRunner Runner, ProbeRun Run, ProbeSubject Subject) Rig(string runtime, JsonObject script, TimeSpan? wall = null)
    {
        var fake = new FakeCli(script);
        _fakes.Add(fake);
        var subject = ProbeSubject.Parse($"{runtime}-fake-{_fakes.Count}", runtime, fake.Model, "BENCH_FAKE_CLI").Ok();
        var run = ProbeRun.Planned(Guid.CreateVersion7(), ProbeStoreFixtures.Oracle(), [subject], 1, DateTimeOffset.UtcNow).Ok();
        var runner = new CliProbeRunner(
            new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance), new ProbeFixtures(WorkRoot), new ProbeArtifacts(_root.Path),
            new CliProbeSettings(new Dictionary<string, string> { [subject.Id.Value] = FakeCli.Executable }, wall ?? TimeSpan.FromSeconds(60)),
            NullLogger<CliProbeRunner>.Instance);

        return (runner, run, subject);
    }

    private static ProbeCell Claimed(ProbeRun run, ProbeSubject subject, ProbeKind probe) =>
        ProbeCellLifecycle.Claim(
            ProbeCell.Pending(Guid.CreateVersion7(), run.Id, new ProbeMatrixCell(probe, subject.Id, 1, 0, 0)), WorkerIdentity.Here("test"), DateTimeOffset.UtcNow, ProbeStoreFixtures.Pin()).Ok();

    private static async Task AssertGoneAsync(int pid)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return; // no such process: killed with the tree
            }

            await Task.Delay(100, Ct);
        }

        Assert.Fail($"pid {pid} outlived the wall — the process tree was not killed");
    }

    public void Dispose()
    {
        foreach (var fake in _fakes)
        {
            fake.Dispose();
        }

        _root.Dispose();
        Gate.Driver.GateDriverRig.DeleteSiblings(_root);
        try
        {
            if (Directory.Exists(WorkRoot))
            {
                Directory.Delete(WorkRoot, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The temp folder is the operating system's to clean.
        }
    }
}
