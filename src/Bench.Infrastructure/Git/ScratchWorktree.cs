using Bench.Domain;
using Bench.Domain.Targets;
using Bench.Infrastructure.Process;

namespace Bench.Infrastructure.Git;

/// <summary>A disposable worktree at a pinned commit, and its removal on every path out — extracted
/// from <see cref="HarvestGates"/> when <see cref="SolutionSignals"/> needed the identical scaffold:
/// the shared per-commit tree is read-only by the family's oldest rule, so everything that BUILDS gets
/// its own tree and gives it back, registry entry included.</summary>
public static class ScratchWorktree
{
    public static Task<Outcome<string>> AddAsync(
        string repositoryDir, CommitSha at, TimeSpan timeout, CancellationToken cancellationToken) =>
        AddAsync(repositoryDir, at, timeout, cancellationToken,
            Path.Combine(Path.GetTempPath(), "bench-scratch"));

    /// <summary>Under an explicit root — the CLI-subject worktrees live under the CHECKOUT root, because
    /// <see cref="Models.WorkspaceTrust"/> may only trust paths under it, and a worktree it cannot trust
    /// is a fifteen-minute hang wearing a permissions dialog.</summary>
    public static async Task<Outcome<string>> AddAsync(
        string repositoryDir, CommitSha at, TimeSpan timeout, CancellationToken cancellationToken, string underRoot)
    {
        var scratch = Path.Combine(underRoot, Guid.CreateVersion7().ToString("N"));

        var added = await GitCommand.RunAsync(
            repositoryDir, timeout, cancellationToken,
            "worktree", "add", "--detach", "--force", scratch, at.Value);

        return added.Match(
            _ => Outcome<string>.Success(scratch),
            reason => Outcome<string>.Failure($"could not create a scratch worktree: {reason}"));
    }

    /// <summary>Cleanup's own ceiling. A worktree remove is seconds; the review found callers handing
    /// this their FULL leg or gate budget (up to fifteen minutes), so a hung remove could block the
    /// single-threaded drain long past the shutdown grace it advertises. Bounded here, once.</summary>
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Best effort, never cancellable: a leaked worktree is disk litter AND a stale entry in
    /// the repository's worktree list, and cleanup must survive the operator's Ctrl+C.</summary>
    public static Task RemoveAsync(string repositoryDir, string scratch) =>
        GitCommand.RunAsync(repositoryDir, CleanupTimeout, CancellationToken.None, "worktree", "remove", "--force", scratch);
}

/// <summary>ONE ceiling for a whole gate run, spent across its steps — never one budget per step.
/// <para>
/// The review measured the alternative: `--gate-timeout-seconds 900` applied independently to each of
/// the ~8 sequential steps of a gate run is a fifteen-minute flag whose real worst case is two hours.
/// This is `LegDeadline`'s discipline one layer down: every wait inside the run shares the ceiling,
/// and a slow early step eats the later steps' time rather than extending the total. The floor of one
/// second keeps a spent deadline failing FAST on the next step instead of dividing by zero.
/// </para></summary>
public sealed class GateDeadline(TimeSpan total)
{
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    public TimeSpan Remaining()
    {
        var left = total - _clock.Elapsed;

        return left > TimeSpan.FromSeconds(1) ? left : TimeSpan.FromSeconds(1);
    }
}

/// <summary>Running one gate command and reading what it said — the shared half of every attended
/// executor here. A command that could not RUN is the environment's failure; one that ran and exited
/// non-zero is a VERDICT, and the two must never merge.</summary>
public static class GateProcess
{
    public static async Task<Outcome<ProcessResult>> RunAsync(
        string workingDirectory, GateCommand command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var attempt = await ProcessRunner.RunAsync(
            command.Executable, command.Arguments, workingDirectory, timeout, cancellationToken);

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

    public static string Tail(string output)
    {
        var trimmed = output.Trim();
        return trimmed.Length <= 300 ? trimmed : "…" + trimmed[^300..];
    }
}
