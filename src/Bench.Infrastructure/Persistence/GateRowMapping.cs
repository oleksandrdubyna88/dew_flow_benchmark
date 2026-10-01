using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Persistence;

/// <summary>Domain ↔ row for the gate tables. A row is a fact about the past, so reading one back re-parses the
/// ids rather than trusting them: a row edited by hand is refused by name instead of served.</summary>
internal static class GateRowMapping
{
    /// <summary>The reason a count stored as <i>not captured</i> reads back with. The original reason was a
    /// harness-authored sentence; it is not stored, because every free-text column is a column the publication
    /// guard has to trust, and the flag is the fact that matters.</summary>
    public const string StoredNotCaptured = "not captured";

    public static GateRunRow ToRow(GateRun run) => new()
    {
        Id = run.Id,
        Gate = run.Gate,
        SuiteStamp = run.SuiteStamp,
        DataDirMode = run.Mode,
        Status = run.Status,
        Source = run.Source.Label,
        CreatedAt = run.CreatedAt,
        PredictionHash = run.PredictionHash,
        AllowProductChange = run.AllowProductChange,
    };

    public static GateRun ToDomain(GateRunRow row) =>
        new(row.Id, row.Gate, row.SuiteStamp, row.DataDirMode, row.Status, Source(row.Source), row.CreatedAt)
        {
            PredictionHash = row.PredictionHash,
            AllowProductChange = row.AllowProductChange,
        };

    public static GateCellRow ToRow(GateCell cell) => new()
    {
        Id = cell.Id,
        RunId = cell.RunId,
        TaskId = cell.Task.Value,
        ReviewerId = cell.Reviewer.Value,
        Repeat = cell.Repeat,
        Slot = cell.Slot,
        Position = cell.Position,
        State = cell.State,
        Attempts = cell.Attempts,
        Owner = cell.Owner.Label,
        OwnerHost = cell.Owner.Host,
        OwnerPid = cell.Owner.Pid,
        ClaimedAt = cell.ClaimedAt,
        PinBinarySha256 = cell.Pin.BinarySha256,
        PinVersionText = cell.Pin.VersionText,
        PinGitSha = cell.Pin.GitSha,
        PinDirtyCaptured = cell.Pin.DirtyFiles.WasCaptured,
        PinDirtyFiles = cell.Pin.DirtyFiles.Value,
        PinCheckedTree = cell.Pin.CheckedTree,
        OutcomeKind = cell.OutcomeKind,
        FailureText = cell.OutcomeDetail,
    };

    public static Outcome<GateCell> ToDomain(GateCellRow row) =>
        GateTaskId.Parse(row.TaskId).Match(
            task => GateReviewerId.Parse(row.ReviewerId).Match(
                reviewer => Outcome<GateCell>.Success(new GateCell(
                    row.Id,
                    row.RunId,
                    task,
                    reviewer,
                    row.Repeat,
                    row.Slot,
                    row.Position,
                    Claimable.Stored(row.State, row.Attempts, WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid), row.ClaimedAt),
                    Pin(row),
                    row.OutcomeKind,
                    row.OutcomeKind == GateCellOutcomeKind.Failed || row.State == CellState.Pending ? row.FailureText : string.Empty)),
                Outcome<GateCell>.Failure),
            Outcome<GateCell>.Failure);

    public static ProductPin Pin(GateCellRow row) =>
        Pin(row.PinBinarySha256, row.PinVersionText, row.PinGitSha, row.PinDirtyCaptured, row.PinDirtyFiles, row.PinCheckedTree);

    /// <summary>The pin read back from its six columns — shared by the gate cell and the probe cell, which carry the same six,
    /// so "which bytes answered" is read one way wherever a cell is pinned.</summary>
    public static ProductPin Pin(string binarySha256, string versionText, string gitSha, bool dirtyCaptured, long dirtyFiles, string checkedTree)
    {
        var dirty = dirtyCaptured ? CapturedCount.Number(dirtyFiles) : CapturedCount.Unavailable(StoredNotCaptured);

        var pin = (binarySha256.Length, versionText.StartsWith(ProductPin.ImportedPrefix, StringComparison.Ordinal)) switch
        {
            ( > 0, _) => ProductPin.Hashed(binarySha256, versionText, gitSha, dirty, checkedTree),
            (0, true) => ProductPin.ImportedStored(versionText, gitSha, dirty),
            _ => Outcome<ProductPin>.Success(ProductPin.None),
        };

        return pin.Match(p => p, _ => ProductPin.None);
    }

    public static GateArtifactRow ToRow(ArtifactRef artifact, DateTimeOffset now) => new()
    {
        RunId = artifact.RunId,
        CellId = artifact.CellId,
        Attempt = artifact.Attempt,
        Class = artifact.Class,
        RelativePath = artifact.Path.Value,
        Sha256 = artifact.Sha256,
        Length = artifact.Length,
        RecordedAt = now,
    };

    public static Outcome<ArtifactRef> ToDomain(GateArtifactRow row) =>
        ArtifactRef.Stored(row.RunId, row.CellId, row.Attempt, row.Class, row.RelativePath, row.Sha256, row.Length);

    public static GateFindingRow ToRow(Guid cellId, int attempt, GateFinding finding) => new()
    {
        CellId = cellId,
        Attempt = attempt,
        Ordinal = finding.Ordinal,
        Severity = finding.Severity,
        Category = finding.Category,
        IsGating = finding.IsGating,
        Line = finding.Line,
        TextHash = finding.TextHash,
        FileHash = finding.FileHash,
    };

    public static Outcome<GateFinding> ToDomain(GateFindingRow row) =>
        GateFinding.Stored(row.Ordinal, row.Severity, row.Category, row.IsGating, row.Line, row.TextHash, row.FileHash);

    private static RunSource Source(string label) =>
        string.Equals(label, new RunSource.Native().Label, StringComparison.Ordinal) || label.Length == 0
            ? new RunSource.Native()
            : new RunSource.Imported(label);
}
