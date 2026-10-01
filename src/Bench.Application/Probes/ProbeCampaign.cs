using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;

namespace Bench.Application.Probes;

/// <param name="StaleAfter">How quiet a claim must be before <see cref="ProbeCampaign.PrepareAsync"/> asks whether its owner is gone.</param>
public sealed record ProbeCampaignOptions(DrainLimits Limits, TimeSpan StaleAfter)
{
    /// <summary>One line per cell as it ends — settled or handed back — so a campaign says what it does as it does it.</summary>
    public Action<string> Progress { get; init; } = static _ => { };

    public static TimeSpan DefaultStaleAfter => TimeSpan.FromMinutes(30);
}

/// <param name="Executables">Every subject's executable as resolved on THIS machine (subject id → absolute path) — the
/// verb resolves the frozen references before anything is claimed; the campaign pins and launches what it is handed.</param>
public sealed record ProbeCampaignInputs(ProbeRun Run, IReadOnlyDictionary<string, string> Executables);

/// <summary>One subject's lane: its own store over its own database context (a context is not thread-safe) and the runner
/// that measures its cells — a CLI runner for a CLI subject, the product runner for the api subject.</summary>
public sealed record ProbeLane(ProbeSubject Subject, IProbeStore Store, IProbeRunner Runner);

/// <summary>What the entry step did: the owner-checked sweep, then the fixture cleanup keyed to the directory.</summary>
public sealed record ProbePrepareReport(ProbeSweepReport Sweep, int StrandedFixturesDeleted);

/// <param name="Benched">The subjects whose account ran out this invocation, and the attempt that said so.</param>
public sealed record ProbeCampaignReport(
    int Settled, int Unmeasured, int Refused, int Faulted, CampaignStop Stop, string Reason, IReadOnlyDictionary<ProbeSubjectId, string> Benched);

/// <summary>How the entry step asks whether a claim's owner is still here: the machine's own name and its process table — the
/// two facts <see cref="WorkerIdentity.IsProvablyGoneOn"/> needs, handed in because this layer cannot ask the operating system.</summary>
public sealed record OwnerLiveness(string Host, Func<int, bool> ProcessIsAlive);

/// <summary>One lane per subject, lanes in parallel, each lane one cell at a time (§5) — a <see cref="LegDrain"/> per lane, as the
/// gate campaign runs its lanes. A lane pins its subject's executable at EVERY claim (D9) through <see cref="IProductPinReader"/>,
/// claims the next cell of ITS subject in matrix order, runs it, and settles it the moment it ends.
/// <para>
/// <b>An empty account is not a measurement</b> (D8, the gate's lane stop): a runner that hands an attempt back benches the
/// subject FIRST — no further cell of it is claimed this invocation — and only then returns the cell Pending with the attempt
/// kept; the other lanes go on, and the campaign ends <see cref="CampaignStop.AccountOut"/> naming the subject.
/// </para>
/// <para>
/// <see cref="PrepareAsync"/> is the one entry step every verb calls: the owner-checked sweep, then <see cref="IProbeFixtures.DeleteStranded"/>
/// over the run — every fixture directory whose cell is not Claimed by a live owner on this host, Pending included (finding 3).
/// </para></summary>
public sealed class ProbeCampaign(IProductPinReader pins, IProbeFixtures fixtures, LegDrain drain, TimeProvider clock, OwnerLiveness liveness)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ProbeSubjectId, string> _benched = [];
    private string _pinRefusal = string.Empty;

    public async Task<ProbePrepareReport> PrepareAsync(IProbeStore store, ProbeRun run, TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var swept = await store.SweepAsync(staleAfter, cancellationToken);
        var cells = await store.CellsAsync(run.Id, cancellationToken);
        var live = cells
            .Where(c => c.State == CellState.Claimed && !c.Owner.IsProvablyGoneOn(liveness.Host, liveness.ProcessIsAlive))
            .Select(c => c.Id)
            .ToHashSet();

        return new ProbePrepareReport(swept, fixtures.DeleteStranded(run.Id, live));
    }

    public async Task<Outcome<ProbeCampaignReport>> RunAsync(
        ProbeCampaignInputs inputs, ProbeCampaignOptions options, Func<ProbeSubject, ProbeLane> lanes, CancellationToken stopping)
    {
        var unresolved = inputs.Run.Subjects.Where(s => !inputs.Executables.ContainsKey(s.Id.Value)).Select(s => s.Id.Value).ToList();

        if (unresolved.Count > 0)
        {
            return Outcome<ProbeCampaignReport>.Failure(
                $"no executable was resolved for subject(s) {string.Join(", ", unresolved)} — every frozen reference is resolved on this machine before anything is claimed");
        }

        var reports = await Task.WhenAll(inputs.Run.Subjects.Select(subject => LaneAsync(lanes(subject), inputs, options, stopping)));

        return Outcome<ProbeCampaignReport>.Success(Report(reports, stopping));
    }

    private async Task<DrainReport> LaneAsync(ProbeLane lane, ProbeCampaignInputs inputs, ProbeCampaignOptions options, CancellationToken stopping)
    {
        var owner = WorkerIdentity.Here($"probe-lane-{lane.Subject.Id}");

        return await drain.DrainAsync(ct => LegAsync(lane, owner, inputs, options.Progress, ct), _ => { }, options.Limits, stopping);
    }

    private async Task<Outcome<LegResult>> LegAsync(
        ProbeLane lane, WorkerIdentity owner, ProbeCampaignInputs inputs, Action<string> progress, CancellationToken cancellationToken)
    {
        if (IsOver(lane.Subject.Id))
        {
            return Outcome<LegResult>.Failure(ClaimRefusal.NoPendingCell);
        }

        var claim = await ClaimAsync(lane, owner, inputs, cancellationToken);

        if (claim is Outcome<ProbeCell>.Fail refused)
        {
            return Outcome<LegResult>.Failure(refused.Reason);
        }

        var cell = ((Outcome<ProbeCell>.Ok)claim).Value;
        var ended = await EndAsync(lane, owner, cell, await lane.Runner.RunAsync(inputs.Run, lane.Subject, cell, cancellationToken));
        progress(Line(cell, ended));

        return ended.Match(
            settled => Outcome<LegResult>.Success(LegResult.Of(settled.Id, string.Empty, string.Empty, [], clock.GetUtcNow())),
            Outcome<LegResult>.Failure);
    }

    /// <summary>The pin is read right before the store takes the cell (D9): the build THEN, not the build when the lane began.
    /// A pin that cannot be read stops every lane — a fact about an unknown build is a fact about nothing.</summary>
    private async Task<Outcome<ProbeCell>> ClaimAsync(ProbeLane lane, WorkerIdentity owner, ProbeCampaignInputs inputs, CancellationToken cancellationToken)
    {
        var pin = await pins.ReadAsync(inputs.Executables[lane.Subject.Id.Value], cancellationToken);

        if (pin is Outcome<ProductPin>.Fail unreadable)
        {
            StopPins($"subject '{lane.Subject.Id}': {unreadable.Reason}");
            return Outcome<ProbeCell>.Failure(ClaimRefusal.NoPendingCell);
        }

        var claim = await lane.Store.ClaimNextAsync(inputs.Run.Id, lane.Subject.Id, owner, ((Outcome<ProductPin>.Ok)pin).Value, cancellationToken);

        return claim is Outcome<ProbeCell>.Fail { Reason: var reason } && reason.Contains(ProbeClaimRefusal.NoPendingCell, StringComparison.Ordinal)
            ? Outcome<ProbeCell>.Failure(ClaimRefusal.NoPendingCell)
            : claim;
    }

    /// <summary>A settlement lands tokenless, like the gate's: a stop that strands a measured cell is a crash with manners.</summary>
    private async Task<Outcome<ProbeCell>> EndAsync(ProbeLane lane, WorkerIdentity owner, ProbeCell cell, ProbeAttemptResult result) => result switch
    {
        ProbeAttemptResult.Settled settled => await lane.Store.SettleAsync(cell.Id, owner, settled.Settlement, CancellationToken.None),
        ProbeAttemptResult.Unmeasured unmeasured => await BenchAsync(lane, owner, cell, unmeasured.Reason),
        _ => throw new InvalidOperationException("unreachable"),
    };

    /// <summary>Benched FIRST — the cell is still claimed by this lane — then handed back with its attempt kept, so no claim
    /// decided after the bench can take one of this subject's cells (the gate's order, proven there by a red test).</summary>
    private async Task<Outcome<ProbeCell>> BenchAsync(ProbeLane lane, WorkerIdentity owner, ProbeCell cell, ProbeReason reason)
    {
        var why = $"{ProbeWord.Of(cell.Probe)} r{cell.Repeat} attempt {cell.Attempts}: {reason}";
        Bench(lane.Subject.Id, why);

        var handed = await lane.Store.HandBackUnmeasuredAsync(cell.Id, owner, cell.Attempts, reason, CancellationToken.None);

        return Outcome<ProbeCell>.Failure(handed is Outcome<ProbeCell>.Fail notHanded
            ? $"{ReviewerAccountOut.Marker}: {why} — and the hand-back failed: {notHanded.Reason}"
            : $"{ReviewerAccountOut.Marker}: {why} — the subject is benched for this invocation and its cells stay pending");
    }

    private static string Line(ProbeCell cell, Outcome<ProbeCell> ended)
    {
        var what = $"{ProbeWord.Of(cell.Probe)}/{cell.Subject}/r{cell.Repeat} g{cell.Generation} attempt {cell.Attempts}";

        return ended switch
        {
            Outcome<ProbeCell>.Ok { Value: var s } => $"settled        {what} — {s.Facts.Kind}",
            Outcome<ProbeCell>.Fail f => $"handed back    {what} — {f.Reason}",
            _ => what,
        };
    }

    private bool IsOver(ProbeSubjectId subject)
    {
        lock (_gate)
        {
            return _pinRefusal.Length > 0 || _benched.ContainsKey(subject);
        }
    }

    private void Bench(ProbeSubjectId subject, string why)
    {
        lock (_gate)
        {
            _benched.TryAdd(subject, why);
        }
    }

    private void StopPins(string reason)
    {
        lock (_gate)
        {
            if (_pinRefusal.Length == 0)
            {
                _pinRefusal = reason;
            }
        }
    }

    private ProbeCampaignReport Report(DrainReport[] lanes, CancellationToken stopping)
    {
        Dictionary<ProbeSubjectId, string> benched;
        string pinRefusal;

        lock (_gate)
        {
            benched = new Dictionary<ProbeSubjectId, string>(_benched);
            pinRefusal = _pinRefusal;
        }

        var (stop, reason) = Stop(lanes, benched, pinRefusal, stopping);

        return new ProbeCampaignReport(lanes.Sum(l => l.Scored), benched.Count, lanes.Sum(l => l.Refused), lanes.Sum(l => l.Faulted), stop, reason, benched);
    }

    private static (CampaignStop Stop, string Reason) Stop(DrainReport[] lanes, IReadOnlyDictionary<ProbeSubjectId, string> benched, string pinRefusal, CancellationToken stopping) =>
        (pinRefusal.Length > 0, lanes.FirstOrDefault(l => l.Stop == DrainStop.TooManyFailures), benched.Count > 0, stopping.IsCancellationRequested) switch
        {
            (true, _, _, _) => (CampaignStop.PinUnreadable, pinRefusal),
            (_, { Stop: DrainStop.TooManyFailures } broken, _, _) => (CampaignStop.TooManyFailures, broken.Reason),
            (_, _, true, _) => (CampaignStop.AccountOut, AccountOutReason(benched)),
            (_, _, _, true) => (CampaignStop.Cancelled, "a stop was requested; the run is resumable"),
            _ => (CampaignStop.Drained, "every pending cell of the run is settled"),
        };

    private static string AccountOutReason(IReadOnlyDictionary<ProbeSubjectId, string> benched) =>
        $"no further cell of {string.Join(", ", benched.Keys.Select(k => k.Value).Order(StringComparer.Ordinal))} was claimed — "
        + string.Join("; ", benched.OrderBy(b => b.Key.Value, StringComparer.Ordinal).Select(b => $"{b.Key}: {b.Value}"))
        + " — its cells stay pending; resume the run once the account is back";
}
