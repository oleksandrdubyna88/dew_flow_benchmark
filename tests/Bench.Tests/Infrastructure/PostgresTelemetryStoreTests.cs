using System.Runtime.CompilerServices;
using Bench.Application;
using Bench.Domain.Telemetry;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Telemetry;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Ingest against a real Postgres, because the guarantee under test — "run it twice and it
/// means it once" — is enforced by a unique index, and a fake would only agree with what we assumed
/// that index does.</summary>
[Collection("postgres")]
public sealed class PostgresTelemetryStoreTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_spool_ingested_twice_stores_its_records_once()
    {
        var records = Records();
        var store = NewStore();

        var first = await store.AppendAsync(records, TestContext.Current.CancellationToken);
        var second = await NewStore().AppendAsync(records, TestContext.Current.CancellationToken);

        // Re-running an ingest over a spool it already drained must be a no-op, and this is the number
        // that proves it — a resumed ingest that silently re-inserted would double every count in
        // every report built afterwards.
        first.Ingested.Should().Be(3);
        second.Ingested.Should().Be(0);
        second.Duplicate.Should().Be(3);
    }

    [Fact]
    public async Task An_interrupted_ingest_resumes_by_storing_exactly_what_is_missing()
    {
        var records = Records();
        var store = NewStore();

        // The shape of a host killed mid-file: some lines committed, the rest not.
        await store.AppendAsync([records[0]], TestContext.Current.CancellationToken);
        var resumed = await NewStore().AppendAsync(records, TestContext.Current.CancellationToken);

        resumed.Ingested.Should().Be(2);
        resumed.Duplicate.Should().Be(1);
    }

    [Fact]
    public async Task One_spool_containing_the_same_line_twice_stores_it_once()
    {
        var records = Records();

        // Deduplicated WITHIN the batch, not only against the database: the unique index would
        // otherwise reject the whole SaveChanges rather than the repeat, and lose the file.
        var report = await NewStore().AppendAsync([records[0], records[0]], TestContext.Current.CancellationToken);

        report.Ingested.Should().Be(1);
        report.Duplicate.Should().Be(1);
    }

    [Fact]
    public async Task Two_clients_on_one_tool_report_as_two_rows_never_as_one_blended_row()
    {
        var records = Records();
        var store = NewStore();

        var codex = records[0] with
        {
            Caller = records[0].Caller with { ClientName = Bench.Domain.Trace.Captured.Text("codex") },
        };

        await store.AppendAsync([records[0], codex], TestContext.Current.CancellationToken);
        var totals = await NewStore().TotalsAsync(TestContext.Current.CancellationToken);

        // An upstream system shipped daily aggregates without a caller column and then could not
        // attribute a change to the switch that caused it. Two callers, two rows, always.
        totals.Where(t => t.Tool == "rt_read_local_file").Should().HaveCountGreaterThanOrEqualTo(2);
        totals.Select(t => t.Caller).Should().Contain(c => c.StartsWith("codex", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_outcome_split_survives_storage_as_three_numbers()
    {
        await NewStore().AppendAsync(Records(), TestContext.Current.CancellationToken);

        var row = (await NewStore().TotalsAsync(TestContext.Current.CancellationToken))
            .Single(t => t.Caller.StartsWith("claude-code", StringComparison.Ordinal) && t.Tool == "rt_read_local_file");

        // Three numbers rather than a rate: a rate over four calls and a rate over four thousand read
        // identically, and only one of them can be acted on.
        row.Answered.Should().BeGreaterThan(0);
        row.Refused.Should().BeGreaterThan(0);
        row.Errored.Should().BeGreaterThan(0);
        (row.Answered + row.Refused + row.Errored).Should().Be(row.Calls);
    }

    [Fact]
    public async Task An_uncaptured_model_comes_back_uncaptured_rather_than_as_an_empty_value()
    {
        var records = Records();
        await NewStore().AppendAsync([records[0]], TestContext.Current.CancellationToken);

        await using var db = postgres.NewContext();
        var stored = db.ToolTelemetry.Single(t => t.Scope == records[0].Scope && t.Outcome == ToolOutcome.Answered);
        var back = PostgresTelemetryStore.ToDomain(stored);

        // Stored as a flag beside the value, never as a NULL: the first consumer to write a GROUP BY
        // over a nullable column reads NULL as "no model", which is the one reading this must prevent.
        back.Caller.Model.WasCaptured.Should().BeFalse();
        back.Caller.Model.Reason.Should().NotBeEmpty();
        back.Tokens.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public async Task An_empty_batch_touches_nothing()
    {
        var report = await NewStore().AppendAsync([], TestContext.Current.CancellationToken);

        // Field by field: this type carries a list, and a record struct compares one by reference.
        report.Ingested.Should().Be(0);
        report.Duplicate.Should().Be(0);
        report.Refused.Should().Be(0);
    }

    private PostgresTelemetryStore NewStore() => new(postgres.NewContext());

    /// <summary>The real emitter's three lines, re-scoped so each test owns its own records.
    /// <para>
    /// Every test in this class shares one container and one database — which is correct, because that
    /// is what a real ingest meets — so identical fixture lines would carry identical fingerprints and
    /// one test's insert would land as another's duplicate. Varying the scope makes each test's
    /// records genuinely distinct calls rather than isolating by truncating the table, which would
    /// hide exactly the cross-run deduplication these tests exist to prove.
    /// </para></summary>
    private static IReadOnlyList<ToolTelemetry> Records([CallerMemberName] string test = "")
    {
        var (records, _) = SpoolIngest.Read(Fixture.Text);
        return [.. records.Select(r => r with { Scope = $"{r.Scope}#{test}" })];
    }
}
