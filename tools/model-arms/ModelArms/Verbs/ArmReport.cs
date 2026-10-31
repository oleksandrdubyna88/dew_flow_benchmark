using System.Text;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Bench.Infrastructure.Process;

namespace ModelArms.Verbs;

/// <summary>One arm's lines for <c>status</c>, every file opened by NAME through <see cref="StatusReads"/>: <c>arm &lt;arm&gt; (scheme S): made .., checked .., runner ..</c>, one line per step of the scheme (<see cref="StepStatuses"/>) and the arm's last timing line as it was recorded.
/// An attempt is counted by probing <c>Stream(stem, n)</c> for n = 1, 2, … until one is absent — never by listing <c>out/</c>. The limits are small on purpose: a marker is a few dozen bytes, a pid file fewer, and <c>timing.jsonl</c> is the one file that grows.</summary>
internal static class ArmReport
{
    private const int MarkerLimit = 4 * 1024;
    private const int PidLimit = 64;
    private const int TimingLimit = 16 * 1024 * 1024;

    public static IEnumerable<string> Lines(Core.Experiment.Experiment experiment, RootPaths root, StatusReads reads, ArmMap map, string arm)
    {
        var scheme = ArmNames.SchemeOf(arm).Match(name => name, _ => string.Empty);
        if (map.TokenOf(arm) is not Outcome<string>.Ok { Value: var token })
        {
            return [$"arm {arm} (scheme {scheme}): not made"];
        }

        return experiment.Schemes.FirstOrDefault(each => each.Name == scheme) is { } found
            ? Made(root, reads, arm, found, root.Arm(token), map)
            : [$"arm {arm}: its scheme {scheme} is not among the experiment's schemes ({string.Join(", ", experiment.Schemes.Select(each => each.Name))})"];
    }

    private static IEnumerable<string> Made(RootPaths root, StatusReads reads, string arm, Scheme scheme, ArmPaths paths, ArmMap map)
    {
        yield return $"arm {arm} (scheme {scheme.Name}): made {StatusVerb.Marker(reads, root.Made(arm))}, checked {StatusVerb.Marker(reads, root.ObjectiveMarker(arm))}, runner {Runner(reads, root.PidFile(arm), map.Scrub)}";
        var timing = reads.Read(paths.Timing, TimingLimit);
        var lines = timing is BoundedRead.Bytes bytes ? TimingLines.Read(Encoding.UTF8.GetString(bytes.Value)) : [];
        foreach (var entry in scheme.Entries)
        {
            yield return entry is SchemeEntry.Step step ? "  " + StepStatuses.Line(step.Name, Decided(StepStatuses.Of(Evidence(reads, paths, step.Name, lines)), timing), map.Scrub) : "  tasks: listed when the task pipeline lands (E4)";
        }

        yield return "  last line: " + LastLine(timing);
    }

    /// <summary>A refused <c>timing.jsonl</c> is NOT an absent one: a step that no marker decided (not <c>unreadable</c>, <c>continuing</c> or <c>done</c>) is read from end lines nobody could read, so it is <c>unreadable — timing.jsonl: why</c> rather than <i>interrupted</i> or <i>not started</i>.</summary>
    private static StepState Decided(StepState state, BoundedRead timing) =>
        timing is BoundedRead.Refused refused && state is not (StepState.Unreadable or StepState.Continuing or StepState.Done) ? new StepState.Unreadable("timing.jsonl: " + refused.Reason) : state;

    /// <summary><c>none recorded</c> (no pid file), <c>pid N alive</c>, <c>pid N gone</c>, or <c>unreadable — why</c> (a link, a FIFO, too big, or text that is no pid).</summary>
    private static string Runner(StatusReads reads, string path, Func<string, string> scrub) =>
        reads.Read(path, PidLimit) switch
        {
            BoundedRead.Absent => "none recorded",
            BoundedRead.Refused refused => "unreadable — " + ExecutableRule.Printable(refused.Reason),
            BoundedRead.Bytes bytes => Pid(Encoding.UTF8.GetString(bytes.Value).Trim(), scrub),
            _ => "none recorded",
        };

    private static string Pid(string text, Func<string, string> scrub) =>
        RunnerPid.Parse(text) is { } pid
            ? $"pid {pid} " + (WorkerLiveness.ProcessIsAlive(pid) ? "alive" : "gone")
            : $"unreadable — the pid file holds '{ExecutableRule.Printable(ExecutableRule.Cut(scrub(text), 20))}', not a pid";

    private static StepEvidence Evidence(StatusReads reads, ArmPaths paths, string stem, IReadOnlyList<TimingLine> timing)
    {
        var (streams, problem) = Streams(reads, paths, stem);
        var done = Marker(reads, paths.Done(stem), DoneMarkers.Read);
        return new StepEvidence(stem, problem.Length > 0 && done is Read<DoneMarker>.Absent ? new Read<DoneMarker>.Refused(problem) : done, Marker(reads, paths.Continue(stem), ContinueMarkers.Read), streams, timing);
    }

    /// <summary>The attempts on disk: <c>Stream(stem, n)</c> probed for 1, 2, … until one is absent. A probe the kernel could not answer ends the count and is returned as the problem.</summary>
    private static (int Count, string Problem) Streams(StatusReads reads, ArmPaths paths, string stem)
    {
        var count = 0;
        while (true)
        {
            var there = reads.Present(paths.Stream(stem, count + 1));
            if (there is Outcome<bool>.Fail failed)
            {
                return (count, failed.Reason);
            }

            if (there is not Outcome<bool>.Ok { Value: true })
            {
                return (count, string.Empty);
            }

            count++;
        }
    }

    /// <summary>A marker by name: absent, its parsed content, or refused — by the road, or because it does not parse. A refusal names the file.</summary>
    private static Read<T> Marker<T>(StatusReads reads, string path, Func<string, Outcome<T>> parse) =>
        reads.Read(path, MarkerLimit) switch
        {
            BoundedRead.Bytes bytes => parse(Encoding.UTF8.GetString(bytes.Value)).Match<Read<T>>(value => new Read<T>.Present(value), reason => new Read<T>.Refused($"{path}: {reason}")),
            BoundedRead.Refused refused => new Read<T>.Refused(refused.Reason),
            _ => new Read<T>.Absent(),
        };

    /// <summary>The last non-blank line as it is on disk: not parsed and written again, so the deadline text and the key order are the runner's own. A file that is not there, is empty or was refused says so.</summary>
    private static string LastLine(BoundedRead timing) =>
        timing switch
        {
            BoundedRead.Bytes bytes => Encoding.UTF8.GetString(bytes.Value).Split('\n').Select(line => line.TrimEnd('\r')).LastOrDefault(line => !string.IsNullOrWhiteSpace(line)) is { } last
                ? ExecutableRule.Printable(last)
                : "none recorded",
            BoundedRead.Refused refused => "unreadable — " + ExecutableRule.Printable(refused.Reason),
            _ => "none recorded",
        };
}
