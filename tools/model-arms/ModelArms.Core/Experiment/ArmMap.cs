using System.Text.Json;
using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>The arm map: which random token is each arm's folder name. An arm cannot guess another arm's
/// folder, so a token never reaches a brief or a log line; the map is what lets the one console writer and
/// the brief filler check that (<see cref="Leaks"/>, <see cref="Scrub"/>). A token is minted once and kept
/// for the life of the experiment — <see cref="Extend"/> never re-mints an existing arm.</summary>
public sealed record ArmMap
{
    private const int TokenBytes = 8;
    private const int MaximumDraws = 64;

    private static readonly Regex TokenShape = new(@"^[0-9a-f]{16}\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private ArmMap(IReadOnlyList<KeyValuePair<string, string>> entries) => Entries = Array.AsReadOnly(entries.ToArray());

    public static ArmMap Empty { get; } = new([]);

    /// <summary>Arm name to token, in the order the arms were added. A read-only view over a private copy, so no cast reaches a writable list.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Entries { get; }

    /// <summary>Equal when the same arms have the same tokens in the same order (ordinal), not when the lists are the same object.</summary>
    public bool Equals(ArmMap? other) => other is not null && Entries.SequenceEqual(other.Entries);

    public override int GetHashCode() => Entries.Aggregate(0, (hash, entry) => HashCode.Combine(hash, entry.Key, entry.Value));

    /// <summary>Reads <c>arms.map.json</c>: an object of arm name to token. Every arm that is wrong is named, one
    /// sentence per line; a shell-era map (12-hex tokens) is refused here and a run mints its own.</summary>
    public static Outcome<ArmMap> Read(string json) =>
        Parse(json).Match(
            pairs => Problems(pairs) is { Count: > 0 } problems
                ? Outcome<ArmMap>.Failure(string.Join('\n', problems))
                : Outcome<ArmMap>.Success(new ArmMap(pairs)),
            reason => Outcome<ArmMap>.Failure(reason));

    /// <summary>The map as an indented JSON object of strings in insertion order, like the shell's map but not byte-identical
    /// (tokens, indent and file name differ).</summary>
    public string ToJson() =>
        JsonText.Write(
            new JsonWriterOptions { Indented = true, NewLine = "\n" },
            writer =>
            {
                writer.WriteStartObject();
                foreach (var (arm, token) in Entries)
                {
                    writer.WriteString(arm, token);
                }

                writer.WriteEndObject();
            });

    /// <summary>The existing map plus a fresh token for each arm not yet in it. An existing entry is never
    /// touched, an arm absent from <paramref name="arms"/> is kept, and a draw that collides with a token already
    /// in the map is drawn again. The random source is a parameter so the decision stays pure; a name that is not
    /// an arm name, or a source that does not give eight bytes or keeps colliding, is a programming error.</summary>
    public static ArmMap Extend(ArmMap existing, IReadOnlyList<string> arms, Func<byte[]> eightRandomBytes)
    {
        var entries = existing.Entries.ToList();
        foreach (var arm in arms.Where(arm => entries.TrueForAll(entry => entry.Key != arm)))
        {
            entries.Add(new KeyValuePair<string, string>(RequireArmName(arm), Mint(entries, eightRandomBytes)));
        }

        return new ArmMap(entries);
    }

    public Outcome<string> TokenOf(string arm) =>
        Entries.Where(entry => entry.Key == arm).Select(entry => entry.Value).FirstOrDefault() is { } token
            ? Outcome<string>.Success(token)
            : Outcome<string>.Failure($"unknown arm '{arm}'; the known arms are: {(Entries.Count == 0 ? "(none)" : string.Join(", ", Entries.Select(e => e.Key)))}");

    /// <summary>What <paramref name="text"/> would reveal to the arm <paramref name="ownArm"/>: every arm name (its own
    /// included) and every scheme name found as a whole word (a one-letter scheme in its own case only, every other name
    /// in any case), and every token but its own found anywhere. Its own
    /// token is its working directory and is allowed. Empty means clean.</summary>
    public IReadOnlyList<string> Leaks(string text, string ownArm, IReadOnlyCollection<string> schemes) =>
        [.. Entries.Select(entry => entry.Key).Concat(schemes)
                .Where(name => ContainsWholeWord(text, name))
                .Concat(Entries.Where(entry => entry.Key != ownArm).Select(entry => entry.Value)
                    .Where(token => text.Contains(token, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)];

    /// <summary>The line with every token of the map replaced by <c>&lt;token&gt;</c>. The console writer calls this
    /// and nothing else does its own copy of it.</summary>
    public string Scrub(string line) =>
        Entries.Aggregate(line, (scrubbed, entry) => scrubbed.Replace(entry.Value, "<token>", StringComparison.Ordinal));

    private static Outcome<List<KeyValuePair<string, string>>> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? PairsOf(document.RootElement)
                : Outcome<List<KeyValuePair<string, string>>>.Failure("the arm map must be a JSON object of arm name to token");
        }
        catch (JsonException ex)
        {
            return Outcome<List<KeyValuePair<string, string>>>.Failure($"the arm map is not valid JSON: {ex.Message}");
        }
    }

    private static Outcome<List<KeyValuePair<string, string>>> PairsOf(JsonElement map)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var property in map.EnumerateObject())
        {
            pairs.Add(new KeyValuePair<string, string>(property.Name, TokenText(property.Value)));
        }

        return Outcome<List<KeyValuePair<string, string>>>.Success(pairs);
    }

    /// <summary>The string itself, or for any other JSON value a description that can never look like a token, so the arm is
    /// named with every other wrong arm instead of ending the read at the first one.</summary>
    private static string TokenText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : $"a JSON {value.ValueKind.ToString().ToLowerInvariant()}";

    private static List<string> Problems(List<KeyValuePair<string, string>> pairs) =>
    [
        .. pairs.Where(pair => !ArmNames.IsArmName(pair.Key))
            .Select(pair => $"arm '{pair.Key}': an arm name is letters then digits, like C1 or SRS2"),
        .. pairs.Where(pair => !TokenShape.IsMatch(pair.Value))
            .Select(pair => $"arm '{pair.Key}': its token must be 16 lower-case hex characters, got '{pair.Value}'"),
        .. pairs.GroupBy(pair => pair.Key, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => $"arm '{group.Key}': it appears more than once"),
        .. pairs.GroupBy(pair => pair.Value, StringComparer.Ordinal).Where(group => group.Select(pair => pair.Key).Distinct().Count() > 1)
            .Select(group => $"arms {string.Join(", ", group.Select(pair => $"'{pair.Key}'"))}: they share one token"),
    ];

    private static string RequireArmName(string arm) =>
        ArmNames.IsArmName(arm) ? arm : throw new ArgumentException($"'{arm}' is not an arm name (letters then digits, like C1)", nameof(arm));

    private static string Mint(List<KeyValuePair<string, string>> taken, Func<byte[]> eightRandomBytes)
    {
        for (var draw = 0; draw < MaximumDraws; draw++)
        {
            var bytes = eightRandomBytes();
            var token = bytes.Length == TokenBytes
                ? Convert.ToHexStringLower(bytes)
                : throw new InvalidOperationException($"the random source gave {bytes.Length} bytes, an arm token needs {TokenBytes}");
            if (taken.TrueForAll(entry => entry.Value != token))
            {
                return token;
            }
        }

        throw new InvalidOperationException($"the random source repeated an existing token {MaximumDraws} times in a row");
    }

    /// <summary>Whole word: not preceded and not followed by an ASCII letter or digit. A name of several characters is matched
    /// whatever its case ("as c1 did" names <c>C1</c>); a single character only in its own case, so the scheme <c>C</c> is
    /// found in "scheme C" and not in "MCP", "C1" or the <c>/mnt/c/</c> of a WSL path.</summary>
    private static bool ContainsWholeWord(string text, string word)
    {
        var comparison = word.Length > 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var at = word.Length == 0 ? -1 : text.IndexOf(word, comparison); at >= 0; at = text.IndexOf(word, at + 1, comparison))
        {
            var after = at + word.Length;
            if ((at == 0 || !char.IsAsciiLetterOrDigit(text[at - 1])) && (after == text.Length || !char.IsAsciiLetterOrDigit(text[after])))
            {
                return true;
            }
        }

        return false;
    }
}
