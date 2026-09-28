using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Trace;

namespace Bench.Tests.Gate;

/// <summary>The runs, tasks and verdicts the report tests are built from — built through the real factories
/// (<see cref="GateVerdict.Under"/>, <see cref="Rubric.Of"/>, <see cref="ProductPin.Hashed"/>), so a fixture the
/// code would refuse cannot quietly turn a test into an assertion about the empty input.</summary>
internal static class GateReportFixtures
{
    public static readonly string StrictHash = StableHash.Of("strict");
    public static readonly string LenientHash = StableHash.Of("lenient");

    public static readonly Rubric StrictRubric = Rubric.Of("strict-v1", RubricKind.Strict, StrictHash).Ok();
    public static readonly Rubric LenientRubric = Rubric.Of("lenient-worth-v1", RubricKind.LenientWorth, LenientHash).Ok();

    public static readonly RubricCatalog Catalog = new([StrictRubric, LenientRubric]);

    public static readonly ProductPin PinA = ProductPin.Hashed(new string('a', 64), "0.0.0+aaaaaaa", "aaaaaaa", CapturedCount.Number(0), "src").Ok();
    public static readonly ProductPin PinB = ProductPin.Hashed(new string('b', 64), "0.0.0+bbbbbbb", "bbbbbbb", CapturedCount.Number(0), "src").Ok();

    public static GateScope Scope(ProductPin pin) => GateScope.Of("gate-seeded#abc", GateKind.Feature, pin, StableHash.Of("settings"));

    public static GateReportInput Input(IReadOnlyList<GateRunRecord> runs, IReadOnlyList<GateVerdict>? verdicts = null) =>
        new(
            [
                Task("cs2", seeds: [("cs2-S1", false), ("cs2-S2", true)]),
                Task("rs3", seeds: [("rs3-S1", false)]),
                Task("calib1", seeds: [("calib1-S1", false)], calibration: true),
                Task("plain1", seeds: []),
            ],
            runs,
            verdicts ?? []);

    public static TaskSummary Task(string id, IReadOnlyList<(string Id, bool Cross)> seeds, bool calibration = false) =>
        new(GateTaskId.Parse(id).Ok(), "C#", calibration, HostedGates.All, [.. seeds.Select(s => new SeedRef(SeedId.Parse(s.Id).Ok(), s.Cross))]);

    public static GateRunRecord Run(
        string task, string reviewer, int repeat, bool valid = true, int findings = 2, double seconds = 180.7,
        CapturedUsd? cost = null, ProductPin? pin = null, Guid? campaign = null, FailureKind failure = FailureKind.None,
        long turnOneCached = 0, int attempt = 1)
    {
        var facts = new GateRunFacts(
            valid,
            valid ? GateVerdictWord.Proceed : GateVerdictWord.CallHuman,
            true,
            CapturedCount.Number(findings),
            2,
            2,
            ["stop", "stop"],
            [200, 200],
            CapturedCount.Number(160_000),
            CapturedCount.Number(12_000),
            CapturedCount.Number(80_000),
            CapturedCount.Unavailable("no reasoning tokens"),
            seconds,
            seconds - 3,
            [140.1, 37.5],
            [CapturedCount.Number(turnOneCached), CapturedCount.Number(80_384)],
            cost ?? CapturedUsd.Amount(0.19m),
            6,
            0,
            valid ? FailureCause.None : new FailureCause(failure, "call 2: finish_reason=length"));

        return new GateRunRecord(
            campaign ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Scope(pin ?? PinA),
            GateTaskId.Parse(task).Ok(),
            GateReviewerId.Parse(reviewer).Ok(),
            repeat,
            attempt,
            facts,
            [],
            new RunSource.Native());
    }

    public static Verdict Strict(StrictReading reading, string seed = "none", ValueLevel value = ValueLevel.Medium) =>
        new Verdict.Strict(reading, value, SeverityFairness.Yes, Grounding.Yes, StableHash.Of("cs2:x"), SeedHit.Parse(seed));

    public static GateVerdict Verdict(
        GateRunRecord run, int ordinal, Verdict reading, string rubricHash, string assessor = "codex-astra", bool familyMatches = false) =>
        GateVerdict.Under(Catalog, rubricHash, run.RunId, ordinal, reading, GateReviewerId.Parse(assessor).Ok(), "b1", rubricHash, familyMatches).Ok();

    /// <summary>The input with a recorded hand-check covering every (campaign, assessor) its strict verdicts come from —
    /// for the tests that are about the arithmetic of a strict rate, not about the gate that withholds it (E4).</summary>
    public static GateReportInput HandChecked(GateReportInput input)
    {
        var campaignOf = input.Runs.ToDictionary(r => r.RunId, r => r.CampaignId);
        var checks = input.Verdicts
            .Where(v => campaignOf.ContainsKey(v.RunId))
            .Select(v => (Campaign: campaignOf[v.RunId], v.Assessor, v.Rubric))
            .Distinct()
            .Select(p => HandCheck.Of([p.Campaign], p.Rubric, p.Assessor, HandCheck.MinVerdicts, HandCheck.MinVerdicts, new string('a', 64), DateTimeOffset.UnixEpoch).Ok())
            .ToList();

        return input with { HandChecks = checks };
    }
}
