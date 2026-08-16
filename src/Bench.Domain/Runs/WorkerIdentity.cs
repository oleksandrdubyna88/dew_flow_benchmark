namespace Bench.Domain.Runs;

/// <summary>Who holds a claim: a label a human reads, and the host and pid a sweep can interrogate.
/// <para>
/// It is one value rather than three loose fields because the three are only ever meaningful together —
/// a pid without its machine is the defect this type exists to remove. A sweep running on host B that
/// reads <c>cli-1234</c> tests pid 1234 against <b>its own</b> process table and reaches a confident wrong
/// answer, requeuing a cell host A is still measuring. Two workers then measure the same leg and the
/// first one's settle is refused for an owner mismatch it did nothing to cause.
/// </para>
/// <para>
/// <b>The window is a margin, not a guarantee.</b> The staleness window (30 minutes against a 10-minute
/// leg wall) is what made time-only sweeping look safe; a margin is not a death certificate, and this
/// system is meant to run unattended for weeks. Reference implementation of the same rule:
/// <c>dew_flow_rag_qln · src/Rag.Infrastructure/Indexing/IndexPassStore.cs:191</c>.
/// </para></summary>
/// <param name="Label">What an operator reads in a refusal or a report — <c>cli</c>, <c>worker-a</c>.
/// Deliberately NOT unique: identity is the triple, and two machines may honestly both be "cli".</param>
/// <param name="Host">The machine that took the claim, as it names itself.</param>
/// <param name="Pid">The process that took the claim, on that machine.</param>
public sealed record WorkerIdentity(string Label, string Host, int Pid)
{
    /// <summary>The owner of a cell nobody holds. Not a null: "pending" is a state with an owner field,
    /// and an empty one is the honest value rather than an absent one.</summary>
    public static readonly WorkerIdentity Nobody = new(string.Empty, string.Empty, 0);

    /// <summary>This process, under a caller-chosen label — the only way a live worker should ever build
    /// one, because the host and the pid are facts about the machine and never arguments.</summary>
    public static WorkerIdentity Here(string label) =>
        new(label, Environment.MachineName, Environment.ProcessId);

    /// <summary>Rebuilt from storage, exactly as recorded. It does NOT validate: a row written before the
    /// owner columns existed is a fact about the past, and <see cref="IsTraceable"/> is where that fact is
    /// interpreted.</summary>
    public static WorkerIdentity Stored(string label, string host, int pid) => new(label, host, pid);

    /// <summary>Whether this identity may take a claim at all. An owner nobody can name and nobody can
    /// check is an owner the sweep would have to guess about.</summary>
    public bool CanClaim => !string.IsNullOrWhiteSpace(Label) && Host.Length > 0 && Pid > 0;

    /// <summary>Whether enough was recorded to ask the question "is it still running".</summary>
    public bool IsTraceable => Host.Length > 0 && Pid > 0;

    public string Canonical => IsTraceable ? $"{Label}@{Host}#{Pid}" : Label;

    /// <summary>Whether nothing is left that could still be doing this work, asked from the machine that
    /// is running the sweep.
    /// <para>
    /// Three rules, and the middle one is the one that costs a wrong answer if it is dropped:
    /// an <b>unrecorded</b> owner is gone by definition — it predates the columns, so nothing can vouch
    /// for it; an owner on <b>another machine</b> is left alone — we cannot see that host, and ending a
    /// live worker's leg is a worse error than leaving a stale row for its own host to sweep; a
    /// <b>live pid here</b> is not gone, whatever the clock says.
    /// </para></summary>
    public bool IsProvablyGoneOn(string host, Func<int, bool> processIsAlive)
    {
        if (!IsTraceable)
        {
            return true;
        }

        if (!string.Equals(Host, host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !processIsAlive(Pid);
    }
}
