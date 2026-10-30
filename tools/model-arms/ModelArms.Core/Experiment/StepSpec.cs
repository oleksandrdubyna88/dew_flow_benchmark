using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>Which tool set a step runs under.</summary>
public enum StepMode { Write, Read, Gate }

/// <summary>What the runner must KNOW about a step: one claude session (<c>Session</c>), a session whose last result's JSON block is written to its verdict output
/// (<c>Review</c>), a step that runs under the gate's lock and reads its status output (<c>Gate</c>), and the task pipeline's coder, launched per task in its worktree
/// (<c>TaskCode</c>). Closed on purpose: everything else about a step is the experiment file's to say (D25). The file's words are <c>session review gate taskCode</c>.</summary>
public enum StepKind { Session, Review, Gate, TaskCode }

/// <summary>The one spelling of a step kind's word in the experiment file.</summary>
public static class StepKinds
{
    private static readonly ImmutableArray<(string Key, StepKind Kind)> Table =
    [
        ("session", StepKind.Session),
        ("review", StepKind.Review),
        ("gate", StepKind.Gate),
        ("taskCode", StepKind.TaskCode),
    ];

    /// <summary>Every word with its kind, in the order the refusal lists them.</summary>
    public static IReadOnlyList<(string Key, StepKind Kind)> Choices => Table;

    /// <summary>The word a step kind is written as.</summary>
    public static string Key(StepKind kind) => Table.Single(row => row.Kind == kind).Key;
}

/// <summary>A step's identity: the key under <c>steps</c> and the id a scheme entry names (<c>"2-hA:hA"</c>). Letters and digits, starting with a letter — open, unlike
/// <see cref="StepKind"/>: an owner adds a step between parts without rebuilding the runner.</summary>
public sealed partial record StepId(string Value)
{
    /// <summary>The id the text spells, or why it is none.</summary>
    public static Outcome<StepId> Parse(string text) => Outcome<StepId>.Success(new StepId(text));

    public override string ToString() => Value;
}

/// <summary>One step as the experiment configures it (D25): its identity and kind, the model alias, the budget in minutes, the tool mode, whether it may start a
/// subagent, the brief template it is filled from (a stem), and the bindings — the outputs it writes (brief field → a path under the arm folder), the field
/// <c>NOTES</c> names, the output a review's verdict (or a gate's status) is, and the <c>.before</c> / <c>.diff</c> pair. <see cref="Notes"/> names one of
/// <see cref="Outputs"/>' fields or an earlier step's, or is empty; <see cref="Verdict"/> and <see cref="Status"/> name one of this step's own outputs or are empty;
/// <see cref="DiffFrom"/> is a step id's value or empty. A field name is bound once within a step and by at most one step of the whole map; a destination is written by at
/// most one producer a scheme places.
/// <para>
/// Equality is by value, <see cref="Outputs"/> included: a record compares a list by reference, and two reads of one file must be equal.
/// </para></summary>
public sealed record StepSpec(
    StepId Id,
    StepKind Kind,
    string Model,
    int Minutes,
    StepMode Mode,
    bool Subagents,
    string Brief,
    IReadOnlyList<OutputBinding> Outputs,
    string Notes,
    string Verdict,
    string Status,
    bool Snapshot,
    string DiffFrom)
{
    /// <summary>What the MODEL is briefed to write: <see cref="Outputs"/> minus the verdict, which the runner derives from the last result. The runner never requires
    /// one of these; it records its absence (E2.S4).</summary>
    public IReadOnlyList<OutputBinding> ModelWritten => Outputs;

    /// <summary>The bound relative path of <paramref name="field"/>, or a failure naming the field and the step.</summary>
    public Outcome<string> OutputPath(string field) => Outcome<string>.Failure("stub");

    public bool Equals(StepSpec? other) =>
        other is not null && Identity == other.Identity && Outputs.SequenceEqual(other.Outputs);

    public override int GetHashCode() => HashCode.Combine(Identity, Outputs.Count);

    private (StepId, StepKind, string, int, StepMode, bool, string, string, string, string, bool, string) Identity =>
        (Id, Kind, Model, Minutes, Mode, Subagents, Brief, Notes, Verdict, Status, Snapshot, DiffFrom);
}
