using System.Globalization;

namespace Bench.Domain.Gate;

/// <summary>An UNAMBIGUOUS canonical form: every field is written as <c>length:value</c>, so no field's content
/// can be read as a boundary.
/// <para>
/// The first cut joined free-text fields with a separator character (<c>\u001f</c>), and a separator inside a
/// field is indistinguishable from a separator between two: epics <c>"E1␟L1"</c> with lessons <c>"X"</c> and
/// epics <c>"E1"</c> with lessons <c>"L1␟X"</c> stamped identically, and one seed whose consequence spelled a
/// second seed stamped as two seeds. A suite stamp is the identity every run is measured against, so two
/// different cases under one stamp is two populations under one label. A length prefix makes the reading
/// deterministic whatever the fields contain — nested forms included, since each nested form is itself one
/// length-prefixed field.
/// </para></summary>
internal static class CanonicalFields
{
    public static string Of(params IEnumerable<string> fields) =>
        string.Concat(fields.Select(field => $"{field.Length.ToString(CultureInfo.InvariantCulture)}:{field}"));
}

/// <summary>The rule for a path that is read INSIDE a checkout: relative to the repository root and staying
/// under it. A rooted path (<c>/x</c>, <c>\x</c>, a drive letter — <c>C:\x</c> and the drive-relative
/// <c>C:x</c> alike) names this machine; a url names somebody else's; and a <c>..</c> segment climbs out of the
/// repository the suite is about. Only a WHOLE <c>..</c> segment climbs — two dots inside a name are a name.</summary>
public static class RepositoryRelative
{
    /// <summary>Why <paramref name="path"/> is not repository-relative, or empty when it is.</summary>
    public static string Refusal(string path) =>
        (IsRooted(path), path.Contains("://", StringComparison.Ordinal), Climbs(path)) switch
        {
            (true, _, _) => $"'{path}' is an absolute path",
            (_, true, _) => $"'{path}' is a url",
            (_, _, true) => $"'{path}' climbs out of the repository with a '..' segment",
            _ => string.Empty,
        };

    private static bool IsRooted(string path) =>
        path.StartsWith('/') || path.StartsWith('\\') || path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static bool Climbs(string path) =>
        path.Split('/', '\\').Any(segment => segment == "..");
}
