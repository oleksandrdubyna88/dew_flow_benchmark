using System.Globalization;
using Bench.Application;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Registry;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Probes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Cli;

/// <summary><c>bench scouts alias-check --opus-id … --sonnet-id … [--claude-exe …] [--work-root …]</c> — §3.14's watch: ONE tiny cell (two
/// scouts, one per alias, in a one-file directory, <c>--max-budget-usd 1</c>), launched exactly as a scout cell is (the allow-list, MCP off,
/// the forwarded children, the probes' minimal environment), its stream kept under the work root, and each scout's <c>resolvedModel</c>
/// compared with the recorded id. Exit 0 the same ids · 3 an alias MOVED (the new id printed — a verdict holds only for the ids it
/// measured) · 4 configuration · 5 nothing measured. A PAID verb: E1 tests it only against the fake CLI. The weekly scheduled task that runs
/// it is registered only once a verdict exists to watch (E3).</summary>
public static class ScoutsAliasCommand
{
    public static async Task<int> RunAsync(CommandLine command, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var (opus, sonnet) = (command.Value("opus-id"), command.Value("sonnet-id"));
        var executable = ScoutsInputs.Executable(command, services);

        if (opus.Length == 0 || sonnet.Length == 0 || executable is Outcome<string>.Fail)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, opus.Length == 0 || sonnet.Length == 0
                ? "scouts alias-check needs --opus-id and --sonnet-id — the resolved ids the verdict recorded"
                : $"the claude CLI cannot run on this machine — {((Outcome<string>.Fail)executable).Reason}");
        }

        var work = Path.Combine(command.Value("work-root", Path.Combine(Path.GetTempPath(), "bench-scout-alias")), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);
        var stream = await LaunchAsync(((Outcome<string>.Ok)executable).Value, work, services, cancellationToken);

        return stream switch
        {
            Outcome<string>.Ok ok => Verdict(ScoutAliasCheck.Compare(ClaudeSubagents.Read(ok.Value), opus, sonnet), work, output, error),
            Outcome<string>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.NoReport, $"the alias check could not launch — {fail.Reason}"),
            _ => ExitCodes.NoReport,
        };
    }

    private static async Task<Outcome<string>> LaunchAsync(string executable, string work, ScoutVerbServices services, CancellationToken cancellationToken)
    {
        var tree = Path.Combine(work, "tree");
        Directory.CreateDirectory(tree);
        await File.WriteAllTextAsync(Path.Combine(tree, "README.md"), "# alias check\n\nOne file, so a scout has something to list.\n", cancellationToken);
        var mcp = Path.Combine(work, "empty-mcp.json");
        await File.WriteAllTextAsync(mcp, ScoutLaunch.EmptyMcpConfig, cancellationToken);

        var child = ProbeChildEnvironment.Of(ScoutsInputs.ParentEnvironment()).Variables.Concat(services.TestChildVariables)
            .GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

        await using var file = new StreamWriter(Path.Combine(work, "stream.ndjson")) { AutoFlush = true, NewLine = "\n" };
        var ask = new AgentAsk(ModelRuntimeKind.CliClaude, executable, ScoutAliasCheck.Driver(), tree, TimeSpan.FromMinutes(10), ScoutLaunch.MainModel)
        {
            Options = ScoutLaunch.Options(ScoutAliasCheck.CapUsd, mcp),
            Environment = AgentEnvironment.Only(child),
            Output = AgentOutputSink.Lines(file.WriteLine),
        };

        return (await new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance).TranscriptAsync(ask, cancellationToken)).Match(
            transcript => Outcome<string>.Success(transcript.Stdout), Outcome<string>.Failure);
    }

    private static int Verdict(AliasVerdict verdict, string work, TextWriter output, TextWriter error)
    {
        output.WriteLine($"stream         {Path.Combine(work, "stream.ndjson")}");

        return verdict switch
        {
            AliasVerdict.Same same => Said(output, $"unchanged      {string.Join(", ", same.Ids)} — the verdict's ids still answer"),
            AliasVerdict.Moved moved => GateRunCommand.Refuse(error, ExitCodes.Environment,
                "an alias MOVED — " + string.Join("; ", moved.Changes.Select(c => $"{c.Alias}: {c.Was} → {c.Now}"))
                + " — a verdict holds only for the ids it measured; suspend the rule (the conventions plan's suspension step) and re-run the calibration"),
            AliasVerdict.NotMeasured none => GateRunCommand.Refuse(error, ExitCodes.NoReport, none.Reason),
            _ => ExitCodes.NoReport,
        };
    }

    private static int Said(TextWriter output, string line)
    {
        output.WriteLine(line);
        return ExitCodes.Pass;
    }
}
