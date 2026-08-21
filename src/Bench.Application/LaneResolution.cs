using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Lanes;

namespace Bench.Application;

/// <summary>
/// Turning a catalog row into the surface a leg actually runs against.
///
/// <para>Pure, and separate from the host for the reason every rule here is separate: the decisions are
/// which presentations this build can serve and what happens when one it cannot is asked for — and reaching
/// those through a CLI would need a database, a checkout and a model.</para>
///
/// <para><b>A presentation this build cannot serve is REFUSED by name, never downgraded.</b> The tempting
/// alternative — run an `McpStdio` lane on whatever engine is at hand — would produce a number labelled with
/// a surface that never ran, and the shape of a surface is the axis that moved a score nine times: the same
/// four tools scored 4 of 63 over the wire against 36 in-process. A silently substituted presentation is the
/// one substitution that makes the whole comparison meaningless.</para>
/// </summary>
public static class LaneResolution
{
    /// <summary>
    /// The surface and the doctrine for one lane.
    ///
    /// <para><paramref name="engine"/> is what this process can offer a subject, or null when it has none.
    /// It is a parameter rather than a lookup because which engine serves a lane is the HOST's decision —
    /// this function only says whether the lane's presentation can be honoured by what it was given.</para>
    /// </summary>
    public static Outcome<LaneChoice> Resolve(ToolLane lane, IEngine? engine)
    {
        var definition = lane.Definition;

        if (definition.Presentation == ToolPresentation.None)
        {
            // The floor arm, and it needs no engine — that is what makes it the floor. Its doctrine still
            // travels: "no tools, but read carefully" is a legitimate instruction and a legitimate arm.
            return Outcome<LaneChoice>.Success(
                new LaneChoice(lane.Name.Value, definition.Doctrine, ToolSurface.Off));
        }

        if (definition.Presentation is not (ToolPresentation.Bridge or ToolPresentation.McpStdio))
        {
            // CliNative and CliNativeWithMcp run their OWN loop inside a cloud CLI; this harness cannot
            // drive them turn by turn, and their tool calls arrive later through the telemetry spool. Naming
            // that here is the difference between "not built yet" and "cannot be driven this way".
            return Outcome<LaneChoice>.Failure(
                $"lane '{lane.Name}' is a {definition.Presentation} surface, which runs its own loop — "
                + "this runner drives Bridge and McpStdio lanes; a CLI agent's calls are reconstructed from "
                + "telemetry after the fact, not observed during the leg");
        }

        if (engine is null)
        {
            return Outcome<LaneChoice>.Failure(
                $"lane '{lane.Name}' offers tools and this run has no engine to serve them");
        }

        var offered = Offered(definition.ToolNames, engine);

        return offered.Match(
            tools => Outcome<LaneChoice>.Success(new LaneChoice(
                lane.Name.Value,
                definition.Doctrine,
                new ToolSurface.Looping(engine, tools, definition.MaxTurns))),
            Outcome<LaneChoice>.Failure);
    }

    /// <summary>
    /// Every lane of a run, resolved together into the roster a plan carries.
    ///
    /// <para><b>All or nothing.</b> One lane that cannot be honoured refuses the whole roster rather than
    /// dropping an arm: a run planned as three doctrines and started as two produces a comparison whose
    /// missing arm is invisible — every cell naming the dropped lane would refuse at leg time, one by one,
    /// hours into a campaign, as a pile of abandoned cells rather than as "this run cannot be planned".</para>
    ///
    /// <para><b>An empty list is the FLOOR, not an error.</b> That is what keeps this axis additive: a run
    /// planned before the lane catalog existed resolves no lane, every cell gets
    /// <see cref="LaneChoice.Floor"/>, and it behaves exactly as it did.</para>
    ///
    /// <para><b>A repeated name is refused</b>, because <see cref="LaneRoster.For"/> takes the first match —
    /// so two lanes called <c>bridge-4</c> would silently measure one of them twice and label half the cells
    /// with a surface that never ran. The same defect the tool catalog refuses a duplicate tool name for.</para>
    /// </summary>
    public static Outcome<LaneRoster> Resolve(IReadOnlyList<ToolLane> lanes, IEngine? engine)
    {
        if (lanes.Count == 0)
        {
            return Outcome<LaneRoster>.Success(LaneRoster.Floor);
        }

        if (Repeated(lanes) is { Count: > 0 } repeated)
        {
            return Outcome<LaneRoster>.Failure(
                $"this run names {string.Join(", ", repeated)} more than once — a cell resolves its lane by "
                + "name, so a repeat measures one of them twice and labels the rest with a surface that never ran");
        }

        var resolved = new List<LaneChoice>(lanes.Count);

        foreach (var lane in lanes)
        {
            var one = Resolve(lane, engine);

            if (one is Outcome<LaneChoice>.Fail failed)
            {
                return Outcome<LaneRoster>.Failure(failed.Reason);
            }

            resolved.Add(((Outcome<LaneChoice>.Ok)one).Value);
        }

        return Outcome<LaneRoster>.Success(LaneRoster.Of(resolved));
    }

    /// <summary>Lane names appearing more than once, quoted and in first-seen order.</summary>
    private static IReadOnlyList<string> Repeated(IReadOnlyList<ToolLane> lanes) =>
    [
        .. lanes.GroupBy(l => l.Name.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}'"),
    ];

    /// <summary>
    /// The tools this lane actually advertises, in the ENGINE's order.
    ///
    /// <para>An empty subset means every tool the engine offers — a real configuration, not an unset field.
    /// A subset naming a tool the engine does not serve is refused with both sides listed, rather than
    /// quietly advertising a shorter surface: a lane whose name says four tools and whose request carried
    /// three is a row that will be compared against other four-tool rows.</para>
    ///
    /// <para>The engine's order is kept rather than the subset's, so two lanes naming the same tools in
    /// different orders send byte-identical tool arrays — the ordering of a tools array is a thing a model
    /// can be sensitive to, and it must not vary with how somebody typed a CLI flag.</para>
    /// </summary>
    private static Outcome<IReadOnlyList<EngineTool>> Offered(
        IReadOnlyList<string> wanted, IEngine engine)
    {
        if (wanted.Count == 0)
        {
            return Outcome<IReadOnlyList<EngineTool>>.Success(engine.Tools);
        }

        var served = engine.Tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var unknown = wanted.Where(name => !served.Contains(name)).ToList();

        return unknown.Count > 0
            ? Outcome<IReadOnlyList<EngineTool>>.Failure(
                $"the lane names {string.Join(", ", unknown.Select(u => $"'{u}'"))}, which this engine does not "
                + $"serve — it offers {string.Join(", ", served.Order(StringComparer.Ordinal))}")
            : Outcome<IReadOnlyList<EngineTool>>.Success(
                [.. engine.Tools.Where(t => wanted.Contains(t.Name, StringComparer.Ordinal))]);
    }
}
