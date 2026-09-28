using System.Text.Json.Nodes;
using Bench.Domain;

namespace Bench.Application.Gate;

/// <summary>How the product is started for one cell attempt: exe + argv, the working directory, the WHOLE child
/// environment (built by <c>CoaiEnvironment</c>, the secret already in it), the file its stderr streams into, and
/// how long the handshake may take.</summary>
public sealed record McpLaunch(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    string StderrPath,
    TimeSpan HandshakeTimeout)
{
    /// <summary>Applied to every stderr line before it is written — the launch's secret becomes <c>[redacted]</c>, so a
    /// product that echoes its environment leaves no key in the artefact root.</summary>
    public Func<string, string> Scrub { get; init; } = static line => line;
}

/// <summary>What a tool call answered: the text of its content (the product answers one text block carrying JSON),
/// whether the RPC layer flagged it an error, and how long it took.</summary>
public sealed record McpToolAnswer(string Text, bool IsError, double Seconds);

/// <summary>One product process spoken to over MCP stdio — the handshake is done before a session exists, so a
/// tool call can never precede it.
/// <para>
/// Every failure the caller can expect is a VALUE: a call that did not answer inside its timeout (the process is
/// then already gone — a session is never left half-alive), a process that exited, an RPC error. After any of
/// them the session is over and every later call is refused saying so.
/// </para></summary>
public interface IMcpSession : IAsyncDisposable
{
    /// <summary><c>serverInfo.version</c> from the handshake.</summary>
    string ServerVersion { get; }

    string ServerName { get; }

    int ProcessId { get; }

    /// <summary>False once the process ended or a call timed out and the session killed it.</summary>
    bool IsAlive { get; }

    /// <summary>Every server-to-client message that was not an answer to a request, in arrival order.</summary>
    IReadOnlyList<string> Notifications { get; }

    Task<Outcome<IReadOnlyList<string>>> ListToolsAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>One <c>tools/call</c> with an ABSOLUTE timeout: at the deadline the process tree is killed and the
    /// call is a failure naming the tool and the budget.</summary>
    Task<Outcome<McpToolAnswer>> CallToolAsync(string tool, JsonObject arguments, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Starts a product process and completes the MCP handshake (<c>initialize</c>, then
/// <c>notifications/initialized</c>). A missing executable, a process that dies before answering and a handshake
/// that times out are refusals, never exceptions.</summary>
public interface IMcpSessionFactory
{
    Task<Outcome<IMcpSession>> OpenAsync(McpLaunch launch, CancellationToken cancellationToken);
}

/// <summary>The ONE session a lane may hold — the type behind "one process per cell".
/// <para>
/// A lane opens a session for the cell it has claimed and disposes it when the cell settles. Opening a second while
/// one is held is a programming error (a lane that claimed a second cell before settling the first, or two cells
/// sharing a process), so it THROWS rather than returning a value: nothing a campaign can meet at run time leads
/// there, and a value would invite somebody to handle it.
/// </para></summary>
public sealed class LaneSlot : IAsyncDisposable
{
    private IMcpSession? _held;
    private Guid _cell;

    public bool IsHolding => _held is not null;

    public async Task<Outcome<IMcpSession>> OpenAsync(Guid cell, IMcpSessionFactory factory, McpLaunch launch, CancellationToken cancellationToken)
    {
        if (_held is not null)
        {
            throw new InvalidOperationException(
                $"this lane already holds the product session of cell {_cell} — one process per cell: settle and release it before opening one for cell {cell}");
        }

        var opened = await factory.OpenAsync(launch, cancellationToken);

        if (opened is Outcome<IMcpSession>.Ok ok)
        {
            _held = ok.Value;
            _cell = cell;
        }

        return opened;
    }

    /// <summary>Ends the held session (the process tree is gone afterwards) and frees the slot.</summary>
    public async Task ReleaseAsync()
    {
        var held = Interlocked.Exchange(ref _held, null);

        if (held is not null)
        {
            await held.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() => await ReleaseAsync();
}
