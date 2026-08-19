namespace Bench.Domain.Lanes;

/// <summary>
/// How a tool surface reaches the model — and it is part of a lane's identity rather than a detail of the
/// adapter.
///
/// <para><b>Measured, and it is the largest single effect in the record after the doctrine:</b> the same
/// four tools over the MCP wire scored <b>4 of 63</b> against <b>36 of 63</b> in-process, nine times, from
/// the form alone; replicated at sixteen tools as 4/47 against 11/47. A leaderboard that ranked a wording
/// across two presentations would attribute to words what belongs to the shape.</para>
/// </summary>
public enum ToolPresentation
{
    /// <summary>No tools at all — the floor every tool claim is measured against. Not an absence of
    /// configuration: an arm.</summary>
    None,

    /// <summary>In-process, through the local-LLM function-calling bridge. No protocol framing, no HTTP
    /// hop — the arm that scored 36/63 where its wire twin scored 4.</summary>
    Bridge,

    /// <summary>A real MCP server as a subprocess over stdio, driven by this harness's own loop.</summary>
    McpStdio,

    /// <summary>A cloud CLI agent with its own native tools, running its own loop. The harness cannot see
    /// inside it — see the reconstructed half of the tool-call ledger.</summary>
    CliNative,

    /// <summary>The same CLI agent, with our MCP server attached beside its native tools.</summary>
    CliNativeWithMcp,
}

/// <summary>
/// One tool surface, as a value: what is offered, in which words, under which ordering instruction, through
/// which shape, for how many turns.
///
/// <para><b>Never edited.</b> Every result names the lane it ran under, so changing a wording in place would
/// relabel numbers already measured. Changing a surface means a new row and retiring the old one — the same
/// rule <see cref="Variants.RetrievalVariant"/> holds, for the same reason.</para>
///
/// <para><b>Every refusal names the legal values.</b> No field here is clamped, and that is deliberate
/// against this repository's usual "clamp numbers, refuse names": a definition is <i>hashed</i>, so silently
/// clamping 1 000 turns to 100 would make two different configurations one identity, and a report over them
/// would be a comparison of nothing.</para>
/// </summary>
public sealed record LaneDefinition
{
    /// <summary>Far above any turn ceiling ever measured — the series that produced the doctrine numbers ran
    /// at 25. It exists to catch a typo, not to express a policy: what stops an expensive loop is the
    /// cost budget and the warning before it, not this number.</summary>
    public const int MaxTurnCeiling = 100;

    private LaneDefinition(
        IReadOnlyList<string> toolNames,
        string descriptionSet,
        string doctrine,
        ToolPresentation presentation,
        int maxTurns)
    {
        ToolNames = toolNames;
        DescriptionSet = descriptionSet;
        Doctrine = doctrine;
        Presentation = presentation;
        MaxTurns = maxTurns;
    }

    /// <summary>The subset the surface must serve. <b>Empty means every tool it offers</b>, unfiltered —
    /// which is a real configuration and not an unset field.</summary>
    public IReadOnlyList<string> ToolNames { get; }

    /// <summary>Which description set the server should read. Empty means each tool's compiled literal —
    /// the floor, and the state a server that was told nothing serves.</summary>
    public string DescriptionSet { get; }

    /// <summary>
    /// The ordering instruction: which channel to use when.
    ///
    /// <para>The primary axis of this whole exercise. Three wordings of this one paragraph moved a score
    /// from 30.0 to 46.5 of 63 with everything else held, ranges not overlapping, while swapping the
    /// toolbox from 4 tools to 18 moved 1 point. It is stored as text rather than a key so a published
    /// database explains its own numbers without a second artefact.</para>
    /// </summary>
    public string Doctrine { get; }

    public ToolPresentation Presentation { get; }

    /// <summary>1 is a single-turn selection micro-task — did the model pick the right tool at all; above 1
    /// is an agentic leg. The same code path at two ceilings, deliberately.</summary>
    public int MaxTurns { get; }

    /// <summary>Whether this lane offers a model any tool. A `None` presentation is the floor arm.</summary>
    public bool OffersTools => Presentation != ToolPresentation.None;

    /// <summary>
    /// The identity, composed in a fixed order.
    ///
    /// <para>Tool names are ordinal-sorted, so a lane written with its tools in a different order is the
    /// same lane. The doctrine enters as its <see cref="StableHash"/> rather than verbatim: a paragraph in
    /// an identity string would make the canonical form unreadable in a log, and the text itself is stored
    /// beside it anyway.</para>
    /// </summary>
    public string Canonical => string.Join(
        '|',
        [
            $"presentation={Presentation}",
            $"tools={(ToolNames.Count == 0 ? "*" : string.Join(',', ToolNames.Order(StringComparer.Ordinal)))}",
            $"descriptions={(DescriptionSet.Length == 0 ? "-" : DescriptionSet)}",
            $"doctrine={DoctrineHash[..12]}",
            $"turns={MaxTurns}",
        ]);

    public string Hash => StableHash.Of(Canonical);

    /// <summary>Stored as its own column so "which wording wins, holding the tool set fixed" is a
    /// <c>GROUP BY</c> rather than JSON parsing in SQL. A projection of the definition, written once and
    /// never updated — the row is immutable, so it cannot drift.</summary>
    public string ToolsHash =>
        StableHash.Of(ToolNames.Count == 0 ? "*" : string.Join(',', ToolNames.Order(StringComparer.Ordinal)));

    /// <summary>The doctrine's own fingerprint. An empty doctrine hashes like any other value rather than
    /// being special-cased: "no instruction" is an arm, and it must be groupable beside the others.</summary>
    public string DoctrineHash => StableHash.Of(Doctrine);

    /// <summary>The floor arm: no tools, no descriptions, one turn. What every tool claim is measured
    /// against, and the reason it is a named constructor rather than a definition whose fields are ignored.</summary>
    public static LaneDefinition NoTools(string doctrine = "") =>
        new([], string.Empty, Clean(doctrine), ToolPresentation.None, 1);

    public static Outcome<LaneDefinition> Create(
        IReadOnlyList<string>? toolNames,
        string? descriptionSet,
        string? doctrine,
        ToolPresentation presentation,
        int maxTurns)
    {
        var tools = (toolNames ?? []).Select(Clean).ToList();
        var set = Clean(descriptionSet);

        var refusal = Refuse(tools, set, presentation, maxTurns);

        return refusal.Length > 0
            ? Outcome<LaneDefinition>.Failure(refusal)
            : Outcome<LaneDefinition>.Success(new LaneDefinition(tools, set, Clean(doctrine), presentation, maxTurns));
    }

    private static string Refuse(
        IReadOnlyList<string> tools, string descriptionSet, ToolPresentation presentation, int maxTurns) =>
        (maxTurns, tools, descriptionSet, presentation) switch
        {
            ( < 1, _, _, _) =>
                $"a lane must allow at least one turn, got {maxTurns}",
            ( > MaxTurnCeiling, _, _, _) =>
                $"{maxTurns} turns is above this build's ceiling of {MaxTurnCeiling}; "
                + "a ceiling that high is a typo, and what bounds an expensive loop is the cost budget",
            _ => RefuseTools(tools, descriptionSet, presentation),
        };

    private static string RefuseTools(
        IReadOnlyList<string> tools, string descriptionSet, ToolPresentation presentation)
    {
        if (tools.Any(name => name.Length == 0 || name.Any(char.IsWhiteSpace)))
        {
            return "a tool name must be a non-blank token without whitespace — "
                + $"got [{string.Join(", ", tools.Select(t => $"'{t}'"))}]";
        }

        var duplicate = tools.GroupBy(name => name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            // Not deduplicated: a list naming one tool twice is a typo, and silently collapsing it would
            // hide the mistake behind an identity that looks deliberate.
            return $"the tool subset names '{duplicate.Key}' more than once";
        }

        return RefusePresentation(tools, descriptionSet, presentation);
    }

    /// <summary>The one cross-field rule: a lane that offers no tools cannot name any.
    /// <para>A `None` lane carrying a tool subset or a description set is a contradiction somebody will
    /// eventually read as a configuration — and the floor arm is exactly the row whose meaning must not be
    /// in doubt, since every tool claim is a comparison against it.</para></summary>
    private static string RefusePresentation(
        IReadOnlyList<string> tools, string descriptionSet, ToolPresentation presentation) =>
        (presentation, tools.Count, descriptionSet.Length) switch
        {
            (ToolPresentation.None, > 0, _) =>
                $"a lane with no tool presentation cannot name {tools.Count} tool(s) — "
                + "the no-tools arm offers nothing by definition",
            (ToolPresentation.None, _, > 0) =>
                $"a lane with no tool presentation cannot name a description set ('{descriptionSet}') — "
                + "there is nothing for it to describe",
            _ => RefuseDescriptionSet(descriptionSet),
        };

    /// <summary>A set name travels to a server and becomes a directory under a configured root. Refusing a
    /// separator here is not defence against this repository's own CLI — it is refusing to record an
    /// identity that cannot be served, before it reaches a hash and a report.</summary>
    private static string RefuseDescriptionSet(string descriptionSet) =>
        descriptionSet.Length > 0
        && (descriptionSet.Contains('/') || descriptionSet.Contains('\\') || descriptionSet.Contains(".."))
            ? $"'{descriptionSet}' is not a usable description set name — it names a path, not a set"
            : string.Empty;

    private static string Clean(string? value) => (value ?? string.Empty).Trim();
}
