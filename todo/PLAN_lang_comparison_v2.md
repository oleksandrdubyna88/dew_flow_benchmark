# PLAN — language comparison v2: C# / .NET 10 against TypeScript / Node 26 coding agents on ten harder tasks with runtime traps

> Status: **plan only, nothing implemented yet, 2026-10-31.** Scope: a new public GitHub repository
> `dew_flow_ai_language_comparison` (task workspaces, statements, visible samples, reference notes); a new spike
> `spikes/lang-bench/` in this repository that imports the v1 harness `spikes/minigit-bench/` (box, proxy, broker, counters,
> scorer, runner) and extends it for long sessions, daemons and web APIs; held-out suites and raw records outside every git
> tree under `~/rsd/lang-v2/`; the series' record under `research/lang-v2/`. Bare arm only (D8). The `## Plan changes`
> ledger opens at this plan's first approval (`common.plan-change-control`); until then the plan has none.

## 1. The question, before any solution

Experiment v1 ([PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md), record
[research/minigit/RESULTS_bare_2026-10-30.md](../research/minigit/RESULTS_bare_2026-10-30.md)) compared C# and TypeScript
coding agents on the tiny minigit task. Its bare arm found: cost C#/TS median **0.92** (C# > TS in 1 of 3 pairs); wall
C#/TS median 0.93; `model_api` C#/TS 0.86; `active − model_api` (mostly build and test time) C#/TS **1.70**, C# slower in
3 of 3 pairs; **every scored session of both stacks was accepted** (22 of 22, lowest held-out percent 100 %). Every session
ran about one minute. The record's own threat section says it: *"On this task the comparison mostly measures how many model
tokens each stack's agent used, not how the toolchains behave at scale"* and *"Quality is at the ceiling: it does not
separate the stacks here"* (§12).

So v1 answers the cost question for a tiny task and does not reach the question people argue about: does one stack make a
coding agent produce **wrong programs** more often when the task has traps the compiler does not catch? The question of v2,
and nothing wider:

> On ten tasks of three kinds (olympiad stdin/stdout, daemon, web API), each with runtime traps on both sides, with **Claude
> Code** (Opus, effort `high`, bare: the task prompt only), how do **C# 14 / .NET 10** and **TypeScript 7 strict on Node 26
> with Fastify** compare in (a) first-submission correctness on held-out tests the agent never sees, (b) correctly counted
> cost, (c) decomposed wall time, per task and over the set?

It is not a claim about the languages in general. It licenses "in k of 3 pairs" per task, counts over the ten tasks, and
whether ranges overlap (§9).

## 2. What exists — verified 2026-10-31 at `de8479b` (this worktree's base)

v2 **reuses** the v1 harness. The table names what is there and where; §6 says what is reused as is and what must change.

| Fact | Where |
|---|---|
| The agent box: `bwrap`, root read-only, home/`/mnt`/`/run`/`/tmp` hidden, an explicit bind list, own network/pid/ipc namespaces, no `--setenv`; `sessionLayout(root, rid)`, `agentEnv({ layout, dummyToken, cellId, nodeDir, tsDir, extra })`, `agentBoxArgv(opts)` | `spikes/minigit-bench/lib/sandbox.mjs:72`, `:93`, `:181`; `research/module_minigit_bench.md` §5 |
| The CONNECT proxy with the SNI check (exact host, one `host_name` entry, ECH refused, bytes per tunnel); `EGRESS_HOSTS.bare` | `lib/egress.mjs`, `lib/tls-hello.mjs`, `lib/egress-hosts.mjs:11` |
| The credential broker: the real token never enters the box; `B` ledger; cap refusals as 429 | `lib/broker.mjs`, `lib/budget.mjs`; parent D23 |
| `launchSession(given, deps, { signal })` — one session end to end, `meta.json` with `stopped` | `lib/session-launch.mjs:252` |
| Four cost views E/O/T/B and checks T1–T5: `sessionCost({ E, O, T, table })`, `crosscheck({ E, O, T, B, table, policy, cliVersion })`; policy `t1 stop`, `t3 stop` for bare data cells | `lib/session-cost.mjs:36`, `lib/crosscheck.mjs:260`, `inputs/crosscheck-policy.json` |
| Time: `spansFrom(...)`, `partition(wall, spans)` — the exclusive work-before-wait partition; primary time measures wall, `model_api`, `active − model_api` | `lib/spans.mjs:179`, `lib/timeline.mjs:68`; parent §10 |
| Held-out suite sealed with AES-256-GCM, key a mode-600 file, clear only in a scoring temp dir; `heldout-tool.mjs seal\|calibrate\|leakcheck\|counts\|sweep`; `runSuite`, `heldoutScore`, `clearCases` | `heldout-tool.mjs`; `lib/heldout-run.mjs:64`, `:107`, `:163`, `:180`; `lib/heldout-seal.mjs`, `lib/heldout-calibrate.mjs` |
| The scorer on a pristine copy, offline from seeded caches, bounded steps (`buildS 600`, `perInvocationS 10`) | `score-session.mjs`; `lib/limits.mjs:3`; `lib/score-steps.mjs` |
| The bare runner: `ridOf`, `maskFor` (masks `0-5` / `8-13`, swapped per pair), `promptFor`, pair phases, the barrier, the outcome table (12 rows), the schedule of voids and the one re-run, resume, records | `bare-runner.mjs`; `lib/bare-plan.mjs:12`, `:28`, `:40`; `lib/bare-outcome.mjs:124`; `lib/bare-schedule.mjs`; `lib/bare-record.mjs` |
| The spend gate: `spendDecision(spent, ceiling, caps, { liveUsd, factor })`, `spendGate({ ledger, liveUsd, caps, ceilingUsd, bareBudgetUsd })` | `spikes/policy-ab-quick/lib/spend.mjs:18`; `lib/spend-gate.mjs:32` |
| The machine-quiet check, the CPU sampler, the spin probe; `quietVerdict`, `waitForQuiet` | `lib/cpu-sampler.mjs:120`; `lib/quiet.mjs`; `inputs/quiet.json` |
| The pre-registration guard and harness identity; the tag `prereg/minigit-bare-20261030`; the commit-reveal seed | `lib/prereg-guard.mjs`; `lib/seed.mjs`; `inputs/seed.sha256`, `inputs/seed.txt` |
| The A/A checks G-1 … G-14 and `aaVerdicts` | `lib/aa-verdicts.mjs:39`; [PLAN_minigit_stack_bench_E3_bare.md](PLAN_minigit_stack_bench_E3_bare.md) E3b.S4 |
| The contamination scan and its 18 instrument defects, 3 of which voided sessions of one stack (defects 1, 14, 16) | `lib/contamination*.mjs`, `lib/session-scan.mjs:80`; RESULTS §11 |
| Cache snapshots per stack, hashed, seeded into every box; `makeSnapshot`, `seedCaches` | `lib/cache-snapshot.mjs:162`, `:58`; `inputs/cache-recipe.json` |
| The generated research record: `extract.mjs` → `data/<series>.json`, `render.mjs` rewrites `<!-- generated:… -->` blocks, `replay.mjs` | `research/minigit/tools/` (11 files, 1402 lines) |
| v1 pins: claude **2.1.310**, node 26.12.1, dotnetSdk 10.0.114, typescript 7.0.2; the TS toolchain read-only under `~/rsd/minigit/toolchains/typescript-7.0.2`, the CLI copy `claude-2.1.310` | `inputs/stacks.json`; `ls ~/rsd/minigit/toolchains` |
| Host today: claude 2.1.310, dotnet SDK 10.0.114 (apt), node v26.12.1 | `claude --version`, `dotnet --list-sdks`, `node --version`, 2026-10-31 |
| Release indexes, read 2026-10-31: .NET 10 `latest-sdk` **10.0.403**, `latest-runtime` 10.0.14, released 2026-10-25, support phase active; Node `v26.13.0` 2026-10-28, lts "Lithium"; v26.12.1 is not marked LTS; v27.0.0-alpha.0 exists | `dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json`; `nodejs.org/dist/index.json` |
| npm, read 2026-10-31: fastify 5.12.7, @fastify/jwt 10.2.6, @fastify/type-provider-typebox 6.3.0, @sinclair/typebox 0.34.52, pino 10.4.0, typescript 7.0.2, express 5.3.0, @nestjs/core 12.3.2, hono 4.13.14 | `npm view <pkg> version` |
| ASP.NET Core 10 Minimal APIs have built-in validation: `builder.Services.AddValidation()`, DataAnnotations on query, header and body, a source generator, 400 on failure, `.DisableValidation()` per endpoint; the APIs live in `Microsoft.Extensions.Validation` | Microsoft Learn, *What's new in ASP.NET Core 10.0*, read 2026-10-31 (**verified**) |

What does **not** exist: a task other than minigit; a driver that starts a program, talks to it over TCP or HTTP, samples its
memory and stops it with SIGTERM; a judge for stdin/stdout tasks with time and memory limits; a session longer than 60 min;
a package cache that holds a web framework; references in two languages per task; the public task repository.

## 3. Decisions

Rows marked "operator, 2026-10-31" are the operator's decisions of that day, recorded as given. Rows marked "this plan" are
proposals of this document for the operator's approval; the open questions (§17) name what they do not settle.

| # | Decision | Who, when |
|---|---|---|
| D1 | **v2 compares C# and TypeScript coding agents on 10 harder tasks**, not the tiny minigit task. v1's finding (cost C#/TS ≈ 0.92, C# slower only in build and test time, every session accepted) motivates harder tasks with **runtime traps**: defects a compiler does not catch, which show in held-out tests | operator, 2026-10-31 |
| D2 | **A new public GitHub repository `dew_flow_ai_language_comparison`** holds the task workspaces: `csharp/` and `node/` (one sub-folder per task in each), `tasks/` (statements, visible samples, reference notes), and the pinned conventions as in the other `dew_flow` repositories. **Held-out tests and raw records stay outside git** (as v1). Because it is public: no secrets, no raw transcripts, no held-out content; known-problem statements are **paraphrased with credit and a link; the link only in the repository README**, never in the agent's prompt or workspace. The bench harness stays in `dew_flow_benchmark` | operator, 2026-10-31 |
| D3 | **Tasks: 5 known + 5 original; 4 olympiad, 3 daemons, 3 web APIs; difficulty from a few minutes to about one hour of agent work.** Traps are runtime traps on both sides across the suite. The list is §4.1 (order = expected difficulty; the times are guesses, calibrated by the pilot). Daemons and web APIs are scored by a harness that starts the program, drives held-out scenarios and checks replies, RSS over time and a clean SIGTERM stop. Consultant review on 2026-10-31 (codex gpt-6-astra, gemini) shaped the list: CSES 1712 dropped (a one-sided JS trap), CSES 1736 dropped (sums stay below 2^53 at CSES limits, ≈ 4e15, so its JS trap does not fire); gemini's claim that .NET console apps default to Server GC was **rejected** (they default to Workstation GC) — still, the harness fixes GC settings explicitly per stack and records them | operator, 2026-10-31 |
| D4 | **Method rules from the consultants, adopted:** the agent never sees a CSES/IOI id or title (paraphrased statement only); workspace samples are small and clean and the traps live in held-out tests, so the first-submission pass rate can show a difference; time and memory limits are calibrated on reference solutions in **both** languages, so no limit rejects one runtime's baseline footprint; a **paid pilot (tasks 1, 3, 10 at least)** measures real times before the full series; the session wall limit rises from v1's 20 min to **about 75 min**; v1's $6 per-session cap is **re-derived from the pilot** | operator, 2026-10-31 |
| D5 | **Packages: option A — a pinned offline package cache per stack**, the same discipline as v1's cache snapshots; **the agent has no general network** | operator, 2026-10-31 |
| D6 | **Stacks: both current LTS lines, latest patch, checked 2026-10-31 against the official release indexes.** C#: .NET 10, SDK **10.0.403** (released 2026-10-25; the host has 10.0.114, so it must be upgraded and pinned), runtime 10.0.14, C# 14 (the default for .NET 10; C# 15 ships with .NET 11, RC3 on 2026-10-31, not stable, so out). Web APIs use ASP.NET Core **Minimal API**; daemons the Generic Host (`Microsoft.Extensions.Hosting`); JWT `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x; tests the agent's choice from the cache (xUnit v3 offered). TypeScript: Node **26.13.0** (LTS "Lithium", released 2026-10-28; the host has 26.12.1, so it must be upgraded and pinned), TypeScript **7.0.2** with `strict` (v1's toolchain line). The Node web stack is the team lead's choice, with the reasons recorded (§4.5): **Fastify 5.12.7** with TypeBox 0.34.x (`@fastify/type-provider-typebox`), `@fastify/jwt` 10.x, pino, `node:test`, `node:net`. Toolchains are pinned in read-only copies with checksums (as `~/rsd/minigit/toolchains/`) and recorded in the pre-registration | operator, 2026-10-31 (stacks); team lead, 2026-10-31 (the Node web stack, reasons in §4.5) |
| D7 | **Budget: $1000** for the paid runs of v2 (bare arm). 10 tasks × 2 stacks × 3 pairs = **60 data sessions**, plus the pilot, the A/A and re-runs, within that limit; the spend gate enforces it | operator, 2026-10-31 |
| D8 | **Same agent as v1: Claude Code (Opus, effort `high`), CLI pinned, bare arm first** (task prompt only). A process arm is out of scope for v2 unless a later decision adds it. v1 pinned CLI **2.1.310** (`inputs/stacks.json`); v2 **re-pins at its pre-registration** to the newest CLI version installed on that day, copied read-only as v1's `init` does (today that is still 2.1.310); the v1 pin is not inherited | operator, 2026-10-31; the re-pin shape: this plan |
| D9 | **Unit of analysis = the session** (task × stack × pair). There is no v2 phase; the 60 sessions are 60 observations in 30 concurrent pairs. The C#/TS comparison is read **per task** (3 pairs) and **over the tasks** (10 task medians, counted) — §9 | this plan |
| D10 | **Order: three rounds.** Round r runs every task once; the task order inside a round is drawn from the seed (commit-reveal as v1); the two cells of a pair start in the same tick on masks `0-5` / `8-13`, swapped per pair as `maskFor` does. A task's second pair runs only after every task has run once, its third only after every task has run twice, so the ceiling (D7) cuts whole rounds, never one task's pairs | this plan |
| D11 | **The agent's workspace is a copy of one task folder** (`csharp/<task>/` or `node/<task>/`) at a pinned commit of the task repository, fetched and hash-checked as v1's `fetchInputs` does. The repository's root files (README, conventions, the other stack's folder, `tasks/`) are never in the box. The bare agent gets **no** conventions: the conventions in the task repository are for the people and agents who maintain it, not for the sessions | this plan |
| D12 | **Fixed per-stack scripts in every workspace, frozen inputs with hashes:** `build.sh` and `run.sh` (olympiad: reads stdin, writes stdout; daemon and web API: the long-running program, `PORT` and the task's inputs from the environment). The agent fills the code; the harness drives only these two scripts, identically for both stacks. A `PACKAGES.md` lists the offered package set of the stack (D5); the statement tells the agent what is frozen | this plan |
| D13 | **Scoring per task kind** (§4.3): olympiad — held-out input/expected pairs under calibrated time and memory limits, each case `pass` / `wrong answer` / `time limit` / `memory limit` / `runtime error`; daemon and web API — held-out scenarios driven by the harness, RSS sampled every second, a SIGTERM stop that must end the process within a grace period with no child left. **Accepted** = clean build, every held-out case or scenario passed, memory and stop checks passed. **The same held-out cases run against both stacks**; the suites are language-neutral (bytes over stdin/stdout, TCP or HTTP) | this plan |
| D14 | **Limits:** time limit per olympiad task = **3 ×** the slower of the two reference solutions' wall, with a floor of 1 s; memory limit = **2 ×** the larger reference peak, with a floor of 256 MB; the RSS bound of a daemon or web API task = **2 ×** the larger reference peak under the task's load scenario. Peak memory is read from the scoring cgroup's `memory.peak` where the host gives it, else from 100 ms RSS samples — never `RLIMIT_AS` (it counts reserved address space, which .NET and V8 reserve in GBs). The numbers are computed by E2 and pre-registered; a change after the pilot is a dated amendment applied to both stacks | this plan |
| D15 | **GC and runtime settings, fixed and recorded per stack:** C# `DOTNET_gcServer=0` (Workstation, set explicitly), `DOTNET_TieredCompilation` default, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`; Node: no `--max-old-space-size` flag from the harness (the memory limit is the cgroup's); `NODE_OPTIONS` empty. Every value is in `agentEnv`'s allow-list, the same in the agent box and the scoring box, and in every session record | this plan |
| D16 | **The pilot's sessions are void (never data)**, as v1's A/A and pilot were; they fix the session cap, the stall timeout, the time guesses of §4.1 and the limits of D14. The pilot runs tasks 1, 3 and 10 as pairs (C# ∥ TS), plus one A/A pair (two identical C# sessions of task 3). The go/no-go table is v1's G-1 … G-14 plus the driver checks of §11 | this plan |
| D17 | **Egress for v2 sessions:** no registry. `EGRESS_HOSTS.lang` holds no host; the only road out is the broker's API door. Package installs resolve from the seeded offline caches (D5). A session whose stream shows a refused CONNECT is flagged `wanted network`, not voided | this plan |
| D18 | **Contamination needles for the known tasks** (id, title, site names; `CSES`, `IOI`, `Werewolf`, `RealWorld`, `Conduit`, the public repository's name and URL) join the scan's needle list, in tool inputs and outputs. A hit is **read by hand**: the agent recalling a known problem from memory is a threat of the design (T1), not a void; a fetch or a path reaching outside the box is a void, as v1 | this plan |
| D19 | **Known-task statements name no source.** The link and the credit live in the task repository's README (D2) and in `tasks/<task>/NOTES.md`; the agent's workspace holds `STATEMENT.md`, the samples, `PACKAGES.md`, `build.sh`, `run.sh` and the stack's pin files, nothing else | operator, 2026-10-31 (D2); the file list: this plan |

## 4. Design

### 4.1 The ten tasks

K = known (paraphrased with credit), O = original. Times are the operator's guesses for one agent session, to be calibrated
by the pilot (D4). Each trap below is one the compiler does not catch.

| # | K/O | Task | Kind | C# traps | TypeScript traps | Guess |
|---|---|---|---|---|---|---|
| 1 | K | *Weird algorithm* (CSES 1068): the Collatz-like sequence of n ≤ 10^6 | olympiad | `int` overflow (values reach 5.7e10) | `n & 1` truncates to 32 bits | 2–5 min |
| 2 | K | *Counting rooms* (CSES 1192): connected components in a grid up to 1000 × 1000 | olympiad | recursive DFS stack overflow on a snake corridor | the same overflow; `Array.shift()` makes BFS O(n²) | 5–10 min |
| 3 | O | *Deep expression evaluator*: input to 2·10^6 chars, nesting depth to 10^6, signed 64-bit wrap-around; the full grammar is published; `MinValue / -1` and the literal 2^63 under unary minus are defined | olympiad | `long.MinValue / -1` throws even unchecked; `long.Parse("9223372036854775808")` fails | `BigInt.asIntN` cost; recursion stack overflow | 20–30 min |
| 4 | O | *Rule API*: HTTP; rules call members by name over a supplied registry, with string arguments | web API | culture-dependent parsing of string arguments (host culture `de-DE`); reflection overload choice and numeric conversion | `__proto__` and inherited member lookup with an observable consequence; `JSON.parse` corrupting 64-bit ids; `Math.round(-1.5)` | 20–30 min |
| 5 | K | *RESP key-value server* (a Redis subset): GET/SET/EXPIRE/PX/INCR over TCP, many clients | daemon | expired keys never purged; string against binary-safe bytes | partial TCP frames; Buffer→string corruption; backpressure | 25–35 min |
| 6 | K | *RealWorld "Conduit" API subset*: users, JWT, articles, pagination | web API | `DateTime.Kind` and UTC serialization; JSON name casing | JWT checks; `Date` and time zones; async errors | 30–45 min |
| 7 | O | *Stream-processor daemon*: an event stream, a sliding window per key, a watermark that expires keys that never return, an RSS limit, graceful SIGTERM; the spec fixes event-time semantics, ordering, bounds, output cadence and EOF | daemon | `Dictionary` does not shrink; `JsonDocument` lifetime; Generic Host shutdown | `Map` leak; `readline` backpressure | 30–45 min |
| 8 | O | *Ledger API with idempotency keys*: money, concurrent retries of the same key | web API | `decimal` rounding mode; a race on the same key | float money; a race on the same key; body limits | 35–50 min |
| 9 | K | *Werewolf* (IOI 2018), stdin/stdout adapter identical for both | olympiad | deep reconstruction-tree chains (recursion); memory of auxiliary structures; allocation pressure | the same three | 45–60 min |
| 10 | O | *DAG job scheduler daemon*: retries with capped backoff, per-job timeouts, cancellation, one process-wide concurrency limit k over overlapping submitted DAGs, cycle detection, failed-dependency propagation, a deterministic final report; a supplied virtual clock and job fixture for deterministic grading plus a real-time smoke | daemon | double semaphore release; unobserved task exceptions; `async void` | an un-cleared `setTimeout` keeps the process alive; unhandled rejections; cycle deadlock | 45–60 min |

Sum of the upper guesses: 370 min per stack per round; three rounds ≈ 18.5 h of paired agent wall, scoring apart.

**Statement rules (D4, D19).** One `STATEMENT.md` per task, byte-identical across the two stacks except the stack sentence
(as v1's `{STACK_SENTENCE}`). A known task is paraphrased: no id, no title, no site; the constraints may keep the original
numbers. An original task's statement is precise enough that two references written apart agree on every held-out case
(E2.S1 checks this, the same way v1 calibrated two Python references). Every statement names: the input and output
contract, the limits (time, memory, RSS), the start contract (`build.sh`, `run.sh`, `PORT`), what is frozen, and that
hidden tests exist. It never names a trap.

**Samples** (`samples/`): a few small clean cases per task that the traps do not reach; the agent may run them. The held-out
suite holds the traps.

### 4.2 Cells, pairs, order (D9, D10)

- Session = (task, stack, pair n), n ∈ 1..3; 60 data sessions, 30 pairs. `rid = sha256(seed:lang:t<task>:<stack>:p<n>:a<attempt>)[0:6]`,
  distinct across every field (v1's T-R1 widened).
- A pair = C# ∥ TS of one task, started in one tick, masks `0-5` / `8-13` (`maskFor`, swapped per pair), core 3 (CPUs 6–7)
  for the harness, nice 19 as v1. Both sessions of a pair are collected and scored before the next pair starts; scoring runs
  on all CPUs after the cells end, as v1.
- Three rounds, the task order per round from the seed; `seed.sha256` committed with the pre-registration, `seed.txt`
  revealed after the A/A and before round 1 (v1's commit-reveal).
- No barrier between phases is needed (no v2 phase). The void and re-run rules of v1 (§9) apply per session; a void is
  re-run once, after the round's own pairs and before the next round; a re-run whose partner stands runs alone and is
  labelled `ran unpaired`.
- The machine-quiet check before every pair; load during a pair is flagged, never a void (v1 E3b.S2).

### 4.3 Scoring per task kind (D13, D14)

All scoring happens on a pristine copy of the workspace, in a scoring box with no network, offline from the session's own
caches, with temp HOME, as v1. `build.sh` first (bounded, 600 s; a failure fails every case). Then by kind:

| Kind | The harness does | A case passes when | Also recorded |
|---|---|---|---|
| Olympiad | runs `run.sh` once per held-out case with the case's stdin, under the task's time and memory limits in a cgroup | stdout equals the expected bytes (trailing whitespace normalized if the statement says so), exit 0, within limits | per case: wall, CPU, peak memory, verdict class |
| Daemon | starts `run.sh` with the task's environment; waits for readiness (the statement defines it); drives scenarios over TCP or stdin (a Python driver per task, run with `-I`); samples RSS every second; sends SIGTERM; waits the grace period | every scenario reply equals the expected one; RSS stays under the bound; the process exits 0 within the grace period; no process of the session is left | RSS over time, exit code and time, stdout/stderr tails |
| Web API | as daemon, over HTTP; scenario steps are requests with expected status, headers and body; concurrency scenarios send the same request from k clients at once | as daemon; concurrent scenarios check the invariant (one effect per idempotency key, one balance) | as daemon, plus the request log |

**Measures per session:** `accepted` (D13); `percent` = cases passed / N (a blocked case counts as not passed); `defects` =
distinct **trap classes** failed, from the trap coverage table (one trap failing in three cases counts once); verdict class
counts. **First-submission** means: what the agent left in the workspace when its session ended, scored once; no retry.

**Calibration before any cell (measurement rule 2):** two references per task, one in C#, one in TypeScript, each pass 100 %
under the registered limits in the scoring box; planted mutants — one per trap per stack, each a plausible version of that
trap — each fail exactly their registered cases. The **trap coverage table** (task × stack × trap → the cases that catch it)
is sealed with the suite and published after the series, as v1 published its case list.

**Adjudication, pre-registered as v1 §5.3:** the primary score is the unrevised one; every failing held-out case is classed
blind (product defect / statement ambiguity / harness defect) in two steps, stack masked in step 1; a case classed ambiguous
yields a revised score beside the primary.

### 4.4 Prompts

One prompt template, byte-identical across stacks except `{STACK}` and `{STACK_SENTENCE}`, registered as `prompts-lang.json`
with sha256 per task and stack. Shape (final wording fixed in E4.S3):

```
Implement the program described in STATEMENT.md in this directory, in {STACK}. {STACK_SENTENCE}
STATEMENT.md, samples/, build.sh, run.sh and PACKAGES.md are frozen inputs: do not change them. The program is built by
`bash build.sh` and started by `bash run.sh` exactly as STATEMENT.md says. Hidden tests will run it under the limits in
STATEMENT.md. Only the packages listed in PACKAGES.md are available; there is no network. Work autonomously; nobody
answers questions. End with a summary of what you built and the checks you ran.
```

Stack sentences: C# — *"Use C# 14 on .NET 10 (SDK 10.0.403 is installed; the project files in this directory are
pinned)."* TypeScript — *"Use TypeScript 7.0.2 with `strict`, run by Node 26 with native type stripping (no build step),
with `tsc --noEmit` as the type check."* Web API tasks add one identical sentence naming the stack's web framework from
`PACKAGES.md`.

### 4.5 Stacks and packages (D5, D6)

**Both stacks get first-party or de-facto-standard pieces at the same level of abstraction:**

| Need | C# / .NET 10 | TypeScript / Node 26 |
|---|---|---|
| HTTP routing, binding | ASP.NET Core Minimal API (in the shared framework) | Fastify 5.12.7 |
| Request validation and serialization | Minimal API built-in validation (`AddValidation()`, DataAnnotations, source-generated; verified §2) + System.Text.Json | TypeBox 0.34.x schemas through `@fastify/type-provider-typebox`; Fastify's JSON-schema validation and fast serialization |
| JWT | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x | `@fastify/jwt` 10.x |
| Long-running process, lifecycle, graceful stop | Generic Host (`Microsoft.Extensions.Hosting`), `IHostApplicationLifetime` | Fastify lifecycle hooks and `close()`; `process.on('SIGTERM')` for daemons |
| Structured logging | `ILogger` (Microsoft.Extensions.Logging) | pino (Fastify's logger) |
| TCP | `System.Net.Sockets` | `node:net` |
| Tests | xUnit v3 offered (the agent's choice) | `node:test` |

**Why Fastify, and what was rejected (team lead, checked 2026-10-31 on npm):**

| Candidate | Version | Verdict | Reason |
|---|---|---|---|
| **Fastify** | 5.12.7 | **chosen** | Node-native and mature; built-in JSON-schema validation and fast serialization (the counterpart of Minimal API's built-in validation and System.Text.Json); lifecycle hooks and graceful close (the counterpart of `IHostApplicationLifetime`); a structured logger (the counterpart of `ILogger`); first-party JWT and TypeBox plugins |
| Express | 5.3.0 | rejected | no built-in schema validation or serialization: a weaker tool than Minimal API; choosing it would handicap TS |
| NestJS | 12.3.2 | rejected | decorators and a DI framework — the analogue of ASP.NET MVC controllers, not of Minimal API; heavier |
| Hono | 4.13.14 | rejected | modern and light, but multi-runtime with a Node adapter layer; its Node ecosystem for JWT and validation is younger |
| raw `node:http` | — | rejected | would make TS write its own framework while C# gets one |

**Package caches (D5).** One hashed snapshot per stack, made in a box through the proxy from a registered recipe
(v1's `makeSnapshot`), then the registries leave the egress list (D17). The C# recipe restores a project that references the
exact set (`Microsoft.Extensions.Hosting` 10.0.x, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x, `xunit.v3`, the
test SDK pieces; the ASP.NET Core shared framework comes with the SDK) and captures `NUGET_PACKAGES` and the first-run
state; `nuget.config` in every C# workspace points at the offline folder only. The Node recipe fills the npm cache with
`fastify@5.12.7`, `@fastify/jwt@10.2.6`, `@fastify/type-provider-typebox@6.3.0`, `@sinclair/typebox@0.34.52`, `pino` (the
version Fastify 5.12.7 resolves), `@types/node` for 26.x, `typescript@7.0.2`; every Node workspace's `.npmrc` sets
`offline=true`. **The exact package set per stack, with pinned versions and the hashes in the cache manifest, is E1.S4's
deliverable** and is pre-registered. `PACKAGES.md` in each workspace lists it (D12).

**Toolchains**, pinned read-only with checksums under `~/rsd/lang-v2/toolchains/` and checked inside the box by the
preflight: .NET SDK 10.0.403 installed from the official install script into that directory (not apt, so an unattended
upgrade cannot move it, as happened to v1 on 2026-10-30), Node 26.13.0 from the official tarball, TypeScript 7.0.2, the
Claude CLI copy (D8). `global.json` pins `10.0.403` with `rollForward: disable`; `package.json` `engines.node` pins 26.13.0;
`tsconfig.json` is v1's (`strict`, `noEmit`, `erasableSyntaxOnly`, `verbatimModuleSyntax`, `module nodenext`).

## 5. The public task repository (D2, D11, D19)

```
dew_flow_ai_language_comparison/
  README.md            what this is; the task list; credits WITH links (CSES, IOI 2018, RealWorld, the RESP spec) — the only place links appear
  LICENSE              the repository's licence (open question Q1)
  AGENTS.md CLAUDE.md .agents/PROJECT.md .agents/conventions   the family's pinned conventions, for maintainers
  tasks/<nn>-<slug>/   STATEMENT.md (stack-neutral text), samples/, NOTES.md (credit, paraphrase notes, design reasons — no traps, no held-out content)
  csharp/<nn>-<slug>/  the C# workspace: STATEMENT.md (with the C# stack sentence), samples/, PACKAGES.md, build.sh, run.sh, global.json, nuget.config, Directory.Build.props, an empty .csproj skeleton
  node/<nn>-<slug>/    the Node workspace: STATEMENT.md, samples/, PACKAGES.md, build.sh, run.sh, package.json, package-lock.json, tsconfig.json, .npmrc
```

Rules: no secrets, no transcripts, no held-out case, no reference solution before the series ends (references are published
beside the report, as a separate commit, after every data session is scored); a `leakcheck` with the suite's needles runs
on every commit range before it is pushed. The harness fetches the repository at a pinned sha into `~/.cache/lang-v2/<sha>/`
and checks every workspace file against `inputs/hashes-lang.txt`, as v1's `fetchInputs`. A `workspace-diff` check proves
the two stacks' folders of one task differ only in the stack sentence and the stack's pin files (v1's `template-diff`).

## 6. The harness — `spikes/lang-bench/`, importing `spikes/minigit-bench/`

The precedent is v1 itself: `spikes/minigit-bench/` imported `spikes/policy-ab-quick/lib/*` and changed those files only
through named seams with tests. v2 does the same one level up: **new modules in `spikes/lang-bench/`, imports from
`spikes/minigit-bench/lib/`, and a short list of named widenings of v1 files, each with a test**; never a second copy.

| v1 piece | v2 use |
|---|---|
| The box, hide list, bind list, `agentEnv`, the supervisor | **as is**; `extraRo` binds the task toolchains; `agentEnv({ extra })` adds D15's keys |
| The CONNECT proxy and SNI check | **as is**; `EGRESS_HOSTS.lang = []` (D17) — a new key, no change to the bare list |
| The broker, `B`, cap refusals | **as is**; the cap value comes from the pilot (D4) |
| `launchSession` | **as is** |
| E/O/T/B, T1–T5, `sessionCost`, `crosscheck`, the alias record (D12 of v1) | **as is**; the policy file for v2 starts from v1's `t3 stop` and is re-decided by the v2 A/A |
| `spansFrom`, `partition`, the categories | **widened:** the `build_test` classifier learns `bash build.sh`, `bash run.sh`, `dotnet publish`, `node --test`, `npm test`, the task drivers' own commands; a test per new literal |
| Held-out sealing, calibration, leakcheck, adjudication | **as is**, pointed at `~/rsd/lang-v2/`; the archive holds ten suites, the trap coverage table and the mutants' ids |
| The scorer pipeline on a pristine copy | **widened:** a `kind` step after the build — the olympiad judge, the daemon driver, the web driver (new modules `lib/judge-stdio.mjs`, `lib/driver-process.mjs`, `lib/driver-tcp.mjs`, `lib/driver-http.mjs`, `lib/rss-sampler.mjs`, `lib/cgroup-limits.mjs`) |
| The bare runner: pairs, masks, voids, re-run, resume, records, prereg guard, spend gate | **widened:** a task dimension and a `single` phase (no barrier, no v2 copy); the schedule drops the v1→v2 rows; the record gains per-kind fields; the guard's `PREREG_FILES` list is the v2 list |
| The quiet check, CPU sampler, spin probe | **as is** |
| Cache snapshots | **as is**, with the v2 recipes |
| `aaVerdicts` G-1 … G-14 | **widened** with G-15 … G-18 (§11) |
| `research/minigit/tools/` extract, render, replay | **widened** to tasks and kinds; the v2 record is generated the same way (E6) |
| Watchdogs | wall **75 min** (D4), stall as v1 (45 min, pilot may change), cost at the derived cap |

Launch argv as v1 (`claude -p --model opus … --max-budget-usd <cap> --tools Task,Bash,Read,Edit,Write,Glob,Grep,TodoWrite`),
no MCP, effort `high` pinned in the session settings file, fresh `CLAUDE_CONFIG_DIR` per session, no `gh` in the box.

## 7. Isolation and contamination

Prevented, as v1: the operator's home and `/mnt` hidden; no network but the broker door; the held-out suite sealed, its key
never in any box's environment; the real token never in the box. Audited, as v1: every tool call scanned for paths outside
the box, walkers, links, token shapes, canaries. New in v2:

- **Known-task needles (D18)** in the scan; a hit is read by hand and recorded as a dated line; it voids only when it shows
  a reach outside the box.
- **The other stack's folder and the task repository's root are never in the box (D11)**, so a session cannot read the other
  stack's workspace or the README's links.
- **The reference solutions** live only in `~/rsd/lang-v2/refs/` (outside git, outside every box) until publication.
- **Session memory:** `CLAUDE_CONFIG_DIR` is fresh per session, so a task's third pair does not see its first; the operator's
  `~/.claude` is hidden by the box.
- **The 18 instrument defects of v1** (RESULTS §11): the fixes of 16–18 are on `feat/minigit-bench-e3b-fix2`, which no paid
  series has run. v2 runs on a harness that carries them; the v2 A/A is the first paid run of that scan. Every void in v2 is
  replayed with the current scan at report time, as v1's `replay.mjs` does, and the record says which voids were the scan's.

## 8. Accounting and time

As v1 §9–10, unchanged: headline USD = the published-price recompute from `modelUsage` with the 5m/1h split from T; the CLI's
total and `B` beside it; every stop-rule figure is the largest of the three; T1 breach stops the series, T2 `otel incomplete`,
T3/T5 per the policy the A/A sets; a killed session without an envelope is priced from `B`. Time: wall from spawn to exit,
the exclusive partition (`build_test > model_api > other_tool > hooks`, then waits, then `unattributed`), primary measures
wall, `model_api`, `active − model_api`; `build_test` against `other_tool` descriptive only (v1's V16: a chained Bash call
takes its highest category; the record counts mixed chains). Prices: a dated copy re-read from the pricing page on the
series' first day. New in v2: per-session **scoring time** (build, cases, scenarios) is recorded apart from the agent's wall,
and per-case runtime of the olympiad tasks is a secondary measure (the runtime's speed, not the agent's).

## 9. Pre-registration and analysis

Committed and tagged `prereg/lang-v2-<yyyymmdd>` **before the pilot** (E4.S3): the statement of §1, the analysis contract
(this section and §4.3's acceptance), the outcome classes and voids, the ten suites' hashes and counts, the trap coverage
table's hash, the limits of D14, the prompts, the pins, the package sets, the cache hashes, `prediction.md`, `seed.sha256`.
After the pilot, dated amendments only; the pilot may change the cap, the stall timeout, the limits (both stacks alike, by
D14's formula) and the time guesses, never the treatment of one stack. `seed.txt` is revealed after the A/A.

**Outcome classes:** v1's twelve rows (`stop: alias drift`, `stop: security`, `stop: auth`, `void: contaminated`,
`void: harness`, `void: harness (interrupted | disposed)`, `killed: cap`, `void: rate limit`, `void: upstream`, `killed: wall`,
`killed: stall`, `completed`, `crashed`), same precedence. A `killed:` session is a data point: its cost is censored at the
cap (`≥`), its quality is what the workspace holds. A void is re-run once (§4.2).

**Primary measures per session:** `accepted`; `percent`; `defects`; cost (headline recompute); wall; `model_api`;
`active − model_api`. **Per task:** the three pair ratios C#/TS of each measure (a bound when a side is censored), their
median, `accepted` count per stack of 3, "C# > TS in k of 3 pairs". **Over the tasks:** the ten task medians per measure;
the count of tasks with median ratio > 1; the `accepted` totals per stack of 30; per kind (olympiad 4, daemon 3, web API 3)
the same counts. Every line is given with and without pairs holding a `ran unpaired` session or a `foreign load` flag.

**What this licenses:** per task, "in k of 3 pairs" and whether ranges overlap (overlap = not distinguished). Over the
tasks, counts: "C# cost above TS in k of 10 task medians", "accepted 27 of 30 against 24 of 30". The ten tasks are a chosen
set, not a sample, so no p-value, interval or effect size is a finding; whether a pre-registered sign test over the ten task
medians is reported as a secondary line is open (Q6). Nothing licenses a claim about other tasks, models, CLI versions or
the languages in general.

**Prediction (draft, to be written verbatim into `prediction.md` in E4.S3 before the pilot; the pilot may amend the cost
and time ranges by a dated line, nothing else):**

1. Correctness: `accepted` differs between the stacks on at least 2 of the 10 tasks; over 30 sessions each, the stacks are
   within 4 accepted of each other. Basis: the traps are balanced by design (D3); v1 saw no quality difference at all.
2. The traps fire: at least 6 of the 20 (task, stack) trap sets catch at least one of their three sessions. Basis: the traps
   were chosen because agents fall into them; a suite where nothing fires measures nothing (the v1 ceiling).
3. Cost: C#/TS task median in 0.8–1.3 on 8 of 10 tasks; the daemon and web tasks pull C# above 1 (more code per feature,
   more build loops), the olympiad tasks do not. Basis: v1's 0.92 on a tiny task; the C# project and build overhead grows
   with the number of build-test loops, which the harder tasks multiply.
4. Time: `active − model_api` C#/TS > 1 on 9 of 10 tasks (v1: 3 of 3 at 1.70); `model_api` C#/TS in 0.8–1.2; wall C#/TS
   in 0.9–1.4.
5. Surprises named in advance: TS accepted fewer than C# by 4 or more; any task where no session of either stack is accepted
   (a floor, not a difference); a task where every session of both stacks is accepted (a ceiling, as v1); a session killed
   at the 75 min wall.

## 10. Threats to validity

| # | Threat | Control, and the sentence the report carries |
|---|---|---|
| T1 | **Contamination of the known tasks.** Five tasks are public problems with public solutions in many languages; the model may recall a solution from training. The recall is not equal across stacks: more TypeScript and JavaScript solutions of CSES and RealWorld exist than C# ones | No network, no id or title in the box (D4, D18, D19); the needles are read by hand; the record reports `accepted` for known and original tasks apart. *"A known-task result compares recall as much as reasoning, and the recall is not symmetric."* |
| T2 | **One-sided traps.** A task whose traps fire only in one stack measures the trap, not the stack | Every task names traps on both sides (§4.1); the trap coverage table (E2.S2) shows the count per stack per task; two consultant-rejected tasks (CSES 1712, 1736) were dropped for this reason (D3). *"Trap counts per stack are published; where they differ, the task's result is read with that difference."* |
| T3 | **n = 3 per task.** Three pairs cannot separate a small difference from noise; v1's within-condition spread on a one-minute task was already wide | Paired concurrent design, ranges, "in k of 3 pairs"; the ten task medians give a second reading over tasks (§9). *"Differences smaller than the within-task range are not distinguished; the over-tasks count is descriptive."* |
| T4 | **The agent's framework knowledge differs per stack.** Minimal API and Fastify are not equally represented in training data; TypeBox and `AddValidation()` (new in .NET 10) less so | The pieces are at the same level of abstraction (§4.5) and the rationale is public; `PACKAGES.md` names them to both; the record shows per-task tool and build loops. *"A web-API difference is a difference between agent-plus-framework pairs, not between languages."* |
| T5 | **Limits favouring one runtime.** A tight time or memory limit rejects the runtime with the larger baseline (JIT warm-up, V8 heap) | D14: limits from both references, 3 × and 2 ×, floors; G-17 re-checks on the series machine; per-case runtime recorded. *"No limit is below 3 × the slower reference or 2 × the larger reference peak."* |
| T6 | **Harness-induced noise.** v1's 18 instrument defects voided sessions of one stack at a time (RESULTS §11); longer sessions, drivers and cgroups add new failure modes; a driver hiccup reads as a product defect | The scan's fixes carried; every void replayed and reported; drivers and judge hand-checked (G-15, G-16); a scenario failure is adjudicated blind as product / ambiguity / harness (§4.3); `scored under load` flagged. *"Every void is named with its cause; harness defects are classed apart."* |
| T7 | **Statement ambiguity in the originals.** Two correct readings give two correct programs and one failing suite | Two references written apart agree on every case before sealing (E2.S1); blind adjudication; the revised score beside the primary. *"Cases classed ambiguous are dropped for both stacks in the revised score."* |
| T8 | **The model moves under a long series.** Three rounds take days; a model change behind one id is invisible | v1's alias record (D12) stops on a new id; rounds interleave the tasks so a change hits both stacks and every task alike; the date of every session is in the record. *"A change behind the same model id is not controlled."* |
| T9 | **Prompt-cache warmth and CPU contention.** The two cells of a pair share the machine and the cache state | Same-tick start; masks swapped per pair; the quiet check; load flagged; first-turn cache read per session. *"Warmth and load meet both stacks of a pair alike; flagged pairs are shown apart."* |
| T10 | **Floor and ceiling effects.** A task nobody passes or everybody passes separates nothing (v1 was at the ceiling) | The difficulty ladder of §4.1; prediction 5 names both cases; `percent` and `defects` carry information where `accepted` is at a floor. *"A task at a floor or a ceiling is reported as such and does not count as a difference."* |
| T11 | **Time partition limits.** One Bash chain takes its highest category (v1's V16); longer sessions have more chains | The mixed-chain count per session; `build_test` against `other_tool` descriptive only; the primary time measures are wall, `model_api`, `active − model_api`. |
| T12 | **The time guesses and the cap.** A cap derived from three piloted tasks may cut a session of a task the pilot did not run | §12's formula scales by the guess ratio and sets `capMax` from the ceiling; a `killed: cap` session is a censored data point, never dropped; the report counts them per stack. *"Capped sessions are reported as bounds."* |
| T13 | **Public repository, secret or held-out leak.** A link in a workspace or a case in a sample would reach the agent or the public | D2's rules; `leakcheck` on every push; `workspace-diff`; the needles. |

## 11. Pilot and A/A (D16)

Void spend, before any data pair: the A/A (two identical C# sessions of task 3, concurrent), then the pilot pairs of tasks
1, 3 and 10 (C# ∥ TS each). GO = every row of v1's G-1 … G-14 holds (G-6 may be `explained`, G-9 never gates, the two
`not exercised` cases as v1 with their replacing evidence), plus:

| Id | Must show | A miss means | Then |
|---|---|---|---|
| G-15 | the olympiad judge by hand: one C# and one TS pilot tree, run by hand on three held-out cases, give the judge's verdicts and times within 10 % | the judge misreads a real tree | fix, re-score (no re-run) |
| G-16 | the daemon and web drivers by hand: the pilot's task-10 trees driven by hand through one scenario each give the driver's replies; the RSS curve by hand (`ps` every second) agrees within 10 %; SIGTERM by hand ends the process as the driver says | a driver misreads a real program | fix, re-score |
| G-17 | both references of every task pass 100 % under the registered limits **in the scoring box on the series machine**, under the quiet check; each mutant fails exactly its cases | a limit rejects one runtime's baseline, or a trap is not caught | recompute the limit by D14 (dated, both stacks) or fix the suite; before round 1 |
| G-18 | the cap derivation (§12) from the pilot's settled costs, written as a dated amendment, and the stall timeout observed as larger than the longest quiet gap seen in any pilot session | the cap or stall would cut sessions the task needs | set them from the pilot; before round 1 |

Stops as v1: G-2, G-8, G-12 refuted and alias drift stop the series until fixed and an A/A is GO.

## 12. Budget (D7)

The ceiling is **$1000** for v2's paid runs: pilot, A/A, data, voids and re-runs, counted on separate lines. The spend gate
before every pair: settled spend + the live O sum of running sessions + 1.1 × Σ caps of the two sessions about to start ≤
$1000 (v1's `spendGate`, with the bare budget line replaced by the one ceiling). The ceiling cuts whole rounds (D10); the
report says "round 3 did not run" and reports the tasks at n = 2.

**The cap, derived from the pilot (D4, G-18), pre-registered as a formula:** per task, `cap = min(2 × the larger settled
cost of its pilot pair, capMax)` for the piloted tasks; for the others, the piloted task of the nearest guess scaled by the
ratio of guesses; `capMax` is set so that 1.1 × Σ over the 60 sessions of their caps ≤ $850 (the ceiling less the pilot, A/A
and a re-run reserve of ≈ $150). If the pilot's costs make `capMax` smaller than 1.5 × any pilot cost, the operator decides
between fewer rounds and a smaller task set **before** the pre-registration is amended.

**Projection, as a guess only:** v1's one-minute sessions cost ≈ $0.35 each; v2's sessions run 5–75 min with many build
and test loops. At $5–20 per session the data series costs $300–1200, so the ceiling may bind at round 3 of the long tasks;
the pilot replaces this guess.

## 13. Growth

| Surface | Size at this plan's volumes | Retired by / interrupted |
|---|---|---|
| Session dirs `~/rsd/lang-v2/<series>/s/<rid>/` with seeded caches (NuGet with Hosting and JwtBearer, npm with Fastify ≈ 150–300 MB each), the 75-minute stream and transcripts (≈ 50–150 MB), the scoring copy | ≈ 0.4–0.6 GB per session; 60 data + ≈ 8 pilot/A/A + re-runs ≈ 75 sessions ≈ **30–45 GB** on `/home` (745 GB free on 2026-10-30); the pilot's measured size replaces this estimate | `bin/ obj/ node_modules/` shed after scoring (safe-rm under the series root, by name); the rest kept until E6's archive and `retire.mjs` after the record is promoted |
| Toolchains `~/rsd/lang-v2/toolchains/` (SDK ≈ 1 GB, Node ≈ 100 MB, TS, the CLI copy 292 MB), two cache snapshots (≈ 0.5–1 GB) | ≈ 3 GB | kept for the series; `retire.mjs` |
| Held-out source, references, mutants `~/rsd/lang-v2/{heldout-src,refs,mutants}/` | MBs | kept (the operator's record); never in git |
| `spikes/lang-bench/heldout/suite.enc`, `inputs/*`, `research/lang-v2/data/*.json` | a few MB, committed | kept |
| The public task repository | small; references added after the series | kept |
| Scoring cgroups and the clear suite in the scoring temp dir | one per scoring run | removed by the scorer's finalizer; `heldout-tool.mjs sweep` for leftovers older than 24 h |
| In-flight states | `launching` sessions, a scorer run, a daemon left by a driver | `retireStale` at every `run`/`resume`; the wall (75 min), the scorer's outer limit (set per kind, ≤ 90 min) and the quiet wait (30 min) bound every non-terminal state; the janitor ends any process carrying the cell's marker |

## 14. Build order

1. **Task repository (E1):** create `dew_flow_ai_language_comparison` (the operator; public; conventions pinned); the ten
   statements and samples; the workspaces with `build.sh`, `run.sh`, pins, `PACKAGES.md`; the Node stack rationale
   (§4.5) in its README; toolchains pinned under `~/rsd/lang-v2/toolchains/`; the two cache snapshots.
2. **References and suites (E2):** two references per task; the held-out suites, mutants and the trap coverage table;
   sealed; limits computed by D14 on the series machine; `leakcheck` clean.
3. **Harness (E3):** `spikes/lang-bench/`; drivers and judge; the runner's task dimension; D15's env; the widened
   categories and records; G-15 … G-18 in `aaVerdicts`; stub tests; a ≤ $0.20 smoke session on task 1.
4. **Pre-registration (E4.S3)** committed and tagged before the A/A; then the A/A, the pilot pairs, the cap derivation and
   amendments, the seed reveal.
5. **Series (E5):** three rounds under the gate.
6. **Record (E6):** `research/lang-v2/RESULTS_<date>.md` generated from the records; blind adjudication; the references and
   the trap table published; archive, retire; this plan promoted with its deviations.

## 15. Test plan

`node spikes/lang-bench/run-tests.mjs` (v1's runner shape: lowered priority, bounded workers; `BENCH_REQUIRE_SANDBOX=1`
turns host skips into failures).

| Id | Guarantee |
|---|---|
| T-L1 | the task repository fetch: a workspace file not matching `hashes-lang.txt` is refused; `workspace-diff` passes on the ten task pairs and fails on a planted extra file |
| T-L2 | the v1 workspace of a session holds exactly the task folder's files (no root files, no other stack) |
| T-L3 | the olympiad judge: fake programs that print wrong output, sleep past the limit, allocate past the limit, exit non-zero, each get their class; a right one passes; peak memory comes from `memory.peak` when present, from samples otherwise, never from `RLIMIT_AS` |
| T-L4 | the process driver: a stub daemon that ignores SIGTERM is killed after the grace period and reported `stop failed`; one that exits 0 passes; a stub that leaves a child is reported `leftover`; RSS samples are every second ± 100 ms |
| T-L5 | the TCP driver against a stub RESP server: partial frames, pipelined commands, a slow reader (backpressure) each scored by the scenario's expectation |
| T-L6 | the HTTP driver: a scenario with k concurrent identical requests checks the invariant once; a 5xx or a timeout is `fail`, never `not run` |
| T-L7 | limit calibration: given two reference measurements, D14's formula gives the registered limits; a reference failing under them is a calibration failure, not a passing suite |
| T-L8 | trap coverage: every mutant fails exactly its registered cases and no other; both references pass every case; a trap with no catching case is a failure of the table |
| T-L9 | the order: three rounds, every task once per round, seeded; rids distinct over task, stack, pair and attempt (T-R1 widened); masks swap per pair |
| T-L10 | the spend gate with the derived caps and the one ceiling; the round rule refuses a task's third pair while another task has run once |
| T-L11 | the record has every per-kind field known, `not captured` with a reason, or `n/a (<kind>)` |
| T-L12 | the contamination needles for the known tasks hit in tool inputs and outputs and are reported for a hand reading, never auto-voided |
| T-L13 | offline caches (effect, in a box): with the registries refused by the proxy, `npm install fastify@5.12.7` and `dotnet restore` of the pinned project succeed; a package outside the set fails naming it |
| T-L14 | D15's env keys are in the box and in the scoring box and in the record; `agentEnv` refuses a clash in any case |
| T-L15 | the classifier: `bash build.sh`, `bash run.sh`, `dotnet publish`, `node --test`, the drivers' commands land in `build_test`; the chain rule unchanged |
| T-L16 | `aaVerdicts` G-15 … G-18: each broken fixture refutes exactly its G; G-17 with one reference over a limit is NO GO |
| T-L17 | the record render: the generated blocks come from `data/*.json` only; a typed number is a failure; the licensing paragraph of §9 is present verbatim |

Live: the smoke session (E3.S5), the A/A and pilot (E4), the by-hand G-15/G-16 lines, the round-1 read-back of one pair.

## 16. Definition of Done

- [ ] Every input but the stack is pinned in the harness; one session per stack in round 1 was read back by hand to prove
      the arms differ only in the stack (measurement rule 1).
- [ ] The judge, the drivers and the suites were hand-checked (G-15 … G-17) before their numbers appear (rule 2).
- [ ] `prediction.md` was committed before the pilot and `seed.txt` after the A/A; the report puts observation beside
      prediction (rule 4).
- [ ] T1–T5 held for every data session, or the breach is in the report with what was done.
- [ ] Every cost carries its basis; pilot, A/A, data and void spend on separate lines; spend ≤ $1000 or the stop and the
      missing rounds named.
- [ ] Time is partitioned (Σ = wall); no span sums; scoring time apart from agent wall.
- [ ] Every failing held-out case was classed blind; the trap coverage table and the references are published after the series.
- [ ] The report names the task repository sha, the harness identity, the prereg tag, the CLI version, the toolchain
      checksums, the cache hashes, the machine and the date (rule 6); its conclusion is no wider than §9.
- [ ] The public repository holds no secret, transcript, held-out case or link in a workspace (`leakcheck` clean on every push).
- [ ] `research/lang-v2/` and `research/README.md` updated; `research/module_minigit_bench.md` or its v2 sibling describes
      the extension; this plan promoted with its deviations; the worktree removed.

## Deviations

None yet. Filled at promotion: what shipped differently from this plan, and for every "impossible", what was CHECKED.

## 17. Open questions

Not settled by this plan; the operator or E1 decides. No answer is invented here.

- **Q1 Licences and credit.** The licence of the new public repository (our originals, the workspace templates), and the
  terms under which the known problems may be paraphrased: CSES statements, IOI 2018 Werewolf (the IOI's task licence to
  be read), RealWorld's spec (MIT), Redis's RESP (a protocol; the trademark). v1's D18 found upstream files with no
  licence; v2's credit text is written after these are read.
- **Q2 Who creates the repository and when.** The operator's account (`oleksandrdubyna88`) is assumed; public from day one
  is possible because no session has network — or private until the series ends. Also: is the task repository itself a
  `dew_flow_conventions` consumer with a `release` pin, or a snapshot branch as v1's D9?
- **Q3 Werewolf's I/O adapter.** IOI 2018 graded through a function interface. "Stdin/stdout adapter identical for both"
  can mean (a) the statement defines a stdin/stdout format and the agent writes everything, or (b) the workspace ships a
  frozen adapter per stack that calls the agent's function. (b) adds frozen code per stack to keep symmetric; (a) adds
  parsing work to the task. Which?
- **Q4 Memory limits in the box.** D14 assumes cgroup v2 `memory.max`/`memory.peak` is available to the scorer inside
  `bwrap` on this WSL2 host. If not, the fallback is RSS sampling with a kill above the limit, which is coarser; E3 checks
  this first and reports which one the series uses.
- **Q5 The cap and the stall timeout** are the pilot's numbers; §12's formula is a proposal. The operator confirms the
  formula or replaces it before the pre-registration.
- **Q6 Any inferential statistic?** §9 reports counts only. A pre-registered sign test over the ten task medians is possible
  as a secondary line with the "chosen set, not a sample" sentence; the operator says whether it is wanted.
- **Q7 The pilot's task set.** Tasks 1, 3, 10 at least (D4). Adding one web API task (4 or 8) would calibrate the HTTP driver
  on a real tree before round 1 at ≈ 2 sessions' cost; otherwise G-16's web half is exercised first in round 1.
- **Q8 Culture for the Rule API (task 4).** The C# trap needs the host culture `de-DE` in the **scoring** box (and in the
  agent box, so the agent can meet it?) — setting it in both is the honest treatment; setting it only in scoring is a trap
  the agent could not have seen. Which, and does the statement say the culture is not invariant?
- **Q9 Offered test frameworks.** xUnit v3 for C# and `node:test` for TS are proposed; `node:test` is built in while xUnit is a
  package. Is a C# package-free option (the agent writing a console test runner) acceptable as equal footing, or is this
  asymmetry simply recorded?
- **Q10 Re-pin of the CLI (D8).** The plan re-pins at pre-registration to the then-current release. If the operator prefers to
  keep 2.1.310 for comparability with v1, say so before E4.S3.
- **Q11 Publication of references.** After the series, in the public repository (proposed) — or only in the record?
- **Q12 Rounds against tasks when the ceiling binds.** D10 cuts round 3 whole. The alternative — finishing the cheap tasks'
  third pairs — gives n = 3 on some tasks and n = 2 on others. D10 is the proposal; the operator confirms.

## Epics

Six epics on the build order of §14, each one branch from the previous epic's commit, one reviewable diff and a usable state
at its end. Stories are written one epic at a time into `todo/PLAN_lang_comparison_v2_E<n>.md` when that epic starts
(`common.planning-docs` § *A plan split into epics and stories*); that document links back here. The intended stories
with their acceptance criteria are listed per epic below so the size is visible. Cornerstone areas are named per epic; the
concrete marks are confirmed by the epics' stories documents, at most Q = ⌈2·E/5⌉ = **3** for E = 6, at most one per epic.
The three areas ranked first: (1) the original tasks' statements — an ambiguous statement makes every failure
unadjudicable (E1); (2) the held-out suites with the trap coverage table and the limit calibration — the acceptance
instrument of the whole experiment (E2); (3) the daemon and web drivers with RSS and SIGTERM — a new instrument v1 never
had (E3). The pilot's go/no-go (E4) gets a strict spec without a mark, as v1's E3 did. Nothing here or in a stories
document names a held-out case, mutant or reference in clear.

Cornerstones: 3 of 3 — E1.S2, E2.S2, E3.S2

### E1 — The task repository and the toolchains (§14 step 1 · 5 stories)

- **Goal:** ten workspaces an agent can start in, identical across stacks but for the stack, on pinned toolchains and
  offline caches, in a public repository that leaks nothing.
- **Scope:** D2, D5, D6, D11, D12, D19; §4.1 statements and samples; §4.5 packages and toolchains; §5 layout.
- **Depends on:** the operator creating the repository (Q2) and the licence answers (Q1).
- **Done when:** T-L1, T-L2, T-L13 green; `workspace-diff` clean on ten tasks; both snapshots hashed; the preflight passes
  inside a box on SDK 10.0.403, Node 26.13.0, TS 7.0.2; the README carries the credits and the Node stack table; `leakcheck`
  clean.
- S1 — repository skeleton: layout of §5, conventions pin, README with credits and the §4.5 table, LICENSE (Q1), the
  `workspace-diff` check. *Accepted when:* the check passes on an empty task pair and fails on a planted difference; the
  README's links appear nowhere under `csharp/`, `node/` or `tasks/*/STATEMENT.md` (a test greps).
- S2 — **the ten statements** (cornerstone area: the five originals): stack-neutral `STATEMENT.md` per task with limits,
  I/O contract, start contract, frozen list, "hidden tests exist"; known tasks paraphrased without id, title or site;
  samples. *Accepted when:* two readers (an Opus judge and a consultant) list no open question on any original statement, or
  every one is resolved by a text change; no statement names a trap; the known-task needles of D18 find nothing in any
  statement.
- S3 — the workspaces: `build.sh`, `run.sh`, `PACKAGES.md`, pin files per stack, the C# `.csproj` skeleton and `nuget.config`
  offline source, the Node `package.json`/lockfile/`tsconfig.json`/`.npmrc offline`. *Accepted when:* in a box with no
  registry, each stack's empty workspace builds (`bash build.sh`) and `run.sh` starts a hello program; T-L2.
- S4 — the package sets and cache snapshots: the recipes, the exact pinned sets with hashes in the cache manifest,
  `PACKAGES.md` generated from the manifest. *Accepted when:* T-L13; the manifest lists every package and version; a
  package outside the set fails naming it; both hashes in `inputs/cache-snapshots-lang.json`.
- S5 — toolchains pinned: SDK 10.0.403 and Node 26.13.0 installed from official artefacts into `~/rsd/lang-v2/toolchains/`,
  read-only, checksums recorded; the preflight pins. *Accepted when:* the preflight refuses a box whose `dotnet --list-sdks`
  or `node --version` differs; the checksums are in `inputs/stacks-lang.json`.
- **Cornerstone area:** the original statements (S2).

### E2 — References, held-out suites, trap coverage and limits (§14 step 2 · 4 stories)

- **Goal:** the acceptance instrument calibrated before any model touches a task: two references per task, suites that catch
  every named trap, limits that reject neither runtime's baseline.
- **Scope:** D13, D14; §4.3 calibration; the sealed archive; `leakcheck`.
- **Depends on:** E1.S2 statements, E1.S5 toolchains.
- **Done when:** T-L7, T-L8 green; G-17's conditions hold on the series machine (recorded, re-checked at the pilot); the
  archive sealed with hashes and counts in `inputs/heldout-prereg-lang.json`; `leakcheck` clean on the branch.
- S1 — references: one C# and one TypeScript reference per task, written from the statement only, by two different
  writers per task where possible. *Accepted when:* both pass every sample and every held-out case; every disagreement
  between them was traced to the statement and fixed there (a dated list).
- S2 — **the held-out suites and the trap coverage table** (cornerstone): cases per task (counts registered), the
  table task × stack × trap → cases, mutants one per trap per stack. *Accepted when:* T-L8; every trap of §4.1 has at least
  one catching case; the table and the mutants' ids are sealed; no case appears in git in clear.
- S3 — limits by D14: reference measurements in the scoring box, the formula applied, the limits registered per task.
  *Accepted when:* T-L7; both references pass under the limits in three consecutive runs under the quiet check.
- S4 — adjudication packets for v2: v1's `adjudicate.mjs` pointed at the v2 series with the stack masked. *Accepted when:* a
  synthetic series produces packets that show case, output and statement sentence and nothing of the stack.
- **Cornerstone area:** the suites and the trap table (S2).

### E3 — Harness extension: long sessions, drivers, judge, scoring per kind, the task dimension (§14 step 3 · 6 stories)

- **Goal:** one command runs one pair of any task under the v1 rules, scores it by its kind, and leaves a record the report
  can be built from; v1's instruments untouched but for named widenings.
- **Scope:** §6; D10, D15, D17, D18; §11's G-15 … G-18; the smoke.
- **Depends on:** E1 workspaces and caches; E2's sealed archive for the stub tests' shape.
- **Done when:** T-L3 … T-L6, T-L9 … T-L12, T-L14 … T-L16 green; a stub pair of each kind runs end to end in real boxes;
  the smoke session (task 1, TS, ≤ $0.20) is read by E/O/T/B with T1–T5; `research/module_minigit_bench.md` (or a v2
  sibling) describes the extension.
- S1 — the task dimension of the runner: `ridOf` with task and `single` phase, the round order from the seed, the pair
  without barrier, the void schedule without v2 rows, the prereg guard's v2 list, `EGRESS_HOSTS.lang`. *Accepted when:*
  T-L9, T-L10, T-L12; `run --dry-run` prints three rounds.
- S2 — **the process, TCP and HTTP drivers with RSS sampling and the SIGTERM check** (cornerstone), in a scoring cgroup
  (Q4 answered first). *Accepted when:* T-L4, T-L5, T-L6; a driver never leaves a process (the janitor finds none after each
  test).
- S3 — the stdin/stdout judge with cgroup limits and verdict classes. *Accepted when:* T-L3.
- S4 — scoring per kind in the pipeline, `accepted`/`percent`/`defects` from the trap table, scoring time recorded.
  *Accepted when:* T-L11; a stub tree of each kind gets a `score.json` with every step known.
- S5 — D15's environment, the widened classifier, `aaVerdicts` G-15 … G-18, the smoke session. *Accepted when:* T-L14,
  T-L15, T-L16; the smoke's record is complete and its cost is in the ledger.
- S6 — the module document: the extension described (entry points, exit codes, the drivers' contracts, the judge's classes).
  *Accepted when:* every command in it runs as written.
- **Cornerstone area:** the drivers (S2).

### E4 — Pilot and pre-registration (§14 step 4 · 4 stories)

- **Goal:** the analysis fixed in public history before the first paid task session; the instruments proven on real trees;
  the cap derived; the seed revealed.
- **Scope:** §9, §11, §12; D4, D8 (the CLI re-pin), D16.
- **Depends on:** E1–E3 complete.
- **Done when:** the tag `prereg/lang-v2-<date>` exists before the A/A's first launch; `AA.md` says GO; the pilot pairs ran;
  the cap, stall and limit amendments are dated lines; `seed.txt` revealed; pilot spend in the ledger.
- S1 — the day-1 price re-read and the CLI pin copy. *Accepted when:* `prices-<date>.json` dated that day; the CLI copy's
  sha256 recorded.
- S2 — `prediction.md` (§9's draft, finalised), `statement.md`, `analysis-contract.md`, the outcome table, the limits, the
  package sets, the hashes, `seed.sha256`. *Accepted when:* v1's prereg tests widened pass (verbatim-from-main, derived not
  typed, seed as hash only, no held-out needle).
- S3 — the commit and the tag, before any paid session. *Accepted when:* the guard peels the tag to the commit.
- S4 — the A/A, the pilot pairs (tasks 1, 3, 10; Q7), G-1 … G-18, the cap derivation (§12), the amendments, the seed reveal.
  *Accepted when:* GO; every amendment is instrument-only or applied to both stacks; the cap formula's inputs are in the
  ledger.
- **Cornerstone area:** none marked (the go/no-go table is written strictly, as v1's E3).

### E5 — The paid pairs (§14 step 5 · 2 stories)

- **Goal:** the 30 pairs run under the pre-registered contract, in three rounds, under the gate.
- **Scope:** §4.2 order, §9 voids and re-runs, §12's gate and stop rules, the round-1 read-back.
- **Depends on:** E4's GO and seed.
- **Done when:** three rounds ran, or the gate or a stop ended the series and the record names the missing round; every
  session has `record.json`, every pair its pair record; the read-back line is dated.
- S1 — rounds 1–3 with the stop rules; resume after any halt. *Accepted when:* every session is classed; voids re-run by
  the schedule; spend lines settled.
- S2 — the round-1 by-hand read-back of one pair (argv, read-back, binds, cache hashes, package list, env keys) by an Opus
  judge, as a dated line; a mismatch reclassifies through `handVerdicts`. *Accepted when:* the line exists before round 2.
- **Cornerstone area:** none.

### E6 — The research record (§14 step 6 · 3 stories)

- **Goal:** a generated record in `research/lang-v2/` that types no number, says what §9 licenses, and publishes what the
  series held back.
- **Scope:** §8's views, §9's tables, the blind adjudication, the replay of every void, the references and trap table
  published, archive and retire, promotion.
- **Depends on:** E5.
- **Done when:** every §16 box is checked.
- S1 — `research/lang-v2/tools/` from `research/minigit/tools/` widened to tasks and kinds; `data/<series>.json`;
  `RESULTS_lang_v2_<date>.md` with generated blocks. *Accepted when:* T-L17; the record renders from the data alone.
- S2 — the blind adjudication of every failing case; the voids replayed with the current scan; the prediction beside the
  observation. *Accepted when:* every failing case has a step-1 and step-2 verdict; the revised score sits beside the
  primary.
- S3 — publication (references and the trap table in the task repository, Q11), archive, `retire.mjs`, `research/README.md`,
  promotion of this plan and its stories documents with deviations. *Accepted when:* `plan-lifecycle.mjs` clean; the
  worktree removed.
- **Cornerstone area:** none.
