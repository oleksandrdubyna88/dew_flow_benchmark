using System.Text.Json;
using Bench.Application;
using Bench.Contracts;
using Bench.Domain;

namespace Bench.Cli;

/// <summary>`bench plan` — materialise a run without executing it: the target, the frozen suite, every
/// cell in execution order, the selection/held-out split, and the warnings worth reading before
/// anything is spent. Offline, free, and the fastest way to see whether a measurement is even
/// well-formed.
/// <para>
/// The command itself does IO and nothing else. Every decision is <see cref="PlanRequestHandler"/>,
/// which the API calls too.
/// </para></summary>
public static class PlanCommand
{
    public static int Run(CommandLine command, TextWriter output, TextWriter error)
    {
        var suiteFile = command.Value("suite-file");

        if (suiteFile.Length == 0)
        {
            return Fail(error, "--suite-file is required", ExitCodes.Configuration);
        }

        if (!File.Exists(suiteFile))
        {
            return Fail(error, $"suite file not found: {suiteFile}", ExitCodes.Environment);
        }

        var request = ReadRequest(command, File.ReadAllText(suiteFile));

        return PlanRequestHandler.Handle(request).Match(
            plan => Write(command, output, plan),
            reason => Fail(error, reason, ExitCodes.Configuration));
    }

    private static PlanRequestDto ReadRequest(CommandLine command, string suiteJson) => new()
    {
        Repo = command.Value("repo"),
        Commit = command.Value("commit"),
        SuiteJson = suiteJson,
        Repeats = command.Int("repeats", 1),
        Seed = command.Int("seed", 1),
        Subjects = command.List("subjects"),
        Lanes = command.List("lanes"),
        Exclusions = command.List("exclude"),
        Engine = command.Value("engine", "NoRetrieval"),
        EngineEndpoint = command.Value("engine-endpoint"),
        EngineVersion = command.Value("engine-version"),
        IndexFingerprint = command.Value("index-fingerprint"),
    };

    private static int Write(CommandLine command, TextWriter output, RunPlanDto plan)
    {
        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
            return ExitCodes.Pass;
        }

        output.WriteLine($"target   {plan.TargetCanonical}");
        output.WriteLine($"suite    {plan.SuiteStamp}  ({plan.QuestionCount} question(s))");
        output.WriteLine($"engine   {plan.EngineCanonical}");
        output.WriteLine($"matrix   {plan.QuestionCount} x {plan.Repeats} repeat(s) x {plan.LegCount} leg(s) = {plan.Cells.Count} cell(s)");
        output.WriteLine($"first    {string.Join(", ", plan.FirstPositionCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");
        output.WriteLine($"split    {string.Join(", ", plan.SplitHalves.Select(kv => $"{kv.Key}={kv.Value}"))}");

        foreach (var warning in plan.Warnings)
        {
            output.WriteLine($"warn     {warning}");
        }

        return ExitCodes.Pass;
    }

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }
}
