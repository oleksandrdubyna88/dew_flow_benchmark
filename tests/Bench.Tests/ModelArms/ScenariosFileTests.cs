using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scenarios file is the list of objective checks the shell kept in <c>scenarios.json</c>: an id, a kind (cs or node) and a name
/// each, strict, every problem at once.
/// <para>
/// What this does NOT prove: that a scenario exists in the target or that its name matches a test (the objective check owns that), or that
/// the file on disk is the one read (the console owns the file).
/// </para></summary>
public sealed class ScenariosFileTests
{
    private static IReadOnlyList<string> Lines(string reason) => reason.Split('\n');

    [Fact]
    public void The_shells_scenarios_shape_reads_with_its_kinds()
    {
        var scenarios = ScenariosFile.Read("""[{"id":"S1","kind":"cs","name":"A"},{"id":"S30","kind":"node","name":"B"}]""").Ok();

        scenarios.Should().Equal(new Scenario("S1", "cs", "A"), new Scenario("S30", "node", "B"));
    }

    [Fact]
    public void An_empty_list_is_refused_naming_the_file()
    {
        ScenariosFile.Read("[]").Reason().Should().Be("scenarios.json holds no scenario");
    }

    [Fact]
    public void A_duplicate_id_is_refused_naming_it()
    {
        var reason = ScenariosFile.Read("""[{"id":"S1","kind":"cs","name":"A"},{"id":"S1","kind":"node","name":"B"}]""").Reason();

        reason.Should().Contain("'S1'").And.Contain("2 times");
    }

    [Fact]
    public void An_unknown_kind_is_refused_naming_the_legal_ones()
    {
        var reason = ScenariosFile.Read("""[{"id":"S1","kind":"py","name":"A"}]""").Reason();

        reason.Should().Contain("scenarios[0].kind").And.Contain("'py'").And.Contain("cs, node");
    }

    [Fact]
    public void An_unknown_or_missing_member_is_refused_by_name_and_every_problem_comes_back_at_once()
    {
        var reason = ScenariosFile.Read("""[{"id":"S1","kind":"cs","colour":"red"},{"id":"S2","kind":"cs","name":"B"},{"id":"S2","kind":"zz","name":"C"}]""").Reason();

        var lines = Lines(reason);
        lines.Should().Contain(l => l.Contains("scenarios[0].colour", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("scenarios[0].name is missing", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("scenarios[2].kind", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("'S2'", StringComparison.Ordinal) && l.Contains("2 times", StringComparison.Ordinal));
    }

    [Fact]
    public void Text_that_is_not_a_json_array_of_objects_is_refused()
    {
        ScenariosFile.Read("{\"id\":\"S1\"}").Reason().Should().Contain("must be a JSON array");
        ScenariosFile.Read("[1]").Reason().Should().Contain("scenarios[0] must be a JSON object");
        ScenariosFile.Read("[{").Reason().Should().Contain("scenarios.json is not JSON");
    }

    [Fact]
    public void An_empty_id_or_name_is_refused_and_a_member_written_twice_is_not_ignored()
    {
        ScenariosFile.Read("""[{"id":"","kind":"cs","name":"A"}]""").Reason().Should().Contain("scenarios[0].id is empty");
        ScenariosFile.Read("""[{"id":"S1","kind":"cs","name":"A","name":"B"}]""").Reason().Should().Contain("scenarios.json is not JSON");
    }
}
