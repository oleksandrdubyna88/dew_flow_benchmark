using Bench.Domain;
using Bench.Domain.Targets;
using Bench.Infrastructure.Process;

namespace Bench.Infrastructure.Git;

/// <summary>One command a gate runs — exe plus argv, NEVER a shell string, per the family's one
/// process rule. The CLI carries it semicolon-joined (<c>dotnet;build;src/App.csproj</c>) because a
/// space-split flag value breaks on the first path with a space in it.</summary>
public sealed record GateCommand(string Executable, IReadOnlyList<string> Arguments)
{
    public static Outcome<GateCommand> Parse(string semicolonJoined)
    {
        var parts = (semicolonJoined ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0
            ? Outcome<GateCommand>.Failure(
                "a gate command is exe;arg;arg — an empty one cannot prove anything about a tree")
            : Outcome<GateCommand>.Success(new GateCommand(parts[0], [.. parts.Skip(1)]));
    }

    public string Describe => $"{Executable} {string.Join(' ', Arguments)}".TrimEnd();
}

/// <summary>What the two gates observed. BOTH must hold for a task to be well-formed: red proves the
/// bug is live at base, green proves the reference fix is the cure — either alone proves nothing
/// (MEASURED_LESSONS §4b: a bug already fixed on HEAD, and a binary that still carried the fix, each
/// produced a task that would have measured noise).</summary>
public sealed record GateReport(bool RedAtBase, string RedDetail, bool GreenWithFix, string GreenDetail)
{
    public bool Passed => RedAtBase && GreenWithFix;

    public string Describe =>
        $"red at base: {(RedAtBase ? "YES" : "NO")} ({RedDetail}); green with fix: {(GreenWithFix ? "YES" : "NO")} ({GreenDetail})";
}

/// <summary>The two harvest gates, run ATTENDED in a disposable scratch worktree
/// (todo/PLAN_investigate_vs_implement.md §3.5; the isolation boundary that would make this
/// schedulable is PLAN_code_lane.md §4.2 and is deliberately not weakened here — this runs while an
/// operator watches, one task at a time).
/// <para>
/// RED: the base tree plus the fix's OWN test files (materialised with <c>git checkout fix -- tests</c>)
/// must fail. GREEN: the whole tree at the fix commit must pass. The shared per-commit worktree is
/// never built in — a build writes, and that tree is read-only by the family's oldest rule — so every
/// run gets its own worktree and removes it on every path out.
/// </para></summary>
public static class HarvestGates
{
    public static async Task<Outcome<GateReport>> RunAsync(
        string repositoryDir,
        CommitSha baseCommit,
        CommitSha fixCommit,
        IReadOnlyList<string> testPaths,
        GateCommand build,
        GateCommand test,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (testPaths.Count == 0)
        {
            return Outcome<GateReport>.Failure(
                "the fix carries no test file — there is no hidden test to be red at base, so the gates cannot prove the bug reproduces");
        }

        var scratch = Path.Combine(Path.GetTempPath(), "bench-gate", Guid.CreateVersion7().ToString("N"));

        var added = await GitCommand.RunAsync(
            repositoryDir, timeout, cancellationToken,
            "worktree", "add", "--detach", "--force", scratch, baseCommit.Value);

        if (added is Outcome<string>.Fail cannotAdd)
        {
            return Outcome<GateReport>.Failure($"could not create the gate worktree: {cannotAdd.Reason}");
        }

        try
        {
            return await GatesAsync(
                repositoryDir, scratch, fixCommit, testPaths, build, test, timeout, cancellationToken);
        }
        finally
        {
            // Best effort, on every path out: a leaked scratch worktree is disk litter AND a stale
            // entry in the repository's worktree list, so both halves are cleaned.
            await GitCommand.RunAsync(
                repositoryDir, timeout, CancellationToken.None, "worktree", "remove", "--force", scratch);
        }
    }

    private static async Task<Outcome<GateReport>> GatesAsync(
        string repositoryDir,
        string scratch,
        CommitSha fixCommit,
        IReadOnlyList<string> testPaths,
        GateCommand build,
        GateCommand test,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var testsIn = await GitCommand.RunAsync(
            scratch, timeout, cancellationToken,
            ["checkout", fixCommit.Value, "--", .. testPaths]);

        if (testsIn is Outcome<string>.Fail noTests)
        {
            return Outcome<GateReport>.Failure(
                $"could not materialise the fix's test files at base: {noTests.Reason}");
        }

        var red = await PhaseAsync(scratch, build, test, timeout, cancellationToken);

        if (red is Outcome<GatePhase>.Fail redBroken)
        {
            return Outcome<GateReport>.Failure(redBroken.Reason);
        }

        var toFix = await GitCommand.RunAsync(
            scratch, timeout, cancellationToken, "checkout", "--force", "--detach", fixCommit.Value);

        if (toFix is Outcome<string>.Fail cannotMove)
        {
            return Outcome<GateReport>.Failure($"could not move the gate worktree to the fix: {cannotMove.Reason}");
        }

        var green = await PhaseAsync(scratch, build, test, timeout, cancellationToken);

        return green.Match(
            greenPhase => Outcome<GateReport>.Success(Report(((Outcome<GatePhase>.Ok)red).Value, greenPhase)),
            Outcome<GateReport>.Failure);
    }

    /// <summary>Red is any failure — a test that fails, or a tree that will not even build with the
    /// fix's tests in it. The DETAIL says which, because red-by-compilation is the weak kind of
    /// evidence (a test using an API the fix introduces cannot run at base at all), and the operator
    /// reading the printout is the one who decides whether that red is good enough.</summary>
    private static GateReport Report(GatePhase red, GatePhase green) => new(
        RedAtBase: !red.BuildPassed || !red.TestsPassed,
        RedDetail: red.BuildPassed
            ? red.TestsPassed ? "the tests PASSED at base — the bug does not reproduce" : $"the tests failed, as a live bug should: {red.Detail}"
            : $"red by COMPILATION, the weak kind — the tree with the fix's tests does not build at base: {red.Detail}",
        GreenWithFix: green.BuildPassed && green.TestsPassed,
        GreenDetail: green.BuildPassed
            ? green.TestsPassed ? "build and tests green at the fix" : $"the tests still fail WITH the fix: {green.Detail}"
            : $"the fix does not build: {green.Detail}");

    private sealed record GatePhase(bool BuildPassed, bool TestsPassed, string Detail);

    private static async Task<Outcome<GatePhase>> PhaseAsync(
        string scratch, GateCommand build, GateCommand test, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var built = await RunAsync(scratch, build, timeout, cancellationToken);

        if (built is Outcome<ProcessResult>.Fail buildBroken)
        {
            return Outcome<GatePhase>.Failure(buildBroken.Reason);
        }

        var buildResult = ((Outcome<ProcessResult>.Ok)built).Value;

        if (!buildResult.Ok)
        {
            return Outcome<GatePhase>.Success(new GatePhase(false, false, Tail(buildResult.Output)));
        }

        var tested = await RunAsync(scratch, test, timeout, cancellationToken);

        return tested.Match(
            result => Outcome<GatePhase>.Success(new GatePhase(true, result.Ok, Tail(result.Output))),
            Outcome<GatePhase>.Failure);
    }

    /// <summary>A command that could not RUN is the environment's failure and fails the whole gate —
    /// distinct from a command that ran and exited non-zero, which is a verdict.</summary>
    private static async Task<Outcome<ProcessResult>> RunAsync(
        string scratch, GateCommand command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var attempt = await ProcessRunner.RunAsync(
            command.Executable, command.Arguments, scratch, timeout, cancellationToken);

        return attempt switch
        {
            ProcessAttempt.Completed done => Outcome<ProcessResult>.Success(done.Result),
            ProcessAttempt.NotFound missing => Outcome<ProcessResult>.Failure(
                $"'{missing.Executable}' is not installed on this machine — the gate cannot run"),
            ProcessAttempt.TimedOut cap => Outcome<ProcessResult>.Failure(
                $"'{command.Describe}' did not finish within {cap.Budget.TotalSeconds:0.#}s"),
            _ => Outcome<ProcessResult>.Failure($"'{command.Describe}' produced an attempt this gate cannot read"),
        };
    }

    private static string Tail(string output)
    {
        var trimmed = output.Trim();
        return trimmed.Length <= 300 ? trimmed : "…" + trimmed[^300..];
    }
}
