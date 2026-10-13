using System.Globalization;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;

namespace Bench.Infrastructure.Scouts;

/// <summary>What <c>bench scouts tasks</c> reads out of a candidate's bare mirror (§3.1) — every read is <c>git</c> over the mirror
/// <see cref="GitCheckoutProvider"/> keeps, nothing checked out: P's commit date (the cut-off rule), the files the change touched and the
/// files that existed at P (G), the fix's lineage for the parent check (S7: its parents, and for a rebase-merged PR whether the recorded base
/// is an ancestor of the fix), and the diff's ADDED lines (the leak check's added identifiers).</summary>
public sealed class ScoutHistory(GitCheckoutProvider mirrors, ScoutTreeSettings settings)
{
    public async Task<Outcome<ScoutCandidateFacts>> FactsAsync(ScoutCandidate candidate, CancellationToken cancellationToken)
    {
        var bare = await MirrorAsync(candidate, cancellationToken);

        if (bare is Outcome<string>.Fail fail)
        {
            return Outcome<ScoutCandidateFacts>.Failure(Withheld(candidate, "the mirror could not be read", $"candidate {candidate.Id}: {fail.Reason}"));
        }

        var mirror = ((Outcome<string>.Ok)bare).Value;
        var date = await GitAsync(mirror, cancellationToken, "show", "-s", "--format=%cI", candidate.Parent.Value);
        var changed = await GitAsync(mirror, cancellationToken, "diff", "--name-only", "--no-renames", candidate.Parent.Value, candidate.Fix.Value);
        var atParent = await GitAsync(mirror, cancellationToken, "ls-tree", "-r", "--full-tree", candidate.Parent.Value);
        var lineage = await LineageAsync(mirror, candidate, cancellationToken);

        return (date, changed, atParent, lineage) switch
        {
            (Outcome<string>.Ok d, Outcome<string>.Ok c, Outcome<string>.Ok p, Outcome<ScoutParentFacts>.Ok l) when DateTimeOffset.TryParse(d.Value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parentDate) =>
                Outcome<ScoutCandidateFacts>.Success(new ScoutCandidateFacts(candidate, parentDate, ScoutGroundTruth.Of(Lines(c.Value), ScoutGroundTruth.TreeEntries(p.Value), candidate.PlanFile), l.Value)),
            _ => Outcome<ScoutCandidateFacts>.Failure(Withheld(candidate, "the mirror could not say when P was committed, what changed, what existed at P or how the fix landed",
                $"candidate {candidate.Id}: the mirror could not say when P was committed, what changed, what existed at P or how the fix landed — {Said(date)}{Said(changed)}{Said(atParent)}{Said(lineage)}")),
        };
    }

    /// <summary>The fix's parents (<c>rev-list --parents -n 1 fix</c> → <c>fix p1 [p2]</c>) and, for a rebase-merged PR with a recorded base,
    /// whether that base is an ancestor of the fix — asked of git, never derived from <c>fix^1</c>, which for a rebase merge is a commit inside
    /// the PR (§3.1).</summary>
    private async Task<Outcome<ScoutParentFacts>> LineageAsync(string mirror, ScoutCandidate candidate, CancellationToken cancellationToken)
    {
        var parents = await GitAsync(mirror, cancellationToken, "rev-list", "--parents", "-n", "1", candidate.Fix.Value);
        var ancestor = candidate.MergeStyle == ScoutMergeStyle.PrRebase && candidate.BaseRef.IsRecorded
            ? await GitCommand.AskAsync(mirror, settings.GitTimeout, cancellationToken, "merge-base", "--is-ancestor", candidate.BaseRef.Oid, candidate.Fix.Value)
            : Outcome<bool>.Success(false);

        return (parents, ancestor) switch
        {
            (Outcome<string>.Ok p, Outcome<bool>.Ok a) => ScoutParentFacts.Parse(p.Value, a.Value),
            (Outcome<string>.Fail f, _) => Outcome<ScoutParentFacts>.Failure(f.Reason),
            (_, Outcome<bool>.Fail f) => Outcome<ScoutParentFacts>.Failure(f.Reason),
            _ => Outcome<ScoutParentFacts>.Failure("unreachable"),
        };
    }

    /// <summary>The lines the change ADDED (<c>+</c> lines of the unified diff, headers excluded).</summary>
    public async Task<Outcome<IReadOnlyList<string>>> AddedLinesAsync(ScoutCandidate candidate, CancellationToken cancellationToken)
    {
        var bare = await MirrorAsync(candidate, cancellationToken);

        if (bare is Outcome<string>.Fail fail)
        {
            return Outcome<IReadOnlyList<string>>.Failure(Withheld(candidate, "the mirror could not be read", $"candidate {candidate.Id}: {fail.Reason}"));
        }

        var diff = await GitAsync(((Outcome<string>.Ok)bare).Value, cancellationToken, "diff", "--no-color", "--unified=0", candidate.Parent.Value, candidate.Fix.Value);

        return diff.Match(
            text => Outcome<IReadOnlyList<string>>.Success([.. text.Split('\n').Where(l => l.StartsWith('+') && !l.StartsWith("+++", StringComparison.Ordinal)).Select(l => l[1..])]),
            Outcome<IReadOnlyList<string>>.Failure);
    }

    private Task<Outcome<string>> MirrorAsync(ScoutCandidate candidate, CancellationToken cancellationToken) =>
        settings.UrlOf(candidate.Repository).Match(
            url => mirrors.EnsureMirrorAsync(MeasurementTarget.At(url, candidate.Fix), cancellationToken),
            reason => Task.FromResult(Outcome<string>.Failure(reason)));

    private Task<Outcome<string>> GitAsync(string bare, CancellationToken cancellationToken, params string[] arguments) =>
        GitCommand.ReadAsync(bare, settings.GitTimeout, cancellationToken, arguments);

    /// <summary>A refusal about the candidate as a public writer may print it (<see cref="ScoutWithheld"/>): git's words, which quote the url
    /// and paths, stay under the trees root for a private candidate.</summary>
    private string Withheld(ScoutCandidate candidate, string summary, string detail) =>
        ScoutWithheld.Refusal(settings.Root, candidate.Privacy, candidate.Id.Value, summary, detail);

    private static string Said<T>(Outcome<T> read) => read is Outcome<T>.Fail fail ? $" [{fail.Reason}]" : string.Empty;

    private static IReadOnlyList<string> Lines(string text) =>
        [.. text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)];
}
