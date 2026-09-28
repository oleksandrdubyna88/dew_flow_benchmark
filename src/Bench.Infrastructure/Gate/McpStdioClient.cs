using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Infrastructure.Process;

namespace Bench.Infrastructure.Gate;

/// <summary>A hand-rolled MCP client over newline JSON-RPC on stdio — a port of the calibration harness's
/// <c>McpStdio</c> and <c>coai-bench</c>'s <c>GateClient</c>, and deliberately no SDK: the product is the thing under
/// test, and a client that owns every byte on the wire is one whose behaviour is visible.
/// <para>
/// One instance is one process (<see cref="ProcessSession"/>), and it exists only after the handshake succeeded, so
/// no tool call can precede <c>initialize</c> / <c>notifications/initialized</c>. A call's timeout is ABSOLUTE: at the
/// deadline the process tree is killed — a review that hangs does not hold its lane, and a session is never left
/// half-alive — and every later call is refused because the session is over.
/// </para></summary>
public sealed class McpStdioClient : IMcpSession
{
    public const string ProtocolVersion = "2025-06-18";

    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonObject>> _waiting = new();
    private readonly ConcurrentQueue<string> _notifications = new();
    private ProcessSession _process = null!;
    private long _nextId;
    private volatile bool _over;
    private string _overReason = string.Empty;

    private McpStdioClient()
    {
    }

    public string ServerVersion { get; private set; } = string.Empty;

    public string ServerName { get; private set; } = string.Empty;

    public int ProcessId => _process.ProcessId;

    public bool IsAlive => !_over && !_process.HasExited;

    public IReadOnlyList<string> Notifications => [.. _notifications];

    /// <summary>Starts the process and completes the handshake inside <see cref="McpLaunch.HandshakeTimeout"/>.</summary>
    public static async Task<Outcome<IMcpSession>> StartAsync(McpLaunch launch, CancellationToken cancellationToken)
    {
        var client = new McpStdioClient();
        var start = ProcessSession.Start(
            new SessionLaunch(launch.Executable, launch.Arguments, launch.WorkingDirectory, launch.Environment, launch.StderrPath) { Scrub = launch.Scrub },
            client.Receive);

        if (start is SessionStart.NotFound missing)
        {
            return Outcome<IMcpSession>.Failure($"the product '{missing.Executable}' could not be started — {missing.Reason}");
        }

        client._process = ((SessionStart.Started)start).Session;
        _ = client._process.Exited.ContinueWith(_ => client.End("the product exited"), TaskScheduler.Default);

        return await client.HandshakeAsync(launch.HandshakeTimeout, cancellationToken);
    }

    public async Task<Outcome<IReadOnlyList<string>>> ListToolsAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        await RequestAsync("tools/list", [], timeout, cancellationToken) switch
        {
            Outcome<JsonObject>.Ok ok => Outcome<IReadOnlyList<string>>.Success(
                [.. (ok.Value["tools"] as JsonArray ?? []).Select(t => t?["name"]?.GetValue<string>() ?? string.Empty).Where(n => n.Length > 0)]),
            Outcome<JsonObject>.Fail fail => Outcome<IReadOnlyList<string>>.Failure(fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    public async Task<Outcome<McpToolAnswer>> CallToolAsync(string tool, JsonObject arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var answered = await RequestAsync("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments.DeepClone() }, timeout, cancellationToken);

        return answered switch
        {
            Outcome<JsonObject>.Ok ok => Outcome<McpToolAnswer>.Success(new McpToolAnswer(TextOf(ok.Value), IsError(ok.Value), clock.Elapsed.TotalSeconds)),
            Outcome<JsonObject>.Fail fail => Outcome<McpToolAnswer>.Failure($"{tool}: {fail.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    public async ValueTask DisposeAsync()
    {
        End("the session was closed");
        await _process.DisposeAsync();
    }

    private async Task<Outcome<IMcpSession>> HandshakeAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var initialized = await RequestAsync(
            "initialize",
            new JsonObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "bench-gate", ["version"] = "1" },
            },
            timeout,
            cancellationToken);

        if (initialized is Outcome<JsonObject>.Fail fail)
        {
            await _process.DisposeAsync();
            return Outcome<IMcpSession>.Failure($"the MCP handshake did not complete — {fail.Reason}");
        }

        var result = ((Outcome<JsonObject>.Ok)initialized).Value;
        ServerName = result["serverInfo"]?["name"]?.GetValue<string>() ?? string.Empty;
        ServerVersion = result["serverInfo"]?["version"]?.GetValue<string>() ?? string.Empty;
        Write(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });

        return Outcome<IMcpSession>.Success(this);
    }

    private async Task<Outcome<JsonObject>> RequestAsync(string method, JsonObject parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_over)
        {
            return Outcome<JsonObject>.Failure($"the session is over — {_overReason}");
        }

        var id = Interlocked.Increment(ref _nextId);
        var waiting = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting[id] = waiting;

        if (!Write(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters }))
        {
            _waiting.TryRemove(id, out _);
            return Outcome<JsonObject>.Failure("the product's stdin is closed — it exited");
        }

        return await AwaitAsync(method, id, waiting.Task, timeout, cancellationToken);
    }

    private async Task<Outcome<JsonObject>> AwaitAsync(string method, long id, Task<JsonObject> answer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = Task.Delay(timeout, cancellationToken);
        var finished = await Task.WhenAny(answer, deadline);
        _waiting.TryRemove(id, out _);

        if (finished == answer)
        {
            return Unwrap(await answer);
        }

        // The deadline, or the caller's stop: either way the process goes with the call, so nothing is left running
        // that the lane no longer watches.
        End($"{method} did not answer within {Seconds(timeout)} s — the product was killed");
        await _process.KillAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return Outcome<JsonObject>.Failure($"did not answer within {Seconds(timeout)} s — the session was killed with it");
    }

    private static Outcome<JsonObject> Unwrap(JsonObject message) =>
        message["error"] is JsonObject error
            ? Outcome<JsonObject>.Failure($"the server answered an error — {error.ToJsonString()}")
            : message[ClosedMarker] is JsonValue closed
                ? Outcome<JsonObject>.Failure(closed.GetValue<string>())
                : Outcome<JsonObject>.Success(message["result"] as JsonObject ?? []);

    private const string ClosedMarker = "__closed";

    private void Receive(string line)
    {
        if (line == ProcessSession.EndOfStream)
        {
            End("the product exited (its stdout closed)");
            return;
        }

        if (Parse(line) is not { } message)
        {
            _notifications.Enqueue(line.Length <= 500 ? line : line[..500]);
            return;
        }

        if (message["id"] is JsonValue idValue && idValue.TryGetValue<long>(out var id) && (message.ContainsKey("result") || message.ContainsKey("error")))
        {
            if (_waiting.TryRemove(id, out var waiting))
            {
                waiting.TrySetResult(message);
            }

            return;
        }

        _notifications.Enqueue(line);
    }

    private static JsonObject? Parse(string line)
    {
        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Marks the session over and fails every call still waiting, saying why.</summary>
    private void End(string reason)
    {
        if (!_over)
        {
            _overReason = reason;
            _over = true;
        }

        foreach (var (id, waiting) in _waiting)
        {
            if (_waiting.TryRemove(id, out _))
            {
                waiting.TrySetResult(new JsonObject { [ClosedMarker] = $"{_overReason} before it answered" });
            }
        }
    }

    private bool Write(JsonObject message) => _process.WriteLine(message.ToJsonString());

    private static string TextOf(JsonObject result) =>
        string.Join('\n', (result["content"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(c => c["type"]?.GetValue<string>() == "text")
            .Select(c => c["text"]?.GetValue<string>() ?? string.Empty));

    private static bool IsError(JsonObject result) => result["isError"]?.GetValue<bool>() ?? false;

    private static string Seconds(TimeSpan timeout) => timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture);
}

/// <summary>The adapter behind <see cref="IMcpSessionFactory"/>.</summary>
public sealed class McpStdioSessionFactory : IMcpSessionFactory
{
    public Task<Outcome<IMcpSession>> OpenAsync(McpLaunch launch, CancellationToken cancellationToken) =>
        McpStdioClient.StartAsync(launch, cancellationToken);
}
