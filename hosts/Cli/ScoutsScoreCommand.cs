using System.Globalization;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts score --run &lt;id&gt; [--handcheck N --seed S] [--out file] [--private-out file] [--deny-list file] [--campaign-key file]</c>
/// (E1 S3, §3.6/§3.7; S4 for the private tasks).
/// <list type="bullet">
/// <item><b>score</b>: every scored cell's recall RECOMPUTED from its stream through the matcher over its task's tree, and stored
/// (<see cref="ScoutScoring"/>). Needs the artefact root (the streams) and the trees.</item>
/// <item><b>--handcheck N --seed S</b>: draws N found and N not-found (output, G file) pairs and N claim verdicts, deterministically by the
/// seed (<see cref="ScoutHandcheck"/>), into a Markdown file a person fills in item by item — <c>--out</c>, by default
/// <c>scouts/&lt;run&gt;/handcheck-seed&lt;S&gt;.md</c> under the artefact root. The file is scrubbed (§3.13) before it is written: home paths,
/// e-mail addresses and every entry of the local deny-list (<c>--deny-list</c>, else <c>BENCH_SCOUT_DENYLIST</c>). A PRIVATE task's items are
/// shown by label and commitment (the campaign key is needed), and their plaintext goes to the LOCAL companion — <c>--private-out</c>, by
/// default beside <c>--out</c> as <c>*.private.md</c> — refused inside git.</item>
/// </list>
/// Exit codes: 0 scored · 3 a cell could not be read or written (its stream gone, its tree unpreparable) · 4 a flag, an unknown run, a
/// deny-list that is named (flag or variable) and does not load, a private task without the key or with a companion path inside git —
/// refused before anything is scored or written · 5 no scored cell.</summary>
public static class ScoutsScoreCommand
{
    public static Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var handcheck = command.Int("handcheck", 0);

        return handcheck is < 0 or > 200 || handcheck > 0 && !command.Has("seed")
            ? Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts score --handcheck takes 1 to 200 items per kind and needs --seed <int> — the draw is reproducible only from its seed"))
            : ScoutsInputs.DenyList(command).Match(
                denyList => ScoutsReadCommand.WithRunAsync(command, artifactsRequired: true, "score", error, cancellationToken, (inputs, run) => ScoreAsync(command, inputs, run, handcheck, denyList, output, error, cancellationToken)),
                reason => Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, reason)));
    }

    private static async Task<int> ScoreAsync(
        CommandLine command, ScoutsInputs inputs, ScoutRun run, int handcheck, ScrubDenyList denyList, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var tasks = await inputs.Store.TasksAsync(run.Id, cancellationToken);
        var privacy = handcheck > 0 ? Privacy(command, inputs, run, tasks) : Outcome<HandcheckFiles>.Success(HandcheckFiles.None);

        if (privacy is Outcome<HandcheckFiles>.Fail unready)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, unready.Reason);
        }

        var cells = await inputs.Store.CellsAsync(run.Id, cancellationToken);
        var attempts = await inputs.Store.AttemptsAsync(run.Id, cancellationToken);
        var scored = await new ScoutScoring(inputs.Verdicts, inputs.Trees, inputs.Artifacts).RunAsync(tasks, cells, attempts, cancellationToken);

        output.WriteLine($"scored         scout run {run.Id} — {scored.Rescored} scored cell(s) rescored from their streams, {scored.Changed} changed");
        foreach (var problem in scored.Unreadable)
        {
            output.WriteLine($"unreadable     {problem}");
        }

        if (handcheck > 0)
        {
            await DrawAsync(command, inputs, run, handcheck, denyList, ((Outcome<HandcheckFiles>.Ok)privacy).Value, tasks, cells, attempts, scored.Pairs, output, cancellationToken);
        }

        return (scored.Rescored, scored.Unreadable.Count) switch
        {
            (0, 0) => GateRunCommand.Refuse(error, ExitCodes.NoReport, $"scout run {run.Id} has no scored cell — nothing to score"),
            (_, > 0) => GateRunCommand.Refuse(error, ExitCodes.Environment, $"{scored.Unreadable.Count} scored cell(s) could not be rescored — see the lines above"),
            _ => ExitCodes.Pass,
        };
    }

    /// <summary>Where the hand-check goes, and how a private item is shown: the public file, the committer and the companion's path when the
    /// run holds a private task — all decided BEFORE anything is scored or written.</summary>
    private sealed record HandcheckFiles(ScoutHandcheckPrivacy Privacy, string Companion)
    {
        public static HandcheckFiles None { get; } = new(ScoutHandcheckPrivacy.AllPublic, string.Empty);
    }

    private static Outcome<HandcheckFiles> Privacy(CommandLine command, ScoutsInputs inputs, ScoutRun run, IReadOnlyList<ScoutTask> tasks)
    {
        var privateTasks = tasks.Where(t => t.IsPrivate).Select(t => t.Id.Value).ToHashSet(StringComparer.Ordinal);

        if (privateTasks.Count == 0)
        {
            return Outcome<HandcheckFiles>.Success(HandcheckFiles.None);
        }

        var committer = ScoutsInputs.Committer(command, run.Campaign);
        var companion = ScoutsInputs.LocalOnlyFile("--private-out", command.Value("private-out", DefaultCompanion(command, inputs, run)));

        return (committer, companion) switch
        {
            (Outcome<ScoutCommitter>.Fail f, _) => Outcome<HandcheckFiles>.Failure($"the run holds {privateTasks.Count} private task(s) — {f.Reason}"),
            (_, Outcome<string>.Fail f) => Outcome<HandcheckFiles>.Failure($"the run holds {privateTasks.Count} private task(s) — {f.Reason}"),
            (Outcome<ScoutCommitter>.Ok c, Outcome<string>.Ok p) => Outcome<HandcheckFiles>.Success(new HandcheckFiles(new ScoutHandcheckPrivacy(privateTasks, c.Value), p.Value)),
            _ => Outcome<HandcheckFiles>.Failure("unreachable"),
        };
    }

    private static string PublicPath(CommandLine command, ScoutsInputs inputs, ScoutRun run) =>
        command.Has("out")
            ? Path.GetFullPath(command.Value("out"))
            : Path.Combine(inputs.Roots.Artifacts, ScoutArtifactsFolder, run.Id.ToString("D"), string.Create(CultureInfo.InvariantCulture, $"handcheck-seed{command.Int("seed", 0)}.md"));

    private static string DefaultCompanion(CommandLine command, ScoutsInputs inputs, ScoutRun run) =>
        Path.ChangeExtension(PublicPath(command, inputs, run), ".private.md");

    private static async Task DrawAsync(
        CommandLine command, ScoutsInputs inputs, ScoutRun run, int perKind, ScrubDenyList denyList, HandcheckFiles files, IReadOnlyList<ScoutTask> tasks,
        IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutAttempt> attempts, IReadOnlyList<HandcheckPair> pairs, TextWriter output, CancellationToken cancellationToken)
    {
        var seed = command.Int("seed", 0);
        var verdicts = (await inputs.Reads.VerdictsAsync(run.Id, cancellationToken)).ToDictionary(v => v.Key);
        var claims = ScoutSettledOutputs.Of(tasks, cells, attempts, inputs.Artifacts).Outputs
            .SelectMany(o => ScoutAssessment.ClaimsOf(o.Cell.Id, o.Task, o.Arm, o.Transcript))
            .Where(c => verdicts.ContainsKey(c.Key))
            .Select(c => new HandcheckClaim(c.Key, c.Task, c.Claim, verdicts[c.Key].Verdict))
            .ToList();
        var draw = ScoutHandcheck.Draw(pairs, claims, perKind, seed);
        var path = PublicPath(command, inputs, run);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, ScoutScrubber.Scrub(ScoutHandcheck.Markdown(run.Id, draw, files.Privacy), denyList), cancellationToken);
        output.WriteLine($"hand-check     {draw.Found.Count} found + {draw.NotFound.Count} not-found pair(s) and {draw.Claims.Count} claim verdict(s), seed {seed} → {path}");

        if (files.Companion.Length > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(files.Companion)!);
            await File.WriteAllTextAsync(files.Companion, ScoutHandcheck.PrivateCompanion(run.Id, draw, files.Privacy.PrivateTasks), cancellationToken);
            output.WriteLine($"hand-check     the private items' plaintext → the companion file (outside git) — never commit it");
        }
    }

    private const string ScoutArtifactsFolder = Infrastructure.Scouts.ScoutArtifacts.Folder;
}
