using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Each verb needs exactly the files its stage needs, and a root missing one is refused naming the first missing file and the verb
/// that writes it. The table below is the parent plan's D18 requirement table, typed once.
/// <para>
/// What this does NOT prove: that a verb calls <c>FirstUnmet</c> before it starts (the console's gate owns that, E1.S9), or that the
/// answer to "does it exist" is true (the caller passes the predicate; here it is a set of strings).
/// </para></summary>
public sealed class RootRequirementsTests
{
    private const string Root = "/r";
    private static readonly (string Path, bool MustExist)[] Known = [("", true), ("experiment.json", true), ("scenarios.json", true)];

    private static readonly Dictionary<string, (string Path, bool MustExist)[]> Rows = new()
    {
        ["init"] = [("", false)],
        ["version"] = [],
        ["help"] = [],
        ["validate"] = Known,
        ["prep-base"] = [.. Known, ("baseline.json", false)],
        ["make"] = [.. Known, ("baseline.json", true), ("base", true)],
        ["chain"] = [.. Known, ("baseline.json", true), ("base", true)],
        ["run-arm"] = [.. Known, ("baseline.json", true), ("arms.map.json", true), ("chain/made-C1", true)],
        ["run-all"] = [.. Known, ("baseline.json", true), ("arms.map.json", true), ("chain/made-C1", true)],
        ["checks"] = [.. Known, ("baseline.json", true), ("arms.map.json", true), ("chain/made-C1", true)],
        ["status"] = [("", true), ("experiment.json", true)],
        ["lock"] = [("", true), ("experiment.json", true)],
        ["unlock"] = [("", true), ("experiment.json", true)],
        ["gate-lock"] = [("", true), ("experiment.json", true)],
    };

    public static TheoryData<string> Verbs() => new(Rows.Keys);

    private static IReadOnlyList<RootRequirement> For(string verb, params string[] arms) =>
        RootRequirements.For(verb, arms, "scenarios.json", "baseline.json");

    private static Func<string, bool> Existing(params string[] relative) =>
        path => relative.Select(r => r.Length == 0 ? Root : System.IO.Path.Combine(Root, r)).Contains(path);

    [Theory]
    [MemberData(nameof(Verbs))]
    public void Each_verb_requires_exactly_the_files_its_stage_needs(string verb)
    {
        var actual = For(verb, "C1").Select(r => (r.RelativePath, r.MustExist)).ToList();

        actual.Should().Equal(Rows[verb]);
    }

    [Fact]
    public void Every_arm_given_to_a_run_verb_must_have_been_made()
    {
        For("run-arm", "C1", "CR2").Select(r => r.RelativePath).Should().Contain(["chain/made-C1", "chain/made-CR2"]);
        For("run-all").Select(r => r.RelativePath).Should().NotContain(p => p.StartsWith("chain/made-", StringComparison.Ordinal), "no arm was given");
    }

    [Fact]
    public void Chain_requires_what_make_requires_because_its_first_action_is_making_every_unmade_arm()
    {
        For("chain", "C1").Should().Equal(For("make", "C1"));
    }

    [Fact]
    public void The_scenarios_and_baseline_names_are_the_configured_ones()
    {
        var requirements = RootRequirements.For("make", [], "cases.json", "measured.json");

        requirements.Select(r => r.RelativePath).Should().Contain(["cases.json", "measured.json"]).And.NotContain(["scenarios.json", "baseline.json"]);
    }

    [Fact]
    public void Each_requirement_names_the_verb_that_writes_it()
    {
        var producers = For("run-arm", "C1").ToDictionary(r => r.RelativePath, r => r.Producer);

        producers.Should().Contain(new Dictionary<string, string>
        {
            [""] = "init",
            ["experiment.json"] = "init",
            ["scenarios.json"] = "init",
            ["baseline.json"] = "prep-base",
            ["arms.map.json"] = "make",
            ["chain/made-C1"] = "make",
        });
    }

    [Fact]
    public void An_unknown_verb_requires_nothing_because_the_command_line_refuses_it_first()
    {
        For("frobnicate").Should().BeEmpty();
    }

    [Fact]
    public void The_files_every_verb_reads_are_the_root_and_its_experiment_file()
    {
        RootRequirements.ToRead.Select(r => (r.RelativePath, r.MustExist, r.Producer)).Should().Equal(("", true, "init"), ("experiment.json", true, "init"));
    }

    [Fact]
    public void The_first_missing_file_is_named_with_the_verb_that_writes_it()
    {
        var reason = RootRequirements.FirstUnmet(For("validate"), Root, Existing("", "experiment.json"));

        reason.Should().Contain("scenarios.json").And.Contain("is missing").And.Contain("`model-arms init`");
        reason.Should().NotContain("experiment.json", "the first missing file is the only one named");
    }

    [Fact]
    public void A_missing_root_is_named_alone_with_the_verb_that_creates_it()
    {
        var reason = RootRequirements.FirstUnmet(For("make"), Root, Existing());

        reason.Should().Be("/r does not exist — `model-arms init /r` creates it");
    }

    [Fact]
    public void A_missing_baseline_names_prep_base_as_its_producer()
    {
        var reason = RootRequirements.FirstUnmet(For("make"), Root, Existing("", "experiment.json", "scenarios.json"));

        reason.Should().Contain("baseline.json").And.Contain("`model-arms prep-base`");
    }

    [Fact]
    public void A_baseline_that_already_exists_refuses_prep_base_telling_the_operator_to_move_it_away()
    {
        var reason = RootRequirements.FirstUnmet(For("prep-base"), Root, Existing("", "experiment.json", "scenarios.json", "baseline.json"));

        reason.Should().Contain("baseline.json").And.Contain("already exists").And.Contain("move it away to measure again");
    }

    [Fact]
    public void An_existing_root_refuses_init_which_never_writes_into_one()
    {
        var reason = RootRequirements.FirstUnmet(For("init"), Root, Existing(""));

        reason.Should().Be("/r already exists — init creates a new root and never writes into an existing one");
    }

    [Fact]
    public void A_root_with_every_file_its_verb_needs_is_met()
    {
        var all = Existing("", "experiment.json", "scenarios.json", "baseline.json", "arms.map.json", "chain/made-C1");

        RootRequirements.FirstUnmet(For("run-arm", "C1"), Root, all).Should().BeEmpty();
        RootRequirements.FirstUnmet(For("init"), Root, Existing()).Should().BeEmpty("a root that does not exist is what init needs");
        RootRequirements.FirstUnmet(For("version"), Root, Existing()).Should().BeEmpty("no requirements, nothing unmet");
    }

    [Fact]
    public async Task A_root_missing_a_required_file_is_refused_naming_the_first_missing_one_with_exit_4()
    {
        using var root = new ExperimentRoot();
        File.Delete(System.IO.Path.Combine(root.Root, "scenarios.json"));

        var passed = await root.PassAsync("validate");

        passed.ExitCode.Should().Be(4, passed.Error);
        passed.Error.Should().Contain("scenarios.json is missing").And.Contain("init");
        passed.Calls.Should().BeEmpty("a verb is never started on a half-made root");
    }
}
