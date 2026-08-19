using Bench.Domain;
using Bench.Domain.Lanes;

namespace Bench.Application.Lanes;

/// <summary>
/// The catalog of named tool surfaces.
///
/// <para>A lane is added and retired; it is never edited. That is the port's whole shape, and it is why
/// there is no <c>UpdateAsync</c>: results name the lane they ran under, so an update would relabel numbers
/// already measured rather than record a new surface. Rewording a doctrine mints a row.</para>
///
/// <para>Uniqueness of a name is the DATABASE's job, not a read-then-write above it — two sessions adding
/// the same name would both find nothing and both insert, and the matrix would then hold two rows a report
/// cannot tell apart.</para>
/// </summary>
public interface ILaneCatalog
{
    Task<Outcome<ToolLane>> AddAsync(ToolLane lane, CancellationToken cancellationToken);

    /// <summary>Every lane, or only the active ones. Retired rows stay listable on purpose: a report over an
    /// old test still has to name the surface it ran against.</summary>
    Task<Outcome<IReadOnlyList<ToolLane>>> ListAsync(bool includeRetired, CancellationToken cancellationToken);

    Task<Outcome<ToolLane>> FindAsync(string name, CancellationToken cancellationToken);

    Task<Outcome<ToolLane>> RetireAsync(string name, DateTimeOffset now, CancellationToken cancellationToken);
}
