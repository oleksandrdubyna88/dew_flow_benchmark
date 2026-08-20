using Bench.Domain.Targets;

namespace Bench.Domain.Authoring;

/// <summary>The payload a code-task bank question carries (`BankQuestion.CodeTaskJson`), in its first
/// owned shape — `PLAN_code_lane.md` §3.1 sketched the concept, `PLAN_investigate_vs_implement.md` §3.5
/// lands this half of it: everything the investigate arm needs, all of it derivable or already vetted.
/// <para>
/// The reference diff is stored ONCE and whole: causal anchors and hidden-test files are derived from
/// it (<see cref="FixDiff"/>) at use, never stored beside it — a second copy of the diff's facts is a
/// copy that drifts. The mechanism text is the judge's reference and arrives from an author later;
/// empty means "not yet authored", which the judge already reports as <i>not judgeable</i> rather than
/// failing anyone.
/// </para></summary>
public sealed record CodeTask(
    string Kind,
    CommitSha BaseCommit,
    CommitSha FixCommit,
    string ReferenceDiff,
    string Mechanism,
    bool GatesRan,
    string GateDetail)
{
    /// <summary>The one kind harvest produces. "implement" (a stated TODO, no pre-existing bug) is the
    /// code lane's other kind and arrives with its authoring, not here.</summary>
    public const string FixKind = "fix";

    /// <summary>A task as the harvest verb lands it: mechanism empty (authored later), gates recorded
    /// as what actually happened — ran-and-passed, or explicitly skipped. A task whose gates FAILED is
    /// never constructed: it is refused upstream with the gate's own verdict, because a bank row
    /// carrying a failed gate would read as a task somebody vouched for.</summary>
    public static Outcome<CodeTask> Harvested(
        CommitSha baseCommit, CommitSha fixCommit, string referenceDiff, bool gatesRan, string gateDetail) =>
        referenceDiff.Trim().Length == 0
            ? Outcome<CodeTask>.Failure(
                "a code task with no reference diff has no ground truth to derive anchors or hidden tests from")
            : Outcome<CodeTask>.Success(new CodeTask(
                FixKind, baseCommit, fixCommit, referenceDiff, string.Empty, gatesRan, gateDetail));
}
