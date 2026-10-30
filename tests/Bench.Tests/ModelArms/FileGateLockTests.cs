using System.Text.Json.Nodes;
using Bench.Infrastructure.Process;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>The gate lock — one gate step at a time across every arm of an experiment (D10) — proved between two REAL processes: the holder is the console's own <c>gate-lock hold</c>, and the test is the other side. Two tasks of
/// one process could not show what the D10 warning is about (a handle a child inherited once made a SHARED lock), so no scenario here holds the lock from a second task and calls that a second holder. The lock under test is
/// <c>.gate.lock</c>; D11's <c>.isolation.lock</c> is a different lock and is not touched.
/// <para>
/// What this does NOT prove: the lock on Windows (the share-mode semantics there are the platform's; nothing here ran on Windows), on a file system whose <c>flock</c> is not supported (the runtime then ignores the lock, said in the
/// adapter's header), or a holder that is not a .NET process (<c>flock(1)</c> — parent §9). The kill test shows that a killed holder frees the lock on THIS host (Linux): a clean release, a kill and a crash all close the descriptor.
/// </para></summary>
public sealed class FileGateLockTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(30);

    private static string LockOf(ExperimentRoot root) => new RootPaths(root.Root).GateLock;

    private static FileGateLock Lock() => new(TimeProvider.System, Poll);

    private static async Task<string> StatusAsync(ExperimentRoot root)
    {
        var (code, output, error) = await ModelArmsBinary.RunAsync(root, "gate-lock", "status");
        code.Should().Be(0, error);
        return output.Trim();
    }

    [Fact]
    public async Task A_second_process_holding_the_lock_makes_the_first_wait()
    {
        using var root = new ExperimentRoot();
        await using var holder = ModelArmsBinary.Start(root, "gate-lock", "hold", "--seconds", "4");
        var heldAt = await holder.LineAsync("model-arms gate-lock: held", Fast);
        var gateLock = Lock();

        gateLock.TryAcquire(LockOf(root)).Should().BeNull("another process holds the lock: null is the answer, not an exception");

        using var waiting = new CancellationTokenSource(Fast);
        using var handle = await gateLock.AcquireAsync(LockOf(root), waiting.Token);
        var acquiredAt = TimeProvider.System.GetUtcNow();

        (acquiredAt - heldAt).Should().BeGreaterThan(TimeSpan.FromSeconds(3), "the holder keeps the lock four seconds after it printed held; the wait ended only when it let go");
        (await holder.ExitAsync(Fast)).Should().Be(0);
        holder.Printed().Should().EndWith("model-arms gate-lock: released", "the lock was freed before the holder said so");
        (await StatusAsync(root)).Should().StartWith("model-arms gate-lock: held", "the handle AcquireAsync returned really holds it: a second process sees it held");
    }

    [Fact]
    public async Task Cancellation_while_waiting_releases_nothing_and_surfaces_as_cancelled()
    {
        using var root = new ExperimentRoot();
        await using var holder = ModelArmsBinary.Start(root, "gate-lock", "hold", "--seconds", "20");
        await holder.LineAsync("model-arms gate-lock: held", Fast);
        using var cancelled = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var waiting = () => Lock().AcquireAsync(LockOf(root), cancelled.Token);

        await waiting.Should().ThrowAsync<OperationCanceledException>("the caller gave up: that is a cancellation, not a lock");
        (await StatusAsync(root)).Should().StartWith("model-arms gate-lock: held", "the holder still has it: nothing was taken from it and nothing was broken");
        holder.Attempt.IsCompleted.Should().BeFalse("the holder is untouched by the waiter's cancellation");
    }

    [Fact]
    public async Task A_cancelled_token_acquires_nothing_even_when_the_lock_is_free()
    {
        using var root = new ExperimentRoot();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var waiting = () => Lock().AcquireAsync(LockOf(root), cancelled.Token);

        await waiting.Should().ThrowAsync<OperationCanceledException>();
        (await StatusAsync(root)).Should().Be("model-arms gate-lock: free", "a waiter that was told to stop holds nothing");
    }

    [Fact]
    public async Task A_child_launched_while_the_lock_is_held_does_not_keep_it()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli(new JsonObject { ["mode"] = "hang" });
        var handle = await Lock().AcquireAsync(LockOf(root), TestContext.Current.CancellationToken);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<ProcessAttempt>? child = null;
        try
        {
            child = ProcessRunner.RunAsync(FakeCli.Executable, ["-p", "--model", fake.Model], root.Temp, TimeSpan.FromSeconds(30), "prompt", null, stop.Token);
            await AliveAsync(fake, child);

            handle.Dispose();

            (await StatusAsync(root)).Should().Be("model-arms gate-lock: free", "a descriptor the child inherited would keep the lock after its owner let go (D10's warning)");
        }
        finally
        {
            handle.Dispose();
            await stop.CancelAsync();
            await ReapAsync(child);
        }
    }

    [Fact]
    public async Task A_holder_that_is_killed_frees_the_lock_and_a_waiter_gets_it()
    {
        using var root = new ExperimentRoot();
        await using var holder = ModelArmsBinary.Start(root, "gate-lock", "hold", "--seconds", "60");
        await holder.LineAsync("model-arms gate-lock: held", Fast);
        (await StatusAsync(root)).Should().StartWith("model-arms gate-lock: held");

        await holder.KillAsync();

        using var waiting = new CancellationTokenSource(Fast);
        using var handle = await Lock().AcquireAsync(LockOf(root), waiting.Token);
        handle.Should().NotBeNull("the kernel closed the dead holder's descriptor: the lock needs no cleanup and no stale file is ever broken (the file stays, one per root)");
        File.Exists(LockOf(root)).Should().BeTrue("the file is created once and never deleted");
    }

    [Fact]
    public async Task Status_says_free_and_held_in_process()
    {
        using var root = new ExperimentRoot();
        var free = await RunInProcessAsync(root, "gate-lock", "status");
        var held = Lock().TryAcquire(LockOf(root));
        var whileHeld = await RunInProcessAsync(root, "gate-lock", "status");
        held!.Dispose();

        free.Should().Be((0, "model-arms gate-lock: free", string.Empty));
        whileHeld.Code.Should().Be(0);
        whileHeld.Output.Should().StartWith("model-arms gate-lock: held", "the lock is held by this very process: the sidecar names it");
        whileHeld.Error.Should().BeEmpty();
        (await RunInProcessAsync(root, "gate-lock", "status")).Output.Should().Be("model-arms gate-lock: free", "status takes the lock only to look and gives it back at once");
    }

    [Theory]
    [InlineData("")]
    [InlineData("--seconds 0")]
    [InlineData("--seconds -3")]
    [InlineData("--seconds 86401")]
    public async Task Gate_lock_hold_needs_its_seconds(string flags)
    {
        using var root = new ExperimentRoot();

        using var ceiling = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ceiling.CancelAfter(TimeSpan.FromSeconds(10));
        (int Code, string Output, string Error) result;
        try
        {
            result = await RunWithAsync(root, ["gate-lock", "hold", .. flags.Split(' ', StringSplitOptions.RemoveEmptyEntries)], ceiling.Token);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("the hold ran instead of refusing the line: it was still holding after 10 s");
            throw;
        }

        var (code, output, error) = result;

        code.Should().Be(4);
        error.Should().Contain("gate-lock hold takes --seconds N");
        output.Should().BeEmpty("nothing was held");
        (await StatusAsync(root)).Should().Be("model-arms gate-lock: free");
    }

    [Fact]
    public async Task A_stop_while_holding_releases_the_lock_and_surfaces_as_a_stop()
    {
        using var root = new ExperimentRoot();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var output = new StringWriter();

        var run = ModelArmsApp.Program.RunAsync(["gate-lock", "hold", "--seconds", "60", "--experiment", root.Root], output, new StringWriter(), root.Environment, stopping.Token);
        await HeldAsync(output);
        await stopping.CancelAsync();

        await run.Awaiting(r => r).Should().ThrowAsync<OperationCanceledException>("Main maps a stop to the resumable exit, as for every verb (5)");
        (await StatusAsync(root)).Should().Be("model-arms gate-lock: free", "the hold ended with the stop and gave the lock back");
    }

    [Fact]
    public async Task A_stop_while_waiting_for_the_lock_surfaces_as_a_stop_and_not_as_the_ceiling_running_out()
    {
        using var root = new ExperimentRoot();
        using var holder = Lock().TryAcquire(LockOf(root));
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));

        var run = RunWithAsync(root, ["gate-lock", "hold", "--seconds", "1"], stopping.Token);

        await run.Awaiting(r => r).Should().ThrowAsync<OperationCanceledException>("the operator's Ctrl+C is not our ceiling: it maps to the resumable exit (5), not to exit 3");
    }

    [Fact]
    public async Task A_hold_that_cannot_get_the_lock_within_its_ceiling_gives_up_in_its_own_words()
    {
        using var root = new ExperimentRoot();
        using var holder = Lock().TryAcquire(LockOf(root));
        var pass = await root.PassAsync("gate-lock", "hold", "--seconds", "1");
        var call = pass.Calls.Single();

        var code = await ModelArmsApp.Verbs.GateLockVerb.RunAsync(call, TimeSpan.FromMilliseconds(300));

        code.Should().Be(3, "the environment did not let the hold happen: not a stop and not a bad line");
        pass.Error.Should().BeEmpty();
        ((StringWriter)call.Verb.Error).ToString().Should().Contain("gate-lock hold").And.Contain("not free within", "a wait we own gives up in its own words, not as a cancellation (common.reliability)");
    }

    [Theory]
    [InlineData("1", null, true)]
    [InlineData("true", null, true)]
    [InlineData("TRUE", null, true)]
    [InlineData("True", false, true)]
    [InlineData("0", true, false)]
    [InlineData("false", true, false)]
    [InlineData("yes", null, false)]
    [InlineData("yes", true, true)]
    [InlineData(" 1", false, false)]
    [InlineData("1 ", false, false)]
    [InlineData(" 0", true, true)]
    [InlineData("0 ", true, true)]
    [InlineData("", true, true)]
    [InlineData("", null, false)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(null, null, false)]
    public void File_locking_switched_off_in_the_runtime_is_refused_as_the_runtime_reads_it(string? variable, bool? runtimeSwitch, bool off)
    {
        FileGateLock.LockingIsOff(variable, runtimeSwitch).Should().Be(
            off, "measured on .NET 10.0.14 (Linux): a variable that reads as a boolean decides, any other value leaves the switch to decide, and neither set means locking is on");
    }

    [Fact]
    public void A_runtime_with_locking_off_would_make_the_lock_inert_and_the_refusal_says_so()
    {
        FileGateLock.Refusal.Should().Be("file locking is disabled in this runtime — the gate lock would be inert");
        FileGateLock.Create(TimeProvider.System, Poll).Match(_ => true, _ => false).Should().BeTrue("this test process runs with file locking on, or every test above would be meaningless");
    }

    [Fact]
    public void A_poll_of_zero_would_spin_and_is_refused()
    {
        var spin = () => new FileGateLock(TimeProvider.System, TimeSpan.Zero);

        spin.Should().Throw<ArgumentOutOfRangeException>("a retry loop has a pause (common.reliability)");
    }

    private static Task<(int Code, string Output, string Error)> RunInProcessAsync(ExperimentRoot root, params string[] args) =>
        RunWithAsync(root, args, CancellationToken.None);

    private static async Task<(int Code, string Output, string Error)> RunWithAsync(ExperimentRoot root, string[] args, CancellationToken stopping)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var code = await ModelArmsApp.Program.RunAsync([.. args, "--experiment", root.Root], output, error, root.Environment, stopping);
        return (code, output.ToString().Trim(), error.ToString().Trim());
    }

    private static async Task HeldAsync(StringWriter output)
    {
        var limit = TimeProvider.System.GetUtcNow() + Fast;
        while (!output.ToString().Contains("gate-lock: held", StringComparison.Ordinal))
        {
            TimeProvider.System.GetUtcNow().Should().BeBefore(limit, "the hold printed held within the ceiling");
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The fake has started (it logs its call before anything else) and is still running.</summary>
    private static async Task AliveAsync(FakeCli fake, Task<ProcessAttempt> child)
    {
        var limit = TimeProvider.System.GetUtcNow() + Fast;
        while (fake.Calls().Count == 0)
        {
            child.IsCompleted.Should().BeFalse("the fake in hang mode never ends by itself");
            TimeProvider.System.GetUtcNow().Should().BeBefore(limit, "the fake started within the ceiling");
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    private static async Task ReapAsync(Task<ProcessAttempt>? child)
    {
        if (child is null)
        {
            return;
        }

        try
        {
            await child;
        }
        catch (OperationCanceledException)
        {
            // the kill was asked for
        }
    }
}
