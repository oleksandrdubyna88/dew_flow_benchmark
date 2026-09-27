using System.Text.RegularExpressions;

namespace Bench.Domain.Gate;

/// <summary>One piece of text the gate would publish, and where it came from — table, column and row id — so a
/// refusal can name the exact cell a reader has to fix.</summary>
public sealed record PublishedText(string Table, string Column, string RowId, string Text);

/// <summary>Why a piece of published text was refused. <see cref="Rule"/> is a short fixed phrase; the value
/// itself is never repeated — the refusal is printed where CI logs are public, and quoting a private name back
/// would publish it in the very sentence that says it must not be. The row id is redacted for the same reason:
/// a reviewer's id is a slug somebody chose, and it can be the private name.</summary>
public sealed record GuardViolation(string Table, string Column, string RowId, string Rule)
{
    public string Describe => $"{Table}.{Column} row {RowId}: {Rule}";
}

/// <summary>The names the operator lists as private — repository and company names from the suite's
/// <c>privateNames[]</c>. Blank entries are dropped; matching is case-insensitive, because <c>Contoso</c> and
/// <c>contoso</c> are one leak.</summary>
public sealed record PrivateNames
{
    private PrivateNames(IReadOnlyList<string> names, IReadOnlyList<string> hosts)
    {
        Names = names;
        Hosts = hosts;
    }

    public IReadOnlyList<string> Names { get; }

    /// <summary>The machine names the guard refuses as whole words — at least the machine the export runs on, which
    /// is the one a campaign's rows could have picked up. Kept apart from <see cref="Names"/> because a host is a
    /// whole-word match, not a substring: <c>box-7</c> must not refuse <c>box-70</c>.</summary>
    public IReadOnlyList<string> Hosts { get; }

    public static PrivateNames None { get; } = new([], []);

    public static PrivateNames Of(IEnumerable<string> names) => new(Clean(names), []);

    public PrivateNames With(PrivateNames other) => new(Clean([.. Names, .. other.Names]), Clean([.. Hosts, .. other.Hosts]));

    public PrivateNames WithHosts(IEnumerable<string> hosts) => new(Names, Clean([.. Hosts, .. hosts]));

    private static IReadOnlyList<string> Clean(IEnumerable<string> names) =>
        [.. names.Select(n => (n ?? string.Empty).Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The 1-based position of the first private name inside <paramref name="text"/>, or zero.</summary>
    public int FirstIn(string text) =>
        Names.Select((name, i) => (name, i)).FirstOrDefault(p => text.Contains(p.name, StringComparison.OrdinalIgnoreCase)) switch
        {
            ({ } name, var i) when name.Length > 0 => i + 1,
            _ => 0,
        };
}

/// <summary>The second line of the publication guard: every piece of text the gate would publish is refused if it
/// carries a url, a machine path, or a name the operator listed as private.
/// <para>
/// The FIRST line is structural — no gate table and no gate DTO has a column that could hold a finding's text —
/// and this one is for what the structure cannot see: a failure sentence quoting a url, an artefact path that
/// somehow grew a drive letter, a reviewer id someone named after a customer. One function, called by the test
/// that re-reads every <c>gate_*</c> row AND by <c>bench gate export --public</c>, so the two can never disagree
/// about what is publishable.
/// </para>
/// <para>
/// <b>The one column checked by a stricter rule than <c>://</c>:</b> a reviewer's endpoint. A public vendor url is
/// a VALUE there by design (the variant-catalog precedent: a recipe names its vendor), so a column named in
/// <paramref name="publicUrlColumns"/> passes the url rule only when <see cref="ReviewerEndpoint.Parse"/> reads it
/// as a public vendor url — a loopback, private-range or bare-name address is still refused, and the path and
/// private-name rules apply to it like to everything else.
/// </para></summary>
public static partial class PublicationGuard
{
    public const string UrlRule = "carries a url ('://')";
    public const string DriveRule = "carries a drive path";
    public const string HomeRule = "carries a /home/ path";
    public const string UsersRule = "carries a \\Users\\ path";
    public const string EndpointRule = "is not a public vendor url";
    public const string HostRule = "carries a host name";

    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Za-z]:[\\/]")]
    private static partial Regex DrivePath { get; }

    public static IReadOnlyList<GuardViolation> Check(
        IEnumerable<PublishedText> texts, PrivateNames privateNames, IReadOnlySet<string> publicUrlColumns) =>
        [.. texts.SelectMany(text => Rules(text, privateNames, publicUrlColumns)
            .Select(rule => new GuardViolation(text.Table, text.Column, FailureRedaction.Redact(text.RowId, privateNames), rule)))];

    private static IEnumerable<string> Rules(PublishedText text, PrivateNames privateNames, IReadOnlySet<string> publicUrlColumns)
    {
        if (UrlRuleFor(text, publicUrlColumns) is { Length: > 0 } urlRule)
        {
            yield return urlRule;
        }

        foreach (var rule in PathRules(text.Text))
        {
            yield return rule;
        }

        if (privateNames.FirstIn(text.Text) is var index and > 0)
        {
            yield return $"carries private name #{index} of the suite";
        }

        if (HostNames.Carries(text.Text, privateNames.Hosts))
        {
            yield return HostRule;
        }
    }

    /// <summary>The url rule for one value. An ordinary column refuses any <c>://</c>. The endpoint column holds a
    /// public vendor url or NOTHING, so there any non-empty value that is not one is refused — with or without a
    /// scheme: <c>llm.corp.internal:8000</c> spells no <c>://</c> and is exactly the machine address the rule is for.</summary>
    private static string UrlRuleFor(PublishedText text, IReadOnlySet<string> publicUrlColumns) =>
        (publicUrlColumns.Contains($"{text.Table}.{text.Column}"), text.Text.Length > 0, text.Text.Contains("://", StringComparison.Ordinal)) switch
        {
            (true, true, _) => IsPublicVendorUrl(text.Text) ? string.Empty : EndpointRule,
            (false, _, true) => UrlRule,
            _ => string.Empty,
        };

    private static bool IsPublicVendorUrl(string text) =>
        ReviewerEndpoint.Parse(text).Match(endpoint => endpoint is ReviewerEndpoint.Value, _ => false);

    private static IEnumerable<string> PathRules(string text)
    {
        if (DrivePath.IsMatch(text))
        {
            yield return DriveRule;
        }

        if (text.Contains("/home/", StringComparison.OrdinalIgnoreCase))
        {
            yield return HomeRule;
        }

        if (text.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase) || text.Contains("/Users/", StringComparison.OrdinalIgnoreCase))
        {
            yield return UsersRule;
        }
    }
}

/// <summary>The redaction a failure cause passes before it is stored — the one free-text column the gate keeps.
/// Urls, machine paths and private names become placeholders; everything else (the call number, the status, the
/// verdict word) stays, because that is what makes the sentence worth keeping. Anything this lets through is
/// still refused by <see cref="PublicationGuard"/>; the redaction exists so an ordinary cause does not have to be.</summary>
public static partial class FailureRedaction
{
    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9+.-]*://\S*")]
    private static partial Regex Url { get; }

    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Za-z]:[\\/][^\s'""]*|(?i:/home/)[^\s'""]*|(?i:[\\/]Users[\\/])[^\s'""]*")]
    private static partial Regex MachinePath { get; }

    public const string UrlPlaceholder = "<url>";
    public const string PathPlaceholder = "<path>";
    public const string NamePlaceholder = "<private>";
    public const string HostPlaceholder = "<host>";

    public static string Redact(string? text, PrivateNames privateNames)
    {
        var withoutUrls = Url.Replace(text ?? string.Empty, UrlPlaceholder);
        var withoutPaths = MachinePath.Replace(withoutUrls, PathPlaceholder);

        var withoutNames = privateNames.Names
            .OrderByDescending(n => n.Length)
            .Aggregate(withoutPaths, (current, name) => current.Replace(name, NamePlaceholder, StringComparison.OrdinalIgnoreCase));

        return HostNames.Replace(withoutNames, privateNames.Hosts, HostPlaceholder);
    }

    public static FailureCause Redact(FailureCause cause, PrivateNames privateNames) =>
        cause with { Text = Redact(cause.Text, privateNames) };
}

/// <summary>What a machine name looks like: a KNOWN host as a whole word (case-insensitive), or the two shapes a
/// host name takes without anyone listing it — an operating system's generated name (<c>DESKTOP-4F2K9QZ</c>,
/// <c>LAPTOP-…</c>, <c>WIN-…</c>) and a private-network domain (<c>*.local</c>, <c>*.lan</c>, <c>*.internal</c>,
/// <c>*.corp</c>, <c>*.home</c>, <c>*.localdomain</c>).</summary>
public static partial class HostNames
{
    [GeneratedRegex(@"(?i)(?<![A-Za-z0-9-])(?:desktop|laptop|win)-[a-z0-9]{6,}(?![A-Za-z0-9-])|(?<![A-Za-z0-9.-])[a-z0-9-]+(?:\.[a-z0-9-]+)*\.(?:local|lan|internal|corp|home|localdomain)(?![A-Za-z0-9-])")]
    private static partial Regex Shaped { get; }

    public static bool Carries(string text, IReadOnlyList<string> known) =>
        Shaped.IsMatch(text) || known.Any(host => Known(host).IsMatch(text));

    public static string Replace(string text, IReadOnlyList<string> known, string placeholder) =>
        known.Aggregate(Shaped.Replace(text, placeholder), (current, host) => Known(host).Replace(current, placeholder));

    private static Regex Known(string host) =>
        new($"(?<![A-Za-z0-9-]){Regex.Escape(host)}(?![A-Za-z0-9-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
