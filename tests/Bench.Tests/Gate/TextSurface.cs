using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Tests.Gate;

/// <summary>Every place a type graph can carry TEXT, found by walking it — the structural half of the
/// publication guard.
/// <para>
/// The first version of this check looked at the top-level <c>string</c> properties of types whose NAME matched
/// <c>Gate*Dto</c>, and let a property through when its NAME was on a list. Each of those three choices was a
/// door: a nested record (<c>FindingNote(string Title)</c>) is not a <c>Gate*Dto</c>, a <c>List&lt;string&gt;</c>
/// is not <c>typeof(string)</c>, and an allowed name (<c>FailureText</c>) is allowed on EVERY type. This walk
/// closes all three: it follows every property into every non-system type it reaches, it treats a collection or
/// dictionary of strings as text and <c>object</c> / <c>JsonElement</c> / <c>JsonNode</c> as opaque (either can
/// hold a sentence), and it keys the allow-list by <c>Type.Property</c>.
/// </para></summary>
internal static class TextSurface
{
    /// <summary>Every <c>Type.Property</c> reachable from <paramref name="roots"/> that can carry text or an
    /// opaque value and is not in <paramref name="allowed"/>, with the reason in brackets.</summary>
    public static IReadOnlyList<string> Offenders(IEnumerable<Type> roots, IReadOnlySet<string> allowed) =>
        [.. Carriers(roots)
            .Where(c => !allowed.Contains(c.Key))
            .Select(c => $"{c.Key} ({c.Why})")
            .Order(StringComparer.Ordinal)];

    /// <summary>Every <c>Type.Property</c> that carries text, allowed or not — so an allow-list entry naming a
    /// property that no longer exists can be found and removed rather than kept forever.</summary>
    public static IReadOnlyList<(string Key, string Why)> Carriers(IEnumerable<Type> roots) =>
        [.. Reachable(roots).SelectMany(type => Properties(type)
            .Select(p => (Key: $"{type.Name}.{p.Name}", Why: Carries(p.PropertyType)))
            .Where(c => c.Why.Length > 0))];

    /// <summary>Every type the walk visits, roots included.</summary>
    public static IReadOnlyList<Type> Reachable(IEnumerable<Type> roots)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>(roots);

        while (queue.TryDequeue(out var type))
        {
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (var next in Properties(type).SelectMany(p => Composites(p.PropertyType)))
            {
                queue.Enqueue(next);
            }
        }

        return [.. seen];
    }

    private static IEnumerable<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0);

    /// <summary>Why a property of this type can carry text: <c>string</c>, <c>opaque</c>, or empty.</summary>
    private static string Carries(Type type) => Parts(type) switch
    {
        var parts when parts.Any(IsString) => "string",
        var parts when parts.Any(IsOpaque) => "opaque",
        _ => string.Empty,
    };

    /// <summary>The composite (walkable) types a property of this type leads to.</summary>
    private static IEnumerable<Type> Composites(Type type) => Parts(type).Where(IsComposite);

    /// <summary>The leaf types a value of <paramref name="type"/> is made of: itself, or the element types of a
    /// nullable, an array, a sequence or a dictionary (key AND value).</summary>
    private static IReadOnlyList<Type> Parts(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        return unwrapped == typeof(string) || IsOpaque(unwrapped)
            ? [unwrapped]
            : Elements(unwrapped);
    }

    private static IReadOnlyList<Type> Elements(Type type)
    {
        var element = type.IsArray ? type.GetElementType() : SequenceElement(type);

        return element is null ? [type] : [.. KeyValue(element).SelectMany(Parts)];
    }

    private static Type? SequenceElement(Type type) =>
        new[] { type }.Concat(type.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();

    private static IReadOnlyList<Type> KeyValue(Type element) =>
        element.IsGenericType && element.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
            ? element.GetGenericArguments()
            : [element];

    private static bool IsString(Type type) => type == typeof(string) || type == typeof(char);

    private static bool IsOpaque(Type type) =>
        type == typeof(object) || type == typeof(JsonElement) || type == typeof(JsonDocument) || typeof(JsonNode).IsAssignableFrom(type);

    private static bool IsComposite(Type type) =>
        !type.IsPrimitive && !type.IsEnum && !IsString(type) && !IsOpaque(type)
        && !(type.Namespace ?? string.Empty).StartsWith("System", StringComparison.Ordinal);
}
