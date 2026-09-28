using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Application.Gate;

/// <param name="Parallel">Lanes — each holds at most ONE product process, for the cell it claimed.</param>
/// <param name="PerEndpoint">Reviews in flight per reviewer endpoint (a url, a reference name, or a CLI's runtime word).</param>
public sealed record GateCampaignOptions(int Parallel, int PerEndpoint, DrainLimits Limits)
{
    public const int DefaultParallel = 4;
    public const int DefaultPerEndpoint = 2;
}

/// <summary>Everything every cell of the campaign is run with.</summary>
public sealed record GateCampaignInputs(
    GateRun Run,
    ProductPin CampaignPin,
    IReadOnlyDictionary<string, GateReviewer> Reviewers,
    IReadOnlyDictionary<string, GateTask> Tasks,
    GateRunSettings RunSettings,
    FileHashKey Key,
    string ProductExecutable);

public enum CampaignStop
{
    Drained,
    Cancelled,
    TooManyFailures,

    /// <summary>The product's bytes changed under a run that was not allowed to change product.</summary>
    ProductMoved,

    /// <summary>The product could not be pinned at a claim — it is gone, or it no longer answers <c>--version</c>.</summary>
    PinUnreadable,
}

public sealed record GateCampaignReport(int Settled, int Refused, int Faulted, CampaignStop Stop, string Reason, IReadOnlyList<ProductPin> PinsSeen);

/// <summary>One lane's own services: a store over its own database context (a context is not thread-safe) and a runner
/// over it, and its one session slot.</summary>
public sealed record GateLane(int Number, IGateStore Store, GateCellRunner Runner, LaneSlot Slot);

/// <summary>A campaign's lanes — <c>--parallel</c> of them, each a <see cref="LegDrain"/> over one delegate — with the
/// per-endpoint cap enforced at the CLAIM: a lane claims only among reviewers whose endpoint has a free slot, so it never
/// holds a claimed cell at the head of the line while another endpoint sits idle; when every pending cell's endpoint is
/// full it waits for a release. The product is pinned at every claim: a product that moved stops the campaign unless the
/// run allows a change, in which case the next cells claim under the new pin and cells already claimed settle under
/// their own.</summary>
public sealed class GateCampaign(IProductPinReader pins, LegDrain drain, TimeProvider clock)
{
    private readonly Lock _stopGate = new();
    private CampaignStop _stop = CampaignStop.Drained;
    private string _stopReason = string.Empty;
    private readonly List<ProductPin> _pins = [];

    public async Task<Outcome<GateCampaignReport>> RunAsync(
        GateCampaignInputs inputs, GateCampaignOptions options, Func<int, GateLane> lanes, CancellationToken stopping)
    {
        var refusal = (options.Parallel, options.PerEndpoint, inputs.Run.Mode) switch
        {
            ( < 1, _, _) => $"--parallel must be at least 1, got {options.Parallel}",
            (_, < 1, _) => $"--per-endpoint must be at least 1, got {options.PerEndpoint}",
            ( > 1, _, DataDirMode.Shared) => "a shared data directory runs ONE lane: its one usage.jsonl carries no session, task or attempt on a row, so "
                                             + "concurrent cells' turns could not be told apart — pass --parallel 1",
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Outcome<GateCampaignReport>.Failure(refusal);
        }

        _pins.Add(inputs.CampaignPin);
        var pool = new EndpointPool(options.PerEndpoint, inputs.Reviewers.Values);
        var reports = await Task.WhenAll(Enumerable.Range(1, options.Parallel).Select(n => LaneAsync(lanes(n), inputs, pool, options, stopping)));

        return Outcome<GateCampaignReport>.Success(Report(reports, stopping));
    }

    private async Task<DrainReport> LaneAsync(GateLane lane, GateCampaignInputs inputs, EndpointPool pool, GateCampaignOptions options, CancellationToken stopping)
    {
        await using var slot = lane.Slot;
        var owner = WorkerIdentity.Here($"gate-lane-{lane.Number}");

        return await drain.DrainAsync(ct => LegAsync(lane, owner, inputs, pool, ct), _ => { }, options.Limits, stopping);
    }

    private async Task<Outcome<LegResult>> LegAsync(GateLane lane, WorkerIdentity owner, GateCampaignInputs inputs, EndpointPool pool, CancellationToken cancellationToken)
    {
        // The pin is read INSIDE the claim, under the pool's lock, right before the store takes the cell: a lane that
        // waited an hour for its endpoint must see the product as it is when its turn comes, not as it was when it began
        // to wait.
        var claim = await pool.ClaimAsync(lane.Store, inputs.Run.Id, owner, () => PinAsync(inputs, cancellationToken), cancellationToken);

        if (claim is Outcome<GateCell>.Fail fail)
        {
            return Outcome<LegResult>.Failure(fail.Reason.Contains(ClaimRefusal.NoPendingCell, StringComparison.Ordinal) || fail.Reason.Contains("no pending gate cell", StringComparison.Ordinal)
                ? ClaimRefusal.NoPendingCell
                : fail.Reason);
        }

        var cell = ((Outcome<GateCell>.Ok)claim).Value;

        try
        {
            var work = new GateCellWork(inputs.Run, cell, inputs.Reviewers[cell.Reviewer.Value], inputs.Tasks[cell.Task.Value], inputs.RunSettings, inputs.Key, owner);
            var settled = await lane.Runner.RunAsync(work, lane.Slot, cancellationToken);

            return settled.Match(
                s => Outcome<LegResult>.Success(LegResult.Of(s.Id, string.Empty, string.Empty, [], clock.GetUtcNow())),
                Outcome<LegResult>.Failure);
        }
        finally
        {
            pool.Release(inputs.Reviewers[cell.Reviewer.Value]);
        }
    }

    /// <summary>The product as it is NOW — read per claim. A moved product ends the campaign (by answering "no more
    /// cells" to every lane) unless the run allows a change.</summary>
    private async Task<Outcome<ProductPin>> PinAsync(GateCampaignInputs inputs, CancellationToken cancellationToken)
    {
        if (Stopped)
        {
            return Outcome<ProductPin>.Failure(_stopReason);
        }

        var read = await pins.ReadAsync(inputs.ProductExecutable, cancellationToken);

        if (read is Outcome<ProductPin>.Fail unreadable)
        {
            Stop(CampaignStop.PinUnreadable, unreadable.Reason);
            return read;
        }

        var pin = ((Outcome<ProductPin>.Ok)read).Value;

        if (pin.Matches(inputs.CampaignPin))
        {
            return read;
        }

        if (!inputs.Run.AllowProductChange)
        {
            Stop(CampaignStop.ProductMoved, ((Outcome<ProductPin>.Fail)ProductPin.Continue(inputs.CampaignPin, pin)).Reason);
            return Outcome<ProductPin>.Failure(_stopReason);
        }

        lock (_stopGate)
        {
            if (!_pins.Any(p => p.Matches(pin)))
            {
                _pins.Add(pin);
            }
        }

        return read;
    }

    private bool Stopped
    {
        get
        {
            lock (_stopGate)
            {
                return _stop is CampaignStop.ProductMoved or CampaignStop.PinUnreadable;
            }
        }
    }

    private void Stop(CampaignStop stop, string reason)
    {
        lock (_stopGate)
        {
            if (!Stopped)
            {
                _stop = stop;
                _stopReason = reason;
            }
        }
    }

    private GateCampaignReport Report(DrainReport[] lanes, CancellationToken stopping)
    {
        var (stop, reason) = (Stopped, lanes.FirstOrDefault(l => l.Stop == DrainStop.TooManyFailures)) switch
        {
            (true, _) => (_stop, _stopReason),
            (_, { Stop: DrainStop.TooManyFailures } broken) => (CampaignStop.TooManyFailures, broken.Reason),
            _ when stopping.IsCancellationRequested => (CampaignStop.Cancelled, "a stop was requested; the run is resumable"),
            _ => (CampaignStop.Drained, "every pending cell of the run is settled"),
        };

        return new GateCampaignReport(lanes.Sum(l => l.Scored), lanes.Sum(l => l.Refused), lanes.Sum(l => l.Faulted), stop, reason, [.. _pins]);
    }
}

/// <summary>The per-endpoint slots, shared by the lanes of one campaign. Claiming happens UNDER the pool's lock, among
/// the reviewers whose endpoint has room, so two lanes can never both take the last slot of one endpoint.</summary>
public sealed class EndpointPool
{
    private readonly int _cap;
    private readonly IReadOnlyList<GateReviewer> _reviewers;
    private readonly Dictionary<string, int> _inflight = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EndpointPool(int cap, IEnumerable<GateReviewer> reviewers)
    {
        _cap = cap;
        _reviewers = [.. reviewers];
    }

    /// <summary>The key a reviewer's reviews are counted under: its endpoint value, its endpoint reference's name, or —
    /// for a CLI row — the runtime word, because one CLI is one account's quota.</summary>
    public static string KeyOf(GateReviewer reviewer) => reviewer.Definition.Endpoint switch
    {
        ReviewerEndpoint.Value v => "url:" + v.Url.TrimEnd('/').ToLowerInvariant(),
        ReviewerEndpoint.Reference r => "ref:" + r.Name,
        _ => "runtime:" + reviewer.Definition.Runtime.Word(),
    };

    public async Task<Outcome<GateCell>> ClaimAsync(
        IGateStore store, Guid runId, WorkerIdentity owner, Func<Task<Outcome<ProductPin>>> pinNow, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task released;
            await _lock.WaitAsync(cancellationToken);

            try
            {
                var free = _reviewers.Where(r => _inflight.GetValueOrDefault(KeyOf(r)) < _cap).Select(r => r.Id).ToList();
                var busy = _inflight.Values.Any(n => n > 0);

                if (free.Count > 0)
                {
                    if (await pinNow() is not Outcome<ProductPin>.Ok { Value: var pin })
                    {
                        return Outcome<GateCell>.Failure(ClaimRefusal.NoPendingCell); // the campaign has stopped: the product moved or is gone
                    }

                    var claim = await store.ClaimNextAmongAsync(runId, owner, pin, free, cancellationToken);
                    var nothingFree = claim is Outcome<GateCell>.Fail f && f.Reason.Contains("no pending gate cell", StringComparison.Ordinal);

                    if (claim is Outcome<GateCell>.Ok ok)
                    {
                        var key = KeyOf(_reviewers.First(r => r.Id.Value == ok.Value.Reviewer.Value));
                        _inflight[key] = _inflight.GetValueOrDefault(key) + 1;
                        return claim;
                    }

                    if (!nothingFree || !busy)
                    {
                        return claim; // a real refusal, or nothing pending anywhere: every endpoint had room
                    }
                }

                released = _released.Task;
            }
            finally
            {
                _lock.Release();
            }

            await Task.WhenAny(released, Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public void Release(GateReviewer reviewer)
    {
        _lock.Wait();

        try
        {
            var key = KeyOf(reviewer);
            _inflight[key] = Math.Max(0, _inflight.GetValueOrDefault(key) - 1);
            var released = _released;
            _released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            released.TrySetResult();
        }
        finally
        {
            _lock.Release();
        }
    }
}
