using Bench.Application;
using Bench.Application.Bank;
using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Bank;
using Bench.Domain.Models;
using Bench.Domain.Registry;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>`bench run` — plan a run, recover what a crash stranded, then actually execute its legs.
/// <para>
/// <b>It reports; it does not judge.</b> There is no bar yet, so a leg that scored badly is a number rather
/// than a failure, and the exit code says whether the MEASUREMENT happened — not whether the subject did
/// well. Turning scores into a pass before anybody has agreed a threshold is how a harness starts arguing
/// with its operator.
/// </para>
/// <para>
/// <b>It is built to be stopped and resumed.</b> The startup sweep hands back cells a dead host claimed, the
/// drain survives one leg's failure and ends the campaign when the failures stop being one leg's, and a
/// signal leaves the run resumable instead of stranded.
/// </para></summary>
public static class RunCommand
{
    /// <summary>The summary's own budget. It runs after a stop as well as after a clean drain, so it
    /// cannot use the root token — but every wait has a ceiling, so it does not use none either.</summary>
    private static readonly TimeSpan SummaryBudget = TimeSpan.FromSeconds(30);

    /// <summary>The wall ceiling for a WHOLE leg, in seconds, when the operator names none.
    /// <para>
    /// Ten minutes, which is what the model runtime was already falling back to per completion — so the
    /// default changes no timing, only who owns it. The difference that matters is the scope: this one is
    /// the ceiling for the leg, and a lane that loops cannot multiply it by a turn count nobody bounded.
    /// It is configuration (<c>--leg-wall-seconds</c>), never a constant a call site edits.
    /// </para></summary>
    private const int DefaultLegWallSeconds = 600;

    /// <summary>Where the read-only checkout cache lives when the operator names no root.
    /// <para>
    /// Under the user's local application data, and deliberately NOT under any repository: this cache holds
    /// a bare mirror per url and a worktree per commit, and the one thing a benchmark must never do is
    /// write into a tree somebody works in. The equivalent component upstream ran <c>git checkout</c> in
    /// place on a configured path, which for a benchmark means rewriting whatever a developer had open.
    /// </para></summary>
    private static string DefaultCheckoutRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bench", "checkouts");

    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        // Same two-step as `plan`, and the split is the contract rather than pedantry: an unset flag is the
        // caller's mistake (4), a named file that is not there is the machine's (3).
        var suiteFile = command.Value("suite-file");

        // Two doors to one place: a suite file, or a selection from the bank. Both end as a FROZEN, hashed
        // suite, so a result cannot tell which door its questions came through — the stamp is the identity.
        if (suiteFile.Length == 0 && command.Value("bank-group").Length == 0)
        {
            return Fail(
                error,
                "--suite-file or --bank-group is required — a run measures a frozen question set, from a file or from the bank",
                ExitCodes.Configuration);
        }

        if (suiteFile.Length > 0 && !File.Exists(suiteFile))
        {
            return Fail(error, $"suite file not found: {suiteFile}", ExitCodes.Environment);
        }

        var inputs = Read(command);

        if (inputs is Outcome<RunInputs>.Fail bad)
        {
            return Fail(error, bad.Reason, ExitCodes.Configuration);
        }

        return await StartAsync(((Outcome<RunInputs>.Ok)inputs).Value, command, output, error, stopping);
    }

    private static async Task<int> StartAsync(
        RunInputs settings, CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        await using var provider = Services(settings);

        var prepared = await PrepareAsync(provider, settings, output, error, stopping);

        if (prepared is null)
        {
            return ExitCodes.Environment;
        }

        // Recovery BEFORE work, always: the cells a crash stranded are the ones nobody is coming back for,
        // and a harness that only ever adds cells to a queue it cannot repair fills that queue with ghosts.
        await SweepCommand.RecoverAsync(provider, SweepCommand.StaleAfter(command), output, stopping);

        return await ExecuteAsync(provider, prepared.Value.Run, prepared.Value.Plan, command, output, stopping);
    }

    private static async Task<(BenchRun Run, LegPlan Plan)?> PrepareAsync(
        ServiceProvider provider, RunInputs settings, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        await using var scope = provider.CreateAsyncScope();

        if (!await MigrateAsync(scope, error, stopping))
        {
            return null;
        }

        var checkout = await CheckoutAsync(scope, settings, output, stopping);

        if (checkout is Outcome<string>.Fail unavailable)
        {
            return Refuse(error, unavailable.Reason);
        }

        var selection = await SelectAsync(scope, settings, stopping);

        return selection is Outcome<BankSelection>.Fail badSuite
            ? Refuse(error, badSuite.Reason)
            : await CreateAsync(scope, settings, ((Outcome<BankSelection>.Ok)selection).Value, output, error, stopping);
    }

    /// <summary>Puts the target's tree on disk at the pinned commit, before anything is created.
    /// <para>
    /// The provider has existed, tested, since the first commits and <b>nothing called it</b>: every run
    /// printed "the target was not checked out, so its commit is recorded but unverified" and measured
    /// against a sha nobody had confirmed exists. A commit that is unpushed, on a fork, or garbage-collected
    /// now ends the run here, by name, instead of producing a campaign of results labelled with a tree that
    /// was never seen.
    /// </para>
    /// <para>
    /// Read-only, always: a bare mirror per url and a worktree per commit under a cache root this process
    /// owns. <c>--no-checkout</c> keeps the old behaviour for a target this machine cannot clone — and keeps
    /// the warning that says the commit is unverified, because then it is.
    /// </para></summary>
    private static async Task<Outcome<string>> CheckoutAsync(
        AsyncServiceScope scope, RunInputs settings, TextWriter output, CancellationToken stopping)
    {
        if (settings.SkipCheckout)
        {
            return Outcome<string>.Success(string.Empty);
        }

        var ensured = await scope.ServiceProvider.GetRequiredService<ICheckoutProvider>()
            .EnsureAsync(settings.Target, stopping);

        return ensured.Match(
            path =>
            {
                output.WriteLine($"checkout {path}");
                return Outcome<string>.Success(path);
            },
            reason => Outcome<string>.Failure($"the target could not be checked out — {reason}"));
    }

    /// <summary>The question set, from a file or from the bank — one frozen suite either way.
    /// <para>
    /// A file-selected test carries no snapshot rows, and that absence is the honest reading: the per-test
    /// snapshot records which GROUP each question was in, and a file has no groups. A test built from the
    /// bank carries one row per question, so a later re-filing cannot move a finished report's numbers into
    /// a different column.
    /// </para></summary>
    private static async Task<Outcome<BankSelection>> SelectAsync(
        AsyncServiceScope scope, RunInputs settings, CancellationToken stopping)
    {
        if (settings.SuiteFile.Length > 0)
        {
            return SuiteJsonLoader.Load(File.ReadAllText(settings.SuiteFile), settings.Target.Commit)
                .Match(suite => Outcome<BankSelection>.Success(new BankSelection(suite, [])), Outcome<BankSelection>.Failure);
        }

        var bank = scope.ServiceProvider.GetRequiredService<IQuestionBank>();
        var found = await bank.QuestionsAsync(settings.Selection, stopping);

        return found.Match(
            entries => BankFreeze.Freeze(settings.SuiteId, entries),
            Outcome<BankSelection>.Failure);
    }

    private static async Task<bool> MigrateAsync(AsyncServiceScope scope, TextWriter error, CancellationToken stopping)
    {
        try
        {
            await scope.ServiceProvider.GetRequiredService<BenchDbContext>().Database.MigrateAsync(stopping);
            return true;
        }
        catch (Exception ex)
        {
            error.WriteLine($"bench: the store is not reachable — {ex.Message.Split('\n')[0]}");
            return false;
        }
    }

    /// <summary>The matrix, the confirmed ceilings, and the cells — in that order, because the order is
    /// the guarantee: a ceiling this runtime cannot impose stops the run BEFORE any cell exists, rather
    /// than being discovered later as a gap in a log the size of a weekend.</summary>
    private static async Task<(BenchRun Run, LegPlan Plan)?> CreateAsync(
        AsyncServiceScope scope,
        RunInputs settings,
        BankSelection selection,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        var frozen = selection.Suite;
        var resolved = await RosterAsync(scope, settings, stopping);

        if (resolved is not Outcome<SubjectRoster>.Ok(var roster))
        {
            return Refuse(error, resolved.Match(_ => string.Empty, reason => reason));
        }

        var run = BenchRun.Planned(settings.Label, settings.Target, EngineRef.Filesystem(), frozen.Stamp, DateTimeOffset.UtcNow);
        var cells = Matrix.Plan(frozen.Questions, settings.Repeats, roster.Subjects, [settings.Lane]);

        if (cells is Outcome<IReadOnlyList<MatrixCell>>.Fail badMatrix)
        {
            return Refuse(error, badMatrix.Reason);
        }

        var budgets = await BudgetConfirmation.ConfirmAsync(
            scope.ServiceProvider.GetRequiredService<IModelRuntime>(), settings.Budgets, stopping);

        if (budgets is Outcome<IReadOnlyList<Budget>>.Fail refused)
        {
            return Refuse(error, refused.Reason);
        }

        var planned = ((Outcome<IReadOnlyList<MatrixCell>>.Ok)cells).Value.Select(c => RunCell.Pending(run.Id, c)).ToList();
        var created = await scope.ServiceProvider.GetRequiredService<PostgresRunStore>().CreateAsync(run, planned, stopping);

        if (created is Outcome<BenchRun>.Fail badRun)
        {
            return Refuse(error, badRun.Reason);
        }

        var snapshot = await SnapshotAsync(scope, run.Id, selection, stopping);

        if (snapshot is Outcome<int>.Fail unsnapshotted)
        {
            return Refuse(error, unsnapshotted.Reason);
        }

        var roles = await RolesAsync(scope, run.Id, settings, stopping);

        if (roles is Outcome<int>.Fail unrecorded)
        {
            return Refuse(error, unrecorded.Reason);
        }

        var confirmed = ((Outcome<IReadOnlyList<Budget>>.Ok)budgets).Value;
        Announce(output, settings, run, selection, roster, planned.Count, confirmed);

        return (run, LegPlan.Reading(frozen, roster) with { Budgets = confirmed });
    }

    /// <summary>Who this run measures, resolved before a single cell exists.
    /// <para>
    /// Either the registry — every key looked up, every reference resolved on THIS machine, a disabled
    /// model refused by name — or the ad-hoc <c>--model</c> pair. Discovering that a key is disabled or
    /// that an environment variable is unset belongs here, not three hours into a sweep as a wall of
    /// identical transport failures.
    /// </para></summary>
    private static async Task<Outcome<SubjectRoster>> RosterAsync(
        AsyncServiceScope scope, RunInputs settings, CancellationToken stopping)
    {
        if (settings.AdHoc.Entries.Count > 0)
        {
            return Outcome<SubjectRoster>.Success(settings.AdHoc);
        }

        var registry = scope.ServiceProvider.GetRequiredService<IModelRegistry>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretSource>();
        var entries = new List<RosterEntry>(settings.SubjectKeys.Count);

        foreach (var key in settings.SubjectKeys)
        {
            var resolved = await ResolveAsync(registry, secrets, key, stopping);

            if (resolved is not Outcome<RosterEntry>.Ok(var entry))
            {
                return Outcome<SubjectRoster>.Failure(resolved.Match(_ => string.Empty, reason => reason));
            }

            entries.Add(entry);
        }

        return Outcome<SubjectRoster>.Success(SubjectRoster.Of(entries));
    }

    private static async Task<Outcome<RosterEntry>> ResolveAsync(
        IModelRegistry registry, ISecretSource secrets, string key, CancellationToken stopping)
    {
        var found = await registry.FindAsync(key, stopping);

        return found.Match(
            model => ModelResolution.Endpoint(model, secrets).Match(
                endpoint => Outcome<RosterEntry>.Success(new RosterEntry(endpoint, model.Config.Sampling)),
                Outcome<RosterEntry>.Failure),
            Outcome<RosterEntry>.Failure);
    }

    /// <summary>Records what the test chose, so a registry edit next month cannot change what a finished
    /// test says it measured.
    /// <para>
    /// An ad-hoc run — one named with <c>--model</c> rather than with registry keys — records no roles, and
    /// that absence is honest: a role names a REGISTRY key, and this run named none. Its subject is still
    /// on every cell.
    /// </para></summary>
    private static async Task<Outcome<int>> RolesAsync(
        AsyncServiceScope scope, Guid runId, RunInputs settings, CancellationToken stopping)
    {
        if (settings.SubjectKeys.Count == 0)
        {
            return Outcome<int>.Success(0);
        }

        var roles = scope.ServiceProvider.GetRequiredService<IRunRoleStore>();
        var saved = await roles.SaveSubjectsAsync(runId, Keys(settings.SubjectKeys), DateTimeOffset.UtcNow, stopping);

        return saved is Outcome<int>.Fail || settings.JudgeKeys.Count == 0
            ? saved
            : await roles.SaveJudgesAsync(runId, Keys(settings.JudgeKeys), DateTimeOffset.UtcNow, stopping);
    }

    /// <summary>Keys already parsed once by <see cref="Read"/>; this is the second half of that parse, and
    /// a key that fails here cannot reach the store.</summary>
    private static IReadOnlyList<ModelKey> Keys(IReadOnlyList<string> keys) =>
        [.. keys.Select(k => ModelKey.Parse(k)).OfType<Outcome<ModelKey>.Ok>().Select(ok => ok.Value)];

    /// <summary>Freezes which group each selected question was in. Written once, right after the cells, so
    /// a test that exists always has the snapshot its per-group report will read — and a re-filing next
    /// month cannot move this test's numbers into a different column.</summary>
    private static async Task<Outcome<int>> SnapshotAsync(
        AsyncServiceScope scope, Guid runId, BankSelection selection, CancellationToken stopping) =>
        selection.Questions.Count == 0
            ? Outcome<int>.Success(0)
            : await scope.ServiceProvider.GetRequiredService<IRunQuestionStore>()
                .SaveAsync(runId, selection.Questions, stopping);

    private static void Announce(
        TextWriter output,
        RunInputs settings,
        BenchRun run,
        BankSelection selection,
        SubjectRoster roster,
        int cells,
        IReadOnlyList<Budget> budgets)
    {
        var frozen = selection.Suite;

        output.WriteLine($"run      {run.Id}");
        output.WriteLine($"target   {settings.Target.Canonical}");
        output.WriteLine($"suite    {frozen.Stamp}  ({frozen.Questions.Count} question(s))");
        output.WriteLine($"matrix   {cells} cell(s) · lane {settings.Lane.Name}");

        // The resolved model ids, not the keys the operator typed: a registry key and the model it names
        // are two different strings, and the one a result carries is the second.
        output.WriteLine($"subjects {roster.Describe}");

        if (settings.JudgeKeys.Count > 0)
        {
            output.WriteLine($"arbiters {string.Join(", ", settings.JudgeKeys)}  (in order; the first is the primary)");
        }

        if (selection.Questions.Count > 0)
        {
            output.WriteLine(
                $"bank     {settings.Selection.Describe} — {selection.Questions.Count} question(s) frozen with their groups");
        }

        // Printed rather than assumed: this ceiling is what stands between a hung endpoint and a campaign
        // that spends days learning what its first leg already said, and it is only real once accepted.
        foreach (var budget in budgets)
        {
            output.WriteLine($"budget   {budget.Describe}");
        }

        if (settings.SkipCheckout)
        {
            output.WriteLine("warn     --no-checkout: the target's commit is recorded but UNVERIFIED, and no tree was fetched");
        }

        if (!settings.Lane.Name.Contains("tool", StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine("warn     this lane surfaces nothing — anchor recall reads 'not applicable', and a correct");
            output.WriteLine("         answer here means the subject answered from its WEIGHTS, which is the memorisation check");
        }
    }

    private static (BenchRun Run, LegPlan Plan)? Refuse(TextWriter error, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return null;
    }

    private static async Task<int> ExecuteAsync(
        ServiceProvider provider,
        BenchRun run,
        LegPlan plan,
        CommandLine command,
        TextWriter output,
        CancellationToken stopping)
    {
        // Host AND pid, not a pid alone: a sweep running on another machine would otherwise test this pid
        // against ITS own process table and confidently requeue a cell this process is still measuring.
        var owner = WorkerIdentity.Here("cli");
        var console = new LegConsole(output);

        output.WriteLine();

        var drained = await provider.GetRequiredService<LegDrain>().DrainAsync(
            token => LegAsync(provider, run.Id, owner, plan, token),
            console.Write,
            Limits(command),
            stopping);

        return await SummariseAsync(provider, run.Id, drained, command, output);
    }

    /// <summary>One leg, and the whole of it: the scope, the resolution and the work.
    /// <para>
    /// It is a single delegate on purpose — the drain wraps this in its per-unit <c>try</c>, and setup left
    /// outside that guard is the same crash through a side door.
    /// </para></summary>
    private static async Task<Outcome<LegResult>> LegAsync(
        ServiceProvider provider, Guid runId, WorkerIdentity owner, LegPlan plan, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<LegRunner>();

        return await runner.RunNextAsync(runId, owner, plan, cancellationToken);
    }

    private static DrainLimits Limits(CommandLine command) =>
        DrainLimits.Default with
        {
            ConsecutiveFailureBudget = Math.Max(
                1, command.Int("max-consecutive-failures", DrainLimits.Default.ConsecutiveFailureBudget)),
        };

    private static async Task<int> SummariseAsync(
        ServiceProvider provider, Guid runId, DrainReport drained, CommandLine command, TextWriter output)
    {
        // NOT the root token: this summary is the shutdown report itself, and a Ctrl+C that also cancelled
        // it would leave the operator with a stopped run and no idea what it did. A ceiling instead.
        using var budget = new CancellationTokenSource(SummaryBudget);

        await using var scope = provider.CreateAsyncScope();
        var results = scope.ServiceProvider.GetRequiredService<PostgresResultStore>();
        var runs = scope.ServiceProvider.GetRequiredService<PostgresRunStore>();

        var progress = await runs.ProgressAsync(runId, budget.Token);

        // Counted in the database. This line used to read every result of the run — prompt, answer and
        // every metric with its metadata — to render the two integers below.
        var scored = await results.ScoreboardAsync(runId, budget.Token);

        output.WriteLine();
        output.WriteLine($"legs     {progress.Describe}");
        output.WriteLine($"drain    {drained.Describe}");
        output.WriteLine($"scored   {scored.Passed} of {scored.Scored} passed every expectation");

        if (command.Has("json"))
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    runId,
                    progress.Settled,
                    progress.Abandoned,
                    scored = scored.Scored,
                    passed = scored.Passed,
                    stop = drained.Stop.ToString(),
                    drained.Reason,
                    faulted = drained.Faulted,
                    refused = drained.Refused,
                },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        return Exit(drained);
    }

    /// <summary>Nothing measured is NOT a pass. And a low score is not a failure either: without an agreed
    /// bar, the exit code answers "did the measurement happen", never "was the subject good".
    /// <para>
    /// The two early exits are separate answers again: a campaign that gave up after N failures in a row is
    /// an ENVIRONMENT verdict, and a campaign an operator stopped has simply not finished — both must stay
    /// distinguishable from "the subject did badly", which is not an exit code at all.
    /// </para></summary>
    private static int Exit(DrainReport drained) =>
        drained.Stop switch
        {
            DrainStop.TooManyFailures => ExitCodes.Environment,
            DrainStop.Cancelled => ExitCodes.NoReport,
            _ => drained.Scored == 0 ? ExitCodes.NoReport : ExitCodes.Pass,
        };

    private static ServiceProvider Services(RunInputs settings) =>
        CliContainer.ForRun(settings.ConnectionString, settings.CheckoutRoot, CliLogging.Start());

    private static Outcome<RunInputs> Read(CommandLine command)
    {
        var suiteFile = command.Value("suite-file");
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);
        var wallSeconds = command.Int("leg-wall-seconds", DefaultLegWallSeconds);

        if (connection.Length == 0)
        {
            return Outcome<RunInputs>.Failure("--db (or BENCH_DB) is required — a run that is not durable is not a run");
        }

        if (wallSeconds <= 0)
        {
            return Outcome<RunInputs>.Failure(
                "--leg-wall-seconds must be positive — an unbounded leg is what turns one hung endpoint into days of wall clock");
        }

        var subjectKeys = Keys(command, "subjects");

        if (subjectKeys is not Outcome<IReadOnlyList<string>>.Ok(var subjects))
        {
            return Outcome<RunInputs>.Failure(subjectKeys.Match(_ => string.Empty, reason => reason));
        }

        var judgeKeys = Keys(command, "judges");

        if (judgeKeys is not Outcome<IReadOnlyList<string>>.Ok(var judges))
        {
            return Outcome<RunInputs>.Failure(judgeKeys.Match(_ => string.Empty, reason => reason));
        }

        return RepoUrl.Parse(command.Value("repo")).Match(
            repo => CommitSha.Parse(command.Value("commit")).Match(
                commit => AdHoc(command, subjects).Match(
                    adHoc => Outcome<RunInputs>.Success(new RunInputs(
                        MeasurementTarget.At(repo, commit).Excluding([.. command.List("exclude")]),
                        suiteFile,
                        adHoc,
                        subjects,
                        judges,
                        Lane.Named(command.Value("lane", "no-tools")),
                        command.Int("repeats", 1),
                        command.Value("label", "run"),
                        connection,
                        [Budget.Of(BudgetKind.Wall, BudgetScope.Question, wallSeconds)],
                        Selection(command),
                        command.Value("suite-id", "bank-selection"),
                        command.Value("checkout-root", DefaultCheckoutRoot),
                        command.Has("no-checkout"))),
                    Outcome<RunInputs>.Failure),
                Outcome<RunInputs>.Failure),
            Outcome<RunInputs>.Failure);
    }

    /// <summary>Registry keys, parsed here so a typo is refused before anything is created.</summary>
    private static Outcome<IReadOnlyList<string>> Keys(CommandLine command, string flag)
    {
        var keys = command.List(flag);

        foreach (var key in keys)
        {
            if (ModelKey.Parse(key) is Outcome<ModelKey>.Fail bad)
            {
                return Outcome<IReadOnlyList<string>>.Failure($"--{flag}: {bad.Reason}");
            }
        }

        return Outcome<IReadOnlyList<string>>.Success(keys);
    }

    /// <summary>The ad-hoc subject pair — <c>--model</c> plus <c>--model-url</c> — or an empty roster when
    /// the run names registry keys instead.
    /// <para>
    /// Both doors stay open on purpose: the registry is how a real test is composed, and the pair is how an
    /// operator points the harness at something once without registering it. What is not allowed is
    /// neither, which is the case the model-id refusal has always covered.
    /// </para></summary>
    private static Outcome<SubjectRoster> AdHoc(CommandLine command, IReadOnlyList<string> subjectKeys) =>
        subjectKeys.Count > 0
            ? Outcome<SubjectRoster>.Success(SubjectRoster.Of([]))
            : ModelRef.Parse(command.Value("model"), Hosting(command)).Match(
                model => ModelEndpoint.Parse(
                    model,
                    command.Value("model-url"),
                    Money(command, "input-cost"),
                    Money(command, "output-cost")).Match(
                    endpoint => Outcome<SubjectRoster>.Success(
                        SubjectRoster.Of(endpoint, Sampling.Deterministic(command.Int("seed", 1)))),
                    Outcome<SubjectRoster>.Failure),
                Outcome<SubjectRoster>.Failure);

    private static ModelHosting Hosting(CommandLine command) =>
        command.Value("hosting", "local").Equals("cloud", StringComparison.OrdinalIgnoreCase)
            ? ModelHosting.Cloud
            : ModelHosting.Local;

    private static decimal Money(CommandLine command, string name) =>
        decimal.TryParse(command.Value(name, "0"), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }

    /// <summary>The bank selection, always accepted-only. A test may not measure a question nobody vouched
    /// for, and putting the filter here rather than at a call site means no future caller can forget it.</summary>
    private static BankQuery Selection(CommandLine command) =>
        BankQuery.Selection(command.Value("bank-group"), command.Int("bank-from", 0), command.Int("bank-to", 0));

    /// <param name="AdHoc">The <c>--model</c> pair as a one-subject roster, or EMPTY when the run names
    /// registry keys. Empty rather than nullable: "resolve these keys" and "use this endpoint" are two
    /// states of one thing, and a roster with no entries says the first without a second field to forget.</param>
    /// <param name="SubjectKeys">Registry keys, resolved before any cell exists.</param>
    /// <param name="JudgeKeys">The test's arbiters, in the order given: the first is the primary.</param>
    /// <param name="Budgets">The ceilings this run ASKS for. They reach a leg only after the runtime has
    /// confirmed each one — an unconfirmed budget is a budget that does not exist.</param>
    /// <param name="Selection">Which bank questions this run freezes, when it is not reading a suite file.</param>
    /// <param name="SuiteId">The name the frozen bank selection is minted under. It appears in the stamp
    /// every result carries, so it is an operator's choice rather than a generated string.</param>
    private sealed record RunInputs(
        MeasurementTarget Target,
        string SuiteFile,
        SubjectRoster AdHoc,
        IReadOnlyList<string> SubjectKeys,
        IReadOnlyList<string> JudgeKeys,
        Lane Lane,
        int Repeats,
        string Label,
        string ConnectionString,
        IReadOnlyList<Budget> Budgets,
        BankQuery Selection,
        string SuiteId,
        string CheckoutRoot,
        bool SkipCheckout);
}
