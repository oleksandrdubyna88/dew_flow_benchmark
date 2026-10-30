using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;

namespace ModelArms.Core.Steps;

/// <summary>The files the runner derives.</summary>
public enum DerivedOutput { Verdict, StepDiff, CoderHead, CoderDiff }

/// <summary>What is on disk.</summary>
public sealed record OutputFacts(IReadOnlySet<DerivedOutput> Present, bool BeforeExists, bool TaskBeforeExists, Outcome<ClaudeResultEnvelope> FinalResult);

/// <summary>The answer.</summary>
public abstract record OutputAnswer
{
    private OutputAnswer()
    {
    }

    public sealed record Complete : OutputAnswer;

    public sealed record Regenerate(IReadOnlyList<DerivedOutput> Outputs) : OutputAnswer
    {
        public bool Equals(Regenerate? other) => other is not null && Outputs.SequenceEqual(other.Outputs);

        public override int GetHashCode() => Outputs.Count;
    }

    public sealed record Resumable(string Reason) : OutputAnswer;
}

/// <summary>Reconciles derived outputs.</summary>
public static class StepOutputs
{
    public static IReadOnlyList<DerivedOutput> Of(StepSpec spec) => [];

    public static bool NeedsBefore(StepSpec spec) => spec.Snapshot;

    public static OutputAnswer Reconcile(StepSpec spec, OutputFacts facts) => new OutputAnswer.Complete();
}
