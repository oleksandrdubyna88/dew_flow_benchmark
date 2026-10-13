using System.Globalization;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Scouts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Cli;

/// <summary><c>bench scouts tasks --candidates … --cutoffs … --opus-id … --sonnet-id … --campaign &lt;seed string&gt; --out tasks.json
/// [--private-tasks-file &lt;file&gt; --campaign-key &lt;file&gt;]</c> — the mechanical, pre-registered selection of §3.1 and D10, ending in frozen tasks:
/// <list type="number">
/// <item>the cut-offs FIRST: each resolved id's published cut-off from <c>cutoffs.json</c> — an id missing, unpublished or listed twice
/// STOPS the selection (4), nothing is guessed;</item>
/// <item>every candidate read off its bare mirror: P's commit date, G (the touched files that existed at P, minus lockfiles, generated files,
/// changelogs, version manifests and the PR's own plan), the fix's lineage — one at a time, each announced as it starts (<c>reading [i/n] id …</c>),
/// since a cold mirror takes a while;</item>
/// <item>admitted only after the LATER cut-off, with P VERIFIED per merge style (S7, <see cref="ScoutParentCheck"/>), 3–15 files of G, not
/// docs-only — every exclusion printed with its reason;</item>
/// <item>the D10 draw (<see cref="ScoutSelectionDraw"/>): the campaign string IS the seed; repositories with at least 5 admissible candidates
/// ordered by SHA-256 of <c>seed|repository</c>, their candidates by SHA-256 of <c>seed|key</c>; the first 8 repositories give one MAIN task each
/// and a RESERVE behind it, repositories 9 and 10 the PILOT;</item>
/// <item>each drawn task's history-free tree BUILT (its hash is frozen on the task), the identifiers its diff added read against the tree at P,
/// and the statement LEAK-CHECKED — any leak refuses the whole selection (4) naming every token, and nothing is written.</item>
/// </list>
/// Written through the one projection: <c>tasks.json</c> (<c>--out</c>) holds the main tasks and their reserves, <c>pilot-tasks.json</c> beside it
/// the pilot; a PRIVATE task (D10, S4) is carried as its label, language, sizes and HMAC commitments under the campaign key, its detail in
/// <c>--private-tasks-file</c> (and <c>pilot-&lt;that file&gt;</c> for a private pilot task), refused inside git. A drawn private task with no key
/// or no private file refuses the selection BEFORE a tree is built. Its console lines, refusals and leak findings name the label only.
/// Launches no model; exit 3 when git or a mirror fails.</summary>
public static class ScoutsTasksCommand
{
    private const string PilotPrefix = "pilot-";

    /// <summary>The harness's own words for a PRIVATE candidate's refusal at the freeze — the only part of it a public writer carries.</summary>
    private const string StatementLeaks = "the statement leaks the answer";

    private const string NotFrozen = "refused at the freeze";

    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var refusal = Flags(command) is { Length: > 0 } flags ? flags : Collision(command);
        if (refusal.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, refusal);
        }

        var cutoff = ScoutCutoffs.Parse(await File.ReadAllTextAsync(command.Value("cutoffs"), cancellationToken))
            .Match(c => c.LaterOf(command.Value("opus-id"), command.Value("sonnet-id")), Outcome<DateOnly>.Failure);
        var candidates = ScoutFiles.Candidates(await File.ReadAllTextAsync(command.Value("candidates"), cancellationToken));

        if ((cutoff, candidates) is not (Outcome<DateOnly>.Ok { Value: var later }, Outcome<IReadOnlyList<ScoutCandidate>>.Ok { Value: var listed }))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, cutoff is Outcome<DateOnly>.Fail f ? $"cut-offs: {f.Reason}" : $"candidates: {((Outcome<IReadOnlyList<ScoutCandidate>>.Fail)candidates).Reason}");
        }

        output.WriteLine($"cut-off        the later published cut-off is {later.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} — only a P committed after it is a candidate");

        var (code, roots) = await ScoutsInputs.RootsAsync(command, artifactsRequired: false, error, cancellationToken);
        return roots is null ? code : await SelectAsync(command, roots, listed, later, output, error, cancellationToken);
    }

    private static async Task<int> SelectAsync(
        CommandLine command, ScoutRootSet roots, IReadOnlyList<ScoutCandidate> candidates, DateOnly later, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var mirrors = new GitCheckoutProvider(CheckoutCacheOptions.Under(roots.Mirrors), NullLogger<GitCheckoutProvider>.Instance);
        var settings = new ScoutTreeSettings(roots.Trees, ScoutsInputs.RepositoryUrlOf(command), TimeSpan.FromMinutes(20));
        var history = new ScoutHistory(mirrors, settings);
        var facts = new List<ScoutCandidateFacts>();

        foreach (var (candidate, at) in candidates.Select((c, i) => (c, i + 1)))
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"reading        [{at}/{candidates.Count}] {candidate.Id} …"));

            switch (await history.FactsAsync(candidate, cancellationToken))
            {
                case Outcome<ScoutCandidateFacts>.Ok ok:
                    facts.Add(ok.Value);
                    break;
                case Outcome<ScoutCandidateFacts>.Fail fail:
                    return GateRunCommand.Refuse(error, ExitCodes.Environment, fail.Reason);
                default:
                    break;
            }
        }

        var selection = ScoutCandidates.Admit(facts, later);

        foreach (var excluded in selection.Excluded)
        {
            output.WriteLine($"excluded       {excluded.Id} — {excluded.Reason}");
        }

        var draw = ScoutSelectionDraw.Of(selection.Admitted, command.Value("campaign"));
        Print(draw, selection.Admitted.Count, command.Value("campaign"), output);

        var privacy = PrivateInputs.Of(command, [.. draw.Tasks.Select(t => t.Facts)]);

        if (privacy is Outcome<PrivateInputs>.Fail unready)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, unready.Reason);
        }

        var trees = new ScoutTrees(settings, mirrors, NullLogger<ScoutTrees>.Instance);
        return await FreezeAsync(command, draw.Tasks, trees, history, ((Outcome<PrivateInputs>.Ok)privacy).Value, output, error, cancellationToken);
    }

    /// <summary>The draw as it was made, line by line — a private repository by its label and the mark, never by name.</summary>
    private static void Print(ScoutDraw draw, int admitted, string campaign, TextWriter output)
    {
        var eligible = draw.Repositories.Where(r => r.Eligible).ToList();
        var leftOut = draw.Repositories.Where(r => !r.Eligible).Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.Shown} ({r.Admissible})")).ToList();

        var ordered = string.Create(CultureInfo.InvariantCulture,
            $"repositories   {eligible.Count} with at least {ScoutSelectionDraw.MinAdmissible} admissible candidates, ordered by sha256(seed|repository): {string.Join(", ", eligible.Select(r => r.Shown))}");
        output.WriteLine(leftOut.Count > 0 ? $"{ordered}; left out: {string.Join(", ", leftOut)}" : ordered);

        var shown = draw.Repositories.ToDictionary(r => r.Repository, r => r.Shown, StringComparer.Ordinal);

        foreach (var task in draw.Tasks)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"selected       {ScoutTaskRoles.Word(task.Role),-8}{shown[task.Facts.Candidate.Repository]} → {task.Facts.Candidate.Id}"));
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"drawn          {draw.Main.Count} main + {draw.Reserves.Count} reserve + {draw.Pilot.Count} pilot of {admitted} admitted candidate(s) in {eligible.Count} eligible repository(ies), seed '{campaign}'"));

        if (draw.Main.Count < ScoutSelectionDraw.MainRepositories || draw.Pilot.Count < ScoutSelectionDraw.PilotRepositories)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"short          the D10 set is {ScoutSelectionDraw.MainRepositories} main repositories and {ScoutSelectionDraw.PilotRepositories} pilot repositories — {eligible.Count} eligible; the files hold what was drawn"));
        }
    }

    /// <summary>What a drawn PRIVATE task needs before any tree is built: the campaign's seed string, the key (read, never created), and the
    /// private file's path outside git. Nothing when no drawn candidate is private.</summary>
    private sealed record PrivateInputs(ScoutCommitter? Committer, string PrivateFile)
    {
        public static Outcome<PrivateInputs> Of(CommandLine command, IReadOnlyList<ScoutCandidateFacts> drawn)
        {
            if (!drawn.Any(d => d.Candidate.IsPrivate))
            {
                return Outcome<PrivateInputs>.Success(new PrivateInputs(null, string.Empty));
            }

            var committer = ScoutsInputs.Committer(command, command.Value("campaign"));
            var file = ScoutsInputs.LocalOnlyFile(ScoutFiles.PrivateTasksFlag, command.Value("private-tasks-file"));

            return (committer, file) switch
            {
                (Outcome<ScoutCommitter>.Fail f, _) => Outcome<PrivateInputs>.Failure($"{Count(drawn)}: {f.Reason}"),
                (_, Outcome<string>.Fail f) => Outcome<PrivateInputs>.Failure($"{Count(drawn)}: {f.Reason}"),
                (Outcome<ScoutCommitter>.Ok c, Outcome<string>.Ok p) => Outcome<PrivateInputs>.Success(new PrivateInputs(c.Value, p.Value)),
                _ => Outcome<PrivateInputs>.Failure("unreachable"),
            };
        }

        private static string Count(IReadOnlyList<ScoutCandidateFacts> drawn) =>
            $"{drawn.Count(d => d.Candidate.IsPrivate)} drawn task(s) are private ({string.Join(", ", drawn.Where(d => d.Candidate.IsPrivate).Select(d => d.Candidate.Id.Value))}); nothing was built or written";
    }

    private static async Task<int> FreezeAsync(
        CommandLine command, IReadOnlyList<ScoutDrawnTask> drawn, ScoutTrees trees, ScoutHistory history, PrivateInputs privacy, TextWriter output, TextWriter error,
        CancellationToken cancellationToken)
    {
        var frozen = new List<ScoutFiles.FrozenTask>();
        var refusals = new List<string>();

        foreach (var (facts, role) in drawn)
        {
            var c = facts.Candidate;
            var tree = await trees.BuildAsync(new ScoutTreeSpec(c.Id, c.Repository, c.Parent, c.PlanFile.Length > 0 ? [c.PlanFile] : []) { Privacy = c.Privacy }, cancellationToken);
            var added = await history.AddedLinesAsync(c, cancellationToken);

            if ((tree, added) is not (Outcome<ScoutTree>.Ok { Value: var built }, Outcome<IReadOnlyList<string>>.Ok { Value: var lines }))
            {
                return GateRunCommand.Refuse(error, ExitCodes.Environment, tree is Outcome<ScoutTree>.Fail f ? f.Reason : ((Outcome<IReadOnlyList<string>>.Fail)added).Reason);
            }

            var identifiers = ScoutGroundTruth.AddedIdentifiers(lines, IdentifiersAt(built));

            switch (ScoutTask.Frozen(c.Id, c.Repository, c.Parent, c.Fix, c.Kind, c.Statement, facts.GroundTruth, identifiers, built.Removed, built.TreeHash, c.Privacy, c.Language))
            {
                case Outcome<ScoutTask>.Ok ok:
                    frozen.Add(Committed(ok.Value, identifiers, privacy.Committer) with { Role = role });
                    output.WriteLine(ScoutPublication.FrozenLine(ok.Value, facts.GroundTruth.Count, built.TreeHash, built.Removed));
                    break;
                case Outcome<ScoutTask>.Fail fail:
                    refusals.Add(Refused(trees.Root, c, facts, identifiers, fail.Reason));
                    break;
                default:
                    break;
            }
        }

        return refusals.Count > 0
            ? GateRunCommand.Refuse(error, ExitCodes.Configuration, "nothing was written — rewrite these statements: " + string.Join(" · ", refusals))
            : await WriteAsync(command, frozen, privacy, output, error, cancellationToken);
    }

    /// <summary>A private task's commitments under the campaign key, and its per-file commitments for <c>tasks.json</c>.</summary>
    private static ScoutFiles.FrozenTask Committed(ScoutTask task, IReadOnlyList<string> identifiers, ScoutCommitter? committer) =>
        task.IsPrivate && committer is { } c
            ? new ScoutFiles.FrozenTask(task with { Commitments = c.Task(task) }, identifiers) { GroundTruthFileCommitments = c.GroundTruthFiles(task) }
            : new ScoutFiles.FrozenTask(task, identifiers);

    /// <summary>A PUBLIC candidate's refusal passes as <see cref="ScoutTask.Frozen"/> wrote it — it names the leaked tokens, which is what the
    /// operator needs to rewrite the statement. A PRIVATE candidate's is reduced to its label and the harness's own code
    /// (<see cref="ScoutPublication.Refusal"/>); the full reason, the statement and the tokens go to the withheld file under the trees root
    /// (<see cref="ScoutWithheld"/>), where nothing public reads. E2 code round: the private reason used to be joined into the public line as
    /// written, token count and all.</summary>
    private static string Refused(string treesRoot, ScoutCandidate candidate, ScoutCandidateFacts facts, IReadOnlyList<string> identifiers, string reason)
    {
        if (!candidate.IsPrivate)
        {
            return reason;
        }

        var tokens = ScoutTask.Leaks(candidate.Statement, facts.GroundTruth, identifiers);
        var detail = $"{reason}\nstatement: {candidate.Statement}\nleaks: {string.Join(", ", tokens.Select(l => $"{l.Kind} '{l.Token}'"))}";

        return ScoutWithheld.Refusal(treesRoot, candidate.Privacy, candidate.Id.Value, tokens.Count > 0 ? StatementLeaks : NotFrozen, detail);
    }

    /// <summary><c>tasks.json</c> with the main tasks and their reserves, <c>pilot-tasks.json</c> beside it with the pilot; the private detail of
    /// each to its own local file.</summary>
    private static async Task<int> WriteAsync(CommandLine command, IReadOnlyList<ScoutFiles.FrozenTask> frozen, PrivateInputs privacy, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (frozen.Count == 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.NoReport, "no candidate was admitted — nothing was frozen or written");
        }

        var campaign = privacy.Committer?.Campaign ?? command.Value("campaign");
        var cutoffs = Path.GetFileName(command.Value("cutoffs"));
        IReadOnlyList<ScoutFiles.FrozenTask> main = [.. frozen.Where(f => f.Role != ScoutTaskRole.Pilot)];
        IReadOnlyList<ScoutFiles.FrozenTask> pilot = [.. frozen.Where(f => f.Role == ScoutTaskRole.Pilot)];

        await File.WriteAllTextAsync(command.Value("out"), ScoutFiles.WriteTasks(main, campaign, cutoffs), cancellationToken);
        output.WriteLine($"written        {main.Count} frozen task(s) ({main.Count(f => f.Role == ScoutTaskRole.Main)} main, {main.Count(f => f.Role == ScoutTaskRole.Reserve)} reserve) to {command.Value("out")} — {main.Count(f => f.Task.IsPrivate)} private, by label and commitment");
        await File.WriteAllTextAsync(Beside(command.Value("out")), ScoutFiles.WriteTasks(pilot, campaign, cutoffs), cancellationToken);
        output.WriteLine($"written        {pilot.Count} pilot task(s) to {Beside(command.Value("out"))} — {pilot.Count(f => f.Task.IsPrivate)} private, by label and commitment");

        await WritePrivateAsync(privacy.PrivateFile, main, "the main and reserve", output, cancellationToken);
        await WritePrivateAsync(privacy.PrivateFile.Length > 0 ? Beside(privacy.PrivateFile) : string.Empty, pilot, "the pilot", output, cancellationToken);

        return ExitCodes.Pass;
    }

    private static async Task WritePrivateAsync(string file, IReadOnlyList<ScoutFiles.FrozenTask> tasks, string which, TextWriter output, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || !tasks.Any(t => t.Task.IsPrivate))
        {
            return;
        }

        await File.WriteAllTextAsync(file, ScoutFiles.WritePrivateTasks(tasks), cancellationToken);
        output.WriteLine($"written        the private detail of {tasks.Count(f => f.Task.IsPrivate)} of {which} task(s) to the private tasks file (outside git) — never commit it");
    }

    /// <summary>The pilot's file beside a tasks file: <c>tasks.json</c> → <c>pilot-tasks.json</c>, <c>tasks.private.json</c> → <c>pilot-tasks.private.json</c>.</summary>
    public static string Beside(string tasksFile) => Path.Combine(Path.GetDirectoryName(tasksFile) ?? string.Empty, PilotPrefix + Path.GetFileName(tasksFile));

    /// <summary>The four files the verb writes — <c>--out</c>, the pilot file beside it, <c>--private-tasks-file</c> and the pilot file beside
    /// that — resolved (links followed, the platform's own case rule: <see cref="ArtifactContainment"/>) BEFORE anything is read: two of them
    /// naming one place would have the last write win, the private detail over the public file or the reverse (E2 code round). The refusal
    /// names the flags, never the private path.</summary>
    private static string Collision(CommandLine command)
    {
        var named = Outputs(command.Value("out"), command.Value("private-tasks-file"));
        var same = named.SelectMany((a, i) => named.Skip(i + 1).Select(b => (A: a, B: b))).Where(pair => ArtifactContainment.Same(pair.A.Path, pair.B.Path)).ToList();

        return same.Count == 0
            ? string.Empty
            : $"scouts tasks would write {same[0].A.Flag} and {same[0].B.Flag} to one file — choose paths that do not collide (the pilot file is {PilotPrefix}<name> beside each); nothing was written";
    }

    private static IReadOnlyList<(string Flag, string Path)> Outputs(string tasks, string privateTasks) =>
    [
        ("--out", Resolved(tasks)),
        ("the pilot file beside --out", Resolved(Beside(tasks))),
        .. privateTasks.Length > 0
            ? new[] { ("--private-tasks-file", Resolved(privateTasks)), ("the pilot file beside --private-tasks-file", Resolved(Beside(privateTasks))) }
            : [],
    ];

    /// <summary>Canonical where it can be; a path that cannot be resolved (a link loop) compares by its full spelling, and the write refuses it later.</summary>
    private static string Resolved(string path) => ArtifactContainment.Resolve(path).Match(full => full, _ => Path.GetFullPath(path));

    /// <summary>Every identifier-shaped word in the tree at P — text files only (a file with a NUL in its first 8 KB is binary), at most 2 MB
    /// each: an identifier the diff added is one that occurs nowhere here.</summary>
    private static IReadOnlySet<string> IdentifiersAt(ScoutTree tree)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in tree.Files.Select(f => Path.Combine(tree.Root, f)).Where(f => new FileInfo(f).Length <= 2 * 1024 * 1024))
        {
            var text = File.ReadAllText(file);

            if (!text.AsSpan(0, Math.Min(text.Length, 8192)).Contains('\0'))
            {
                identifiers.UnionWith(ScoutGroundTruth.Identifiers(text));
            }
        }

        return identifiers;
    }

    private static string Flags(CommandLine command) =>
        (File.Exists(command.Value("candidates")), File.Exists(command.Value("cutoffs")), command.Value("opus-id").Length > 0 && command.Value("sonnet-id").Length > 0, command.Value("campaign").Trim().Length > 0, command.Value("out").Length > 0) switch
        {
            (false, _, _, _, _) => "scouts tasks needs --candidates <candidates.json> — the listed changes to select from, each with its merge style",
            (_, false, _, _, _) => "scouts tasks needs --cutoffs <cutoffs.json> — each resolved id's published cut-off, its source url and the date read, recorded BEFORE any candidate is listed",
            (_, _, false, _, _) => "scouts tasks needs --opus-id and --sonnet-id — the resolved ids whose cut-offs decide which changes are after them",
            (_, _, _, false, _) => "scouts tasks needs --campaign <seed string> — the one string the draw is seeded with and every private commitment is bound to (scouts-preliminary-2026-10-12)",
            (_, _, _, _, false) => "scouts tasks needs --out <tasks.json> — pilot-tasks.json is written beside it",
            _ => string.Empty,
        };
}
