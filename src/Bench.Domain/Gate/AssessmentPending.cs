namespace Bench.Domain.Gate;

/// <summary>How many findings one assessor call reads — at most <see cref="Max"/>, the other harness's batch of 24. A
/// larger batch is refused at the flag, never truncated downstream: the prompt carries every row, and a batch the
/// assessor cannot hold is one it answers partially.</summary>
public sealed record BatchSize
{
    public const int Max = 24;

    private BatchSize(int value) => Value = value;

    public int Value { get; }

    public static BatchSize Default { get; } = new(Max);

    public static Outcome<BatchSize> Of(int value) =>
        value is >= 1 and <= Max
            ? Outcome<BatchSize>.Success(new BatchSize(value))
            : Outcome<BatchSize>.Failure($"a batch holds 1 to {Max} findings, got {value} — the strict rubric was calibrated on batches of {Max}");
}

/// <summary>Which blinded findings still need a reading from ONE assessor under ONE rubric, and in which batches.
/// <para>
/// A finding is pending when this assessor has no verdict on it under this rubric's hash, or ONLY
/// <see cref="Verdict.AssessmentFailure"/> rows — so a later pass re-asks exactly the failed findings, and the new
/// reading supersedes the failure (<see cref="GatePopulation"/> prefers a real reading). Another assessor's verdicts
/// are not this one's: the agreement figure needs both.
/// </para></summary>
public static class AssessmentPending
{
    public static IReadOnlyList<BlindKeyEntry> Of(
        IReadOnlyList<BlindKeyEntry> entries, IReadOnlyList<GateVerdict> verdicts, GateReviewerId assessor, Rubric rubric)
    {
        var read = verdicts
            .Where(v => v.Assessor == assessor && v.Rubric == rubric && v.Reading.CountsInRates)
            .Select(v => (v.RunId, v.FindingOrdinal))
            .ToHashSet();

        return [.. entries.Where(e => !read.Contains((e.RunId, e.Ordinal)))];
    }

    /// <summary>Per task (in task order), in the key's order, cut into batches of at most <paramref name="size"/>. A batch
    /// never spans two tasks: the prompt names one task, one checkout and one seed list.</summary>
    public static IReadOnlyList<IReadOnlyList<BlindKeyEntry>> Batches(IReadOnlyList<BlindKeyEntry> pending, BatchSize size) =>
        [.. pending
            .GroupBy(e => e.Task.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g.Chunk(size.Value).Select(chunk => (IReadOnlyList<BlindKeyEntry>)[.. chunk]))];
}
