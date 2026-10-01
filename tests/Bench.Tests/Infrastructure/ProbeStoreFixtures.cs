using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Tests.Infrastructure;

/// <summary>The shapes the probe store tests build again and again — written once, beside <see cref="GateStoreFixtures"/>.</summary>
internal static class ProbeStoreFixtures
{
    public static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public static ProductPin Pin(char letter = 'a') =>
        ProductPin.Hashed(new string(letter, 64), "2.1.286", "abc1234", CapturedCount.Number(0), "cli").Ok();

    public static ProbeOracle Oracle() => ProbeOracle.Parse("0.52.0", OracleSource.Registry).Ok();

    public static ProbeSubject Subject(string id = "claude-sonnet", string runtime = "claude") =>
        ProbeSubject.Parse(id, runtime, "model-x", "BENCH_CLAUDE").Ok();

    public static ProbeRun Run(params ProbeSubject[] subjects) =>
        ProbeRun.Planned(Guid.CreateVersion7(), Oracle(), subjects.Length == 0 ? [Subject()] : subjects, repeats: 3, Noon).Ok();

    /// <summary>A planned run of <paramref name="count"/> cells of ONE probe over ONE subject — repeats 1..count, one per slot.</summary>
    public static (ProbeRun Run, IReadOnlyList<ProbeCell> Cells) Planned(int count = 1, ProbeKind probe = ProbeKind.ReadOutsideBare)
    {
        var run = Run();
        var cells = Enumerable.Range(0, count)
            .Select(i => ProbeCell.Pending(Guid.CreateVersion7(), run.Id, new ProbeMatrixCell(probe, run.Subjects[0].Id, Repeat: i + 1, Slot: i, Position: 0)))
            .ToList();

        return (run, cells);
    }

    /// <summary>A planned run through the matrix: the given probes over the given subjects, two repeats.</summary>
    public static (ProbeRun Run, ProbePlan Plan, IReadOnlyList<ProbeCell> Cells) PlannedMatrix(IReadOnlyList<ProbeKind> probes, params ProbeSubject[] subjects)
    {
        var run = ProbeRun.Planned(Guid.CreateVersion7(), Oracle(), subjects, repeats: 2, Noon).Ok();
        var plan = ProbeMatrix.Plan(probes, subjects, repeats: 2).Ok();

        return (run, plan, [.. plan.Cells.Select(c => ProbeCell.Pending(Guid.CreateVersion7(), run.Id, c))]);
    }

    /// <summary>An answered attempt with every fact filled and three artefacts.</summary>
    public static ProbeSettlement Settlement(ProbeFact canary = ProbeFact.Yes) => new(
        ProbeFacts.Answered(CapturedCount.Number(0)) with { CanaryRead = canary, ReadAttempted = ProbeFact.No, AnswerCurrent = ProbeFact.NotCaptured, ToolEvidence = ProbeFact.NotCaptured },
        [Artifact(ProbeArtifactKind.Answer, 'a', 12), Artifact(ProbeArtifactKind.Stdout, 'b', 2048), Artifact(ProbeArtifactKind.Stderr, 'c', 0)]);

    public static ProbeArtifact Artifact(ProbeArtifactKind kind, char sha, long length) =>
        ProbeArtifact.Of(kind, ArtifactPath.Parse($"probes/run/cell/g1/a1/{kind.ToString().ToLowerInvariant()}.txt").Ok(), new string(sha, 64), length).Ok();

    public static WorkerIdentity Here(string label = "lane") => WorkerIdentity.Here(label);
}
