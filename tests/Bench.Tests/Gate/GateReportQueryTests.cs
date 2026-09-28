using Bench.Application.Gate;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Gate;

/// <summary>The gate report's use cases (E6) over a scripted read port: which scope and rubric were asked for, and every
/// refusal in words — a malformed request, a scope or rubric this database does not hold, and a scope whose suite's tasks
/// were never recorded. What each figure is over is <see cref="GateReport"/>'s and is tested there.</summary>
public sealed class GateReportQueryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task The_scope_list_names_each_partition_its_sources_the_rubrics_it_carries_and_whether_its_tasks_are_recorded()
    {
        var reads = Reads(out var a, out _);

        var scopes = Answer(await GateReportQuery.ScopesAsync(reads, string.Empty, Ct));

        scopes.Should().HaveCount(2, "two product pins in one campaign are two partitions, never one");
        var first = scopes.Single(s => s.ProductVersion == PinA.VersionText);
        first.Id.Should().Be(a.Scope.Id);
        first.Gate.Should().Be("feature");
        first.Runs.Should().Be(2);
        first.Sources.Should().Equal("native");
        first.TasksRecorded.Should().BeTrue();
        first.Rubrics.Should().ContainSingle().Which.Should().Be(new GateRubricDto("strict-v1", "Strict", StrictHash, StrictRubric.Stamp, 1));
        scopes.Single(s => s.ProductVersion == PinB.VersionText).Rubrics.Should().BeEmpty("nobody read that partition's findings");
    }

    [Fact]
    public async Task The_scope_list_of_one_gate_holds_only_that_gate_and_a_word_that_is_no_gate_is_a_bad_request()
    {
        var reads = Reads(out _, out _);

        Answer(await GateReportQuery.ScopesAsync(reads, "plan", Ct)).Should().BeEmpty();
        Refusal(await GateReportQuery.ScopesAsync(reads, "7", Ct)).Should().Be((GateRefusalKind.BadRequest, "'7' is not a gate — plan, code or feature"));
    }

    [Fact]
    public async Task A_table_asked_without_a_scope_or_without_a_rubric_is_a_bad_request_that_says_which()
    {
        var reads = Reads(out var a, out _);

        Refusal(await GateReportQuery.ModelsAsync(reads, "feature", string.Empty, "strict-v1", Ct))
            .Should().Be((GateRefusalKind.BadRequest, GateReportQuery.NoScopeNamed));
        Refusal(await GateReportQuery.ModelsAsync(reads, "feature", a.Scope.Id, string.Empty, Ct))
            .Should().Be((GateRefusalKind.BadRequest, GateReportQuery.NoRubricNamed));
        Refusal(await GateReportQuery.RunsAsync(reads, "feature", string.Empty, Ct))
            .Should().Be((GateRefusalKind.BadRequest, GateReportQuery.NoScopeNamed));
    }

    [Fact]
    public async Task A_scope_this_gate_does_not_hold_is_not_found_and_the_refusal_names_the_scopes_it_does_hold()
    {
        var reads = Reads(out var a, out _);

        var refused = Refusal(await GateReportQuery.ModelsAsync(reads, "feature", "000000000000", "strict-v1", Ct));

        refused.Kind.Should().Be(GateRefusalKind.NotFound);
        refused.Reason.Should().Contain(a.Scope.Id);
        Refusal(await GateReportQuery.ModelsAsync(reads, "code", a.Scope.Id, "strict-v1", Ct)).Reason
            .Should().Contain("the gate has no runs yet", "a feature scope is not a code scope, whatever its id");
    }

    [Fact]
    public async Task A_scope_whose_suite_tasks_were_never_recorded_refuses_its_table_as_a_conflict_and_still_lists_its_runs()
    {
        var reads = Reads(out var a, out _) with { Tasks = new Dictionary<string, IReadOnlyList<TaskSummary>>() };

        var refused = Refusal(await GateReportQuery.ModelsAsync(reads, "feature", a.Scope.Id, "strict-v1", Ct));
        var runs = Answer(await GateReportQuery.RunsAsync(reads, "feature", a.Scope.Id, Ct));

        refused.Kind.Should().Be(GateRefusalKind.Conflict, "the scope exists; a prerequisite is missing");
        refused.Reason.Should().Contain("bench gate suite record");
        runs.Should().NotBeEmpty().And.OnlyContain(r => !r.TaskRecorded && r.Language.Length == 0,
            "a task the database does not know reads NOT RECORDED, never a measured task with no language");
        Answer(await GateReportQuery.ScopesAsync(reads, "feature", Ct)).Should().OnlyContain(s => !s.TasksRecorded);
    }

    [Fact]
    public async Task Only_a_rubric_the_scope_s_verdicts_carry_can_be_chosen()
    {
        var reads = Reads(out var a, out _);

        var refused = Refusal(await GateReportQuery.ModelsAsync(reads, "feature", a.Scope.Id, "lenient-worth-v1", Ct));

        refused.Kind.Should().Be(GateRefusalKind.NotFound);
        refused.Reason.Should().Contain(StrictRubric.Stamp, "the refusal names what the scope's verdicts DO carry");
        Answer(await GateReportQuery.ModelsAsync(reads, "feature", a.Scope.Id, StrictRubric.Stamp, Ct)).RubricId.Should().Be("strict-v1");
    }

    [Fact]
    public async Task A_scope_with_no_verdicts_at_all_refuses_its_table_saying_nothing_was_assessed()
    {
        var reads = Reads(out _, out var b) with { Rubrics = [], Verdicts = [] };

        var refused = Refusal(await GateReportQuery.ModelsAsync(reads, "feature", b.Scope.Id, "strict-v1", Ct));

        refused.Kind.Should().Be(GateRefusalKind.NotFound);
        refused.Reason.Should().Contain("nothing in this scope was assessed");
    }

    [Fact]
    public async Task The_table_puts_calibration_tasks_apart_and_carries_every_task_beside_them()
    {
        var reads = Reads(out var a, out _);

        var table = Answer(await GateReportQuery.ModelsAsync(reads, "feature", a.Scope.Id, "strict-v1", Ct));

        table.Rows.Should().ContainSingle().Which.Runs.Should().Be(1);
        table.CalibrationRows.Should().ContainSingle().Which.Runs.Should().Be(1);
        table.AllTaskRows.Should().ContainSingle().Which.Runs.Should().Be(2);
        table.PerTask.Should().Contain(t => t.TaskId == "calib1" && t.Calibration);
        table.Scope.Id.Should().Be(a.Scope.Id);
    }

    [Fact]
    public async Task A_strict_percentage_travels_as_not_hand_checked_until_a_hand_check_covers_it_and_its_counts_stay()
    {
        var reads = Reads(out var a, out _);

        var unchecked_ = Answer(await GateReportQuery.ModelsAsync(reads, "feature", a.Scope.Id, "strict-v1", Ct)).Rows[0];
        var check = HandCheck.Of([a.CampaignId], StrictRubric, GateReviewerId.Parse("codex-astra").Ok(), 20, 20, new string('a', 64), DateTimeOffset.UnixEpoch).Ok();
        var checked_ = Answer(await GateReportQuery.ModelsAsync(reads with { HandChecks = [check] }, "feature", a.Scope.Id, "strict-v1", Ct)).Rows[0];

        unchecked_.SupportedPct.Should().Be(GateFigureDto.NotHandChecked);
        unchecked_.Supported.Should().Be(1);
        checked_.SupportedPct.Should().Be(GateFigureDto.Of(100));
    }

    [Fact]
    public async Task The_run_list_marks_an_earlier_attempt_superseded_and_an_unrecorded_turn_count_unknown()
    {
        var campaign = Guid.CreateVersion7();
        var first = Run("cs2", "grok", 1, valid: false, campaign: campaign, attempt: 1, failure: FailureKind.HttpError);
        var second = Run("cs2", "grok", 1, campaign: campaign, attempt: 2);
        var noLedger = Run("rs3", "grok", 1, campaign: campaign) is var r ? r with { Facts = r.Facts with { TurnFactsCaptured = false } } : r;
        var reads = new ScriptedGateReads([first, second, noLedger], TasksOf(first.Scope.SuiteStamp), [], [], []);

        var runs = Answer(await GateReportQuery.RunsAsync(reads, "feature", first.Scope.Id, Ct));

        runs.Single(x => x.RunId == first.RunId).Superseded.Should().BeTrue();
        runs.Single(x => x.RunId == first.RunId).FailureKind.Should().Be("HttpError");
        runs.Single(x => x.RunId == second.RunId).Superseded.Should().BeFalse();
        runs.Single(x => x.RunId == noLedger.RunId).Turns.Should().Be(GateFigureDto.Unknown, "a harness that kept no ledger recorded no turns — not zero turns");
        runs.Single(x => x.RunId == second.RunId).Turns.Should().Be(GateFigureDto.Of(2));
    }

    [Fact]
    public async Task One_run_is_answered_with_its_findings_and_verdicts_and_an_unknown_run_is_not_found()
    {
        var reads = Reads(out var a, out _);

        var detail = Answer(await GateReportQuery.RunAsync(reads, a.RunId, Ct));

        detail.Summary.RunId.Should().Be(a.RunId);
        detail.PromptHash.Should().Be("p-" + a.RunId.ToString("N")[..8]);
        detail.Verdicts.Should().ContainSingle().Which.Reading.Should().Be("Supported");
        detail.Scope.Id.Should().Be(a.Scope.Id);
        Refusal(await GateReportQuery.RunAsync(reads, Guid.CreateVersion7(), Ct)).Kind.Should().Be(GateRefusalKind.NotFound);
    }

    [Fact]
    public async Task A_scope_id_resolves_and_a_suite_stamp_spanning_two_scopes_is_refused_listing_both()
    {
        var scopes = Answer(await GateReportQuery.ScopesAsync(Reads(out var a, out var b), "feature", Ct));

        Answer(GateReportQuery.ResolveScope(scopes, a.Scope.Id)).Id.Should().Be(a.Scope.Id);
        var spanning = Refusal(GateReportQuery.ResolveScope(scopes, a.Scope.SuiteStamp));
        spanning.Kind.Should().Be(GateRefusalKind.BadRequest);
        spanning.Reason.Should().Contain("spans 2 scopes").And.Contain(a.Scope.Id).And.Contain(b.Scope.Id,
            "picking one of two products for the reader would report on a population nobody chose");
        Refusal(GateReportQuery.ResolveScope(scopes, string.Empty)).Reason.Should().StartWith(GateReportQuery.NoScopeNamed).And.Contain(a.Scope.Id);
        Refusal(GateReportQuery.ResolveScope(scopes, "nope")).Kind.Should().Be(GateRefusalKind.NotFound);
    }

    [Fact]
    public void Every_figure_state_has_a_word_on_the_wire_and_none_of_them_is_a_known_zero()
    {
        foreach (var state in Enum.GetValues<FigureState>().Where(s => s != FigureState.Known))
        {
            var dto = GateReportContract.Figure(new Figure(false, 0, state));

            dto.Known.Should().BeFalse(state.ToString());
            dto.State.Should().NotBe(GateFigureDto.KnownState, state.ToString());
        }

        GateReportContract.Figure(Figure.NotHandChecked).State.Should().Be("not-hand-checked");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    /// <summary>Two partitions of one feature campaign: pin A holds a measured run (one strict verdict) and a calibration
    /// run; pin B holds one unassessed run.</summary>
    private static ScriptedGateReads Reads(out GateRunRecord a, out GateRunRecord b)
    {
        var campaign = Guid.CreateVersion7();
        a = Run("cs2", "grok", 1, campaign: campaign) with { Findings = [Finding(1)] };
        var calibration = Run("calib1", "grok", 1, campaign: campaign);
        b = Run("rs3", "grok", 1, campaign: campaign, pin: PinB);
        var verdict = Verdict(a, 1, Strict(StrictReading.Supported, "cs2-S1"), StrictHash);

        return new ScriptedGateReads([a, calibration, b], TasksOf(a.Scope.SuiteStamp), [StrictRubric, LenientRubric], [verdict], []);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TaskSummary>> TasksOf(string stamp) =>
        new Dictionary<string, IReadOnlyList<TaskSummary>> { [stamp] = Input([]).Tasks };

    private static GateFinding Finding(int ordinal) =>
        GateFinding.Of(ordinal, FindingSeverity.Major, FindingCategory.Reliability, true, 10, "a finding", "src/a.cs", FileHashKey.Of(new byte[32]).Ok()).Ok();

    private static T Answer<T>(GateAnswer<T> answer) =>
        answer.Should().BeOfType<GateAnswer<T>.Answered>().Which.Value;

    private static (GateRefusalKind Kind, string Reason) Refusal<T>(GateAnswer<T> answer)
    {
        var refused = answer.Should().BeOfType<GateAnswer<T>.Refused>().Which;
        return (refused.Kind, refused.Reason);
    }
}
