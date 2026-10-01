using System.Globalization;
using Bench.Application;
using Bench.Application.Gate;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Infrastructure.Probes;

namespace Bench.Cli;

/// <summary><c>bench probes run | resume | rerun | status | sweep | report | prune</c> — the question consultant's capability
/// probes (<c>research/PLAN_question_consultant_probes.md</c>, S3). The measuring verbs are here; the reading and housekeeping ones
/// are <see cref="ProbesReadCommand"/>.
/// <para>
/// Exit codes (§5): 0 cells produced · 3 resumable — an account out, the environment (oracle, database, pin, a broken lane) ·
/// 4 configuration — flags, the subjects file, a reference that does not resolve, a pruned or unknown run · 5 nothing produced.
/// Every verb that claims runs the entry step (<see cref="ProbeCampaign.PrepareAsync"/>: the owner-checked sweep, then the
/// stranded-fixture cleanup) BEFORE it claims.
/// </para></summary>
public static class ProbesCommand
{
    public const int DefaultRepeats = 3;
    public const int DefaultCellTimeoutMinutes = 5;

    private const string SubVerbs = "run, resume, rerun, status, sweep, report or prune";

    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = NpmRegistryOracle.Timeout };
        return await RunAsync(command, ProbeVerbServices.Machine(http), output, error, cancellationToken);
    }

    /// <summary>The seam a test drives: the verb with its outside world handed in.</summary>
    public static async Task<int> RunAsync(CommandLine command, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "run" => await PlanAndRunAsync(command, services, output, error, cancellationToken),
            "resume" => await ResumeAsync(command, services, output, error, cancellationToken),
            "rerun" => await RerunAsync(command, services, output, error, cancellationToken),
            "status" => await ProbesReadCommand.StatusAsync(command, output, error, cancellationToken),
            "sweep" => await ProbesReadCommand.SweepAsync(command, output, error, cancellationToken),
            "report" => await ProbesReadCommand.ReportAsync(command, output, error, cancellationToken),
            "prune" => await ProbesReadCommand.PruneAsync(command, output, error, cancellationToken),
            var other => GateRunCommand.Refuse(error, ExitCodes.Configuration, other.Length == 0
                ? $"probes needs a sub-verb — {SubVerbs}"
                : $"unknown probes sub-verb '{other}' — {SubVerbs}"),
        };

    /// <summary><c>bench probes run --subjects-file … [--probes a,b] [--repeats 3] [--oracle-version x.y.z]</c> — refused in the order a
    /// person fixes things: flags (4) · the subjects file (3 missing, 4 malformed) · roots and database (4 / 3) · every subject's
    /// reference (4) · the oracle (3, D7: read BEFORE anything is planned) · the plan · the entry step · the campaign.</summary>
    private static async Task<int> PlanAndRunAsync(CommandLine command, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var asked = RunAsk.Of(command);
        if (asked is Outcome<RunAsk>.Fail badFlags)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, badFlags.Reason);
        }

        var ask = ((Outcome<RunAsk>.Ok)asked).Value;
        if (!File.Exists(ask.SubjectsFile))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, $"the subjects file {Path.GetFileName(ask.SubjectsFile)} is not there");
        }

        var subjects = ProbeSubjectsFile.Read(await File.ReadAllTextAsync(ask.SubjectsFile, cancellationToken));
        if (subjects is Outcome<IReadOnlyList<ProbeSubject>>.Fail badFile)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"{Path.GetFileName(ask.SubjectsFile)}: {badFile.Reason}");
        }

        var (code, inputs) = await ProbesInputs.LoadAsync(command, artifactsRequired: true, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            return await ResolveAndPlanAsync(command, ask, ((Outcome<IReadOnlyList<ProbeSubject>>.Ok)subjects).Value, inputs, services, output, error, cancellationToken);
        }
    }

    private static async Task<int> ResolveAndPlanAsync(
        CommandLine command, RunAsk ask, IReadOnlyList<ProbeSubject> subjects, ProbesInputs inputs, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var executables = ProbesInputs.Executables(subjects, services);
        if (executables is Outcome<IReadOnlyDictionary<string, string>>.Fail unresolved)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, unresolved.Reason);
        }

        var oracle = ask.Oracle.Length > 0
            ? ProbeOracle.Parse(ask.Oracle, OracleSource.Manual)
            : await services.Oracle.LatestAsync(cancellationToken);
        if (oracle is Outcome<ProbeOracle>.Fail unread)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment,
                $"the web oracle could not be read — {unread.Reason}; nothing was planned (pass --oracle-version <semver> to pin it by hand)");
        }

        var planned = await PlanAsync(inputs, ask, subjects, ((Outcome<ProbeOracle>.Ok)oracle).Value, output, cancellationToken);
        if (planned is Outcome<ProbeRun>.Fail notPlanned)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, notPlanned.Reason);
        }

        var run = ((Outcome<ProbeRun>.Ok)planned).Value;
        return await DriveAsync(command, inputs, run, ((Outcome<IReadOnlyDictionary<string, string>>.Ok)executables).Value, services, output, error, cancellationToken);
    }

    private static async Task<Outcome<ProbeRun>> PlanAsync(
        ProbesInputs inputs, RunAsk ask, IReadOnlyList<ProbeSubject> subjects, ProbeOracle oracle, TextWriter output, CancellationToken cancellationToken)
    {
        var plan = ProbeMatrix.Plan(ask.Probes, subjects, ask.Repeats);
        var run = ProbeRun.Planned(Guid.CreateVersion7(), oracle, subjects, ask.Repeats, DateTimeOffset.UtcNow)
            .Match(r => Outcome<ProbeRun>.Success(r with { Probes = ask.Probes }), Outcome<ProbeRun>.Failure);

        if ((plan, run) is not (Outcome<ProbePlan>.Ok { Value: var matrix }, Outcome<ProbeRun>.Ok { Value: var planned }))
        {
            return Outcome<ProbeRun>.Failure(plan is Outcome<ProbePlan>.Fail f ? f.Reason : ((Outcome<ProbeRun>.Fail)run).Reason);
        }

        var stored = await inputs.Store.PlanAsync(planned, [.. matrix.Cells.Select(c => ProbeCell.Pending(Guid.CreateVersion7(), planned.Id, c))], cancellationToken);

        if (stored is Outcome<ProbeRun>.Ok)
        {
            output.WriteLine($"planned        probe run {planned.Id} — {matrix.Cells.Count} cell(s): {ask.Probes.Count} probe(s) × {subjects.Count} subject(s) × {ask.Repeats} repeat(s)");
            output.WriteLine($"oracle         @openai/codex {oracle.Version} ({ProbeReport.Word(oracle.Source)})");
            foreach (var dropped in matrix.Dropped)
            {
                output.WriteLine($"not measured   {dropped.Describe}");
            }
        }

        return stored;
    }

    /// <summary><c>bench probes resume --run &lt;id&gt;</c> — the run's frozen subjects and oracle, never re-read; exactly the cells that
    /// are not settled.</summary>
    private static async Task<int> ResumeAsync(CommandLine command, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "probes resume needs --run <guid>");
        }

        var (code, inputs) = await ProbesInputs.LoadAsync(command, artifactsRequired: true, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            var run = await inputs.OpenRunAsync(runId, cancellationToken);
            var executables = run.Match(r => ProbesInputs.Executables(r.Subjects, services), Outcome<IReadOnlyDictionary<string, string>>.Failure);

            return (run, executables) switch
            {
                (Outcome<ProbeRun>.Ok ok, Outcome<IReadOnlyDictionary<string, string>>.Ok resolved) =>
                    await DriveAsync(command, inputs, ok.Value, resolved.Value, services, output, error, cancellationToken),
                (Outcome<ProbeRun>.Fail fail, _) => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                (_, Outcome<IReadOnlyDictionary<string, string>>.Fail fail) => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
    }

    /// <summary><c>bench probes rerun --cell &lt;id&gt; | --run &lt;id&gt; --subject s [--probe p]</c> — a new generation per named lineage
    /// (D2), measured by lanes of the named subjects only; generation 1 stays.</summary>
    private static async Task<int> RerunAsync(CommandLine command, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var asked = RerunAsk.Of(command);
        if (asked is Outcome<RerunAsk>.Fail badFlags)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, badFlags.Reason);
        }

        var (code, inputs) = await ProbesInputs.LoadAsync(command, artifactsRequired: true, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            var run = await RunOfAsync(inputs, ((Outcome<RerunAsk>.Ok)asked).Value, cancellationToken);

            return run is Outcome<ProbeRun>.Ok ok
                ? await RerunRunAsync(command, inputs, ok.Value, ((Outcome<RerunAsk>.Ok)asked).Value, services, output, error, cancellationToken)
                : GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<ProbeRun>.Fail)run).Reason);
        }
    }

    private static async Task<Outcome<ProbeRun>> RunOfAsync(ProbesInputs inputs, RerunAsk ask, CancellationToken cancellationToken)
    {
        var runId = ask switch
        {
            RerunAsk.Slice slice => Outcome<Guid>.Success(slice.Run),
            RerunAsk.OneCell one => (await inputs.Store.CellAsync(one.Cell, cancellationToken)).Match(c => Outcome<Guid>.Success(c.RunId), Outcome<Guid>.Failure),
            _ => throw new InvalidOperationException("unreachable"),
        };

        return runId is Outcome<Guid>.Ok id ? await inputs.OpenRunAsync(id.Value, cancellationToken) : Outcome<ProbeRun>.Failure(((Outcome<Guid>.Fail)runId).Reason);
    }

    private static async Task<int> RerunRunAsync(
        CommandLine command, ProbesInputs inputs, ProbeRun run, RerunAsk ask, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        await PrepareAsync(command, inputs, run, output, cancellationToken);

        var cells = await inputs.Store.CellsAsync(run.Id, cancellationToken);
        var seeds = ask switch
        {
            RerunAsk.Slice slice => ProbeRerunTargets.ForSlice(run, cells, slice.Subject, slice.Probes),
            RerunAsk.OneCell one => ProbeRerunTargets.ForCell(cells, one.Cell),
            _ => throw new InvalidOperationException("unreachable"),
        };

        if (seeds is Outcome<IReadOnlyList<ProbeCell>>.Fail refused)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, refused.Reason);
        }

        var targets = ((Outcome<IReadOnlyList<ProbeCell>>.Ok)seeds).Value;
        var narrowed = run with { Subjects = [.. run.Subjects.Where(s => targets.Any(t => t.Subject == s.Id))] };
        var executables = ProbesInputs.Executables(narrowed.Subjects, services);

        if (executables is Outcome<IReadOnlyDictionary<string, string>>.Fail unresolved)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, unresolved.Reason);
        }

        var appended = await AppendAsync(inputs, targets, output, cancellationToken);
        return appended.Length > 0
            ? GateRunCommand.Refuse(error, ExitCodes.Configuration, appended)
            : await CampaignAsync(command, inputs, narrowed, ((Outcome<IReadOnlyDictionary<string, string>>.Ok)executables).Value, services, output, error, cancellationToken);
    }

    /// <summary>One generation per seed; the first refusal (a concurrent re-run took the number) stops the rest and is reported.</summary>
    private static async Task<string> AppendAsync(ProbesInputs inputs, IReadOnlyList<ProbeCell> seeds, TextWriter output, CancellationToken cancellationToken)
    {
        foreach (var seed in seeds)
        {
            switch (await inputs.Store.NextGenerationAsync(seed.Id, Guid.CreateVersion7(), cancellationToken))
            {
                case Outcome<ProbeCell>.Ok next:
                    output.WriteLine($"appended       {Name(next.Value)} — cell {next.Value.Id}, beside generation {seed.Generation}");
                    break;
                case Outcome<ProbeCell>.Fail fail:
                    return fail.Reason;
                default:
                    break;
            }
        }

        return string.Empty;
    }

    /// <summary>The entry step, then the campaign — <c>run</c> and <c>resume</c>.</summary>
    private static async Task<int> DriveAsync(
        CommandLine command, ProbesInputs inputs, ProbeRun run, IReadOnlyDictionary<string, string> executables, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        await PrepareAsync(command, inputs, run, output, cancellationToken);
        output.WriteLine($"measuring      probe run {run.Id} — {(await inputs.Store.CellsAsync(run.Id, cancellationToken)).Count(c => c.State == CellState.Pending)} pending cell(s)");

        return await CampaignAsync(command, inputs, run, executables, services, output, error, cancellationToken);
    }

    /// <summary>S2's entry step: the owner-checked sweep, then every fixture folder of the run no live owner holds.</summary>
    internal static async Task<ProbePrepareReport> PrepareAsync(CommandLine command, ProbesInputs inputs, ProbeRun run, TextWriter output, CancellationToken cancellationToken)
    {
        var prepared = await inputs.Campaign().PrepareAsync(inputs.Store, run, ProbesInputs.StaleAfter(command), cancellationToken);
        output.WriteLine(Prepared(prepared));
        return prepared;
    }

    internal static string Prepared(ProbePrepareReport prepared) =>
        $"prepared       {prepared.Sweep.Requeued} claim(s) handed back, {prepared.Sweep.Abandoned} abandoned; "
        + $"{prepared.StrandedFixturesDeleted} stranded fixture folder(s) deleted";

    private static async Task<int> CampaignAsync(
        CommandLine command, ProbesInputs inputs, ProbeRun run, IReadOnlyDictionary<string, string> executables, ProbeVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var wall = TimeSpan.FromMinutes(command.Int("cell-timeout-minutes", DefaultCellTimeoutMinutes));
        var options = new ProbeCampaignOptions(DrainLimits.Default, ProbesInputs.StaleAfter(command)) { Progress = Printer(output) };
        var report = await inputs.Campaign().RunAsync(new ProbeCampaignInputs(run, executables), options, s => inputs.Lane(s, executables, wall, services), cancellationToken);

        return report switch
        {
            Outcome<ProbeCampaignReport>.Ok ok => await FinishAsync(inputs, run, ok.Value, output, error),
            Outcome<ProbeCampaignReport>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static async Task<int> FinishAsync(ProbesInputs inputs, ProbeRun run, ProbeCampaignReport report, TextWriter output, TextWriter error)
    {
        var cells = await inputs.Store.CellsAsync(run.Id, CancellationToken.None);
        var resume = $"bench probes resume --run {run.Id}";

        output.WriteLine($"campaign       {report.Settled} cell(s) settled, {report.Unmeasured} subject(s) benched, {report.Refused} refused, {report.Faulted} faulted — {report.Stop}: {report.Reason}");
        if (Pending(cells) is { Length: > 0 } pending)
        {
            output.WriteLine($"pending        {pending}");
            output.WriteLine($"resume         {resume}");
        }

        return report.Stop switch
        {
            CampaignStop.AccountOut or CampaignStop.PinUnreadable or CampaignStop.TooManyFailures =>
                GateRunCommand.Refuse(error, ExitCodes.Environment, $"{report.Reason} ({resume})"),
            CampaignStop.Drained when report.Settled > 0 => ExitCodes.Pass,
            CampaignStop.Cancelled => GateRunCommand.Refuse(error, ExitCodes.NoReport, $"stopped after {report.Settled} cell(s) — the run is resumable ({resume})"),
            _ => GateRunCommand.Refuse(error, ExitCodes.NoReport, $"no cell was produced — {report.Reason}"),
        };
    }

    /// <summary>The cells still pending, per subject — what a benched subject left for the resume.</summary>
    private static string Pending(IReadOnlyList<ProbeCell> cells) =>
        string.Join(" · ", cells.Where(c => c.State == CellState.Pending)
            .GroupBy(c => c.Subject.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} {g.Count()}"));

    internal static string Name(ProbeCell cell) =>
        $"{ProbeWord.Of(cell.Probe)}/{cell.Subject}/r{cell.Repeat.ToString(CultureInfo.InvariantCulture)} g{cell.Generation.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>One line per cell as it ends, under a lock — lanes finish at once.</summary>
    private static Action<string> Printer(TextWriter output)
    {
        var gate = new Lock();
        return line =>
        {
            lock (gate)
            {
                output.WriteLine(line);
            }
        };
    }

    /// <summary>What <c>run</c> was asked for, read and refused by name before anything is opened.</summary>
    private sealed record RunAsk(string SubjectsFile, IReadOnlyList<ProbeKind> Probes, int Repeats, string Oracle)
    {
        public static Outcome<RunAsk> Of(CommandLine command)
        {
            var probes = command.Has("probes") ? command.List("probes").Select(ProbeWord.Parse).ToList() : [.. ProbeWord.All.Select(Outcome<ProbeKind>.Success)];
            var oracle = command.Value("oracle-version");

            var refusal = (command.Value("subjects-file").Length > 0, probes.OfType<Outcome<ProbeKind>.Fail>().FirstOrDefault(), command.Int("repeats", DefaultRepeats) >= 1,
                    oracle.Length == 0 || ProbeOracle.Parse(oracle, OracleSource.Manual) is Outcome<ProbeOracle>.Ok) switch
            {
                (false, _, _, _) => "probes run needs --subjects-file <subjects.json> (samples/question-consultant-probe-subjects.json is the plan's)",
                (_, { } bad, _, _) => $"--probes: {bad.Reason}",
                (_, _, false, _) => "--repeats must be at least 1 (three is the floor for a variance)",
                (_, _, _, false) => $"--oracle-version: {((Outcome<ProbeOracle>.Fail)ProbeOracle.Parse(oracle, OracleSource.Manual)).Reason}",
                _ => string.Empty,
            };

            return refusal.Length > 0
                ? Outcome<RunAsk>.Failure(refusal)
                : Outcome<RunAsk>.Success(new RunAsk(command.Value("subjects-file"), [.. probes.OfType<Outcome<ProbeKind>.Ok>().Select(p => p.Value)], command.Int("repeats", DefaultRepeats), oracle));
        }
    }

    /// <summary>What <c>rerun</c> was asked for: one cell's lineage, or a run's subject slice (no probes = every probe).</summary>
    private abstract record RerunAsk
    {
        private RerunAsk()
        {
        }

        public sealed record OneCell(Guid Cell) : RerunAsk;

        public sealed record Slice(Guid Run, ProbeSubjectId Subject, IReadOnlyList<ProbeKind> Probes) : RerunAsk;

        public static Outcome<RerunAsk> Of(CommandLine command)
        {
            var cell = Guid.TryParse(command.Value("cell"), out var c) ? c : Guid.Empty;
            var run = Guid.TryParse(command.Value("run"), out var r) ? r : Guid.Empty;
            var subject = ProbeSubjectId.Parse(command.Value("subject"));
            var probes = command.List("probe").Select(ProbeWord.Parse).ToList();

            return (cell != Guid.Empty, run != Guid.Empty, subject, probes.OfType<Outcome<ProbeKind>.Fail>().FirstOrDefault()) switch
            {
                (true, true, _, _) => Outcome<RerunAsk>.Failure("probes rerun takes --cell <id> OR --run <id> --subject <id>, not both"),
                (false, false, _, _) => Outcome<RerunAsk>.Failure("probes rerun needs --cell <id> (one cell's lineage) or --run <id> --subject <id> [--probe <word>]"),
                (_, _, _, { } bad) => Outcome<RerunAsk>.Failure($"--probe: {bad.Reason}"),
                (true, _, _, _) => Outcome<RerunAsk>.Success(new OneCell(cell)),
                (_, _, Outcome<ProbeSubjectId>.Ok ok, _) => Outcome<RerunAsk>.Success(new Slice(run, ok.Value, [.. probes.OfType<Outcome<ProbeKind>.Ok>().Select(p => p.Value)])),
                _ => Outcome<RerunAsk>.Failure("probes rerun --run needs --subject <id> — a re-run names what it re-measures; the whole run is never re-run"),
            };
        }
    }
}
