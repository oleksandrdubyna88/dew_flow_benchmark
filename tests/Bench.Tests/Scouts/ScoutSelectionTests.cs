using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.1's selection, pure: the cut-offs are evidence (a missing, unpublished or ambiguous id STOPS it), only a P after the LATER
/// cut-off is a candidate, G is filtered and sized, P is verified per merge style (S7), and the D10 draw reproduces a KNOWN order from the
/// campaign string — the golden list below was computed independently (node's SHA-256) over this synthetic screened list.</summary>
public sealed class ScoutSelectionTests
{
    private const string Opus = "claude-opus-5-5";
    private const string Sonnet = "claude-sonnet-5-5";

    /// <summary>The campaign string of the 2026-10-12 screen — THE seed of the draw.</summary>
    private const string Seed = "scouts-preliminary-2026-10-12";

    private const string Lima = "file:////wsl.localhost/Ubuntu/home/someone/git/lima";

    private static readonly DateTimeOffset AfterCutoff = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static string Cutoffs(string opusCutoff = "2026-03-31", string sonnetCutoff = "2026-01-31", string extra = "") => $$"""
        { "models": [
          { "modelId": "{{Opus}}", "cutoff": "{{opusCutoff}}", "source": "https://example.invalid/models", "readOn": "2026-10-12" },
          { "modelId": "{{Sonnet}}", "cutoff": "{{sonnetCutoff}}", "source": "https://example.invalid/models", "readOn": "2026-10-12" }{{extra}}
        ] }
        """;

    [Fact]
    public void The_later_of_the_two_recorded_cut_offs_is_the_bar()
    {
        ScoutCutoffs.Parse(Cutoffs()).Ok().LaterOf(Opus, Sonnet).Ok().Should().Be(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void An_id_with_no_recorded_cut_off_stops_the_selection_naming_it()
    {
        ScoutCutoffs.Parse(Cutoffs()).Ok().LaterOf("claude-opus-6-0", Sonnet).Reason().Should().Contain("claude-opus-6-0").And.Contain("stops");
    }

    [Fact]
    public void An_unpublished_or_ambiguous_cut_off_stops_the_selection_rather_than_being_guessed()
    {
        var unpublished = ScoutCutoffs.Parse(Cutoffs(sonnetCutoff: "")).Ok();
        var twice = ScoutCutoffs.Parse(Cutoffs(extra: $$""", { "modelId": "{{Opus}}", "cutoff": "2026-05-01", "source": "https://x.invalid", "readOn": "2026-10-12" }""")).Ok();

        unpublished.LaterOf(Opus, Sonnet).Reason().Should().Contain("no published cut-off").And.Contain(Sonnet);
        twice.LaterOf(Opus, Sonnet).Reason().Should().Contain("listed 2 times").And.Contain("ambiguous");
    }

    [Theory]
    [InlineData("""{ "models": [ { "modelId": "m", "cutoff": "2026-03-31", "source": "http://insecure", "readOn": "2026-10-12" } ] }""", "https")]
    [InlineData("""{ "models": [ { "modelId": "m", "cutoff": "March 2026", "source": "https://x", "readOn": "2026-10-12" } ] }""", "not a date")]
    [InlineData("""{ "models": [ { "modelId": "m", "cutoff": "2026-03-31", "source": "https://x" } ] }""", "readOn")]
    [InlineData("""{ "nothing": [] }""", "models")]
    public void A_cut_offs_file_missing_its_evidence_is_refused_by_name(string json, string named)
    {
        ScoutCutoffs.Parse(json).Reason().Should().Contain(named);
    }

    [Fact]
    public void A_candidate_is_admitted_only_after_the_later_cut_off()
    {
        var later = new DateOnly(2026, 3, 31);
        IReadOnlyList<ScoutCandidateFacts> candidates =
        [
            Facts("after", new DateTimeOffset(2026, 4, 1, 0, 0, 1, TimeSpan.Zero)),
            Facts("on-the-day", new DateTimeOffset(2026, 3, 31, 23, 59, 0, TimeSpan.Zero)),
            Facts("before", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero)),
        ];

        var selection = ScoutCandidates.Admit(candidates, later);

        selection.Admitted.Select(c => c.Candidate.Id.Value).Should().Equal("after");
        selection.Excluded.Should().Contain(e => e.Id.Value == "on-the-day" && e.Reason.Contains("not after the later cut-off 2026-03-31"));
        selection.Excluded.Should().Contain(e => e.Id.Value == "before");
    }

    [Fact]
    public void G_out_of_range_or_docs_only_is_excluded_with_its_reason()
    {
        var later = new DateOnly(2026, 1, 1);

        var selection = ScoutCandidates.Admit(
            [Facts("two", AfterCutoff, ["a.cs", "b.cs"]), Facts("docs", AfterCutoff, ["a.md", "b.md", "c.md"]), Facts("many", AfterCutoff, [.. Enumerable.Range(1, 16).Select(i => $"f{i}.cs")])],
            later);

        selection.Admitted.Should().BeEmpty();
        selection.Excluded.Select(e => e.Reason).Should().Contain(r => r.Contains("2 file(s)")).And.Contain(r => r.Contains("docs-only")).And.Contain(r => r.Contains("16 file(s)"));
    }

    // ── S7: P verified per merge style ───────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly string P = new('a', 40);
    private static readonly string Inside = new('e', 40);
    private static readonly string Other = new('f', 40);

    public static TheoryData<string, string, string, string[], bool, string> ParentCases => new()
    {
        // style, parent, baseRefOid, fix's parents, base is ancestor, expected code (empty = admitted)
        { "merge-commit", P, "", [P, Inside], false, "" },
        { "merge-commit", P, "", [P], false, ScoutParentCheck.MergeCommitNeedsTwoParents },
        { "merge-commit", Inside, "", [P, Inside], false, ScoutParentCheck.NotFirstParent },
        { "pr-squash", P, "", [P], false, "" },
        { "pr-squash", P, "", [P, Inside], false, ScoutParentCheck.SquashNeedsOneParent },
        { "pr-squash", P, "", [Inside], false, ScoutParentCheck.NotFirstParent },
        { "local-squash", P, "", [P], false, "" },
        { "local-squash", P, "", [Inside], false, ScoutParentCheck.NotFirstParent },
        { "pr-rebase", P, P, [Inside], true, "" },
        { "pr-rebase", Inside, P, [Inside], true, ScoutParentCheck.NotBaseRef },
        { "pr-rebase", Other, Other, [Inside], false, ScoutParentCheck.BaseRefNotAncestor },
        { "pr-rebase", P, "", [Inside], true, ScoutParentCheck.RebaseNeedsBaseRef },
    };

    [Theory]
    [MemberData(nameof(ParentCases))]
    public void P_is_verified_per_merge_style_and_a_mismatch_is_a_reason_code(string style, string parent, string baseRef, string[] fixParents, bool ancestor, string code)
    {
        var candidate = Candidate("c", parent, style: style, baseRef: baseRef, number: style.StartsWith("pr-", StringComparison.Ordinal) ? 7 : 0);
        var facts = new ScoutParentFacts([.. fixParents.Select(p => CommitSha.Parse(p).Ok())], ancestor);

        ScoutParentCheck.Refusal(candidate, facts).Should().Be(code);
    }

    [Fact]
    public void A_parent_mismatch_excludes_the_candidate_by_id_with_the_code_alone_never_a_value()
    {
        var mismatch = new ScoutCandidateFacts(Candidate("p04", Inside, style: "pr-rebase", baseRef: P, number: 9, privacy: ScoutTaskPrivacy.Private), AfterCutoff, ["a.cs", "b.cs", "c.cs"], new ScoutParentFacts([CommitSha.Parse(Inside).Ok()], true));
        var unread = new ScoutCandidateFacts(Candidate("c1", P), AfterCutoff, ["a.cs", "b.cs", "c.cs"], ScoutParentFacts.Unread);

        var selection = ScoutCandidates.Admit([mismatch, unread], new DateOnly(2026, 1, 1));

        selection.Admitted.Should().BeEmpty();
        selection.Excluded.Should().Contain(e => e.Id.Value == "p04" && e.Reason == ScoutParentCheck.NotBaseRef);
        selection.Excluded.Should().Contain(e => e.Id.Value == "c1" && e.Reason == ScoutParentCheck.SquashNeedsOneParent, "lineage nobody read never admits");
        selection.Excluded.Should().OnlyContain(e => !e.Reason.Contains(P) && !e.Reason.Contains(Inside), "a reason code carries no sha");
    }

    [Fact]
    public void The_fixs_parents_are_read_off_rev_list_and_a_malformed_sha_refuses()
    {
        var facts = ScoutParentFacts.Parse($"{Inside} {P} {Other}\n", baseRefIsAncestorOfFix: false).Ok();

        facts.FixParents.Select(p => p.Value).Should().Equal(P, Other);
        ScoutParentFacts.Parse($"{Inside} nope", false).Reason().Should().Contain("parents");
        ScoutParentFacts.Parse(Inside, false).Ok().FixParents.Should().BeEmpty("a root commit has no parent");
    }

    // ── S7: the D10 draw, keyed and seeded by the campaign string ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("example-org/example-repo", "example-repo")]
    [InlineData("https://github.com/example-org/example-repo.git", "example-repo")]
    [InlineData("git@github.com:example-org/example-repo.git", "example-repo")]
    [InlineData(Lima, "lima")]
    [InlineData(Lima + "/", "lima")]
    [InlineData("file:///C:/clones/lima.GIT", "lima")]
    public void The_short_repository_name_is_the_slugs_name_or_a_urls_last_path_segment(string repository, string shortName)
    {
        ScoutRepository.ShortName(repository).Should().Be(shortName);
    }

    [Fact]
    public void A_candidates_key_is_repo_hash_number_for_a_pull_request_and_repo_at_fixsha_for_a_local_commit()
    {
        Candidate("c", P, repository: "example-org/example-repo", style: "pr-squash", number: 42).Key.Should().Be("example-repo#42");
        Candidate("c", P, repository: "example-org/example-repo", style: "pr-rebase", number: 7, baseRef: P).Key.Should().Be("example-repo#7");
        Candidate("c", P, repository: "example-org/example-repo", style: "merge-commit", number: 3).Key.Should().Be("example-repo#3");
        Candidate("p01", P, repository: Lima, style: "local-squash", fix: Inside).Key.Should().Be($"lima@{Inside}");
        Candidate("p02", P, repository: Lima, style: "merge-commit", fix: Inside).Key.Should().Be($"lima@{Inside}", "a local merge commit has no number");
    }

    /// <summary>The golden list: computed OUTSIDE this code (node's crypto, SHA-256 over UTF-8 <c>seed|short-name</c> and <c>seed|key</c>, hex
    /// ascending) over ten public repositories with 5 PRs each (<c>alpha … juliet</c>, keys <c>name#1..5</c>), <c>kilo</c> with 4, and the private
    /// <c>lima</c> with 5 local commits (keys <c>lima@111…1 … lima@555…5</c>).</summary>
    [Fact]
    public void The_draw_reproduces_the_known_order_from_the_seed_eight_main_with_reserves_then_two_pilot()
    {
        var draw = ScoutSelectionDraw.Of(Screened(), Seed);

        draw.Repositories.Where(r => r.Eligible).Select(r => r.ShortName).Should().Equal("india", "juliet", "alpha", "echo", "charlie", "foxtrot", "delta", "lima", "golf", "hotel", "bravo");
        draw.Main.Select(t => t.Facts.Candidate.Id.Value).Should().Equal("india-4", "juliet-4", "alpha-1", "echo-1", "charlie-1", "foxtrot-1", "delta-4", "p03");
        draw.Reserves.Select(t => t.Facts.Candidate.Id.Value).Should().Equal("india-1", "juliet-2", "alpha-4", "echo-2", "charlie-3", "foxtrot-4", "delta-1", "p01");
        draw.Pilot.Select(t => t.Facts.Candidate.Id.Value).Should().Equal("golf-1", "hotel-4");
        draw.Tasks.Should().HaveCount(18, "bravo is eligible, eleventh, and unused");
        draw.Tasks.Select(t => t.Role).Should().Equal([.. Enumerable.Repeat(ScoutTaskRole.Main, 8), .. Enumerable.Repeat(ScoutTaskRole.Reserve, 8), .. Enumerable.Repeat(ScoutTaskRole.Pilot, 2)]);
    }

    [Fact]
    public void A_repository_with_four_admissible_candidates_is_left_out_and_said_so()
    {
        var draw = ScoutSelectionDraw.Of(Screened(), Seed);

        var kilo = draw.Repositories.Single(r => r.ShortName == "kilo");
        (kilo.Admissible, kilo.Eligible, kilo.Shown).Should().Be((4, false, "kilo"));
        draw.Tasks.Should().NotContain(t => t.Facts.Candidate.Repository == "org/kilo");
        draw.Repositories.Single(r => r.ShortName == "lima").Shown.Should().Be("p03 (private)", "a private repository is shown by its first candidate's label");
    }

    [Fact]
    public void Another_seed_draws_another_order_and_the_same_seed_the_same_whatever_the_input_order()
    {
        var screened = Screened();
        var shuffled = screened.Reverse().ToList();

        ScoutSelectionDraw.Of(shuffled, Seed).Main.Select(t => t.Facts.Candidate.Id.Value).Should().Equal(ScoutSelectionDraw.Of(screened, Seed).Main.Select(t => t.Facts.Candidate.Id.Value));
        ScoutSelectionDraw.Of(screened, "scouts-decision-2027-01-01").Main.Select(t => t.Facts.Candidate.Id.Value).Should().NotEqual(ScoutSelectionDraw.Of(screened, Seed).Main.Select(t => t.Facts.Candidate.Id.Value));
    }

    [Fact]
    public void Fewer_eligible_repositories_give_fewer_tasks_and_no_pilot_rather_than_a_refusal()
    {
        var draw = ScoutSelectionDraw.Of([.. Screened().Where(c => c.Candidate.Repository == "org/alpha")], Seed);

        draw.Main.Select(t => t.Facts.Candidate.Id.Value).Should().Equal("alpha-1");
        draw.Reserves.Select(t => t.Facts.Candidate.Id.Value).Should().Equal("alpha-4");
        draw.Pilot.Should().BeEmpty();
    }

    // ── the candidates file ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_candidate_carries_its_merge_style_number_and_base_ref_and_label_may_stand_in_for_a_private_id()
    {
        var json = $$"""
            { "candidates": [
              { "id": "c1", "repository": "org/r", "parent": "{{P}}", "fix": "{{Inside}}", "kind": "how-end-to-end", "statement": "How?", "planFile": "", "mergeStyle": "pr-rebase", "number": 12, "baseRefOid": "{{P}}", "baseRefSource": "gh pr view 12 --json baseRefOid" },
              { "label": "p03", "private": true, "language": "PHP", "repository": "{{Lima}}", "parent": "{{P}}", "fix": "{{Inside}}", "kind": "where-decided", "statement": "Where?", "planFile": "", "mergeStyle": "local-squash" }
            ] }
            """;

        var candidates = ScoutFiles.Candidates(json).Ok();

        candidates[0].Should().Match<ScoutCandidate>(c => c.MergeStyle == ScoutMergeStyle.PrRebase && c.Number == 12 && c.BaseRef.Oid == P && c.BaseRef.Source == "gh pr view 12 --json baseRefOid" && c.Key == "r#12");
        candidates[1].Should().Match<ScoutCandidate>(c => c.Id.Value == "p03" && c.Label == "p03" && c.IsPrivate && c.MergeStyle == ScoutMergeStyle.LocalSquash && c.Number == 0 && !c.BaseRef.IsRecorded);
    }

    [Theory]
    [InlineData("""{ "id": "c1", "mergeStyle": "squash", "number": 1 }""", "not a merge style")]
    [InlineData("""{ "id": "c1", "mergeStyle": "pr-squash" }""", "names its \"number\"")]
    [InlineData("""{ "id": "c1", "mergeStyle": "local-squash", "number": 4 }""", "has no PR \"number\"")]
    [InlineData("""{ "id": "c1", "mergeStyle": "pr-rebase", "number": 4 }""", "records \"baseRefOid\"")]
    [InlineData("""{ "id": "c1", "mergeStyle": "pr-rebase", "number": 4, "baseRefOid": "nope" }""", "baseRefOid")]
    [InlineData("""{ "id": "c1", "label": "c2", "mergeStyle": "pr-squash", "number": 4 }""", "is not its id")]
    [InlineData("""{ "id": "p03", "private": true, "language": "PHP", "mergeStyle": "pr-rebase", "number": 4, "baseRefOid": "nope" }""", "its baseRefOid is not a commit sha")]
    public void A_candidate_whose_shape_does_not_match_its_merge_style_is_refused_by_name(string fields, string named)
    {
        var json = $$"""{ "candidates": [ { "repository": "org/r", "parent": "{{P}}", "fix": "{{Inside}}", "kind": "how-end-to-end", "statement": "How?", "planFile": "", {{fields[1..^1]}} } ] }""";

        var refusal = ScoutFiles.Candidates(json).Reason();

        refusal.Should().Contain(named);

        if (fields.Contains("private", StringComparison.Ordinal))
        {
            refusal.Should().NotContain("nope", "a private candidate's malformed base ref is named by field only");
        }
    }

    [Fact]
    public void Tasks_json_carries_the_campaign_and_each_tasks_role_and_a_reserve_never_runs()
    {
        var main = new ScoutFiles.FrozenTask(ScoutStoreFixtures.FrozenTask("t01"), []);
        var reserve = new ScoutFiles.FrozenTask(ScoutStoreFixtures.FrozenTask("t02"), []) { Role = ScoutTaskRole.Reserve };

        var json = ScoutFiles.WriteTasks([main, reserve], Seed, "cutoffs.json");

        json.Should().Contain($"\"campaign\": \"{Seed}\"").And.Contain("\"role\": \"main\"").And.Contain("\"role\": \"reserve\"").And.NotContain("\"seed\"");
        ScoutFiles.Campaign(json).Should().Be(Seed);
        ScoutFiles.Entries(json).Ok().Select(e => (e.Id, e.Role)).Should().Equal(("t01", ScoutTaskRole.Main), ("t02", ScoutTaskRole.Reserve));
        ScoutFiles.Tasks(json).Ok().Select(t => t.Id.Value).Should().Equal(["t01"], "the reserve waits for the operator; run never plans it");
        ScoutFiles.Tasks(json.Replace("\"role\": \"reserve\"", "\"role\": \"spare\"", StringComparison.Ordinal)).Reason().Should().Contain("not a task role");
    }

    [Fact]
    public void G_keeps_the_files_that_existed_at_P_and_drops_lockfiles_generated_files_changelogs_manifests_and_the_plan()
    {
        var atParent = new HashSet<string>(["src/A.cs", "src/B.cs", "package-lock.json", "CHANGELOG.md", "src/X.Designer.cs", "version.json", "todo/PLAN_x.md", "docs/Guide.md"], StringComparer.Ordinal);

        ScoutGroundTruth.Of(["src/A.cs", "src/B.cs", "src/New.cs", "package-lock.json", "CHANGELOG.md", "src/X.Designer.cs", "version.json", "todo/PLAN_x.md", "docs/Guide.md"], atParent, "todo/PLAN_x.md")
            .Should().Equal("docs/Guide.md", "src/A.cs", "src/B.cs");
    }

    [Fact]
    public void An_added_identifier_is_one_that_occurs_nowhere_in_the_tree_at_P()
    {
        var atParent = new HashSet<string>(ScoutGroundTruth.Identifiers("public sealed class ScoutStore { void Settle() {} } the and for"), StringComparer.Ordinal);

        ScoutGroundTruth.AddedIdentifiers(["    public void Recover(ScoutStore store) => Settle();", "// the ledger"], atParent)
            .Should().Equal("Recover", "ledger", "store");
    }

    [Fact]
    public void A_drawn_statement_naming_an_added_identifier_is_refused_when_frozen()
    {
        var frozen = ScoutTask.Frozen(
            ScoutTaskId.Parse("t1").Ok(), "org/repo", CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, "Where does Recover decide the charge?", ["src/A.cs", "src/B.cs", "src/C.cs"], ["Recover"], [], new string('c', 64));

        frozen.Reason().Should().Contain("AddedIdentifier 'Recover'");
    }

    /// <summary>The synthetic screened list of the golden test: every candidate admissible (after the cut-off, |G| = 3, lineage consistent).</summary>
    private static IReadOnlyList<ScoutCandidateFacts> Screened()
    {
        var list = new List<ScoutCandidateFacts>();

        foreach (var name in new[] { "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india", "juliet" })
        {
            list.AddRange(Enumerable.Range(1, 5).Select(n => Facts($"{name}-{n}", AfterCutoff, repository: $"org/{name}", style: "pr-squash", number: n)));
        }

        list.AddRange(Enumerable.Range(1, 4).Select(n => Facts($"kilo-{n}", AfterCutoff, repository: "org/kilo", style: "pr-squash", number: n)));
        list.AddRange(Enumerable.Range(1, 5).Select(n => Facts($"p0{n}", AfterCutoff, repository: Lima, style: "local-squash", fix: new string((char)('0' + n), 40), privacy: ScoutTaskPrivacy.Private)));
        return list;
    }

    private static ScoutCandidateFacts Facts(
        string id, DateTimeOffset parentDate, IReadOnlyList<string>? groundTruth = null, string repository = "org/repo", string style = "local-squash", int number = 0, string? fix = null,
        ScoutTaskPrivacy privacy = ScoutTaskPrivacy.Public) =>
        new(Candidate(id, P, repository, style, number, fix: fix, privacy: privacy), parentDate, groundTruth ?? ["src/A.cs", "src/B.cs", "src/C.cs"], new ScoutParentFacts([CommitSha.Parse(P).Ok()], false));

    private static ScoutCandidate Candidate(
        string id, string parent, string repository = "org/repo", string style = "local-squash", int number = 0, string baseRef = "", string? fix = null, ScoutTaskPrivacy privacy = ScoutTaskPrivacy.Public) =>
        new(ScoutTaskId.Parse(id).Ok(), repository, CommitSha.Parse(parent).Ok(), CommitSha.Parse(fix ?? new string('b', 40)).Ok(), ScoutQuestionKind.HowEndToEnd, "How does it work?", string.Empty)
        {
            MergeStyle = ScoutMergeStyles.Parse(style).Ok(),
            Number = number,
            BaseRef = baseRef.Length > 0 ? new ScoutBaseRef(baseRef, "test") : ScoutBaseRef.None,
            Privacy = privacy,
            Language = privacy == ScoutTaskPrivacy.Private ? "PHP" : string.Empty,
        };
}
