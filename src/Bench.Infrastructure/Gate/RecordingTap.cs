using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bench.Application.Gate;
using Bench.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Gate;

/// <summary>A recording pass-through in front of one cell's <c>api</c> reviewer — a port of the calibration harness's
/// <c>Tap</c>. The product's vendor row points at <c>http://127.0.0.1:&lt;port&gt;/v1</c>; every request is forwarded to
/// the reviewer's real base url and the exchange is recorded under the attempt's <c>tap/</c> folder.
/// <list type="bullet">
/// <item><b>The body is written BEFORE forwarding</b> (<c>call-NN.request.json</c>), so a call that never came back still
/// leaves its ceiling and its fields on disk.</item>
/// <item><b><c>Authorization</c> travels in memory only.</b> The facts file lists request headers as sorted NAMES without
/// it, and the value (and its bearer token) is scrubbed from every body before it is written — a vendor that echoes
/// the header in a 401 leaves <c>[redacted]</c> on disk. The product still receives the vendor's bytes unmodified.</item>
/// <item><b>An absolute deadline per call</b> cancels the upstream exchange — the connection is aborted, not left
/// dripping — and the call is marked <c>closed_by_deadline</c>.</item>
/// <item><b>Nothing is left open at the end</b>: <see cref="CloseAsync"/> waits for in-flight calls, then aborts and MARKS
/// any still open, so every forwarded request has a facts file before the cell settles.</item>
/// </list>
/// The upstream is fixed at start; a request selects only a path UNDER it, never a host.</summary>
public sealed class RecordingTap : IRecordingTap
{
    private const string Prefix = "/v1";

    private static readonly HashSet<string> DroppedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Connection", "Accept-Encoding", "Transfer-Encoding", "Expect",
    };

    /// <summary>Request headers whose VALUES are credentials: forwarded from memory, never written, and scrubbed from
    /// every body and every kept response header before it reaches the disk.</summary>
    private static readonly string[] CredentialHeaders = ["Authorization", "Proxy-Authorization", "x-api-key", "api-key", "x-goog-api-key", "Cookie"];

    private static readonly string[] KeptResponseHeaders =
    [
        "content-type", "x-request-id", "x-ratelimit-remaining-requests", "x-ratelimit-remaining-tokens", "retry-after", "date",
        "x-grok-conv-id", "x-envoy-upstream-service-time",
    ];

    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private readonly Uri _upstream;
    private readonly string _directory;
    private readonly TimeSpan _deadline;
    private readonly CancellationTokenSource _closing = new();
    private readonly ConcurrentDictionary<int, bool> _inflight = new();
    private int _calls;
    private int _markedAtClose;

    private readonly long _maxBody;

    private RecordingTap(WebApplication app, Uri upstream, string directory, TimeSpan deadline, long maxBody)
    {
        _app = app;
        _upstream = upstream;
        _directory = directory;
        _deadline = deadline;
        _maxBody = maxBody;

        // No redirect is followed: the upstream is fixed at start, and a 307 to another host would otherwise carry the
        // body and every non-Authorization header there. The product sees the redirect and decides.
        _client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            AllowAutoRedirect = false,
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public string Endpoint { get; private set; } = string.Empty;

    public static async Task<Outcome<IRecordingTap>> StartAsync(TapLaunch launch, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(launch.Upstream.TrimEnd('/'), UriKind.Absolute, out var upstream))
        {
            return Outcome<IRecordingTap>.Failure("the tap's upstream is not an absolute url");
        }

        Directory.CreateDirectory(launch.RecordDirectory);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders(); // a request line carries the key's header; nothing here logs one
        builder.WebHost.UseKestrel(k => k.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        var tap = new RecordingTap(app, upstream, launch.RecordDirectory, launch.Deadline, launch.MaxBodyBytes);
        app.Run(tap.ForwardAsync);
        await app.StartAsync(cancellationToken);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        tap.Endpoint = address.TrimEnd('/') + Prefix;

        return Outcome<IRecordingTap>.Success(tap);
    }

    public async Task<int> CloseAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        while (!_inflight.IsEmpty && clock.Elapsed < wait && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(50, CancellationToken.None);
        }

        await _closing.CancelAsync();

        var drain = Stopwatch.StartNew();
        while (!_inflight.IsEmpty && drain.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(20, CancellationToken.None);
        }

        await _app.StopAsync(CancellationToken.None);
        return _markedAtClose;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closing.IsCancellationRequested)
        {
            await CloseAsync(TimeSpan.Zero, CancellationToken.None);
        }

        await _app.DisposeAsync();
        _client.Dispose();
        _closing.Dispose();
    }

    private async Task ForwardAsync(HttpContext context)
    {
        var number = Interlocked.Increment(ref _calls);
        _inflight[number] = true;

        try
        {
            await ExchangeAsync(context, number);
        }
        finally
        {
            _inflight.TryRemove(number, out _);
        }
    }

    private async Task ExchangeAsync(HttpContext context, int number)
    {
        var request = context.Request;
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, _closing.Token).ConfigureAwait(false);
        var body = buffer.ToArray();
        var scrub = Scrubber(request.Headers);

        if (body.Length > 0)
        {
            await WriteAsync($"call-{number:00}.request.json", scrub(body));
        }

        var clock = Stopwatch.StartNew();
        var (status, data, headers, closed) = await UpstreamAsync(request, body, number);
        var kept = headers.ToDictionary(h => h.Key, h => Encoding.UTF8.GetString(scrub(Encoding.UTF8.GetBytes(h.Value))), StringComparer.OrdinalIgnoreCase);
        var facts = Facts(number, request, body.Length, status, clock.Elapsed.TotalSeconds, kept, data.Length, closed);

        await WriteAsync($"call-{number:00}.json", Encoding.UTF8.GetBytes(facts));
        await WriteAsync($"call-{number:00}.response.json", scrub(data));
        await RespondAsync(context, status, headers, data);
    }

    private async Task<(int Status, byte[] Data, Dictionary<string, string> Headers, string Closed)> UpstreamAsync(HttpRequest request, byte[] body, int number)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        deadline.CancelAfter(_deadline);

        try
        {
            using var message = Message(request, body);
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var data = await BoundedAsync(response, deadline.Token);

            return data.Length > _maxBody
                ? (502, Error($"call {number}: the response exceeded the recorder's cap of {_maxBody} bytes and was cut"), JsonHeaders(), "too_large")
                : ((int)response.StatusCode, data, ResponseHeaders(response), string.Empty);
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
            Interlocked.Increment(ref _markedAtClose);
            return (499, Error("the call was still open when the cell ended; the recorder closed it"), JsonHeaders(), "closed_at_cell_end");
        }
        catch (OperationCanceledException)
        {
            return (504, Error($"upstream connection closed by the recorder's deadline ({_deadline.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s)"), JsonHeaders(), "closed_by_deadline");
        }
        catch (HttpRequestException ex)
        {
            return (502, Error($"call {number}: {ex.GetType().Name}: {ex.HttpRequestError}"), JsonHeaders(), string.Empty);
        }
    }

    /// <summary>The response body, read no further than one byte past the cap — a body that long is refused, never held.</summary>
    private async Task<byte[]> BoundedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while (buffer.Length <= _maxBody && (read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private HttpRequestMessage Message(HttpRequest request, byte[] body)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method), Target(request.Path.Value ?? string.Empty, request.QueryString.Value ?? string.Empty));

        if (body.Length > 0 || !HttpMethods.IsGet(request.Method))
        {
            message.Content = new ByteArrayContent(body);
        }

        foreach (var header in request.Headers.Where(h => !DroppedRequestHeaders.Contains(h.Key)))
        {
            if (!message.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value))
            {
                message.Content?.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
            }
        }

        return message;
    }

    /// <summary>The upstream url for a request path: the fixed base plus the part of the path under <c>/v1</c>. The host
    /// never comes from the request.</summary>
    private Uri Target(string path, string query)
    {
        var rest = path.StartsWith(Prefix, StringComparison.Ordinal) ? path[Prefix.Length..] : path;
        var target = new Uri(_upstream.AbsoluteUri.TrimEnd('/') + "/" + rest.TrimStart('/') + query);

        return string.Equals(target.Authority, _upstream.Authority, StringComparison.OrdinalIgnoreCase) ? target : _upstream;
    }

    private static Dictionary<string, string> ResponseHeaders(HttpResponseMessage response) =>
        response.Headers.Concat(response.Content.Headers)
            .GroupBy(h => h.Key.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => string.Join(", ", g.SelectMany(h => h.Value)), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> JsonHeaders() => new(StringComparer.OrdinalIgnoreCase) { ["content-type"] = "application/json" };

    private static byte[] Error(string text) => JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["tap_error"] = text });

    private static string Facts(int number, HttpRequest request, int requestBytes, int status, double wall, Dictionary<string, string> headers, int responseBytes, string closed) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["call"] = number,
            ["utc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["method"] = request.Method,
            ["path"] = request.Path.Value ?? string.Empty,
            ["request_bytes"] = requestBytes,
            ["request_headers_sent"] = request.Headers.Keys
                .Where(k => !DroppedRequestHeaders.Contains(k) && !string.Equals(k, "Authorization", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            ["status"] = status,
            ["wall_s"] = Math.Round(wall, 3),
            ["response_headers"] = headers.Where(h => KeptResponseHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase)).ToDictionary(h => h.Key, h => h.Value),
            ["response_bytes"] = responseBytes,
            ["closed_by_deadline"] = closed == "closed_by_deadline",
            ["closed_at_cell_end"] = closed == "closed_at_cell_end",
            ["too_large"] = closed == "too_large",
        });

    private static async Task RespondAsync(HttpContext context, int status, Dictionary<string, string> headers, byte[] data)
    {
        try
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = headers.GetValueOrDefault("content-type", "application/json");
            context.Response.ContentLength = data.Length;
            await context.Response.Body.WriteAsync(data, context.RequestAborted);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidOperationException)
        {
            // The shim gave up on its own deadline; the exchange is on disk regardless.
        }
    }

    private async Task WriteAsync(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory, name);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, CancellationToken.None);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>Removes every credential header's value — whole, and a bearer token alone — from bytes about to be written.
    /// Applied to the DISK copy only; what the product receives is the vendor's own bytes.</summary>
    private static Func<byte[], byte[]> Scrubber(IHeaderDictionary headers)
    {
        var values = CredentialHeaders.SelectMany(h => headers[h].Select(v => v ?? string.Empty));
        var secrets = values.SelectMany(v => new[] { v, v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? v[7..] : string.Empty })
            .Select(s => s.Trim())
            .Where(s => s.Length >= 8)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return secrets.Length == 0
            ? bytes => bytes
            : bytes => Encoding.UTF8.GetBytes(secrets.Aggregate(Encoding.UTF8.GetString(bytes), (text, secret) => text.Replace(secret, "[redacted]", StringComparison.Ordinal)));
    }
}

/// <summary>The adapter behind <see cref="IRecordingTapFactory"/>.</summary>
public sealed class RecordingTapFactory : IRecordingTapFactory
{
    public Task<Outcome<IRecordingTap>> StartAsync(TapLaunch launch, CancellationToken cancellationToken) =>
        RecordingTap.StartAsync(launch, cancellationToken);
}
