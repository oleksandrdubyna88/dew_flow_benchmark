using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>Fills, once and with representative facts, every brief template the schemes use at every position they use it, through <see cref="BriefFiller"/> and its leak guard.
/// A template that could not be filled at the position a scheme places it — a field nothing earlier provides, a KNOWN text the guard would refuse — is found by <c>validate</c>,
/// never at a step's launch hours into the chain. The fields are <see cref="BriefFields.For"/> over <see cref="SchemePositions.Visible"/>, the rule the runner fills by.
/// <para>
/// The facts are the experiment's own (plan, base, branch, KNOWN) with a stand-in for what only a run knows: the arms are the ones the runs name (and an
/// <c>&lt;scheme&gt;1</c> for a scheme no run uses), each with a token minted here, and every brief of a scheme is filled for its first arm. Not proved:
/// that a value a run computes later (a task's files, a deadline, a split's problem list) is free of names; the stand-ins for those hold none.
/// </para></summary>
public static class BriefRehearsal
{
    /// <summary>The shape C6's deadline has, so a template is rehearsed with a deadline the way the runner spells one.</summary>
    private const string Deadline = "00:00 UTC (00:00 +00:00)";
    private const string TaskId = "T1";

    /// <summary>The brief stems of every step the schemes place, a tasks entry counting its three steps, each once, in the order they are first placed.</summary>
    public static IReadOnlyList<string> StemsUsed(Experiment.Experiment experiment) =>
        [.. experiment.Schemes.SelectMany(scheme => SchemePositions.Steps(scheme, experiment.Tasks)).SelectMany(placed => StemOf(experiment, placed.Id)).Distinct(StringComparer.Ordinal)];

    /// <summary>Every problem the rehearsal found, each once: the refusal of each fill, and a template that could not be read at each position that needs it, naming the template, the
    /// scheme, the position and (for an unreadable one) the step. A stem without an entry in <paramref name="templates"/> was not read and is not rehearsed.</summary>
    public static IReadOnlyList<string> Problems(
        Experiment.Experiment experiment, RootPaths root, int scenarioCount, IReadOnlyDictionary<string, Outcome<string>> templates)
    {
        var arms = ArmsOf(experiment);
        var map = ArmMap.Extend(ArmMap.Empty, arms, Tokens());
        var refused = experiment.Schemes.SelectMany(scheme => Refusals(experiment, scheme, arms, map, root, scenarioCount, templates));
        return [.. refused.Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> Refusals(
        Experiment.Experiment experiment,
        Scheme scheme,
        IReadOnlyList<string> arms,
        ArmMap map,
        RootPaths root,
        int scenarioCount,
        IReadOnlyDictionary<string, Outcome<string>> templates)
    {
        var own = arms.First(arm => SchemeOf(arm) == scheme.Name);
        var paths = root.Arm(map.Entries.Single(entry => entry.Key == own).Value);
        var guard = new BriefLeakGuard(map, own, [.. experiment.Schemes.Select(s => s.Name)], root.Root);
        var facts = Representative(experiment, paths, scenarioCount);
        return SchemePositions.Steps(scheme, experiment.Tasks).Select(placed => Refusal(experiment, scheme, placed, templates, paths, facts, guard)).Where(sentence => sentence.Length > 0);
    }

    private static string Refusal(
        Experiment.Experiment experiment,
        Scheme scheme,
        PlacedStep placed,
        IReadOnlyDictionary<string, Outcome<string>> templates,
        ArmPaths paths,
        BriefFacts facts,
        BriefLeakGuard guard)
    {
        var spec = experiment.Steps[placed.Id];
        var name = BriefTemplates.NameOf(spec.Brief).Match(file => file, _ => spec.Brief);
        var fields = BriefFields.For(spec, SchemePositions.Visible(scheme, placed.Position, placed.Id, experiment.Steps, experiment.Tasks), experiment.Tasks, paths, facts);
        var where = $"{name} at schemes.{scheme.Name}[{placed.Position}] ({placed.Label}):";
        return templates.GetValueOrDefault(spec.Brief) switch
        {
            Outcome<string>.Ok template when BriefFiller.Fill(template.Value, fields, guard, name) is Outcome<string>.Fail fail => $"{where} {fail.Reason}",
            Outcome<string>.Fail unreadable => $"{where} step {placed.Id}: {unreadable.Reason}",
            _ => string.Empty,
        };
    }

    private static IEnumerable<string> StemOf(Experiment.Experiment experiment, StepId id) =>
        experiment.Steps.TryGetValue(id, out var spec) ? [spec.Brief] : [];

    private static BriefFacts Representative(Experiment.Experiment experiment, ArmPaths paths, int scenarioCount) =>
        new(
            experiment.Target.Plan,
            experiment.Target.Base.Value,
            experiment.Target.Branch,
            scenarioCount,
            Deadline,
            experiment.Known,
            new TaskFacts(TaskId, paths.TaskBrief(TaskId), "src/Example.cs", paths.TaskBefore(TaskId)),
            "none");

    /// <summary>The arms the runs name, then <c>&lt;scheme&gt;1</c> for each scheme that no run names an arm of.</summary>
    private static IReadOnlyList<string> ArmsOf(Experiment.Experiment experiment)
    {
        var named = experiment.Runs.SelectMany(run => run.Rounds).SelectMany(round => round).Distinct(StringComparer.Ordinal).ToList();
        return [.. named, .. experiment.Schemes.Where(scheme => !named.Exists(arm => SchemeOf(arm) == scheme.Name)).Select(scheme => scheme.Name + "1")];
    }

    private static string SchemeOf(string arm) => ArmNames.SchemeOf(arm).Match(scheme => scheme, _ => string.Empty);

    /// <summary>Distinct tokens without a random source: this is a rehearsal, the tokens only have to differ.</summary>
    private static Func<byte[]> Tokens()
    {
        var drawn = 0UL;
        return () => BitConverter.GetBytes(++drawn * 0x9E3779B97F4A7C15UL);
    }
}
