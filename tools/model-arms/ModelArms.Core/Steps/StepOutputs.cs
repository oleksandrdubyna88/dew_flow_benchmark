using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;

namespace ModelArms.Core.Steps;

/// <summary>The files the RUNNER derives for a step (plan D21, D25) — as opposed to the ones the model writes: the file a review's <c>verdict</c> binding names, the <c>.diff</c> a <c>diffFrom</c> asks for, and a task coder's
/// head and diff.</summary>
public enum DerivedOutput { Verdict, StepDiff, CoderHead, CoderDiff }

/// <summary>What is on disk, read by the runner and handed in as values: which derived outputs exist, whether the <c>.before</c> the step's diff is taken from exists (the one <c>diffFrom</c> names), whether the task's
/// <c>.before</c> exists, and the last result of the step's final stream (a failure is "no result line").</summary>
public sealed record OutputFacts(IReadOnlySet<DerivedOutput> Present, bool BeforeExists, bool TaskBeforeExists, Outcome<ClaudeResultEnvelope> FinalResult);

/// <summary>What reconciling a done step's derived outputs found.</summary>
public abstract record OutputAnswer
{
    private OutputAnswer()
    {
    }

    /// <summary>Nothing missing.</summary>
    public sealed record Complete : OutputAnswer;

    /// <summary>Missing, and every input is there: write them again, by the function the first run used.</summary>
    public sealed record Regenerate(IReadOnlyList<DerivedOutput> Outputs) : OutputAnswer
    {
        public bool Equals(Regenerate? other) => other is not null && Outputs.SequenceEqual(other.Outputs);

        public override int GetHashCode() => Outputs.Count;
    }

    /// <summary>An input is missing: the step stays without <c>.done</c>, resumable, and the reason is the log's and the <c>stopped</c> line's.</summary>
    public sealed record Resumable(string Reason) : OutputAnswer;
}

/// <summary>Reconciles a step's derived outputs against what is on disk (plan D21, over the step's BINDINGS, never a kind table; E2 §8 items 2 and 3). A missing output whose inputs are present is regenerated, a missing
/// INPUT leaves the step resumable — never done with a hole.</summary>
public static class StepOutputs
{
    /// <summary>What the step must have left, in this order: the verdict when <see cref="StepSpec.Verdict"/> is bound, the diff when <see cref="StepSpec.DiffFrom"/> is set, and for a <c>taskCode</c> step the coder's head and
    /// diff (its bindings are refused by the reader, so it has no other).</summary>
    public static IReadOnlyList<DerivedOutput> Of(StepSpec spec) =>
        spec.Kind == StepKind.TaskCode
            ? [DerivedOutput.CoderHead, DerivedOutput.CoderDiff]
            : [.. Selected(spec.Verdict.Length > 0, DerivedOutput.Verdict), .. Selected(spec.DiffFrom.Length > 0, DerivedOutput.StepDiff)];

    /// <summary>Whether the runner snapshots HEAD into <c>.before</c> before the step's first attempt.</summary>
    public static bool NeedsBefore(StepSpec spec) => spec.Snapshot;

    /// <summary>The missing outputs: none is <see cref="OutputAnswer.Complete"/>; else the FIRST missing output whose input is absent answers <see cref="OutputAnswer.Resumable"/> with the reason; else
    /// <see cref="OutputAnswer.Regenerate"/> with every missing output in <see cref="Of"/>'s order.</summary>
    public static OutputAnswer Reconcile(StepSpec spec, OutputFacts facts)
    {
        var missing = Of(spec).Where(output => !facts.Present.Contains(output)).ToList();
        var reason = missing.Select(output => InputMissing(output, spec, facts)).FirstOrDefault(found => found.Length > 0);
        return missing.Count == 0 ? new OutputAnswer.Complete()
            : reason is { Length: > 0 } ? new OutputAnswer.Resumable(reason)
            : new OutputAnswer.Regenerate(missing);
    }

    private static IEnumerable<DerivedOutput> Selected(bool condition, DerivedOutput output) => condition ? [output] : [];

    /// <summary>Why <paramref name="output"/> cannot be derived, or <c>""</c> when its input is there.</summary>
    private static string InputMissing(DerivedOutput output, StepSpec spec, OutputFacts facts) =>
        output switch
        {
            DerivedOutput.Verdict => HasVerdictText(facts.FinalResult) ? string.Empty : "the review's final stream has no result text — no verdict to read; the step stays resumable",
            DerivedOutput.StepDiff => facts.BeforeExists ? string.Empty : $"{spec.DiffFrom}.before is missing — the step's diff has no base; the step stays resumable",
            _ => facts.TaskBeforeExists ? string.Empty : "the task's .before is missing — the coder's head and diff have no base; the step stays resumable",
        };

    /// <summary>True when the stream's last word is a finished attempt with text: the SAME predicate as the review's answer (<c>StepClassifier.Classify(lastResult, "", AttemptFacts.Ran)</c> is <see cref="StepEnd.Finished"/>), so the two
    /// gates cannot disagree on a captured 429/529 whose <c>is_error</c> says false, or on an earlier success followed by a cut result line (E2 §8 item 51; the E2.S4 follow-up is item 52). An error result's text is the error, never a verdict (the repair script's finding).</summary>
    private static bool HasVerdictText(Outcome<ClaudeResultEnvelope> result) =>
        StepClassifier.Classify(result, string.Empty, AttemptFacts.Ran) is StepEnd.Finished
        && result is Outcome<ClaudeResultEnvelope>.Ok { Value: var envelope } && !string.IsNullOrWhiteSpace(envelope.Result);
}
