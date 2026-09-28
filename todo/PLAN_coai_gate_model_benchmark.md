# PLAN — the coai gate-model benchmark: plan, diff and feature gates, in C#, re-runnable

> Status: **E1 (the domain and the contracts) and E2 (the store and the privacy guard) landed 2026-09-27, E3 (the
> driver), E4 (the blinded strict assessment), E5 (the import) and E6 (the report, the API and the page) 2026-09-28 —
> `research/module_gate.md` describes them; E7 (the first campaign) open.** Scope: a new bounded context `Gate` across
> `src/Bench.Domain`, `src/Bench.Application`, `src/Bench.Infrastructure`, `src/Bench.Contracts`,
> `src/Bench.Api`, `src/Bench.Ui` and `hosts/Cli`; new Postgres tables `gate_*` (no existing table is
> touched); a private artefact root OUTSIDE git; a hashed `prompts/gate-assess/` catalog; one `Gate` tab in
> the console. The product under measurement is `coai-mcp` (ConnectOtherAIs), driven the way a person's
> editor drives it — never a side harness.
>
> Related docs: [architecture.md](../research/architecture.md), [MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md),
> [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) §6 (surfaces), [PLAN_scoremeter_port.md](PLAN_scoremeter_port.md)
> (the permanent-payload precedent), [PLAN_bench_console.md](../research/PLAN_bench_console.md) (how a page is
> mounted), [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) (the catalog-row precedent).
> Cross-repository citations are **paths, not links**: `coai ·` is `dew_flow_connect_other_ais`; `calib ·` is the
> operator's local Python harness under the session scratchpad (`calib/harness/*.py`, not in any repository).

## 1. The goal, before any solution

The operator's ask (2026-09-27): *write the tests into `dew_flow_benchmark`; a SEPARATE page for testing coai
models on the feature gate; the analogous tests for the plan gate and the diff (code) gate; everything in the
benchmark project, in C#, so it can be run again.*

What exists today is three instruments in two languages, in three places, with three record shapes and two
rubrics — and none of them is in this repository:

| gate | what measured it | language · where | runnable again? | what it recorded |
|---|---|---|---|---|
| **plan** | thirteen models × two runs over one seeded plan (8 planted defects D1–D8), scored by reading (`coai · research/RESULTS_model_comparison.md`); five real plans × five models, agreement by reading (`RESULTS_five_plans_five_models.md`); twelve focused lenses × three wordings (`RESULTS_focused_prompts.md`) | throwaway scripts of 2026-09-01/02; raw JSON on the operator's machine, some paths named in those documents | **no** — each was "written by hand at least twice" (`coai · src_bench/README.md:4`) and no script is in git | counts, seconds, tokens, a hand-made cluster file |
| **plan + code** | `coai-bench` — the kept instrument: `open → review_plan (≤4 rounds) → resolve accept-all → review_code → resolve`, one server process per run, the operator's own panel settings, Fable/Opus judging *worth having* one finding at a time | **C#**, `coai · src_bench/CoaiBench` (18 files, 16 test classes, 80 tests on 2026-09-05); campaigns in `research/data/bench-2026-09-05`, `bench-2026-09-06`, `artifacts/bench/*` (`runs.json`) | **yes** (`coai-bench run --exe … --repo . --corpus … --arm … --repeat n`), against the *installed* binary | `RunRecord` → `StageResult` → `Finding{Useful,Verdict}`; no planted defects on the code side, so recall is unscorable there (`RESULTS_model_comparison_code.md`: *"the obvious next measurement"*) |
| **feature** | the pack trial (86 cells, 319 blinded verdicts, `RESULTS_feature_pack_trial.md`, 2026-09-26) and the reviewer-model calibration (`RESULTS_feature_reviewer_models.md`, in progress: 48 of 84 phase-2 runs on 2026-09-27) | **Python**, `calib/harness/{run,mcpclient,tap,inputs,models,check,assess,report}.py` (~1 730 lines) over the PRODUCT (`review_feature` over MCP stdio, one `api` vendor per run, an isolated `COAI_DATA_DIR`); the earlier `trial/` and `models-trial/` harnesses beside it | **yes, on one machine** — `python harness/run.py --phase 2 --plan` resumes from `runs.jsonl`; every path in it is absolute and local | `runs.jsonl` (one line per run: transport preset, product sha + dirty count, validity, verdict, findings, turns, HTTP calls, finish reasons, tokens in/out/cached/reasoning from the ledger AND the wire, per-turn seconds, cost, served/refused source requests, failure cause); `runs/<id>/` (request, reply, server stderr, the run's data dir with `usage.jsonl`, every HTTP exchange, every prompt/answer file); `assess.jsonl` + `assess-key/key.json` (not yet produced: 0 findings assessed) |

Three things follow, and they are the goal:

1. **One measurement model for the three gates**, so a plan-gate number and a feature-gate number are the same
   kind of number: the subject is a reviewer model *plus its calibrated transport*; the task is a frozen seeded
   case; a run produces findings; a blinded strict assessment says which were supported and which seeds were
   hit; time, tokens (in, out, cached, reasoning) and cost per run; **≥ 3 repeats**; persisted after every run,
   resumable.
2. **Through the product path only.** A run drives the `coai-mcp` binary over MCP stdio with the real tools
   (`review_plan` / `review_code` / `review_feature`), a data directory of its own, and the binary's version
   pinned on the run. The Python harness already does this for the feature gate; `coai-bench` does it for plan
   and code. Nothing here re-implements a pack builder, a prompt or a parser.
3. **A page.** *coai models — feature gate*, with plan-gate and code-gate views beside it, reading the same
   per-model table the CLI prints — valid %, seeds hit (mean and range), strict-supported %, high-value
   findings per run, overstated %, time p50/p90, tokens and cache %, cost per run and per seed, run-to-run
   variance — and showing **today's** Python measurement on day one, imported read-only, without spending a
   cent to re-run it.

The repository is **public** (`gh repo view --json visibility` → `PUBLIC`, checked 2026-09-27). Seven of the
fourteen task repositories behind the feature trial are corporate, and the packs, prompts and answers carry
their source. So the fourth goal is a constraint rather than a feature: **task definitions, raw prompts and
answers live outside git and outside the published database**; only anonymised numbers, task ids, languages
and methods are committed — no repository or company names, no user paths, no keys.

## 2. What already exists here, verified

Every reference was opened on 2026-09-27 at `74cac65` (`origin/main`).

| capability | where | reused how |
|---|---|---|
| `Outcome<T>` — expected failures as values | `src/Bench.Domain/Outcome.cs` | every refusal below |
| `StableHash.Of` | `src/Bench.Domain/StableHash.cs:18` | suite stamp, settings hash, prompt hash, reviewer-row hash |
| `Captured` / `CapturedCount` — *not captured* ≠ zero | `src/Bench.Domain/Trace/LegTrace.cs:10`, `:21` | tokens, reasoning tokens, cost, cached tokens on a run |
| `ModelRef.Parse` refuses an unset id | `src/Bench.Domain/Runs/Axes.cs` (`ModelRef`) | the reviewer's model id |
| registry rows hold **references, never values** (`ModelConfig.Parse`, `LooksLikeAValue`) | `src/Bench.Domain/Registry/ModelConfig.cs:40`, `:55`, `:72` | the vault access key and any machine-local endpoint on a reviewer row |
| `ISecretSource` + `EnvironmentSecrets` — a reference resolved at use, refused by NAME when unset | `src/Bench.Application/Registry/ModelRegistry.cs:48`; `src/Bench.Infrastructure/Models/EnvironmentSecrets.cs:17` | `COAI_CREDS_KEY` reaches the child's environment and nowhere else |
| the one process launcher: exe + argv, timeout as a value, kill the whole tree, stdin without a BOM | `src/Bench.Infrastructure/Process/ProcessRunner.cs:71` | the assessor CLI launch; extended (not copied) for the long-lived MCP pipe — §3 D4 |
| claim / settle / sweep as pure functions; a sweep that is ownership-checked | `src/Bench.Domain/Runs/RunCell.cs:101` (`CellLifecycle`), `:111`, `:146`, `:165`; `src/Bench.Domain/Runs/WorkerIdentity.cs:21`, `:55`; `src/Bench.Infrastructure/Persistence/PostgresRunStore.cs:58` (`ClaimNextAsync`), `:126` (`SweepAsync`) | the gate's cells — widened, §3 D1 |
| the drain: one failed leg recorded and skipped, a consecutive-failure breaker, a planned stop with grace | `src/Bench.Application/LegDrain.cs:82`, `:84` (`DrainAsync(Func<CancellationToken, Task<Outcome<LegResult>>> leg, …)`), `:46` (`DrainLimits.Default`) | reused through a leg delegate; the per-endpoint pools sit outside it — §3 D14 |
| the matrix's global slot rotation (balanced first positions at odd repeat counts) | `src/Bench.Domain/Runs/Matrix.cs:69` (`Plan`), `:109` (`Rotated`, private) | extracted to a shared `SlotRotation` — §3 D1 |
| a read-only checkout: a bare mirror per url, a worktree per commit | `src/Bench.Application/Ports.cs:32` (`ICheckoutProvider`); `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs:51` | the assessor reads the seeded variant through it; `review_code`/`review_feature` are handed the checkout the product needs |
| a CLI agent launched with the prompt on stdin, argv pinned per kind | `src/Bench.Application/CliAgent.cs:20`, `:30`, `:45`; `src/Bench.Infrastructure/Models/CliAgentRuntime.cs:36` (`CliArgv.For`) | the blinded assessor — widened with the read-only sandbox and output-schema flags, §4 S4.3 |
| the arbiter that re-scores stored answers and never re-runs a leg; `SelfJudged` counted, not refused | `src/Bench.Application/JudgeRunner.cs:16`, `:36`; `src/Bench.Application/Ports.cs:232` (`IJudge`) | the shape of the assessment pass; the family-match flag mirrors `SelfJudged` |
| a permanent, append-only payload table sized as a budget line | `src/Bench.Domain/Trace/StagePayload.cs`; `src/Bench.Application/Ports.cs:249` (`IStagePayloadStore`) | the precedent for *the artefact is kept forever* — here on disk, outside git |
| a hashed prompt catalog (`prompts/author`, `prompts/review`) | `src/Bench.Application/PromptCatalog.cs:44`, `:107` | `prompts/gate-assess/strict.md`, hashed onto every verdict |
| immutable catalog rows: added and retired, never edited, hashed, unknown fields refused | `src/Bench.Domain/Variants/RetrievalVariant.cs:36`, `:105` | the reviewer catalog — §3 D6 |
| the console: `Read<T>` (arrived / missing / unasked), one API service, pages by primary-constructor DI, `PendingKind` for a declared-but-unbuilt tab, bUnit over `ScriptedBenchApi` | `src/Bench.Ui/Services/BenchConsoleApi.cs:23`, `:49`, `:129`; `src/Bench.Ui/Components/BenchmarkTabs.razor:25`, `:33`; `src/Bench.Ui/Pages/SidecarArms.razor.cs`; `tests/Bench.Tests/Ui/ScriptedBenchApi.cs:24` | the Gate tab and its three pages — §3 D11 |
| the API route group, 400-against-404, DTOs shared with `--json` | `src/Bench.Api/BenchApi.cs:23`, `:54`; `src/Bench.Application/RunReportContract.cs:20`, `:48` | `/api/bench/gate/*` |
| the CLI: verbs dispatched in one switch, one container per verb, exit codes as a contract | `hosts/Cli/Program.cs:79`, `:92`; `hosts/Cli/CliContainer.cs:30`, `:69`; `hosts/Cli/ExitCodes.cs:12`; `hosts/Cli/RunCommand.cs:62` (`DefaultCheckoutRoot`) | `bench gate …` |
| the layering guard | `tests/Bench.Tests/ArchitectureTests.cs:13`, `:40` | two new assertions — §3 D1 |

And in coai, the instrument this plan **ports mechanism from**, and the product surface it drives (the product
references are from the `feat/feature-review-e3-dialects` checkout the calibration built, `98acfa74`):

| what | where |
|---|---|
| the protocol, in the order the product enforces; `Passed` = `proceed \| good_enough \| continue_anyway`; ≤ 4 plan rounds; accept-all resolves; the resolve reply KEPT | `coai · src_bench/CoaiBench/Running/RoundRunner.cs:27`, `:30`, `:33`, `:86`, `:114` |
| one server process per run over newline JSON-RPC; `initialize` + `notifications/initialized`; stderr kept; kill the tree on dispose | `coai · src_bench/CoaiBench/Running/GateClient.cs:99`, `:111`, `:170` |
| a ref per run (`bench/<arm>/<case>-r<n>`), the environment for a cell (panel settings + `COAI_PROVIDERS` + `COAI_VENDORS` + `COAI_CALLER_SESSION`), one caller identity per run, a data dir per run or one shared | `coai · src_bench/CoaiBench/Running/Bench.cs:101`, `:123`, `:149`, `:207` |
| the vendor rows read from the panel's `settings.json`, written back as the one JSON string the server reads | `coai · src_bench/CoaiBench/Running/Vendors.cs:59`, `:123` |
| asked-for settings against the session's own config on disk; the session read scoped to THIS run | `coai · src_bench/CoaiBench/Running/SettingsApplied.cs:19`, `:34`; `OnDisk.cs:10`, `:41` |
| the record: `Case`, `Finding{Useful = "unjudged"}`, `StageResult`, `RunRecord.Key` | `coai · src_bench/CoaiBench/Model/BenchRun.cs:8`, `:26`, `:47`, `:54`, `:94`, `:104` |
| the lenient judge: one finding per call, the file windowed ±80 lines at the reviewed commit, one turn, no tools; a pass that saves after every run and skips what THIS judge already answered | `coai · src_bench/CoaiBench/Judging/Judge.cs:29`, `:45`, `:111`, `:162`; `JudgePass.cs:20`, `:67` |
| who found it alone (overlap across arms) | `coai · src_bench/CoaiBench/Judging/Overlap.cs:44`, `:157` |
| the tools: `providers`, `open(repoPath, branch, callerModel)`, `review_plan(planText, …)`, `review_code(baseRef, planText, …)`, `review_feature(repoPath, planPath, baseRef, epics, lessons, again, callerModel)`, `resolve(decisions, …)` | `coai · src_mcp/src/Tools.cs:25`, `:54`, `:84`, `:117`, `:232`, `:311` |
| the ledger row the product writes per reviewer turn: `provider, model, role, stage, seconds, tokensIn, tokensOut, tokensCached, tokensReasoning, costUsd, outcome` | `coai · src_mcp/src/Api/AskApiMode.cs:299` (`UsageLine`); one `usage.jsonl` under the run's data dir (read 2026-09-27, a phase-2 run: `tokensReasoning: 10674` on the row) |
| the knobs: `COAI_DATA_DIR`, `COAI_VENDORS` (a JSON array in one string), `COAI_CALLER_SESSION`, `COAI_CREDS_KEY` | `coai · src_mcp/src/Server/PanelSettings.cs:668`, `:1132`; `CallerSessions.cs:46`; `KeyVault.cs:30` |
| the version: `serverInfo.version` on `initialize` (`Major.Minor.Build`), `--version` from the informational version (`0.0.0+<sha>` on a checkout build, the tag on a release) | `coai · src_mcp/src/Program.cs:1870`, `:2087` |

And the Python harness, function by function, so §3 D13 can say what ports and what is replaced:

| function | `calib ·` | what it does |
|---|---|---|
| `child_env` | `harness/run.py:103` | the child's environment: every `COAI_*` of the parent dropped; data dir, `COAI_CREDS_KEY` (read from the machine's coai settings, `:85`), `COAI_VENDORS` (one `api` row, `feature: true`), `COAI_FEATURE_MIN_EPICS=1`, follow-ups, ceiling, effort, per-turn timeout, the 20-minute whole-review cap, concurrency 2, consult off |
| `one_run` | `harness/run.py:256` | request → data dir → tap → `McpStdio` → `review_feature` → reply, stderr, answers, ledger, tap facts → `summarise` (`:195`) → `failure_cause` (`:232`) → `run.json` + one appended line (`append_record`, `:76`: write + flush + fsync); an id already recorded is skipped |
| `phase2_plan` | `harness/run.py:343` | repeats OUTERMOST so the three repeats of one task never run back to back; two pools of two, one per endpoint |
| `McpStdio` | `harness/mcpclient.py:10`, `:65`, `:71` | hand-rolled newline JSON-RPC over stdio, stderr to a file, kill by pid on close |
| `Tap` | `harness/tap.py:20`, `:78`, `:152`, `:177` | a loopback recording pass-through in front of the vendor: request body written BEFORE forwarding, raw response, status, wall, an absolute deadline that closes the upstream socket; `Authorization` forwarded in memory and never written (`:15`) |
| `request_of` / `synthetic_plan` / `epics_of` / `lessons_of` | `harness/inputs.py:111`, `:39`, `:62`, `:83` | the three caller inputs of `review_feature` per task, reconstructed from the trial's manifests |
| `MODELS` / `PRESETS` | `harness/models.py:10`, `:20`, `:28`, `:29` | endpoints, vault key NAMES, list prices, the calibrated transport preset per model |
| `export` / `run_task` / `ROW` / `INSTRUCTIONS` | `harness/assess.py:108`, `:177`, `:29`, `:39` | blinded export (fresh 8-hex ids, no model or run on a row, key aside), batches of 24 per task to `codex exec -s read-only --output-schema`, verdict rows appended per batch, prior cluster keys carried |
| `per_model` / `q` / `seed_evidence` / `write_results_md` | `harness/report.py:103`, `:60`, `:194`, `:249` | the per-model table (exactly the operator's columns), p50/p90, where each seed's evidence sat (pack / on request / withheld), the results document regenerated after every run |

## 3. Decisions

### D1 — a new bounded context, `Gate`, beside the retrieval benchmark; four things widened, nothing copied

The retrieval benchmark's run is ONE target (`MeasurementTarget`, `src/Bench.Domain/Targets/MeasurementTarget.cs:71`)
× one frozen suite × subjects × lanes × variants × repeats, whose leg is a completion the harness prompts
(`LegRunner`) and whose result row is prompt · answer · hits · funnel (`results`, `BenchDbContext.cs:240`). A gate
run is **seven targets** (one per task, each its own repository at its own variant commit) × reviewers ×
repeats, whose leg is a multi-turn product session the harness never prompts, and whose result is a reply of
findings plus a ledger. `Subject(ModelRef, Sampling)` carries sampling the gate cannot send (the product's
dialect decides it); `EngineRef` and `VariantSelection` mean nothing to it. Folding the gate into `cells` +
`results` would either leave half the columns empty per row or grow two tables that are published beside the
retrieval results with meanings that depend on the row's kind. So: a sibling context, its own tables
(`gate_runs`, `gate_cells`, `gate_findings`, `gate_verdicts`, `gate_reviewers`), its own report — and the parts
that ARE the same, made shared rather than duplicated:

1. **`Claimable`** — extract from `RunCell` the four fields the lifecycle actually reads (`State`, `Attempts`,
   `Owner`, `ClaimedAt`) into one record that `RunCell` and `GateCell` both compose; `CellLifecycle.Claim /
   Settle / Reclaim / IsStale` (`RunCell.cs:111`, `:146`, `:165`) become functions over `Claimable`. The
   existing tests keep passing unchanged, which is the proof the extraction changed nothing.
2. **`SlotRotation`** — `Matrix.Rotated` (`Matrix.cs:109`) becomes a public pure function in `Bench.Domain`
   that both `Matrix.Plan` and `GateMatrix.Plan` call; `MatrixOrderTests` keep pinning the 2:1 defect.
3. **`LegDrain`** is reused as it is: it takes a leg delegate (`LegDrain.cs:85`), so a gate leg is a
   `Func<CancellationToken, Task<Outcome<LegResult>>>` like any other. `LegResult` gains nothing; the gate's
   outcome is settled on `gate_cells` and the drain only sees success/failure/stop.
4. **`ProcessRunner`** stays the one-shot launcher. An MCP server is a long-lived pipe, which it cannot be; the
   widening is a `ProcessSession` in the same folder that shares `IsAlreadyGone` and the kill-the-tree path
   rather than a second `Process.Start` with its own escaping (`coai · GateClient.cs:170` is the shape).

Two new architecture assertions: every `Gate` type that decides anything lives in `Bench.Domain`; `Bench.Ui`
still references `Bench.Contracts` only.

### D2 — the measurement tuple of a gate run

```
gate      = plan | code | feature
task      = (taskId, language, seeds[], suiteStamp)      -- frozen, hashed; the repository is NOT in the tuple
reviewer  = (reviewerId, rowHash)                         -- model + runtime + calibrated transport, from the catalog
product   = (binarySha256, versionText, gitSha?, dirtyFiles?)   -- pinned per run, refused if it moves inside a campaign
settings  = settingsHash                                   -- every COAI_* as SENT, minus secrets, stored as JSON beside the hash
prompt    = promptHash                                     -- the product's own turn-1 prompt file, when the run left one
repeat    = ordinal, 1..n, n >= 3
```

`ComparisonScope` for the gate is `(suiteStamp, gate, product.versionText, settingsHash)`; everything else is an
axis compared along. A page that puts two products or two settings hashes in one column has folded two
populations — the compute-backend lesson, one level up (`architecture.md`, *The compute backend*). The scope is
on screen, as the arms page does it.

**Why the transport is part of the subject.** The calibration found that qwen3.8-max at its vendor default
never answered inside any deadline and answered in 5.4 minutes at `medium`; grok-4.7 at `high` took 20–26
minutes and at `medium` 9–12 (`coai · research/RESULTS_feature_reviewer_models.md`, *Phase 1*). A row that
named the model alone would put those under one label. The reviewer row therefore carries the dialect, the
effort, the ceiling, the per-turn timeout, the follow-up count and the whole-review cap — and its hash changes
when any of them does.

### D3 — one frozen seeded case serves all three gates

A seeded task is `(base, variant head, plan path, epics, lessons, seeds[])` where each seed is
`(id, file, old, new, what, trigger, mechanism, consequence, crossEpic)` — the trial's shape
(`calib · harness/assess.py:39`, the assessor reads `seed_spec`). The same case is:

| gate | what the product is called with | ground truth |
|---|---|---|
| plan | `open(repoPath, branch)` → `review_plan(planText)` — the plan text at the variant head | for a **seeded plan** (the 8-defect nightly-export plan of `RESULTS_model_comparison.md`): the defect list; for a real plan: agreement and the blinded value verdicts only |
| code | after a plan round `Passed`: `review_code(baseRef = base, branch = a ref at the variant, planText = the plan as scope)` | the seeds — this is the planted-defect diff the code half never had |
| feature | `review_feature(repoPath, planPath, baseRef = base, epics, lessons)` from the checkout at the variant | the seeds, plus *where the evidence sat* per seed (pack / on request / withheld, `calib · harness/report.py:194`) |

So the seven seeded variants already prepared (one per language: C#, Rust, JavaScript, TypeScript, Python, TSX,
PHP; 14 seeds, 8 cross-epic) are the first suite for ALL three gates, and the plan-gate seeded plan is an
eighth task whose code and feature gates are *not applicable* (a task declares which gates it can host).
Calibration tasks are marked and reported apart, as the Python report does.

### D4 — through the product only, one process per run, a data directory per run

Exactly what both existing instruments do, taken as the contract:

- The run starts the `coai-mcp` binary the operator names (`--coai-exe`; no default — *"a default spends
  somebody's quota on a guess"*, `coai · src_bench/README.md:82`), over stdio, with `initialize` +
  `notifications/initialized`, and calls the tools; stderr is kept whole per run (the only thing left when a
  round produces nothing).
- **Isolated by default**: a fresh `COAI_DATA_DIR` per run under the artefact root, so runs cannot see each
  other and the calibration's `settings-free` property holds (every setting travels in the environment).
  `--shared-data-dir` is the five-windows case and a deliberate choice, as `coai-bench --isolate` is in reverse
  (`coai · Options.cs:457`) — the two instruments chose opposite defaults for two different questions; this
  one measures models, not the store.
- **One caller session id per run** (`COAI_CALLER_SESSION = bench-gate-<campaign>-<reviewer>-<task>-r<n>`) — the
  split order is given once per caller, and one identity for a campaign measured the split path once and the
  already-split path ever after (`coai · Bench.cs:149` and `RESULTS_bench_campaign_0_17_1.md` §*What the campaign
  got wrong about itself* 1). `callerModel` is passed as `bench-gate` — the harness is the caller and says so.
- Plan and code: a ref per run at the variant (`bench/gate/<reviewer>/<task>-r<n>`, the `Bench.cs:101` fix that
  ended two servers creating one worktree), the plan loop to `Passed` or four rounds, accept-all resolves, the
  resolve reply kept, then `review_code` over `base..ref`. Feature: `review_feature` is its own session and
  needs no `open`.
- **Every `COAI_*` sent is stored** on the run as JSON beside its hash, secrets excluded by NAME
  (`COAI_CREDS_KEY`, anything ending `_KEY`/`_TOKEN`), and the session's own config on disk is compared with what
  was asked (`coai · SettingsApplied.cs:34`): a setting accepted and ignored looks exactly like one that worked.
- Consult off (`COAI_CONSULT_ENABLED=false`), min-epics 1, the caps and pools as the calibrated preset says.

**Isolation, as paths (plan round, finding 1).** A cell's `COAI_DATA_DIR` is
`<artifact-root>/runs/<runId>/cells/<cellId>/attempt-<n>/data` — created by the harness before the process
starts and refused if it already exists. Under `--shared-data-dir` every cell of the run uses
`<artifact-root>/runs/<runId>/data-shared`, and the MODE is stored on the run, so a resumed campaign cannot
flip it. The path is derived from the cell's stored mode in one function (`CellPaths.DataDirFor`), and the
artefact store refuses a write outside the cell's own root — an isolated cell cannot write under the shared
path and a shared run cannot write under a cell's private one. RED (S3.2): two cells drained concurrently
never resolve to one directory unless shared was asked; a run planned isolated and resumed with
`--shared-data-dir` is refused naming the stored mode.

**One process per CELL, and a cell is atomic (findings 6 and 7).** One `ProcessSession` — one `coai-mcp`
process, one stdio pipe — per cell, opened when the cell is claimed and disposed when it settles; lanes never
share a session, and the shared-data-dir mode shares the DIRECTORY, never the process. An interrupted cell
(swept from a dead worker, or a lane that died mid-review) is restarted FRESH on its next claim: attempt `n+1`
gets a new data dir (`attempt-<n+1>`), a new caller session id
(`bench-gate-<campaign>-<reviewer>-<task>-r<repeat>-a<attempt>`) and a new process; the interrupted attempt's
artefacts stay under `attempt-<n>/`, marked `Interrupted` on the run record, and are never continued — no
`again: true`, no resumed session, nothing read back from the dead attempt's data dir. `Claimable.Attempts` is
the counter and the three-attempt abandon rule applies unchanged.

### D5 — the product is pinned, and a campaign refuses a product that moved

`ProductPin.Read(path)` = SHA-256 of the binary's bytes + the text of `--version` + (when the binary sits under a
git checkout) `git rev-parse --short HEAD` and the count of dirty files, exactly what the Python run records
(`product_sha`, `product_dirty_files`). It is stored on every run, and the `serverInfo.version` the handshake
returns is stored beside it. A `bench gate run` that continues a campaign (same suite, same reviewers) with a
different `binarySha256` is **refused** naming both, passable with `--allow-product-change` — which starts a
new scope, never extends the old one. The calibration itself crossed this line once (the branch was rebased
mid-measurement; phase-1 and phase-2 runs record different shas, `calib · RESUME.md`), which is precisely the
case a report must be able to see.

**The dirty check, scoped (plan round, finding 10).** `git -C <checkout> status --porcelain
--untracked-files=no -- <product source tree>`, where the source tree is the project directory the binary was
built from (found by walking up from the binary to the nearest `*.csproj`), falling back to the whole checkout
when no project is found and saying so on the pin. Untracked files are excluded on purpose: a scratch file
beside the source is not a changed product, and counting it would have marked every calibration run dirty.

**`--allow-product-change` and in-flight cells.** The pin is stored **per cell at claim time**, never read once
per run: a claimed cell finishes under the pin it started with, pending cells claim under the new pin, and the
run record lists every pin it saw. `ComparisonScope` carries the product version, so the report partitions the
run's cells by pin rather than averaging two products — the partitioning the arms page already does across
scopes.

### D6 — the reviewer catalog: immutable rows, hashed, the `COAI_VENDORS` row derived in exactly one place

`gate_reviewers` mirrors the variant catalog (`RetrievalVariant.cs:36`): a row is added and retired, never
edited, its definition hashed, unknown fields refused. A row is:

```
id · runtime (api | cli | local | remote) · model · endpoint (a public vendor URL as a VALUE, or a REFERENCE
when loopback/private — ModelConfig's LooksLikeAValue rule, inverted for addresses) · keyName (the vault
ENTRY name — a name, never a secret) · credsKeyRef (the NAME of the env var holding COAI_CREDS_KEY) ·
dialect · reasoningEffort · maxTokens · timeoutMinutes · followUps · reviewMinutesCap · prices (in / cached /
out per 1M, an optional tier) · gates ticked (plan / code / feature) · addedAt · retiredAt
```

`CoaiVendorRow.From(reviewer)` is the one function that turns a row into the JSON string the server reads
(`coai · Vendors.cs:123`, `PanelSettings.cs:1132`) — *a recipe becomes a request in exactly one place*, the
`QlnRequest.From` precedent. A vendor field this build does not know is refused by name. `bench gate reviewers
add | list | retire`, with `--from-coai-settings` to import the operator's own panel rows the way `coai-bench`
reads them (`Vendors.cs:59`) — an id is not a vendor, the runtime and the model are.

### D7 — keys: the vault's access key travels once, into the child's environment, and is never written

`COAI_CREDS_KEY` is what coai reads (`coai · KeyVault.cs:30`); the vendor key itself is read by the product from
the CredsForDevs vault by NAME and never reaches this harness. The gate resolves `credsKeyRef` through
`ISecretSource` (`EnvironmentSecrets`, refused by name when unset — *never* an empty string that fails three
layers later), puts the value into the child process's environment last, and:

- stores the settings JSON with that variable **removed** before hashing, so the hash is stable and the row
  clean;
- never logs the environment (a `ProcessSession` log line names variable NAMES only);
- the tap (D15) forwards `Authorization` from memory and writes request headers as a sorted list of names
  (`calib · harness/tap.py:15`, `:120`).

A second `ISecretSource`, `CoaiSettingsSecrets`, reads `env.COAI_CREDS_KEY` from the machine's coai
`settings.json` (`calib · harness/run.py:85` does this) — because a server run from a shell does not get the
panel's key (`coai · research/RESULTS_api_vendors_probe_and_first_trial.md` §1). It is opt-in
(`--creds-key-from-coai-settings`), read into memory, never copied anywhere.

### D8 — private data: outside git, outside the published database

| what | where it lives | what the database holds |
|---|---|---|
| the suite (task → repository path, base, variant, plan path, epics, lessons, seeds, private names) | a local JSON file named on the command line (`--suite-file`), git-ignored wherever it sits; the checked-in `samples/gate-suite.schema.json` documents the shape with a made-up example | `suiteStamp` (hash of the file's canonical form), task id, language, seed ids + `crossEpic`, which gates the task hosts, calibration flag |
| repository clones at the variant heads | under the checkout root (`RunCommand.cs:62`, a bare mirror per url + a worktree per commit — the url may be a local path), never in a directory anyone works in | nothing |
| the run's artefacts: request, reply, server stderr, the data dir (`usage.jsonl`, sessions, `coai.db`), the tap, every prompt/answer file | the **artefact root** (`--artifact-root`, default `%LOCALAPPDATA%\bench\gate\`), `runs/<runId>/` | `ArtifactRef` per file class: relative path, SHA-256, byte length |
| finding TEXT (title, why, fix quote private code) and the file path it names | the artefact store (`findings.jsonl` per run) | per finding: ordinal, severity, category, `isGating`, line, SHA-256 of the text, a `fileHash` — enough for every count on the page, no code |
| the blinded assessment key (`blindedId → run, finding ordinal`) | the artefact store | the verdict rows keyed by `blindedId`; the join to runs happens through the key at report time, in the process that holds the artefact root |

**The guard is STRUCTURAL first (plan round, finding 8).** Finding text and any code quote never enter the
database at all: the domain record `GateFinding` has no text field — ordinal, severity, category, `isGating`,
line, `TextHash`, `FileHash` — so the EF entity cannot store text, and `FindingText` is a type of the artefact
store alone. The public DTOs (`Bench.Contracts` `Gate*Dto`) carry no free-text field from a finding or a
prompt. An architecture/type test enumerates every string property of every `gate_*` entity and every
`Gate*Dto` and asserts each is on an allow-list (ids, hashes, enum names, language, reviewer id, product
version text, suite stamp) — a new column or DTO field is red until it is named there. The one free-text
column is the failure cause: stored as a `FailureKind` enum plus a text that has passed the redaction below,
and the type test names it as the exception.

**A publication guard test** is the second line: it re-reads every row of every `gate_*` table (the
`ModelConfigTests` re-read precedent) and every `ArtifactRef` path and refuses: `://`, a drive letter or
`/home/`, `\Users\`, any string from the suite's `privateNames[]` (the operator lists repository and company
names there; the test loads the suite named by `BENCH_GATE_SUITE` when set, and always the sample).
`bench gate export --public` writes the per-model tables and per-run rows to CSV/JSON through the same guard.

The `coai-bench` corpus points at plans inside coai's own public repository, so its cases and findings are not
private — the import (D12) still routes them through the artefact store, because one rule for all rows is the
whole point of a guard.

### D9 — assessment: blinded, strict, batched, resumable, and never two rubrics in one number

The rubric is the calibration's (`calib · harness/assess.py:39`), verbatim, in `prompts/gate-assess/strict.md`,
hashed onto every verdict: `supported` only when trigger, mechanism AND consequence are all correct at the code;
`partial` when the core is right and one is wrong or exaggerated; `refuted`; `unresolved` — plus `value`
(high / medium / low / none), `severityFair`, `grounded`, `cluster`, `seedHit`, `note`.

- **Export is blind**: fresh 8-hex ids, no model, no run id, no reviewer on a row; the key stays in the artefact
  store; a row carries the repository path of the *checkout the assessor may read* (a worktree at the variant
  head from `ICheckoutProvider`), `base`, `head`, the seed spec path and the later-fix candidates.
- **The assessor is a CLI agent** through `ICliAgentRuntime` (`CliAgent.cs:45`): `codex exec -s read-only
  --skip-git-repo-check --output-schema <schema> -o <out> --json -m <model> -` with every MCP server disabled
  (`assess.py:177`), or the Claude CLI in plan mode with edits disallowed (`coai · Judge.cs:162`). `CliArgv.For`
  (`CliAgentRuntime.cs:36`) is widened with an `AgentAskOptions` (sandbox, output schema file, disallowed tools)
  rather than a second launcher.
- **Batches of ≤ 24 per task**, verdict rows appended per batch with the assessor id, the batch id, the rubric
  hash and the prompt hash; a task whose findings are all assessed is skipped.
- **An assessor's output failure is a verdict, never a gap (plan round, finding 9).** A batch whose output does
  not parse, is truncated, or names ids the batch did not carry is retried once as a whole; if the retry fails
  the same way, every finding of that batch receives an explicit `AssessmentFailure` verdict named by cause
  (`Unparseable` · `Truncated` · `UnknownIds` · `NoAnswer`), under the same rubric hash. Those rows are
  excluded from supported % and from every rate, and shown as their own count (*assessment failed: n*) — the
  page never reads them as `—` (nobody looked) nor as `refuted`. A later `bench gate assess` re-asks exactly
  them (the failure is a row, so the skip rule can see it), and the new verdict supersedes the failure.
- **Prior cluster keys** for a task are handed to the next batch so one issue keeps one key.
- **Family match is a flag, not a refusal**: an assessor of the same vendor family as the reviewer it judges is
  counted apart (`AssessorFamilyMatches`), the `SelfJudged` discipline (`JudgeRunner.cs:16`; `MEASURED_LESSONS.md`
  §4d).
- **Two rubrics never sum.** The `coai-bench` judge's *worth having yes/no* (one finding, one turn, the file
  windowed) is imported as rubric `lenient-worth-v1`; the page shows it in its own column with the rubric named
  and refuses a mean across rubrics.
- **Hand-check before publication** (measurement rule 2): the first campaign's page carries no strict-supported %
  until a person has read twenty verdicts against the code and the agreement is recorded on the run's
  `assessmentNote`.

### D10 — the report: the operator's columns, medians, refusals in words

`GateReport.PerModel(gate, scope, rubric)` — pure, in `Bench.Domain.Gate`, over the facts and verdicts — takes
the **`RubricKind` as a required dimension (plan round, finding 11)**, exactly as `--metric` has no default:
`strict-v1` and `lenient-worth-v1` are two populations, every verdict row carries its `RubricKind`, and there
is no aggregate across kinds anywhere — not in the domain function, not in the API, not on the page, whose
rubric control is a filter beside the scope control. It produces per reviewer: runs, **valid %** (a failed run is IN the denominator and named by cause), findings/run, **seeds hit
mean (min–max)**, distinct seeds (cross-epic), **supported % (strict)**, supported+partial %, **high-value
findings/run**, **overstated %**, **seconds p50 / p90**, turns mean, runs with extra calls / extra calls
(repairs — the count proves an extra call, not its cause), served / refused source requests, **tokens in / out /
cached per run, cache %**, turn-1 cached (warm runs), reasoning tokens/run, **cost/run, cost/seed, total** — and
**run-to-run variance**: per task, the spread of seeds hit and of findings across repeats, and a `Withheld`
state below three repeats. `q(xs, p)` is the linear-interpolated quantile the Python report uses
(`report.py:60`), so imported and native numbers are computed by one function.

Refusals are words on the page, the `Unproven` precedent: *not enough repeats to state variance*, *unassessed*
(`—`, never 0), *cost unknown* (never free: `CapturedCount`), *calibration task — reported apart*, *two products
in this scope — partitioned*. No reviewer is ever nominated best by score.

### D11 — the page

A **Gate** tab in `BenchmarkTabs.razor` (after Code, before Math) and three routes in `Bench.Ui`:

- `/benchmarking/gate/feature` — *coai models — feature gate*: the scope control (suite stamp × product ×
  settings hash, from the runs, never a fixed list — the arms page's move), the per-model table, a per-task ×
  model table (calibration tasks marked), the seed-evidence table, and the run list with each run's
  `valid / verdict / findings / turns / s / cost / failure`.
- `/benchmarking/gate/plan` and `/benchmarking/gate/code` — the same components over the other gates.

Until the read side exists each renders `PendingKind` naming this plan. The console is mounted by
`dew_flow_rag_qln` through the submodule pin (`research/PLAN_bench_submodule_pin.md`), so the cross-repository
step is a pin bump there — no code on that side. **Owner and order (plan round, finding 5):** the coordinator
opens the qln pin-bump pull request right after E6 merges here; the order is this repository's E6 pull request
→ the qln pin → the coai cross-reference pull request (§6). E6's Definition of Done requires the PINNED console
— qln at the new pin — to show the Gate page, not only this repository's bUnit render of it.

### D12 — import: today's measurement on the page without re-spending

`bench gate import` reads, **read-only**:

1. the calibration's `runs.jsonl` and `runs/<id>/` (71 lines on 2026-09-27; 160 MB): one line → one `gate_run`
   (gate = feature, `Source = Imported("calib-py", harnessSha)`), its facts mapped field for field, the run
   directory COPIED into the artefact root, the reply's findings → `gate_findings`, the product sha recorded as
   given (no binary to hash — `binarySha256` empty and flagged), the transport preset → a reviewer row created
   or matched by hash;
2. `assess.jsonl` + `assess-key/key.json` when they exist → `gate_verdicts` under rubric `strict-v1`;
3. `coai · research/data/bench-2026-09-05/**/runs.json`, `bench-2026-09-06/runs.json`, `artifacts/bench/*/runs.json`
   (`RunRecord` lists): one `StageResult` → one `gate_run` (plan or code), `Finding.Useful` → a verdict under
   `lenient-worth-v1` with `JudgedBy` as the assessor, the arm as the reviewer id (a vendor SET arm is one
   reviewer row named for the set);
4. the 2026-09-01/02 model-comparison raw JSON **if still present** on the machine (the documents name their
   folders); otherwise the per-model numbers in those documents are imported as `SummaryOnly` rows that carry a
   citation and no findings — shown, never averaged with runs.

Import is idempotent (the harness id is the key) and imported runs never enter a native variance figure without
the source label on the row.

### D13 — what ports from Python and what the benchmark's machinery replaces

| Python | fate |
|---|---|
| `McpStdio`, `Tap`, `child_env`, `one_run`'s record, `summarise`, `failure_cause`, `phase2_plan` order | **ported** to C# (`ProcessSession` / `McpStdioClient`, `RecordingTap`, `CoaiEnvironment`, `GateRunFacts`, `FailureCause`, `GateMatrix` with repeats outermost) |
| `runs.jsonl` append + `run.json` | **replaced** by `gate_runs` + `gate_cells` (claim / settle / sweep, resume) and the artefact store — the durability the retrieval benchmark already proved |
| `inputs.py` (manifest → request) | **replaced** by the suite file: the operator's harness writes the seven requests ONCE into the suite (`epics`, `lessons`, `planPath`, `planSynthetic`) with a one-off `harness/export_suite.py` on the operator's machine — the last Python this plan asks for, and it stays outside git |
| `models.py` | **replaced** by `gate_reviewers` (`--from-calib-models` imports the four rows with their calibrated presets and list prices) |
| `assess.py` | **ported** (export, batches, key, prior clusters) over `ICliAgentRuntime` |
| `report.py` | **replaced** by `GateReport` + the API + the page; the RESULTS markdown is written by a person from `bench gate report --json` |
| `check.py` | **ported** as `bench gate probe` — start the product with a run's environment, `tools/list`, `providers`, no model call |
| `prepare_repos.py` | **replaced** by `ICheckoutProvider` over the bundle/clone paths the suite names |
| `probe.py` (a vendor field's effect through `--ask-api`) | **not ported** — a calibration instrument for the product, which belongs in coai (`coai · src_mcp` already has `--probe-api`) |

### D14 — parallelism and order

Cells are planned task × reviewer × repeat with **repeats outermost** and `SlotRotation` inside, so the three
repeats of one task are hours apart (cache warmth, rate limits) and no reviewer always goes first. Lanes =
`--parallel` (default 4) with a per-endpoint cap (`--per-endpoint`, default 2) — a semaphore keyed by the
reviewer row's endpoint; a lane claims through `IGateStore.ClaimNextAsync` the way `LegRunner` claims, and
`LegDrain` runs each lane (one failed leg recorded and skipped; twenty consecutive failures end the campaign
with exit 3; Ctrl+C leaves it resumable, exit 5). **Every lane holds exactly one `ProcessSession` at a time,
for the cell it has claimed, and disposes it before claiming the next** — two lanes never share a coai process,
and a process never outlives its cell (D4). **The limits are enforced by a test, not by a comment (plan round,
finding 3):** a scripted fake server records the number of concurrently open sessions and, per endpoint, the
number of concurrently in-flight reviews; the test asserts the observed maxima never exceed `--parallel` and
`--per-endpoint`, and that they REACH the limits when there is enough work — a cap that is never reached is
indistinguishable from serial execution. `bench gate resume --run <id>` rebuilds the plan from the stored suite
stamp and cells, and **`bench gate status --run <id>` (finding 2)** prints, before anything is claimed, the
cells by state — pending, claimed (with owner host/pid and age), abandoned (with the last cause), settled —
plus the pins the run has seen and the data-dir mode; `resume --dry-run` is the same listing and exits 0
without claiming.

### D15 — the tap is a diagnostic, on by default for `api` reviewers, and it is the biggest thing on disk

The ledger row carries the primary numbers (tokens in/out/cached/reasoning, seconds, cost, outcome), so the tap
is not what the table is computed from. It is what makes measurement rule 1 checkable — *read back what was
actually sent, one cell per arm* — and what names a failure (`finish_reason: length`, HTTP 4xx/5xx, the vendor's
own cost ticks). Per `api` reviewer run it records `call-NN.request.json / .response.json / .json` and closes the
upstream socket at an absolute deadline (the cap plus the diagnostic wait), so a finished run leaves no
connection open (`calib · harness/tap.py:78`). Off for `cli`/`local` reviewers (no HTTP to sit in front of).
`bench gate prune --tap-retention-days 30` releases request/response bodies past the window and keeps the
facts file — the `retrieved_hits` snippet precedent.

## 3b. From the plan round (2026-09-27), accepted

The coai plan gate ran over this document on pull request #39: verdict `good_enough`, 17 findings — 11
accepted and folded in below, 6 rejected with reasons because the plan already covered them (recorded on the
round). Each accepted finding names where it landed:

| # | finding | where it landed |
|---|---|---|
| 1 | isolation as exact paths per run and for `--shared-data-dir`; a RED test that concurrent cells never share a dir unless asked, and that the mode cannot write into the other's dir | D4 *Isolation, as paths*; S2.3, S3.2 |
| 2 | a `bench gate status` (or `resume --dry-run`) listing pending, claimed, abandoned and done cells before resuming | D14; S3.8 |
| 3 | a test that `--parallel` and `--per-endpoint` are enforced — a scripted fake server counting concurrent calls, never above the limits | D14; S3.8; §8 |
| 4 | an architecture test that `CoaiVendorRow.From` is the only producer of the `COAI_VENDORS` string | S1.2 (a value type with one factory, plus the literal scan); §8 |
| 5 | the qln console pin bump has an owner (the coordinator, right after E6 merges), an order (E6 → qln pin → coai cross-reference), and E6's DoD requires the pinned console to show the Gate page | D11; E6 DoD; §6; §9 Q8 |
| 6 | resume semantics: a cell is atomic — an interrupted cell restarts fresh with a new data dir and caller session id (attempt suffix); the interrupted attempt's artefacts are kept, marked, never continued | D4 *One process per CELL, and a cell is atomic*; S3.2, S3.8 |
| 7 | one `ProcessSession` (one coai process, one stdio pipe) per CELL; lanes never share a session | D4, D14; S3.1 |
| 8 | the publication guard made STRUCTURAL: finding text and code quotes never enter the database (hashes only); the public DTOs have no free-text finding or prompt field, enforced by a type/architecture test; the string guard stays as the second line | D8; S1.7, S2.4; §8 |
| 9 | an assessor batch that does not parse, is truncated or names unknown ids is retried once, then each of its findings gets an explicit `AssessmentFailure` verdict by cause, excluded from supported % and shown as a count | D9; S1.7, S1.8, S4.4 |
| 10 | the product dirty check is `git status --porcelain --untracked-files=no` scoped to the product's source tree; with `--allow-product-change`, in-flight cells finish under their own pin and new cells start the new scope | D5; S3.6, S3.8 |
| 11 | `RubricKind` is an explicit dimension of `GateReport` and a UI filter; no aggregate across rubric kinds | D10; S1.7, S1.8, S6.3; §8 |

## 4. Growth surfaces — projected size, who retires it, what a crash leaves

| surface | projected at one full campaign (3 gates × 8 tasks × 4 reviewers × 3 repeats = 288 runs) | retires it | interrupted |
|---|---|---|---|
| `gate_runs`, `gate_cells` | 288 rows each, < 1 MB | kept forever (the measurement) | a cell `Claimed` by a dead worker is swept at the next `bench gate run|resume|sweep`, ownership-checked (`WorkerIdentity.IsProvablyGoneOn`, `WorkerIdentity.cs:55`); three hand-backs → `Abandoned` |
| `gate_findings`, `gate_verdicts` | ~5 findings/run → ~1 500 rows; verdicts ≤ findings × assessors | kept forever | an assessment batch that dies leaves its ids unassessed; the next `bench gate assess` re-asks them |
| the artefact root, `runs/<id>/` | **measured 2026-09-27: 160 MB for 71 feature runs ≈ 2.3 MB/run** → ~650 MB per campaign, of which the tap bodies are most | `bench gate prune` releases tap bodies past 30 days; request / reply / stderr / ledger / answers are kept forever — they are the evidence a published number is re-checked against | a run directory without a `run.json` is a run that never finished; the importer and the report ignore it and `bench gate sweep` names it |
| checkouts at the variant heads | 8 worktrees + bare mirrors, size of the repositories (hundreds of MB for the largest) | the checkout root's existing owner; `bench gate suite verify --prune` removes worktrees no suite names | — |
| `gate_reviewers` | tens of rows | never deleted, retired | — |

## 5. Epics and stories

Every story: RED tests first (named for the guarantee), then the code, then the family checks. Model: **Fable**
for the architecture of the new context, everything that touches a secret, and the publication guard; **Opus**
for the rest.

### E1 — the domain and the contracts (Fable) — DONE 2026-09-27

> Landed as four commits on `feat/gate-e1-domain`; every story's RED is quoted in its commit body and the
> module is described in [module_gate.md](../research/module_gate.md). Deviations from the text below:
> the finding's `category` is an enum of the product's words with `Unknown` as a counted state (a word this
> build has not met is a state, not a refusal, because the value is the product's); `ReviewerHash` is
> `ReviewerDefinition.Hash` plus `GateReviewerCatalog.SameConfiguration` rather than a type of its own;
> `GateVerdict.Under` takes the rubric catalog so a verdict cannot exist under a hash nobody holds; the
> report's per-model row gained `AssessedRuns` beside `Runs`, because "unassessed" is per run and the Python
> report's zero-for-unassessed was the thing to stop; `GateRunRecord` names the campaign and the run apart,
> since a run is one cell's product session. `SeedEvidence` landed in S1.8 as the pure classifier over the
> turn-1 prompt (S4.5 keeps the reading of that prompt off disk).
>
> **The code round on pull request #40 (coai gate + our own review), folded in before merge — further
> deviations:** `GateScope` carries the binary's sha beside the version text (D2's scope named the text only; a
> dirty rebuild at one HEAD shares the text). `CoaiVendorsSetting.From` — not `CoaiVendorRow.From` (S1.2) — is
> the one producer, because C# gives an enclosing type no access to a nested type's private constructor, so a
> builder class needs an `internal` door (the first cut had one, `Sealed`); the factory lives inside the type,
> the variable name is private and `ApplyTo(env)` is the only way into an environment; `CoaiVendorRow` keeps the
> vocabulary and a reader that refuses a mistyped field by name. `ReviewerRuntime` has one member per product
> runtime WORD (`codex`, `gemini`, `claude`, `antigravity` beside `api`, `local`, `remote`) instead of D6's
> `cli`, which the product would have run on Codex; the words are pinned against a copied fixture of the
> product's `RuntimeNames`, and an effort of `none` (the module default) is not written. Canonical forms are
> length-prefixed; `GateSuite.Freeze` snapshots; the plan path refuses rooted paths and `..`; IPv4-mapped IPv6
> is judged as the IPv4 it carries (the loopback case was already caught by `IPAddress.IsLoopback`; the private
> ranges were the hole); `GateCell.Pending` takes a caller-minted id. `GateFinding.FileHash` is an HMAC under a
> `FileHashKey` that lives only in the artefact root (resolving it is E2/E3) over a normalised path. The report
> takes the `Rubric` (id + kind + hash) rather than the `RubricKind`, and `GatePopulation` decides its inputs
> once: the latest attempt per cell (campaign, task, reviewer, repeat) — the campaign in the key, so two
> campaigns in one scope never collapse — with `Attempts` / `AttemptsFailed` columns (the Python report's
> `final_attempts`), one verdict per finding (real over `AssessmentFailure`, independent over family-matched,
> `AssessorFamilyMatched` counted), a valid zero-finding run assessed with zero hits, calibration tasks in
> `ModelTable.Calibration`, and variance as two spreads over the cells — seeds need three assessed readings,
> findings three repeats. Every rounding the Python harness does — `q`, `pct`, the means, the costs, the run's
> seconds — goes through `PythonRound` (the exact binary value, half to even), pinned on vectors printed by
> Python 3.14.6. `ReviewerDefinition.Canonical` and `ReviewerTransport.Canonical` are length-prefixed like the
> suite's forms, which changed every reviewer hash (no reviewer row is stored yet). The DTO guard walks the type
> graph with a `Type.Property` allow-list.

- [x] **S1.1** `GateKind`, `GateTask` (id, language, hosted gates, calibration flag, seeds), `SeedSpec`, `GateSuite`
  (parse → freeze → `Stamp` via `StableHash`; `privateNames[]`; refuses a task naming a gate it cannot host — a
  plan-only task on the code gate, a task with no seeds on a seeded-recall column). RED: two suites differing only
  in a repository path have the same stamp when the operator's canonical form excludes paths — **no**: paths are
  OUT of the canonical form by construction, and the test asserts a moved clone keeps its stamp.
- [x] **S1.2** `GateReviewer` row + `CoaiVendorRow.From` + `ReviewerHash`. RED: a row with a loopback endpoint as a
  value is refused by name; an unknown vendor field is refused; retiring keeps the row readable; two rows with one
  hash are reported as one configuration under two names. **And the one-producer guarantee is a type and a
  test (plan round, finding 4):** the environment builder takes a `CoaiVendorsSetting` value whose only
  constructor is private and whose only factory is `CoaiVendorRow.From`, so no other code can hand the server
  a vendors string; an architecture test scans `src/` and `hosts/` for the literal `COAI_VENDORS` and asserts it
  occurs in exactly one production file, naming any other by path and line.
- [x] **S1.3** `ProductPin` (sha256, version text, git sha, dirty count) + `ProductPin.Matches`. RED: a campaign's
  second run against a different sha is refused naming both.
- [x] **S1.4** `Claimable` extracted; `CellLifecycle` over it; `GateCell` composed. RED: every existing
  `CellLifecycleTests` unchanged and green; a `GateCell` follows the same three-attempt abandon rule.
- [x] **S1.5** `GateRunFacts` (valid, verdict, findings count, turns, HTTP calls, finish reasons, tokens in/out/cached/
  reasoning as `CapturedCount`, seconds total and per turn, cost as `CapturedCount`, served/refused, failure cause)
  + the `valid` rule (verdict ∈ {proceed, revise}, every ledger turn `ok`, findings a list — `calib · run.py:195`).
  RED: a reply that parses with zero turns is not valid; a ledger with no cost gives cost *unknown*, never 0.
- [x] **S1.6** `SlotRotation` extracted; `GateMatrix.Plan(tasks, reviewers, repeats)` repeats-outermost. RED:
  `MatrixOrderTests` unchanged; the three repeats of one task are never adjacent; first positions balanced.
- [x] **S1.7** `GateFinding` (hash-only fields), `Verdict` (carries its `RubricKind`; `AssessmentFailure` with its
  cause is a verdict case, not a null), `Rubric` (id, kind, hash). RED: a verdict under a rubric hash the catalog
  does not hold is refused; a `GateFinding` has no property of type `string` other than enum names and hashes
  (asserted by reflection).
- [x] **S1.8** `GateReport.PerModel(gate, scope, rubric)`, `PerTask`, `SeedEvidence`, `Variance` — pure, `RubricKind`
  required. RED: `Withheld` below 3 repeats; `—` for unassessed; a mean across two rubric kinds is refused by
  name; `AssessmentFailure` rows are excluded from supported % and counted in their own column; a failed run
  stays in the denominator; `q` matches the Python quantile on a fixed vector; a scope with two product
  versions partitions; cells claimed under two pins in one run land in two partitions.
- [x] **S1.9** `Bench.Contracts`: `GateModelTableDto`, `GateRunSummaryDto`, `GateRunDetailDto`, `GateScopeDto`.
- [x] **S1.10** architecture tests: gate decisions in `Bench.Domain`; `Bench.Ui` references contracts only.
- [x] DoD: 0 warnings; every RED observed with its real symptom; `architecture.md` names the new context.

### E2 — the store and the privacy guard (Fable for S2.4; Opus otherwise) — DONE 2026-09-27

> Landed on `feat/gate-e2-store`; the store, the artefact layout and protocol, the guard and the growth table are in
> [module_gate.md](../research/module_gate.md). Every story's RED was observed by REVERTING its guard in the
> finished code and watching the named test fail for the real symptom (the implementation was drafted before the
> tests; the revert is how each test proved it has teeth), then restoring it green.
>
> **From E2's plan round (2026-09-27), accepted** — folded in as written:
>
> | # | finding | where it landed |
> |---|---|---|
> | 1 | the hand-back is ONE guarded atomic UPDATE, the observed owner and state in its WHERE, the attempt count incremented once; a test of two concurrent sweepers | `PostgresGateStore.HandBackAsync` re-checks state, owner (label, host, pid), claim time, attempt count and a live run in one statement and picks requeue or abandon inside it; the count moves only at the claim (`Attempts + 1` in the claim's UPDATE), so across hand-back + re-claim it moves exactly once. RED by revert: the guard reduced to the id → 6 and 8 of 8 simultaneous sweepers each counted the same cell |
> | 2 | the sweep is scoped to cells of runs that are NOT finished | every sweep query and the hand-back's WHERE require a run that is neither `Finished` nor `Failed`; a terminal run is not claimed from either. RED by revert: both statuses' stranded cells went back to `Pending` |
> | 3 | canonical containment: `GetFullPath` on root and target, separator-aware starts-with, refuse `..` in any id segment, resolve symlinks and junctions so a link cannot escape the root or the cell's own root; RED tests for traversal and a link escape, skipping only if the OS forbids links | `ArtifactPath` refuses `..`, `.`, empty, rooted, drive and backslash segments at parse; `ArtifactContainment` resolves every existing component (dangling links too) and compares separator-aware; a writable root reached through a link is not a writable root. RED by revert: link resolution off → a write through a junction landed outside the root, and a `cells/<a>` → `cells/<b>` link let cell a write into b; prefix-only compare → `attempt-1-evil` accepted as under `attempt-1`. The link tests make a symlink, or a junction via `cmd /c mklink /J` when the symlink privilege is missing, and skip only when both fail (the rules repository's precedent) — on this machine they ran, zero skips |
> | 4 | an atomic staging/commit protocol: staging name, fsync, SHA-256 + length, rename, persist the `ArtifactRef`, only then mark the cell complete; an interrupted attempt is kept and marked, a later attempt gets a NEW directory, a cell never gets stuck; termination tested at each step | `FileSystemGateArtifactStore.WriteAsync` + `GateCellCompletion` (artefacts with `run.json` LAST → refs in one transaction → settle). `GateCellCompletionTests` kills the process at seven points (staged, flushed, hashed, renamed, after the run record, before the refs, before the settle): the cell stays claimed, is swept, re-claimed as attempt 2, begun in `attempt-2` with `attempt-1` marked interrupted, and settles. RED by revert: settle before the refs → a cell SETTLED over refs never written; no staging → a half file under the real name at every pre-rename step |
> | 5 | the public export is built ONLY from database rows and never includes artefact file contents; test that a private name planted in an artefact body cannot reach it | `GatePublication.Export` over `PostgresGatePublicationSource` (every `gate_*` row read through the EF model); the artefact root is not an input. Tested with a findings file carrying two private names and a user path; the export carries its ref (path, SHA-256, length) and none of the text. No revert exists for this one: no export step can open an artefact, which is the guarantee |
> | 6 | the FileHash HMAC key is a random key file (≥ 32 bytes) in the artefact root, owner-only (0600 / restricted ACL), never derived from a path, never in the database, never printed; a missing key with existing hashes is refused | `file-hash.key`, 32 bytes from the OS generator, `CreateNew`, `UnixCreateMode` 0600 on POSIX and a PROTECTED single-rule ACL on Windows — set AT creation, feasible, not merely documented; `GateFileHashKeys` refuses a keyless root while `gate_findings` has rows. RED by revert: a plain `FileStream` left the Windows ACL inheriting; skipping the findings check regenerated the key |
> | 7 | prune: `--tap-retention-days` defaults to 30 and `--dry-run` lists what would go; facts durably written before any body is deleted — a crash mid-prune leaves the facts plus some bodies, never neither | per call: facts file flushed, the release appended to `tap/pruned.jsonl` and flushed, THEN the bodies; a call with no facts keeps its bodies. RED by revert: log after deletes → the crash test found no record of the body it had deleted; dry run deleting → the dry-run tests found bodies gone |
>
> **Deviations from the stories as written:**
> - **`gate_runs` is the CAMPAIGN, and a session's facts live on its cell.** §4 projected "288 rows each" for
>   `gate_runs` and `gate_cells`, which reads as one run per session, but D4 stores the data-dir MODE "on the run",
>   the paths are `runs/<runId>/cells/<cellId>/…`, and the accepted finding scopes the sweep to cells of runs that
>   are not finished — a run is the campaign. Only one attempt of a cell ever settles, so the session's facts are
>   columns of `gate_cells` and `GateRunRecord.RunId` is the cell id; six tables, no seventh for sessions. An
>   import with several attempts per repeat (E5) is several cells, or E5's own migration.
> - **One free-text column, and it is also the cell's outcome detail.** A `Failed` settle stores its cause in
>   `FailureText` (so `OutcomeDetail` reads back from it) and `GateRunFacts.NotProduced(cause)` as its facts; the
>   harness-authored *reasons* of not-captured counts are not stored (a flag is), so they read back as
>   `not captured`.
> - **`GateFinding.Stored`** was added — E1's record had no read path, and a store that cannot read back what it
>   wrote cannot compute a report. It refuses any hash that is not 64 lower-case hex; `Of` stays the only way a NEW
>   finding enters.
> - **The reviewer endpoint is the one column checked by a stricter rule than `://`** — ACCEPTED by the coordinator
>   as a deviation (2026-09-27). D6 makes a public vendor
>   url a VALUE and D8 refuses `://` in every row; `gate_reviewers.EndpointUrl` therefore passes the url rule only
>   when `ReviewerEndpoint.Parse` reads it as public (a loopback one is refused — tested); every other rule applies
>   to it. The endpoint is split into `EndpointUrl` and `EndpointRef`.
> - **A refusal never prints the private text**, and the ROW ID is redacted too — a reviewer id is a slug somebody
>   chose, and it can be the private name. Violations name the private name by its index in the suite.
> - **The export is JSON rows, not per-model tables.** The per-model table needs E6's report mapping; E2 exports the
>   guarded rows of every `gate_*` table. A dirty row exits 5 (no report), not 4.
> - **`bench gate prune` prints every run's footprint** (`ArtifactFootprint.Describe`, *unknown (why)* when the
>   measurement failed); `bench gate run` does not exist yet, so "printed by every run" is E3's call site.
> - **`GateFileHashKeys`, `GateCellCompletion`, `IGatePublicationSource`** are new Application pieces the stories
>   implied but did not name; the CLI verb class is `CoaiGateCommand` (`GateCommand` is already a harvest type).
> - `samples/gate-suite.sample.json` is new: no tasks, three made-up private names the guard test always loads.
> - **The claim owner is never published** (coordinator, 2026-09-27). `gate_cells.Owner`, `OwnerHost` and
>   `OwnerPid` stay in the database for the sweep; the public export's rows have no such field (the publication
>   source leaves them out), `GatePublication.Check` refuses any row that still carries one whatever its value, and
>   the string guard gained a host rule — this machine's name as a whole word, OS-generated names
>   (`DESKTOP-…`, `LAPTOP-…`, `WIN-…`) and private-network domains (`*.local`, `*.lan`, `*.internal`, `*.corp`,
>   `*.home`); the failure redaction replaces them with `<host>`. RED first: the export of a claimed campaign carried
>   `Owner`/`OwnerHost`/`OwnerPid` and this machine's name, a planted host name passed. **The retrieval benchmark has
>   the same leak and is NOT changed here:** `cells` and `preparations` carry `Owner`/`OwnerHost`/`OwnerPid`,
>   `run_machines.FactsJson` carries `Hostname`, and `MachineDto.Hostname` travels on the report DTO
>   (`bench report --json`, `GET /api/runs/{id}/report`) of a database the docs call "published unedited".
>
> **The code round (coai, 2026-09-27, `good_enough`, 12 of 12 reviewers, 37 findings) — 13 accepted, 24 rejected
> with reasons recorded on the round.** Taken, each RED first: a schemeless machine address in
> `gate_reviewers.EndpointUrl` (`llm.corp.internal:8000`) passed the guard — the endpoint column now requires a
> public vendor url for ANY non-empty value; `/users/` in lower case passed; `IsWithin` refused every path under a
> drive or volume root (the trimmed root kept its separator and got a second one); `GateCellCompletion` wrote and
> recorded refs for a scope whose attempt did not match the claim, or whose owner did not hold the cell — it now
> checks the claim before the first byte; an existing key file whose permissions had been loosened was accepted —
> every read re-checks owner-only; the export's staging file was not flushed, an unwritable `--out` threw instead
> of exiting 3, and the suite-read refusal named only the exception type. The footprint now aggregates while it
> enumerates. Declined (reasons on the round): progress output, streaming the export, a size cap on the operator's
> own suite file, `--out` path policing, `Verb` vs `Operand` dispatch, the CLI's synchronous dispatch and its role as
> composition root, an explicit export projection beside the model-driven one, and the scenario harness — this
> repository has none yet and no `research/module_tests.md`, a gap older than E2 (open question).

- [x] **S2.1** EF entities + one migration for `gate_runs`, `gate_cells`, `gate_findings`, `gate_verdicts`,
  `gate_reviewers`, `gate_artifacts` — `GateTables`, whose up-operations touch only `gate_*` (asserted).
- [x] **S2.2** `IGateStore` (plan / claim / settle / sweep / recent / facts) + `PostgresGateStore` — the guarded-UPDATE
  claim of `PostgresRunStore.cs:58`. RED (`PostgresFixture`): two workers, one cell, one winner; a stale claim by a
  dead pid on this host is handed back; one on another host is left alone. RED by revert, observed: claim guard off
  → 5 of 16 simultaneous claimers won; ownership check off → the other host's cell went `Pending`; settle owner
  check off → a stranger's settle succeeded.
- [x] **S2.3** `IGateArtifactStore` + filesystem adapter under the artefact root; `CellPaths.DataDirFor(run, cell,
  attempt)` as the one path function; `ArtifactRef` written with SHA-256 and length; a root inside any git
  checkout is refused. RED: a write outside the root is refused; a write outside the CELL's own root is refused
  (an isolated cell cannot reach `data-shared`, a shared run cannot reach a cell's private dir); a ref re-read
  hashes to what was written; an attempt directory that already exists is refused rather than reused. Observed by
  revert: the mode tests first passed on the ref's own refusal while the file had ALREADY been written — they now
  assert nothing reached the disk, and went red for exactly that.
- [x] **S2.4** the guard, structural first: the reflection test over every `gate_*` entity and every `Gate*Dto`
  string property against the allow-list (the failure cause named as the one exception); then the publication
  guard test + `bench gate export --public`. RED: a planted `GateFindingRow.Title` column is named by type and
  column; seeded rows carrying `C:\Users\x`, `https://`, `/home/`, a drive path in an artefact path, or a private
  name from the sample suite are named by table, column and row id.
- [x] **S2.5** `bench gate prune` + `FootprintAsync` printed. RED: bodies past the window are gone, facts files stay,
  a run with no `run.json` is listed, not deleted (revert: ignoring `run.json` released an unfinished attempt).
- [x] DoD: migrations apply on an empty database (`PostgresFixture` migrates a fresh container and a fresh database
  per guard test); `research/module_gate.md` carries the growth table of §4.

### E3 — the driver (Fable for S3.1, S3.2, S3.7; Opus otherwise) — DONE 2026-09-28

> Landed on `feat/gate-e3-driver`; the driver, the protocols, the tap and the CLI are in
> [module_gate.md](../research/module_gate.md). Built on Opus throughout (the coordinator's assignment), with the two
> secret-bearing stories (S3.2, S3.7) put to a risk consultation before they were written. Every story's RED is quoted
> in its commit body: the pure pieces (S3.4–S3.6) failed to compile first and then went red by revert; every guard
> was reverted in the finished code and its test watched failing for the real symptom, then restored. **Run once
> against the real product** (`GateDriverLiveTests`, the installed coai-mcp 0.39.0, a `local` reviewer at a loopback
> stand-in vendor): `providers` answered, one plan cell completed valid (proceed, one ledger turn, one vendor call) and
> was stored with `serverInfo.version`.
>
> **Deviations from the stories as written:**
> - **Two E1/E2 defects found and fixed test-first.** `PostgresGateStore` claimed by `Position` then `Slot`, reversing
>   the matrix (RED: planned `a1 b1 b2 a2`, claimed `a1 b2 b1 a2`); `FindingCategory` named eight words the product
>   never writes (RED against a copied fixture of the product's enums: `Architecture` read as `Unknown`).
> - **The risk consultation (codex) changed three things.** A value endpoint could carry a credential — user-info, a
>   query, a fragment, plain http to a public host — and a refusal quoted the url: now refused without quoting it. A
>   vendor that echoes the header in a 401 would have put the key on disk: the tap scrubs the request's Authorization
>   value (and its bearer token) from every body it WRITES, the product still receives the vendor's bytes. The child
>   inherited the variable the reviewer names as holding the creds key: `CoaiEnvironment` drops it, and the product's
>   stderr is scrubbed of the key as it is streamed.
> - **The cadence consultation** added the per-cell `ReferencesHash` (a resume whose references now resolve elsewhere is
>   refused naming the reviewer), capacity-aware claiming, and one lane for a shared data directory; its store-order
>   finding is the first defect above.
> - **The ledger's stage words are the product's `Stage` enum** — `PlanReview`, `CodeReview`, `FeatureReview` — not
>   `plan`/`code`/`feature`; a code cell's facts are its `CodeReview` rows only. `tokensReasoning` is written only by the
>   calibration branch, so an absent field reads *not captured*.
> - **The product's words widened the domain**: `GateVerdictWord.Skipped` (a feature review of too few epics),
>   `GateReply.Refused` + `FailureKind.ToolRefused` (the `{error}` object, e.g. `review_code` before a passed plan
>   round).
> - **`GateSettlement.Notes`** (server version, references hash, settings check counts) rides on the settlement as an
>   init property; `IGateArtifactStore.AdoptAsync` and a `GateCellCompletion` overload commit files written LIVE
>   (stderr, tap) where they lie instead of copying them.
> - **`effort` / `thinking` / `reviewMinutes`** exist only on the calibration branch (announced for 0.40.0); on 0.39.0
>   they are accepted and silently ignored, and `SettingsCheck` reports every knob the session file cannot show as
>   *unchecked*, never as applied.
> - **A shared data directory is refused above one lane**, and a cell's ledger is the slice appended during its own
>   session — `usage.jsonl` carries no session, task or attempt on a row.
> - **`Bench.Infrastructure` carries the ASP.NET Core framework reference** for the tap's Kestrel and drops three
>   package references the framework now provides (NU1510).
> - **The feature protocol does not resolve** its round (the calibration harness's shape), and **the plan gate measures
>   one round**.
> - **The per-story risk consultations (codex, after the code was written, before the code round)** found the key
>   could still reach disk and the database: an RPC error echoing it reached `gate_cells.FailureText`, a reply or an
>   inherited `*_TOKEN` echoed to stderr reached the artefact root, a credential in `x-api-key` or echoed into a kept
>   response header reached the tap's files, a 307 was followed to another host, and a response was buffered whole.
>   Each was reproduced RED and fixed: every product text is scrubbed of the vault key and the inherited secret-named
>   values (raw and JSON-escaped) before it is read or written; the tap scrubs every credential header, follows no
>   redirect and cuts a body past its cap. The parent environment itself still passes through to the child (a CLI
>   reviewer may authenticate by it, and the calibration did the same).
> - **Our own review (Opus) found**, each fixed test-first: a cell that could not be prepared or whose product never
>   started was settled terminal and COUNTED as produced (a dead environment finished the run with exit 0) — it is now a
>   refused leg with its claim left for the sweep, and `run`/`resume` resolve every reviewer's references and key before
>   planning (exit 3); the pin was read before a lane's endpoint wait (a lane that waited could claim under stale bytes)
>   — it is read under the pool's lock at the claim; a failed resolve discarded the review it followed — the review is
>   kept as the measurement; a code cell's served/refused, prompt hash and tap calls included its plan loop's — sliced
>   at a `MeasuredMark`; a clone interrupted between clone and checkout was reused — it is repaired or re-made; a resume
>   against an unreachable database or an unknown run said "pass --reviewers" (4) — now 3 and "no gate run".
> - **Decisions by the coordinator (2026-09-28), after the code round:**
>   - **The child's environment is inherited; the written artefacts are scrubbed.** The product runs with the harness's
>     environment minus `COAI_*` and the creds-ref variable, as the editor launches it (a CLI reviewer may sign in
>     through a variable); every text the harness writes is scrubbed of the vault key and every inherited secret-named
>     value. The child is not filtered.
>   - **A cell refused BEFORE launch is handed back at once and is not an attempt.** Could not be prepared, or the
>     product binary is not there: `HandBackUnmeasuredAsync` returns it to `Pending` in one guarded UPDATE, gives the
>     attempt back, records the redacted cause on the cell; it never walks toward Abandoned. RED first: three resumes of
>     a pre-launch refusal left the cell claimed and the campaign drained (`Expected … TooManyFailures … but found
>     Drained`); GREEN: the cell is `Pending`, attempts 0, the cause on it, and each campaign ends on the breaker. A
>     product that started and then failed still counts as an attempt (tested).
>   - **A code cell measures the code stage only; the plan stage is measured on its own**, by plan cells — the definition,
>     written into `module_gate.md`.
>   - The product-moved check's per-run query stays for E6.
> - **The code round (coai, 2026-09-28, `good_enough`, 12 of 12 reviewers, 27 findings — 7 accepted, 20 rejected with
>   reasons on the round).** Taken, each RED first and checked by revert: a suite gate word nobody recognises was
>   silently dropped (a mistyped `featre` ran a campaign without the feature gate); a `--set` name given twice threw
>   out of `ToDictionary`; the probe left its throwaway data directory behind; a campaign printed nothing between
>   "planned" and its end (three reviewers) — now one line per cell as it ends; and the transport variables were two
>   lists (the environment and the scope-hash exclusions) — now one. Declined: a tuple null check that is really a
>   deconstructed element, a context disposed while lanes still run (they have ended), a lock around the environment
>   snapshot (there is none), unbounded waits (bounded, and a stopped campaign exits them), a tap leaked on the vendors
>   refusal (disposed there), exit 4 for twenty failures (D14 says 3), naming nits, the repository's exhaustive
>   `unreachable` arms, local clone paths in the operator's own suite (D8), and the product-moved check's per-run query
>   (bounded at 200, once per campaign; a scoped pin query belongs to E6's report store).
> - **Not built** (named, not silently missing): `reviewers add --from-coai-settings` / `--from-calib-models` (E7),
>   `suite verify --prune` (§4), synthetic (uncommitted) plans (E7's export writes them into the suite's checkout).

> **How E3 is built (decided 2026-09-27, before its plan round).** The stories below are the contract; these are
> the decisions they left open, each checked against the code at `4c24116` and the product's source.
>
> - **Layers.** The ports are Application (`IMcpSessionFactory`/`IMcpSession`, `IProductPinReader`,
>   `IRecordingTapFactory`, `ISessionConfigReader` for S3.5, `IGateReviewerCatalog`, `IGateCheckouts`); the
>   pure decisions are Domain (`CoaiEnvironment`, the protocol step rules `GateProtocolRules.Passed` /
>   `AcceptAll` / `RunRef`, `GateReplyParser`, `LedgerRows`, `SettingsCheck.Compare`, `StderrFacts`); the
>   adapters are Infrastructure (`ProcessSession` beside `ProcessRunner`, `McpStdioClient`, `ProductPinReader`,
>   `RecordingTap` on Kestrel — a `FrameworkReference` to `Microsoft.AspNetCore.App`, no package —,
>   `SessionConfigReader`, `PostgresGateReviewerCatalog`, `CoaiSettingsSecrets`). The protocols and the cell
>   runner (`GateCellRunner`, `GateCampaign`) are Application over the ports, so every protocol test runs against
>   the fake server through the real `McpStdioClient`.
> - **The fake product** is a console project, `tests/FakeCoai`, in the solution (not under
>   `tests/Bench.Tests/Fixtures/`, whose `.cs` files the test project would compile): newline JSON-RPC, a script
>   file named by `FAKE_COAI_SCRIPT` (replies per tool, `hang`, `crash`, `nonjson`, `refuse-code-without-plan` as
>   the product does), a ledger it writes into `COAI_DATA_DIR`, a session file, and an event log (open/close,
>   review start/end per endpoint with timestamps) the concurrency test folds into observed maxima.
> - **The pin hashes the PRODUCT bytes — the whole deployment set** (plan round, finding 13). A
>   framework-dependent .NET app's `coai-mcp.exe` is a small apphost whose bytes barely change between builds;
>   the code is in `coai-mcp.dll` beside it AND in the project-reference assemblies (`core`, `runners`) beside
>   that. So when a sibling `<name>.dll` exists, `ProductPin.Read` hashes a manifest of every `.dll`, `.exe` and
>   `.json` file under the binary's directory (relative path + SHA-256 per file, sorted) — a rebuild that changed
>   one dependency must move the pin, and a test changes a dependency only; a self-contained single file is
>   hashed alone. `--version` is run with a 30-second timeout. The pin is read per CLAIM (S1.3's rule), not once
>   per run.
> - **What `settingsHash` covers.** The snapshot is every `COAI_*` SENT, minus secrets by name (`COAI_CREDS_KEY`,
>   `*_KEY`, `*_TOKEN`, `*_SECRET`, `*_PASSWORD`), stored per attempt as `settings.json` in the artefact root
>   (it holds the data-dir path, so never in the database). The HASH — the `settingsHash` of `GateScope` — is
>   over the snapshot minus the keys that are another axis already: the per-cell identity (`COAI_DATA_DIR`,
>   `COAI_CALLER_SESSION`) and the reviewer-derived values (the vendors string, whose tap port also moves per
>   run, and the transport knobs the reviewer row sets — they are in `ReviewerDefinition.Hash`). Otherwise
>   every cell would be its own scope and no two reviewers could share a table. The excluded names are one
>   list with a reason each, tested.
> - **The plan gate measures ONE round; the code gate measures the code stage.** A plan cell is `open →
>   review_plan → resolve accept-all` (the reply of that round is the measurement; the bench never revises the
>   plan, so a second round re-reviews the same text). A code cell is `open → plan loop (≤ 4 rounds, accept-all,
>   to Passed = proceed | good_enough | continue_anyway) → review_code → resolve accept-all`; its facts are the
>   code reply and the ledger rows of the code stage; the plan rounds are kept as artefacts
>   (`stage-plan-<n>.reply.json`, `…resolve.json`) and their spend is not the code reviewer's. A code cell whose
>   plan loop never passed is a completed session, invalid, cause `VerdictNotPassing` naming the plan loop.
> - **The run's ref carries the run and the attempt**: `bench/gate/<run8>/<reviewer>/<task>-r<n>-a<k>`, created
>   with `git branch -f` at the variant head. With `--shared-data-dir` a retried attempt would otherwise find the
>   dead attempt's session under the same repo+branch and continue it.
> - **Checkouts: the product is handed a GATE-OWNED clone, never the shared read-only checkout** (plan round,
>   finding 14). `ICheckoutProvider` keeps its bare mirror per url and read-only worktree per commit untouched; the
>   gate makes one `git clone --shared --no-checkout` of that mirror per run and task under
>   `<checkout-root>/gate/<runId>/<task>` (objects borrowed through alternates, so it costs a working tree, not a
>   second object store), checks it out detached at the variant head, and creates the run's refs THERE. The
>   product's own worktrees and sessions are made against that clone, so nothing any other benchmark reads is
>   written. A task's `CloneLocation` that is a plain path is passed as `file://`.
> - **A whole-cell deadline** (plan round, finding 2): every cell runs under one absolute budget,
>   `--cell-timeout-minutes` (default 300, the calibration's five-hour tool call), on top of each call's own
>   timeout; at the deadline the session is killed, the attempt is settled `Failed` with cause `Interrupted`
>   naming the deadline, and the lane moves on.
> - **Lanes and endpoints.** `--parallel` lanes, each a `LegDrain` over one delegate; a lane CLAIMS, then waits on
>   the per-endpoint semaphore of the claimed cell's reviewer (keyed by the endpoint value or reference name, or
>   the runtime word for a CLI row), then opens the cell's one `ProcessSession`. A `LaneSlot` holds at most one
>   session; opening a second while one is held throws (a programming error, not an outcome).
> - **The prediction** (`--prediction`) is free text, so it goes where free text goes: `runs/<runId>/prediction.txt`
>   in the artefact root, its SHA-256 on `gate_runs.PredictionHash`. The structural guard keeps
>   `FailureText` as the one free-text column.
> - **One migration (`GateDriver`)** adds `gate_runs.PredictionHash`, `gate_runs.AllowProductChange`,
>   `gate_cells.ServerVersion` (the handshake's `serverInfo.version`, beside the pin), `gate_cells.ReferencesHash`
>   (the consultation's point (a), below) and
>   `gate_cells.SettingsChecked` / `SettingsMismatches` (S3.5's verdict as counts; the sentences go to
>   `settings-check.json`). Every new string column joins the guard's allow-list by name.
> - **The product moved.** `run` finds earlier native runs of the same gate and suite stamp that measured any of
>   the same reviewers; if their latest cell pin does not `Match` the binary now, it is refused (exit 4, both
>   shas named) unless `--allow-product-change`, which is stored on the run. A lane that reads a moved pin at a
>   later claim stops claiming (exit 4) in a run without the flag; with it, it claims under the new pin, and a
>   cell already claimed settles under its own.
> - **Findings.** The reply's `findings[]` → `GateFinding.Of(ordinal, severity, category, isGating, line, text =
>   the finding's canonical JSON, file, key)`; the text goes to `findings.jsonl`. Served/refused come from the
>   server's stderr line (`reviewer … answered in … over N turns`), the turn-1 prompt hash from the prompt file named
>   on the server's stderr LOG of the api shim's argv (`--prompt-file <p> … --out <a>`) — absent, never a crash,
>   when no such line exists (a CLI reviewer) — both ports of the other harness's functions.
> - **Growth, per attempt** (plan round, finding 12): `settings.json`, `settings-check.json`, `request.json`,
>   `reply.json` (and one per plan-loop stage), `stderr.txt`, `findings.jsonl`, `run.json`, the product's data dir
>   (`usage.jsonl`, `sessions/`, `coai.db`, logs) and, for `api` reviewers, the tap — ≈ 2.3 MB per feature run as
>   the calibration measured, most of it tap bodies. Interrupted attempts are kept whole (≤ 2 per cell by the
>   abandon rule); the gate clones are one working tree per run × task. `module_gate.md`'s growth table carries
>   each with its owner: `bench gate prune` for tap bodies; a gate clone is removed by `bench gate sweep` once its
>   run is `Finished` or `Failed`.
> - **From the plan round (2026-09-27, coai `good_enough`, 3 of 3 reviewers, 15 findings — 4 accepted, 11
>   rejected with reasons on the round):** accepted — a whole-cell deadline (2), the growth accounting (12), the
>   pin over the whole deployment set (13), the refs in a gate-owned clone (14); rejected — state before a kill,
>   orphan processes, interrupted-while-running (all already decided by E2's owner-checked sweep and fresh attempt
>   directories), the concurrency test's realism (it proves the harness's limits, not the product's capacity),
>   pre-flight (run reads the pin, the references and the suite before planning; `probe` is the dry run),
>   cleaning interrupted attempts (binding: kept as evidence), batch mode (lanes are parallel), the settings hash
>   and the vendors string (the reviewer's configuration is its own hashed axis), the prompt-file source (misread:
>   it is the stderr log of the shim's argv), migrating stored categories (no driver has written a finding yet),
>   and `proceed`-only passing (the product's `AdvanceOnResolve` is set by good_enough and continue_anyway too).
> - **From the cadence consultation over E1–E3 (codex, closed `solved` after each point was checked in the
>   code):** (a) a reviewer row hashes its reference NAMES, so an endpoint reference re-pointed from one address
>   to another keeps `ReviewerDefinition.Hash` and would have been one population — each cell now carries
>   `ReferencesHash` (SHA-256 over the resolved values the row's references took on this machine, the tap's
>   loopback address excluded), and a run whose reviewer resolves differently at a later claim or a resume is
>   refused naming the reference; (b) **an E2 defect**: `PostgresGateStore` claimed pending cells by `Position`
>   then `Slot`, reversing the matrix's nesting (one task, reviewers A/B, two repeats: planned A1 B1 B2 A2,
>   claimed A1 B2 B1 A2) — fixed test-first to `Slot` then `Position`, over a real `GateMatrix.Plan`; (c)
>   claim-then-wait blocks a lane at the head of the line (two lanes, cap one, queue A A B: B idles while a lane
>   waits for A) — a lane now claims only among reviewers whose endpoint has a free slot
>   (`ClaimNextAsync(…, among)`), waits for a release when every pending cell's endpoint is full, and a barrier
>   test requires B to start while the second A waits; (d) a SHARED data dir has one `usage.jsonl` with no
>   session, task or attempt on a row, so concurrent cells' turns cannot be told apart — `--shared-data-dir`
>   therefore runs ONE lane (a `--parallel` above 1 is refused naming why) and a cell's ledger is the slice
>   appended between its session's start and end offsets.
> - **Product facts, verified against coai's source 2026-09-27** (`origin/main` `9cb01a2b` = mcp 0.39.0, and
>   the calibration branch `feat/feature-review-e3-dialects` `9afda135`), and what each changes here:
>   the runtime words are `codex gemini claude antigravity local remote api` (`ReviewerRuntime.cs`
>   `RuntimeNames`; an unknown word runs on Codex — E1 already pins this). The vendor row parser ignores an
>   unknown field SILENTLY (no `UnmappedMemberHandling`), and `effort` / `thinking` / `reviewMinutes` exist only
>   on the calibration branch (announced for 0.40.0, unreleased) — so on 0.39.0 they are accepted and ignored,
>   which is exactly S3.5's case: the settings check reports them `unchecked` rather than applied, and the run
>   record names the product version they were sent to. `COAI_FEATURE_MIN_EPICS` defaults to **3** and a plan
>   with fewer epics is NOT refused — the verdict is `skipped` — so the environment sets it to 1 and the verdict
>   word `skipped` joins `GateVerdictWord` (a skipped review is invalid and says why). The api key: the product
>   runs `creds config <COAI_CREDS_KEY>`, reads one JSON object of name → key, takes the row's `key` (default the
>   row id) and hands it to the api shim as `COAI_API_KEY` in ITS child environment; unset `COAI_CREDS_KEY` makes
>   the row `unavailable` (excluded from rounds). `review_code` refuses with *"no plan round has reached
>   'proceed' in this session — the plan gate comes first (review_plan)"* until a plan round is RESOLVED at
>   proceed / good_enough / continue_anyway. The ledger is `<COAI_DATA_DIR>/usage.jsonl`, one row per reviewer
>   turn: `utc provider model role stage seconds tokensIn tokensOut costUsd(nullable) outcome email kind
>   tokensCached costNote` — **`tokensReasoning` only on the calibration branch** (defaulting to 0), so the
>   reader treats an ABSENT field as *not captured* and the ledger artefact (it carries an `email`) stays in the
>   artefact root. The review reply is `verdict gatingCount threshold reviewers findings[] discounted[]
>   instruction cost{tokensIn,tokensOut,usd} commands[]`; a refusal is a separate `{error}` object. A finding's
>   `category` is one of `Architecture Security Reliability Performance Ux Convention Clarity Completeness
>   Consistency Feasibility` — **E1's `FindingCategory` names eight other words**, so every product category but
>   three would read `Unknown`; S3.4 replaces the enum with the product's words, pinned by a copied fixture of
>   `core/Findings/Finding.cs` as the runtime words are. `--version` prints `connect-other-ais <version>` and
>   exits without serving; `serverInfo.version` is `Major.Minor.Build`. Session files are
>   `<data>/sessions/session-<hash16>.json` holding `state.repoPath`, `state.branch` and `state.config
>   {roles{<Role>{maxRounds,threshold,enabled}}, onExhausted}` — S3.5 matches repo+branch the way `coai-bench`'s
>   `Sessions.Owner` does (separators and a trailing slash normalised, case-insensitive path, ordinal branch).
>   `COAI_FEATURE_API_REVIEW_MINUTES` exists only on the calibration branch; `COAI_FEATURE_SOURCE_FOLLOWUPS` on
>   0.39.0. The installed binary the editor runs is a single self-contained file (~26 MB); a checkout build is
>   an apphost beside `coai-mcp.dll`, which is why the pin hashes both.
> - **Not in E3** (named so nobody reads their absence as done): `reviewers add --from-coai-settings` /
>   `--from-calib-models` (D6, S7.1), `suite verify --prune` (§4) and `bench gate prune`'s scheduling.

- [x] **S3.1** `ProcessSession` (long-lived exe + argv, stdin/stdout pipes, stderr to a file, kill the tree on
  dispose, `IsAlreadyGone` shared) + `McpStdioClient` (`initialize`, `notifications/initialized`, `tools/list`,
  `tools/call` with an absolute per-call timeout, notifications kept). RED against a fake MCP server in the test
  project (a tiny console that speaks newline JSON-RPC): handshake before any call; a hung call ends at its
  timeout with the process gone; stderr survives a crash; **one session per cell** — a lane that claims a second
  cell while holding a session is a programming error the type refuses (`ProcessSession` is owned by the cell's
  scope and disposed at settle).
- [x] **S3.2** `CoaiEnvironment.For(run, reviewer, task, attempt)`: parent `COAI_*` dropped, every knob set, the
  vendor row, the caller session id with its attempt suffix, `COAI_DATA_DIR` from `CellPaths.DataDirFor`, the
  secret injected last; `Snapshot` = the same map minus secrets, hashed. RED: the snapshot never contains the
  key's value; the hash is stable across two runs with different keys; two cells resolved concurrently never
  share a data dir unless the run is shared; a run stored isolated cannot be resumed shared, and vice versa;
  attempt 2 of a cell gets a different data dir and caller session id from attempt 1, and attempt 1's directory
  is untouched.
- [x] **S3.3** `PlanGateProtocol`, `CodeGateProtocol` (open → plan loop → resolve accept-all → code → resolve; a ref per
  run; `Passed`), `FeatureGateProtocol` (`review_feature` with the suite's inputs). RED against the fake server:
  the code gate is refused when no plan round passed (the product's rule, replayed by the fake); the resolve
  reply's refusal is kept on the stage; `again` is never sent on a first call.
- [x] **S3.4** the reply parser + the ledger reader (`usage.jsonl` rows → turns) + `GateRunFacts` + `FailureCause`.
  RED: a length-cut call is named as such; a non-JSON reply is `tool answered non-JSON`; a `call_human` verdict
  is not valid and says why.
- [x] **S3.5** `SettingsCheck` port: asked-for settings against the session config on disk, scoped to this run's
  session. RED: an accepted-and-ignored knob is reported as a mismatch.
- [x] **S3.6** `ProductPin.Read` over the binary and its checkout: `git status --porcelain --untracked-files=no`
  scoped to the product's source tree. RED: a binary outside any checkout has an empty git sha and says so; an
  untracked scratch file beside the source does not dirty the pin; a modified tracked file under the product's
  project does; a modified file elsewhere in the checkout does not, and the pin says which tree was checked.
- [x] **S3.7** `RecordingTap` (Kestrel on a loopback port per run; body written before forwarding; absolute deadline
  closing the upstream socket; `Authorization` in memory only; `read_calls`/`facts` equivalents). RED: the
  recorded request file never contains the header value; a dripping upstream is closed at the deadline with the
  call marked; every forwarded request is answered or marked before the run finishes.
- [x] **S3.8** `bench gate run` (`--gate`, `--suite-file`, `--reviewers`, `--repeats`, `--coai-exe`, `--artifact-root`,
  `--parallel`, `--per-endpoint`, `--shared-data-dir`, `--no-tap`, `--prediction "<text>"`, `--db`), `resume`
  (`--dry-run`), `status`, `sweep`, `probe`, `reviewers add|list|retire`, `suite verify`. Exit codes: 0 legs
  produced · 3 environment · 4 configuration · 5 none. The prediction text is stored on the run (measurement
  rule 4); the pin is stored on each cell at claim. RED (`CliContractTests` shape): no reviewer → 4; a suite file
  that is not there → 3; a product that moved → 4 naming both shas; with `--allow-product-change` a claimed cell
  settles under its original pin while the next pending cell claims under the new one; `status` lists pending /
  claimed (owner, age) / abandoned (cause) / settled and the pins seen, and claims nothing; a swept cell's next
  attempt runs in a fresh process, a fresh data dir and a fresh caller session, and the first attempt's artefacts
  are still on disk and marked `Interrupted`; **the concurrency test** — the fake server counts open sessions and
  in-flight reviews per endpoint: never above `--parallel` / `--per-endpoint`, and equal to them under enough
  work.
- [x] DoD: a live test class (skipped when `BENCH_GATE_COAI_EXE` is unset, the `QlnEngineLiveTests` shape) drives
  `providers` and one `review_plan` against the real binary with a fake vendor and stores a run.

### E4 — the assessment (Opus) — DONE 2026-09-28

> Landed on `feat/gate-e4-assessment`; the assessment, its files, the verdict store and the hand-check are in
> [module_gate.md](../research/module_gate.md). Every guard was reverted in the finished code and its test watched
> failing for the real symptom, then restored (the observations are in the commit body); the turn-ceiling fix below
> was RED before it was written.
>
> **From E4's plan round (coai, 2026-09-28, `good_enough`, 3 of 3 reviewers, 16 findings — 9 accepted, 7 rejected with
> reasons on the round), accepted and folded in:** the key is extended under an EXCLUSIVE lock and replaced atomically
> (staged, flushed, renamed) — a torn or lost key would re-mint ids it held, and two assessors run side by side; the
> Claude CLI has no output schema, so its answer is extracted from its prose by `AgentJson` (extraction, not repair);
> `--scope` resolves to the runs of that suite stamp and must be the suite file's own, `--run` must name runs of the
> suite given; the growth table (below, and in `module_gate.md`); `VendorFamily` normalises case and a `vendor/` route
> prefix; the database is the commit point per batch — log first, then one transaction, an orphan log line never read
> as a verdict (the hand-check joins by blinded id AND batch); what the assessor is SENT (prompt, working folder,
> schema and answer paths) is tested to name no model, reviewer, campaign or cell; a hand-check is recorded only from a
> DRAWN sample whose rows still show the stored verdicts. Rejected with reasons: a checkout-root pre-check, stale or
> locked worktrees, the batch-size flag (already refused at parse), a dry run, the file-hash key (already
> `GateFileHashKeys`), a key per rubric (a blinded id names a finding, and pending is per assessor × rubric), and an
> `AssessmentFailure` row for ids missing after their re-ask (the stories say *unassessed*).
>
> **From the cadence consultation over E4–E6 (codex, closed `solved`):** the verdict store is the ingestion contract E5
> writes through too — a batch naming a finding no settled attempt stored is refused whole, and a replay is a no-op (a
> unique index on cell, ordinal, rubric hash, assessor, batch); a hand-check covers (rubric, assessor) over a SET of
> campaigns — an imported history is many small campaigns — and the gate is computed from the verdicts joined to the
> row's own runs, never the rubric's whole population; the paired agreement between two assessors is E6's (the
> population keeps one verdict per finding, so agreement is computed before that choice).
>
> **Deviations from the stories as written:**
> - **The Claude assessor, as specified, cannot read the code — measured.** Claude Code 2.1.258 with `--max-turns 1`
>   and a prompt that needs one file printed `Error: Reached max turns (1)` and exited 0; the same argv with three turns
>   read the file and answered. The argv is built as S4.3 says; the pass records such a batch as `NoAnswer` naming the
>   turn ceiling (RED first: it read as `Unparseable`), and raising the ceiling is the operator's decision (§9).
> - **The claude launch adds `--permission-mode plan` and `--strict-mcp-config`** — D9's plan mode, and "MCP servers off"
>   spelled for that CLI (no `--mcp-config` beside it loads none). An option a CLI has no flag for is REFUSED by name
>   (an output schema on claude, a turn ceiling on codex, anything on gemini). Codex's `-o` answer file is read in place
>   of stdout; the calibration's `--json` event log is not asked for.
> - **The verdict log is one file per ASSESSOR** (`assess/verdicts/<assessor>.jsonl`), and one pass per assessor at a
>   time holds a lock: .NET's `FileMode.Append` is not a kernel append, so two writers into one file would tear it.
> - **The key is one per artefact root**, not per campaign, so a blinded id is never reused anywhere.
> - **`laterfix_candidates` is not on the row**: the suite carries none. The strict rubric names it as candidate evidence
>   only, and is sent verbatim.
> - **The prompt hash on a verdict is of the batch prompt as SENT**; the rubric hash is of the file.
> - **Hand-checks are a table** (`gate_hand_checks`, the seventh), not a note on the run: `bench gate hand-check sample`
>   draws twenty verdicts into a file a person answers in the artefact root, `record` checks and stores the counts and
>   the file's hash. `Figure.NotHandChecked` is a new state (the DTO's `not-hand-checked` for E6).
> - **The strict-% gate applies to `SupportedPct` and `SupportedOrPartialPct`**; the counts beside them stay visible.
> - **Seed evidence is printed by `bench gate assess`, not stored** (it is derivable from the artefacts).
>
> **The code round (coai, 2026-09-28, `good_enough`, 12 of 12 reviewers, 36 findings — 11 accepted, 25 rejected with
> reasons on the round) and our own review (Opus), each accepted finding RED first:** a findings file whose line does not
> hash to the stored finding is refused (line position is the ordinal, checked against `TextHash`); a batch the database
> refuses is reported unassessed, not read (`Expected value to be 0 ... but found 2`); prior cluster keys come only from
> THIS assessor's committed lines (an orphan line and the other assessor's keys leaked in); a hand-check binds each answer
> to a hash of everything its row showed (a note, a seed hit or a title edited after the draw was accepted) and covers
> only the campaigns that had verdicts to draw (an empty campaign named on the command line was unlocked); a batch is
> announced when it is sent; `--scope` reads every run of the stamp in SQL (it filtered the newest 10 000 in memory). From
> our own review: the assessor's working folder, schema, answer file and seed list were INSIDE `assess/` — the key and
> every run's findings one `ls ..` away under a read-only (not read-blind) sandbox — now a temp workspace archived
> afterwards; an append after a torn line glued its first line onto the fragment; a log held by one pass's writer could
> not be read by the other assessor's pass. Declined: the Claude `--max-turns 1` (the operator's specification — measured,
> documented, asked), prompt injection through a finding (rows are JSON-escaped, ids and seeds are validated),
> misreadings of the evidence loop, streaming small files, a key per rubric, naming nits.

- [x] **S4.1** `prompts/gate-assess/strict.md` (the calibration's rubric verbatim) + `lenient-worth-v1.md` (the coai
  judge's question, for the import's label only), hashed through `PromptCatalog`.
- [x] **S4.2** blinded export: fresh ids, key to the artefact store, rows carrying the assessor's checkout path from
  `ICheckoutProvider.EnsureAsync(target at the variant)`. RED: an exported row contains no model, run id or
  reviewer; an id is never reused.
- [x] **S4.3** `AgentAskOptions` on `CliArgv.For` (sandbox, output schema, disallowed tools, MCP servers off) +
  `FindingAssessor` over `ICliAgentRuntime`. RED: the argv for codex carries `-s read-only` and `--output-schema`;
  the claude argv carries `--disallowedTools Edit Write NotebookEdit --max-turns 1`.
- [x] **S4.4** batches of ≤ 24, verdict rows appended per batch, prior cluster keys carried; a batch whose output
  does not parse, is truncated or names unknown ids is retried once, then every finding of it gets an
  `AssessmentFailure` verdict named by cause. RED: a killed batch leaves earlier batches' rows in place; a re-run
  skips them; a batch answering with an extra id is retried and, failing again, yields `UnknownIds` rows for every
  finding it carried; a later pass re-asks exactly the failed rows and its verdicts supersede the failures; the
  report counts failures in their own column and never in supported %.
- [x] **S4.5** seed matching and evidence: `seedHit` must name a seed of the row's task; `SeedEvidence` from the
  product's turn-1 prompt file (pack / on request / withheld). RED: a seed id from another task is refused.
- [x] **S4.6** `bench gate assess --run|--scope … --assessor <reviewer-catalog id> [--rubric strict-v1]`.
- [x] **S4.7** `AssessorFamilyMatches` flagged on the verdict and counted apart in the report.
- [x] DoD: the hand-check (twenty verdicts read by a person) is recorded before any strict % is shown for a scope — the
  report refuses the number (`NotHandChecked`) until `bench gate hand-check record` has stored one; the person's reading
  itself is E7's campaign (S7.4).

### E5 — the import (Opus) — DONE 2026-09-28

> Landed on `feat/gate-e5-import`; the import, its verbs, its tables and the real-data measurement are in
> [module_gate.md](../research/module_gate.md). Every guard was reverted in the finished code and its test watched failing
> for the real symptom, then restored (the observations are in the commit body). **Run against the real data**: the
> calibration's 111 cells and 340 verdicts, coai-bench's 160 cells and 692 lenient verdicts, three published tables as
> 220 summary-only numbers; every phase-2 per-model number equal to `results.json` of 2026-09-27 but the two named below.
>
> **Deviations from the stories as written:**
> - **The imported pin's version text is the population, not the commit** (a domain change to E1's `ProductPin.Imported`,
>   decided before the plan round and put to it). Phase 2 recorded ten product commits; keyed by commit it was ten
>   scopes. The commit stays on each cell's pin.
> - **`ModelTable.AllTasks`** (a report widening): the Python `per_model` reads every task, and E1's rows keep the
>   calibration tasks apart. The DoD is checked against `AllTasks`; the default reading is unchanged.
> - **`GateRunFacts.TurnFactsCaptured`** and a column for it: coai-bench kept no ledger, so its turns, calls, served and
>   refused are *not captured* — the fourth `gate_cells` column family E5 adds; every earlier row reads true.
> - **Two numbers differ from `results.json`, both by earlier decisions, not by the import:** the strict percentages are
>   `not hand-checked` (E4's gate — their counts are equal, so the percentages are 67.3 / 22.3 / 43.2 / 45.2 and
>   87.8 / 42.9 / 62.2 / 82.8 once a person checks), and deepseek-v4-pro's seeds hit mean and high-value/run (0.60 / 0.45
>   against 0.57 / 0.43) because its one invalid run found nothing and E1 counts only a VALID empty run as an assessed
>   zero. The calibration's own hand-check was made by the agent session, not a person, and is not imported.
> - **The DoD's "page"** is E6's; E5 proves the numbers through `GateReport.PerModel` over the imported rows, the object
>   the page will render. The fixture pins FOUR numbers PER MODEL (valid %, p50 seconds, cost per run, tokens in per run)
>   plus runs / attempts / failed attempts, over the whole redacted phase-2 population (plan round, finding 8) — not four
>   numbers in all.
> - **"71 runs"** was the count on 2026-09-27 morning; the workspace now holds 113 lines = 111 cells (19 phase-1
>   iterations, 84 phase-2 cells, 8 second attempts).
> - **S5.4 ran although the raw JSON is NOT gone** — it is still in the operator's WSL home (`coai-models`,
>   `coai-models-code`, `coai-select`). The summary-only rows were imported anyway (the numbers are the published ones and
>   carry their citation); importing the raw JSON as runs is D12.4's first branch and no story builds it (open question).
> - **The harness's sha is not on the campaign**: the harness evolved during the measurement, and a hash of today's
>   files names none of the versions that ran; the source label is `calib-py`.
> - **`--calib-models`** (the models file) is an input the stories did not name: `runs.jsonl` records no endpoint, key
>   name or price, and a reviewer row needs all three. The operator's one-off `export_suite.py` (outside git, S7.1's) wrote
>   the suite file and the models file for the real run.
> - **The coai-bench arms get no catalog row** (D12 said "one reviewer row named for the set"): a vendor set is not one
>   runtime and one model, and the record names neither model — a row would invent a definition. The cells name
>   `coai-bench-<arm>`. The cases become a suite the import writes into the artefact root.
> - **`good_enough` coai-bench rounds are not valid** — the gate's one valid rule (proceed or revise), the rule a native
>   plan or code cell is judged by too.
>
> **The code round (coai, 2026-09-28, `good_enough`, 12 of 12 reviewers, 35 findings — 9 accepted, 26 rejected with reasons
> on the round) and our own review (Opus), each accepted finding RED first and checked by revert:**
> - **coai-bench ids carried the suite stamp** — one suite over every file of an invocation, so a later import with another
>   file or case re-keyed every record (RED: a file that also carried another case imported the known records again,
>   `Expected value to be 2 … but found 0` unchanged). Campaigns are now keyed by (location, gate), cells by the record,
>   and each location's cases are its own suite; the stages are grouped once instead of scanned per stage.
> - **A summary table imported again under another gate** was silently kept under the first (RED: `expected a failure, got
>   success`) — refused now, and the unique-index race is an Outcome, not an exception; the CLI's summary verb matched on it
>   with a cast (RED: exit 0 where 4 was due).
> - **A source path with a trailing separator** found no file at all (RED: `Expected boolean to be True … but found
>   False`); **an unreadable runs.jsonl, assess.jsonl, reply.json or run file** read as empty or as a skipped name (RED:
>   `expected a failure, got success`; `its reply carries 0 finding(s) and its run record says 4`) — each is a refusal
>   naming the file; `UnauthorizedAccessException` is caught with `IOException`.
> - **An empty short sha resolved to the checkout's HEAD** (RED: `git rev-parse exited 1` where "no sha" was due — git
>   reads `^{commit}` alone as HEAD) — refused by name.
> - From our own review: **a wrong `--assessor` was refused only after 111 cells were written** (RED: `expected a failure,
>   got success` with the cells in place) — the assessor-name and key-clash checks run before the first byte now; **a
>   product sha that is not one was pinned to nothing** (RED: `expected a failure, got success`) — refused by the
>   pre-flight; **two coai-bench records on one cell** and **a re-judged finding** were accepted silently (RED:
>   `expected a failure, got success` each) — both refused; **the per-task turns and the repair columns read zero** for a
>   harness with no ledger (RED: `Expected … FigureState.Unknown … but found FigureState.Known`) — `Figure`s now, unknown when
>   nothing recorded them; `--gate 7` was accepted as a gate (RED) — refused; a database that fails mid-import is exit 3.
> - Declined (reasons on the round): record shapes, lock ordering, the designed resume and log orders, path traversal
>   (`ArtifactPath` refuses it at parse), parallelism and streaming (measured: 25 s for 271 MB), progress (a line per cell),
>   injected writers and the CLI as composition root, value types for the id derivation's inputs, `CommitSha` for 7-hex
>   product shas, git-call caching (two cases). Our own review's point that a RE-EXPORTED calibration suite re-keys the
>   calibration cells is declined too: a different suite file is a different measurement statement, and its cells are
>   imported again under that suite's campaign rather than silently re-labelled.

> **How E5 is built (decided 2026-09-28, before its plan round).** Checked against `cd4934e` and the real source data:
> the calibration's `runs.jsonl` now holds **113 lines** (21 phase-1 lines = 19 ids, one re-run with `--force`; 92
> phase-2 attempts = 84 cells, 8 grok cells re-run as `-a2` after vendor 500s), 111 run directories (269 MB),
> `assess.jsonl` 340 verdicts by one assessor (`codex`), `assess-key/key.json` 340 entries; `results.json` was
> regenerated 2026-09-27T19:19Z from exactly these files. The seven coai-bench `runs.json` files hold `plan-1` and
> `code` stages only (`artifacts/bench/matrix-v0.18.0/runs.json` is a byte copy of `bench-2026-09-06/runs.json`). The
> 2026-09-01/02 raw JSON is **still on the machine** (the WSL home: `coai-models`, `coai-models-code`, `coai-select`).
>
> - **One verb, four sources**: `bench gate import calib | coai-bench | summary`, each read-only over its source (a test
>   hashes the source tree before and after). Ports in Application (`IGateImportStore`), the readers and every mapping
>   decision pure in Domain (`CalibRecord`, `CoaiBenchRecord`, `SummaryTable`), adapters in Infrastructure.
> - **Idempotent by construction**: every imported campaign and cell id is DERIVED — a name-based UUID over
>   (harness, source key) — so a second import finds every cell already there. Each imported cell also commits its
>   source record as an artefact (`import-source.json`, SHA-256 in `gate_artifacts`); a re-import of an unchanged record
>   is a no-op, of a CHANGED one (a later `--force` line) a refusal naming the id — never a silent overwrite. A cell whose
>   attempt directory exists with no row (an import killed between the files and the transaction) is refused naming it.
> - **An import writes settled cells directly**, never through a claim: `IGateImportStore.ImportCellAsync` inserts the
>   campaign (status `Finished` — a terminal run is never swept or claimed) if missing, then the cell row (`Settled`,
>   `Attempts` = the source's attempt number, the pin, the facts), its findings and its artefact refs in ONE transaction,
>   after the files are committed under `runs/<campaign>/cells/<cell>/attempt-<n>/` by `IGateArtifactStore.WriteAsync`.
>   **A source cell with two attempts is two cells** (E2's deviation said so): the population's latest-attempt rule
>   then reads `p2-grok-4.7-cs2-r3-a2` as the run and counts `…-r3` in the attempts columns — the Python report's
>   `final_attempts`, reproduced.
> - **calib**: `bench gate import calib --calib <dir> --suite-file <suite.json> --calib-models <models.json>
>   --artifact-root … --db … [--assessor <catalog id>]`. The suite is the operator's (the same file E7 runs, so imported
>   and native runs share a stamp); `--calib-models` is `models.py`'s `MODELS` as JSON (endpoint, vault key NAME, prices —
>   what `runs.jsonl` does not record). One campaign per PHASE. A reviewer row per (model, transport preset) — created
>   as `<model>-<hash8>` or MATCHED by definition hash to a row that already exists under any name. Facts field for field
>   (`CalibRecord.Facts`): Python's `0` over no ledger turn is *not captured*, `None` is *not captured*, the failure
>   sentence goes through `FailureRedaction` with its kind read off its first reason; served/refused by the Python
>   REPORT's rule (`served_count or note.count("served ")`), because the report is what the numbers are compared with.
>   Findings from `reply.json` through `GateReplyParser` (the one parser) → `GateFinding.Of` under the root's file-hash
>   key → `findings.jsonl`, so a later native `bench gate assess` could read an imported run like any other. The run
>   directory is copied file by file (committed, hashed, a ref each).
> - **The imported pin (a domain change).** `ProductPin.Imported(gitSha, dirty)` put the sha INTO the version text,
>   so the scope partitioned by commit: phase 2 recorded ten product commits and would have been ten scopes of a few
>   runs each, none comparable with `results.json`. The sha stays on the pin (`GitSha`, per cell — a partition by
>   commit is one query); the VERSION TEXT becomes the population the other harness declared: `imported from calib-py
>   phase 2 — binary not hashed`. That is exactly the comparison unit the Python report used, and it keeps phase 1
>   (a different population: the pre-rebase commits, other presets) out of phase 2's scope. The settings hash of an
>   imported cell is the hash of `imported:<harness>:settings-not-recorded` (the harness kept no `COAI_*` snapshot).
> - **The all-tasks table (a report widening).** Python's `per_model` is over EVERY task; `GateReport.PerModel` puts the
>   calibration tasks (js3, ts2) apart. `ModelTable.AllTasks` is added — every task, calibration included, named as the
>   other harness's population and never the default reading — so the DoD compares like with like. `Rows` and
>   `Calibration` are unchanged.
> - **calib verdicts (S5.2)**: through `IGateVerdictStore.RecordAsync`, the ingestion contract, one call per Python
>   batch; the rubric is `strict-v1` (the file IS the calibration's instructions, byte for byte but the final newline,
>   which the rubric hash normalises); Python ids enter OUR key (`ExtendKeyAsync`; an id the key already holds for another
>   finding is refused) and the notes and cluster text go to the assessor's verdict log, so a person can later hand-check
>   imported verdicts with `bench gate hand-check`; the cluster is HMAC'd; the prompt hash is empty (the other harness
>   archived no batch prompt — said, not guessed). `--assessor` names the catalog row the verdicts are attributed to
>   (the E4 rule: an assessor is a catalog row), whose family is compared with each reviewer's. The calibration's own
>   hand-check was made by the agent session, not a person, and is NOT imported: strict % stays `not hand-checked`.
> - **coai-bench (S5.3)**: `bench gate import coai-bench --runs <file,…> --repo <coai checkout> --artifact-root … --db …`.
>   `plan-1` → a plan cell, `code` → a code cell; one campaign per (file content, gate); the cell key is the record's own
>   (`arm|case|repeat|startedUtc`) so a copied file adds nothing. The cases become a suite the importer writes into the
>   artefact root (short shas resolved by `git rev-parse` in `--repo`; no seeds; plan and code hosted). An arm is a vendor
>   SET and the record names no model, so its reviewer id is `coai-bench-<arm>` and NO catalog row is invented. `Useful`
>   yes/no → `Lenient(WorthHaving)` under `lenient-worth-v1`, assessor = the record's `judgedBy` (or
>   `coai-bench-unrecorded` where the judge wrote no name), family by model prefix against the arm's CLI words;
>   `unjudged` → no verdict row. What coai-bench never recorded (turns, HTTP calls, served/refused, the ledger) is *not
>   captured* — `GateRunFacts.TurnFactsCaptured` (default true) and a column for it — never a zero.
> - **summary (S5.4)**: `bench gate import summary --document <RESULTS_*.md> --section "<heading>" --gate plan|code
>   --db …` reads one markdown table: the first column is the row label (a slug), each other column a metric (a slug of
>   its header, `.1`/`.2` for `a / b` cells), each cell its NUMBER (`k`/`M`, `$`, `%` understood) or *not captured*
>   (`—`, `electricity`). Stored in `gate_summaries` (the eighth table, its own migration `GateImport`) with the
>   document's name, the section slug and the document's SHA-256 as the citation — no text; a table the report never
>   reads, so it can never be averaged with runs. Unique per (document sha, section, row, metric): a re-import is a no-op.
> - **Order**: S5.1 (the domain mappings, then the store, then the verb) → S5.2 → S5.3 → S5.4 → the DoD fixture. The DoD
>   fixture is grok-4.7's 29 phase-2 lines and 98 verdicts, REDACTED (no note, no cluster text, no path, no commit sha, no
>   repository name; placeholder findings carrying the recorded severities) over a made-up suite with the real task and
>   seed ids; it pins four of `results.json`'s grok numbers through `GateReport.PerModel(...).AllTasks`.
> - **Not in E5**: importing the 09-01/02 RAW JSON as runs (D12.4's first branch; no story builds it — the raw is still
>   on the machine, an open question); the page (E6).
>
> **From E5's plan round (coai, 2026-09-28, `good_enough`, 3 of 3 reviewers, 15 findings — 10 accepted, 5 rejected with
> reasons on the round), accepted and folded in:**
>
> | # | finding | where it landed |
> |---|---|---|
> | 0 | verify the source before any write | a PRE-FLIGHT reads and parses every source record (every line, its run directory's `reply.json`, the key, every verdict) and refuses — naming the file and line — before the first byte is written; a truncated line is a refusal, never a skipped record |
> | 2, 6, 11 | files committed before the row: a crash leaves an attempt directory nobody can import again | the retry RESUMES instead of refusing: every file of an imported attempt is written, or — when it is already there from a killed import — ADOPTED and its SHA-256 compared with the source bytes; equal is reused, different is refused naming the path; only then the one transaction. An attempt directory with a row is the finished case, compared through `import-source.json` |
> | 7 | the source key of a two-attempt cell | the calib key is the record's own id, which carries the attempt (`…-r3`, `…-r3-a2`); a MUTATION is the same id with other bytes (compared through `import-source.json`), a new attempt is a new id |
> | 8 | four grok numbers do not prove the population | the DoD fixture is the WHOLE phase-2 population (92 lines, 340 verdicts, four models), redacted, and the test pins four numbers per model plus the runs / attempts / latest-attempt columns |
> | 9 | a coai-bench file that grows gets a second campaign | the campaign is keyed by (gate, the source's LOCATION label — the folder the file sits in), not its content; a grown file adds its new records to the same campaign; a campaign row is created only when it gets at least one new cell, so a byte copy elsewhere adds nothing |
> | 12 | two records on one population key | the Python report's `latest_by_id` (the last line of an id wins) is applied before mapping, and the importer refuses two records that map to one (campaign, task, reviewer, repeat, attempt) — phase-1 iterations take the iteration as their repeat |
> | 13 | `--assessor` optional while verdicts need it | required whenever the source has an `assess.jsonl`, and it must be a catalog row; refused otherwise |
> | 14 | header slugs can collide | a cell splits into `.1`/`.2` only on ` / `; two columns that slug alike refuse the table naming both |

- [x] **S5.1** `runs.jsonl` + run directories → runs, findings, artefacts, reviewer rows by preset hash. RED: a fixture
  line (a real one, redacted to the sample suite's names) round-trips every fact; a second import changes nothing.
- [x] **S5.2** `assess.jsonl` + `key.json` → verdicts under `strict-v1`.
- [x] **S5.3** `coai-bench` `runs.json` → plan/code runs, `Useful` → `lenient-worth-v1`, unjudged stays unassessed.
- [x] **S5.4** `SummaryOnly` rows for the 2026-09-01/02 documents when the raw JSON is gone.
- [x] DoD: the page shows the 71 imported feature runs with the exact per-model numbers of the Python `results.json`
  of the same day (a fixture pins four of them).

### E6 — report, API, page, docs (Opus) — DONE 2026-09-28

> Landed on `feat/gate-e6-report`; the report, the read port, the ninth table, the routes and the page are in
> [module_gate.md](../research/module_gate.md). Every story's RED is quoted in the commit body — the domain keys, the
> adapters and the pages watched failing against stubs, the query, the CLI hooks, the API's port resolution and the
> console's escaping by revert.
>
> **Deviations from the stories as written:**
> - **A ninth table, `gate_suite_tasks`**, and a verb to backfill it (`bench gate suite record`): the report needs the
>   suite's tasks and the read hosts have no suite file (the plan round's design, below). A scope whose tasks are not
>   recorded answers 409 / exit 3 naming the verb — which is the state of the LOCAL database today: E5 imported the
>   seeded suite before the table existed, so its feature table renders only after the operator runs
>   `bench gate suite record --suite-file <that suite's file>` (the file stays outside git; the run list renders now).
> - **`--rubric` is required on `bench gate report`** as `rubric` is on `/models` (D10, §8's "a required dimension of every
>   report call") — the story's command line did not show it.
> - **The qln console needs one line of code after all**: its daemon registers the bench read ports by hand, so the pin
>   bump adds the `IGateReads` registration beside `IResultStore`. The gate routes resolve the port per request and
>   answer 503 naming that line without it — never a startup failure (measured: a handler parameter failed every route of
>   such a host, the existing mount tests included).
> - **`GateModelRowDto.RepairRuns/RepairCalls`, the per-task and run-list turns became figures**, `GateRunSummaryDto`
>   lost `CreatedAt` (a gate record carries no time the list could honestly show) and gained `Superseded` and
>   `TaskRecorded`; `GateScopeDto` gained its key, sources, rubrics and `TasksRecorded`; `GateModelTableDto` its
>   all-task rows; `GateFigureDto` the `not-hand-checked` state.
> - **The page takes `?scope=` and `?rubric=`** so a link opens on one table (and so a static render can show one); a
>   control with ONE option is chosen for the reader, two wait.
> - **One figure renderer for both surfaces**, `GateFigureWords` in the contracts, rather than one in the page and one in
>   the CLI.
> - **Not built:** the seed-evidence table (a read host carries no artefact root; `bench gate assess` prints it), the
>   paired agreement between two assessors (one assessor in the data).
>
> **The code round (coai, 2026-09-28, `good_enough`, 12 of 12 reviewers, 22 findings — 6 accepted, 16 rejected with reasons
> on the round) and our own review (Opus), each accepted finding RED first (by revert or against the unfixed code):** a
> read materialised every gate's history (`Expected gates to be equal to {Feature}, but {Plan, Code, Feature}`) — now the
> asked gate only, and one run through its own gate; the CLI read the gate twice per ask (`… to contain 3 item(s) … but found
> 6`) — one snapshot; an `InvalidOperationException` was reported as "the database is unreachable" (`IsStoreFailure(new
> InvalidOperationException()) … found True`) — the import verbs' store-failure rule, shared. From our own review: a late
> table of a scope the reader left replaced the chosen scope's (the held answer released → `… to contain "reviewer-of-b"`
> failed) — reads are generation-guarded; the lenient text report printed the strict-only columns; a scope run whose task
> the recorded set lacked was read as MEASURED (`Expected …Refused … but found …Answered`) — 409 naming it; two first records
> of one suite at once refused the loser (`Expected …Outcome`1+Ok`) — it re-reads and finds the same rows; an address rubric
> id naming two wordings was chosen silently and ids were matched case-sensitively; a calibration-only scope said nothing;
> the coai-bench import recorded its suites outside its store-failure boundary. Declined: primitive words at the HTTP
> boundary, misreadings (an empty rubric IS a 400, the repair columns ARE figures, `TasksAsync` never returns null), file-size
> / timeout / traversal guards on the operator's own suite file, a catch-all exception filter, pagination.
>
> **How E6 is built (decided 2026-09-28, before its plan round).** Checked against `765b4ee` and the local bench
> database (18 campaigns, 243 cells, 340 `strict-v1` + 692 `lenient-worth-v1` verdicts, no hand-check).
>
> **Two facts the stories did not see, and what they force:**
> 1. **The report cannot be computed from the database today.** `GateReport.PerModel` takes the TASKS
>    (`TaskSummary`: language, calibration, seeds with cross-epic) and they live only in the suite file — outside git
>    and outside the database (D8), and neither the read API nor the qln console has it. So the task summaries
>    become the NINTH table, `gate_suite_tasks` (suite stamp, task id, language, calibration, hosted gates, seed ids,
>    seed cross-epic flags — ids, words and booleans only, walked by the structural guard and the string guard like
>    every `gate_*` table), migration `GateReportReads`. Written, idempotently (same stamp + task = same row, a
>    different row under a stamp it already holds is refused), by `bench gate run`/`resume` (the suite it loads),
>    `bench gate import calib` (its `--suite-file`), `bench gate import coai-bench` (each location's suite it builds)
>    and a new backfill verb `bench gate suite record --suite-file <file>[,…] --db …` for what E5 imported before the
>    table existed. A scope whose suite's tasks are NOT recorded is listed (`tasksRecorded: false`) and its table is
>    REFUSED with the sentence naming the verb — never computed with the calibration tasks folded into the measured
>    rows. The run list still renders (language and calibration read *not recorded*).
> 2. **The rubric catalog for a READ comes from the rows.** The verdict store resolves a verdict's rubric through a
>    `RubricCatalog` built from `prompts/gate-assess/` — which a read host does not carry. A read builds the catalog
>    from the distinct (id, kind, hash) the stored verdicts and hand-checks carry: a verdict row is the record of the
>    wording it was judged under; the prompt files are what an INGEST is checked against.
>
> **The shape:**
> - **Domain**: `GateScope.Id` — twelve hex of `StableHash` over the scope's five fields, length-prefixed
>   (`CanonicalFields`): the one key the CLI's `--scope`, the API's `?scope=` and the page's control pass. A pure
>   `GateRunList.Of(records)` marks each attempt `Superseded` when a later attempt of its cell exists (the population's
>   latest-attempt rule, shown rather than hidden).
> - **Application** (`Bench.Application/Gate/GateReportQuery.cs`, `GateReportContract.cs`): a READ port `IGateReads`
>   (every settled record, the tasks of a stamp, the stored rubrics, verdicts and hand-checks of a run set, one run's
>   detail with its prompt hash) and a write port `IGateSuiteTasks`. `GateReportQuery` answers scopes, one scope's
>   per-model table under ONE rubric, one scope's run list, one run; its refusals are values (unknown gate word and
>   missing scope/rubric → 400; unknown scope, a rubric this scope's verdicts do not carry, tasks not recorded,
>   unknown run → 404). `GateReportContract` maps `ModelTable` → `GateModelTableDto` — the object `bench gate report
>   --json` prints and the API answers.
> - **Contracts** (widened, guard allow-list updated): `GateScopeDto` gains `Id`, `Sources` (native or the import's
>   harness — an imported scope is labelled on screen), `Rubrics` (`GateRubricDto(id, kind, hash, stamp, verdicts)` —
>   what the rubric control offers) and `TasksRecorded`; `GateFigureDto` gains `not-hand-checked` (E4's state);
>   `GateModelRowDto.RepairRuns/RepairCalls` and the per-task and run-list turns become figures (E5 made them
>   *not captured* for coai-bench); `GateModelTableDto` gains `AllTaskRows` (E5's `ModelTable.AllTasks`, the population
>   the other harness published — shown apart, only when calibration tasks exist, never the default reading);
>   `GateRunSummaryDto` gains `Superseded`. No host, owner or pid on any DTO (the claim columns never leave the store).
> - **API** (`src/Bench.Api/GateApi.cs`, mapped from `MapBenchApi` so every host that maps the bench group gets it):
>   `GET /api/bench/gate/scopes[?gate=]`, `/gate/{gate}/models?scope=&rubric=`, `/gate/{gate}/runs?scope=`,
>   `/gate/runs/{id}`. `rubric` is REQUIRED on `/models`, as `metric` is on `/runs/{id}/report` (D10: no default,
>   no aggregate across kinds). The handlers resolve `IGateReads` from the request's services and answer **503 naming
>   the missing registration** when the host did not register it — a plain parameter of an unregistered interface is
>   inferred as a BODY by minimal APIs, and a GET with an inferred body fails the whole endpoint table at startup,
>   which would take every route of the qln console down with it. `http/gate/gate.http` per the contracts rule.
> - **CLI**: `bench gate report --gate plan|code|feature --scope <scope id | suite stamp> --rubric <id> --db …
>   [--json]` — a stamp that spans several scopes of the gate is refused (4) listing each scope's id, product and
>   settings hash; no `--scope` → 4 listing the gate's scopes; no `--rubric` → 4 listing the rubrics the scope carries.
>   Text output: the per-model table (calibration apart, all-tasks apart), refusals in words, the variance sentence.
> - **Page**: a **Gate** tab (after Code, before Math) → `/benchmarking/gate/feature`, `/plan`, `/code`
>   (`GateFeature`/`GatePlan`/`GateCode`, `.razor` + `.razor.cs`, a gate sub-nav), each rendering one shared
>   `GateScopeView` (primary-constructor `BenchConsoleApi`: scope control from `/scopes` only, rubric control from the
>   chosen scope's `Rubrics` only, re-reading `/models` when either changes) with `GateModelTable` and `GateRunList`
>   inside. Every figure is rendered by ONE pure function (`GateFigureText`): `—` for unassessed, *unknown*,
>   *withheld*, *n/a*, *not hand-checked* — never a zero. The verdict columns are headed with the rubric's own words
>   and id (*supported (strict-v1)* against *worth having (lenient-worth-v1)*), so two kinds never share a column. A
>   scope with no verdicts shows its run list and says nothing was assessed. The page starts nothing — no durable-status
>   action exists on it.
>
> **Not in E6, said rather than dropped:** the seed-evidence table (D11) — it is read off the turn-1 prompt FILES in
> the artefact root, which no read host carries (E4 prints it from `bench gate assess`); the paired agreement between two
> assessors (one assessor exists in the data); the qln pin bump (the coordinator's, after this merges — and the qln
> daemon registers its bench read ports by hand, so that pull request adds ONE line registering `IGateReads`; without it
> the Gate page renders the 503 sentence, not a crash).
>
> **Order**: domain + contracts (RED: scope id stability, superseded marking, the figure states) → the table, migration
> and the two adapters (RED on `PostgresFixture`: idempotent record, a conflicting row refused, reads round-trip) → the
> query + contract mapping (RED: every refusal, the rubric filter, tasks-not-recorded) → API + `.http` → CLI → the
> console service + pages (every bUnit RED S6.3 lists) → docs → the real database.
>
> **From E6's plan round (coai, 2026-09-28, `good_enough`, 3 of 3 reviewers, 16 findings — 11 accepted, 5 rejected with
> reasons on the round), accepted and folded in:** a scope whose tasks are not recorded answers **409** (the scope
> exists; a prerequisite is missing), never 404, and the CLI exits 3; a suite's task set is written in ONE transaction,
> so an interrupted record leaves the stamp unrecorded, never half-recorded — and the same set twice is a no-op, a
> different row under a held stamp a refusal, each tested; the read catalog is built from the stored rows and tested
> with no prompt files and with no verdicts at all; a scope with no verdicts hides the rubric control and says *no
> assessment recorded*; the scope id is pinned on a vector and shown to change with each of its five fields; the 503
> sentence names the port and the registration line; `gate_suite_tasks` is in the growth table (bounded by tasks per
> suite, kept forever); the imported phase-2 population is pinned THROUGH the query (calibration apart, all-tasks
> beside, repair columns known for the calibration's runs and unknown for coai-bench's, strict % `not-hand-checked`).
> Rejected with reasons: the pre-flight (already a whole-scope refusal), an empty all-tasks section (never rendered),
> a replacement verb (a stamp is its tasks' hash), resume with an edited suite (E3 refuses it), a scope id over the tasks
> (the stamp already is).

- **S6.1** `bench gate report --gate feature --scope <stamp> [--json]`; the same DTO as the API.
- **S6.2** `GET /api/bench/gate/scopes`, `/gate/{gate}/models?scope=`, `/gate/{gate}/runs?scope=`, `/gate/runs/{id}`
  — 400 for a missing scope, 404 for an unknown run. `http/gate/*.http` per the contracts rule.
- **S6.3** the Gate tab + `GateFeature.razor`, `GatePlan.razor`, `GateCode.razor` (+ `.razor.cs`, primary-constructor
  DI) + shared `GateModelTable`, `GateRunList` components; a **rubric filter** beside the scope control, no
  aggregate across kinds. bUnit RED: the scope control offers only scopes the runs echoed; the rubric control
  offers only kinds the verdicts carry and the table re-reads when it changes; `Withheld` renders as words
  above the table; `—` is never `0`; *assessment failed: n* is its own cell; an unknown cost renders
  *unknown*; a calibration task is marked; two rubric kinds never share a column.
- **S6.4** `research/module_gate.md` (purpose, Mermaid, entities, entry points, growth table), `architecture.md`
  (the new context, the tuple, the guard), `research/README.md` row, and the qln pin bump named as the
  cross-repository step with its owner and order (D11).
- [x] S6.1 · [x] S6.2 · [x] S6.3 · [x] S6.4 (the cross-repository step and its one registration line in
  `module_gate.md` § *Measured: the report and the page*).
- DoD: `PendingKind` is gone from the three routes (they never carried it — the tab arrived with the pages);
  `BenchUiRegistrationTests` cover the new service reads; **the pinned console shows the Gate page** — the coordinator's qln pin-bump pull request, opened right after
  this epic merges, renders `/benchmarking/gate/feature` from the new pin, and that observation is recorded in
  the epic's pull-request description.

### E7 — the first re-run through the C# driver (Opus; the operator's campaign)

- **S7.1** `harness/export_suite.py` (one-off, on the operator's machine, outside git) writes the seven seeded
  tasks + the seeded plan into the local suite file; `bench gate suite verify` proves every clone is at its
  variant head and every plan path exists; `bench gate reviewers add --from-calib-models`.
- **S7.2** **A/A first**: one task, one reviewer, two repeats through the C# driver against the SAME product sha
  and settings hash as a Python phase-2 run of that task — the prompt hash of turn 1 must be identical, or the
  port has a leak and nothing after it is worth reading.
- **S7.3** the 36 remaining phase-2 feature runs through `bench gate run`, then the plan and code gates over the
  same eight tasks at three repeats.
- **S7.4** `bench gate assess` with the codex assessor; the hand-check; the family-match count reported.
- **S7.5** the results document belongs to coai's `research/` (`RESULTS_feature_reviewer_models.md` is regenerated
  from `bench gate report --json` rather than from `report.py`); this repository's record is `module_gate.md`.

**Prediction, written before S7.2 runs:** with the same product sha, the same settings hash and the same suite,
the C# driver's per-task validity and seeds-hit range for a model will fall inside the Python runs' range for
that model on that task; a difference in turn-1 prompt hash or in the ledger's token sums on a valid run is a
harness defect, not a model result. If it does not hold, this sentence is the record of a wrong guess.

#### E7 amended 2026-09-28 — measured before S7.2 ran, and the operator's answers

**S7.2's criterion cannot pass as written, and the calibration's own records show it.** Two Python runs of ONE task by
ONE reviewer at ONE product sha — `p2-grok-4.7-cs2-r1` and `-r2`, both at `5d73ead7` — carry different turn-1 prompt
hashes (`b9f45507…` and `d6650ee2…` in `gate_cells.PromptHash`). The 184 903-byte prompts differ in 18 lines, and in
exactly two things: the product's 8-hex SESSION id, which fences every section (`--- the plan — … (fb0f5dd6) ---`, 8
fences; `coai · src_mcp/src/Server/PanelService.cs:420` makes it `Guid.NewGuid().ToString("N")[..8]`), and the run's
data directory, named in the gate-history section (`Gate history unavailable: there is no rounds database at
<data-dir>\coai.db.`). With those two normalised, **all five phase-2 cs2 runs at `5d73ead7`** — grok ×2, deepseek, qwen,
and glm (a run with one dirty product file) — produce ONE shape, `837fed1ae93f14bf…`. So on cs2 at `5d73ead7` the turn-1
prompt is the same for every API reviewer measured, and the raw hash is per-run by construction. The A/A compares the NORMALISED prompt.

- **S7.2a — `PromptShape` and `bench gate aa` (code, RED first; Opus — measurement tooling, no money path).**
  - `src/Bench.Domain/Gate/PromptShape.cs` (pure). `PromptShape.Of(string prompt)` learns the run-specific tokens
    rather than pattern-replacing them anywhere: the SESSION id is taken only from whole FENCE lines
    (`^--- .+ \(([0-9a-f]{8})\) ---$`, an optional trailing `\r` kept as part of the line), every fence must carry the
    SAME id (two ids → the shape is `Ambiguous`, named by line), and it is replaced with `<session>` on fence lines
    only; the DATA DIRECTORY is taken only from the one whole gate-history line (`^Gate history unavailable: there is
    no rounds database at (.+)[\\/]coai\.db\.$`, at most one such line, a second → `Ambiguous`) and only its captured
    group becomes `<data-dir>`. Nothing else is touched: **no line-ending folding** (a gate clone checked out with other
    line endings than the Python harness's is a real input difference the reviewer's tokens see, so it must fail), and
    an 8-hex id in parentheses inside the task's own source, or a quoted copy of the history sentence, stays and is
    reported. The shape carries the normalised text's SHA-256. `PromptShape.Compare(a, b)` → `Same`, or `Differs`
    with the count of differing line POSITIONS and the first 20 of them (the first differing position always kept,
    so an insertion that shifts every later line still names where it began) — never the lines themselves, because a
    prompt carries a private repository's source.
  - `src/Bench.Application/Gate/GateTurnOnePrompt.cs` — the ONE rule for "which file is the turn-1 prompt", extracted
    from `GateSeedEvidence.cs:24-45` (reuse-first, move 2): among a cell attempt's already-fetched artefacts, class
    `Prompt`, first by ordinal path. Three outcomes, not two: `Present(text, sha)` · `Absent` · `Unreadable(reason)`
    (the store's read-and-verify refused it). `GateSeedEvidence` keeps its loops and its one `ArtifactsAsync` call per
    campaign and maps `Unreadable` to "try the next cell", exactly as today — pinned by a RED test of that fall-through
    written BEFORE the extraction. The native classifier (`GateAttemptRecord.cs:39`, name contains `prompt`) and the
    import's (`ImportedFiles.cs:51`, `.prompt` suffix) differ in wording, so the verb also checks, per cell, that the
    file it read hashes to the stored `gate_cells.PromptHash` (the runner's `GateCellRunner.cs:318`, the import's
    `ImportedFiles.cs:58`); a mismatch is exit 3 — proof at run time that the three sites picked the same file.
  - `bench gate aa --run <campaign> --against <cell id> --suite-file <file>[,<file>] --db … --artifact-root …`
    (`CoaiGateCommand.cs` dispatch + `SubVerbs`, which is also stale today — it lacks import, report and suite record).
    Refused first, in the order a person fixes things: flags · 4; an unknown run or cell · 4; the run's and the
    reference's gates differ · 4 (turn-1 prompts of plan, code and feature differ by construction); a suite file not
    covering the run's and the reference's stamps · 4; the reference with no prompt (a CLI row, or absent) · 4; the
    database or the reference's prompt unreadable / hash-mismatched · 3; the run not `Finished` or with unsettled
    cells · 5, naming them. Then one line per cell of the run, its SETTLED attempt: cell id, reviewer, task, repeat,
    raw hash (12), shape hash (12), and one of `same shape` · `differs at lines …(n)` · `ambiguous fences at …` ·
    `API reviewer left no prompt` (a FAILURE — the capture broke) · `CLI reviewer — no prompt by design` (decided by
    the row's runtime, `ReviewerEndpoint.cs:43`, never by a missing file) · `other task — not compared` (the task
    DEFINITION — id, base, variant head, plan path — read from the suite files, differs from the reference's) · `other
    product pin — not compared` (git sha or dirty count differs). Exit: 3 any cell unreadable · 1 any failure · 5 no
    cell was compared (never a vacuous pass) · 0 otherwise. Output is cell ids, reviewer/task ids and hash prefixes
    only, passed through `FailureRedaction` with the suites' private names; no path is ever printed. Nothing is
    stored — like seed evidence it is derivable from the artefacts whenever asked.
  - Tests (RED first): the measured vectors as synthetic text (8 fences, one history line, no private content) → one
    shape across two session ids and two data dirs; a source line `(deadbeef) ---` that differs stays a difference;
    two ids across fences → ambiguous; CRLF against LF → differs; an insertion reports its first position; the cap at
    20. The extraction's fall-through test. The verb over a `PostgresFixture` campaign with artefacts on disk,
    driven through the CLI entry (`CoaiGateCommand`) with real arguments — a scenario of the flow, not of a double:
    same shape → 0; one line changed → 1; an API cell with no prompt → 1; a CLI-only run → 5; a tampered prompt → 3;
    another task → listed, not compared; an unfinished run → 5; each refusal → 4.
- **S7.1 as it stands.** The suite file exists (`<artifact-root>/suite.json`, 7 tasks, stamp `fb80578c897f`). The
  seeded 8-defect plan is **deliberately excluded**, not missing: §9 Q5 (may it be committed as `samples/`) is still
  unanswered and nothing in E7 reads it, so every gate runs over the seven tasks; `bench gate suite verify` already
  checks each of them at its variant head with its plan committed. `reviewers add --from-calib-models` is not built
  and not needed: E5's import created the phase-2 rows (`grok-4-7-9ca08acf` xai/medium, `glm-5-3-106e63ec`
  dashscope/high, 8192 tokens, 20 min, 3 follow-ups — so the A/A's reviewer settings are the Python runs' by
  construction; the SETTINGS HASH of S7.2 as first written cannot be compared, because an imported cell carries one
  constant per harness, `ImportedSettings.Hash`, and the turn-1 prompt shape stands in for "the same input"). Rows
  added with `bench gate reviewers add` before any run (rows are immutable; the imported ones host only `feature`):
  `claude-fable-5-1` and `claude-opus-5-5` (runtime `claude`, gates plan,code,feature, 20 min, 3 follow-ups, cost
  unknown); `codex-gpt-6-astra` exists for `feature`; for S7.3 a `grok-4.7` and a `glm-5.3` row hosting plan,code with
  the phase-2 transport. `bench gate probe` for each new row before it runs.
- **S7.2 — the A/A, one campaign.** (1) The A/A suite: `<artifact-root>/aa-cs2.suite.json`, `suite.json` with only the
  `cs2` task (the same definition, the same private names), written once and checked with `bench gate suite verify`.
  (2) The product built from a clean worktree at `coai@5d73ead7`. (3) **Self-check before anything is spent:** `bench
  gate aa` over the imported phase-2 campaign against `p2-grok-4.7-cs2-r1`'s cell must print `same shape` for every
  cs2 cell at `5d73ead7` — the five that produced `837fed1ae93f14bf…` under a scratch normaliser (CR stripped; its
  other rules are the ones above). If the C# shapes of those five disagree among themselves, the normaliser is wrong
  and nothing is run. (4) `bench gate run --gate feature --suite-file aa-cs2.suite.json --repeats 2` over grok, glm,
  Fable 5.1, Opus 5.5 and gpt-6-astra, isolated data directories, the prediction below passed as `--prediction`.
  (5) `bench gate aa --run <it> --against <p2-grok-4.7-cs2-r1's cell>`; the cell id is `CalibImport.CellId` of that
  record (printed in the results document). (6) `bench gate assess --run <it>` with the codex assessor, so seeds hit
  and validity are known before S7.3 — `bench gate report` over the A/A scope shows them per reviewer. For the three
  CLI reviewers there is no Python baseline and no prompt file: their A/A is weaker, and said so — both repeats valid,
  the pin recorded, findings and seeds hit side by side. A recording wrapper around the CLI (the product passes the
  prompt on stdin and honours `executablePath`) would make their input observable; it is a new binary on the product
  path with its own exit/cancel forwarding risk and it would change the rows' `executable-ref`, so it is NOT in E7 and
  is recorded as an open question in §9.
- **S7.3 — narrowed by the operator (2026-09-28).** The phase-2 feature runs are no longer outstanding (all 84 settled
  in Python and imported, `RESULTS_gate_feature_first_real_report.md`). What remains is the plan and code gates over the
  seven tasks × 3 repeats for **grok-4.7 + glm-5.3** (84 runs, ~$25–35), on the current coai `main` built from a clean
  worktree (its own pin; D5 refuses a moved product mid-campaign), then `bench gate assess`. Whether the three CLI
  reviewers join the matrix is asked with the A/A's result, not assumed.
- **S7.4 — the hand-check is deferred by the operator.** Strict percentages stay `not hand-checked`, and this plan
  keeps that item open rather than ticking it.
- **S7.5 — the record.** `research/RESULTS_gate_aa_cs2.md` (this repository): subject shas, harness commit, date, the
  pinned variables, the self-check, the per-cell `aa` lines, observed against predicted; then the S7.3 results the same
  way. `module_gate.md`: domain rows for `PromptShape` and `GateTurnOnePrompt`, the `SeedEvidence` row updated, an
  Entry-points line for `bench gate aa` with its exit codes. Only the redacted output is ever copied into a document.
- **Growth.** ≈ 2.3 MB per feature run on disk (§4, measured) → 10 A/A runs ≈ 25 MB and 84 plan/code runs ≈ 200 MB
  under `runs/<id>/`, covered by the existing rows of §4 (`bench gate prune` for tap bodies past 30 days, the rest kept
  with the run; an interrupted attempt kept whole and resumed by `bench gate resume`, its clones removed by `bench gate
  sweep`). `bench gate aa` itself writes nothing.
- **Not in E7, said rather than dropped:** this repository has no `research/module_tests.md`, which the shared
  scenario-tests rule requires of every repository — a repository-wide gap older than this epic, recorded in §9.
- **Code egress, confirmed by the operator:** the same seven repositories and vendors as the 09-27 calibration (xAI,
  Alibaba DashScope), plus Anthropic (Claude CLI) and OpenAI (Codex CLI) for the A/A.
- DoD (E7): `PromptShape`, `GateTurnOnePrompt` and `bench gate aa` merged with every RED watched; the self-check
  printed `same shape` for the five imported cs2 cells; the A/A ran, was compared and assessed; S7.3 ran and was
  assessed; `RESULTS_gate_aa_cs2.md` and `module_gate.md` written; the hand-check still open and said so.

**Prediction for the amended S7.2, written before it runs:** every grok-4.7 and glm-5.3 cell of the A/A campaign has
shape `837fed1ae93f14bf…` (the Python runs' shape) — any other shape is a port defect, named by line; grok's seeds hit on
cs2 falls in the Python range for that task (its three repeats), glm's likewise; each of the three CLI reviewers settles
both repeats valid. If a line of this does not hold, it is the record of a wrong guess.

## 6. Boundaries named on both sides

| item | this plan | the other side |
|---|---|---|
| the product's own instrument | ports mechanism from `coai-bench`; does not replace it for coai's own release campaigns (five windows, store fixes) | `coai · research/module_bench.md` gains one row pointing here for MODEL measurement — to be added in a coai pull request when E3 lands |
| the feature-gate calibration | imports its records and completes its phase 2 through the C# driver | `coai · todo/PLAN_feature_review.md` §9 / S1.2 names this plan as where the re-runnable measurement lives — same pull request |
| the console host | the pages live here | `dew_flow_rag_qln` bumps the submodule pin; no code. Owner: the coordinator, in a qln pull request right after E6 merges. Order: this repository's E6 pull request → the qln pin → the coai cross-reference pull request |
| the code lane (`PLAN_code_lane.md`) | measures a REVIEWER reading a diff | measures a SUBJECT producing one; disjoint |
| the tool benchmark (`PLAN_tool_benchmark.md`) | the catalog-row precedent is borrowed; nothing else | — |

## 7. Test plan

- Domain (E1): pure tests for every record's refusals; `SlotRotation` and `Claimable` extractions proven by the
  existing suites staying green byte-for-byte.
- Store (E2): `PostgresFixture` for claim/settle/sweep and the publication guard over real rows; the guard's
  fixture contains one deliberately dirty row per rule and the test names it.
- Driver (E3): a **fake `coai-mcp`** in `tests/Bench.Tests/Fixtures/fake-coai/` (a console speaking newline
  JSON-RPC, scripted replies per tool, a `hang` verb) drives every protocol test; one live class against the real
  binary, skipped when unconfigured.
- Tap (E3): a local dripping upstream; a header never written; a closed socket at the deadline.
- Assessment (E4): a fake CLI (the `CliAgentRuntimeTests` shape) returning scripted schema rows, a missing id,
  and a malformed batch.
- Import (E5): fixtures redacted from real records (one `runs.jsonl` line, one `runs.json` record, one verdict)
  — no private name, checked by the guard.
- Report and page (E1, E6): fixed vectors for the quantiles; bUnit for every refusal in words.
- The whole suite by the executable, never `dotnet test`.

## 8. Definition of Done

- [ ] `dotnet build dew_flow_benchmark.slnx -c Release` — 0 warnings; the test executable green; the architecture
      guard passes with its two new assertions.
- [ ] Every story's RED was watched failing for its real symptom and is named in the commit body.
- [ ] A campaign of `bench gate run --gate feature|plan|code` runs, stops, resumes and sweeps; every run pinned to
      a product sha; every `COAI_*` sent is stored beside its hash with no secret in it.
- [ ] The 71 Python runs are imported and the page shows them; the prediction of §5 E7 is compared with the
      A/A observation and recorded.
- [ ] The publication guard passes over a database holding imported and native runs; `bench gate export --public`
      carries no `://`, no user path, no private name.
- [ ] Twenty strict verdicts hand-checked before any strict % appears for a scope.
- [ ] `research/module_gate.md` written; `architecture.md` updated; the qln pin bump and the two coai
      cross-references named in their pull requests, in the order of D11.
- [ ] The structural guard (reflection over every `gate_*` entity and `Gate*Dto` string property) and the
      string guard both pass; `COAI_VENDORS` is spelled in exactly one production file.
- [ ] The concurrency test reaches and never exceeds `--parallel` and `--per-endpoint`; two concurrent cells
      never share a data dir unless the run is shared; an interrupted cell's next attempt is fresh and the old
      attempt is kept and marked.
- [ ] `RubricKind` is a required dimension of every report call; `AssessmentFailure` rows are counted apart.
- [ ] This plan promoted with its deviations — and a partial landing extracts what is left into a fresh `todo/`
      plan rather than holding this one.

## 9. Open questions for the operator

> **Assumed 2026-09-27, pending the operator** — E1 was built on these answers so it could land; each is a
> one-line change if the operator decides otherwise, and none is a promise the code makes on its own:
>
> 1. isolated data directory by default; 2. a checkout build is "the product", pinned by sha; 3. assessors —
> codex primary, the Claude CLI for the agreement figure; 4. the lenient 09-05/09-06 verdicts are shown in
> their own labelled column (`RubricKind.LenientWorth`, never in a strict figure); 5. the seeded 8-defect plan
> and coai's own plans may go in `samples/`; 6. the suite file lives in the local artefact root, outside git;
> 7. CLI reviewers show *cost unknown*, never zero (`CapturedUsd`, `ReviewerPrices.Unknown`, `Figure.Unknown`);
> 8. the coordinator bumps the qln pin after E6.

1. **Isolated or real data directory by default?** This plan says isolated (models are the subject, the store is
   not). `coai-bench` defaults to the real one so rounds show in the panel while a person watches.
2. **Which binary is "the product" for a re-run** — the installed one (`coai-bench`'s default) or a checkout build
   (the calibration's)? Both are pinnable; the plan refuses a silent switch mid-campaign either way.
3. **The assessor**: keep `codex exec` with `gpt-6-astra` (the calibration's), add the Claude CLI as a second
   assessor for an agreement figure, or both? Its family match with a reviewer is flagged, never refused.
   **E4 (measured):** as specified (`--max-turns 1`), the Claude assessor stops at its turn ceiling as soon as it
   reaches for a read tool — raise the ceiling, or inline the code windows into its prompt the way the coai-bench judge
   does, before it can give an agreement figure worth reading.
4. **Should the lenient `worth having` verdicts of the 09-05/09-06 campaigns appear on the plan/code pages?** The
   plan says yes, in their own column, labelled — never in a strict figure.
5. **May the seeded 8-defect plan and coai's own plans be committed as `samples/`?** They are public text; the
   seven trial repositories stay private regardless.
6. **The suite file's home**: a git-ignored folder in this checkout, or the artefact root? The plan says the
   artefact root (`--suite-file` defaults to `<artifact-root>/suite.json`), so one path names everything private.
7. **CLI reviewers** (codex / gemini / claude through their CLIs) cannot be tapped and codex reports no cost —
   accept *unknown* on those rows, or exclude them from the cost columns?
8. ~~**Who bumps the qln submodule pin** for the page, and in which order with the coai cross-reference pull request?~~
   **Settled by the plan round (finding 5):** the coordinator, in a qln pull request right after E6 merges;
   E6 → the qln pin → the coai cross-reference pull request (D11, §6).
9. **(E7) A recording wrapper for CLI reviewers?** The product passes a CLI reviewer its prompt on stdin and honours
   `executablePath`, so a wrapper could record the input of Fable/Opus/Astra runs and give them a prompt-shape A/A like
   the API rows. It is a new binary on the product path (exit, output and cancellation forwarding) and changes the rows'
   `executable-ref`; not built in E7 — build it as its own plan?
10. **(E7) `research/module_tests.md` is missing here.** The shared scenario-tests rule requires every repository to
   catalogue its flows (derived from the CLI verbs) and the ones not covered. This repository has none; a plan of its
   own?
