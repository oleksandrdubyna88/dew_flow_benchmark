using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The mapping of the two <c>probe_*</c> tables — a context of its own inside the one database, beside the gate's and
/// the retrieval benchmark's, touching none of them. Every enum is stored as its NAME, the rule every table here follows.</summary>
internal static class ProbeModel
{
    public const string TablePrefix = "probe_";

    /// <summary>The one probe column that holds a public vendor url by design — the api subject's endpoint (S2) — checked by the
    /// guard's endpoint rule, as <see cref="GateModel.PublicUrlColumns"/> is; a CLI subject's entry there is empty.</summary>
    public static IReadOnlySet<string> PublicUrlColumns { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "probe_runs.SubjectEndpoints" };

    public static void Configure(ModelBuilder builder)
    {
        Runs(builder);
        Cells(builder);
    }

    private static void Runs(ModelBuilder builder) =>
        builder.Entity<ProbeRunRow>(run =>
        {
            run.ToTable("probe_runs");
            run.HasKey(r => r.Id);
            run.Property(r => r.OracleSource).HasConversion<string>();
            run.HasIndex(r => r.CreatedAt);
            run.HasMany(r => r.Cells).WithOne(c => c.Run!).HasForeignKey(c => c.RunId).OnDelete(DeleteBehavior.Cascade);
        });

    private static void Cells(ModelBuilder builder) =>
        builder.Entity<ProbeCellRow>(cell =>
        {
            cell.ToTable("probe_cells");
            cell.HasKey(c => c.Id);
            cell.Property(c => c.Probe).HasConversion<string>();
            cell.Property(c => c.State).HasConversion<string>();
            cell.Property(c => c.AttemptKind).HasConversion<string>();
            cell.Property(c => c.CanaryRead).HasConversion<string>();
            cell.Property(c => c.ReadAttempted).HasConversion<string>();
            cell.Property(c => c.AnswerCurrent).HasConversion<string>();
            cell.Property(c => c.ToolEvidence).HasConversion<string>();
            cell.Property(c => c.Reachable).HasConversion<string>();
            cell.Property(c => c.AccountOut).HasConversion<string>();
            cell.Property(c => c.Reason).HasConversion<string>();

            // One row per (run, probe, subject, repeat, GENERATION): a re-run appends a generation and never rewrites one (D2),
            // and two re-runs racing for the same number reach one row — the loser's insert fails.
            cell.HasIndex(c => new { c.RunId, c.Probe, c.SubjectId, c.Repeat, c.Generation }).IsUnique();

            // The claim takes a SUBJECT's pending cells in matrix order; the sweep scans claimed ones by age. The two hot paths.
            cell.HasIndex(c => new { c.RunId, c.SubjectId, c.State, c.Slot, c.Position });
            cell.HasIndex(c => new { c.State, c.ClaimedAt });
        });
}
