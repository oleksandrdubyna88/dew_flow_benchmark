using Bench.Domain.Registry;
using Bench.Domain.Runs;

namespace Bench.Infrastructure.Persistence;

/// <summary>One model of the registry.
/// <para>
/// <see cref="ConfigJson"/> holds REFERENCES, never values — an environment variable's name, not the
/// endpoint or the key it names. This table sits in the database this project promises to publish
/// unedited, and a guarantee scoped to result rows while the registry sits in the same schema is not a
/// guarantee but a redaction pass nobody has scheduled.
/// </para></summary>
public sealed class ModelRow
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public ModelRuntimeKind Runtime { get; set; }

    public ModelHosting Hosting { get; set; }

    public string ConfigJson { get; set; } = "{}";

    /// <summary>Disabled, never deleted: a run names the key it measured under, and a removed row would
    /// make a finished test's subject unreadable.</summary>
    public bool Enabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A test's answering models. Add-only — removing one would leave its settled cells naming a
/// subject the test no longer admits.</summary>
public sealed class RunSubjectRow
{
    public Guid RunId { get; set; }

    public string ModelKey { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; set; }

    public RunRow? Run { get; set; }
}

/// <summary>A test's arbiters, ORDERED: the first is the primary. Without the ordinal, "the primary
/// arbiter disagreed" is a sentence nobody can evaluate.</summary>
public sealed class RunJudgeRow
{
    public Guid RunId { get; set; }

    public string ModelKey { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public RunRow? Run { get; set; }
}
