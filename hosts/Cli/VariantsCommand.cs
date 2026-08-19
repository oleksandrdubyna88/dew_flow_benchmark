using Bench.Application.Variants;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Variants;

namespace Bench.Cli;

/// <summary>The catalog of retrieval configurations, from the command line.
/// <para>
/// Three verbs and no fourth: a variant is added and retired, never edited. An <c>edit</c> here would be
/// the one operation that silently relabels numbers already measured, so it does not exist — changing a
/// configuration means adding a row under a new name and retiring the old one.
/// </para></summary>
public static class VariantsCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        IVariantCatalog catalog,
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
                    ? "bench variants needs an action — 'add', 'list' or 'retire'"
                    : $"unknown variants action '{other}' — try 'add', 'list' or 'retire'"),
        };

    private static async Task<int> AddAsync(
        CommandLine command,
        IVariantCatalog catalog,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var definition = ReadDefinition(command);
        if (definition is not Outcome<VariantDefinition>.Ok(var recipe))
        {
            return Fail(error, definition.Reason());
        }

        var variant = RetrievalVariant.Create(
            command.Value("name"), command.Value("display"), recipe, clock.GetUtcNow());

        if (variant is not Outcome<RetrievalVariant>.Ok(var created))
        {
            return Fail(error, variant.Reason());
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
        IVariantCatalog catalog,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var listed = await catalog.ListAsync(command.Has("all"), cancellationToken);

        return listed.Match(
            variants =>
            {
                output.WriteLine($"{variants.Count} variant(s)");
                foreach (var variant in variants)
                {
                    // The state is printed rather than implied by absence: a retired variant listed with
                    // --all must never read like an active one.
                    var state = variant.IsActive ? "active " : "retired";
                    output.WriteLine($"  {state}  {variant.Stamp,-28}  {variant.DisplayName}");
                }

                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> RetireAsync(
        CommandLine command,
        IVariantCatalog catalog,
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
            variant =>
            {
                output.WriteLine($"retired {variant.Stamp} — historical cells still name it");
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    /// <summary>The definition, from a file, from a literal, or from the flags. The file and the literal
    /// exist because a definition is a stored artefact: pasting back the exact JSON a catalog holds must
    /// reproduce the same variant, flag-by-flag reconstruction or not.</summary>
    private static Outcome<VariantDefinition> ReadDefinition(CommandLine command)
    {
        var file = command.Value("definition-file");
        if (file.Length > 0)
        {
            return File.Exists(file)
                ? VariantJson.Read(File.ReadAllText(file))
                : Outcome<VariantDefinition>.Failure($"definition file not found: {file}");
        }

        var literal = command.Value("definition");
        return literal.Length > 0 ? VariantJson.Read(literal) : FromFlags(command);
    }

    private static Outcome<VariantDefinition> FromFlags(CommandLine command)
    {
        var engine = command.Value("engine", "qln");
        if (string.Equals(engine, "noretrieval", StringComparison.OrdinalIgnoreCase))
        {
            return Outcome<VariantDefinition>.Success(VariantDefinition.NoRetrieval);
        }

        if (!Enum.TryParse<EngineKind>(engine, ignoreCase: true, out var kind)
            || !Enum.TryParse<RetrievalChannels>(command.Value("channels", "hybrid"), ignoreCase: true, out var channels))
        {
            return Outcome<VariantDefinition>.Failure(
                $"unknown engine or channels — engines: {string.Join(", ", Enum.GetNames<EngineKind>()).ToLowerInvariant()}; "
                + $"channels: {string.Join(", ", Enum.GetNames<RetrievalChannels>()).ToLowerInvariant()}");
        }

        return Compose(command, kind, channels);
    }

    private static Outcome<VariantDefinition> Compose(
        CommandLine command, EngineKind engine, RetrievalChannels channels) =>
        FusionSpec.Parse(
            command.Value("fusion", FusionSpec.ReciprocalRank),
            command.Int("k", 60),
            Weight(command, "dense-weight"),
            Weight(command, "sparse-weight"),
            command.Value("norm", FusionSpec.NoNormalization)).Match(
            fusion => CorpusSpec.Parse(
                command.Value("text-shape"), command.Int("chunk-tokens", 0), command.Value("embed-model")).Match(
                corpus => Rerank(command).Match(
                    rerank => VariantDefinition.Retrieval(
                        engine, channels, fusion, corpus, rerank, command.Int("limit", 20)),
                    Outcome<VariantDefinition>.Failure),
                Outcome<VariantDefinition>.Failure),
            Outcome<VariantDefinition>.Failure);

    /// <summary>A pool of zero means the reranker is off — stated as one flag rather than two, because a
    /// separate <c>--rerank true --rerank-pool 0</c> pair has a state that means nothing.</summary>
    private static Outcome<RerankSpec> Rerank(CommandLine command)
    {
        var pool = command.Int("rerank-pool", 0);
        return pool > 0 ? RerankSpec.Pooled(pool) : Outcome<RerankSpec>.Success(RerankSpec.Off);
    }

    private static double Weight(CommandLine command, string flag) =>
        double.TryParse(
            command.Value(flag, "1"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : double.NaN;

    private static int Fail(TextWriter error, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return ExitCodes.Configuration;
    }
}
