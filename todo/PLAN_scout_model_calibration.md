# PLAN — Scout subagents: does a Sonnet team find what an Opus team finds, how fast, at what price?

> Status: **plan only, nothing implemented yet, 2026-10-12.** Scope: a new **Scouts section** of this benchmark — its
> own sibling context `Bench.*.Scouts`, the `bench scouts` verbs, a read API and a **Scouts** tab in the console, as the
> gate and the probes each have — two options added to the one launcher, and the detailed results in `research/`
> (`RESULTS_scout_models.md` plus the per-cell records under `research/scout-calibration/`).
>
> Related: [module_probes.md](../research/module_probes.md) (the context this one is composed from),
> [module_gate.md](../research/module_gate.md) (the blinded assessor), [architecture.md](../research/architecture.md).
> The rule this measurement decides lives in the shared conventions: `dew_flow_conventions · common/subagent-models.md`,
> and its own plan is `dew_flow_conventions · todo/PLAN_scout_subagents_on_sonnet.md`.

## 1. The question, before any solution

The shared rule today puts **every** subagent on Opus. The operator wants the **scouts** — read-only subagents whose
whole output is facts and file lists (code explorers, the built-in Explore agent, lookup agents) — to run on the newest
Sonnet, and to launch half as many again: where N Opus scouts would run, ⌈1.5·N⌉ Sonnet scouts run (1→2, 2→3, 3→5).
Judges — reviewers, architects, planners, implementers, finding-verifiers — stay on Opus.

The operator chose to **measure before switching**. This plan answers, for each of the three team sizes separately:

1. **Quality (decides).** Does the Sonnet team find the files that matter as well as the Opus team, without more wrong
   claims?
2. **Time (reported).** Which team finishes sooner?
3. **Cost (reported).** Which team costs less, in tokens converted to dollars?

### Operator decisions (2026-10-12, recorded so nobody re-asks them)

| # | Decision |
|---|---|
| D1 | A scout is any read-only fact-gathering subagent, in every task — not only inside one plugin's workflow. |
| D2 | Reviewers are judges and stay on Opus; so do architects, planners, implementers and finding-verifiers. |
| D3 | One scout becomes two: the formula has no exception at N = 1. |
| D4 | Calibrate first; switch only on the result. |
| D5 | The accepted loss: the Sonnet team may find at most **3 percentage points** fewer of the right files, and make at most **0.5 more wrong claims per 10 claims**. Each team size passes or fails on its own; the rule changes only if all three pass. |
| D6 | Time and cost are measured and reported for every team size. They do not gate the decision unless the operator says so before the main run starts. |

D5 is a non-inferiority margin, and it is the operator's, not the harness's: a noisy harness must never widen it. The
pilot sizes the experiment to distinguish it (§3.8); it does not move it.

## 2. What exists — verified 2026-10-12

| Fact | Where |
|---|---|
| One launcher for every CLI, already widened once for the probes: `JsonEvents` (claude `--output-format stream-json --verbose`), `DisallowedTools`, an `AllowedTools` allow-list, `McpServersOff`, `MaxTurns`, `Restricted`, `AddDirectories` | `src/Bench.Application/CliAgent.cs:77-123`, argv at `src/Bench.Infrastructure/Models/CliAgentRuntime.cs:97-109` |
| A stream-json reader that already knows the event shapes, skips `system/task_*`, reads tool fates without reading tool content | `src/Bench.Domain/Probes/ClaudeStream.cs:1-116` |
| A campaign with `run · resume · rerun · status · sweep · report · prune`, exit codes 0/3/4/5, an owner-checked sweep before any claim, generations appended on re-run | `hosts/Cli/ProbesCommand.cs:9-40`, `src/Bench.Application/Probes/ProbeCampaign.cs:46-67` (`PrepareAsync` at :55, `RunAsync` at :67) |
| Cells that compose `Claimable` and `ProductPin`, the gate store's guarded claim, `ArtifactCommit`, `PublicationGuard` on public export | [architecture.md § The capability probes](../research/architecture.md) |
| A blinded assessor with fresh shuffled ids; codex runs it read-only, claude only with `MaxTurns = 1` (cannot open files) | `src/Bench.Application/Gate/FindingAssessor.cs:81-94`, `src/Bench.Domain/Gate/BlindExport.cs:133-153` |
| Pre-trust of a working tree, only under the checkout root | `src/Bench.Infrastructure/Models/WorkspaceTrust.cs:52-85` |
| A task base at the parent of a fix commit, merge commits refused | `hosts/Cli/HarvestCommand.cs:13-25` |
| **Gap:** the CLI-subject worktree is a git worktree whose `.git` redirects to a full mirror that holds the later fix — only the agent's behaviour keeps it out of the future | `src/Bench.Infrastructure/Git/ScratchWorktree.cs:21-33`, [architecture.md:411-416](../research/architecture.md) |
| **Gap:** no `--forward-subagent-text`, no `--max-budget-usd` option on the launcher | `src/Bench.Application/CliAgent.cs:77-123` |
| **Gap:** `ClaudeStream` does not group by `parent_tool_use_id`, so it cannot tell one subagent from another | `src/Bench.Domain/Probes/ClaudeStream.cs:25-58` (`Read` takes the whole stream as one tool trace) |

**Feasibility probe, 2026-10-12, claude 2.1.289** (prediction written first; the stream was read, nothing scored). A main
`claude -p --model opus --output-format stream-json --verbose --forward-subagent-text --max-budget-usd 2 --disallowedTools
"WebFetch WebSearch" --no-session-persistence` session launched two `feature-dev:code-explorer` subagents in one message,
one with `model: "sonnet"`, one with `model: "opus"`. Observed, as predicted:

- every child event carries `parent_tool_use_id` = its Agent call's id, and `message.model` = `claude-sonnet-5-5` /
  `claude-opus-5-5`;
- each Agent result carries `tool_use_result.resolvedModel`, `totalDurationMs`, `totalToolUseCount`, `toolStats`;
  `system/task_progress` and `task_notification` carry running `duration_ms`; 34 of 55 events carry a `timestamp`;
- the final `result` carries `duration_ms`, `total_cost_usd`, `modelUsage` split by model, and `subagent_stats`
  (`spawned`, `completed`, `failed`, `refused.budget`).

Not predicted: `modelUsage` also lists an internal `claude-haiku-4-5` call; and when the main session and its scouts share
a model (the Opus team), `modelUsage` cannot split them — per-scout tokens must be summed from each child's messages,
**deduplicated by message id** because usage repeats across a message's content blocks.

## 3. Design

### 3.1 Tasks and their ground truth

A task is one past change in a **public** family repository, investigated at its parent commit P:

- **Selection, pre-registered and mechanical.** Candidates are squash-merged PRs that changed 3–15 files that exist at
  P, merged after the later of the two models' knowledge cut-offs, and are not a release, a version bump, a
  dependency bump or docs-only. **The cut-offs are evidence, not memory:** before any candidate is listed, the run
  records, for each resolved model id behind `opus` and `sonnet`, the vendor's published training-data cut-off with
  its source URL and the date it was read. A model whose cut-off is unpublished or ambiguous for that exact id stops
  the selection — it is not guessed. A candidate whose P lies before the later cut-off is excluded, because a model
  that saw the code in training gains recall that says nothing about scouting. Per repository, a seeded draw picks from the candidates; the seed and the candidate
  list are committed before the draw. Sources: the six public family repositories; the one private family repository
  is excluded.
- **Three kinds of question**, roughly a third each: *where is X decided*, *how does Y work end to end*, *what must
  change if Z changes*.
- **Ground truth G** = `git diff --name-only P S` restricted to files that exist at P, minus lockfiles, generated files,
  changelogs, version manifests, and the PR's own plan or results document.
- **The task statement** is at most three sentences about the problem, written before any run and frozen. An automatic
  leak check refuses a statement that contains a basename or stem from G, or an identifier the diff added.
- **How many**: at least 12; the final count comes from §3.8's simulation, capped at 30.

### 3.2 The tree a scout sees: history-free

`git archive P` from the existing bare mirror, extracted under the checkout root, then a fresh `git init` and ONE
commit — so `WorktreeAudit` and `WorkspaceTrust` keep working, and there is no later history to read. A submodule's
content at its pinned sha is archived into its path. If the PR's own plan existed at P, it is removed and the removal
is recorded on the task. The tree's hash is recorded and checked before every cell.

**Removing history is not confinement.** The archive removes the future from the tree; it does not stop a child from
reading elsewhere. Measured 2026-10-12 on claude 2.1.289, from a one-commit tree with a random-token canary in its parent
directory (prediction written first; streams kept with the probe):

| Launch | Child's outside reads | Plugin agent |
|---|---|---|
| default permissions | Read (absolute and `../`) and Grep `..` → `permission_denied`, reason `workingDir`; Glob `../*.txt` → "No files found"; no token anywhere | works |
| `--restricted` | — | **gone**: `feature-dev:code-explorer` "not found" (plugins come from user settings, which restricted ignores) |
| `--tools "Task,Read,Glob,Grep"` + `--strict-mcp-config` with an empty config | the parent canary denied — but a Read and a Glob inside a directory the operator's user settings grant (`permissions.additionalDirectories`) **succeeded** | works; only those four tools offered |

The older evidence (`research/PLAN_question_consultant_probes.md:630`, claude 2.1.258) saw outside reads succeed; the
newer CLI denies them unless a setting grants the directory. So the boundary is **every readable location that holds
future evidence**, not where the tree is placed:

- **Launch:** the explicit allow-list (`AllowedTools` → `--tools "Task,Read,Glob,Grep"`) and MCP off — never `--restricted`,
  which removes the agent under test.
- **Grants:** a granted directory is readable from any tree. E2 S4 first tries a launch that keeps the plugin but drops the
  user grant (a pinned `--settings` file and the setting sources narrowed, the plugin supplied by `--plugin-dir` from
  the pinned cache) and proves it with the canary. If no such launch exists, every task whose repository has a checkout
  inside a granted directory is excluded from the candidates, and the exclusion is recorded.
- **Path audit, fail-stop:** the read-back resolves the target of every child `Read`, `Glob`, `Grep` and `LS` — relative
  paths, an omitted search root, absolute glob patterns, and a junction or symlink by its resolved target. An inside
  target is fine; an outside attempt that was denied is recorded; an outside call that returned content, or a Glob that
  returned any outside path, **stops the campaign** and leaves the cell unscored — a void-and-retry would keep the broken
  boundary. A tool result that is missing, truncated or unrecognised never counts as a safe read, and an explicit denial
  is kept distinct from an empty successful result. A child tool outside the read-only set voids the cell.
- **Access check (pilot, E2 S4):** the canary check above repeated with the exact campaign launch, for
  `feature-dev:code-explorer` and for Explore, against a sibling task tree, a granted directory, the mirrors, and a
  junction escaping the tree. A child that never attempts a call is *inconclusive*, not protected.

### 3.3 Arms

| Workload | Opus team (A) | Sonnet team (B) | Secondary |
|---|---|---|---|
| W1 | 1 × opus, focus F1 | 2 × sonnet, F1–F2 | — |
| W2 | 2 × opus, F1–F2 | 3 × sonnet, F1–F3 | — |
| W3 | 3 × opus, F1–F3 | 5 × sonnet, F1–F5 | C: 3 × sonnet, F1–F3 (separates the model effect from the count effect) |

- Every scout is `feature-dev:code-explorer` with `model` named explicitly; the agent type is pinned by the plugin
  cache hash. An **Explore replication** — W3, arms A and B only, a quarter of the tasks — tells the rule whether its
  Explore clause is measured or extrapolated.
- **Focus areas are frozen per task before the run**, distinct, never copies: F1 similar features, F2 architecture and
  abstractions, F3 current implementation, F4 tests and fixtures, F5 wiring, configuration and entry points. The extra
  Sonnet scouts get the extra areas, as the rule would tell a real session to do; RESULTS says that the variable is the
  whole policy (model + count + split), not the model alone.
- Every scout ends with two blocks, in this order: `KEY FILES` — at most 10 repository-relative paths; `CLAIMS` — at
  most 10 lines, each `path[:line] — one factual statement about that file` (what it defines, calls, decides or
  contains). The output contract is part of the frozen prompt, identical across arms.

### 3.4 One cell

One main `claude -p --model opus` session per cell, working directory = the task's tree, flags: `--tools
"Task,Read,Glob,Grep" --strict-mcp-config --mcp-config <empty> --output-format stream-json --verbose
--forward-subagent-text --max-budget-usd <reservation> --no-session-persistence`, plus whatever E2 S4 proves is needed
to drop the user grant (§3.2). The driver prompt says: *in one message launch exactly
these N Agent calls, these prompts verbatim, then reply DONE.* The `--max-budget-usd` value is fixed **per arm** in E2
from the pilot's largest observed cell cost × 1.5, and recorded in `schedule.json`; it is never left to a default.

Pinned, and recorded on every cell: CLI version, plugin cache hash, the resolved ids behind `opus` and `sonnet`, the
operator's settings hash (effort levels included — they are the real condition the rule will run under), the tree
hash. **A change of any pin mid-run stops the run.** Cells run one at a time, in a seeded schedule that interleaves
the arms inside each task, so drift in time of day and server load falls on every arm.

### 3.5 Read-back — every cell, not a sample

Exactly N Agent calls; each call's `input.model` and prompt hash equal the frozen ones; each child's `resolvedModel` and
`message.model` belong to the arm's model; no tool outside the read-only set in any child; every read path inside the
tree (§3.2's fail-stop audit); `subagent_stats.completed = N`. Any miss
makes the cell void with its reason. The main session making any call other than Agent voids the cell too.

### 3.6 Metrics

| Metric | Definition | Role |
|---|---|---|
| **Recall** | \|G ∩ ∪ KEY FILES of the team\| ÷ \|G\| | **decides** (D5) |
| **Wrong claims per 10** | every line of every scout's `CLAIMS` block is assessed (no sampling); team rate = wrong ÷ assessed × 10, a ratio over the team's claims, not a mean of per-scout rates | **decides** (D5) |
| Team time | the first Agent `tool_use` event's `timestamp` → the last Agent `tool_result` event's `timestamp` (both carry one in the probe; `task_notification` does not); cross-checked against the slowest `totalDurationMs` | reported (D6) |
| Scout time | each child's `totalDurationMs` | reported |
| Session time | `result.duration_ms` | reported |
| Tokens per scout | input, output, cache read, cache write — summed over the child's messages, deduplicated by message id | reported |
| **Cost per scout and per team** | tokens × the vendor price table read on the run date (URL and date recorded) | reported (D6) |
| Cost per right file | team cost ÷ files of G found | reported |
| Caller load | size of the union — the files the Opus caller must then open | reported |

**Cost check:** per cell, the computed main + scouts cost must equal the CLI's `total_cost_usd` within 2 %; a larger gap
stops the run — the price table or the token sum is wrong, and every cost after it would be too.

**Claims, exactly.** A `CLAIMS` line that does not parse as `path[:line] — statement` is counted as *unparseable* and
reported per arm; it is not assessed and not dropped silently. A scout with no `CLAIMS` block contributes zero assessed
claims and is counted as *silent*. A team with zero assessed claims in a cell has no wrong-claim rate for that cell:
the cell still counts for recall, and the task's wrong-claim mean is taken over the cells that have one. If more than
10 % of an arm's cells are silent, the arm's wrong-claim result is reported as *not measured* and that workload cannot
pass (D5 needs both halves). Per task, the rate is pooled over its runs (Σ wrong ÷ Σ assessed × 10), so a run with one
claim does not weigh as much as a run with ten.

### 3.7 Hand-checks before any number is published

- **Recall matcher**: 20 seeded (output, G file) pairs, half found and half not, with basename-only and `path:line`
  forms planted. At least 19 of 20 must agree with a person's reading; otherwise fix the matcher and draw 20 new pairs.
- **Assessor**: codex read-only, launched with the options `FindingAssessor.OptionsFor` already builds (claude is capped
  at one turn there and cannot open a file) — but with its **own** prompt, schema and working directory (the task's
  tree), because the gate's assessor is shaped for finding batches, not for one-line claims. 20 verdicts re-checked by
  opening the file. At least 18 of 20 must agree.
- Rubric for a wrong claim: the path does not exist; the symbol is not in the named file; the code contradicts the
  stated behaviour; the cited line is off by more than 10.

### 3.8 The decision, pre-registered

Per workload W, over tasks (the task is the unit; its runs are averaged first):

- Δrecall = mean(B) − mean(A); Δwrong = mean(B) − mean(A), per 10 claims.
- Paired bootstrap over tasks, resampling whole repositories as clusters, fixed seed, one-sided 95 % bounds.
- **W passes** when the lower bound of Δrecall ≥ −0.03 **and** the upper bound of Δwrong ≤ +0.5.
- **The rule changes only if W1, W2 and W3 all pass.** Any failure or any interval that crosses a bound is reported as
  *not shown*, and Opus stays. There is no extension round: an inconclusive result is a result.
- Explore: its clause in the rule is written as *measured* only if the replication passes the same test; otherwise the
  rule says its Explore clause is an extrapolation.

**Sizing (E2).** The pilot's variance feeds a simulation of exactly this procedure — clustered tasks, repeated runs —
at a true Δ of 0 and at the −0.03 boundary. The task count is the smallest that gives ≥ 80 % passes at Δ = 0 and
≤ 5 % false passes at the boundary, with runs per task fixed at 3. If 30 tasks do not reach that, the run is not
started and the operator gets the numbers instead: precision is bought with tasks, never with a wider margin.

### 3.9 Prediction — committed before the main run

Written into `research/RESULTS_scout_models.md` before cell one, with its commit sha cited in the result:

- Recall: B ≥ A in W2 and W3; W1 between −0.02 and +0.05. C below A by 0.03–0.08 (each Sonnet scout is a little less
  thorough; the extra scouts make up for it).
- Wrong claims: B ≈ A + 0.3 per 10.
- Team time: B ≈ 0.6–0.8 × A (smaller model, parallel scouts, team time = the slowest scout).
- Team cost: B ≈ 0.3–0.5 × A. Basis: the probe, where one trivial Sonnet scout cost $0.023 against $0.57 for one Opus
  scout — a single unmatched run, so a hint, not a measurement.

### 3.10 Persistence, spend and interruption

- The probe store's shape: cells with claim/settle, generations on re-run, the owner-checked sweep at every entry, so
  a crash, a reboot or a spend limit loses at most the cell in flight; `bench scouts resume --run <id>` continues.
- **An experiment-wide spend ledger**: before a cell launches, its reservation (the `--max-budget-usd` value plus 20 %
  headroom, because the CLI's cap is an estimate and can overshoot) is reserved against the run's ceiling; the actual
  `total_cost_usd` settles it. A run whose next reservation does not fit stops with exit 3 and says so.
- A spend-limit or quota error settles the cell as *aborted:spend* and stops the run; it is never scored.
- **Money survives a crash.** Today's launcher buffers a child's whole output in memory and hands it back at exit
  (`src/Bench.Infrastructure/Process/ProcessRunner.cs:168`), and the probe store's hand-back requeues an orphaned cell
  without asking what it spent (`src/Bench.Infrastructure/Persistence/PostgresProbeStore.cs:314`). A scout cell is paid
  for while it runs, so: every **attempt** carries its own reservation row; the stream is written to the artefact root
  line by line as it arrives (the launcher widened with an output sink, not copied); and recovery settles each attempt
  exactly once — from its `result` envelope when one survived, otherwise as an explicit **unknown-cost charge** equal
  to its reservation, counted in the ledger and reported. **The launch boundary is durable, and doubt is charged:** the attempt is marked *launching* in the store BEFORE the CLI
  is invoked, so recovery never has to guess — an attempt with no *launching* mark provably never started and its
  reservation is released; an attempt marked *launching* with no surviving `result` is charged its reservation as
  unknown cost, even if the process may have died before spending anything. The run's
  final check is then *ledger = Σ `total_cost_usd` of settled attempts + Σ unknown-cost charges*, never a bare
  equality that a crash would break.

### 3.11 Growth budget (planning-docs)

| Surface | Projected size | Retired by |
|---|---|---|
| History-free trees | ≤ 30 tasks × ~40 MB ≈ 1.2 GB, under a git-ignored directory of the checkout root (each has its own `.git`) | `bench scouts prune --run` after RESULTS is merged — only directories the run created, path checked |
| Raw streams | main run ≈ 30 tasks × 3 runs × 7 arms = 630 cells, + ~45 Explore cells, + void re-runs (budget 10 %) ≈ 740 × ~1 MB ≈ 0.75 GB, local only | kept until the conventions rule ships, then gzipped; never committed |
| Wall clock | ~740 cells one at a time ≈ two days of serial running for E3 | resumable at any cell |
| Committed records | per-cell metrics + scrubbed scout texts ≈ 5 MB | kept with RESULTS |
| Interrupted cells | the existing owner-checked sweep | at every verb's entry |

### 3.12 A section of its own, and results written out in full (operator, 2026-10-12)

This calibration is not a script beside the benchmark: it is a **section** of it, shaped like the gate and the probes —
its own context, verbs, store tables, read API and console tab — so a re-run when an alias moves is one command and the
history of every run is browsable. One pure `ScoutReport` answers `bench scouts report --json`,
`GET /api/bench/scouts/runs[/{id}]` and the console's **Scouts** tab byte for byte, as `ProbeReport` does; the tab polls
through `LivePoller` while a cell is open, and starting a run stays a CLI verb the page shows as a command
(`bench-api` is read-only by decision).

The results are written out **in detail** in `research/`, not summarised:

| File | Holds |
|---|---|
| `research/RESULTS_scout_models.md` | the verdict per workload; recall, wrong claims, team/scout/session time and cost per workload with intervals; observed against predicted; per-task tables (task id, repository, kind, \|G\|, each arm's recall, time, cost); the Explore replication; arm C against A and B; void and aborted cells with reasons; hand-check outcomes; every pin; what it does not settle |
| `research/scout-calibration/tasks.json` | each task: id, repository, P, S, kind, statement, G, removals, tree hash |
| `research/scout-calibration/prompts.json` | the frozen focus prompts per task and the driver prompt, with hashes |
| `research/scout-calibration/schedule.json` | the seeded schedule and the seed |
| `research/scout-calibration/cells.jsonl` | one line per cell: arm, run, pins, read-back result, per-scout model / time / tokens / cost / KEY FILES, recall, assessed claims |
| `research/scout-calibration/pilot.md` | the pilot: every cell with its outcome, the hand read-back, the access check, the budget-exhausted cell, the A/A, and each instrument correction with the RED fixture behind it |
| `research/scout-calibration/cutoffs.json` | per resolved model id: the published cut-off, its source URL, the date read — committed before the candidate list |
| `research/scout-calibration/handcheck.md` | the 20 + 20 hand-checked items and the person's reading of each |
| `research/module_scouts.md` | the section as built: the tuple, the verbs, the store, the report |

### 3.13 Public-repository hygiene

The repository is public. Committed records hold repository-relative paths, metrics and scrubbed scout texts only. The
scrubber removes drive and home paths, e-mail addresses, and every entry of a deny-list that is read from a local file
and never committed; a test fails when a committed record still matches, and when any file under the trees' directory
is tracked.

### 3.14 Watching the alias after the verdict

A verdict holds for the model ids it measured. `bench scouts alias-check` runs one tiny cell (a two-scout probe like
§2's, budget-capped at $1) and compares each scout's `resolvedModel` with the ids the verdict recorded. A Windows
scheduled task runs it weekly — registered by `scripts/register-scouts-alias-check.ps1`, as the telemetry ingest is —
and a changed id exits non-zero, writes the new id into `research/scout-calibration/alias-watch.md` and names the
conventions plan's suspension step. Detection lives here because a session cannot see the move: the Agent result a
caller receives carries the text and an agent id, not the model.

## 4. Build order — three epics, the gate once per epic

Each epic: its own branch from the previous epic's commit, one plan round, every story built, ONE code commit, one code
round over the epic's diff, the fixes folded into that commit. (E2 adds three evidence commits whose order is the
pre-registration — see S5.) Every epic ends green from the test executable at zero
warnings and is coherent alone. The model is fixed per story — Opus for ordinary work, Fable where a wrong answer is paid
later. The context has ONE migration (E1 S1); a column a later story needs is added to it before E1's commit, as the
probes did. The split was decided on Fable (2026-10-12); the operator then put S1 and S2 on Opus.

**E1 — The scouts context, end to end offline.** Branch `feat/scouts-context`. Goal: `bench scouts` plans, runs,
resumes, scores and reports a run against a controlled CLI and real storage, and no paid model has been called.

- **S1 — The facts and the store** *(Opus, by the operator's decision of 2026-10-12, which overrides the split's Fable choice: the deciders every later story composes, a migration, a ledger holding
  money, the read-back deciding validity).* `Bench.Domain.Scouts`: `ScoutTask`, `ScoutArm` (seven arms + Explore),
  `ScoutCell` composing `Claimable` + `ProductPin` as `ProbeCell` does, `ScoutRun` (no status column, frozen pins,
  ceiling), `ScoutSchedule` (seeded, arms interleaved per task), generations. `ClaudeSubagents` beside `ClaudeStream`.
  The `KEY FILES` and `CLAIMS` parsers. `ReadBack` (§3.5) with void reasons as an allow-listed enum — no free sentence
  reaches the database. Pure: leak check, recall matcher, cost calculator + price table + the 2 % comparison, the
  scrubber. Store: run, task, cell and per-scout tables, the
  ONE migration, the gate store's guarded claim and owner-checked sweep reused, the spend ledger as guarded updates on
  the run row, the tables joined to the publication walk; ports `IScoutStore`, `IScoutReads` (no write verb),
  `IScoutRunner`, `IScoutTrees`; the deciders registered in the architecture tests. Implements §3.1 (leak), §3.3
  (contract), §3.5, §3.6, §3.10 (ledger), §3.13. Done when every pure piece and the store are tested (store on
  Testcontainers; the migration on an empty and on a gate-holding database) and no process is launched yet. The §2
  probe stream, scrubbed, is the first fixture under `tests/Bench.Tests/Fixtures/scouts/` (kept locally since the
  probe; one re-run, about $0.60, if it is lost).
- **S2 — One cell end to end, then the measuring verbs** *(Opus, by the same decision: the history-free tree and the launch deny-list are
  the measurement's trust boundary; the budget cap and the ledger are money).* `AgentAskOptions.ForwardSubagentText` and
  `MaxBudgetUsd` in `CliArgv.For` for claude, refused by name elsewhere. `ScoutTrees` (§3.2). `CliScoutRunner`: reserve →
  launch → raw stream committed FIRST → read → read-back → cost check → settle / void / abort. `ScoutCampaign`: one cell at
  a time in schedule order, pins read before each claim (a change stops the run), exit 3 when the next reservation does
  not fit. Verbs `tasks` (candidates only behind `cutoffs.json` — a missing or ambiguous id refuses; the pilot's tasks
  excluded; seeded draw; statements leak-checked and frozen), `run`, `resume`, `rerun`, `sweep`, `prune`,
  `alias-check` (§3.14); exit codes as the probes'. `FakeCli` today finds its script by the model id
  (`tests/FakeCli/Program.cs:113`), and every scout cell launches `--model opus` — so two tests would share one script
  home. It gains a test-owned home that does not depend on the model, passed through the runner's allow-listed
  environment in tests only (production argv and environment unchanged), and a per-prompt-hash `stdoutFile`. Implements the §2 gaps, §3.1, §3.2, §3.4,
  §3.10, §3.11, §3.14. Done when `run` settles a two-task replay with one void and one aborted cell, and the crash tests (§5) pass.
- **S3 — The analysis, the read side and the scenario** *(Opus: composes `ProbeReport`, the probes API and tab,
  `LivePoller`).* Pure analysis first: claim-rate pooling, the clustered bootstrap and `Decide()` (§3.8), tested as a
  table before anything renders them.
  Verbs `status`, `score` (with `--handcheck N --seed` drawing the 20 + 20 for a person), `assess` (every `CLAIMS` line
  through codex read-only, §3.7). `ScoutReport` (pure) → `report --json`, `GET /api/bench/scouts/runs[/{id}]`, the
  Scouts tab (`.razor` + `.razor.cs`, bUnit), the contracts guard. The scenario test (§5). `research/module_scouts.md`,
  the `architecture.md` section, and `research/module_tests.md` — created, as a catalogue derived from the CLI's verbs and
  the API's routes: the scouts flows `covered` naming their test, everything else `not covered` with a reason, the run
  command, and what it does not prove. Done when the scenario is green and catalogued.

Code round: the trust boundary (tree, deny-list, child environment), the ledger's arithmetic, read-back completeness,
the claims and matcher rules, DTO allow-lists, the migration. Cost: nothing beyond the fixture. Risk: a large round — the
story boundaries are the reviewer's map.

**E2 — Pilot and sizing.** Branch `feat/scouts-pilot`. Goal: the instrument is corrected on live streams, the
hand-checks pass, and the task set, prompts, schedule and prediction are committed before cell one.

- **S4 — The pilot** *(Fable: each correction changes a validity rule on evidence).* `cutoffs.json` first; two pilot
  tasks, excluded from the later candidate list; every arm once. The access check (canary, sibling path); the read-back
  by hand on one cell per arm; one budget-exhausted cell settles `aborted:spend` unscored; a randomized A/A; the cost
  check within 2 %; `CLAIMS` parsed and assessed on real output, silent and unparseable counts seen; one `alias-check`.
  Every live stream becomes a fixture named by CLI version, and a reader that was wrong goes RED on it first. Record:
  `research/scout-calibration/pilot.md`. Done when every pilot cell is settled, void or aborted with its reason and the
  hand read-back agrees.
- **S5 — Hand-checks, sizing, pre-registration** *(Opus; the sizing simulation is pure code with its own tests, reusing
  S3's bootstrap).* Matcher ≥ 19/20 and assessor ≥ 18/20 into `handcheck.md`;
  the per-arm budget cap fixed (§3.4); the sizing simulation at Δ = 0 and −0.03 → a task count ≤ 30, **or STOP with the
  numbers, and E3 does not start**; candidates, seeded draw, statements written and leak-checked → `tasks.json`,
  `prompts.json`, `schedule.json`; §3.9 into the RESULTS skeleton. **Pre-registration needs an order a single commit
  cannot prove**, so E2 lands as separate evidence commits — (1) `cutoffs.json`; (2) the candidate list and the seed;
  (3) the draw, statements, prompts, schedule, caps and prediction — each sha recorded in RESULTS, with E2's code and
  fixture changes in its one reviewed commit and one code round over the whole epic.

Code round: the RED fixtures behind each correction, the pre-registered files, the stop-or-go. Cost: about 25 cells — the
last point where a finding is cheap.

**E3 — The main run and the result** *(conditional on S5's go).* Branch `feat/scouts-main-run`. Goal: the frozen
schedule runs to the end and RESULTS answers §1 per workload against the prediction.

- **S6 — The run** *(Opus: operating a frozen design).* `run` / `resume` across days; a pin change stops it; void cells
  re-run as generations; the Explore replication; `assess` over every claim; `score`; ledger total = Σ `total_cost_usd`;
  `cells.jsonl` scrubbed. Done when nothing is pending or claimed and every void and aborted cell carries its reason.
- **S7 — The write-up and the hand-over** *(Fable: the conclusion becomes a rule about every scout).*
  `RESULTS_scout_models.md` as §3.12 lists, its `research/README.md` row, `module_scouts.md` with the run, `todo/README.md`,
  the alias check registered as a weekly scheduled task (§3.14), the plan promoted with its deviations, `prune`. The
  verdict goes to `dew_flow_conventions · todo/PLAN_scout_subagents_on_sonnet.md` as a PR there — the open tail outside
  this repository.

Code round: no new code expected — every RESULTS number checked against `cells.jsonl` and the prediction's sha; the
hygiene test. Cost: about 740 cells, about two days of serial wall clock.

## 5. Test plan

E1 tests run on synthetic fixtures and the one probe stream — no network, no paid model. E2 replaces each synthetic
fixture with a live capture, and a reader that was wrong goes RED on the capture before it is fixed.

**S1 — facts and store**

- `ClaudeSubagents` reads the probe fixture: two children, the right models, durations, deduplicated tokens; a stream
  with a missing `result` reads as incomplete; a child with no `resolvedModel` is a void reason, not a crash.
- The leak check refuses a statement carrying a ground-truth stem.
- Read-back voids: a wrong model, a prompt-hash mismatch, a wrong call count, a web call in a child, a non-Agent call by
  the main session.
- The path audit, on the three 2026-10-12 probe streams as fixtures: a denied outside Read is recorded, not voided; an
  outside Read or Glob that returned content STOPS the campaign; a relative `../`, an omitted Grep root, an absolute glob
  and a junction are resolved before the check; a truncated tool result is never safe.
- The claims parser: a well-formed line, a line without a path, a `path:line` with no statement, a missing block
  (silent), more than 10 lines (the extra lines counted, not assessed); the pooled rate against a hand-computed table.
- The matcher: `\` and `/`, `./`, `file:42`, backticks, a unique basename matches, an ambiguous basename does not.
- The cost calculator against a hand-computed cell; the 2 % check stops on a planted mismatch.
- The spend ledger refuses a reservation that does not fit and survives a restart.
- The scrubber removes a planted home path, an e-mail and a deny-list entry; the hygiene test fails on a tracked file
  under the trees' directory.
- `Decide()` as a table: pass, fail on recall, fail on wrong claims, interval crossing, one workload failing, a workload
  whose wrong-claim result is *not measured*.
- The store on Testcontainers; the migration on an empty database and on one that already holds gate and probe tables.

**S2 — one cell and the measuring verbs**

- `CliArgv.For` emits `--forward-subagent-text` and `--max-budget-usd` for claude and refuses them by name for codex and
  agy.
- The tree builder leaves one commit and no other history; the tree hash is stable; a changed tree stops the cell.
- The cut-off guard: an id with a recorded cut-off admits candidates after it; an id with none stops selection; a pilot
  task never enters the candidate list.
- `FakeCli` replays per prompt hash; resume after a killed cell; the sweep settles an orphaned claim; a pin change stops
  the run; exit 3 when the next reservation does not fit.
- `alias-check` exits non-zero on a replayed stream whose `resolvedModel` differs from the recorded id.
- **Crash tests at the money boundaries**, by fault injection through a checkpoint port the runner calls: a crash
  before the *launching* mark releases the reservation; a crash after the mark with no output is charged as unknown;
  a crash after paid output with the stream on disk settles once from it; a
  crash after paid output with no `result` line becomes an unknown-cost charge; a crash after the artefact commit but
  before settlement settles once, never twice. Plus ONE out-of-process test: a real `bench scouts run` process killed
  mid-cell, then `resume` — the ledger identity holds. The existing probe test that plants a dead owner's row stays
  useful, but it is not evidence of interruption.
- `FakeCli` isolation: two setups in parallel, both launching `--model opus` with the same prompt, each receive their
  own stream and their own call count.

**S3 — read side and scenario**

- `ScoutReport` from a hand-built run; CLI bytes = API bytes; the tab's states (empty, running, finished, refused);
  the poller stops when no cell is open.
- **Scenario (offline, the real flow).** `bench scouts run` against a two-task fixture set, with the real store and
  `FakeCli` replaying recorded streams per arm — one cell goes void (wrong model), one is interrupted and resumed by
  `bench scouts resume`. The run is then read three ways: `bench scouts report --json` and
  `GET /api/bench/scouts/runs/{id}` must agree byte for byte; the Scouts tab, rendered with bUnit over the API client
  fed those bytes, must show the same verdict, per-arm numbers and void count. Catalogued in `research/module_tests.md`
  with its run command, as the scenario-tests rule requires — that file does not exist yet (checked 2026-10-12), so S3
  creates it.

**Break-it** (each goes red with the real symptom, then back): the matcher accepting any basename turns the hand-check
fixture red; the read-back ignoring `resolvedModel` turns the wrong-model fixture red; the cost check widened to 20 %
turns the planted-mismatch case red.

All suites run from the test executable, never `dotnet test`.

## 6. What this does not settle

- Scouts that are not file finders: web research, documentation lookup — the rule will say these are extrapolated.
- Repositories outside the family, private repositories, languages not in the sample.
- Whether the caller's final plan or code gets better or worse — only what the scouts hand it.
- A later model behind the `sonnet` or `opus` alias: the result is about the ids recorded on the run.
- Prices after the run date.
- Opus teams larger than three, and Sonnet counts other than ⌈1.5·N⌉.

## 7. Noted while planning, not in scope

Named here so they are not lost; each needs its own decision.

- `CliSubjectRuntime.cs:59-61` says a per-call cost ceiling cannot be enforced and that "the harness's next-leg gate"
  enforces it. The CLI has `--max-budget-usd`, and no such gate exists in `LegDrain`.
- The CLI-subject worktree exposes the later history through its `.git` redirect (the §3.2 gap) for every
  investigate-arm leg, not only this one.
- `LegPhase.CostUsd` is declared and never filled; cost lives only inside a text field.
- `todo/README.md`'s *Currently open* table lists one plan while the folder holds ten.

## 8. Definition of Done

- [ ] The launcher has the two new options, tested, refused by name where a CLI has no flag.
- [ ] `bench scouts` runs, resumes, sweeps, checks the alias and reports; every verb has tests; every break-it went red
      and back.
- [ ] The Scouts section answers the CLI report and the API byte for byte, and the console tab shows the same verdict,
      per-arm numbers and void count; the scenario test proves it and is catalogued in `research/module_tests.md`.
- [ ] `cutoffs.json` — per resolved id, the cut-off, its source and the date read — was committed before the candidate
      list.
- [ ] `pilot.md` holds the pilot: the hand read-back, the access check, the budget-exhausted cell, the A/A, the cost
      check, and each correction with its RED fixture.
- [ ] The matcher and the assessor passed their hand-checks before any number appeared.
- [ ] The task set, prompts, schedule, per-arm budget caps, sizing and prediction were committed before cell one — or
      the sizing said STOP, the numbers are recorded, and E3 did not start.
- [ ] Every `CLAIMS` line was assessed; silent and unparseable counts are reported per arm; a workload with more than
      10 % silent cells is reported *not measured*.
- [ ] `research/RESULTS_scout_models.md` and `research/scout-calibration/*` hold everything §3.12 lists: per workload
      and per task, recall, wrong claims, time and cost against the prediction, the subject shas, the harness path,
      the date and every pin, void and aborted cells, and what the run does not settle.
- [ ] The weekly alias check is registered and has run once green.
- [ ] Nothing committed carries a private name, a home path or a key, and no tree file is tracked.
- [ ] `module_scouts.md`, `architecture.md`, `research/README.md` and `todo/README.md` are updated; this plan is
      promoted with its deviations.
- [ ] Open tail, outside this repository: the verdict handed to
      `dew_flow_conventions · todo/PLAN_scout_subagents_on_sonnet.md` as a PR there.
