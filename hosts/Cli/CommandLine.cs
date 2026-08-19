namespace Bench.Cli;

/// <summary>Flag parsing, kept deliberately dull. <c>--name value</c> and <c>--flag</c>; unknown flags
/// are refused rather than ignored, because an agent that mistypes a budget ceiling must be told, not
/// quietly given the default.</summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, string> _values;
    private readonly List<string> _operands;

    private CommandLine(string verb, Dictionary<string, string> values, List<string> operands)
    {
        Verb = verb;
        _values = values;
        _operands = operands;
    }

    public string Verb { get; }

    /// <summary>Positional words after the verb — <c>bench telemetry <b>ingest</b></c>. A sub-verb is a
    /// word, not a flag: <c>--action ingest</c> would read as configuration when it is the command.</summary>
    public string Operand(int index, string fallback = "") =>
        index >= 0 && index < _operands.Count ? _operands[index] : fallback;

    public bool Has(string name) => _values.ContainsKey(name);

    public string Value(string name, string fallback = "") =>
        _values.TryGetValue(name, out var value) ? value : fallback;

    public int Int(string name, int fallback) =>
        _values.TryGetValue(name, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    /// <summary>Parsed INVARIANT, never with the machine's culture: a threshold typed as <c>0.25</c> must
    /// mean a quarter on a machine whose decimal separator is a comma, or the same command produces two
    /// different comparisons depending on where it was run.</summary>
    public double Double(string name, double fallback) =>
        _values.TryGetValue(name, out var value)
        && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    public IReadOnlyList<string> List(string name) =>
        Value(name).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static CommandLine Parse(string[] args)
    {
        var verb = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : string.Empty;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var operands = new List<string>();
        var consumed = new HashSet<int>();

        for (var i = verb.Length > 0 ? 1 : 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                // A bare word that was not eaten as the previous flag's value is a sub-verb.
                if (!consumed.Contains(i))
                {
                    operands.Add(args[i]);
                }

                continue;
            }

            var name = args[i][2..];
            values[name] = NextValue(args, i);
            if (values[name] != "true" || (i + 1 < args.Length && args[i + 1] == "true"))
            {
                consumed.Add(i + 1);
            }
        }

        return new CommandLine(verb, values, operands);
    }

    private static string NextValue(string[] args, int index) =>
        index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[index + 1]
            : "true";
}
