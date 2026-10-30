using System.Runtime.InteropServices;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using Xunit;
using static Bench.Tests.ModelArms.GateStepKit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S7, fix round — <c>gate.json</c> is the gate MODEL's file, so the gate reads it as a stranger's (D17): through a bounded, non-blocking, no-follow road (a FIFO returns at once, a link is not followed, a file bigger than
/// <see cref="GateStatusFile.LimitBytes"/> is refused and never copied), and what it says is clamped before it is written down. Also the two ends of the lock wait: a ceiling the timer cannot hold, and the holder named by the lock the gate was GIVEN.
/// <para>
/// What this does NOT prove: the bounded read on another architecture or on macOS (the fallback is tested on this host by calling it; a FIFO is not detected there); a gate.json that is swapped between the check and the read (the descriptor is compared with the path).
/// </para></summary>
public sealed class GateStepInputTests
{
    private static readonly TimeSpan Backstop = TimeSpan.FromSeconds(10);

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "links, FIFOs and statx are POSIX");

    private static string StatusPath(StepBench bench)
    {
        var file = bench.NotesFile("notes/gate.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        return file;
    }

    /// <summary>The gate run on a worker thread and bounded: a read that blocks on a FIFO never returns on its own, so the test says "hung" as an assertion and frees the thread by opening the FIFO's other end.</summary>
    private static async Task<StepOutcome> BoundedAsync(StepBench bench, string fifo)
    {
        var run = Task.Run(() => new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct), Ct);
        var finished = await Task.WhenAny(run, Task.Delay(Backstop, Ct));
        if (finished != run)
        {
            _ = Task.Run(() => new FileStream(fifo, FileMode.Open, FileAccess.Write).Dispose(), Ct);
            await Task.WhenAny(run, Task.Delay(Backstop, Ct));
        }

        finished.Should().BeSameAs(run, "the gate reads gate.json without waiting: a FIFO has no writer, and a read that waits for one never ends");
        return await run;
    }

    [Fact]
    public async Task A_gate_json_that_is_a_FIFO_is_refused_at_once_and_the_gate_stops_naming_it()
    {
        Posix();
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var fifo = StatusPath(bench);
        mkfifo(fifo, Convert.ToUInt32("600", 8)).Should().Be(0);

        var outcome = await BoundedAsync(bench, fifo);

        var stopped = outcome.IsStopped(5);
        stopped.Why.Should().Contain("gate.json").And.Contain("not a regular file");
        bench.GateWaits().Should().BeEmpty("a status nobody could read is not a blocked round");
        File.Exists(bench.Paths.Done(GateName)).Should().BeTrue("the step finished; only its status cannot be judged, and the next entry judges again");
        bench.LastStopped().Why.Should().Be(stopped.Why);
    }

    [Fact]
    public async Task A_gate_json_that_is_a_link_is_not_followed_and_the_gate_stops_naming_it()
    {
        Posix();
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var outside = bench.Write("""{"status":"blocked","why":"outside"}""", "outside", "elsewhere.json");
        File.CreateSymbolicLink(StatusPath(bench), outside);

        var outcome = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsStopped(5).Why.Should().Contain("gate.json").And.Contain("link");
        bench.GateWaits().Should().BeEmpty("a file outside notes/ was not read");
        bench.Waits.Recorded.Should().BeEmpty();
    }

    [Fact]
    public async Task A_gate_json_bigger_than_the_limit_is_refused_and_never_copied_into_the_timing()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        File.WriteAllText(StatusPath(bench), "{\"status\":\"blocked\",\"why\":\"" + new string('x', 5 * 1024 * 1024) + "\"}");

        var outcome = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsStopped(5).Why.Should().Contain("bigger than 65536 bytes");
        bench.GateWaits().Should().BeEmpty();
        new FileInfo(bench.Paths.Timing).Length.Should().BeLessThan(16 * 1024, "the model's five megabytes are in no timing line");
    }

    [Fact]
    public async Task A_why_longer_than_the_clamp_is_clamped_in_the_timing_line_and_the_marker_and_never_cut_inside_an_emoji()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var why = new string('y', 2047) + "😀" + new string('z', 10_000);
        bench.WriteStatus("blocked", why);
        var rig = new GateRig();
        rig.WhenWaiting(bench, _ =>
        {
            ContinueMarkers.Read(File.ReadAllText(bench.Paths.Continue(GateName))).Ok().Why.Length.Should().BeLessThanOrEqualTo("gate blocked: ".Length + 2048);
            bench.WriteStatus("proceed");
        });

        (await rig.Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();

        var line = bench.GateWaits().Single();
        line.Why.Length.Should().BeLessThanOrEqualTo(2048);
        line.Why.Should().StartWith("yyyy").And.NotMatchRegex("[\\uD800-\\uDBFF]$", "a clamp never leaves half an emoji");
        new FileInfo(bench.Paths.Timing).Length.Should().BeLessThan(16 * 1024);
    }

    private static byte[] Bom(string json) => [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes(json)];

    /// <summary>A gate whose status file holds exactly <paramref name="bytes"/>; the first wait rewrites it to proceed, so a gate that waits finishes.</summary>
    private static async Task<(StepOutcome Outcome, StepBench Bench)> RunWithAsync(byte[] bytes)
    {
        var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        File.WriteAllBytes(StatusPath(bench), bytes);
        var rig = new GateRig();
        rig.WhenWaiting(bench, _ => bench.WriteStatus("proceed"));
        return (await rig.Gate(bench).RunAsync(bench.GateContext(), Ct), bench);
    }

    [Theory]
    [InlineData("""{"status":"blocked","why":"x"}""", "a UTF-8 byte order mark")]
    [InlineData("{\r\n  \"status\": \"blocked\",\r\n  \"why\": \"x\"\r\n}\r\n", "CRLF line ends")]
    public async Task A_blocked_status_is_read_as_blocked_whatever_wraps_it(string json, string wrapped)
    {
        var (outcome, bench) = await RunWithAsync(wrapped.Contains("byte order mark", StringComparison.Ordinal) ? Bom(json) : System.Text.Encoding.UTF8.GetBytes(json));
        using (bench)
        {
            outcome.IsDone();
            bench.GateWaits().Should().HaveCount(1, $"a blocked status behind {wrapped} is a blocked gate, never a missing one");
            bench.Waits.Recorded.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task A_proceed_status_behind_a_byte_order_mark_is_read_as_proceed()
    {
        var (outcome, bench) = await RunWithAsync(Bom("""{"status":"proceed"}"""));
        using (bench)
        {
            outcome.IsDone();
            bench.Waits.Recorded.Should().BeEmpty();
            bench.LogLines.Last().Should().EndWith("gate status proceed");
        }
    }

    [Theory]
    [InlineData("UTF-16 little endian", new byte[] { 0xFF, 0xFE, 0x7B, 0x00 }, "UTF-16")]
    [InlineData("UTF-16 big endian", new byte[] { 0xFE, 0xFF, 0x00, 0x7B }, "UTF-16")]
    [InlineData("a trailing NUL", new byte[] { 0x7B, 0x7D, 0x00 }, "NUL")]
    public async Task A_status_that_is_not_UTF_8_text_is_refused_by_name_and_never_read_as_missing(string what, byte[] bytes, string named)
    {
        var (outcome, bench) = await RunWithAsync(bytes);
        using (bench)
        {
            outcome.IsStopped(5).Why.Should().Contain(named, what);
            bench.GateWaits().Should().BeEmpty();
        }
    }

    [Fact]
    public void The_portable_read_takes_a_byte_order_mark_off_and_refuses_UTF_16_and_a_NUL_as_the_kernel_road_does()
    {
        using var bench = new StepBench();
        var withBom = bench.Write("x", "portable", "bom.json");
        File.WriteAllBytes(withBom, Bom("""{"status":"blocked"}"""));
        var utf16 = bench.Write("x", "portable", "utf16.json");
        File.WriteAllBytes(utf16, [0xFF, 0xFE, 0x7B, 0x00]);
        var nul = bench.Write("x", "portable", "nul.json");
        File.WriteAllBytes(nul, [0x7B, 0x00]);

        GateStatusFile.ReadFallback(withBom, 1000).Should().Be(new GateStatusFile.Answer.Text("""{"status":"blocked"}"""));
        GateStatusFile.ReadFallback(utf16, 1000).Should().BeOfType<GateStatusFile.Answer.Refused>().Which.Reason.Should().Contain("UTF-16");
        GateStatusFile.ReadFallback(nul, 1000).Should().BeOfType<GateStatusFile.Answer.Refused>().Which.Reason.Should().Contain("NUL");
    }

    [Fact]
    public async Task The_bounded_read_that_off_Linux_x64_takes_over_refuses_a_link_a_folder_and_a_file_past_the_limit_and_reads_the_rest()
    {
        Posix();
        using var bench = new StepBench();
        var file = bench.Write("""{"status":"proceed"}""", "fallback", "gate.json");
        var folder = bench.Combine("fallback", "a-folder");
        Directory.CreateDirectory(folder);
        var link = Path.Combine(bench.Combine("fallback"), "link.json");
        File.CreateSymbolicLink(link, file);
        var exact = bench.Write(new string('a', 100), "fallback", "exact.json");
        var past = bench.Write(new string('a', 101), "fallback", "past.json");

        GateStatusFile.ReadFallback(file, 1000).Should().Be(new GateStatusFile.Answer.Text("""{"status":"proceed"}"""));
        GateStatusFile.ReadFallback(link, 1000).Should().BeOfType<GateStatusFile.Answer.Refused>().Which.Reason.Should().Contain("link");
        GateStatusFile.ReadFallback(folder, 1000).Should().BeOfType<GateStatusFile.Answer.Refused>().Which.Reason.Should().Contain("not a regular file");
        GateStatusFile.ReadFallback(exact, 100).Should().BeOfType<GateStatusFile.Answer.Text>("a file of exactly the limit is read");
        GateStatusFile.ReadFallback(past, 100).Should().BeOfType<GateStatusFile.Answer.Refused>().Which.Reason.Should().Contain("bigger than 100 bytes");
        GateStatusFile.ReadFallback(Path.Combine(bench.Combine("fallback"), "none.json"), 100).Should().BeOfType<GateStatusFile.Answer.Absent>();
    }

    [Fact]
    public void A_lock_ceiling_the_timer_cannot_hold_is_refused_when_the_gate_is_built_and_the_largest_one_is_accepted()
    {
        using var bench = new StepBench();
        var rig = new GateRig();
        var largest = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

        FluentActions.Invoking(() => rig.Gate(bench, ceiling: largest + TimeSpan.FromMilliseconds(1))).Should().Throw<ArgumentOutOfRangeException>().WithMessage("*49.17:02:47.2940000*");
        FluentActions.Invoking(() => rig.Gate(bench, ceiling: largest)).Should().NotThrow();
        FluentActions.Invoking(() => rig.Gate(bench, ceiling: TimeSpan.Zero)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task The_holder_a_busy_gate_names_is_the_one_the_lock_it_was_given_describes()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");

        var outcome = await new GateRig().Gate(bench, gateLock: new BusyGateLock(), ceiling: TimeSpan.FromMilliseconds(300)).RunAsync(bench.GateContext(), Ct);

        outcome.IsStopped(5).Why.Should().Contain($"held {BusyGateLock.Holder}");
        bench.LogLines.Should().Contain(line => line.Contains($"the gate lock is held {BusyGateLock.Holder}", StringComparison.Ordinal), "the line before the wait names the holder the port says");
    }
}
