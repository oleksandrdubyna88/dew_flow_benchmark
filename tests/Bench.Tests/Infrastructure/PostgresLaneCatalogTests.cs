using Bench.Domain;
using Bench.Domain.Lanes;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>
/// The lane catalog against a real Postgres.
///
/// <para>Against a real one because the two guarantees worth having here are the database's, not the
/// adapter's: a name is unique under concurrency, and a retirement does not overwrite an earlier one. A
/// fake would agree with whatever the adapter assumed.</para>
/// </summary>
[Collection("postgres")]
public sealed class PostgresLaneCatalogTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_lane_round_trips_with_its_doctrine_intact()
    {
        var name = Unique("lane");
        var lane = Lane(name, doctrine: "retrieval first, then confirm, then read");

        (await Catalog().AddAsync(lane, Ct)).Should().BeOfType<Outcome<ToolLane>.Ok>();

        var found = (await Catalog().FindAsync(name, Ct))
            .Should().BeOfType<Outcome<ToolLane>.Ok>().Subject.Value;

        found.Hash.Should().Be(lane.Hash);
        found.Definition.Doctrine.Should().Be("retrieval first, then confirm, then read");
        found.Definition.MaxTurns.Should().Be(25);
        found.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_name_already_in_the_catalog_is_refused_with_the_reason_a_lane_is_immutable()
    {
        var name = Unique("dup");
        await Catalog().AddAsync(Lane(name), Ct);

        var second = await Catalog().AddAsync(Lane(name, doctrine: "a different instruction"), Ct);

        second.Should().BeOfType<Outcome<ToolLane>.Fail>()
            .Which.Reason.Should().Contain("already in the catalog").And.Contain("never redefined");
    }

    [Fact]
    public async Task The_projected_columns_are_written_so_a_leaderboard_is_a_group_by()
    {
        // "Which wording wins, holding the tool set and the presentation fixed" is the question this catalog
        // exists for. It is a GROUP BY only if those three are columns rather than JSON.
        var name = Unique("cols");
        var lane = Lane(name, doctrine: "cover all the channels");
        await Catalog().AddAsync(lane, Ct);

        await using var db = postgres.NewContext();
        var row = await db.Lanes.AsNoTracking().SingleAsync(l => l.Name == name, Ct);

        row.ToolsHash.Should().Be(lane.Definition.ToolsHash);
        row.DoctrineHash.Should().Be(lane.Definition.DoctrineHash);
        row.DescriptionSet.Should().Be("concise-v1");
        row.Presentation.Should().Be(nameof(ToolPresentation.Bridge));
        row.Hash.Should().Be(lane.Hash);
    }

    [Fact]
    public async Task Two_lanes_differing_only_in_doctrine_share_a_tools_hash_and_not_a_hash()
    {
        // The shape a doctrine leaderboard reads: same surface, different words, and the pair must be
        // groupable by the first while remaining two rows by the second.
        var first = Lane(Unique("dc-a"), doctrine: "one");
        var second = Lane(Unique("dc-b"), doctrine: "two");
        await Catalog().AddAsync(first, Ct);
        await Catalog().AddAsync(second, Ct);

        await using var db = postgres.NewContext();
        var rows = await db.Lanes.AsNoTracking()
            .Where(l => l.Name == first.Name.Value || l.Name == second.Name.Value)
            .ToListAsync(Ct);

        rows.Select(r => r.ToolsHash).Distinct().Should().HaveCount(1);
        rows.Select(r => r.Hash).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task A_retired_lane_stays_findable_because_historical_cells_still_name_it()
    {
        var name = Unique("ret");
        await Catalog().AddAsync(Lane(name), Ct);

        var retired = await Catalog().RetireAsync(name, Noon, Ct);

        retired.Should().BeOfType<Outcome<ToolLane>.Ok>();
        var found = (await Catalog().FindAsync(name, Ct))
            .Should().BeOfType<Outcome<ToolLane>.Ok>().Subject.Value;
        found.IsActive.Should().BeFalse();
        found.RetiredAt.Should().Be(Noon);
    }

    [Fact]
    public async Task Retiring_twice_does_not_move_the_date_the_lane_actually_stopped()
    {
        // The guarded UPDATE. Two sessions retiring at once must produce one retirement date rather than the
        // later one overwriting the earlier — the date a report quotes has to be when it stopped.
        var name = Unique("twice");
        await Catalog().AddAsync(Lane(name), Ct);
        await Catalog().RetireAsync(name, Noon, Ct);

        var again = await Catalog().RetireAsync(name, Noon.AddHours(3), Ct);

        again.Should().BeOfType<Outcome<ToolLane>.Fail>()
            .Which.Reason.Should().Contain("already retired");
        (await Catalog().FindAsync(name, Ct))
            .Should().BeOfType<Outcome<ToolLane>.Ok>()
            .Which.Value.RetiredAt.Should().Be(Noon);
    }

    [Fact]
    public async Task A_listing_hides_retired_lanes_unless_it_is_asked_for_them()
    {
        var active = Unique("act");
        var gone = Unique("gone");
        await Catalog().AddAsync(Lane(active), Ct);
        await Catalog().AddAsync(Lane(gone), Ct);
        await Catalog().RetireAsync(gone, Noon, Ct);

        var listed = (await Catalog().ListAsync(includeRetired: false, Ct))
            .Should().BeOfType<Outcome<IReadOnlyList<ToolLane>>.Ok>().Subject.Value;
        var all = (await Catalog().ListAsync(includeRetired: true, Ct))
            .Should().BeOfType<Outcome<IReadOnlyList<ToolLane>>.Ok>().Subject.Value;

        listed.Select(l => l.Name.Value).Should().Contain(active).And.NotContain(gone);
        all.Select(l => l.Name.Value).Should().Contain(active).And.Contain(gone);
    }

    [Fact]
    public async Task A_row_that_cannot_be_read_fails_the_LISTING_rather_than_disappearing_from_it()
    {
        // A hand-edited row is a real event. Skipping it would render a catalog quietly missing a surface
        // somebody is measuring against — which reads as "that lane was never added".
        var name = Unique("broken");
        await Catalog().AddAsync(Lane(name), Ct);

        await using (var db = postgres.NewContext())
        {
            await db.Lanes.Where(l => l.Name == name)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.DefinitionJson, """{"presentation":"telepathy"}"""), Ct);
        }

        var listed = await Catalog().ListAsync(includeRetired: true, Ct);

        listed.Should().BeOfType<Outcome<IReadOnlyList<ToolLane>>.Fail>()
            .Which.Reason.Should().Contain(name).And.Contain("telepathy");

        // Removed again, and this is the point of the assertion above rather than an afterthought: the
        // failure is deliberately TOTAL — one unreadable row fails the whole listing — so a broken row left
        // behind would fail every other test sharing this database. It did, once, which is how this line
        // came to exist.
        await using var cleanup = postgres.NewContext();
        await cleanup.Lanes.Where(l => l.Name == name).ExecuteDeleteAsync(Ct);
    }

    private PostgresLaneCatalog Catalog() => new(postgres.NewContext());

    private static ToolLane Lane(string name, string doctrine = "") =>
        ToolLane.Create(
            name,
            displayName: "",
            LaneDefinition.Create(["a_tool", "b_tool"], "concise-v1", doctrine, ToolPresentation.Bridge, 25)
                .Should().BeOfType<Outcome<LaneDefinition>.Ok>().Subject.Value,
            Noon)
            .Should().BeOfType<Outcome<ToolLane>.Ok>().Subject.Value;

    /// <summary>Random, not a v7 guid — a truncated v7 is a timestamp, so two tests starting in the same
    /// millisecond would share their "unique" name and fail against each other's rows.</summary>
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16].TrimEnd('-');
}
