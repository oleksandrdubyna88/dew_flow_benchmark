using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>A file an imported attempt commits: its name under the attempt root, its class, its bytes.</summary>
public sealed record ImportFile(string RelativePath, ArtifactClass Class, byte[] Bytes);

/// <summary>Everything one imported cell is made of, decided before a byte is written.</summary>
/// <param name="SourceJson">The source record as read — committed as <c>import-source.json</c>, compared on re-import.</param>
/// <param name="SourceId">The source's own id for the record, named in messages.</param>
public sealed record ImportPlan(
    GateRun Campaign,
    GateCell Cell,
    GateSettlement.Completed Settlement,
    string SourceId,
    string SourceJson,
    IReadOnlyList<ImportFile> Files);

public enum ImportOutcome
{
    Imported,
    Unchanged,
}

/// <summary>Writes one imported cell — files first, then ONE transaction — and makes a second import of the same record a
/// no-op and of a changed one a refusal.
/// <para>
/// A crash between the files and the transaction leaves an attempt directory with no row. The next import RESUMES it:
/// every file is written, or — when a killed import already committed it — adopted and compared with the source bytes,
/// so the same bytes are reused and different ones are refused naming the path. <c>run.json</c> is committed LAST, as a
/// native attempt's is.
/// </para></summary>
public sealed class GateImportWriter(IGateArtifactStore artifacts, IGateImportStore store)
{
    public const string SourceFile = "import-source.json";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public async Task<Outcome<ImportOutcome>> WriteAsync(ImportPlan plan, CancellationToken cancellationToken)
    {
        var state = await store.CellStateAsync(plan.Cell.Id, cancellationToken);
        var sourceBytes = Encoding.UTF8.GetBytes(plan.SourceJson);

        return state.Exists
            ? Unchanged(plan, state, sourceBytes)
            : await FreshAsync(plan, sourceBytes, cancellationToken);
    }

    private static Outcome<ImportOutcome> Unchanged(ImportPlan plan, ImportedCellState state, byte[] sourceBytes) =>
        string.Equals(state.SourceSha256, Sha(sourceBytes), StringComparison.Ordinal)
            ? Outcome<ImportOutcome>.Success(ImportOutcome.Unchanged)
            : Outcome<ImportOutcome>.Failure(
                $"record {plan.SourceId} was imported before and now reads differently — a changed record is refused, never overwritten; "
                + "if the source was re-run on purpose, it is a new record only under a new id");

    private async Task<Outcome<ImportOutcome>> FreshAsync(ImportPlan plan, byte[] sourceBytes, CancellationToken cancellationToken)
    {
        var scope = new ArtifactScope(plan.Campaign, plan.Cell.Id, plan.Cell.Attempts);
        var root = CellPaths.AttemptRoot(scope);
        var refs = new List<ArtifactRef>();

        ImportFile[] files = [.. plan.Files, new ImportFile(SourceFile, ArtifactClass.Other, sourceBytes), RunRecord(plan)];

        foreach (var file in files)
        {
            var committed = await CommitAsync(scope, root, file, cancellationToken);
            if (committed is Outcome<ArtifactRef>.Fail fail)
            {
                return Outcome<ImportOutcome>.Failure($"record {plan.SourceId}: {fail.Reason}");
            }

            refs.Add(((Outcome<ArtifactRef>.Ok)committed).Value);
        }

        return (await store.ImportCellAsync(new ImportedCell(plan.Campaign, plan.Cell, plan.Settlement, refs), cancellationToken)).Match(
            _ => Outcome<ImportOutcome>.Success(ImportOutcome.Imported),
            reason => Outcome<ImportOutcome>.Failure($"record {plan.SourceId}: {reason}"));
    }

    /// <summary>Write, or — when a killed import already committed this file — adopt it and require the same bytes.</summary>
    private async Task<Outcome<ArtifactRef>> CommitAsync(ArtifactScope scope, ArtifactPath root, ImportFile file, CancellationToken cancellationToken)
    {
        if (root.Then(file.RelativePath) is not Outcome<ArtifactPath>.Ok { Value: var path })
        {
            return Outcome<ArtifactRef>.Failure($"'{file.RelativePath}' is not a name an artefact can have");
        }

        var written = await artifacts.WriteAsync(scope, file.Class, path, file.Bytes, cancellationToken);
        if (written is Outcome<ArtifactRef>.Ok)
        {
            return written;
        }

        return await artifacts.AdoptAsync(scope, file.Class, path, cancellationToken) switch
        {
            Outcome<ArtifactRef>.Ok adopted when adopted.Value.Sha256 == Sha(file.Bytes) => adopted,
            Outcome<ArtifactRef>.Ok => Outcome<ArtifactRef>.Failure(
                $"{path} is already there from an earlier import with OTHER bytes — refused rather than mixing two readings of one record; remove that attempt directory to import it again"),
            _ => written,
        };
    }

    private static ImportFile RunRecord(ImportPlan plan) =>
        new(CellPaths.RunRecordFile, ArtifactClass.RunRecord, Encoding.UTF8.GetBytes(new JsonObject
        {
            ["runId"] = plan.Campaign.Id.ToString("D"),
            ["cellId"] = plan.Cell.Id.ToString("D"),
            ["attempt"] = plan.Cell.Attempts,
            ["gate"] = plan.Campaign.Gate.ToString(),
            ["task"] = plan.Cell.Task.Value,
            ["reviewer"] = plan.Cell.Reviewer.Value,
            ["repeat"] = plan.Cell.Repeat,
            ["pin"] = plan.Cell.Pin.Describe,
            ["source"] = plan.Campaign.Source.Label,
            ["sourceId"] = plan.SourceId,
            ["outcome"] = plan.Settlement.Kind.ToString(),
            ["facts"] = JsonSerializer.SerializeToNode(plan.Settlement.Facts),
        }.ToJsonString(Indented)));

    public static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
