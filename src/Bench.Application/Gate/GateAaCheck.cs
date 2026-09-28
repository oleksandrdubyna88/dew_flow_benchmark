using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>What one cell of the run was found to be, against the reference cell's turn-1 prompt.</summary>
public enum AaVerdict
{
    /// <summary>Its normalised prompt is the reference's.</summary>
    SameShape,

    /// <summary>Its normalised prompt differs — a port defect, or a run-specific field the product added.</summary>
    Differs,

    /// <summary>Its prompt carries two session ids or two history lines, so no shape can be claimed for it.</summary>
    Ambiguous,

    /// <summary>An API reviewer's completed session committed no prompt: the capture broke.</summary>
    ApiLeftNoPrompt,

    /// <summary>A CLI reviewer, which by design leaves no prompt file.</summary>
    CliNoPrompt,

    /// <summary>The session failed; there is no input to compare and no port defect to infer.</summary>
    SessionFailed,

    /// <summary>The reference cell itself, when it belongs to the run — never compared with itself.</summary>
    Reference,

    /// <summary>Another task's definition — its prompt differs by construction.</summary>
    OtherTask,

    /// <summary>Another product commit — its prompt may legitimately differ.</summary>
    OtherPin,

    /// <summary>A prompt is recorded but the store refused it on reading.</summary>
    Unreadable,

    /// <summary>The file read is not the one the settlement hashed: this lookup and the runner (or the import) disagree.</summary>
    HashMismatch,
}

/// <summary>One cell's line. Ids and hash prefixes only — a prompt's text is never carried.</summary>
public sealed record AaLine(Guid Cell, GateReviewerId Reviewer, GateTaskId Task, int Repeat, string RawHash, string ShapeHash, AaVerdict Verdict, string Detail)
{
    public bool Compared => Verdict is AaVerdict.SameShape or AaVerdict.Differs or AaVerdict.Ambiguous;
}

/// <summary>How the check ended, in the order an exit code is chosen: an unreadable artefact first, then any failure, then
/// a check that compared nothing — never a vacuous pass.</summary>
public enum AaStanding
{
    Pass,
    Failed,
    Unreadable,
    NothingCompared,
}

/// <summary>Why the check was refused before any cell was read — the kind decides the exit code.</summary>
public enum AaRefusalKind
{
    /// <summary>Asked wrongly: an unknown run or cell, a gate or suite mismatch, a reference without a prompt.</summary>
    Asked,

    /// <summary>The reference's prompt could not be read or no longer verifies.</summary>
    Environment,

    /// <summary>The run is not finished, or some cells never settled.</summary>
    Unsettled,
}

public abstract record AaResult
{
    private AaResult()
    {
    }

    public sealed record Refused(AaRefusalKind Kind, string Reason) : AaResult;

    public sealed record Checked(string ReferenceShape, IReadOnlyList<AaLine> Lines) : AaResult
    {
        public int ComparedCount => Lines.Count(l => l.Compared);

        public AaStanding Standing => (
            Lines.Any(l => l.Verdict is AaVerdict.Unreadable or AaVerdict.HashMismatch),
            Lines.Any(l => l.Verdict is AaVerdict.Differs or AaVerdict.Ambiguous or AaVerdict.ApiLeftNoPrompt),
            ComparedCount) switch
        {
            (true, _, _) => AaStanding.Unreadable,
            (_, true, _) => AaStanding.Failed,
            (_, _, 0) => AaStanding.NothingCompared,
            _ => AaStanding.Pass,
        };
    }
}

/// <summary>The prompt hash a cell's settlement stored — the runner's <c>GateCellRunner.PromptHash</c> or the import's
/// <c>ImportedFiles.TurnOnePromptHash</c>; empty when none was.</summary>
public interface IGatePromptHashes
{
    Task<string> PromptHashAsync(Guid cellId, CancellationToken cancellationToken);
}

/// <summary>The A/A (E7, S7.2a): every cell of a FINISHED run against one reference cell's turn-1 prompt, compared by
/// <see cref="PromptShape"/> — so the per-run session id and data directory are not a difference and everything else is.
/// Cells the reference cannot speak for (another task's definition, another product commit, a failed session, a CLI
/// reviewer with no prompt by design) are listed and not compared, and a check that compared nothing is said to have
/// compared nothing. Each read is checked against the hash the settlement stored, so the three places that name "the
/// turn-1 prompt" are proven to agree on every cell they are asked about. Nothing is stored.</summary>
public sealed class GateAaCheck(IGateStore store, IGateArtifactStore artifacts, IGatePromptHashes hashes, IGateReviewerCatalog catalog)
{
    public async Task<AaResult> RunAsync(Guid runId, Guid referenceCell, IReadOnlyList<GateSuite> suites, CancellationToken cancellationToken) =>
        await LoadAsync(runId, referenceCell, suites, cancellationToken) switch
        {
            Step<Loaded>.Stop stop => stop.Refused,
            Step<Loaded>.Go go => await CheckAsync(go.Value, cancellationToken),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private async Task<AaResult> CheckAsync(Loaded context, CancellationToken cancellationToken) =>
        await ReferenceAsync(context, cancellationToken) switch
        {
            Step<PromptShape>.Stop stop => stop.Refused,
            Step<PromptShape>.Go go => new AaResult.Checked(go.Value.Sha256, await LinesAsync(context, go.Value, cancellationToken)),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private async Task<Step<Loaded>> LoadAsync(Guid runId, Guid referenceCell, IReadOnlyList<GateSuite> suites, CancellationToken cancellationToken)
    {
        var run = await store.LoadAsync(runId, cancellationToken);
        var cell = await store.CellAsync(referenceCell, cancellationToken);

        return (run, cell) switch
        {
            (Outcome<GateRun>.Fail, _) => Step<Loaded>.Refuse(AaRefusalKind.Asked, $"there is no gate run {runId}"),
            (_, Outcome<GateCell>.Fail) => Step<Loaded>.Refuse(AaRefusalKind.Asked, $"there is no gate cell {referenceCell}"),
            (Outcome<GateRun>.Ok r, Outcome<GateCell>.Ok c) => await ReferenceRunAsync(r.Value, c.Value, suites, cancellationToken),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private async Task<Step<Loaded>> ReferenceRunAsync(GateRun run, GateCell refCell, IReadOnlyList<GateSuite> suites, CancellationToken cancellationToken) =>
        await store.LoadAsync(refCell.RunId, cancellationToken) switch
        {
            Outcome<GateRun>.Ok refRun => await ContextAsync(run, refRun.Value, refCell, suites, cancellationToken),
            Outcome<GateRun>.Fail fail => Step<Loaded>.Refuse(AaRefusalKind.Asked, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private async Task<Step<Loaded>> ContextAsync(GateRun run, GateRun refRun, GateCell refCell, IReadOnlyList<GateSuite> suites, CancellationToken cancellationToken)
    {
        var cells = await store.CellsAsync(run.Id, cancellationToken);
        var covering = new[] { run.SuiteStamp, refRun.SuiteStamp }.Select(stamp => suites.Where(s => s.Stamp == stamp).ToList()).ToList();
        var refusal = Precondition(run, refRun, cells, covering.All(found => found.Count > 0));

        if (refusal.Reason.Length > 0)
        {
            return new Step<Loaded>.Stop(refusal);
        }

        var ids = cells.Select(c => c.Reviewer).Append(refCell.Reviewer).Distinct().ToList();
        var facts = await store.FactsAsync(run.Id, cancellationToken);

        return (await catalog.GetAsync(ids, cancellationToken)).Match<Step<Loaded>>(
            reviewers => new Step<Loaded>.Go(new Loaded(run, refRun, refCell, cells, facts, covering[0][0], covering[1][0], reviewers.ToDictionary(r => r.Id))),
            reason => Step<Loaded>.Refuse(AaRefusalKind.Asked, reason));
    }

    /// <summary>What must hold before a cell is read, in the order a person fixes it; an empty reason is "all of it holds".</summary>
    private static AaResult.Refused Precondition(GateRun run, GateRun refRun, IReadOnlyList<GateCell> cells, bool suitesCovered)
    {
        var open = cells.Where(c => !c.IsTerminal).Select(c => c.Id.ToString()).ToList();

        return (run.Gate == refRun.Gate, suitesCovered, open.Count, run.Status == GateRunStatus.Finished) switch
        {
            (false, _, _, _) => new(AaRefusalKind.Asked, $"the run is a {Word(run.Gate)} run and the reference cell's a {Word(refRun.Gate)} one — turn-1 prompts of different gates differ by construction"),
            (_, false, _, _) => new(AaRefusalKind.Asked, $"pass the suite files of both stamps — the run's {run.SuiteStamp} and the reference's {refRun.SuiteStamp} — with --suite-file"),
            (_, _, > 0, _) => new(AaRefusalKind.Unsettled, $"the run has {open.Count} unsettled cell(s): {string.Join(", ", open)} — resume it before comparing"),
            (_, _, _, false) => new(AaRefusalKind.Unsettled, $"the run is {run.Status}, not Finished — resume it before comparing"),
            _ => new(AaRefusalKind.Asked, string.Empty),
        };
    }

    private static string Word(GateKind gate) => gate.ToString().ToLowerInvariant();

    private async Task<Step<PromptShape>> ReferenceAsync(Loaded context, CancellationToken cancellationToken)
    {
        var committed = await store.ArtifactsAsync(context.RefRun.Id, cancellationToken);
        var attempt = SettledAttempt(await store.FactsAsync(context.RefRun.Id, cancellationToken), context.RefCell);
        var prompt = await GateTurnOnePrompt.ReadAsync(artifacts, committed, context.RefCell.Id, attempt, cancellationToken);
        var stored = await hashes.PromptHashAsync(context.RefCell.Id, cancellationToken);

        return prompt switch
        {
            TurnOnePrompt.Present p when p.Sha256 == stored => Usable(PromptShape.Of(p.Text), context.RefCell.Id),
            TurnOnePrompt.Present => Step<PromptShape>.Refuse(AaRefusalKind.Environment, $"the reference cell's prompt file is not the one its settlement hashed ({Short(stored)})"),
            TurnOnePrompt.Unreadable => Step<PromptShape>.Refuse(AaRefusalKind.Environment, $"the reference cell's prompt cannot be read — {GoneOrChanged}"),
            _ => Step<PromptShape>.Refuse(AaRefusalKind.Asked, $"the reference cell {context.RefCell.Id} has no turn-1 prompt — pick a cell of an API reviewer"),
        };
    }

    /// <summary>An ambiguous reference is refused, not compared: every cell would read "ambiguous" at the REFERENCE's lines
    /// and the run would be blamed for a defect of the one cell it was measured against.</summary>
    private static Step<PromptShape> Usable(PromptShape shape, Guid cell) =>
        shape.AmbiguousLines.Count == 0
            ? new Step<PromptShape>.Go(shape)
            : Step<PromptShape>.Refuse(AaRefusalKind.Asked,
                $"the reference cell {cell}'s prompt carries two session ids or two history lines (lines {string.Join(",", shape.AmbiguousLines)}) — pick another cell");

    /// <summary>What an unreadable prompt is said as. The store's own reason names the artefact's path, and this check prints no path.</summary>
    private const string GoneOrChanged = "the file is gone or changed since it was committed (bench gate status names the attempt)";

    private async Task<IReadOnlyList<AaLine>> LinesAsync(Loaded context, PromptShape reference, CancellationToken cancellationToken)
    {
        var committed = await store.ArtifactsAsync(context.Run.Id, cancellationToken);
        var lines = new List<AaLine>();

        foreach (var cell in context.Cells.OrderBy(c => c.Slot).ThenBy(c => c.Position))
        {
            lines.Add(await LineAsync(context, reference, committed, cell, cancellationToken));
        }

        return lines;
    }

    private async Task<AaLine> LineAsync(Loaded context, PromptShape reference, IReadOnlyList<ArtifactRef> committed, GateCell cell, CancellationToken cancellationToken)
    {
        var standing = Standing(context, cell);
        if (standing is not AaVerdict.SameShape)
        {
            return Line(cell, string.Empty, string.Empty, standing, string.Empty);
        }

        var prompt = await GateTurnOnePrompt.ReadAsync(artifacts, committed, cell.Id, SettledAttempt(context.Facts, cell), cancellationToken);
        var stored = await hashes.PromptHashAsync(cell.Id, cancellationToken);
        var cli = context.Reviewers[cell.Reviewer].Definition.Runtime.IsCli();

        return Compare(cell, prompt, stored, reference, cli, Dirty(context.RefCell.Pin, cell.Pin));
    }

    /// <summary>Whether the reference can speak for this cell at all — <see cref="AaVerdict.SameShape"/> meaning "read it".</summary>
    /// <summary>Whether the reference can speak for this cell at all — <see cref="AaVerdict.SameShape"/> meaning "read it". The
    /// reference itself comes first: compared with itself it would always agree, and one such line could make a pass.</summary>
    private static AaVerdict Standing(Loaded context, GateCell cell) =>
        (cell.Id == context.RefCell.Id,
            SameDefinition(Definition(context.Suite, cell.Task), Definition(context.RefSuite, context.RefCell.Task)),
            SamePin(cell.Pin, context.RefCell.Pin),
            cell.OutcomeKind) switch
        {
            (true, _, _, _) => AaVerdict.Reference,
            (_, false, _, _) => AaVerdict.OtherTask,
            (_, _, false, _) => AaVerdict.OtherPin,
            (_, _, _, GateCellOutcomeKind.Failed) => AaVerdict.SessionFailed,
            _ => AaVerdict.SameShape,
        };

    /// <summary>A stored hash with NO file behind it is the same disagreement as a file with another hash: the runner or the
    /// import hashed a prompt this lookup did not find.</summary>
    private static AaLine Compare(GateCell cell, TurnOnePrompt prompt, string stored, PromptShape reference, bool cli, string dirty) => prompt switch
    {
        TurnOnePrompt.Present p when p.Sha256 != stored => Line(cell, p.Sha256, string.Empty, AaVerdict.HashMismatch, $"the settlement hashed {Short(stored)}"),
        TurnOnePrompt.Present p => Shaped(cell, p.Sha256, PromptShape.Of(p.Text), reference, dirty),
        TurnOnePrompt.Unreadable => Line(cell, string.Empty, string.Empty, AaVerdict.Unreadable, GoneOrChanged),
        _ when stored.Length > 0 => Line(cell, string.Empty, string.Empty, AaVerdict.HashMismatch, $"the settlement hashed {Short(stored)} and no prompt file is committed"),
        _ => Line(cell, string.Empty, string.Empty, cli ? AaVerdict.CliNoPrompt : AaVerdict.ApiLeftNoPrompt, string.Empty),
    };

    private static AaLine Shaped(GateCell cell, string raw, PromptShape shape, PromptShape reference, string dirty) => PromptShape.Compare(reference, shape) switch
    {
        PromptComparison.Same => Line(cell, raw, shape.Sha256, AaVerdict.SameShape, dirty),
        PromptComparison.Differs d => Line(cell, raw, shape.Sha256, AaVerdict.Differs, Join($"at lines {Positions(d.Lines, d.Count)} ({d.Count})", dirty)),
        PromptComparison.Ambiguous a => Line(cell, raw, shape.Sha256, AaVerdict.Ambiguous, Join($"at lines {Positions(a.Lines, a.Lines.Count)}", dirty)),
        _ => throw new InvalidOperationException("unreachable"),
    };

    private static string Positions(IReadOnlyList<int> listed, int count) => string.Join(",", listed) + (count > listed.Count ? ",…" : string.Empty);

    private static AaLine Line(GateCell cell, string raw, string shape, AaVerdict verdict, string detail) =>
        new(cell.Id, cell.Reviewer, cell.Task, cell.Repeat, Short(raw), Short(shape), verdict, detail);

    private static string Join(string first, string second) => second.Length == 0 ? first : $"{first}; {second}";

    /// <summary>A differing dirty count is COMPARED, not skipped — the calibration's runs at one commit carry 0 or 1 dirty
    /// files and one shape — and said beside the verdict, so a difference there has its likely cause next to it.</summary>
    private static string Dirty(ProductPin reference, ProductPin cell) =>
        reference.DirtyFiles == cell.DirtyFiles ? string.Empty : $"{cell.DirtyText} against the reference's {reference.DirtyText}";

    /// <summary>The same product: the same hashed bytes (<see cref="ProductPin.Matches"/>), or one commit named at two lengths
    /// — a native pin records <c>git rev-parse --short</c>, an imported one what the other harness kept (8 characters), so
    /// the shorter being a prefix of the longer, both at least 7, is one commit.</summary>
    private static bool SamePin(ProductPin cell, ProductPin reference) =>
        cell.Matches(reference)
        || (Math.Min(cell.GitSha.Length, reference.GitSha.Length) >= 7
            && (cell.GitSha.StartsWith(reference.GitSha, StringComparison.OrdinalIgnoreCase) || reference.GitSha.StartsWith(cell.GitSha, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The task as its suite DEFINES it — id, base, variant head, plan path; an id the suite lacks defines nothing.</summary>
    private static IReadOnlyList<string> Definition(GateSuite suite, GateTaskId id) =>
        [.. suite.Tasks.Where(t => t.Id == id).Take(1).SelectMany(t => new[] { t.Id.Value, t.Case.Base.Value, t.Case.VariantHead.Value, t.Case.PlanPath })];

    private static bool SameDefinition(IReadOnlyList<string> first, IReadOnlyList<string> second) => first.Count > 0 && first.SequenceEqual(second, StringComparer.Ordinal);

    private static int SettledAttempt(IReadOnlyList<GateRunRecord> facts, GateCell cell) =>
        facts.Where(r => r.RunId == cell.Id).Select(r => r.Attempt).DefaultIfEmpty(cell.Attempts).First();

    private static string Short(string sha) => sha.Length > 12 ? sha[..12] : sha;

    /// <summary>A loading step either goes on with its value or stops with the refusal the caller answers with.</summary>
    private abstract record Step<T>
    {
        private Step()
        {
        }

        public static Step<T> Refuse(AaRefusalKind kind, string reason) => new Stop(new AaResult.Refused(kind, reason));

        public sealed record Go(T Value) : Step<T>;

        public sealed record Stop(AaResult.Refused Refused) : Step<T>;
    }

    private sealed record Loaded(
        GateRun Run, GateRun RefRun, GateCell RefCell, IReadOnlyList<GateCell> Cells, IReadOnlyList<GateRunRecord> Facts, GateSuite Suite, GateSuite RefSuite,
        IReadOnlyDictionary<GateReviewerId, GateReviewer> Reviewers);
}
