using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Infrastructure.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.1 — one product process over MCP stdio, against the fake coai-mcp: the handshake comes before any call,
/// a hung call ends at its deadline with the process gone, stderr survives a crash, and a lane holds ONE session.</summary>
public sealed class McpStdioClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_handshake_completes_before_any_tool_call_reaches_the_product()
    {
        using var fake = new FakeCoai(new JsonObject { ["serverVersion"] = "0.39.7" });
        await using var session = (await fake.OpenAsync()).Ok();

        (await session.CallToolAsync("open", new JsonObject { ["repoPath"] = "r", ["branch"] = "b" }, TimeSpan.FromSeconds(30), Ct)).Ok();

        session.ServerVersion.Should().Be("0.39.7", "serverInfo.version is kept beside the pin");
        fake.Events().Select(e => e.Text).Where(t => t.StartsWith("recv ", StringComparison.Ordinal)).Take(3)
            .Should().Equal(["recv initialize", "recv notifications/initialized", "recv tools/call"], "a server refuses every tool before the handshake");
    }

    [Fact]
    public async Task A_hung_call_ends_at_its_timeout_with_the_process_gone()
    {
        using var fake = new FakeCoai(new JsonObject { ["hang"] = new JsonArray("review_plan") });
        await using var session = (await fake.OpenAsync()).Ok();
        var pid = session.ProcessId;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var hung = await session.CallToolAsync("review_plan", new JsonObject(), TimeSpan.FromSeconds(1), Ct);

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "the timeout is absolute");
        hung.Reason().Should().Contain("review_plan").And.Contain("did not answer within 1 s");
        session.IsAlive.Should().BeFalse();
        IsRunning(pid).Should().BeFalse("a call that timed out takes its process with it — nothing is left running that the lane no longer watches");
        (await session.CallToolAsync("open", new JsonObject(), TimeSpan.FromSeconds(5), Ct)).Reason().Should().Contain("session is over");
    }

    [Fact]
    public async Task Stderr_survives_a_crash_and_the_call_says_the_product_exited()
    {
        using var fake = new FakeCoai(new JsonObject { ["crash"] = new JsonArray("review_feature") });
        var launch = fake.Launch();
        await using var session = (await new McpStdioSessionFactory().OpenAsync(launch, Ct)).Ok();

        var crashed = await session.CallToolAsync("review_feature", new JsonObject(), TimeSpan.FromSeconds(30), Ct);

        crashed.Reason().Should().Contain("exited");
        await session.DisposeAsync();
        (await File.ReadAllTextAsync(launch.StderrPath, Ct)).Should().Contain("fake coai-mcp starting").And.Contain("FAILED: the fake crashes on review_feature");
    }

    [Fact]
    public async Task A_missing_product_is_a_refusal_and_not_an_exception()
    {
        using var fake = new FakeCoai();
        var launch = fake.Launch() with { Executable = Path.Combine(fake.Root, "no-such-coai-mcp.exe") };

        (await new McpStdioSessionFactory().OpenAsync(launch, Ct)).Reason().Should().Contain("could not be started");
    }

    [Fact]
    public async Task A_lane_that_opens_a_second_session_while_holding_one_is_refused_as_a_programming_error()
    {
        using var fake = new FakeCoai();
        var factory = new McpStdioSessionFactory();
        await using var slot = new LaneSlot();
        var first = Guid.NewGuid();

        (await slot.OpenAsync(first, factory, fake.Launch(), Ct)).Ok();

        var second = () => slot.OpenAsync(Guid.NewGuid(), factory, fake.Launch(), Ct);
        (await second.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(first.ToString()).And.Contain("one process per cell");
        fake.MaxConcurrent("open", "close", byKey: false)["*"].Should().Be(1, "the refused open started no process");

        await slot.ReleaseAsync();
        slot.IsHolding.Should().BeFalse();
        (await slot.OpenAsync(Guid.NewGuid(), factory, fake.Launch(), Ct)).Ok();
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
