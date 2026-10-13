using System.Globalization;
using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Persistence;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Scouts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Tests.Cli;

/// <summary>Everything <c>bench scouts</c> is pointed at in a test: a fresh database; an artefact root, a trees root and a mirror root, all
/// outside git; a REAL local repository with a parent commit P and a squash commit S (the tasks' trees are built from its bare mirror); a
/// test-owned plugin cache and settings file (the pins, and the grant guard's input — no grant); the fake CLI with its own home, replaying
/// each cell's stream by its prompt; and the price table the synthetic streams agree with. The verbs run through
/// <see cref="ScoutsCommand.RunAsync(CommandLine, ScoutVerbServices, TextWriter, TextWriter, CancellationToken)"/> exactly as
/// <c>bench scouts …</c> parses them. Nothing paid is launched.</summary>
internal sealed class ScoutsCliSetup : IAsyncDisposable
{
    public static readonly string[] GroundTruth = ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"];

    private ScoutsCliSetup(string connection, TempRoot root, DatedGitRepo repo)
    {
        Connection = connection;
        Root = root;
        Repo = repo;
        Trees = root.Sibling("trees");
        Checkouts = root.Sibling("checkouts");
        PluginCache = root.Sibling("plugin");
        SettingsFile = Path.Combine(root.Sibling("claude"), "settings.json");
        TasksFile = Path.Combine(root.Sibling("files"), "tasks.json");
        PricesFile = Path.Combine(root.Sibling("files"), "prices.json");
        Fake = new ScoutFake(root.Sibling("fake"));

        Directory.CreateDirectory(PluginCache);
        File.WriteAllText(Path.Combine(PluginCache, "code-explorer.md"), "# code explorer, pinned\n");
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, "{\"effortLevel\":\"high\"}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(TasksFile)!);
        File.WriteAllText(PricesFile, ScoutCellStreams.PricesJson);
    }

    public string Connection { get; }

    public TempRoot Root { get; }

    public DatedGitRepo Repo { get; }

    public string ArtifactRoot => Root.Path;

    public string Trees { get; }

    public string Checkouts { get; }

    public string PluginCache { get; }

    public string SettingsFile { get; }

    public string TasksFile { get; }

    public string PricesFile { get; }

    public ScoutFake Fake { get; }

    public string Parent { get; private set; } = string.Empty;

    public string Fix { get; private set; } = string.Empty;

    public IReadOnlyList<ScoutTask> Tasks { get; private set; } = [];

    public static async Task<ScoutsCliSetup> StartAsync(PostgresFixture postgres, int tasks = 1)
    {
        var setup = new ScoutsCliSetup(await postgres.NewDatabaseAsync($"scouts_cli_{Guid.NewGuid():N}"), GateStoreFixtures.NewRoot(), new DatedGitRepo(Xunit.TestContext.Current.CancellationToken));
        await setup.HistoryAsync();
        await setup.FreezeAsync(tasks);
        return setup;
    }

    /// <summary>P: a small repository with the PR's own plan in it; S: three source files changed and the plan edited.</summary>
    private async Task HistoryAsync()
    {
        await Repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        Parent = await Repo.CommitManyAsync(
            [("src/Alpha.cs", "class Alpha {}\n"), ("src/Beta.cs", "class Beta {}\n"), ("src/Gamma.cs", "class Gamma {}\n"), ("src/Other.cs", "class Other {}\n"), ("todo/PLAN_change.md", "# the plan\n")],
            ("p", "2026-06-01T10:00:00+00:00"));
        Fix = await Repo.CommitManyAsync(
            [("src/Alpha.cs", "class Alpha { int Settled; }\n"), ("src/Beta.cs", "class Beta { int Settled; }\n"), ("src/Gamma.cs", "class Gamma { int Settled; }\n"), ("todo/PLAN_change.md", "# the plan, done\n")],
            ("s", "2026-06-02T10:00:00+00:00"));
    }

    /// <summary>The tasks frozen as <c>tasks</c> would: each tree built (and its hash frozen), the plan removed and recorded.</summary>
    private async Task FreezeAsync(int count)
    {
        var builder = TreeBuilder();
        var frozen = new List<ScoutFiles.FrozenTask>();

        foreach (var i in Enumerable.Range(1, count))
        {
            var id = ScoutTaskId.Parse($"t{i:00}").Ok();
            var tree = (await builder.BuildAsync(new ScoutTreeSpec(id, "example-org/example-repo", CommitSha.Parse(Parent).Ok(), ["todo/PLAN_change.md"]), Xunit.TestContext.Current.CancellationToken)).Ok();
            var task = ScoutTask.Frozen(id, "example-org/example-repo", CommitSha.Parse(Parent).Ok(), CommitSha.Parse(Fix).Ok(), ScoutQuestionKind.HowEndToEnd,
                $"How does a cell reach its settlement after a crash, variant {i}?", GroundTruth, [], tree.Removed, tree.TreeHash).Ok();
            frozen.Add(new ScoutFiles.FrozenTask(task, []));
        }

        Tasks = [.. frozen.Select(f => f.Task)];
        await File.WriteAllTextAsync(TasksFile, ScoutFiles.WriteTasks(frozen, 1, "cutoffs.json"));
    }

    public ScoutTrees TreeBuilder() =>
        new(new ScoutTreeSettings(Trees, Repo.Url, TimeSpan.FromMinutes(2)), new GitCheckoutProvider(CheckoutCacheOptions.Under(Checkouts), NullLogger<GitCheckoutProvider>.Instance), NullLogger<ScoutTrees>.Instance);

    /// <summary>Every (task, arm) of <paramref name="arms"/> answered by its own stream; <paramref name="variant"/> picks a deviation per cell.</summary>
    public void Answer(IReadOnlyList<ScoutArmId> arms, Func<ScoutTask, ScoutArmId, CellVariant>? variant = null)
    {
        foreach (var task in Tasks)
        {
            foreach (var arm in arms.Select(ScoutArms.Of))
            {
                Fake.Answer(ScoutPrompts.ForCell(task, arm).Driver, ScoutCellStreams.For(task, arm, variant?.Invoke(task, arm.Id) ?? CellVariant.Clean));
            }
        }
    }

    public string StreamOf(ScoutTask task, ScoutArmId arm, CellVariant variant = CellVariant.Clean) => ScoutCellStreams.For(task, ScoutArms.Of(arm), variant);

    /// <summary>The run's schedule, as the verb plans it — so a test can put a deviation on the LAST cell.</summary>
    public IReadOnlyList<ScheduledCell> Schedule(IReadOnlyList<ScoutArmId> arms, int seed = 1) =>
        ScoutSchedule.Plan([.. Tasks.Select(t => t.Id)], arms, [], 1, seed).Ok();

    public ScoutVerbServices Services(IScoutCheckpoints? checkpoints = null, WorkerIdentity? owner = null) =>
        new(word => Bench.Domain.Outcome<string>.Failure($"'{word}' is not on PATH in this test"), checkpoints ?? NoScoutCheckpoints.Instance, Fake.ChildVariables, owner ?? WorkerIdentity.Here("scouts-test"));

    /// <summary>The verb, with every root, the fake, the settings, the plugin cache and the prices filled in unless the test passes its own.</summary>
    public async Task<(int Code, string Output, string Error)> RunAsync(ScoutVerbServices services, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ScoutsCommand.RunAsync(CommandLine.Parse(["scouts", .. WithDefaults(args)]), services, output, error, Xunit.TestContext.Current.CancellationToken);

        return (code, output.ToString(), error.ToString());
    }

    public string[] WithDefaults(string[] args)
    {
        var all = args.ToList();
        Default(all, "--db", Connection);
        Default(all, "--artifact-root", ArtifactRoot);
        Default(all, "--trees-root", Trees);
        Default(all, "--checkout-root", Checkouts);
        Default(all, "--plugin-cache", PluginCache);
        Default(all, "--claude-settings", SettingsFile);
        Default(all, "--repo-url-template", Repo.Url);
        Default(all, "--claude-exe", ScoutFake.Executable);
        Default(all, "--prices", PricesFile);
        return [.. all];
    }

    /// <summary><c>scouts run</c> over the frozen tasks with the given arms, ceiling and cap.</summary>
    public string[] RunArgs(IReadOnlyList<ScoutArmId> arms, decimal ceilingUsd = 50m, decimal capUsd = 1m, params string[] extra) =>
    [
        "run", "--tasks-file", TasksFile, "--opus-id", ScoutStreams.Opus, "--sonnet-id", ScoutStreams.Sonnet,
        "--ceiling-usd", ceilingUsd.ToString(CultureInfo.InvariantCulture), "--cap-usd", capUsd.ToString(CultureInfo.InvariantCulture),
        "--arms", string.Join(',', arms.Select(ScoutArms.Word)), .. extra,
    ];

    public static Guid RunIdOf(string output) => Guid.Parse(output.Split("scout run ")[1][..36]);

    public PostgresScoutStore NewStore() => new(PostgresFixture.Context(Connection), TimeProvider.System);

    private static void Default(List<string> args, string flag, string value)
    {
        if (!args.Contains(flag))
        {
            args.AddRange([flag, value]);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Fake.Dispose();
        Repo.Dispose();
        Root.Dispose();

        foreach (var sibling in new[] { Trees, Checkouts, PluginCache, Path.GetDirectoryName(SettingsFile)!, Path.GetDirectoryName(TasksFile)! })
        {
            try
            {
                TempGitRepo.DeleteTree(sibling);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A child still closing a file; the temp folder is the operating system's to clean.
            }
        }

        await Task.CompletedTask;
    }
}

/// <summary>A crash of the harness at ONE money boundary: the first time <see cref="Point"/> is reached, it throws — nothing after it in the
/// runner or the campaign happens, exactly as if the process had died there.</summary>
internal sealed class CrashAt(ScoutCheckpoint point) : IScoutCheckpoints
{
    private int _fired;

    public ScoutCheckpoint Point => point;

    public bool Fired => _fired == 1;

    public void Reached(ScoutCheckpoint checkpoint, Guid attemptId)
    {
        if (checkpoint == point && Interlocked.Exchange(ref _fired, 1) == 0)
        {
            throw new ScoutCrash(checkpoint);
        }
    }
}

internal sealed class ScoutCrash(ScoutCheckpoint at) : Exception($"the harness crashed at {at}");
