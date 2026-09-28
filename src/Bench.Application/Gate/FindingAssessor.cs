using System.Text;
using System.Text.Json.Nodes;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Registry;

namespace Bench.Application.Gate;

/// <summary>Who assesses, and with what: a reviewer-catalog row (the assessor is a model plus its transport too), the
/// executable resolved on THIS machine, a wall per call, and the rubric it is asked under.</summary>
public sealed record AssessorLaunch(GateReviewer Assessor, ModelRuntimeKind Kind, string Executable, TimeSpan Wall, LoadedRubric Rubric);

/// <summary>One batch put to the assessor: its id, its task, the blinded rows, the task's seed ids (what a
/// <c>seed_hit</c> may name) and the cluster keys earlier batches of the task already minted.</summary>
public sealed record BatchAsk(string BatchId, GateTaskId Task, IReadOnlyList<AssessmentRow> Rows, IReadOnlyCollection<SeedId> Seeds, IReadOnlyList<string> PriorClusters);

/// <param name="PromptHash">SHA-256 of the prompt as SENT — the rubric plus this batch's framing.</param>
public sealed record BatchAnswer(BatchReading Reading, string PromptHash);

/// <summary>The blinded assessor: one batch → one CLI launch → one reading. A port of the calibration's <c>run_task</c>
/// body over <see cref="ICliAgentRuntime"/> — the prompt is the rubric verbatim, then PRIOR CLUSTER KEYS, then the INPUT
/// ROWS as JSONL, exactly the other harness's framing, so a verdict here and a verdict there answer the same text.
/// <para>
/// Codex is launched <c>-s read-only</c> with the calibration's output schema enforced and its final message written
/// to a file, every MCP server off. The Claude CLI has no schema flag: it runs in print and plan mode with the write
/// tools taken away, one turn, no MCP server, and its answer is taken out of whatever prose surrounds it by
/// <see cref="AgentJson"/> — extraction, not repair. Either way the reading is <see cref="AssessorOutput.Read"/>'s.
/// </para></summary>
public sealed class FindingAssessor(ICliAgentRuntime runtime, IGateAssessmentFiles files)
{
    public const string SchemaFile = "verdict-schema.json";
    public const string PromptFile = "prompt.txt";
    public const string AnswerFile = "answer.json";

    /// <summary>The calibration's output schema (<c>assess.py: SCHEMA</c>), byte for byte in meaning.</summary>
    public static readonly string Schema = new JsonObject
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray("rows"),
        ["properties"] = new JsonObject
        {
            ["rows"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray([.. AssessorOutput.Keys.Select(k => (JsonNode)k)]),
                    ["properties"] = new JsonObject
                    {
                        ["id"] = Str(),
                        ["task"] = Str(),
                        ["verdict"] = Enum("supported", "partial", "refuted", "unresolved"),
                        ["value"] = Enum("high", "medium", "low", "none"),
                        ["severity_fair"] = Enum("yes", "overstated", "understated"),
                        ["grounded"] = Enum("yes", "near", "no"),
                        ["cluster"] = Str(),
                        ["seed_hit"] = Str(),
                        ["note"] = Str(),
                    },
                },
            },
        },
    }.ToJsonString();

    /// <summary>Which CLI a catalog row is. Only codex and claude assess: they are the two with a read-only launch.</summary>
    public static Outcome<ModelRuntimeKind> KindOf(GateReviewer assessor) => assessor.Definition.Runtime switch
    {
        ReviewerRuntime.Codex => Outcome<ModelRuntimeKind>.Success(ModelRuntimeKind.CliCodex),
        ReviewerRuntime.Claude => Outcome<ModelRuntimeKind>.Success(ModelRuntimeKind.CliClaude),
        var other => Outcome<ModelRuntimeKind>.Failure(
            $"assessor '{assessor.Id}' runs on {other.Word()} — an assessor is a codex or claude row, the two CLIs with a read-only launch"),
    };

    /// <summary>The launch options per CLI: the read-only sandbox, the schema and the answer file for codex; plan mode, the
    /// write tools denied and one turn for claude; MCP servers off for both.</summary>
    public static AgentAskOptions OptionsFor(ModelRuntimeKind kind, string batchDirectory) => kind == ModelRuntimeKind.CliCodex
        ? new AgentAskOptions
        {
            Sandbox = AgentSandbox.ReadOnly,
            OutputSchemaFile = Path.Combine(batchDirectory, SchemaFile),
            LastMessageFile = Path.Combine(batchDirectory, AnswerFile),
            McpServersOff = true,
        }
        : new AgentAskOptions
        {
            Sandbox = AgentSandbox.ReadOnly,
            DisallowedTools = ["Edit", "Write", "NotebookEdit"],
            MaxTurns = 1,
            McpServersOff = true,
        };

    public static string Prompt(string rubricText, IReadOnlyList<string> prior, GateTaskId task, IReadOnlyList<AssessmentRow> rows)
    {
        var text = new StringBuilder(rubricText.TrimEnd());
        text.Append("\n\nPRIOR CLUSTER KEYS\n");
        text.Append(prior.Count > 0 ? string.Join('\n', prior.Order(StringComparer.Ordinal).Select(k => $"- {k}")) : "(none)");
        text.Append($"\n\nINPUT ROWS ({rows.Count} findings, task {task})\n");
        text.Append(string.Join('\n', rows.Select(r => r.ToJson())));
        return text.ToString();
    }

    public async Task<BatchAnswer> AskAsync(AssessorLaunch launch, BatchAsk ask, CancellationToken cancellationToken)
    {
        var directory = await files.BeginBatchAsync(ask.BatchId, cancellationToken);
        var prompt = Prompt(launch.Rubric.Text, ask.PriorClusters, ask.Task, ask.Rows);

        await files.WriteBatchFileAsync(directory, PromptFile, prompt, cancellationToken);
        await files.WriteBatchFileAsync(directory, SchemaFile, Schema, cancellationToken);

        var answer = await runtime.AskAsync(
            new AgentAsk(launch.Kind, launch.Executable, prompt, directory, launch.Wall, launch.Assessor.Definition.Model)
            {
                Options = OptionsFor(launch.Kind, directory),
            },
            cancellationToken);

        var reading = answer switch
        {
            Outcome<AgentAnswer>.Ok ok when StoppedAtTurnCeiling(ok.Value.Text) => new BatchReading.Failed(
                AssessmentFailureCause.NoAnswer, $"the {launch.Kind} assessor stopped at its turn ceiling before answering — {ok.Value.Text.Trim()}"),
            Outcome<AgentAnswer>.Ok ok => AssessorOutput.Read(Payload(ok.Value.Text), ask.Rows, ask.Seeds),
            Outcome<AgentAnswer>.Fail fail => new BatchReading.Failed(AssessmentFailureCause.NoAnswer, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

        if (answer is Outcome<AgentAnswer>.Ok written && launch.Kind != ModelRuntimeKind.CliCodex)
        {
            await files.WriteBatchFileAsync(directory, AnswerFile, written.Value.Text, cancellationToken);
        }

        await files.ArchiveBatchAsync(directory, ask.BatchId, cancellationToken);
        return new BatchAnswer(reading, StableHash.Of(prompt));
    }

    /// <summary>Claude Code's own line when <c>--max-turns</c> ran out: measured 2026-09-28 (2.1.258) — with one turn, an
    /// assessor that reaches for a read tool prints <c>Error: Reached max turns (1)</c> and exits 0. That is no answer, not
    /// an answer that failed to parse.</summary>
    private static bool StoppedAtTurnCeiling(string text) =>
        text.TrimStart().StartsWith("Error: Reached max turns", StringComparison.Ordinal);

    /// <summary>The answer's JSON object, out of any fence or prose around it; the text unchanged when there is none — a
    /// cut document then reads as truncated rather than as prose.</summary>
    private static string Payload(string text) =>
        AgentJson.Extract(text, '{', '}', candidate => candidate.Contains("\"rows\"", StringComparison.Ordinal) && Parses(candidate)).Json;

    private static bool Parses(string candidate)
    {
        try
        {
            return JsonNode.Parse(candidate) is JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static JsonObject Str() => new() { ["type"] = "string" };

    private static JsonObject Enum(params string[] words) => new() { ["type"] = "string", ["enum"] = new JsonArray([.. words.Select(w => (JsonNode)w)]) };
}
