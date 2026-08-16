using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Variants;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The catalog against a real database — and the one column that ties a measured cell to the
/// configuration that produced it.
/// <para>
/// The uniqueness of a name is a database guarantee rather than a read-then-write, for the same reason a
/// claim is: two sessions adding the same variant would both find nothing and both insert, and the matrix
/// would then hold two rows a report cannot tell apart.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresVariantCatalogTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_variant_survives_a_round_trip_with_its_definition_intact()
    {
        var catalog = NewCatalog();
        var variant = Variant("round-trip");

        (await catalog.AddAsync(variant, Ct)).Ok().Name.Value.Should().Be("round-trip");

        var found = (await catalog.FindAsync("round-trip", Ct)).Ok();
        found.Definition.Should().Be(variant.Definition);
        found.Hash.Should().Be(variant.Hash);
        found.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Two_variants_cannot_share_a_name()
    {
        var catalog = NewCatalog();
        await catalog.AddAsync(Variant("shared-name"), Ct);

        (await NewCatalog().AddAsync(Variant("shared-name"), Ct))
            .Reason().Should().Contain("shared-name");
    }

    [Fact]
    public async Task A_retired_variant_leaves_the_active_list_and_stays_readable()
    {
        var catalog = NewCatalog();
        await catalog.AddAsync(Variant("to-retire"), Ct);

        (await catalog.RetireAsync("to-retire", Noon, Ct)).Ok().IsActive.Should().BeFalse();

        (await catalog.ListAsync(includeRetired: false, Ct)).Ok().Should().NotContain(v => v.Name.Value == "to-retire");
        (await catalog.ListAsync(includeRetired: true, Ct)).Ok().Should().Contain(v => v.Name.Value == "to-retire");
        (await catalog.FindAsync("to-retire", Ct)).Ok().RetiredAt.Should().Be(Noon,
            "historical cells name this variant; it stops being offered, it never stops resolving");
    }

    [Fact]
    public async Task Retiring_a_variant_that_is_not_there_is_refused_by_name()
    {
        (await NewCatalog().RetireAsync("never-existed", Noon, Ct))
            .Reason().Should().Contain("never-existed");
    }

    [Fact]
    public async Task A_cell_remembers_which_variant_it_ran_under()
    {
        var catalog = NewCatalog();
        var variant = Variant("cell-bound");
        await catalog.AddAsync(variant, Ct);

        var (run, cells) = Plan(variant.Select());
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        var claimed = (await store.ClaimNextAsync(run.Id, "worker", Ct)).Ok();

        claimed.Variant.Should().Be(variant.Select());
        claimed.Leg.Should().Contain("#cell-bound");
    }

    [Fact]
    public async Task A_cell_planned_without_the_catalog_stores_no_variant_rather_than_a_made_up_one()
    {
        var (run, cells) = Plan(VariantSelection.None);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        var claimed = (await store.ClaimNextAsync(run.Id, "worker", Ct)).Ok();

        claimed.Variant.Should().Be(VariantSelection.None);
    }

    private PostgresVariantCatalog NewCatalog() => new(postgres.NewContext());

    private static RetrievalVariant Variant(string name) =>
        RetrievalVariant.Create(name, string.Empty, VariantDefinitionTests.Retrieval().Ok(), Noon).Ok();

    private static (BenchRun Run, IReadOnlyList<RunCell> Cells) Plan(VariantSelection variant)
    {
        var target = new MeasurementTarget(
            RepoUrl.Parse("https://github.com/org/repo.git").Ok(),
            CommitSha.Parse(new string('d', 40)).Ok(),
            []);

        var question = new Question("q1", "prompt", [Expectation.File(SourceAnchor.File("src/F.cs", target.Commit))], string.Empty);
        var subject = new Subject(ModelRef.Parse("m1", ModelHosting.Local).Ok(), Sampling.Deterministic(1));
        var cells = Matrix.Plan([question], 1, [subject], [Lane.Named("a")], [variant]).Ok();
        var run = BenchRun.Planned("variant-cells", target, EngineRef.Filesystem(), "s@v1#abc", Noon);

        return (run, [.. cells.Select(c => RunCell.Pending(run.Id, c))]);
    }
}
