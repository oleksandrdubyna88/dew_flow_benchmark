using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary><c>chain &lt;run&gt;</c> (parent §4.7, D27, D20; <c>after6.sh</c>, <c>after7.sh</c>; E5.S4): one named run, unattended, from <c>make</c> to <c>checks</c> — make the run's arms, lock the isolation, start each round's arms detached through <c>run-all</c> and wait for them, unlock, check every arm — with the stage markers under <c>chain/&lt;run&gt;/</c>, the host kept awake for the whole of it (D12), and the decision of what to do next
/// <see cref="Core.Chain.ChainPlan"/>'s, pure. The markers have ONE writer, this chain, and it is made true by the run's own lock (<c>chain/&lt;run&gt;/.chain.lock</c>, <see cref="ChainRunLock"/>): taken once, before any side effect, never waited for, held for the whole invocation — a second chain of the same run is exit 4 naming the holder. A chain of ANOTHER run is not refused; the two meet at <c>run-all</c>'s queue and at <see cref="Core.Chain.ChainAction.StopOtherRunAlive"/>.
/// <para>
/// <b>In order:</b> (1) the run is one of <c>experiment.json</c>'s (4); (2) the run's lock; (3) <c>pids/</c> made by name; (4) the PATH and configuration folders, every executable the experiment needs and <c>setsid</c> and the module resolved to REAL paths, printed and written whole to <c>chain/executables.txt</c> BEFORE the first stage (D17 c), then the isolation line; (5) the keep-awake scope; (6) the loop of <see cref="ChainWalk"/>. <b>The failure path (D11, parent §4.7):</b> the chain unlocks ONLY through its own <c>Unlock</c> stage — every other
/// exit, an exception and the operator's stop included, leaves the isolation applied, stops no runner (this type holds no member that signals a process) and prints <see cref="Trailer"/>: the isolation line and what to do. Exit 0 the run is done; 3 the host; 4 configuration (an unknown run, the run's lock held, a marker over a snapshot it does not match, a refusal of a verb it called); 5 stopped and resumable (an arm died, a bounded wait ran out, an arm of another run is alive, <c>unlock</c> refused, the operator's stop).
/// </para>
/// <para>
/// D26 (a)–(f) for the chain's own doing: (a) every path it opens is the root's own and named — the lock by <see cref="FileGateLock"/>'s judgement, the markers and <c>chain/executables.txt</c> by <see cref="ArmFiles.ReplaceFile"/> (stage and rename by name), <c>pids/</c> by <see cref="RootFolders"/>; no folder is ever listed (the root is at 300 while the isolation stands); (b) it launches nothing itself — <c>run-all</c> launches the runners by the real path it resolves, <c>checks</c> and <c>make</c> their tools; (c) the PATH and configuration folders are judged before any resolution; (d)–(f) as the verbs it calls.
/// </para>
/// <para>
/// What this does NOT do: hold SRS arms before E4.S3 (its <c>Start</c> stops at <c>run-all</c>'s refusal, exit 4); the consumer check (E6); stop or kill a runner (never).
/// </para></summary>
internal static class ChainVerb
{
    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, ChainSeams.Real);

    internal static async Task<int> RunAsync(ExperimentInput input, ChainSeams seams)
    {
        var run = input.Verb.Positionals[0];
        if (input.Experiment.Runs.FirstOrDefault(each => each.Name == run) is not { } spec)
        {
            input.Verb.Error.WriteLine($"model-arms chain: there is no run {ExecutableRule.Printable(run)} — the runs are {string.Join(", ", input.Experiment.Runs.Select(each => each.Name))}");
            return ExitCodes.Configuration;
        }

        var taken = ChainRunLock.Take(input, run);
        if (taken is Passage<IDisposable>.Stopped refused)
        {
            return refused.Print(input.Verb.Error);
        }

        using var handle = ((Passage<IDisposable>.Through)taken).Value;
        var finished = false;
        try
        {
            var code = await HeldAsync(input, seams, spec);
            finished = code == ExitCodes.Pass;
            return code;
        }
        finally
        {
            if (!finished)
            {
                Trailer(input, run);
            }
        }
    }

    /// <summary>Steps 3 to 6, under the run's lock.</summary>
    private static async Task<int> HeldAsync(ExperimentInput input, ChainSeams seams, RunSpec spec)
    {
        var host = await seams.Host(input);
        var ready = ChainAdmission.Admit(input, spec, host, seams.Detached);
        if (ready is Passage<string>.Stopped stop)
        {
            return stop.Print(input.Verb.Error);
        }

        var started = await seams.Awake(input, host, input.Verb.Stopping);
        if (started is Passage<IHostAwake>.Stopped awakeStop)
        {
            return awakeStop.Print(input.Verb.Error);
        }

        await using var awake = ((Passage<IHostAwake>.Through)started).Value;
        return await new ChainWalk(input, seams, spec).RunAsync();
    }

    /// <summary>On every exit that is not 0 once the run's lock is held: the isolation as it stands now (read again) and the reminder that nothing was undone.</summary>
    private static void Trailer(ExperimentInput input, string run)
    {
        var error = input.Verb.Error;
        error.WriteLine($"isolation: {IsolationLine.Of(IsolationFiles.State(input.Root))}");
        error.WriteLine($"model-arms chain {run}: the isolation is left as it stands and no runner was stopped; the arms that outlive this chain are not kept awake — to abandon the run, model-arms unlock once no arm is alive");
        error.Flush();
    }
}
