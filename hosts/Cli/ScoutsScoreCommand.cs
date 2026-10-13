using System.Globalization;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts score --run &lt;id&gt; [--handcheck N --seed S] [--out file] [--deny-list file]</c> (E1 S3, §3.6/§3.7).
/// <list type="bullet">
/// <item><b>score</b>: every scored cell's recall RECOMPUTED from its stream through the matcher over its task's tree, and stored
/// (<see cref="ScoutScoring"/>). Needs the artefact root (the streams) and the trees.</item>
/// <item><b>--handcheck N --seed S</b>: draws N found and N not-found (output, G file) pairs and N claim verdicts, deterministically by the
/// seed (<see cref="ScoutHandcheck"/>), into a Markdown file a person fills in item by item — <c>--out</c>, by default
/// <c>scouts/&lt;run&gt;/handcheck-seed&lt;S&gt;.md</c> under the artefact root. The file is scrubbed (§3.13) before it is written: home paths,
/// e-mail addresses and every entry of the local deny-list (<c>--deny-list</c>, else <c>BENCH_SCOUT_DENYLIST</c>).</item>
/// </list>
/// Exit codes: 0 scored · 3 a cell could not be read or written (its stream gone, its tree unpreparable) · 4 a flag, an unknown run, or a
/// deny-list that is named (flag or variable) and does not load — refused before anything is scored or written ·
/// 5 no scored cell.</summary>
public static class ScoutsScoreCommand
{
    public static Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var handcheck = command.Int("handcheck", 0);

        return handcheck is < 0 or > 200 || handcheck > 0 && !command.Has("seed")
            ? Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts score --handcheck takes 1 to 200 items per kind and needs --seed <int> — the draw is reproducible only from its seed"))
            : DenyList(command).Match(
                denyList => ScoutsReadCommand.WithRunAsync(command, artifactsRequired: true, "score", error, cancellationToken, (inputs, run) => ScoreAsync(command, inputs, run, handcheck, denyList, output, error, cancellationToken)),
                reason => Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, reason)));
    }

    private static async Task<int> ScoreAsync(
        CommandLine command, ScoutsInputs inputs, ScoutRun run, int handcheck, ScrubDenyList denyList, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var tasks = await inputs.Store.TasksAsync(run.Id, cancellationToken);
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
            await DrawAsync(command, inputs, run, handcheck, denyList, tasks, cells, attempts, scored.Pairs, output, cancellationToken);
        }

        return (scored.Rescored, scored.Unreadable.Count) switch
        {
            (0, 0) => GateRunCommand.Refuse(error, ExitCodes.NoReport, $"scout run {run.Id} has no scored cell — nothing to score"),
            (_, > 0) => GateRunCommand.Refuse(error, ExitCodes.Environment, $"{scored.Unreadable.Count} scored cell(s) could not be rescored — see the lines above"),
            _ => ExitCodes.Pass,
        };
    }

    private static async Task DrawAsync(
        CommandLine command, ScoutsInputs inputs, ScoutRun run, int perKind, ScrubDenyList denyList, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells,
        IReadOnlyList<ScoutAttempt> attempts, IReadOnlyList<HandcheckPair> pairs, TextWriter output, CancellationToken cancellationToken)
    {
        var seed = command.Int("seed", 0);
        var verdicts = (await inputs.Reads.VerdictsAsync(run.Id, cancellationToken)).ToDictionary(v => v.Key);
        var claims = ScoutSettledOutputs.Of(tasks, cells, attempts, inputs.Artifacts).Outputs
            .SelectMany(o => ScoutAssessment.ClaimsOf(o.Cell.Id, o.Task, o.Arm, o.Transcript))
            .Where(c => verdicts.ContainsKey(c.Key))
            .Select(c => new HandcheckClaim(c.Key, c.Task, c.Claim, verdicts[c.Key].Verdict))
            .ToList();
        var draw = ScoutHandcheck.Draw(pairs, claims, perKind, seed);
        var path = command.Has("out")
            ? Path.GetFullPath(command.Value("out"))
            : Path.Combine(inputs.Roots.Artifacts, ScoutArtifactsFolder, run.Id.ToString("D"), string.Create(CultureInfo.InvariantCulture, $"handcheck-seed{seed}.md"));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, ScoutScrubber.Scrub(ScoutHandcheck.Markdown(run.Id, draw), denyList), cancellationToken);
        output.WriteLine($"hand-check     {draw.Found.Count} found + {draw.NotFound.Count} not-found pair(s) and {draw.Claims.Count} claim verdict(s), seed {seed} → {path}");
    }

    private const string ScoutArtifactsFolder = Infrastructure.Scouts.ScoutArtifacts.Folder;

    private const string DenyListVariable = "BENCH_SCOUT_DENYLIST";

    /// <summary>The operator's local deny-list (§3.13) — never committed; none when neither flag nor variable names a file. A path that IS
    /// named must load: a missing or unreadable file refuses the verb (4) before anything is scored or written, because a silently empty
    /// deny-list publishes exactly the names it exists to hide.</summary>
    private static Outcome<ScrubDenyList> DenyList(CommandLine command) =>
        command.Value("deny-list", Environment.GetEnvironmentVariable(DenyListVariable) ?? string.Empty) switch
        {
            { Length: 0 } => Outcome<ScrubDenyList>.Success(ScrubDenyList.None),
            var file => Load(file, command.Has("deny-list") ? "--deny-list" : DenyListVariable),
        };

    private static Outcome<ScrubDenyList> Load(string file, string source)
    {
        var refusal = $"the deny-list {file} (named by {source}) does not load — fix the path; nothing was scored or written, because an empty deny-list would publish the private names it exists to hide";

        try
        {
            return File.Exists(file) ? Outcome<ScrubDenyList>.Success(ScrubDenyList.Parse(File.ReadAllText(file))) : Outcome<ScrubDenyList>.Failure(refusal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<ScrubDenyList>.Failure($"{refusal} ({ex.Message})");
        }
    }
}
