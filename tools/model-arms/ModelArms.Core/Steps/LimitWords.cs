using System.Text.RegularExpressions;

namespace ModelArms.Core.Steps;

/// <summary>This module's own list of limit words (plan D6): the shell's <c>RX</c> (<c>run-arm.sh:56</c> of the part-6 experiment), character for character, as a .NET regex. It is not
/// <c>ReviewerAccountOut</c>, which refuses a plain 429 on purpose.
/// <para>
/// The one documented difference: .NET's <c>\w</c> and <c>\b</c> are Unicode where node's are ASCII — a superset, so nothing the shell matched is missed. The match timeout is the ceiling
/// every wait has; it is not caught, and on a result text or a 4000-character tail it cannot fire — if it ever does the runner sees an exception rather than a silent
/// <c>Errored</c>. <c>partial</c> only because <c>[GeneratedRegex]</c> requires it.
/// </para></summary>
public static partial class LimitWords
{
    /// <summary>The text of the first match, in its original case, or <c>""</c> when there is none. .NET's leftmost-match rule decides which alternative is reported: in
    /// <c>You've hit your individual spend limit</c> it is <c>hit your individual spend limit</c> (it starts at character 7, <c>spend limit</c> at 23); in <c>API Error: 529 Overloaded</c> it is <c>529</c>.</summary>
    public static string FirstIn(string text) => Shell().Match(text) is { Success: true } match ? match.Value : string.Empty;

    [GeneratedRegex(
        @"usage.?limit|spend limit|session limit|rate.?limit|hit your (\w+ )*limit|limit reached|limit will reset|resets? at|overloaded|\b529\b|\b429\b|too many requests",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shell();
}
