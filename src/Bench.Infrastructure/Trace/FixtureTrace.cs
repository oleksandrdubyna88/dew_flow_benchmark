using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Trace;

/// <summary>The white-box trace, replayed from a stored payload.
/// <para>
/// <b>Why a fixture is a real implementation and not a placeholder.</b> An interface with a single
/// implementation proves nothing about its own shape — it fits its one caller by construction. Two is
/// the minimum at which it becomes visible whether the abstraction has quietly grown into one engine.
/// So the white-box side ships now, against a payload on disk, and the port is exercised in both modes
/// from the first commit rather than waiting for an engine that can emit a funnel.
/// </para>
/// <para>
/// <b>The fixture is hand-authored and says so.</b> The plan intended to derive v0 from a captured
/// payload of a real stage instrument; that engine went out of scope, so there is nothing to capture
/// yet. The file carries an <c>authored</c> field naming itself as such, and this reader REFUSES a
/// fixture that omits it — the failure this project has retracted more than once is a claim about a
/// system taken from a description of the system, and an unlabelled fixture is exactly how a
/// provisional contract quietly becomes canonical.
/// </para></summary>
public sealed class FixtureTrace(string fixturePath) : IRunTrace
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public TraceMode Mode => TraceMode.WhiteBox;

    public async Task<Outcome<LegTrace>> CaptureAsync(
        MeasurementTuple tuple, CancellationToken cancellationToken)
    {
        if (!File.Exists(fixturePath))
        {
            return Outcome<LegTrace>.Failure($"no trace fixture at {fixturePath}");
        }

        var parsed = Parse(await File.ReadAllTextAsync(fixturePath, cancellationToken));
        if (parsed is Outcome<FunnelFixture>.Fail bad)
        {
            return Outcome<LegTrace>.Failure(bad.Reason);
        }

        var fixture = ((Outcome<FunnelFixture>.Ok)parsed).Value;

        return Build(fixture);
    }

    private static Outcome<LegTrace> Build(FunnelFixture fixture)
    {
        if (fixture.Authored.Length == 0)
        {
            return Outcome<LegTrace>.Failure(
                "the fixture does not say who authored it — an unlabelled payload is how a provisional "
                + "contract becomes canonical without anyone deciding it should");
        }

        var funnel = new RetrievalFunnel(
            fixture.ContractVersion,
            [.. fixture.Stages.Select(s => new FunnelStage(s.Name, s.In, s.Out, s.Ms))],
            fixture.TotalMs,
            fixture.Absent);

        var validated = TraceContract.Validate(funnel);
        if (validated is Outcome<RetrievalFunnel>.Fail invalid)
        {
            return Outcome<LegTrace>.Failure(invalid.Reason);
        }

        // The prompt and the answer are deliberately NOT in the fixture: this file carries the funnel,
        // which is the one thing a black-box observer cannot see. Everything else about a leg comes
        // from having run it, and inventing it here would make a replayed trace look like a measurement.
        return Outcome<LegTrace>.Success(new LegTrace(
            Captured.Unavailable("replayed from a fixture, which carries the funnel and nothing else"),
            Captured.Unavailable("replayed from a fixture, which carries the funnel and nothing else"),
            [],
            default,
            default,
            0m,
            funnel));
    }

    private static Outcome<FunnelFixture> Parse(string text)
    {
        try
        {
            var fixture = JsonSerializer.Deserialize<FunnelFixture>(text, Json);
            return fixture is null
                ? Outcome<FunnelFixture>.Failure("the trace fixture is empty")
                : Outcome<FunnelFixture>.Success(fixture);
        }
        catch (JsonException ex)
        {
            return Outcome<FunnelFixture>.Failure($"malformed trace fixture: {ex.Message}");
        }
    }
}

/// <summary>The stored funnel, as JSON. Its own type rather than the domain record, so the payload can
/// change shape when a real engine finally emits one without dragging the domain behind it.</summary>
internal sealed record FunnelFixture
{
    [JsonPropertyName("contractVersion")] public string ContractVersion { get; init; } = string.Empty;

    /// <summary>Who produced this payload, and how. Required: see the class remarks on FixtureTrace.</summary>
    [JsonPropertyName("authored")] public string Authored { get; init; } = string.Empty;

    /// <summary>Measured end to end, independently of the stages. Its whole purpose is to disagree with
    /// their sum when a stage is missing.</summary>
    [JsonPropertyName("totalMs")] public long TotalMs { get; init; }

    /// <summary>Contract stages this engine does not perform, by name.</summary>
    [JsonPropertyName("absent")] public IReadOnlyList<string> Absent { get; init; } = [];

    [JsonPropertyName("stages")] public IReadOnlyList<StageFixture> Stages { get; init; } = [];
}

internal sealed record StageFixture
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;

    [JsonPropertyName("in")] public int In { get; init; }

    [JsonPropertyName("out")] public int Out { get; init; }

    [JsonPropertyName("ms")] public long Ms { get; init; }
}
