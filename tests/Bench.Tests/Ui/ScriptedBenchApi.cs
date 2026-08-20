using System.Net;
using System.Text;
using System.Text.Json;

namespace Bench.Tests.Ui;

/// <summary>An API the console can actually talk to: routes scripted per test, every call recorded.
/// <para>
/// <b>The seam is the HTTP handler, not an interface.</b> <see cref="Bench.Ui.Services.BenchConsoleApi"/> is
/// a sealed class over <c>HttpClient</c>, and extracting an interface to mock it would be a production change
/// made for the tests. Faking the handler leaves the real client, the real URLs and the real JSON in the
/// test — so a page test also pins the REQUEST the page causes, which is the half a mock throws away. The
/// idiom this repository already uses for its engine clients, and the sibling console's too.
/// </para>
/// <para>
/// <b>Web JSON, deliberately.</b> Bodies are serialised camelCase, exactly as the minimal APIs answer. A fake
/// writing PascalCase would still bind — the client reads case-insensitively — and would therefore hide a
/// real casing mismatch forever.
/// </para>
/// <para>
/// <b>An unscripted route answers 404.</b> That is how a page meets an API that is not there, and a test says
/// it cares about that path by leaving the route unscripted rather than by scripting a failure.
/// </para></summary>
public sealed class ScriptedBenchApi : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, (HttpStatusCode Status, object? Body)> _routes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every path-with-query the console asked for, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Not a real host: an attempt to leave the process fails loudly rather than reaching something.</summary>
    public static Uri BaseAddress { get; } = new("http://bench.test/");

    /// <summary>Scripts one route. The key is the path WITHOUT its query — a query belongs to the request
    /// (asserted through <see cref="Calls"/>), never to the routing.</summary>
    public ScriptedBenchApi Answers(string path, object? body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[path] = (status, body);
        return this;
    }

    public HttpClient Client() => new(this) { BaseAddress = BaseAddress };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Calls.Add(uri.PathAndQuery);

        return Task.FromResult(_routes.TryGetValue(uri.AbsolutePath, out var scripted)
            ? Json(scripted.Status, scripted.Body)
            : Json(HttpStatusCode.NotFound, null));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object? body) =>
        new(status)
        {
            Content = new StringContent(
                body is null ? string.Empty : JsonSerializer.Serialize(body, Web),
                Encoding.UTF8,
                "application/json"),
        };
}
