using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Registry;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The model registry, and the roles a test chose from it.
/// <para>
/// The last test is the one this schema exists to keep true: every stored configuration must be
/// publishable unedited. It reads the whole table rather than the rows it wrote, because the guarantee is
/// about the DATABASE, not about a well-behaved test.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresModelRegistryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_model_round_trips_with_its_references_and_its_sampling()
    {
        var registry = Registry();
        var key = Unique("qwen");

        (await registry.AddAsync(Model(key), Ct)).Failed().Should().BeFalse();

        var found = (await registry.FindAsync(key, Ct)).Ok();
        found.Config.BaseUrlRef.Should().Be("BENCH_QWEN_URL");
        found.Config.Sampling.Seed.Should().Be(7);
        found.Runtime.Should().Be(ModelRuntimeKind.OpenAiEndpoint);
        found.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task A_key_already_in_the_registry_is_refused_rather_than_replaced()
    {
        var registry = Registry();
        var key = Unique("dup");
        await registry.AddAsync(Model(key), Ct);

        var second = await registry.AddAsync(Model(key, modelId: "something-else:latest"), Ct);

        second.Failed().Should().BeTrue();
        second.Reason().Should().Contain("disabled, never replaced",
            "a run names the key it measured under, so replacing a row would relabel finished numbers");
    }

    [Fact]
    public async Task A_disabled_model_leaves_the_default_listing_and_keeps_its_history()
    {
        var registry = Registry();
        var key = Unique("retired");
        await registry.AddAsync(Model(key), Ct);

        (await registry.SetEnabledAsync(key, false, Ct)).Ok().Enabled.Should().BeFalse();

        (await registry.ListAsync(includeDisabled: false, Ct)).Ok().Should().NotContain(m => m.Key.Value == key);
        (await registry.ListAsync(includeDisabled: true, Ct)).Ok().Should().Contain(m => m.Key.Value == key);
        (await registry.FindAsync(key, Ct)).Failed().Should().BeFalse("a finished test that names it must still resolve");
    }

    [Fact]
    public async Task A_disabled_model_is_refused_when_a_test_would_use_it_by_name()
    {
        var registry = Registry();
        var key = Unique("off");
        await registry.AddAsync(Model(key), Ct);
        await registry.SetEnabledAsync(key, false, Ct);

        var resolved = ModelResolution.Endpoint((await registry.FindAsync(key, Ct)).Ok(), new FakeSecrets());

        resolved.Failed().Should().BeTrue();
        resolved.Reason().Should().Contain("disabled").And.Contain("by name");
    }

    [Fact]
    public async Task A_reference_that_does_not_resolve_here_is_refused_naming_the_reference()
    {
        var registry = Registry();
        var key = Unique("unset");
        await registry.AddAsync(Model(key, baseUrlRef: "BENCH_NOT_SET_ANYWHERE"), Ct);

        var resolved = ModelResolution.Endpoint((await registry.FindAsync(key, Ct)).Ok(), new FakeSecrets());

        // Discovered at test creation, by name — not three hours into a sweep as a wall of identical
        // transport failures.
        resolved.Failed().Should().BeTrue();
        resolved.Reason().Should().Contain("BENCH_NOT_SET_ANYWHERE").And.Contain("unset on this machine");
    }

    [Fact]
    public async Task A_runtime_this_build_cannot_drive_is_refused_rather_than_attempted()
    {
        var registry = Registry();
        var key = Unique("cli");
        await registry.AddAsync(Model(key, runtime: ModelRuntimeKind.CliClaude), Ct);

        var resolved = ModelResolution.Endpoint((await registry.FindAsync(key, Ct)).Ok(), new FakeSecrets());

        resolved.Reason().Should().Contain("no runtime for it").And.Contain("tool benchmark");
    }

    [Fact]
    public async Task A_tests_subjects_and_its_ordered_arbiters_are_stored_on_the_test()
    {
        var roles = new PostgresRunRoleStore(postgres.NewContext());
        var runId = await SeedRunAsync();
        var first = Unique("primary");
        var second = Unique("second");

        (await roles.SaveSubjectsAsync(runId, [Key(first)], Noon, Ct)).Ok().Should().Be(1);
        (await roles.SaveJudgesAsync(runId, [Key(first), Key(second)], Noon, Ct)).Ok().Should().Be(2);

        var judges = (await roles.JudgesAsync(runId, Ct)).Ok();
        judges.Select(j => j.Model.Value).Should().Equal([first, second],
            "arbiters are ordered — 'the primary arbiter disagreed' is a sentence only an order makes evaluable");
        judges[0].Ordinal.Should().Be(0);
        (await roles.SubjectsAsync(runId, Ct)).Ok().Should().ContainSingle();
    }

    [Fact]
    public async Task A_test_that_names_nobody_is_refused()
    {
        var roles = new PostgresRunRoleStore(postgres.NewContext());

        (await roles.SaveSubjectsAsync(await SeedRunAsync(), [], Noon, Ct)).Reason()
            .Should().Contain("names no subject", "a test that measures nobody is not a test");
    }

    [Fact]
    public async Task A_subject_may_be_ADDED_to_an_existing_test_but_never_twice()
    {
        var roles = new PostgresRunRoleStore(postgres.NewContext());
        var runId = await SeedRunAsync();
        var first = Unique("early");
        var later = Unique("later");

        await roles.SaveSubjectsAsync(runId, [Key(first)], Noon, Ct);

        // Adding one later is the expansion the matrix is built around: a settled test reopens for exactly
        // the new cells. It is not a second write of a frozen choice.
        (await new PostgresRunRoleStore(postgres.NewContext())
            .SaveSubjectsAsync(runId, [Key(later)], Noon.AddDays(1), Ct)).Ok().Should().Be(1);

        var again = await new PostgresRunRoleStore(postgres.NewContext())
            .SaveSubjectsAsync(runId, [Key(first)], Noon.AddDays(2), Ct);

        again.Failed().Should().BeTrue();
        again.Reason().Should().Contain("appears once per role",
            "the same model twice would double a column in every report the test produces");
        (await roles.SubjectsAsync(runId, Ct)).Ok().Should().HaveCount(2);
    }

    [Fact]
    public async Task An_arbiter_added_later_is_second_rather_than_a_rival_primary()
    {
        var roles = new PostgresRunRoleStore(postgres.NewContext());
        var runId = await SeedRunAsync();
        var primary = Unique("first");
        var late = Unique("late");

        await roles.SaveJudgesAsync(runId, [Key(primary)], Noon, Ct);
        await new PostgresRunRoleStore(postgres.NewContext()).SaveJudgesAsync(runId, [Key(late)], Noon.AddDays(1), Ct);

        var judges = (await roles.JudgesAsync(runId, Ct)).Ok();

        judges.Select(j => j.Model.Value).Should().Equal([primary, late]);
        judges.Select(j => j.Ordinal).Should().Equal([0, 1],
            "an ordinal that restarted would leave two models both claiming to be primary");
    }

    [Fact]
    public async Task No_stored_model_configuration_contains_an_absolute_path_or_a_secret_shaped_value()
    {
        var registry = Registry();
        await registry.AddAsync(Model(Unique("publishable")), Ct);

        await using var db = postgres.NewContext();
        var rows = await db.Models.AsNoTracking().Select(m => new { m.Key, m.ConfigJson }).ToListAsync(Ct);

        rows.Should().NotBeEmpty("a guarantee asserted over an empty table is not asserted");

        foreach (var row in rows)
        {
            // Re-read through the domain's own rule rather than by pattern-matching the JSON here: the
            // guard and the assertion must be the same rule, or one of them will drift and it will be the
            // one nobody runs. This is the whole-database version of the promise §3.5 makes about results.
            ModelConfigJson.Read(row.ConfigJson).Failed().Should().BeFalse(
                $"model '{row.Key}' holds a configuration that would not survive publication: {row.ConfigJson}");
        }
    }

    private PostgresModelRegistry Registry() => new(postgres.NewContext());

    /// <summary>Random, not a v7 guid — a truncated v7 is a timestamp, so tests starting in the same
    /// millisecond would share their "unique" key and fail against each other's rows.</summary>
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16].TrimEnd('-');

    private static ModelKey Key(string value) => ModelKey.Parse(value).Ok();

    private static RegisteredModel Model(
        string key,
        string modelId = "qwen3-coder:latest",
        string baseUrlRef = "BENCH_QWEN_URL",
        ModelRuntimeKind runtime = ModelRuntimeKind.OpenAiEndpoint) =>
        RegisteredModel.Create(
            key,
            "Qwen 3 Coder",
            runtime,
            ModelHosting.Local,
            ModelConfig.Parse(modelId, baseUrlRef, string.Empty, string.Empty, Sampling.Deterministic(7)).Ok(),
            Noon).Ok();

    private async Task<Guid> SeedRunAsync()
    {
        var commit = CommitSha.Parse(new string('e', 40)).Ok();
        var target = MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/roles.git").Ok(), commit);
        var run = BenchRun.Planned("roles", target, EngineRef.Filesystem(), "roles@v1#abc", Noon);

        var cells = Matrix.Plan(
            [new Question("q1", "p", [Expectation.File(SourceAnchor.File("src/A.cs", commit))], string.Empty)],
            repeats: 1,
            [new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1))],
            [Lane.Named("no-tools")]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, cells, Ct);

        return run.Id;
    }

    /// <summary>One reference that resolves and everything else unset — the state of a real machine, where
    /// some rows are usable and some are not.</summary>
    private sealed class FakeSecrets : ISecretSource
    {
        public Outcome<string> Resolve(string reference) =>
            reference == "BENCH_QWEN_URL"
                ? Outcome<string>.Success("http://127.0.0.1:11434/v1")
                : Outcome<string>.Failure(
                    $"the environment variable '{reference}' is unset on this machine — a registry row names references, "
                    + "and this one resolves to nothing");
    }
}
