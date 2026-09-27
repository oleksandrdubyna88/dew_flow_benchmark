# PLAN — the coai gate-model benchmark: plan, diff and feature gates, in C#, re-runnable

> Status: **E1 (the domain and the contracts) and E2 (the store and the privacy guard) landed 2026-09-27 —
> `research/module_gate.md` describes them; E3–E7 open.** Scope: a new bounded context `Gate` across
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
> - **The reviewer endpoint is the one column checked by a stricter rule than `://`.** D6 makes a public vendor
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

### E3 — the driver (Fable for S3.1, S3.2, S3.7; Opus otherwise)

- **S3.1** `ProcessSession` (long-lived exe + argv, stdin/stdout pipes, stderr to a file, kill the tree on
  dispose, `IsAlreadyGone` shared) + `McpStdioClient` (`initialize`, `notifications/initialized`, `tools/list`,
  `tools/call` with an absolute per-call timeout, notifications kept). RED against a fake MCP server in the test
  project (a tiny console that speaks newline JSON-RPC): handshake before any call; a hung call ends at its
  timeout with the process gone; stderr survives a crash; **one session per cell** — a lane that claims a second
  cell while holding a session is a programming error the type refuses (`ProcessSession` is owned by the cell's
  scope and disposed at settle).
- **S3.2** `CoaiEnvironment.For(run, reviewer, task, attempt)`: parent `COAI_*` dropped, every knob set, the
  vendor row, the caller session id with its attempt suffix, `COAI_DATA_DIR` from `CellPaths.DataDirFor`, the
  secret injected last; `Snapshot` = the same map minus secrets, hashed. RED: the snapshot never contains the
  key's value; the hash is stable across two runs with different keys; two cells resolved concurrently never
  share a data dir unless the run is shared; a run stored isolated cannot be resumed shared, and vice versa;
  attempt 2 of a cell gets a different data dir and caller session id from attempt 1, and attempt 1's directory
  is untouched.
- **S3.3** `PlanGateProtocol`, `CodeGateProtocol` (open → plan loop → resolve accept-all → code → resolve; a ref per
  run; `Passed`), `FeatureGateProtocol` (`review_feature` with the suite's inputs). RED against the fake server:
  the code gate is refused when no plan round passed (the product's rule, replayed by the fake); the resolve
  reply's refusal is kept on the stage; `again` is never sent on a first call.
- **S3.4** the reply parser + the ledger reader (`usage.jsonl` rows → turns) + `GateRunFacts` + `FailureCause`.
  RED: a length-cut call is named as such; a non-JSON reply is `tool answered non-JSON`; a `call_human` verdict
  is not valid and says why.
- **S3.5** `SettingsCheck` port: asked-for settings against the session config on disk, scoped to this run's
  session. RED: an accepted-and-ignored knob is reported as a mismatch.
- **S3.6** `ProductPin.Read` over the binary and its checkout: `git status --porcelain --untracked-files=no`
  scoped to the product's source tree. RED: a binary outside any checkout has an empty git sha and says so; an
  untracked scratch file beside the source does not dirty the pin; a modified tracked file under the product's
  project does; a modified file elsewhere in the checkout does not, and the pin says which tree was checked.
- **S3.7** `RecordingTap` (Kestrel on a loopback port per run; body written before forwarding; absolute deadline
  closing the upstream socket; `Authorization` in memory only; `read_calls`/`facts` equivalents). RED: the
  recorded request file never contains the header value; a dripping upstream is closed at the deadline with the
  call marked; every forwarded request is answered or marked before the run finishes.
- **S3.8** `bench gate run` (`--gate`, `--suite-file`, `--reviewers`, `--repeats`, `--coai-exe`, `--artifact-root`,
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
- DoD: a live test class (skipped when `BENCH_GATE_COAI_EXE` is unset, the `QlnEngineLiveTests` shape) drives
  `providers` and one `review_plan` against the real binary with a fake vendor and stores a run.

### E4 — the assessment (Opus)

- **S4.1** `prompts/gate-assess/strict.md` (the calibration's rubric verbatim) + `lenient-worth-v1.md` (the coai
  judge's question, for the import's label only), hashed through `PromptCatalog`.
- **S4.2** blinded export: fresh ids, key to the artefact store, rows carrying the assessor's checkout path from
  `ICheckoutProvider.EnsureAsync(target at the variant)`. RED: an exported row contains no model, run id or
  reviewer; an id is never reused.
- **S4.3** `AgentAskOptions` on `CliArgv.For` (sandbox, output schema, disallowed tools, MCP servers off) +
  `FindingAssessor` over `ICliAgentRuntime`. RED: the argv for codex carries `-s read-only` and `--output-schema`;
  the claude argv carries `--disallowedTools Edit Write NotebookEdit --max-turns 1`.
- **S4.4** batches of ≤ 24, verdict rows appended per batch, prior cluster keys carried; a batch whose output
  does not parse, is truncated or names unknown ids is retried once, then every finding of it gets an
  `AssessmentFailure` verdict named by cause. RED: a killed batch leaves earlier batches' rows in place; a re-run
  skips them; a batch answering with an extra id is retried and, failing again, yields `UnknownIds` rows for every
  finding it carried; a later pass re-asks exactly the failed rows and its verdicts supersede the failures; the
  report counts failures in their own column and never in supported %.
- **S4.5** seed matching and evidence: `seedHit` must name a seed of the row's task; `SeedEvidence` from the
  product's turn-1 prompt file (pack / on request / withheld). RED: a seed id from another task is refused.
- **S4.6** `bench gate assess --run|--scope … --assessor <reviewer-catalog id> [--rubric strict-v1]`.
- **S4.7** `AssessorFamilyMatches` flagged on the verdict and counted apart in the report.
- DoD: the hand-check (twenty verdicts read by a person) is recorded before any strict % is shown for a scope.

### E5 — the import (Opus)

- **S5.1** `runs.jsonl` + run directories → runs, findings, artefacts, reviewer rows by preset hash. RED: a fixture
  line (a real one, redacted to the sample suite's names) round-trips every fact; a second import changes nothing.
- **S5.2** `assess.jsonl` + `key.json` → verdicts under `strict-v1`.
- **S5.3** `coai-bench` `runs.json` → plan/code runs, `Useful` → `lenient-worth-v1`, unjudged stays unassessed.
- **S5.4** `SummaryOnly` rows for the 2026-09-01/02 documents when the raw JSON is gone.
- DoD: the page shows the 71 imported feature runs with the exact per-model numbers of the Python `results.json`
  of the same day (a fixture pins four of them).

### E6 — report, API, page, docs (Opus)

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
- DoD: `PendingKind` is gone from the three routes; `BenchUiRegistrationTests` cover the new service reads;
  **the pinned console shows the Gate page** — the coordinator's qln pin-bump pull request, opened right after
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
