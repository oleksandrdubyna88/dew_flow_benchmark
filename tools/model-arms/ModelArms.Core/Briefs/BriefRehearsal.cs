using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>Fills, once and with representative facts, every brief template the schemes use, through <see cref="BriefFiller"/> and its leak guard. A KNOWN
/// or an override template that the guard would refuse (a scheme name such as <c>C</c> in free text: "C#", "(C)") is found by <c>validate</c>, never at a
/// step's launch hours into the chain.
/// <para>
/// The facts are the experiment's own (plan, base, branch, KNOWN) with a stand-in for what only a run knows: the arms are the ones the rounds name (and an
/// <c>&lt;scheme&gt;1</c> for a scheme no round uses), each with a token minted here, and the brief is filled for the first arm of each scheme. Not proved:
/// that a value a run computes later (a task's files, a deadline, a split's problem list) is free of names; the stand-ins for those hold none.
/// </para></summary>
public static class BriefRehearsal
{
    private const string Deadline = "2000-01-01 00:00 UTC";
    private const string TaskId = "T1";

    /// <summary>The step kinds some scheme runs, each once.</summary>
    public static IReadOnlyList<StepKind> KindsUsed(Experiment.Experiment experiment) =>
        [.. experiment.Schemes.SelectMany(scheme => scheme.KindsUsed).Distinct()];

    /// <summary>Every sentence the rehearsal produced, each once: a template that could not be read, then the refusal of each fill, prefixed with the template's name.
    /// A kind without an entry in <paramref name="templates"/> was not read and is not rehearsed.</summary>
    public static IReadOnlyList<string> Problems(
        Experiment.Experiment experiment, RootPaths root, int scenarioCount, IReadOnlyDictionary<StepKind, Outcome<string>> templates)
    {
        var arms = ArmsOf(experiment);
        var map = ArmMap.Extend(ArmMap.Empty, arms, Tokens());
        var unreadable = templates.Values.OfType<Outcome<string>.Fail>().Select(fail => fail.Reason);
        var refused = experiment.Schemes.SelectMany(scheme => Refusals(experiment, scheme, arms, map, root, scenarioCount, templates));
        return [.. unreadable.Concat(refused).Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> Refusals(
        Experiment.Experiment experiment,
        Scheme scheme,
        IReadOnlyList<string> arms,
        ArmMap map,
        RootPaths root,
        int scenarioCount,
        IReadOnlyDictionary<StepKind, Outcome<string>> templates)
    {
        var own = arms.First(arm => SchemeOf(arm) == scheme.Name);
        var paths = root.Arm(map.Entries.Single(entry => entry.Key == own).Value);
        var guard = new BriefLeakGuard(map, own, [.. experiment.Schemes.Select(s => s.Name)], root.Root);
        var facts = Representative(experiment, paths, scenarioCount);
        return scheme.KindsUsed.Select(kind => Refusal(kind, templates, paths, facts, guard)).Where(sentence => sentence.Length > 0);
    }

    private static string Refusal(
        StepKind kind, IReadOnlyDictionary<StepKind, Outcome<string>> templates, ArmPaths paths, BriefFacts facts, BriefLeakGuard guard) =>
        templates.GetValueOrDefault(kind) is Outcome<string>.Ok template
        && BriefFiller.Fill(template.Value, BriefFields.For(kind, paths, facts), guard, BriefTemplates.NameOf(kind)) is Outcome<string>.Fail fail
            ? $"{BriefTemplates.NameOf(kind)}: {fail.Reason}"
            : string.Empty;

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

    /// <summary>The arms the rounds name, then <c>&lt;scheme&gt;1</c> for each scheme that no round names an arm of.</summary>
    private static IReadOnlyList<string> ArmsOf(Experiment.Experiment experiment)
    {
        var named = experiment.Rounds.SelectMany(round => round).Distinct(StringComparer.Ordinal).ToList();
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
