using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Domain.Variants;

namespace Bench.Application;

/// <summary>Where suites and results live. Declared here and implemented outside, so the domain never
/// learns what a database is — and so the CLI can run over an in-memory store before Postgres exists.</summary>
public interface IBenchStore
{
    Task<Outcome<Suite>> LoadSuiteAsync(string suiteId, int version, CancellationToken cancellationToken);

    Task<Outcome<Suite>> SaveSuiteAsync(Suite suite, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListSuiteIdsAsync(CancellationToken cancellationToken);
}

/// <summary>A pinned, READ-ONLY tree at one commit.
/// <para>
/// Read-only is the contract, not a preference. The equivalent component upstream ran
/// <c>git checkout</c> in place on a configured repository path — which for a benchmark means
/// rewriting whatever working tree the operator happens to have open to a commit they did not ask
/// for. Implementations keep a bare clone per url and a worktree per commit, and never touch a
/// directory anyone works in.
/// </para></summary>
public interface ICheckoutProvider
{
    Task<Outcome<string>> EnsureAsync(MeasurementTarget target, CancellationToken cancellationToken);
}

/// <summary>A retrieval engine under measurement, including the one that does no retrieval at all.
/// Capabilities are DECLARED, never assumed: an engine that claims a trace-contract version we do not
/// know degrades to black-box rather than failing the run.</summary>
public interface IEngine
{
    EngineRef Describe { get; }

    /// <summary>The trace-contract version this engine emits; empty when it emits none.</summary>
    string TraceContractVersion { get; }

    /// <summary>What the subject can call. An engine is not something that "returns results" — it is
    /// the SURFACE a model works through, and the model decides what to call and when.
    /// <para>
    /// That distinction is measured rather than stylistic: the same four tools behind a different
    /// surface shape scored 4/63 against 37/63 on identical tasks. An engine port that exposed a single
    /// <c>SearchAsync</c> would measure retrieval quality while the thing that actually moves the score
    /// — how an agent works a surface — became invisible.
    /// </para></summary>
    IReadOnlyList<EngineTool> Tools { get; }

    Task<Outcome<string>> WarmAsync(string checkoutPath, CancellationToken cancellationToken);

    /// <summary>Runs one tool call. Expected refusals are VALUES: a path outside the checkout is an
    /// answer the subject can read and correct itself from, never an exception that ends the leg.</summary>
    Task<ToolAnswer> InvokeAsync(string tool, string argumentsJson, CancellationToken cancellationToken);
}

/// <param name="Query">What to retrieve for — the question's own text, in the single-shot lane.</param>
/// <param name="Recipe">The variant's recipe. A <see cref="VariantDefinition.Baseline"/> can never reach a
/// retriever: the runner does not call one for the control arm, and the type says so.</param>
public sealed record RetrievalRequest(string Query, VariantDefinition.RetrievalRecipe Recipe);

/// <summary>Retrieval as a CALL, for the single-shot lane — distinct from <see cref="IEngine"/>, which is
/// retrieval as a SURFACE a subject works.
/// <para>
/// Two ports rather than a <c>SearchAsync</c> on the engine, and the distinction is measured rather than
/// stylistic. In the single-shot lane the harness retrieves and the subject reads what it was given; in the
/// agentic lane the subject decides what to call and when, and the same four tools behind a different
/// surface shape scored 4/63 against 37/63 on identical tasks. Folding them into one method would make the
/// second measurement impossible to express — and folding the first into a tool loop would make it
/// impossible to attribute, since a subject that never searched would produce an empty funnel.
/// </para></summary>
public interface IRetriever
{
    EngineRef Describe { get; }

    /// <summary>Whether this engine can serve a recipe AT ALL, answered without a round trip.
    /// <para>
    /// Asked once per variant before a single cell exists, for the reason the model registry is resolved
    /// there too: a recipe naming an axis this engine has no field for, or a corpus shape it does not have,
    /// would otherwise be discovered as a wall of identical leg failures hours into a sweep. On success it
    /// returns the axes it WOULD send, so an operator can read what a variant actually becomes.
    /// </para></summary>
    Outcome<string> CanServe(VariantDefinition.RetrievalRecipe recipe);

    /// <summary>What is actually in the index that will answer this recipe.
    /// <para>
    /// A round trip, unlike <see cref="CanServe"/>, and the one that closes the half of a variant no request
    /// can select. A corpus is built by an indexing pass and a search reaches whatever collection the engine
    /// resolves, so the chunk size and the embedder a recipe names are claims until something reads them
    /// back. Missing that read cost exactly one measurement: a variant declaring 512 embed tokens recorded
    /// against a 256-token index, with every number in it real.
    /// </para></summary>
    Task<Outcome<IndexState>> InspectAsync(
        VariantDefinition.RetrievalRecipe recipe, CancellationToken cancellationToken);

    /// <summary>One retrieval, with its funnel and its echoed axes. A refusal is a VALUE: an engine that is
    /// down, a collection that was never built, or a recipe this build cannot express honestly are all
    /// facts a leg records — never exceptions that end a campaign of ten thousand.</summary>
    Task<Outcome<RetrievedContext>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken);
}

/// <summary>A model that can answer a question, local or cloud.</summary>
public interface IModelRuntime
{
    ModelHosting Hosting { get; }

    /// <summary>Confirms a budget actually reached the runtime. Returns the runtime identifier to
    /// stamp on <see cref="Budget.AcceptedBy"/>, or a failure when the knob does not exist here —
    /// which is the case that must be visible rather than assumed.</summary>
    Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken);

    /// <summary>Asks the model once.
    /// <para>
    /// A refusal is a VALUE: an endpoint that is down, a model that is not pulled, a request the runtime
    /// rejects are all answers a leg records and moves on from — never exceptions that end a run of ten
    /// thousand legs over one unlucky call.
    /// </para></summary>
    Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken);
}

/// <param name="Endpoint">Where to ask, and what its tokens cost.</param>
/// <param name="Sampling">What to ask FOR. What actually went out is reported back on the answer,
/// because at least one runtime substitutes its own defaults over a request it was handed.</param>
/// <param name="Budgets">The ceilings this call must respect. Any the runtime cannot enforce were already
/// refused by <see cref="IModelRuntime.AcceptBudgetAsync"/> — passing them here does not make them real.</param>
public sealed record ModelRequest(
    ModelEndpoint Endpoint,
    Sampling Sampling,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<Budget> Budgets)
{
    /// <summary>What the model may call. Empty is a single-shot completion — exactly what every request
    /// through this port has been until now, which is why it is an <c>init</c> property with an empty
    /// default rather than a positional parameter.</summary>
    public IReadOnlyList<EngineTool> Tools { get; init; } = [];

    /// <summary>The conversation so far: what the model said, and what its calls answered. Empty means the
    /// first turn.
    /// <para>The user's question is NOT in here — it stays on <see cref="UserPrompt"/> and is sent once, so
    /// there is one place that can say what was asked rather than two that can disagree.</para></summary>
    public IReadOnlyList<ModelTurn> Transcript { get; init; } = [];

    public static ModelRequest Of(ModelEndpoint endpoint, Sampling sampling, string userPrompt) =>
        new(endpoint, sampling, string.Empty, userPrompt, []);

    /// <summary>One turn of a tool-calling loop: the same question, the same tools, and everything that has
    /// happened since.
    /// <para>The doctrine rides in <paramref name="systemPrompt"/> — this is the factory through which
    /// <c>Lane.Preamble</c> finally reaches a model, having been declared on day one and read by
    /// nothing.</para></summary>
    public static ModelRequest OfTurn(
        ModelEndpoint endpoint,
        Sampling sampling,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<EngineTool> tools,
        IReadOnlyList<ModelTurn> transcript) =>
        new(endpoint, sampling, systemPrompt, userPrompt, budgets) { Tools = tools, Transcript = transcript };
}

public enum TraceMode
{
    BlackBox,
    WhiteBox,
}

/// <summary>The trace of one leg. Two implementations satisfy this port from the first commit — a live
/// black-box one and a fixture-replay white-box one — because an interface with a single implementation
/// proves nothing about its own shape.</summary>
public interface IRunTrace
{
    TraceMode Mode { get; }

    Task<Outcome<LegTrace>> CaptureAsync(MeasurementTuple tuple, CancellationToken cancellationToken);
}

/// <summary>Hardware, sampled out of band. A failure in here may never fail or delay a run: measuring
/// the instrument instead of the subject is the classic way to make a whole series worthless.</summary>
/// <summary>Reads the machine a run is about to measure on, ONCE, before any cell exists.
/// <para>
/// Separate from <see cref="IHardwareSampler"/> because the two have different lifetimes and mixing them
/// would make both useless: this answers what the machine IS — an operating system, a driver, a disk — and
/// that does not change between legs, while the sampler answers what it was DOING and that changes every
/// second. Writing a driver version ten thousand times is a column nobody can index; taking a machine-wide
/// VRAM range across a ten-thousand-cell campaign answers nothing.
/// </para>
/// <para>
/// It never fails a run. A machine nobody could read is <c>MachineFacts.NotRecorded</c>, which is a state the
/// shape carries and a report can print — and which every run stored before this port existed already is.
/// </para></summary>
public interface IMachineProbe
{
    /// <param name="volumePath">The path whose volume matters — the checkout root, because that is the disk
    /// a run's corpus and worktrees actually live on and the one whose cluster size and free space bear on
    /// the numbers.</param>
    Task<MachineFacts> ReadAsync(string volumePath, CancellationToken cancellationToken);
}

/// <summary>Readings of the machine, taken out of band and asked for by WINDOW.
/// <para>
/// A window rather than a drain, and the difference is not stylistic: a drain empties, so two legs running in
/// one process would each get half the readings and neither would know it. Asking for an interval leaves the
/// samples where they are, lets a leg that overlapped another still describe itself, and makes the buffer's
/// bound this adapter's business rather than every caller's.
/// </para>
/// <para>
/// The two streams are separate because they cost three orders of magnitude apart — processor and memory are
/// microseconds, a vendor-neutral VRAM read on Windows is about a second (measured,
/// <c>research/PLAN_hardware_sampler.md</c> §7.3) — and one cadence for both would make the sampler the thing it
/// measures.
/// </para></summary>
public interface IHardwareSampler
{
    /// <summary>Readings taken in <c>[from, to)</c>. Empty is an ordinary answer: on a host with nothing to
    /// sample, or for a leg shorter than the slow stream's cadence, nobody read anything — which the
    /// summaries express as <em>not sampled</em> rather than as zero.</summary>
    Task<(IReadOnlyList<LoadSample> Load, IReadOnlyList<VramSample> Vram)> ReadAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

/// <summary>The arbiter. Selectable per suite, and re-runnable over STORED answers — re-judging must
/// never require re-running the legs, or changing the judge costs the price of the whole run again.</summary>
public interface IJudge
{
    ModelRef Model { get; }

    Task<Outcome<JudgeVerdict>> JudgeAsync(string question, string answer, string reference, CancellationToken cancellationToken);
}

public sealed record JudgeVerdict(bool Passed, string Reason);

/// <summary>The raw model exchanges of the delivered-work pipeline, kept so a score can be recomputed
/// without paying for a single call.
///
/// <para><b>Append-only and permanent.</b> There is no update and no delete, and that is the interface
/// saying what the table is: a payload that could be rewritten would make an old score unreproducible
/// while still looking reproducible, and one that could be aged out would end the recompute property the
/// port exists for. The size that buys is a budget line, not a leak — see <see cref="StagePayload"/>.</para>
/// </summary>
public interface IStagePayloadStore
{
    /// <summary>Records one exchange. Refuses a duplicate <c>(result, stage, ordinal)</c>: two payloads for
    /// one attempt would make "was this re-asked" unanswerable, and that is read off the ordinal.</summary>
    Task<Outcome<StagePayload>> AppendAsync(StagePayload payload, CancellationToken cancellationToken);

    /// <summary>Everything stored for one result, in stage then ordinal order — the order a rescore replays
    /// them in. Empty for a result measured before this existed, which is the honest answer rather than a
    /// refusal: those runs are simply not rescorable, and a reader must be able to tell that apart from a
    /// run whose model said nothing.</summary>
    Task<IReadOnlyList<StagePayload>> ForResultAsync(Guid resultId, CancellationToken cancellationToken);

    /// <summary>How much this table holds, so the number nobody prints is not the number nobody notices
    /// growing. Rows and bytes together: a million small payloads and a thousand enormous ones are
    /// different problems with the same row count.</summary>
    Task<StagePayloadFootprint> FootprintAsync(CancellationToken cancellationToken);
}

/// <param name="Results">How many results have payloads at all — the rescorable population, which is what
/// a reader actually wants when they ask how much is stored.</param>
public sealed record StagePayloadFootprint(long Rows, long Results, long Bytes)
{
    public string Describe =>
        $"{Rows} payload(s) over {Results} result(s), {Bytes / (1024d * 1024):0.#} MiB — kept permanently so "
        + "every stored score can be recomputed without a model call";
}
