namespace ModelArms.Adapters;

/// <summary>What one attempt to take the gate lock came to. Five states, because they are five different facts: the lock is <see cref="Held"/> by the caller; <see cref="Busy"/> with another holder (the answer, not an error);
/// <see cref="Absent"/> — nothing is at the path and the caller asked for nothing to be created; the path is not one the lock may use, <see cref="Refused"/> (a link, a folder, a FIFO, a file that changed under the check — the
/// operator's root is wrong, exit 4); or the host could not say, <see cref="Unread"/> (a kernel read that failed, exit 3).</summary>
public abstract record GateLockAnswer
{
    private GateLockAnswer() { }

    /// <summary>The lock, held until <paramref name="Handle"/> is disposed.</summary>
    public sealed record Held(IDisposable Handle) : GateLockAnswer;

    public sealed record Busy : GateLockAnswer;

    public sealed record Absent : GateLockAnswer;

    /// <summary>The path is not a regular file of the root's own; the reason names the path, what it is and D17.</summary>
    public sealed record Refused(string Reason) : GateLockAnswer;

    public sealed record Unread(string Reason) : GateLockAnswer;
}
