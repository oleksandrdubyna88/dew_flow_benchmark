using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms;

/// <summary>The places the gate touches the host, handed in so a test can stand where the race is or be another host: how a path's facts are read, what happens right after <c>experiment.json</c> has been read and before it is looked at again, and the
/// <see cref="Road"/> the host takes (which facts are read, which are judged, and the one line that says what was not verified — E3.S0). <see cref="Real"/> is the console's own.</summary>
internal sealed record GateSeams(Func<string, FileKind, FactsRead> ReadFacts, Func<string, Task> AfterRead, PlatformRoad Road)
{
    public static GateSeams Real { get; } = new(RootFacts.ReadFacts, _ => Task.CompletedTask, PlatformFactsReader.CurrentRoad);
}
