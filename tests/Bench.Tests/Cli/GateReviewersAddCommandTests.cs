using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>bench gate reviewers add</c> through <see cref="Program.Run"/> for the two things the fidelity plan found it
/// could not say: the thinking switch in the product's three states (D1), and the long-context price tier (D3).</summary>
[Collection("postgres")]
public sealed class GateReviewersAddCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A row added without the flag asked for OFF before three states existed — the product refused it for xai and
    /// glm (E7's first A/A). Absent is now the vendor's default; the bare flag stays "on", as rows were added with it.</summary>
    [Theory]
    [InlineData(null, ThinkingSetting.VendorDefault)]
    [InlineData("", ThinkingSetting.On)]
    [InlineData("on", ThinkingSetting.On)]
    [InlineData("off", ThinkingSetting.Off)]
    public async Task The_thinking_flag_is_the_products_three_states_and_absent_is_the_vendors_default(string? flag, ThinkingSetting expected)
    {
        var db = postgres.ConnectionString;
        var id = Unique();
        string[] thinking = flag switch { null => [], "" => ["--thinking"], _ => ["--thinking", flag] };

        var (code, _, error) = Run([.. Add(db, id), .. thinking]);

        code.Should().Be(ExitCodes.Pass, error);
        (await ReviewerAsync(db, id)).Definition.Transport.Thinking.Should().Be(expected);
    }

    [Fact]
    public async Task A_thinking_word_that_is_neither_on_nor_off_is_refused_by_name()
    {
        var db = postgres.ConnectionString;
        var id = Unique();

        var (code, _, error) = Run([.. Add(db, id), "--thinking", "maybe"]);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--thinking").And.Contain("'maybe'");
    }

    /// <summary>D3: grok doubles every rate past 200 000 tokens, and a row added by hand could not say so — its long calls were
    /// priced at the base rate. The four tier flags go together, and the vendor row carries the tier to the product.</summary>
    [Fact]
    public async Task A_price_tier_given_with_all_four_flags_is_stored_and_reaches_the_vendor_row()
    {
        var db = postgres.ConnectionString;
        var id = Unique();

        var (code, _, error) = Run([.. Add(db, id), .. Tier("200000", "4", "1", "12")]);

        code.Should().Be(ExitCodes.Pass, error);
        var reviewer = await ReviewerAsync(db, id);
        reviewer.Definition.Prices.TierFromTokens.Should().Be(200_000);
        (reviewer.Definition.Prices.TierIn, reviewer.Definition.Prices.TierCached, reviewer.Definition.Prices.TierOut).Should().Be((4m, 1m, 12m));
        var price = (JsonNode.Parse(CoaiVendorsSetting.From([reviewer], GateKind.Code, ResolvedReferences.Empty).Ok().Json)![0]!["price"])!;
        price["tierFrom"]!.GetValue<long>().Should().Be(200_000);
    }

    [Theory]
    [InlineData("--price-tier-out")]
    [InlineData("--price-tier-from")]
    public async Task A_price_tier_missing_one_of_its_four_flags_is_refused_naming_it(string missing)
    {
        var db = postgres.ConnectionString;
        var id = Unique();
        string[] tier = Tier("200000", "4", "1", "12");
        var given = tier.Select((a, i) => (a, i)).Where(p => p.a != missing && (p.i == 0 || tier[p.i - 1] != missing)).Select(p => p.a).ToArray();

        var (code, _, error) = Run([.. Add(db, id), .. given]);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain(missing).And.Contain("all four");
    }

    /// <summary>Code round, 2026-09-29: the tier flags without the base prices were dropped without a word — the row read
    /// "prices unknown" and the tier the operator typed was gone. A tier needs the prices it is a tier OF.</summary>
    [Fact]
    public async Task A_price_tier_without_the_base_prices_is_refused_rather_than_dropped()
    {
        var db = postgres.ConnectionString;
        var id = Unique();
        var add = Add(db, id);
        var withoutBase = add.Select((a, i) => (a, i)).Where(p => !p.a.StartsWith("--price-", StringComparison.Ordinal) && (p.i == 0 || !add[p.i - 1].StartsWith("--price-", StringComparison.Ordinal))).Select(p => p.a).ToArray();

        var (code, _, error) = Run([.. withoutBase, .. Tier("200000", "4", "1", "12")]);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--price-in").And.Contain("tier");
    }

    [Fact]
    public async Task A_price_tier_starting_below_one_token_is_refused_rather_than_read_as_no_tier()
    {
        var db = postgres.ConnectionString;
        var id = Unique();

        var (code, _, error) = Run([.. Add(db, id), .. Tier("0", "4", "1", "12")]);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--price-tier-from").And.Contain("at least 1");
    }

    /// <summary>A reviewer id of its own per test, in the fixture's ONE shared database — a database per test pushed the CI
    /// container past its client limit once already (53300, 2026-09-29).</summary>
    private static string Unique() => $"rev-{Guid.NewGuid():N}"[..24];

    private static string[] Add(string db, string id) =>
        ["gate", "reviewers", "add", "--id", id, "--runtime", "api", "--model", "grok-4.7", "--endpoint", "https://api.x.ai/v1",
         "--key-name", "grok", "--dialect", "xai", "--effort", "medium", "--price-in", "2", "--price-cached", "0.5", "--price-out", "6",
         "--gates", "plan,code", "--db", db];

    private static string[] Tier(string from, string @in, string cached, string @out) =>
        ["--price-tier-from", from, "--price-tier-in", @in, "--price-tier-cached", cached, "--price-tier-out", @out];

    private static async Task<GateReviewer> ReviewerAsync(string db, string id)
    {
        await using var context = PostgresFixture.Context(db);
        return (await new PostgresGateReviewerCatalog(context).GetAsync([GateReviewerId.Parse(id).Ok()], Ct)).Ok().Single();
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
