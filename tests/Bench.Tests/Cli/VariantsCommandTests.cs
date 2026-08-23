using Bench.Application.Variants;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>The catalog verb's contract, over a catalog held in memory.
/// <para>
/// The store is faked here on purpose: what these tests are about is the shape of the command — which
/// refusals are configuration and which are the environment, and that a malformed definition is refused
/// BEFORE anything is written. The catalog's own guarantees are tested against a real Postgres in
/// <see cref="Infrastructure.PostgresVariantCatalogTests"/>, where they belong.
/// </para></summary>
public sealed class VariantsCommandTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_unknown_action_is_a_configuration_problem_and_names_the_real_ones()
    {
        var (code, _, error) = Run("edit");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("add").And.Contain("list").And.Contain("retire");
    }

    [Fact]
    public void A_missing_action_says_what_the_verb_needs()
    {
        Run().Error.Should().Contain("needs an action");
    }

    [Fact]
    public void A_definition_with_an_unknown_axis_is_refused_before_anything_is_stored()
    {
        var catalog = new InMemoryCatalog();

        var (code, _, error) = Run(catalog, "add", "--name", "with-extra", "--definition",
            """{"engine":"noretrieval","graphExpansion":true}""");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("graphExpansion");
        catalog.Added.Should().BeEmpty("a refusal must not leave half a variant behind");
    }

    [Fact]
    public void A_name_that_is_not_a_slug_is_refused_before_anything_is_stored()
    {
        var catalog = new InMemoryCatalog();

        var (code, _, error) = Run(catalog, "add", "--name", "Hybrid RRF", "--engine", "noretrieval");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("variant name");
        catalog.Added.Should().BeEmpty();
    }

    [Fact]
    public void A_variant_is_composed_from_the_flags_and_reported_by_its_stamp()
    {
        var catalog = new InMemoryCatalog();

        var (code, output, _) = Run(catalog, "add", "--name", "hybrid-rrf-256", "--display", "hybrid · rrf · 256",
            "--engine", "qln", "--channels", "hybrid", "--fusion", "rrf", "--k", "60",
            "--text-shape", "src", "--chunk-tokens", "256", "--embed-model", "bge-m3",
            "--rerank-pool", "50", "--limit", "20");

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("hybrid-rrf-256#").And.Contain("hybrid · rrf · 256");
        catalog.Added.Single().Definition.Should().BeOfType<VariantDefinition.RetrievalRecipe>();
    }

    [Fact]
    public void A_variant_can_declare_the_vector_WIDTH_its_corpus_was_built_at()
    {
        var catalog = new InMemoryCatalog();

        var (code, _, _) = Run(catalog, "add", "--name", "wide", "--engine", "qln",
            "--text-shape", "src", "--chunk-tokens", "256", "--embed-model", "bge-m3",
            "--dimensions", "1024");

        // EmbedDimensions, its three states, its refusal and its warning all existed with no way for an
        // operator to declare one: the guard could never fire, because the recipe side was always
        // NotDeclared. A width nobody can state is the "built and never called" pattern.
        code.Should().Be(ExitCodes.Pass);
        Corpus(catalog).Dimensions.Declared.Should().BeTrue();
        Corpus(catalog).Dimensions.Value.Should().Be(1024);
    }

    [Fact]
    public void A_variant_can_name_WHOSE_TOKENS_its_chunk_size_was_counted_in()
    {
        var catalog = new InMemoryCatalog();

        var (code, _, _) = Run(catalog, "add", "--name", "counted", "--engine", "qln",
            "--text-shape", "src", "--chunk-tokens", "256", "--embed-model", "bge-m3",
            "--tokenizer", "bge-m3");

        // chunkTokens is an int with no unit: 256 under two models' tokenizers is two different amounts of
        // text hashed as one comparable configuration. The engine already reports whose tokens it counted
        // in; until now nothing on this side could say what it EXPECTED.
        code.Should().Be(ExitCodes.Pass);
        Corpus(catalog).Tokenizer.Should().Be("bge-m3");
    }

    [Fact]
    public void A_declared_tokenizer_and_width_are_VISIBLE_in_the_listing()
    {
        var catalog = new InMemoryCatalog();
        Run(catalog, "add", "--name", "counted", "--engine", "qln",
            "--text-shape", "src", "--chunk-tokens", "256", "--embed-model", "bge-m3",
            "--tokenizer", "bge-m3", "--dimensions", "1024");

        var (_, output, _) = Run(catalog, "list");

        // An axis buried in a hash is an axis nobody can check against the engine's echo.
        output.Should().Contain("bge-m3").And.Contain("1024");
    }

    private static CorpusSpec Corpus(InMemoryCatalog catalog) =>
        ((VariantDefinition.RetrievalRecipe)catalog.Added.Single().Definition).Corpus;

    [Fact]
    public void The_baseline_needs_no_retrieval_flags()
    {
        var catalog = new InMemoryCatalog();

        var (code, _, _) = Run(catalog, "add", "--name", "no-retrieval", "--engine", "noretrieval");

        code.Should().Be(ExitCodes.Pass);
        catalog.Added.Single().Definition.Should().Be(VariantDefinition.NoRetrieval);
    }

    [Fact]
    public void A_weighted_sum_without_a_normalization_is_refused_by_the_command_too()
    {
        var (code, _, error) = Run(new InMemoryCatalog(), "add", "--name", "wsum-raw",
            "--fusion", "wsum", "--text-shape", "src", "--chunk-tokens", "256", "--embed-model", "bge-m3");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("normalization");
    }

    [Fact]
    public void Listing_marks_a_retired_variant_rather_than_letting_it_read_as_active()
    {
        var catalog = new InMemoryCatalog();
        Run(catalog, "add", "--name", "will-retire", "--engine", "noretrieval");
        Run(catalog, "retire", "--name", "will-retire");

        var (code, output, _) = Run(catalog, "list", "--all");

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("retired").And.Contain("will-retire#");
    }

    [Fact]
    public void Retiring_without_a_name_is_refused()
    {
        Run(new InMemoryCatalog(), "retire").Error.Should().Contain("--name is required");
    }

    private static (int Code, string Output, string Error) Run(params string[] args) =>
        Run(new InMemoryCatalog(), args);

    private static (int Code, string Output, string Error) Run(InMemoryCatalog catalog, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = VariantsCommand
            .RunAsync(CommandLine.Parse(["variants", .. args]), catalog, new FixedClock(Noon), output, error,
                TestContext.Current.CancellationToken)
            .GetAwaiter().GetResult();

        return (code, output.ToString(), error.ToString());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryCatalog : IVariantCatalog
    {
        private readonly List<RetrievalVariant> _variants = [];

        public IReadOnlyList<RetrievalVariant> Added => _variants;

        public Task<Outcome<RetrievalVariant>> AddAsync(RetrievalVariant variant, CancellationToken cancellationToken)
        {
            if (_variants.Any(v => v.Name.Value == variant.Name.Value))
            {
                return Task.FromResult(Outcome<RetrievalVariant>.Failure($"the name '{variant.Name}' is already in the catalog"));
            }

            _variants.Add(variant);
            return Task.FromResult(Outcome<RetrievalVariant>.Success(variant));
        }

        public Task<Outcome<IReadOnlyList<RetrievalVariant>>> ListAsync(bool includeRetired, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<IReadOnlyList<RetrievalVariant>>.Success(
                [.. _variants.Where(v => includeRetired || v.IsActive)]));

        public Task<Outcome<RetrievalVariant>> FindAsync(string name, CancellationToken cancellationToken)
        {
            var found = _variants.FirstOrDefault(v => v.Name.Value == name);
            return Task.FromResult(found is null
                ? Outcome<RetrievalVariant>.Failure($"no variant '{name}' in the catalog")
                : Outcome<RetrievalVariant>.Success(found));
        }

        public async Task<Outcome<RetrievalVariant>> RetireAsync(
            string name, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var found = await FindAsync(name, cancellationToken);

            return found.Match(
                variant => variant.Retire(now).Match(
                    retired =>
                    {
                        _variants[_variants.IndexOf(variant)] = retired;
                        return Outcome<RetrievalVariant>.Success(retired);
                    },
                    Outcome<RetrievalVariant>.Failure),
                Outcome<RetrievalVariant>.Failure);
        }
    }
}
