using Bench.Domain.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The reviewer catalog's storage of the thinking switch in the product's three states (D1 of the fidelity plan):
/// the vendor's default is a NULL column, and the two states every row stored before it existed — <c>true</c> and
/// <c>false</c> — read back with their meaning and their hash unchanged. A row whose hash moved would be refused as
/// "edited in place", and every cell measured under it would lose its reviewer.</summary>
[Collection("postgres")]
public sealed class PostgresGateReviewerCatalogTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ThinkingSetting.VendorDefault, null)]
    [InlineData(ThinkingSetting.On, true)]
    [InlineData(ThinkingSetting.Off, false)]
    public async Task Each_thinking_state_is_stored_as_its_column_and_reads_back_with_its_hash(ThinkingSetting thinking, bool? column)
    {
        var id = GateReviewerId.Parse($"think-{thinking.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}"[..40].TrimEnd('-')).Ok();
        var reviewer = GateReviewer.Create(id, GateReviewerTests.Definition(thinking: thinking).Ok(), Now);

        await using (var db = postgres.NewContext())
        {
            (await new PostgresGateReviewerCatalog(db).AddAsync(reviewer, Ct)).Ok();
        }

        await using var read = postgres.NewContext();
        (await read.GateReviewers.AsNoTracking().SingleAsync(r => r.Id == id.Value, Ct)).Thinking.Should().Be(column);
        var back = (await new PostgresGateReviewerCatalog(read).GetAsync([id], Ct)).Ok().Single();
        back.Definition.Transport.Thinking.Should().Be(thinking);
        back.Hash.Should().Be(reviewer.Hash, "a row must hash to what it was stored with, or it is refused as edited in place");
    }
}
