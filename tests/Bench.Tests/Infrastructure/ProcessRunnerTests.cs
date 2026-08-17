using Bench.Infrastructure.Process;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The one place external processes are launched, so the properties that matter are asserted
/// here rather than trusted at each call site.</summary>
public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task A_missing_executable_is_an_answer_not_an_exception()
    {
        var attempt = await ProcessRunner.RunAsync(
            "definitely-not-a-real-executable-9f3c", ["--version"], Path.GetTempPath(),
            TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        attempt.Should().BeOfType<ProcessAttempt.NotFound>();
        attempt.Describe.Should().Contain("is not on PATH");
    }

    [Fact]
    public async Task A_non_zero_exit_is_reported_with_its_output_rather_than_thrown()
    {
        var attempt = await ProcessRunner.RunAsync(
            "git", ["cat-file", "-e", "0000000000000000000000000000000000000000"], Path.GetTempPath(),
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        attempt.Should().BeOfType<ProcessAttempt.Completed>()
            .Which.Result.Ok.Should().BeFalse("a failing git command is a result, not a crash");
    }

    [Fact]
    public async Task Input_reaches_the_child_on_STDIN()
    {
        // `git hash-object --stdin` reads until end-of-input and prints the hash of what it read, so a known
        // answer proves both halves: the bytes arrived, and the pipe was CLOSED — without the close it would
        // wait forever and this would be a timeout instead.
        var attempt = await ProcessRunner.RunAsync(
            "git", ["hash-object", "--stdin"], Path.GetTempPath(),
            TimeSpan.FromSeconds(30), "hello", TestContext.Current.CancellationToken);

        attempt.Should().BeOfType<ProcessAttempt.Completed>()
            .Which.Result.Output.Should().StartWith(
                "b6fc4c620b67d95f953a5c1c1230aaab5db5a1b0",
                "git's hash of exactly the five bytes 'hello' — anything else means something other than the "
                + "prompt arrived");
    }

    [Fact]
    public async Task An_input_larger_than_the_platforms_argument_limit_still_arrives()
    {
        // The whole reason stdin exists here. An argument list caps out around 32 KB on Windows, and a CLI
        // agent's prompt runs to kilobytes — so the failure would have arrived on the machine with the biggest
        // target repository, at the moment somebody made the prompt longer.
        var prompt = new string('x', 200_000);

        var attempt = await ProcessRunner.RunAsync(
            "git", ["hash-object", "--stdin"], Path.GetTempPath(),
            TimeSpan.FromSeconds(60), prompt, TestContext.Current.CancellationToken);

        var output = attempt.Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;
        output.Ok.Should().BeTrue(output.Output);
        output.Output.Trim().Should().HaveLength(40, "a hash means the whole 200 KB was read");
    }

    [Fact]
    public async Task A_child_that_reads_no_input_is_not_left_waiting_on_an_open_pipe()
    {
        // Redirected only when there is something to write: a child that reads stdin and finds an open empty
        // pipe waits forever, and the timeout would then report a hang this launcher caused.
        var attempt = await ProcessRunner.RunAsync(
            "git", ["--version"], Path.GetTempPath(),
            TimeSpan.FromSeconds(30), string.Empty, TestContext.Current.CancellationToken);

        attempt.Should().BeOfType<ProcessAttempt.Completed>().Which.Result.Ok.Should().BeTrue();
    }

    [Fact]
    public void A_process_that_exits_between_the_check_and_the_kill_is_not_an_error()
    {
        // Both are the same fact — there is nothing left to kill — and only the first was ever caught.
        // A race is not something a test can schedule, so the rule is asserted directly rather than by
        // trying to make a real process die on cue.
        ProcessRunner.IsAlreadyGone(new InvalidOperationException("No process is associated with this object"))
            .Should().BeTrue();
        ProcessRunner.IsAlreadyGone(new System.ComponentModel.Win32Exception(5))
            .Should().BeTrue("access denied on a dying pid is the OTHER way a kill is refused, and it escaped a best-effort path");
    }

    [Fact]
    public void A_kill_that_failed_for_any_other_reason_still_propagates()
    {
        ProcessRunner.IsAlreadyGone(new IOException("the pipe is broken"))
            .Should().BeFalse("a guard that swallowed everything would turn a real fault into silence");
    }

    [Fact]
    public async Task Arguments_travel_as_argv_so_a_hostile_value_is_never_re_parsed()
    {
        // If this were built into a shell string, the semicolon and quotes would be syntax. As argv it
        // is one literal argument, and git simply reports that it is not a known revision.
        var attempt = await ProcessRunner.RunAsync(
            "git", ["rev-parse", "--verify", "\"; echo pwned; #"], Path.GetTempPath(),
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        attempt.Should().BeOfType<ProcessAttempt.Completed>();
        ((ProcessAttempt.Completed)attempt).Result.Output.Should().NotContain("pwned");
    }
}
