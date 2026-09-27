using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>One value of one published column. A closed hierarchy so the guard can see exactly which values are
/// TEXT — the only ones that can carry a sentence — and the export can write every kind as its JSON self.</summary>
public abstract record PublishedValue
{
    private PublishedValue()
    {
    }

    public sealed record Text(string Value) : PublishedValue;

    public sealed record Texts(IReadOnlyList<string> Values) : PublishedValue;

    public sealed record Number(decimal Value) : PublishedValue;

    public sealed record Real(double Value) : PublishedValue;

    public sealed record Numbers(IReadOnlyList<decimal> Values) : PublishedValue;

    public sealed record Reals(IReadOnlyList<double> Values) : PublishedValue;

    public sealed record Flag(bool Value) : PublishedValue;

    public sealed record Flags(IReadOnlyList<bool> Values) : PublishedValue;

    public sealed record Moment(DateTimeOffset Value) : PublishedValue;

    public sealed record Identity(Guid Value) : PublishedValue;

    /// <summary>The text this value can carry — empty for every kind that cannot hold a word.</summary>
    public IReadOnlyList<string> Words => this switch
    {
        Text t => [t.Value],
        Texts t => t.Values,
        _ => [],
    };
}

public sealed record PublishedField(string Column, PublishedValue Value);

public sealed record PublishedRow(string Id, IReadOnlyList<PublishedField> Fields);

public sealed record PublishedTable(string Name, IReadOnlyList<PublishedRow> Rows);

/// <summary>Every row of every <c>gate_*</c> table, read back as the database holds it — the ONLY input the public
/// export has. A new table or a new column is read without anyone remembering to add it, because the adapter walks
/// the model rather than a list.</summary>
public interface IGatePublicationSource
{
    Task<IReadOnlyList<PublishedTable>> ReadAsync(CancellationToken cancellationToken);

    /// <summary>The columns that hold a public vendor url BY DESIGN — <c>table.column</c> — and are checked by the
    /// endpoint rule instead of the plain <c>://</c> rule.</summary>
    IReadOnlySet<string> PublicUrlColumns { get; }
}

/// <summary>The public export and the publication guard over the same rows.
/// <para>
/// <b>Built from database rows only</b> — ids, hashes, enum names, numbers and the one redacted failure sentence.
/// It never opens the artefact root: request, reply, prompt, answer and finding bodies stay on the machine that
/// holds them, and nothing inside an artefact file can reach an export because no export step reads one. The
/// guard runs over exactly the rows the export would write; one violation refuses the whole export and writes
/// nothing.
/// </para></summary>
public static class GatePublication
{
    public const string ExportKind = "bench-gate-public-export";

    public static IEnumerable<PublishedText> Texts(IReadOnlyList<PublishedTable> tables) =>
        tables.SelectMany(table => table.Rows.SelectMany(row => row.Fields.SelectMany(field =>
            field.Value.Words.Select(word => new PublishedText(table.Name, field.Column, row.Id, word)))));

    public static IReadOnlyList<GuardViolation> Check(
        IReadOnlyList<PublishedTable> tables, PrivateNames privateNames, IReadOnlySet<string> publicUrlColumns) =>
        PublicationGuard.Check(Texts(tables), privateNames, publicUrlColumns);

    /// <summary>The export document as JSON, or the refusal naming every violation by table, column and row.</summary>
    public static Outcome<string> Export(
        IReadOnlyList<PublishedTable> tables, PrivateNames privateNames, IReadOnlySet<string> publicUrlColumns, DateTimeOffset now)
    {
        var violations = Check(tables, privateNames, publicUrlColumns);

        return violations.Count > 0
            ? Outcome<string>.Failure(
                $"the public export was refused — {violations.Count} value(s) would publish something private:{Environment.NewLine}"
                + string.Join(Environment.NewLine, violations.Select(v => "  " + v.Describe)))
            : Outcome<string>.Success(Document(tables, now).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonObject Document(IReadOnlyList<PublishedTable> tables, DateTimeOffset now)
    {
        var body = new JsonObject();

        foreach (var table in tables.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            body[table.Name] = new JsonArray([.. table.Rows.Select(Row)]);
        }

        return new JsonObject
        {
            ["kind"] = ExportKind,
            ["version"] = 1,
            ["exportedAt"] = now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["tables"] = body,
        };
    }

    private static JsonNode Row(PublishedRow row)
    {
        var node = new JsonObject();

        foreach (var field in row.Fields)
        {
            node[field.Column] = Json(field.Value);
        }

        return node;
    }

    /// <summary>Every kind as its JSON self; a non-finite real — never produced by the store — is written as null,
    /// never as a zero that would read as a measured nothing.</summary>
    private static JsonNode? Json(PublishedValue value) => value switch
    {
        PublishedValue.Text t => JsonValue.Create(t.Value),
        PublishedValue.Texts t => new JsonArray([.. t.Values.Select(v => (JsonNode)JsonValue.Create(v))]),
        PublishedValue.Number n => JsonValue.Create(n.Value),
        PublishedValue.Real r => double.IsFinite(r.Value) ? JsonValue.Create(r.Value) : null,
        PublishedValue.Numbers n => new JsonArray([.. n.Values.Select(v => (JsonNode)JsonValue.Create(v))]),
        PublishedValue.Reals r => new JsonArray([.. r.Values.Select(v => double.IsFinite(v) ? (JsonNode)JsonValue.Create(v) : null)]),
        PublishedValue.Flag f => JsonValue.Create(f.Value),
        PublishedValue.Flags f => new JsonArray([.. f.Values.Select(v => (JsonNode)JsonValue.Create(v))]),
        PublishedValue.Moment m => JsonValue.Create(m.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
        PublishedValue.Identity i => JsonValue.Create(i.Value.ToString("D", CultureInfo.InvariantCulture)),
        _ => throw new InvalidOperationException("unreachable"),
    };
}

/// <summary>Reads the <c>privateNames</c> of a gate suite file — the one part of the suite the publication guard
/// needs. The whole suite is loaded by the driver (E3); the guard must not have to wait for it, and must not
/// parse more of a private file than it uses.</summary>
public static class GatePrivateNames
{
    public static Outcome<PrivateNames> Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("privateNames", out var names)
                   && names.ValueKind == JsonValueKind.Array
                ? Names(names)
                : Outcome<PrivateNames>.Failure(
                    "the suite file has no 'privateNames' array — list the repository and company names the export must never carry, or [] when there are none");
        }
        catch (JsonException ex)
        {
            return Outcome<PrivateNames>.Failure($"the suite file is not JSON — {ex.Message}");
        }
    }

    private static Outcome<PrivateNames> Names(JsonElement names) =>
        names.EnumerateArray().All(n => n.ValueKind == JsonValueKind.String)
            ? Outcome<PrivateNames>.Success(PrivateNames.Of(names.EnumerateArray().Select(n => n.GetString() ?? string.Empty)))
            : Outcome<PrivateNames>.Failure("every entry of 'privateNames' is a string");
}
