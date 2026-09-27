namespace Bench.Domain.Gate;

/// <summary>The part of a gate run's identity that must match before two runs may be put beside each other:
/// the suite, the gate, the product's version text and the settings hash. Everything else — reviewer, task,
/// repeat — is an axis compared ALONG. A page that puts two products or two settings hashes in one column has
/// folded two populations: the compute-backend lesson, one level up.
/// <para>
/// The product is identified by its BYTES as well as its words: <see cref="ProductVersion"/> is what
/// <c>--version</c> printed and <see cref="ProductSha256"/> is the binary's hash. A dirty rebuild at one HEAD
/// prints the same version text from different bytes, and a scope keyed by the text alone averaged the two.
/// An imported pin has no binary to hash; its sha is empty and its version text says so.
/// </para></summary>
public sealed record GateScope(string SuiteStamp, GateKind Gate, string ProductVersion, string ProductSha256, string SettingsHash)
{
    public static GateScope Of(string suiteStamp, GateKind gate, ProductPin pin, string settingsHash) =>
        new(suiteStamp, gate, pin.VersionText, pin.BinarySha256, settingsHash);

    public string Describe =>
        $"{SuiteStamp} · {Gate.ToString().ToLowerInvariant()} · {ProductVersion} ({Bytes}) · settings {HashText.Short(SettingsHash)}";

    private string Bytes => ProductSha256.Length > 0 ? HashText.Short(ProductSha256) : "binary not hashed";
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
