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

    /// <summary>The campaign's seed string from <c>tasks.json</c> (S4) — frozen on the run, so every later verb commits under it.</summary>
    public string Campaign { get; init; } = string.Empty;

    public static async Task<Outcome<ScoutRunAsk>> OfAsync(CommandLine command, CancellationToken cancellationToken)
    {
        var kind = ScoutRunKinds.Parse(command.Value("kind", ScoutRunKinds.Word(ScoutRunKind.Preliminary)));

        if (kind is Outcome<ScoutRunKind>.Fail badKind)
        {
            return Outcome<ScoutRunAsk>.Failure($"--kind: {badKind.Reason}");
        }

        var asked = ((Outcome<ScoutRunKind>.Ok)kind).Value;
        var refusal = Flags(command, asked);

        return refusal.Length > 0
            ? Outcome<ScoutRunAsk>.Failure(refusal)
            : (await AskedAsync(command, asked, cancellationToken)).Match(ask => Outcome<ScoutRunAsk>.Success(ask with { Kind = asked }), Outcome<ScoutRunAsk>.Failure);
    }

    private static async Task<Outcome<ScoutRunAsk>> AskedAsync(CommandLine command, ScoutRunKind kind, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(command.Value("tasks-file"), cancellationToken);
        var tasks = await TasksAsync(command, json, cancellationToken);
        var arms = ArmsOf(command);

        return (tasks, arms) switch
        {
            (Outcome<IReadOnlyList<ScoutTask>>.Fail fail, _) => Outcome<ScoutRunAsk>.Failure($"{Path.GetFileName(command.Value("tasks-file"))}: {fail.Reason}"),
            (_, Outcome<IReadOnlyList<ScoutArmId>>.Fail fail) => Outcome<ScoutRunAsk>.Failure(fail.Reason),
            (Outcome<IReadOnlyList<ScoutTask>>.Ok t, Outcome<IReadOnlyList<ScoutArmId>>.Ok a) => (await BudgetsAsync(command, kind, a.Value, ScoutFiles.Campaign(json), cancellationToken)).Match(
                budgets => Budgeted(command, t.Value, a.Value, budgets).Match(ask => Outcome<ScoutRunAsk>.Success(ask with { Campaign = ScoutFiles.Campaign(json) }), Outcome<ScoutRunAsk>.Failure),
                Outcome<ScoutRunAsk>.Failure),
            _ => Outcome<ScoutRunAsk>.Failure("unreachable"),
        };
    }

    /// <summary>Where the arms' caps and walls come from (S6, <see cref="ScoutRunLimits"/>): a PILOT from the flags, under its bounds; a
    /// preliminary or decision run from <c>schedule.json</c> ONLY — a budget flag named is refused by name, the schedule must be there, hold
    /// every arm, and have been made under the tasks file's campaign.</summary>
    private static async Task<Outcome<IReadOnlyList<ScoutArmBudget>>> BudgetsAsync(CommandLine command, ScoutRunKind kind, IReadOnlyList<ScoutArmId> arms, string campaign, CancellationToken cancellationToken)
    {
        var named = ScoutRunLimits.BudgetFlags.Where(flag => command.Has(flag[2..])).ToList();
        var source = ScoutRunLimits.SourceRefusal(kind, named, command.Has("schedule"));

        return (source.Length > 0, kind) switch
        {
            (true, _) => Outcome<IReadOnlyList<ScoutArmBudget>>.Failure(source),
            (_, ScoutRunKind.Pilot) => FlagBudgets(command, arms),
            _ => Scheduled(ScoutFiles.Schedule(await File.ReadAllTextAsync(command.Value("schedule"), cancellationToken)), arms, campaign),
        };
    }

    private static Outcome<IReadOnlyList<ScoutArmBudget>> FlagBudgets(CommandLine command, IReadOnlyList<ScoutArmId> arms)
    {
        var wall = command.Has("cell-timeout-seconds")
            ? command.Int("cell-timeout-seconds", ScoutArmBudget.DefaultWallSeconds)
            : command.Int("cell-timeout-minutes", ScoutsCommand.DefaultCellTimeoutMinutes) * 60;

        return ScoutArmBudgets.For(arms, Usd(command, "cap-usd"), command.List("arm-caps"), wall).Match(
            budgets => ScoutRunLimits.PilotRefusal(budgets, Usd(command, "ceiling-usd")) is { Length: > 0 } refusal
                ? Outcome<IReadOnlyList<ScoutArmBudget>>.Failure(refusal)
                : Outcome<IReadOnlyList<ScoutArmBudget>>.Success(budgets),
            Outcome<IReadOnlyList<ScoutArmBudget>>.Failure);
    }

    private static Outcome<IReadOnlyList<ScoutArmBudget>> Scheduled(Outcome<ScoutScheduleFile> schedule, IReadOnlyList<ScoutArmId> arms, string campaign) =>
        schedule.Match(
            file => file.Campaign.Length > 0 && campaign.Length > 0 && file.Campaign != campaign
                ? Outcome<IReadOnlyList<ScoutArmBudget>>.Failure($"schedule.json was made under campaign '{file.Campaign}' and the tasks file under '{campaign}' — a run takes its caps from its own campaign's pilot")
                : ScoutArmBudgets.FromSchedule(arms, file.Budgets),
            reason => Outcome<IReadOnlyList<ScoutArmBudget>>.Failure($"{ScoutRunLimits.ScheduleFlag}: {reason}"));

    /// <summary>The frozen tasks: a public-only file as it is; a file with a PRIVATE task through <see cref="ScoutFiles.Resolve"/> — the private
    /// file (outside git) and the campaign key, every commitment re-derived and a mismatch refused by label (S4).</summary>
    private static async Task<Outcome<IReadOnlyList<ScoutTask>>> TasksAsync(CommandLine command, string json, CancellationToken cancellationToken)
    {
        var entries = ScoutFiles.Entries(json);

        if (entries is not Outcome<IReadOnlyList<ScoutFiles.PublicEntry>>.Ok { Value: var listed } || !listed.Any(e => e.IsPrivate))
        {
            return ScoutFiles.Tasks(json);
        }

        var committer = ScoutsInputs.Committer(command, ScoutFiles.Campaign(json));
        var file = ScoutsInputs.LocalOnlyFile(ScoutFiles.PrivateTasksFlag, command.Value("private-tasks-file"));

        return (committer, file) switch
        {
            (Outcome<ScoutCommitter>.Fail f, _) => Outcome<IReadOnlyList<ScoutTask>>.Failure($"the file holds a private task — {f.Reason}"),
            (_, Outcome<string>.Fail f) => Outcome<IReadOnlyList<ScoutTask>>.Failure($"the file holds a private task — {f.Reason}"),
            (Outcome<ScoutCommitter>.Ok c, Outcome<string>.Ok p) when !File.Exists(p.Value) => Outcome<IReadOnlyList<ScoutTask>>.Failure($"the file holds a private task and the private tasks file {p.Value} is not there"),
            (Outcome<ScoutCommitter>.Ok c, Outcome<string>.Ok p) => ScoutFiles.Resolve(json, await File.ReadAllTextAsync(p.Value, cancellationToken), c.Value),
            _ => Outcome<IReadOnlyList<ScoutTask>>.Failure("unreachable"),
        };
    }

    private static Outcome<ScoutRunAsk> Budgeted(CommandLine command, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutArmBudget> budgets)
    {
        var replication = command.List("replication-tasks").Select(ScoutTaskId.Parse).ToList();

        return replication.OfType<Outcome<ScoutTaskId>.Fail>().FirstOrDefault() is { } bad
            ? Outcome<ScoutRunAsk>.Failure($"--replication-tasks: {bad.Reason}")
            : Outcome<ScoutRunAsk>.Success(new ScoutRunAsk(
                tasks, command.Value("opus-id"), command.Value("sonnet-id"), Usd(command, "ceiling-usd"), arms, budgets,
                [.. replication.OfType<Outcome<ScoutTaskId>.Ok>().Select(r => r.Value)], command.Int("repeats", 1), command.Int("seed", 1)));
    }

    /// <summary>The flags every kind needs, then the one its budget source needs: a pilot <c>--cap-usd</c>, any other kind <c>--schedule</c>.</summary>
    private static string Flags(CommandLine command, ScoutRunKind kind) =>
        (File.Exists(command.Value("tasks-file")), command.Value("opus-id").Length > 0 && command.Value("sonnet-id").Length > 0, Usd(command, "ceiling-usd") > 0m) switch
        {
            (false, _, _) => "scouts run needs --tasks-file <tasks.json> (what bench scouts tasks wrote)",
            (_, false, _) => "scouts run needs --opus-id and --sonnet-id — the resolved model ids the run is pinned to (bench scouts alias-check prints them)",
            (_, _, false) => "scouts run needs --ceiling-usd <USD> — a scout run spends money and is capped",
            _ => SourceFlags(command, kind),
        };

    private static string SourceFlags(CommandLine command, ScoutRunKind kind) =>
        (kind, Usd(command, "cap-usd") > 0m, File.Exists(command.Value("schedule"))) switch
        {
            (ScoutRunKind.Pilot, false, _) => "scouts run --kind pilot needs --cap-usd <USD> (at most $3) — every cell launches under its arm's --max-budget-usd, never a default",
            (not ScoutRunKind.Pilot, _, false) when command.Has("schedule") => $"scouts run --kind {ScoutRunKinds.Word(kind)}: {ScoutRunLimits.ScheduleFlag} {command.Value("schedule")} is not there",
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

        // The pinned settings file is written ONCE, here, from the user settings (S5) — a NEW run pins what the operator has now; a found
        // file that drifted from it, or carries another key, is a configuration refusal naming what to do.
        if (inputs.Artifacts.PinnedSettings(inputs.PinSources.SettingsFiles) is Outcome<string>.Fail notPinned)
        {
            return Outcome<ScoutRun>.Failure(notPinned.Reason);
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

        var budgeted = planned with { Budgets = ask.Budgets, Kind = ask.Kind, Campaign = ask.Campaign };
        var stored = await inputs.Store.PlanAsync(budgeted, ask.Tasks, [.. cells.Select(c => ScoutCell.Pending(Guid.CreateVersion7(), budgeted.Id, c))], cancellationToken);

        if (stored is Outcome<ScoutRun>.Ok)
        {
            output.WriteLine($"planned        scout run {budgeted.Id} — {ScoutRunKinds.Word(ask.Kind)}, {cells.Count} cell(s): {ask.Tasks.Count} task(s) × {ask.Arms.Count} arm(s) × {ask.Repeats} repeat(s), seed {ask.Seed}");
            output.WriteLine($"pinned         claude {local.CliVersion} · opus {ask.OpusId} · sonnet {ask.SonnetId} · plugin {local.PluginCacheHash[..12]} · pinned settings {local.SettingsHash[..12]}");
            var caps = string.Join(", ", ask.Budgets.Select(b => string.Create(CultureInfo.InvariantCulture, $"{ScoutArms.Word(b.Arm)} ${b.CapUsd} ({b.Wall.TotalMinutes:0.#} min)")));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"budgets        ceiling ${ask.CeilingUsd} · {caps}"));
        }

        return stored;
    }
}
