using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Targets;

namespace ModelArms.Core.Experiment;

/// <summary>The <c>target</c> object: the repository the arms work on and every command the objective checks run.</summary>
internal static class TargetSection
{
    private static readonly string[] Members =
        ["source", "planBranch", "base", "plan", "branch", "rulesSubmodule", "lowPriority", "build", "suite", "format", "extension", "nodeModules", "testGlobs"];

    private static readonly string[] SuiteMembers = ["command", "ctrfFile", "namePrefix"];
    private static readonly string[] FormatMembers = ["solution", "verbs"];
    private static readonly string[] ExtensionMembers = ["directory", "command"];

    public static TargetFacts Read(StrictJson json, JsonObject top, ExperimentLocation location)
    {
        var target = json.ObjectMember(top, string.Empty, "target", Members, Members);
        var source = PathFields.Absolute(json, target, "target", "source");
        var forbidden = ExecutableRule.ForbiddenRoots(source, location.Root);

        return new TargetFacts(
            source,
            json.Text(target, "target", "planBranch"),
            json.Sha(target, "target", "base"),
            PathFields.Relative(json, target, "target", "plan", "the target"),
            json.Text(target, "target", "branch"),
            PathFields.Relative(json, target, "target", "rulesSubmodule", "the target"),
            Command(json, target, "target", "lowPriority", forbidden),
            Command(json, target, "target", "build", forbidden),
            Suite(json, target, forbidden),
            Format(json, target),
            Extension(json, target, forbidden),
            PathFields.Relatives(json, target, "target", "nodeModules", "the target"),
            PathFields.Relatives(json, target, "target", "testGlobs", "the target"));
    }

    /// <summary>A command: an executable and its arguments, the executable checked against D17.</summary>
    internal static IReadOnlyList<string> Command(StrictJson json, JsonObject obj, string path, string name, IReadOnlyList<string> forbidden)
    {
        var argv = json.Texts(obj, path, name, atLeastOne: true);
        var refusal = argv.Count > 0 && argv[0].Length > 0 ? ExecutableRule.Refusal(argv[0], forbidden) : string.Empty;
        if (refusal.Length > 0)
        {
            json.Add($"{StrictJson.Join(path, name)}[0]: {refusal}");
        }

        return argv;
    }

    private static SuiteFacts Suite(StrictJson json, JsonObject target, IReadOnlyList<string> forbidden)
    {
        var suite = json.ObjectMember(target, "target", "suite", SuiteMembers, SuiteMembers);
        return new SuiteFacts(
            Command(json, suite, "target.suite", "command", forbidden),
            json.Text(suite, "target.suite", "ctrfFile"),
            json.Text(suite, "target.suite", "namePrefix"));
    }

    private static FormatFacts Format(StrictJson json, JsonObject target)
    {
        var format = json.ObjectMember(target, "target", "format", FormatMembers, FormatMembers);
        return new FormatFacts(
            PathFields.Relative(json, format, "target.format", "solution", "the target"),
            json.Texts(format, "target.format", "verbs"));
    }

    private static ExtensionFacts Extension(StrictJson json, JsonObject target, IReadOnlyList<string> forbidden)
    {
        var extension = json.ObjectMember(target, "target", "extension", ExtensionMembers, ExtensionMembers);
        return new ExtensionFacts(
            PathFields.Relative(json, extension, "target.extension", "directory", "the target"),
            Command(json, extension, "target.extension", "command", forbidden));
    }
}
