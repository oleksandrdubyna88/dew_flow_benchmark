using System.Globalization;

namespace ModelArms.Verbs;

/// <summary>The one reading of <c>pids/&lt;arm&gt;.pid</c>: the decimal text of a process id and nothing else — digits only, more than zero, a whole <see cref="int"/>. <c>status</c> and <c>checks</c> both ask it (<c>common.reuse-first</c>); nothing writes a pid file until E5.S3's <c>run-all</c>, so this is the format the story fixes, not one E4 has settled.</summary>
internal static class RunnerPid
{
    /// <summary>The pid <paramref name="text"/> (already trimmed) names, or <c>null</c> when it is no pid.</summary>
    public static int? Parse(string text) =>
        text.Length > 0 && text.All(char.IsAsciiDigit) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0 ? pid : null;
}
