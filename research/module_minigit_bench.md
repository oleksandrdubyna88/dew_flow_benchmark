# minigit stack bench — the measurement instruments (epic E1)

> What exists **today**, in `spikes/minigit-bench/`. Epic E1 of `todo/PLAN_minigit_stack_bench.md` is built: three
> instruments that give correct numbers before any model touches the task. No session has been run through them yet,
> and nothing here is a result. What is still missing is in [What it does not do yet](#what-it-does-not-do-yet).
>
> **This repository is public and the sessions it will score have network.** This document, like every file committed
> in clear, names the held-out suite only by shape: its counts (N1 = 29 cases for v1, N2 = 29 for v2, M = 14 mutants)
> and the ids-only calibration result. It never names a case, a reference implementation or a mutant.

## Purpose

The bench compares the same task (build `minigit` from a specification) on two stacks, with and without the family's
process. The original benchmark it descends from failed on correctness, cost and time (a cache billed twice, a test
that passes a wrong parent, a swallowed build error, a test the agent could edit, a crash read as "no tests"). E1
builds the three instruments and proves each on fixtures and on one real capture:

| Instrument | Question it answers | Entry |
|---|---|---|
| **Scorer** with its **held-out suite** | Did the cell's `minigit` do what the SPEC says, measured on a pristine copy by tests the agent never saw, one defect counted once | `score-session.mjs`, `heldout-tool.mjs`, `adjudicate.mjs` |
| **Counters** | What the session cost at published prices, cross-checked three ways, never double-billing a cache and never priced from a fitted rate | libraries (`lib/`), no command |
| **Time partition** and the **coai ledger reader** | Where the wall went, in categories that sum to the wall; what the review models consumed, in tokens | libraries (`lib/`), no command (the rounds reader opens `coai.db`) |

Everything is plain ESM `.mjs` on Node 26 with no dependency and no `package.json`. A value that could not be measured is
`{ known: false, why }` and renders `unknown`, never `0`. CI does not run the spike; the required environment is Linux with
`bwrap` and user namespaces (the series machine, WSL2 Ubuntu).

## How the pieces connect

```mermaid
flowchart LR
  subgraph INPUTS["Pinned inputs"]
    direction TB
    FORK["fork oleksandrdubyna88/minigit-bench<br/>branch inputs, pinned sha df00cac..."]
    HASHES["inputs/hashes.txt<br/>sha256 per pinned file"]
    FETCH["fetchInputs<br/>clone once, verify every file"]
    CACHE["~/.cache/minigit-bench/inputs/sha"]
    FORK --> FETCH
    HASHES --> FETCH
    FETCH --> CACHE
  end

  subgraph SCORER["Instrument 1 - scorer: score-session.mjs"]
    direction TB
    SRC["a finished session's tree"]
    COPY["sweep, copy, preflight,<br/>input hashes"]
    subgraph BOX["Scoring box: bwrap, no network, own pid namespace"]
      direction TB
      BUILD["build from clean, 600 s"]
      VIS["fixed visible scripts<br/>harness copy, read-only"]
      HELD["held-out step"]
      UP["upstream originals<br/>reference only"]
      BUILD --> VIS --> HELD --> UP
    end
    VERDICT["verdict: accepted is true,<br/>false or unknown"]
    SCOREJSON["score/score.json"]
    SRC --> COPY --> BOX --> VERDICT --> SCOREJSON
  end

  subgraph SUITE["Instrument 2 - held-out suite: heldout-tool.mjs"]
    direction TB
    ENC["heldout/suite.enc<br/>AES-256-GCM, sealed at rest"]
    KEY["key file, mode 600<br/>outside the repository"]
    CLEAR["clear copy in a 0700 dir under home,<br/>removed after the run"]
    RUNNER["runner on the host<br/>drives the binary through the box"]
    CAL["calibrate: references a, b, b2 at 100 %,<br/>14 of 14 mutants fail exactly their cases"]
    CALJSON["heldout/calibration.json<br/>ids only"]
    ENC --> CLEAR
    KEY --> CLEAR
    CLEAR --> RUNNER
    RUNNER --> CAL --> CALJSON
  end

  subgraph COUNTERS["Instrument 3 - counters, time partition, coai ledger"]
    direction TB
    E["E: result envelope<br/>stream.ndjson"]
    O["O: OTel api_request<br/>receiver or sink"]
    T["T: transcripts<br/>main and subagents"]
    CHECKS["crosscheck T1-T4<br/>crosscheck-policy.json"]
    COST["sessionCost<br/>headline USD with its basis, E total beside it"]
    ALIAS["aliasRecord / checkAliases"]
    SPANS["spansFrom: OTel, audit hook,<br/>hook pairs, coai rounds"]
    PART["partition of the wall<br/>work first, then waits"]
    ROUNDS["coai.db rounds<br/>roundsSpans"]
    LEDGER["coai usage.jsonl<br/>readLedger, ledgerSummary"]
    E --> CHECKS
    O --> CHECKS
    T --> CHECKS
    E --> COST
    O --> COST
    T --> COST
    E --> ALIAS
    O --> SPANS
    ROUNDS --> SPANS
    SPANS --> PART
  end

  CACHE -->|"visible and upstream scripts"| BOX
  ENC -->|"hash checked in hashes.txt"| HASHES
  HELD -->|"deps.heldout"| RUNNER
  RUNNER -->|"result per case"| HELD
  CAL -.->|"same pipeline scores each subject"| SCORER
  CAPTURE["capture-session.mjs<br/>one paid session, sanitised fixture"] -.->|"decided the decoder's names"| COUNTERS
```

Solid arrows are data the code moves today. Only the `coai.db` rounds feed the spans; `usage.jsonl` is summarised in tokens and never becomes a span. Dotted arrows are use: calibration scores its subjects through the scorer, and
the one real capture is the evidence the counters' decoder and policy were built on.

## The three instruments

### 1. The scorer — `score-session.mjs`

One command scores one finished session the same way for both arms (`lib/score-pipeline.mjs`). Its steps, in order, each
recorded in `score/score.json`:

1. **sweep** — removes this session's scorer-owned leftovers older than the wall limit (ownership markers live in
   `score/.owned/`, a directory the box never binds); anything unmarked is only reported;
2. **copy** — the session tree to `score/copy` (build output and dependency directories left out), plus per-scoring copies
   of the seeded offline NuGet and npm caches and a scratch HOME and TMPDIR;
3. **preflight** — the box can start;
4. **inputs** — the session's specs against the pinned hashes: a mismatch is recorded as `inputsTampered`, a missing file as
   `inputsAbsent`;
5. **build** from clean, 600 s;
6. **visible** — the harness's fixed `test-v1.sh` (and `test-v2.sh` for a v2 phase), each on its own fresh copy of the built
   tree, with a per-invocation timeout shim (10 s) in front of `minigit`;
7. **heldout** — the sealed suite's runner against the built binary;
8. **upstream** — the original upstream scripts, reported as reference only and never part of the verdict;
9. **verdict** — `accepted` is true only when the build is clean, every visible script is 100 %, the held-out primary is
   100 % and (process arm) the work was delivered. A definite failure is `{ known: true, value: false }`; a missing
   ingredient is `{ known: false, missing }`, never `false`. `delivered` has no source yet, so a process session reads
   `unknown` (see below).

A script counts as 100 % only when its summary says so **and** the script itself ended cleanly: exit 0, no signal, no
timeout. A summary that cannot be read scores every test as failed, never "0 of 0". Which of the fork's scripts are used and
what was changed in them is in `spikes/minigit-bench/inputs/UPSTREAM.md`.

### 2. The held-out suite — `heldout-tool.mjs`, `adjudicate.mjs`

The suite is **sealed at rest and calibrated before any cell**.

- **Sealed.** `heldout/suite.enc` is one deterministic archive (suite, references and mutants together, nothing in clear)
  encrypted with AES-256-GCM. The key is a mode-600 file (`~/.config/minigit-bench/heldout.key` by default), read by the
  scorer itself and **never** from or into an environment variable; a key readable by group or others, or a link, is
  refused. `suite.enc.sha256` and `heldout/manifest.json` (counts, plaintext hash) sit beside it, and the archive's sha256 is
  also a line of `inputs/hashes.txt`.
- **Clear only in a scoring temp directory.** `withClearSuite` decrypts into a fresh 0700 directory under the operator's
  home (never the temp directory, never inside a git worktree), removes it in a `finally`, and `sweep` removes any a crash
  left behind. The suite's case repositories are built by the cell's own `init`, `add` and `commit` under a directory the box
  binds, and are removed once the result is written.
- **Run.** The runner is trusted code on the host (`python3 -I`); it reaches the cell's binary only through the scoring
  box argv it is handed. A case that crashes, times out or finds no command fails; a case whose setup needs a core command
  that failed its own case is `blocked` (reported apart, counted as not passed, never as a second failure).
  `heldoutScore` gives two pre-registered measures: `percent` (cases passed over N, blocked counted as not passed) and
  `defects` (distinct failed SPEC clauses, so one broken command counts once). A result that cannot be read is
  `{ known: false }`.
- **Calibrated.** Against the sealed suite, scored through the same pipeline as a session: three independent reference
  implementations (a, b, b2) score 100 % on the visible and held-out cases in both phases, and each of the M = 14 mutants
  fails **exactly** its pre-registered cases in both phases (14 of 14 exact). The pre-registration is the registry inside
  the archive; an observed set is never copied into it. The ids-only result is `heldout/calibration.json`. Two references
  can share one misreading, and mutants prove sensitivity to the defects chosen, not completeness; the series report says so.
- **Leak check.** `leakcheck` reads literal and six-word-shingle needles from inside the archive and scans every file git
  would commit, the commit messages and every patch of the branch range. A hit names the file and the needle's number, never
  the needle. A paraphrase sharing no six consecutive words is undetectable by design.
- **Adjudication** (`adjudicate.mjs`, per series directory outside git because the packets hold case text). Step 1 sees
  only the SPEC sentence, the case and its captured output, with stack and arm replaced by a random id and runtime names,
  paths, frames and exception names masked (the masking rules are printed with the packets); that step alone can class a
  case as SPEC ambiguity. `reveal` refuses until every packet has a step-1 verdict. Step 2 sees the code and separates
  product from harness defects. `revised` drops every case classed ambiguous in any cell from all cells and writes the
  revised score **beside** the primary one, which is never rewritten.

### 3. The counters, the time partition and the coai ledger reader

Pure over records, except at four I/O edges: `lib/otel-receiver.mjs` (the live receiver), `capture/*` (the capture run, its file
sink and the reading and writing of capture directories), `lib/coai-usage.mjs` (`roundsSpans` opens a copy of `coai.db` read-only through
`node:sqlite`) and the scorer's own files. There is no command that runs the counters on a session yet (a runner is E3).

**Three views of one session** (all from the same CLI, so cross-checks of the reading, not independent billing):

| View | Source | What it supplies |
|---|---|---|
| **E** `envelopeView` | the stream's result envelope | `total_cost_usd`; per-model tokens and `costUSD` from `modelUsage` (it includes subagents; `usage` is main-loop only and is never used for totals) |
| **O** `otelView` | OTel `claude_code.api_request` records, decoded from OTLP/HTTP JSON | per request: model, four token kinds, `cost_usd`, `duration_ms`, `query_source` (mapped to a role by `roleOf`), `request_id`, `agent_id`; plus `tool_result` records |
| **T** `transcriptView` | `projects/<session>.jsonl` and `subagents/agent-*.jsonl`, grouped by `message.id` | final output tokens and the cache-write 5m/1h split, on main and child lines |

The stream (`streamView`) is deduplicated by `message.id` and prices nothing but a labelled estimate: its output tokens are
placeholders.

**`sessionCost`** returns the headline USD with its basis stated, the CLI's own total beside it, per-model tokens and flags.
The headline is a sum over models of the four token kinds priced separately at the dated published table
(`inputs/prices-2026-10-29.json`, which carries a Fable 5.1 row), tokens from `modelUsage` and the 5m/1h split from T; a
model with no message-level split is an interval, not a guess. A killed session without an envelope is priced from O and
flagged `no envelope`; with neither it is `unknown`. A model the table lacks is `unknown`: there is no prefix match and no
default row. `cliRates` entries name the CLI version they were observed on and apply only to it.
`no-fitted-rates.test.mjs` scans for every caller of the earlier spike's fitted pricing (`costFromMessages`,
`summarize`, `parallelism`, `childCosts`, `CostMeter`) and fails if the new spike calls one.

**`crosscheck` T1–T4**, with the verdict each breach carries read from `inputs/crosscheck-policy.json`:

| Check | Compares | Limit | On breach (policy today) |
|---|---|---|---|
| T1 | E against itself: Σ `modelUsage[*].costUSD` against `total_cost_usd` | $0.01 | `stop` |
| T2 | O over all sources against E, per model and token kind; cost | max(0.5 %, 100 tokens); cost 1 % | `otel-incomplete`, the cell is kept |
| T3 | T re-priced as the CLI bills against O's `cost_usd`, joined by `request_id` | 2 % | `investigate` until E3 arms it |
| T4 | T's input and cache tokens against the O records T names | 0.1 % | `investigate` |

T is compared with O and never with E, because E's `modelUsage` carries no `query_source` and cannot be cut down to what T
sees. A check that cannot be made is `unchecked` with its reason, never a pass: an O record whose role is main, subagent or
unknown and that no transcript names (a lost subagent file), two T messages sharing one `request_id`, an O record missing a
token field or `cost_usd`, or no envelope. `loadPolicy` refuses a policy whose T3 fallback would leave nothing to stop the
series.

**`aliasRecord` / `checkAliases`** (decision D12). The alias → exact model id record of one session, checked against the
series baseline: an alias that now resolves to a different id, or an unexpected internal model, is a `stop`; a first use only
extends the baseline; a `model_refusal_fallback` is a `flag`.

**The OTel receiver** (`startReceiver`) listens on `127.0.0.1` only, JSON only, bounded; it keeps every body raw, the decoded
records, and a running `cost_usd` sum (final per request, children included) for a cost watchdog.

**The time partition** (`timeline.mjs`, `spans.mjs`, `classify.mjs`, `categories.mjs`). `partition(wall, spans)` takes every
elementary interval of the wall and gives it the highest-priority category active in it **across all agents**, so a wait counts
only where no agent is working: one child's `sleep 300` cannot erase another child's build. Priority: work
`build_test > model_api > other_tool > hooks`, then waits `coai_gate > consultant > ci_wait > coderabbit_wait > sleep300`,
then `unattributed`. The categories plus `unattributed` sum to the wall to the millisecond; `active` is the sum of the work
categories, never wall minus waits. Nothing is a sum of spans: the inclusive view is a union, agent-seconds are labelled work
volume, and `overlapMatrix` is separate. `classifyTool` is pure string work on the command (no process is run); it splits at
the top level of the quoting and the first matching rule decides. Spans come from OTel (`model_api`, `[ts − duration, ts]`
because a record's time marks the end of the request), the audit hook's tool records, the hook start/response pairs placed by
the runner's arrival stamp, and coai `rounds`. `crossCheckTools` compares tool spans with OTel `tool_result` durations within
max(1 s, 2 %). `mapClock` places the launcher's monotonic clock on the epoch and flags a skew over 1 s.

**The coai ledger reader** (`coai-usage.mjs`). `readLedger` and `ledgerSummary` read `usage.jsonl`: per provider, model,
kind, role and stage, in tokens, with `cached` inside `in` (uncached = in − cached, asserted on every row, so the cache is
never added twice). A subscription CLI reports no money: a null cost is `unknown (subscription)`, mixed ledgers give the known
sum plus "N unknown", and no field of the summary can be added to Claude's dollars. A `usage not captured` row makes its
tokens a lower bound. `roundsSpans` reads the gate rounds from `coai.db` through `node:sqlite`. Its fixtures are
synthetic: no real ledger has been captured yet.

## Entry points

Run from the repository root. `write` goes to stdout; the exit code is the contract. No command takes the held-out key on
argv or in the environment.

| Command | What it does | Exit codes |
|---|---|---|
| `node spikes/minigit-bench/run-tests.mjs [glob…]` | the one way to run the spike's tests: lowered priority, `node --test` with at most `min(8, CPUs − 1)` workers; default glob `test/**/*.test.mjs`. `BENCH_REQUIRE_SANDBOX=1` turns every skip that needs `bwrap`, `python3` or the key into a failure | the child's own exit code (0 all passed); `128 + signal` if it was killed; 1 if it could not start |
| `node spikes/minigit-bench/score-session.mjs --session <dir> --source <dir> --arm process\|bare --stack csharp\|node --phase v1\|v2 --toolchain <dir>… [--nuget-cache <dir>] [--npm-cache <dir>] [--inputs-repo <url\|path>]` | scores one finished session and writes `<session>/score/score.json` | 0 scored (whatever the score) · 3 environment, resumable (no `bwrap`, a toolchain or cache absent, the box cannot start, another scoring in progress, inputs unreachable or not matching `hashes.txt`) · 4 usage or missing `--source` · 5 the scorer itself failed |
| `node spikes/minigit-bench/heldout-tool.mjs keygen [--key-file P]` | writes a new 32-byte key at mode 600; refuses to overwrite | 0 · 1 refused · 4 · 5 |
| `… heldout-tool.mjs seal --src <clear dir> [--key-file P] [--out-dir D] [--hashes F]` | validates the clear suite, seals it, writes `suite.enc`, its hash, `manifest.json` and the `hashes.txt` line | 0 · 1 refused · 3 · 4 · 5 |
| `… heldout-tool.mjs calibrate [--src <clear dir>] [--key-file P] [--out-dir D] [--work-root D] [--inputs-repo U]` | `--src` is the authoring mode (nothing written); without it, decrypts the sealed archive, scores references and mutants through the pipeline, and on success writes `calibration.json` | 0 calibrated · 1 a mismatch · 3 · 4 · 5 |
| `… heldout-tool.mjs leakcheck [--key-file P] [--out-dir D] [--repo D] [--range A..B] [--work-root D]` | scans the repository and a commit range for the suite's needles | 0 clean · 1 a leak · 3 · 4 · 5 |
| `… heldout-tool.mjs counts [--key-file P] [--out-dir D] [--work-root D]` | prints the manifest's counts and checks them against the archive | 0 the archive agrees · 1 stale manifest · 3 · 4 · 5 |
| `… heldout-tool.mjs sweep [--work-root D] [--max-age-hours N]` | removes clear leftovers older than N hours (default 24) | 0 · 1 a removal failed · 4 · 5 |
| `node spikes/minigit-bench/adjudicate.mjs packets --series <dir>` | writes the masked blind packets; prints the masking rules | 0 · 1 refused (packets already exist) · 4 · 5 |
| `… adjudicate.mjs classify --series <dir> --blind <id> --verdict spec-ambiguity\|not-ambiguous --reason <text>` | records a step-1 verdict | 0 · 1 refused (unknown id) · 4 · 5 |
| `… adjudicate.mjs reveal --series <dir>` | after every packet has a verdict, maps blind ids to case, cell, arm, stack and code path | 0 · 1 refused (a verdict is missing) · 4 · 5 |
| `… adjudicate.mjs class --series <dir> --blind <id> --class product\|harness --reason <text>` | records a step-2 class | 0 · 1 refused · 4 · 5 |
| `… adjudicate.mjs revised --series <dir>` | the revised score beside the primary | 0 · 1 refused · 4 · 5 |
| `node spikes/minigit-bench/capture/capture-session.mjs run --out <dir outside every git tree> [--dry-run]` | **spends money** (≤ $1.50): one tiny session on an unrelated task with OTel pointed at a raw file sink. The subscription token (from `claude setup-token`) is read ONLY from `~/.config/minigit-bench/claude-oauth-token` (a regular file of mode 600 in a directory of mode 700); an environment variable has no mode to check, so `CLAUDE_CODE_OAUTH_TOKEN` in the shell is ignored. Prints a start line (the out dir and its `capture.log`) before the wait and a final `capture status:` line | 0 done · 3 environment (no token file, a wrong mode, `claude` missing, bad out dir) · 4 usage · 5 the capture failed |
| `… capture-session.mjs sanitise --from <dir> --to <fixture dir>` | copies the capture through the sanitiser into a sibling staging directory and renames it into place after every write; a leak finding or a failed write leaves nothing in the fixture directory | same |
| `… capture-session.mjs verdicts --fixture <fixture dir>` | computes the assumption table into `CAPTURE.md` and prints the decisions (`join`, `t3OutputSource`, `OTEL_STAMP_MARKS`, `HOOK_TIME_SOURCE`); nothing typed by hand | same |

The counters, the partition and the ledger reader are libraries; their public functions are `sessionCost`, `crosscheck`,
`aliasRecord`, `checkAliases`, `startReceiver`, `spansFrom`, `partition`, `readLedger`, `ledgerSummary` and `roundsSpans`.

## Inputs, and where they live

The specifications and the two visible test scripts are **not in this repository** (decision D18: the upstream files carry no
licence, so they live only inside a public GitHub fork, relying on GitHub's Terms of Service for viewing and forking).

- The fork is `oleksandrdubyna88/minigit-bench`, branch `inputs`, **pinned at commit
  `df00cac8dae3f7bdc64a8cd5a5b7f777fec055c3`** (`PINNED_INPUTS_SHA` in `lib/fetch-inputs.mjs`; a test keeps it equal to the
  value in `inputs/UPSTREAM.md`). The branch holds the four upstream files unchanged under `upstream/`, the two specifications
  again, and the two **fixed** visible scripts the agents and the scorer run.
- This repository holds `inputs/hashes.txt`: the sha256 of six fork files and of `heldout/suite.enc`.
  `fetchInputs` clones the fork once into `~/.cache/minigit-bench/inputs/<sha>/`, outside every repository, checks every file
  against `hashes.txt` (the suite against the spike folder, not the fork) and refuses anything that does not match. A test walks
  the spike and fails if a distinctive upstream line appears in it.
- The three edits to the visible scripts: the build fails loudly (`BUILD FAILED`, `PASSED: 0`, `FAILED: 1`, exit 1); a crash
  counts as failures (`set -e` dropped, the expected test count carried, an unrun test counts as failed); the parent of a commit
  is checked in v2's checkout and reset tests. Each fixed script begins with a header naming the upstream commit, the upstream
  file's hash and the edits.
- Changing a pinned input never edits a file in place: a new commit on the fork's `inputs` branch, then one commit here with
  the hashes, the pinned sha and `inputs/UPSTREAM.md`.
- Other inputs in `inputs/`: `prices-2026-10-29.json` (dated published prices with `cliRates` scoped by CLI version) and
  `crosscheck-policy.json` (the pre-registered stop policy, decided by the capture).

## What the real capture decided

One paid session on an unrelated task (CLI 2.1.310, $0.18, two subagent children on Sonnet and Fable) is kept as a
sanitised replay fixture in `spikes/minigit-bench/test/fixtures/real/claude-2.1.310-capture/`, with
`CAPTURE.md` holding the table below, computed by `capture/verdicts.mjs`. Every parser was written against it; a refuted
assumption became a dated amendment in `todo/PLAN_minigit_stack_bench_E1.md` before the affected code.

| Id | Assumption | Verdict on the capture |
|---|---|---|
| A-E1, A-E2 | `modelUsage` carries every child's model; Σ `costUSD` equals `total_cost_usd` | hold (gap 0) |
| A-S1 | the stream repeats a message per content block with a placeholder output | holds |
| A-T1, A-T2 | transcripts exist for main and every child with the 5m/1h split on every line; their output tokens are final | hold, so `t3OutputSource = transcript` |
| A-J1 | a transcript message and an O record name the same request by `request_id` | holds (7 of 7), so `join = request_id` |
| A-O1 | OTLP/HTTP **JSON** on `/v1/logs`, every attribute name the decoder reads present | holds |
| A-O2 | an `api_request` record's time marks the **end** of the request | holds (7 of 7), so `OTEL_STAMP_MARKS = end` |
| A-O3, A-O4 | O summed over all sources equals E per model and kind; Σ O `cost_usd` equals `total_cost_usd` | hold (gap 0) |
| A-O5 | child requests carry `agent_id` and `parent_agent_id` | **refuted as worded**: `agent_id` on 4 of 4, `parent_agent_id` on 0 of 4 at depth one; `agent_id` alone attributes per child |
| A-H1, A-H2 | hook events carry a `hook_id` and no timestamp; with tool details on, `tool_result` records carry `tool_use_id` and `duration_ms` | hold, so `HOOK_TIME_SOURCE = arrival` (pinned to the verdict in `capture-real.test.mjs`) and hook spans use the runner's arrival stamp |
| A-M1 | model ids are exact and the alias record can be built | holds; the subagent tool is reported as **`Task`** while `--tools Agent` is accepted, so the guard accepts both |
| A-P1 | the CLI bills at published rates, except an observed cache-read rate for that CLI version | holds once re-worded: Sonnet cache reads are billed at the published $0.10 on 2.1.310; the $0.20 belonged to 2.1.289 |

The consequences in the code:

- **`query_source` roles.** Headless `-p` reports main requests as **`sdk`** and child requests as **`agent:*`** (here
  `agent:builtin:general-purpose`), not the documented `main` / `subagent`. `roleOf` maps `sdk` or `main` to main,
  `agent:*` or `subagent` to subagent, anything else to auxiliary, and an absent source to unknown, never auxiliary.
- **Version-scoped `cliRates`.** Each entry names the CLI version it was observed on and applies only to it. A version with no
  entry is billed as published.
- **`Task` is the subagent tool** the init event reports.
- `api_request` also carries `cache_creation_5m_tokens`, `cache_creation_1h_tokens` and `cost_usd_micros`; `duration_ms` is an
  integer there and a string on some other events.
- Headless `-p` needs an explicit allow-list for every tool the prompt uses.
- After the review of the counters, a lost subagent transcript is incompleteness: any unmatched O record whose role is main,
  subagent or unknown makes T3 and T4 `unchecked`, and a killed session whose O has no complete record is `unknown`, never $0.

## Isolation facts that were verified

The scoring box (`lib/bwrap.mjs`, `scoreBoxArgv`) exists because the scorer runs agent-written code as the operator after the
session, possibly while the partner cell still runs. Each fact below has a test in `test/bwrap.test.mjs`, run on the series
machine.

- The whole filesystem is bound **read-only**; the operator's home, `/mnt`, **`/run`**, **`/tmp`** and **`/var/tmp`** are
  replaced by empty tmpfs; only what scoring needs is bound back in afterwards (toolchains read-only; the copy, HOME, TMPDIR
  and the cache copies read-write; the case directory only for the held-out step).
- **`/run/user` is hidden for a reason that was reproduced.** A process inside the box that could reach the user's session bus
  (`/run/user/<uid>/bus`) could ask the systemd user manager to start a unit **outside** the box. The test first shows that,
  with the tmpfs removed, `systemd-run --user` succeeds from inside the box (the control), then that with it in place the bus
  socket is not there. `/tmp` and `/var/tmp` are hidden because they hold the host's build-server sockets.
- **No network** (`--unshare-net`), its own pid namespace (`--unshare-pid --die-with-parent`, so killing the bwrap process
  ends every grandchild and a timed-out child cannot hold a pipe open), a new session, and an environment of a fixed
  allow-list only (`--clearenv`).
- **The harness's test script is read-only over the writable copy.** The tree under test cannot rewrite the script that scores
  it; the held-out step binds the copy itself read-only.
- A box that cannot start, or a spawn that fails, is an **environment** failure (exit 3, resumable), never a session that
  scored badly.
- Not in the box: the suite's key and its clear copy (the runner is host code, and the clear directory is under the hidden
  home); the per-session sandbox for the agent itself is E2.

## What it does not do yet

| Missing | Epic |
|---|---|
| The **session sandbox** for the agent (`sandbox.mjs`: its bind list and credentials; the box primitives of `lib/bwrap.mjs` are the ones it will compose), coai in its own bwrap and the stdio↔socket bridge, the argv and settings files, the watchdogs and janitor, the read-back before the first paid turn | E2 (S2, S3) |
| The `policy-ab-quick` fixes: killing the process group instead of one pid, the audit hook's `--out`, `PostToolUse` `duration_ms`, the spend cap list. `spansFrom` assumes the audit record shape that fix will write | E2 (S1) |
| The treatment itself: the conventions snapshot branch, the one-source templates, the four template branches, the lint gate | E2 (S4) |
| Wiring the OTel receiver to a live session (the cost and stall watchdogs read its running sum) | E2 (S2) |
| A **runner**: blocks, barrier, seeded order, voids, re-runs, resume and the spend ceiling; no command yet runs the counters or the partition on a session | E3 (S1) |
| The **compliance checker** over GitHub, `coai.db` and the stream, and GitHub's intervals (`ci_wait`, `coderabbit_wait` stay generic `wait` until then); a `delivered` verdict, so a process session's `accepted` reads `unknown` | E3 (S2) |
| Pre-registration committed before the first paid session, P0 (no model), the model smoke through the bridge | E3 (S3) |
| The A/A and P1 pilots that decide whether **T3 becomes a stop** (`policy.t3` is `investigate` today; the pre-registered fallback is `fallback-t2` with `t2 = stop`), the day-1 price re-read, the draw | E3 (S4) |
| A **real coai ledger**: the reader's fixtures are synthetic | E3 (S3–S4) |
| The twelve-cell series, the block-1 by-hand read-back, the blind hand classing of failing held-out cases, the ≥ 20-verdict compliance hand check | E4 (S1–S2) |
| `report.mjs` (the per-session, per-stack × arm and pair-ratio tables, the series report beside this file), credential teardown, archive and `retire.mjs`, publishing the suite's case list beside the report, promotion of the plans | E4 (S3) |

Until E3 has run, nothing in `spikes/minigit-bench/` is a measurement of either stack: E1 only shows that the instruments are right.

## Verifying this document

```bash
BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs     # 0 failed; read the skips against the list below
node spikes/minigit-bench/heldout-tool.mjs counts                    # needs the key; the archive agrees with the manifest
node spikes/minigit-bench/capture/capture-session.mjs verdicts --fixture spikes/minigit-bench/test/fixtures/real/claude-2.1.310-capture   # regenerates the A-* table
```

The command, not a figure written here, is the current truth. `BENCH_REQUIRE_SANDBOX=1` turns a skip for a missing `bwrap`, `python3`
or held-out key into a failure, so those can never hide behind a skip. It does **not** cover the skips that depend on the host, which are
reported by their reasons and are the only skips a complete run may show:

| Skip | When | What is checked instead |
|---|---|---|
| the coai source checks (`The_rounds_columns_…`, `The_ledger_fields_…`) | there is no sibling `dew_flow_connect_other_ais` checkout beside the repository (or beside the main checkout of a worktree) and no `COAI_SRC` | the reader against its own copied DDL and field list |
| the user runtime directory check (`The_box_cannot_see_the_users_runtime_dir_…`) | the host has no `/run/user/<uid>` | nothing: the box still hides `/run` |
| the user manager escape check (`The_box_cannot_start_a_unit_on_the_hosts_user_manager`) | the host has no working `systemd-run --user` | nothing: with no manager there is nothing to escape to |
| the open-descriptor check of the file sink | no `/proc/self/fdinfo` | the sink's other tests |

## Where to read further

- `todo/PLAN_minigit_stack_bench.md` — the parent plan (§5 inputs and scoring, §8 the harness, §9 accounting, §10 time) and
  the epics E2–E4 that remain.
- `todo/PLAN_minigit_stack_bench_E1.md` — the E1 stories with their test ids and the dated amendments.
- `spikes/minigit-bench/inputs/UPSTREAM.md` — where the inputs come from and the licence decision.
- `spikes/minigit-bench/test/fixtures/real/claude-2.1.310-capture/CAPTURE.md` — the capture's table with observed values.
- `research/scout-calibration/prices.json` — the earlier dated price table the new one descends from.
