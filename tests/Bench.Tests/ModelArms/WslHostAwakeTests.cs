using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain;
using Bench.Infrastructure.Process;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The WSL keep-awake (D12, E5.S2): <c>WslHostAwake</c> over <c>tests/FakeCli</c> linked as <c>pwsh.exe</c> in <c>hang</c> mode — the real <c>pwsh.exe</c> and the Windows host's power settings are never touched. The fake reads its stdin to the end before it records its call and the session keeps stdin open, so "held" is the child's pid alive; the
/// argv is read from the call record that appears after the dispose closes stdin, and an exit is made by killing the pid. The wait seam is released one tick at a time, so a check happens exactly when a test says. The path text (<c>WslPaths</c>), the child's environment and the shipped scripts are checked without a process.
/// <para>
/// What this does NOT prove: a real <c>pwsh.exe</c> holding a real <c>SetThreadExecutionState</c> from this adapter (the measured probes M-9 and M-10 are not the adapter), a Windows host (<c>WindowsAwakeTests</c> runs there only), and a relay lost without a signal.
/// </para></summary>
public sealed class WslHostAwakeTests
{
    private const string Ubuntu = "Ubuntu";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The fake as <c>pwsh.exe</c>, a root, the output and a tick the test releases; disposing kills whatever the test left alive and removes the folder.</summary>
    private sealed class PwshKit : IAsyncDisposable
    {
        private readonly TempFolder _temp;
        private int _released;
        private int _entered;

        public PwshKit()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "the fake is linked as pwsh.exe and killed by pid; the process rows are Linux only");
            _temp = new TempFolder();
            Root = new RootPaths(_temp.Folder("root"));
            var pwsh = _temp.Combine("bin", "pwsh.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(pwsh)!);
            File.CreateSymbolicLink(pwsh, FakeCli.Executable);
            Pwsh = pwsh;
            Script = "/home/u/model-arms/windows/keep-awake.ps1";
            Events = _temp.Folder("pwsh-events");
            var script = _temp.File(new JsonObject { ["mode"] = "hang" }.ToJsonString(), "pwsh-script.json");
            var environment = new Dictionary<string, string>
            {
                ["FAKE_CLI_SCRIPT"] = script,
                ["FAKE_CLI_EVENTS"] = Events,
                ["PATH"] = Path.GetDirectoryName(pwsh)!,
            };
            foreach (var name in new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64" }.Where(name => !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(name))))
            {
                environment[name] = System.Environment.GetEnvironmentVariable(name)!;
            }

            Awake = new WslHostAwake(Pwsh, Script, Root.Root, environment, Root, TimeProvider.System, Wait, Output) { ScriptLineWithin = TimeSpan.FromMilliseconds(250) };
        }

        public RootPaths Root { get; }

        public string Pwsh { get; }

        public string Script { get; }

        public string Events { get; }

        public StringWriter Output { get; } = new();

        public WslHostAwake Awake { get; }

        public TempFolder Temp => _temp;

        private int Entered => Volatile.Read(ref _entered);

        public string Log => File.Exists(Root.KeepAwakeLog) ? File.ReadAllText(Root.KeepAwakeLog) : string.Empty;

        /// <summary>The wait the adapter is given: it blocks until the test releases a tick (or the adapter is stopped) and counts how many times the loop came back to it.</summary>
        private async Task Wait(TimeSpan span, CancellationToken ct)
        {
            span.Should().Be(WslHostAwake.CheckEvery, "the loop checks every two minutes (after6.sh:19)");
            Interlocked.Increment(ref _entered);
            await _ticks.WaitAsync(ct);
        }

        private readonly SemaphoreSlim _ticks = new(0);

        /// <summary>Lets exactly one check happen and returns once the loop has either come back to its next wait or said it will not relaunch again.</summary>
        public async Task TickAsync()
        {
            var target = Interlocked.Increment(ref _released) + 1;
            _ticks.Release();
            await Eventually(() => Entered >= target || Log.Contains("not relaunched again", StringComparison.Ordinal), $"the check loop neither came back to its wait nor left after tick {target - 1}");
        }

        public int Child => Awake.ChildProcessId ?? throw new InvalidOperationException("no child is held");

        public static void Kill(int pid)
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
        }

        public async Task KillChildAsync()
        {
            Kill(Child);
            await Eventually(() => Awake.ChildHasExited, "the adapter did not see the killed child exit");
        }

        public IReadOnlyList<JsonObject> Calls() =>
            [.. Directory.GetFiles(Events, "call-*.json").Select(file => (JsonObject)JsonNode.Parse(File.ReadAllText(file))!)];

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Awake.DisposeAsync();
            }
            finally
            {
                _temp.Dispose();
            }
        }
    }

    private static async Task Eventually(Func<bool> condition, string because)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            deadline.Elapsed.Should().BeLessThan(Patience, because);
            await Task.Delay(25, Ct);
        }
    }

    [Fact]
    public async Task The_pwsh_child_is_held_for_the_scope_and_killed_on_dispose()
    {
        await using var kit = new PwshKit();

        kit.Awake.Start().Should().BeOfType<Outcome<int>.Ok>();
        var pid = kit.Child;
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);
        WorkerLiveness.ProcessIsAlive(pid).Should().BeTrue("the child is held for the whole scope with no tick released");
        kit.Output.ToString().Should().Contain("held, not confirmed", "the fake never prints the script's own line, so the host is not said to be held");

        await kit.Awake.DisposeAsync();

        WorkerLiveness.ProcessIsAlive(pid).Should().BeFalse($"disposing ends the scope: process {pid} must not be alive");
        var call = kit.Calls().Should().ContainSingle("the fake records its call once its stdin is closed — by the dispose").Subject;
        call["argv"]!.AsArray().Select(argument => argument!.GetValue<string>()).Should().Equal(WslHostAwake.Arguments(kit.Script));
        kit.Log.TrimEnd().Split('\n').Last().Should().EndWith("keep-awake: stopped");
    }

    [Fact]
    public async Task A_child_that_exits_is_relaunched_at_the_next_check()
    {
        await using var kit = new PwshKit();
        kit.Awake.Start().Should().BeOfType<Outcome<int>.Ok>();
        var first = kit.Child;

        await kit.KillChildAsync();
        await kit.TickAsync();

        kit.Child.Should().NotBe(first, "the next check found the child gone and launched another");
        WorkerLiveness.ProcessIsAlive(kit.Child).Should().BeTrue();
        kit.Log.Should().Contain($"keep-awake exited (pid {first}) — relaunched");
        var second = kit.Child;

        await kit.Awake.DisposeAsync();

        WorkerLiveness.ProcessIsAlive(first).Should().BeFalse();
        WorkerLiveness.ProcessIsAlive(second).Should().BeFalse();
    }

    [Fact]
    public async Task A_child_that_keeps_exiting_is_relaunched_three_times_then_said_and_left()
    {
        await using var kit = new PwshKit();
        kit.Awake.Start().Should().BeOfType<Outcome<int>.Ok>();
        var pids = new List<int> { kit.Child };

        for (var launch = 1; launch <= WslHostAwake.RelaunchesInARow; launch++)
        {
            await kit.KillChildAsync();
            await kit.TickAsync();
            kit.Child.Should().NotBe(pids[^1], $"Expected {launch + 1} launches: the check after kill {launch} launches another child");
            pids.Add(kit.Child);
        }

        pids.Distinct().Should().HaveCount(4, "pids 1 to 4 were launched: the first and three relaunches");
        await kit.KillChildAsync();
        await kit.TickAsync();

        kit.Log.Should().Contain("not relaunched again", "the fourth consecutive exit is said and left");
        kit.Child.Should().Be(pids[^1], "no fifth child was launched");
        Directory.GetFiles(kit.Root.Root, "keep-awake.*.err").Should().HaveCount(4, "one stderr file per launch: Expected 4 launches");
        kit.Output.ToString().Split("not relaunched again").Should().HaveCount(2, "said once on the output");
        kit.Log.Split("not relaunched again").Should().HaveCount(2, "said once in the log");
    }

    [Fact]
    public async Task A_child_found_alive_at_a_check_resets_the_count_of_exits_in_a_row()
    {
        await using var kit = new PwshKit();
        kit.Awake.Start().Should().BeOfType<Outcome<int>.Ok>();
        var pids = new List<int> { kit.Child };

        for (var launch = 1; launch <= WslHostAwake.RelaunchesInARow; launch++)
        {
            await kit.KillChildAsync();
            await kit.TickAsync();
            kit.Child.Should().NotBe(pids[^1], $"Expected {launch + 1} launches: the check after kill {launch} launches another child");
            pids.Add(kit.Child);
        }

        await kit.TickAsync();
        kit.Child.Should().Be(pids[^1], "a child found alive is left alone");
        await kit.KillChildAsync();
        await kit.TickAsync();

        kit.Child.Should().NotBe(pids[^1], "the alive check reset the count, so the fourth exit of the scope is relaunched");
        kit.Log.Should().NotContain("not relaunched again");
    }

    /// <summary>A shell script as <c>pwsh.exe</c>: <paramref name="body"/> runs under the adapter's three-name environment (builtins only — no PATH tool is on it).</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static async Task<(WslHostAwake Awake, StringWriter Output, TempFolder Temp)> StandInAsync(string body, TimeSpan within)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the stand-in is a POSIX script");
        var temp = new TempFolder();
        var pwsh = temp.Executable("bin", "pwsh.exe");
        await File.WriteAllTextAsync(pwsh, $"#!/bin/sh\n{body}\n", Ct);
        var output = new StringWriter();
        var root = new RootPaths(temp.Folder("root"));
        var environment = new Dictionary<string, string> { ["PATH"] = Path.GetDirectoryName(pwsh)! };
        return (new WslHostAwake(pwsh, "/s/keep-awake.ps1", root.Root, environment, root, TimeProvider.System, (_, ct) => Task.Delay(Timeout.Infinite, ct), output) { ScriptLineWithin = within }, output, temp);
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_host_is_said_to_be_held_once_the_script_has_reported()
    {
        var (awake, output, temp) = await StandInAsync("echo 'keep-awake on pid=7 prev=1'\nwhile read -r _l; do :; done", TimeSpan.FromSeconds(30));
        using var folder = temp;
        await using var held = awake;
        var started = System.Diagnostics.Stopwatch.StartNew();

        awake.Start().Should().BeOfType<Outcome<int>.Ok>();

        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "the script's line ends the wait, not the bound");
        output.ToString().Should().Contain($"keep-awake: {awake.Describe}").And.NotContain("has not reported").And.NotContain("exited before");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_pwsh_that_exits_at_once_is_said_at_once_and_does_not_fail_the_chain()
    {
        var (awake, output, temp) = await StandInAsync("echo 'cannot reach the script' >&2\nexit 1", TimeSpan.FromSeconds(60));
        using var folder = temp;
        await using var held = awake;
        var started = System.Diagnostics.Stopwatch.StartNew();

        awake.Start().Should().BeOfType<Outcome<int>.Ok>("the chain is never failed by its keep-awake");

        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "an exited pwsh ends the wait at once, not at the bound");
        output.ToString().Should().Contain("exited before the script reported").And.Contain("/s/keep-awake.ps1").And.NotContain($"keep-awake: {awake.Describe}");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_pwsh_that_is_silent_is_said_to_be_held_but_not_confirmed_at_the_bound()
    {
        var (awake, output, temp) = await StandInAsync("while read -r _l; do :; done", TimeSpan.FromMilliseconds(300));
        using var folder = temp;
        await using var held = awake;

        awake.Start().Should().BeOfType<Outcome<int>.Ok>();

        output.ToString().Should().Contain("has not reported in 0.3 s").And.Contain("held, not confirmed");
    }

    public static TheoryData<string, string, string, string> WindowsPaths => new()
    {
        { "/home/u/x/keep-awake.ps1", Ubuntu, @"\\wsl.localhost\Ubuntu\home\u\x\keep-awake.ps1", string.Empty },
        { "/mnt/c/Users/x/k.ps1", Ubuntu, @"C:\Users\x\k.ps1", string.Empty },
        { "/mnt/d", Ubuntu, @"D:\", string.Empty },
        { "/mnt/cc/k.ps1", Ubuntu, @"\\wsl.localhost\Ubuntu\mnt\cc\k.ps1", string.Empty },
        { "/home/u/x/k.ps1", string.Empty, string.Empty, "WSL_DISTRO_NAME is not set — the keep-awake script's Windows path cannot be named" },
        { "x/k.ps1", Ubuntu, string.Empty, "is not an absolute Linux path" },
        { "/a/../k.ps1", Ubuntu, string.Empty, "'..' segment" },
        { @"/a\b", Ubuntu, string.Empty, "backslash or a NUL" },
        { "/a/k.ps1", @"Ub\untu", string.Empty, "slash or a backslash" },
        { "/a/k.ps1", "Ub/untu", string.Empty, "slash or a backslash" },
    };

    [Theory]
    [MemberData(nameof(WindowsPaths))]
    public void The_keep_awake_script_is_handed_to_pwsh_as_a_windows_path(string linuxPath, string distro, string expected, string refusal)
    {
        var windows = WslPaths.Windows(linuxPath, distro);

        if (refusal.Length == 0)
        {
            windows.Should().BeOfType<Outcome<string>.Ok>(windows.ToString()).Which.Value.Should().Be(expected);
        }
        else
        {
            windows.Should().BeOfType<Outcome<string>.Fail>(windows.ToString()).Which.Reason.Should().Contain(refusal);
        }
    }

    [Fact]
    public void A_path_holding_a_nul_is_refused()
    {
        WslPaths.Windows("/a\0b", Ubuntu).Should().BeOfType<Outcome<string>.Fail>().Which.Reason.Should().Contain("backslash or a NUL");
    }

    [Fact]
    public void The_script_is_one_of_five_tokens_after_the_executable()
    {
        WslHostAwake.Arguments(@"\\wsl.localhost\Ubuntu\a.ps1").Should().Equal("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", @"\\wsl.localhost\Ubuntu\a.ps1");
    }

    [Fact]
    public void The_pwsh_child_gets_only_the_interop_variables()
    {
        var parent = new Dictionary<string, string>
        {
            ["PATH"] = "/bin",
            ["WSL_DISTRO_NAME"] = Ubuntu,
            ["WSL_INTEROP"] = "/run/WSL/1_interop",
            ["HOME"] = "/home/u",
            ["BENCH_DB"] = "Host=x",
            ["ANTHROPIC_API_KEY"] = "sk-x",
            ["COAI_CREDS_KEY"] = "k",
        };

        WslHostAwake.EnvironmentOf(parent).Keys.Should().BeEquivalentTo(["PATH", "WSL_DISTRO_NAME", "WSL_INTEROP"]);
        WslHostAwake.EnvironmentOf(new Dictionary<string, string> { ["PATH"] = "/bin", ["HOME"] = "/h" }).Keys.Should().BeEquivalentTo(["PATH"], "the interop names are passed only when the parent has them");
    }

    [Theory]
    [InlineData("keep-awake.ps1")]
    [InlineData("check-keep-awake.ps1")]
    [InlineData("stop-keep-awake.ps1")]
    public void The_shipped_scripts_are_the_harness_copies_byte_for_byte(string name)
    {
        var harness = Path.Combine(Cli.Repository.Root, "research", "local-model-coding", "harness", "windows", name);
        var shipped = Path.Combine(Cli.Repository.Root, "tools", "model-arms", "ModelArms", "windows", name);
        var copied = Path.Combine(AppContext.BaseDirectory, "windows", name);

        Sha(shipped).Should().Be(Sha(harness), $"{shipped} is the harness copy");
        File.Exists(copied).Should().BeTrue($"{copied}: the project copies the script beside the binary");
        Sha(copied).Should().Be(Sha(harness), "the copy beside the binary has the same bytes");
    }

    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
