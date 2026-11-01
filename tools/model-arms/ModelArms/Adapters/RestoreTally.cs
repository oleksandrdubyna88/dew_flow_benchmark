using System.Collections.Immutable;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>What the entries of one restore came to, entry by entry — the one place the two adapters turn an <see cref="EntryVerdict"/> and the outcome of its <c>chmod</c> into the five lists of a <see cref="RestoreReport"/>: restored by a call, already at the saved mode (a harmless retry, said), skipped, left to the operator, failed.
/// Immutable: <see cref="Add"/> answers a NEW tally, so the accumulation is a fold (<c>Aggregate</c>) and nothing is mutated.</summary>
internal sealed record RestoreTally
{
    public static RestoreTally Empty { get; } = new();

    private ImmutableList<string> Restored { get; init; } = [];

    private ImmutableList<string> Harmless { get; init; } = [];

    private ImmutableList<string> Skipped { get; init; } = [];

    private ImmutableList<string> Unresolved { get; init; } = [];

    private ImmutableList<string> Failed { get; init; } = [];

    /// <summary><paramref name="restore"/> sets the saved mode and answers a refusal in words, or the empty string; it is called only for a verdict that asks for it.</summary>
    public RestoreTally Add(EntryVerdict verdict, string path, Func<string> restore) =>
        verdict switch
        {
            EntryVerdict.Restore => restore() is { Length: > 0 } refusal ? this with { Failed = Failed.Add(refusal) } : this with { Restored = Restored.Add(path) },
            EntryVerdict.AlreadyRestored => this with { Harmless = Harmless.Add(path) },
            EntryVerdict.Unresolved unresolved => this with { Unresolved = Unresolved.Add(unresolved.Sentence) },
            EntryVerdict.Skip skip => this with { Skipped = Skipped.Add(skip.Sentence) },
            EntryVerdict.Fail fail => this with { Failed = Failed.Add(fail.Sentence) },
            _ => this with { Failed = Failed.Add(path) },
        };

    public RestoreReport Report() => new(Restored, Skipped, Unresolved, Failed) { Harmless = Harmless };
}
