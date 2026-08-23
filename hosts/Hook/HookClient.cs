using System.Text;
using System.Text.Json;
using Bench.Contracts;

namespace Bench.Hook;

/// <summary>Turns one Claude Code hook invocation into one session event.
/// <para>
/// The whole capture path for the reference runtime, and it is deliberately this small. The alternative
/// considered and rejected was an <c>ANTHROPIC_BASE_URL</c> proxy: it would have had to reassemble tool
/// arguments out of streamed JSON fragments, would only ever see a call's RESULT one request later, and
/// would sit in the credential path of every request the operator makes. A hook is told the tool name and
/// its arguments directly, is told the result when it finishes, and never sees a token.
/// </para></summary>
public static class HookClient
{
    /// <summary>How long the post is given before the event goes to the spool instead. Short on purpose:
    /// the operator's tool call is waiting, and a collector that is slow is indistinguishable from one that
    /// is down as far as this process should care.</summary>
    private const int PostTimeoutMs = 1500;

    /// <summary>How much of a tool's response travels. Budgeted at EMIT, per the family's telemetry rule —
    /// the bytes should not cross the wire at all rather than being trimmed once they arrive.</summary>
    private const int ResponseBudget = 8000;

    public static async Task<int> RunAsync(string[] args, TextReader input, TextWriter error)
    {
        // Taken FIRST, before stdin is drained and before git is asked anything — see Stamp below for why
        // the moment this is read decides whether the duration column measures a tool or measures us.
        var entered = DateTimeOffset.UtcNow;

        try
        {
            var payload = await input.ReadToEndAsync();
            var wire = Compose(args, payload, entered);

            await DeliverAsync(wire, error);
        }
        catch (Exception ex)
        {
            // Deliberately broad, and deliberately silent on stdout. This process cannot be allowed to
            // fail the tool call it is observing, and stderr is the one channel that reaches an operator
            // without doing so.
            error.WriteLine($"bench-hook: {ex.GetType().Name} — {ex.Message.Split('\n')[0]}");
        }

        return 0;
    }

    /// <summary>Builds the event from the hook payload and the environment the terminal was opened with.</summary>
    /// <param name="entered">When this process started, before it did anything. Used for the events where
    /// the earliest instant is the honest one — see <see cref="Stamp"/>.</param>
    public static SessionEventDto Compose(string[] args, string payload, DateTimeOffset entered)
    {
        var hook = HookPayload.Parse(payload);
        var kind = args.Length > 0 && args[0].Length > 0 ? args[0] : hook.EventName;
        var worktree = Worktree.Read(hook.Cwd, hook.ToolName, hook.Command);

        return new SessionEventDto(
            Schema: SessionEventDto.Version,
            At: Stamp(kind, entered),
            Source: "hook",
            Runtime: "claude-code",
            Event: kind,
            SessionKey: hook.SessionId,
            Task: new SessionTaskDto(
                Environment.GetEnvironmentVariable(SessionCollector.TaskIdVariable) ?? string.Empty,
                Environment.GetEnvironmentVariable(SessionCollector.TaskNameVariable) ?? string.Empty,
                Environment.GetEnvironmentVariable(SessionCollector.PlanVariable) ?? string.Empty),
            WorkspacePath: hook.Cwd,
            Branch: Worktree.Branch(hook.Cwd),
            // A hook payload does not say which model is driving. Absent is the honest reading, and filling
            // it from a guess would put a model name on a trace nobody verified.
            Model: CapturedTextDto.Unavailable("a hook payload does not name the model"),
            Tool: hook.ToolName,
            ArgumentsJson: hook.ToolInputJson,
            ArgumentsTruncatedBytes: 0,
            Response: Response(hook),
            ResponseChars: hook.ToolResponseJson.Length,
            // The failure event IS the outcome. Reading it off the event name rather than hunting for a
            // flag inside a payload whose shape nobody documents is the reliable half of the two.
            Outcome: kind == "PostToolUseFailure" ? "error" : hook.Outcome,
            DurationMs: CapturedCountDto.Unavailable("the collector measures this between the two hooks"),
            Worktree: worktree);
    }

    /// <summary>Each hook stamps the instant CLOSEST to the tool boundary it observes.
    /// <para>
    /// A pre-event stamps as LATE as it can — now, with its work behind it — because the tool starts a
    /// moment after this process exits. A post-event stamps as EARLY as it can — the instant it was
    /// entered — because the tool had already returned by then. The duration the collector computes is
    /// the gap between those two stamps, so this rule is what decides whether that column measures the
    /// tool or measures this instrument.
    /// </para>
    /// <para>
    /// It was measured before the rule existed: reading one file recorded <b>~220 ms</b>, of which the
    /// file read was a handful. Both stamps sat on the far side of two process launches and a git call,
    /// so the column was almost entirely us. Asymmetric stamping is not a trick — each half genuinely
    /// knows which of its own instants is nearest the thing being measured.
    /// </para></summary>
    private static DateTimeOffset Stamp(string kind, DateTimeOffset entered) =>
        kind is "PostToolUse" or "PostToolUseFailure" or "SessionEnd" ? entered : DateTimeOffset.UtcNow;

    private static CapturedTextDto Response(HookPayload hook) =>
        hook.ToolResponseJson.Length == 0
            ? CapturedTextDto.Unavailable("this hook event carried no tool response")
            : CapturedTextDto.Text(Clip(hook.ToolResponseJson));

    /// <summary>Post it, and spool it when that fails.
    /// <para>
    /// One spool, on THIS side of the wire, and none on the collector's. A hook that could not deliver has
    /// the event in its hand and a disk under it; a collector that could not store one has to have received
    /// it first. Putting the fallback here covers both failures — collector down and database down — with
    /// one mechanism and one drain (<c>bench sessions ingest</c>).
    /// </para></summary>
    private static async Task DeliverAsync(SessionEventDto wire, TextWriter error)
    {
        var collector = Environment.GetEnvironmentVariable(SessionCollector.UrlVariable)
            ?? SessionCollector.DefaultUrl;

        var refusal = await Post.SendAsync(collector, wire, PostTimeoutMs);

        if (refusal.Length == 0)
        {
            return;
        }

        Spool.Append(SpoolRoot(), wire);
        error.WriteLine($"bench-hook: spooled — {refusal}");
    }

    private static string SpoolRoot() =>
        Environment.GetEnvironmentVariable(SessionCollector.SpoolVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "bench",
                "sessions-spool");

    private static string Clip(string value) =>
        value.Length <= ResponseBudget ? value : value[..ResponseBudget];
}

/// <summary>Posting the event, with no <see cref="HttpClient"/> kept alive — this process exists for
/// milliseconds, so pooling would cost more than it saves.</summary>
internal static class Post
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>An empty string means delivered. Anything else is the reason it was not, in words the
    /// operator can act on.</summary>
    public static async Task<string> SendAsync(string collector, SessionEventDto wire, int timeoutMs)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            using var body = new StringContent(
                JsonSerializer.Serialize<IReadOnlyList<SessionEventDto>>([wire], Json),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(collector.TrimEnd('/') + SessionCollector.EventsRoute, body);

            return response.IsSuccessStatusCode ? string.Empty : $"the collector answered {(int)response.StatusCode}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return $"{collector} could not be reached: {ex.Message.Split('\n')[0]}";
        }
    }
}

/// <summary>The fallback file. A day folder and one file per session, matching the family's log layout so a
/// human hunting a session's spool looks in the shape they already know.</summary>
internal static class Spool
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Append(string root, SessionEventDto wire)
    {
        try
        {
            var folder = Path.Combine(root, DateTime.UtcNow.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(folder);

            var file = Path.Combine(folder, $"session-{Safe(wire.SessionKey)}.jsonl");

            // Append, never rewrite: several hook processes of one session can be writing at once, and a
            // read-modify-write would lose whichever one lost the race.
            File.AppendAllText(file, JsonSerializer.Serialize(wire, Json) + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The disk said no. There is nowhere left to put this event, and failing the operator's tool
            // call over it would be the worse trade by a wide margin.
        }
    }

    private static string Safe(string key) =>
        new([.. key.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')]);
}
