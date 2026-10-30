namespace ModelArms.Core.Steps;

/// <summary>How one attempt of a step ended (plan C5, D6, D8): a closed set the runner switches on. Only <see cref="Finished"/> and <see cref="HardDeadline"/> are
/// done; a limit, an error and an interruption are never done — an error result is never "done" (the 2026-10-28 incident).</summary>
public abstract record StepEnd
{
    private StepEnd()
    {
    }

    /// <summary>The attempt printed a result that is not an error.</summary>
    public sealed record Finished : StepEnd;

    /// <summary>No result, and the wall killed the tree.</summary>
    public sealed record HardDeadline : StepEnd;

    /// <summary>The account is out: a spend or usage limit, a 429 or a 529. The reason names what decided it.</summary>
    public sealed record Limited(string Reason) : StepEnd;

    /// <summary>An error result that is not a limit; <paramref name="Text"/> is the result's first 200 characters.</summary>
    public sealed record Errored(string Text) : StepEnd;

    /// <summary>No result and no limit: the process ended without saying how, or the operator stopped it.</summary>
    public sealed record Interrupted(string Why) : StepEnd;

    /// <summary>The launch itself was refused — the executable was missing, say — so nothing ran.</summary>
    public sealed record NotStarted(string Reason) : StepEnd;

    /// <summary>True for <see cref="Finished"/> and <see cref="HardDeadline"/>.</summary>
    public bool IsDone => this is Finished or HardDeadline;

    /// <summary>The spelling of <c>.done</c> and of the end line: <c>finished</c>, <c>hard-deadline</c>, <c>limited</c>, <c>errored</c>, <c>interrupted</c>, <c>not-started</c>.</summary>
    public string Word =>
        this switch
        {
            Finished => "finished",
            HardDeadline => "hard-deadline",
            Limited => "limited",
            Errored => "errored",
            Interrupted => "interrupted",
            NotStarted => "not-started",
            _ => throw new InvalidOperationException("unreachable"),
        };
}

/// <summary>What the launcher knew of one attempt besides its streams: why it did not start (empty when it did), whether the wall killed the tree and whether the operator
/// stopped it.</summary>
public sealed record AttemptFacts(string NotStartedReason, bool WallKilled, bool StoppedByOperator)
{
    /// <summary>An attempt that ran and was neither killed nor stopped — what recovery assumes of a stream read back from disk.</summary>
    public static AttemptFacts Ran { get; } = new(string.Empty, false, false);

    /// <summary>True when the launch was not refused.</summary>
    public bool Started => NotStartedReason.Length == 0;
}
