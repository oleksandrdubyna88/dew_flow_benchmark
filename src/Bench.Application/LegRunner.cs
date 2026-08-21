using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Domain.Suites;
using Bench.Domain.Variants;
using Microsoft.Extensions.Logging;

namespace Bench.Application;

/// <param name="Suite">The frozen suite this run measures. Questions are found in it by the id on the cell.</param>
/// <param name="Subjects">Every subject of this run, and where each one's legs are sent. A LIST, because a
/// run has always been able to plan several while the runner held one endpoint — which would have sent
/// every leg to the first model and labelled the results with the cell's subject.</param>
/// <param name="Budgets">Only ceilings a runtime has ACCEPTED belong here.</param>
public sealed record LegPlan(
    Suite Suite,
    SubjectRoster Subjects,
    IReadOnlyList<Budget> Budgets,
    TaskKind Kind)
{
    /// <summary>Every retrieval recipe this run measures, looked up per cell.
    /// <para>
    /// <see cref="VariantRoster.Baseline"/> by default and an <c>init</c> property rather than a positional
    /// parameter, so the retrieval lane is additive: a run planned without the catalog resolves every cell to
    /// the control arm and behaves exactly as it did before this axis existed.
    /// </para></summary>
    public VariantRoster Variants { get; init; } = VariantRoster.Baseline;

    /// <summary>How much of one hit's text reaches the prompt. Configuration rather than a constant, because
    /// it changes what the subject read and therefore belongs to the run's record.</summary>
    public RagPromptLimits Prompt { get; init; } = RagPromptLimits.Default;

    /// <summary>Every tool lane this run measures, looked up per cell by the name the cell already carries.
    /// <para>
    /// <see cref="LaneRoster.Floor"/> by default and an <c>init</c> property, exactly like
    /// <see cref="Variants"/> above and for the same reason: the tool lane is additive. A run planned
    /// without the lane catalog resolves every cell to the floor — no tools, no doctrine — and behaves
    /// exactly as it did before this axis existed.
    /// </para></summary>
    public LaneRoster Lanes { get; init; } = LaneRoster.Floor;

    /// <summary>A single-subject reading plan — the ad-hoc shape, and what a test with one subject
    /// collapses to.</summary>
    public static LegPlan Reading(Suite suite, ModelEndpoint endpoint, Sampling sampling) =>
        new(suite, SubjectRoster.Of(endpoint, sampling), [], TaskKind.Reading);

    public static LegPlan Reading(Suite suite, SubjectRoster subjects) =>
        new(suite, subjects, [], TaskKind.Reading);
}

/// <summary>One leg's resolved context, carried as a value so the methods below take four arguments instead
/// of nine. Everything here was decided before the model was asked, and none of it changes afterwards.</summary>
internal sealed record LegWork(
    RunCell Cell,
    WorkerIdentity Owner,
    LegPlan Plan,
    Question Question,
    RosterEntry Subject,
    LegDeadline Deadline,
    RetrievedContext Retrieved);

/// <summary>One leg, end to end: claim a cell, run its phases, score the answer, store it, settle.
/// <para>
/// Every piece it uses existed separately before this class and none of them were connected. This is the
/// assembly, and it is where the design is actually tested — a seam that looked fine in isolation is only
/// proved by something driving all of them in order.
/// </para>
/// <para>
/// <b>Result first, settle second, and re-entrant across the gap.</b> A crash between the two leaves the
/// cell claimed rather than settled, so the sweep hands it back and a retry runs again — at which point the
/// result store refuses the duplicate. That refusal must not deadlock the retry, so the runner asks whether
/// the leg was already scored and, if it was, finishes the job it interrupted instead of starting over.
/// </para></summary>
public sealed class LegRunner(
    IRunStore runs,
    IResultStore results,
    IModelRuntime runtime,
    IRetriever retriever,
    IHardwareSampler sampler,
    ToolLoopRunner loop,
    TimeProvider clock,
    ILogger<LegRunner> logger)
{
    /// <summary>Claims one cell and runs it. "Nothing to claim" is a VALUE — several workers draining a
    /// queue is the expected shape, and a loser must not treat losing as a fault.</summary>
    public async Task<Outcome<LegResult>> RunNextAsync(
        Guid runId, WorkerIdentity owner, LegPlan plan, CancellationToken cancellationToken)
    {
        var claim = await runs.ClaimNextAsync(runId, owner, cancellationToken);

        if (claim is Outcome<RunCell>.Fail nothing)
        {
            return Outcome<LegResult>.Failure(nothing.Reason);
        }

        return await RunAsync(((Outcome<RunCell>.Ok)claim).Value, owner, plan, cancellationToken);
    }

    private async Task<Outcome<LegResult>> RunAsync(
        RunCell cell, WorkerIdentity owner, LegPlan plan, CancellationToken cancellationToken)
    {
        if (await results.HasResultAsync(cell.Id, cancellationToken))
        {
            // The interrupted case: scored, never settled. Finish that rather than measure it twice.
            logger.LogInformation("Cell {Cell} was already scored — settling the leg a crash left open", cell.Id);
            await runs.SettleAsync(cell.Id, owner, new LegOutcome.Completed(), cancellationToken);
            return Outcome<LegResult>.Failure($"cell {cell.Id} was already scored; the open leg is now settled");
        }

        var question = plan.Suite.Questions.FirstOrDefault(q => q.Id == cell.QuestionId);

        return question is null
            ? await AbandonAsync(cell, owner, $"suite {plan.Suite.Stamp} has no question '{cell.QuestionId}'", cancellationToken)
            : await AskAsync(cell, owner, plan, question, cancellationToken);
    }

    /// <summary>The measured part of the leg, under ONE deadline.
    /// <para>
    /// The deadline is created here rather than per call, and that placement is the point: a lane that
    /// answers in one completion and a lane that loops twenty-five times must cost the same wall clock
    /// ceiling. When the loop lands it turns here, reading <see cref="LegDeadline.Exhausted"/> between
    /// turns and passing <see cref="LegDeadline.ForCall"/> down — never a fresh budget per turn.
    /// </para></summary>
    private async Task<Outcome<LegResult>> AskAsync(
        RunCell cell, WorkerIdentity owner, LegPlan plan, Question question, CancellationToken cancellationToken)
    {
        // The cell says which subject this leg is FOR, so the endpoint is looked up rather than assumed.
        // A miss is settled, never defaulted: a leg sent to another subject's endpoint would carry this
        // cell's label and be invisible in every number built afterwards.
        if (plan.Subjects.For(cell.SubjectModelId) is not Outcome<RosterEntry>.Ok(var subject))
        {
            return await AbandonAsync(
                cell, owner, plan.Subjects.For(cell.SubjectModelId).Match(_ => string.Empty, reason => reason), cancellationToken);
        }

        // And the same for the other axis: the cell names its variant, so the recipe is looked up rather
        // than taken from whichever one the run resolved first.
        if (plan.Variants.For(cell.Variant) is not Outcome<VariantChoice>.Ok(var variant))
        {
            return await AbandonAsync(
                cell, owner, plan.Variants.For(cell.Variant).Match(_ => string.Empty, reason => reason), cancellationToken);
        }

        // A fix leg runs PHASES (todo/PLAN_investigate_vs_implement.md step 4). Only the
        // investigate-only arm is runnable today — it builds nothing, so the code lane's isolation
        // gate does not bind it; the arms that produce a diff are refused BY NAME before any phase
        // row exists, because a cell whose arm the runner cannot honour must not look started.
        if (plan.Kind == TaskKind.Fix && cell.Arm != FixArm.InvestigateOnly)
        {
            return await BlockAsync(
                cell, owner,
                $"the {cell.Arm.Canonical()} arm needs the sandbox executor — the code lane is attended-only "
                + "until the isolation assertions of PLAN_code_lane.md §4.2 pass, and only investigate-only runs today",
                cancellationToken);
        }

        var deadline = LegDeadline.For(plan.Budgets, clock.GetUtcNow());

        if (plan.Kind == TaskKind.Fix)
        {
            await OpenInvestigateAsync(cell, cancellationToken);
        }

        var retrieved = await RetrieveAsync(question, variant, deadline, cancellationToken);

        var outcome = await MeasureAsync(
            cell, owner, plan, question, subject, deadline, retrieved, cancellationToken);

        if (plan.Kind == TaskKind.Fix)
        {
            await ClosePhasesAsync(cell.Id, deadline, cancellationToken);
        }

        return outcome;
    }

    private async Task<Outcome<LegResult>> MeasureAsync(
        RunCell cell,
        WorkerIdentity owner,
        LegPlan plan,
        Question question,
        RosterEntry subject,
        LegDeadline deadline,
        Outcome<RetrievedContext> retrieved,
        CancellationToken cancellationToken)
    {
        // UnansweredAsync, not AbandonAsync: retrieval is INSIDE the leg's wall, so an engine that ran the
        // clock out is a recorded CAP and an engine that failed inside the budget is a crash. Settling both
        // as crashes would report the harness as broken over a merely slow index.
        if (retrieved is Outcome<RetrievedContext>.Fail unretrieved)
        {
            return await UnansweredAsync(cell, owner, deadline, unretrieved.Reason, cancellationToken);
        }

        var context = ((Outcome<RetrievedContext>.Ok)retrieved).Value;
        // The QUERY-TIME subset, not the whole recipe: index-time selectors are verified against
        // /index-state, where the engine actually reports them. See RetrievedContext.QueryTimeRequested.
        var echoed = context.QueryTimeRequested.AssertAppliedIn(context.Applied);

        return echoed is Outcome<EngineAxes>.Fail ignored
            ? await BlockAsync(cell, owner, ignored.Reason, cancellationToken)
            : await AnswerAsync(
                new LegWork(cell, owner, plan, question, subject, deadline, context), cancellationToken);
    }

    /// <summary>Materialises the investigate-only phase record and starts its working phase. Rows that
    /// already exist come back as stored — a swept-back leg resumes the record its crash left.</summary>
    private async Task OpenInvestigateAsync(RunCell cell, CancellationToken cancellationToken)
    {
        var materialised = PhasePlan.Materialise(cell.Id, TaskKind.Fix, cell.Arm);

        if (materialised is not Outcome<IReadOnlyList<LegPhase>>.Ok(var fresh))
        {
            return; // unreachable for the arm the guard above admitted; the leg still runs and settles honestly
        }

        var phases = await runs.EnsurePhasesAsync(cell.Id, fresh, cancellationToken);
        var first = phases.FirstOrDefault(p => p.Ordinal == 0);

        if (first is { State: PhaseState.Pending } && PhasePlan.Start(first, phases) is Outcome<LegPhase>.Ok(var started))
        {
            await runs.SavePhasesAsync([started], cancellationToken);
        }
    }

    /// <summary>Closes a fix leg's phase record from the outcome the cell SETTLED with — after the
    /// fact, deliberately: every path out of a leg already settles the cell, and reading that one
    /// stored answer beats threading a phase handle through five of them. A cap or a crash stops the
    /// LEG, so the later phases go <see cref="PhaseState.Stopped"/>; a completed leg leaves the Judge
    /// phase Pending for the judge pass to close.</summary>
    private async Task ClosePhasesAsync(Guid cellId, LegDeadline deadline, CancellationToken cancellationToken)
    {
        if (await runs.CellAsync(cellId, cancellationToken) is not Outcome<RunCell>.Ok(var cell) || !cell.IsTerminal)
        {
            return;
        }

        var phases = await runs.PhasesAsync(cellId, cancellationToken);
        var working = phases.FirstOrDefault(p => p.Kind != PhaseKind.Judge && p.State is PhaseState.Running or PhaseState.Pending);

        if (working is null)
        {
            return;
        }

        var elapsed = clock.GetUtcNow() - deadline.StartedAt;
        var (ended, others) = PhasePlan.End(
            working with { Thinking = elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero },
            phases, cell.OutcomeKind, cell.OutcomeDetail);

        await runs.SavePhasesAsync([ended, .. others], cancellationToken);
    }

    /// <summary>A leg whose ARM could not be trusted, settled without being run.
    /// <para>
    /// Distinct from a crash on purpose: a crash says this harness or that runtime is broken, and a block says
    /// the configuration does not hold together — an engine that applied an axis differently from the way it
    /// was asked, a recipe whose corpus is not the one answering. The subject never saw the question, so it is
    /// not scored either; a blocked cell is cheap and visible, and a mislabelled measurement is permanent.
    /// </para></summary>
    private async Task<Outcome<LegResult>> BlockAsync(
        RunCell cell, WorkerIdentity owner, string reason, CancellationToken cancellationToken)
    {
        await runs.SettleAsync(cell.Id, owner, new LegOutcome.Blocked(reason), cancellationToken);
        logger.LogWarning("Cell {Cell} was blocked rather than measured: {Reason}", cell.Id, reason);

        return Outcome<LegResult>.Failure($"blocked: {reason}");
    }

    /// <summary>Retrieval, for the arms that have any, under what the LEG has left.
    /// <para>
    /// The control arm is not asked, and that is the whole reason the definition is a closed union rather
    /// than a recipe with retrieval switched off: a baseline leg must produce
    /// <see cref="RetrievedContext.NotPerformed"/>, which every metric downstream reads as "this arm
    /// surfaces nothing" — never as a search that came back empty.
    /// </para>
    /// <para>
    /// <b>Bounded by the leg's own deadline, not by a transport default.</b> A retrieval is a wait inside a
    /// leg, and the whole argument for one deadline per leg is that every wait inside it shares that ceiling
    /// — otherwise a hung engine adds its own timeout to the model's, and the leg costs more than the arm
    /// allowed. The remainder is what the model call then receives through <c>ForCall</c>, so slow retrieval
    /// eats into thinking time rather than extending the leg.
    /// </para></summary>
    private async Task<Outcome<RetrievedContext>> RetrieveAsync(
        Question question, VariantChoice variant, LegDeadline deadline, CancellationToken cancellationToken)
    {
        if (variant.Definition is not VariantDefinition.RetrievalRecipe recipe)
        {
            return Outcome<RetrievedContext>.Success(RetrievedContext.NotPerformed);
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (deadline.IsBounded)
        {
            budget.CancelAfter(deadline.Remaining(clock.GetUtcNow()));
        }

        try
        {
            return await retriever.RetrieveAsync(new RetrievalRequest(question.Prompt, recipe), budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our ceiling, not the operator stopping the run. A value, so the leg settles as a cap rather
            // than the campaign unwinding over one slow index.
            return Outcome<RetrievedContext>.Failure(
                $"the retrieval did not answer inside the {deadline.Describe} the leg had");
        }
    }

    /// <summary>The model half: the prompt is assembled from the question AND whatever retrieval surfaced,
    /// then sent under what the leg has left of its own deadline.
    /// <para>
    /// <b>The deadline is checked BETWEEN the two steps.</b> This is the between-turns check the deadline was
    /// designed for, arriving with the first leg that has more than one step: a leg whose wall went while
    /// retrieval was working has nothing left to think with, and asking anyway would spend a completion to
    /// produce an answer generated under no budget — measuring the ceiling and calling it a score.
    /// </para></summary>
    private async Task<Outcome<LegResult>> AnswerAsync(LegWork work, CancellationToken cancellationToken)
    {
        if (work.Deadline.Exhausted(clock.GetUtcNow()))
        {
            return await UnansweredAsync(
                work.Cell, work.Owner, work.Deadline, "retrieval used the whole leg", cancellationToken);
        }

        // A fix leg's prompt carries the diagnosis contract; a reading leg's is exactly what it always
        // was — the arm must not change the baseline it is compared against.
        var prompt = work.Plan.Kind == TaskKind.Fix
            ? DiagnosisPrompt.Assemble(work.Question, work.Retrieved, work.Plan.Prompt)
            : RagPrompt.Assemble(work.Question, work.Retrieved, work.Plan.Prompt);

        var lane = work.Plan.Lanes.For(work.Cell.LaneName);
        if (lane is Outcome<LaneChoice>.Fail unresolved)
        {
            // A refusal, not a fallback to the first lane: a leg measured under an instruction it was not
            // planned for is a number no report can catch.
            return await AbandonAsync(work.Cell, work.Owner, unresolved.Reason, cancellationToken);
        }

        var chosen = ((Outcome<LaneChoice>.Ok)lane).Value;

        return chosen.Surface is ToolSurface.Looping looping
            ? await LoopAsync(work, prompt, chosen.Doctrine, looping, cancellationToken)
            : await AskOnceAsync(work, prompt, chosen.Doctrine, cancellationToken);
    }

    /// <summary>The single completion this runner has always made — with one thing new: the doctrine now
    /// reaches <c>SystemPrompt</c> instead of <c>string.Empty</c>. A floor lane's doctrine is empty, so a run
    /// planned without the catalog sends exactly the request it sent before.</summary>
    private async Task<Outcome<LegResult>> AskOnceAsync(
        LegWork work, string prompt, string doctrine, CancellationToken cancellationToken)
    {
        var asked = await runtime.AskAsync(
            new ModelRequest(
                work.Subject.Endpoint,
                work.Subject.Sampling,
                doctrine,
                prompt,
                work.Deadline.ForCall(clock.GetUtcNow())),
            cancellationToken);

        return asked is Outcome<ModelAnswer>.Fail failed
            ? await UnansweredAsync(work.Cell, work.Owner, work.Deadline, failed.Reason, cancellationToken)
            : await ScoreAsync(
                work, prompt, ((Outcome<ModelAnswer>.Ok)asked).Value, ToolLedger.NotOffered, cancellationToken);
    }

    /// <summary>
    /// A leg that works a tool surface.
    ///
    /// <para>A leg that spends its turn ceiling settles as <see cref="LegOutcome.CapExceeded"/> with
    /// <see cref="BudgetKind.Turns"/> — never as a crash, and never as a wrong answer. That keeps it out of
    /// paired deltas through the existing <c>CountsInPairedDelta</c> rule, which is the whole reason the
    /// distinction is worth making: a model that was still working when the ceiling arrived did not get
    /// anything wrong, and averaging it in would report the instrument's limit as the subject's score.</para>
    /// </summary>
    private async Task<Outcome<LegResult>> LoopAsync(
        LegWork work, string prompt, string doctrine, ToolSurface.Looping surface, CancellationToken cancellationToken)
    {
        // The DEADLINE, not a frozen budget list. ForCall narrows the wall to what remains, and its own
        // comment says why that matters here: it "is what makes twenty-five turns share one ceiling instead
        // of each starting a fresh one". Computed once out here, every turn would receive the remainder as
        // it stood before turn one, and a leg could outrun its wall by a factor of its turn ceiling.
        var looped = await loop.RunAsync(
            work.Subject.Endpoint,
            work.Subject.Sampling,
            doctrine,
            prompt,
            work.Deadline,
            surface,
            cancellationToken);

        if (looped is Outcome<ToolLoopResult>.Fail failed)
        {
            return await UnansweredAsync(work.Cell, work.Owner, work.Deadline, failed.Reason, cancellationToken);
        }

        var result = ((Outcome<ToolLoopResult>.Ok)looped).Value;

        if (result.End == LoopEnd.TurnsSpent)
        {
            await runs.SettleAsync(
                work.Cell.Id,
                work.Owner,
                new LegOutcome.CapExceeded(
                    BudgetKind.Turns, BudgetScope.Question, surface.MaxTurns, result.TurnsSpent),
                cancellationToken);

            return Outcome<LegResult>.Failure(
                $"the leg spent its {surface.MaxTurns}-turn ceiling after {result.Calls.Count} tool call(s)");
        }

        return await ScoreAsync(work, prompt, result.Answer, Watched(work, result.Calls), cancellationToken);
    }

    /// <summary>The loop's calls as a durable ledger.
    /// <para>
    /// <c>Watched</c> rather than <c>Recovered</c>: this harness drove every turn, so the ordinals mean
    /// what they say. A CLI-agent lane produces the other kind, later and from a server's spool, and the
    /// two are never averaged together.
    /// </para></summary>
    private static ToolLedger Watched(LegWork work, IReadOnlyList<TurnCall> calls) =>
        ToolLedger.Watched(
            [.. calls.Select((record, ordinal) => new LedgerEntry(ordinal, record.Turn, Phase(work), record.Call))]);

    /// <summary>Which phase a tool call happened in. A fix leg's loop runs while it INVESTIGATES — the arms
    /// that produce a diff cannot be planned yet — so the phase is decided by the run's kind rather than
    /// guessed from the call.</summary>
    private static PhaseKind Phase(LegWork work) =>
        work.Plan.Kind == TaskKind.Fix ? PhaseKind.Investigate : PhaseKind.Answer;

    /// <summary>What the subject reached for, for the tool-use metric.
    ///
    /// <para><b><c>Available</c> is true for any looping lane, even when the list is empty.</b> "It called
    /// nothing" and "it could not call anything" are the two readings this flag exists to keep apart, and
    /// only the second is a not-applicable — a tool lane whose subject ignored every tool is a REAL zero and
    /// one of the more interesting results the wording experiment can produce.</para>
    ///
    /// <para><b>A REFUSED call still counts as called</b>, and deliberately: the expectation asks whether the
    /// subject picked this tool, which is exactly what a description is being measured on. A model that
    /// selected the right tool and passed it a path outside the checkout demonstrated the selection the
    /// metric is about. The outcome is not lost — it is on the ledger's own <c>ToolCall</c>, which is where a
    /// reader asking the different question ("did the calls WORK") has to look.</para>
    ///
    /// <para><b>Derived from the ledger rather than built beside it</b>, so the number a report prints and
    /// the artefact it can be re-read against cannot disagree. They were assembled separately for one
    /// commit, which is one commit longer than two representations of the same fact should ever live apart.</para>
    /// </summary>
    private static ToolUsageObservation Observed(ToolLedger ledger) =>
        new(ledger.Offered, ledger.Sequence);

    /// <summary>A leg that produced no answer: a CEILING when its own wall ran out, a crash otherwise.
    /// <para>
    /// The distinction is the reason this method exists. Both cases used to settle as
    /// <see cref="LegOutcome.Crashed"/>, which reads as "the harness is broken" — so a campaign against a
    /// merely slow endpoint would report its own instrument as faulty, and a genuinely broken one would
    /// look identical to it. Neither is scored: a subject that was never reached, and one cut off at a
    /// ceiling, did not get anything wrong.
    /// </para></summary>
    private async Task<Outcome<LegResult>> UnansweredAsync(
        RunCell cell, WorkerIdentity owner, LegDeadline deadline, string reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        if (!deadline.Exhausted(now))
        {
            return await AbandonAsync(cell, owner, reason, cancellationToken);
        }

        await runs.SettleAsync(cell.Id, owner, deadline.Cap(now), cancellationToken);
        logger.LogInformation("Cell {Cell} spent its {Budget} leg wall budget", cell.Id, deadline.Describe);

        return Outcome<LegResult>.Failure($"{reason} — the leg spent its {deadline.Describe} wall budget");
    }

    /// <summary>What the machine was doing across this leg's window.
    /// <para>
    /// The window is the leg's own clock — <c>LegDeadline.StartedAt</c> to now — so the readings a leg claims
    /// are the ones taken while it ran, and the half-open boundary in <c>LegSampling</c> keeps the next leg
    /// from claiming the same ones.
    /// </para>
    /// <para>
    /// <b>It cannot fail a leg.</b> A sampler that throws leaves the load unsampled and the result otherwise
    /// intact: the leg's answer is the expensive artefact and losing it to a performance counter would be a
    /// far worse trade than a row that says nobody watched.
    /// </para>
    /// <para>
    /// <c>heldAcceleratorAlone</c> is FALSE, unconditionally and for now. Only the accelerator lease can
    /// establish that a leg had the card to itself (<c>todo/PLAN_variant_matrix.md</c> §3.4b), and until it
    /// exists nothing here may claim <c>Attributed</c> — a figure that says "this leg used 20 GB" when
    /// something else held them is the one error indistinguishable from a correct measurement afterwards.
    /// </para></summary>
    private async Task<LegLoad> LoadAsync(LegWork work, CancellationToken cancellationToken)
    {
        try
        {
            var (load, vram) = await sampler.ReadAsync(
                work.Deadline.StartedAt, clock.GetUtcNow(), cancellationToken);

            return LegSampling.Over(
                load, vram, work.Deadline.StartedAt, clock.GetUtcNow(), heldAcceleratorAlone: false, sharedWith: string.Empty);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "The machine could not be sampled for cell {Cell}", work.Cell.Id);
            return LegLoad.NotSampled("the sampler failed for this leg");
        }
    }

    /// <summary>Scoring and persisting one leg: the answer's metrics, retrieval's metrics, and every piece
    /// of evidence the two were computed from — in ONE write, before the cell settles.</summary>
    private async Task<Outcome<LegResult>> ScoreAsync(
        LegWork work,
        string prompt,
        ModelAnswer answer,
        ToolLedger ledger,
        CancellationToken cancellationToken)
    {
        var stored = await results.SaveAsync(
            LegResult.Of(
                work.Cell.Id, prompt, answer.Text.Value, Metrics(work, answer, Observed(ledger)), clock.GetUtcNow()) with
            {
                Thinking = answer.Thinking,
                Meta = ResponseMeta.Of(answer),
                Retrieval = work.Retrieved,
                Load = await LoadAsync(work, cancellationToken),
                // The ledger is DURABLE, not just scored. A metric says a tool was called; only the ledger
                // says in what order, with what arguments, and whether the engine took it — which is the
                // difference between "the surface moved the score" and any account of HOW it was worked.
                Calls = ledger,
            },
            cancellationToken);

        if (stored is Outcome<LegResult>.Fail unsaved)
        {
            return Outcome<LegResult>.Failure(unsaved.Reason);
        }

        var settled = await runs.SettleAsync(work.Cell.Id, work.Owner, Outcome(answer), cancellationToken);

        if (settled is Outcome<RunCell>.Fail unsettled)
        {
            // The result is already durable, so this is a report rather than a loss — and it is the exact
            // window the re-entrancy check above exists to close.
            logger.LogWarning("Cell {Cell} was scored but not settled: {Reason}", work.Cell.Id, unsettled.Reason);
        }

        return stored;
    }

    /// <summary>Both mechanical readings of one leg.
    /// <para>
    /// Anchor recall stays where it was — inside <see cref="AnswerScoring"/>, fed by an observation — so
    /// there is exactly one definition of recall in the system. The rank-sensitive metrics are additive and
    /// empty for the control arm, which keeps the no-retrieval baseline carrying precisely the metric set it
    /// carried before this lane existed.
    /// </para></summary>
    private static IReadOnlyList<StoredMetric> Metrics(
        LegWork work, ModelAnswer answer, ToolUsageObservation tools) =>
    [
        .. AnswerScoring.Score(
            work.Question, answer, RetrievalScoring.Observe(work.Question, work.Retrieved), tools),
        .. RetrievalScoring.Score(work.Question, work.Retrieved),
        .. DiagnosisMetrics(work, answer),
    ];

    /// <summary>The diagnosis instrument, for fix legs only (todo/PLAN_investigate_vs_implement.md §3.3).
    /// <para>
    /// The causal ground truth is the question's own retrieval anchors — the harvest lands the
    /// reference fix's touched spans as exactly those, so the rag lane and the diagnosis read ONE set
    /// of anchors. Symptom anchors are not landed yet, so the trap metric stays un-emitted rather than
    /// always-passing. An uncaptured answer reads <i>not answered</i>, never a failed parse — the model
    /// may have been cut off, and AnswerScoring already keeps those two facts apart.
    /// </para></summary>
    private static IEnumerable<StoredMetric> DiagnosisMetrics(LegWork work, ModelAnswer answer)
    {
        if (work.Plan.Kind != TaskKind.Fix)
        {
            return [];
        }

        if (!answer.Text.WasCaptured)
        {
            return [StoredMetric.Text(
                DiagnosisScoring.Parses, "not answered",
                $"no text was captured: {answer.Text.Reason}", failed: false, "Unknown")];
        }

        return DiagnosisScoring.Score(
            DiagnosisJson.Read(answer.Text.Value),
            [.. work.Question.RetrievalExpectations.Select(e => e.Anchor)],
            symptoms: []);
    }

    /// <summary>An answer cut off at a ceiling ends the leg as a CAP, not as a completion. Scored as a
    /// wrong answer it would measure the ceiling, and a capped leg is excluded from paired deltas — which
    /// is only possible if the ceiling is recorded here rather than inferred from the text later.</summary>
    private static LegOutcome Outcome(ModelAnswer answer) =>
        answer.WasCutOff
            ? new LegOutcome.CapExceeded(BudgetKind.Context, BudgetScope.Question, 0, 0)
            : new LegOutcome.Completed();

    private async Task<Outcome<LegResult>> AbandonAsync(
        RunCell cell, WorkerIdentity owner, string reason, CancellationToken cancellationToken)
    {
        await runs.SettleAsync(cell.Id, owner, new LegOutcome.Crashed(reason), cancellationToken);
        return Outcome<LegResult>.Failure(reason);
    }
}
