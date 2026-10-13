using System.Globalization;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Bench.Cli;

/// <summary><c>bench scouts assess --run &lt;id&gt; --assessor-model &lt;id&gt; [--codex-exe path] [--assess-timeout-minutes 10]</c> (E1 S3, §3.7) — a
/// PAID verb: every <c>CLAIMS</c> line of every scored cell that has no verdict yet goes to codex read-only (the gate assessor's launch options,
/// this context's own prompt and schema, the task's history-free tree as its working directory, fresh blinded ids), and each batch's verdicts
/// are stored the moment they arrive (<see cref="ScoutAssessCampaign"/>). Idempotent: a re-run asks only the claims still without a verdict.
/// <para>
/// E1 tests it only against <c>tests/FakeCli</c> replaying recorded verdicts; its first real call is E2 S4's.
/// </para>
/// Exit codes: 0 every claim has a verdict · 3 claims remain (a failed batch, an unanswered id, a stream or tree that could not be read —
/// re-run to fill them) · 4 a flag, an unknown run, a codex that is not there · 5 no claim to assess.</summary>
public static class ScoutsAssessCommand
{
    public const int DefaultTimeoutMinutes = 10;

    public static Task<int> RunAsync(CommandLine command, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var model = command.Value("assessor-model");
        var codex = ScoutsInputs.Executable(command, services, "codex-exe", "codex");
        var minutes = command.Int("assess-timeout-minutes", DefaultTimeoutMinutes);

        return (model.Trim().Length > 0, codex, minutes is >= 1 and <= 120) switch
        {
            (false, _, _) => Refused(error, "scouts assess needs --assessor-model <id> — the codex model every verdict is recorded under; never the CLI's default"),
            (_, Outcome<string>.Fail fail, _) => Refused(error, $"the codex CLI cannot run on this machine — {fail.Reason}"),
            (_, _, false) => Refused(error, "--assess-timeout-minutes takes 1 to 120"),
            (_, Outcome<string>.Ok exe, _) => ScoutsReadCommand.WithRunAsync(command, artifactsRequired: true, "assess", error, cancellationToken,
                (inputs, run) => AssessAsync(inputs, run, new ScoutAssessorLaunch(exe.Value, model.Trim(), TimeSpan.FromMinutes(minutes)), output, error, cancellationToken)),
            _ => Refused(error, "unreachable"),
        };
    }

    private static Task<int> Refused(TextWriter error, string reason) => Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, reason));

    private static async Task<int> AssessAsync(ScoutsInputs inputs, ScoutRun run, ScoutAssessorLaunch launch, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var assessor = new ScoutClaimAssessor(new CliAgentRuntime(inputs.Logs.CreateLogger<CliAgentRuntime>()), inputs.Artifacts);
        var campaign = new ScoutAssessCampaign(inputs.Verdicts, inputs.Trees, inputs.Artifacts, assessor, TimeProvider.System);
        var report = await campaign.RunAsync(
            run,
            await inputs.Store.TasksAsync(run.Id, cancellationToken),
            await inputs.Store.CellsAsync(run.Id, cancellationToken),
            await inputs.Store.AttemptsAsync(run.Id, cancellationToken),
            launch,
            new Random(),
            output.WriteLine,
            cancellationToken);

        foreach (var problem in report.Problems)
        {
            output.WriteLine($"problem        {problem}");
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"assess         scout run {run.Id} — {report.Claims} claim(s): {report.AlreadyAssessed} already assessed, {report.Asked} asked, {report.Stored} given a verdict, {report.Remaining} remaining · assessor {launch.Model}, rubric {ScoutAssessment.RubricVersion}"));

        return (report.Claims, report.Remaining, report.Problems.Count) switch
        {
            (0, _, 0) => GateRunCommand.Refuse(error, ExitCodes.NoReport, $"scout run {run.Id} has no claim to assess — no scored cell carries a CLAIMS line"),
            (_, 0, 0) => ExitCodes.Pass,
            _ => GateRunCommand.Refuse(error, ExitCodes.Environment,
                $"{report.Remaining} claim(s) still have no verdict{(report.Stopped ? $" — {ScoutAssessCampaign.ConsecutiveFailureLimit} batches failed in a row and the pass stopped" : string.Empty)}; re-run bench scouts assess --run {run.Id} to ask exactly those"),
        };
    }
}
