using Bench.Cli;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;
using ModelArms.Verbs;

namespace ModelArms.Adapters;

/// <summary>The keep-awake factory (D12, E5.S2): the adapter <see cref="HostAwakeSelection"/> picks for the experiment's <c>awake</c> and the host, started and ready, each printing one line <c>keep-awake: &lt;Describe&gt;</c>. A named adapter the host cannot run is exit 4 with <c>validate</c>'s own sentence (<c>chain</c> does not run <c>validate</c>);
/// the WSL road needs <c>pwsh.exe</c> resolved to a real path (exit 3 absent, 4 refused, D17), the module's script beside the binary, and <c>WSL_DISTRO_NAME</c> to write that script's path for Windows (exit 3 each). The caller disposes the result when the scope ends.</summary>
internal static class HostAwakes
{
    private const string Verb = "chain";
    private const string Name = "model-arms " + Verb;

    public static Task<Passage<IHostAwake>> StartAsync(ExperimentInput input, HostKind host, CancellationToken stopping)
    {
        stopping.ThrowIfCancellationRequested();
        return HostAwakeSelection.For(input.Experiment.Awake, host).Match(
            kind => StartAsync(input, kind),
            reason => Task.FromResult(Passage<IHostAwake>.Stop(ExitCodes.Configuration, $"{Name}: {reason}")));
    }

    private static async Task<Passage<IHostAwake>> StartAsync(ExperimentInput input, AwakeKind kind) =>
        kind switch
        {
            AwakeKind.Windows => await WindowsAsync(input.Verb.Output),
            AwakeKind.Wsl => Wsl(input),
            _ => new Passage<IHostAwake>.Through(new NoAwake(input.Experiment.Awake == AwakeChoice.None ? "awake is \"none\"" : "this host is neither Windows nor WSL", input.Verb.Output)),
        };

    private static async Task<Passage<IHostAwake>> WindowsAsync(TextWriter output) =>
        OperatingSystem.IsWindows()
            ? new Passage<IHostAwake>.Through(await WindowsAwake.StartAsync(output))
            : Passage<IHostAwake>.Stop(ExitCodes.Environment, $"{Name}: the Windows keep-awake is a Windows call and this is not Windows");

    private static Passage<IHostAwake> Wsl(ExperimentInput input)
    {
        var environment = input.Verb.Environment;
        var roots = ExecutableRule.ForbiddenRoots(input.Experiment.Target.Source, input.Experiment.Root);
        return LaunchAdmission.Resolve(Verb, "pwsh.exe", environment, roots).Bind(pwsh =>
            ScriptOf(input).Bind(script =>
                WslPaths.Windows(Path.GetFullPath(script), environment.GetValueOrDefault("WSL_DISTRO_NAME", string.Empty)).Match(
                    windowsPath => Begin(input, pwsh, windowsPath),
                    reason => Passage<IHostAwake>.Stop(ExitCodes.Environment, $"{Name}: {reason}"))));
    }

    private static Passage<string> ScriptOf(ExperimentInput input) =>
        File.Exists(input.Verb.Module.KeepAwakeScript)
            ? new Passage<string>.Through(input.Verb.Module.KeepAwakeScript)
            : Passage<string>.Stop(ExitCodes.Environment, $"{Name}: {input.Verb.Module.KeepAwakeScript} is not beside the binary — the module's keep-awake script is missing");

    private static Passage<IHostAwake> Begin(ExperimentInput input, string pwsh, string windowsPath)
    {
        var awake = new WslHostAwake(
            pwsh, windowsPath, input.Root.Root, WslHostAwake.EnvironmentOf(input.Verb.Environment), input.Root, TimeProvider.System, (span, ct) => Task.Delay(span, ct), input.Verb.Output);
        return awake.Start().Match<Passage<IHostAwake>>(
            _ => new Passage<IHostAwake>.Through(awake),
            reason => Passage<IHostAwake>.Stop(ExitCodes.Environment, $"{Name}: {reason}"));
    }
}
