using Bench.Domain;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using System.Text.Json.Nodes;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S4 — the arm's files (<see cref="ArmFiles"/>) and the transcript probe (<see cref="ClaudeSessionTranscripts"/>), over real folders. The runner's tests already walk the happy paths through them; these pin what only a restart
/// or a refusal meets: the attempt counted by name, the prompt that may be rewritten only before its stream exists, the writes that refuse to overwrite and the one that must.
/// <para>
/// What this does NOT prove: a crash in the middle of a staged write (the staging name says it is not a file; no test kills the process there), or a file system without atomic rename.
/// </para></summary>
public sealed class StepFilesTests
{
    private const string Step = "1-code";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (TempFolder Temp, ArmPaths Paths, ArmFiles Files) Arm()
    {
        var temp = new TempFolder();
        var paths = new ArmPaths(temp.Path, "00000000000000c1");
        return (temp, paths, new ArmFiles(paths));
    }

    [Fact]
    public void Prepare_makes_every_folder_the_arm_writes_under()
    {
        var (temp, paths, files) = Arm();
        using (temp)
        {
            files.Prepare();

            new[] { paths.Out, paths.Notes, paths.Work, paths.Tmp }.Should().OnlyContain(folder => Directory.Exists(folder));
        }
    }

    [Fact]
    public void The_next_attempt_is_the_smallest_number_whose_stream_does_not_exist_probed_by_name()
    {
        var (temp, paths, files) = Arm();
        using (temp)
        {
            files.NextAttempt(Step).Should().Be(1);
            files.LastAttempt(Step).Should().Be(0);
            Directory.CreateDirectory(paths.Out);
            File.WriteAllText(paths.Stream(Step, 1), "x");
            File.WriteAllText(paths.Stream(Step, 2), "x");
            File.WriteAllText(paths.Stream(Step, 4), "x");
            File.WriteAllText(paths.Prompt(Step, 3), "a prompt is not an attempt");

            files.NextAttempt(Step).Should().Be(3, "the first gap: the number is reserved by the stream, never by a prompt");
            files.LastAttempt(Step).Should().Be(2);
        }
    }

    [Fact]
    public void A_prompt_is_rewritten_while_its_stream_does_not_exist_and_refused_once_the_stream_does()
    {
        var (temp, paths, files) = Arm();
        using (temp)
        {
            files.WritePrompt(Step, 1, "first").Should().BeFalse("no prompt was there");
            files.WritePrompt(Step, 1, "second").Should().BeTrue("a prompt of an attempt that never launched was there, and is replaced");
            File.ReadAllText(paths.Prompt(Step, 1)).Should().Be("second");

            File.WriteAllText(paths.Stream(Step, 1), string.Empty);
            var act = () => files.WritePrompt(Step, 1, "third");

            act.Should().Throw<InvalidOperationException>("a prompt is immutable once its attempt has started").Which.Message.Should().Contain("1-code.a1");
            File.ReadAllText(paths.Prompt(Step, 1)).Should().Be("second");
        }
    }

    [Fact]
    public void A_file_written_once_is_never_overwritten_and_a_replaced_one_is_swapped_whole_leaving_no_staging_file()
    {
        var (temp, paths, files) = Arm();
        using (temp)
        {
            files.WriteOnce(paths.Done(Step), "one");
            var again = () => files.WriteOnce(paths.Done(Step), "two");
            again.Should().Throw<IOException>("an artefact is committed once: the runtime refuses to move a staged file over it");
            File.ReadAllText(paths.Done(Step)).Should().Be("one");

            files.Replace(paths.Session(Step), "a");
            files.Replace(paths.Session(Step), "b");

            File.ReadAllText(paths.Session(Step)).Should().Be("b");
            Directory.EnumerateFiles(paths.Out, "*.staging-*").Should().BeEmpty("a finished write leaves no staging file");
        }
    }

    [Fact]
    public void A_session_reads_back_as_the_guid_that_was_written_and_a_bad_file_is_a_failure_never_a_throw()
    {
        var (temp, paths, files) = Arm();
        using (temp)
        {
            files.ReadSession(Step).Failed().Should().BeTrue("there is no .session yet");
            var id = Guid.NewGuid();

            files.ReplaceSession(Step, id);

            File.Exists(paths.Session(Step)).Should().BeTrue("the session file is made, with its folder, on first use");
            files.ReadSession(Step).Should().Be(Outcome<Guid>.Success(id));
            File.ReadAllText(paths.Session(Step)).Should().Be(id.ToString("D") + "\n", "the shell wrote the id and a newline");
            File.WriteAllText(paths.Session(Step), "not a guid");
            files.ReadSession(Step).Failed().Should().BeTrue();
        }
    }

    [Fact]
    public void A_stream_is_opened_new_and_shared_for_reading_and_the_tail_is_the_last_characters_of_a_file_or_empty_when_absent()
    {
        var (temp, paths, files) = Arm();
        using (temp)
        {
            files.Tail(paths.Stderr(Step, 1), 10).Should().BeEmpty("an absent file has no tail");
            Directory.CreateDirectory(paths.Out);
            using (var writer = files.Open(paths.Stderr(Step, 1)))
            {
                writer.Line("0123456789");
                writer.Line("abcdef");
                File.Exists(paths.Stderr(Step, 1)).Should().BeTrue("the file is created when it is opened");
                using var reading = new FileStream(paths.Stderr(Step, 1), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                reading.Length.Should().BeGreaterThan(0, "a line is flushed as it is written, and a reader may open the file meanwhile");
                var second = () => files.Open(paths.Stderr(Step, 1));
                second.Should().Throw<IOException>("the stream of an attempt is created new, never reopened");
            }

            files.Tail(paths.Stderr(Step, 1), 10).Should().Be("789\nabcdef\n");
            files.Tail(paths.Stderr(Step, 1), 4000).Should().Be("0123456789\nabcdef\n");
        }
    }

    [Fact]
    public void Timing_lines_are_appended_one_per_line_and_read_back()
    {
        var (temp, _, files) = Arm();
        using (temp)
        {
            files.ReadTiming().Should().BeEmpty();
            var stopped = new TimingLine.Stopped("C1", Step, 5, "why", new DateTimeOffset(2026, 10, 28, 21, 32, 38, TimeSpan.Zero));
            files.AppendTiming(stopped);
            files.AppendTiming(new TimingLine.ArmDone("C1", stopped.AtUtc));

            files.ReadTiming().Should().Equal(stopped, new TimingLine.ArmDone("C1", stopped.AtUtc));
        }
    }

    [Fact]
    public void An_empty_claude_home_has_no_transcript_and_a_file_at_the_path_the_module_spells_is_one()
    {
        using var temp = new TempFolder();
        var session = Guid.NewGuid();
        var cwd = temp.Folder("clone");
        new ClaudeSessionTranscripts(string.Empty).Exists(cwd, session).Should().BeFalse("no home to name: no transcript, a fresh start");
        var probe = new ClaudeSessionTranscripts(temp.Combine("home", ".claude"));
        probe.Exists(cwd, session).Should().BeFalse();

        temp.File(string.Empty, "home", ".claude", "projects", System.Text.RegularExpressions.Regex.Replace(cwd, "[^A-Za-z0-9]", "-"), session.ToString("D") + ".jsonl");

        probe.Exists(cwd, session).Should().BeTrue();
        probe.Exists(cwd, Guid.NewGuid()).Should().BeFalse("another session has no transcript");
    }

    [Fact]
    public async Task A_limited_attempt_resumes_through_the_real_transcript_probe_and_the_fake_refuses_a_resume_without_a_transcript()
    {
        using var bench = new StepBench();
        bench.Script(new JsonObject
        {
            ["transcripts"] = true,
            ["stdoutByCall"] = new JsonObject { ["1"] = StepBench.Replay("result-429-usage-limit-reached.jsonl"), ["2"] = StepBench.Replay("result-success.jsonl") },
        });
        var runner = new ClaudeSessionTranscripts(SessionTranscripts.ClaudeHome(bench.Parent));
        var context = bench.Context("C", 0);

        var outcome = await new StepRunner(
            new Bench.Infrastructure.Models.CliAgentRuntime(Microsoft.Extensions.Logging.Abstractions.NullLogger<Bench.Infrastructure.Models.CliAgentRuntime>.Instance),
            runner, bench.Clock, bench.Waits.Wait, bench.Log, bench.Map, StepProbe.None).RunAsync(context, Ct);

        outcome.IsDone().End.Should().BeOfType<StepEnd.Finished>("the fake refuses --resume for a session with no transcript, so a finished second attempt means the probe found the one the first left");
        var calls = bench.Fake.Calls();
        calls.Should().HaveCount(2);
        calls[1].After("--resume").Should().Be(calls[0].After("--session-id"));
    }
}
