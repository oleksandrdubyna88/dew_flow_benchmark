using Bench.Domain.Runs;

namespace Bench.Infrastructure.Persistence;

/// <summary>What one leg produced. Joined to its cell — and through it to the run — rather than
/// duplicating the measurement key, so there is exactly one place a result's target and engine come from.</summary>
public sealed class ResultRow
{
    public Guid Id { get; set; }

    public Guid CellId { get; set; }

    public string Prompt { get; set; } = string.Empty;

    /// <summary>The subject's answer, stored so a second arbiter costs its own inference and nothing else.</summary>
    public string Answer { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<MetricRow> Metrics { get; set; } = [];

    public CellRow? Cell { get; set; }
}

/// <summary>One metric, as a ROW.
/// <para>
/// This is the whole reason storage is ours rather than the library's disk store. As rows, "the average
/// anchor recall per engine on this run" is a group-by. As a JSON blob — or as a composite directory name,
/// which is what the disk store's key really is — it is a full scan that parses strings back into
/// structure, and this project refuses that trade everywhere it appears.
/// </para></summary>
public sealed class MetricRow
{
    public Guid Id { get; set; }

    public Guid ResultId { get; set; }

    public string Name { get; set; } = string.Empty;

    public MetricKind Kind { get; set; }

    public string Value { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public bool Failed { get; set; }

    /// <summary>Stored as the rating's NAME. An enum kept as its ordinal changes meaning the day somebody
    /// inserts a member, and old results staying comparable is the point of this system.</summary>
    public string Rating { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public ResultRow? Result { get; set; }
}
