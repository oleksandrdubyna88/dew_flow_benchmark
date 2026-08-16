using System.ComponentModel;

namespace Bench.Infrastructure.Process;

/// <summary>Asks this machine whether a process is still running.
/// <para>
/// It is the one fact the sweep cannot get from SQL — a set-based update cannot ask the operating system
/// whether pid 1234 is alive — and it is why the sweep loads its stale candidates before deciding. That
/// costs nothing in practice: claimed-and-stale rows are ~0 in a healthy system, and the staleness
/// predicate stays in the WHERE so only those few are ever materialised.
/// </para></summary>
public static class WorkerLiveness
{
    /// <summary>This machine, as it names itself. The same string the claim recorded, so the comparison a
    /// sweep makes is between two answers to the same question.</summary>
    public static string ThisHost => Environment.MachineName;

    /// <summary>Whether a pid on THIS machine is running.
    /// <para>
    /// The three catches are three different facts, and only two of them mean "gone". Being <b>refused</b>
    /// an answer is not being told the process ended, so an unanswerable owner counts as ALIVE: leaving a
    /// stale claim costs one retry after the next sweep, ending a live one costs two workers measuring the
    /// same leg and a settle refused for an owner mismatch nobody caused.
    /// </para></summary>
    public static bool ProcessIsAlive(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false; // No such process: the owner is gone, which is exactly what the sweep exists for.
        }
        catch (InvalidOperationException)
        {
            return false; // It ended between the lookup and the question.
        }
        catch (Win32Exception)
        {
            // Access denied, or the pid belongs to a protected process. We were refused, not answered.
            return true;
        }
    }
}
