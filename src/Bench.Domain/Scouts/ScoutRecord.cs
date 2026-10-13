using Bench.Domain.Probes;
using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>What one scout of a settled cell is recorded as — the per-scout row (§3.12's <c>cells.jsonl</c> fields): which call it was,
/// its focus, the model it was asked for and the one that answered, its time, its tokens and cost, its KEY FILES and its claim counts.
/// Never its text: the report and the claims live in the artefact root, and what is committed of them is scrubbed.</summary>
/// <param name="Ordinal">The scout's place in its arm, 1..N — the focus area it was given (F1..F5).</param>
/// <param name="OutputSeparable">Whether <see cref="Tokens"/>' output count is this scout's own (<see cref="ScoutTokens"/>).</param>
/// <param name="KeyFiles">Repository-relative paths as the matcher normalised them; a path that is not repository-relative is not stored.</param>
public sealed record ScoutRecord(
    int Ordinal,
    ScoutFocus Focus,
    string InputModel,
    string PromptSha256,
    string ResolvedModel,
    CapturedCount DurationMs,
    TokenUsage Tokens,
    bool OutputSeparable,
    CapturedUsd CostUsd,
    IReadOnlyList<string> KeyFiles,
    int KeyFilesUnparseable,
    int KeyFilesExtra,
    bool Silent,
    int ClaimsParsed,
    int ClaimsUnparseable,
    int ClaimsExtra);
