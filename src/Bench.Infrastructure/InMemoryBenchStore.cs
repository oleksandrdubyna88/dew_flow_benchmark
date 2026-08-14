using System.Collections.Concurrent;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Suites;

namespace Bench.Infrastructure;

/// <summary>The store the walking skeleton runs on, before Postgres exists. Deliberately shipped
/// rather than stubbed: a port with one implementation is an assertion about a shape nobody has had to
/// satisfy yet, and this is the second implementation that keeps the first one honest.</summary>
public sealed class InMemoryBenchStore : IBenchStore
{
    private readonly ConcurrentDictionary<(string Id, int Version), Suite> _suites = new();

    public Task<Outcome<Suite>> LoadSuiteAsync(string suiteId, int version, CancellationToken cancellationToken) =>
        Task.FromResult(_suites.TryGetValue((suiteId, version), out var suite)
            ? Outcome<Suite>.Success(suite)
            : Outcome<Suite>.Failure($"no suite {suiteId} at version {version}"));

    public Task<Outcome<Suite>> SaveSuiteAsync(Suite suite, CancellationToken cancellationToken)
    {
        if (!suite.IsFrozen)
        {
            return Task.FromResult(Outcome<Suite>.Failure(
                $"suite {suite.Id} is a draft — only frozen versions are stored, so a stored version can never change"));
        }

        _suites[(suite.Id, suite.Version)] = suite;
        return Task.FromResult(Outcome<Suite>.Success(suite));
    }

    public Task<IReadOnlyList<string>> ListSuiteIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. _suites.Keys.Select(k => k.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
}
