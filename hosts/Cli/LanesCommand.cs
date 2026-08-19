using Bench.Application.Lanes;
using Bench.Domain;
using Bench.Domain.Lanes;

namespace Bench.Cli;

/// <summary>The catalog of tool surfaces, from the command line.
/// <para>
/// Three verbs and no fourth, exactly as <see cref="VariantsCommand"/>: a lane is added and retired, never
/// edited. An <c>edit</c> here would be the one operation that silently relabels numbers already measured —
/// and on this axis it would be the likeliest one to reach for, because rewording a doctrine feels like
/// tidying a sentence rather than minting a configuration. It is minting a configuration.
/// </para></summary>
public static class LanesCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        ILaneCatalog catalog,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "add" => await AddAsync(command, catalog, clock, output, error, cancellationToken),
            "list" => await ListAsync(command, catalog, output, error, cancellationToken),
            "retire" => await RetireAsync(command, catalog, clock, output, error, cancellationToken),
            var other => Fail(
                error,
                other.Length == 0
                    ? "bench lanes needs an action — 'add', 'list' or 'retire'"
                    : $"unknown lanes action '{other}' — try 'add', 'list' or 'retire'"),
        };

    private static async Task<int> AddAsync(
        CommandLine command,
        ILaneCatalog catalog,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var definition = ReadDefinition(command);
        if (definition is not Outcome<LaneDefinition>.Ok(var surface))
        {
            return Fail(error, definition.Reason());
        }

        var lane = ToolLane.Create(command.Value("name"), command.Value("display"), surface, clock.GetUtcNow());
        if (lane is not Outcome<ToolLane>.Ok(var created))
        {
            return Fail(error, lane.Reason());
        }

        var added = await catalog.AddAsync(created, cancellationToken);

        return added.Match(
            value =>
            {
                output.WriteLine($"added {value.Stamp} — {value.DisplayName}");
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> ListAsync(
        CommandLine command,
        ILaneCatalog catalog,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var listed = await catalog.ListAsync(command.Has("all"), cancellationToken);

        return listed.Match(
            lanes =>
            {
                output.WriteLine($"{lanes.Count} lane(s)");
                foreach (var lane in lanes)
                {
                    // The state is printed rather than implied by absence: a retired lane listed with --all
                    // must never read like an active one.
                    var state = lane.IsActive ? "active " : "retired";
                    output.WriteLine($"  {state}  {lane.Stamp,-28}  {Surface(lane)}  {lane.DisplayName}");
                }

                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> RetireAsync(
        CommandLine command,
        ILaneCatalog catalog,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var name = command.Value("name");
        if (name.Length == 0)
        {
            return Fail(error, "--name is required");
        }

        var retired = await catalog.RetireAsync(name, clock.GetUtcNow(), cancellationToken);

        return retired.Match(
            lane =>
            {
                output.WriteLine($"retired {lane.Stamp} — historical cells still name it");
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    /// <summary>What a listing shows about the surface itself, in one column: how it reaches the model, how
    /// many tools it offers, and which words. The doctrine is not printed — it is a paragraph, and a listing
    /// that wrapped one would stop being scannable. <c>bench lanes show</c> is where it belongs, once it
    /// exists.</summary>
    private static string Surface(ToolLane lane)
    {
        var tools = lane.Definition.ToolNames.Count == 0 ? "all" : $"{lane.Definition.ToolNames.Count}";
        var set = lane.Definition.DescriptionSet.Length == 0 ? "literal" : lane.Definition.DescriptionSet;
        var doctrine = lane.Definition.Doctrine.Length == 0 ? "no-doctrine" : lane.Definition.DoctrineHash[..8];

        return $"{lane.Definition.Presentation,-16} tools={tools,-4} desc={set,-12} doctrine={doctrine,-12} turns={lane.Definition.MaxTurns}";
    }

    /// <summary>
    /// The definition, from a file, from a literal, or from the flags.
    ///
    /// <para>The file matters more here than it does for a variant: a doctrine is a PARAGRAPH, and a
    /// paragraph on a command line is a quoting accident waiting to relabel an arm. <c>--doctrine-file</c>
    /// reads one from disk, which is also where the three shipped doctrines live as text.</para>
    /// </summary>
    private static Outcome<LaneDefinition> ReadDefinition(CommandLine command)
    {
        var file = command.Value("definition-file");
        if (file.Length > 0)
        {
            return File.Exists(file)
                ? LaneJson.Read(File.ReadAllText(file))
                : Outcome<LaneDefinition>.Failure($"definition file not found: {file}");
        }

        var literal = command.Value("definition");
        return literal.Length > 0 ? LaneJson.Read(literal) : FromFlags(command);
    }

    private static Outcome<LaneDefinition> FromFlags(CommandLine command)
    {
        var presentation = command.Value("presentation", nameof(ToolPresentation.Bridge));
        if (!Enum.TryParse<ToolPresentation>(presentation, ignoreCase: true, out var parsed))
        {
            return Outcome<LaneDefinition>.Failure(
                $"unknown presentation '{presentation}' — this build knows "
                + string.Join(", ", Enum.GetNames<ToolPresentation>()));
        }

        var doctrine = ReadDoctrine(command);

        return doctrine.Match(
            text => LaneDefinition.Create(
                // Absent means every tool the surface offers — the definition's own "empty is a
                // configuration" rule, so nothing here invents a default list.
                command.List("tools"),
                command.Value("descriptions"),
                text,
                parsed,
                command.Int("max-turns", 1)),
            Outcome<LaneDefinition>.Failure);
    }

    /// <summary>A doctrine from a file beats one on the command line, and both beat nothing. An unreadable
    /// path is refused rather than treated as an empty doctrine: "no instruction" is a real arm, and it must
    /// not be reachable by a typo in a filename.</summary>
    private static Outcome<string> ReadDoctrine(CommandLine command)
    {
        var file = command.Value("doctrine-file");
        if (file.Length == 0)
        {
            return Outcome<string>.Success(command.Value("doctrine"));
        }

        return File.Exists(file)
            ? Outcome<string>.Success(File.ReadAllText(file))
            : Outcome<string>.Failure($"doctrine file not found: {file}");
    }

    private static int Fail(TextWriter error, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return ExitCodes.Configuration;
    }
}
