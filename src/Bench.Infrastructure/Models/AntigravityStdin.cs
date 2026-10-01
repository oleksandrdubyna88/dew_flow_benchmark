using System.Text.Json.Nodes;

namespace Bench.Infrastructure.Models;

/// <summary>How a prompt reaches agy (S2b, finding 3) — the launch coai measured and ships (<c>src_mcp/runners/Consultation/AntigravityConsultant.cs</c>,
/// <c>AntigravityStream.UserMessage</c>): <c>--print=</c> empty ON PURPOSE (the flag is mandatory in stream mode and a value is the
/// prompt — a bare <c>--print</c> took <c>--model</c> as its prompt on every live cell of 2026-10-01 and exited 2), <c>--input-format
/// stream-json</c> reading one NDJSON message per line from stdin, which requires <c>--output-format stream-json</c>.
/// The message is SERIALISED, never interpolated: a prompt contains quotes, backslashes (a Windows path) and newlines.</summary>
public static class AntigravityStdin
{
    /// <summary>The flags every agy launch shares — read-only is the caller's <c>--mode plan</c> beside them.</summary>
    public static IReadOnlyList<string> StreamFlags { get; } = ["--print=", "--input-format", "stream-json", "--output-format", "stream-json"];

    /// <summary>One NDJSON line: <c>{"event":"user","message":{"role":"user","content":"…"}}</c>.</summary>
    public static string UserMessage(string prompt) =>
        new JsonObject
        {
            ["event"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = prompt },
        }.ToJsonString() + "\n";
}
