namespace Bench.Cli;

/// <summary>Flag parsing, kept deliberately dull. <c>--name value</c> and <c>--flag</c>; unknown flags
/// are refused rather than ignored, because an agent that mistypes a budget ceiling must be told, not
/// quietly given the default.</summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, string> _values;

    private CommandLine(string verb, Dictionary<string, string> values)
    {
        Verb = verb;
        _values = values;
    }

    public string Verb { get; }

    public bool Has(string name) => _values.ContainsKey(name);

    public string Value(string name, string fallback = "") =>
        _values.TryGetValue(name, out var value) ? value : fallback;

    public int Int(string name, int fallback) =>
        _values.TryGetValue(name, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    public IReadOnlyList<string> List(string name) =>
        Value(name).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static CommandLine Parse(string[] args)
    {
        var verb = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : string.Empty;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = verb.Length > 0 ? 1 : 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = args[i][2..];
            values[name] = NextValue(args, i);
        }

        return new CommandLine(verb, values);
    }

    private static string NextValue(string[] args, int index) =>
        index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[index + 1]
            : "true";
}
