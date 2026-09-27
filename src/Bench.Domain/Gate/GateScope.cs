namespace Bench.Domain.Gate;

/// <summary>The part of a gate run's identity that must match before two runs may be put beside each other:
/// the suite, the gate, the product's version text and the settings hash. Everything else — reviewer, task,
/// repeat — is an axis compared ALONG. A page that puts two products or two settings hashes in one column has
/// folded two populations: the compute-backend lesson, one level up.</summary>
public sealed record GateScope(string SuiteStamp, GateKind Gate, string ProductVersion, string SettingsHash)
{
    public static GateScope Of(string suiteStamp, GateKind gate, ProductPin pin, string settingsHash) =>
        new(suiteStamp, gate, pin.VersionText, settingsHash);

    public string Describe => $"{SuiteStamp} · {Gate.ToString().ToLowerInvariant()} · {ProductVersion} · settings {Short(SettingsHash)}";

    private static string Short(string hash) => hash.Length >= 12 ? hash[..12] : hash;
}

/// <summary>Where a run came from. An imported run never enters a native figure without this label on the
/// row, because the other harness's numbers were produced by a different driver.</summary>
public abstract record RunSource
{
    private RunSource()
    {
    }

    public sealed record Native : RunSource;

    /// <param name="Harness">The other harness's name — <c>calib-py</c>, <c>coai-bench</c>.</param>
    public sealed record Imported(string Harness) : RunSource;

    public string Label => this switch
    {
        Imported i => i.Harness,
        _ => "native",
    };
}

/// <summary>One RUN — one cell's product session — as the report reads it: which campaign planned it, which
/// scope its pin put it in, which task, reviewer and repeat, the facts, the findings (hashes only), and where
/// it came from. A verdict joins on <see cref="RunId"/>; the campaign is the <c>bench gate run</c> invocation.</summary>
public sealed record GateRunRecord(
    Guid CampaignId,
    Guid RunId,
    GateScope Scope,
    GateTaskId Task,
    GateReviewerId Reviewer,
    int Repeat,
    int Attempt,
    GateRunFacts Facts,
    IReadOnlyList<GateFinding> Findings,
    RunSource Source)
{
    public int FindingsCount => Facts.Findings.WasCaptured ? (int)Facts.Findings.Value : Findings.Count;
}

/// <summary>Everything a report is computed from: the tasks as the database knows them, the runs, and the
/// verdicts already joined to (run, finding ordinal) through the blinded key.</summary>
public sealed record GateReportInput(
    IReadOnlyList<TaskSummary> Tasks,
    IReadOnlyList<GateRunRecord> Runs,
    IReadOnlyList<GateVerdict> Verdicts);
