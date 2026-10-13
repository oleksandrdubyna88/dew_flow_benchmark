using Bench.Domain.Scouts;

namespace Bench.Infrastructure.Persistence;

/// <summary>The public export's projection of the scout rows (plan §4 S4, §3.13): what the publication walk hands the guard and the export for
/// a PRIVATE task's rows, decided field by field by <see cref="ScoutPublication"/> — the one door — and applied here to the row shapes the EF
/// model maps. A private task's row keeps its label, privacy, language, kind, tree hash and its two commitments; its repository becomes its
/// label, its shas, statement, ground truth and removals empty. The rows of its cells lose their scouts' KEY FILES and their claims' paths.
/// A public task's rows pass through untouched. The walk works on COPIES: the database is never edited by an export.</summary>
internal static class ScoutRowPublication
{
    /// <summary>Which tasks (by run and id) are private, and which cells belong to them — read once per export from the rows themselves.</summary>
    public sealed record PrivateRows(IReadOnlySet<(Guid RunId, string TaskId)> Tasks, IReadOnlySet<Guid> Cells)
    {
        public static PrivateRows Of(IEnumerable<ScoutTaskRow> tasks, IEnumerable<ScoutCellRow> cells)
        {
            var privateTasks = tasks.Where(t => t.Privacy == ScoutTaskPrivacy.Private).Select(t => (t.RunId, t.TaskId)).ToHashSet();

            return new PrivateRows(privateTasks, cells.Where(c => privateTasks.Contains((c.RunId, c.TaskId))).Select(c => c.Id).ToHashSet());
        }

        public ScoutTaskPrivacy PrivacyOfCell(Guid cellId) => Cells.Contains(cellId) ? ScoutTaskPrivacy.Private : ScoutTaskPrivacy.Public;
    }

    /// <summary>The row as the export may carry it — the same object for a public row, a projected copy for a private task's.</summary>
    public static object Published(object row, PrivateRows privateRows) => row switch
    {
        ScoutTaskRow task when task.Privacy == ScoutTaskPrivacy.Private => Task(task),
        ScoutRecordRow record when privateRows.Cells.Contains(record.CellId) => Record(record),
        ScoutClaimRow claim when privateRows.Cells.Contains(claim.CellId) => Claim(claim),
        _ => row,
    };

    private static ScoutTaskRow Task(ScoutTaskRow row) => new()
    {
        Id = row.Id,
        RunId = row.RunId,
        TaskId = row.TaskId,
        Repository = row.TaskId,
        ParentSha = string.Empty,
        FixSha = string.Empty,
        Kind = row.Kind,
        Statement = string.Empty,
        GroundTruth = [],
        Removals = [],
        TreeHash = row.TreeHash,
        Privacy = row.Privacy,
        Language = row.Language,
        StatementCommitment = row.StatementCommitment,
        GroundTruthCommitment = row.GroundTruthCommitment,
    };

    private static ScoutRecordRow Record(ScoutRecordRow row) => new()
    {
        Id = row.Id,
        CellId = row.CellId,
        Ordinal = row.Ordinal,
        Focus = row.Focus,
        InputModel = row.InputModel,
        PromptSha256 = row.PromptSha256,
        ResolvedModel = row.ResolvedModel,
        DurationCaptured = row.DurationCaptured,
        DurationMs = row.DurationMs,
        InputTokens = row.InputTokens,
        OutputTokens = row.OutputTokens,
        CacheReadTokens = row.CacheReadTokens,
        CacheWrite5mTokens = row.CacheWrite5mTokens,
        CacheWrite1hTokens = row.CacheWrite1hTokens,
        OutputSeparable = row.OutputSeparable,
        CostCaptured = row.CostCaptured,
        CostUsd = row.CostUsd,
        KeyFiles = [.. ScoutPublication.Paths(ScoutTaskPrivacy.Private, row.KeyFiles)],
        KeyFilesUnparseable = row.KeyFilesUnparseable,
        KeyFilesExtra = row.KeyFilesExtra,
        Silent = row.Silent,
        ClaimsParsed = row.ClaimsParsed,
        ClaimsUnparseable = row.ClaimsUnparseable,
        ClaimsExtra = row.ClaimsExtra,
    };

    private static ScoutClaimRow Claim(ScoutClaimRow row) => new()
    {
        Id = row.Id,
        RunId = row.RunId,
        CellId = row.CellId,
        Ordinal = row.Ordinal,
        Index = row.Index,
        Path = ScoutPublication.Path(ScoutTaskPrivacy.Private, row.Path),
        LineCaptured = row.LineCaptured,
        Line = row.Line,
        StatementSha256 = row.StatementSha256,
        Verdict = row.Verdict,
        AssessorModel = row.AssessorModel,
        RubricSha256 = row.RubricSha256,
        AssessedAt = row.AssessedAt,
    };
}
