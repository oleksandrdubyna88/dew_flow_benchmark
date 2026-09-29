using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The reviewer catalog row: a model PLUS its calibrated transport, hashed, never edited; references
/// where a value would be this machine's or this account's identity; and a duplicate detected rather than
/// explained. Mirrors <c>RetrievalVariantTests</c> because the row mirrors the variant row.</summary>
public sealed class GateReviewerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("http://localhost:8080/v1")]
    [InlineData("http://192.168.1.5:8000/v1")]
    [InlineData("http://10.0.0.7/v1")]
    [InlineData("http://172.20.3.9/v1")]
    [InlineData("http://gpu-box:11434/v1")]
    [InlineData("http://gpu-box.local:11434/v1")]
    [InlineData("http://[::1]:11434/v1")]
    public void A_loopback_or_private_endpoint_given_as_a_value_is_refused_by_name(string endpoint)
    {
        ReviewerEndpoint.Parse(endpoint).Reason()
            .Should().Contain("machine-local").And.Contain("NAME",
                "a loopback address is this machine's identity, and a reviewer row is published with the results");
    }

    [Theory]
    [InlineData("http://[::ffff:127.0.0.1]:11434/v1")]
    [InlineData("http://[::ffff:7f00:1]:11434/v1")]
    [InlineData("http://[::ffff:10.0.0.7]/v1")]
    [InlineData("http://[::ffff:192.168.1.5]:8000/v1")]
    [InlineData("http://[::ffff:172.20.3.9]/v1")]
    [InlineData("http://[::ffff:169.254.1.1]/v1")]
    public void An_ipv4_mapped_ipv6_address_is_judged_as_the_ipv4_address_it_carries(string endpoint)
    {
        ReviewerEndpoint.Parse(endpoint).Reason().Should().Contain("machine-local",
            "::ffff:127.0.0.1 IS 127.0.0.1 — a range check that only reads the IPv4 bytes of an IPv4 address publishes this machine");
    }

    [Fact]
    public void A_mapped_public_address_is_still_a_value()
    {
        ReviewerEndpoint.Parse("https://[::ffff:8.8.8.8]/v1").Ok().Should().BeOfType<ReviewerEndpoint.Value>(
            "the normalisation is for the range check, not a refusal of the notation — this one names no machine of ours");
    }

    /// <summary>A value endpoint is published with the results and copied into the product's vendor string, so it
    /// may carry no credential: no user-info, no query (a <c>?key=</c> is a key), no fragment, and no plain http to a
    /// public host (a bearer token sent in the clear). The refusal never repeats the url — the url IS the secret.</summary>
    [Theory]
    [InlineData("https://user:sentinel-4711@api.vendor.example.com/v1", "user-info")]
    [InlineData("https://api.vendor.example.com/v1?token=sentinel-4711", "query")]
    [InlineData("https://api.vendor.example.com/v1#sentinel-4711", "fragment")]
    [InlineData("http://api.vendor.example.com/sentinel-4711/v1", "https")]
    public void A_value_endpoint_carrying_a_credential_or_sent_in_the_clear_is_refused_without_repeating_it(string endpoint, string rule)
    {
        var reason = ReviewerEndpoint.Parse(endpoint).Reason();

        reason.Should().Contain(rule).And.NotContain("sentinel-4711", "a refusal that quotes the url publishes what it refused");
    }

    [Fact]
    public void A_short_or_empty_hash_still_describes_rather_than_throwing()
    {
        var ids = new[] { Id("grok-a"), Id("grok-b") };

        new SharedConfiguration("abc", ids).Describe.Should().Contain("(abc)",
            "a stamp is a label; producing one must never be the thing that fails");
        new SharedConfiguration(string.Empty, ids).Describe.Should().Contain("under 2 names");
        HashText.Short("0123456789abcdef").Should().Be("0123456789ab");
        HashText.Short("abc").Should().Be("abc");
    }

    [Fact]
    public void A_public_vendor_url_is_a_value_and_a_name_is_a_reference()
    {
        ReviewerEndpoint.Parse("https://api.x.ai/v1").Ok().Should().Be(new ReviewerEndpoint.Value("https://api.x.ai/v1"));
        ReviewerEndpoint.Parse("LOCAL_LLM_URL").Ok().Should().Be(new ReviewerEndpoint.Reference("LOCAL_LLM_URL"));
        ReviewerEndpoint.Parse(string.Empty).Ok().Should().Be(new ReviewerEndpoint.None());
        ReviewerEndpoint.Parse("not a url, not a name").Reason().Should().Contain("neither a url nor a usable reference");
    }

    [Fact]
    public void An_unset_model_is_refused_and_never_defaulted()
    {
        Definition(model: " ").Reason().Should().Contain("unset id is a refusal");
    }

    [Fact]
    public void An_api_reviewer_without_an_endpoint_is_refused()
    {
        Definition(endpoint: string.Empty).Reason().Should().Contain("api reviewer needs an endpoint");
    }

    [Theory]
    [InlineData("sk-abc123def456")]
    [InlineData("xai-live-key-here")]
    [InlineData("/home/someone/.keys/grok")]
    public void A_key_name_that_is_a_secret_or_a_path_is_refused(string keyName)
    {
        Definition(keyName: keyName).Reason().Should().Contain("keyName").And.MatchRegex("NAME|SECRET",
            "the refusal names the rule — a name, never a secret or a path");
    }

    [Fact]
    public void A_creds_key_ref_and_an_executable_ref_are_names()
    {
        Definition(credsKeyRef: "COAI_CREDS_KEY").Ok().CredsKeyRef.Should().Be("COAI_CREDS_KEY");
        Definition(credsKeyRef: @"C:\keys\creds.txt").Reason().Should().Contain("credsKeyRef was given a VALUE");
        Definition(executableRef: "CODEX_EXE").Ok().ExecutableRef.Should().Be("CODEX_EXE");
        Definition(executableRef: "/usr/local/bin/codex").Reason().Should().Contain("executableRef was given a VALUE");
    }

    [Fact]
    public void Moving_a_separator_between_the_model_and_the_url_changes_the_hash()
    {
        var a = Definition(model: "grok-4.7|endpoint=url:https://api.x.ai/v1", endpoint: "https://api.x.ai/v2").Ok();
        var b = Definition(model: "grok-4.7", endpoint: "https://api.x.ai/v1|endpoint=url:https://api.x.ai/v2").Ok();

        b.Hash.Should().NotBe(a.Hash,
            "two different models at two different urls are two subjects — a '|'-joined form could not tell them apart");
    }

    [Fact]
    public void Moving_a_separator_between_the_dialect_and_the_effort_changes_the_transport()
    {
        var a = ReviewerTransport.Parse("xai,effort=high", "medium", 8192, 20, 3, 20, true).Ok();
        var b = ReviewerTransport.Parse("xai", "high,effort=medium", 8192, 20, 3, 20, true).Ok();

        b.Canonical.Should().NotBe(a.Canonical, "the transport is part of the subject, and each of its knobs is its own field");
    }

    [Fact]
    public void The_hash_changes_when_the_transport_changes_because_the_transport_is_part_of_the_subject()
    {
        var medium = Definition(effort: "medium").Ok();
        var high = Definition(effort: "high").Ok();

        high.Hash.Should().NotBe(medium.Hash, "one model at two efforts never answered inside one deadline and did inside the other");
        Definition(effort: "medium").Ok().Hash.Should().Be(medium.Hash, "the hash is a function of the definition alone");
    }

    [Fact]
    public void Two_rows_with_one_hash_are_reported_as_one_configuration_under_two_names()
    {
        var a = GateReviewer.Create(Id("grok-medium"), Definition().Ok(), Now);
        var b = GateReviewer.Create(Id("grok-med-again"), Definition().Ok(), Now);
        var c = GateReviewer.Create(Id("grok-high"), Definition(effort: "high").Ok(), Now);

        var shared = GateReviewerCatalog.SameConfiguration([a, b, c]);

        shared.Should().ContainSingle();
        shared[0].Ids.Select(i => i.Value).Should().Equal(["grok-med-again", "grok-medium"]);
        shared[0].Describe.Should().Contain("one configuration").And.Contain("under 2 names");
        GateReviewerCatalog.SameConfiguration([a, c]).Should().BeEmpty();
    }

    [Fact]
    public void Retiring_keeps_the_row_readable_and_leaves_the_original_untouched()
    {
        var active = GateReviewer.Create(Id("grok-medium"), Definition().Ok(), Now);

        var retired = active.Retire(Now.AddDays(1)).Ok();
        var rehydrated = GateReviewer.Rehydrate("grok-medium", retired.Definition, retired.AddedAt, retired.RetiredAt).Ok();

        retired.IsActive.Should().BeFalse();
        active.IsActive.Should().BeTrue("a row is a value; retiring one may never mutate the one already held elsewhere");
        rehydrated.Hash.Should().Be(active.Hash, "a retired row still resolves every run that names it");
        rehydrated.Stamp.Should().Be(active.Stamp);
        retired.Retire(Now.AddDays(2)).Reason().Should().Contain("already retired");
        GateReviewer.Rehydrate("Grok Medium", retired.Definition, Now, default).Reason().Should().Contain("reviewer id");
    }

    [Fact]
    public void Prices_are_values_and_unknown_is_a_state_not_a_zero()
    {
        ReviewerPrices.Unknown.Known.Should().BeFalse();
        ReviewerPrices.Unknown.Canonical.Should().Be("unknown");
        ReviewerPrices.Of(2.0m, 0.5m, 6.0m, 200_000, 4.0m, 1.0m, 12.0m).Ok().HasTier.Should().BeTrue();
        ReviewerPrices.Of(-1m, 0m, 0m).Reason().Should().Contain("negative price");
        ReviewerPrices.Of(2.0m, 0.5m, 6.0m).Ok().Canonical.Should().Be("in=2,cached=0.5,out=6");
    }

    [Fact]
    public void A_transport_names_every_knob_and_refuses_an_unset_one()
    {
        ReviewerTransport.Parse("", "medium", 8192, 20, 3, 20, true).Reason().Should().Contain("dialect");
        ReviewerTransport.Parse("xai", "", 8192, 20, 3, 20, true).Reason().Should().Contain("reasoning effort").And.Contain("'none'");
        ReviewerTransport.Parse("xai", "medium", 0, 20, 3, 20, true).Reason().Should().Contain("maxTokens");
        ReviewerTransport.Parse("xai", "medium", 8192, 20, 3, 20, true).Ok().Canonical
            .Should().Be("9:transport3:xai6:medium4:81922:201:32:2011:thinking-on", "every knob is one length-prefixed field");
    }

    /// <summary>D1 of the fidelity plan: the product reads thinking in THREE states (absent is the vendor's default), the
    /// bench modelled two, and a row added without the flag asked for OFF — which the product refuses for xai and glm. The
    /// two states stored before keep their canonical text exactly, so every stored row still hashes to its stored hash.</summary>
    [Fact]
    public void Thinking_is_three_states_and_the_two_stored_before_keep_their_canonical_text()
    {
        static string Tail(ThinkingSetting t) => ReviewerTransport.Parse("xai", "medium", 8192, 20, 3, 20, t).Ok().Canonical.Split("2:20")[^1];

        Tail(ThinkingSetting.On).Should().Be("11:thinking-on");
        Tail(ThinkingSetting.Off).Should().Be("12:thinking-off");
        Tail(ThinkingSetting.VendorDefault).Should().Be("16:thinking-default");
        ReviewerTransport.Parse("xai", "medium", 8192, 20, 3, 20, true).Ok().Thinking.Should().Be(ThinkingSetting.On);
        ReviewerTransport.Parse("xai", "medium", 8192, 20, 3, 20, false).Ok().Thinking.Should().Be(ThinkingSetting.Off);
    }

    /// <summary>The hashes rows were STORED under before three states existed, computed by that code (main `3e08dd7`, with the
    /// two-state transport) and frozen here — so a change that moved them is a red test, not a catalog of rows refused as
    /// "edited in place". The canonical text agreeing with itself proves nothing about the rows already stored (the cadence
    /// consultation, 2026-09-29).</summary>
    [Theory]
    [InlineData(true, "8aeec9c5c5a5084a1c47683a2221ad4771bab1ddce2ad3ffa6a132670734b540")]
    [InlineData(false, "296dfe988e01378c43236fdbc3d4b81f9589f87783ae1288fa2a018e1c0cfc49")]
    public void A_definition_stored_before_three_states_hashes_exactly_as_it_did(bool thinking, string storedHash)
    {
        var definition = ReviewerDefinition.Parse(
            ReviewerRuntime.Api, "grok-4.7", ReviewerEndpoint.Parse("https://api.x.ai/v1").Ok(), "grok", "COAI_CREDS_KEY", string.Empty, string.Empty,
            ReviewerTransport.Parse("xai", "medium", 8192, 20, 3, 20, thinking).Ok(),
            ReviewerPrices.Of(2.0m, 0.5m, 6.0m, 200_000, 4.0m, 1.0m, 12.0m).Ok(),
            HostedGates.Of([GateKind.Plan, GateKind.Code, GateKind.Feature]).Ok()).Ok();

        definition.Hash.Should().Be(storedHash);
    }

    private static GateReviewerId Id(string value) => GateReviewerId.Parse(value).Ok();

    internal static Outcome<ReviewerDefinition> Definition(
        string model = "grok-4.7",
        string endpoint = "https://api.x.ai/v1",
        string keyName = "grok",
        string credsKeyRef = "COAI_CREDS_KEY",
        string executableRef = "",
        string effort = "medium",
        ReviewerRuntime runtime = ReviewerRuntime.Api,
        IReadOnlyList<GateKind>? gates = null,
        ThinkingSetting thinking = ThinkingSetting.On) =>
        ReviewerDefinition.Parse(
            runtime,
            model,
            ReviewerEndpoint.Parse(endpoint).Ok(),
            keyName,
            credsKeyRef,
            executableRef,
            remoteVendor: string.Empty,
            ReviewerTransport.Parse("xai", effort, 8192, 20, 3, 20, thinking).Ok(),
            ReviewerPrices.Of(2.0m, 0.5m, 6.0m, 200_000, 4.0m, 1.0m, 12.0m).Ok(),
            HostedGates.Of(gates ?? [GateKind.Plan, GateKind.Code, GateKind.Feature]).Ok());
}
