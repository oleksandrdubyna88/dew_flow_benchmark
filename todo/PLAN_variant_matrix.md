# PLAN — the variant matrix: a question bank in five groups, reviewer marks, engine axes in the grid, and the console

> Status: **steps 1, 2 and 3 of §5 IMPLEMENTED 2026-08-16; steps 4–11 open.** Step 3, the model registry:
> `models` / `run_subjects` / `run_judges` (migration `ModelRegistry`), `bench models add|list|disable|enable`,
> and `bench run --subjects <keys> [--judges <keys>]` resolving every key — and every reference — BEFORE a
> single cell exists. `ConfigJson` holds references and refuses values by name, so the database stays
> publishable unedited; `bench judge` with no `--judge-model` uses the arbiters the test chose, in order.
>
> Deviations, step 3:
> - **A defect found while wiring it, not by a test: the runner had ONE endpoint.** `Matrix.Plan` has
>   always taken a LIST of subjects while `LegPlan` held a single `ModelEndpoint`, so a two-subject run
>   would have sent every leg to the first model and labelled the results with the cell's subject — two
>   models named, one measured, and nothing in any report able to show it. `SubjectRoster` is the fix: the
>   endpoint is looked up by the cell's subject, and a cell this run cannot reach is SETTLED rather than
>   redirected (`Each_subjects_leg_is_sent_to_THAT_subjects_endpoint`, red before the change).
> - **The ad-hoc `--model` pair stays**, and records no roles — a role names a REGISTRY key, and such a run
>   names none. Its subject is still on every cell, so nothing is lost.
> - **`bench judge` runs EVERY arbiter of the test, in order**, when none is given. Per-group rollups are
>   step 10; what this step owed was that the choice travels with the test — and a stored role nothing
>   reads is the `SweepAsync` shape this repository has already paid for once.
> - **An arbiter added later continues the order** instead of restarting it; a subject may be ADDED to an
>   existing test (that is step 6's expansion) but never twice.
> - **No foreign key from the role tables to `models`**: a role names a key, and a run must stay readable
>   for a subject named ad hoc. `enable` ships beside the plan's `add|disable|list` — a disabled row that
>   could never come back is a dead row.
>
> Step 1, the variant catalog:
> `Bench.Domain/Variants` (definition + hash + immutable catalog row + selection), the variant axis in
> `Matrix.Plan`, `variants` table and `cells.VariantId`, `bench variants add|list|retire`.
> Step 2, the question bank: `question_groups` / `bank_questions` / `reviewers` / `question_reviews` /
> `question_group_moves` / `run_questions` (migration `QuestionBank`), `bench questions
> import|list|groups|review|accept|reject|move`, and `bench run --bank-group` freezing a selection through
> the EXISTING suite machinery — a test built from the bank and one built from a file mint the same kind of
> hashed stamp, and a result cannot tell which door it came through.
>
> Deviations, step 2:
> - **The bank stores the SEED** (`SeedKind`/`SeedReference`/`SeedAt`), three columns §3.3's schema does
>   not list. The memorisation check is computed from the seed's DATE, and the only date the plan's columns
>   offered was the import date — which would certify every imported question as clear against every
>   subject's cutoff. A question that declares no seed gets `unstated` at the beginning of time, so it reads
>   as *may recall* rather than as safe.
> - **Admission reuses `QuestionCandidate.Propose`** instead of a second rule: a non-human source must name
>   its author model, and a question with no retrieval expectation has nothing to score against the code.
>   Two admission rules would drift, and the drifted one would be the unread one.
> - **`QuestionJson` was extracted from `SuiteJsonLoader`** so a suite file and a bank row share ONE wire
>   shape and one mapping to `Question`; `Slug` was extracted so `VariantName`, `GroupKey` and
>   `ReviewerKey` share one rule rather than three copies of a regex.
> - **More verbs than the plan named.** `import` alone could not make a question selectable or exercise
>   `question_group_moves`, so `accept`/`reject`/`move`/`review`/`groups` ship with it. A move REFUSES
>   without `--reason`: the history row exists to explain a finished report's snapshot, and one with no
>   reason records that something changed and nothing about why.
> - **A file-selected run writes no snapshot rows**, and that absence is the honest reading — the snapshot
>   records which GROUP each question was in, and a file has no groups.
> - **`BankFreeze` refuses a duplicate suite-facing id** before promotion. `Suite.With` would have taken
>   both, and the suite would then have scored one question twice under one id, invisibly.
>
> Scope: `Bench.Domain`, `Bench.Application`,
> `Bench.Infrastructure` (persistence + engines), `Bench.Api`, a NEW `hosts/Web` + `hosts/Web.Client` +
> `src/Bench.Ui`, `hosts/Cli`. The engine-side half lives in the sibling plan
> `dew_flow_rag_qln · todo/PLAN_search_variant_axes.md` — a change that crosses the repository boundary is
> named in both plans.
>
> Related: [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) (founding plan),
> [../research/architecture.md](../research/architecture.md),
> [PLAN_corpus_litter.md](PLAN_corpus_litter.md) (every cell this plan adds is a corpus this plan must not leak),
> [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) (the design of this plan's §3.8 agent lane — boundary in §3.8a),
> [PLAN_corpus_axis_integrity.md](PLAN_corpus_axis_integrity.md) (two more corpus identity axes and the recipe echo),
> [../research/PLAN_reliability_tail.md](../research/PLAN_reliability_tail.md) (implemented 2026-08-16 —
> its items 2 and 1's wall-budget tail were two of step 9's gates, and both are now clear).

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
| Reviewer/authoring domain | ~~designed, unwired~~ — **wired 2026-08-16**: the bank is its store (`Bench.Domain/Bank`, `PostgresQuestionBank`), admission goes through `QuestionCandidate.Propose`, and a selection is promoted by `AuthoringBatch.Promote`. What is still unwritten is the pipeline that GENERATES candidates — a later plan's verbs, not tables | `src/Bench.Domain/Authoring/*`, `src/Bench.Domain/Bank/*` |
| Suites | frozen + hashed, stamp `id@vN#hash12`; from a JSON file **or from a bank selection** (2026-08-16), both through the same freeze | `src/Bench.Domain/Suites/Suite.cs:30-77`, `src/Bench.Domain/Bank/BankFreeze.cs` |
| Commit pinning | `MeasurementTarget` demands a full 40-char sha, and since 2026-08-16 `bench run` **checks the target out** before it creates anything — a commit that is not in the repository ends the run by name | `src/Bench.Domain/Targets/MeasurementTarget.cs:36-62`, `hosts/Cli/RunCommand.cs` (`CheckoutAsync`) |
| Drain loop | `LegDrain` — per-leg `try`, consecutive-failure breaker, bounded backoff, typed `DrainStop`, grace on cancel | `src/Bench.Application/LegDrain.cs:82-121`, driven from `hosts/Cli/RunCommand.cs:153` |
| Checkout cache | bare mirror + worktree per commit — ~~not wired into any run path~~ **wired into `bench run` 2026-08-16**; gates the run, and its lock map no longer leaks | `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs`, `hosts/Cli/RunCommand.cs` |
| QLN adapter | exists, parses the funnel, degrades honestly — but is **test-only** and sends exactly one axis (`limit`) | `src/Bench.Infrastructure/Engines/QlnEngine.cs:112-144, 222-238` |
| Execution | `LegRunner` single-shot ask, no engine wired, no tool loop — but **multi-subject since 2026-08-16**: the endpoint is looked up per cell from `SubjectRoster`, and a leg with a wall budget cannot be multiplied by a turn count | `src/Bench.Application/LegRunner.cs`, `src/Bench.Domain/Runs/SubjectRoster.cs` |
| Judges | multiple arbiters by design (`Judge verdict · {modelId}` per-arbiter series, NOT-EXISTS work selection); **the test's own ordered arbiters are used when none is given** (2026-08-16) | `src/Bench.Application/JudgeRunner.cs`, `hosts/Cli/JudgeCommand.cs` |
| Model registry | **implemented 2026-08-16** — `models` with references-never-values config, `run_subjects`/`run_judges` on the test, resolution refused by name at creation | `src/Bench.Domain/Registry/*`, `src/Bench.Infrastructure/Persistence/PostgresModelRegistry.cs` |
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
  variants × current subjects × repeats), inserts the cells that do not exist (`NOT EXISTS`), touches
  nothing settled. This single operation is what makes a finished test reopen as in-progress when a
  variant — or a subject — is added; completion was never stored, only derived.
- **Expansion is bounded, and the preview is a GATE rather than a display.** The cross product this verb
  materializes is not small: ten questions × the 96 resident corpora of §3.4a × three subjects × two
  repeats is 5 760 rows, and the same expansion over one whole hundred-question group is 57 600. Written
  as one transaction from a UI button that is one click from a settled test, that is a long lock over the
  table every cell claim contends on, held while an operator wonders whether the page is stuck. So:
  inserts go in **chunks of a fixed size** (the `PostgresTelemetryStore` unbounded-`IN` lesson —
  `research/PLAN_reliability_tail.md` item 4, since shipped as `SpoolIngest.ReadChunksAsync` — is the
  same shape one layer down), and an expansion whose computed
  size exceeds a configured cap is **refused by name**, with its number, until the caller confirms it
  explicitly. The "what would expansion cost" preview of §3.6 page 5 is what produces that number; it is
  the thing the confirmation confirms, not a decoration beside the button.
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
   warning at `RunCommand.cs:127` dies. The worktree is what filesystem lanes and index verification see.
2. **Index readiness** — the cell's variant names a corpus recipe; the run's target names a commit. A new
   `index_preparations` table tracks `(TargetCommit, CorpusRecipe, EngineEndpoint) → Requested | Building |
   Ready | Failed`, filled by asking qln's index-state endpoint (sibling plan §3) and, when the operator
   triggers it, starting a pass over HTTP. qln does not check commits out itself — this repository keeps a
   **writable indexing checkout** per target repo at a stable path (distinct from the read-only worktree
   cache, which stays untouched per the founding rule), moves it to the test's commit, then requests the
   pass with `ExpectedCommit` so qln refuses a mismatched tree. Cells whose preparation is not `Ready` stay `Pending` with a
   visible reason — never a silent zero-hit measurement (the founding plan's `WarmAsync` lesson, applied
   per-variant). A recorded qln commit that differs from the test's commit **blocks** the cell.

   **The checkout is leased for the whole pass, not for the `git checkout`.** One writable checkout serves
   every corpus of a target repo, and qln reads `git rev-parse HEAD` once, at scan start (sibling plan
   §3.2). Nothing in that pair serializes "the benchmark moves the tree" against "qln is twenty minutes
   into scanning it": a `git checkout` mid-pass produces an index of the NEW tree stamped with the OLD
   sha, which passes every check this plan makes and is undetectable from the stored row afterwards — the
   commit stamp would then be worse than no stamp, because it reads as evidence. So the benchmark takes a
   **lease on the indexing checkout** (keyed on the target repo path, the same advisory-lock mechanism as
   §3.4b) before it moves the tree, and holds it until qln reports the pass terminal — `Ready` or
   `Failed`, not merely "started". qln's half is the other end of the same guarantee: it re-reads HEAD at
   pass END and fails the pass, rather than stamping it, if the tree moved underneath. Neither half is
   sufficient alone; the lease stops the race, the re-read catches the lease being bypassed.

   **A preparation has an owner, a heartbeat and a sweep.** `Requested | Building | Ready | Failed` is a
   state machine, and a state machine with no owner is the `SweepAsync` finding again — the audit's most
   instructive one, a crash-recovery path fully implemented and called by nothing
   (`.claude/rules/shared/common/reliability.md` § Background work). A qln restart mid-pass leaves a row
   in `Building` forever, and because §3.4 blocks every cell whose preparation is not `Ready`, that one
   stranded row blocks every cell of that variant for the life of the deployment — a stall that looks
   exactly like a slow index. So the row carries the `WorkerIdentity` that started it and a heartbeat
   stamp refreshed while the pass runs, and `BenchRunWorker`'s startup sweep (§3.6) walks preparations
   beside cells: `Building` past a configured window with no live qln pass behind it becomes
   `Failed(reason)`, which is a state the operator can retry from. The window is configuration, and it is
   longer than a real pass — a 24-minute aspnetcore pass must never be swept out from under itself.
3. **Retrieve** — `QlnEngine` grows the variant's axes: `AxesWire` (`QlnEngine.cs:235-238`, one `limit`
   field) extends from
   `limit`-only to the full definition (channels, fusion mode + params, rerank, textShape). The response's
   echoed axes are stored — proof of what actually served the query, not what was asked.

   **Stored is not enforced, and only enforced is proof.** `Platform.Contracts/SearchModels.cs:15-101`
   declares `SearchAxes` with no `JsonUnmappedMemberHandling.Disallow` and nothing in that repository sets
   it globally, so an un-upgraded qln handed `fusion: "wsum"` ignores the field, runs RRF, and echoes back
   a perfectly well-formed `SearchAxes` that simply has no fusion member in it. Storing that echo records
   `wsum` in the variant's `DefinitionJson` and RRF in the numbers, with nothing anywhere disagreeing.
   This is the reranker scar exactly — a stale pinned port left four measured arms running with no
   reranker while the settings page reported one — and it is the failure the echo discipline was invented
   to end, defeated by keeping the echo and not reading it. So the runner **asserts** the echo: every axis
   the variant's `DefinitionJson` names must appear in the response's axes with the requested value, and a
   missing or differing axis **blocks the cell with a named reason** carrying both values. Same move, same
   reason as the surface fingerprint in [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) §3.3 and the
   corpus recipe in [PLAN_corpus_axis_integrity.md](PLAN_corpus_axis_integrity.md) §3.2; a blocked cell is
   visible and cheap, a mislabelled measurement is permanent. qln's half — refusing an unknown axis field
   at the boundary instead of dropping it — is named in the sibling plan §3.1 and §7, because an engine
   that refuses is one the benchmark never has to catch.
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

### 3.4a The corpus budget: how many are resident, who evicts, and who owns them

The number this section exists for is **96**, and it is arithmetic rather than a worry.
`dew_flow_rag_qln · todo/PLAN_corpus_variants.md` promises 24 embed-text variants alive at once for one
project (2³ optional ingredients × 3 forms). Its sibling then adds two more identity axes: `ChunkTokens`
at 256 and 512 (`PLAN_search_variant_axes.md` §3.3) and a second dense embedder (§3.4). Collection
identity already fingerprints all of them — `model`, `shape`, `window`, `overlap`, written out field by
field at `dew_flow_rag_qln · src/Rag.Domain/Corpus/CorpusVariant.cs:92-93`, precisely so that adding a
field cannot silently re-hash — so those are not three ways of describing 24 corpora. They are 24 × 2 × 2
= **96 coexisting collections, per target repository**. And 96 is the floor, not the ceiling:
[PLAN_corpus_axis_integrity.md](PLAN_corpus_axis_integrity.md) §3.1 proposes `ChunkStrategy` and
`Tokenizer` as two more identity fields, each of which multiplies again.

What that costs is measured, not estimated. [PLAN_corpus_litter.md](PLAN_corpus_litter.md) records one
`dotnet/aspnetcore` corpus at **75 218 points ≈ 2 GB**. Ninety-six of them is **~190 GB of Qdrant for one
target repository** — against a recorded incident in which **24.38 GB** was already the crisis that
produced that plan. A matrix that quietly assumes all 96 are resident is a matrix that fills a disk in an
afternoon and reports the failure as "no space" during an unrelated run.

So this plan states the budget rather than inheriting it:

- **The budget is in BYTES, with a count cap beside it, and both are configuration.** A count alone is
  dishonest across targets: 96 corpora of this repository is minutes and megabytes, 96 of aspnetcore is
  190 GB. The store's own reported size is the number the budget is checked against — the same figure the
  retention listing prints, never a second estimate that can drift from it.
- **Eviction is least-recently-SERVED, and it refuses to evict a corpus any unsettled cell still needs.**
  A corpus under an open test is not a cache entry. Eviction is recorded — which corpus, when, at whose
  request — so a later rebuild is an explainable cost rather than a mysterious 24 minutes.
- **A matrix corpus is the THIRD ownership row of `PLAN_corpus_litter`, not the second.** That plan's
  middle row ("the arm builds a corpus for the cell → delete when the run ends") is the leaking shape it
  was written to close, and it is the wrong row for anything here: `ExpandAsync` (§3.2) exists precisely
  to reopen a settled test months later when a variant or subject is added, and a corpus deleted at run
  end makes that expansion a full re-index of a tree that has not changed. Matrix corpora are *"built to
  be compared across runs — keep, and account for it"*. Accounting for it is this section; the amendment
  that stops the delete-on-finish default from applying to them is in that plan.
- **Blocked, never silently smaller.** An expansion whose corpora would exceed the budget is refused with
  the projected figure, exactly as an oversized expansion is refused in §3.2. A matrix that silently ran
  fewer variants than it says it ran is the failure mode this whole plan is built against.

### 3.4b One accelerator, one lease — and it blocks the worker

The founding plan already answered this and the answer was then deferred: *"With N models and one card,
concurrent runs make every hardware number meaningless and every latency number a queue measurement. A
run takes a lease; the wait is its own bucket"* ([PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) §5.1,
scheduled as step 7b, behind everything). Since it was written, three new GPU consumers have been planned
ahead of it, and the one mitigation the sibling plan names is a property **this** document removes.

The consumers, as they will actually be deployed: qln's `IndexPassWorker` running a pass; the sidecar
holding dense, sparse and reranker engines; Ollama holding the second dense embedder resident, because
`OLLAMA_KEEP_ALIVE=-1` is set machine-wide here and means never evict
(`dew_flow_rag_qln · tests/Rag.Tests/Runtime/RuntimeFactTests.cs:12`); and now **two** benchmark drains
at once, since §3.6 puts execution in a `BenchRunWorker` while the CLI keeps working headless against the
same tables. `dew_flow_rag_qln · todo/PLAN_search_variant_axes.md` §3.4 rests its VRAM answer on "the
benchmark's one-cell-at-a-time claim loop" — which was true of a single CLI drain and is not true of two
front doors. This family has already recorded what happens when the arithmetic is left to chance: two
concurrent index passes co-loading a coder and an embedder for **30 GB on a 32 GB card**.

The minimum viable shape, specified so it can be built before it is needed rather than after:

- **One advisory lock, keyed on the accelerator** (the card, not the process). Postgres advisory locks are
  the right primitive because they are held by a *session* and released when it dies — a lease whose
  holder crashed must not need a sweep to come back.
- **It lives in qln, and the benchmark asks for it over HTTP.** The card is qln's — it owns the sidecar
  and the Ollama registry — and the family invariant is that the measured party never links the measurer
  (`dew_flow_rag_qln · research/repository_boundaries.md`), so the lock cannot live in this repository's
  database without pointing the arrow backwards. qln's pass takes it in-process; a benchmark cell that
  touches the GPU on its own account (a `bridge-local` subject, a local judge) takes it over the wire.
- **Max in-flight = 1**, with a ceiling on the wait and a named refusal past it — every wait has one
  (`.claude/rules/shared/common/reliability.md` § Every wait has a ceiling).
- **The wait is recorded in the existing infrastructure-wait bucket**, never in thinking time
  ([PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) §5.3). This is the whole point of the third bucket:
  a busy card must read as a busy card, not as a slow model.

**This blocks §3.6 and build-order step 9.** `BenchRunWorker` is the change that makes two drains real, so
it may not land before the lease exists; the sibling plan carries the same boundary from its side.

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
- Nothing stores an absolute local path, a secret, or a machine-specific value **anywhere in this
  database** — the database must survive publication unedited. The guarantee is deliberately written
  wider than "result rows": the model registry of §3.7 lives in the same database and would otherwise be
  the one table that carries an endpoint, a CLI path and a credential, which is exactly what a
  publication redaction pass would then have to find by hand. (Publication itself: deferred by operator
  decision — "Postgres now, a mirror server later." Open question §8.3 records the intent.)

#### What grows, and who owns it

This section adds three append-only payload surfaces — `funnels`, `retrieved_hits`, and `ThinkingText` +
`ResponseMetaJson` on `results` — and the shared rule is that an append-only table **names its retention
or rollup** (`.claude/rules/shared/common/reliability.md` § Everything that grows has an owner; the
violation it cites is `dew_flow_rag_qln · index_passes`, one row per pass and deleted never). The founding
plan put it harder, and its reason is the one that matters here: decide retention **before the first
write**, because the budget belongs in the schema rather than in a clean-up job someone writes after the
disk fills.

The projection, at this plan's own stated target of *"tens of thousands of cells"*
(`src/Bench.Infrastructure/Persistence/BenchDbContext.cs:165,171`): one cell stores a prompt with its
retrieved context, an answer, thinking text, a funnel, and `limit` hits with their signatures and
snippets. At a default `limit` of 20 the hits alone are the largest part, and a conservative few tens of
kilobytes per cell puts 50 000 cells in the **low hundreds of gigabytes** — in a database this plan
promises will survive publication unedited, which means the retention decision is also a decision about
what the published artefact is.

The owners, one per surface:

| surface | owner |
|---|---|
| `results.Prompt` / `Answer` / `ThinkingText` / `ResponseMetaJson` | **kept forever** — this is the artefact; the whole point is that a published number can be re-checked against the text that produced it. Its size is therefore a *budget line*, printed by the retention listing, not a cleanup target. |
| `funnels` | kept forever. Small and fixed-size per cell; it is the white-box evidence. |
| `retrieved_hits` | **rolled up.** Ranks, scores, paths, line spans and channel membership are kept forever — every retrieval metric recomputes from them. The hit **snippet text** is the bulk and is redundant (the corpus at the pinned commit reproduces it), so it is kept raw for a configured window and dropped after, leaving the row intact and the metrics recomputable. The reference shape is `dew_flow_rag_qln · src/Rag.Infrastructure/Runtime/SizeHistoryStore.cs:76,159-204` — 7 days raw, hourly rollup beyond, one place, tested. |

A cell's projected bytes is printed by the expansion preview of §3.2, beside its row count, for the same
reason: a cost that is only discovered after the run is a cost nobody chose.

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
- **`BenchRunWorker` is a HOST for `LegDrain`, not a second drain.** `src/Bench.Application/LegDrain.cs`
  shipped on 2026-08-16 (582f1e9) carrying exactly what a long-running worker needs and what a
  hand-written loop reliably forgets: the per-unit `try` that skips one bad leg instead of ending the
  campaign, the consecutive-failure breaker, bounded backoff so a lost claim race cannot spin at zero
  delay, a grace window so a cancelled leg settles its cell instead of stranding it, and a typed
  `DrainStop` so "the queue is empty" is never confused with "we stopped early". The worker supplies the
  leg delegate and its `DrainLimits` exactly as `RunCommand.ExecuteAsync` does today
  (`hosts/Cli/RunCommand.cs:153`), and adds only what a hosted service adds: the startup sweep and the
  lifetime. Writing a second drain is a defect from the moment it compiles — the two would drift, and the
  copy would be the one running unattended for weeks
  (`.claude/rules/shared/common/reuse-first.md`).
- **Two dictionaries stopped being a gate on 2026-08-16.** `LiveTrace._byLeg` and
  `GitCheckoutProvider`'s lock map both `GetOrAdd`-ed and never removed; they were harmless only while
  nothing long-running wired them in, and this worker is what would have changed that.
  [../research/PLAN_reliability_tail.md](../research/PLAN_reliability_tail.md) item 2 shipped first, as
  its build order required: capture now evicts the recorder it hands over, and the checkout gates are
  reference-counted. This worker may wire them in.
- **The accelerator lease of §3.4b is the other gate.** This worker is what makes two concurrent drains
  real, and two drains are what invalidate the sibling plan's only stated VRAM mitigation.
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
     claimed — the `CompanyProjects` poll shape (start only when in-flight, stop when quiet) **as
     repaired by `dew_flow_rag_qln · todo/PLAN_reliability_tail.md` item 6 — done 2026-08-16, and it is no
     longer a shape to copy but a TYPE to reuse**: `dew_flow_rag_qln · src/Rag.Ui/Services/LivePoller.cs`,
     about seventy lines with no dependency beyond `ILogger`. Port it rather than re-deriving it, and keep
     its two findings, both of which cost a session of silent staleness each: the catch-all logs and sets a
     `StoppedReason` the page renders as *"live updates stopped — reload"*, and the quiet path is filtered
     on **the token** rather than on the exception type — `TaskCanceledException` derives from
     `OperationCanceledException`, so an unfiltered catch swallows every HTTP timeout as a normal ending.
     The shape as it stood before that fix ran its loop in a detached `Task.Run` catching
     `OperationCanceledException` and nothing else, with no logger in the file, so any other exception ended
     polling for the rest of the session and recorded nothing.
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
              ConfigJson (REFERENCES only — see below), Enabled, CreatedAt
run_subjects: RunId, ModelKey, AddedAt          -- the test's answering models (§3.2, add-only)
run_judges:   RunId, ModelKey, Ordinal          -- the test's arbiters, ordered: first = primary
```

- **`ConfigJson` holds REFERENCES, never values.** An environment variable's NAME, a user-secrets key, a
  configuration section path — resolved at use, stored never. The obvious shape ("endpoint, model id, CLI
  path, sampling defaults") puts a machine's absolute paths and, sooner or later, an API key into the one
  database §3.5 promises will survive publication unedited; a guarantee scoped to result rows while the
  registry sits in the same schema is not a guarantee, it is a redaction pass nobody has scheduled.
  Sampling defaults are ordinary data and stay as values — they are neither secret nor machine-specific,
  and a run must be able to say what sampling it asked for. What leaves is anything whose value is a
  property of *this machine* or *this account*.
- The settings page manages the registry; `bench models add|disable|list` is the CLI face. Every role a
  model can play — subject, arbiter, and (phase 2) question author and reviewer — draws from this one
  list; `reviewers` (§3.3) gains an optional `ModelKey` so an automated reviewer is the same identity
  everywhere. A registry row whose referenced secret or path does not resolve on THIS machine is refused
  at test creation by name, exactly as a disabled model is — the same failure mode, discovered at the
  same moment, instead of three hours into a sweep.
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
- Ordered last: the single-shot lane must be proven end to end first. **Everything in this section is now
  designed and built by [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md)** — a lane becomes a catalog row
  there, with a doctrine preamble as the primary axis and an L0/L1/L2 ladder; what survives here is the
  lane AXIS in the matrix and the registry row that names a CLI subject. The full division is §3.8a.

### 3.8a The boundary with the tool benchmark — named from THIS side too

[PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) is the full design of the §3.8 agent lane, authored
after this document and delimiting its slice carefully, in prose, from its side. This document said
nothing, and a reader starting here would build `CliAgentRuntime` a second time — the failure the
planning rule now names explicitly: *a division of labour named on one side is not a division of labour*
(`.claude/rules/shared/common/planning-docs.md` § A boundary between two plans is named on BOTH sides).
The same table, from here:

| Item | Built by | This plan's part |
|---|---|---|
| **`CliAgentRuntime`** — a cloud CLI headless, per-leg workspace, MCP config injection, budgets | **the tool benchmark** (its step 11) | none. §3.8 and step 11 below are **superseded** by that plan; what remains here is that a `Runtime = cli-*` registry row is a legal subject. |
| **The `agent-mcp` lane** | **the tool benchmark** — a lane is a catalog row there, immutable and hashed, with a `MaxTurns` and a doctrine preamble | this plan keeps `Lane(Name, Preamble)` as a matrix AXIS and crosses it with variants; it does not define what a tool lane contains |
| **Telemetry correlation** (agent MCP calls landing in `tool_telemetry` attributed to the leg) | **the tool benchmark**, with `dew_flow_mcp · research/PLAN_tool_surface_config.md` as its other half | this plan consumes the attributed rows for the per-cell byte and count figures of §3.5 |
| **`LegRunner` step ordering** — where phases and a tool loop sit inside a leg | **the tool benchmark** (§3.4: one collaborator, the runner keeps its shape) | this plan inserts retrieve-then-ask for a `qln` variant (§3.4 steps 3–4); the two must compose, and the tool plan is the one that decides the ordering |
| **The console shell** (`hosts/Web`, `hosts/Web.Client`, `src/Bench.Ui`) | **this plan** (§3.6) | the tool benchmark mounts its pages in that console and builds no shell of its own |
| **The variant catalog** (`Bench.Domain/Variants/*`, `Bench.Application/Variants/*`) | **this plan** (step 1, landed 2026-08-16) | the tool benchmark builds a structurally parallel lane catalog in new files and touches none of these |

Disjoint: this plan owns the question bank, the model registry, expansion, index preparation and the
console; that one owns lanes, the doctrine axis, the tool loop and the L0/L1/L2 ladder. They meet in the
matrix, where a test crosses retrieval variants with tool lanes, and nowhere else.

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
5. **`/search` refuses an unknown axis field rather than ignoring it** (`JsonUnmappedMemberHandling.Disallow`
   on the axes contract, absent today at
   `dew_flow_rag_qln · src/Platform.Contracts/SearchModels.cs:15-101`). Without it, an un-upgraded engine
   silently serves RRF for a `wsum` request and echoes a record that merely lacks the field — §3.4 step 3.
6. **A pass re-reads HEAD at pass END** and fails rather than stamps when the tree moved mid-scan, and
   **the indexing checkout is leased** for the duration of a pass — §3.4 step 2.
7. **An accelerator lease, served by qln**, taken by its own pass in-process and by this repository over
   HTTP for any cell that drives the card on its own account — §3.4b.
8. **The collection prefix is selectable per pass and per search**, so benchmark-created corpora are
   `bench_…` and "sweep everything this tool made" is decidable from a name
   ([PLAN_corpus_litter.md](PLAN_corpus_litter.md)). The application layer already carries it
   (`FastLaneRequest.CollectionPrefix`, `SearchRequest.CollectionPrefix`); nothing exposes it over HTTP.

## 5. Build order

Each step ships alone, tests green, before the next starts. **API-first throughout: a UI step never
precedes the API + CLI it renders.**

1. **Variant catalog** — `variants` table + domain type + immutability tests + `bench variants` verbs.
   `cells.VariantId` migration. `Matrix.Plan` variant axis + rotation-balance tests.
2. ~~**Question bank**~~ — **IMPLEMENTED 2026-08-16.** All five tables plus `run_questions`, the import
   verb (and the four the plan did not name — see the status block), selection freeze through
   `AuthoringBatch.Promote` + `Suite.Freeze`, and `bench run --bank-group`. The promotion refusals
   (collision, empty, and a duplicate id the plan did not anticipate) are asserted in `BankFreezeTests`;
   the concurrency rules — one key, one suite-facing id, one mark per reviewer per question, one snapshot
   per test — are asserted against real Postgres in `PostgresQuestionBankTests`.
3. ~~**Model registry**~~ — **IMPLEMENTED 2026-08-16.** The three tables, `bench models
   add|list|disable|enable`, and `bench run --subjects/--judges` reading registry keys. Resolution happens
   before any cell exists: a disabled model, an unknown key and an environment variable that is unset on
   THIS machine are each refused by name. The multi-subject defect this uncovered — one endpoint for a
   matrix that always planned several — is in the status block.
4. **Checkout + engine wiring** — ~~`ICheckoutProvider` into run start~~ **(landed 2026-08-16:
   `bench run` mirrors and checks out the pinned commit before anything is created, `--no-checkout` keeps
   the old behaviour and its warning, `RunCommand`'s unconditional "unverified" line is gone)**;
   `QlnEngine` full `AxesWire`;
   engine-per-variant resolution in `LegRunner`; single-shot RAG prompt assembly; funnel + hits + thinking
   persistence (§3.5 migrations); retrieval metrics. Verify the `collapse` repair end to end here.
5. **Index preparations** — the table with its owner + heartbeat, the qln index-state poll, the writable
   indexing checkout **and its lease**, the block-with-reason path, the echo assertion of §3.4 step 3.
   (Depends on sibling plan §3.2 landing first; until then cells block honestly.)
6. **Expansion** — `ExpandAsync` over variants AND subjects + derived progress + CLI `bench expand`,
   chunked inserts and the size cap of §3.2. Explicit tests: settle everything, add a variant → reopen;
   add a subject → reopen; % = settled ÷ current total.
6a. **The corpus budget and the accelerator lease** (§3.4a, §3.4b). Both are cheap while nothing is
   resident and expensive to retrofit once 96 corpora and two drains exist. **Step 9 may not land before
   this one.**
7. **API read surface + reports** — run/matrix/variant/question/model endpoints in `Bench.Api`, plus a
   `bench report` verb surfacing the comparison queries that today have no caller
   (`AverageByEngineAsync`/`AverageByLaneAsync`).
8. **Console, read paths** — `hosts/Web` + `Web.Client` + `Bench.Ui` skeleton, AppHost registration;
   settings, questions, matrix and variant pages over the step-7 endpoints.
9. **Console, write paths** — new-test flow, `BenchRunWorker` hosting `LegDrain` (§3.6), start/expand
   buttons, polling, sweep on startup. **Gated on three things, all of them cheaper before than after:**
   step 6a's accelerator lease (two drains against one card), ~~`PLAN_reliability_tail.md` **item 2**~~
   (the two unbounded dictionaries this worker makes live — **cleared 2026-08-16**, see
   `research/PLAN_reliability_tail.md`), and
   ~~`dew_flow_rag_qln · PLAN_reliability_tail.md` **item 6** (the poll shape §3.6 page 3 copies)~~ —
   **cleared 2026-08-16**: it shipped as `LivePoller`, so page 3 reuses a type instead of copying a shape.
   Only step 6a's lease still gates this step.
10. **Judges** — ordered per-test arbiters from the registry, per-group rollups + the analysis block.
11. ~~**Agent lane**~~ — **superseded by [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md)**, which owns
    `CliAgentRuntime`, the `agent-mcp` lane and telemetry correlation as its own step 11 (boundary table,
    §3.8a). What remains here: a `Runtime = cli-*` row is a legal subject in the registry, and the lane
    axis crosses the variant axis in the matrix.

## 6. Test plan

- xUnit v3 exe (never `dotnet test`), `PostgresFixture` (Testcontainers) for every table this plan adds;
  migrations applied for real, loud failure without Docker — the repo's existing discipline.
- Domain: variant immutability/refusal-of-unknown-axes; matrix balance with the third axis; expansion
  idempotence (run twice = no new cells); derived progress across the reopen scenario.
- Bank: one-mark-per-reviewer uniqueness; only-Accepted-selectable; selection freeze produces a stable
  stamp for the same selection and a different stamp for a different one.
- Engine: `AxesWire` round-trip against a fake qln (echoed axes stored, degraded funnel stored with
  reason); index-prep blocking on commit mismatch. Plus the enforcement half, which is the one that
  matters: `A_variant_axis_the_engine_did_not_echo_blocks_the_cell` — a fake qln that accepts `wsum` and
  answers with axes lacking the field must block, naming both, never store a result.
- Preparations: `A_preparation_stranded_by_a_restart_is_reopened_not_left_building` — a `Building` row
  whose owner is gone and whose heartbeat is past the window becomes `Failed(reason)` at the worker's
  startup sweep, and a preparation whose pass is genuinely still running is left alone (the `SweepAsync`
  ownership lesson: a sweep that requeues live work is worse than one that never ran).
- Budget and lease: an expansion projected past the corpus budget is refused with its figure; eviction
  never selects a corpus an unsettled cell needs; two cells contending for the accelerator serialize, and
  the loser's wait lands in the infrastructure bucket rather than in thinking time.
- Publication safety: `No_stored_model_configuration_contains_an_absolute_path_or_a_secret_shaped_value`
  — over every `models.ConfigJson` row, asserting the §3.7 rule mechanically rather than by review.
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
- [ ] Every axis a variant names is present in the engine's echo with the requested value, or the cell is
      blocked naming both — a stored echo nobody compares is not proof.
- [ ] The resident-corpus budget is stated in bytes, enforced at expansion, and its eviction rule cannot
      take a corpus an unsettled cell needs; a matrix corpus is accounted for, never delete-on-finish.
- [ ] Nothing touches the accelerator without the lease; the wait is in the infrastructure bucket.
- [ ] The indexing checkout is leased for the whole of a pass, and a pass whose tree moved mid-scan fails
      instead of stamping a commit it did not index.
- [ ] `index_preparations` has an owner, a heartbeat and a startup sweep; no restart can leave a variant
      blocked in `Building` forever.
- [ ] Every append-only surface this plan adds names its retention or rollup, with a projected size.
- [ ] `models.ConfigJson` contains no absolute path, no secret and no machine-specific value — asserted by
      a test, and the publication guarantee covers the whole database rather than result rows alone.
- [ ] `BenchRunWorker` hosts `LegDrain`; there is exactly one drain implementation in the repository.
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
