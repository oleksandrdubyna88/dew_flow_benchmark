using System.Text.Json.Nodes;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>Which product replies say the reviewer's ACCOUNT is out — over the three lines the stored S7.3 cells carry,
/// one per account that ran dry on 2026-09-29, and the lines that must not be mistaken for one.</summary>
public sealed class ReviewerAccountOutTests
{
    [Theory]
    [InlineData("rate limited (after 1 attempt): {\"type\":\"error\",\"message\":\"You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase more credits or try again at Oct 3rd, 2026 7:…")]
    [InlineData("exit 77: [coai-mcp] the API refused the key for vendor 'grok-4-7-think' (HTTP 403) - check the vault entry under 'grok-4-7-think'; the response body is not shown")]
    [InlineData("exit 1: You've hit your monthly spend limit. Switch to another model to continue. (HTTP 429)")]
    [InlineData("exit 1: Your credit balance is too low to access the Anthropic API. (HTTP 400)")]
    [InlineData("HTTP 429: insufficient_quota")]
    public void A_round_nobody_answered_because_the_account_is_out_names_the_failure(string failure)
    {
        var reason = ReviewerAccountOut.Reason(Reply($"0 of 1 reviewers answered; failed: rev-a/PlanCritique: {failure}"));

        reason.Should().Be($"rev-a/PlanCritique: {failure}");
    }

    [Theory]
    [InlineData("1 of 1 reviewers answered")]
    [InlineData("0 of 1 reviewers answered; failed: rev-a/PlanCritique: rate limited (after 3 attempts): Too many requests")]
    [InlineData("0 of 1 reviewers answered; failed: rev-a/PlanCritique: exit 1 (the CLI said nothing on stderr)")]
    [InlineData("0 of 1 reviewers answered; failed: rev-a/PlanCritique: exit 1: connection refused")]
    [InlineData("0 of 1 reviewers answered; failed: rev-a/PlanCritique: exit 1: see the billing FAQ for model names")]
    [InlineData("1 of 2 reviewers answered; failed: rev-b/PlanCritique: exit 1: You've hit your monthly spend limit. (HTTP 429)")]
    public void Anything_else_is_not_an_empty_account(string reviewers) =>
        ReviewerAccountOut.Reason(Reply(reviewers)).Should().BeEmpty();

    [Theory]
    [InlineData("")]
    [InlineData("this is not json at all")]
    [InlineData("{\"error\":\"no plan round has reached 'proceed'\"}")]
    [InlineData("{\"verdict\":\"proceed\",\"findings\":[]}")]
    public void A_reply_without_a_reviewers_line_is_not_one_either(string reply) =>
        ReviewerAccountOut.Reason(reply).Should().BeEmpty();

    [Fact]
    public void The_refusal_it_becomes_carries_the_marker_and_is_read_back_whole()
    {
        var refusal = ReviewerAccountOut.Refusal("rev-a/PlanCritique: exit 1: You've hit your monthly spend limit.");

        ReviewerAccountOut.IsRefusal(refusal).Should().BeTrue();
        ReviewerAccountOut.IsRefusal("the cell was not measured and was handed back — refused before launch: no key").Should().BeFalse();
        refusal.Should().Contain("monthly spend limit");
    }

    private static string Reply(string reviewers) =>
        new JsonObject { ["verdict"] = "call_human", ["reviewers"] = reviewers, ["findings"] = new JsonArray() }.ToJsonString();
}
