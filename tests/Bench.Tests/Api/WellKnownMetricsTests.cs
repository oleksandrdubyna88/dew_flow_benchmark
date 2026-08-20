using Bench.Contracts;
using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Api;

/// <summary>The console's metric list against the domain's own constants.
/// <para>
/// <see cref="WellKnownMetrics"/> exists because <c>Bench.Contracts</c> may not reference the domain, so the
/// strings are copied. A copy nobody checks is a copy that drifts — and the drift is invisible in exactly the
/// wrong way: the page keeps offering the old name, the report comes back with no legs, and it reads as a run
/// that measured nothing rather than as a metric that no longer exists.
/// </para></summary>
public sealed class WellKnownMetricsTests
{
    [Fact]
    public void Every_offered_name_is_the_domain_constant_it_copies()
    {
        WellKnownMetrics.AnchorRecall.Should().Be(AnswerScoring.AnchorRecall);
        WellKnownMetrics.ToolUse.Should().Be(AnswerScoring.ToolUse);
        WellKnownMetrics.RetrievalMrr.Should().Be(RetrievalScoring.Mrr);
        WellKnownMetrics.RetrievalFirstHitRank.Should().Be(RetrievalScoring.FirstHitRank);
        WellKnownMetrics.DiagnosisParses.Should().Be(DiagnosisScoring.Parses);
        WellKnownMetrics.DiagnosisAnchorRecall.Should().Be(DiagnosisScoring.AnchorRecall);
        WellKnownMetrics.DiagnosisAnchorPrecision.Should().Be(DiagnosisScoring.Precision);
        WellKnownMetrics.DiagnosisSymptomOnly.Should().Be(DiagnosisScoring.SymptomOnly);
    }

    [Fact]
    public void The_offered_list_holds_every_name_declared_beside_it()
    {
        // Guards the other direction: a constant added above and forgotten in All is a metric the console
        // never offers, which is the same invisible outcome as a stale copy.
        WellKnownMetrics.All.Should().HaveCount(8).And.OnlyHaveUniqueItems();
        WellKnownMetrics.All.Should().Contain(WellKnownMetrics.DiagnosisSymptomOnly);
    }
}
