namespace Bench.Cli;

/// <summary>The CLI's contract with whoever runs it — and the first consumer is an agent, not a human.
/// <para>
/// The distinction that earns its keep is between <see cref="Regression"/> and everything from
/// <see cref="Environment"/> upward: "the measurement ran and the answer is bad" must never be
/// confused with "the harness could not run". An agent that cannot tell those apart will confidently
/// report a regression when the real news is a missing checkout, and will keep doing it.
/// </para>
/// <para>Copied deliberately from the http-smoke contract, because a second vocabulary for the same
/// idea is a second thing to remember.</para></summary>
public static class ExitCodes
{
    /// <summary>The command did what it was asked and the verdict is good.</summary>
    public const int Pass = 0;

    /// <summary>A real regression: the measurement ran, and the numbers are worse than the bar.</summary>
    public const int Regression = 1;

    /// <summary>The environment is not ready — a missing checkout, an unreachable engine, no disk.</summary>
    public const int Environment = 3;

    /// <summary>The invocation or the configuration is wrong — a bad sha, an unset model, no suite.</summary>
    public const int Configuration = 4;

    /// <summary>The command completed without producing a report. Nothing was measured; do not read
    /// this as a pass, and do not read it as a failure of the subject.</summary>
    public const int NoReport = 5;
}
