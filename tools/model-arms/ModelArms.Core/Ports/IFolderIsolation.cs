using Bench.Domain;
using ModelArms.Core.Isolation;

namespace ModelArms.Core.Ports;

/// <summary>What a restore (or a roll-back) came to, entry by entry: the paths restored, and the sentences for what was skipped (gone, replaced, a link), left to the operator (a mode changed by hand) or failed (outside the bound, a refused <c>chmod</c>). Only <see cref="Failed"/> keeps the snapshot.</summary>
public sealed record RestoreReport(IReadOnlyList<string> Restored, IReadOnlyList<string> Skipped, IReadOnlyList<string> Unresolved, IReadOnlyList<string> Failed)
{
    /// <summary>The entries that needed no call because they already stood at their saved mode (a harmless retry): counted as restored, and SAID.</summary>
    public IReadOnlyList<string> Harmless { get; init; } = [];

    /// <summary>Restored by a call plus already at the saved mode: the <c>k</c> of <i>restored k of n</i>.</summary>
    public int Settled => Restored.Count + Harmless.Count;

    /// <summary><c>(m already at their saved mode)</c> when there were any, else nothing.</summary>
    public string HarmlessWords => Harmless.Count == 0 ? string.Empty : $" ({Harmless.Count} already at their saved mode)";
}

/// <summary>Every entry of the plan, pinned, with the mode it had: what <see cref="IFolderIsolation.Pin"/> hands back and the snapshot is written from. The adapter keeps the descriptors (kernel road) until it is disposed.</summary>
public sealed record Pinned(IReadOnlyList<SavedMode> Modes);

/// <summary>The paths <c>unlock</c> may set a mode on (finding 11), computed by the verb from today's <c>experiment.json</c> and the map: the root, the home folder and every configured path of any run.</summary>
public sealed record RestoreBound(string Root, string Home, IReadOnlyList<string> Configured)
{
    public bool Holds(string path) => IsolationPlans.InBound(path, Root, Home, Configured);
}

/// <summary>The file system's side of D11 — mechanical; the VERB decides (the table of <see cref="LockDecision"/> and <see cref="UnlockDecision"/>, the order of the writes, the exit codes). One instance serves one verb run: on the kernel road it holds the descriptors <see cref="Pin"/> opened until it is disposed.
/// Every method answers for every input; what the host refused comes back in words.</summary>
public interface IFolderIsolation : IDisposable
{
    /// <summary><c>POSIX file modes (Linux x86-64, kernel road)</c> · <c>POSIX file modes (&lt;platform&gt;, runtime road)</c> · <c>none — &lt;why&gt;</c>.</summary>
    string Describe { get; }

    /// <summary>False for the adapter that does nothing (Windows).</summary>
    bool Applies { get; }

    /// <summary>The line a road that verifies less prints before its first mode call; empty on the kernel road.</summary>
    string NotVerifiedLine { get; }

    /// <summary>Whether this process runs as root — a mode does nothing to root, so the verb refuses before anything.</summary>
    bool IsRoot { get; }

    /// <summary>Step 0: every entry opened and judged (kind, links, the ancestors); a refusal names the entry (exit 4). Nothing is written.</summary>
    Outcome<Pinned> Pin(IsolationPlan plan);

    /// <summary>Step 2: each locked mode set through its descriptor (or the path, on the runtime road), in plan order; the first failure's sentence — the caller rolls back.</summary>
    Outcome<string> Apply(Pinned pinned, IsolationPlan plan);

    /// <summary>Step 3: every entry tried the way an arm would (a listing, a read); empty means the isolation was observed to hold, a sentence per entry that still can be listed or read.</summary>
    IReadOnlyList<string> Verify(IsolationPlan plan);

    /// <summary>The saved modes in reverse through the HELD descriptors — the lock's own apply or verify failure, same process.</summary>
    RestoreReport RollBack(Pinned pinned);

    /// <summary>The saved modes pinned ANEW (the killed lock's descriptors died with it), each judged by <see cref="RestoreJudgement"/> before any call.</summary>
    RestoreReport Restore(IReadOnlyList<SavedMode> reverse, RestoreBound bound);

    /// <summary>An applied snapshot's open entries: re-pinned (the identity must be the saved one), the locked mode set again. The standing snapshot stays the authority.</summary>
    Outcome<string> ReApply(IReadOnlyList<SavedMode> open, IsolationPlan plan);
}
