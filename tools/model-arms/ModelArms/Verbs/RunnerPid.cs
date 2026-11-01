using System.Globalization;

namespace ModelArms.Verbs;

/// <summary>The one reading of <c>pids/&lt;arm&gt;.pid</c>: the decimal text of a process id and nothing else — digits only, more than zero, a whole <see cref="int"/>. <c>status</c> and <c>checks</c> both ask it (<c>common.reuse-first</c>); the runner writes it itself (E5.S1, D13: <c>RunArmAdmission</c> after its two locks), as this format, and liveness is asked of the runner's lock (<c>RunnerLiveness</c>), never of the pid — which <c>status</c> and <c>checks</c> keep printing and testing as E3 built them (a reused pid reads alive there: the safe direction).</summary>
internal static class RunnerPid
{
    /// <summary>The pid <paramref name="text"/> (already trimmed) names, or <c>null</c> when it is no pid.</summary>
    public static int? Parse(string text) =>
        text.Length > 0 && text.All(char.IsAsciiDigit) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0 ? pid : null;
}
