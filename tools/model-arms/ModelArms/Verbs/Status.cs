using System.Text;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;

namespace ModelArms.Verbs;

/// <summary>The two ways <c>status</c> looks at the file system, and the only two: a bounded read of ONE file by its name (C16 — no link followed, no FIFO waited on, never more than the limit) and whether ONE name is there. There is no member that lists a folder, so no code that
/// holds this seam can: an unlisted folder is at mode 300 while the isolation stands (traversable, never listable, D11), and a verb that listed one would fail at the one moment it is wanted. A test hands in a recording pair to see every name asked.</summary>
internal sealed record StatusReads(Func<string, int, BoundedRead> Read, Func<string, Outcome<bool>> Present)
{
    /// <summary>This host's: <see cref="BoundedFile"/> on the host's bounded road, and <see cref="KernelFacts.Present"/> (an entry by that name, a dangling link included; a kernel that cannot say is a failure, never "absent").</summary>
    public static StatusReads Real { get; } = new((path, limit) => BoundedFile.Read(path, limit, PlatformFactsReader.CurrentRoad), KernelFacts.Present);
}

/// <summary><c>status</c> (plan §4.1, D11, F0): where every arm of the experiment stands, read from its files BY NAME and printed — read-only. It lists the arms of the arm map in its order, then the arms of the runs that the map lacks (<c>not made</c>), each with its made and checked
/// markers, its runner's pid and liveness, every step of its scheme (<see cref="ArmReport"/>) and its last timing line as recorded; every run's three markers; and the isolation snapshot's one line. <b>It creates nothing, restores nothing and lists no folder</b> — a path asked for is one
/// <see cref="RootPaths"/> or <see cref="ArmPaths"/> spells, through <see cref="StatusReads"/>, and every line goes out through <see cref="ArmMap.Scrub"/>, so no token is ever printed. Exit 0 once printed; 4 when the arm map is refused — by the road (a link, a FIFO, too big) or as a map. A root
/// with no map yet is every arm of every run <c>not made</c>: before <c>make</c> that is the answer, not an error.
/// <para>
/// What it does not do: take or report the gate lock (<c>gate-lock status</c> says it), print a cost (E3 touches none), or list the task pipeline's steps (a <c>tasks</c> entry is one line until E4.S3).
/// </para></summary>
public static class StatusVerb
{
    private const string Name = "model-arms status";

    /// <summary>The most the arm map may hold (E3.S4): an experiment has tens of arms.</summary>
    private const int MapLimit = 1024 * 1024;

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, StatusReads.Real);

    /// <summary>The same with the reads handed in: how a test sees every name asked, and what a host that refuses a read would show.</summary>
    internal static Task<int> RunAsync(ExperimentInput input, StatusReads reads) =>
        Task.FromResult(MapOf(input.Root, reads).Match(map => Printed(input, reads, map), reason => Refused(input, reason)));

    private static int Refused(ExperimentInput input, string reason)
    {
        input.Verb.Error.WriteLine($"{Name}: arms.map.json is refused: {string.Join('\n', reason.Split('\n').Select(ExecutableRule.Printable))}");
        return ExitCodes.Configuration;
    }

    private static Outcome<ArmMap> MapOf(RootPaths root, StatusReads reads) =>
        reads.Read(root.ArmMapFile, MapLimit) switch
        {
            BoundedRead.Bytes bytes => ArmMap.Read(Encoding.UTF8.GetString(bytes.Value)),
            BoundedRead.Refused refused => Outcome<ArmMap>.Failure(refused.Reason),
            _ => Outcome<ArmMap>.Success(ArmMap.Empty),
        };

    private static int Printed(ExperimentInput input, StatusReads reads, ArmMap map)
    {
        foreach (var line in Lines(input, reads, map))
        {
            input.Verb.Stopping.ThrowIfCancellationRequested();
            input.Verb.Output.WriteLine(map.Scrub(line));
        }

        return ExitCodes.Pass;
    }

    /// <summary>The whole report, in order: the root, the isolation line, every run, then every arm. Lazy: a stop between two arms ends the walk.</summary>
    private static IEnumerable<string> Lines(ExperimentInput input, StatusReads reads, ArmMap map)
    {
        var root = input.Root;
        yield return $"{Name}: {root.Root}";
        yield return "isolation: " + IsolationLine.Of(IsolationFiles.State(root, reads.Read));
        foreach (var run in input.Experiment.Runs)
        {
            yield return $"run {run.Name}: locked {Marker(reads, root.RunMarker(run.Name, RunStage.Locked))}, unlocked {Marker(reads, root.RunMarker(run.Name, RunStage.Unlocked))}, arms done {Marker(reads, root.RunMarker(run.Name, RunStage.ArmsDone))}";
        }

        // Every arm of the map in its order, then each arm of the runs that the map lacks, once.
        var arms = map.Entries.Select(entry => entry.Key).Concat(RunArms.Of(input.Experiment.Runs)).Distinct(StringComparer.Ordinal);
        foreach (var line in arms.SelectMany(arm => ArmReport.Lines(input.Experiment, root, reads, map, arm)))
        {
            yield return line;
        }
    }

    /// <summary><c>yes</c> or <c>no</c> for a marker by name; a kernel that cannot say is <c>unknown (why)</c> — never a <c>no</c> read off a failure.</summary>
    internal static string Marker(StatusReads reads, string path) =>
        reads.Present(path).Match(there => there ? "yes" : "no", reason => $"unknown ({ExecutableRule.Printable(reason)})");
}
