using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>Where an import reads from — the other harness's folder, READ-ONLY. Paths are relative to the source's root and
/// <c>/</c>-separated; nothing here can write.</summary>
public interface IImportSource
{
    /// <summary>What the source is called in a message and in a campaign key — its folder's name, never its full path.</summary>
    string Label { get; }

    bool Exists(string relative);

    Task<Outcome<byte[]>> ReadBytesAsync(string relative, CancellationToken cancellationToken);

    /// <summary>Every file under <paramref name="relativeDirectory"/>, relative to IT, in ordinal order; none when it is not there.</summary>
    Task<IReadOnlyList<string>> FilesUnderAsync(string relativeDirectory, CancellationToken cancellationToken);
}

/// <summary>Where an imported cell stands in the database: not there, or there with the SHA-256 of the source record it was
/// imported from (its <c>import-source.json</c> ref) — what a re-import compares to tell "unchanged" from "changed".</summary>
public sealed record ImportedCellState(bool Exists, string SourceSha256)
{
    public static ImportedCellState Absent { get; } = new(false, string.Empty);
}

/// <summary>One settled cell as an import writes it: the campaign it belongs to, the cell (settled, its attempt number the
/// source's), the session's facts and findings, and the refs of the files already committed for it.</summary>
public sealed record ImportedCell(GateRun Campaign, GateCell Cell, GateSettlement.Completed Settlement, IReadOnlyList<ArtifactRef> Artifacts);

/// <summary>Where a summary-only table came from: the harness label, the document's file name, the section and the SHA-256
/// of the document's bytes — the citation a summary row carries instead of findings.</summary>
public sealed record SummaryCitation(string Source, string Document, string DocumentSha256);

/// <summary>The database half of an import. An import is not a claim: it writes SETTLED cells of a FINISHED campaign in one
/// transaction each — the campaign (if it is new), the cell, its findings, its artefact refs.</summary>
public interface IGateImportStore
{
    Task<ImportedCellState> CellStateAsync(Guid cellId, CancellationToken cancellationToken);

    /// <summary>Refused when the cell exists already, when the campaign exists under another gate or suite, or when a ref's path
    /// is taken. Nothing of a refused cell is written.</summary>
    Task<Outcome<ImportedCell>> ImportCellAsync(ImportedCell cell, CancellationToken cancellationToken);

    /// <summary>Summary-only rows of one table, all or none; a row already stored (the same document hash, section, row and
    /// metric) is not stored again. Returns how many were new.</summary>
    Task<Outcome<int>> RecordSummaryAsync(SummaryCitation citation, GateKind gate, SummaryTable table, CancellationToken cancellationToken);
}
