using System.Globalization;
using System.Text;
using Bench.Application;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Registry;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Scouts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Cli;

/// <summary><c>bench scouts access-check --out &lt;summary.md&gt; --artifact-root … [--work-root …] [--claude-exe …] [--plugin-cache …]
/// [--plugin-registry …] [--claude-settings …] [--trees-root …] [--checkout-root …]</c> — plan §3.2's access check (E2 S5), LIVE: the exact
/// campaign launch, once for <c>feature-dev:code-explorer</c> and once for <c>Explore</c> (each scout on <c>sonnet</c>, each launch capped at
/// $1), against a sibling tree, every directory the user settings grant (read only — an existing file is asked for), the mirror root and a
/// junction escaping the tree, each planted with its own random canary (<see cref="ScoutAccessPlanting"/>). Per target per agent: denied =
/// protected; no attempt = inconclusive; content returned, or a canary token in any tool result = the boundary is broken.
/// <para>
/// Exit 0 every target denied for every agent · 3 content returned (naming target and agent), or a target NOT denied by every agent — one
/// agent inconclusive is enough (E2 code round) · 4 configuration (the flags, the roots, the pinned files) · 5 a launch that did not run.
/// Each launch is announced before it is awaited and said to be running every 30 s while it is. Nothing a public writer prints is a path: a
/// granted directory is <c>grant[i]</c>, the streams stay under the work root, and the Markdown summary at <c>--out</c> carries names and hashes only.
/// </para></summary>
public static class ScoutsAccessCommand
{
    /// <summary>The git wall of the planting: <c>git init</c> and one commit of two files in a fresh folder.</summary>
    private static readonly TimeSpan PlantingGitWall = TimeSpan.FromMinutes(2);

    /// <summary>How often a running launch says so on the console — a launch is paid for and takes a minute or more.</summary>
    private static readonly TimeSpan ProgressEvery = TimeSpan.FromSeconds(30);

    private sealed record Judged(string Agent, AccessLaunch Launch, IReadOnlyList<AccessVerdict> Verdicts);

    /// <summary>What the verb was asked for: the summary's path and the CLI, refused by name (4).</summary>
    private sealed record Asked(string Summary, string Executable);

    public static async Task<int> RunAsync(CommandLine command, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var asked = AskedOf(command, services);

        if (asked is Outcome<Asked>.Fail refused)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, refused.Reason);
        }

        var (code, roots) = await ScoutsInputs.RootsAsync(command, artifactsRequired: true, error, cancellationToken);
        if (roots is null)
        {
            return code;
        }

        var ask = ((Outcome<Asked>.Ok)asked).Value;
        var context = await ScoutsInputs.LaunchContextAsync(command, roots, ask.Executable, cancellationToken);
        if (context is Outcome<ScoutLaunchContext>.Fail notReady)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, notReady.Reason);
        }

        return await CheckAsync(command, services, roots, ((Outcome<ScoutLaunchContext>.Ok)context).Value, ask, output, error, cancellationToken);
    }

    private static Outcome<Asked> AskedOf(CommandLine command, ScoutVerbServices services) =>
        (command.Value("out"), ScoutsInputs.Executable(command, services)) switch
        {
            ({ Length: 0 }, _) => Outcome<Asked>.Failure("scouts access-check needs --out <summary.md> — where its Markdown summary is written"),
            (_, Outcome<string>.Fail fail) => Outcome<Asked>.Failure($"the claude CLI cannot run on this machine — {fail.Reason}"),
            (var summary, Outcome<string>.Ok exe) => Outcome<Asked>.Success(new Asked(summary, exe.Value)),
            _ => Outcome<Asked>.Failure("unreachable"),
        };

    private static async Task<int> CheckAsync(
        CommandLine command, ScoutVerbServices services, ScoutRootSet roots, ScoutLaunchContext context, Asked asked, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var work = Path.Combine(command.Value("work-root", Path.Combine(Path.GetTempPath(), "bench-scout-access")), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);
        var grants = ClaudeGrants.Read(context.Pins.SettingsFiles, Path.Combine(work, "tree"));

        if (grants is Outcome<IReadOnlyList<string>>.Fail unreadable)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"the Claude user settings could not be read — {unreadable.Reason}");
        }

        output.WriteLine($"pinned         claude {context.Local.CliVersion} · plugin {context.Local.PluginCacheHash[..12]} · pinned settings {context.Local.SettingsHash[..12]}");
        output.WriteLine($"grants         {((Outcome<IReadOnlyList<string>>.Ok)grants).Value.Count} directory(ies) granted by the user settings — named grant[i], never by path");

        var planted = await ScoutAccessPlanting.PlantAsync(work, roots.Mirrors, ((Outcome<IReadOnlyList<string>>.Ok)grants).Value, PlantingGitWall, cancellationToken);

        if (planted is Outcome<AccessPlant>.Fail notPlanted)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, notPlanted.Reason);
        }

        var plant = ((Outcome<AccessPlant>.Ok)planted).Value;
        output.WriteLine($"planted        {string.Join(", ", plant.Targets.Select(t => t.Testable ? t.Name : $"{t.Name} (not tested — no small text file to ask for)"))} — work folder {Path.GetFileName(work)}");

        try
        {
            return await LaunchAllAsync(plant, asked.Executable, context.Files, services, output, cancellationToken) switch
            {
                Outcome<IReadOnlyList<Judged>>.Ok judged => await ConcludeAsync(judged.Value, plant, context.Local, asked.Summary, output, error, cancellationToken),
                Outcome<IReadOnlyList<Judged>>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.NoReport, fail.Reason),
                _ => ExitCodes.NoReport,
            };
        }
        finally
        {
            ScoutAccessPlanting.Unplant(plant);
        }
    }

    /// <summary>One launch per agent type — announced before it is awaited, said to be running every <see cref="ProgressEvery"/> while it is,
    /// reported as it ends; the first launch that did not run ends the check (5).</summary>
    private static async Task<Outcome<IReadOnlyList<Judged>>> LaunchAllAsync(AccessPlant plant, string executable, ScoutLaunchFiles files, ScoutVerbServices services, TextWriter output, CancellationToken cancellationToken)
    {
        var judged = new List<Judged>();

        foreach (var agent in ScoutAccessCheck.Agents)
        {
            output.WriteLine($"launching      {agent} …");
            var one = await ElapsedTicker.WithProgressAsync(
                LaunchAsync(agent, plant, executable, files, services, cancellationToken), ProgressEvery,
                elapsed => output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"running        {agent} — {elapsed.TotalSeconds:0} s elapsed")), cancellationToken);

            if (one is Outcome<Judged>.Fail notLaunched)
            {
                return Outcome<IReadOnlyList<Judged>>.Failure($"the access check could not launch {agent} — {notLaunched.Reason}");
            }

            judged.Add(((Outcome<Judged>.Ok)one).Value);
            Report(((Outcome<Judged>.Ok)one).Value, output);
        }

        return Outcome<IReadOnlyList<Judged>>.Success(judged);
    }

    private static async Task<Outcome<Judged>> LaunchAsync(string agent, AccessPlant plant, string executable, ScoutLaunchFiles files, ScoutVerbServices services, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(plant.Work, agent.Replace(':', '-').ToLowerInvariant());
        Directory.CreateDirectory(folder);
        var driver = ScoutAccessCheck.Driver(agent, ScoutAccessCheck.ScoutPrompt(plant.Targets, plant.ControlFile));
        await File.WriteAllTextAsync(Path.Combine(folder, "prompt.txt"), driver, cancellationToken);

        Outcome<AgentTranscript> transcript;
        await using (var file = new StreamWriter(Path.Combine(folder, "stream.ndjson"), false, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
        {
            var ask = new AgentAsk(ModelRuntimeKind.CliClaude, executable, driver, plant.Tree, ScoutLaunch.CheckWall, ScoutLaunch.MainModel)
            {
                Options = ScoutLaunch.Options(ScoutAccessCheck.CapUsd, files),
                Environment = AgentEnvironment.Only(ScoutsAliasCommand.ChildEnvironment(services)),
                Output = AgentOutputSink.Lines(file.WriteLine),
            };

            transcript = await new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance).TranscriptAsync(ask, cancellationToken);
        }

        return transcript.Match(
            t => Outcome<Judged>.Success(Judge(agent, t.Stdout, plant)),
            Outcome<Judged>.Failure);
    }

    private static Judged Judge(string agent, string stdout, AccessPlant plant)
    {
        var read = ClaudeSubagents.Read(stdout);

        return new Judged(
            agent,
            new AccessLaunch(agent, read.Envelope.TotalCostUsd, read.Envelope.DurationMs) { Fallbacks = ScoutAccessCheck.ModelFallbacks(stdout) },
            ScoutAccessCheck.Judge(agent, read, stdout, plant.Targets, plant.Tree));
    }

    private static void Report(Judged judged, TextWriter output)
    {
        output.WriteLine($"launched       {judged.Launch.Line}");
        output.WriteLine($"verdict        {judged.Agent}: {string.Join(" · ", judged.Verdicts.Select(ScoutAccessCheck.Describe))}");
    }

    private static async Task<int> ConcludeAsync(IReadOnlyList<Judged> judged, AccessPlant plant, ScoutLocalPins local, string summary, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var verdicts = judged.SelectMany(j => j.Verdicts).ToList();
        var inputs = new AccessReportInputs(DateTimeOffset.UtcNow, local.CliVersion, local.PluginCacheHash, local.SettingsHash, plant.Targets, [.. judged.Select(j => j.Launch)], verdicts);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(summary)) ?? ".");
        await File.WriteAllTextAsync(summary, ScoutAccessReport.Markdown(inputs), new UTF8Encoding(false), cancellationToken);
        output.WriteLine("summary        written to --out");

        var conclusion = ScoutAccessCheck.Conclude(verdicts);
        var sentence = ScoutAccessReport.Conclusion(conclusion);

        if (conclusion.Protected)
        {
            output.WriteLine($"access-check   {sentence}");
            return ExitCodes.Pass;
        }

        return GateRunCommand.Refuse(error, ExitCodes.Environment, sentence);
    }
}
