using System.Text.RegularExpressions;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>What a filled brief may not say. The map knows every arm name and token; <paramref name="OwnArm"/> is the arm
/// the brief is for (its own token is its working directory, and every path field carries it); <paramref name="Schemes"/>
/// are the scheme names of the experiment. <paramref name="Root"/> is the experiment root, exactly as the path fields spell
/// it: a value that begins with it is checked without it, because the operator's own folder is not what the guard guards
/// (on a Windows root such as <c>C:\exp</c> the drive letter would otherwise be a scheme named <c>C</c>). Empty: no exemption.</summary>
public sealed record BriefLeakGuard(ArmMap Map, string OwnArm, IReadOnlyCollection<string> Schemes, string Root = "");

/// <summary>Fills a brief template: every <c>{{NAME}}</c> becomes the field of that name, and a brief that would reach a
/// model with a placeholder still in it, or with a name that tells the arm what it is, is refused instead.</summary>
public static class BriefFiller
{
    private static readonly Regex Placeholder = new(@"\{\{([A-Z_]+)\}\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Replaces each placeholder whose name is a field, in one pass (a value is never expanded again). Then, in this
    /// order, it refuses: a placeholder with no field, naming each once; a template that itself holds an arm name, a scheme
    /// name or another arm's token (an experiment root's override is the operator's text), naming <paramref name="templateName"/>;
    /// a SUBSTITUTED value that does (the guard's root prefix excepted), naming the field; and a result that still holds a
    /// placeholder, which only a value can have carried. A field the template never uses is not checked.</summary>
    public static Outcome<string> Fill(string template, IReadOnlyDictionary<string, string> fields, BriefLeakGuard guard, string templateName = "")
    {
        var names = Placeholder.Matches(template).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
        var unfilled = names.Where(name => !fields.ContainsKey(name)).ToList();
        if (unfilled.Count > 0)
        {
            return Outcome<string>.Failure($"the brief has unfilled fields: {string.Join(' ', unfilled.Select(name => "{{" + name + "}}"))}");
        }

        var refusal = TemplateLeak(template, guard, templateName) is { Length: > 0 } inTemplate ? inTemplate : ValueLeak(names, fields, guard);
        return refusal.Length > 0 ? Outcome<string>.Failure(refusal) : Replaced(template, fields);
    }

    private static Outcome<string> Replaced(string template, IReadOnlyDictionary<string, string> fields)
    {
        var filled = Placeholder.Replace(template, match => fields[match.Groups[1].Value]);
        var carried = Placeholder.Matches(filled).Select(match => match.Value).Distinct(StringComparer.Ordinal).ToList();
        return carried.Count == 0
            ? Outcome<string>.Success(filled)
            : Outcome<string>.Failure($"the filled brief still holds {string.Join(' ', carried)} — a field value must not carry a placeholder, and a value is never expanded again");
    }

    private static string TemplateLeak(string template, BriefLeakGuard guard, string templateName) =>
        guard.Map.Leaks(template, guard.OwnArm, guard.Schemes) is { Count: > 0 } leaks
            ? $"the template{(templateName.Length > 0 ? $" '{templateName}'" : string.Empty)} would carry {Quoted(leaks)} into a brief — {Rule}"
            : string.Empty;

    /// <summary>The refusal for the first substituted field that would carry a name, or empty.</summary>
    private static string ValueLeak(IReadOnlyList<string> names, IReadOnlyDictionary<string, string> fields, BriefLeakGuard guard) =>
        names.Select(name => (Name: name, Leaks: guard.Map.Leaks(WithoutRoot(fields[name], guard.Root), guard.OwnArm, guard.Schemes)))
            .Where(field => field.Leaks.Count > 0)
            .Select(field => $"the field {field.Name} would carry {Quoted(field.Leaks)} into a brief — {Rule}")
            .FirstOrDefault() ?? string.Empty;

    private const string Rule = "no brief names an arm, a scheme or another arm's token (D15)";

    private static string Quoted(IReadOnlyList<string> leaks) => string.Join(", ", leaks.Select(leak => $"'{leak}'"));

    private static string WithoutRoot(string value, string root) =>
        root.Length > 0 && value.StartsWith(root, StringComparison.Ordinal) ? value[root.Length..] : value;
}
