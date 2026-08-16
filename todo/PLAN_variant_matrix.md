# PLAN — the variant matrix: a question bank in five groups, reviewer marks, engine axes in the grid, and the console

> Status: **plan only, nothing implemented yet.** Scope: `Bench.Domain`, `Bench.Application`,
> `Bench.Infrastructure` (persistence + engines), `Bench.Api`, a NEW `hosts/Web` + `hosts/Web.Client` +
> `src/Bench.Ui`, `hosts/Cli`. The engine-side half lives in the sibling plan
> `dew_flow_rag_qln · todo/PLAN_search_variant_axes.md` — a change that crosses the repository boundary is
> named in both plans.
>
> Related: [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) (founding plan),
> [../research/architecture.md](../research/architecture.md),
> [PLAN_corpus_litter.md](PLAN_corpus_litter.md) (every cell this plan adds is a corpus this plan must not leak).

## 1. The goal, before any solution

The operator wants to answer, on one screen: *which retrieval configuration wins, on which kind of
question* — and to keep answering it as the configuration space grows.

Concretely:

1. A **question bank** of five named reading groups (~100 questions each, eventually): direct code
   lookup, semantic/architectural intent, PR/diff-based, bug/root-cause, adversarial cross-class — plus
   a sixth group, **code writing**, whose lifecycle is different enough to have its own plan
   ([PLAN_code_lane.md](PLAN_code_lane.md)): the bank carries it, this plan does not run it. Every
   question carries **reviewer marks** — who reviewed it (minimum three reviewers: `claude`, `codex`,
   `gemini`; the set must be extensible) — rendered as checkmarks in a UI.
2. A **test** = a fixed selection of questions (e.g. group 1, questions 1–10) × **all active variants** ×
   the chosen subjects × repeats, pinned to one repository commit. Created from the UI with one button.
3. A **matrix page** per test: one cell per variant (× subject), each showing done / in-progress (with %)
   / not started, each cell an anchor opening the variant's page in a new tab.
4. A **variant page**: the short name (`sparse · bge-m3 · ast+xml · rrf`), metrics per question, rollups
   **per question group**, a summary table, an analysis block (what was good, what was bad), and the
   operational numbers — durations, speeds, log/telemetry volume (bytes and counts), answer quality.
5. Everything persisted **structured** in the benchmark's own Postgres: the prompt as sent, the retrieved
   hits and the white/black-box funnel, the model's answer and its thinking, sampling as-sent, every
   metric, every judge verdict. The database is the artefact — it goes public later (a mirror server is
   the current intent; not designed here, but nothing may be stored in a shape that would prevent it).
6. **The matrix is modular.** New axes will arrive (chunk 256/512 already named; more fusion modes, more
   channels, more embedders later). Adding a variant must be a catalog row, not a schema change — and a
   test that was 100 % done must **reopen as in-progress** for exactly the new cells, with a button to
   run just those.
7. **Models are configuration, chosen per test.** A settings page lists the available models (cloud CLI
   agents, API endpoints, local models through the bridge); a test picks its subjects and its arbiters
   (ordered — who judges first) from that list, and a subject added to an existing test reopens it the
   same way a new variant does.
8. **Cloud-CLI subjects must also run as agents**: native tools plus our MCP server — beside the
   no-tools and single-shot lanes.

## 2. What exists today, verified

| Capability | State | Where |
|---|---|---|
| Matrix planning | question × repeat × subject × lane; **engine is one value per run, not an axis** | `src/Bench.Domain/Runs/Matrix.cs:23-74`, `src/Bench.Application/PlanRun.cs:20-38` |
| Durable cells | claim/settle/sweep, guarded UPDATE, result-first-settle-second, `MaxAttempts=3` | `src/Bench.Domain/Runs/RunCell.cs:81-141`, `src/Bench.Infrastructure/Persistence/PostgresRunStore.cs:54-121` |
| Reviewer/authoring domain | **designed, unwired** — `QuestionCandidate` (author model, `Proposed→Accepted/Rejected`, review note, dedup, memorisation check, batch promotion into a frozen suite); no store, no CLI, no UI | `src/Bench.Domain/Authoring/QuestionCandidate.cs`, `AuthoringRules.cs` |
| Suites | frozen + hashed, stamp `id@vN#hash12`; live **only as JSON files**, never in Postgres | `src/Bench.Domain/Suites/Suite.cs:30-77`, `src/Bench.Application/SuiteJsonLoader.cs:75-108` |
| Commit pinning | `MeasurementTarget` demands a full 40-char sha; `bench run` **never checks out** (prints a warning) | `src/Bench.Domain/Targets/MeasurementTarget.cs:36-62`, `hosts/Cli/RunCommand.cs:110` |
| Checkout cache | bare mirror + worktree per commit, written and tested, **not wired into any run path** | `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs:37-135` |
| QLN adapter | exists, parses the funnel, degrades honestly — but is **test-only** and sends exactly one axis (`limit`) | `src/Bench.Infrastructure/Engines/QlnEngine.cs:112-144, 222-238` |
| Execution | `LegRunner` single-shot ask, no engine wired, no tool loop | `src/Bench.Application/LegRunner.cs:44-137` |
| Judges | multiple arbiters by design (`Judge verdict · {modelId}` per-arbiter series, NOT-EXISTS work selection) | `src/Bench.Application/JudgeRunner.cs`, `src/Bench.Domain/Runs/JudgeScoring.cs` |
| API / UI | `MapBenchApi` (health + plan) hosted by **nobody**; no web project at all | `src/Bench.Api/BenchApi.cs:15-27`, `hosts/AppHost/AppHost.cs:60-63` |
| Comparison queries | `AverageByEngineAsync`/`AverageByLaneAsync` exist, surfaced by nothing | `src/Bench.Application/ResultStore.cs:43-48` |

**Prerequisite from the boundary audit:** the trace/v0 `collapse` repair — qln emits an eighth stage the
`TraceContract` did not define, so every real funnel degraded to black-box. Tracked in
`dew_flow_rag_qln · todo/PLAN_boundary_repairs.md`; this plan's white-box storage (§5.4) is worthless until
that repair is deployed on both sides. Verify before step 3 lands.

## 3. The shape — decisions

### 3.1 A variant is a catalog row, and its definition is immutable

New table `variants`. One row = one named retrieval configuration:

```
variants: Id (uuid), Name (unique, short: "hybrid-rrf-bge-256"), DisplayName ("hybrid · rrf · bge-m3 · 256"),
          DefinitionJson, Hash (StableHash of the canonical definition), CreatedAt, RetiredAt (null = active)
```

`DefinitionJson` is the whole recipe the runner needs, axes as data:

```json
{
  "engine":   "qln",
  "channels": "hybrid | dense | sparse",
  "fusion":   { "mode": "rrf | wsum", "k": 60, "denseWeight": 1.0, "sparseWeight": 1.0, "norm": "minmax" },
  "corpus":   { "textShape": "src-cgx", "chunkTokens": 256, "embedModel": "bge-m3" },
  "rerank":   { "enabled": true, "pool": 50 },
  "limit":    20
}
```

Rules, mirroring `Suite.Freeze`:

- A variant is **never edited**. Changing a definition mints a new row (new name or version suffix); the
  old one is `Retired`. Every result names the `VariantId` it ran under, so a redefinition can never
  silently relabel old numbers — the same immutability the suite stamp already enforces for questions.
- `Retired` variants stop appearing in new tests and in expansion, but their historical cells render
  normally.
- The runner **refuses** a definition field it does not know (the telemetry `UnknownVersion` discipline,
  applied to configs): axes are data, but unknown axes are not silently ignored.
- CLI: `bench variants add|retire|list`. The UI gets management later; the catalog works headless first.

### 3.2 A test is a run whose matrix can grow

No parallel aggregate. The existing `BenchRun` + `RunCell` machinery **is** the test — extended:

- `cells` gains `VariantId` (FK → `variants`, nullable; null = legacy single-engine rows). New index
  `(RunId, VariantId, State)`.
- `Matrix.Plan` (`Matrix.cs:52-62`) gains the variant axis: `legs = subjects × lanes × variants`, same
  rotation balancing, `FirstPositionCounts` still proves it.
- `runs` gains `run_questions` child rows `(RunId, QuestionId, GroupKey, Ordinal)` — the frozen selection
  snapshot, which is what per-group reporting reads. The `SuiteStamp` column keeps its meaning: creating a
  test freezes the selection through the existing `Suite` machinery (a suite built from the selected bank
  questions), so every result still names a frozen, hashed question set.
- The test's subjects are rows, not a frozen field: `run_subjects (RunId, ModelKey, AddedAt)`. Adding a
  subject to an existing test is legal (removing is not — settled cells would dangle); the matrix then
  grows exactly as it does for a new variant.
- **Expansion is the new verb**: `ExpandAsync(runId)` enumerates the current matrix (selection × active
  variants × current subjects × repeats), inserts the cells that do not exist (`NOT EXISTS`, one
  transaction), touches nothing settled. This single operation is what makes a finished test reopen as
  in-progress when a variant — or a subject — is added; completion was never stored, only derived.
- **The percentage is always settled ÷ total of the CURRENT matrix**, shown beside its absolute numbers
  (`312 / 480`) so a drop after expansion reads as growth, not regression. The matrix page also shows the
  per-variant and per-subject breakdown, and an expansion log line ("+120 cells, 2026-08-17: subject
  `local-qwen` added") explains every drop.
- **Run status becomes derived.** `runs.Status = Completed` is replaced by a computed progress
  (settled / total, claimed → %) served by `ProgressAsync`; the column stays for legacy reads but the UI
  never trusts it. A test is "done" only relative to the variant catalog *now*.

### 3.3 The question bank lives in Postgres, and wires the existing authoring domain

New tables — the persistence the `Authoring` domain never got:

```
question_groups:  Id, Key ("code-lookup" | "semantic-intent" | "pr-diff" | "bug-root-cause"
                  | "adversarial" | "code-writing"), Title, Ordinal
bank_questions:   Id, GroupId (FK), Ordinal (the number the operator quotes: "group 1, questions 1–10"),
                  TaskKind (Reading | Code), CodeTaskJson (null for Reading — see PLAN_code_lane.md §3.1),
                  Prompt, ReferenceAnswer, ExpectationsJson (same wire shape SuiteJsonLoader reads),
                  TargetRepoUrl, AuthoredAtCommit, SourceKind (RepositoryHistory|BugsAndTests|Synthetic|Human),
                  AuthorModel, State (Proposed|Accepted|Rejected), CreatedAt
reviewers:        Id, Key ("claude"|"codex"|"gemini"|…), DisplayName, Ordinal      -- extensible, data not enum
question_reviews: QuestionId (FK), ReviewerId (FK), Verdict (Approved|Rejected), Note, At
                  -- unique (QuestionId, ReviewerId): one mark per reviewer per question
```

- Phase 1 (this plan): `bench questions import <json>` loads authored questions into the bank;
  review marks arrive by import or are toggled in the UI. The UI's checkmark row per question is a join
  over `question_reviews` × `reviewers` — adding a fourth reviewer is one row in `reviewers`.
- Phase 2 (a follow-up plan, **not** this one): `bench author` / `bench review` drive the three CLI agents
  to generate candidates per group and review them — the `AuthoringBatch.Promote` path. The schema above
  is deliberately the shape that pipeline needs, so phase 2 adds verbs, not tables.
- Only `Accepted` questions are selectable into a test. Selection UI: group + ordinal range + checkboxes.
- **Group membership is versioned, flexibly**: `bank_questions.GroupId` is the current home;
  `question_group_moves (QuestionId, FromGroup, ToGroup, At, Reason)` is the history;
  `run_questions.GroupKey` stays the per-test snapshot. Reports read the snapshot by default (a finished
  report must not change retroactively), a toggle regroups by the current bank, and a badge marks
  questions whose group changed after the test was created.

### 3.4 One cell, end to end (always the full pipeline)

Per the operator's decision: every cell runs retrieval **and** the model. The leg for a `qln`-engine
variant:

1. **Checkout** — `ICheckoutProvider.EnsureAsync(target)` wired into run start (both CLI and worker); the
   warning at `RunCommand.cs:110` dies. The worktree is what filesystem lanes and index verification see.
2. **Index readiness** — the cell's variant names a corpus recipe; the run's target names a commit. A new
   `index_preparations` table tracks `(TargetCommit, CorpusRecipe, EngineEndpoint) → Requested | Building |
   Ready | Failed`, filled by asking qln's index-state endpoint (sibling plan §3) and, when the operator
   triggers it, starting a pass over HTTP. qln does not check commits out itself — this repository keeps a
   **writable indexing checkout** per target repo at a stable path (distinct from the read-only worktree
   cache, which stays untouched per the founding rule), moves it to the test's commit, then requests the
   pass with `ExpectedCommit` so qln refuses a mismatched tree. Cells whose preparation is not `Ready` stay `Pending` with a
   visible reason — never a silent zero-hit measurement (the founding plan's `WarmAsync` lesson, applied
   per-variant). A recorded qln commit that differs from the test's commit **blocks** the cell.
3. **Retrieve** — `QlnEngine` grows the variant's axes: `AxesWire` (`QlnEngine.cs:222-238`) extends from
   `limit`-only to the full definition (channels, fusion mode + params, rerank, textShape). The response's
   echoed axes are stored — proof of what actually served the query, not what was asked.
4. **Ask** — the prompt is assembled from the question + retrieved context (single-shot RAG; the agentic
   tool loop stays future work per the founding plan), sent via `IModelRuntime.AskAsync` with budgets;
   answer, thinking, tokens, `SamplingAsSent`, stop reason, wall time all captured.
5. **Score** — mechanical: `AnswerScoring` against expectations, plus retrieval metrics computed from the
   stored hits vs the question's anchors (recall@k, MRR, first-hit rank) — free at this point, stored as
   metric rows like everything else.
6. **Persist** — result + funnel + hits (§3.5), then settle. Result first, settle second, unchanged.
7. **Judge** — separate passes, one per arbiter, each appending its own `Judge verdict · {modelId}`
   series; the existing NOT-EXISTS selection makes re-runs and new arbiters idempotent. The arbiters of a
   test are chosen from the model registry (§3.7) as ordered rows — `run_judges (RunId, ModelKey,
   Ordinal)`: the first is the primary whose verdict headlines the report, the rest render beside it as
   counter-opinions. A local model through the bridge is a legal arbiter like any other registry row.

### 3.5 What gets persisted, structured (the public-artefact discipline)

Existing: `runs`, `cells`, `results (Prompt, Answer)`, `metrics` (metric-as-row), `tool_telemetry`.
New:

```
funnels:        ResultId (FK, unique), ContractVersion, StagesJson, TotalMs, AbsentJson,
                Degraded (bool), DegradationReason, PayloadBytes
retrieved_hits: ResultId (FK), Rank, RelativePath, StartLine, EndLine, MemberKey, Signature,
                Score, Ordering, ChannelsJson, RanksJson
results +      ThinkingText (empty when the runtime returns none), ResponseMetaJson
                (tokens in/out, stop reason, duration, sampling as sent, response bytes)
```

- The funnel row is the white-box record when the contract validated, and the black-box record (with its
  named reason) when it degraded — both are data, per the founding plan's two-vantage-points table.
- Log volume ("how many megabytes, how many lines") is recorded per cell from what the harness itself
  observes: request/response byte sizes, funnel payload bytes, and the count + byte total of
  correlation-matched `tool_telemetry` lines. The daemon's own log files are not attributable per-leg and
  are **not** claimed — a number we cannot attribute honestly is not stored (`FactSource` discipline).
- Nothing stores an absolute local path, a secret, or a machine-specific value in result rows — the
  database must survive publication unedited. (Publication itself: deferred by operator decision —
  "Postgres now, a mirror server later." Open question §8.3 records the intent.)

### 3.6 The console: library-first, thin shell (operator decision 2026-08-16)

The operator's constraint: **the UI is a component library**; the host is an embedding detail that must be
easy to change later. Combined with the founding plan's own decision ("API alongside the CLI over the same
domain; UI in an RCL from birth") and the qln console's conventions:

- `src/Bench.Ui` — Razor Class Library holding **every page and component**, references **only**
  `Bench.Contracts` (the qln lesson: the UI project must never see the API library — `Rag.Ui.csproj`'s
  comment). Pages + `.razor.cs` split, one typed client `BenchConsoleApi` copying `RagConsoleApi`'s two
  disciplines: transport failures return empty, domain failures return the `ProblemDetails` detail so
  "no data" and "the store is down" never render the same. The client's base address is configuration,
  never a constant — that is what makes the library mountable by any host.
- `src/Bench.Api` — already a library; **every endpoint stays here**, the host only calls `MapBenchApi()`.
- `hosts/Web` — a **thin shell**: a Blazor Web App `Program.cs` of a few dozen lines (SSR +
  InteractiveWebAssembly, dual-container service registration, unconditional `UseStaticWebAssets()` per
  `dew_flow_rag_qln/hosts/Daemon/Program.cs:27-59`, `AddDewFlowLogging`, `MapBenchApi()`,
  `AddAdditionalAssemblies(Bench.Ui)`). `hosts/Web.Client` — the WASM bootstrapper, equally thin. **No
  page markup and no endpoint logic may live in either host project** — splitting the console out later
  (own port, own deploy, or a different host entirely) means moving the shell, not the pages.
- The shell is registered in the **existing** `hosts/AppHost/AppHost.cs` as the project resource the
  file's own comment reserved space for (`.WithReference(bench db)`) — no second orchestrator.
- Explicitly rejected: mounting `Bench.Ui` into the qln console. The daemon is the measured system, and
  the family invariant is that **the measured party never links the measurer**
  (`dew_flow_rag_qln · research/repository_boundaries.md`); a console the benchmark needs must be served
  from this repository's own process.
- **Execution moves into the Web host**: a `BenchRunWorker : BackgroundService` (the qln
  `IndexPassWorker` shape — startup sweep first, then drain) executes claimed cells so a run started from
  the UI survives F5, crashes, and browser death; the button persists state before work starts and the
  page derives everything from `cells` (family rule §8: in-component flags are optimistic only). The CLI
  keeps working headless against the same tables — two front doors, one claim queue, and the existing
  owner-guarded settle already makes them safe together.
- **API-first is a gate, not a preference** (operator decision 2026-08-16): every capability ships
  domain + API endpoint + CLI verb first; a UI page may only consume endpoints that already exist. The
  build order (§5) encodes this — the console's pages trail the API they render by two steps.
- Pages:
  1. **Questions** — grouped list, reviewer checkmark columns, state filter, import.
  2. **New test** — pick group(s)/ranges/checkboxes, subjects, repeats, commit (default: the target's
     current HEAD, shown and editable); one button plans run + cells across all active variants.
  3. **Test matrix** — grid: variants × subjects; per cell `✔ done` / `N %` / `— not started` /
     `blocked: index not ready`, derived from `ProgressAsync` by `(VariantId, SubjectModelId)`; every cell
     an `<a target="_blank">` to the variant page. A banner when the catalog has variants the test has no
     cells for: "3 new variants → 120 new cells · [Plan them]" → `ExpandAsync`. Poll while anything is
     claimed (the `CompanyProjects` poll shape: start only when in-flight, stop when quiet).
  4. **Variant page** (`/tests/{run}/variants/{variant}?subject=`) — short name + full definition echo;
     per-question table (retrieval metrics, answer metrics, judge verdicts per arbiter, duration, bytes);
     **rollups per question group**; the summary comparison table; an analysis block (best/worst
     questions by each metric, degradations with reasons, cap-exceeded and crashed legs listed by name);
     expandable per-leg detail — prompt, hits with funnel, answer, thinking, sampling as-sent.
  5. **Variants** — the catalog, with retire and "what would expansion cost" preview.
  6. **Settings · Models** — the registry (§3.7): add, disable, configure; roles are chosen per test,
     never here.

### 3.7 The model registry and per-test roles (operator decision 2026-08-16)

Models are configuration, never constants:

```
models:       Id, Key (unique: "claude-opus", "local-qwen32"), DisplayName,
              Runtime (openai-endpoint | cli-claude | cli-codex | cli-gemini | bridge-local),
              ConfigJson (endpoint / model id / CLI path / sampling defaults), Enabled, CreatedAt
run_subjects: RunId, ModelKey, AddedAt          -- the test's answering models (§3.2, add-only)
run_judges:   RunId, ModelKey, Ordinal          -- the test's arbiters, ordered: first = primary
```

- The settings page manages the registry; `bench models add|disable|list` is the CLI face. Every role a
  model can play — subject, arbiter, and (phase 2) question author and reviewer — draws from this one
  list; `reviewers` (§3.3) gains an optional `ModelKey` so an automated reviewer is the same identity
  everywhere.
- A local model through the bridge (`Mcp.Bridge` / `LocalLlmToolBridge`, agent mode) is a registry row
  like any other — `Runtime = bridge-local`.
- Creating a test means choosing subjects and arbiters from the enabled rows; both choices are stored on
  the test, so the registry can change without rewriting history. A disabled model is refused at test
  creation by name.

### 3.8 The agent lane: native tools plus our MCP server (operator decision 2026-08-16)

Cloud-CLI subjects (Claude Code, Codex CLI, Gemini CLI) must be measurable in **agent mode**: the CLI
runs headless over the checked-out worktree with its native tool set, plus our MCP server attached — the
tool surface a customer's agent would actually see. The lane axis therefore grows to
`{no-tools, rag-context, agent-mcp}` (lanes are already data — `Lane(Name, Preamble)`).

- Needs a `CliAgentRuntime` beside `OpenAiCompatibleRuntime`: prompt in, final answer out, per-leg
  workspace, the MCP endpoint injected through each CLI's own config mechanism, budgets enforced by the
  harness. The `bridge-local` row rides the same lane with an in-process runtime instead of a CLI.
- Telemetry correlation closes its loop here: the agent's MCP calls land in the spool with the leg/phase
  the harness supplies, so `tool_telemetry` finally attributes real tool traffic to cells.
- Hard dependency, named in the sibling plan (its §3.6): the qln daemon currently serves **one** MCP tool
  (`rt_read_local_file`) — the retrieval tool (`rag_search`) does not exist and the `IToolProvider` seam
  is unimplemented. The agent lane is honest only after that tool ships (and the mcp submodule bump —
  `PLAN_boundary_repairs.md` item 3 — lands).
- Ordered last (§5 step 11): the single-shot lane must be proven end to end first.

### 3.9 What this plan deliberately does not do

- **No code-lane execution** — group 6 lives in the bank here (a `TaskKind`, a payload, reviewer marks
  like any other question) and runs in [PLAN_code_lane.md](PLAN_code_lane.md), which owns the phases,
  the sandbox, the mechanical signals and the delivered-work score. The two plans meet at the bank and
  at the matrix axes, nowhere else.
- No `bench author`/`bench review` automation — phase 2, its own plan, on top of §3.3's tables (the
  model registry already carries the roles it will need).
- No public mirror/export design — operator: "Postgres now, public later"; §3.5 keeps rows publication-safe.
- No BM25/SPLADE execution in this repo — the qln sibling plan lays the channel contracts; the benchmark
  consumes whatever the echoed axes say actually ran.
- No hardware sampler (still founding-plan step 7b).

## 4. Cross-repository contract (the sibling plan's half)

What this repo needs qln to provide — named identically in
`dew_flow_rag_qln · todo/PLAN_search_variant_axes.md`:

1. `/search` accepts the fusion axes (`fusion.mode`, weights, `norm`) and `textShape` selects chunk
   variants — additive to the existing input, `trace/v0` untouched.
2. An **index-state read**: per (project, branch, corpus variant) — collection name, recipe, indexed
   commit sha, point count, finished-at. This is what `index_preparations` polls.
3. Passes **record the commit** they scanned; a pass can be started over HTTP naming the corpus variant.
4. A second dense embedder (`qwen`/`jina`, dense-only) registered behind the same recipe machinery, so
   `embedModel` is a legal variant axis.

## 5. Build order

Each step ships alone, tests green, before the next starts. **API-first throughout: a UI step never
precedes the API + CLI it renders.**

1. **Variant catalog** — `variants` table + domain type + immutability tests + `bench variants` verbs.
   `cells.VariantId` migration. `Matrix.Plan` variant axis + rotation-balance tests.
2. **Question bank** — groups/questions/reviewers/reviews/`question_group_moves` tables + import verb +
   selection freeze into the existing suite stamp + `run_questions` snapshot. Unit: promotion refusals
   (collision, empty) now hit Postgres-backed tests.
3. **Model registry** — `models`/`run_subjects`/`run_judges` tables + `bench models` verbs; `bench run`
   learns to read subjects and arbiters from the test instead of its `--model` singletons.
4. **Checkout + engine wiring** — `ICheckoutProvider` into run start; `QlnEngine` full `AxesWire`;
   engine-per-variant resolution in `LegRunner`; single-shot RAG prompt assembly; funnel + hits + thinking
   persistence (§3.5 migrations); retrieval metrics. Verify the `collapse` repair end to end here.
5. **Index preparations** — the table, the qln index-state poll, the writable indexing checkout, the
   block-with-reason path. (Depends on sibling plan §3.2 landing first; until then cells block honestly.)
6. **Expansion** — `ExpandAsync` over variants AND subjects + derived progress + CLI `bench expand`.
   Explicit tests: settle everything, add a variant → reopen; add a subject → reopen;
   % = settled ÷ current total.
7. **API read surface + reports** — run/matrix/variant/question/model endpoints in `Bench.Api`, plus a
   `bench report` verb surfacing the comparison queries that today have no caller
   (`AverageByEngineAsync`/`AverageByLaneAsync`).
8. **Console, read paths** — `hosts/Web` + `Web.Client` + `Bench.Ui` skeleton, AppHost registration;
   settings, questions, matrix and variant pages over the step-7 endpoints.
9. **Console, write paths** — new-test flow, `BenchRunWorker`, start/expand buttons, polling, sweep on
   startup.
10. **Judges** — ordered per-test arbiters from the registry, per-group rollups + the analysis block.
11. **Agent lane** — `CliAgentRuntime` + the bridge runtime, telemetry correlation, the `agent-mcp`
    lane. Gated on the qln `rag_search` tool and the submodule bump (sibling plan §3.6).

## 6. Test plan

- xUnit v3 exe (never `dotnet test`), `PostgresFixture` (Testcontainers) for every table this plan adds;
  migrations applied for real, loud failure without Docker — the repo's existing discipline.
- Domain: variant immutability/refusal-of-unknown-axes; matrix balance with the third axis; expansion
  idempotence (run twice = no new cells); derived progress across the reopen scenario.
- Bank: one-mark-per-reviewer uniqueness; only-Accepted-selectable; selection freeze produces a stable
  stamp for the same selection and a different stamp for a different one.
- Engine: `AxesWire` round-trip against a fake qln (echoed axes stored, degraded funnel stored with
  reason); index-prep blocking on commit mismatch.
- UI: bUnit for the matrix cell (status derivation, % render, blocked reason) and the reviewer checkmark
  row — following the family's component-test recipe; a Blazor rule file is added to
  `.claude/rules/csharp/` alongside the first component (this repo has none yet).
- Registry: role choices stored per test; a disabled model refused at test creation by name; subject
  addition reopens a settled test (mirror of the variant-expansion test).
- Every bug found during the build gets its RED test first, per `.claude/rules/common/testing.md`.

## 7. Definition of Done

- [ ] A variant is a catalog row; adding one requires no migration and no recompile of the runner.
- [ ] A test created from the UI pins a 40-char commit, checks the worktree out at run start, and refuses
      retrieval cells whose index commit differs.
- [ ] The matrix page derives every cell state from Postgres; F5, browser death and host restart change
      nothing the sweep cannot repair.
- [ ] Adding a variant and pressing Expand turns a 100 % test into an in-progress test with exactly the
      new cells pending — proven by an automated test, not by demonstration.
- [ ] Every result row carries prompt, hits, funnel (or its degradation reason), answer, thinking,
      sampling-as-sent, tokens, durations, byte sizes — no `null` where "not captured + reason" belongs.
- [ ] Per-group rollups render on the variant page; three reviewer checkmark columns render on the
      questions page and a fourth reviewer is one data row.
- [ ] The web host projects contain no page markup and no endpoint logic — every page in `Bench.Ui`,
      every endpoint in `Bench.Api`; the shell is replaceable without touching either.
- [ ] Subjects and arbiters are chosen per test from the settings registry; adding a subject to an
      existing test reopens it with % = settled ÷ current total and a logged expansion line.
- [ ] A cloud-CLI subject runs the agent lane with native tools + our MCP server, and its tool calls
      arrive in `tool_telemetry` attributed to the leg.
- [ ] Every UI page consumes only endpoints that existed before it — API-first held throughout.
- [ ] Multiple arbiters produce parallel `Judge verdict · {model}` series over one test.
- [ ] `todo/README.md` table updated; `research/` module docs updated as steps land.

## 8. Open questions

Former questions 1 (arbiter transport), 2 (subjects) and 4 (group versioning) were answered by operator
decisions 2026-08-16 and are now §3.7, §3.2 and §3.3 respectively.

1. **Publication mechanics.** Operator intent: mirror the local Postgres to a public server, later. When
   it becomes current, it needs its own plan: schema-stability promises, a redaction audit, a licence.
2. **CLI-agent harness specifics** — per-CLI headless flags, MCP config injection, and what "thinking"
   each CLI exposes. Measured at step 11 against the real CLIs, not guessed here.
3. **A creation-time baseline number** beside the current-matrix percentage ("100 % of the matrix as it
   was, 78 % of today's") — a display choice, deferred to the matrix page's first real use.
