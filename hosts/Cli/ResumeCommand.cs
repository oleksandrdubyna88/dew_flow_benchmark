using Bench.Application;
using Bench.Application.Bank;
using Bench.Application.Lanes;
using Bench.Domain;
using Bench.Domain.Bank;
using Bench.Domain.Lanes;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Infrastructure.Engines;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>
/// `bench resume --run &lt;id&gt;` — finish a campaign something interrupted.
///
/// <para><b>Why this exists, and it was found the hard way.</b> `RunCommand`'s own summary says the harness
/// "is built to be stopped and resumed", and that was true of one thing only: the sweep hands back cells a
/// dead host CLAIMED, inside a drain that is still running. Nothing could pick up an interrupted run from a
/// cold start — <c>bench run</c> always plans a NEW one — so a campaign killed at minute ten left its cells
/// <c>Pending</c> inside a run stuck at <c>Running</c> forever, with no verb that could reach them. Found by
/// losing 6 of 45 legs to a shell timeout, which on a multi-hour full-bank campaign is not a hypothetical.
/// It is also the durable-status rule: a status nothing can advance out of is exactly the stuck in-flight
/// state that rule forbids.</para>
///
/// <para><b>The run's identity comes from STORAGE; only its reachability comes from the operator.</b> The
/// questions are rebuilt from the bank and proved byte for byte against the stamp the run froze, the subject
/// model ids come off the cells, the lanes and variants come off the cells. What was never stored is the
/// model's base URL and the wall budget — an endpoint is where a model was reachable that day, not a
/// property of the measurement — so those are named again on the command line, and a model id that
/// disagrees with what the cells recorded is refused rather than silently substituted.</para>
///
/// <para><b>A retired lane does not block a resume</b>, and that is the whole reason retired rows stay
/// listable: this run already measures it. Refusing here would strand exactly the campaigns that ran long
/// enough for somebody to tidy the catalog underneath them.</para>
/// </summary>
public static class ResumeCommand
{
    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return Fail(error, "--run <id> is required — a resume continues one named run", ExitCodes.Configuration);
        }

        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

        if (connection.Length == 0)
        {
            return Fail(error, "--db (or BENCH_DB) is required", ExitCodes.Configuration);
        }

        var modelUrl = command.Value("model-url");

        if (modelUrl.Length == 0)
        {
            return Fail(
                error,
                "--model-url is required — an endpoint is where a model was reachable that day rather than a "
                + "property of the measurement, so it is the one thing a run does not store",
                ExitCodes.Configuration);
        }

        await using var provider = CliContainer.ForRun(
            connection,
            command.Value("checkout-root", RunCommand.DefaultCheckoutRoot),
            QlnEngineOptions.None,
            CliLogging.Start());

        var rebuilt = await RebuildAsync(provider, runId, command, modelUrl, output, error, stopping);

        if (rebuilt is null)
        {
            return ExitCodes.Environment;
        }

        await SweepCommand.RecoverAsync(provider, SweepCommand.StaleAfter(command), output, stopping);

        return await RunCommand.ExecuteAsync(provider, rebuilt.Value.Run, rebuilt.Value.Plan, command, output, stopping);
    }

    /// <summary>The run and the plan it was drained under, reconstructed from what it stored.</summary>
    private static async Task<(BenchRun Run, LegPlan Plan)?> RebuildAsync(
        ServiceProvider provider,
        Guid runId,
        CommandLine command,
        string modelUrl,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BenchDbContext>().Database.MigrateAsync(stopping);

        var loaded = await scope.ServiceProvider.GetRequiredService<IRunStore>().LoadAsync(runId, stopping);

        if (loaded is Outcome<BenchRun>.Fail missing)
        {
            return Refuse(error, missing.Reason);
        }

        var run = ((Outcome<BenchRun>.Ok)loaded).Value;
        var progress = await scope.ServiceProvider.GetRequiredService<IRunStore>().ProgressAsync(runId, stopping);

        if (progress.IsFinished)
        {
            // Not an error: a resume of a finished run is a no-op somebody asked for, and saying so plainly
            // beats a second drain that measures nothing and prints a summary as though it had.
            output.WriteLine($"resume   run {runId} is already finished — {progress.Settled} settled cell(s), nothing pending");
            return null;
        }

        var suite = await SuiteAsync(scope, run, output, stopping);

        if (suite is Outcome<Suite>.Fail unrebuilt)
        {
            return Refuse(error, unrebuilt.Reason);
        }

        var roster = await RosterAsync(scope, runId, command, modelUrl, stopping);

        if (roster is Outcome<SubjectRoster>.Fail unreachable)
        {
            return Refuse(error, unreachable.Reason);
        }

        // The LANES first, because they decide whether a tree is needed at all. A run planned with
        // --no-checkout has no tree and needs none; demanding one here would make every floor-arm run
        // unresumable, and the flag it was planned under is not something a run stores. Found by a test:
        // the first version checked out unconditionally and could not resume its own fixtures.
        var rows = await LanesOfAsync(scope, runId, stopping);

        if (rows is Outcome<IReadOnlyList<ToolLane>>.Fail unknown)
        {
            return Refuse(error, unknown.Reason);
        }

        var chosen = ((Outcome<IReadOnlyList<ToolLane>>.Ok)rows).Value;
        var checkout = string.Empty;

        if (chosen.Any(lane => lane.Definition.Presentation != ToolPresentation.None))
        {
            var tree = await CheckoutAsync(scope, run, output, error, stopping);

            if (tree is null)
            {
                return null;
            }

            checkout = tree;
        }

        var lanes = LaneResolution.Resolve(chosen, checkout.Length > 0 ? new FilesystemEngine(checkout) : null);

        if (lanes is Outcome<LaneRoster>.Fail unresolved)
        {
            return Refuse(error, unresolved.Reason);
        }

        var frozen = ((Outcome<Suite>.Ok)suite).Value;
        var subjects = ((Outcome<SubjectRoster>.Ok)roster).Value;
        var resolved = ((Outcome<LaneRoster>.Ok)lanes).Value;

        Announce(output, run, progress, subjects, resolved);

        return (
            run,
            new LegPlan(frozen, subjects, Budgets(command), run.Kind) with { Lanes = resolved });
    }

    /// <summary>The tree again, at the commit the run pinned. A resumed leg must read the same files the
    /// legs before it read, so this is the run's commit rather than anything the operator may pass.</summary>
    private static async Task<string?> CheckoutAsync(
        AsyncServiceScope scope, BenchRun run, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        var ensured = await scope.ServiceProvider.GetRequiredService<ICheckoutProvider>()
            .EnsureAsync(run.Target, stopping);

        if (ensured is Outcome<string>.Fail unavailable)
        {
            error.WriteLine($"bench: the run's target could not be checked out — {unavailable.Reason}");
            return null;
        }

        var path = ((Outcome<string>.Ok)ensured).Value;
        output.WriteLine($"checkout {path}");

        return path;
    }

    /// <summary>The questions, rebuilt from the bank and PROVED by the stamp the run froze.
    /// <para>
    /// The same guard the judge applies, and for the same reason: a bank that drifted since the run started
    /// would hand the remaining legs different questions under the old stamp, and half a campaign measured
    /// against a different set is worse than half a campaign.
    /// </para></summary>
    private static async Task<Outcome<Suite>> SuiteAsync(
        AsyncServiceScope scope, BenchRun run, TextWriter output, CancellationToken stopping) =>
        await FrozenSuite.FromBankAsync(scope, run.Id, run.SuiteStamp, output, stopping);

    /// <summary>Who this run measures — the model ids off its own cells, at the endpoint named now.
    /// <para>
    /// A run with several subjects resumes with all of them, because the cells say which. What the operator
    /// may NOT do is substitute a different model: <c>--model</c> is optional, and when given it must agree
    /// with what the cells recorded, or the second half of a campaign would be a different subject wearing
    /// the first half's label.
    /// </para></summary>
    private static async Task<Outcome<SubjectRoster>> RosterAsync(
        AsyncServiceScope scope, Guid runId, CommandLine command, string modelUrl, CancellationToken stopping)
    {
        var ids = await scope.ServiceProvider.GetRequiredService<BenchDbContext>().Cells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .Select(c => c.SubjectModelId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(stopping);

        var named = command.Value("model");

        if (named.Length > 0 && !ids.Contains(named, StringComparer.Ordinal))
        {
            return Outcome<SubjectRoster>.Failure(
                $"--model '{named}' is not a subject of this run — its cells name "
                + $"{string.Join(", ", ids.Select(i => $"'{i}'"))}, and a resume may not change who is measured");
        }

        var wanted = named.Length > 0 ? [named] : ids;
        var entries = new List<RosterEntry>(wanted.Count);

        foreach (var id in wanted)
        {
            var endpoint = ModelRef.Parse(id, ModelHosting.Local)
                .Match(model => ModelEndpoint.Parse(model, modelUrl), Outcome<ModelEndpoint>.Failure);

            if (endpoint is Outcome<ModelEndpoint>.Fail bad)
            {
                return Outcome<SubjectRoster>.Failure(bad.Reason);
            }

            entries.Add(new RosterEntry(
                ((Outcome<ModelEndpoint>.Ok)endpoint).Value, Sampling.Deterministic(command.Int("seed", 1))));
        }

        return Outcome<SubjectRoster>.Success(SubjectRoster.Of(entries));
    }

    /// <summary>The lanes this run measures, from its own cells — RETIRED ones included.
    /// <para>
    /// A lane retired since the run started must not strand it. That is exactly what retired rows stay
    /// listable for: a report over an old test still has to name the surface it ran against, and a campaign
    /// long enough for somebody to tidy the catalog underneath it is the campaign most worth finishing.
    /// </para></summary>
    private static async Task<Outcome<IReadOnlyList<ToolLane>>> LanesOfAsync(
        AsyncServiceScope scope, Guid runId, CancellationToken stopping)
    {
        var names = await scope.ServiceProvider.GetRequiredService<BenchDbContext>().Cells.AsNoTracking()
            .Where(c => c.RunId == runId && c.LaneName != string.Empty)
            .Select(c => c.LaneName)
            .Distinct()
            .ToListAsync(stopping);

        var catalog = await scope.ServiceProvider.GetRequiredService<ILaneCatalog>().ListAsync(true, stopping);

        if (catalog is Outcome<IReadOnlyList<ToolLane>>.Fail unreadable)
        {
            return Outcome<IReadOnlyList<ToolLane>>.Failure(unreadable.Reason);
        }

        var rows = ((Outcome<IReadOnlyList<ToolLane>>.Ok)catalog).Value;

        // A name the catalog does not hold is the FLOOR, not an error — and the first version of this got it
        // backwards, refusing every run planned before the lane axis existed. Those runs carry the default
        // `no-tools` lane name, which was never a catalog row and never will be; at leg time an unresolved
        // lane already resolves to `LaneChoice.Floor`. The "deleted lane" this used to refuse cannot happen
        // either: the catalog adds and RETIRES, never deletes, so a row that once served is still there.
        // Found by a test that could not resume its own fixture.
        return Outcome<IReadOnlyList<ToolLane>>.Success(
            [.. rows.Where(lane => names.Contains(lane.Name.Value, StringComparer.Ordinal))]);
    }

    /// <summary>The wall budget, named again because it was never stored. It is an operator's ceiling on how
    /// long a leg may take, not a property of the measurement — and the summary prints it, so a resume under
    /// a different wall says so rather than hiding it.</summary>
    private static IReadOnlyList<Budget> Budgets(CommandLine command) =>
        [Budget.Of(BudgetKind.Wall, BudgetScope.Question, command.Int("leg-wall-seconds", 600))];

    private static void Announce(
        TextWriter output, BenchRun run, RunProgress progress, SubjectRoster subjects, LaneRoster lanes)
    {
        output.WriteLine($"run      {run.Id}  ({run.Label})");
        output.WriteLine($"target   {run.Target.Canonical}");
        output.WriteLine($"resume   {progress.Describe}");
        output.WriteLine($"subjects {string.Join(", ", subjects.Entries.Select(e => e.Endpoint.Model.Id))}");

        if (lanes.Entries.Count > 0)
        {
            output.WriteLine($"lanes    {string.Join(", ", lanes.Entries.Select(l => l.Name))}");
        }
    }

    private static (BenchRun, LegPlan)? Refuse(TextWriter error, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return null;
    }

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }
}
