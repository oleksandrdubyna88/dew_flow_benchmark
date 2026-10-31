using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The declared nothing (D12): the host is not kept awake and <see cref="Describe"/> says why — <c>none — &lt;why&gt;</c>. It writes nothing itself (the factory prints <c>keep-awake: &lt;Describe&gt;</c> once); disposing does nothing.</summary>
internal sealed class NoAwake(string why) : IHostAwake
{
    public string Describe { get; } = $"none — {why}";

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
