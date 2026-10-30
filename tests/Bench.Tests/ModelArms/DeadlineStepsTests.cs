using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The steps whose <c>.done</c> says they ended on their deadline (E3.S5, D8, C17): the facts <c>objective.json</c> carries as <c>stepsOnDeadline</c>. A function over a scheme and a way to read each marker — nothing here opens a file.
/// <para>
/// What this does NOT prove: that the verb reads the markers from the right files, through the bounded road (<c>ChecksVerbTests.Steps_that_ended_on_their_deadline_reach_the_report</c> does).
/// </para></summary>
public sealed class DeadlineStepsTests
{
    private static Read<DoneMarker> Done(string state) => new Read<DoneMarker>.Present(new DoneMarker(1, CapturedCount.Number(0), "x", state, false));

    private static readonly Scheme Scheme = new(
        "SRS",
        [
            new SchemeEntry.Step("1-code", new StepId("code")),
            new SchemeEntry.Tasks(),
            new SchemeEntry.Step("3-review", new StepId("review")),
            new SchemeEntry.Step("4-fix", new StepId("fix")),
            new SchemeEntry.Step("5-gate", new StepId("gate")),
        ]);

    [Fact]
    public void Only_hard_deadline_markers_are_listed_in_scheme_order()
    {
        var markers = new Dictionary<string, Read<DoneMarker>>
        {
            ["4-fix"] = Done("hard-deadline"),
            ["1-code"] = Done("hard-deadline"),
            ["3-review"] = Done("finished"),
            ["5-gate"] = new Read<DoneMarker>.Refused("5-gate.done is a link"),
        };
        var asked = new List<string>();

        var listed = DeadlineSteps.Of(Scheme, stem =>
        {
            asked.Add(stem);
            return markers.GetValueOrDefault(stem) ?? new Read<DoneMarker>.Absent();
        });

        listed.Should().Equal(["1-code", "4-fix"], "in the scheme's order, not the order the markers were made; a finished, a refused and an absent marker are not on their deadline");
        asked.Should().Equal(["1-code", "3-review", "4-fix", "5-gate"], "each STEP of the scheme is asked once, in order — the tasks block is not a step with a marker of its own here");
    }

    [Fact]
    public void A_shells_marker_with_no_end_word_is_not_on_its_deadline()
    {
        DeadlineSteps.Of(Scheme, _ => Done(string.Empty)).Should().BeEmpty("the previous generation's marker carries no state word, and a missing word is not a deadline");
    }
}
