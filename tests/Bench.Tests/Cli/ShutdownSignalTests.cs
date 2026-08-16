using Bench.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>The root token, and the two-signal contract around it.
/// <para>
/// Before 2026-08-16 this repository had no signal handling of any kind and passed
/// <c>CancellationToken.None</c> everywhere in the live path, so stopping the harness was byte-for-byte
/// a crash: the leg in flight died mid-settle and left its cell Claimed for a sweep that nothing ran.
/// </para>
/// <para>
/// <see cref="ShutdownSignal.Request"/> is exercised directly rather than by raising a real SIGTERM at
/// the test process — which would end the test run rather than prove anything about it. What the real
/// handlers add is the wiring, and that is one line each.
/// </para></summary>
public sealed class ShutdownSignalTests
{
    [Fact]
    public void The_first_signal_cancels_the_root_token_and_says_what_will_happen()
    {
        var notice = new StringWriter();
        using var signal = ShutdownSignal.Install(notice);

        signal.Token.IsCancellationRequested.Should().BeFalse("nothing has asked us to stop yet");

        signal.Request("SIGTERM").Should().BeTrue("the first signal is a planned stop the harness handles itself");

        signal.Token.IsCancellationRequested.Should().BeTrue(
            "this token is what stops the drain claiming another cell");
        notice.ToString().Should().Contain("finishing the leg in flight",
            "an operator who is not told the stop is graceful will reach for a second signal immediately");
    }

    [Fact]
    public void A_second_signal_is_left_to_the_runtime_so_an_operator_can_always_abort()
    {
        var notice = new StringWriter();
        using var signal = ShutdownSignal.Install(notice);

        signal.Request("Ctrl+C").Should().BeTrue();

        signal.Request("Ctrl+C").Should().BeFalse(
            "a harness that refuses to die is a harness somebody kills with a bigger hammer, mid-write");
    }

    [Fact]
    public void Disposing_the_signal_leaves_no_handler_behind()
    {
        var notice = new StringWriter();
        var signal = ShutdownSignal.Install(notice);

        signal.Dispose();

        // The token dies with it; a caller that kept it is holding a disposed source, which is the
        // honest state — a process that stopped listening for stops must not look like it is listening.
        signal.Invoking(s => _ = s.Token.IsCancellationRequested)
            .Should().Throw<ObjectDisposedException>();
    }
}
