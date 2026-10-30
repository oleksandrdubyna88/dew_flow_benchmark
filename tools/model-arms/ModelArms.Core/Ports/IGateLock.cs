namespace ModelArms.Core.Ports;

/// <summary>The lock the gate step holds while it runs (plan C7, D10): one holder at a time across every arm of an experiment, released while the gate waits out a blocked round.
/// The adapter (E2.S6) is a file lock; Core only names the shape.</summary>
public interface IGateLock
{
    /// <summary>The held lock, or <c>null</c> when another holder has it (null is the answer here, not an error — the caller waits or gives up).</summary>
    IDisposable? TryAcquire(string path);

    /// <summary>Waits until the lock is free and returns it held. Cancellation throws and holds nothing.</summary>
    Task<IDisposable> AcquireAsync(string path, CancellationToken cancellationToken);
}
