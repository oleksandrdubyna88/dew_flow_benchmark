using System.Globalization;
using System.Text;
using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>One launch of the access check, as the summary prints it: the agent, what it cost and how long it took — never a path.</summary>
public sealed record AccessLaunch(string Agent, CapturedUsd CostUsd, CapturedCount DurationMs)
{
    /// <summary>The main session's model fallbacks (<see cref="ScoutAccessCheck.ModelFallbacks"/>) — a session that answered on another model
    /// than the one launched is said so, in the summary and on the console.</summary>
    public IReadOnlyList<string> Fallbacks { get; init; } = [];

    public string Line => $"{Agent}: {Cost(CostUsd)}, {Duration(DurationMs)}{(Fallbacks.Count == 0 ? string.Empty : $", the main session fell back {string.Join("; ", Fallbacks)}")}";

    private static string Cost(CapturedUsd usd) => usd.WasCaptured ? string.Create(CultureInfo.InvariantCulture, $"${usd.Value}") : "cost not captured";

    private static string Duration(CapturedCount ms) => ms.WasCaptured ? string.Create(CultureInfo.InvariantCulture, $"{ms.Value / 1000.0:0.0} s") : "duration not captured";
}

/// <summary>What the Markdown summary of <c>bench scouts access-check</c> is written from. Every string here may be published (plan §3.13):
/// the pins are hashes, the targets are their NAMES, a granted directory is <c>grant[i]</c>.</summary>
public sealed record AccessReportInputs(
    DateTimeOffset At,
    string CliVersion,
    string PluginCacheHash,
    string SettingsHash,
    IReadOnlyList<AccessTarget> Targets,
    IReadOnlyList<AccessLaunch> Launches,
    IReadOnlyList<AccessVerdict> Verdicts);

/// <summary>The summary <c>--out</c> receives: the pins, the launch's shape (flag names, no values), one table target × agent, the conclusion.</summary>
public static class ScoutAccessReport
{
    public const string LaunchShape = "--setting-sources \"\" --settings <pinned-settings.json> --plugin-dir <pinned feature-dev> beside the §3.4 scout launch (the allow-list, MCP off, the forwarded children, the cap)";

    public static string Markdown(AccessReportInputs inputs)
    {
        var agents = inputs.Launches.Select(l => l.Agent).ToList();
        var text = new StringBuilder();

        text.Append("# Scout access check — ").Append(inputs.At.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)).Append("\n\n");
        text.Append("- claude ").Append(inputs.CliVersion).Append(" · plugin cache ").Append(Short(inputs.PluginCacheHash)).Append(" · pinned settings ").Append(Short(inputs.SettingsHash)).Append('\n');
        text.Append("- launch: ").Append(LaunchShape).Append('\n');
        text.Append("- targets: ").Append(string.Join(", ", inputs.Targets.Select(t => t.Testable ? t.Name : $"{t.Name} (not tested — no small text file to ask for)"))).Append('\n');
        text.Append("- granted directories: ").Append(inputs.Targets.Count(t => t.Kind == AccessTargetKind.Grant).ToString(CultureInfo.InvariantCulture)).Append(" (named by index, never by path)\n\n");
        text.Append("| target | ").Append(string.Join(" | ", agents)).Append(" |\n|---|").Append(string.Concat(agents.Select(_ => "---|"))).Append('\n');
        text.Append(string.Concat(inputs.Targets.Where(t => t.Testable).Select(t => Row(t, agents, inputs.Verdicts)))).Append('\n');
        text.Append(string.Concat(inputs.Launches.Select(l => $"- {l.Line}\n")));
        text.Append('\n').Append(Conclusion(ScoutAccessCheck.Conclude(inputs.Verdicts))).Append('\n');
        return text.ToString();
    }

    /// <summary>One table row: the target, then its verdict under each agent — <c>not launched</c> where that launch never happened.</summary>
    private static string Row(AccessTarget target, IReadOnlyList<string> agents, IReadOnlyList<AccessVerdict> verdicts) =>
        $"| {target.Name} | {string.Join(" | ", agents.Select(agent => verdicts.FirstOrDefault(v => v.Target == target.Name && v.Agent == agent) is { } verdict ? Cell(verdict) : "not launched"))} |\n";

    /// <summary>The one sentence the console ends with and the summary ends with.</summary>
    public static string Conclusion(AccessConclusion conclusion) =>
        (conclusion.Leaks.Count > 0, conclusion.InconclusiveTargets.Count > 0) switch
        {
            (true, _) => "CONTENT RETURNED — " + string.Join("; ", conclusion.Leaks.Select(l => $"{l.Target} by {l.Agent} ({string.Join(", ", l.Calls.Where(c => c.Finding == AccessFinding.Content).Select(c => c.Tool))})"))
                         + " — the boundary is broken; do not run the pilot under this launch",
            (_, true) => "inconclusive — " + string.Join(", ", conclusion.InconclusiveTargets) + ": not denied for every agent (no attempt, or no readable result, from at least one); nothing is concluded about them",
            _ => "every target denied for every agent — the launch drops the grant",
        };

    private static string Cell(AccessVerdict verdict) =>
        verdict.Calls.Count == 0
            ? ScoutAccessCheck.Word(verdict.Finding)
            : $"{ScoutAccessCheck.Word(verdict.Finding)} — {string.Join(", ", verdict.Calls.Select(c => $"{c.Tool} {ScoutAccessCheck.Word(c.Finding)}"))}";

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;
}
