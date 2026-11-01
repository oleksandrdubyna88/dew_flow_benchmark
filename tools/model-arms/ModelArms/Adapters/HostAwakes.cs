using Bench.Cli;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;
using ModelArms.Verbs;

namespace ModelArms.Adapters;

/// <summary>The keep-awake factory (D12, E5.S2): the adapter <see cref="HostAwakeSelection"/> picks for the experiment's <c>awake</c> and the host, started, and the ONE place that writes <c>keep-awake: &lt;Describe&gt;</c> (the adapters write nothing of the kind). A named adapter the host cannot run is exit 4 with <c>validate</c>'s own sentence (<c>chain</c> does not run <c>validate</c>);
/// the WSL road needs <c>pwsh.exe</c> resolved to a real path (exit 3 absent, 4 refused, D17), the module's script beside the binary, and <c>WSL_DISTRO_NAME</c> to write that script's path for Windows (exit 3 each). Sentences are prefixed with the running verb's name. <c>stopping</c> reaches every adapter's start. The caller disposes the result when the scope ends.</summary>
internal static class HostAwakes
{
    public static async Task<Passage<IHostAwake>> StartAsync(ExperimentInput input, HostKind host, CancellationToken stopping)
    {
        stopping.ThrowIfCancellationRequested();
        var passage = await HostAwakeSelection.For(input.Experiment.Awake, host).Match(
            kind => StartAsync(input, kind, stopping),
            reason => Task.FromResult(Passage<IHostAwake>.Stop(ExitCodes.Configuration, $"{NameOf(input)}: {reason}")));
        return passage.Map(awake =>
        {
            input.Verb.Output.WriteLine($"keep-awake: {awake.Describe}");
            return awake;
        });
    }

    private static string NameOf(ExperimentInput input) => "model-arms " + input.Verb.Verb;

    private static async Task<Passage<IHostAwake>> StartAsync(ExperimentInput input, AwakeKind kind, CancellationToken stopping) =>
        kind switch
        {
            AwakeKind.Windows => await WindowsAsync(input, stopping),
            AwakeKind.Wsl => await WslAsync(input, stopping),
            _ => new Passage<IHostAwake>.Through(new NoAwake(input.Experiment.Awake == AwakeChoice.None ? "awake is \"none\"" : "this host is neither Windows nor WSL")),
        };

    private static async Task<Passage<IHostAwake>> WindowsAsync(ExperimentInput input, CancellationToken stopping) =>
        OperatingSystem.IsWindows()
            ? new Passage<IHostAwake>.Through(await WindowsAwake.StartAsync(stopping))
            : Passage<IHostAwake>.Stop(ExitCodes.Environment, $"{NameOf(input)}: the Windows keep-awake is a Windows call and this is not Windows");

    private static Task<Passage<IHostAwake>> WslAsync(ExperimentInput input, CancellationToken stopping)
    {
        var environment = input.Verb.Environment;
        var roots = ExecutableRule.ForbiddenRoots(input.Experiment.Target.Source, input.Experiment.Root);
        var admitted = LaunchAdmission.Resolve(input.Verb.Verb, "pwsh.exe", environment, roots).Bind(pwsh =>
            ScriptOf(input).Bind(script =>
                WslPaths.Windows(Path.GetFullPath(script), environment.GetValueOrDefault("WSL_DISTRO_NAME", string.Empty)).Match(
                    windowsPath => new Passage<(string Pwsh, string Script)>.Through((pwsh, windowsPath)),
                    reason => Passage<(string Pwsh, string Script)>.Stop(ExitCodes.Environment, $"{NameOf(input)}: {reason}"))));
        return admitted.Match(
            through => BeginAsync(input, through.Pwsh, through.Script, stopping),
            stop => Task.FromResult<Passage<IHostAwake>>(new Passage<IHostAwake>.Stopped(stop.ExitCode, stop.Lines)));
    }

    private static Passage<string> ScriptOf(ExperimentInput input) =>
        File.Exists(input.Verb.Module.KeepAwakeScript)
            ? new Passage<string>.Through(input.Verb.Module.KeepAwakeScript)
            : Passage<string>.Stop(ExitCodes.Environment, $"{NameOf(input)}: {input.Verb.Module.KeepAwakeScript} is not beside the binary — the module's keep-awake script is missing");

    private static async Task<Passage<IHostAwake>> BeginAsync(ExperimentInput input, string pwsh, string windowsPath, CancellationToken stopping)
    {
        var awake = new WslHostAwake(
            pwsh, windowsPath, input.Root.Root, WslHostAwake.EnvironmentOf(input.Verb.Environment), input.Root, TimeProvider.System, (span, ct) => Task.Delay(span, ct), input.Verb.Output);
        return (await awake.StartAsync(stopping)).Match<Passage<IHostAwake>>(
            _ => new Passage<IHostAwake>.Through(awake),
            reason => Passage<IHostAwake>.Stop(ExitCodes.Environment, $"{NameOf(input)}: {reason}"));
    }
}
