using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Application.Scouts;

/// <summary>One scored cell read back from its stream: the cell, its task and arm, and each scout's report by ordinal — exactly the reading
/// settlement made (<see cref="ScoutCellReading.Reports"/>), so <c>score</c>, <c>assess</c> and the hand-check see the same scouts as the
/// records do.</summary>
public sealed record ScoutCellOutput(ScoutCell Cell, ScoutTask Task, ScoutArm Arm, SubagentTranscript Transcript, IReadOnlyList<ScoutCallReport> Reports)
{
    /// <summary>Every KEY FILES entry of the team, as the scouts wrote them.</summary>
    public IReadOnlyList<string> KeyFilesAsWritten => [.. Reports.SelectMany(r => ScoutOutput.KeyFiles(r.Text).Paths)];
}

/// <summary>The SCORED cells of a run — each lineage at its highest settled generation — read back from the stream of the attempt that settled
/// them (the cell's claim attempt: its attempt number is the cell's attempt count). A scored cell whose stream is gone from the artefact root
/// is reported as missing rather than silently skipped.</summary>
public static class ScoutSettledOutputs
{
    public sealed record Read(IReadOnlyList<ScoutCellOutput> Outputs, IReadOnlyList<ScoutCell> StreamMissing);

    public static Read Of(IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutAttempt> attempts, IScoutStreams streams)
    {
        var byTask = tasks.ToDictionary(t => t.Id.Value, StringComparer.Ordinal);
        var scored = ScoutGenerations.LatestSettled(cells).Where(c => c.Outcome == ScoutCellOutcome.Scored && byTask.ContainsKey(c.Task.Value)).ToList();
        var outputs = new List<ScoutCellOutput>();
        var missing = new List<ScoutCell>();

        foreach (var cell in scored)
        {
            var stream = attempts.FirstOrDefault(a => a.CellId == cell.Id && a.Number == cell.Attempts) is { } attempt ? streams.StreamText(attempt) : string.Empty;

            if (stream.Length == 0)
            {
                missing.Add(cell);
                continue;
            }

            outputs.Add(Output(cell, byTask[cell.Task.Value], stream));
        }

        return new Read(outputs, missing);
    }

    private static ScoutCellOutput Output(ScoutCell cell, ScoutTask task, string stream)
    {
        var arm = ScoutArms.Of(cell.Arm);
        var transcript = ClaudeSubagents.Read(stream);

        return new ScoutCellOutput(cell, task, arm, transcript, ScoutCellReading.Reports(transcript, ScoutPrompts.ForCell(task, arm).Sha256s));
    }
}
