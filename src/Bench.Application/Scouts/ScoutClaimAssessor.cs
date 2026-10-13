using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Registry;
using Bench.Domain.Scouts;

namespace Bench.Application.Scouts;

/// <summary>Who assesses: the codex CLI resolved on this machine, the model it is pinned to (recorded on every verdict), and a wall per batch.</summary>
public sealed record ScoutAssessorLaunch(string Executable, string Model, TimeSpan Wall);

/// <summary>Where an assessment batch's files go: a fresh folder per batch under the artefact root, never inside the tree the assessor reads.</summary>
public interface IScoutAssessmentFiles
{
    /// <summary>A FRESH folder for one batch of one run; its absolute path.</summary>
    Task<string> BeginBatchAsync(Guid runId, string batchId, CancellationToken cancellationToken);

    Task WriteBatchFileAsync(string batchDirectory, string name, string text, CancellationToken cancellationToken);
}

/// <summary>The scouts' blinded claim assessor (§3.7): one batch → one codex launch → the verdicts it gave. Codex runs with the options the
/// gate's assessor already builds (<see cref="FindingAssessor.OptionsFor"/>: <c>-s read-only</c>, the output schema enforced, the final message
/// written to a file, every MCP server off) — but with THIS context's own prompt and schema (<see cref="ScoutAssessment"/>), and with the
/// task's history-free tree as its working directory, so it checks each claim against the code the scouts read. The batch folder holds the
/// prompt, the schema and the answer; the blinding KEY is written there only after the answer is read.</summary>
public sealed class ScoutClaimAssessor(ICliAgentRuntime runtime, IScoutAssessmentFiles files)
{
    public const string KeyFile = "key.json";

    public async Task<Outcome<IReadOnlyList<ScoutClaimAnswer>>> AskAsync(
        ScoutAssessorLaunch launch, Guid runId, IReadOnlyList<ScoutBlindClaim> batch, string treeRoot, CancellationToken cancellationToken)
    {
        var directory = await files.BeginBatchAsync(runId, Guid.CreateVersion7().ToString("N"), cancellationToken);
        var prompt = ScoutAssessment.Prompt(batch);

        await files.WriteBatchFileAsync(directory, FindingAssessor.PromptFile, prompt, cancellationToken);
        await files.WriteBatchFileAsync(directory, FindingAssessor.SchemaFile, ScoutAssessment.Schema, cancellationToken);

        var answer = await runtime.AskAsync(
            new AgentAsk(ModelRuntimeKind.CliCodex, launch.Executable, prompt, treeRoot, launch.Wall, launch.Model)
            {
                Options = FindingAssessor.OptionsFor(ModelRuntimeKind.CliCodex, directory),
            },
            cancellationToken);

        await files.WriteBatchFileAsync(directory, KeyFile, Key(batch), cancellationToken);

        return answer.Match(ok => ScoutAssessment.Read(ok.Text, batch), Outcome<IReadOnlyList<ScoutClaimAnswer>>.Failure);
    }

    /// <summary>Which claim each blinded id stands for — for an audit from disk; the database holds verdicts already joined to their claims.</summary>
    private static string Key(IReadOnlyList<ScoutBlindClaim> batch) =>
        string.Join('\n', batch.Select(c => System.Text.Json.JsonSerializer.Serialize(new
        {
            id = c.Id.Value,
            cell = c.Claim.Key.CellId,
            scout = c.Claim.Key.Ordinal,
            claim = c.Claim.Key.Index,
            task = c.Claim.Task,
        }))) + "\n";
}
