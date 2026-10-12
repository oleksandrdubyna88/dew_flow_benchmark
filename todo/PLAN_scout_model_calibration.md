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
| A campaign with `run · resume · rerun · status · sweep · report · prune`, exit codes 0/3/4/5, an owner-checked sweep before any claim, generations appended on re-run | `hosts/Cli/ProbesCommand.cs:9-40`, `src/Bench.Application/Probes/ProbeCampaign.cs` |
| Cells that compose `Claimable` and `ProductPin`, the gate store's guarded claim, `ArtifactCommit`, `PublicationGuard` on public export | [architecture.md § The capability probes](../research/architecture.md) |
| A blinded assessor with fresh shuffled ids; codex runs it read-only, claude only with `MaxTurns = 1` (cannot open files) | `src/Bench.Application/Gate/FindingAssessor.cs:81-94`, `src/Bench.Domain/Gate/BlindExport.cs:133-153` |
| Pre-trust of a working tree, only under the checkout root | `src/Bench.Infrastructure/Models/WorkspaceTrust.cs:52-85` |
| A task base at the parent of a fix commit, merge commits refused | `hosts/Cli/HarvestCommand.cs:13-25` |
| **Gap:** the CLI-subject worktree is a git worktree whose `.git` redirects to a full mirror that holds the later fix — only the agent's behaviour keeps it out of the future | `src/Bench.Infrastructure/Git/ScratchWorktree.cs:21-33`, [architecture.md:411-416](../research/architecture.md) |
| **Gap:** no `--forward-subagent-text`, no `--max-budget-usd` option on the launcher | `src/Bench.Application/CliAgent.cs:77-123` |
| **Gap:** `ClaudeStream` does not group by `parent_tool_use_id`, so it cannot tell one subagent from another | `src/Bench.Domain/Probes/ClaudeStream.cs` |

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
  P, merged after the later of the two models' published knowledge cut-offs, and are not a release, a version bump, a
  dependency bump or docs-only. Per repository, a seeded draw picks from the candidates; the seed and the candidate
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

**Access check (pilot, §4 E2):** from the real scout environment, try to read a canary planted next to the tree and a
known file from the later fix through a sibling path; both must fail. A cell whose read-back shows any web, shell or
git call is **void** and re-run.

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
- Every scout ends with a `KEY FILES` block of at most 10 repository-relative paths.

### 3.4 One cell

One main `claude -p --model opus` session per cell, working directory = the task's tree, flags: `--output-format
stream-json --verbose --forward-subagent-text --max-budget-usd <reservation> --disallowedTools WebFetch WebSearch Bash
PowerShell Write Edit NotebookEdit --no-session-persistence`. The driver prompt says: *in one message launch exactly
these N Agent calls, these prompts verbatim, then reply DONE.*

Pinned, and recorded on every cell: CLI version, plugin cache hash, the resolved ids behind `opus` and `sonnet`, the
operator's settings hash (effort levels included — they are the real condition the rule will run under), the tree
hash. **A change of any pin mid-run stops the run.** Cells run one at a time, in a seeded schedule that interleaves
the arms inside each task, so drift in time of day and server load falls on every arm.

### 3.5 Read-back — every cell, not a sample

Exactly N Agent calls; each call's `input.model` and prompt hash equal the frozen ones; each child's `resolvedModel` and
`message.model` belong to the arm's model; no forbidden tool in any child; `subagent_stats.completed = N`. Any miss
makes the cell void with its reason. The main session making any call other than Agent voids the cell too.

### 3.6 Metrics

| Metric | Definition | Role |
|---|---|---|
| **Recall** | \|G ∩ ∪ KEY FILES of the team\| ÷ \|G\| | **decides** (D5) |
| **Wrong claims per 10** | blind assessor (§3.7) over up to 10 seeded claims per scout | **decides** (D5) |
| Team time | first Agent `tool_use` → last `task_notification`, from event timestamps; cross-checked against the slowest `totalDurationMs` | reported (D6) |
| Scout time | each child's `totalDurationMs` | reported |
| Session time | `result.duration_ms` | reported |
| Tokens per scout | input, output, cache read, cache write — summed over the child's messages, deduplicated by message id | reported |
| **Cost per scout and per team** | tokens × the vendor price table read on the run date (URL and date recorded) | reported (D6) |
| Cost per right file | team cost ÷ files of G found | reported |
| Caller load | size of the union — the files the Opus caller must then open | reported |

**Cost check:** per cell, the computed main + scouts cost must equal the CLI's `total_cost_usd` within 2 %; a larger gap
stops the run — the price table or the token sum is wrong, and every cost after it would be too.

### 3.7 Hand-checks before any number is published

- **Recall matcher**: 20 seeded (output, G file) pairs, half found and half not, with basename-only and `path:line`
  forms planted. At least 19 of 20 must agree with a person's reading; otherwise fix the matcher and draw 20 new pairs.
- **Assessor**: codex read-only through the existing `FindingAssessor` options (claude is capped at one turn there and
  cannot open a file); 20 verdicts re-checked by opening the file. At least 18 of 20 must agree.
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

### 3.11 Growth budget (planning-docs)

| Surface | Projected size | Retired by |
|---|---|---|
| History-free trees | ≤ 30 tasks × ~40 MB ≈ 1.2 GB | `bench scouts prune --run` after RESULTS is merged — only directories the run created, path checked |
| Raw streams | cells ≈ 30 × 3 × 7 arms ≈ 630 × ~1 MB ≈ 0.6 GB, local only | kept until the conventions rule ships, then gzipped; never committed |
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
| `research/scout-calibration/handcheck.md` | the 20 + 20 hand-checked items and the person's reading of each |
| `research/module_scouts.md` | the section as built: the tuple, the verbs, the store, the report |

### 3.13 Public-repository hygiene

The repository is public. Committed records hold repository-relative paths, metrics and scrubbed scout texts only. The
scrubber removes drive and home paths, e-mail addresses, and every entry of a deny-list that is read from a local file
and never committed; a test fails when a committed record still matches.

## 4. Build order — three epics, the gate once per epic, the feature gate at the end

**E1 — The scouts context (code).**

1. Widen the launcher: `AgentAskOptions.ForwardSubagentText` and `MaxBudgetUsd`, mapped in `CliArgv.For` for claude,
   refused by name for the other CLIs (`CliAgentRuntime.cs:134`).
2. `ClaudeSubagents` — a pure reader beside `ClaudeStream`: per Agent call its input, child events by
   `parent_tool_use_id`, `resolvedModel`, durations, deduplicated usage, final text; plus the `result` envelope.
   Fixture: the probe's stream, scrubbed, under `tests/Bench.Tests/Fixtures/scouts/`.
3. `ScoutTask`, `ScoutArm`, `ScoutCell`, `ScoutRun` in `Bench.Domain.Scouts`, composing `Claimable` and `ProductPin`
   as `ProbeCell` does; the tree builder (§3.2); the leak check; the read-back (§3.5).
4. Store tables + migration, joined to the publication walk; the spend ledger.
5. `bench scouts tasks | run | resume | rerun | status | sweep | score | handcheck | assess | report | prune`, exit codes
   as the probes'.
6. The recall matcher, the cost calculator, the bootstrap and `Decide()`, all pure.
7. `ScoutReport` (pure), `bench scouts report --json`, `GET /api/bench/scouts/runs[/{id}]`, the console's **Scouts**
   tab with `LivePoller` — split `.razor` / `.razor.cs`, bUnit tests, as the Probes tab.
8. `research/module_scouts.md` + a section in `architecture.md`.

**E2 — Pilot and sizing (measurement).** Two tasks outside the set, every arm once: read-back by hand on one cell per
arm, the access check, one deliberately budget-exhausted cell, a small randomized A/A (two copies of arm A), the cost
check against the CLI, the matcher hand-check. Then the sizing simulation (§3.8), the frozen task set, prompts and
schedule, and the prediction commit.

**E3 — The main run and the result.** The schedule, the Explore replication, the assessor and its hand-check,
`research/RESULTS_scout_models.md` (observed against predicted, per workload: recall, wrong claims, time, cost; void and
aborted cells counted; what it does not settle) and its `research/README.md` row. Then the verdict goes to the
conventions plan.

## 5. Test plan

Fixtures only, no network, no paid model:

- `ClaudeSubagents` reads the probe fixture: two children, the right models, durations, deduplicated tokens; a stream
  with a missing `result` reads as incomplete; a child with no `resolvedModel` is a void reason, not a crash.
- `CliArgv.For` emits `--forward-subagent-text` and `--max-budget-usd` for claude and refuses them by name for codex and
  agy.
- The tree builder leaves no `.git` history beyond one commit; the tree hash is stable.
- The leak check refuses a statement carrying a ground-truth stem.
- Read-back voids: a wrong model, a prompt-hash mismatch, a wrong call count, a web call in a child.
- The cost calculator against a hand-computed cell; the 2 % check stops on a planted mismatch.
- The spend ledger refuses a reservation that does not fit and survives a restart.
- Resume after a killed cell; the sweep settles an orphaned claim.
- The matcher: `\` and `/`, `./`, `file:42`, backticks, a unique basename matches, an ambiguous basename does not.
- The scrubber removes a planted home path, an e-mail and a deny-list entry.
- `Decide()` as a table: pass, fail on recall, fail on wrong claims, interval crossing, one workload failing.
- **Break-it:** make the matcher accept any basename and watch the hand-check fixture go red; make the read-back ignore
  `resolvedModel` and watch the wrong-model fixture go red.

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
- [ ] `bench scouts` runs, resumes, sweeps and reports; every verb has tests; the break-it checks went red and back.
- [ ] The Scouts section answers the CLI report, the API and the console tab from one `ScoutReport`, byte for byte.
- [ ] The pilot's read-back, access check, budget-exhausted cell, A/A and cost check are recorded.
- [ ] The task set, prompts, schedule, sizing and prediction were committed before cell one.
- [ ] The matcher and the assessor passed their hand-checks before any number appeared.
- [ ] `research/RESULTS_scout_models.md` and `research/scout-calibration/*` hold everything §3.12 lists: per workload
      and per task, recall, wrong claims, time and cost against the prediction, the subject shas, the harness path,
      the date and every pin, void and aborted cells, and what the run does not settle.
- [ ] Nothing committed carries a private name, a home path or a key.
- [ ] `module_scouts.md`, `architecture.md`, `research/README.md` and `todo/README.md` are updated; this plan is
      promoted with its deviations.
- [ ] The verdict was handed to `dew_flow_conventions · todo/PLAN_scout_subagents_on_sonnet.md`.
