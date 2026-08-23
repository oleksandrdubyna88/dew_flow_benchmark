using System.Text.Json;
using Bench.Application.Sessions;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Sessions;

namespace Bench.Cli;

/// <summary>`bench sessions` — the agent-session traces, from the terminal.
/// <para>
/// Four actions with four different jobs: <c>install</c> puts the hooks into a repository so a session
/// starts recording itself, <c>list</c> and <c>show</c> read what was recorded, and <c>ingest</c> drains
/// the spool a hook client wrote when it could not reach the collector.
/// </para>
/// <para>
/// <c>list</c> is also, deliberately, the verb that CREATES the schema. The collector refuses to start
/// against a database that is behind, and the CLI owns migrations here — so the first thing an operator
/// runs is the thing that makes the rest possible.
/// </para></summary>
public static class SessionsCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        ISessionStore store,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "list" => await ListAsync(command, store, output, cancellationToken),
            "show" => await ShowAsync(command, store, output, error, cancellationToken),
            "ingest" => await SessionSpool.IngestAsync(command, store, output, error, cancellationToken),
            "install" => SessionHooks.Install(command, output, error),
            var other => Fail(
                error,
                other.Length == 0
                    ? "bench sessions needs an action — 'install', 'list', 'show' or 'ingest'"
                    : $"unknown sessions action '{other}' — try 'install', 'list', 'show' or 'ingest'",
                ExitCodes.Configuration),
        };

    private static async Task<int> ListAsync(
        CommandLine command, ISessionStore store, TextWriter output, CancellationToken cancellationToken)
    {
        var sessions = await store.RecentAsync(command.Int("limit", 25), cancellationToken);
        var rows = sessions.Select(SessionContract.From).ToList();

        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(rows, Indented));
            return rows.Count == 0 ? ExitCodes.NoReport : ExitCodes.Pass;
        }

        if (rows.Count == 0)
        {
            output.WriteLine("no sessions recorded — install the hooks with `bench sessions install --repo <path>`");
            return ExitCodes.NoReport;
        }

        output.WriteLine($"{"session",-38} {"task",-22} {"phase",-13} {"calls",6} {"R",5} {"E",5} {"V",5} {"open",5} {"fail",5}");
        foreach (var row in rows)
        {
            output.WriteLine(
                $"{row.SessionId,-38} {Clip(Name(row), 22),-22} {row.Phase,-13} {row.Calls,6} " +
                $"{row.Research,5} {row.Execution,5} {row.Verification,5} {row.Unfinished,5} {row.CompileFailures,5}");
        }

        output.WriteLine();
        output.WriteLine("R/E/V = research · execution · verification calls; 'open' = calls that never closed");
        return ExitCodes.Pass;
    }

    private static async Task<int> ShowAsync(
        CommandLine command,
        ISessionStore store,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.Value("session"), out var id))
        {
            return Fail(error, "--session <guid> is required", ExitCodes.Configuration);
        }

        var found = await store.ByIdAsync(id, cancellationToken);

        if (found is not Outcome<SessionRun>.Ok(var run))
        {
            return Fail(error, found.Match(_ => string.Empty, reason => reason), ExitCodes.NoReport);
        }

        var detail = SessionContract.From(run, id, ToolTaxonomy.For(run.Runtime));

        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(detail, Indented));
            return ExitCodes.Pass;
        }

        Render(output, detail);
        return ExitCodes.Pass;
    }

    private static void Render(TextWriter output, SessionDetailDto detail)
    {
        output.WriteLine($"session   {detail.Summary.SessionId}  ({detail.Summary.Source}/{detail.Summary.Runtime})");
        output.WriteLine($"task      {Name(detail.Summary)}");
        output.WriteLine($"plan      {Or(detail.Summary.Task.PlanPath, "— none linked")}");
        output.WriteLine($"workspace {detail.Summary.WorkspacePath}  [{Or(detail.Summary.Branch, "?")}]");
        output.WriteLine();

        output.WriteLine($"{"phase",-14} {"calls",6} {"seconds",10}");
        foreach (var phase in detail.Phases)
        {
            output.WriteLine($"{phase.Phase,-14} {phase.Calls,6} {phase.Seconds,10:F1}");
        }

        output.WriteLine();
        output.WriteLine(
            $"switches {detail.Switches} · compile failures {detail.Summary.CompileFailures} · " +
            $"unclassified tools {detail.UnknownTools} · unfinished calls {detail.Summary.Unfinished}");

        RenderFindings(output, detail);
        RenderCalls(output, detail);
    }

    private static void RenderFindings(TextWriter output, SessionDetailDto detail)
    {
        output.WriteLine();

        if (detail.Findings.Count == 0)
        {
            output.WriteLine("findings  none");
            return;
        }

        foreach (var finding in detail.Findings)
        {
            output.WriteLine($"{finding.Kind,-22} {Clip(finding.Subject, 40),-40} {finding.Detail}");
            output.WriteLine($"{string.Empty,-22} calls {string.Join(", ", finding.Ordinals)}");
        }
    }

    private static void RenderCalls(TextWriter output, SessionDetailDto detail)
    {
        output.WriteLine();
        output.WriteLine(
            $"{"#",4} {"tool",-18} {"class",-8} {"phase",-13} {"tree",-10} {"ms",7} {"chars",8} {"target"}");

        foreach (var call in detail.Calls)
        {
            var flag = call.Disagrees ? " ⚠" : string.Empty;
            output.WriteLine(
                $"{call.Ordinal,4} {Clip(call.ToolName, 18),-18} {call.Class,-8} {call.Phase,-13} " +
                $"{call.Mutation,-10} {Duration(call),7} {Chars(call),8} {Clip(call.Target, 56)}{flag}");
        }

        output.WriteLine();
        output.WriteLine(
            "chars = what the tool RETURNED, exact even where the stored body was capped — the input to "
            + "'read forty thousand characters to edit six lines'");
    }

    /// <summary>An unfinished call returned nothing YET, which is not the same as returning nothing.</summary>
    private static string Chars(SessionToolCallDto call) =>
        call.State == "Started" ? "—" : call.ResponseChars.ToString();

    private static string Duration(SessionToolCallDto call) =>
        call.DurationMs.Captured ? call.DurationMs.Value.ToString() : "—";

    private static string Name(SessionSummaryDto row) =>
        row.Task.Name.Length > 0 ? row.Task.Name : Or(row.Task.Id, "— unnamed");

    private static string Or(string value, string fallback) => value.Length > 0 ? value : fallback;

    private static string Clip(string value, int width) =>
        value.Length <= width ? value : value[..(width - 1)] + "…";

    private static JsonSerializerOptions Indented { get; } = new() { WriteIndented = true };

    internal static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }
}
