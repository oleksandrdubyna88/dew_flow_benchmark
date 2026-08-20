using Bench.Domain;
using Bench.Domain.Engines;

namespace Bench.Application;

/// <summary>
/// What a leg may call, and for how long.
///
/// <para>Closed, and the two cases are not a flag: <see cref="None"/> is the no-tools ARM, the floor every
/// tool claim is measured against, not the absence of configuration. A boolean beside a tool list would let
/// a caller express "no tools, but here are three" — which is a lane the catalog already refuses to
/// define.</para>
/// </summary>
public abstract record ToolSurface
{
    private ToolSurface() { }

    public sealed record None : ToolSurface;

    /// <param name="Engine">Where a call is actually invoked. The engine IS the surface a model works
    /// through — not a search API — which is what lets the same loop drive the in-process bridge, an MCP
    /// server and a plain filesystem.</param>
    /// <param name="Tools">Exactly what the model is told it has. Advertised as sent, so a report can say
    /// which surface produced a number rather than which one was requested.</param>
    /// <param name="MaxTurns">1 is a single-turn selection micro-task; above 1 is an agentic leg. The same
    /// code path at two ceilings, deliberately — a ladder made of two mechanisms would have two sets of
    /// bugs, and the rung that measures "did it pick the tool" must be the same instrument as the rung that
    /// measures "did picking it help".</param>
    public sealed record Looping(IEngine Engine, IReadOnlyList<EngineTool> Tools, int MaxTurns) : ToolSurface;

    /// <summary>The floor. What every run planned before this axis existed keeps running, unchanged.</summary>
    public static ToolSurface Off { get; } = new None();

    public bool OffersTools => this is Looping;
}

/// <summary>One lane of a run, resolved from the catalog into the surface a leg actually runs against, and
/// the doctrine it is instructed with.</summary>
/// <param name="Name">The lane name a cell carries. The join, and the only part of a lane a cell stores.</param>
/// <param name="Doctrine">The ordering instruction, sent as the system prompt. This is
/// <c>Lane.Preamble</c>'s value — declared in the founding tuple, documented, and read by nothing until
/// now.</param>
public sealed record LaneChoice(string Name, string Doctrine, ToolSurface Surface)
{
    /// <summary>The control arm: no tools, no instruction. What a run planned before the lane catalog
    /// existed resolves to, and what it must keep doing.</summary>
    public static LaneChoice Floor { get; } = new(string.Empty, string.Empty, ToolSurface.Off);
}

/// <summary>
/// Every lane a run measures, resolved once and looked up per leg.
///
/// <para>The same shape as <see cref="Bench.Domain.Runs.VariantRoster"/> and for the same reason, one axis
/// over: a run holds several lanes and each cell names the one it belongs to. <b>The plan's headline
/// experiment is exactly this</b> — three doctrines are three lanes in one run — so a runner holding a
/// single surface would send every leg through the first lane and label the results with the cell's. That
/// is the defect the subject roster was introduced to end, in a third axis.</para>
///
/// <para>A cell naming a lane that is not here is a REFUSAL, never a fallback to the first surface: a leg
/// measured under an instruction it was not planned for is a number no report can catch.</para>
/// </summary>
public sealed record LaneRoster(IReadOnlyList<LaneChoice> Entries)
{
    /// <summary>A run with no lanes resolved — every cell is the floor.</summary>
    public static LaneRoster Floor { get; } = new([]);

    public static LaneRoster Of(IReadOnlyList<LaneChoice> entries) => new(entries);

    /// <summary>The surface and instruction for one cell's lane.
    /// <para>A cell whose lane was never resolved gets the floor, which is what makes this axis additive:
    /// every run that exists today keeps running exactly as it did, because none of them resolved a lane.</para></summary>
    public Outcome<LaneChoice> For(string laneName) =>
        Entries.Count == 0
            ? Outcome<LaneChoice>.Success(LaneChoice.Floor)
            : Entries.FirstOrDefault(e => string.Equals(e.Name, laneName, StringComparison.Ordinal)) is { } found
                ? Outcome<LaneChoice>.Success(found)
                : Outcome<LaneChoice>.Failure(
                    $"this run resolved no lane '{laneName}' — it knows {string.Join(", ", Entries.Select(e => $"'{e.Name}'"))}");
}
