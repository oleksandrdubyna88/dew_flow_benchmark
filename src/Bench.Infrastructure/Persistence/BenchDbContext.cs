using Bench.Domain.Runs;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>A run, flattened for storage. The measurement target and the engine live here as columns
/// rather than as a foreign key to "current configuration": what a run measured must be readable from the
/// run itself, years later, without trusting whatever the settings say by then.</summary>
public sealed class RunRow
{
    public Guid Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public string RepoUrl { get; set; } = string.Empty;

    public string CommitSha { get; set; } = string.Empty;

    public List<string> Exclusions { get; set; } = [];

    public EngineKind EngineKind { get; set; }

    public string EngineEndpoint { get; set; } = string.Empty;

    public string EngineVersion { get; set; } = string.Empty;

    public string IndexFingerprint { get; set; } = string.Empty;

    public string SuiteStamp { get; set; } = string.Empty;

    public RunStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<CellRow> Cells { get; set; } = [];
}

public sealed class CellRow
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public string QuestionId { get; set; } = string.Empty;

    public int Repeat { get; set; }

    public string Leg { get; set; } = string.Empty;

    public int Position { get; set; }

    public CellState State { get; set; }

    public int Attempts { get; set; }

    public string Owner { get; set; } = string.Empty;

    public DateTimeOffset ClaimedAt { get; set; }

    public LegOutcomeKind OutcomeKind { get; set; }

    public string OutcomeDetail { get; set; } = string.Empty;

    public RunRow? Run { get; set; }
}

public sealed class BenchDbContext(DbContextOptions<BenchDbContext> options) : DbContext(options)
{
    public DbSet<RunRow> Runs => Set<RunRow>();

    public DbSet<CellRow> Cells => Set<CellRow>();

    public DbSet<ResultRow> Results => Set<ResultRow>();

    public DbSet<MetricRow> Metrics => Set<MetricRow>();

    public DbSet<ToolTelemetryRow> ToolTelemetry => Set<ToolTelemetryRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ToolTelemetryRow>(telemetry =>
        {
            telemetry.ToTable("tool_telemetry");
            telemetry.HasKey(t => t.Id);
            telemetry.Property(t => t.Outcome).HasConversion<string>();

            // The idempotency guard, in the DATABASE rather than in a read-then-write: two ingests
            // racing over one spool would both find nothing and both insert. A unique index makes the
            // second one a conflict the adapter can absorb.
            telemetry.HasIndex(t => t.Fingerprint).IsUnique();

            // The report groups by (tool, caller) and windows by time; neither should ever become a
            // sequential scan over the largest table in the system.
            telemetry.HasIndex(t => new { t.Tool, t.CallerKey });
            telemetry.HasIndex(t => t.At);

            // Splitting server time and tokens across a leg's phases is the join this vantage point
            // could not otherwise make; it must not be a scan over the largest table in the system.
            telemetry.HasIndex(t => new { t.Leg, t.Phase });
        });

        builder.Entity<ResultRow>(result =>
        {
            result.ToTable("results");
            result.HasKey(r => r.Id);
            result.HasIndex(r => r.CellId).IsUnique();
            result.HasOne(r => r.Cell!).WithMany().HasForeignKey(r => r.CellId).OnDelete(DeleteBehavior.Cascade);
            result.HasMany(r => r.Metrics).WithOne(m => m.Result!).HasForeignKey(m => m.ResultId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MetricRow>(metric =>
        {
            metric.ToTable("metrics");
            metric.HasKey(m => m.Id);
            metric.Property(m => m.Kind).HasConversion<string>();
            metric.Property(m => m.MetadataJson).HasColumnType("jsonb");

            // The aggregate this schema exists for: one metric across a run, grouped by a dimension of
            // the measurement key. An index on the name keeps that a lookup rather than a scan.
            metric.HasIndex(m => new { m.ResultId, m.Name });
            metric.HasIndex(m => m.Name);
        });

        builder.Entity<RunRow>(run =>
        {
            run.ToTable("runs");
            run.HasKey(r => r.Id);
            run.Property(r => r.Status).HasConversion<string>();
            run.Property(r => r.EngineKind).HasConversion<string>();
            run.HasMany(r => r.Cells).WithOne(c => c.Run!).HasForeignKey(c => c.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CellRow>(cell =>
        {
            cell.ToTable("cells");
            cell.HasKey(c => c.Id);
            cell.Property(c => c.State).HasConversion<string>();
            cell.Property(c => c.OutcomeKind).HasConversion<string>();

            // The claim query orders by (Position, QuestionId, Repeat) within one run's pending cells, and
            // the sweep scans claimed ones by age. Both are the hot paths of a run with tens of thousands
            // of cells, and neither should ever become a sequential scan.
            cell.HasIndex(c => new { c.RunId, c.State, c.Position });
            cell.HasIndex(c => new { c.State, c.ClaimedAt });
        });
    }
}
