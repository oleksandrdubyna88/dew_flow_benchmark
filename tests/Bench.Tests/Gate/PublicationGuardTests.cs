using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The string guard's rules, one at a time, and the redaction the one free-text column passes first.</summary>
public sealed class PublicationGuardTests
{
    private static readonly PrivateNames Names = PrivateNames.Of(["contoso-orders", "Fabrikam"]);

    private static readonly IReadOnlySet<string> UrlColumns = new HashSet<string>(StringComparer.Ordinal) { "gate_reviewers.EndpointUrl" };

    [Theory]
    [InlineData("https://gw.example/v1", PublicationGuard.UrlRule)]
    [InlineData("see file://server/share", PublicationGuard.UrlRule)]
    [InlineData("C:\\work\\x", PublicationGuard.DriveRule)]
    [InlineData("d:/work/x", PublicationGuard.DriveRule)]
    [InlineData("/home/someone/x", PublicationGuard.HomeRule)]
    [InlineData("/Users/someone/x", PublicationGuard.UsersRule)]
    [InlineData("/users/someone/x", PublicationGuard.UsersRule)]
    [InlineData("contoso-ORDERS", "carries private name #1 of the suite")]
    [InlineData("the fabrikam key", "carries private name #2 of the suite")]
    public void Each_rule_refuses_its_shape(string text, string rule)
    {
        Check("gate_cells", "FailureText", text).Select(v => v.Rule).Should().Contain(rule);
    }

    [Theory]
    [InlineData("0.0.0+abc1234")]
    [InlineData("sample#0123456789ab")]
    [InlineData("call 2: HTTP 502 | verdict CallHuman")]
    [InlineData("runs/0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/cells/x/attempt-1/reply.json")]
    [InlineData("a:b ratio 3:4")]
    public void Ids_hashes_relative_paths_and_ordinary_sentences_pass(string text)
    {
        Check("gate_cells", "FailureText", text).Should().BeEmpty();
    }

    [Fact]
    public void A_public_vendor_url_passes_only_in_the_endpoint_column_and_a_loopback_one_never()
    {
        Check("gate_reviewers", "EndpointUrl", "https://api.x.ai/v1").Should().BeEmpty("a vendor's public url is a VALUE there by design");
        Check("gate_reviewers", "RemoteVendor", "https://api.x.ai/v1").Select(v => v.Rule).Should().Equal(PublicationGuard.UrlRule);
        Check("gate_reviewers", "EndpointUrl", "http://127.0.0.1:11434/v1").Select(v => v.Rule).Should().Equal(PublicationGuard.EndpointRule);
        Check("gate_reviewers", "EndpointUrl", "https://contoso-orders.example/v1").Select(v => v.Rule)
            .Should().Equal("carries private name #1 of the suite");
    }

    [Theory]
    [InlineData("llm.corp.internal:8000")]
    [InlineData("localhost:11434")]
    [InlineData("10.0.0.1:8000")]
    public void A_schemeless_machine_address_in_the_endpoint_column_is_refused(string endpoint)
    {
        Check("gate_reviewers", "EndpointUrl", endpoint).Select(v => v.Rule).Should().Equal([PublicationGuard.EndpointRule],
            "the endpoint column holds a public vendor url or nothing — a bare host is not exempt just because it has no '://'");
        Check("gate_reviewers", "EndpointUrl", string.Empty).Should().BeEmpty("an endpoint held as a reference leaves the url column empty");
    }

    [Fact]
    public void A_refusal_never_repeats_the_private_text()
    {
        var violation = PublicationGuard.Check([new PublishedText("gate_reviewers", "Id", "contoso-orders-reviewer", "contoso-orders-reviewer")], Names, UrlColumns)
            .Should().ContainSingle().Subject;

        violation.Describe.Should().Be("gate_reviewers.Id row <private>-reviewer: carries private name #1 of the suite");
    }

    [Fact]
    public void Redaction_removes_urls_paths_and_private_names_and_keeps_the_rest()
    {
        var redacted = FailureRedaction.Redact(
            "call 1: HTTP 401 from https://gw.contoso-orders.example/v1 reading C:\\Users\\x\\Fabrikam\\plan.md and /home/y/z", Names);

        redacted.Should().Be("call 1: HTTP 401 from <url> reading <path> and <path>");
        Check("gate_cells", "FailureText", redacted).Should().BeEmpty("whatever the redaction produces, the guard accepts");
    }

    [Fact]
    public void A_blank_private_name_matches_nothing()
    {
        PrivateNames.Of(["", "  "]).Names.Should().BeEmpty("an empty name would match every string and refuse the whole database");
    }

    private static IReadOnlyList<GuardViolation> Check(string table, string column, string text) =>
        PublicationGuard.Check([new PublishedText(table, column, "row-1", text)], Names, UrlColumns);
}
