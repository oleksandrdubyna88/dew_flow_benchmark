using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Variants;

namespace Bench.Application;

/// <summary>The retriever of a run that has no retrieval engine.
/// <para>
/// A null object rather than a nullable dependency: the runner asks for a recipe only when a cell names a
/// variant, so this is reachable only when a run planned a retrieval variant without an engine to serve it
/// — a configuration fault the CLI already refuses before any cell exists. It answers with a REFUSAL naming
/// the missing flag rather than with an empty context, because an empty context would be stored as "the
/// search found nothing", which is a claim about the index instead of an admission that nothing was asked.
/// </para></summary>
public sealed class NoRetriever : IRetriever
{
    public EngineRef Describe => EngineRef.Filesystem();

    public Outcome<string> CanServe(VariantDefinition.RetrievalRecipe recipe) =>
        Outcome<string>.Failure(Refusal(recipe));

    public Task<Outcome<IndexState>> InspectAsync(
        VariantDefinition.RetrievalRecipe recipe, CancellationToken cancellationToken) =>
        Task.FromResult(Outcome<IndexState>.Failure(Refusal(recipe)));

    public Task<Outcome<RetrievedContext>> RetrieveAsync(
        RetrievalRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Outcome<RetrievedContext>.Failure(Refusal(request.Recipe)));

    private static string Refusal(VariantDefinition.RetrievalRecipe recipe) =>
        $"this run has no retrieval engine, so variant recipe '{recipe.Canonical}' cannot be served — "
        + "name an engine with --engine-url. An empty context stored here would read as an index that found "
        + "nothing, which is a different claim from nothing having been asked";
}
