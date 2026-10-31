using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>What the entries of one restore came to, entry by entry — the one place the two adapters turn an <see cref="EntryVerdict"/> and the outcome of its <c>chmod</c> into the five lists of a <see cref="RestoreReport"/>: restored by a call, already at the saved mode (a harmless retry, said), skipped, left to the operator, failed.</summary>
internal sealed class RestoreTally
{
    private readonly List<string> _restored = [];
    private readonly List<string> _harmless = [];
    private readonly List<string> _skipped = [];
    private readonly List<string> _unresolved = [];
    private readonly List<string> _failed = [];

    /// <summary><paramref name="restore"/> sets the saved mode and answers a refusal in words, or the empty string; it is called only for a verdict that asks for it.</summary>
    public void Add(EntryVerdict verdict, string path, Func<string> restore)
    {
        var (list, text) = verdict switch
        {
            EntryVerdict.Restore => Called(path, restore()),
            EntryVerdict.AlreadyRestored => (_harmless, path),
            EntryVerdict.Unresolved unresolved => (_unresolved, unresolved.Sentence),
            EntryVerdict.Skip skip => (_skipped, skip.Sentence),
            EntryVerdict.Fail fail => (_failed, fail.Sentence),
            _ => (_failed, path),
        };
        list.Add(text);
    }

    public RestoreReport Report() => new(_restored, _skipped, _unresolved, _failed) { Harmless = _harmless };

    private (List<string> List, string Text) Called(string path, string refusal) => refusal.Length == 0 ? (_restored, path) : (_failed, refusal);
}
