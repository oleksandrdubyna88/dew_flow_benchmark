using Bench.Domain;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The adapter that does nothing (D11): isolation by file modes is POSIX only, and on Windows <c>lock</c> says so and exits 0. <c>lock</c> short-circuits on an empty plan and on <see cref="IFolderIsolation.Applies"/> false, so every other member is never reached; asked, it throws — a restore that did
/// nothing would be a lie. <c>unlock</c> reaches it on Windows only, where no snapshot can stand (a standing one there is refused by the verb, never restored).</summary>
internal sealed class NoIsolation(string why) : IFolderIsolation
{
    public string Describe => "none — " + why;

    public bool Applies => false;

    public string NotVerifiedLine => string.Empty;

    public bool IsRoot => false;

    public Outcome<Pinned> Pin(IsolationPlan plan) => throw Unreachable();

    public Outcome<string> Apply(Pinned pinned, IsolationPlan plan) => throw Unreachable();

    public IReadOnlyList<string> Verify(IsolationPlan plan) => throw Unreachable();

    public RestoreReport RollBack(Pinned pinned) => throw Unreachable();

    public RestoreReport Restore(IReadOnlyList<SavedMode> reverse, RestoreBound bound) => throw Unreachable();

    public Outcome<string> ReApply(IReadOnlyList<SavedMode> open, IsolationPlan plan) => throw Unreachable();

    public void Dispose()
    {
    }

    private static InvalidOperationException Unreachable() => new("NoIsolation does nothing; the verb never asks it to");
}
