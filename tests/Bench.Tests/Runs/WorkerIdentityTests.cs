using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>Who may be swept, as a rule rather than as a database query.
/// <para>
/// The three cases below are the whole of the ownership decision, and the middle one is the reason the
/// decision exists: a sweep that judged another machine's pid against its OWN process table would reach a
/// confident wrong answer and requeue a cell that host is still measuring. Two workers would then measure
/// the same leg, and the honest one's settle would be refused for a mismatch it did nothing to cause.
/// </para></summary>
public sealed class WorkerIdentityTests
{
    private const string ThisHost = "bench-host-a";

    [Fact]
    public void An_owner_that_recorded_no_host_or_pid_is_gone_by_definition()
    {
        WorkerIdentity.Stored("cli", string.Empty, 0)
            .IsProvablyGoneOn(ThisHost, Alive).Should().BeTrue(
                "it predates the owner columns, so nothing can vouch for it and nobody will ever move it");
    }

    [Fact]
    public void An_owner_on_another_machine_is_left_for_that_machine_to_sweep()
    {
        WorkerIdentity.Stored("cli", "bench-host-b", 1234)
            .IsProvablyGoneOn(ThisHost, _ => throw new InvalidOperationException(
                "this host's process table says nothing about pid 1234 on ANOTHER host, so it must not be asked"))
            .Should().BeFalse();
    }

    [Fact]
    public void A_live_pid_on_this_machine_is_not_gone_however_long_the_claim_has_been_quiet()
    {
        WorkerIdentity.Stored("cli", ThisHost, 1234)
            .IsProvablyGoneOn(ThisHost, Alive).Should().BeFalse(
                "the staleness window is a margin against a slow leg, not a death certificate");
    }

    [Fact]
    public void A_dead_pid_on_this_machine_is_gone()
    {
        WorkerIdentity.Stored("cli", ThisHost, 1234)
            .IsProvablyGoneOn(ThisHost, Dead).Should().BeTrue();
    }

    [Fact]
    public void The_host_is_compared_case_insensitively_because_a_machine_names_itself_either_way()
    {
        WorkerIdentity.Stored("cli", "BENCH-HOST-A", 1234)
            .IsProvablyGoneOn(ThisHost, Dead).Should().BeTrue();
    }

    [Fact]
    public void This_process_records_a_host_and_a_pid_that_can_be_checked()
    {
        var here = WorkerIdentity.Here("cli");

        here.CanClaim.Should().BeTrue();
        here.Host.Should().Be(Environment.MachineName);
        here.Pid.Should().Be(Environment.ProcessId);
        here.Canonical.Should().Contain("cli@").And.Contain($"#{Environment.ProcessId}");
    }

    [Fact]
    public void Nobody_holds_nothing_and_can_claim_nothing()
    {
        WorkerIdentity.Nobody.CanClaim.Should().BeFalse();
        WorkerIdentity.Nobody.IsTraceable.Should().BeFalse();
    }

    private static bool Alive(int pid) => true;

    private static bool Dead(int pid) => false;
}
