using System.Diagnostics;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Models;

/// <summary>How a CLI subject's legs are staged.</summary>
/// <param name="RepositoryDir">A directory where git resolves the target's commits — the run's own
/// checked-out tree.</param>
/// <param name="Commit">The commit every leg's worktree is created at — the run's target, the tree the
/// solver investigates.</param>
/// <param name="WorktreeRoot">Where the per-leg worktrees live. Under the CHECKOUT root, not temp,
/// because <see cref="WorkspaceTrust"/> may only trust paths under it.</param>
public sealed record CliSubjectOptions(
    string Executable,
    string RepositoryDir,
    CommitSha Commit,
    string WorktreeRoot,
    WorkspaceTrust Trust);

/// <summary>A cloud CLI measured AS a subject (todo/PLAN_investigate_vs_implement.md §3.6) — the other
/// side of the worker/subject boundary `ICliAgentRuntime` guards, meeting `PLAN_tool_benchmark.md`
/// step 11 at exactly this seam through <see cref="SubjectRouter"/>.
/// <para>
/// Every ask stages its OWN disposable worktree at the pinned commit — never the shared per-commit
/// tree, which an agent that writes would poison for every sibling leg — plants the deny-writes
/// settings, pre-trusts the tree (measured: an untrusted workspace costs the whole wall waiting on a
/// dialog nobody can answer), runs the CLI in it, AUDITS it afterwards, and removes it on every path
/// out. A leg that wrote is marked in its stored record — the settings are advisory hardening, the
/// audit is the evidence.
/// </para>
/// <para>
/// The answer, the tokens (cache included) and the CLI's own cost come from the JSON envelope;
/// sampling is NOT CAPTURED — a CLI exposes no temperature or seed, and claiming the requested values
/// were applied is the unpinned-sampler lie. The wall is the one enforceable ceiling: the process is
/// killed at it. Turn and cost ceilings are refused by name.
/// </para></summary>
public sealed class CliSubjectRuntime(CliSubjectOptions options, ILogger<CliSubjectRuntime> logger) : IModelRuntime
{
    /// <summary>When the request carries no wall budget. Bounded regardless: an unbounded CLI call is
    /// how one hung agent becomes a day of wall clock.</summary>
    private static readonly TimeSpan DefaultWall = TimeSpan.FromMinutes(10);

    public ModelHosting Hosting => ModelHosting.Cloud;

    public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
        Task.FromResult(budget.Kind switch
        {
            BudgetKind.Wall => Outcome<string>.Success(nameof(CliSubjectRuntime)),
            BudgetKind.Turns => Outcome<string>.Failure(
                "a CLI subject's inner loop cannot be turn-capped from outside — the wall is the ceiling, and it is recorded as such"),
            BudgetKind.CostUsd => Outcome<string>.Failure(
                "a CLI reports its cost after the fact — a per-call cost ceiling cannot be enforced here; the harness's "
                + "next-leg gate is the enforcement"),
            _ => Outcome<string>.Failure(
                $"a CLI subject cannot enforce a {budget.Kind} ceiling — an accepted budget nobody enforces is the "
                + "context-compaction lie, re-run"),
        });

    public async Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var wall = Wall(request);
        var staged = await ScratchWorktree.AddAsync(
            options.RepositoryDir, options.Commit, wall, cancellationToken, options.WorktreeRoot);

        if (staged is Outcome<string>.Fail unstaged)
        {
            return Outcome<ModelAnswer>.Failure(unstaged.Reason);
        }

        var worktree = ((Outcome<string>.Ok)staged).Value;

        try
        {
            return await AskInAsync(worktree, request, wall, cancellationToken);
        }
        finally
        {
            await ScratchWorktree.RemoveAsync(options.RepositoryDir, worktree);
        }
    }

    private async Task<Outcome<ModelAnswer>> AskInAsync(
        string worktree, ModelRequest request, TimeSpan wall, CancellationToken cancellationToken)
    {
        await WorktreeAudit.DenyWritesAsync(worktree, cancellationToken);

        var trusted = options.Trust.Ensure(worktree);

        trusted.Match(
            result => 0,
            reason =>
            {
                logger.LogWarning("The subject worktree could not be pre-trusted: {Reason}", reason);
                return 0;
            });

        var clock = Stopwatch.StartNew();

        var attempt = await ProcessRunner.RunAsync(
            options.Executable,
            ["-p", "--model", request.Endpoint.Model.Id, "--output-format", "json"],
            worktree,
            wall,
            Prompt(request),
            cancellationToken);

        var read = Read(attempt, clock.Elapsed);

        return await AuditedAsync(worktree, read, wall, cancellationToken);
    }

    /// <summary>The audit, appended to the record rather than kept in a log: a leg that wrote into its
    /// tree must be readable as one FROM THE STORED RESULT, months later, without this session's
    /// scrollback.</summary>
    private async Task<Outcome<ModelAnswer>> AuditedAsync(
        string worktree, Outcome<ModelAnswer> read, TimeSpan wall, CancellationToken cancellationToken)
    {
        if (read is not Outcome<ModelAnswer>.Ok(var answer))
        {
            read.Match(
                ok => 0,
                reason =>
                {
                    logger.LogWarning("The CLI subject did not answer: {Reason}", reason);
                    return 0;
                });

            return read;
        }

        var audit = await WorktreeAudit.ChangesAsync(worktree, wall, cancellationToken);

        if (audit is Outcome<string>.Ok(var changes) && changes.Length > 0)
        {
            logger.LogWarning("The CLI subject WROTE into its worktree: {Changes}", changes);

            return Outcome<ModelAnswer>.Success(answer with
            {
                StopDetail = answer.StopDetail + $"; WROTE to its worktree: {changes.Replace('\n', ' ')}",
            });
        }

        return read;
    }

    /// <summary>One attempt as an answer or a named refusal — pure, because these readings are the
    /// logic worth asserting and a test of them needs no process. STDOUT alone, never the merged text:
    /// the trust warning beside the envelope defeated a JSON parse once already.</summary>
    public static Outcome<ModelAnswer> Read(ProcessAttempt attempt, TimeSpan elapsed) =>
        attempt switch
        {
            ProcessAttempt.NotFound missing => Outcome<ModelAnswer>.Failure(
                $"'{missing.Executable}' is not installed on this machine — a configuration fact, not the subject's failure"),

            ProcessAttempt.TimedOut cap => Outcome<ModelAnswer>.Failure(
                $"the CLI subject did not answer within {cap.Budget.TotalSeconds:0.#}s"),

            ProcessAttempt.Completed { Result.Ok: false } failed => Outcome<ModelAnswer>.Failure(
                $"the CLI subject exited {failed.Result.ExitCode}: {Tail(failed.Result.Output)}"),

            ProcessAttempt.Completed done => Answer(done.Result.StandardOutput, elapsed),

            _ => Outcome<ModelAnswer>.Failure("the CLI subject produced an attempt this runtime cannot read"),
        };

    private static Outcome<ModelAnswer> Answer(string stdout, TimeSpan elapsed) =>
        ClaudeEnvelope.Read(stdout).Match(
            reading => Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text(reading.Text),
                reading.TokensIn,
                reading.TokensOut,
                elapsed,
                SamplingAsSent.NotCaptured("a CLI subject exposes no sampling controls"),
                StopReason.Completed,
                reading.CostCaptured
                    ? $"cli; cost {reading.CostUsd.ToString(System.Globalization.CultureInfo.InvariantCulture)} USD"
                    : "cli; cost not reported")),
            Outcome<ModelAnswer>.Failure);

    /// <summary>The doctrine rides ahead of the user prompt in the one channel a print-mode CLI reads.
    /// An approximation and RECORDED as one — where it lands in the CLI's real context is not something
    /// this harness can confirm, which is the §3.2 cross-presentation caveat of the tool benchmark.</summary>
    private static string Prompt(ModelRequest request) =>
        request.SystemPrompt.Length > 0
            ? request.SystemPrompt + "\n\n---\n\n" + request.UserPrompt
            : request.UserPrompt;

    private static TimeSpan Wall(ModelRequest request)
    {
        var wall = request.Budgets.FirstOrDefault(b => b.Kind == BudgetKind.Wall);

        return wall is { Limit: > 0 } ? TimeSpan.FromSeconds((double)wall.Limit) : DefaultWall;
    }

    private static string Tail(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= 400 ? trimmed : "…" + trimmed[^400..];
    }
}
