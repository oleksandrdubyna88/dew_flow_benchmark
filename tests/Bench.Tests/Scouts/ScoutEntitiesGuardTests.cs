using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The publication guard, structural first, over the scout store — the probes' walk (<see cref="TextSurface"/>) over the
/// <c>scout_*</c> entities the EF model maps. Every property that can carry text is named here as <c>Type.Property</c>; a new column is
/// red until it is. ONE sentence is stored by design — the task statement, which §3.12 commits to <c>tasks.json</c> anyway and which
/// passed the leak check; no scout report, claim or transcript has a column.</summary>
public sealed class ScoutEntitiesGuardTests
{
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        // scout_runs — the frozen pins (a version, hashes, model ids), the arms as enum NAMES, the task ids as slugs, and (S4) the campaign's
        // seed string — a published word, the domain the private commitments are bound to.
        "ScoutRunRow.PinCliVersion", "ScoutRunRow.PinPluginCacheHash", "ScoutRunRow.PinResolvedOpus", "ScoutRunRow.PinResolvedSonnet", "ScoutRunRow.PinSettingsHash",
        "ScoutRunRow.Arms", "ScoutRunRow.TaskIds", "ScoutRunRow.Campaign",

        // scout_tasks — a slug, a repository (a public slug; a PRIVATE task's local url is LOCAL ONLY — the publication walk replaces it with the
        // label, ScoutRowPublication), two shas, G and the removals as repository-relative paths (RepositoryRelative refuses a rooted, url or
        // climbing one; emptied for a private task by the walk), a tree hash, the statement (leak-checked, ≤ 3 sentences; emptied for a private
        // task), and (S4) a language word and the two HMAC commitments a private task is published as.
        "ScoutTaskRow.TaskId", "ScoutTaskRow.Repository", "ScoutTaskRow.ParentSha", "ScoutTaskRow.FixSha", "ScoutTaskRow.Statement",
        "ScoutTaskRow.GroundTruth", "ScoutTaskRow.Removals", "ScoutTaskRow.TreeHash",
        "ScoutTaskRow.Language", "ScoutTaskRow.StatementCommitment", "ScoutTaskRow.GroundTruthCommitment",

        // scout_cells — a slug, the claim's owner (never published: GatePublication.ClaimOwnerColumns), the CLI pin.
        "ScoutCellRow.TaskId", "ScoutCellRow.Owner", "ScoutCellRow.OwnerHost",
        "ScoutCellRow.PinBinarySha256", "ScoutCellRow.PinVersionText", "ScoutCellRow.PinGitSha", "ScoutCellRow.PinCheckedTree",

        // scout_records — model ids, a prompt's SHA-256, and the KEY FILES as repository-relative paths the matcher normalised.
        "ScoutRecordRow.InputModel", "ScoutRecordRow.PromptSha256", "ScoutRecordRow.ResolvedModel", "ScoutRecordRow.KeyFiles",

        // scout_claims (S3) — a claim's repository-relative path (empty when it named a machine path), its statement as a SHA-256 only, the
        // assessor's model id and the rubric's SHA-256. Never the claim's text, never the assessor's note.
        "ScoutClaimRow.Path", "ScoutClaimRow.StatementSha256", "ScoutClaimRow.AssessorModel", "ScoutClaimRow.RubricSha256",
    };

    [Fact]
    public void Every_text_bearing_column_of_every_scout_table_is_allowed_by_type_and_name()
    {
        TextSurface.Offenders(ScoutEntities(), Allowed).Should().BeEmpty(
            "a text column that is not an id, a hash, an enum name, a model id or a repository-relative path could hold a scout's report in a "
            + "table that is published; name it here, as Type.Property, only if it provably cannot");
    }

    [Fact]
    public void The_walk_covers_all_six_scout_tables_from_the_model()
    {
        ScoutEntities().Select(t => t.Name).Should().BeEquivalentTo(["ScoutRunRow", "ScoutTaskRow", "ScoutCellRow", "ScoutRecordRow", "ScoutAttemptRow", "ScoutClaimRow"]);
    }

    [Fact]
    public void Every_allow_list_entry_names_a_column_that_exists_and_the_statement_is_the_one_sentence()
    {
        var carriers = TextSurface.Carriers(ScoutEntities()).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

        Allowed.Where(entry => !carriers.Contains(entry)).Should().BeEmpty("an allow-list entry for a column that is gone is a door left open");
        carriers.Where(c => c.EndsWith("Text", StringComparison.Ordinal) && !c.EndsWith("VersionText", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public void The_scout_tables_join_the_export_walk_after_the_gate_and_the_probes()
    {
        using var db = Context();

        PostgresGatePublicationSource.PublishedEntities(db).Select(e => e.GetTableName()).Should()
            .EndWith(["probe_cells", "probe_runs", "scout_attempts", "scout_cells", "scout_claims", "scout_records", "scout_runs", "scout_tasks"]);
    }

    private static IReadOnlyList<Type> ScoutEntities()
    {
        using var db = Context();

        return [.. PostgresGatePublicationSource.ScoutEntities(db).Select(e => e.ClrType)];
    }

    private static BenchDbContext Context() => new(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql("Host=model-only").Options);
}
