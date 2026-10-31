using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary><c>lock.log</c> (D11, E5.S1 question 10): one line per <c>lock</c> or <c>unlock</c> run — appended by those two verbs alone, <c>&lt;utc&gt; lock &lt;run&gt;: &lt;the printed line&gt;</c>. Opened by NAME with <see cref="FileMode.Append"/>, and only when the name is not a link (a link there: one line on the error
/// writer, nothing appended). A failed append never fails the verb: it is one line on the error writer. <b>The residual:</b> between the link check and the open a link could be planted — only the operator can write the root (D17), and the same window is the keep-awake log's. The appending itself is <see cref="RootLogFile"/>'s, shared with that log.</summary>
internal static class LockLog
{
    public static void Append(RootPaths root, TimeProvider clock, string line, TextWriter error) => RootLogFile.Append(root.LockLog, clock, line, error);
}
