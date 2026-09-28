using System.Text.Json;
using Bench.Application.Gate;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bench.Cli;

/// <summary><c>bench gate report</c> and <c>bench gate suite record</c> (E6). The report prints the SAME object the API
/// answers (<see cref="GateReportContract"/> through <see cref="GateReportQuery"/>), as JSON with <c>--json</c> and as a
/// table otherwise; every refusal is a sentence and an exit code — 4 for a request asked wrongly (it names what there is
/// to choose), 3 for a database that cannot answer or does not yet hold the suite's tasks.</summary>
public static class GateReportCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary><c>bench gate report --gate plan|code|feature --scope &lt;scope id | suite stamp&gt; --rubric &lt;id&gt; --db … [--json]</c>.</summary>
    public static async Task<int> ReportAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var flags = (GateWord.Parse(command.Value("gate")), GateCliInputs.Connection(command).Length) switch
        {
            (Outcome<GateKind>.Fail bad, _) => $"gate report needs --gate plan|code|feature — {bad.Reason}",
            (_, 0) => "gate report needs the database — pass --db or set BENCH_DB",
            _ => string.Empty,
        };

        if (flags.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, flags);
        }

        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
            return await AnswerAsync(command, new PostgresGateReads(db, TimeProvider.System), output, error, cancellationToken);
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}");
        }
    }

    /// <summary><c>bench gate suite record --suite-file &lt;file&gt;[,&lt;file&gt;…] --db …</c> — the backfill for what was imported
    /// before <c>gate_suite_tasks</c> existed. Each suite in one transaction; the same suite again records nothing.</summary>
    public static async Task<int> RecordSuitesAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var files = command.List("suite-file");
        var flags = (files.Count, GateCliInputs.Connection(command).Length) switch
        {
            (0, _) => "gate suite record needs --suite-file <suite.json>[,<suite.json>…]",
            (_, 0) => "gate suite record needs the database — pass --db or set BENCH_DB",
            _ => string.Empty,
        };
        var absent = files.FirstOrDefault(f => !File.Exists(f));

        if (flags.Length > 0 || absent is not null)
        {
            return flags.Length > 0
                ? GateRunCommand.Refuse(error, ExitCodes.Configuration, flags)
                : GateRunCommand.Refuse(error, ExitCodes.Environment, $"the suite file {Path.GetFileName(absent)} is not there");
        }

        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
            return await RecordEachAsync(files, new PostgresGateSuiteTasks(db, TimeProvider.System), output, error, cancellationToken);
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}");
        }
    }

    /// <summary>Records one suite's tasks and says so — the line every verb that loads a suite prints (run, resume, the imports).
    /// Empty on success; the refusal otherwise.</summary>
    public static async Task<string> RecordAsync(IGateSuiteTasks tasks, GateSuite suite, TextWriter output, CancellationToken cancellationToken) =>
        await tasks.RecordAsync(suite, cancellationToken) switch
        {
            Outcome<int>.Ok { Value: 0 } => Said(output, $"suite          {suite.Stamp} — 0 task(s) recorded (already there)"),
            Outcome<int>.Ok recorded => Said(output, $"suite          {suite.Stamp} — {recorded.Value} task(s) recorded"),
            Outcome<int>.Fail refused => refused.Reason,
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static async Task<int> RecordEachAsync(
        IReadOnlyList<string> files, IGateSuiteTasks tasks, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            var refusal = GateSuiteFile.Parse(await File.ReadAllTextAsync(file, cancellationToken)) switch
            {
                Outcome<GateSuite>.Ok suite => await RecordAsync(tasks, suite.Value, output, cancellationToken),
                Outcome<GateSuite>.Fail bad => $"{Path.GetFileName(file)}: {bad.Reason}",
                _ => throw new InvalidOperationException("unreachable"),
            };

            if (refusal.Length > 0)
            {
                return GateRunCommand.Refuse(error, ExitCodes.Configuration, refusal);
            }
        }

        return ExitCodes.Pass;
    }

    private static async Task<int> AnswerAsync(CommandLine command, IGateReads reads, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var gate = command.Value("gate").ToLowerInvariant();
        var scopes = ((GateAnswer<IReadOnlyList<GateScopeDto>>.Answered)await GateReportQuery.ScopesAsync(reads, gate, cancellationToken)).Value;

        return GateReportQuery.ResolveScope(scopes, command.Value("scope")) switch
        {
            GateAnswer<GateScopeDto>.Answered scope => await TableAsync(command, reads, scope.Value, output, error, cancellationToken),
            GateAnswer<GateScopeDto>.Refused refused => GateRunCommand.Refuse(error, ExitCodes.Configuration, refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static async Task<int> TableAsync(
        CommandLine command, IGateReads reads, GateScopeDto scope, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var rubric = command.Value("rubric");
        if (rubric.Length == 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"{GateReportQuery.NoRubricNamed} — {Rubrics(scope)}");
        }

        return await GateReportQuery.ModelsAsync(reads, scope.Gate, scope.Id, rubric, cancellationToken) switch
        {
            GateAnswer<GateModelTableDto>.Answered table => Printed(output, command.Has("json") ? JsonSerializer.Serialize(table.Value, Web) : GateReportText.Of(table.Value)),
            GateAnswer<GateModelTableDto>.Refused { Kind: GateRefusalKind.Conflict } refused => GateRunCommand.Refuse(error, ExitCodes.Environment, refused.Reason),
            GateAnswer<GateModelTableDto>.Refused refused => GateRunCommand.Refuse(error, ExitCodes.Configuration, refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static string Rubrics(GateScopeDto scope) =>
        scope.Rubrics.Count == 0
            ? "nothing in this scope was assessed yet (bench gate assess)"
            : "this scope's verdicts carry " + string.Join(", ", scope.Rubrics.Select(r => $"{r.Stamp} ({r.Kind}, {r.Verdicts} verdict(s))"));

    private static int Printed(TextWriter output, string text)
    {
        output.WriteLine(text);
        return ExitCodes.Pass;
    }

    private static string Said(TextWriter output, string line)
    {
        output.WriteLine(line);
        return string.Empty;
    }
}
