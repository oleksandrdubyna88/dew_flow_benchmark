using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

// SPIKE — does Microsoft.Extensions.AI.Evaluation carry this benchmark's shape?
// Five criteria, stated before the spike started. No LLM is called: everything here is exercised with a
// deterministic evaluator, so the answer costs nothing and cannot vary between runs.

var root = Path.Combine(Path.GetTempPath(), $"eval-spike-{Guid.NewGuid():N}");
Console.WriteLine($"storage: {root}\n");

var found = new List<string>();
void Note(string line)
{
    found.Add(line);
    Console.WriteLine(line);
}

// ---------------------------------------------------------------- criterion 1: anchor matching
// Our retrieval expectation is "did the engine surface src/Orders.cs#OrderService.Total". That is not a
// chat message, so it has to travel as EvaluationContext and be judged by our own code.
var config = DiskBasedReportingConfiguration.Create(
    storageRootPath: root,
    evaluators: [new AnchorRecallEvaluator()],
    chatConfiguration: null,
    executionName: "spike-exec-1");

var target = "https://github.com/dotnet/aspnetcore.git@3f1acb59718cadf111a0a796681e3d3509bb3381";

// ---------------------------------------------------------------- criterion 4: our dimensions
// Six-dimensional key (target, engine, suite, subject, lane, repeat) against a three-level store
// (execution / scenario / iteration) plus a flat tag list.
await EvaluateAsync(config, "order-total", engine: "qln", lane: "retrieval", subject: "opus@cloud", repeat: 0,
    hits: ["src/Orders.cs#OrderService.Total", "src/Orders.cs#OrderService.Apply"], expected: "src/Orders.cs#OrderService.Total", target);
await EvaluateAsync(config, "order-total", engine: "noretrieval", lane: "native", subject: "opus@cloud", repeat: 0,
    hits: ["src/Cache.cs#ReadCache.Invalidate"], expected: "src/Orders.cs#OrderService.Total", target);
await EvaluateAsync(config, "cache-invalidation", engine: "qln", lane: "retrieval", subject: "qwen@local", repeat: 1,
    hits: ["src/Cache.cs#ReadCache.Invalidate"], expected: "src/Cache.cs#ReadCache.Invalidate", target);

// ---------------------------------------------------------------- read back: what actually survived
var store = new DiskBasedResultStore(root);
var scenarios = new List<string>();
await foreach (var s in store.GetScenarioNamesAsync("spike-exec-1"))
{
    scenarios.Add(s);
}

Note($"[4] scenario names stored: {string.Join(" | ", scenarios)}");

var results = new List<ScenarioRunResult>();
foreach (var scenario in scenarios)
{
    await foreach (var iteration in store.GetIterationNamesAsync("spike-exec-1", scenario))
    {
        await foreach (var r in store.ReadResultsAsync("spike-exec-1", scenario, iteration))
        {
            results.Add(r);
        }
    }
}

Note($"[4] results read back: {results.Count}");

var first = results[0];
Note($"[1] metric name={first.EvaluationResult.Metrics.Keys.First()} " +
     $"value={JsonSerializer.Serialize(((NumericMetric)first.EvaluationResult.Metrics.Values.First()).Value)} " +
     $"failed={first.EvaluationResult.Metrics.Values.First().Interpretation?.Failed}");
Note($"[4] tags survived: {string.Join(", ", first.Tags ?? [])}");
Note($"[4] metric metadata survived: {JsonSerializer.Serialize(first.EvaluationResult.Metrics.Values.First().Metadata)}");
Note($"[2] the SUBJECT's answer is stored with the result: \"{first.ModelResponse.Text}\"");

// ---------------------------------------------------------------- criterion 2: re-score, no re-run
// A second judge reads the STORED response. Nothing is re-inferred.
var rescored = await new KeywordJudgeEvaluator().EvaluateAsync(
    first.Messages, first.ModelResponse, chatConfiguration: null!, additionalContext: [], CancellationToken.None);
Note($"[2] re-scored stored answer with a different evaluator: {rescored.Metrics.Keys.First()}=" +
     $"{((BooleanMetric)rescored.Metrics.Values.First()).Value}");

// ---------------------------------------------------------------- criterion 3: resume granularity
Note($"[3] resume unit = scenario+iteration already present in the store: " +
     $"{string.Join(", ", results.Select(r => $"{r.ScenarioName}/{r.IterationName}"))}");

// ---------------------------------------------------------------- report
var reportPath = Path.Combine(root, "report.html");
await new HtmlReportWriter(reportPath).WriteReportAsync(results, CancellationToken.None);
Note($"[bonus] HTML report written: {new FileInfo(reportPath).Length / 1024} KB, no code of ours");

Console.WriteLine("\n--- summary ---");
foreach (var line in found)
{
    Console.WriteLine(line);
}

static async Task EvaluateAsync(
    ReportingConfiguration config, string question, string engine, string lane, string subject, int repeat,
    string[] hits, string expected, string target)
{
    // The composite scenario name is the experiment: six dimensions into a three-level tree.
    // FINDING: the scenario name is a PATH SEGMENT on disk — "|" is rejected outright by the
    // store's validator. A composite key has to be path-safe, which constrains how six dimensions
    // may be flattened into one string.
    var scenarioName = $"{question}.{engine}.{lane}.{subject.Replace('@', '-')}";

    await using var run = await config.CreateScenarioRunAsync(
        scenarioName,
        iterationName: repeat.ToString(),
        additionalTags: [$"engine:{engine}", $"lane:{lane}", $"subject:{subject}", $"target:{target}"]);

    var messages = new List<ChatMessage> { new(ChatRole.User, $"question {question}") };
    var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, $"the answer for {question} mentions tenant"));

    await run.EvaluateAsync(
        messages,
        response,
        additionalContext: [new RetrievalContext(hits, expected)]);
}

/// <summary>Our ground truth as an EvaluationContext: what the engine surfaced, and what it had to.</summary>
internal sealed class RetrievalContext(IEnumerable<string> hits, string expectedAnchor)
    : EvaluationContext("Retrieval", $"expected {expectedAnchor}; hits: {string.Join(", ", hits)}")
{
    public IReadOnlyList<string> Hits { get; } = [.. hits];

    public string ExpectedAnchor { get; } = expectedAnchor;
}

/// <summary>Criterion 1: is "did the engine surface this anchor" expressible as a first-class evaluator?</summary>
internal sealed class AnchorRecallEvaluator : IEvaluator
{
    public IReadOnlyCollection<string> EvaluationMetricNames => ["Anchor recall"];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration,
        IEnumerable<EvaluationContext>? additionalContext,
        CancellationToken cancellationToken = default)
    {
        var context = additionalContext?.OfType<RetrievalContext>().FirstOrDefault();

        var recall = context is null ? 0.0 : context.Hits.Contains(context.ExpectedAnchor) ? 1.0 : 0.0;

        var metric = new NumericMetric("Anchor recall", recall)
        {
            Reason = context is null
                ? "no retrieval context was supplied"
                : $"{context.ExpectedAnchor} was {(recall > 0 ? "surfaced" : "ABSENT")} among {context.Hits.Count} hit(s)",
            Interpretation = new EvaluationMetricInterpretation(
                recall > 0 ? EvaluationRating.Exceptional : EvaluationRating.Unacceptable,
                failed: recall == 0,
                reason: "a target absent from the candidate set cannot be promoted by any reranker"),
        };

        metric.AddOrUpdateMetadata("anchor", context?.ExpectedAnchor ?? "");
        metric.AddOrUpdateMetadata("hitCount", (context?.Hits.Count ?? 0).ToString());

        return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
    }
}

/// <summary>Criterion 2: a DIFFERENT judge, run over an answer that was stored earlier.</summary>
internal sealed class KeywordJudgeEvaluator : IEvaluator
{
    public IReadOnlyCollection<string> EvaluationMetricNames => ["Mentions tenant"];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration,
        IEnumerable<EvaluationContext>? additionalContext,
        CancellationToken cancellationToken = default)
    {
        var mentions = modelResponse.Text.Contains("tenant", StringComparison.OrdinalIgnoreCase);

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            new BooleanMetric("Mentions tenant", mentions) { Reason = "re-scored from stored text" }));
    }
}
