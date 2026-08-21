# PLAN — the tool benchmark: a lane is a catalog row, the doctrine is an axis, and the loop finally turns

> Status: **steps 1–4 implemented 2026-08-19; steps 6–11 open.** Step 5 shipped in `dew_flow_mcp`
> 2026-08-16. A lane is a hashed catalog row, the loop turns, the doctrine reaches the model, and a tool
> expectation scores — the harness can now measure a tool surface, but nothing yet reaches a real one. Scope: `Bench.Domain` (new `Lanes/`, extensions to
> `Runs/` and `Suites/`), `Bench.Application` (the tool loop and its ports), `Bench.Infrastructure`
> (a new engine, a new runtime, two migrations), `Bench.Api`, `hosts/Cli`, and later `src/Bench.Ui`.
> One new cross-repository edge: this repository vendors `dew_flow_mcp` as a submodule.
>
> The sibling half — file-driven tool descriptions, a tool subset chosen at startup, a surface
> fingerprint and telemetry correlation — is `dew_flow_mcp · research/PLAN_tool_surface_config.md`. A change
> that crosses the boundary is named in both plans.
>
> Related: [PLAN_variant_matrix.md](PLAN_variant_matrix.md) (this plan is the design of its §3.8 agent
> lane), [PLAN_code_lane.md](PLAN_code_lane.md) (meets this plan at `PhaseKind`, nowhere else),
> [PLAN_investigate_vs_implement.md](PLAN_investigate_vs_implement.md) (consumes step 11's measured-CLI
> subject and §3.5's reconstructed tool calls; builds neither),
> [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) (founding plan; its §3 tuple already reserves the
> axis this plan fills), [../research/architecture.md](../research/architecture.md),
> [../research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md).

## 1. The goal, before any solution

We are about to add tools to a product whose entire pitch is that the tools are good. Right now, when a
tool ships, nobody can answer three questions about it:

1. **Does it work at all** — not "does the unit test pass", but *does a model presented with this tool
   actually reach for it, and can it form arguments the tool accepts*.
2. **Is it better than the agent's own native tools, and in which concrete cases** — the honest answer
   has never been "always", and the one time it was measured the margin was one point out of
   sixty-three.
3. **Which description text, and which ordering doctrine, makes it get used correctly** — because this
   is measurably the largest effect in the entire system, and it is currently unmeasurable here.

The same three questions for a **local** model through the bridge and for a **cloud CLI agent** with its
native tools plus our server. Those are different subjects, not one subject at two prices.

### 1.1 The measured priors that fix this plan's shape

Carried in [MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md); the doctrine numbers are new here and
come from `DewFlow · todo/RESULTS_native_toolset_arms.md:169-236`. Every one of them either refutes
something plausible or explains a decision below.

| what was varied | effect | why it shapes this plan |
|---|---|---|
| **The ordering doctrine text alone** — one paragraph telling the model which channel to use when, three wordings (`cover` / `first` / `late`), same 18 tools, same model, same 9 questions, same 25-turn cap | avg **30.0 → 46.5 of 63**, a **16.5-point** spread; the ranges do not overlap (`first` [44,49] > `cover` [36,41] > `late` [29,31]) | The doctrine is the **primary axis**, not a detail of the preamble. §3.2 |
| The **toolbox** — 4 plain filesystem tools against the full 18-tool retrieval surface, same everything else | **36/63 against 37/63** — one point, at 52 % more wall-clock | Adding a tool is not self-evidently an improvement. The no-tools and native-tools baselines are first-class arms, never a formality. §3.6 |
| The **shape of the surface** — the same four tools over the MCP wire against the in-process bridge | **4/63 against 36/63**, nine times, from the form alone; replicated at 16 tools as 4/47 against 11/47 | `Presentation` is part of a lane's identity, and the in-process bridge must be measured genuinely in-process. §3.11 |
| The **register** a model searches in — native tools against an MCP surface | **not one of 37 searches** was in natural language natively; on the MCP surface the same models wrote full behavioural sentences | A tool can be unreachable not because it is bad but because nothing makes the model ask in the register it answers. That is a description/doctrine effect, and it is what §3.7's `ToolUsed` expectations detect. |
| **Reading tasks**, strong model | 89–100 %, against a ≤80 % discrimination threshold | Reading saturates. The discriminating instruments here are the **micro-task** (did it pick the right tool) and, later, the fix lane. §3.6 |
| Repeat spread **within** one doctrine at temperature 0 with a pinned seed | **5, 5 and 2 points** — a noise floor of ≈±5 against ≈8 between doctrines | `n ≥ 2` is not optional, and a wording difference smaller than ~8 points is not a finding. §3.10 |

And the three ways the earlier programme's own numbers turned out to be wrong, each now a guard rather
than a memory: an **unpinned sampler** produced a fake 2× (6/14/7 of 47 on identical setups) because the
OpenAI-compatible route substitutes its own defaults over a model file; **answer-key leakage** into the
measured tree contaminated 10 of 13 legs of one arm and retracted a published headline; a **self-judging
model passed 6 of 6 answers** an independent arbiter failed 0 of 6.

## 2. What exists today, verified

> **Re-verified 2026-08-19 against the code rather than against this plan's own claims.** Four rows had moved,
> and all four in the direction that makes this plan cheaper: the telemetry correlation now has both ends, the
> MCP surface it measures went from one tool to four with the subset and description-set axes turnable and
> echoed, the by-lane average was generalised into a report dimension, and the §3.7 loader defect moved file.
> The rows below are corrected in place; the build order after them is unchanged.

| capability | state | where |
|---|---|---|
| `Lane(Name, Preamble)` as a matrix axis | **`Preamble` is dead** — nothing reads it; `LegRunner` sends `SystemPrompt = string.Empty` unconditionally. Only `Lane.Name` is persisted and grouped on | `src/Bench.Domain/Runs/Axes.cs:79-84`, `src/Bench.Application/LegRunner.cs:76` |
| The tool loop | **does not exist.** `LegRunner` asks the model exactly once; `Retrieval(plan)` returns `RetrievalObservation.None` unconditionally, so every lane is a no-tools lane | `src/Bench.Application/LegRunner.cs:75-77, 122` |
| `IEngine` as *the surface a model works through* (not a search API), `EngineTool(Name, Description, ArgumentsSchema)` | built, tested, **driven by nothing** | `src/Bench.Application/Ports.cs:38-60`, `src/Bench.Domain/Engines/EngineTool.cs:10` |
| `BudgetKind.Turns` | declared, and `OpenAiCompatibleRuntime` already **refuses** it: *"one completion has no turns — a turn ceiling belongs to an agentic loop, not to this runtime"* | `src/Bench.Domain/Runs/Budgets.cs:7`, `src/Bench.Infrastructure/Models/OpenAiCompatibleRuntime.cs:45-46` |
| `ToolCall(Name, ArgumentsJson, Refused, Error, Duration)`, `TimeBuckets`, `Captured`/`CapturedCount` | built; `LegTrace` is never captured by `bench run` | `src/Bench.Domain/Trace/LegTrace.cs:33-49, 99-130` |
| `TelemetryCorrelation(Leg, Phase)` + `IsAttributed`, and the ingest codec that reads an absent `correlation` as unattributed | **both ends now built** — the emitter gained the field 2026-08-16 with `--correlation`, so §3.5's reconstructed lane has a join key | `src/Bench.Domain/Telemetry/ToolTelemetry.cs:52-64`, `src/Bench.Application/TelemetryCodec.cs:123-128, 191`; emitter: `dew_flow_mcp · src/Mcp.Telemetry/TelemetryRecord.cs:19` |
| The catalog pattern to mirror — immutable row, typed slug, `StableHash`, `Create`/`Rehydrate`/`Retire`, `Stamp`, never `Update` | **implemented 2026-08-16** as step 1 of the variant matrix; steps 2–11 of that plan remain open | `src/Bench.Domain/Variants/RetrievalVariant.cs:41-123`, `Variants/VariantSelection.cs:9-59`, `src/Bench.Domain/StableHash.cs:18-19` |
| `ExpectationKind` (4 values), `Expectation(Kind, Anchor, Text, Required)`, `Question.RetrievalExpectations` | built; no notion of a tool. **The unknown-kind fallback §3.7 fixes has MOVED**: it is `QuestionJson.ToExpectation`, not `SuiteJsonLoader`, and BOTH suite loading and stored bank rows reach it — so the typo trap is one step wider than §3.7 describes | `src/Bench.Domain/Suites/SuiteItems.cs:36-72`, `src/Bench.Application/QuestionJson.cs:63-75` |
| `RetrievalObservation(Available, …)` and the *not applicable* honesty rule for a lane that surfaces nothing | built and load-bearing — the exact shape §3.7 mirrors | `src/Bench.Domain/Runs/AnswerScoring.cs:9-15, 87-104` |
| Averaging by lane | **generalised since this was written** — the two methods became one `AverageByAsync(ReportDimension)` with `Lane` as a member, over columns the cell already carries. Cheaper than this plan assumed: the report axis exists and needs no join | `src/Bench.Application/ResultStore.cs:12-30` |
| The MCP surface it will measure | **four tools, and every axis this plan needs is now turnable** (2026-08-19): `dew_flow_rag_qln`'s daemon serves `rag_search_project_context`, `graf_search_types` and `graf_get_type_relations` beside `rt_read_local_file`; descriptions are read from `prompts/tools/<set>/*.md` with the compiled literal as the floor; a tool SUBSET and a description SET are chosen at process start and echoed by `GET /api/mcp/surface`. The surface a lane names can therefore be both served and verified | `dew_flow_rag_qln · research/module_tools.md`; `dew_flow_mcp · src/Mcp.Application/ToolSurfaceOptions.cs` |
| The submodule precedent | `dew_flow_rag_qln` vendors this exact repository and project-references five of its projects | `dew_flow_rag_qln · .gitmodules`, `hosts/Daemon/Daemon.csproj:26-30` |

**Two facts are worth reading twice.** The measurement tuple has carried `lane = (toolSurface,
preamble)` since the founding plan — the axis that moved the score sixteen times harder than the toolbox
was *declared on day one and never connected to anything*. And the runtime already refuses a turn
ceiling with a sentence naming the component this plan builds. This is not a redesign; it is finishing a
sentence the domain started.

**Boundary with the variant matrix.** `Bench.Domain/Variants/*` and `Bench.Application/Variants/*` landed
on 2026-08-16 as that plan's step 1, and its steps 2–11 are still open and still being worked.
**This plan touches none of those files.** It builds a structurally parallel catalog in new files,
mirroring their shape; the two axes meet only in the matrix, where a test crosses retrieval variants with
tool lanes — which is where "does retrieval help, and for which tool surface" finally becomes one grid
rather than two separate campaigns. The only ordering constraint is step 3 below, against that plan's own
step 4, and it is named there.

## 3. The shape — decisions

### 3.1 A lane is a catalog row, immutable and hashed

New table `lanes`, mirroring `variants` row for row. One row = one named tool surface.

```csharp
// Bench.Domain/Lanes/LaneDefinition.cs
public enum ToolPresentation { None, Bridge, McpStdio, CliNative, CliNativeWithMcp }

public sealed record LaneDefinition(
    IReadOnlyList<string> ToolNames,   // [] = every tool the surface offers, unfiltered
    string DescriptionSet,             // names a set on the MCP side; "" = each tool's compiled literal
    string Doctrine,                   // the ordering instruction — the 16.5-point axis, §3.2
    ToolPresentation Presentation,
    int MaxTurns);                     // 1 = a single-turn selection micro-task; > 1 = an agentic leg
```

- **Never edited.** Changing a wording mints a new row and retires the old one, exactly as
  `RetrievalVariant` does and for the same reason: every result names the lane it ran under, so an edit
  in place would silently relabel numbers already measured.
- **Unknown fields are refused, not dropped** — `JsonUnmappedMemberHandling.Disallow`, the discipline
  `VariantJson` already applies to retrieval axes.
- `Canonical` composes the tool names (ordinal-sorted), the description-set name, the presentation, the
  turn ceiling and `StableHash.Of(Doctrine)`. `Hash` is `StableHash.Of(Canonical)`. `Stamp` is
  `{name}#{hash[..12]}`.

**No `cells.LaneId` column, and that is a decision rather than an omission.** The variant catalog needed
a foreign key because *variant* was a brand-new axis. *Lane* has been an axis since the first matrix:
`RunCell.LaneName` already carries a stable, never-renamed identity, and a catalog changes what a lane
name **resolves to**, not how a cell stores it. So this ships with zero schema change to `cells` — one
fewer migration racing the parallel session. A `LaneId` may be added later for join speed; it buys no
correctness that the unique name does not already buy.

**Four hashes are stored as columns beside the JSON definition** — `ToolsHash`, `DescriptionSet`,
`DoctrineHash`, `Presentation` — so "which wording wins, holding the tool set fixed" is a `GROUP BY`
rather than JSON parsing in SQL. The definition JSON remains the source of truth; the columns are its
projection, written once at insert and never updated (the row is immutable, so they cannot drift).

### 3.2 The doctrine is `Lane.Preamble`, revived — not a new field

`Lane.Preamble` exists, is documented, and is read by nothing. `LaneDefinition.Doctrine` becomes its
value, and `LegRunner` finally sends it as `ModelRequest.SystemPrompt` instead of `string.Empty`
(`LegRunner.cs:76`). Reviving the dead field is cheaper than deprecating it and building a parallel
concept, and it makes the axis legible in the one place a reader already looks for it.

Three doctrines ship as the first measured arms, ported as **text, not as code**, from the series that
measured them (`DewFlow · prompts/benchmarks/eval_channels_{cover,first,late}.md`): *cover all channels*,
*retrieval first, then confirm, then read*, *investigate freely, then close the gaps with retrieval*.
They are the control: if this harness cannot reproduce a spread of roughly that size on a comparable
setup, the instrument is wrong before any new tool is judged by it.

**A doctrine's effect is not portable across presentations.** The bridge places it verbatim as a system
message; a CLI agent receives it through that CLI's own mechanism, whose effect on the model's real
context is not something this harness can confirm. Cross-presentation doctrine comparisons carry that
caveat; same-presentation is the trustworthy default (§3.10).

### 3.3 The surface is echoed, never assumed

A lane names a tool subset and a description set; the server resolves them. If the harness stores only
what it *asked for*, then "every result names the exact description text that produced it" is a hope.
This is the `QlnEngine` axes-echo discipline applied to tool surfaces, and it is also the L0 rung of the
ladder.

The sibling plan makes `Mcp.Host --print-surface` emit a `SurfaceFingerprint` and exit: tool names, the
exact description text served for each, the description-set name, and hashes over all of it. Then:

- `bench lanes verify --lane <name> --server <path-or-url>` runs it, compares against the lane's stored
  definition, and exits `0` match / `1` mismatch (a real regression in what is served) / `3`
  unreachable. This needs no model at all.
- At run start, the same fingerprint is fetched once per leg-process and recorded on the run. A mismatch
  **blocks the cell with a named reason** rather than measuring a surface nobody chose — the same move
  `PLAN_variant_matrix.md` §3.4 makes for an index whose commit does not match.

### 3.4 The tool loop — one collaborator, and the existing runner keeps its shape

`LegPlan` gains one field whose default is today's behaviour, so every existing caller and test compiles
and behaves identically:

```csharp
// Bench.Application/ToolSurface.cs — closed union, the LegOutcome shape
public abstract record ToolSurface
{
    public sealed record None : ToolSurface;
    public sealed record Looping(IEngine Engine, IReadOnlyList<EngineTool> Tools, int MaxTurns) : ToolSurface;
    public static ToolSurface Off { get; } = new None();
}

// LegPlan (LegRunner.cs:12-21) gains `ToolSurface Surface`; LegPlan.Reading(...) passes ToolSurface.Off
```

The conversation shapes are minimal additions to the model types:

```csharp
// Bench.Domain/Models/ModelTurn.cs
public sealed record RequestedToolCall(string Id, string Name, string ArgumentsJson);
public abstract record ModelTurn
{
    public sealed record Assistant(string Text, IReadOnlyList<RequestedToolCall> ToolCalls) : ModelTurn;
    public sealed record ToolResult(string ToolCallId, string ToolName, string Content, bool Refused) : ModelTurn;
}
```

`ModelRequest` gains `Tools` and `Transcript` with a new `OfTurn(...)` factory; the existing
`ModelRequest.Of(...)` (`Ports.cs:93-94`) keeps its signature and passes `[]` for both, so a no-tools
request is still exactly what it was. `ModelAnswer` gains `ToolCalls` and `IsFinal => ToolCalls.Count == 0`.
`OpenAiCompatibleRuntime.Body()` (`OpenAiCompatibleRuntime.cs:91-117`) folds the transcript into
`messages` and emits `tools:[…]` when the request carries any; `Read()` (`:119-139`) parses
`message.tool_calls[]`.

`ToolLoopRunner` (new, `Bench.Application`) owns the loop so `LegRunner.RunAsync` stays a dispatch:
ask → if final, done → otherwise invoke each requested call through `IEngine.InvokeAsync`, append the
`ToolAnswer` as a tool message, next turn. Per call it records a `Trace.ToolCall` with **outcome**, not
size — `ToolAnswer.WasRefused` is already the right shape, and "a refused call and an executed one were
indistinguishable" is the defect that made a false read-only guarantee stand for months.

**The `Turns` budget is confirmed by the loop, never by the runtime.** The runtime is right to refuse it
and must keep refusing it; `ToolLoopRunner` is the component its refusal message names. A leg that
exhausts its turns settles as `LegOutcome.CapExceeded(BudgetKind.Turns, …)` — never `Crashed`, never a
wrong answer — which keeps it out of paired deltas through the existing `CountsInPairedDelta`
(`Budgets.cs:58-60`). This mirrors, one arm wider, the `WasCutOff` handling already at
`LegRunner.cs:127-130`.

**Warn before spending.** `PlanRun` already warns on a billable cloud subject; a looping lane with no
cost ceiling gets the same warning. An agentic loop against a billed model is the one configuration here
that can spend without bound.

### 3.5 Two kinds of tool-call record, and they are never blended

| | directly observed | reconstructed |
|---|---|---|
| who saw it | `ToolLoopRunner` — the harness drove every turn | the MCP server's spool, joined by `TelemetryCorrelation` |
| lanes | `Bridge`, `McpStdio` | `CliNative*` — the CLI runs its own loop and the harness cannot see inside it |
| carries | arguments, outcome, duration, and the turn it happened on | arguments, outcome, server time, scope — but no turn ordering relative to the model's thinking |
| arrives | during the leg | after `bench telemetry ingest`, in a later pass |

Every stored tool call carries which of the two it is. A report may show them side by side and may never
average them together — the same rule `architecture.md` already states for the bench trace against the
server telemetry, one level down. A CLI-agent leg is therefore settled on what is directly observable
(final answer, exit, wall time) and **re-scored later** for tool usage, exactly as `JudgeRunner` re-scores
stored answers without re-running a leg.

### 3.6 The ladder: L0, L1, L2 — and L1/L2 are one mechanism at two turn ceilings

- **L0 — does the tool work at all, no model involved.** Ordinary xUnit tests in whichever repository
  owns the tool, plus `bench lanes verify` (§3.3) for "is the surface actually serving what the lane
  says". A tool that fails L0 never reaches a model, and a benchmark number produced against a broken
  tool is worse than no number.
- **L1 — does a model pick it, and can it form the arguments.** A suite of one-turn micro-questions run
  under a lane with `MaxTurns = 1`. The model is given the surface and a situation; the expectation is
  about **which tool it called**, not about the prose it produced. Cheap, deterministic, and it isolates
  the failure this whole plan exists to catch: a good tool nobody calls.
- **L2 — does it help.** Real tasks, `MaxTurns` high enough to finish, the same expectations plus the
  answer expectations already in every suite. Compared against the native-tools lane and the no-tools
  floor.

L1 and L2 are the same code path at different `MaxTurns`. That is the point: a ladder made of three
mechanisms would have three sets of bugs, and the rung that measures "did it pick the tool" must be the
same instrument as the rung that measures "did picking it help".

**Reading saturates, so L2 alone is not enough.** Group 6 of the question bank — fixing a real bug —
stays where it is, in [PLAN_code_lane.md](PLAN_code_lane.md). This plan supplies the loop and the phases
it will run through; it does not duplicate its sandbox or its scoring.

### 3.7 Two expectation kinds, and the not-applicable honesty rule

```csharp
public enum ExpectationKind { File, Member, AnswerContains, AnswerExcludes, ToolUsed, ToolNotUsed }
```

The tool name rides in the existing `Expectation.Text` field — no new field, and **no change to
`SuiteJsonLoader` at all**: `ToExpectation` parses the kind with a case-insensitive `Enum.TryParse` and
passes `Text` straight through (`src/Bench.Application/SuiteJsonLoader.cs:61-73`), so
`{"kind": "ToolUsed", "text": "rt_read_local_file"}` loads the day the enum values exist.

**One defect found while verifying that, and it gets fixed here because this plan makes it likelier.**
That same parse falls back to `ExpectationKind.File` when the kind does not match
(`SuiteJsonLoader.cs:63-65`), so a misspelt `"ToolUsedd"` is not refused — it silently becomes a file
expectation against an empty path, which then scores as a retrieval miss the suite author never wrote.
Every other unknown value in this system is refused by name (`VariantJson`'s `Disallow`, the trace
contract's unknown stage, the telemetry codec's unknown version); this one is not, and adding two new
kind names is exactly the change that turns a latent typo trap into a live one. An unknown expectation
kind must refuse the suite, naming the kind and the question. RED test first, per the repository's rule.

Scoring mirrors `RetrievalObservation` member for member:

```csharp
public sealed record ToolUsageObservation(bool Available, IReadOnlyList<string> ToolsCalled)
{
    public static ToolUsageObservation None => new(false, []);
    public static ToolUsageObservation Of(IReadOnlyList<string> names) => new(true, names);
}
```

`AnswerScoring.Score` gains the observation as a parameter and emits one metric per tool expectation.
**A `ToolUsed` expectation in a lane with no tools is *not applicable*, never a miss** — the identical
rule `RecallMetric` already applies at `AnswerScoring.cs:96-104`, and for the identical reason: the
no-tools floor exists to be compared fairly, and scoring it zero for not calling a tool it never had
would make the baseline look worse than it is and flatter every tool lane by exactly that much.

`ToolNotUsed` is the trap half, and it matters as much: a description that makes a model call a tool
where it should not have is a defect in the description, and it is invisible unless something asserts
the negative.

**Per-question tool affinity.** `Question` gains an optional `ToolAffinity` label (empty by default) —
"this question is one `graf_`-shaped question", "this one is a literal lookup". It is what turns the
operator's question (b) from *"is the tool better on average"* — which the measured answer says is
roughly a wash — into *"on which kind of question is it better"*, which is where the one violent
inversion in the record lived (8/8 in 254 s against 0/8 in 1 058 s on a single task).

### 3.8 What may be compared with what

A leaderboard over wordings is only meaningful inside one tool set and one presentation. Ranking a
verbose description on an 18-tool bridge lane against a terse one on a 4-tool stdio lane attributes to
wording what belongs to the surface — which is precisely the confound the 9× finding is made of.

So a comparison scope is computed and **refused by name** when it is mixed: same `ToolsHash`, same
`Presentation`, same subject, same suite, same target. Everything else is an axis to compare along.
Beside it, the existing rule stands unchanged: `n ≥ 2`, and a difference inside the repeat spread is
reported as unproven rather than as a result.

### 3.9 What is persisted

Two migrations, both additive, neither touching a column the parallel session is writing:

```
lanes       Id uuid PK, Name text UNIQUE, DisplayName text, DefinitionJson jsonb, Hash text,
            ToolsHash text, DescriptionSet text, DoctrineHash text, Presentation text,
            CreatedAt timestamptz, RetiredAt timestamptz          -- RetiredAt default = active
tool_calls  Id uuid PK, ResultId uuid FK, Ordinal int, Turn int, Phase text,
            ToolName text, ArgumentsJson text, Refused bool, Error text, DurationMs bigint,
            Source text                                           -- observed | reconstructed (§3.5)
runs        + SurfaceFingerprintJson text                         -- what the server actually served (§3.3)
```

The doctrine text itself lives in `lanes.DefinitionJson`, so a published database explains its own
numbers without a second artefact. Nothing stores an absolute local path, a token or a machine name —
the database must survive publication unedited, per the founding rule.

### 3.9a What the operator asked for, and what it costs (2026-08-19)

Stated during step 2, and it is a requirement rather than a preference: **the UI must list the tools, let a
human pick ONE and test whether it works, and show the prompts that were actually sent.**

Two of the three are already paid for and one is not:

| the ask | where it comes from |
|---|---|
| a list of tools | `runs.SurfaceFingerprintJson` (§3.3) — what the server actually served, not what a lane asked for |
| pick one and test it | the L1 rung (§3.6) narrowed to one tool: a lane whose `ToolNames` is that one tool, at `MaxTurns = 1`. No new mechanism — a lane already IS a tool subset |
| **see the prompts sent** | **partly missing.** `LegResult.Prompt` already stores the assembled user prompt (the prompt · answer · thinking trio is called the artefact for exactly this reason), the doctrine is in `lanes.DefinitionJson`, and the advertised tools are in the fingerprint. What nothing holds is the middle of a loop: each turn's assistant prose and each tool's returned CONTENT |

So the consequence for the build order is one sentence, and it lands in step 3 rather than waiting for the
UI: **`ToolLoopRunner` keeps the transcript it built instead of discarding it after the last turn.** Storing
it is step 7's migration; throwing it away in step 3 would make step 7 a re-run rather than a write.

What this deliberately does **not** do is store a second copy of the system prompt or the tool list per leg.
Both are already recoverable — from the lane the cell names and the fingerprint the run recorded — and a
second copy is a second thing that can disagree with the first about what was sent.

### 3.10 The reports, and the three questions they answer

Every one of these reads existing tables plus the two above; none needs a new store.

| operator question | the report |
|---|---|
| (a) does tool T work at all — **including one tool a human picked**, §3.9a | `bench lanes verify` (L0, no model) and the L1 rollup: per tool, how often it was *offered*, *called*, *refused*, *errored*, and how often its arguments were rejected. A tool with a healthy L0 and a zero call rate is the headline finding, not a footnote |
| (b) is T better than native, and where | paired deltas per `ToolAffinity` group between two lanes at the same question and repeat, using only `Completed` legs — the existing `CountsInPairedDelta` rule. Reported per group, with totals shown last, because the totals are what hid the inversion last time |
| (c) which description / doctrine wins | a leaderboard inside one comparison scope (§3.8), with the repeat spread printed beside every mean and any gap inside it labelled unproven |

`AverageByAsync(ReportDimension.Lane)` is what the lane rollup is built on. *(Corrected 2026-08-19: this
read `AverageByLaneAsync`, which has since been generalised into one dimension-taking method — the axis
the rollup needs is a member of an enum now, not a fifth near-identical port method.)*

### 3.11 How the harness reaches the surface: a submodule

`dew_flow_benchmark` vendors `dew_flow_mcp` at `external/dew_flow_mcp` and project-references
`Mcp.Contracts`, `Mcp.Application`, `Mcp.Bridge`, `Workspace.Application`, `Workspace.Infrastructure`
from `Bench.Infrastructure`. The precedent is exact: `dew_flow_rag_qln` already does this
(`hosts/Daemon/Daemon.csproj:26-30`), and the direction is the allowed one — the measurer references the
measured, never the reverse.

This is what makes the `Bridge` presentation honest. The in-process bridge is not a transport detail; it
is the arm that scored 36/63 where its wire twin scored 4/63, and measuring it through an HTTP hop would
measure a third thing. `McpBridgeEngine : IEngine` therefore constructs a real `ToolCatalog` in this
process and dispatches through `LocalLlmToolBridge` — the same code a customer's in-process host runs.
`McpStdio` spawns `Mcp.Host --stdio` per leg and speaks the protocol; `CliNativeWithMcp` hands that same
subprocess to a CLI agent as its own MCP server.

Explicitly rejected: putting a conversation runner into `dew_flow_mcp` so the benchmark could drive a
model over one HTTP call. That repository is public and is a tool server; a loop that drives models is
half a harness, and it would also duplicate the hashing that §3.3 relies on into a second codebase that
must agree byte for byte.

### 3.12 What this plan deliberately does not do

- **No question bank, no model registry, no console shell** — those are [PLAN_variant_matrix.md](PLAN_variant_matrix.md)'s
  §3.3 and §3.7 and §3.6. This plan's UI pages mount in the console that plan builds; if it has not
  landed when step 9 is reached, the pages wait rather than a second shell appearing.
- **No fix-lane execution** — [PLAN_code_lane.md](PLAN_code_lane.md) owns the sandbox, the mechanical
  signals and the delivered-work score. The two meet at `PhaseKind` and at `ToolSurface`, nowhere else.
- **No new judge** — L1 is mechanical by construction; L2 uses the arbiters that already exist. A
  self-judged verdict stays marked, per the 6-of-6 against 0-of-6 finding.
- **No editing tools anywhere** — the measured surface stays read-only, per the sibling repository's own
  Phase 4 boundary.

## 4. The cross-repository contract

What this plan needs from `dew_flow_mcp · research/PLAN_tool_surface_config.md`, named identically in both.

> **All five shipped 2026-08-16** — that plan is now IMPLEMENTED and lives in its repository's
> `research/`. Two things it decided differently, both of which this side must know:
>
> - **The fingerprint carries no `BuiltAt`.** Deterministic builds leave no honest build timestamp, so
>   it reports `version` as a `captured / value / reason` triple (the assembly's informational version,
>   which resolves to `1.0.0+<sha>`) plus `takenAt`. Read the two hashes, not a build time, when
>   deciding whether two runs saw the same surface.
> - **`correlation` is ALWAYS written**, unattributed included, with the *same* reason strings this
>   repository substitutes for a missing object — so "the line predates the field" and "the caller
>   declared nothing" stay one fact rather than two.
>
> Verified live against `TelemetryCodec.ReadLine` here: a real emitter line with a correlation parses
> and reads as attributed. One consequence for this repository: `Fixtures/mcp-spool-v0.jsonl` is
> documented as being REPLACED from a fresh emitter run whenever the emitter's shape changes, but
> `A_line_written_before_correlation_existed_still_reads_and_reads_as_unattributed` needs a
> pre-`correlation` line. **One file can no longer be both — this needs a second fixture, not a
> replaced one.**
>
> **Done 2026-08-17.** Two fixtures now: `mcp-spool-v0-precorrelation.jsonl` (the old file, `git mv`-d so
> its provenance survives) and `mcp-spool-v0-correlated.jsonl`, emitted by a new `SpoolFixtureTests` in
> `dew_flow_mcp` that writes it beside its own test binary for exactly this copy — still the real emitter,
> never hand-authored. `Fixture` exposes both plus `BothShapes`, and two tests were added: the correlated
> line reads as attributed, and both shapes agree on everything except the field that distinguishes them.
>
> The split earned itself immediately. With `TelemetryCodec`'s correlation mapping deliberately broken to
> `TelemetryCorrelation.None`, the new test failed — *"Expected record.Correlation.IsAttributed to be True
> … but found False"* — and **the pre-correlation test still passed**. A reader that silently discarded
> the field was green under the single-fixture arrangement, which is precisely the hole a second fixture
> closes.
>
> **The end-to-end path is also proven on live traffic now** (it had only ever been proven from the
> emitter's own test output). A real MCP client drove the shipped `Mcp.Host` over stdio; the production
> sink wrote two records; `bench telemetry ingest` reported *"ingested 2, duplicate 0, refused 0"* and
> retired the file, a re-run said *"nothing to ingest"*, `bench telemetry report` rendered
> `rt_read_local_file · live-check/?/stdio · 2 calls · 1 ans · 1 ref · p50 0.8 ms`, and the rows in
> Postgres carry `Leg = cell-live` / `Phase = verify`.

1. **A tool subset chosen at process start** — `--tools a,b,c`; unset means every tool, today's behaviour.
2. **Descriptions from a file catalog** — `--descriptions <dir> --description-set <name>`; a missing or
   empty file falls back to the compiled literal, which is never empty.
3. **`--print-surface`** — emit the `SurfaceFingerprint` (tool names, exact description text per tool,
   set name, hashes) and exit. This is L0's instrument and the run-start echo.
4. **`--correlation <legId[/phase]>`** — stamp every telemetry record this process emits, additively
   within `telemetry/v0`. Honest only for a per-leg process; the shared HTTP transport must not use it.
5. **Parity holds through the configuration** — the protocol surface and the bridge must still advertise
   byte-identical schemas after a subset and a description set are applied.

## 5. Build order

Each step ships alone, tests green, before the next. **CLI and API first, UI strictly after the endpoints
it renders** — the API-first gate the family already applies.

1. ~~**Lane catalog**~~ **DONE 2026-08-19.** `Bench.Domain/Lanes/*`, `LaneJson`, `ILaneCatalog` +
   `PostgresLaneCatalog`, the `lanes` table and its migration, and `bench lanes add|list|retire`. 37 tests,
   eight of them against a real Postgres. It touched no file under `Variants/`, as promised.

   **One deviation: there is no `LaneSelection` type.** The step asked for "definition, catalog entry,
   selection, slug", and the selection turned out to exist already — `ToolLane.Select()` returns the
   EXISTING `Lane(Name, Preamble)` axis record with its dead field finally set to the doctrine. A parallel
   selection type would have been a second way to say what a leg's lane is, beside the one every cell
   already stores.

   **A defect this step's own tests produced, worth keeping.** The listing fails TOTALLY on one unreadable
   row — deliberately, since skipping it renders a catalog quietly missing a surface somebody is measuring
   against. The test that asserts this left its broken row behind, and every other listing test sharing the
   Postgres fixture failed. The guarantee and the pollution are the same property; the test now removes the
   row it broke, and says why in place.

   **And a fourth copy of one helper was not written.** `Reason<T>()` on `Outcome` was already private in
   three CLI commands; it is now `OutcomeText` and the three copies are gone.
2. ~~**Multi-turn model plumbing**~~ **DONE 2026-08-19.** `ModelTurn`, `RequestedToolCall`,
   `ModelAnswer.ToolCalls`/`IsFinal`, `ModelRequest.Tools`/`Transcript`/`OfTurn`, and the runtime's body and
   parse. **The proof held: all 16 existing runtime tests passed unchanged**, and 10 were added beside them.

   Every addition is an `init` property with an empty default, following `ModelAnswer.Thinking`'s own
   precedent, so no existing construction moved. `tools` is **absent** rather than empty when a lane offers
   none — `tools: []` is a different request at several endpoints, and the no-tools arm is the floor
   everything else is measured against.

   **Argument JSON is never re-serialized, on either side.** A local model emits broken JSON regularly, and
   "can it form the arguments" is one of the three questions this plan exists to answer; a parse-and-rewrite
   would repair the mistake on its way in and make the observation impossible. Watched failing: adding a
   four-line "repair" turned the test red immediately.

   Two decisions the plan did not name. A tool whose advertised schema is not JSON is **refused before the
   HTTP call**, so a broken lane records a configuration fault rather than an unreachable model. And a turn
   that asks for a tool while saying nothing reports *"this turn asked for a tool rather than answering"*
   instead of *"the response carried no message content"* — otherwise every multi-turn leg would carry a
   fault in its record for behaving normally.
3. ~~**`ToolSurface` + `ToolLoopRunner` + `LegRunner` wiring**~~ **DONE 2026-08-19.** The doctrine reaches
   `SystemPrompt`, the turn ceiling is confirmed by the loop and settles as `CapExceeded(Turns)`, and 11
   tests cover it. `LegRunner` stayed the assembly it was: the loop scores nothing, persists nothing and
   settles nothing.

   **One deviation, and it is not small: `LegPlan` gained a `LaneRoster`, not a single `ToolSurface`.** The
   step as written put one surface on the plan, which works only while a run measures one lane — and the
   plan's own headline experiment is three doctrines, which is three lanes in ONE run. A single surface
   would have sent every leg through the first lane and labelled the results with the cell's, the exact
   defect the subject roster was introduced to end in another axis. The roster mirrors `VariantRoster` row
   for row, resolves by the lane name a cell already carries, and refuses an unresolved lane rather than
   falling back to the first.

   **A defect its own tests found.** The request carried the growing transcript by REFERENCE, so the record
   of what was sent changed after it was sent — every turn would have rendered as if it had carried the
   whole conversation. It matters more now than it would have last week: §3.9a's "show me the prompts" reads
   exactly that record. Fixed with a per-turn snapshot.

   The parallel session was in `Runs/` throughout (the arm axis) but never in `LegRunner`; the two test
   files that construct the runner directly gained the new collaborator, and no test BODY moved.
4. ~~**Tool expectations and scoring**~~ **DONE 2026-08-19.** `ExpectationKind.ToolUsed/ToolNotUsed`,
   `ToolUsageObservation`, the `AnswerScoring` overload, `Question.ToolAffinity`. Pure domain, no
   infrastructure, 19 tests.

   **The §3.7 loader fix reversed a decision that was PINNED, which §3.7 did not know.** The fallback was not
   an oversight: `An_unrecognised_expectation_kind_falls_back_to_File_rather_than_failing_the_batch` asserted
   it deliberately, and the trade was defensible — one bad entry should not cost a whole suite, and with four
   kind names that look nothing like each other a typo was unlikely. Two things changed it. `ToolUsed` and
   `ToolNotUsed` differ from a misspelling by one character, so the typo stopped being unlikely; and the cost
   was never "one loose expectation" but a `File` anchor against an empty path — a retrieval miss the author
   never wrote, scored forever, silently. The old test was replaced by its opposite with the reversal
   recorded in it, rather than deleted.

   Watched RED first: four failures, all `Ok` where `Fail` was expected. And the guard went where the defect
   actually lives — `QuestionJson.ToExpectation`, reached by BOTH a suite read from JSON and a question
   rehydrated from a bank row, so the refusal had to be threaded through three call sites (`SuiteJsonLoader`,
   `BankImport`, and `AuthoringPass`, where an unreadable candidate becomes a rejection with its reason
   rather than a throw in a model-driven pass).

   `ToolUsageObservation` mirrors `RetrievalObservation` member for member, including the fairness rule: a
   tool expectation in a lane with no tools is **not applicable**, never a miss, emitted as TEXT so the
   numeric aggregate reports a smaller denominator instead of a diluted mean. Repeat calls are kept — "it
   called search four times" and "once" are different facts about how a model works a surface.
5. ~~**Sibling repository, steps 1–2**~~ **DONE 2026-08-16, in `dew_flow_mcp`** — all five §4 items,
   not just 1–3 and 5: `--correlation` shipped in the same pass. Nothing is owed by that side.
6. **What steps 1–4 revealed the engine is NOT: the blocker.** Checked against the code on 2026-08-19
   after the four steps landed, and the plan's order was wrong in a way worth writing down. Three things
   stand between a built loop and a first agentic leg, and vendoring is the last of them:

   1. **No engine advertises a real JSON Schema.** Both `FilesystemEngine` and `QlnEngine` described their
      arguments in a shorthand — `{"path":"string","startLine":"int?"}` — which is valid JSON and is not a
      schema: no `type`, no `properties`. Step 2's runtime parses it and sends it as `parameters`, so a model
      would be handed nonsense and the symptom would read as *"the model cannot use tools"* rather than *"we
      sent it a broken schema"*. **Everything measured before this is a measurement of our own defect**, which
      is why it goes first. The guard written in step 2 does not catch it either: it checked that the schema
      was valid JSON, and the bar is that it is a SCHEMA.
   2. ~~**Nothing resolves a lane name into a surface.**~~ **DONE 2026-08-22.** `bench run --lanes <names>`
      joins them: every named lane is an arm of the matrix, resolved before a cell exists.

      **The engine factory this item predicted does not exist, and should not.** The reasoning that called
      for one was right about the constraint — the engine must be rooted at the run's pinned checkout, a
      path unknown when the container is composed — and wrong about the conclusion. The engine is
      constructed in the plan path and rides inside `ToolSurface.Looping`, which is where `LegRunner`
      already reads it from; nothing needs it from DI, so a factory would have been a port with one
      implementation serving one caller.

      **Two orderings were wrong on the first attempt, both caught by tests rather than by review.** The
      tree was checked before the catalog, so a typo'd lane name under `--no-checkout` was answered with a
      complaint about the checkout and the operator would fix the wrong thing — and a test written for the
      unknown-name refusal could never reach the lookup at all. And the tree was demanded of EVERY lane,
      including a floor lane, which is the one lane that provably needs no tree: "no tools, but read
      carefully" is a legitimate arm, and refusing it for want of a checkout denies the floor every tool
      claim is measured against.
   3. **Then the submodule and `McpBridgeEngine`** — vendor `dew_flow_mcp`, add the project references,
      implement the engine, `bench lanes verify` against `--print-surface`. Still required for the arm that
      matters most (4/63 against 36/63 must be measured genuinely in-process) and still **not** a
      prerequisite for the first agentic leg: `FilesystemEngine` already serves four real tools and IS the
      native-tools baseline that scored 36/63.

   **And one defect fixed on the way, found by reading rather than by running.** `LegRunner` computed
   `LegDeadline.ForCall` ONCE, outside the turn loop, so every turn was handed the wall as it stood before
   turn one — defeating the mechanism the type's own doc comment describes, that narrowing the wall to the
   remainder "is what makes twenty-five turns share one ceiling instead of each starting a fresh one". A
   25-turn lane against one hanging endpoint would have spent 25 walls: 4 h 10 m where 10 minutes was
   declared, the same arithmetic `LegDeadline` uses to argue for its own existence. The loop now takes the
   deadline plus a clock, recomputes the remainder per turn, and stops BETWEEN turns when the wall is gone —
   as a FAILURE, so the existing `UnansweredAsync` settles it as a wall `CapExceeded` rather than a crash and
   the campaign continues past it. `bool Exhausted` on the result became `enum LoopEnd { Answered,
   TurnsSpent }` in the same change, so a reader does not have to know which way `true` pointed. No campaign
   had run long enough to expose it, which is the whole point: an unenforced budget is indistinguishable from
   a working one until the day it costs a week.

   **First end-to-end milestone** is therefore reachable one step earlier than this plan assumed: a local
   model through the filesystem engine, temperature 0 and seed as-sent, tool calls recorded with outcomes.

   **THE FIRST AGENTIC RUN HAPPENED, 2026-08-22, and the number is the one this whole repository was
   built to produce.** `Gemma4-26B-A4B-Uncensored:latest` over Ollama's OpenAI-compatible route, nine
   `code-lookup` questions against `dew_flow_rag_qln` at `0daa9254`, both lanes in ONE run so the pairing is
   within-run rather than across two:

   | lane | legs | passed every expectation | failed metric assertions |
   |---|---|---|---|
   | `fs-bridge` — four filesystem tools, locate-first doctrine, 12-turn ceiling | 9 | **6** | 3 |
   | `floor` — no tools, no doctrine | 9 | **0** | 12 |

   Same model, same questions, same seed, same wall. 18 legs in 6.5 minutes, 0 refused, 0 faulted. A
   single-question probe run first isolates it further: the identical question passed on `fs-bridge` and
   failed on `floor`, naming `src/Rag.Application/Runtime/StoreNaming.cs` and a PRIVATE method the model
   could not have known — this repository is not in anyone's weights.

   **The unplanned observation, and the more interesting half.** Floor answers average **3 291 characters**;
   tool answers average **417**. Given nothing to read, the model writes long speculative prose; given a
   tree, it answers short and specific. Nobody predicted that, and it means answer LENGTH is a candidate
   signal for "answered from weights" that costs nothing to record.

   **What that first run could not tell you — now BUILT (step 7).** At the time the harness had no record of
   WHICH tools were called or in what order — `ToolLoopResult.Calls` reaches the scorer for the metric and is then
   dropped, because nothing persists it yet (step 7). So this measures that the surface moved the score, and
   says nothing about how it was worked. The doctrine's whole claim — locate before you read — is currently
   unfalsifiable for want of a ledger.

   **And with the ledger built, the doctrine was tested — and is INERT for this subject (2026-08-22).**
   A third lane, `fs-mute`: the identical four tools, the identical 12-turn ceiling, and **no doctrine at
   all**. The prediction was written down before the run, per this family's own rule — *a 26B tool-trained
   model probably searches first by instinct, so expect the ordering largely unchanged (≤2 of 9 legs opening
   with a blind read) and the doctrine's effect small.*

   | lane | passed of 9 | legs opening with a blind `read_file` | tool calls | mean answer chars |
   |---|---|---|---|---|
   | `fs-bridge` — locate-first doctrine | 6 | **0 of 9** | 27 (3.0/leg) | 417 |
   | `fs-mute` — same tools, no doctrine | **7** | **0 of 9** | 23 (2.6/leg) | 1 218 |
   | `floor` — no tools | 0 | n/a | 0 | 3 291 |

   Observed: **0 of 9 either way.** Every leg in both lanes opened with `search_literal` or
   `list_directory`; not one opened by reading a file it had guessed at. The doctrine's entire stated claim —
   *locate before you read* — is one this model already satisfies unprompted, so the instruction had nothing
   to add. It cost ~15 % more tool calls and scored one leg lower, which on n=9 is noise and certainly not
   support.

   **What this does and does not say.** It does NOT refute doctrine-wording effects: the measured 16.5-of-63
   swing that motivated this whole axis came from a different model over a different tool set. It says that
   *this* doctrine, on *this* subject, bought nothing — and that is a result only the ledger could produce,
   because without a record of call ORDER the claim was unfalsifiable and would have been carried forward as
   a design belief. The obvious next arm is a doctrine whose claim the model does NOT already satisfy.

   **The answer-length gradient held and sharpened**: 3 291 chars with no tools, 1 218 with tools and no
   doctrine, 417 with tools and the doctrine. The doctrine did change the model's behaviour measurably — it
   made it terser — it just did not change the ordering it was written to change.

   **L1 — and the "inert" verdict above is now properly bounded (2026-08-22).** Six one-turn micro-tasks
   whose expectation is WHICH tool was called (`samples/l1-tool-choice-suite.json`), three wordings, one run.
   The third doctrine is adversarial on purpose — *read first, search later* — because three similar-good
   wordings cannot tell an inert channel from a redundant instruction. Prediction written first: no-doctrine
   high, locate-first equal or better, read-first breaks the behaviour-shaped question.

   | question (wanted) | no doctrine | locate-first | read-first |
   |---|---|---|---|
   | behaviour, no path given (`search_literal`) | ✔ | ✔ | ✘ `list_directory` |
   | project files by name (`find_files`) | ✔ | ✔ | ✔ |
   | list the root (`list_directory`) | ✔ | ✔ | ✔ |
   | an exact identifier (`search_literal`) | ✔ | ✔ | ✔ |
   | file NAMES not content (`find_files`, not `search_literal`) | ✔ | ✔ | ✘ **no call at all** |
   | a file whose path was GIVEN (`read_file`) | ✘ `list_directory` | ✔ | ✔ |
   | **correct tool choice** | **5 / 6** | **6 / 6** | **4 / 6** |

   **The doctrine channel is not inert — the L2 doctrine was REDUNDANT.** Wording moved the tool picked on
   two questions in each direction, monotone and in the predicted order. The distinction matters: at L2 the
   model already ordered its calls correctly on those nine questions, so an instruction telling it to do what
   it already did could only cost turns. At L1 the same doctrine fixed the one question where the default was
   wrong — the model listed a directory instead of reading a file whose exact path it had been handed.

   **And the adversarial arm produced the failure this whole plan exists to catch, on demand**: on the
   file-names question, *read first* produced **no tool call at all**. "A good tool nobody calls" is not a
   hypothetical the harness has to wait for — a bad doctrine manufactures it.

   **What is still NOT calibrated.** The plan's DoD asks for a spread larger than this harness's measured
   REPEAT NOISE, and repeat noise has not been measured — n = 6, one model, one seed. A monotone 6/5/4 with a
   legible mechanism is a reason to keep the axis, not a calibration. The plan's own control (three channel
   doctrines over an 18-tool RAG surface) still needs the QLN engine wired to a lane, and a filesystem
   surface is not a comparable setup for it.

7. **`tool_calls` persistence + the run's surface fingerprint** — the two migrations, the observed/
   reconstructed source flag, result-store round-trip.
8. **L1 suite + the control** — the micro-task suite, and the three doctrine lanes run as the
   instrument's own calibration. If the doctrine spread does not appear, stop and fix the harness before
   judging any tool with it.
9. **API + reports** — the lane, tool-usage, affinity-delta and doctrine-leaderboard endpoints in
   `Bench.Api`, plus `bench report` verbs over the same use cases. Comparison-scope refusal lands here.
10. **UI** — `Bench.Ui` pages over the step-9 endpoints only: lanes catalog with the definition echoed,
    a tool-health page answering (a), an affinity page answering (b), a wording leaderboard answering (c)
    with the unproven label rendered, and a leg detail showing every tool call with its outcome and its
    source. Mounted in the console shell `PLAN_variant_matrix.md` §3.6 builds.

    **Three things §3.9a requires of it explicitly**, since they were asked for by name: the tool list comes
    from the run's SURFACE FINGERPRINT rather than from the lane's request, so the page shows what was
    served; picking one tool to test is a lane with that one name at `MaxTurns = 1`, offered from the page
    rather than typed as JSON; and the leg detail renders **what was sent** — the doctrine from the lane, the
    user prompt from the result, the advertised tools from the fingerprint, and each turn's own messages from
    the transcript step 3 keeps and step 7 stores.
11. **`CliAgentRuntime` + telemetry correlation** — the second subject: a cloud CLI headless over the
    worktree with its native tools, and the per-leg `Mcp.Host --stdio --correlation <cell>` attached; the
    reconstruction pass that turns ingested spool rows into `tool_calls` with `Source = reconstructed`.
    Gated, as `PLAN_variant_matrix.md` §3.8 already states, on the retrieval tool existing at all — an
    agent lane over an empty surface measures nothing.

## 6. Test plan

- xUnit v3 executables only, never `dotnet test`; `PostgresFixture`/Testcontainers for every table.
- **Domain**: lane immutability and retire-not-edit; an unknown definition field is refused by name;
  `Canonical`/`Hash` stability (the same definition hashes identically across processes — the property
  `StableHash` exists for); a doctrine edit produces a different hash.
- **Loop**: a fake `IEngine` and a fake runtime prove — a final answer ends the loop; a refused tool call
  is recorded as refused and the loop continues; malformed argument JSON comes back as a tool failure the
  model can retry rather than an exception; turns exhausted settles `CapExceeded(Turns)` and **not**
  `Crashed`; tool time lands in `TimeBuckets.Tools`.
- **Scoring**: `ToolUsed` in a no-tools lane renders *not applicable* and does not fail the leg — the
  test that pins the fairness rule; `ToolNotUsed` fires when the tool was called; an unanswered leg is
  not scored as a wrong one.
- **Loader**: a suite naming an unknown expectation kind is refused, and the message names the kind and
  the question. Watched RED against today's silent `File` fallback before the fix — the failure message
  must show a `File` expectation where a `ToolUsed` was written, which is the actual symptom.
- **Backward compatibility**: the existing `LegRunner` and `OpenAiCompatibleRuntime` tests pass with no
  edit — a changed assertion there means the addition was not additive.
- **Comparison scope**: two lanes with different tool sets are refused for a wording leaderboard, by name.
- **Surface echo**: a fingerprint that disagrees with the lane blocks the cell with a reason, and the
  reason names both sides.
- **Engine (L0)**: `McpBridgeEngine` advertises exactly the subset it was configured with, and a call to
  a tool outside the subset is refused rather than dispatched.
- Every defect found while building gets its RED test first, watched failing for the real symptom.

## 7. Definition of Done

- [ ] A lane is a catalog row; adding a wording, a doctrine or a tool subset needs no migration and no
      recompile of the runner.
- [ ] The doctrine text reaches the model — proven by a test that asserts the outgoing system prompt,
      not by reading the configuration.
- [ ] `bench run` drives a real tool loop; every tool call is stored with its **outcome** (answered /
      refused / failed), its duration, its turn, and whether it was observed or reconstructed.
- [ ] A leg that exhausts its turns settles `CapExceeded`, is excluded from paired deltas, and is never
      scored as a wrong answer.
- [ ] A `ToolUsed` expectation in a no-tools lane reads *not applicable*, and the no-tools floor is
      therefore not penalised for lacking tools.
- [ ] An unknown expectation kind refuses the suite by name instead of silently loading as a `File`
      expectation.
- [ ] Every result names the exact surface that produced it — tool names, description-set, description
      text hash, doctrine hash, presentation — and a mismatch between what was asked for and what was
      served blocks the cell instead of measuring it.
- [ ] The three doctrine arms reproduce a spread larger than the measured repeat noise on this harness,
      or the harness is fixed before any tool is judged by it.
- [ ] The three operator questions are answerable from the CLI, from the API, and from a UI page, in
      that order of arrival.
- [ ] Rows are publication-safe: no local path, no secret, no machine name.
- [ ] `todo/README.md` updated; `research/architecture.md` records the new submodule edge and the loop;
      the sibling plan's DoD is met on its side.

## 8. Open questions

1. **Which CLI agents, and with what headless flags.** Claude Code first, per the operator. The exact
   invocation, the MCP-config injection point, and what each CLI exposes as "thinking" are measured at
   step 11 against the real binaries, not guessed here — the same call `PLAN_variant_matrix.md` §8.2
   already makes.
2. **Whether a tool's JSON Schema is itself an axis.** A schema with described parameters against a bare
   one is plausibly as large an effect as the description prose, and nothing in the record measures it.
   Cheap to add later — it is another field of a description set — and deliberately not in scope now.
3. **How many turns is a fair ceiling per presentation.** A CLI agent's own loop and our
   `ToolLoopRunner` do not count turns identically, so a shared number may quietly favour one. Left as a
   per-lane value until there is data; the comparison-scope rule (§3.8) keeps a mixed comparison from
   being reported as a clean one in the meantime.
