using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The Windows keep-awake (D12, E5.S2): <c>SetThreadExecutionState</c> held on one dedicated thread. <b>Windows only</b>: it is a kernel32 call, so on any other host the test is skipped with that reason and the adapter is NOT observed — the module doc says so.</summary>
public sealed class WindowsAwakeTests
{
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task The_execution_state_is_held_on_one_thread_and_released_on_dispose()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "SetThreadExecutionState is a Windows call");
        var output = new StringWriter();

        var awake = await WindowsAwake.StartAsync(output);
        var released = awake.ReleasedOnThread;
        var started = DateTime.UtcNow;
        await awake.DisposeAsync();

        awake.Previous.Should().NotBe(0u, "a previous state of 0 is the call failing");
        (await released).Should().Be(awake.HeldOnThread, "the state belongs to the thread that set it, so the release runs on that thread");
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(10));
        output.ToString().Should().Contain("keep-awake: SetThreadExecutionState");
    }
}
