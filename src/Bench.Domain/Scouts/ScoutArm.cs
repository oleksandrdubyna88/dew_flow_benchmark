namespace Bench.Domain.Scouts;

/// <summary>The three team sizes the operator asked about (plan §1): one, two and three Opus scouts.</summary>
public enum ScoutWorkload
{
    W1 = 1,
    W2 = 2,
    W3 = 3,
}

/// <summary>The model a scout is launched on, by the ALIAS the Agent call names. The resolved id behind each alias is a pin
/// frozen on the run (<see cref="ScoutPins"/>), because the alias is what moves.</summary>
public enum ScoutModel
{
    Opus,
    Sonnet,
}

/// <summary>Which agent type a scout is: the plugin's code explorer for every arm, Explore for the replication (§3.3).</summary>
public enum ScoutAgent
{
    CodeExplorer,
    Explore,
}

/// <summary>The five focus areas, frozen per task before the run (§3.3). A team of N takes the first N.</summary>
public enum ScoutFocus
{
    SimilarFeatures = 1,
    Architecture = 2,
    CurrentImplementation = 3,
    TestsAndFixtures = 4,
    WiringAndEntryPoints = 5,
}

/// <summary>The arms of §3.3: A is the Opus team, B the Sonnet team of ⌈1.5·N⌉, C3 the secondary arm that separates the
/// model effect from the count effect, and the two Explore replication arms of W3.</summary>
public enum ScoutArmId
{
    A1,
    B1,
    A2,
    B2,
    A3,
    B3,
    C3,
    A3Explore,
    B3Explore,
}

/// <summary>One arm: which workload it answers, the model its scouts run on, how many, and which agent type.</summary>
public sealed record ScoutArm(ScoutArmId Id, ScoutWorkload Workload, ScoutModel Model, int Count, ScoutAgent Agent)
{
    /// <summary>The focus areas this arm's scouts take, in order: the first <see cref="Count"/> of F1..F5.</summary>
    public IReadOnlyList<ScoutFocus> Focuses => [.. Enum.GetValues<ScoutFocus>().Take(Count)];

    /// <summary>The <c>model</c> the Agent call names — what the read-back compares <c>input.model</c> with.</summary>
    public string ModelAlias => ScoutModels.Alias(Model);

    /// <summary>The <c>subagent_type</c> the Agent call names.</summary>
    public string AgentType => Agent == ScoutAgent.Explore ? ScoutArms.ExploreType : ScoutArms.CodeExplorerType;

    public bool IsReplication => Agent == ScoutAgent.Explore;

    public string Word => ScoutArms.Word(Id);
}

/// <summary>The model aliases, spelled once.</summary>
public static class ScoutModels
{
    public static string Alias(ScoutModel model) => model == ScoutModel.Opus ? "opus" : "sonnet";
}

/// <summary>The arm catalogue — the one table every later story reads, so a team size is never retyped.</summary>
public static class ScoutArms
{
    public const string CodeExplorerType = "feature-dev:code-explorer";

    public const string ExploreType = "Explore";

    public static IReadOnlyList<ScoutArm> All { get; } =
    [
        new(ScoutArmId.A1, ScoutWorkload.W1, ScoutModel.Opus, 1, ScoutAgent.CodeExplorer),
        new(ScoutArmId.B1, ScoutWorkload.W1, ScoutModel.Sonnet, 2, ScoutAgent.CodeExplorer),
        new(ScoutArmId.A2, ScoutWorkload.W2, ScoutModel.Opus, 2, ScoutAgent.CodeExplorer),
        new(ScoutArmId.B2, ScoutWorkload.W2, ScoutModel.Sonnet, 3, ScoutAgent.CodeExplorer),
        new(ScoutArmId.A3, ScoutWorkload.W3, ScoutModel.Opus, 3, ScoutAgent.CodeExplorer),
        new(ScoutArmId.B3, ScoutWorkload.W3, ScoutModel.Sonnet, 5, ScoutAgent.CodeExplorer),
        new(ScoutArmId.C3, ScoutWorkload.W3, ScoutModel.Sonnet, 3, ScoutAgent.CodeExplorer),
        new(ScoutArmId.A3Explore, ScoutWorkload.W3, ScoutModel.Opus, 3, ScoutAgent.Explore),
        new(ScoutArmId.B3Explore, ScoutWorkload.W3, ScoutModel.Sonnet, 5, ScoutAgent.Explore),
    ];

    /// <summary>The seven arms every task runs.</summary>
    public static IReadOnlyList<ScoutArm> Main { get; } = [.. All.Where(a => !a.IsReplication)];

    /// <summary>The Explore replication — W3, arms A and B only, a quarter of the tasks.</summary>
    public static IReadOnlyList<ScoutArm> Replication { get; } = [.. All.Where(a => a.IsReplication)];

    public static ScoutArm Of(ScoutArmId id) => All.First(a => a.Id == id);

    public static string Word(ScoutArmId id) => id switch
    {
        ScoutArmId.A3Explore => "A3-explore",
        ScoutArmId.B3Explore => "B3-explore",
        _ => id.ToString(),
    };

    /// <summary>An arm by its word; an unknown word is refused NAMING the legal ones.</summary>
    public static Outcome<ScoutArm> Parse(string? word) =>
        All.FirstOrDefault(a => string.Equals(a.Word, (word ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)) is { } arm
            ? Outcome<ScoutArm>.Success(arm)
            : Outcome<ScoutArm>.Failure($"'{word}' is not a scout arm — one of {string.Join(", ", All.Select(a => a.Word))}");
}
