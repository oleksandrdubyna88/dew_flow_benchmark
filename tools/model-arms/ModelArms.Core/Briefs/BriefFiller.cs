using System.Text.RegularExpressions;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>What a filled brief may not say. The map knows every arm name and token; <paramref name="OwnArm"/> is the arm
/// the brief is for (its own token is its working directory, and every path field carries it); <paramref name="Schemes"/>
/// are the scheme names of the experiment.</summary>
public sealed record BriefLeakGuard(ArmMap Map, string OwnArm, IReadOnlyCollection<string> Schemes);

/// <summary>Fills a brief template: every <c>{{NAME}}</c> becomes the field of that name, and a brief that would reach a
/// model with a placeholder still in it, or with a name that tells the arm what it is, is refused instead.</summary>
public static class BriefFiller
{
    private static readonly Regex Placeholder = new(@"\{\{([A-Z_]+)\}\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Replaces each placeholder whose name is in <paramref name="fields"/>, in one pass (a value that itself holds
    /// <c>{{X}}</c> is not scanned again). Then, in this order: a placeholder with no field is refused naming each one once;
    /// a SUBSTITUTED value that <see cref="ArmMap.Leaks"/> finds an arm name, a scheme name or another arm's token in is
    /// refused naming the field and what it would carry. A field the template never uses is not checked.</summary>
    public static Outcome<string> Fill(string template, IReadOnlyDictionary<string, string> fields, BriefLeakGuard guard)
    {
        var names = Placeholder.Matches(template).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
        var unfilled = names.Where(name => !fields.ContainsKey(name)).ToList();
        return unfilled.Count > 0
            ? Outcome<string>.Failure($"the brief has unfilled fields: {string.Join(' ', unfilled.Select(name => "{{" + name + "}}"))}")
            : Leak(names, fields, guard) is { Length: > 0 } leak
                ? Outcome<string>.Failure(leak)
                : Outcome<string>.Success(Placeholder.Replace(template, match => fields[match.Groups[1].Value]));
    }

    /// <summary>The refusal for the first substituted field that would carry a name, or empty.</summary>
    private static string Leak(IReadOnlyList<string> names, IReadOnlyDictionary<string, string> fields, BriefLeakGuard guard) =>
        names.Select(name => (Name: name, Leaks: guard.Map.Leaks(fields[name], guard.OwnArm, guard.Schemes)))
            .Where(field => field.Leaks.Count > 0)
            .Select(field => $"the field {field.Name} would carry {string.Join(", ", field.Leaks.Select(leak => $"'{leak}'"))} into a brief — no brief names an arm, a scheme or another arm's token (D15)")
            .FirstOrDefault() ?? string.Empty;
}
