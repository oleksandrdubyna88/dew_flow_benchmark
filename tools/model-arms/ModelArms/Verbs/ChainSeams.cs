using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>What <c>chain</c> is handed that production never changes (E5.S4): the verbs it calls in process over its one <see cref="ExperimentInput"/> (D4), how it asks whether an arm runs, how it keeps the host awake, what it sleeps with and what it reads the time from. Internal: a console that could be given another <c>Start</c> could skip the isolation the chain exists to hold. The run's own lock is NOT a seam —
/// it is always the real <see cref="FileGateLock"/> (as the runner's own lock is, E5 §8 item 35), so a test holds it with a real second process. Every elapsed span the chain uses is <see cref="Clock"/> now minus an instant this process recorded, so a test moves the clock inside <see cref="Tick"/> and nowhere else.</summary>
internal sealed record ChainSeams(
    Func<ExperimentInput, IReadOnlyList<string>, Task<int>> Make,
    Func<ExperimentInput, string, CancellationToken, Task<LockOutcome>> Lock,
    Func<ExperimentInput, CancellationToken, Task<UnlockOutcome>> Unlock,
    Func<ExperimentInput, IReadOnlyList<string>, Task<int>> Start,
    Func<ExperimentInput, string, Task<int>> Check,
    Func<RootPaths, string, RunnerState> Liveness,
    Func<ExperimentInput, HostKind, CancellationToken, Task<Passage<IHostAwake>>> Awake,
    Func<ExperimentInput, Task<HostKind>> Host,
    Func<TimeSpan, CancellationToken, Task> Tick,
    TimeProvider Clock)
{
    public static ChainSeams Real { get; } = new(
        MakeVerb.RunAsync,
        LockVerb.LockAsync,
        (input, stopping) => UnlockVerb.UnlockAsync(input, force: false, stopping),
        (input, arms) => RunAllVerb.StartAsync(input, arms, RunAllSeams.Real),
        (input, arm) => ChecksVerb.RunAsync(input with { Verb = input.Verb with { Verb = "checks", Positionals = [arm] } }),
        RunnerLiveness.Of,
        HostAwakes.StartAsync,
        HostFactsReader.HostKindAsync,
        Task.Delay,
        TimeProvider.System);
}
