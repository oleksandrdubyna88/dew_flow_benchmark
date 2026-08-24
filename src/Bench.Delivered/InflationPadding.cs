using System.Text;

namespace Bench.Delivered;

/// <summary>One generated file of behaviour-neutral padding.</summary>
public sealed record PaddingFile(string Path, string Content);

/// <summary>The padding an over-engineering attempt produces, generated rather than written.
///
/// <para>This is the instrument's own test subject. The question it exists to answer is whether the
/// delivered-work score can be raised by VOLUME ALONE — so the padding has to be code that adds lines and
/// cannot add behaviour, and it has to be produced the same way every time or the arm is not repeatable.</para>
///
/// <para><b>Three properties, and they are properties of this generator rather than promises about it:</b></para>
///
/// <para><b>Deterministic.</b> No model writes any of it. The same request produces the same bytes on every
/// machine, so an arm can be rebuilt and re-verified by somebody who was not there. The source's own
/// precedent was hand-written padding and therefore not reproducible, which is the flaw this fixes.</para>
///
/// <para><b>Behaviour-neutral by construction.</b> Nothing generated here reads or writes anything the real
/// change touches. Every type it names is another type it generated, which is checkable — and checked — so
/// "it cannot affect the outcome" is a property of the text rather than an assurance about it.</para>
///
/// <para><b>Not dead, either.</b> Every class is constructor-wired into the next and the whole graph hangs
/// off one root. Dead code would be dismissed in a sentence and the arm would prove nothing; the padding
/// has to be the kind a reviewer would call over-engineering rather than the kind they would call a
/// mistake.</para>
///
/// <para>What it is NOT is subtle. A reviewer stops this long before a metric sees it. The arm measures
/// whether the METRIC resists inflation, not whether the change would survive review.</para>
/// </summary>
public static class InflationPadding
{
    /// <summary>The four layers, mirroring the shape the source's precedent used so the two experiments are
    /// comparable rather than merely similar.</summary>
    private static readonly string[] Kinds =
        ["Timeout", "Refused", "Malformed", "Unreachable", "Throttled", "Conflict"];

    private static readonly string[] Policies = ["Retry", "Escalate", "Suppress", "Log"];

    private static readonly string[] Rules = ["NotEmpty", "InRange", "Known", "Ordered"];

    private static readonly string[] Concerns = ["Secrets", "Paths", "Volume"];

    /// <summary>A padded tree under one namespace. <paramref name="scale"/> repeats the whole graph, which
    /// is how a x10 arm is built: repetition of a realistic shape, not one absurdly long file.</summary>
    public static IReadOnlyList<PaddingFile> Generate(string ns, int scale)
    {
        if (scale < 1)
        {
            return [];
        }

        return
        [
            .. Enumerable.Range(1, scale).SelectMany(i => Graph($"{ns}.Gen{i:00}", $"G{i:00}")),
        ];
    }

    /// <summary>The padding as a unified diff, so it can be appended to a real one and measured by the same
    /// pipeline that measures the real thing.</summary>
    public static string AsDiff(IReadOnlyList<PaddingFile> files) =>
        string.Join('\n', files.SelectMany(file => (string[])
        [
            $"diff --git a/{file.Path} b/{file.Path}",
            "new file mode 100644",
            "--- /dev/null",
            $"+++ b/{file.Path}",
            $"@@ -0,0 +1,{file.Content.Split('\n').Length} @@",
            .. file.Content.Split('\n').Select(line => "+" + line),
        ]));

    /// <summary>Every type this generator produces, so a test can assert that the padding names nothing
    /// else. That assertion is what makes "behaviour-neutral" checkable rather than claimed.</summary>
    public static IReadOnlyList<string> TypesIn(IReadOnlyList<PaddingFile> files) =>
        [.. files.Select(f => f.Path.Split('/')[^1].Replace(".cs", string.Empty, StringComparison.Ordinal))];

    private static IEnumerable<PaddingFile> Graph(string ns, string prefix) =>
    [
        // Layer 1 — a failure taxonomy nobody raises.
        File(ns, $"{prefix}FailureKind", Interface(ns, $"{prefix}FailureKind", "string Describe();")),
        .. Kinds.Select(kind => File(ns, $"{prefix}{kind}Failure", Kind(ns, prefix, kind))),
        File(ns, $"{prefix}FailureRegistry", Registry(ns, prefix, "FailureKind", "Failure", Kinds)),

        // Layer 2 — policies and their registry, plus a collector nothing collects from.
        File(ns, $"{prefix}Policy", Interface(ns, $"{prefix}Policy", $"string Apply({prefix}FailureRegistry registry);")),
        .. Policies.Select(policy => File(ns, $"{prefix}{policy}Policy", Policy(ns, prefix, policy))),
        File(ns, $"{prefix}PolicyRegistry", Registry(ns, prefix, "Policy", "Policy", Policies)),

        // Layer 3 — a validation chain, one class per rule.
        File(ns, $"{prefix}Rule", Interface(ns, $"{prefix}Rule", "bool Holds(string value);")),
        .. Rules.Select(rule => File(ns, $"{prefix}{rule}Rule", Rule(ns, prefix, rule))),
        File(ns, $"{prefix}RuleChain", Registry(ns, prefix, "Rule", "Rule", Rules)),

        // Layer 4 — a sanitizer chain for rendering that renders nothing.
        File(ns, $"{prefix}Sanitizer", Interface(ns, $"{prefix}Sanitizer", "string Clean(string value);")),
        .. Concerns.Select(concern => File(ns, $"{prefix}{concern}Sanitizer", Sanitizer(ns, prefix, concern))),

        // The root that wires the whole graph together — this is what keeps it from being dead code.
        File(ns, $"{prefix}Root", Root(ns, prefix)),
    ];

    private static PaddingFile File(string ns, string name, string content) =>
        new($"src/{ns.Replace('.', '/')}/{name}.cs", content);

    private static string Interface(string ns, string name, string member) =>
        Header(ns) + $"public interface {name}\n{{\n    {member}\n}}\n";

    private static string Kind(string ns, string prefix, string kind) =>
        Header(ns)
        + $"public sealed class {prefix}{kind}Failure : {prefix}FailureKind\n{{\n"
        + $"    public string Name {{ get; }} = \"{kind.ToLowerInvariant()}\";\n\n"
        + $"    public int Weight {{ get; }} = {kind.Length};\n\n"
        + $"    public string Describe() => $\"{{Name}} weighted {{Weight}}\";\n}}\n";

    private static string Policy(string ns, string prefix, string policy) =>
        Header(ns)
        + $"public sealed class {prefix}{policy}Policy({prefix}FailureRegistry registry) : {prefix}Policy\n{{\n"
        + $"    public string Apply({prefix}FailureRegistry other) =>\n"
        + $"        $\"{policy.ToLowerInvariant()}:{{other.Count}}:{{registry.Count}}\";\n}}\n";

    private static string Rule(string ns, string prefix, string rule) =>
        Header(ns)
        + $"public sealed class {prefix}{rule}Rule : {prefix}Rule\n{{\n"
        + $"    public bool Holds(string value) => value.Length >= {rule.Length};\n}}\n";

    private static string Sanitizer(string ns, string prefix, string concern) =>
        Header(ns)
        + $"public sealed class {prefix}{concern}Sanitizer : {prefix}Sanitizer\n{{\n"
        + $"    public string Clean(string value) => value.Replace(\"{concern.ToLowerInvariant()}\", \"***\");\n}}\n";

    private static string Registry(string ns, string prefix, string contract, string suffix, string[] members)
    {
        var built = new StringBuilder(Header(ns));

        built.Append($"public sealed class {prefix}{suffix}Registry\n{{\n");
        built.Append($"    private readonly List<{prefix}{contract}> _all =\n    [\n");

        foreach (var member in members)
        {
            built.Append($"        new {prefix}{member}{suffix}(),\n");
        }

        built.Append("    ];\n\n");
        built.Append("    public int Count => _all.Count;\n\n");
        built.Append($"    public IReadOnlyList<{prefix}{contract}> All => _all;\n}}\n");

        return built.ToString();
    }

    /// <summary>The root. It constructs every registry and returns a string — reachable, wired, and unable
    /// to change anything the real code does, which is the whole trick.</summary>
    private static string Root(string ns, string prefix) =>
        Header(ns)
        + $"public sealed class {prefix}Root\n{{\n"
        + $"    private readonly {prefix}FailureRegistry _failures = new();\n"
        + $"    private readonly {prefix}RuleRegistry _rules = new();\n\n"
        + $"    public string Report() =>\n"
        + $"        $\"{{_failures.Count}}/{{_rules.Count}}\";\n}}\n";

    private static string Header(string ns) => $"namespace {ns};\n\n";
}
