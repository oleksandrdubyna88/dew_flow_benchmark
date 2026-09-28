using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Registry;
using Bench.Infrastructure.Gate;
using Bench.Tests.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.Assessment.AssessmentFixtures;

namespace Bench.Tests.Gate.Assessment;

/// <summary>S4.3 — one batch through <see cref="FindingAssessor"/>: the claude CLI has no output schema, so its answer is
/// taken out of whatever prose surrounds it; and a claude run that stopped at its turn ceiling is named as that — no
/// answer — rather than as an answer that did not parse.</summary>
public sealed class FindingAssessorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_claude_answer_wrapped_in_prose_and_a_fence_is_read_as_its_json()
    {
        var reading = await AskAsync("Here is my assessment:\n```json\n" + Answer(AnswerRow("0000000a")) + "\n```\nDone.");

        reading.Should().BeOfType<BatchReading.Answered>().Which.Rows.Should().ContainSingle();
    }

    /// <summary>Measured 2026-09-28 against Claude Code 2.1.258: with <c>--max-turns 1</c> an assessor that reaches for a
    /// read tool prints <c>Error: Reached max turns (1)</c> and exits 0; with three turns the same prompt read the file and
    /// answered. Read as JSON, that line is "unparseable" — a claim about the answer when there was none.</summary>
    [Fact]
    public async Task A_claude_run_stopped_at_its_turn_ceiling_is_no_answer_named_as_such()
    {
        var reading = await AskAsync("Error: Reached max turns (1)");

        var failed = reading.Should().BeOfType<BatchReading.Failed>().Subject;
        failed.Cause.Should().Be(AssessmentFailureCause.NoAnswer);
        failed.Reason.Should().Contain("turn ceiling");
    }

    private static async Task<BatchReading> AskAsync(string answer)
    {
        using var root = GateStoreFixtures.NewRoot();
        var files = new FileSystemGateAssessmentFiles(root.Path);
        var rubric = GateRubrics.Load(Path.Combine(Repository.Root, "prompts")).Ok()[0];
        var launch = new AssessorLaunch(AssessRig.Assessor(), ModelRuntimeKind.CliClaude, "claude", TimeSpan.FromMinutes(1), rubric);
        var assessor = new FindingAssessor(new ScriptedAssessor((_, _) => answer), files);

        return (await assessor.AskAsync(launch, new BatchAsk($"cs2-{Guid.NewGuid():N}"[..12], Cs2, [Row("0000000a")], [Seed("cs2-S1")], []), Ct)).Reading;
    }
}
