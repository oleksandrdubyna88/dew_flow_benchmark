using System.Globalization;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Scouts;

namespace Bench.Cli;

/// <summary>What <c>bench scouts run</c> was asked for, read and refused by name before anything is opened (4).</summary>
internal sealed record ScoutRunAsk(
    IReadOnlyList<ScoutTask> Tasks, string OpusId, string SonnetId, decimal CeilingUsd, IReadOnlyList<ScoutArmId> Arms,
    IReadOnlyList<ScoutArmBudget> Budgets, IReadOnlyList<ScoutTaskId> Replication, int Repeats, int Seed)
{
    /// <summary><c>--kind preliminary|decision</c> (D7) — PRELIMINARY unless a decision run is asked for by name, so a run can never claim a
    /// verdict it was not planned to give.</summary>
    public ScoutRunKind Kind { get; init; } = ScoutRunKind.Preliminary;

    public static async Task<Outcome<ScoutRunAsk>> OfAsync(CommandLine command, CancellationToken cancellationToken)
    {
        var refusal = Flags(command);
        var kind = ScoutRunKinds.Parse(command.Value("kind", ScoutRunKinds.Word(ScoutRunKind.Preliminary)));

        if (refusal.Length > 0 || kind is Outcome<ScoutRunKind>.Fail)
        {
            return Outcome<ScoutRunAsk>.Failure(refusal.Length > 0 ? refusal : $"--kind: {((Outcome<ScoutRunKind>.Fail)kind).Reason}");
        }

        return (await AskedAsync(command, cancellationToken)).Match(
            ask => Outcome<ScoutRunAsk>.Success(ask with { Kind = ((Outcome<ScoutRunKind>.Ok)kind).Value }),
            Outcome<ScoutRunAsk>.Failure);
    }

    private static async Task<Outcome<ScoutRunAsk>> AskedAsync(CommandLine command, CancellationToken cancellationToken)
    {

        var tasks = ScoutFiles.Tasks(await File.ReadAllTextAsync(command.Value("tasks-file"), cancellationToken));
        var arms = ArmsOf(command);

        return (tasks, arms) switch
        {
            (Outcome<IReadOnlyList<ScoutTask>>.Fail fail, _) => Outcome<ScoutRunAsk>.Failure($"{Path.GetFileName(command.Value("tasks-file"))}: {fail.Reason}"),
            (_, Outcome<IReadOnlyList<ScoutArmId>>.Fail fail) => Outcome<ScoutRunAsk>.Failure(fail.Reason),
            (Outcome<IReadOnlyList<ScoutTask>>.Ok t, Outcome<IReadOnlyList<ScoutArmId>>.Ok a) => Budgeted(command, t.Value, a.Value),
            _ => Outcome<ScoutRunAsk>.Failure("unreachable"),
        };
    }

    private static Outcome<ScoutRunAsk> Budgeted(CommandLine command, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutArmId> arms)
    {
        var wall = command.Has("cell-timeout-seconds")
            ? command.Int("cell-timeout-seconds", ScoutArmBudget.DefaultWallSeconds)
            : command.Int("cell-timeout-minutes", ScoutsCommand.DefaultCellTimeoutMinutes) * 60;
        var budgets = ScoutArmBudgets.For(arms, Usd(command, "cap-usd"), command.List("arm-caps"), wall);
        var replication = command.List("replication-tasks").Select(ScoutTaskId.Parse).ToList();

        return (budgets, replication.OfType<Outcome<ScoutTaskId>.Fail>().FirstOrDefault()) switch
        {
            (Outcome<IReadOnlyList<ScoutArmBudget>>.Fail fail, _) => Outcome<ScoutRunAsk>.Failure(fail.Reason),
            (_, { } bad) => Outcome<ScoutRunAsk>.Failure($"--replication-tasks: {bad.Reason}"),
            (Outcome<IReadOnlyList<ScoutArmBudget>>.Ok b, _) => Outcome<ScoutRunAsk>.Success(new ScoutRunAsk(
                tasks, command.Value("opus-id"), command.Value("sonnet-id"), Usd(command, "ceiling-usd"), arms, b.Value,
                [.. replication.OfType<Outcome<ScoutTaskId>.Ok>().Select(r => r.Value)], command.Int("repeats", 1), command.Int("seed", 1))),
            _ => Outcome<ScoutRunAsk>.Failure("unreachable"),
        };
    }

    private static string Flags(CommandLine command) =>
        (File.Exists(command.Value("tasks-file")), command.Value("opus-id").Length > 0 && command.Value("sonnet-id").Length > 0, Usd(command, "ceiling-usd") > 0m, Usd(command, "cap-usd") > 0m) switch
        {
            (false, _, _, _) => "scouts run needs --tasks-file <tasks.json> (what bench scouts tasks wrote)",
            (_, false, _, _) => "scouts run needs --opus-id and --sonnet-id — the resolved model ids the run is pinned to (bench scouts alias-check prints them)",
            (_, _, false, _) => "scouts run needs --ceiling-usd <USD> — a scout run spends money and is capped",
            (_, _, _, false) => "scouts run needs --cap-usd <USD> — every cell launches under its arm's --max-budget-usd, never a default",
            _ => string.Empty,
        };

    private static Outcome<IReadOnlyList<ScoutArmId>> ArmsOf(CommandLine command)
    {
        var words = command.Has("arms") ? command.List("arms") : [.. ScoutArms.Main.Select(a => a.Word)];
        var parsed = words.Select(ScoutArms.Parse).ToList();

        return parsed.OfType<Outcome<ScoutArm>.Fail>().FirstOrDefault() is { } bad
            ? Outcome<IReadOnlyList<ScoutArmId>>.Failure($"--arms: {bad.Reason}")
            : Outcome<IReadOnlyList<ScoutArmId>>.Success([.. parsed.OfType<Outcome<ScoutArm>.Ok>().Select(a => a.Value.Id)]);
    }

    private static decimal Usd(CommandLine command, string name) =>
        decimal.TryParse(command.Value(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var usd) ? usd : 0m;
}

/// <summary>The plan of a new run, in the order a person fixes things: every task's tree prepared and re-hashed (a tree that no longer
/// hashes to its frozen hash is a configuration fact, 4; git failing is the environment, 3), the local pins read with the CLI binary (3), the
/// pins frozen with the two resolved ids (4), the run and its seeded schedule planned in one transaction (4).</summary>
internal static class ScoutRunPlanner
{
    /// <summary>The prefix of a refusal that is the ENVIRONMENT's (3), not the configuration's.</summary>
    public const string EnvironmentMark = "environment: ";

    public static async Task<Outcome<ScoutRun>> PlanAsync(ScoutsInputs inputs, ScoutRunAsk ask, string executable, TextWriter output, CancellationToken cancellationToken)
    {
        foreach (var task in ask.Tasks)
        {
            if (await inputs.Trees.PrepareAsync(task, cancellationToken) is Outcome<Application.Scouts.ScoutTree>.Fail tree)
            {
                return Outcome<ScoutRun>.Failure(tree.Reason.Contains(Application.Scouts.ScoutTreeRefusal.HashChanged, StringComparison.Ordinal) ? tree.Reason : EnvironmentMark + tree.Reason);
            }
        }

        var local = await new ScoutPinReader(new ProductPinReader(), inputs.PinSources).ReadAsync(executable, cancellationToken);

        return local switch
        {
            Outcome<Application.Scouts.ScoutLocalPins>.Ok pins => await StoreAsync(inputs, ask, pins.Value, output, cancellationToken),
            Outcome<Application.Scouts.ScoutLocalPins>.Fail fail => Outcome<ScoutRun>.Failure($"{EnvironmentMark}the pins could not be read — {fail.Reason}"),
            _ => Outcome<ScoutRun>.Failure("unreachable"),
        };
    }

    private static async Task<Outcome<ScoutRun>> StoreAsync(ScoutsInputs inputs, ScoutRunAsk ask, Application.Scouts.ScoutLocalPins local, TextWriter output, CancellationToken cancellationToken)
    {
        var taskIds = ask.Tasks.Select(t => t.Id).ToList();
        var pins = ScoutPins.Of(local.CliVersion, local.PluginCacheHash, ask.OpusId, ask.SonnetId, local.SettingsHash);
        var run = pins.Match(p => ScoutRun.Planned(Guid.CreateVersion7(), DateTimeOffset.UtcNow, p, ask.Seed, ask.Repeats, ask.Arms, taskIds, ask.CeilingUsd), Outcome<ScoutRun>.Failure);
        var schedule = ScoutSchedule.Plan(taskIds, ask.Arms, ask.Replication, ask.Repeats, ask.Seed);

        if ((run, schedule) is not (Outcome<ScoutRun>.Ok { Value: var planned }, Outcome<IReadOnlyList<ScheduledCell>>.Ok { Value: var cells }))
        {
            return Outcome<ScoutRun>.Failure(run is Outcome<ScoutRun>.Fail f ? f.Reason : ((Outcome<IReadOnlyList<ScheduledCell>>.Fail)schedule).Reason);
        }

        var budgeted = planned with { Budgets = ask.Budgets, Kind = ask.Kind };
        var stored = await inputs.Store.PlanAsync(budgeted, ask.Tasks, [.. cells.Select(c => ScoutCell.Pending(Guid.CreateVersion7(), budgeted.Id, c))], cancellationToken);

        if (stored is Outcome<ScoutRun>.Ok)
        {
            output.WriteLine($"planned        scout run {budgeted.Id} — {ScoutRunKinds.Word(ask.Kind)}, {cells.Count} cell(s): {ask.Tasks.Count} task(s) × {ask.Arms.Count} arm(s) × {ask.Repeats} repeat(s), seed {ask.Seed}");
            output.WriteLine($"pinned         claude {local.CliVersion} · opus {ask.OpusId} · sonnet {ask.SonnetId} · plugin {local.PluginCacheHash[..12]} · settings {local.SettingsHash[..12]}");
            var caps = string.Join(", ", ask.Budgets.Select(b => string.Create(CultureInfo.InvariantCulture, $"{ScoutArms.Word(b.Arm)} ${b.CapUsd} ({b.Wall.TotalMinutes:0.#} min)")));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"budgets        ceiling ${ask.CeilingUsd} · {caps}"));
        }

        return stored;
    }
}
