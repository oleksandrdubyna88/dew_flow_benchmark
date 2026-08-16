using Bench.Domain;
using Bench.Domain.Variants;

namespace Bench.Application.Variants;

/// <summary>The catalog of named retrieval configurations.
/// <para>
/// A variant is added and retired; it is never edited. That is the port's whole shape, and it is why
/// there is no <c>UpdateAsync</c>: results name the variant they ran under, so an update would relabel
/// numbers already measured rather than record a new configuration.
/// </para>
/// <para>
/// Uniqueness of a name is the database's job, not a read-then-write above it — two sessions adding the
/// same name would both find nothing and both insert, and the matrix would then hold two rows a report
/// cannot tell apart.
/// </para></summary>
public interface IVariantCatalog
{
    Task<Outcome<RetrievalVariant>> AddAsync(RetrievalVariant variant, CancellationToken cancellationToken);

    /// <summary>Every variant, or only the active ones. Retired rows stay listable on purpose: a report
    /// over an old test still has to name what it ran.</summary>
    Task<Outcome<IReadOnlyList<RetrievalVariant>>> ListAsync(bool includeRetired, CancellationToken cancellationToken);

    Task<Outcome<RetrievalVariant>> FindAsync(string name, CancellationToken cancellationToken);

    Task<Outcome<RetrievalVariant>> RetireAsync(string name, DateTimeOffset now, CancellationToken cancellationToken);
}
