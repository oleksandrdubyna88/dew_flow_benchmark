using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The declared nothing (D12): the host is not kept awake and the chain says so once, at construction — <c>keep-awake: none — &lt;why&gt;</c>. Disposing does nothing.</summary>
internal sealed class NoAwake : IHostAwake
{
    public NoAwake(string why, TextWriter output)
    {
        Describe = $"none — {why}";
        output.WriteLine($"keep-awake: {Describe}");
    }

    public string Describe { get; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
