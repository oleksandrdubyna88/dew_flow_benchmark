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
