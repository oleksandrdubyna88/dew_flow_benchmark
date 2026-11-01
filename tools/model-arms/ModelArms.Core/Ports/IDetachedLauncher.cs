namespace ModelArms.Core.Ports;

/// <summary>What starting a detached program came to (C24, closed). <see cref="Started"/> says the operating system took the start — it does NOT say the program runs: the runner names itself (D13), and liveness is its own lock. <see cref="Note"/> is one line the caller prints when the start was
/// not as clean as asked (empty otherwise). <see cref="Failed"/> carries the exit code the caller keeps (3 the host, 4 a configuration) and why.</summary>
public abstract record DetachedStart
{
    private DetachedStart()
    {
    }

    public sealed record Started(string Note = "") : DetachedStart;

    public sealed record Failed(int ExitCode, string Why) : DetachedStart;
}

/// <summary>C24, D13: starts a program that must outlive the process that started it. <c>executable</c> is a REAL absolute path (D17 d), <c>arguments</c> are argv[1..] (never a shell string), <c>log</c> is appended to and created at mode 0600, never through a link; <c>environment</c> REPLACES the child's;
/// <c>workingDirectory</c> is chosen. <b>No pid is returned</b>: whoever is started names itself, and an arm is alive when its own lock is held (D13).</summary>
public interface IDetachedLauncher
{
    /// <summary>Whether the launcher keeps what the program writes in the <c>log</c> it is handed. <c>true</c> by default; the Windows launcher cannot (it has no way to give a child a file as stdout without a pipe that ties the child to its starter), says <c>false</c>, and <c>run-all</c> prints that once.</summary>
    bool WritesLog => true;

    DetachedStart Start(string executable, IReadOnlyList<string> arguments, string log, IReadOnlyDictionary<string, string> environment, string workingDirectory);
}
