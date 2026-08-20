using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The judge's framing (todo/PLAN_investigate_vs_implement.md step 7): the port still asks one
/// binary over (question, answer, reference) — the framing decides what those three ARE. A fix run's
/// reference is the reference FIX, and the verdict is about the CAUSE, not about restating prose.</summary>
public sealed class ModelJudgeFramingTests
{
    [Fact]
    public async Task The_diagnosis_framing_judges_the_cause_against_the_reference_fix()
    {
        var runtime = new CapturingRuntime("YES\nthe diagnosis names the recomputed delay the fix carries forward");
        var judge = new ModelJudge(runtime, Endpoint(), seed: 7, JudgeFraming.Diagnosis);

        var verdict = (await judge.JudgeAsync(
            "Retries stop honouring the cap.", "the delay is recomputed", "diff --git a/x b/x", CancellationToken.None)).Ok();

        verdict.Passed.Should().BeTrue();
        runtime.Seen.SystemPrompt.Should().Contain("DIAGNOSIS").And.Contain("unified diff")
            .And.Contain("symptom", "patching a symptom is the failure the verdict exists to catch");
        runtime.Seen.UserPrompt.Should().Contain("THE REFERENCE FIX (unified diff)")
            .And.Contain("THE CANDIDATE'S DIAGNOSIS");
    }

    [Fact]
    public async Task The_default_framing_is_byte_for_byte_the_reading_judge()
    {
        var runtime = new CapturingRuntime("NO\nabout something else");
        var judge = new ModelJudge(runtime, Endpoint(), seed: 7);

        await judge.JudgeAsync("q", "a", "r", CancellationToken.None);

        runtime.Seen.SystemPrompt.Should().Contain("reference answer").And.NotContain("DIAGNOSIS",
            "the reading judge must stay exactly what every earlier verdict was issued under");
        runtime.Seen.UserPrompt.Should().Contain("REFERENCE ANSWER").And.Contain("CANDIDATE ANSWER");
    }

    private static ModelEndpoint Endpoint() => ModelEndpoint.Parse(
        ModelRef.Parse("gemma-judge", ModelHosting.Local).Ok(), "http://127.0.0.1:11434/v1").Ok();

    private sealed class CapturingRuntime(string verdict) : IModelRuntime
    {
        public ModelRequest Seen { get; private set; } = null!;

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("fake"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Seen = request;

            return Task.FromResult(Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text(verdict),
                CapturedCount.Number(10),
                CapturedCount.Number(5),
                TimeSpan.FromMilliseconds(50),
                SamplingAsSent.From(request.Sampling, "request-body"),
                StopReason.Completed,
                "stop")));
        }
    }
}
