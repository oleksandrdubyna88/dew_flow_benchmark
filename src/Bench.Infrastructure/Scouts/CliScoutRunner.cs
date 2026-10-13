using Bench.Application;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Registry;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Probes;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Scouts;

/// <param name="Executable">The claude CLI resolved on this machine.</param>
/// <param name="Prices">The vendor price table read on the run date — what the cost check prices the stream's tokens with (§3.6).</param>
/// <param name="ParentEnvironment">The harness's own environment — what <see cref="ProbeChildEnvironment"/> builds the CLI's minimal one FROM.</param>
public sealed record CliScoutSettings(string Executable, PriceTable Prices, IReadOnlyDictionary<string, string> ParentEnvironment)
{
    /// <summary>Variables added to the child's allow-listed environment — TESTS ONLY (the fake CLI's own home, <c>FAKE_CLI_HOME</c>, so two
    /// setups that both launch <c>--model opus</c> never share one); production composes none, and its environment is exactly the probes'.</summary>
    public IReadOnlyDictionary<string, string> TestChildVariables { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>One attempt of one claimed cell, end to end (§3.4, §3.10). The campaign has claimed the cell and RESERVED its money; this runner:
/// <list type="number">
/// <item>prepares the task's history-free tree (a tree that no longer hashes to its frozen hash STOPS the cell), the empty MCP config, the
/// cell's prompts, and the launch's argv — every refusal here is <see cref="ScoutAttemptResult.NotLaunched"/>, nothing marked, nothing spent;</item>
/// <item>opens the attempt's stream file, then marks the attempt <b>launching</b> in the store — durable, BEFORE the CLI is invoked;</item>
/// <item>launches <c>claude -p --model opus</c> in the tree with the allow-list, MCP off, the forwarded children and the arm's cap, the
/// driver on stdin, under the probes' MINIMAL environment, with the arm's wall (the shared launcher kills the whole process tree at it and
/// on any cancellation) — every stdout line written to the stream and flushed the moment it is read;</item>
/// <item>commits stderr, the launch record, the prompt and the stream's seal — evidence the artefact root refuses is a fault that THROWS:
/// a verdict nobody can audit from disk is not recorded, and the next entry's recovery settles the money from the stream;</item>
/// <item>reads the stream (<see cref="ClaudeSubagents"/>) into a settlement (<see cref="ScoutCellReading"/>: read-back, path audit, pins,
/// spend, cost check) and hands it back with what the CLI said it cost.</item>
/// </list>
/// The <see cref="IScoutCheckpoints"/> port is passed at each money boundary in that order.</summary>
public sealed class CliScoutRunner(
    ICliAgentTranscripts agents, IScoutTrees trees, ScoutArtifacts artifacts, IScoutStore store, IScoutCheckpoints checkpoints, CliScoutSettings settings, ILogger<CliScoutRunner> logger)
    : IScoutRunner
{
    private readonly ChildEnvironment _child = ProbeChildEnvironment.Of(settings.ParentEnvironment);

    private sealed record Launch(ScoutArm Arm, ScoutArmBudget Budget, ScoutTree Tree, ScoutCellPrompts Prompts, AgentAskOptions Options);

    public async Task<ScoutAttemptResult> RunAsync(ScoutRun run, ScoutTask task, ScoutCell claimed, ScoutAttempt attempt, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(run, task, claimed, cancellationToken);

        if (prepared is Outcome<Launch>.Fail refused)
        {
            logger.LogWarning("Scout cell {Cell} was not launched: {Reason}", claimed.Id, refused.Reason);

            return refused.Reason.Contains(ScoutTreeRefusal.HashChanged, StringComparison.Ordinal)
                ? new ScoutAttemptResult.Stopped(ScoutReason.PinMoved, refused.Reason)
                : new ScoutAttemptResult.NotLaunched(refused.Reason);
        }

        var launch = ((Outcome<Launch>.Ok)prepared).Value;

        return artifacts.OpenStream(attempt) switch
        {
            Outcome<ScoutStreamWriter>.Ok stream => await MarkAndLaunchAsync(run, task, attempt, launch, stream.Value, cancellationToken),
            Outcome<ScoutStreamWriter>.Fail fail => new ScoutAttemptResult.NotLaunched(fail.Reason),
            _ => new ScoutAttemptResult.NotLaunched("unreachable"),
        };
    }

    /// <summary>Everything decided before anything is marked: the arm's budget, the tree, the MCP config, the prompts, an argv the CLI has
    /// flags for, an executable that is there.</summary>
    private async Task<Outcome<Launch>> PrepareAsync(ScoutRun run, ScoutTask task, ScoutCell claimed, CancellationToken cancellationToken)
    {
        var arm = ScoutArms.Of(claimed.Arm);
        var budget = run.BudgetOf(claimed.Arm);
        var tree = budget is Outcome<ScoutArmBudget>.Ok ? await trees.PrepareAsync(task, cancellationToken) : Outcome<ScoutTree>.Failure(string.Empty);
        var mcp = artifacts.EmptyMcpConfig();

        return (budget, tree, mcp, File.Exists(settings.Executable)) switch
        {
            (Outcome<ScoutArmBudget>.Fail fail, _, _, _) => Outcome<Launch>.Failure(fail.Reason),
            (_, Outcome<ScoutTree>.Fail fail, _, _) => Outcome<Launch>.Failure(fail.Reason),
            (_, _, Outcome<string>.Fail fail, _) => Outcome<Launch>.Failure(fail.Reason),
            (_, _, _, false) => Outcome<Launch>.Failure($"the claude executable {settings.Executable} is not there"),
            (Outcome<ScoutArmBudget>.Ok b, Outcome<ScoutTree>.Ok t, Outcome<string>.Ok m, _) => Argv(new Launch(arm, b.Value, t.Value, ScoutPrompts.ForCell(task, arm), ScoutLaunch.Options(b.Value.CapUsd, m.Value))),
            _ => Outcome<Launch>.Failure("unreachable"),
        };
    }

    private static Outcome<Launch> Argv(Launch launch) =>
        CliArgv.For(ModelRuntimeKind.CliClaude, ScoutLaunch.MainModel, launch.Options, []).Match(_ => Outcome<Launch>.Success(launch), Outcome<Launch>.Failure);

    private async Task<ScoutAttemptResult> MarkAndLaunchAsync(ScoutRun run, ScoutTask task, ScoutAttempt attempt, Launch launch, ScoutStreamWriter stream, CancellationToken cancellationToken)
    {
        Outcome<AgentTranscript> transcript;

        // The stream file is closed the moment the process has ended — before it is sealed, committed beside and read.
        using (stream)
        {
            checkpoints.Reached(ScoutCheckpoint.BeforeLaunchingMark, attempt.Id);

            if (await store.MarkLaunchingAsync(attempt.Id, cancellationToken) is Outcome<ScoutAttempt>.Fail notMarked)
            {
                return new ScoutAttemptResult.NotLaunched($"{attempt.Label} could not be marked launching — {notMarked.Reason}");
            }

            checkpoints.Reached(ScoutCheckpoint.AfterLaunchingMark, attempt.Id);
            transcript = await agents.TranscriptAsync(Ask(attempt, launch, stream), cancellationToken);
        }

        return transcript switch
        {
            Outcome<AgentTranscript>.Ok ok => await MeasureAsync(run, task, attempt, launch, ok.Value, cancellationToken),
            Outcome<AgentTranscript>.Fail fail => Unlaunched(attempt, fail.Reason),
            _ => Unlaunched(attempt, "unreachable"),
        };
    }

    private AgentAsk Ask(ScoutAttempt attempt, Launch launch, ScoutStreamWriter stream)
    {
        var first = true;

        return new AgentAsk(ModelRuntimeKind.CliClaude, settings.Executable, launch.Prompts.Driver, launch.Tree.Root, launch.Budget.Wall, ScoutLaunch.MainModel)
        {
            Options = launch.Options,
            Environment = AgentEnvironment.Only(ChildVariables()),
            Output = AgentOutputSink.Lines(line =>
            {
                stream.Line(line);
                Passed(attempt, ref first, line);
            }),
        };
    }

    /// <summary>The checkpoints the stream passes: its first line, and the <c>result</c> envelope.</summary>
    private void Passed(ScoutAttempt attempt, ref bool first, string line)
    {
        if (first)
        {
            first = false;
            checkpoints.Reached(ScoutCheckpoint.AfterFirstOutputLine, attempt.Id);
        }

        if (line.StartsWith('{') && line.Contains("\"type\":\"result\"", StringComparison.Ordinal))
        {
            checkpoints.Reached(ScoutCheckpoint.AfterResultLine, attempt.Id);
        }
    }

    private IReadOnlyDictionary<string, string> ChildVariables() =>
        new Dictionary<string, string>(_child.Variables.Concat(settings.TestChildVariables).GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()), StringComparer.OrdinalIgnoreCase);

    /// <summary>The launcher refused AFTER the launching mark (an executable that vanished between the check and the launch): the attempt is
    /// marked, so doubt is charged — no result, the whole reservation as unknown cost — and the cell is void as an incomplete stream.</summary>
    private ScoutAttemptResult Unlaunched(ScoutAttempt attempt, string reason)
    {
        logger.LogError("{Attempt} was marked launching and the launch was refused: {Reason}. It is charged its whole reservation as unknown cost", attempt.Label, reason);

        return new ScoutAttemptResult.Settled(
            new ScoutSettlement(ScoutCellOutcome.Void, ScoutReason.StreamIncomplete, ScoutCellFacts.None), [], CapturedUsd.Unavailable(reason));
    }

    private async Task<ScoutAttemptResult> MeasureAsync(ScoutRun run, ScoutTask task, ScoutAttempt attempt, Launch launch, AgentTranscript transcript, CancellationToken cancellationToken)
    {
        await CommitAsync(attempt, launch, transcript, cancellationToken);
        checkpoints.Reached(ScoutCheckpoint.AfterArtifactCommit, attempt.Id);

        var read = ClaudeSubagents.Read(transcript.Stdout);
        var context = new ScoutCellContext(
            new ReadBackExpectation(launch.Arm, launch.Prompts.Sha256s, run.Pins.Resolved(launch.Arm.Model), launch.Tree.Root),
            run.Pins, settings.Prices, task.GroundTruth, launch.Tree.Files, transcript.Stderr);
        var verdict = ScoutCellReading.Read(read, transcript.Stdout, context, FileSystemLinkTargets.Here);

        if (transcript.TimedOut)
        {
            logger.LogWarning("{Attempt}: the wall of {Wall} ended the CLI's process tree; the cell settles from the stream that reached disk", attempt.Label, launch.Budget.Wall);
        }

        return new ScoutAttemptResult.Settled(verdict.Settlement, verdict.Scouts, verdict.ReportedUsd);
    }

    /// <summary>The evidence beside the stream; a refusal THROWS — the attempt stays launching and the next entry settles it from the stream.</summary>
    private async Task CommitAsync(ScoutAttempt attempt, Launch launch, AgentTranscript transcript, CancellationToken cancellationToken)
    {
        (ScoutArtifactKind Kind, string Text)[] files =
        [
            (ScoutArtifactKind.Stderr, _child.Scrub(transcript.Stderr)),
            (ScoutArtifactKind.Argv, ProbeAttemptCommits.LaunchJson(transcript.Argv, [.. ChildVariables().Keys.Order(StringComparer.Ordinal)])),
            (ScoutArtifactKind.Prompt, launch.Prompts.Driver),
            (ScoutArtifactKind.StreamSeal, artifacts.Seal(attempt)),
        ];

        foreach (var (kind, text) in files)
        {
            if (await artifacts.CommitAsync(attempt, kind, text, CancellationToken.None) is Outcome<Infrastructure.Gate.CommittedBytes>.Fail refused)
            {
                throw new IOException($"{refused.Reason} — the attempt stays launching; the next entry settles its money from the stream on disk");
            }
        }
    }
}
