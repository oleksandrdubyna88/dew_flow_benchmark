using System.Text.Json.Nodes;
using Bench.Domain.Probes;
using Bench.Domain.Scouts;

namespace Bench.Tests.Scouts;

/// <summary>How a synthetic cell stream deviates from a clean one.</summary>
public enum CellVariant
{
    /// <summary>The arm's N calls, the frozen prompts, the pinned ids, a consistent envelope.</summary>
    Clean,

    /// <summary><c>subagent_stats.completed</c> one short — a VOID cell (<see cref="ScoutReason.SubagentsNotCompleted"/>).</summary>
    Void,

    /// <summary>The CLI's own budget cap reached (<c>error_max_budget_usd</c>) — <i>aborted:spend</i>.</summary>
    BudgetExhausted,

    /// <summary>A child answered with another resolved id than the run's pin — a moved alias, a STOP.</summary>
    AliasMoved,

    /// <summary>The envelope's <c>total_cost_usd</c> 10 % off the computed cost — the cost check STOPS.</summary>
    CostOff,

    /// <summary>A child read a file outside the tree and got content — the path audit STOPS.</summary>
    OutsideRead,

    /// <summary>The main session launched the scouts on the OTHER alias (opus for a Sonnet arm, sonnet for an Opus arm), and they answered on
    /// it — the pins hold, the envelope agrees, and the read-back VOIDS the cell (<see cref="ScoutReason.InputModelMismatch"/>).</summary>
    WrongModel,
}

/// <summary>Whole synthetic cell streams for a (task, arm) — S1's <see cref="ScoutStreams"/> builder in claude 2.1.289's shape, with the
/// cell's REAL frozen prompts, the tree's paths spelled <c>__CWD__</c> (the fake CLI writes its working directory there), KEY FILES naming
/// the task's G, and an envelope whose <c>modelUsage</c> and <c>total_cost_usd</c> agree with <see cref="Prices"/> to the micro-dollar.</summary>
internal static class ScoutCellStreams
{
    public const string Cwd = "__CWD__";

    public static PriceTable Prices { get; } = PriceTable.Of("https://example.invalid/pricing", new DateOnly(2026, 10, 12),
    [
        ModelPrice.Of(ScoutStreams.Opus, 5m, 25m, 0.5m, 6.25m, 10m).Ok(),
        ModelPrice.Of(ScoutStreams.Sonnet, 3m, 15m, 0.3m, 3.75m, 6m).Ok(),
    ]).Ok();

    public const string PricesJson = """
        { "source": "https://example.invalid/pricing", "readOn": "2026-10-12", "models": [
          { "modelId": "claude-opus-5-5", "input": 5, "output": 25, "cacheRead": 0.5, "cacheWrite5m": 6.25, "cacheWrite1h": 10 },
          { "modelId": "claude-sonnet-5-5", "input": 3, "output": 15, "cacheRead": 0.3, "cacheWrite5m": 3.75, "cacheWrite1h": 6 } ] }
        """;

    public static string For(ScoutTask task, ScoutArm arm, CellVariant variant = CellVariant.Clean, string tree = Cwd)
    {
        var prompts = ScoutPrompts.ForCell(task, arm);
        var model = ModelOf(arm, variant);
        var resolved = model == ScoutModel.Opus ? ScoutStreams.Opus : ScoutStreams.Sonnet;
        var children = prompts.Scouts.Select((prompt, i) => new ChildSpec(
            prompt,
            InputModel: ScoutModels.Alias(model),
            ResolvedModel: resolved,
            MessageModel: resolved,
            AgentType: arm.AgentType,
            Tools: [Tool(variant, i)],
            Report: Report(task, i))).ToList();

        var stream = ScoutStreams.Cell(children, completed: variant == CellVariant.Void ? children.Count - 1 : null).Replace(ScoutStreams.Tree, Cwd, StringComparison.Ordinal);

        return Moved(Priced(stream, arm, variant), resolved, variant).Replace(Cwd, tree, StringComparison.Ordinal);
    }

    /// <summary>A moved alias: the FIRST Agent result's <c>resolvedModel</c> becomes another id, after the envelope was priced — the drift
    /// stop is what this variant pins, and it outranks the cost check that would otherwise trip too.</summary>
    private static string Moved(string stream, string resolved, CellVariant variant)
    {
        var marker = $"\"resolvedModel\":\"{resolved}\"";
        var at = stream.IndexOf(marker, StringComparison.Ordinal);

        return variant == CellVariant.AliasMoved && at >= 0
            ? string.Concat(stream.AsSpan(0, at), "\"resolvedModel\":\"claude-moved-9-9\"", stream.AsSpan(at + marker.Length))
            : stream;
    }

    private static ToolSpec Tool(CellVariant variant, int i) =>
        variant == CellVariant.OutsideRead && i == 0
            ? new ToolSpec("Read", """{"file_path":"C:/elsewhere/future/Store.cs"}""", Result: "class Store { /* the answer */ }")
            : new ToolSpec("Read", $$"""{"file_path":"{{ScoutStreams.Tree}}/src/Alpha.cs"}""", Result: "class Alpha {}");

    private static string Report(ScoutTask task, int i) =>
        $"Found it.\n\nKEY FILES\n- {task.GroundTruth[i % task.GroundTruth.Count]}\n- README.md\n\nCLAIMS\n- {task.GroundTruth[i % task.GroundTruth.Count]}:1 — defines the type\n- README.md — describes the project";

    /// <summary>The envelope made consistent: <c>modelUsage</c> per model (the main session's output on Opus, 50 per child on its model), then
    /// <c>total_cost_usd</c> = the cost the calculator computes from it — or 10 % off it for <see cref="CellVariant.CostOff"/>.</summary>
    /// <summary>The model the scouts actually ran on — the arm's, or the other one for <see cref="CellVariant.WrongModel"/>.</summary>
    private static ScoutModel ModelOf(ScoutArm arm, CellVariant variant) =>
        variant != CellVariant.WrongModel ? arm.Model : arm.Model == ScoutModel.Opus ? ScoutModel.Sonnet : ScoutModel.Opus;

    private static string Priced(string stream, ScoutArm arm, CellVariant variant)
    {
        var scoutModel = ModelOf(arm, variant) == ScoutModel.Opus ? ScoutStreams.Opus : ScoutStreams.Sonnet;
        var usage = new JsonObject
        {
            [ScoutStreams.Opus] = Line(100 + (scoutModel == ScoutStreams.Opus ? 50 * arm.Count : 0)),
        };

        if (scoutModel == ScoutStreams.Sonnet)
        {
            usage[ScoutStreams.Sonnet] = Line(50 * arm.Count);
        }

        var withUsage = ScoutStreams.Edit(stream, e => Result(e, r => r["modelUsage"] = usage.DeepClone()));
        var cost = ScoutCost.Of(ScoutTokens.Attribute(ClaudeSubagents.Read(withUsage)).Ok(), Prices).Ok().TotalUsd;
        var reported = variant == CellVariant.CostOff ? decimal.Round(cost * 1.1m, 6) : decimal.Round(cost, 6);

        return ScoutStreams.Edit(withUsage, e => Result(e, r =>
        {
            r["total_cost_usd"] = reported;
            r["subtype"] = variant == CellVariant.BudgetExhausted ? "error_max_budget_usd" : "success";
        }));
    }

    public static decimal CostOf(string stream) => ClaudeSubagents.Read(stream).Envelope.TotalCostUsd.Value;

    private static JsonObject Result(JsonObject e, Action<JsonObject> edit)
    {
        if (e["type"]?.GetValue<string>() == "result")
        {
            edit(e);
        }

        return e;
    }

    private static JsonObject Line(long output) => new() { ["inputTokens"] = 1, ["outputTokens"] = output, ["cacheReadInputTokens"] = 0, ["cacheCreationInputTokens"] = 0, ["costUSD"] = 0 };
}
