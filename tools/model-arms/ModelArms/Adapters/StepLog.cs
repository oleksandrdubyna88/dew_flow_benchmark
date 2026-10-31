using System.Globalization;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>The runner's log and its stop line (E1 §8 item 14): every line is <c>&lt;utc&gt; [&lt;arm&gt;] &lt;text&gt;</c>, passed through <see cref="ArmMap.Scrub"/> before it is written — the log is the verb's output, and no token reaches it. A stop is
/// written to <c>timing.jsonl</c> first and to the log second, and needs no cancellation token: a stop must leave its record, so nothing here waits on the one that may be cancelled.</summary>
internal sealed class StepLog(TextWriter writer, ArmMap map, TimeProvider clock, string arm, ArmFiles files)
{
    public void Line(string text)
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{clock.GetUtcNow():yyyy-MM-dd'T'HH:mm:ss'Z'} [{arm}] ") + map.Scrub(text));
        writer.Flush();
    }

    /// <summary>The arm stops with <paramref name="exit"/> (D16): the <c>stopped</c> timing line, the log line and the outcome.</summary>
    public StepOutcome.Stopped Stop(string step, int exit, string why)
    {
        files.AppendTiming(new TimingLine.Stopped(arm, step, exit, why, clock.GetUtcNow()));
        Line($"{step} stopped (exit {exit.ToString(CultureInfo.InvariantCulture)}): {why}");
        return new StepOutcome.Stopped(exit, why);
    }
}

/// <summary>What the runner is given, once: the launcher, the transcript probe, the clock, the wait, the probe and the log's writer and map.</summary>
internal sealed record StepDeps(
    Bench.Application.ICliAgentTranscripts Cli,
    ModelArms.Core.Ports.ISessionTranscripts Transcripts,
    TimeProvider Clock,
    Func<TimeSpan, CancellationToken, Task> Wait,
    TextWriter Log,
    ArmMap Map,
    StepProbe Probe);
