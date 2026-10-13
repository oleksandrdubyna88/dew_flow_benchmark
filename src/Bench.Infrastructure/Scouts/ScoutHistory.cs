using System.Globalization;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;

namespace Bench.Infrastructure.Scouts;

/// <summary>What <c>bench scouts tasks</c> reads out of a candidate's bare mirror (§3.1) — every read is <c>git</c> over the mirror
/// <see cref="GitCheckoutProvider"/> keeps, nothing checked out: P's commit date (the cut-off rule), the files the change touched and the
/// files that existed at P (G), and the diff's ADDED lines (the leak check's added identifiers).</summary>
public sealed class ScoutHistory(GitCheckoutProvider mirrors, ScoutTreeSettings settings)
{
    public async Task<Outcome<ScoutCandidateFacts>> FactsAsync(ScoutCandidate candidate, CancellationToken cancellationToken)
    {
        var bare = await MirrorAsync(candidate, cancellationToken);

        if (bare is Outcome<string>.Fail fail)
        {
            return Outcome<ScoutCandidateFacts>.Failure($"candidate {candidate.Id}: {fail.Reason}");
        }

        var mirror = ((Outcome<string>.Ok)bare).Value;
        var date = await GitAsync(mirror, cancellationToken, "show", "-s", "--format=%cI", candidate.Parent.Value);
        var changed = await GitAsync(mirror, cancellationToken, "diff", "--name-only", "--no-renames", candidate.Parent.Value, candidate.Fix.Value);
        var atParent = await GitAsync(mirror, cancellationToken, "ls-tree", "-r", "--full-tree", candidate.Parent.Value);

        return (date, changed, atParent) switch
        {
            (Outcome<string>.Ok d, Outcome<string>.Ok c, Outcome<string>.Ok p) when DateTimeOffset.TryParse(d.Value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parentDate) =>
                Outcome<ScoutCandidateFacts>.Success(new ScoutCandidateFacts(candidate, parentDate, ScoutGroundTruth.Of(Lines(c.Value), ScoutGroundTruth.TreeEntries(p.Value), candidate.PlanFile))),
            _ => Outcome<ScoutCandidateFacts>.Failure($"candidate {candidate.Id}: the mirror could not say when P was committed, what changed or what existed at P"),
        };
    }

    /// <summary>The lines the change ADDED (<c>+</c> lines of the unified diff, headers excluded).</summary>
    public async Task<Outcome<IReadOnlyList<string>>> AddedLinesAsync(ScoutCandidate candidate, CancellationToken cancellationToken)
    {
        var bare = await MirrorAsync(candidate, cancellationToken);

        if (bare is Outcome<string>.Fail fail)
        {
            return Outcome<IReadOnlyList<string>>.Failure(fail.Reason);
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

    private static IReadOnlyList<string> Lines(string text) =>
        [.. text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)];
}
