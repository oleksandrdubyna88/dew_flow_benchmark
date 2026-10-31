using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>One command line, split: the verb, the words that belong to the verb, and what is left for the shared flag parser, <c>verb</c> first.</summary>
public sealed record VerbLine(string Verb, IReadOnlyList<string> Positionals, string[] Rest);

/// <summary>D20: <c>model-arms</c>'s command line is parsed in two halves. This is the first and it is pure: the verb and exactly its positional
/// words, by a per-verb table. The rest goes, unchanged, to <c>Bench.Cli.CommandLine.Parse</c>, which stays flags-only and stays what every
/// <c>bench</c> verb's parse is tested as.</summary>
public static class ModelArmsCommandLine
{
    private const string ExperimentFlag = "--experiment";

    private sealed record Shape(string Verb, int Min, int Max, string Usage, string Takes, IReadOnlyList<string> Legal, IReadOnlyList<string> Flags);

    /// <summary>Each verb names the flags it takes (the parent plan's §4.1 table and the E2 spec): <c>--experiment</c> for every verb that reads an
    /// experiment, <c>--seconds</c> for <c>gate-lock hold</c>, <c>--parse-only</c> for <c>checks</c>. Any other flag is refused, so a misspelt
    /// <c>--experimnt</c> is not silently dropped while the root falls back to the environment.</summary>
    private static readonly Shape[] Table =
    [
        Fixed("init", 1, "init <root>", "exactly one argument, the root folder"),
        Fixed("validate", 0, "validate", "no positional argument", ExperimentFlag),
        Fixed("prep-base", 0, "prep-base", "no positional argument", ExperimentFlag),
        Fixed("make", 0, "make", "no positional argument", ExperimentFlag),
        Fixed("lock", 1, "lock <run>", "exactly one argument, the run", ExperimentFlag),
        Fixed("unlock", 0, "unlock [--force]", "no positional argument", ExperimentFlag, "--force"),
        Fixed("run-arm", 1, "run-arm <arm>", "exactly one argument, the arm", ExperimentFlag),

        // --seconds: not named for run-all by any spec yet (E4.S3 specifies the verb); allowed because the command line's own first scenario passes it.
        new("run-all", 1, int.MaxValue, "run-all <arm>...", "one or more arms", [], [ExperimentFlag, "--seconds"]),
        Fixed("chain", 0, "chain", "no positional argument", ExperimentFlag),
        Fixed("checks", 1, "checks <arm>", "exactly one argument, the arm", ExperimentFlag, "--parse-only"),
        Fixed("status", 0, "status", "no positional argument", ExperimentFlag),
        new("gate-lock", 1, 1, "gate-lock status|hold", "exactly one argument, status or hold", ["status", "hold"], [ExperimentFlag, "--seconds"]),
        Fixed("version", 0, "version", "no positional argument"),
        Fixed("help", 0, "help", "no positional argument"),
    ];

    /// <summary>The verbs, in the order the help prints them.</summary>
    public static IReadOnlyList<string> Verbs { get; } = [.. Table.Select(shape => shape.Verb)];

    /// <summary>One usage per verb, in the order of <see cref="Verbs"/>: <c>run-arm &lt;arm&gt;</c>.</summary>
    public static IReadOnlyList<string> Usages { get; } = [.. Table.Select(shape => shape.Usage)];

    /// <summary>The verb and its words, or why the line is refused. No arguments is the help. The words are the consecutive tokens after the verb that
    /// do not start with <c>--</c>; their count is checked against the verb's shape. After them every <c>--flag</c> may be followed by ONE bare token,
    /// its value (the shared parser's own rule); any other bare token is refused. <see cref="VerbLine.Rest"/> is the verb and the flag tokens.</summary>
    public static Outcome<VerbLine> Split(string[] args) =>
        args.Length == 0
            ? Outcome<VerbLine>.Success(new VerbLine("help", [], ["help"]))
            : ShapeOf(args[0]).Match(shape => Take(args, shape), Outcome<VerbLine>.Failure);

    private static Outcome<Shape> ShapeOf(string verb) =>
        Table.FirstOrDefault(shape => shape.Verb == verb) is { } found
            ? Outcome<Shape>.Success(found)
            : Outcome<Shape>.Failure($"'{verb}' is not a verb — model-arms takes: {string.Join(", ", Verbs)}");

    private static Outcome<VerbLine> Take(string[] args, Shape shape)
    {
        IReadOnlyList<string> positionals = [.. args.Skip(1).TakeWhile(arg => !IsFlag(arg))];
        var problem = WordsProblem(shape, positionals);
        if (problem.Length > 0)
        {
            return Outcome<VerbLine>.Failure(problem);
        }

        var flags = args[(1 + positionals.Count)..];
        var refusal = FlagsProblem(shape, flags);
        return refusal.Length == 0
            ? Outcome<VerbLine>.Success(new VerbLine(shape.Verb, positionals, [shape.Verb, .. flags]))
            : Outcome<VerbLine>.Failure(refusal);
    }

    /// <summary>The refusal of the tokens after the words, or the empty string: a bare token that is no flag's value, then a flag the verb does not take.</summary>
    private static string FlagsProblem(Shape shape, string[] flags)
    {
        var stray = StrayAt(flags, 0);
        var unknown = flags.FirstOrDefault(flag => IsFlag(flag) && !shape.Flags.Contains(flag));
        return (stray, unknown) switch
        {
            ( >= 0, _) => $"'{flags[stray]}' is not a flag or a value — {Refusal(shape)}",
            (_, { }) => $"'{unknown}' is not a flag of {shape.Verb} — {shape.Verb} takes {FlagsText(shape)}: model-arms {shape.Usage}",
            _ => string.Empty,
        };
    }

    private static string FlagsText(Shape shape) =>
        shape.Flags.Count == 0 ? "no flags" : $"the flags {string.Join(", ", shape.Flags)}";

    /// <summary>The refusal of the words themselves, or the empty string: how many there are, and for a verb with a closed set of words, which one.</summary>
    private static string WordsProblem(Shape shape, IReadOnlyList<string> positionals) =>
        !Fits(shape, positionals.Count) ? Refusal(shape)
        : IsOutsideLegal(shape, positionals) ? $"'{positionals[0]}' is not a {shape.Verb} action — {string.Join(" or ", shape.Legal)}"
        : string.Empty;

    private static bool Fits(Shape shape, int count) => count >= shape.Min && count <= shape.Max;

    private static bool IsOutsideLegal(Shape shape, IReadOnlyList<string> positionals) =>
        shape.Legal.Count > 0 && !shape.Legal.Contains(positionals[0]);

    private static string Refusal(Shape shape) => $"{shape.Verb} takes {shape.Takes}: model-arms {shape.Usage}";

    /// <summary>The index of the first bare token that is neither a flag nor a flag's value; -1 when there is none.</summary>
    private static int StrayAt(string[] flags, int index) =>
        index switch
        {
            _ when index >= flags.Length => -1,
            _ when !IsFlag(flags[index]) => index,
            _ => StrayAt(flags, index + Step(flags, index)),
        };

    /// <summary>How many tokens the flag at <paramref name="index"/> takes: itself, and the bare token after it as its value.</summary>
    private static int Step(string[] flags, int index) =>
        index + 1 < flags.Length && !IsFlag(flags[index + 1]) ? 2 : 1;

    private static bool IsFlag(string token) => token.StartsWith("--", StringComparison.Ordinal);

    private static Shape Fixed(string verb, int count, string usage, string takes, params string[] flags) =>
        new(verb, count, count, usage, takes, [], flags);
}
