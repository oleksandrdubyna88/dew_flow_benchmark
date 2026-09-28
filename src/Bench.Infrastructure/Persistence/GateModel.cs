using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The mapping of the eight <c>gate_*</c> tables — a context of its own inside the one database, beside
/// the retrieval benchmark's tables and touching none of them. Every enum is stored as its NAME, the rule every
/// other table here follows: an ordinal changes meaning the day somebody inserts a member.</summary>
internal static class GateModel
{
    /// <summary>Every <c>table.column</c> that holds a public vendor url by design, checked by the endpoint rule
    /// rather than the plain <c>://</c> rule. One entry, and it is named here so the guard and the mapping agree.</summary>
    public static IReadOnlySet<string> PublicUrlColumns { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "gate_reviewers.EndpointUrl" };

    public const string TablePrefix = "gate_";

    public static void Configure(ModelBuilder builder)
    {
        Runs(builder);
        Cells(builder);
        Findings(builder);
        Verdicts(builder);
        Reviewers(builder);
        Artifacts(builder);
        HandChecks(builder);
        Summaries(builder);
    }

    private static void Summaries(ModelBuilder builder) =>
        builder.Entity<GateSummaryRow>(summary =>
        {
            summary.ToTable("gate_summaries");
            summary.HasKey(s => s.Id);
            summary.Property(s => s.Gate).HasConversion<string>();

            // A number of a document is stored once per (document bytes, section, row, metric): a re-import is a no-op, and
            // an edited document is a new citation beside the old one, never a silent overwrite.
            summary.HasIndex(s => new { s.DocumentSha256, s.Section, s.RowOrdinal, s.Metric }).IsUnique();
        });

    private static void Runs(ModelBuilder builder) =>
        builder.Entity<GateRunRow>(run =>
        {
            run.ToTable("gate_runs");
            run.HasKey(r => r.Id);
            run.Property(r => r.Gate).HasConversion<string>();
            run.Property(r => r.DataDirMode).HasConversion<string>();
            run.Property(r => r.Status).HasConversion<string>();
            run.HasIndex(r => r.CreatedAt);
            run.HasMany(r => r.Cells).WithOne(c => c.Run!).HasForeignKey(c => c.RunId).OnDelete(DeleteBehavior.Cascade);
        });

    private static void Cells(ModelBuilder builder) =>
        builder.Entity<GateCellRow>(cell =>
        {
            cell.ToTable("gate_cells");
            cell.HasKey(c => c.Id);
            cell.Property(c => c.State).HasConversion<string>();
            cell.Property(c => c.OutcomeKind).HasConversion<string>();
            cell.Property(c => c.Verdict).HasConversion<string>();
            cell.Property(c => c.FailureKind).HasConversion<string>();

            // The claim orders a run's pending cells by position; the sweep scans claimed ones by age. The two hot
            // paths, and neither may become a scan over a campaign's every cell.
            cell.HasIndex(c => new { c.RunId, c.State, c.Position });
            cell.HasIndex(c => new { c.State, c.ClaimedAt });
        });

    private static void Findings(ModelBuilder builder) =>
        builder.Entity<GateFindingRow>(finding =>
        {
            finding.ToTable("gate_findings");
            finding.HasKey(f => f.Id);
            finding.Property(f => f.Severity).HasConversion<string>();
            finding.Property(f => f.Category).HasConversion<string>();

            // One row per (session, ordinal): a finding is its position in the reply, and two rows for one
            // position would make every count on the page ambiguous.
            finding.HasIndex(f => new { f.CellId, f.Attempt, f.Ordinal }).IsUnique();
            finding.HasOne<GateCellRow>().WithMany().HasForeignKey(f => f.CellId).OnDelete(DeleteBehavior.Cascade);
        });

    private static void Verdicts(ModelBuilder builder) =>
        builder.Entity<GateVerdictRow>(verdict =>
        {
            verdict.ToTable("gate_verdicts");
            verdict.HasKey(v => v.Id);
            verdict.Property(v => v.RubricKind).HasConversion<string>();
            verdict.Property(v => v.Reading).HasConversion<string>();
            verdict.Property(v => v.Value).HasConversion<string>();
            verdict.Property(v => v.SeverityFair).HasConversion<string>();
            verdict.Property(v => v.Grounded).HasConversion<string>();
            verdict.Property(v => v.FailureCause).HasConversion<string>();
            verdict.HasIndex(v => new { v.CellId, v.FindingOrdinal, v.RubricHash });

            // A verdict is written once per (finding, rubric, assessor, batch): a replay of a batch after a crash between
            // the verdict log and the database changes nothing (E4).
            verdict.HasIndex(v => new { v.CellId, v.FindingOrdinal, v.RubricHash, v.AssessorId, v.BatchId }).IsUnique();
            verdict.HasOne<GateCellRow>().WithMany().HasForeignKey(v => v.CellId).OnDelete(DeleteBehavior.Cascade);
        });

    private static void Reviewers(ModelBuilder builder) =>
        builder.Entity<GateReviewerRow>(reviewer =>
        {
            reviewer.ToTable("gate_reviewers");
            reviewer.HasKey(r => r.Id);
            reviewer.Property(r => r.Runtime).HasConversion<string>();

            // "Is this the same configuration under another name" — a lookup, never a re-hash of every row.
            reviewer.HasIndex(r => r.Hash);
        });

    private static void HandChecks(ModelBuilder builder) =>
        builder.Entity<GateHandCheckRow>(check =>
        {
            check.ToTable("gate_hand_checks");
            check.HasKey(c => c.Id);
            check.Property(c => c.RubricKind).HasConversion<string>();
            check.HasIndex(c => new { c.RubricHash, c.AssessorId });
        });

    private static void Artifacts(ModelBuilder builder) =>
        builder.Entity<GateArtifactRow>(artifact =>
        {
            artifact.ToTable("gate_artifacts");
            artifact.HasKey(a => a.Id);
            artifact.Property(a => a.Class).HasConversion<string>();

            // An artefact is written ONCE, under its own attempt: a second row for one path would be two answers
            // to "what bytes were committed here".
            artifact.HasIndex(a => a.RelativePath).IsUnique();
            artifact.HasIndex(a => new { a.RunId, a.CellId, a.Attempt });
            artifact.HasOne<GateRunRow>().WithMany().HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);
        });
}
