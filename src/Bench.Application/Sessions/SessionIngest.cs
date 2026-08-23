using Bench.Contracts;

namespace Bench.Application.Sessions;

/// <summary>Accepting session events — the one door every vantage point comes through.
/// <para>
/// Thin on purpose, and thinner than it looks: the version check lives in
/// <see cref="SessionCodec"/>, the pairing of a call's open and close lives in the store, and the phase
/// label lives in the domain. What is left here is the accounting — how many were taken, how many were
/// already known, and which were refused and why — because that is the part both surfaces need to report
/// identically.
/// </para>
/// <para>
/// <b>A refusal never fails the batch.</b> One unreadable event out of two hundred must not cost the other
/// hundred and ninety-nine: an agent session is a live thing that cannot be replayed, and an
/// all-or-nothing ingest would turn a single malformed line into a lost session.
/// </para></summary>
public static class SessionIngest
{
    public static async Task<SessionIngestResultDto> AcceptAsync(
        IReadOnlyList<SessionEventDto> events,
        ISessionStore store,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var accepted = 0;
        var last = new SessionIngestOutcome(Guid.Empty, 0, false, string.Empty);

        foreach (var wire in events)
        {
            var taken = await AcceptOneAsync(wire, store, reasons, cancellationToken);

            accepted += taken.HasValue ? 1 : 0;
            last = taken ?? last;
        }

        return new SessionIngestResultDto(
            last.SessionId, last.Ordinal, last.Duplicate, accepted, reasons.Count, reasons);
    }

    private static async Task<SessionIngestOutcome?> AcceptOneAsync(
        SessionEventDto wire,
        ISessionStore store,
        List<string> reasons,
        CancellationToken cancellationToken)
    {
        switch (SessionCodec.ReadEvent(wire))
        {
            case SessionVerdict.Read read:
                return await store.RecordAsync(read.Record, cancellationToken);

            case SessionVerdict.UnknownVersion unknown:
                reasons.Add(unknown.Reason);
                return null;

            case SessionVerdict.Unreadable unreadable:
                reasons.Add(unreadable.Reason);
                return null;

            default:
                reasons.Add("unclassified session event");
                return null;
        }
    }
}
