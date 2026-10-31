using Bench.Cli;
using Bench.Infrastructure.Process;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>The keep-awake factory (D12, E5.S2): which adapter an experiment's <c>awake</c> and the host's kind start, and the exit code and sentence of each refusal. The WSL road starts the fake <c>pwsh.exe</c> (a link to <c>tests/FakeCli</c> in <c>hang</c> mode) by the path the resolution found; the real one is never run.
/// <para>
/// What this does NOT prove: the Windows adapter (<c>WindowsAwakeTests</c>, on Windows only) or a real <c>pwsh.exe</c>.
/// </para></summary>
public sealed class HostAwakesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The input the gate hands <c>validate</c>, with <c>awake</c> as given, the verb's environment replaced and its output a fresh writer.</summary>
    private static async Task<(ModelArmsApp.ExperimentInput Input, StringWriter Output)> InputAsync(ExperimentRoot root, string awake, IReadOnlyDictionary<string, string> environment)
    {
        root.Edit(top => top["awake"] = awake);
        var seen = (await root.PassAsync("validate")).Calls.Single();
        var output = new StringWriter();
        return (seen with { Verb = seen.Verb with { Environment = environment, Output = output } }, output);
    }

    private static string Text(ModelArmsApp.Passage<IHostAwake> passage) => passage.Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Stopped>().Subject.Lines.Single();

    [Fact]
    public async Task The_factory_refuses_a_mismatch_with_validates_sentence_and_says_none_once()
    {
        using var root = new ExperimentRoot();
        var (wsl, _) = await InputAsync(root, "wsl", root.Environment);

        var mismatch = await HostAwakes.StartAsync(wsl, HostKind.OtherPosix, Ct);

        var stopped = mismatch.Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Stopped>().Subject;
        stopped.ExitCode.Should().Be(4, "a configuration the host cannot run is exit 4");
        stopped.Lines.Single().Should().Contain("needs a Wsl host").And.Contain("this host is OtherPosix").And.Contain("model-arms chain");

        var (none, output) = await InputAsync(root, "none", root.Environment);
        var quiet = await HostAwakes.StartAsync(none, HostKind.Wsl, Ct);

        await using var awake = quiet.Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Through>().Subject.Value;
        awake.Should().BeOfType<NoAwake>();
        output.ToString().Split("keep-awake: none — awake is \"none\"").Should().HaveCount(2, "said once");

        var (auto, other) = await InputAsync(root, "auto", root.Environment);
        await using var other1 = (await HostAwakes.StartAsync(auto, HostKind.OtherPosix, Ct)).Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Through>().Subject.Value;
        other.ToString().Should().Contain("keep-awake: none — this host is neither Windows nor WSL");
    }

    [Fact]
    public async Task A_wsl_host_without_its_distribution_name_is_exit_3_naming_it()
    {
        using var root = new ExperimentRoot();
        await root.WithTargetAsync();
        var environment = root.Environment.Where(variable => variable.Key != "WSL_DISTRO_NAME").ToDictionary(variable => variable.Key, variable => variable.Value);
        var (input, _) = await InputAsync(root, "wsl", environment);

        var passage = await HostAwakes.StartAsync(input, HostKind.Wsl, Ct);

        var stopped = passage.Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Stopped>().Subject;
        stopped.ExitCode.Should().Be(3);
        stopped.Lines.Single().Should().Contain("WSL_DISTRO_NAME");
    }

    [Fact]
    public async Task A_wsl_host_without_pwsh_exe_is_exit_3()
    {
        using var root = new ExperimentRoot();
        await root.WithTargetAsync();
        File.Delete(Path.Combine(root.Temp, "bin", "pwsh.exe"));
        var (input, _) = await InputAsync(root, "wsl", new Dictionary<string, string>(root.Environment) { ["WSL_DISTRO_NAME"] = "Ubuntu" });

        var passage = await HostAwakes.StartAsync(input, HostKind.Wsl, Ct);

        passage.Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Stopped>().Which.ExitCode.Should().Be(3);
        Text(passage).Should().Contain("pwsh.exe");
    }

    [Fact]
    public async Task The_wsl_road_starts_the_resolved_pwsh_with_the_windows_path_and_ends_it_on_dispose()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the stand-in is a POSIX script");
        using var root = new ExperimentRoot();
        await root.WithTargetAsync();
        var argv = Path.Combine(root.Temp, "pwsh-argv.txt");
        var pwsh = Path.Combine(root.Temp, "bin", "pwsh.exe");
        File.WriteAllText(pwsh, $"#!/bin/sh\nprintf '%s\\n' \"$@\" > '{argv}'\nwhile read -r _line; do :; done\n");
        var (input, output) = await InputAsync(root, "wsl", new Dictionary<string, string>(root.Environment) { ["WSL_DISTRO_NAME"] = "Ubuntu" });

        var passage = await HostAwakes.StartAsync(input, HostKind.Wsl, Ct);

        var awake = passage.Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Through>(passage.ToString()).Subject.Value;
        awake.Should().BeOfType<WslHostAwake>();
        var pid = ((WslHostAwake)awake).ChildProcessId!.Value;
        WorkerLiveness.ProcessIsAlive(pid).Should().BeTrue("the stand-in holds its stdin open until the scope ends");
        output.ToString().Should().Contain("keep-awake: pwsh.exe \\\\wsl.localhost\\Ubuntu").And.Contain("keep-awake.ps1, held, checked every 2 min");

        await awake.DisposeAsync();

        WorkerLiveness.ProcessIsAlive(pid).Should().BeFalse();
        var arguments = File.ReadAllLines(argv);
        arguments.Should().Equal("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", arguments[^1]);
        arguments[^1].Should().StartWith(@"\\wsl.localhost\Ubuntu\").And.EndWith(@"\windows\keep-awake.ps1");
    }

    [Fact]
    public async Task The_windows_adapter_off_windows_is_exit_3()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "off Windows only");
        using var root = new ExperimentRoot();
        var (input, _) = await InputAsync(root, "auto", root.Environment);

        var passage = await HostAwakes.StartAsync(input, HostKind.Windows, Ct);

        passage.Should().BeOfType<ModelArmsApp.Passage<IHostAwake>.Stopped>().Which.ExitCode.Should().Be(3);
    }
}
