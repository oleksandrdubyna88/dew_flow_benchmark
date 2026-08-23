using System.Text.Json;

namespace Bench.Domain.Sessions;

/// <summary>What a call was ABOUT, reduced to one comparable string.
/// <para>
/// Every loop detector is a question about repetition — the same file read three times, the same search
/// asked four ways — and repetition can only be seen through a normal form. Two reads of
/// <c>src/Foo.cs</c> and <c>./src/Foo.cs</c> are the same read, and a detector comparing raw argument
/// JSON would see two different ones and find nothing.
/// </para>
/// <para>
/// Stored on the call rather than derived at query time, for the reason <c>CallerKey</c> is: the reports
/// group by it, and a value computed in C# cannot be indexed.
/// </para></summary>
public static class ToolTarget
{
    /// <summary>How much of a command survives as its identity. Enough to tell <c>dotnet build</c> from
    /// <c>dotnet test</c>, short enough that two builds of different projects still count as the same
    /// kind of act.</summary>
    private const int CommandWords = 3;

    /// <summary>Argument keys that name a call's subject, in the order they are believed.
    /// <para>
    /// A file path outranks a pattern because a tool carrying both — a scoped search — is more usefully
    /// identified by what it searched than by what it searched for. Nothing here is guessed from the tool
    /// name: the keys are read off the arguments, so a tool this table has never heard of still yields a
    /// target as long as it names its subject conventionally.
    /// </para></summary>
    private static readonly string[] SubjectKeys =
    [
        "file_path", "filePath", "notebook_path", "path", "url",
        "pattern", "query", "text", "search", "glob", "name",
    ];

    /// <summary>The call's subject, or an empty string when the arguments name none. Empty is a real
    /// answer — a tool taking no subject has no target, and inventing one would give every such call the
    /// same identity and make them look like a loop.</summary>
    public static string Of(string toolName, string argumentsJson)
    {
        var command = Argument(argumentsJson, "command");

        return command.Length > 0 ? Head(command) : Subject(argumentsJson);
    }

    /// <summary>One argument by name, or empty. Malformed JSON answers empty rather than throwing: an
    /// unreadable argument blob costs its call's target, never the ingest of the session around it.</summary>
    public static string Argument(string argumentsJson, string key)
    {
        if (argumentsJson.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);

            return Read(document.RootElement, key);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>A search's comparable form: lower case, punctuation gone, words sorted and de-duplicated.
    /// <para>
    /// Sorting is what makes the variant-chain detector work at all. The pattern being detected is a model
    /// re-asking one question in rearranged words — <c>graph resolved</c>, <c>resolved graph</c>,
    /// <c>GraphResolved</c> — and word ORDER is precisely the thing that varies between the attempts.
    /// </para></summary>
    public static IReadOnlySet<string> Words(string target)
    {
        var separated = new string([.. target.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ')]);

        return new HashSet<string>(
            separated.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);
    }

    /// <summary>How alike two searches are, as the share of words they share (Jaccard). Two empty sets are
    /// NOT alike — they are two calls whose subject nothing could read, and calling them identical would
    /// let a run of unparsable arguments render as a loop.</summary>
    public static double Overlap(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var shared = left.Count(right.Contains);

        return (double)shared / (left.Count + right.Count - shared);
    }

    private static string Subject(string argumentsJson)
    {
        foreach (var key in SubjectKeys)
        {
            var value = Argument(argumentsJson, key);
            if (value.Length > 0)
            {
                return Normalise(value);
            }
        }

        return string.Empty;
    }

    private static string Read(JsonElement root, string key) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>A path in its comparable form: forward slashes, no leading <c>./</c>, case preserved.
    /// <para>
    /// Case is preserved deliberately even though this machine is Windows. The repositories being measured
    /// are read on Linux CI too, and a normal form that folded case would quietly merge two files that a
    /// case-sensitive checkout keeps apart.
    /// </para></summary>
    private static string Normalise(string value)
    {
        var slashed = value.Replace('\\', '/').Trim();

        return slashed.StartsWith("./", StringComparison.Ordinal) ? slashed[2..] : slashed;
    }

    private static string Head(string command)
    {
        var words = Normalise(command)
            .Split((char[])[' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Take(CommandWords);

        return string.Join(' ', words);
    }
}
