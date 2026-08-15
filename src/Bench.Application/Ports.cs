using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;

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

/// <summary>A model that can answer a question, local or cloud.</summary>
public interface IModelRuntime
{
    ModelHosting Hosting { get; }

    /// <summary>Confirms a budget actually reached the runtime. Returns the runtime identifier to
    /// stamp on <see cref="Budget.AcceptedBy"/>, or a failure when the knob does not exist here —
    /// which is the case that must be visible rather than assumed.</summary>
    Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken);
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
public interface IHardwareSampler
{
    Task<IReadOnlyList<HardwareSample>> DrainAsync(CancellationToken cancellationToken);
}

/// <summary>The arbiter. Selectable per suite, and re-runnable over STORED answers — re-judging must
/// never require re-running the legs, or changing the judge costs the price of the whole run again.</summary>
public interface IJudge
{
    ModelRef Model { get; }

    Task<Outcome<JudgeVerdict>> JudgeAsync(string question, string answer, string reference, CancellationToken cancellationToken);
}

public sealed record JudgeVerdict(bool Passed, string Reason);
