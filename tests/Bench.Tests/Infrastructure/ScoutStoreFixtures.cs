using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Domain.Trace;

namespace Bench.Tests.Infrastructure;

/// <summary>The shapes the scout store tests build again and again — written once, beside <see cref="ProbeStoreFixtures"/>.</summary>
internal static class ScoutStoreFixtures
{
    public static readonly DateTimeOffset Noon = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    public static ScoutPins Pins() => ScoutPins.Of("2.1.289", "f359d43b4060", "claude-opus-5-5", "claude-sonnet-5-5", new string('9', 64)).Ok();

    public static ProductPin Pin(char letter = 'a') =>
        ProductPin.Hashed(new string(letter, 64), "2.1.289 (Claude Code)", string.Empty, CapturedCount.Unavailable("no checkout"), string.Empty).Ok();

    public static ScoutTask FrozenTask(string id) =>
        ScoutTask.Frozen(
            ScoutTaskId.Parse(id).Ok(), "example-org/example-repo", CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.HowEndToEnd, "How does a cell reach its settlement after a crash?", ["src/Store.cs", "src/Sweep.cs"], [], ["todo/PLAN_x.md"], new string('c', 64)).Ok();

    /// <summary>A planned run of <paramref name="tasks"/> tasks × <paramref name="arms"/>, one repeat, under a ceiling, through the real schedule.</summary>
    public static (ScoutRun Run, IReadOnlyList<ScoutTask> Tasks, IReadOnlyList<ScoutCell> Cells) Planned(
        int tasks = 1, ScoutArmId[]? arms = null, decimal ceilingUsd = 10m, int repeats = 1)
    {
        var frozen = Enumerable.Range(1, tasks).Select(i => FrozenTask($"t{i:00}")).ToList();
        var armIds = arms ?? [ScoutArmId.A1, ScoutArmId.B1];
        var run = ScoutRun.Planned(Guid.CreateVersion7(), Noon, Pins(), seed: 7, repeats, armIds, [.. frozen.Select(t => t.Id)], ceilingUsd).Ok();
        var schedule = ScoutSchedule.Plan([.. frozen.Select(t => t.Id)], armIds, [], repeats, seed: 7).Ok();

        return (run, frozen, [.. schedule.Select(c => ScoutCell.Pending(Guid.CreateVersion7(), run.Id, c))]);
    }

    public static ScoutSettlement Scored() => new(
        ScoutCellOutcome.Scored,
        ScoutReason.None,
        new ScoutCellFacts(CapturedCount.Number(1), CapturedCount.Number(2), CapturedCount.Number(20_988), CapturedCount.Number(24_556), CapturedUsd.Amount(0.252278m), CapturedUsd.Amount(0.2523m)));

    public static ScoutRecord Scout(int ordinal) => new(
        ordinal,
        (ScoutFocus)ordinal,
        "sonnet",
        new string('d', 64),
        "claude-sonnet-5-5",
        CapturedCount.Number(3_614),
        new TokenUsage(2, 118, 5_951, 8_384, 0),
        OutputSeparable: true,
        CapturedUsd.Amount(0.023338m),
        ["src/Store.cs", "src/Other.cs"],
        KeyFilesUnparseable: 1,
        KeyFilesExtra: 0,
        Silent: false,
        ClaimsParsed: 4,
        ClaimsUnparseable: 1,
        ClaimsExtra: 2);

    public static WorkerIdentity Here(string label = "scouts") => WorkerIdentity.Here(label);
}
