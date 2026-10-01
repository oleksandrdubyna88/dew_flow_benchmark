using Bench.Contracts;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Components;

/// <summary>The probe × subject matrix of one probe run (S4). Every word comes from the report — <see cref="ProbeWords"/>' closed
/// sets —; this component only lays it out and spells the words for a reader: <c>not-captured</c> reads <i>not captured</i>, an exit
/// code nobody captured reads <i>exit not captured</i>, and neither is ever drawn as a zero.</summary>
public partial class ProbeMatrixTable : ComponentBase
{
    [Parameter, EditorRequired]
    public ProbeRunReportDto Report { get; set; } = default!;

    /// <summary>The facts each probe reads (§4) — the same selection <c>bench probes report</c> prints.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> FactsOf = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["read-inside"] = ["canaryRead"],
        ["read-outside-bare"] = ["canaryRead"],
        ["read-outside-granted"] = ["canaryRead"],
        ["web-search"] = ["answerCurrent", "toolEvidence"],
        ["read-denied"] = ["canaryRead", "readAttempted"],
        ["web-confined"] = ["canaryRead", "readAttempted", "answerCurrent"],
        ["api-reachable"] = ["reachable", "accountOut"],
    };

    private static readonly string[] EveryFact = ["canaryRead", "readAttempted", "answerCurrent", "toolEvidence", "reachable", "accountOut"];

    /// <summary>The probes in the order the run planned them — the order their cells first appear.</summary>
    private static IReadOnlyList<string> Probes(ProbeRunReportDto report) => [.. report.Cells.Select(c => c.Probe).Distinct(StringComparer.Ordinal)];

    private static IReadOnlyList<ProbeCellReportDto> Repeats(ProbeRunReportDto report, string probe, string subject) =>
        [.. report.Cells.Where(c => c.Probe == probe && c.Subject == subject).OrderBy(c => c.Repeat)];

    private static bool IsDropped(ProbeRunReportDto report, string probe, string subject) =>
        report.Dropped.Any(d => d.Probe == probe && d.Subject == subject);

    private static IEnumerable<(string Name, string Word)> Facts(ProbeCellReportDto cell) =>
        (FactsOf.TryGetValue(cell.Probe, out var names) ? names : EveryFact).Select(name => (name, Fact(cell.Facts, name)));

    private static string Fact(ProbeFactsDto facts, string name) => name switch
    {
        "canaryRead" => facts.CanaryRead,
        "readAttempted" => facts.ReadAttempted,
        "answerCurrent" => facts.AnswerCurrent,
        "toolEvidence" => facts.ToolEvidence,
        "reachable" => facts.Reachable,
        _ => facts.AccountOut,
    };

    /// <summary><c>canaryRead</c> → <c>canary read</c>.</summary>
    private static string Label(string name) =>
        string.Concat(name.Select(c => char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    /// <summary>A closed word for a reader: <c>launch-refused</c> → <c>launch refused</c>.</summary>
    private static string Words(string word) => word.Replace('-', ' ');

    private static string FactWord(string word) => word == ProbeWords.NotCaptured ? "not captured" : word;

    private static string FactClass(string word) => word is ProbeWords.Yes or ProbeWords.No ? "fw-semibold" : "fst-italic text-body-secondary";

    private static string Exit(ProbeExitDto exit) => exit.Captured ? $"exit {exit.Code}" : "exit not captured";

    private static string StateWords(string state) => state == ProbeWords.Claimed ? "claimed — measuring" : state;

    private static string StateBadge(string state) => state switch
    {
        ProbeWords.Settled => "text-bg-success",
        ProbeWords.Claimed => "text-bg-primary",
        ProbeWords.Abandoned => "text-bg-danger",
        _ => "text-bg-secondary",
    };
}
