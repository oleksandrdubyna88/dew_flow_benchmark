using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Who has the gate lock, and what a holder that does not let go costs. The kernel's lock is the only truth; <c>.gate.lock.holder</c> is a sidecar the holder writes AFTER it has the lock (written beside and renamed over) and removes
/// BEFORE it releases, so <c>gate-lock status</c> can say <c>held by pid N since &lt;utc&gt;</c> when the sidecar names a live process that started when it says, and <c>held (holder unknown)</c> otherwise. A stale sidecar — a killed
/// holder leaves one — never makes a free lock look held, and a live pid with another start time (a reused pid) is not the holder. The last two tests are the case no in-process test reaches: the holder DIES while a child it started lives.
/// <para>
/// What this does NOT prove: Windows (the holder helper and <c>/proc</c> are Linux's), a holder that is wedged and then killed by something other than SIGKILL, or that a gate step prints the wait (E2.S7's obligation, E2 §8 item 57).
/// </para></summary>
public sealed class GateLockHolderTests
{
    private const string HeldLine = "model-arms gate-lock: held";

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "the holder helper, /proc and signals are Linux's");

    /// <summary>The holder's note, read as JSON; its absence is a failed assertion, not a missing-file fault.</summary>
    private static JsonObject Sidecar(ExperimentRoot root)
    {
        File.Exists(GateLockTestKit.HolderOf(root)).Should().BeTrue("a holder writes .gate.lock.holder once it has the lock");
        return JsonNode.Parse(File.ReadAllText(GateLockTestKit.HolderOf(root)))!.AsObject();
    }

    private static int PidOf(string line) => int.Parse(line.Split(' ')[^1], System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Status_names_the_live_holder_by_pid_and_start_and_the_sidecar_goes_when_the_holder_lets_go()
    {
        Posix();
        using var root = new ExperimentRoot();
        await using var holder = ModelArmsBinary.Start(root, "gate-lock", "hold", "--seconds", "20");
        await holder.LineAsync(HeldLine, GateLockTestKit.Fast);
        var note = Sidecar(root);
        var pid = note["pid"]!.GetValue<int>();

        var status = await GateLockTestKit.StatusAsync(root);

        status.Should().Be($"{HeldLine} by pid {pid} since {note["since_utc"]!.GetValue<string>()}");
        using (var process = Process.GetProcessById(pid))
        {
            process.ProcessName.Should().Be("model-arms", "the pid the sidecar names is the holder's");
        }

        await holder.KillAsync();
        (await GateLockTestKit.StatusAsync(root)).Should().Be("model-arms gate-lock: free", "a killed holder frees the lock whatever its sidecar says: the kernel is the truth");

        await using var second = ModelArmsBinary.Start(root, "gate-lock", "hold", "--seconds", "1");
        await second.LineAsync(HeldLine, GateLockTestKit.Fast);
        Sidecar(root)["pid"]!.GetValue<int>().Should().NotBe(pid, "the next holder wrote its own note over the dead one's");
        (await second.ExitAsync(GateLockTestKit.Fast)).Should().Be(0);
        File.Exists(GateLockTestKit.HolderOf(root)).Should().BeFalse("a holder that releases removes its sidecar before the lock goes");
        GateLockTestKit.RootFiles(root).Should().NotContain(name => name.EndsWith(".tmp", StringComparison.Ordinal), "the note is written beside and renamed over, and no temporary file is left");
    }

    [Theory]
    [InlineData("a dead pid", 2000000000, 0)]
    [InlineData("a live pid whose kernel start time is not the one the note recorded", 0, 1)]
    public async Task A_sidecar_that_does_not_name_a_live_holder_prints_holder_unknown_and_never_overrules_the_lock(string why, int pid, int ticksOff)
    {
        Posix();
        using var root = new ExperimentRoot();
        using var held = GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root));
        held.Should().NotBeNull();
        var note = Sidecar(root);
        note["pid"] = pid == 0 ? Environment.ProcessId : pid;
        note["start_ticks"] = RealTicks() + ticksOff;
        File.WriteAllText(GateLockTestKit.HolderOf(root), note.ToJsonString());

        (await GateLockTestKit.StatusAsync(root)).Should().Be($"{HeldLine} (holder unknown)", why);
    }

    /// <summary>The kernel's start time of this process, read from the function that computes it (never guessed); a null is a failed assertion.</summary>
    private static long RealTicks()
    {
        var ticks = GateHolderNote.StartTicksOf(Environment.ProcessId);
        ticks.Should().NotBeNull("the kernel names a start time for a live process (field 22 of /proc/<pid>/stat)");
        return ticks!.Value;
    }

    [Fact]
    public async Task A_note_that_names_the_live_pid_and_the_kernels_start_ticks_is_believed_whatever_a_clock_says()
    {
        Posix();
        using var root = new ExperimentRoot();
        using var held = GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root));
        File.WriteAllText(GateLockTestKit.HolderOf(root), new JsonObject { ["pid"] = Environment.ProcessId, ["start_ticks"] = RealTicks(), ["since_utc"] = "2026-10-30T08:00:00Z" }.ToJsonString());

        (await GateLockTestKit.StatusAsync(root)).Should().Be($"{HeldLine} by pid {Environment.ProcessId} since 2026-10-30T08:00:00Z", "the start time is the kernel's integer, the same for every reader — no two clock readings are compared");
    }

    [Fact]
    public void A_live_holder_is_named_by_every_read_not_by_most_of_them()
    {
        Posix();
        using var root = new ExperimentRoot();
        using var held = GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root));

        var misses = Enumerable.Range(0, 300).Count(_ => !GateHolderNote.Describe(GateLockTestKit.LockOf(root)).StartsWith("by pid ", StringComparison.Ordinal));

        misses.Should().Be(0, "a live holder that started near a second boundary used to read as unknown in about 1 of 30 reads (Process.StartTime drifts between two readings of one pid)");
    }

    [Theory]
    [InlineData("123 (bash) S 1 123 123 0 -1 4194304 1 0 0 0 1 2 0 0 20 0 1 0 987654 1 2 3", 987654L)]
    [InlineData("123 (my proc) S 1 123 123 0 -1 4194304 1 0 0 0 1 2 0 0 20 0 1 0 555 1 2 3", 555L)]
    [InlineData("123 (a) b) S 1 123 123 0 -1 4194304 1 0 0 0 1 2 0 0 20 0 1 0 42 1 2 3", 42L)]
    [InlineData("123 ((x) S 9 8 7) R 1 123 123 0 -1 4194304 1 0 0 0 1 2 0 0 20 0 1 0 77 1", 77L)]
    [InlineData("123 (bash) S 1 123 123 0 -1 4194304 1 0 0 0 1 2 0 0 20 0 1 0 12", 12L)]
    [InlineData("123 (bash) S 1 2 3", null)]
    [InlineData("123 (bash) S 1 123 123 0 -1 4194304 1 0 0 0 1 2 0 0 20 0 1 0 notanumber 1", null)]
    [InlineData("123 bash S 1 123", null)]
    [InlineData("", null)]
    public void The_start_time_is_field_22_of_the_stat_line_read_after_the_last_closing_parenthesis(string stat, long? expected)
    {
        GateHolderNote.StartTicksFrom(stat).Should().Be(expected, "the command name (field 2) may hold spaces and parentheses, so the fields are counted after the LAST ')'");
    }

    [Fact]
    public async Task A_note_far_bigger_than_a_note_is_not_read_and_reads_as_holder_unknown()
    {
        Posix();
        using var root = new ExperimentRoot();
        using var held = GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root));
        var valid = new JsonObject { ["pid"] = Environment.ProcessId, ["start_ticks"] = RealTicks(), ["since_utc"] = "2026-10-30T08:00:00Z" }.ToJsonString();
        await using (var stream = new FileStream(GateLockTestKit.HolderOf(root), FileMode.Create, FileAccess.Write))
        {
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(valid), TestContext.Current.CancellationToken);
            await stream.WriteAsync(new byte[10 * 1024 * 1024].Select(_ => (byte)' ').ToArray(), TestContext.Current.CancellationToken);
        }

        GateHolderNote.Describe(GateLockTestKit.LockOf(root)).Should().Be("(holder unknown)", "a valid note with ten megabytes of blanks after it is not a note: the reader reads at most 4 KiB and says unknown");
    }

    [Fact]
    public async Task A_huge_note_costs_the_reader_a_bounded_amount_of_memory()
    {
        Posix();
        using var root = new ExperimentRoot();
        using var held = GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root));
        await using (var stream = new FileStream(GateLockTestKit.HolderOf(root), FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(200L * 1024 * 1024);
        }

        GateHolderNote.Describe(GateLockTestKit.LockOf(root));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var said = GateHolderNote.Describe(GateLockTestKit.LockOf(root));
        var spent = GC.GetAllocatedBytesForCurrentThread() - before;

        said.Should().Be("(holder unknown)");
        spent.Should().BeLessThan(256 * 1024, "a 200 MB sparse note must not be read into memory (it cost 1 GB of RSS, and 1.2–3 GB gave an OutOfMemoryException exit 3)");
    }

    [Fact]
    public async Task A_note_that_is_a_FIFO_or_a_link_to_one_or_a_folder_returns_at_once_as_holder_unknown()
    {
        Posix();
        using var root = new ExperimentRoot();
        using var held = GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root));
        var note = GateLockTestKit.HolderOf(root);
        var fifo = Path.Combine(root.Temp, "fifo");
        mkfifo(fifo, Convert.ToUInt32("600", 8)).Should().Be(0);

        foreach (var plant in new Action[]
                 {
                     () => { File.Delete(note); mkfifo(note, Convert.ToUInt32("600", 8)); },
                     () => { File.Delete(note); File.CreateSymbolicLink(note, fifo); },
                     () => { File.Delete(note); Directory.CreateDirectory(note); },
                 })
        {
            plant();
            var task = Task.Run(() => GateHolderNote.Describe(GateLockTestKit.LockOf(root)), TestContext.Current.CancellationToken);
            (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))).Should().BeSameAs(task, "reading a FIFO would wait for a writer for ever: the note is opened without blocking and its kind is judged from the descriptor");
            (await task).Should().Be("(holder unknown)");
            if (Directory.Exists(note))
            {
                Directory.Delete(note);
            }
        }
    }

    [Fact]
    public async Task A_sidecar_that_is_a_link_or_is_not_JSON_is_not_believed()
    {
        Posix();
        using var root = new ExperimentRoot();
        using var held = GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root));
        var real = Sidecar(root);
        var outside = Path.Combine(root.Temp, "outside-note.json");
        File.WriteAllText(outside, real.ToJsonString());
        File.Delete(GateLockTestKit.HolderOf(root));
        File.CreateSymbolicLink(GateLockTestKit.HolderOf(root), outside);

        (await GateLockTestKit.StatusAsync(root)).Should().Be($"{HeldLine} (holder unknown)", "a link is not read: it could name a file anywhere");

        File.Delete(GateLockTestKit.HolderOf(root));
        File.WriteAllText(GateLockTestKit.HolderOf(root), "not json");
        (await GateLockTestKit.StatusAsync(root)).Should().Be($"{HeldLine} (holder unknown)");
    }

    [Fact]
    public async Task A_holder_that_is_stopped_keeps_the_lock_and_status_names_it_until_it_is_killed()
    {
        Posix();
        using var root = new ExperimentRoot();
        await using var holder = ModelArmsBinary.Start(root, "gate-lock", "hold", "--seconds", "20");
        await holder.LineAsync(HeldLine, GateLockTestKit.Fast);
        var pid = Sidecar(root)["pid"]!.GetValue<int>();

        kill(pid, 19).Should().Be(0, "SIGSTOP: the holder hangs without dying");
        try
        {
            GateLockTestKit.Lock().TryAcquire(GateLockTestKit.LockOf(root)).Should().BeNull("a wedged holder keeps the lock: nothing but a kill frees it, and every waiter is bounded only by its own ceiling");
            (await GateLockTestKit.StatusAsync(root)).Should().StartWith($"{HeldLine} by pid {pid} since ", "status says who to look at");
        }
        finally
        {
            await holder.KillAsync();
        }

        (await GateLockTestKit.StatusAsync(root)).Should().Be("model-arms gate-lock: free");
    }

    /// <summary>The holder is a separate test-only executable that takes the lock through <c>FileGateLock</c>, starts <c>/usr/bin/sleep</c> by its absolute path and waits. The test kills the HOLDER only (by its pid; the launcher's tree kill would
    /// take the child too) and asks whether the lock is free while the child lives, and whether the child holds a descriptor of the lock file: the descriptor was opened close-on-exec, so it does not.</summary>
    [Fact]
    public async Task A_holder_killed_while_its_child_lives_frees_the_lock_and_the_child_holds_no_descriptor_of_it()
    {
        Posix();
        Assert.SkipUnless(File.Exists("/usr/bin/sleep"), "the child is /usr/bin/sleep, started by its absolute path");
        using var root = new ExperimentRoot();
        var lockPath = GateLockTestKit.LockOf(root);
        await using var holder = ModelArmsBinary.StartHolder(root, lockPath, "/usr/bin/sleep", "40");
        var holderPid = PidOf(await WaitFor(holder, "held "));
        var childPid = PidOf(await WaitFor(holder, "child "));
        try
        {
            (await GateLockTestKit.StatusAsync(root)).Should().StartWith($"{HeldLine} by pid {holderPid}");

            using (var process = Process.GetProcessById(holderPid))
            {
                process.Kill();
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            using var child = Process.GetProcessById(childPid);
            child.HasExited.Should().BeFalse("the child outlives the holder: that is the case");
            Directory.EnumerateFileSystemEntries($"/proc/{childPid}/fd").Select(entry => new FileInfo(entry).LinkTarget ?? string.Empty)
                .Should().NotContain(target => target.StartsWith(lockPath, StringComparison.Ordinal), "the child holds no descriptor of the lock file: it was opened close-on-exec");
            (await GateLockTestKit.StatusAsync(root)).Should().Be("model-arms gate-lock: free", "a killed holder frees the lock although its child lives");
        }
        finally
        {
            KillIfAlive(childPid);
        }
    }

    private static async Task<string> WaitFor(ModelArmsBinary.Child holder, string prefix)
    {
        await holder.LineAsync(prefix, GateLockTestKit.Fast);
        return holder.Printed().Split(" | ").First(line => line.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void KillIfAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
        }
        catch (ArgumentException)
        {
            // already gone: the test's own cleanup has nothing to do
        }
    }
}
