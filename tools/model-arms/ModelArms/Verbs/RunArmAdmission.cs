using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>The arm's scheme and name, once the experiment has said it runs here.</summary>
internal sealed record ArmChoice(string Arm, Scheme Scheme);

/// <summary>Where the arm lives: the map that holds its token and the paths that token names.</summary>
internal sealed record ArmPlaced(ArmChoice Choice, ArmMap Map, ArmPaths Paths);

/// <summary>A hold that holds nothing: what an <see cref="ArmPlan"/> carries between <c>Equip</c> and the end of admission, where the real two replace it.</summary>
internal sealed class NoHold : IDisposable
{
    public static NoHold Instance { get; } = new();

    public void Dispose()
    {
    }
}

/// <summary>The executables <c>run-arm</c> resolved: the REAL paths, printed and launched as one value (D17 c, d). <paramref name="Build"/> holds git and — only for a scheme with a <c>tasks</c> block, whose worktrees get a warm build (E4.S3) — the target's <c>lowPriority[0]</c> (<c>Node</c>) and <c>build[0]</c> (<c>Build</c>); every other member is the empty text.</summary>
internal sealed record ArmTools(string Claude, string Git, ToolPaths Build);

/// <summary>Everything the walk needs, once admission has let the arm in: <paramref name="ScenarioIds"/> are the ids of <c>scenarios.json</c> as the gate judged it (the task plan's every scenario must be delivered by a task), their count is what the briefs say. <see cref="IsolationHold"/> is the runner's SHARED hold of <c>.isolation.lock</c> and <see cref="RunnerLock"/> its own <c>pids/&lt;arm&gt;.lock</c> (E5.S1, D13): both live as long as the runner does — <c>RunArmVerb.RunAsync</c> disposes them after the walk, the kernel when the process dies.</summary>
internal sealed record ArmPlan(ExperimentInput Input, string Arm, Scheme Scheme, ArmMap Map, ArmPaths Paths, ArmTools Tools, IReadOnlyList<string> ScenarioIds, IDisposable IsolationHold, IDisposable RunnerLock)
{
    public int ScenarioCount => ScenarioIds.Count;
}

/// <summary>Admission for <c>run-arm</c>. <b>The runner's two locks are the first thing after the arm's name is known; nothing — the map, PATH, git — is read before them, and every refusal after them releases them</b> (E5.S1 questions 12 and 18): <c>&lt;root&gt;/pids</c> made by name, the SHARED <c>.isolation.lock</c> (so no <c>lock</c>/<c>unlock</c> changes a mode while this runner lives, and none can start
/// between an <c>unlock</c>'s last liveness probe and its restore), then the runner's own <c>pids/&lt;arm&gt;.lock</c> (D13: the one answer to whether the arm runs) and its pid (<c>pids/&lt;arm&gt;.pid</c>, the decimal pid and a newline). The isolation WARNING (question 20) follows the map and never refuses. Each refusal ends the walk, so a later check never runs on what an earlier one refused and nothing is launched before all of them have passed — the scheme (the arm's letters must be among the experiment's
/// schemes), the arm's token from <c>arms.map.json</c>, the PATH the child programs inherit and the executables that PATH resolves (<c>claude</c> and <c>git</c>; for a scheme with a <c>tasks</c> block also the target's <c>lowPriority[0]</c> and <c>build[0]</c>, E4.S3), and <c>scenarios.json</c> as the gate judged it. The PATH is judged before the
/// executables are looked up in it: git and claude look their own helpers up there for hours, and <c>validate</c>'s judgement is not carried over by magic. <c>HOME</c> and <c>XDG_CONFIG_HOME</c> are judged next, by the same helper <c>validate</c> uses (D26 c: git reads its global config there and a claude its settings and hooks). The one launch admission itself makes is <c>git --version</c> of the resolved git, from the experiment root, under the hardened environment (<see cref="LaunchAdmission.FloorAsync"/>, D23, E4.S0): a git below the floor is exit 3 before <c>scenarios.json</c> is counted. Exit 3 is the host (a CLI that is not there, a file that cannot be read), exit 4 a wrong configuration.</summary>
internal static class RunArmAdmission
{
    private const string Verb = "run-arm";
    private const string Name = "model-arms " + Verb;
    private static readonly TimeSpan LockPoll = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LockWaitPoll = TimeSpan.FromMilliseconds(250);

    public static async Task<Passage<ArmPlan>> AdmitAsync(ExperimentInput input)
    {
        var choice = Choose(input);
        if (choice is Passage<ArmChoice>.Stopped stopped)
        {
            return new Passage<ArmPlan>.Stopped(stopped.ExitCode, stopped.Lines);
        }

        var arm = ((Passage<ArmChoice>.Through)choice).Value.Arm;
        var held = await HoldAsync(input, arm);
        if (held is Passage<Holds>.Stopped refused)
        {
            return new Passage<ArmPlan>.Stopped(refused.ExitCode, refused.Lines);
        }

        var locks = ((Passage<Holds>.Through)held).Value;
        Passage<ArmPlan> admitted;
        try
        {
            admitted = await Task.FromResult(choice)
                .Then(each => Place(input, each))
                .Then(placed => Warned(input, placed))
                .Then(placed => Equip(input, placed))
                .ThenAsync(plan => FlooredAsync(plan))
                .ThenAsync(plan => CountedAsync(plan));
        }
        catch
        {
            locks.Dispose();
            throw;
        }

        if (admitted is Passage<ArmPlan>.Stopped)
        {
            locks.Dispose();
        }

        return admitted.Map(plan => plan with { IsolationHold = locks.Isolation, RunnerLock = locks.Runner });
    }

    /// <summary>The two held locks, disposed together.</summary>
    private sealed record Holds(IDisposable Isolation, IDisposable Runner) : IDisposable
    {
        public void Dispose()
        {
            Runner.Dispose();
            Isolation.Dispose();
        }
    }

    /// <summary>Questions 12 (a)–(d): <c>pids/</c> by name, the shared <c>.isolation.lock</c>, <c>pids/&lt;arm&gt;.lock</c> and the pid file. A probe's momentary hold of a lock is waited out for <see cref="RunnerLiveness.RunnerLockWaitMilliseconds"/>; the ceiling is 4 (the operator's stop, told apart by the verb's token, is 5). Everything taken is released when a later step refuses.</summary>
    private static async Task<Passage<Holds>> HoldAsync(ExperimentInput input, string arm)
    {
        var pids = RootFolders.Pids(input.Root);
        if (pids is Passage<string>.Stopped notMade)
        {
            return Passage<Holds>.Stop(notMade.ExitCode, [.. notMade.Lines.Select(line => $"{Name} {arm}: {line}")]);
        }

        var created = FileGateLock.Create(TimeProvider.System, LockWaitPoll);
        if (created is Outcome<FileGateLock>.Fail cannot)
        {
            return Passage<Holds>.Stop(ExitCodes.Environment, $"{Name} {arm}: {cannot.Reason}");
        }

        var gate = ((Outcome<FileGateLock>.Ok)created).Value;
        var isolation = await TakenAsync(input, arm, gate, input.Root.IsolationLock, () => gate.TakeShared(input.Root.IsolationLock), busy => $"{Name} {arm}: the isolation is being changed (.isolation.lock is held {busy}) — start the arm once lock or unlock has returned");
        if (isolation is Passage<IDisposable>.Stopped noIsolation)
        {
            return new Passage<Holds>.Stopped(noIsolation.ExitCode, noIsolation.Lines);
        }

        var shared = ((Passage<IDisposable>.Through)isolation).Value;
        var runner = await TakenAsync(input, arm, gate, input.Root.RunnerLock(arm), () => gate.Take(input.Root.RunnerLock(arm), create: true, note: true), busy => $"{Name} {arm}: another runner holds {input.Root.RunnerLock(arm)} — {busy}");
        if (runner is Passage<IDisposable>.Stopped noRunner)
        {
            shared.Dispose();
            return new Passage<Holds>.Stopped(noRunner.ExitCode, noRunner.Lines);
        }

        var holds = new Holds(shared, ((Passage<IDisposable>.Through)runner).Value);
        return PidWritten(input, arm) is Outcome<string>.Fail failed
            ? Disposed(holds, Passage<Holds>.Stop(ExitCodes.Environment, $"{Name} {arm}: the pid file could not be written: {failed.Reason}"))
            : new Passage<Holds>.Through(holds);
    }

    private static Passage<Holds> Disposed(Holds holds, Passage<Holds> stop)
    {
        holds.Dispose();
        return stop;
    }

    /// <summary>One take under the 2 s ceiling: <c>Held</c>, or the refusal in the verb's words. A busy lock past the ceiling is a held lock (4); the operator's stop is 5.</summary>
    private static async Task<Passage<IDisposable>> TakenAsync(ExperimentInput input, string arm, FileGateLock gate, string path, Func<GateLockAnswer> take, Func<string, string> busy)
    {
        using var ceiling = CancellationTokenSource.CreateLinkedTokenSource(input.Verb.Stopping);
        ceiling.CancelAfter(TimeSpan.FromMilliseconds(RunnerLiveness.RunnerLockWaitMilliseconds));
        try
        {
            return await gate.AcquireAnswerAsync(path, ceiling.Token, take) switch
            {
                GateLockAnswer.Held held => new Passage<IDisposable>.Through(held.Handle),
                GateLockAnswer.Refused refused => Passage<IDisposable>.Stop(ExitCodes.Configuration, $"{Name} {arm}: {refused.Reason}"),
                GateLockAnswer.Unread unread => Passage<IDisposable>.Stop(ExitCodes.Environment, $"{Name} {arm}: {unread.Reason}"),
                _ => Passage<IDisposable>.Stop(ExitCodes.Environment, $"{Name} {arm}: {path} could not be taken"),
            };
        }
        catch (OperationCanceledException) when (input.Verb.Stopping.IsCancellationRequested)
        {
            return Passage<IDisposable>.Stop(ExitCodes.NoReport, $"{Name} {arm}: stopped while waiting for {path} — run the same command again");
        }
        catch (OperationCanceledException)
        {
            return Passage<IDisposable>.Stop(ExitCodes.Configuration, busy(gate.Describe(path)));
        }
    }

    /// <summary>The runner names itself (D13): <c>pids/&lt;arm&gt;.pid</c> holds this process's id as decimal digits and one newline, staged beside and renamed over — by name, never through a write that lists the folder.</summary>
    private static Outcome<string> PidWritten(ExperimentInput input, string arm)
    {
        try
        {
            ArmFiles.ReplaceFile(input.Root.PidFile(arm), Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
            return Outcome<string>.Success(input.Root.PidFile(arm));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<string>.Failure(ex.Message);
        }
    }

    /// <summary>Question 20 (the coordinator's ruling 3): the arm's run has an isolation block and its isolation is not applied — ONE line on stderr, the arm runs on. An arm no run names, a run whose plan is empty, or a plan that cannot be made (its words, once) print no warning of this kind. Never a refusal: the unattended path's refusal is the chain's marker reconciliation.</summary>
    private static Passage<ArmPlaced> Warned(ExperimentInput input, ArmPlaced placed)
    {
        var arm = placed.Choice.Arm;
        if (input.Experiment.Runs.FirstOrDefault(run => run.Rounds.Any(round => round.Contains(arm))) is not { } spec)
        {
            return new Passage<ArmPlaced>.Through(placed);
        }

        switch (LockVerb.PlanFor(input, spec, placed.Map))
        {
            case Passage<IsolationPlan?>.Stopped stopped:
                input.Verb.Error.WriteLine($"{Name} {arm}: the isolation of run {spec.Name} cannot be planned, so it is not checked: {string.Join(' ', stopped.Lines)}");
                break;
            case Passage<IsolationPlan?>.Through { Value: { } plan }:
                var state = IsolationFiles.Read(input.Root, PlatformFactsReader.CurrentRoad).State;
                if (state is not LockState.Applied applied || !string.Equals(applied.Run, plan.Run, StringComparison.Ordinal) || !string.Equals(applied.Digest, plan.Digest, StringComparison.Ordinal))
                {
                    input.Verb.Error.WriteLine($"{Name} {arm}: run {plan.Run} has an isolation block and its isolation is not applied ({IsolationLine.Of(state)}) — the arms can list each other's sessions; run `model-arms lock {plan.Run}`");
                }

                break;
        }

        return new Passage<ArmPlaced>.Through(placed);
    }

    /// <summary>The root's own gate lock, or the one a test handed in; a host that cannot lock is exit 3 before anything is taken.</summary>
    public static Outcome<IGateLock> LockFor(RunArmSeams seams) =>
        seams.Lock is { } given
            ? Outcome<IGateLock>.Success(given)
            : FileGateLock.Create(TimeProvider.System, LockPoll).Match(created => Outcome<IGateLock>.Success(created), Outcome<IGateLock>.Failure);

    private static Passage<ArmChoice> Choose(ExperimentInput input) => ArmChoices.Of(input, Verb);

    private static Passage<ArmPlaced> Place(ExperimentInput input, ArmChoice choice)
    {
        try
        {
            return MapOf(input, choice, File.ReadAllText(input.Root.ArmMapFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<ArmPlaced>.Stop(ExitCodes.Environment, $"{Name}: {input.Root.ArmMapFile} cannot be read: {ex.Message}");
        }
    }

    private static Passage<ArmPlaced> MapOf(ExperimentInput input, ArmChoice choice, string json) =>
        ArmMap.Read(json).Match(
            map => map.TokenOf(choice.Arm).Match(
                token => new Passage<ArmPlaced>.Through(new ArmPlaced(choice, map, input.Root.Arm(token))),
                reason => Passage<ArmPlaced>.Stop(ExitCodes.Configuration, $"{Name}: {reason}")),
            reason => Passage<ArmPlaced>.Stop(ExitCodes.Configuration, $"{Name}: arms.map.json is refused: {reason}"));

    private static Passage<ArmPlan> Equip(ExperimentInput input, ArmPlaced placed)
    {
        var environment = input.Verb.Environment;
        var roots = ExecutableRule.ForbiddenRoots(input.Experiment.Target.Source, input.Experiment.Root);
        var refusal = LaunchAdmission.FirstRefusal(environment, roots);
        return refusal.Length > 0
            ? Passage<ArmPlan>.Stop(ExitCodes.Configuration, $"{Name}: {refusal}")
            : LaunchAdmission.Resolve(Verb, "claude", environment, roots)
                .Bind(claude => LaunchAdmission.Resolve(Verb, "git", environment, roots)
                    .Bind(git => BuildTools(input, placed.Choice.Scheme, git, roots).Map(build => Planned(input, placed, new ArmTools(claude, git, build)))));
    }

    /// <summary>What the worktrees' warm build starts (E4.S3): the target's wrapper interpreter and its build program, resolved like the others — and only for a scheme that has worktrees. Another scheme resolves nothing it will not launch (a host without <c>dotnet</c> still runs C).</summary>
    private static Passage<ToolPaths> BuildTools(ExperimentInput input, Scheme scheme, string git, IReadOnlyList<string> roots)
    {
        var none = new ToolPaths(git, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        if (!scheme.Entries.Any(entry => entry is SchemeEntry.Tasks))
        {
            return new Passage<ToolPaths>.Through(none);
        }

        var target = input.Experiment.Target;
        var environment = input.Verb.Environment;
        return LaunchAdmission.Resolve(Verb, target.LowPriority[0], environment, roots)
            .Bind(node => LaunchAdmission.Resolve(Verb, target.Build[0], environment, roots).Map(build => none with { Node = node, Build = build }));
    }

    private static ArmPlan Planned(ExperimentInput input, ArmPlaced placed, ArmTools tools) =>
        new(input, placed.Choice.Arm, placed.Choice.Scheme, placed.Map, placed.Paths, tools, ScenarioIds: [], NoHold.Instance, NoHold.Instance);

    /// <summary>D23: the resolved git's version against the floor, before <c>scenarios.json</c> is counted and before the gate lock (which <c>RunArm.WalkAsync</c> takes only after admission) — a git below the floor runs no repository operation.</summary>
    private static async Task<Passage<ArmPlan>> FlooredAsync(ArmPlan plan) =>
        (await LaunchAdmission.FloorAsync(Verb, plan.Tools.Git, plan.Input.Verb.Environment, plan.Input.Root.Root, plan.Input.Verb.Stopping)).Map(_ => plan);

    /// <summary><c>scenarios.json</c> as the gate judged it (the same road <c>validate</c> reads it by): its count is what the briefs say. A file that does not read is exit 4 naming why.</summary>
    private static async Task<Passage<ArmPlan>> CountedAsync(ArmPlan plan)
    {
        var read = await HostFactsReader.ScenariosAsync(plan.Input, GateSeams.Real);
        return read.Bind(scenarios => scenarios.Match(
            list => new Passage<ArmPlan>.Through(plan with { ScenarioIds = [.. list.Select(scenario => scenario.Id)] }),
            reason => Passage<ArmPlan>.Stop(ExitCodes.Configuration, $"{Name}: {reason}")));
    }
}
