using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Domain.Variants;

namespace Bench.Domain.Retrieval;

/// <summary>How far along one corpus is toward being measurable.
/// <para>
/// Four states, and the pair that matters is <see cref="Building"/> against <see cref="Failed"/>: a state
/// machine with no owner is the audit's most instructive finding all over again. An engine restart mid-pass
/// leaves a row in <c>Building</c> forever, and because every cell of that corpus waits on it, one stranded
/// row stalls a whole variant for the life of the deployment — a stall that looks exactly like a slow index.
/// </para></summary>
public enum PreparationState
{
    /// <summary>Somebody asked for this corpus and nothing has started it.</summary>
    Requested,

    /// <summary>A pass is running, held by the worker in <see cref="IndexPreparation.Owner"/>.</summary>
    Building,

    /// <summary>The corpus is there and its pass succeeded. The only state a cell may run under.</summary>
    Ready,

    /// <summary>It ended badly, and the reason says how. A state an operator can retry FROM, which is why a
    /// stranded <see cref="Building"/> is moved here rather than left or deleted.</summary>
    Failed,
}

/// <summary>Which corpus a preparation is about — the identity a request is keyed by.
/// <para>
/// The CORPUS half of a recipe, never the whole thing: two variants differing only in their result limit
/// share one index, and keying by the full recipe would build it twice and then compare a corpus against
/// itself. The engine endpoint is part of the key because two daemons are two indexes.
/// </para></summary>
public sealed record CorpusKey(CommitSha Commit, string RecipeHash, string EngineEndpoint)
{
    public static CorpusKey Of(CommitSha commit, CorpusSpec corpus, string engineEndpoint) =>
        new(commit, StableHash.Of(corpus.Canonical), engineEndpoint);

    public string Canonical => $"{Commit.Value[..12]}|{RecipeHash[..12]}|{EngineEndpoint}";
}

/// <summary>One corpus being made ready, with an owner, a heartbeat and a pass to point at.
/// <para>
/// <b>Every transition returns a new value and refuses an illegal one.</b> The rules are here rather than in
/// the store for the same reason <see cref="CellLifecycle"/>'s are: they can then be tested without a
/// database, and the database's only job is the one thing only it can do — make a claim atomic.
/// </para></summary>
/// <param name="PassId">The engine's own id for the pass building this corpus, so a poll asks about THAT
/// pass rather than about the newest one and hopes. Empty until a pass is actually started.</param>
/// <param name="Heartbeat">Refreshed while the pass runs. What separates a worker still watching a
/// twenty-four-minute pass from one that died four minutes in.</param>
public sealed record IndexPreparation(
    Guid Id,
    CorpusKey Key,
    PreparationState State,
    WorkerIdentity Owner,
    string PassId,
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset Heartbeat)
{
    public static IndexPreparation Requested(CorpusKey key, DateTimeOffset now) => new(
        Guid.CreateVersion7(),
        key,
        PreparationState.Requested,
        WorkerIdentity.Nobody,
        PassId: string.Empty,
        Reason: string.Empty,
        RequestedAt: now,
        Heartbeat: default);

    /// <summary>An index this run found already built — recorded as Ready without anybody having asked for
    /// it, so a later report can tell a corpus this benchmark BUILT from one it merely found.</summary>
    public static IndexPreparation Found(CorpusKey key, DateTimeOffset now) =>
        Requested(key, now) with { State = PreparationState.Ready, Reason = "already indexed when first asked" };

    public bool IsTerminal => State is PreparationState.Ready or PreparationState.Failed;

    /// <summary>Takes the request and records who is watching the pass. Refused unless the row is still
    /// waiting: two workers starting one pass would build a corpus twice and both would poll the wrong id.</summary>
    public Outcome<IndexPreparation> Start(WorkerIdentity owner, string passId, DateTimeOffset now)
    {
        if (!owner.CanClaim)
        {
            return Outcome<IndexPreparation>.Failure(
                "a preparation needs an owner with a host and a pid — one nobody can vouch for can never be swept");
        }

        return State == PreparationState.Requested
            ? Outcome<IndexPreparation>.Success(this with
            {
                State = PreparationState.Building,
                Owner = owner,
                PassId = passId,
                Heartbeat = now,
            })
            : Outcome<IndexPreparation>.Failure(
                $"corpus {Key.Canonical} is {State}, not {PreparationState.Requested} — only a waiting request may be started");
    }

    /// <summary>Says the watcher is still there. A pass of twenty-four minutes is normal, and the only thing
    /// separating it from an abandoned one is this stamp being refreshed.</summary>
    public IndexPreparation Beat(DateTimeOffset now) =>
        State == PreparationState.Building ? this with { Heartbeat = now } : this;

    public Outcome<IndexPreparation> Ready(DateTimeOffset now) => End(PreparationState.Ready, string.Empty, now);

    public Outcome<IndexPreparation> Failed(string reason, DateTimeOffset now) =>
        End(PreparationState.Failed, reason, now);

    /// <summary>The sweep decision for one row. Not an <c>Outcome</c>: a sweep asks this of every row and
    /// "nothing to do here" is the normal answer rather than a failure — the same shape as
    /// <see cref="CellLifecycle.Reclaim"/>.
    /// <para>
    /// A <see cref="Building"/> row goes to <see cref="Failed"/> only when BOTH its heartbeat is older than
    /// the window and its owner is provably gone. Either alone is a wrong answer: a live worker past the
    /// window is legitimately watching a long pass, and an owner on another machine cannot be interrogated
    /// from here at all.
    /// </para></summary>
    public IndexPreparation Strand(DateTimeOffset now, TimeSpan window, string host, Func<int, bool> processIsAlive)
    {
        if (State != PreparationState.Building || now - Heartbeat < window)
        {
            return this;
        }

        return Owner.IsProvablyGoneOn(host, processIsAlive)
            ? this with
            {
                State = PreparationState.Failed,
                Owner = WorkerIdentity.Nobody,
                Reason = $"the worker watching pass '{PassId}' is gone and its last heartbeat was "
                    + $"{(now - Heartbeat).TotalMinutes:0} min ago — retry from here",
            }
            : this;
    }

    public string Describe => $"{Key.Canonical} → {State}{(Reason.Length > 0 ? $" ({Reason})" : string.Empty)}";

    private Outcome<IndexPreparation> End(PreparationState state, string reason, DateTimeOffset now) =>
        State is PreparationState.Requested or PreparationState.Building
            ? Outcome<IndexPreparation>.Success(this with
            {
                State = state,
                Owner = WorkerIdentity.Nobody,
                Reason = reason,
                Heartbeat = now,
            })
            : Outcome<IndexPreparation>.Failure(
                $"corpus {Key.Canonical} is already {State} — a terminal preparation is retried by requesting a new one");
}
