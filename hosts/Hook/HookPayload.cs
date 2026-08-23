using System.Text.Json;

namespace Bench.Hook;

/// <summary>What Claude Code puts on this process's stdin.
/// <para>
/// Read defensively, field by field, and never deserialized into a typed record. The payload's exact shape
/// is documented for the fields we rely on (<c>session_id</c>, <c>cwd</c>, <c>hook_event_name</c>,
/// <c>tool_name</c>, <c>tool_input</c>) and NOT documented for the one we would most like —
/// <c>tool_response</c>. A strict deserializer would refuse the whole payload the first time a field it
/// did not expect appeared, which is the day an agent upgrade silently ends the measurement.
/// </para>
/// <para>
/// So every reader here answers an empty string rather than throwing, and an absent field becomes "not
/// captured" downstream instead of a lost event.
/// </para></summary>
internal sealed record HookPayload(
    string SessionId,
    string Cwd,
    string EventName,
    string ToolName,
    string ToolInputJson,
    string ToolResponseJson,
    string Command,
    string Outcome)
{
    public static HookPayload Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            // A payload we cannot read still tells us a hook fired. Recording it with empty fields would
            // put a call with no name in the ledger, so this becomes an event with no session key — which
            // the collector refuses BY NAME, and the refusal is the signal that this parser needs work.
            return Empty;
        }
    }

    private static HookPayload Empty { get; } =
        new(string.Empty, string.Empty, string.Empty, string.Empty, "{}", string.Empty, string.Empty, "notcaptured");

    /// <summary>Where a tool's answer might be, in the order the fields are trusted.
    /// <para>
    /// <c>tool_response</c> is the documented one and comes first. The rest are for the FAILURE event,
    /// whose payload shape is not documented at all: measured on a real session, a failed read closed with
    /// no <c>tool_response</c> and therefore with no reason — and "why did that call fail" is exactly what
    /// an analysis of wasted work needs. Reading several plausible names costs nothing and turns a blank
    /// into a sentence; where none of them is present, "not captured" is still the honest answer.
    /// </para></summary>
    private static readonly string[] AnswerFields = ["tool_response", "tool_error", "error", "result", "message"];

    private static HookPayload Read(JsonElement root)
    {
        var input = Raw(root, "tool_input");
        var response = Answer(root);

        return new HookPayload(
            Text(root, "session_id"),
            Text(root, "cwd"),
            Text(root, "hook_event_name"),
            Text(root, "tool_name"),
            input.Length > 0 ? input : "{}",
            response,
            ReadCommand(input),
            ReadOutcome(root, response));
    }

    /// <summary>The shell command, when this call is a shell call. Lifted out of the arguments here as
    /// well as in the domain because the decision it feeds — is the porcelain read worth its milliseconds —
    /// happens before anything is sent anywhere.</summary>
    private static string ReadCommand(string toolInputJson) =>
        Bench.Domain.Sessions.ToolTarget.Argument(toolInputJson, "command");

    /// <summary>How the call ended, from whatever the response happens to look like.
    /// <para>
    /// Three readings, in order of how much they are trusted: an explicit error flag, then the mere
    /// presence of a response, then nothing. The last one is <c>notcaptured</c> rather than <c>answered</c>
    /// — a pre-tool event has no result yet, and calling that a success would put a 100 % answer rate on
    /// every trace this system takes.
    /// </para></summary>
    private static string ReadOutcome(JsonElement root, string response)
    {
        if (Flag(root, "tool_response", "is_error") || Flag(root, "tool_response", "isError"))
        {
            return "error";
        }

        return response.Length > 0 ? "answered" : "notcaptured";
    }

    /// <summary>The first answer field this payload actually carries, verbatim.</summary>
    private static string Answer(JsonElement root)
    {
        foreach (var field in AnswerFields)
        {
            var value = Raw(root, field);
            if (value.Length > 0 && value != "null")
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>A nested value verbatim, whatever shape it is. <c>tool_response</c> is an object for some
    /// tools, a bare string for others and an array for a few; keeping the raw text means this client never
    /// has to have an opinion about which.</summary>
    private static string Raw(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value)
            ? value.GetRawText()
            : string.Empty;

    private static bool Flag(JsonElement root, string parent, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(parent, out var nested)
        && nested.ValueKind == JsonValueKind.Object
        && nested.TryGetProperty(name, out var flag)
        && flag.ValueKind == JsonValueKind.True;
}
