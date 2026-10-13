using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The mapping of the five <c>scout_*</c> tables — a context of its own inside the one database, beside the gate's and the
/// probes', touching none of them. Every enum is stored as its NAME; money is <c>numeric(18,6)</c>.</summary>
internal static class ScoutModel
{
    public const string TablePrefix = "scout_";

    /// <summary>No scout column holds a url by design.</summary>
    public static IReadOnlySet<string> PublicUrlColumns { get; } = new HashSet<string>(StringComparer.Ordinal);

    private const int MoneyPrecision = 18;

    private const int MoneyScale = 6;

    public static void Configure(ModelBuilder builder)
    {
        Runs(builder);
        Tasks(builder);
        Cells(builder);
        Records(builder);
        Attempts(builder);
        Claims(builder);
    }

    private static void Runs(ModelBuilder builder) =>
        builder.Entity<ScoutRunRow>(run =>
        {
            run.ToTable("scout_runs");
            run.HasKey(r => r.Id);
            run.Property(r => r.Kind).HasConversion<string>();
            run.Property(r => r.CeilingUsd).HasPrecision(MoneyPrecision, MoneyScale);
            run.Property(r => r.ReservedUsd).HasPrecision(MoneyPrecision, MoneyScale);
            run.Property(r => r.SettledUsd).HasPrecision(MoneyPrecision, MoneyScale);
            run.Property(r => r.UnknownUsd).HasPrecision(MoneyPrecision, MoneyScale);
            run.HasIndex(r => r.CreatedAt);
            run.HasMany(r => r.Tasks).WithOne(t => t.Run!).HasForeignKey(t => t.RunId).OnDelete(DeleteBehavior.Cascade);
            run.HasMany(r => r.Cells).WithOne(c => c.Run!).HasForeignKey(c => c.RunId).OnDelete(DeleteBehavior.Cascade);
        });

    private static void Tasks(ModelBuilder builder) =>
        builder.Entity<ScoutTaskRow>(task =>
        {
            task.ToTable("scout_tasks");
            task.HasKey(t => t.Id);
            task.HasIndex(t => new { t.RunId, t.TaskId }).IsUnique();
            task.Property(t => t.Kind).HasConversion<string>();
            task.Property(t => t.Privacy).HasConversion<string>();
        });

    private static void Cells(ModelBuilder builder) =>
        builder.Entity<ScoutCellRow>(cell =>
        {
            cell.ToTable("scout_cells");
            cell.HasKey(c => c.Id);
            cell.Property(c => c.Arm).HasConversion<string>();
            cell.Property(c => c.State).HasConversion<string>();
            cell.Property(c => c.Outcome).HasConversion<string>();
            cell.Property(c => c.Reason).HasConversion<string>();
            cell.Property(c => c.ComputedUsd).HasPrecision(MoneyPrecision, MoneyScale);
            cell.Property(c => c.ReportedUsd).HasPrecision(MoneyPrecision, MoneyScale);
            cell.Property(c => c.TeamUsd).HasPrecision(MoneyPrecision, MoneyScale);

            // One row per (run, task, arm, repeat, GENERATION): a re-run appends, and two re-runs racing for a number reach one row.
            cell.HasIndex(c => new { c.RunId, c.TaskId, c.Arm, c.Repeat, c.Generation }).IsUnique();

            // The claim takes the run's pending cells in schedule order; the sweep scans claimed ones by age.
            cell.HasIndex(c => new { c.RunId, c.State, c.Slot, c.Position });
            cell.HasIndex(c => new { c.State, c.ClaimedAt });
            cell.HasMany(c => c.Scouts).WithOne(s => s.Cell!).HasForeignKey(s => s.CellId).OnDelete(DeleteBehavior.Cascade);
        });

    private static void Records(ModelBuilder builder) =>
        builder.Entity<ScoutRecordRow>(scout =>
        {
            scout.ToTable("scout_records");
            scout.HasKey(s => s.Id);
            scout.Property(s => s.Focus).HasConversion<string>();
            scout.Property(s => s.CostUsd).HasPrecision(MoneyPrecision, MoneyScale);
            scout.HasIndex(s => new { s.CellId, s.Ordinal }).IsUnique();
        });

    private static void Attempts(ModelBuilder builder) =>
        builder.Entity<ScoutAttemptRow>(attempt =>
        {
            attempt.ToTable("scout_attempts");
            attempt.HasKey(a => a.Id);
            attempt.Property(a => a.State).HasConversion<string>();
            attempt.Property(a => a.ReservedUsd).HasPrecision(MoneyPrecision, MoneyScale);
            attempt.Property(a => a.SettledUsd).HasPrecision(MoneyPrecision, MoneyScale);

            // One reservation per claim attempt of a cell: a second reserve for the same attempt is the unique index's to refuse.
            attempt.HasIndex(a => new { a.CellId, a.Number }).IsUnique();
            attempt.HasIndex(a => new { a.RunId, a.State });
            attempt.HasOne<ScoutRunRow>().WithMany().HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);
            attempt.HasOne<ScoutCellRow>().WithMany().HasForeignKey(a => a.CellId).OnDelete(DeleteBehavior.Cascade);
        });

    private static void Claims(ModelBuilder builder) =>
        builder.Entity<ScoutClaimRow>(claim =>
        {
            claim.ToTable("scout_claims");
            claim.HasKey(c => c.Id);
            claim.Property(c => c.Verdict).HasConversion<string>();

            // One verdict per claim: a second assess of an assessed claim is the unique index's to refuse, never an overwrite.
            claim.HasIndex(c => new { c.CellId, c.Ordinal, c.Index }).IsUnique();
            claim.HasIndex(c => c.RunId);
            claim.HasOne<ScoutRunRow>().WithMany().HasForeignKey(c => c.RunId).OnDelete(DeleteBehavior.Cascade);
            claim.HasOne<ScoutCellRow>().WithMany().HasForeignKey(c => c.CellId).OnDelete(DeleteBehavior.Cascade);
        });
}
