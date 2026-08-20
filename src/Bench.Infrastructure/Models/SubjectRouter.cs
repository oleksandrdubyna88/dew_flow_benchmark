using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;

namespace Bench.Infrastructure.Models;

/// <summary>One <see cref="IModelRuntime"/> for a run whose subjects are DRIVEN differently — local
/// models over the OpenAI route, Claude rows as a CLI process. Routes by the model id on the request's
/// endpoint, which is the same key every roster lookup and every aggregate already uses.
/// <para>
/// Mutable by registration, deliberately: the routes are resolved from the REGISTRY, which lives behind
/// the same container this router is built by, so they cannot be constructor arguments. `Add` is called
/// while the run is being prepared and never during a drain — the same before-any-cell moment every
/// other resolution happens at.
/// </para>
/// <para>
/// A budget is confirmed by EVERY runtime here or refused whole: a turn ceiling the CLI cannot enforce
/// must refuse a mixed run out loud, not silently bind only the half that understood it — the
/// context-compaction ceiling that reached no CLI arm is exactly this failure, measured.
/// </para></summary>
public sealed class SubjectRouter(IModelRuntime fallback) : IModelRuntime
{
    private readonly Dictionary<string, IModelRuntime> _routes = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string modelId, IModelRuntime runtime) => _routes[modelId] = runtime;

    public int Routes => _routes.Count;

    public ModelHosting Hosting => fallback.Hosting;

    public async Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken)
    {
        var accepted = await fallback.AcceptBudgetAsync(budget, cancellationToken);

        if (accepted is Outcome<string>.Fail)
        {
            return accepted;
        }

        foreach (var (modelId, runtime) in _routes)
        {
            var also = await runtime.AcceptBudgetAsync(budget, cancellationToken);

            if (also is Outcome<string>.Fail refused)
            {
                return Outcome<string>.Failure($"subject '{modelId}': {refused.Reason}");
            }
        }

        return _routes.Count == 0
            ? accepted
            : Outcome<string>.Success($"{((Outcome<string>.Ok)accepted).Value} + {_routes.Count} CLI subject(s)");
    }

    public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
        _routes.TryGetValue(request.Endpoint.Model.Id, out var routed)
            ? routed.AskAsync(request, cancellationToken)
            : fallback.AskAsync(request, cancellationToken);
}
