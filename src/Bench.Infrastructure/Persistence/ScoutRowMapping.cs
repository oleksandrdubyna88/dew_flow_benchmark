using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Persistence;

/// <summary>Domain ↔ row for the scout tables. A row is a fact about the past, so reading one back RE-PARSES what it holds — the pins,
/// the arms, the task ids, the shas, the task's shape and its leak check — and a hand-edited row is refused by name.</summary>
internal static class ScoutRowMapping
{
    public static ScoutRunRow ToRow(ScoutRun run) => new()
    {
        Id = run.Id,
        CreatedAt = run.CreatedAt,
        Seed = run.Seed,
        Repeats = run.Repeats,
        PinCliVersion = run.Pins.CliVersion,
        PinPluginCacheHash = run.Pins.PluginCacheHash,
        PinResolvedOpus = run.Pins.ResolvedOpus,
        PinResolvedSonnet = run.Pins.ResolvedSonnet,
        PinSettingsHash = run.Pins.SettingsHash,
        Arms = [.. run.Arms.Select(a => a.ToString())],
        TaskIds = [.. run.Tasks.Select(t => t.Value)],
        ArmCapsUsd = [.. run.Arms.Select(a => run.Budgets.Where(b => b.Arm == a).Select(b => b.CapUsd).DefaultIfEmpty(0m).First())],
        ArmWallSeconds = [.. run.Arms.Select(a => run.Budgets.Where(b => b.Arm == a).Select(b => b.WallSeconds).DefaultIfEmpty(0).First())],
        Kind = run.Kind,
        Campaign = run.Campaign,
        CeilingUsd = run.Ledger.CeilingUsd,
        ReservedUsd = run.Ledger.ReservedUsd,
        SettledUsd = run.Ledger.SettledUsd,
        UnknownUsd = run.Ledger.UnknownUsd,
    };

    public static Outcome<ScoutRun> ToDomain(ScoutRunRow row)
    {
        var unknownArm = row.Arms.FirstOrDefault(name => !Enum.TryParse<ScoutArmId>(name, ignoreCase: false, out var arm) || !Enum.IsDefined(arm));
        var tasks = row.TaskIds.Select(ScoutTaskId.Parse).ToList();
        var refusal = (unknownArm, tasks.FirstOrDefault(t => t is Outcome<ScoutTaskId>.Fail)) switch
        {
            ({ } name, _) => $"scout run {row.Id}: '{name}' is not a scout arm — the row was edited",
            (_, Outcome<ScoutTaskId>.Fail fail) => $"scout run {row.Id}: {fail.Reason}",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<ScoutRun>.Failure(refusal)
            : ScoutPins.Of(row.PinCliVersion, row.PinPluginCacheHash, row.PinResolvedOpus, row.PinResolvedSonnet, row.PinSettingsHash).Match(
                pins => Outcome<ScoutRun>.Success(new ScoutRun(
                    row.Id, row.CreatedAt, pins, row.Seed, row.Repeats,
                    [.. row.Arms.Select(Enum.Parse<ScoutArmId>)],
                    [.. tasks.Select(t => ((Outcome<ScoutTaskId>.Ok)t).Value)],
                    new SpendLedger(row.CeilingUsd, row.ReservedUsd, row.SettledUsd, row.UnknownUsd))
                { Budgets = Budgets(row), Kind = row.Kind, Campaign = row.Campaign }),
                reason => Outcome<ScoutRun>.Failure($"scout run {row.Id}: {reason}"));
    }

    /// <summary>The arms' budgets, read back from the two parallel arrays; an arm stored without a positive cap and wall has none — a cell of
    /// it is then refused rather than launched under a default.</summary>
    private static IReadOnlyList<ScoutArmBudget> Budgets(ScoutRunRow row) =>
        row.ArmCapsUsd.Count != row.Arms.Count || row.ArmWallSeconds.Count != row.Arms.Count
            ? []
            : [.. row.Arms.Select((arm, i) => ScoutArmBudget.Of(Enum.Parse<ScoutArmId>(arm), row.ArmCapsUsd[i], row.ArmWallSeconds[i]))
                .OfType<Outcome<ScoutArmBudget>.Ok>().Select(ok => ok.Value)];

    public static ScoutTaskRow ToRow(Guid runId, ScoutTask task) => new()
    {
        Id = Guid.CreateVersion7(),
        RunId = runId,
        TaskId = task.Id.Value,
        Repository = task.Repository,
        ParentSha = task.Parent.Value,
        FixSha = task.Fix.Value,
        Kind = task.Kind,
        Statement = task.Statement,
        GroundTruth = [.. task.GroundTruth],
        Removals = [.. task.Removals],
        TreeHash = task.TreeHash,
        Privacy = task.Privacy,
        Language = task.Language,
        StatementCommitment = task.Commitments.Statement,
        GroundTruthCommitment = task.Commitments.GroundTruth,
    };

    /// <summary>The task read back through <see cref="ScoutTask.Frozen"/> again — its shape and its leak check against G (the diff's added
    /// identifiers are not stored, so that half of the check ran once, at freezing) — with its privacy, language and commitments.</summary>
    public static Outcome<ScoutTask> ToDomain(ScoutTaskRow row) =>
        ScoutTaskId.Parse(row.TaskId).Match(
            id => CommitSha.Parse(row.ParentSha).Match(
                parent => CommitSha.Parse(row.FixSha).Match(
                    fix => ScoutTask.Frozen(id, row.Repository, parent, fix, row.Kind, row.Statement, row.GroundTruth, [], row.Removals, row.TreeHash, row.Privacy, row.Language)
                        .Match(task => Outcome<ScoutTask>.Success(task with { Commitments = new ScoutTaskCommitments(row.StatementCommitment, row.GroundTruthCommitment) }), Outcome<ScoutTask>.Failure),
                    Outcome<ScoutTask>.Failure),
                Outcome<ScoutTask>.Failure),
            Outcome<ScoutTask>.Failure);

    public static ScoutCellRow ToRow(ScoutCell cell)
    {
        var row = new ScoutCellRow
        {
            Id = cell.Id,
            RunId = cell.RunId,
            TaskId = cell.Task.Value,
            Arm = cell.Arm,
            Repeat = cell.Repeat,
            Generation = cell.Generation,
            Slot = cell.Slot,
            Position = cell.Position,
            State = cell.State,
            Attempts = cell.Attempts,
            Owner = cell.Owner.Label,
            OwnerHost = cell.Owner.Host,
            OwnerPid = cell.Owner.Pid,
            ClaimedAt = cell.Claim.ClaimedAt,
            PinBinarySha256 = cell.Pin.BinarySha256,
            PinVersionText = cell.Pin.VersionText,
            PinGitSha = cell.Pin.GitSha,
            PinDirtyCaptured = cell.Pin.DirtyFiles.WasCaptured,
            PinDirtyFiles = cell.Pin.DirtyFiles.Value,
            PinCheckedTree = cell.Pin.CheckedTree,
        };

        Apply(row, new ScoutSettlement(cell.Outcome, cell.Reason, cell.Facts));
        return row;
    }

    /// <summary>The settlement's columns on a row — the half of a settle that is not the guarded state change, and the same code the
    /// planner's row goes through, so one column list exists.</summary>
    public static void Apply(ScoutCellRow row, ScoutSettlement settlement)
    {
        var facts = settlement.Facts;
        row.Outcome = settlement.Outcome;
        row.Reason = settlement.Reason;
        (row.RecallFoundCaptured, row.RecallFound) = (facts.RecallFound.WasCaptured, facts.RecallFound.Value);
        (row.RecallTotalCaptured, row.RecallTotal) = (facts.RecallTotal.WasCaptured, facts.RecallTotal.Value);
        (row.TeamMsCaptured, row.TeamMs) = (facts.TeamMs.WasCaptured, facts.TeamMs.Value);
        (row.SessionMsCaptured, row.SessionMs) = (facts.SessionMs.WasCaptured, facts.SessionMs.Value);
        (row.ComputedUsdCaptured, row.ComputedUsd) = (facts.ComputedUsd.WasCaptured, facts.ComputedUsd.Value);
        (row.ReportedUsdCaptured, row.ReportedUsd) = (facts.ReportedUsd.WasCaptured, facts.ReportedUsd.Value);
        var team = facts.TeamTokens.Value;
        (row.TeamTokensCaptured, row.TeamInputTokens, row.TeamOutputTokens) = (facts.TeamTokens.WasCaptured, team.Input, team.Output);
        (row.TeamCacheReadTokens, row.TeamCacheWrite5mTokens, row.TeamCacheWrite1hTokens) = (team.CacheRead, team.CacheWrite5m, team.CacheWrite1h);
        (row.TeamUsdCaptured, row.TeamUsd) = (facts.TeamUsd.WasCaptured, facts.TeamUsd.Value);
    }

    public static Outcome<ScoutCell> ToDomain(ScoutCellRow row) =>
        ScoutTaskId.Parse(row.TaskId).Match(
            task => Outcome<ScoutCell>.Success(new ScoutCell(
                row.Id,
                row.RunId,
                task,
                row.Arm,
                row.Repeat,
                row.Generation,
                row.Slot,
                row.Position,
                Claimable.Stored(row.State, row.Attempts, WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid), row.ClaimedAt),
                GateRowMapping.Pin(row.PinBinarySha256, row.PinVersionText, row.PinGitSha, row.PinDirtyCaptured, row.PinDirtyFiles, row.PinCheckedTree),
                row.Outcome,
                row.Reason,
                Facts(row))),
            reason => Outcome<ScoutCell>.Failure($"scout cell {row.Id}: {reason}"));

    /// <summary>A cell nobody settled reads back as exactly <see cref="ScoutCellFacts.None"/>; a settled one with the stored reason.</summary>
    private static ScoutCellFacts Facts(ScoutCellRow row) => row.Outcome == ScoutCellOutcome.None ? ScoutCellFacts.None : new(
        Count(row.RecallFoundCaptured, row.RecallFound),
        Count(row.RecallTotalCaptured, row.RecallTotal),
        Count(row.TeamMsCaptured, row.TeamMs),
        Count(row.SessionMsCaptured, row.SessionMs),
        Usd(row.ComputedUsdCaptured, row.ComputedUsd),
        Usd(row.ReportedUsdCaptured, row.ReportedUsd))
    {
        TeamTokens = row.TeamTokensCaptured
            ? CapturedTokens.Of(new TokenUsage(row.TeamInputTokens, row.TeamOutputTokens, row.TeamCacheReadTokens, row.TeamCacheWrite5mTokens, row.TeamCacheWrite1hTokens))
            : CapturedTokens.NotCaptured,
        TeamUsd = row.TeamUsdCaptured ? CapturedUsd.Amount(row.TeamUsd) : ScoutCellFacts.TeamUsdNotCaptured,
    };

    public static ScoutClaimRow ToRow(ScoutAssessedClaim claim) => new()
    {
        Id = Guid.CreateVersion7(),
        RunId = claim.RunId,
        CellId = claim.Key.CellId,
        Ordinal = claim.Key.Ordinal,
        Index = claim.Key.Index,
        Path = claim.Path,
        LineCaptured = claim.Line.WasCaptured,
        Line = claim.Line.Value,
        StatementSha256 = claim.StatementSha256,
        Verdict = claim.Verdict,
        AssessorModel = claim.AssessorModel,
        RubricSha256 = claim.RubricSha256,
        AssessedAt = claim.AssessedAt,
    };

    public static ScoutAssessedClaim ToDomain(ScoutClaimRow row) => new(
        new ScoutClaimKey(row.CellId, row.Ordinal, row.Index), row.RunId, row.Path, ScoutAssessment.Line(row.LineCaptured, row.Line),
        row.StatementSha256, row.Verdict, row.AssessorModel, row.RubricSha256, row.AssessedAt);

    public static ScoutRecordRow ToRow(Guid cellId, ScoutRecord scout) => new()
    {
        Id = Guid.CreateVersion7(),
        CellId = cellId,
        Ordinal = scout.Ordinal,
        Focus = scout.Focus,
        InputModel = scout.InputModel,
        PromptSha256 = scout.PromptSha256,
        ResolvedModel = scout.ResolvedModel,
        DurationCaptured = scout.DurationMs.WasCaptured,
        DurationMs = scout.DurationMs.Value,
        InputTokens = scout.Tokens.Input,
        OutputTokens = scout.Tokens.Output,
        CacheReadTokens = scout.Tokens.CacheRead,
        CacheWrite5mTokens = scout.Tokens.CacheWrite5m,
        CacheWrite1hTokens = scout.Tokens.CacheWrite1h,
        OutputSeparable = scout.OutputSeparable,
        CostCaptured = scout.CostUsd.WasCaptured,
        CostUsd = scout.CostUsd.Value,
        KeyFiles = [.. scout.KeyFiles],
        KeyFilesUnparseable = scout.KeyFilesUnparseable,
        KeyFilesExtra = scout.KeyFilesExtra,
        Silent = scout.Silent,
        ClaimsParsed = scout.ClaimsParsed,
        ClaimsUnparseable = scout.ClaimsUnparseable,
        ClaimsExtra = scout.ClaimsExtra,
    };

    public static ScoutRecord ToDomain(ScoutRecordRow row) => new(
        row.Ordinal,
        row.Focus,
        row.InputModel,
        row.PromptSha256,
        row.ResolvedModel,
        Count(row.DurationCaptured, row.DurationMs),
        new TokenUsage(row.InputTokens, row.OutputTokens, row.CacheReadTokens, row.CacheWrite5mTokens, row.CacheWrite1hTokens),
        row.OutputSeparable,
        Usd(row.CostCaptured, row.CostUsd),
        [.. row.KeyFiles],
        row.KeyFilesUnparseable,
        row.KeyFilesExtra,
        row.Silent,
        row.ClaimsParsed,
        row.ClaimsUnparseable,
        row.ClaimsExtra);

    public static ScoutAttemptRow ToRow(ScoutAttempt attempt) => new()
    {
        Id = attempt.Id,
        RunId = attempt.RunId,
        CellId = attempt.CellId,
        Number = attempt.Number,
        ReservedUsd = attempt.ReservedUsd,
        State = attempt.State,
        SettledUsd = attempt.SettledUsd,
        ReservedAt = attempt.ReservedAt,
    };

    public static ScoutAttempt ToDomain(ScoutAttemptRow row) =>
        new(row.Id, row.RunId, row.CellId, row.Number, row.ReservedUsd, row.State, row.SettledUsd, row.ReservedAt);

    private static CapturedCount Count(bool captured, long value) => captured ? CapturedCount.Number(value) : CapturedCount.Unavailable(GateRowMapping.StoredNotCaptured);

    private static CapturedUsd Usd(bool captured, decimal value) => captured ? CapturedUsd.Amount(value) : CapturedUsd.Unavailable(GateRowMapping.StoredNotCaptured);
}
