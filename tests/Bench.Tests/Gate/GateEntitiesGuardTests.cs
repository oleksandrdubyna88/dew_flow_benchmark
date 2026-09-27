using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The publication guard, structural first — over the STORE this time, as <see cref="GateContractsGuardTests"/>
/// is over the wire. Every <c>gate_*</c> entity the EF model maps is walked (<see cref="TextSurface"/>, the same walk),
/// and every property that can carry text must be named here as <c>Type.Property</c>. A new column is red until it is
/// named, and the one free-text column — the redacted failure cause — is named on its own type as the exception.
/// <para>
/// The entities come from the MODEL, not from a list in this file: a seventh <c>gate_*</c> table is walked the moment
/// it is mapped, which is the property a hand-kept list of types cannot have.
/// </para></summary>
public sealed class GateEntitiesGuardTests
{
    /// <summary>What text a gate row may carry, by the type that carries it. Grouped so a reviewer can check that none
    /// of it could hold a sentence somebody wrote about private code.</summary>
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        // gate_runs — the suite's stamp, and native or the harness an import came from.
        "GateRunRow.SuiteStamp", "GateRunRow.Source",

        // gate_cells — slug ids, the claim's owner (a label, a host name), the pin (hashes, the --version text, the
        // product tree the dirty check covered), the vendor's finish WORDS, and hashes.
        "GateCellRow.TaskId", "GateCellRow.ReviewerId", "GateCellRow.Owner", "GateCellRow.OwnerHost",
        "GateCellRow.PinBinarySha256", "GateCellRow.PinVersionText", "GateCellRow.PinGitSha", "GateCellRow.PinCheckedTree",
        "GateCellRow.FinishReasons", "GateCellRow.SettingsHash", "GateCellRow.PromptHash",
        TheOneException,

        // gate_findings — two hashes and nothing else, by construction of GateFinding.
        "GateFindingRow.TextHash", "GateFindingRow.FileHash",

        // gate_verdicts — rubric id and hash, the verdict case's name, hashes, a seed id, the assessor's catalog id.
        "GateVerdictRow.RubricId", "GateVerdictRow.RubricHash", "GateVerdictRow.Kind", "GateVerdictRow.ClusterHash",
        "GateVerdictRow.SeedHit", "GateVerdictRow.AssessorId", "GateVerdictRow.BatchId", "GateVerdictRow.PromptHash",

        // gate_reviewers — the catalog row: an id, its hash, the model id, REFERENCES (names, never values), the one
        // public vendor url (checked by the endpoint rule), the dialect and the effort words.
        "GateReviewerRow.Id", "GateReviewerRow.Hash", "GateReviewerRow.Model", "GateReviewerRow.EndpointUrl", "GateReviewerRow.EndpointRef",
        "GateReviewerRow.KeyName", "GateReviewerRow.CredsKeyRef", "GateReviewerRow.ExecutableRef", "GateReviewerRow.RemoteVendor",
        "GateReviewerRow.Dialect", "GateReviewerRow.ReasoningEffort",

        // gate_artifacts — a path RELATIVE to the artefact root, built from ids, and the bytes' hash.
        "GateArtifactRow.RelativePath", "GateArtifactRow.Sha256",
    };

    /// <summary>The one column that is prose: the failure cause, redacted before it is written.</summary>
    private const string TheOneException = "GateCellRow.FailureText";

    [Fact]
    public void Every_text_bearing_column_of_every_gate_table_is_allowed_by_type_and_name()
    {
        TextSurface.Offenders(GateEntities(), Allowed).Should().BeEmpty(
            "a text column that is not an id, a hash, an enum name, a reference or a word could hold a finding's or a prompt's "
            + "text in a table that is published; name it here, as Type.Property, only if it provably cannot");
    }

    [Fact]
    public void The_walk_covers_all_six_gate_tables_from_the_model()
    {
        GateEntities().Select(t => t.Name).Should().BeEquivalentTo(
            ["GateRunRow", "GateCellRow", "GateFindingRow", "GateVerdictRow", "GateReviewerRow", "GateArtifactRow"],
            "a scan that finds nothing passes forever — the tables come from the EF model, so a new one is walked when it is mapped");
    }

    [Fact]
    public void Every_allow_list_entry_names_a_column_that_exists_and_the_exception_is_one_of_them()
    {
        var carriers = TextSurface.Carriers(GateEntities()).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

        Allowed.Where(entry => !carriers.Contains(entry)).Should().BeEmpty(
            "an allow-list entry for a column that is gone is a door left open for the next one to take its name");
        carriers.Should().Contain(TheOneException);
        carriers.Where(c => c.EndsWith("Text", StringComparison.Ordinal) && !c.EndsWith("VersionText", StringComparison.Ordinal))
            .Should().Equal([TheOneException], "one free-text column, and it is the failure cause");
    }

    [Fact]
    public void A_planted_title_column_on_a_gate_row_is_named_by_type_and_column()
    {
        TextSurface.Offenders([typeof(PlantedFindingRow)], Allowed).Should().Equal(["PlantedFindingRow.Title (string)"]);
        TextSurface.Offenders([typeof(PlantedCellRow)], Allowed).Should().Equal(["PlantedCellRow.FailureText (string)"],
            "the exception is allowed on GateCellRow, not wherever the name FailureText turns up");
    }

    private static IReadOnlyList<Type> GateEntities()
    {
        using var db = new BenchDbContext(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql("Host=model-only").Options);

        return [.. PostgresGatePublicationSource.GateEntities(db).Select(e => e.ClrType)];
    }

    private sealed class PlantedFindingRow
    {
        public int Ordinal { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    private sealed class PlantedCellRow
    {
        public string FailureText { get; set; } = string.Empty;
    }
}
