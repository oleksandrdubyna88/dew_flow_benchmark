using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Targets;

namespace ModelArms.Core.Experiment;

/// <summary>How one suite run ended. <paramref name="Completed"/> is false when the run was killed, timed out or never produced its report: its
/// failure list is then unknown, not empty.</summary>
public sealed record SuiteRun(bool Completed, int ExitCode, IReadOnlyList<string> Failed, int Total);

/// <summary>What the target's two suites failed before any arm touched it. The members keep the shell's <c>mcpFailed</c> and <c>tsFailed</c> names
/// (the objective check's reader still uses them): <c>McpFailed</c> is the main suite's, <c>TsFailed</c> the extension's.</summary>
public sealed record Baseline(
    CommitSha Base,
    DateTimeOffset MeasuredAt,
    string Host,
    IReadOnlyList<string> McpFailed,
    IReadOnlyList<string> TsFailed,
    int SuiteTotal,
    int ExtensionTotal,
    string Note);

/// <summary>D24's <c>baseline.json</c>. Absent is not zero: an empty failure list claims the suite passed, so a baseline is composed only from two
/// runs that completed, and read back only whole — unknown members, another schema or a missing member are refused by name.</summary>
public static class BaselineFile
{
    public const string Schema = "model-arms/baseline-v1";

    private const string FileName = "baseline.json";
    private static readonly string[] Members = ["schema", "base", "measuredAt", "host", "mcpFailed", "tsFailed", "suiteTotal", "extensionTotal", "note"];

    /// <summary>The baseline of two completed runs, or every reason one cannot be made, one per line. The instant is kept as the same instant in UTC.</summary>
    public static Outcome<Baseline> Compose(CommitSha @base, DateTimeOffset measuredAt, string host, SuiteRun suite, SuiteRun extension, string note)
    {
        var problems = new[] { Unnamed(host), NegativeTotal(suite, extension) }
            .Concat(RunProblems("main", suite))
            .Concat(RunProblems("extension", extension))
            .Where(problem => problem.Length > 0)
            .ToList();
        return problems.Count > 0
            ? Outcome<Baseline>.Failure(string.Join('\n', problems))
            : Outcome<Baseline>.Success(new Baseline(@base, measuredAt.ToUniversalTime(), host, suite.Failed, extension.Failed, suite.Total, extension.Total, note));
    }

    /// <summary>The file's text: indented JSON, members in the order of the plan, the instant in UTC ending in <c>Z</c>.</summary>
    public static string Write(Baseline baseline) =>
        new JsonObject
        {
            ["schema"] = Schema,
            ["base"] = baseline.Base.Value,
            ["measuredAt"] = baseline.MeasuredAt.ToUniversalTime().UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["host"] = baseline.Host,
            ["mcpFailed"] = Names(baseline.McpFailed),
            ["tsFailed"] = Names(baseline.TsFailed),
            ["suiteTotal"] = baseline.SuiteTotal,
            ["extensionTotal"] = baseline.ExtensionTotal,
            ["note"] = baseline.Note,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    public static Outcome<Baseline> Read(string json)
    {
        var reader = new StrictJson();
        var top = reader.ParseObject(json, FileName) is { } parsed ? reader.Object(parsed, string.Empty, Members, Members) : new JsonObject();
        var baseline = Walk(reader, top);
        return reader.Result(() => baseline);
    }

    /// <summary>Every member read before the verdict, so a problem found while reading counts. After a problem the values are stand-ins nobody sees.</summary>
    private static Baseline Walk(StrictJson reader, JsonObject top)
    {
        CheckSchema(reader, top);
        return new Baseline(
            reader.Sha(top, string.Empty, "base"),
            MeasuredAt(reader, top),
            reader.Text(top, string.Empty, "host"),
            reader.Texts(top, string.Empty, "mcpFailed"),
            reader.Texts(top, string.Empty, "tsFailed"),
            reader.Count(top, string.Empty, "suiteTotal"),
            reader.Count(top, string.Empty, "extensionTotal"),
            reader.AnyText(top, string.Empty, "note"));
    }

    private static JsonArray Names(IReadOnlyList<string> names) => new(names.Select(name => (JsonNode?)JsonValue.Create(name)).ToArray());

    private static void CheckSchema(StrictJson reader, JsonObject top)
    {
        var schema = reader.Text(top, string.Empty, "schema");
        if (schema.Length > 0 && schema != Schema)
        {
            reader.Add($"schema is '{schema}' — this build reads exactly '{Schema}'");
        }
    }

    private static DateTimeOffset MeasuredAt(StrictJson reader, JsonObject top)
    {
        var text = reader.Text(top, string.Empty, "measuredAt");
        return text.Length == 0
            ? default
            : Utc(text).Match(
                instant => instant,
                reason =>
                {
                    reader.Add(reason);
                    return default;
                });
    }

    private static Outcome<DateTimeOffset> Utc(string text) =>
        DateTime.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant) && instant.Kind == DateTimeKind.Utc
            ? Outcome<DateTimeOffset>.Success(new DateTimeOffset(instant, TimeSpan.Zero))
            : Outcome<DateTimeOffset>.Failure($"measuredAt is '{text}' — it must be an ISO-8601 instant in UTC ending in Z, like 2026-10-29T12:30:15.0000000Z");

    /// <summary>What is wrong with one run. A run that did not complete has no failure list at all; a run that did must still say something a baseline can
    /// stand on: an exit code that agrees with its names, a count above zero, and no more failures than tests. A completed exit 0 with failures
    /// named is NOT refused: the exit code is the runner's opinion, the names are the measurement.</summary>
    private static IEnumerable<string> RunProblems(string which, SuiteRun run) =>
        run.Completed
            ? [ExitedWithoutNames(which, run), ZeroTotal(which, run), MoreFailuresThanTests(which, run)]
            : [Incomplete(which)];

    private static string Incomplete(string which) =>
        $"the {which} suite did not run to completion — a baseline with an empty failure list would claim it passed (absent is not zero)";

    private static string ExitedWithoutNames(string which, SuiteRun run) =>
        run.ExitCode != 0 && run.Failed.Count == 0
            ? $"the {which} suite exited {run.ExitCode.ToString(CultureInfo.InvariantCulture)} and named no failing test — a baseline with an empty failure list would claim it passed (absent is not zero)"
            : string.Empty;

    private static string ZeroTotal(string which, SuiteRun run) =>
        run.Total == 0 ? $"the {which} suite counted 0 tests — a run that ran nothing proves nothing about the target" : string.Empty;

    private static string MoreFailuresThanTests(string which, SuiteRun run) =>
        run.Failed.Count > run.Total
            ? $"the {which} suite named {run.Failed.Count.ToString(CultureInfo.InvariantCulture)} failing tests of {run.Total.ToString(CultureInfo.InvariantCulture)} — more failures than tests"
            : string.Empty;

    private static string Unnamed(string host) =>
        host.Trim().Length == 0 ? "the host is empty — a baseline names the machine it was measured on" : string.Empty;

    private static string NegativeTotal(SuiteRun suite, SuiteRun extension) =>
        suite.Total < 0 || extension.Total < 0 ? "a suite total is negative — it counts tests, zero or more" : string.Empty;
}
