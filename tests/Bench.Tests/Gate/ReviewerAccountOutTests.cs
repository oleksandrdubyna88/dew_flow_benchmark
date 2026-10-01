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

    /// <summary>S2 (D8) — the same question asked of a CLI's own stdout/stderr, one marker per CLI as each prints it when its
    /// account is out (claude, codex, agy, measured 2026-10-01). The matched LINE is the reason, so a quota stop benches the
    /// subject with the CLI's own words and never a banner.</summary>
    [Theory]
    [InlineData("Claude AI usage limit reached|1759340000", "usage limit")]
    [InlineData("You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase more credits or try again at Oct 6th, 2026.", "usage limit")]
    [InlineData("{\"type\":\"error\",\"error\":{\"code\":\"usage_limit_reached\",\"message\":\"Usage limit reached\"}}", "usage_limit_reached")]
    [InlineData("Individual quota reached for model gemini-3.1-pro. Try again later.", "quota reached")]
    [InlineData("Error: 429 RESOURCE_EXHAUSTED: Quota exceeded for quota metric 'Generate Content API requests per day'", "RESOURCE_EXHAUSTED")]
    public void A_cli_that_printed_its_quota_marker_names_the_line_that_carries_it(string line, string marker)
    {
        var reason = ReviewerAccountOut.CliReason($"True color (24-bit) support not detected\n{line}\n");

        reason.Should().Contain(marker).And.NotContain("True color", "the reason is the line that says it, not the banner above it");
    }

    [Theory]
    [InlineData("HTTP 429 Too Many Requests — rate limited, retrying in 2s")]
    [InlineData("Error: 429 RESOURCE_EXHAUSTED")]
    [InlineData("rate limited (after 3 attempts): Too many requests")]
    [InlineData("The latest version of @openai/codex is 0.52.0, read from https://www.npmjs.com/package/@openai/codex.")]
    [InlineData("")]
    public void A_plain_rate_limit_or_an_ordinary_answer_does_not_bench_a_cli_subject(string text) =>
        ReviewerAccountOut.CliReason(text).Should().BeEmpty("a transient 429 is retried by the next attempt; only a named quota or account marker benches");

    private static string Reply(string reviewers) =>
        new JsonObject { ["verdict"] = "call_human", ["reviewers"] = reviewers, ["findings"] = new JsonArray() }.ToJsonString();
}
