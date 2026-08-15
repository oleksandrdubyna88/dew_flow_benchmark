using Bench.Domain.Targets;

namespace Bench.Domain.Runs;

public enum RunStatus
{
    /// <summary>Written, with every cell, and not yet started.</summary>
    Planned,

    Running,

    /// <summary>Every cell reached a terminal state. Not the same as "every cell succeeded".</summary>
    Completed,
}

/// <summary>One execution of one suite against one target through one engine.
/// <para>
/// The target, the engine and the suite stamp are stored ON the run rather than looked up later, because
/// they are what makes its numbers comparable to anything else — and a lookup can be answered by whatever
/// the configuration happens to say today. A run that cannot prove what it measured is a run whose numbers
/// quietly become a hypothesis again.
/// </para></summary>
public sealed record BenchRun(
    Guid Id,
    string Label,
    MeasurementTarget Target,
    EngineRef Engine,
    string SuiteStamp,
    RunStatus Status,
    DateTimeOffset CreatedAt)
{
    public static BenchRun Planned(string label, MeasurementTarget target, EngineRef engine, string suiteStamp, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), label, target, engine, suiteStamp, RunStatus.Planned, now);

    /// <summary>The scope this run's rows may be compared within — the target and the suite, never the
    /// engine or the model, which are the axes you compare ALONG.</summary>
    public ComparisonScope Scope => new(Target.Canonical, SuiteStamp);
}

/// <summary>How far a run has got. Abandoned is reported separately and never folded into settled:
/// a cell nobody could finish is not a measurement, and averaging it in as a zero invents data.</summary>
public readonly record struct RunProgress(int Pending, int Claimed, int Settled, int Abandoned)
{
    public int Total => Pending + Claimed + Settled + Abandoned;

    public bool IsFinished => Pending == 0 && Claimed == 0 && Total > 0;

    public string Describe =>
        $"{Settled} settled · {Claimed} in flight · {Pending} pending" + (Abandoned > 0 ? $" · {Abandoned} ABANDONED" : "");
}
