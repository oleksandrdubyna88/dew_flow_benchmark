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

    /// <summary>The arm the engine ECHOED, as <c>host/provider/device</c>, or empty when it declared none.
    /// <para>
    /// Empty is the value of every run stored before this column existed and of every run against an engine
    /// that does not report one — which is all of them today. It reads back as <em>not declared</em> rather
    /// than parsing into an arm, because silence is not agreement: two sidecars must stay two rows, and an
    /// unattributed run must not join either one's aggregate.
    /// </para></summary>
    public string EngineBackend { get; set; } = string.Empty;

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

    /// <summary>Leg's two axes as their own columns. "Average by subject" and "pass rate per model" are
    /// group-bys or they are string parsing, and the metric-as-row decision already settled which of those
    /// this schema is for.</summary>
    public string SubjectModelId { get; set; } = string.Empty;

    public string LaneName { get; set; } = string.Empty;

    /// <summary>Which catalog variant this cell ran under. Absent — not an empty guid — for a cell planned
    /// before the catalog existed: "no variant was chosen" and "a variant nobody can look up" are
    /// different facts, and only one of them is true here.</summary>
    public Guid? VariantId { get; set; }

    public string VariantName { get; set; } = string.Empty;

    public int Position { get; set; }

    public CellState State { get; set; }

    public int Attempts { get; set; }

    /// <summary>The owner's LABEL — what a refusal quotes back at an operator. It is not the identity:
    /// two machines may honestly both call themselves "cli", which is why the two columns below exist.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>The machine and the process that hold the claim, so a sweep can ask whether the owner is
    /// still running instead of inferring it from elapsed time. Empty/zero on a row claimed before these
    /// columns existed — an owner nothing can vouch for, which the sweep treats as gone.</summary>
    public string OwnerHost { get; set; } = string.Empty;

    public int OwnerPid { get; set; }

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

    public DbSet<FunnelRow> Funnels => Set<FunnelRow>();

    public DbSet<RetrievedHitRow> RetrievedHits => Set<RetrievedHitRow>();

    public DbSet<PreparationRow> Preparations => Set<PreparationRow>();

    public DbSet<ToolTelemetryRow> ToolTelemetry => Set<ToolTelemetryRow>();

    public DbSet<VariantRow> Variants => Set<VariantRow>();

    public DbSet<LaneRow> Lanes => Set<LaneRow>();

    public DbSet<QuestionGroupRow> QuestionGroups => Set<QuestionGroupRow>();

    public DbSet<ReviewerRow> Reviewers => Set<ReviewerRow>();

    public DbSet<BankQuestionRow> BankQuestions => Set<BankQuestionRow>();

    public DbSet<QuestionReviewRow> QuestionReviews => Set<QuestionReviewRow>();

    public DbSet<QuestionGroupMoveRow> QuestionGroupMoves => Set<QuestionGroupMoveRow>();

    public DbSet<RunQuestionRow> RunQuestions => Set<RunQuestionRow>();

    public DbSet<ModelRow> Models => Set<ModelRow>();

    public DbSet<RunSubjectRow> RunSubjects => Set<RunSubjectRow>();

    public DbSet<RunJudgeRow> RunJudges => Set<RunJudgeRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        Bank(builder);
        Registry(builder);
        Retrieval(builder);

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
            result.Property(r => r.ResponseMetaJson).HasColumnType("jsonb");
            result.HasOne(r => r.Cell!).WithMany().HasForeignKey(r => r.CellId).OnDelete(DeleteBehavior.Cascade);
            result.HasMany(r => r.Metrics).WithOne(m => m.Result!).HasForeignKey(m => m.ResultId).OnDelete(DeleteBehavior.Cascade);
            result.HasMany(r => r.Hits).WithOne(h => h.Result!).HasForeignKey(h => h.ResultId).OnDelete(DeleteBehavior.Cascade);
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

            // The matrix page's question: how far along is each variant of this test. Without the index it
            // is a scan over every cell of a run that may hold tens of thousands.
            cell.HasIndex(c => new { c.RunId, c.VariantId, c.State });

            // Restrict, not cascade: deleting a variant that legs were measured under would delete the
            // measurements. A variant leaves the catalog by being RETIRED, which keeps every row resolvable.
            cell.HasOne<VariantRow>().WithMany()
                .HasForeignKey(c => c.VariantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VariantRow>(variant =>
        {
            variant.ToTable("variants");
            variant.HasKey(v => v.Id);
            variant.Property(v => v.DefinitionJson).HasColumnType("jsonb");

            // The name is an identity results quote, so uniqueness is enforced where concurrency actually
            // happens rather than by a read-then-write two sessions can both pass.
            variant.HasIndex(v => v.Name).IsUnique();

            // "Is this the same recipe under another name" — a lookup, never a scan that re-parses rows.
            variant.HasIndex(v => v.Hash);
        });

        builder.Entity<LaneRow>(lane =>
        {
            lane.ToTable("lanes");
            lane.HasKey(l => l.Id);
            lane.Property(l => l.DefinitionJson).HasColumnType("jsonb");

            // The name is the identity every cell already carries in RunCell.LaneName, so uniqueness is
            // enforced where concurrency actually happens rather than by a read-then-write two sessions
            // can both pass.
            lane.HasIndex(l => l.Name).IsUnique();

            // "Is this the same surface under another name" — the variant catalog's lookup, one axis over.
            lane.HasIndex(l => l.Hash);

            // The comparison scope: a leaderboard over wordings is only meaningful inside one tool set and
            // one presentation, so that pair is the index the reports group by.
            lane.HasIndex(l => new { l.ToolsHash, l.Presentation });
        });
    }

    /// <summary>What retrieval surfaced, per leg. The funnel is the white-box evidence and the hits are the
    /// list every retrieval metric recomputes from — which is why they are rows rather than a blob on the
    /// result.</summary>
    private static void Retrieval(ModelBuilder builder)
    {
        builder.Entity<FunnelRow>(funnel =>
        {
            funnel.ToTable("funnels");
            funnel.HasKey(f => f.Id);
            funnel.Property(f => f.StagesJson).HasColumnType("jsonb");
            funnel.Property(f => f.AbsentJson).HasColumnType("jsonb");
            funnel.Property(f => f.RequestedAxesJson).HasColumnType("jsonb");
            funnel.Property(f => f.AppliedAxesJson).HasColumnType("jsonb");

            // One funnel per leg, held where concurrency happens: a retry that raced its own earlier write
            // would otherwise leave two readings of one search and no way to say which served the answer.
            funnel.HasIndex(f => f.ResultId).IsUnique();

            // "How many legs degraded to black-box, and on which engine" — a lookup, not a scan.
            funnel.HasIndex(f => f.Degraded);

            funnel.HasOne(f => f.Result!).WithOne(r => r.Funnel!)
                .HasForeignKey<FunnelRow>(f => f.ResultId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PreparationRow>(preparation =>
        {
            preparation.ToTable("index_preparations");
            preparation.HasKey(p => p.Id);
            preparation.Property(p => p.State).HasConversion<string>();

            // The concurrency story of this whole table, and it is the DATABASE's rather than a read-then-write
            // above it: two runs that want the same index at the same moment must reach ONE row, or the matrix
            // holds two readiness answers for one collection with nothing able to say which is current.
            preparation.HasIndex(p => new { p.CommitSha, p.RecipeHash, p.EngineEndpoint }).IsUnique();

            // The sweep's own query — building rows by how long ago they were last heard from.
            preparation.HasIndex(p => new { p.State, p.Heartbeat });
        });

        builder.Entity<RetrievedHitRow>(hit =>
        {
            hit.ToTable("retrieved_hits");
            hit.HasKey(h => h.Id);
            hit.Property(h => h.ChannelsJson).HasColumnType("jsonb");
            hit.Property(h => h.RanksJson).HasColumnType("jsonb");

            // One rank per result: the list a search returned has no duplicate positions, and a retry that
            // half-wrote its hits must not be able to produce one.
            hit.HasIndex(h => new { h.ResultId, h.Rank }).IsUnique();

            // Retention's own query — the oldest rows that still hold text — over the largest table in the
            // system. Without this index the prune is a sequential scan of everything ever retrieved.
            hit.HasIndex(h => new { h.CreatedAt, h.SnippetPrunedAt });

            // "Which file did this engine keep surfacing" — the question a retrieval report asks first.
            hit.HasIndex(h => h.RelativePath);
        });
    }

    /// <summary>The question bank. Separate method because the model configuration is where this schema's
    /// guarantees actually live — the unique keys below are the ones a read-then-write above them cannot
    /// hold when two sessions import the same file at the same moment.</summary>
    private static void Bank(ModelBuilder builder)
    {
        builder.Entity<QuestionGroupRow>(group =>
        {
            group.ToTable("question_groups");
            group.HasKey(g => g.Id);
            group.HasIndex(g => g.Key).IsUnique();
        });

        builder.Entity<ReviewerRow>(reviewer =>
        {
            reviewer.ToTable("reviewers");
            reviewer.HasKey(r => r.Id);
            reviewer.HasIndex(r => r.Key).IsUnique();

            // Every vetting pass asks "which slots still take part" before it launches anything, exactly as the
            // model registry's list does.
            reviewer.HasIndex(r => r.Enabled);
        });

        builder.Entity<BankQuestionRow>(question =>
        {
            question.ToTable("bank_questions");
            question.HasKey(q => q.Id);
            question.Property(q => q.Kind).HasConversion<string>();
            question.Property(q => q.SourceKind).HasConversion<string>();
            question.Property(q => q.State).HasConversion<string>();
            question.Property(q => q.ExpectationsJson).HasColumnType("jsonb");

            // The suite-facing identity every cell and every result carries. Unique across the BANK, not
            // per group: a question keeps its id when it is re-filed, and a report joins on it.
            question.HasIndex(q => q.QuestionId).IsUnique();

            // How a selection reads: one group, an ordinal range. The operator quotes exactly that.
            question.HasIndex(q => new { q.GroupId, q.Ordinal });
            question.HasIndex(q => q.State);

            // Restrict: deleting a group that questions live in would orphan them. A group is emptied by
            // moving its questions, which leaves the history row that explains where they went.
            question.HasOne(q => q.Group!).WithMany()
                .HasForeignKey(q => q.GroupId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuestionReviewRow>(review =>
        {
            review.ToTable("question_reviews");
            review.HasKey(r => r.Id);
            review.Property(r => r.Verdict).HasConversion<string>();

            // One mark per reviewer per question, held where concurrency happens. Without it, two sessions
            // marking the same question would both find nothing and both insert, and the page would render
            // one reviewer twice with opposite verdicts.
            review.HasIndex(r => new { r.QuestionId, r.ReviewerId }).IsUnique();

            review.HasOne<BankQuestionRow>().WithMany()
                .HasForeignKey(r => r.QuestionId).OnDelete(DeleteBehavior.Cascade);
            review.HasOne<ReviewerRow>().WithMany()
                .HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuestionGroupMoveRow>(move =>
        {
            move.ToTable("question_group_moves");
            move.HasKey(m => m.Id);
            move.HasIndex(m => new { m.QuestionId, m.At });
            move.HasOne<BankQuestionRow>().WithMany()
                .HasForeignKey(m => m.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        Snapshot(builder);
    }

    /// <summary>The model registry and the roles a test chose from it. The choices live on the RUN, so a
    /// registry edit next month cannot change what a finished test says it measured.</summary>
    private static void Registry(ModelBuilder builder)
    {
        builder.Entity<ModelRow>(model =>
        {
            model.ToTable("models");
            model.HasKey(m => m.Id);
            model.Property(m => m.Runtime).HasConversion<string>();
            model.Property(m => m.Hosting).HasConversion<string>();
            model.Property(m => m.ConfigJson).HasColumnType("jsonb");

            // The key is the identity every role names, so uniqueness is held where concurrency happens.
            model.HasIndex(m => m.Key).IsUnique();
            model.HasIndex(m => m.Enabled);
        });

        builder.Entity<RunSubjectRow>(subject =>
        {
            subject.ToTable("run_subjects");
            subject.HasKey(s => new { s.RunId, s.ModelKey });
            subject.HasOne(s => s.Run!).WithMany()
                .HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RunJudgeRow>(judge =>
        {
            judge.ToTable("run_judges");
            judge.HasKey(j => new { j.RunId, j.ModelKey });
            judge.HasIndex(j => new { j.RunId, j.Ordinal });
            judge.HasOne(j => j.Run!).WithMany()
                .HasForeignKey(j => j.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        // No foreign key from either role table to `models`, deliberately: a role names a KEY, and a run
        // must stay readable even for a subject added ad hoc from the command line without a registry row.
    }

    private static void Snapshot(ModelBuilder builder)
    {
        builder.Entity<RunQuestionRow>(selected =>
        {
            selected.ToTable("run_questions");

            // The pair IS the row: one question appears once in a test's selection, and the composite key
            // is what makes a second write of the same snapshot a conflict rather than a duplicate.
            selected.HasKey(q => new { q.RunId, q.QuestionId });
            selected.HasIndex(q => new { q.RunId, q.GroupKey });
            selected.HasOne(q => q.Run!).WithMany()
                .HasForeignKey(q => q.RunId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
