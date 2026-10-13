using System.Globalization;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Scouts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Cli;

/// <summary><c>bench scouts tasks --candidates … --cutoffs … --opus-id … --sonnet-id … --out tasks.json [--pilot a,b] [--seed 1]
/// [--per-repository 1]</c> — the mechanical, pre-registered selection of §3.1, ending in frozen tasks:
/// <list type="number">
/// <item>the cut-offs FIRST: each resolved id's published cut-off from <c>cutoffs.json</c> — an id missing, unpublished or listed twice
/// STOPS the selection (4), nothing is guessed;</item>
/// <item>every candidate read off its bare mirror: P's commit date, G (the touched files that existed at P, minus lockfiles, generated files,
/// changelogs, version manifests and the PR's own plan) — one at a time, each announced as it starts (<c>reading [i/n] id …</c>), since a
/// cold mirror takes a while;</item>
/// <item>admitted only after the LATER cut-off, never a pilot task, 3–15 files of G, not docs-only — every exclusion printed with its reason;</item>
/// <item>a seeded draw per repository;</item>
/// <item>each drawn task's history-free tree BUILT (its hash is frozen on the task), the identifiers its diff added read against the tree at P,
/// and the statement LEAK-CHECKED — any leak refuses the whole selection (4) naming every token, and nothing is written.</item>
/// </list>
/// Launches no model; exit 3 when git or a mirror fails.</summary>
public static class ScoutsTasksCommand
{
    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var refusal = Flags(command);
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

        var selection = ScoutCandidates.Admit(facts, later, command.List("pilot").ToHashSet(StringComparer.Ordinal));
        var drawn = ScoutCandidates.Draw(selection.Admitted, command.Int("per-repository", 1), command.Int("seed", 1));

        foreach (var excluded in selection.Excluded)
        {
            output.WriteLine($"excluded       {excluded.Id} — {excluded.Reason}");
        }

        output.WriteLine($"drawn          {drawn.Count} of {selection.Admitted.Count} admitted candidate(s), {command.Int("per-repository", 1)} per repository, seed {command.Int("seed", 1)}");

        var trees = new ScoutTrees(settings, mirrors, NullLogger<ScoutTrees>.Instance);
        return await FreezeAsync(command, drawn, trees, history, output, error, cancellationToken);
    }

    private static async Task<int> FreezeAsync(
        CommandLine command, IReadOnlyList<ScoutCandidateFacts> drawn, ScoutTrees trees, ScoutHistory history, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var frozen = new List<ScoutFiles.FrozenTask>();
        var leaks = new List<string>();

        foreach (var facts in drawn)
        {
            var c = facts.Candidate;
            var tree = await trees.BuildAsync(new ScoutTreeSpec(c.Id, c.Repository, c.Parent, c.PlanFile.Length > 0 ? [c.PlanFile] : []), cancellationToken);
            var added = await history.AddedLinesAsync(c, cancellationToken);

            if ((tree, added) is not (Outcome<ScoutTree>.Ok { Value: var built }, Outcome<IReadOnlyList<string>>.Ok { Value: var lines }))
            {
                return GateRunCommand.Refuse(error, ExitCodes.Environment, tree is Outcome<ScoutTree>.Fail f ? f.Reason : ((Outcome<IReadOnlyList<string>>.Fail)added).Reason);
            }

            var identifiers = ScoutGroundTruth.AddedIdentifiers(lines, IdentifiersAt(built));

            switch (ScoutTask.Frozen(c.Id, c.Repository, c.Parent, c.Fix, c.Kind, c.Statement, facts.GroundTruth, identifiers, built.Removed, built.TreeHash))
            {
                case Outcome<ScoutTask>.Ok ok:
                    frozen.Add(new ScoutFiles.FrozenTask(ok.Value, identifiers));
                    output.WriteLine($"frozen         {c.Id} — {c.Repository} at {c.Parent.Short}, |G| = {facts.GroundTruth.Count}, tree {built.TreeHash[..12]}{(built.Removed.Count > 0 ? $", removed {string.Join(", ", built.Removed)}" : string.Empty)}");
                    break;
                case Outcome<ScoutTask>.Fail fail:
                    leaks.Add(fail.Reason);
                    break;
                default:
                    break;
            }
        }

        if (leaks.Count > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "nothing was written — rewrite these statements: " + string.Join(" · ", leaks));
        }

        if (frozen.Count == 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.NoReport, "no candidate was admitted — nothing was frozen or written");
        }

        await File.WriteAllTextAsync(command.Value("out"), ScoutFiles.WriteTasks(frozen, command.Int("seed", 1), Path.GetFileName(command.Value("cutoffs"))), cancellationToken);
        output.WriteLine($"written        {frozen.Count} frozen task(s) to {command.Value("out")}");
        return ExitCodes.Pass;
    }

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
        (File.Exists(command.Value("candidates")), File.Exists(command.Value("cutoffs")), command.Value("opus-id").Length > 0 && command.Value("sonnet-id").Length > 0, command.Value("out").Length > 0) switch
        {
            (false, _, _, _) => "scouts tasks needs --candidates <candidates.json> — the listed squash-merged changes to select from",
            (_, false, _, _) => "scouts tasks needs --cutoffs <cutoffs.json> — each resolved id's published cut-off, its source url and the date read, recorded BEFORE any candidate is listed",
            (_, _, false, _) => "scouts tasks needs --opus-id and --sonnet-id — the resolved ids whose cut-offs decide which changes are after them",
            (_, _, _, false) => "scouts tasks needs --out <tasks.json>",
            _ => string.Empty,
        };
}
