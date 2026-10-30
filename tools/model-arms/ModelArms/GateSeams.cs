using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms;

/// <summary>The two places the gate touches the host in between its steps, handed in so a test can stand where the race is: how a path's facts are
/// read, and what happens right after <c>experiment.json</c> has been read and before it is looked at again. <see cref="Real"/> is the console's own.</summary>
internal sealed record GateSeams(Func<string, FileKind, FactsRead> ReadFacts, Func<string, Task> AfterRead)
{
    public static GateSeams Real { get; } = new(RootFacts.ReadFacts, _ => Task.CompletedTask);
}
