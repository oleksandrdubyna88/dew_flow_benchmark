using System.Text.RegularExpressions;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;
using ArmsProgram = ModelArms.Program;
using FactsRead = ModelArms.Adapters.FactsRead;
using GateSeams = ModelArms.GateSeams;
using VerbInput = ModelArms.VerbInput;

namespace Bench.Tests.ModelArms;

/// <summary>The console's two halves of the road in: the dispatch (every verb the command line accepts has an arm; one not built says which story
/// builds it) and the gate (the one road in for every verb that reads an experiment).
/// <para>
/// What this does NOT prove: any verb's own work (each has its tests), the process's own signal handling (<c>ShutdownSignal</c> has its own), or
/// the trust check on Windows, which is declared not performed.
/// </para></summary>
public sealed partial class ModelArmsProgramTests
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironment = new Dictionary<string, string>();

    private static async Task<(int Code, string Output, string Error)> RunAsync(string[] args, IReadOnlyDictionary<string, string>? environment = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ArmsProgram.RunAsync(args, output, error, environment ?? NoEnvironment, CancellationToken.None);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Every_verb_the_command_line_accepts_has_a_dispatch_arm()
    {
        var source = File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "ModelArms", "Program.cs"));
        var start = source.IndexOf("private static Task<int> Dispatch(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "a scan that finds no Dispatch method passes forever");
        var body = source[start..source.IndexOf("\n    }", start, StringComparison.Ordinal)];

        var arms = Arm().Matches(body).Select(m => m.Groups["verb"].Value).ToList();

        arms.Should().NotBeEmpty("the scan must still read the arms");
        arms.Should().BeEquivalentTo(ModelArmsCommandLine.Verbs, "one arm per verb, no more and no fewer");
        arms.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task A_verb_not_built_yet_is_refused_naming_its_epic_with_exit_4()
    {
        var (code, _, error) = await RunAsync(["chain", "--experiment", "/x"]);

        code.Should().Be(4);
        error.Should().Contain("E4.S5").And.Contain("chain");
    }

    [Fact]
    public async Task Version_and_help_need_no_experiment_root()
    {
        var version = await RunAsync(["version"]);
        var help = await RunAsync([]);

        version.Code.Should().Be(0);
        version.Output.Should().MatchRegex(@"^model-arms \S+");
        help.Code.Should().Be(0);
        help.Output.Should().NotBeEmpty();
        foreach (var verb in ModelArmsCommandLine.Verbs)
        {
            help.Output.Should().Contain($"model-arms {verb}", "the help names every verb");
        }
    }

    [Theory]
    [InlineData("frob")]
    [InlineData("run-arm")]
    public async Task An_unknown_verb_or_a_bad_shape_exits_4(string line)
    {
        var (code, output, error) = await RunAsync(line.Split(' '));

        code.Should().Be(4);
        error.Should().StartWith("model-arms:");
        output.Should().BeEmpty();
    }

    [Fact]
    public async Task The_gate_refuses_a_missing_root_an_untrusted_file_and_an_invalid_experiment_in_that_order()
    {
        var none = await ExperimentRoot.ThroughAsync(["status"], NoEnvironment);
        none.ExitCode.Should().Be(4);
        none.Error.Should().Contain("MODEL_ARMS_EXPERIMENT").And.Contain("--experiment");
        none.Calls.Should().BeEmpty();

        var missing = await ExperimentRoot.ThroughAsync(["status", "--experiment", Path.Combine(Path.GetTempPath(), "testsToDelete-modelarms-absent-root")], NoEnvironment);
        missing.ExitCode.Should().Be(4);
        missing.Error.Should().Contain("init").And.Contain("does not exist");
        missing.Calls.Should().BeEmpty();

        using var root = new ExperimentRoot();
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(Path.Combine(root.Root, "experiment.json"), (UnixFileMode)Convert.ToInt32("664", 8));
            var untrusted = await root.PassAsync("status");
            untrusted.ExitCode.Should().Be(4);
            untrusted.Error.Should().Contain("664").And.Contain("experiment.json");
            untrusted.Calls.Should().BeEmpty("a file another user can change is never read");
            File.SetUnixFileMode(Path.Combine(root.Root, "experiment.json"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        root.Edit(top => top["colour"] = 1);
        var invalid = await root.PassAsync("status");
        invalid.ExitCode.Should().Be(4);
        invalid.Error.Should().Contain("experiment.json is refused").And.Contain("colour");
        invalid.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_file_that_is_both_untrusted_and_invalid_is_refused_for_its_trust_and_its_content_is_never_read()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes are POSIX");
            return;
        }
        using var root = new ExperimentRoot();
        root.Edit(top => top["colour"] = 1);
        File.SetUnixFileMode(Path.Combine(root.Root, "experiment.json"), (UnixFileMode)Convert.ToInt32("664", 8));

        var passed = await root.PassAsync("status");

        passed.ExitCode.Should().Be(4);
        passed.Error.Should().Contain("664");
        passed.Error.Should().NotContain("colour", "trust (step 3) comes before the content is read (step 4)");
        passed.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_valid_root_reaches_the_verb_once_with_the_experiment_it_read()
    {
        using var root = new ExperimentRoot();

        var passed = await root.PassAsync("status");

        passed.ExitCode.Should().Be(0, passed.Error);
        var call = passed.Calls.Should().ContainSingle().Subject;
        call.Experiment.Root.Should().Be(root.Root);
        call.Root.Root.Should().Be(root.Root);
        call.Verb.Verb.Should().Be("status");
    }

    [Fact]
    public async Task The_root_may_be_named_by_the_environment_and_a_trailing_separator_is_trimmed()
    {
        using var root = new ExperimentRoot();
        var environment = new Dictionary<string, string>(root.Environment) { ["MODEL_ARMS_EXPERIMENT"] = root.Root + "/" };

        var passed = await ExperimentRoot.ThroughAsync(["status"], environment);

        passed.ExitCode.Should().Be(0, passed.Error);
        passed.Calls.Should().ContainSingle().Which.Experiment.Root.Should().Be(root.Root);
    }

    [Fact]
    public async Task A_flag_the_verb_cannot_read_exits_4_naming_the_flag()
    {
        var (code, _, error) = await RunWithAsync(["gate-lock", "hold", "--seconds", "soon"], input => Task.FromResult(input.Flags.Int("seconds", 0)));

        code.Should().Be(4);
        error.Should().Contain("model-arms gate-lock:").And.Contain("--seconds").And.Contain("soon");
    }

    [Fact]
    public async Task An_unexpected_fault_exits_3_naming_the_verb_and_the_exception_type()
    {
        var (code, _, error) = await RunWithAsync(["version"], _ => throw new InvalidOperationException("boom"));

        code.Should().Be(3);
        error.Should().Contain("model-arms version: unexpected InvalidOperationException: boom");
    }

    [Fact]
    public async Task A_stop_the_operator_asked_for_travels_up_for_main_to_map_and_a_cancellation_nobody_asked_for_is_a_fault()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        var asked = async () => await ArmsProgram.RunAsync(["version"], new StringWriter(), new StringWriter(), NoEnvironment, stop.Token, _ => throw new OperationCanceledException());

        await asked.Should().ThrowAsync<OperationCanceledException>("Main maps a requested stop to the resumable exit");

        var (code, _, error) = await RunWithAsync(["version"], _ => throw new OperationCanceledException());

        code.Should().Be(3, "no stop was requested, so this is a fault of the verb");
        error.Should().Contain("unexpected OperationCanceledException");
    }

    [Fact]
    public async Task A_root_reached_through_a_symbolic_link_is_refused_with_exit_4_and_the_verb_is_not_called()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("the trust check reads POSIX facts");
            return;
        }

        using var root = new ExperimentRoot();
        var link = Path.Combine(root.Temp, "root-link");
        Directory.CreateSymbolicLink(link, root.Root);

        var passed = await ExperimentRoot.ThroughAsync(["status", "--experiment", link], root.Environment);

        passed.ExitCode.Should().Be(4, "a link is a refusal, not a host that could not say");
        passed.Error.Should().Contain("symbolic link").And.Contain(link);
        passed.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task An_experiment_file_that_is_a_symbolic_link_is_refused_and_never_read()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("the trust check reads POSIX facts");
            return;
        }

        using var root = new ExperimentRoot();
        var real = Path.Combine(root.Temp, "elsewhere.json");
        File.Move(Path.Combine(root.Root, "experiment.json"), real);
        File.CreateSymbolicLink(Path.Combine(root.Root, "experiment.json"), real);

        var passed = await root.PassAsync("status");

        passed.ExitCode.Should().Be(4);
        passed.Error.Should().Contain("symbolic link").And.Contain("experiment.json");
        passed.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_scenarios_file_another_user_can_change_is_refused_with_exit_4_naming_its_mode()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes are POSIX");
            return;
        }

        using var root = new ExperimentRoot();
        File.SetUnixFileMode(Path.Combine(root.Root, "scenarios.json"), (UnixFileMode)Convert.ToInt32("666", 8));

        var passed = await root.PassAsync("validate");

        passed.ExitCode.Should().Be(4);
        passed.Error.Should().Contain("scenarios.json").And.Contain("666");
        passed.Calls.Should().BeEmpty();

        var readonlyVerb = await root.PassAsync("status");

        readonlyVerb.ExitCode.Should().Be(4, "scenarios.json is trusted by every verb that reads an experiment when the root has one");
    }

    [Fact]
    public async Task An_experiment_file_the_operator_cannot_read_is_the_environment_exit_3_not_a_refusal()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes are POSIX");
            return;
        }

        using var root = new ExperimentRoot();
        var path = Path.Combine(root.Root, "experiment.json");
        File.SetUnixFileMode(path, UnixFileMode.None);
        Assert.SkipWhen(CanRead(path), "this user reads a mode 000 file (root)");

        var passed = await root.PassAsync("status");

        passed.ExitCode.Should().Be(3);
        passed.Error.Should().Contain("experiment.json cannot be read");
        passed.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_run_verb_is_refused_an_arm_that_was_never_made()
    {
        using var root = new ExperimentRoot();
        root.Write("baseline.json", "{}");

        var passed = await root.PassAsync("run-arm", "C1");

        passed.ExitCode.Should().Be(4, passed.Error);
        passed.Error.Should().Contain("arms.map.json is missing").And.Contain("make");
        passed.Calls.Should().BeEmpty();

        root.Write("arms.map.json", "{}");
        var unmade = await root.PassAsync("run-arm", "C1");

        unmade.ExitCode.Should().Be(4, unmade.Error);
        unmade.Error.Should().Contain("made-C1").And.Contain("make");
        (await root.PassAsync("status")).ExitCode.Should().Be(0, "a verb that is given no arm needs no arm made");
    }

    private static async Task<(int Code, string Output, string Error)> RunWithAsync(string[] args, Func<VerbInput, Task<int>> dispatch)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ArmsProgram.RunAsync(args, output, error, NoEnvironment, CancellationToken.None, dispatch);
        return (code, output.ToString(), error.ToString());
    }

    private static bool CanRead(string path)
    {
        try
        {
            File.ReadAllText(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [Fact]
    public async Task An_experiment_file_swapped_for_another_file_between_its_trust_check_and_its_read_is_refused()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("inodes are read with GNU stat here");
            return;
        }
        using var root = new ExperimentRoot();
        var path = Path.Combine(root.Root, "experiment.json");
        var seams = GateSeams.Real with
        {
            AfterRead = _ =>
            {
                var other = root.Write("other.json", File.ReadAllText(path));
                File.Move(other, path, overwrite: true);
                return Task.CompletedTask;
            },
        };

        var passed = await root.PassAsync(seams, "status");

        passed.ExitCode.Should().Be(4, passed.Error);
        passed.Error.Should().Contain("experiment.json changed between its trust check and its read").And.Contain("inode").And.Contain("D17");
        passed.Calls.Should().BeEmpty("what was read is not what was judged");
    }

    [Fact]
    public async Task An_experiment_file_that_becomes_a_link_after_the_read_is_refused_as_a_link()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("links are read with GNU stat here");
            return;
        }
        using var root = new ExperimentRoot();
        var path = Path.Combine(root.Root, "experiment.json");
        var seams = GateSeams.Real with
        {
            AfterRead = _ =>
            {
                File.Move(path, Path.Combine(root.Temp, "moved.json"));
                File.CreateSymbolicLink(path, Path.Combine(root.Temp, "moved.json"));
                return Task.CompletedTask;
            },
        };

        var passed = await root.PassAsync(seams, "status");

        passed.ExitCode.Should().Be(4);
        passed.Error.Should().Contain("symbolic link");
        passed.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_file_that_does_not_change_passes_the_second_look_and_the_hook_sees_the_judged_path()
    {
        using var root = new ExperimentRoot();
        var seen = new List<string>();
        var seams = GateSeams.Real with { AfterRead = path => { seen.Add(path); return Task.CompletedTask; } };

        var passed = await root.PassAsync(seams, "status");

        passed.ExitCode.Should().Be(0, passed.Error);
        seen.Should().Equal(Path.Combine(root.Root, "experiment.json"));
    }

    [Fact]
    public async Task An_experiment_file_that_is_a_folder_is_refused_with_exit_4_naming_what_it_is()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file kinds are read with GNU stat here");
            return;
        }
        using var root = new ExperimentRoot();
        var path = Path.Combine(root.Root, "experiment.json");
        File.Delete(path);
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var passed = await root.PassAsync("status");

        passed.ExitCode.Should().Be(4, passed.Error);
        passed.Error.Should().Contain("is a directory, not a regular file").And.Contain("experiment.json");
        passed.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task An_experiment_file_that_is_a_fifo_is_refused_without_blocking_the_gate()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("a FIFO is made with mkfifo here");
            return;
        }
        using var root = new ExperimentRoot();
        var path = Path.Combine(root.Root, "experiment.json");
        File.Delete(path);
        await MakeFifoAsync(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var pass = root.PassAsync("status");
        var finished = await Task.WhenAny(pass, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        if (finished != pass)
        {
            await using var release = new FileStream(path, FileMode.Open, FileAccess.Write);
        }

        finished.Should().BeSameAs(pass, "a FIFO read as a file blocks until somebody writes to it");
        var passed = await pass;
        passed.ExitCode.Should().Be(4, passed.Error);
        passed.Error.Should().Contain("is a FIFO, not a regular file");
        passed.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_file_the_host_could_not_read_and_a_file_that_is_refused_are_both_printed_and_the_exit_is_3()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("the trust check reads POSIX facts");
            return;
        }
        using var root = new ExperimentRoot();
        var seams = GateSeams.Real with
        {
            ReadFacts = (path, _, _) => Task.FromResult<FactsRead>(path == root.Root
                ? new FactsRead.Unread("stat -c: no such tool")
                : new FactsRead.Refused($"{path} is a symbolic link to 'elsewhere'")),
        };

        var passed = await root.PassAsync(seams, "status");

        passed.ExitCode.Should().Be(3, "the host could not say about one path, so the walk is environment");
        var lines = passed.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("stat -c: no such tool");
        lines[1].Should().Contain("symbolic link").And.Contain("experiment.json");
        passed.Calls.Should().BeEmpty();
    }

    private static async Task MakeFifoAsync(string path)
    {
        var start = new System.Diagnostics.ProcessStartInfo("mkfifo") { UseShellExecute = false };
        start.ArgumentList.Add(path);
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        process.ExitCode.Should().Be(0, "mkfifo makes the FIFO the test needs");
    }

    [GeneratedRegex("""\"(?<verb>[a-z][a-z-]*)" =>""")]
    private static partial Regex Arm();
}
