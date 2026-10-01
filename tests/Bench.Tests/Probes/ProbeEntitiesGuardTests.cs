using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>The publication guard, structural first, over the probe store — <see cref="GateEntitiesGuardTests"/>' walk
/// (<see cref="TextSurface"/>) over the <c>probe_*</c> entities the EF model maps. Every property that can carry text is named
/// here as <c>Type.Property</c>; a new column is red until it is. Unlike the gate, the probes have NO free-text column: the one
/// "why" a row carries is an allow-listed word (D11).</summary>
public sealed class ProbeEntitiesGuardTests
{
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        // probe_runs — the oracle's semver, and the frozen subjects as four parallel lists: slug ids, runtime enum NAMES, model
        // ids (the string a runtime is asked for, never a url or a path — ProbeSubject refuses one), and environment variable
        // NAMES for the executables (D4; a path or a key is refused by ProbeSubject).
        "ProbeRunRow.OracleVersion", "ProbeRunRow.SubjectIds", "ProbeRunRow.SubjectRuntimes", "ProbeRunRow.SubjectModels", "ProbeRunRow.SubjectExecutableRefs",

        // probe_cells — a slug id, the claim's owner (a label, a host name — never published, GatePublication.ClaimOwnerColumns),
        // the pin (hashes, the --version text, the tree the dirty check covered), and the artefacts as kind names, paths RELATIVE
        // to the artefact root (ArtifactPath: never rooted, never climbing) and hashes.
        "ProbeCellRow.SubjectId", "ProbeCellRow.Owner", "ProbeCellRow.OwnerHost",
        "ProbeCellRow.PinBinarySha256", "ProbeCellRow.PinVersionText", "ProbeCellRow.PinGitSha", "ProbeCellRow.PinCheckedTree",
        "ProbeCellRow.ArtifactKinds", "ProbeCellRow.ArtifactPaths", "ProbeCellRow.ArtifactSha256s",
    };

    [Fact]
    public void Every_text_bearing_column_of_every_probe_table_is_allowed_by_type_and_name()
    {
        TextSurface.Offenders(ProbeEntities(), Allowed).Should().BeEmpty(
            "a text column that is not an id, a hash, an enum name, a reference or a relative path could hold an answer or a transcript "
            + "in a table that is published; name it here, as Type.Property, only if it provably cannot");
    }

    [Fact]
    public void The_walk_covers_both_probe_tables_from_the_model()
    {
        ProbeEntities().Select(t => t.Name).Should().BeEquivalentTo(["ProbeRunRow", "ProbeCellRow"],
            "a scan that finds nothing passes forever — the tables come from the EF model, so a new one is walked when it is mapped");
    }

    [Fact]
    public void Every_allow_list_entry_names_a_column_that_exists_and_no_probe_column_is_free_text()
    {
        var carriers = TextSurface.Carriers(ProbeEntities()).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

        Allowed.Where(entry => !carriers.Contains(entry)).Should().BeEmpty("an allow-list entry for a column that is gone is a door left open");
        carriers.Where(c => c.EndsWith("Text", StringComparison.Ordinal) && !c.EndsWith("VersionText", StringComparison.Ordinal))
            .Should().BeEmpty("the probes store no sentence — the reason is an allow-listed word, and the answers live in the artefact root");
    }

    [Fact]
    public void The_gate_walk_still_sees_its_own_nine_tables_only()
    {
        using var db = Context();

        PostgresGatePublicationSource.GateEntities(db).Should().HaveCount(9, "the probe tables join the EXPORT walk, not the gate's own list");
        PostgresGatePublicationSource.PublishedEntities(db).Select(e => e.GetTableName()).Should().EndWith(["probe_cells", "probe_runs"]);
    }

    private static IReadOnlyList<Type> ProbeEntities()
    {
        using var db = Context();

        return [.. PostgresGatePublicationSource.ProbeEntities(db).Select(e => e.ClrType)];
    }

    private static BenchDbContext Context() => new(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql("Host=model-only").Options);
}
