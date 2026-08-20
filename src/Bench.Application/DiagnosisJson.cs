using System.Text.Json;
using Bench.Application.Bank;
using Bench.Domain.Runs;
using Bench.Domain.Suites;

namespace Bench.Application;

/// <summary>Reading a <see cref="Diagnosis"/> out of an Investigate phase's answer
/// (todo/PLAN_investigate_vs_implement.md §3.2).
/// <para>
/// Over <see cref="AgentJson"/> — the authoring pass's extractor, reused rather than rewritten, because
/// its rules were paid for live: a fence is unwrapped, prose around the payload is KEPT rather than
/// dropped, and the acceptance test is the real parser (a balanced slice that is not the answer is
/// exactly what a braced snippet in prose produces). Extraction, never repair: editing fields to make
/// them parse would make a batch unattributable.
/// </para>
/// <para>
/// Unknown members are TOLERATED, deliberately — an agent's answer is a payload, not a catalog row, and
/// the <c>Disallow</c> discipline guards configurations, where a stray field silently becomes a
/// different arm. Here a stray field is commentary; the measured contract is the required half: at
/// least the mechanism must be present, or the reading is malformed and says which half is missing.
/// </para></summary>
public static class DiagnosisJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static DiagnosisReading Read(string answerText)
    {
        var payload = AgentJson.Extract(answerText, '{', '}', Recognisable);

        if (!payload.Json.Contains('{'))
        {
            return new DiagnosisReading.Absent();
        }

        try
        {
            var wire = JsonSerializer.Deserialize<DiagnosisWire>(payload.Json, Options);

            if (wire is null)
            {
                return new DiagnosisReading.Malformed("the object read as null", Sample(payload.Json));
            }

            return string.IsNullOrWhiteSpace(wire.Mechanism)
                ? new DiagnosisReading.Malformed(
                    "the diagnosis names no mechanism — the causal chain is the contract's required half",
                    Sample(payload.Json))
                : new DiagnosisReading.Parsed(ToDomain(wire), payload.Said);
        }
        catch (JsonException error)
        {
            return new DiagnosisReading.Malformed(error.Message, Sample(payload.Json));
        }
    }

    /// <summary>Whether a balanced slice is recognisably a diagnosis — the scan's acceptance test. An
    /// empty object in prose deserialises happily into all-null fields, so "it parses" alone would let
    /// `new Options { }` steal the read from the real answer further down.</summary>
    private static bool Recognisable(string candidate)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<DiagnosisWire>(candidate, Options);

            return wire is not null && (wire.Anchors is not null || !string.IsNullOrWhiteSpace(wire.Mechanism));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>A pathless anchor is kept, inert — it can match nothing, so it honestly lowers the
    /// aim metric instead of being silently dropped from the denominator.</summary>
    private static Diagnosis ToDomain(DiagnosisWire wire) => new(
        [.. (wire.Anchors ?? []).Select(a => new DiagnosisAnchor(
            (a.Path ?? string.Empty).Trim(),
            (a.Member ?? string.Empty).Trim(),
            a.Lines is null ? LineSpan.Whole : new LineSpan(a.Lines.Start, a.Lines.End)))],
        wire.Mechanism!.Trim(),
        (wire.FixIntent ?? string.Empty).Trim());

    private static string Sample(string json) => json.Length <= 200 ? json : json[..200] + "…";

    private sealed record DiagnosisWire(List<AnchorWire>? Anchors, string? Mechanism, string? FixIntent);

    private sealed record AnchorWire(string? Path, string? Member, LinesWire? Lines);

    private sealed record LinesWire(int Start, int End);
}
