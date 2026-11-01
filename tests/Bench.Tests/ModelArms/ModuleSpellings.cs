using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelArms.Adapters;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;

namespace Bench.Tests.ModelArms;

/// <summary>Every path the module spells (E6.S1, the other half of <see cref="ShellLayout"/>): the public members of <see cref="RootPaths"/> and <see cref="ArmPaths"/> rendered by REFLECTION with a placeholder for each argument (<c>&lt;step&gt;</c>, <c>&lt;id&gt;</c>, <c>&lt;stamp&gt;</c>… from the parameter's own name; a number by a sentinel replaced by <c>&lt;n&gt;</c> or
/// <c>&lt;i&gt;</c>; a <see cref="RunStage"/> by each of its three values), the gate holder's note beside the gate lock, and the names the worked example binds - the template files of its steps, the notes and work files of their outputs, the files <c>baseline</c> and <c>scenarios</c> name, the suite's CTRF file. All relative to the experiment
/// root, forward slashes. A member added to either type is in this list the moment it exists: a list typed by hand would be the second copy the scan is there to catch.
/// <para>
/// What this does NOT see: a path spelled inside the module without these two types or the example (a staging name a death leaves, E5 §8 item 54; the launcher's own files); the MODEL's files, which no brief or binding of the module spells.
/// </para></summary>
internal static class ModuleSpellings
{
    private const string Marker = "<root>";
    private const string Token = "<token>";
    private const int Sentinel = 7;
    private const string Number = @"\d+";

    private static readonly Dictionary<string, string> Named = new(StringComparer.Ordinal)
    {
        ["n"] = Number,
        ["i"] = Number,
        ["step"] = @"(?:<step>|\d[0-9A-Za-z]*-[^/]+)",
        ["id"] = @"(?:<id>|t\d+)",
        ["arm"] = @"(?:<arm>|[A-Z]+\d+)",
        ["token"] = "(?:<token>|[0-9a-f]{16})",
    };

    public static IReadOnlyList<string> All { get; } = Build();

    /// <summary>The regular expression a concrete path (relative to the root) must match to be an instance of <paramref name="spelling"/>. A placeholder stands for the literal placeholder (a shell name the scan normalised) or for a name of its own shape - a step starts with a digit (<c>2-hA</c>), a task is <c>t&lt;n&gt;</c>, an
    /// arm is capitals and a number, a token is 16 hex digits - so <c>&lt;step&gt;.diff</c> does not read <c>task-&lt;id&gt;-haiku.diff</c> as a step's; <c>&lt;n&gt;</c> and <c>&lt;i&gt;</c> are a number and any other placeholder one name.</summary>
    public static Regex PatternOf(string spelling) =>
        new("^" + Regex.Replace(Regex.Escape(spelling), "<(?<name>[a-z]+)>", each => Shape(each.Groups["name"].Value)) + "$", RegexOptions.CultureInvariant);

    private static string Shape(string placeholder) => Named.TryGetValue(placeholder, out var shape) ? shape : "[^/]+";

    private static List<string> Build()
    {
        var root = new RootPaths(Marker);
        var spelled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Members(root).Concat(Members(root.Arm(Token))).Concat(FromExample(root)))
        {
            if (path.StartsWith(Marker + "/", StringComparison.Ordinal))
            {
                spelled.Add(path[(Marker.Length + 1)..].Replace('\\', '/'));
            }
        }

        spelled.Add(Relative(GateHolderNote.PathFor(root.GateLock)));
        return [.. spelled.Order(StringComparer.Ordinal)];
    }

    private static string Relative(string path) => path[(Marker.Length + 1)..];

    private static IEnumerable<string> Members(object target)
    {
        var type = target.GetType();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(each => each.PropertyType == typeof(string) && each.GetIndexParameters().Length == 0))
        {
            yield return (string)property.GetValue(target)!;
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(each => each.ReturnType == typeof(string) && !each.IsSpecialName && each.Name != nameof(ToString)))
        {
            foreach (var rendered in Rendered(target, method))
            {
                yield return rendered;
            }
        }
    }

    private static IEnumerable<string> Rendered(object target, MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Any(each => each.ParameterType != typeof(string) && each.ParameterType != typeof(int) && each.ParameterType != typeof(RunStage)))
        {
            yield break;
        }

        foreach (var stage in parameters.Any(each => each.ParameterType == typeof(RunStage)) ? Enum.GetValues<RunStage>().Cast<object?>() : [null])
        {
            var arguments = parameters.Select(each => each.ParameterType == typeof(string) ? $"<{each.Name}>" : each.ParameterType == typeof(int) ? (object)Sentinel : stage!).ToArray();
            var text = (string)method.Invoke(target, arguments)!;
            foreach (var each in parameters.Where(each => each.ParameterType == typeof(int)))
            {
                text = text.Replace(Sentinel.ToString(CultureInfo.InvariantCulture), $"<{each.Name}>", StringComparison.Ordinal);
            }

            yield return text;
        }
    }

    /// <summary>The names the worked example binds: its steps' template stems, their outputs, the baseline and scenarios files, the suite's CTRF file under the objective folder.</summary>
    private static IEnumerable<string> FromExample(RootPaths root)
    {
        var example = JsonNode.Parse(File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json")), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!;
        yield return root.Of(example["baseline"]!.GetValue<string>()).Match(path => path, reason => throw new InvalidOperationException(reason));
        yield return root.Of(example["scenarios"]!.GetValue<string>()).Match(path => path, reason => throw new InvalidOperationException(reason));
        yield return Path.Combine(root.Arm(Token).ObjectiveFolder, example["target"]!["suite"]!["ctrfFile"]!.GetValue<string>());
        foreach (var step in example["steps"]!.AsObject().Select(each => each.Value!))
        {
            yield return Path.Combine(root.Briefs, BriefTemplates.NameOf(step["brief"]!.GetValue<string>()).Match(name => name, reason => throw new InvalidOperationException(reason)));
            foreach (var output in step["outputs"]?.AsObject() ?? [])
            {
                yield return root.Arm(Token).NotesFile(OutputBinding.Parse(output.Key, output.Value!.GetValue<string>()).Match(binding => binding, reason => throw new InvalidOperationException(reason)));
            }
        }
    }
}
