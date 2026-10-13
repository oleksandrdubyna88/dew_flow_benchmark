using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Scouts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>S7's parent check over REAL git histories (plan §3.1: "P is the PR's base, not S^1"): one repository whose feature branch lands
/// three ways — a merge commit, a squash, a rebase (the PR's commits replayed on main, so the fix's first parent is a commit INSIDE the PR)
/// — read through <see cref="ScoutHistory"/> off the bare mirror and judged by <see cref="ScoutCandidates.Admit"/>. A mismatch excludes the
/// candidate by id with a reason code.</summary>
public sealed class ScoutParentCheckTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private sealed record Landed(string P, string Merge, string Squash, string RebasedFirst, string RebasedFix);

    [Fact]
    public async Task Each_merge_style_is_verified_against_the_mirror_and_a_mismatch_excludes_by_id_with_a_code()
    {
        var h = await HistoryAsync();
        IReadOnlyList<ScoutCandidate> candidates =
        [
            Candidate("merge-ok", h.P, h.Merge, "merge-commit"),
            Candidate("merge-one-parent", h.P, h.Squash, "merge-commit"),
            Candidate("squash-ok", h.P, h.Squash, "pr-squash", number: 1),
            Candidate("local-ok", h.P, h.Squash, "local-squash"),
            Candidate("squash-not-first-parent", h.P, h.RebasedFix, "pr-squash", number: 2),
            Candidate("rebase-ok", h.P, h.RebasedFix, "pr-rebase", number: 3, baseRef: h.P),
            Candidate("rebase-p-is-fix-parent", h.RebasedFirst, h.RebasedFix, "pr-rebase", number: 4, baseRef: h.P),
            Candidate("rebase-base-not-ancestor", h.Merge, h.RebasedFix, "pr-rebase", number: 5, baseRef: h.Merge),
        ];
        var facts = new List<ScoutCandidateFacts>();

        foreach (var candidate in candidates)
        {
            facts.Add((await History().FactsAsync(candidate, Ct)).Ok());
        }

        var selection = ScoutCandidates.Admit(facts, new DateOnly(2026, 1, 1));

        selection.Admitted.Select(c => c.Candidate.Id.Value).Should().Equal("merge-ok", "squash-ok", "local-ok", "rebase-ok");
        selection.Excluded.Select(e => (e.Id.Value, e.Reason)).Should().Equal(
            ("merge-one-parent", ScoutParentCheck.MergeCommitNeedsTwoParents),
            ("squash-not-first-parent", ScoutParentCheck.NotFirstParent),
            ("rebase-p-is-fix-parent", ScoutParentCheck.NotBaseRef),
            ("rebase-base-not-ancestor", ScoutParentCheck.BaseRefNotAncestor));
        facts.Single(f => f.Candidate.Id.Value == "merge-ok").Parents.FixParents.Select(p => p.Value).Should().Equal(h.P, await _repo.GitAsync("rev-parse", "feature").ContinueWith(t => t.Result.Trim(), TaskScheduler.Default));
        facts.Single(f => f.Candidate.Id.Value == "rebase-ok").Parents.Should().Match<ScoutParentFacts>(p => p.FixParents.Single().Value == h.RebasedFirst && p.BaseRefIsAncestorOfFix,
            "the rebased fix's first parent is a commit inside the PR, and P is the recorded base");
        facts.Single(f => f.Candidate.Id.Value == "rebase-ok").GroundTruth.Should().Equal(["src/A.cs", "src/B.cs", "src/C.cs"], "G is read against P, the base, not against the fix's parent");
    }

    /// <summary>main: init → P (A, B, C). feature from P: f1 (A, B), f2 (C). Landed three ways: <c>merge --no-ff</c> on main; <c>merge --squash</c>
    /// on a branch at P; the two commits cherry-picked onto a branch at P (what a rebase merge leaves).</summary>
    private async Task<Landed> HistoryAsync()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        var p = await _repo.CommitManyAsync([("src/A.cs", "class A {}\n"), ("src/B.cs", "class B {}\n"), ("src/C.cs", "class C {}\n")], ("p", "2026-06-01T10:00:00+00:00"));
        await _repo.GitAsync("checkout", "-q", "-b", "trunk");
        await _repo.GitAsync("checkout", "-q", "-b", "feature");
        var f1 = await _repo.CommitManyAsync([("src/A.cs", "class A { int x; }\n"), ("src/B.cs", "class B { int x; }\n")], ("f1", "2026-06-02T10:00:00+00:00"));
        var f2 = await _repo.CommitAsync(("src/C.cs", "class C { int x; }\n"), ("f2", "2026-06-03T10:00:00+00:00"));

        await _repo.GitAsync("checkout", "-q", "trunk");
        await _repo.GitAsync("merge", "-q", "--no-ff", "-m", "merge feature", "feature");
        var merge = (await _repo.GitAsync("rev-parse", "HEAD")).Trim();

        await _repo.GitAsync("checkout", "-q", "-b", "squashed", p);
        await _repo.GitAsync("merge", "-q", "--squash", "feature");
        await _repo.GitAsync("commit", "-q", "-m", "squash feature");
        var squash = (await _repo.GitAsync("rev-parse", "HEAD")).Trim();

        await _repo.GitAsync("checkout", "-q", "-b", "rebased", p);
        await _repo.GitAsync("cherry-pick", f1, f2);
        var rebasedFix = (await _repo.GitAsync("rev-parse", "HEAD")).Trim();
        var rebasedFirst = (await _repo.GitAsync("rev-parse", "HEAD~1")).Trim();

        return new Landed(p, merge, squash, rebasedFirst, rebasedFix);
    }

    private ScoutHistory History() => new(
        new GitCheckoutProvider(CheckoutCacheOptions.Under(_root.Sibling("mirrors")), NullLogger<GitCheckoutProvider>.Instance),
        new ScoutTreeSettings(_root.Sibling("trees"), ScoutTreeSettings.GitHub, TimeSpan.FromMinutes(2)));

    private ScoutCandidate Candidate(string id, string parent, string fix, string style, int number = 0, string baseRef = "") =>
        new(ScoutTaskId.Parse(id).Ok(), _repo.Url, CommitSha.Parse(parent).Ok(), CommitSha.Parse(fix).Ok(), ScoutQuestionKind.HowEndToEnd, "How does it work end to end?", string.Empty)
        {
            MergeStyle = ScoutMergeStyles.Parse(style).Ok(),
            Number = number,
            BaseRef = baseRef.Length > 0 ? new ScoutBaseRef(baseRef, "the test") : ScoutBaseRef.None,
        };

    public void Dispose()
    {
        _repo.Dispose();
        _root.Dispose();
    }
}
