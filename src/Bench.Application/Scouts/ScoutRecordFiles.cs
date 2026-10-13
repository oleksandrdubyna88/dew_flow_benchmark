using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;

namespace Bench.Application.Scouts;

/// <summary>The two committed records of §3.12 that are built from a run's rows, pure: <c>cells.jsonl</c> (one line per cell — arm, run,
/// pins, the read-back's outcome and reason, per-scout model / time / tokens / cost / KEY FILES, recall, assessed claims) and the RESULTS
/// skeleton (the per-workload and per-task tables of <c>research/RESULTS_scout_models.md</c>, to be filled with the reading). Every task
/// value goes through <see cref="ScoutPublication"/> — a private task's KEY FILES are not written, its repository is its label — and the
/// <c>cells.jsonl</c> text is scrubbed (§3.13) on the way out, the second line behind the projection.</summary>
public static class ScoutRecordFiles
{
    /// <summary>One line per cell, or a typed failure when a cell names a task the run does not hold — never a line written under a guessed
    /// privacy (E2 code round: a missing task used to read as <i>private</i>, the safe default, which turned a defect in the join into a
    /// committed record).</summary>
    public static Outcome<string> CellsJsonl(
        ScoutRun run, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutCellRecord> records,
        IReadOnlyList<ScoutAssessedClaim> verdicts, ScrubDenyList denyList)
    {
        var byTask = tasks.ToDictionary(t => t.Id.Value, StringComparer.Ordinal);
        var missing = cells.Select(c => c.Task.Value).Where(t => !byTask.ContainsKey(t)).Distinct(StringComparer.Ordinal).ToList();

        if (missing.Count > 0)
        {
            return Outcome<string>.Failure($"cells.jsonl: the cells name {missing.Count} task(s) ({string.Join(", ", missing)}) not among the run's {tasks.Count} task(s) — nothing was written");
        }

        var scouts = records.ToLookup(r => r.CellId, r => r.Scout);
        var assessed = verdicts.ToLookup(v => v.Key.CellId);
        var lines = cells.OrderBy(c => c.Slot).ThenBy(c => c.Position).ThenBy(c => c.Generation)
            .Select(cell => Line(run, cell, byTask[cell.Task.Value], [.. scouts[cell.Id].OrderBy(s => s.Ordinal)], assessed[cell.Id].Count()).ToJsonString());

        return Outcome<string>.Success(ScoutScrubber.Scrub(string.Join('\n', lines) + "\n", denyList));
    }

    private static JsonObject Line(ScoutRun run, ScoutCell cell, ScoutTask task, IReadOnlyList<ScoutRecord> scouts, int assessedClaims)
    {
        var privacy = task.Privacy;

        return new JsonObject
        {
            ["run"] = run.Id.ToString("D"),
            ["cell"] = cell.Id.ToString("D"),
            ["task"] = cell.Task.Value,
            ["privacy"] = ScoutTaskPrivacies.Word(privacy),
            ["arm"] = ScoutArms.Word(cell.Arm),
            ["repeat"] = cell.Repeat,
            ["generation"] = cell.Generation,
            ["pins"] = new JsonObject
            {
                ["cliVersion"] = run.Pins.CliVersion,
                ["pluginCacheHash"] = run.Pins.PluginCacheHash,
                ["resolvedOpus"] = run.Pins.ResolvedOpus,
                ["resolvedSonnet"] = run.Pins.ResolvedSonnet,
                ["settingsHash"] = run.Pins.SettingsHash,
            },
            ["outcome"] = cell.Outcome.ToString().ToLowerInvariant(),
            ["reason"] = cell.Reason.ToString(),
            ["recallFound"] = Count(cell.Facts.RecallFound),
            ["recallTotal"] = Count(cell.Facts.RecallTotal),
            ["teamMs"] = Count(cell.Facts.TeamMs),
            ["sessionMs"] = Count(cell.Facts.SessionMs),
            ["teamUsd"] = Usd(cell.Facts.TeamUsd),
            ["reportedUsd"] = Usd(cell.Facts.ReportedUsd),
            ["assessedClaims"] = assessedClaims,
            ["scouts"] = new JsonArray([.. scouts.Select(s => (JsonNode)Scout(s, privacy))]),
        };
    }

    private static JsonObject Scout(ScoutRecord scout, ScoutTaskPrivacy privacy) => new()
    {
        ["ordinal"] = scout.Ordinal,
        ["focus"] = scout.Focus.ToString(),
        ["inputModel"] = scout.InputModel,
        ["resolvedModel"] = scout.ResolvedModel,
        ["durationMs"] = Count(scout.DurationMs),
        ["tokens"] = Tokens(scout.Tokens),
        ["outputSeparable"] = scout.OutputSeparable,
        ["costUsd"] = Usd(scout.CostUsd),
        ["keyFiles"] = new JsonArray([.. ScoutPublication.Paths(privacy, scout.KeyFiles).Select(p => (JsonNode)JsonValue.Create(p))]),
        ["keyFilesCount"] = scout.KeyFiles.Count,
        ["silent"] = scout.Silent,
        ["claimsParsed"] = scout.ClaimsParsed,
        ["claimsUnparseable"] = scout.ClaimsUnparseable,
    };

    private static JsonObject Tokens(TokenUsage usage) => new()
    {
        ["input"] = usage.Input,
        ["output"] = usage.Output,
        ["cacheRead"] = usage.CacheRead,
        ["cacheWrite5m"] = usage.CacheWrite5m,
        ["cacheWrite1h"] = usage.CacheWrite1h,
    };

    private static JsonNode? Count(CapturedCount count) => count.WasCaptured ? JsonValue.Create(count.Value) : null;

    private static JsonNode? Usd(CapturedUsd usd) => usd.WasCaptured ? JsonValue.Create(usd.Value) : null;

    /// <summary>The skeleton of <c>RESULTS_scout_models.md</c>: the run's identity and pins, the verdict line, one row per workload with its
    /// intervals, one row per arm, and the per-task table — every task through the projection; the reading itself is the writer's to add.</summary>
    public static string ResultsSkeleton(ScoutRunReportDto report)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# RESULTS — scout model calibration ({report.Kind})\n\n");
        text.Append(CultureInfo.InvariantCulture, $"> Status: **{Verdict(report.Verdict)}** — run `{report.RunId:D}`, created {report.CreatedAt:yyyy-MM-dd HH:mm} UTC, seed {report.Seed}, {report.Repeats} repeat(s).\n");
        text.Append(CultureInfo.InvariantCulture, $"> Pins: claude {report.Pins.CliVersion} · opus `{report.Pins.ResolvedOpus}` · sonnet `{report.Pins.ResolvedSonnet}` · plugin `{Short(report.Pins.PluginCacheHash)}` · settings `{Short(report.Pins.SettingsHash)}`.\n\n");
        text.Append("## The reading\n\n_(to be written from the tables below — observed against predicted, per workload)_\n\n");
        Workloads(text, report);
        Arms(text, report);
        Tasks(text, report);
        return text.ToString();
    }

    private static void Workloads(StringBuilder text, ScoutRunReportDto report)
    {
        text.Append("## Per workload\n\n| workload | A | B | outcome | Δrecall [lower, upper] | Δwrong/10 [lower, upper] | tasks | repositories | reasons |\n|---|---|---|---|---|---|---|---|---|\n");

        foreach (var w in report.Workloads)
        {
            text.Append(CultureInfo.InvariantCulture, $"| {w.Workload} | {w.ArmA} | {w.ArmB} | {w.Outcome} | {Interval(w.Recall)} | {Interval(w.WrongPerTen)} | {w.Recall.Tasks} | {w.Recall.Clusters} | {string.Join(", ", w.Reasons)} |\n");
        }

        text.Append('\n');
    }

    private static void Arms(StringBuilder text, ScoutRunReportDto report)
    {
        text.Append("## Per arm\n\n| arm | scouts | recall | wrong/10 | team ms | scout ms | team $ | $ per right file | caller load | cells | scored | void | stopped | aborted |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");

        foreach (var a in report.Arms)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"| {a.Arm} | {a.Scouts} × {a.Model} | {F(a.Recall)} | {F(a.WrongPerTen)} | {F(a.TeamMs)} | {F(a.ScoutMs)} | {F(a.TeamUsd)} | {F(a.UsdPerRightFile)} | {F(a.CallerLoad)} | {a.Counts.Cells} | {a.Counts.Scored} | {a.Counts.Void} | {a.Counts.Stopped} | {a.Counts.Aborted} |\n");
        }

        text.Append('\n');
    }

    private static void Tasks(StringBuilder text, ScoutRunReportDto report)
    {
        text.Append("## Per task\n\n| task | repository | kind | \\|G\\| | per arm (recall · wrong/10 · team ms · team $) |\n|---|---|---|---|---|\n");

        foreach (var t in report.Tasks)
        {
            var arms = string.Join(" · ", t.Arms.Select(a => string.Create(CultureInfo.InvariantCulture, $"{a.Arm} {F(a.Recall)} / {F(a.WrongPerTen)} / {F(a.TeamMs)} / {F(a.TeamUsd)}")));
            text.Append(CultureInfo.InvariantCulture, $"| {t.Task} | {Repository(t)} | {t.Kind} | {t.GroundTruth} | {arms} |\n");
        }
    }

    /// <summary>A private task reads <c>p03 (private · PHP)</c>: the DTO's repository is already the label, the table says why.</summary>
    private static string Repository(ScoutTaskReportDto task) =>
        task.Privacy == ScoutTaskPrivacies.PrivateWord ? $"{task.Repository} (private · {task.Language})" : task.Language.Length > 0 ? $"{task.Repository} · {task.Language}" : task.Repository;

    private static string Verdict(string verdict) => verdict == "preliminary" ? "preliminary — not a verdict" : verdict;

    private static string Interval(ScoutIntervalDto i) =>
        i.Known ? string.Create(CultureInfo.InvariantCulture, $"{i.Point:+0.####;-0.####;0} [{i.Lower:+0.####;-0.####;0}, {i.Upper:+0.####;-0.####;0}]{(i.Estimable ? string.Empty : " (not estimable)")}") : "—";

    private static string F(ScoutFigureDto figure) => figure.State switch
    {
        ScoutFigureDto.KnownState => figure.Value.ToString("0.######", CultureInfo.InvariantCulture),
        ScoutFigureDto.NotMeasuredState => "not measured",
        _ => "—",
    };

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;
}
