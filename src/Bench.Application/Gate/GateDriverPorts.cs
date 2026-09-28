using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>Reads which bytes of the product a claim is about to measure: the SHA-256 of its deployment set, the text
/// of <c>--version</c>, and — when the binary sits under a git checkout — the short sha and the dirty count of the
/// product's own source tree. Read per CLAIM, so a product that moved mid-campaign is seen at the next cell.</summary>
public interface IProductPinReader
{
    Task<Outcome<ProductPin>> ReadAsync(string executable, CancellationToken cancellationToken);
}

/// <summary>One tap in front of one cell's api reviewer, for the length of the cell.</summary>
public interface IRecordingTap : IAsyncDisposable
{
    /// <summary>The loopback base url the product's vendor row is pointed at (<c>http://127.0.0.1:&lt;port&gt;/v1</c>).</summary>
    string Endpoint { get; }

    /// <summary>Waits until every forwarded request is answered or closed by its deadline, then stops listening; a
    /// request still open at <paramref name="wait"/> is closed and MARKED. After this, every call has a facts file.</summary>
    Task<int> CloseAsync(TimeSpan wait, CancellationToken cancellationToken);
}

/// <param name="Upstream">The reviewer's real base url — a public vendor url, never a loopback one.</param>
/// <param name="RecordDirectory">The attempt's <c>tap/</c> folder, absolute, inside the artefact root.</param>
/// <param name="Deadline">The ABSOLUTE budget of one upstream call: at it the connection is closed and the call marked.</param>
public sealed record TapLaunch(string Upstream, string RecordDirectory, TimeSpan Deadline)
{
    /// <summary>The largest body the tap buffers, each way. A response above it is cut, answered as a failure and marked —
    /// a fast, large answer must not exhaust memory before the deadline does anything.</summary>
    public long MaxBodyBytes { get; init; } = 64L * 1024 * 1024;
}

public interface IRecordingTapFactory
{
    Task<Outcome<IRecordingTap>> StartAsync(TapLaunch launch, CancellationToken cancellationToken);
}

/// <summary>The working tree the product is handed for one task of one run — a GATE-OWNED clone, so the refs a cell
/// creates and the worktrees the product makes never touch the shared read-only checkout.</summary>
public interface IGateCheckouts
{
    /// <summary>A clone for this run and task, detached at the variant head; made once, reused by every cell.</summary>
    Task<Outcome<string>> EnsureAsync(Guid runId, GateTask task, CancellationToken cancellationToken);

    /// <summary>Creates (or moves) <paramref name="branch"/> in that clone at the variant head.</summary>
    Task<Outcome<string>> CreateRefAsync(string clone, string branch, GateTask task, CancellationToken cancellationToken);

    /// <summary>Removes the clones of runs that ended; returns how many were removed.</summary>
    Task<int> RemoveFinishedAsync(IReadOnlyCollection<Guid> finishedRuns, CancellationToken cancellationToken);
}

/// <summary>The reviewer catalog: rows are added and retired, never edited.</summary>
public interface IGateReviewerCatalog
{
    Task<Outcome<GateReviewer>> AddAsync(GateReviewer reviewer, CancellationToken cancellationToken);

    Task<IReadOnlyList<GateReviewer>> ListAsync(bool includeRetired, CancellationToken cancellationToken);

    /// <summary>The named rows, in the order named — refused naming every id that is not in the catalog.</summary>
    Task<Outcome<IReadOnlyList<GateReviewer>>> GetAsync(IReadOnlyList<GateReviewerId> ids, CancellationToken cancellationToken);

    Task<Outcome<GateReviewer>> RetireAsync(GateReviewerId id, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Where a secret comes from for a cell — the reviewer's creds-key reference through the environment, or the
/// machine's own coai settings file when the operator opted in. Never logged; the value arrives as a
/// <see cref="SecretValue"/>.</summary>
public interface IGateSecrets
{
    Outcome<SecretValue> CredsKey(GateReviewer reviewer);

    /// <summary>The values the reviewer's REFERENCES take on this machine (a referenced endpoint's url, a CLI's path).</summary>
    Outcome<ResolvedReferences> References(GateReviewer reviewer);
}
