using System.Diagnostics;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Models;

/// <summary>Where a CLI subject runs, and as what.
/// <para>
/// The working directory is the leg's OWN disposable worktree, never the shared per-commit one: an
/// agent that writes into the shared tree poisons every sibling leg at that commit. Plant
/// <see cref="WorktreeAudit.DenyWritesAsync"/> before the leg and read
/// <see cref="WorktreeAudit.ChangesAsync"/> after — the settings are advisory hardening, the AUDIT is
/// the evidence.
/// </para></summary>
public sealed record CliSubjectOptions(string Executable, string WorkingDirectory);

/// <summary>A cloud CLI measured AS a subject (todo/PLAN_investigate_vs_implement.md §3.6) — the other
/// side of the worker/subject boundary `ICliAgentRuntime` guards: that port launches processes that
/// work FOR the harness; this one answers `IModelRuntime` so a leg can be measured THROUGH the CLI.
/// It meets `PLAN_tool_benchmark.md` step 11 at exactly this seam; the roster wiring that lets
/// `bench run --subjects` name a CLI row belongs there and is deliberately not smuggled in here.
/// <para>
/// What is honest today: the answer, the tokens (cache included) and the CLI's own cost come from the
/// JSON envelope; sampling is NOT CAPTURED — a CLI exposes no temperature or seed, and claiming the
/// requested values were applied is the unpinned-sampler lie the family already paid for. The wall is
/// the one enforceable ceiling: the process is killed at it. A turn ceiling is refused by name — the
/// CLI's inner loop cannot be counted from outside, and a budget nobody can enforce must not be
/// recorded as accepted.
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
        var clock = Stopwatch.StartNew();

        var attempt = await ProcessRunner.RunAsync(
            options.Executable,
            ["-p", "--model", request.Endpoint.Model.Id, "--output-format", "json"],
            options.WorkingDirectory,
            Wall(request),
            Prompt(request),
            cancellationToken);

        var read = Read(attempt, clock.Elapsed);

        read.Match(
            ok => 0,
            reason =>
            {
                logger.LogWarning("The CLI subject {Model} did not answer: {Reason}", request.Endpoint.Model.Id, reason);
                return 0;
            });

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
