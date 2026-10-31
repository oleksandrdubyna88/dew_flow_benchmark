namespace ModelArms.Core.Ports;

/// <summary>The host kept awake for a scope (D12, E5.S2): started by its factory, and ended by disposing — the scope is the object's life. Three adapters answer it: Windows in-process, a held <c>pwsh.exe</c> from WSL, and the declared nothing.</summary>
public interface IHostAwake : IAsyncDisposable
{
    /// <summary>What this adapter does, in one phrase the chain prints after <c>keep-awake: </c>.</summary>
    string Describe { get; }
}
