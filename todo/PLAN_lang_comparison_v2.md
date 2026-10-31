# PLAN — language comparison v2: C# / .NET 10 against TypeScript / Node 26 coding agents on ten harder tasks, bare and under the family's process

> Status: **plan only, nothing implemented yet; approved 2026-10-31 by the coai plan gate (PROCEED on `aa5e413`, its five
> findings applied in `8e1a39a` as the approved baseline); then changed twice the same day — 1.1 (D34, metrics are only
> added) and 1.2 (the spec-format contract the pilot spec 01 showed) — so the plan is stopped for re-review
> (`common.plan-change-control`, epoch 1 `stopped`): no new code starts on any epic, no merge carries its work, until the
> whole plan is re-reviewed and epoch 2 is approved. The one pilot artefact so far, the spec `01-descent-to-one.md`
> (`dew_flow_ai_language_comparison` `60e468a`, local), waits for the re-review's sorting.**
> Scope: a new public GitHub repository `dew_flow_ai_language_comparison` (gated and frozen task specs, task workspaces,
> visible samples, run bases of the process arm); a conventions parity audit and release in `dew_flow_conventions`
> (prerequisite, other repository); a new spike `spikes/lang-bench/` in this repository that imports the v1 harness
> `spikes/minigit-bench/` and extends it for two arms, long sessions, daemons and web APIs; shared black-box acceptance
> suites with a held-out part and raw records outside every git tree under `~/rsd/lang-v2/`; the series' record under
> `research/lang-v2/`.

## Plan changes

> Epoch 1 · approved 2026-10-31 · by reviewers · state: stopped

- 1.1 · 2026-10-31 · owner · contract — metrics are only added, never replaced or dropped; D34 and T-L26 (commit `d8da443`)
  (An owner's request counts exactly like one the agent finds. Every v1 metric stays in every v2 record beside the new
  ones; a v2 table may lead with a new metric and never omits a v1 one.)
- 1.2 · 2026-10-31 · agent · contract — the spec-format contract, found by the pilot spec 01 (`dew_flow_ai_language_comparison` `todo/tasks/01-descent-to-one.md`, `60e468a`): the format was underdetermined
  (One cause, three parts. (a) Limits in the spec: the spec states D14's floors — 1 s, 256 MB — and the 3 × / 2 × rule
  as guarantees; the exact values are registered at calibration and given to the runner, never to the author, so a spec
  never needs an amendment to carry them; §4.1, §4.3 and the prompts of §4.5 changed. (b) The visible-sample format is
  fixed now, `samples/<id>.in` and `samples/<id>.out`, and E2.S2's suite case format reads it, so it is no longer decided
  in E2.S2; E1.S6, E2.S2 and §14 changed. (c) Spec conventions adopted from the pilot: byte-exact output as the series
  default; stable requirement ids R1…Rn that held-out cases cite; header rows (kind, gate verdict, freeze tag, the frozen
  sentence, amendments) with "not yet gated" / "none yet" placeholders; no mention that a task is known; frozen-list
  wording for a stack-neutral spec; the 600 s build bound stated; invalid input "does not occur; unspecified; never
  tested" unless the task defines errors; no plan story ids in agent-visible text; the `⏎` glyph for sample bytes.
  This second change stopped the plan.)
  (Not an entry, recorded here so the diff is explained: §4.3's trap row 1 is a fact fix — the TypeScript trap "`n & 1`
  truncates to 32 bits" was wrong and is replaced by bitwise halving, dated "consultant claim 2026-10-31, refuted by
  probe". A corrected fact changes no scope, goal, contract or approach.)

### Re-review for epoch 2

Narrowed: no — both changes add (a metric-retention rule and a spec-format contract); no scope, Definition of Done, merge condition or release got smaller, checked against §9, §16 and the epics

| Work | Where | Verdict | Action |
|---|---|---|---|
| pilot spec 01 `todo/tasks/01-descent-to-one.md`, `60e468a` (E1.S2's first spec, not yet gated) | `dew_flow_ai_language_comparison`, local commit, no remote | — | verdict by an Opus judge after the re-review (rule step 5); the `—` stands until then, no code was built on any epic |
| D34 and T-L26, `d8da443` | `dew_flow_benchmark` main (merged in #124, `f369e66`) | — | plan text only, no code; it is entry 1.1, not work to sort |

## 1. The question, before any solution

Experiment v1 ([PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md), record
[research/minigit/RESULTS_bare_2026-10-30.md](../research/minigit/RESULTS_bare_2026-10-30.md)) compared C# and TypeScript
coding agents on the tiny minigit task. Its bare arm found: cost C#/TS median **0.92** (C# > TS in 1 of 3 pairs); wall
C#/TS median 0.93; `model_api` C#/TS 0.86; `active − model_api` (mostly build and test time) C#/TS **1.70**, C# slower in
3 of 3 pairs; **every scored session of both stacks was accepted** (22 of 22, lowest held-out percent 100 %). Every session
ran about one minute. The record's own threat section says it: *"On this task the comparison mostly measures how many model
tokens each stack's agent used, not how the toolchains behave at scale"* and *"Quality is at the ceiling: it does not
separate the stacks here"* (§12). v1's process arm was designed (its §4.2, §5.4, §6, §7.2, §8) and not run.

So v1 answers the cost question for a tiny task and does not reach the question people argue about: does one stack make a
coding agent produce **wrong programs** more often, and at what **cost per correct program**, when the task has traps the
compiler does not catch? The question of v2, and nothing wider:

> On ten tasks of three kinds (olympiad stdin/stdout, daemon, web API), each with the same functional requirements and the
> same black-box checks for both stacks, with **Claude Code** (Opus, effort `high`), how do **C# 14 / .NET 10** and
> **TypeScript 7 strict on Node 26 with Fastify** compare in **cost per correct solution** — and beside it success rate,
> tokens and decomposed time — (a) for a bare agent with the task prompt only, and (b) for the same task done end to end
> under this family's process (pinned conventions, coai gates, an Opus team lead, Sonnet coders, PR, CI, merge)?

It licenses a per-task reading (3 repeats per arm per stack) and a pre-registered overall reading over the ten tasks
(§9). It is not a claim about the languages in general.

## 2. What exists — verified 2026-10-31 at `de8479b` (this worktree's base)

v2 **reuses** the v1 harness and v1's process-arm design. The table names what is there and where; §6 says what is reused
as is, what must change, and what v1 still has to build.

| Fact | Where |
|---|---|
| The agent box: `bwrap`, root read-only, home/`/mnt`/`/run`/`/tmp` hidden, an explicit bind list, own network/pid/ipc namespaces, no `--setenv`; `sessionLayout(root, rid)`, `agentEnv({ layout, dummyToken, cellId, nodeDir, tsDir, extra })`, `agentBoxArgv(opts)` | `spikes/minigit-bench/lib/sandbox.mjs:72`, `:93`, `:181`; `research/module_minigit_bench.md` §5 |
| The CONNECT proxy with the SNI check (exact host, one `host_name` entry, ECH refused, bytes per tunnel); `EGRESS_HOSTS.bare` | `lib/egress.mjs`, `lib/tls-hello.mjs`, `lib/egress-hosts.mjs:11` |
| The credential broker: the real token never enters the box; `B` ledger; cap refusals as 429 | `lib/broker.mjs`, `lib/budget.mjs`; v1 D23 |
| `launchSession(given, deps, { signal })` — one session end to end, `meta.json` with `stopped` | `lib/session-launch.mjs:252` |
| Four cost views E/O/T/B and checks T1–T5: `sessionCost({ E, O, T, table })`, `crosscheck({ E, O, T, B, table, policy, cliVersion })`; policy `t1 stop`, `t3 stop` for bare data cells | `lib/session-cost.mjs:36`, `lib/crosscheck.mjs:260`, `inputs/crosscheck-policy.json` |
| Time: `spansFrom(...)`, `partition(wall, spans)` — the exclusive work-before-wait partition with the process categories `coai_gate`, `consultant`, `ci_wait`, `sleep300` already defined; primary time measures wall, `model_api`, `active − model_api` | `lib/spans.mjs:179`, `lib/timeline.mjs:68`; v1 §10 |
| Held-out suite sealed with AES-256-GCM, key a mode-600 file, clear only in a scoring temp dir; `heldout-tool.mjs seal\|calibrate\|leakcheck\|counts\|sweep`; the Python runner `runSuite` (`/usr/bin/python3`, `-I`), `heldoutScore`, `clearCases` | `heldout-tool.mjs`; `lib/heldout-run.mjs:19`, `:64`, `:107`, `:163`, `:180` |
| The scorer on a pristine copy, offline from seeded caches, bounded steps (`buildS 600`, `perInvocationS 10`) | `score-session.mjs`; `lib/limits.mjs:3`; `lib/score-steps.mjs` |
| The bare runner: `ridOf`, `maskFor` (masks `0-5` / `8-13`, swapped per pair), `promptFor`, pair phases, the barrier, the outcome table (12 rows), the schedule of voids and the one re-run, resume, records | `bare-runner.mjs`; `lib/bare-plan.mjs:12`, `:28`, `:40`; `lib/bare-outcome.mjs:124`; `lib/bare-schedule.mjs`; `lib/bare-record.mjs` |
| The spend gate: `spendDecision(spent, ceiling, caps, { liveUsd, factor })`, `spendGate({ ledger, liveUsd, caps, ceilingUsd, bareBudgetUsd })` | `spikes/policy-ab-quick/lib/spend.mjs:18`; `lib/spend-gate.mjs:32` |
| The machine-quiet check, the CPU sampler, the spin probe; `quietVerdict`, `waitForQuiet` | `lib/cpu-sampler.mjs:120`; `lib/quiet.mjs`; `inputs/quiet.json` |
| The pre-registration guard and harness identity; the tag `prereg/minigit-bare-20261030`; the commit-reveal seed | `lib/prereg-guard.mjs`; `lib/seed.mjs`; `inputs/seed.sha256`, `inputs/seed.txt` |
| The A/A checks G-1 … G-14 and `aaVerdicts` | `lib/aa-verdicts.mjs:39`; [PLAN_minigit_stack_bench_E3_bare.md](PLAN_minigit_stack_bench_E3_bare.md) E3b.S4 |
| The contamination scan and its 18 instrument defects, 3 of which voided sessions of one stack (defects 1, 14, 16) | `lib/contamination*.mjs`, `lib/session-scan.mjs:80`; RESULTS §11 |
| Cache snapshots per stack, hashed, seeded into every box; `makeSnapshot`, `seedCaches` | `lib/cache-snapshot.mjs:162`, `:58`; `inputs/cache-recipe.json` |
| The generated research record: `extract.mjs` → `data/<series>.json`, `render.mjs` rewrites `<!-- generated:… -->` blocks, `replay.mjs` | `research/minigit/tools/` (11 files, 1402 lines) |
| **v1's process arm, designed and NOT built:** coai in its own box behind the stdio↔socket bridge (E2.S3), the one-source templates, snapshot branch, rulesets, PATs, tag (E2.S4), the process runner with blocks and the arm-lead draw (E3.S1), the 17-item compliance checker over GitHub, `coai.db` and the stream with its hand-check protocol (E3.S2), P0 and the model smoke (E3.S3), P1 (E3.S4) | `research/module_minigit_bench.md` § *What it does not do yet*; v1 §4.2, §5.4, §6, §7.2, §8 |
| v1's rule-asymmetry measurement: for `--task implement --task test --task git --task pr`, C# gets 34 rules / 331,833 B, TS 32 / 324,781 B (run 2026-10-29) | v1 §12 V1 |
| v1 pins: claude **2.1.310**, node 26.12.1, dotnetSdk 10.0.114, typescript 7.0.2; the TS toolchain read-only under `~/rsd/minigit/toolchains/typescript-7.0.2`, the CLI copy `claude-2.1.310` | `inputs/stacks.json`; `ls ~/rsd/minigit/toolchains` |
| Host today: claude 2.1.310, dotnet SDK 10.0.114 (apt), node v26.12.1; `/usr/bin/python3` is v1's held-out runner | `claude --version`, `dotnet --list-sdks`, `node --version`, `lib/heldout-run.mjs:19`, 2026-10-31 |
| Release indexes, read 2026-10-31: .NET 10 `latest-sdk` **10.0.403**, `latest-runtime` 10.0.14, released 2026-10-25, support phase active; Node `v26.13.0` 2026-10-28, lts "Lithium"; v26.12.1 is not marked LTS; v27.0.0-alpha.0 exists | `dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json`; `nodejs.org/dist/index.json` |
| npm, read 2026-10-31: fastify 5.12.7, @fastify/jwt 10.2.6, @fastify/type-provider-typebox 6.3.0, @sinclair/typebox 0.34.52, pino 10.4.0, typescript 7.0.2, express 5.3.0, @nestjs/core 12.3.2, hono 4.13.14 | `npm view <pkg> version` |
| ASP.NET Core 10 Minimal APIs have built-in validation: `builder.Services.AddValidation()`, DataAnnotations on query, header and body, a source generator, 400 on failure, `.DisableValidation()` per endpoint; the APIs live in `Microsoft.Extensions.Validation` | Microsoft Learn, *What's new in ASP.NET Core 10.0*, read 2026-10-31 (**verified**) |

What does **not** exist: a task other than minigit; a task spec gated and frozen; a driver that starts a program, talks to it
over TCP or HTTP, samples its memory and stops it with SIGTERM; a judge for stdin/stdout tasks with time and memory limits;
a session longer than 60 min; a package cache that holds a web framework; references in two languages per task; the public
task repository; a parity audit of the C# and TypeScript rules in `dew_flow_conventions`; any process-arm code.

## 3. Decisions

Rows marked "operator, 2026-10-31" are the operator's decisions of that day, recorded as given (D1–D8 from the morning,
D20–D31 from the afternoon's instructions A–G). Rows marked "team lead, 2026-10-31, from the coai plan gate" settle
questions the gate round on `aa5e413` closed (D32, D33). Rows marked "this plan" are proposals for the operator's approval;
the open questions (§17) name what they do not settle.

| # | Decision | Who, when |
|---|---|---|
| D1 | **v2 compares C# and TypeScript coding agents on 10 harder tasks**, not the tiny minigit task. v1's finding (cost C#/TS ≈ 0.92, C# slower only in build and test time, every session accepted) motivates harder tasks with **runtime traps**: defects a compiler does not catch, which show in held-out tests | operator, 2026-10-31 |
| D2 | **A new public GitHub repository `dew_flow_ai_language_comparison`** holds the task workspaces: `csharp/` and `node/` (one sub-folder per task in each), `tasks/` (statements, visible samples, reference notes), and the pinned conventions as in the other `dew_flow` repositories. **Held-out tests and raw records stay outside git** (as v1). Because it is public: no secrets, no raw transcripts, no held-out content; known-problem statements are **paraphrased with credit and a link; the link only in the repository README**, never in the agent's prompt or workspace. The bench harness stays in `dew_flow_benchmark` | operator, 2026-10-31 |
| D3 | **Tasks: 5 known + 5 original; 4 olympiad, 3 daemons, 3 web APIs; difficulty from a few minutes to about one hour of agent work.** Traps are runtime traps on both sides across the suite. The list is §4.1 (order = expected difficulty; the times are guesses, calibrated by the pilot). Daemons and web APIs are scored by a harness that starts the program, drives held-out scenarios and checks replies, RSS over time and a clean SIGTERM stop. Consultant review on 2026-10-31 (codex gpt-6-astra, gemini) shaped the list: CSES 1712 dropped (a one-sided JS trap), CSES 1736 dropped (sums stay below 2^53 at CSES limits, ≈ 4e15, so its JS trap does not fire); gemini's claim that .NET console apps default to Server GC was **rejected** (they default to Workstation GC) — still, the harness fixes GC settings explicitly per stack and records them | operator, 2026-10-31 |
| D4 | **Method rules from the consultants, adopted:** the agent never sees a CSES/IOI id or title (paraphrased statement only); workspace samples are small and clean and the traps live in held-out tests, so the first-submission pass rate can show a difference; time and memory limits are calibrated on reference solutions in **both** languages, so no limit rejects one runtime's baseline footprint; a **paid pilot (tasks 1, 3, 10 at least)** measures real times before the full series; the bare session wall limit rises from v1's 20 min to **about 75 min**; v1's $6 per-session cap is **re-derived from the pilot** | operator, 2026-10-31 |
| D5 | **Packages: option A — a pinned offline package cache per stack**, the same discipline as v1's cache snapshots; **the agent has no general network** (the process arm reaches GitHub, as v1's D21–D22) | operator, 2026-10-31 |
| D6 | **Stacks: both current LTS lines, latest patch, checked 2026-10-31 against the official release indexes.** C#: .NET 10, SDK **10.0.403** (released 2026-10-25; the host has 10.0.114, so it must be upgraded and pinned), runtime 10.0.14, C# 14 (the default for .NET 10; C# 15 ships with .NET 11, RC3 on 2026-10-31, not stable, so out). Web APIs use ASP.NET Core **Minimal API**; daemons the Generic Host (`Microsoft.Extensions.Hosting`); JWT `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x; tests the agent's choice from the cache (xUnit v3 offered). TypeScript: Node **26.13.0** (LTS "Lithium", released 2026-10-28; the host has 26.12.1, so it must be upgraded and pinned), TypeScript **7.0.2** with `strict` (v1's toolchain line). The Node web stack is the team lead's choice, with the reasons recorded (§4.6): **Fastify 5.12.7** with TypeBox 0.34.x (`@fastify/type-provider-typebox`), `@fastify/jwt` 10.x, pino, `node:test`, `node:net`. Toolchains are pinned in read-only copies with checksums (as `~/rsd/minigit/toolchains/`) and recorded in the pre-registration | operator, 2026-10-31 (stacks); team lead, 2026-10-31 (the Node web stack, reasons in §4.6) |
| D7 | **Budget: $1000** for the paid runs of v2. The spend gate enforces it. (Set for the experiment; with two arms the split between arms is re-derived from the pilot, D30) | operator, 2026-10-31 |
| D8 | **Same agent as v1: Claude Code (Opus, effort `high`), CLI pinned.** v1 pinned CLI **2.1.310** (`inputs/stacks.json`); v2 **re-pins at its pre-registration** to the newest CLI version installed on that day, copied read-only as v1's `init` does (today that is still 2.1.310); the v1 pin is not inherited. *Superseded in part by D20: v2 runs two arms, not the bare arm alone* | operator, 2026-10-31; the re-pin shape: this plan |
| D9 | **Unit of analysis = the task**, with repeated measures: per task, stack and arm, 3 sessions (D20). The 120 sessions are not 120 independent samples (D28) | this plan, per instruction E5 |
| D10 | **Order: six rounds, arms alternating.** A round runs every task once in one arm; the arm of round 1 is drawn from the seed and the arms alternate (B P B P B P or P B P B P B), so after any even number of rounds both arms hold the same n; the task order inside a round is drawn from the seed; a task's second repeat in an arm runs only after every task has run once in that arm. The two cells of a pair (C# ∥ TS, same task, same arm) start in the same tick on masks `0-5` / `8-13`, swapped per pair as `maskFor` does; the two arms never overlap. The ceiling (D7) cuts whole round pairs (one bare + one process), never one task's repeats | this plan, per instruction E4 |
| D11 | **The bare agent's workspace is a copy of one task folder** (`csharp/<task>/` or `node/<task>/`) at a pinned commit of the task repository, fetched and hash-checked as v1's `fetchInputs` does. The repository's root files, the other stack's folder, `tasks/` and `todo/` are never in the bare box. The bare agent gets **no** conventions. **The process agent's workspace is a single-branch clone of its run base `run/<rid>/main`** in the task repository (v1 §4.2), which carries the conventions, the hooks, the frozen spec and both stack folders; its prompt names its stack's folder | this plan |
| D12 | **Fixed per-stack scripts in every workspace, frozen inputs with hashes:** `build.sh` and `run.sh` (olympiad: reads stdin, writes stdout; daemon and web API: the long-running program, `PORT` and the task's inputs from the environment). The agent fills the code; the harness drives only these two scripts, identically for both stacks. A `PACKAGES.md` lists the offered package set of the stack (D5); the spec tells the agent what is frozen | this plan |
| D13 | **Scoring per task kind** (§4.3): olympiad — held-out input/expected pairs under calibrated time and memory limits, each case `pass` / `wrong answer` / `time limit` / `memory limit` / `runtime error`; daemon and web API — held-out scenarios driven by the harness, RSS sampled every second, a SIGTERM stop that must end the process within a grace period with no child left. **Accepted** = clean build, every held-out case or scenario passed, memory and stop checks passed. One suite per task runs unchanged against both stacks (D22) | this plan |
| D14 | **Limits:** time limit per olympiad task = **3 ×** the slower of the two reference solutions' wall, with a floor of 1 s; memory limit = **2 ×** the larger reference peak, with a floor of 256 MB; the RSS bound of a daemon or web API task = **2 ×** the larger reference peak under the task's load scenario. The spec states the floors and this rule as guarantees; the exact values go to the runner, never into the spec (change 1.2a). Peak memory is read from the scoring cgroup's `memory.peak` where the host gives it, else from 100 ms RSS samples — never `RLIMIT_AS`. The numbers are computed by E2 and pre-registered; a change after the pilot is a dated amendment applied to both stacks | this plan |
| D15 | **GC and runtime settings, fixed and recorded per stack:** C# `DOTNET_gcServer=0` (Workstation, set explicitly), `DOTNET_TieredCompilation` default, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`; Node: no `--max-old-space-size` flag from the harness (the memory limit is the cgroup's); `NODE_OPTIONS` empty. Every value is in `agentEnv`'s allow-list, the same in the agent box and the scoring box, and in every session record | this plan |
| D16 | **The pilot's sessions are void (never data)**, as v1's A/A and pilot were; they fix the session caps per arm, the stall timeouts, the time guesses of §4.1, the limits of D14 and the budget split (D30). Bare pilot: tasks 1, 3 and 10 as pairs (C# ∥ TS) plus one A/A pair (two identical bare C# sessions of task 3). Process pilot: tasks 1 and 3 as pairs (v1's P1 conditions, §11). The go/no-go table is v1's G-1 … G-14 plus §11's G-15 … G-19 | this plan |
| D17 | **Egress:** bare sessions — no registry, no host; the only road out is the broker's API door. Process sessions — the GitHub hosts v1's E2.S4 settles, nothing else; package installs in both arms resolve from the seeded offline caches (D5). A refused CONNECT is flagged `wanted network`, not voided | this plan |
| D18 | **Contamination needles for the known tasks** (id, title, site names; `CSES`, `IOI`, `Werewolf`, `RealWorld`, `Conduit`, the public repository's name and URL) join the scan's needle list, in tool inputs and outputs. A hit is **read by hand**: the agent recalling a known problem from memory is a threat of the design (T1), not a void; a fetch or a path reaching outside the box is a void, as v1 | this plan |
| D19 | **Known-task statements name no source.** The link and the credit live in the task repository's README (D2) and in `tasks/<task>/NOTES.md`; the bare workspace holds the statement, the samples, `PACKAGES.md`, `build.sh`, `run.sh` and the stack's pin files, nothing else | operator, 2026-10-31 (D2); the file list: this plan |
| D20 | **Two arms, as v1's parent plan designed them.** **Bare:** 3 runs per task per stack on pure Opus with the task prompt only. **Process:** 3 runs per task per stack under our rules — the harness, the pinned conventions, the coai gates (plan gate, code gate, consultant), an Opus team lead and Sonnet coders. So each task has **6 repeats per stack**, 120 data sessions. v1's parent plan is reused for how the process arm is run and measured (§4.2, §6, §8); "bare first, process out of scope" is withdrawn everywhere | operator, 2026-10-31 (instruction A) |
| D21 | **Task specs are written by the team lead**, not by the agents under test. Each of the 10 specs goes through the **coai gates by all the rules, with a consultant**; the gated specs are stored in the new repository at **`todo/tasks/NN-slug.md`**; after the gate they are **FROZEN** (a tag `spec-frozen-<yyyymmdd>` and the hashes in this repository's pre-registration; the harness refuses a session whose spec blob differs). Then, per language and in isolation, the **process-arm agent makes its own plan** from the frozen spec; that plan goes through the gates in its run; **the task spec is never changed**. One story (E1.S2) covers the ten specs with per-task acceptance criteria | operator, 2026-10-31 (instruction B) |
| D22 | **Cross-language tests: one shared black-box acceptance suite per task** that runs unchanged against both implementations — stdin/stdout for olympiad tasks, HTTP for web APIs, TCP/RESP and signals for daemons. Written once, in one neutral harness language, **owned by the bench**, not by either stack. A **visible part** (samples in the workspace) and a **held-out part** (outside git). The harness language is **Python 3, standard library only, run with `-I`** — v1's held-out runner — because it is third to both stacks; the argument is §4.3 | operator, 2026-10-31 (instruction C); the language choice: this plan |
| D23 | **The conventions are pinned in the new repository, as in the other `dew_flow` repos, at a release that passes a conventions parity audit:** every C#/.NET rule in `dew_flow_conventions` has an equivalent Node/TypeScript rule adapted to Node, and the other way round. The audit **runs now, in parallel** (E0.S1); its result is a prerequisite story: missing counterpart rules are written and released in `dew_flow_conventions` **before the v2 pre-registration** (E0.S2), and the new repository pins that release (E0.S3). Threat T14 names the bias the audit controls | operator, 2026-10-31 (instruction D) |
| D24 | **Primary metric: cost per correct solution.** For task t, stack s and arm a: `CPC(t, s, a) = Σ cost of all data attempts of (t, s, a) / number of accepted solutions among them`. Beside it: success rate (accepted / attempts), tokens per model and kind, wall, `model_api`, `active − model_api`. A cheap unfinished run never looks like a winner: it adds to the numerator and nothing to the denominator. A (t, s, a) with **0 accepted** shows **"no correct solution"** plus its total cost and is **not dropped**; its ratio is a bound (`≥` when the other side has a value). A second line counts void attempts in the numerator too (§9) | operator, 2026-10-31 (instruction E1); the void line: this plan |
| D25 | **The same functional requirements and the same checks for both stacks.** Statements describe the required behaviour neutrally. **The trap list is an INTERNAL test-design aid**, kept with the held-out material, never in a statement; **every held-out test checks behaviour the statement requires**. **Review step:** a reviewer — the coai document gate plus a consultant — checks each statement and its tests for a bias toward or against one language's specifics, before the freeze (D21) and before the suite is sealed. The trap table is kept, on the test-design side (§4.3) | operator, 2026-10-31 (instruction E2) |
| D26 | **Freeze before any paid data run:** tasks (specs), tests (suites, hashes, counts), budgets (ceiling, caps, split), the analysis (§9), the exclusion grounds (voids, stops) and the re-run grounds — all in the pre-registration, tagged. **Every attempt is kept**, including failed, voided and re-run ones, in the records and the report | operator, 2026-10-31 (instruction E3) |
| D27 | **Spread the 6 repeats per task over time:** a seeded random task order per round; the paired concurrent C# ∥ TS start of v1 stays; the CPU masks alternate per pair as v1 did; **bare and process rounds interleave** (D10), so one good or bad API day does not decide the result; the start and end time of every session is recorded | operator, 2026-10-31 (instruction E4) |
| D28 | **Analysis is per task:** 10 tasks with repeated measures, not 120 independent samples. Each task is shown; the **overall result is the median of the per-task C#/TS ratios** (primary), the **geometric mean of the per-task ratios** beside it, and the **count of tasks where C# is higher** — per arm and per measure; pre-registered. No expensive task may hide the others: no pooled sum across tasks is a headline | operator, 2026-10-31 (instruction E5); the choice of the median as primary: this plan |
| D29 | **Known and original tasks are shown separately**, and the report discloses that the five public tasks may be familiar to the model | operator, 2026-10-31 (instruction E6) |
| D30 | **The $1000 is not changed by this plan.** With two arms (120 data sessions) and dearer process sessions, the **split between arms is re-derived from the pilot** (§12); whether $1000 covers both arms or the process arm needs its own limit is **open (Q-A)** | operator, 2026-10-31 (instruction F) |
| D31 | **The repository name stays `dew_flow_ai_language_comparison`, public from day one.** Held-out material and raw records stay outside git | operator, 2026-10-31 (instruction G) |
| D32 | **v2 builds the process-arm pieces itself** (E3.S6–S9): the coai box and the stdio↔socket bridge, the process templates, rulesets and PATs (the repository side in E1.S7), the process runner, the compliance checker, P0 and the model smoke. They do not exist: v1's E2.S3, E2.S4 and E3.S1–S3 are not built. **v1 is closed after its bare arm; v2 is the active plan.** v1's parent plan gets only a STATUS line saying its process arm moved to this plan — a status update, not a plan change — as a follow-up outside this commit (§16). v1's design (its §4.2, §5.4, §6, §7.2, §8) stays the specification v2 builds to | team lead, 2026-10-31, from the coai plan gate (closes Q-B) |
| D33 | **Werewolf I/O: option (a).** The spec `todo/tasks/09-werewolf.md` defines an exact stdin/stdout grammar and the agent implements the parser. There is no frozen helper or adapter code in `csharp/09-*` or `node/09-*`; the workspace holds the same files as every other olympiad task | team lead, 2026-10-31, from the coai plan gate (closes Q3) |
| D34 | **Metrics are only added, never replaced or dropped.** Every v1 metric stays in every v2 record, per session and per pair: the four cost views (headline recompute, E envelope, O OTel, B broker) with the checks T1–T5; tokens per model and kind, with the 5m / 1h cache-write split; the exclusive time partition (wall spawn to exit, `model_api`, `active − model_api` and the per-category spans); visible and held-out quality as counts, `percent` and `defects`; outcome class, labels, flags and charges; the process arm's compliance items. v2 ADDS beside them: the cost per correct solution (D24, and its void line), the success rate, the per-task medians and geometric means (D28), and the known/original split (D29). A v2 table may lead with a new metric; it never omits a v1 one | operator, 2026-10-31 ("only add metrics") |

## 4. Design

### 4.1 The ten tasks

K = known (paraphrased with credit), O = original. Times are the operator's guesses for one bare agent session, to be
calibrated by the pilot (D4). The runtime traps each task is designed around are on the test-design side, §4.3 (D25).

| # | K/O | Task | Kind | Guess (bare) |
|---|---|---|---|---|
| 1 | K | *Weird algorithm* (CSES 1068): the Collatz-like sequence of n ≤ 10^6 | olympiad | 2–5 min |
| 2 | K | *Counting rooms* (CSES 1192): connected components in a grid up to 1000 × 1000 | olympiad | 5–10 min |
| 3 | O | *Deep expression evaluator*: input to 2·10^6 chars, nesting depth to 10^6, signed 64-bit wrap-around; the full grammar is published; `MinValue / -1` and the literal 2^63 under unary minus are defined | olympiad | 20–30 min |
| 4 | O | *Rule API*: HTTP; rules call members by name over a supplied registry, with string arguments | web API | 20–30 min |
| 5 | K | *RESP key-value server* (a Redis subset): GET/SET/EXPIRE/PX/INCR over TCP, many clients | daemon | 25–35 min |
| 6 | K | *RealWorld "Conduit" API subset*: users, JWT, articles, pagination | web API | 30–45 min |
| 7 | O | *Stream-processor daemon*: an event stream, a sliding window per key, a watermark that expires keys that never return, an RSS limit, graceful SIGTERM; the spec fixes event-time semantics, ordering, bounds, output cadence and EOF | daemon | 30–45 min |
| 8 | O | *Ledger API with idempotency keys*: money, concurrent retries of the same key | web API | 35–50 min |
| 9 | K | *Werewolf* (IOI 2018): the spec defines an exact stdin/stdout grammar, identical for both stacks, and the agent implements the parser; no adapter or helper code is supplied (D33) | olympiad | 45–60 min |
| 10 | O | *DAG job scheduler daemon*: retries with capped backoff, per-job timeouts, cancellation, one process-wide concurrency limit k over overlapping submitted DAGs, cycle detection, failed-dependency propagation, a deterministic final report; a supplied virtual clock and job fixture for deterministic grading plus a real-time smoke | daemon | 45–60 min |

Sum of the upper guesses: 370 min per stack per bare round; a process round is longer (v1 predicted 2–5 h per process
cell against minutes for bare); the pilot replaces both guesses.

**Spec rules (D4, D19, D21, D25).** One spec per task, `todo/tasks/NN-slug.md` in the task repository, written by the team
lead, stack-neutral: the required behaviour, the input and output contract, **the limits as guarantees** (change 1.2a: the
spec states D14's floors — 1 s of wall time and 256 MB per run, the RSS floor for a daemon — and the registration rule,
at least 3 × the slower reference's wall and 2 × the larger reference's peak; the exact values are registered at
calibration, E2.S3, and given to the runner, never to the author, so a spec never needs an amendment to carry them), the
start contract (`build.sh`, `run.sh`, `PORT`), the frozen list, "hidden tests exist", the visible samples named. A known
task is paraphrased: no id, no title, no site, **and no mention that the task is known**; the constraints may keep the
original numbers. It never names a trap or a language specific. The workspace's `STATEMENT.md` is the spec's text plus the
stack sentence (as v1's `{STACK_SENTENCE}`), generated, byte-identical across the two stacks except that sentence.

**Spec format (change 1.2c, adopted from the pilot spec 01, `dew_flow_ai_language_comparison` `60e468a`), binding for
all ten:** a header table with the rows kind (what the program is: reads stdin once / a long-running server …), gate
verdict, freeze tag, the frozen sentence ("frozen: changes only by a dated amendment before the v2 pre-registration") and
amendments, with the placeholders "not yet gated" and "none yet" until the gate and the tag; every requirement is a
numbered sentence with a **stable id R1 … Rn**, and held-out cases cite these ids (§4.3, E2.S2) — nothing outside them is
tested; **byte-exact output is the series default** (compared byte for byte, nothing normalized; a spec that normalizes
says so in a requirement); the frozen list is written for a stack-neutral spec ("`STATEMENT.md`, `samples/`, `build.sh`,
`run.sh`, `PACKAGES.md` and the toolchain pin files of the stack"); the **600 s build bound** is stated; invalid input
"does not occur in any visible or hidden test; what the program does with it is unspecified and never tested" unless the
task itself defines error behaviour; **no plan story id, epic id or decision id in agent-visible text**; sample bytes are
shown with the `⏎` glyph for the single line feed, and the same cases exist as files (1.2b). An original spec is precise enough that two references written
apart agree on every held-out case (E2.S1). **Gate and freeze:** each spec goes through the coai document gate with
`purposeText` naming who acts on it (the agents under test, in both stacks, and the suite's author), plus a consultant, plus
the bias review of D25; every finding resolved; then the tag `spec-frozen-<date>`; the spec's sha256 in `inputs/specs.json`
here. After the tag the spec changes only by a dated amendment applied to both stacks **before** the pre-registration; after
the pre-registration never.

### 4.2 The two arms (D20; v1 §4.2 reused)

| | BARE | PROCESS |
|---|---|---|
| Where | a copy of `csharp/<task>/` or `node/<task>/` at the pinned sha, in an empty box dir | a fresh single-branch clone of `run/<rid>/main` in the public task repository (v1 §4.2, §6) |
| Instructions | none; the task prompt | the repository's `AGENTS.md` → ENTRY → the pinned conventions (D23); all hooks |
| Who works | Opus, effort `high` | an Opus team lead; Sonnet coders, Opus judges, Fable cornerstone specs as the subagent-model rule says; effort `high` pinned for every model (v1 D17) |
| MCP | none (`--strict-mcp-config`, empty) | coai in its own bwrap behind the stdio↔socket bridge (v1 §7.2, E2.S3): plan gate, code gate, consultant, `ask_consultants`; `COAI_ON_EXHAUSTED=good_enough` (v1 D5) |
| GitHub | none; `gh` hidden | a fine-grained PAT for this one repository; Contents/PR RW, Actions/Checks/Statuses R; no Workflows (v1 §4.2); egress to the GitHub hosts only |
| The spec | `STATEMENT.md` in the workspace | `todo/tasks/NN-slug.md`, frozen; the agent writes **its own plan** in `todo/` of its run branch and takes it through the plan gate (D21) |
| Packages and toolchains | the offline caches and pinned toolchains (D5, D6) | the same caches and toolchains, seeded into the box |
| Done when | the session ends | merged into the run base with green required checks, or the session ends; an undelivered session is scored on its PR head and marked so (v1 §5.3) |
| Session cap and wall | from the pilot (D4); wall ≈ 75 min | from the pilot; v1 planned $35 / 240 min for a far smaller task — the pilot sets v2's |
| Scored how | the workspace as left (§4.3) | the run base tip as left (or the PR head); plus the 17-item compliance score of v1 §5.4, recorded, not part of `accepted` |

Templates, run bases, rulesets, CI, PATs and the deploy key follow v1 §6 with these changes: there is one repository with
two stack folders, so the per-stack template difference is the folder the prompt names and the stack's pin files; the CI
workflow has one job per stack filtered by path; the required check for a run base is the stack's `build · test` job and
the family checks; the acceptance job (the visible suite) is not required, for v1's reason (several PRs allowed). The
conventions pin is the parity release (D23), on a snapshot branch as v1's D9 so `pin-check` stays green while `release`
moves. v1's 17 compliance items apply unchanged (plan file, rules read, worktree, plan gate before code gate, every finding
resolved, Sonnet coder and Opus judge children, test-first, CI, docs, PR, 300 s, inputs unchanged, pin unchanged), plus
one v2 item: **the frozen spec unchanged** (its blob equals the tag's).

### 4.3 Shared acceptance suites, scoring per kind, and the test-design side (D13, D14, D22, D25)

**One suite per task, in Python 3 (standard library only, `-I`), owned by the bench.** Why Python and not Node: Node is one
of the two runtimes under test, so a Node harness shares its number model (doubles, `JSON.parse` on 64-bit ids), its string
and Buffer semantics and its event loop with the TypeScript implementation — the exact places several traps live — and a
check written in Node could pass a TS defect the harness itself shares, or encode a JS-shaped expectation a C# program
meets differently. Python is third to both: arbitrary-precision integers (exact 64-bit checks), bytes apart from text,
`socket`, `asyncio`, `http.client`, `signal` and `subprocess` in the standard library, no package to pin, and it is already
v1's held-out runner (`lib/heldout-run.mjs:19`). Its cost: Python's own client speed bounds the load a scenario can apply;
load scenarios are sized so the reference solutions of both stacks pass them with margin (D14), and the limit is on the
program under test, not on the driver. The Python version is pinned by checksum at pre-registration.

**Visible part** (`samples/` in the workspace): a few small clean cases per task that the traps do not reach; the agent may
run them; CI's acceptance job runs them in the process arm. **The format is fixed (change 1.2b): one input file and one
expected-output file per case, `samples/<id>.in` and `samples/<id>.out`, bytes exact**; for a daemon or web task the same
two files hold the scenario script and its expected transcript in the suite's scenario notation. **Held-out part** (outside
git, sealed as v1): the cases and scenarios that decide `accepted`, in the same `<id>.in` / `<id>.out` shape plus a
per-case line naming the requirement ids (R-ids) it asserts. The Python runner of E2.S2 **reads this format**; it is no
longer E2.S2's to decide. Both parts use the same case format and the same runner.

All scoring happens on a pristine copy of the result, in a scoring box with no network, offline from the session's own
caches, with temp HOME, as v1. `build.sh` first (bounded, 600 s; a failure fails every case). Then by kind:

| Kind | The harness does | A case passes when | Also recorded |
|---|---|---|---|
| Olympiad | runs `run.sh` once per held-out case with the case's stdin, under the task's registered time and memory limits in a cgroup | stdout equals the expected bytes **byte for byte** (the series default, change 1.2c; a normalization applies only where a requirement of the spec names it), exit 0, within limits | per case: wall, CPU, peak memory, verdict class |
| Daemon | starts `run.sh` with the task's environment; waits for readiness (the spec defines it); drives scenarios over TCP (RESP) or stdin; samples RSS every second; sends SIGTERM; waits the grace period | every scenario reply equals the expected one; RSS stays under the bound; the process exits 0 within the grace period; no process of the session is left | RSS over time, exit code and time, stdout/stderr tails |
| Web API | as daemon, over HTTP; scenario steps are requests with expected status, headers and body; concurrency scenarios send the same request from k clients at once | as daemon; concurrent scenarios check the invariant (one effect per idempotency key, one balance) | as daemon, plus the request log |

**Measures per session:** `accepted` (D13); `percent` = cases passed / N (a blocked case counts as not passed); `defects` =
distinct **trap classes** failed, from the trap coverage table (one trap failing in three cases counts once); verdict class
counts. **First-submission** means: what the session left when it ended, scored once; no retry.

**The test-design side (internal, D25).** The traps below are the operator's design aid for the suites (2026-10-31). They
are not in any spec; every held-out case that catches one asserts a behaviour the spec requires (a 64-bit result, a
reply within the limit, a stopped process). The authoritative **trap coverage table** (task × stack × trap → the cases
that catch it) is sealed with the suites and published after the series.

| # | C# trap | TypeScript trap |
|---|---|---|
| 1 | `int` overflow (values reach 5.7e10); `int` parsing of the input; `int` arithmetic in `3·v + 1` | **bitwise halving:** `v >> 1` is wrong for v ≥ 2^31 (`2482111348 >> 1` = −906427974); `>>> 1` and `\| 0` are wrong for v ≥ 2^32 (`4294967298 >>> 1` = 1). *Fact fix, not a plan change: the earlier row said "`n & 1` truncates to 32 bits" — a consultant claim of 2026-10-31, refuted by probe the same day: ToInt32 is v mod 2^32, 2^32 is even, so parity survives (Node: `56991483521 & 1` = 1).* |
| 2 | recursive DFS stack overflow on a snake corridor | the same overflow; `Array.shift()` makes BFS O(n²) |
| 3 | `long.MinValue / -1` throws even unchecked; `long.Parse("9223372036854775808")` fails | `BigInt.asIntN` cost; recursion stack overflow |
| 4 | culture-dependent parsing of string arguments (host culture `de-DE`); reflection overload choice and numeric conversion | `__proto__` and inherited member lookup with an observable consequence; `JSON.parse` corrupting 64-bit ids; `Math.round(-1.5)` |
| 5 | expired keys never purged; string against binary-safe bytes | partial TCP frames; Buffer→string corruption; backpressure |
| 6 | `DateTime.Kind` and UTC serialization; JSON name casing | JWT checks; `Date` and time zones; async errors |
| 7 | `Dictionary` does not shrink; `JsonDocument` lifetime; Generic Host shutdown | `Map` leak; `readline` backpressure |
| 8 | `decimal` rounding mode; a race on the same key | float money; a race on the same key; body limits |
| 9 | deep reconstruction-tree chains (recursion); memory of auxiliary structures; allocation pressure | the same three |
| 10 | double semaphore release; unobserved task exceptions; `async void` | an un-cleared `setTimeout` keeps the process alive; unhandled rejections; cycle deadlock |

On task 1 the C# side has more trap points than the TypeScript side (three `int` sites against one bitwise idiom): a
property of the problem, not of the design, and the trap coverage table reports it per T2 so the task's result is read
with that difference.

**Calibration before any cell (measurement rule 2):** two references per task, one in C#, one in TypeScript, each pass
100 % under the registered limits in the scoring box; planted mutants — one per trap per stack, each a plausible version
of that trap — each fail exactly their registered cases. **Bias review (D25):** each spec and its suite (visible and
held-out, in clear from a scratch dir, through `documentText`) go to the coai document gate plus a consultant with the
question *"does any requirement or check favour or disfavour one of C# / .NET 10 and TypeScript / Node 26 beyond what the
required behaviour needs?"*; every finding resolved; the verdict and the reviewers' notes recorded in `inputs/bias-review/`
(no case text). The vendors' reviewers see held-out cases before sealing; the agents under test never do (no network;
the suite is sealed; Q-C names the residual).

**Adjudication, pre-registered as v1 §5.3:** the primary score is the unrevised one; every failing held-out case is classed
blind (product defect / spec ambiguity / harness defect) in two steps, stack and arm masked in step 1; a case classed
ambiguous yields a revised score beside the primary.

### 4.4 Cells, pairs, rounds, order (D9, D10, D27)

- Session = (arm, task, stack, repeat n), n ∈ 1..3; 120 data sessions in 60 pairs. `rid = sha256(seed:<arm>:t<task>:<stack>:r<n>:a<attempt>)[0:6]`,
  distinct across every field (v1's T-R1 widened).
- A pair = C# ∥ TS of one task in one arm, started in one tick, masks `0-5` / `8-13` (`maskFor`, swapped per pair), core 3
  (CPUs 6–7) for the harness, coai and the vendor CLIs, nice 19 as v1. Both sessions of a pair are collected and scored
  before the next pair starts; scoring runs on all CPUs after the cells end.
- Six rounds, arms alternating, the leading arm and the task order per round from the seed (D10); `seed.sha256` committed
  with the pre-registration, `seed.txt` revealed after the A/A and before round 1 (v1's commit-reveal). Every session's
  spawn and exit time is in its record (D27).
- The void and re-run rules of v1 (§9) apply per session; a void is re-run once, after the round's own pairs and before the
  next round; a re-run whose partner stands runs alone and is labelled `ran unpaired`. The process arm adopts v1's
  `void: no reviewer` (`call_human` with zero reviewers answering) and keeps `call_human` with ≥ 1 reviewer as a data point.
- The machine-quiet check before every pair; load during a pair is flagged, never a void (v1 E3b.S2).

### 4.5 Prompts

One template per arm, byte-identical across stacks except `{STACK}`, `{STACK_FOLDER}` and `{STACK_SENTENCE}`, registered
as `prompts-lang.json` with sha256 per arm, task and stack. Shape (final wording fixed in E4.S2):

**Bare:**

```
Implement the program described in STATEMENT.md in this directory, in {STACK}. {STACK_SENTENCE}
STATEMENT.md, samples/, build.sh, run.sh and PACKAGES.md are frozen inputs: do not change them. The program is built by
`bash build.sh` and started by `bash run.sh` exactly as STATEMENT.md says. Hidden tests will run it under time and memory
limits that are never below the floors STATEMENT.md guarantees. Only the packages listed in PACKAGES.md are available;
there is no network. Work autonomously; nobody answers questions. End with a summary of what you built and the checks
you ran.
```

**Process** (v1 §4.3's process prompt, with the task slot):

```
Implement the program specified in todo/tasks/{TASK}.md of this repository, in {STACK}, inside {STACK_FOLDER}/. {STACK_SENTENCE}
The spec, the samples, build.sh, run.sh and PACKAGES.md in that folder are frozen inputs: do not change them. The work is
done when, from a clean checkout of the base branch, `bash build.sh` succeeds and the visible samples pass within the floors
the spec guarantees. Hidden tests will run the program the same way, under limits never below those floors.
Follow this repository's own instructions — its CLAUDE.md, AGENTS.md, .agents/PROJECT.md and the rules they point to — for
how work is done here, in full: loading the rules, your own plan in todo/, the review gate on the plan and on the code,
consultants, subagents, tests first, the research/ documents, the pull request, CI and the review that follows it.
The base branch of this task is the branch checked out when this session starts. Branch from it, open every pull request
against it — never against main — and finish only when your work is merged into it with green CI.
Work autonomously. Nobody answers questions in this session. Where you would ask a person, decide, and record the decision
and its reason in your plan and in the pull request description. If the review gate returns call_human, stop shipping:
leave the pull request open, record why, and end the session.
Stay inside this task: do not read other run/* branches, other pull requests or issues of this repository, and do not use
the web. End with a summary: the pull request(s) and merge commit, the tests you ran and their results, the gate rounds,
and every decision you made.
```

Stack sentences: C# — *"Use C# 14 on .NET 10 (SDK 10.0.403 is installed; the project files in this folder are pinned)."*
TypeScript — *"Use TypeScript 7.0.2 with `strict`, run by Node 26 with native type stripping (no build step), with `tsc
--noEmit` as the type check."* Web API tasks add one identical sentence naming the stack's web framework from `PACKAGES.md`.

### 4.6 Stacks and packages (D5, D6)

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
version Fastify 5.12.7 resolves), `@types/node` for 26.x, `typescript@7.0.2`, and the lint packages the process arm's
template needs (v1 D4's shape); every Node workspace's `.npmrc` sets `offline=true`. **The exact package set per stack, with
pinned versions and the hashes in the cache manifest, is E1.S4's deliverable** and is pre-registered. `PACKAGES.md` in each
workspace lists it (D12).

**Toolchains**, pinned read-only with checksums under `~/rsd/lang-v2/toolchains/` and checked inside the box by the
preflight: .NET SDK 10.0.403 installed from the official install script into that directory (not apt, so an unattended
upgrade cannot move it, as happened to v1 on 2026-10-30), Node 26.13.0 from the official tarball, TypeScript 7.0.2, the
Claude CLI copy (D8), Python 3 by checksum of `/usr/bin/python3` (D22). `global.json` pins `10.0.403` with `rollForward:
disable`; `package.json` `engines.node` pins 26.13.0; `tsconfig.json` is v1's (`strict`, `noEmit`, `erasableSyntaxOnly`,
`verbatimModuleSyntax`, `module nodenext`).

## 5. The public task repository (D2, D11, D21, D31)

```
dew_flow_ai_language_comparison/              public from day one (D31)
  README.md            what this is; the task list; credits WITH links (CSES, IOI 2018, RealWorld, the RESP spec) — the only place links appear; the Node stack rationale (§4.6)
  LICENSE              the repository's licence (open question Q1)
  AGENTS.md CLAUDE.md .agents/PROJECT.md .agents/conventions   the family's pinned conventions at the parity release (D23), on a snapshot branch; the hooks
  .github/workflows/   ci.yml (one job per stack, path-filtered; acceptance = visible samples, not required), family-checks, pr-title (v1 §6)
  todo/README.md       the index; todo/tasks/NN-slug.md — the ten gated, FROZEN specs (D21); the process agent's own plans land in todo/ of its run branch
  research/            stubs (architecture, module_tests) as v1's templates; the references and the trap table arrive here after the series
  tasks/<nn>-<slug>/   NOTES.md (credit, paraphrase notes, design reasons — no traps, no held-out content), samples/ (the visible part of the suite, D22)
  csharp/<nn>-<slug>/  the C# workspace: STATEMENT.md (generated from the spec + stack sentence), samples/ (a copy), PACKAGES.md, build.sh, run.sh, global.json, nuget.config, Directory.Build.props, an empty .csproj skeleton
  node/<nn>-<slug>/    the Node workspace: STATEMENT.md, samples/, PACKAGES.md, build.sh, run.sh, package.json, package-lock.json, tsconfig.json, .npmrc, eslint.config.mjs
  run/<rid>/main       process-arm run bases, cut from the frozen tag by the deploy key (v1 §6); rulesets `run-main`, `template-frozen`, tag rulesets
```

Rules: no secrets, no transcripts, no held-out case, no reference solution before the series ends (references and the
trap table are published beside the report, as a separate commit, after every data session is scored); a `leakcheck`
with the suites' needles runs on every commit range before it is pushed. The bare harness fetches the repository at the
frozen sha into `~/.cache/lang-v2/<sha>/` and checks every workspace file against `inputs/hashes-lang.txt`, as v1's
`fetchInputs`; the process harness cuts run bases from the same sha. A `workspace-diff` check proves the two stacks' folders
of one task differ only in the stack sentence and the stack's pin files (v1's `template-diff`). The lifecycle checker reads
only `todo/PLAN_*.md`, so `todo/tasks/*.md` needs no status line; each spec carries a header naming its gate verdict, its
freeze tag and the sentence "frozen: changes only by a dated amendment before the v2 pre-registration".

## 6. The harness — `spikes/lang-bench/`, importing `spikes/minigit-bench/`; the boundary with v1

The precedent is v1 itself: `spikes/minigit-bench/` imported `spikes/policy-ab-quick/lib/*` and changed those files only
through named seams with tests. v2 does the same one level up: **new modules in `spikes/lang-bench/`, imports from
`spikes/minigit-bench/lib/`, and a short list of named widenings of v1 files, each with a test**; never a second copy.

| v1 piece | v2 use |
|---|---|
| The box, hide list, bind list, `agentEnv`, the supervisor | **as is**; `extraRo` binds the task toolchains and Python; `agentEnv({ extra })` adds D15's keys |
| The CONNECT proxy and SNI check | **as is**; `EGRESS_HOSTS.langBare = []`, `EGRESS_HOSTS.langProcess` = the GitHub hosts v1 E2.S4 settles (D17) |
| The broker, `B`, cap refusals | **as is**; the caps per arm come from the pilot |
| `launchSession` | **as is** |
| E/O/T/B, T1–T5, `sessionCost`, `crosscheck`, the alias record (v1 D12) | **as is**; the v2 policy starts from v1's `t3 stop` and is re-decided by the v2 A/A |
| `spansFrom`, `partition`, the categories | **widened:** `build_test` learns `bash build.sh`, `bash run.sh`, `dotnet publish`, `node --test`, `npm test`, the drivers' commands; the process categories (`coai_gate`, `consultant`, `ci_wait`, `sleep300`, `hooks`) as v1 defined them; a test per new literal |
| Held-out sealing, calibration, leakcheck, adjudication, the Python runner | **as is**, pointed at `~/rsd/lang-v2/`; the archive holds ten suites' held-out parts, the trap coverage table and the mutants' ids; the runner gains the driver modes (D22) |
| The scorer pipeline on a pristine copy | **widened:** a `kind` step after the build — the olympiad judge, the daemon driver, the web driver (new `lib/judge-stdio.mjs`, `lib/driver-process.mjs`, `lib/driver-tcp.mjs`, `lib/driver-http.mjs`, `lib/rss-sampler.mjs`, `lib/cgroup-limits.mjs`, each a thin Node wrapper over the Python suite runner) |
| The bare runner: pairs, masks, voids, re-run, resume, records, prereg guard, spend gate | **widened:** an arm and a task dimension and a `single` phase (no barrier, no v2 copy); six alternating rounds; the schedule drops the v1→v2 rows; the record gains per-kind and per-arm fields; `PREREG_FILES` is the v2 list; the spec-freeze guard |
| The quiet check, CPU sampler, spin probe | **as is** |
| Cache snapshots | **as is**, with the v2 recipes |
| `aaVerdicts` G-1 … G-14 | **widened** with G-15 … G-19 (§11) |
| `research/minigit/tools/` extract, render, replay | **widened** to arms, tasks and kinds; CPC and the per-task ratio tables (E6) |
| Watchdogs | wall per arm from the pilot (bare ≈ 75 min, D4), stall as v1 (45 min, the pilot may change), cost at the derived cap |
| **v1's process-arm design, never built** (coai box + bridge; templates, snapshot branch, rulesets, PATs; process runner; compliance checker; P0 and model smoke) | **built here (D32):** the repository side in E1.S7, the coai box and bridge in E3.S6, the process runner in E3.S7, the compliance checker with the `spec unchanged` item in E3.S8, P0 and the model smoke in E3.S9 — to v1's design, for one repository with two stack folders |

**Boundary with v1** (`common.planning-docs` § *a boundary … on BOTH sides*). By D32 v1 closes after its bare arm, so the
table below is the division; v1's parent plan gets only a status line pointing here (a status update, not a plan change;
a follow-up outside this commit, §16):

| Item | v1 | v2 |
|---|---|---|
| The process arm: coai box and bridge, templates, rulesets, PATs, process runner, compliance checker, P0, smoke | designed (its §4.2, §5.4, §6, §7.2, §8); E2.S3, E2.S4, E3.S1–S3 never built; closed | **built**: E1.S7, E3.S6–S9, to v1's design, plus the `spec unchanged` item |
| The bare runner's pair, masks, voids, re-run, spend gate | built (E3b) | widened with arm, task, rounds (E3.S1) |
| Minigit's inputs, suite, references, fork, the bare record | v1 only | none — disjoint |
| Task specs, shared suites, drivers, judge, CPC analysis, the task repository, the parity audit | none | v2 only — disjoint |

Order: E1.S7 (the repository side), then E3.S6–S9. Launch argv as v1 (`claude -p --model opus … --max-budget-usd <cap> --tools
Task,Bash,Read,Edit,Write,Glob,Grep,TodoWrite[,ToolSearch]`), MCP only in the process arm, effort `high` pinned in the
session settings file, fresh `CLAUDE_CONFIG_DIR` per session, `gh` in the box only in the process arm.

## 7. Isolation and contamination

Prevented, as v1: the operator's home and `/mnt` hidden; the bare box has no network but the broker door; the held-out
suite sealed, its key never in any box's environment; the real token never in the box; coai's vendor sign-ins outside the
agent box in coai's own box. Audited, as v1: every tool call scanned for paths outside the box, walkers, links, token
shapes, canaries; in the process arm, pushes and PRs outside the session's `run/<rid>/` namespace, other `run/*` reads
(the repository is public, so other runs' code is **audited, not prevented**, v1 §7.3), the pin and the frozen spec
unchanged. New in v2:

- **Known-task needles (D18)** in the scan; a hit is read by hand and recorded as a dated line; it voids only when it shows
  a reach outside the box.
- **The bare box holds one task folder (D11)**; the process clone holds the whole repository at the frozen tag, so a process
  agent can read the other stack's empty template and the other tasks' specs — recorded as a property of the arm, not a
  contamination; it cannot read the held-out suite, the references or another run's branch without a fetch, which the audit
  flags.
- **The reference solutions** live only in `~/rsd/lang-v2/refs/` (outside git, outside every box) until publication.
- **Session memory:** `CLAUDE_CONFIG_DIR` is fresh per session; coai's `COAI_DATA_DIR` is per session; question consultants'
  disk-reading rows are off (v1 D14).
- **The 18 instrument defects of v1** (RESULTS §11): the fixes of 16–18 are on `feat/minigit-bench-e3b-fix2`, which no paid
  series has run. v2 runs on a harness that carries them; the v2 A/A is the first paid run of that scan. Every void in v2 is
  replayed with the current scan at report time, as v1's `replay.mjs` does, and the record says which voids were the scan's.

## 8. Accounting and time

As v1 §9–10, unchanged: headline USD = the published-price recompute from `modelUsage` with the 5m/1h split from T; the CLI's
total and `B` beside it; every stop-rule figure is the largest of the three; T1 breach stops the series, T2 `otel incomplete`,
T3/T5 per the policy the A/A sets; a killed session without an envelope is priced from `B`. **Process arm:** the coai vendors'
consumption from `coai/usage.jsonl` and `coai.db` — tokens per provider, USD a number or "unknown (subscription)", never 0
and never added to Claude's USD (v1 §9). **CPC's cost is the Claude headline USD** of the session; the vendors' tokens are
reported beside it, bounded as v1 bounds them. Time: wall from spawn to exit, the exclusive partition (`build_test >
model_api > other_tool > hooks`, then `coai_gate > consultant > ci_wait > sleep300`, then `unattributed`); primary measures
wall, `model_api`, `active − model_api`; in the process arm **active time** (work categories only) stands beside wall (v1
§10). `build_test` against `other_tool` descriptive only (v1's V16). Prices: a dated copy re-read on the series' first day.
New in v2: per-session **scoring time** recorded apart from the agent's wall; per-case runtime of the olympiad tasks as a
secondary measure; every session's spawn and exit timestamp (D27).

## 9. Pre-registration and analysis (D24, D26, D28, D29)

Committed and tagged `prereg/lang-v2-<yyyymmdd>` **before the pilot** (E4.S3) and amended only by dated lines after it;
**frozen before any paid data run (D26):** the ten specs' hashes and the freeze tag; the suites' hashes and counts; the
trap coverage table's hash; the limits of D14; the prompts; the pins and checksums (CLI, SDK, Node, TS, Python); the
package sets and cache hashes; the conventions parity release sha (D23); the ceiling, the caps per arm and the budget split
(D30, from the pilot, as a dated amendment before round 1); this section as the analysis contract; the outcome classes, the
void (exclusion) grounds and the re-run grounds; `prediction.md`; `seed.sha256`. After the pilot, amendments may change the
caps, the stall timeouts, the limits (both stacks alike, by D14's formula), the time guesses and the split — never the
treatment of one stack. `seed.txt` is revealed after the A/A.

**Outcome classes:** v1's twelve rows (`stop: alias drift`, `stop: security`, `stop: auth`, `void: contaminated`,
`void: harness`, `void: harness (interrupted | disposed)`, `killed: cap`, `void: rate limit`, `void: upstream`, `killed: wall`,
`killed: stall`, `completed`, `crashed`), same precedence; the process arm adds v1's `void: no reviewer`. A `killed:` or
`crashed:` session is a **data attempt**: its cost is in CPC's numerator (censored at the cap, `≥`), its quality is what it
left. A void is **kept** in the records and re-run once (§4.4); **every attempt appears in the report** (D26).

**Primary metric, exactly (D24):** for task t, stack s, arm a, over the data attempts A(t, s, a) (completed, killed, crashed —
every session that was not void):

`CPC(t, s, a) = Σ_{x ∈ A} cost(x) / |{ x ∈ A : accepted(x) }|`

with cost = the headline recompute (§8). If no attempt is accepted, CPC is **"no correct solution"** and the total cost is
shown; the (t, a) ratio is then a bound (`≥ Σ cost / 1 ÷ CPC(other)` when the other stack has a value; "neither" when both
have none). **Second line, pre-registered:** `CPC_all`, the same with void attempts' cost added to the numerator — the
"what the series actually paid" view. Beside both: success rate = accepted / |A|; tokens per model and kind; wall;
`model_api`; `active − model_api`; in the process arm active time and the compliance score k/18.

**Per task (D28):** for each arm, the C#/TS ratio of CPC and of every secondary measure, the two success rates (k of 3),
"C# > TS" per measure. **Overall, per arm and per measure:** the **median of the ten per-task ratios** (primary), the
**geometric mean of the ten per-task ratios** (a bound-carrying ratio enters the geometric mean at its bound and is marked),
and the **count of tasks where C# is higher**. Per kind (olympiad 4, daemon 3, web API 3), and **known (5) against original
(5) separately (D29)**, the same three figures. The **process effect** per stack (process/bare CPC per task, median over
tasks) and the **interaction** (C#/TS process ÷ C#/TS bare, median over tasks) as v1 §11 defined them. Every line is given
with and without pairs holding a `ran unpaired` session or a `foreign load` flag.

**What this licenses:** per task, "accepted k of 3", "C# > TS in j of 3 pairs", whether ranges overlap (overlap = not
distinguished). Over the tasks, the median and geometric mean of ten ratios and "C# higher in k of 10 tasks" — descriptive
summaries of a chosen set, not a sample; no p-value, interval or effect size is a finding (whether a sign test is shown as a
secondary line is Q6). The report discloses that the five known tasks may be familiar to the model (D29). Nothing licenses
a claim about other tasks, models, CLI versions or the languages in general.

**Prediction (draft, to be written verbatim into `prediction.md` in E4.S2 before the pilot; the pilot may amend the cost
and time ranges by a dated line, nothing else):**

1. Correctness, bare: success rate differs between the stacks on at least 2 of the 10 tasks; over 30 bare sessions each,
   the stacks are within 4 accepted of each other. Basis: the traps are balanced by design (D3, D25); v1 saw no quality
   difference at all.
2. The traps fire: at least 6 of the 20 (task, stack) trap sets catch at least one bare session. Basis: a suite where nothing
   fires measures nothing (the v1 ceiling).
3. CPC, bare: the median of the per-task C#/TS ratios in 0.8–1.4; C# higher in 4–7 of 10 tasks; the daemon and web tasks
   pull C# above 1, the olympiad tasks do not. Basis: v1's 0.92 on a tiny task; C# build loops multiply with task size.
4. Time, bare: `active − model_api` C#/TS > 1 on 9 of 10 tasks (v1: 3 of 3 at 1.70); `model_api` C#/TS in 0.8–1.2.
5. Process arm: success rate at or above bare on 7 of 10 tasks per stack; CPC process/bare 5–20× per stack; the
   interaction (C#/TS process ÷ C#/TS bare) between 0.9 and 1.3 — the process bundle adds more to C# (T14, T15) and the
   gates catch more C# build defects, so the direction is unclear and both are named.
6. Delivery, process: ≥ 24 of 30 process sessions per stack merge into their base; compliance median ≥ 14/18.
7. Surprises named in advance: TS accepted fewer than C# by 4 or more in either arm; a task where no session of either
   stack and arm is accepted (a floor); a task where every session is accepted (a ceiling, as v1); a bare session killed at
   the wall; a process session over its cap; the ceiling binding before round 4.

## 10. Threats to validity

| # | Threat | Control, and the sentence the report carries |
|---|---|---|
| T1 | **Contamination of the known tasks.** Five tasks are public problems with public solutions in many languages; the model may recall a solution from training. The recall is not equal across stacks: more TypeScript and JavaScript solutions of CSES and RealWorld exist than C# ones | No network in the bare box, no id or title anywhere the agent reads (D4, D18, D19); the needles are read by hand; known and original tasks reported apart (D29). *"The five known tasks may be familiar to the model; a known-task result compares recall as much as reasoning, and the recall is not symmetric."* |
| T2 | **One-sided traps.** A task whose traps fire only in one stack measures the trap, not the stack | Every task's test design names traps on both sides (§4.3); the trap coverage table shows the count per stack per task; two consultant-rejected tasks (CSES 1712, 1736) were dropped for this reason (D3); the bias review of D25. *"Trap counts per stack are published; where they differ, the task's result is read with that difference."* |
| T3 | **n = 3 per task per arm.** Three pairs cannot separate a small difference from noise; v1's within-condition spread on a one-minute task was already wide | Paired concurrent design, ranges, "in k of 3 pairs"; the per-task reading first and the ten-task median second (D28). *"Differences smaller than the within-task range are not distinguished; the over-tasks figures are descriptive."* |
| T4 | **The agent's framework knowledge differs per stack.** Minimal API and Fastify are not equally represented in training data; TypeBox and `AddValidation()` (new in .NET 10) less so | The pieces are at the same level of abstraction (§4.6) and the rationale is public; `PACKAGES.md` names them to both; the record shows per-task tool and build loops. *"A web-API difference is a difference between agent-plus-framework pairs, not between languages."* |
| T5 | **Limits favouring one runtime.** A tight time or memory limit rejects the runtime with the larger baseline (JIT warm-up, V8 heap) | D14: limits from both references, 3 × and 2 ×, floors; G-17 re-checks on the series machine; per-case runtime recorded. *"No limit is below 3 × the slower reference or 2 × the larger reference peak."* |
| T6 | **Harness-induced noise.** v1's 18 instrument defects voided sessions of one stack at a time (RESULTS §11); longer sessions, drivers, cgroups, coai and GitHub add failure modes; a driver hiccup reads as a product defect | The scan's fixes carried; every void kept, replayed and reported (D26); drivers and judge hand-checked (G-15, G-16); a scenario failure is adjudicated blind as product / ambiguity / harness (§4.3); `scored under load` flagged. *"Every void is named with its cause; harness defects are classed apart."* |
| T7 | **Spec ambiguity in the originals.** Two correct readings give two correct programs and one failing suite | The team lead's specs go through the document gate, a consultant and the bias review before the freeze (D21, D25); two references written apart agree on every case before sealing (E2.S1); blind adjudication; the revised score beside the primary. *"Cases classed ambiguous are dropped for both stacks in the revised score."* |
| T8 | **The model moves under a long series.** Six rounds take days; a model change behind one id is invisible | v1's alias record (D12) stops on a new id; rounds interleave arms and tasks (D10, D27) so a change hits both stacks, both arms and every task alike; every session's time is in the record. *"A change behind the same model id is not controlled; the interleave spreads it."* |
| T9 | **Prompt-cache warmth and CPU contention.** The two cells of a pair share the machine and the cache state | Same-tick start; masks swapped per pair; the quiet check; load flagged; first-turn cache read per session. *"Warmth and load meet both stacks of a pair alike; flagged pairs are shown apart."* |
| T10 | **Floor and ceiling effects.** A task nobody passes or everybody passes separates nothing (v1 was at the ceiling) | The difficulty ladder of §4.1; prediction 7 names both cases; CPC shows "no correct solution" with its cost (D24); `percent` and `defects` carry information where `accepted` is at a floor. *"A task at a floor or a ceiling is reported as such and does not count as a difference."* |
| T11 | **Time partition limits.** One Bash chain takes its highest category (v1's V16); longer sessions have more chains | The mixed-chain count per session; `build_test` against `other_tool` descriptive only; the primary time measures are wall, `model_api`, `active − model_api`. |
| T12 | **The time guesses and the caps.** A cap derived from piloted tasks may cut a session of a task the pilot did not run; a process cap set from two piloted tasks is weaker still | §12's formula scales by the guess ratio and sets `capMax` per arm from the split; a `killed: cap` session is a censored data attempt in CPC's numerator, never dropped; the report counts them per stack and arm. *"Capped sessions are reported as bounds."* |
| T13 | **Public repository, secret or held-out leak.** A link in a workspace or a case in a sample would reach the agent or the public; the process agent's PR bodies are public | D2's rules; `leakcheck` on every push and on every PR body and comment (v1 §7.6 token scan widened); `workspace-diff`; the needles. |
| T14 | **Rule asymmetry biases the process arm.** v1 measured 34 rules / 331,833 B for C# against 32 / 324,781 B for TS; a rule that exists for one stack and not the other (a build flag, a test runner shape, a logging sink) is a treatment difference the process arm applies to one side only | **The conventions parity audit (D23, E0):** every C#/.NET rule has a Node/TypeScript counterpart and the reverse, released before the pre-registration; rule bytes actually read per session are recorded as v1's V1 asks. *"The process arm compares C#-under-the-family-rules with TS-under-the-family-rules; the rules were audited for parity at release `<sha>`, and the bytes read per session are in the record."* |
| T15 | **The process arm is a bundle and its wall is mostly waiting** (v1 V1, V3): gates, consultants, CI, `sleep 300`; the bundle's C# half carries the analyzers, `-m:4`, MTP, warnings as errors | The partition; active time beside wall; the compliance score beside `accepted`; the interaction reported with the sentence v1 wrote. *"The process arm's C#/TS difference is C#-under-the-process against TS-under-the-process, not a language effect; process wall time is largely not agent time."* |
| T16 | **Arm against time.** The two arms never overlap; v1's bare-first run confounded arm with window | D10's strict alternation of arm per round and the seeded lead; after every even round both arms have equal n and spanned the same days. *"Arms alternate by round; a window effect meets both arms."* |
| T17 | **Vendor reviewers saw the held-out cases** (the bias review of D25 sends suites to coai's vendors before sealing) | The agents under test have no network and the suite is sealed; the vendor models are not the agent under test; recorded as a residual (Q-C). *"Reviewers of other vendors read the suites before sealing; the agent under test never did."* |

## 11. Pilot and A/A (D16)

Void spend, before any data pair: the bare A/A (two identical bare C# sessions of task 3, concurrent), then the bare pilot
pairs of tasks 1, 3 and 10, then the **process pilot pairs of tasks 1 and 3** (v1's P1 conditions: merged with green
checks, coai rounds in its db, the scorer's numbers reproduced by hand, T1–T5, `num_turns` > 1, audit and canaries ran,
read-back matches the pins), after v1's P0 (no model) and model smoke have passed on this repository's templates. GO =
every row of v1's G-1 … G-14 holds (G-6 may be `explained`, G-9 never gates, the two `not exercised` cases as v1 with their
replacing evidence), plus:

| Id | Must show | A miss means | Then |
|---|---|---|---|
| G-15 | the olympiad judge by hand: one C# and one TS pilot tree, run by hand on three held-out cases, give the judge's verdicts and times within 10 % | the judge misreads a real tree | fix, re-score (no re-run) |
| G-16 | the daemon and web drivers by hand: the pilot's task-10 trees driven by hand through one scenario each give the driver's replies; the RSS curve by hand (`ps` every second) agrees within 10 %; SIGTERM by hand ends the process as the driver says | a driver misreads a real program | fix, re-score |
| G-17 | **calibration order held:** every task's limits were registered only after E2.S1 was final for that task (both references pass every visible and held-out case, no spec amendment pending), and no spec amendment is dated after that task's limit registration; both references of every task pass 100 % under the registered limits **in the scoring box on the series machine**, under the quiet check; each mutant fails exactly its cases | a limit was registered before its references were final, or a spec amendment voided it, or a limit rejects one runtime's baseline, or a trap is not caught | a spec amendment after registration **voids that task's limits**: E2.S3 runs again for it and re-registers (dated, both stacks) before the pre-registration; otherwise recompute by D14 or fix the suite; all before round 1 |
| G-18 | the cap derivation per arm and the budget split (§12) from the pilot's settled costs, written as a dated amendment; the stall timeout per arm larger than the longest quiet gap seen in any pilot session | the caps or stall would cut sessions the task needs | set them from the pilot; before round 1 |
| G-19 | the process pilot: both pilot pairs delivered (merged, green), every compliance item computable, the frozen spec unchanged in both runs, the plan gate reached `proceed` before any code gate, CPC computed by hand for one pair equals the tool's | the process arm's instruments misread a real run | fix; one more process pilot pair before round 1 |

Stops as v1: G-2, G-8, G-12 refuted, and alias drift, stop the series until fixed and an A/A is GO.

## 12. Budget (D7, D30)

The ceiling is **$1000** for v2's paid runs: pilot, A/A, data, voids and re-runs, counted on separate lines; the plan does
not change it. The spend gate before every pair: settled spend + the live O sum of running sessions + 1.1 × Σ caps of the
two sessions about to start ≤ $1000 (v1's `spendGate`, with the bare budget line replaced by **two arm lines** from the
split). The ceiling cuts whole round pairs (D10); the report says "rounds 5–6 did not run" and reports every task at the n
it reached, per arm.

**The split and the caps, derived from the pilot (D30, G-18), pre-registered as a formula:** per arm, per task,
`cap = min(2 × the larger settled cost of its pilot pair, capMax(arm))` for the piloted tasks; for the others, the piloted
task of the nearest guess scaled by the ratio of guesses (the process arm has two pilot points, so its scaling is marked
weaker); `capMax(arm)` is set so that 1.1 × Σ over the arm's 60 sessions of their caps ≤ the arm's share of $850 (the
ceiling less pilot, A/A and a re-run reserve of ≈ $150); the **share** is proposed as proportional to the arms' expected
totals from the pilot (so neither arm is starved by the formula), and is the operator's to confirm (Q-A).

**Deterministic default when the frame breaks** (the cap rule fails — `capMax(arm)` < 1.5 × any pilot cost of that arm —
or the projected total, Σ expected costs + 1.1 × the re-run reserve, exceeds the ceiling) **and the operator has not
answered:** the runner **refuses to start round 1**. It exits with the resumable-halt code **3** and the named halt
`budget-frame`, prints the measured numbers (per arm: every pilot session's settled cost, the derived caps, `capMax`, the
projected total, the ceiling) and the exact resume command, `lang-runner.mjs resume --series <dir> --amendment <date>`,
which is accepted only when a dated budget line of that date exists in `amendments.md`. **It never cuts rounds, tasks or
arms on its own.** Any cut — fewer rounds, a smaller task set, a process-arm limit of its own, a raised ceiling — is the
operator's dated amendment, applied before round 1 and recorded in the pre-registration's amendments. The gate's own
round-pair cut (D10) applies only inside a frame the operator has accepted.

**Projection, as a guess only:** v1's one-minute bare sessions cost ≈ $0.35 each; v2's bare sessions run 5–75 min with many
build and test loops, so $5–20 each → 60 bare sessions $300–1200. v1 predicted $30–70 per **process** cell on a far smaller
task → 60 process sessions $1800–4200 at that rate. Under $1000 the formula will cut rounds or the operator will change the
frame; the pilot replaces this guess and the question is open (Q-A).

## 13. Growth

| Surface | Size at this plan's volumes | Retired by / interrupted |
|---|---|---|
| Session dirs `~/rsd/lang-v2/<series>/s/<rid>/` with seeded caches (≈ 150–300 MB each), the stream and transcripts (bare ≈ 50–150 MB; process ≈ 300 MB raw as v1 §14 measured for a process cell, plus the coai dir ≤ 20 MB), the scoring copy | bare ≈ 0.4–0.6 GB, process ≈ 0.7–1 GB per session; 120 data + ≈ 12 pilot/A/A + re-runs ≈ 140 sessions ≈ **70–110 GB** on `/home` (745 GB free, 2026-10-30); the pilot's measured sizes replace this estimate | `bin/ obj/ node_modules/` shed after scoring (safe-rm under the series root, by name); the rest kept until E6's archive and `retire.mjs` after the record is promoted |
| Toolchains `~/rsd/lang-v2/toolchains/` (SDK ≈ 1 GB, Node ≈ 100 MB, TS, the CLI copy 292 MB), two cache snapshots (≈ 0.5–1 GB) | ≈ 3 GB | kept for the series; `retire.mjs` |
| Held-out source, references, mutants `~/rsd/lang-v2/{heldout-src,refs,mutants}/` | MBs | kept (the operator's record); never in git |
| `spikes/lang-bench/heldout/suite.enc`, `inputs/*`, `research/lang-v2/data/*.json` | a few MB, committed | kept |
| The public task repository: specs, workspaces, ≈ 60 process run bases + pilot + re-runs ≈ 70 `run/*` branches and ≈ 70–200 PRs | small; the branches and PRs are the audit trail | kept permanently (v1 §14) |
| coai session dirs, vendor CLI stores | as v1 §14 | with the session dir; the vendor stores are the operator's, growth measured and named |
| Credentials: one fine-grained PAT, one deploy key | live for the series | **teardown** after the last cell is scored and archived, as v1 §14 |
| Scoring cgroups and the clear suite in the scoring temp dir | one per scoring run | removed by the scorer's finalizer; `heldout-tool.mjs sweep` for leftovers older than 24 h |
| In-flight states | `launching` sessions, a scorer run, a daemon left by a driver, an open PR of a void session | `retireStale` at every `run`/`resume`; the walls, the scorer's outer limit (≤ 90 min) and the quiet wait (30 min) bound every non-terminal state; the janitor ends any process carrying the cell's marker; an open PR of a void session is closed by the harness with a comment (v1 §14) |

## 14. Build order

0. **Conventions parity audit (E0, other repository, now, in parallel):** the audit table; the missing counterpart rules
   written and released in `dew_flow_conventions`; the snapshot branch cut at that release — before step 4.
1. **Task repository (E1):** the operator creates `dew_flow_ai_language_comparison` (public); the team lead writes the ten
   specs, each gated (document gate + consultant + bias review) and frozen (tag, hashes); the workspaces with `build.sh`,
   `run.sh`, pins, `PACKAGES.md`; samples; the Node stack rationale in the README; toolchains pinned; the two cache snapshots;
   the process-arm templates, CI, rulesets, PAT and deploy key (E1.S7, built here to v1 §6's design, D32).
2. **References and suites (E2), in a strict order per task:** the visible samples in the fixed `<id>.in` / `<id>.out`
   format (E1.S6, change 1.2b) → the two references **final** (both pass every visible and
   held-out case, no spec amendment pending — E2.S1) → the shared suites, mutants and the trap coverage table (E2.S2) → only
   then calibration and **limit registration** (E2.S3, by D14 on the series machine); a spec amendment after a task's
   registration voids that task's limits and its calibration runs again before the pre-registration; the bias review of each
   suite; sealed; `leakcheck` clean.
3. **Harness (E3):** `spikes/lang-bench/`; drivers and judge, and `research/module_tests.md` updated with their flows; the
   runner's arm, task and round dimensions; D15's env; the widened categories and records; CPC in the record; G-15 … G-19;
   the process arm built here (the coai box and bridge, the process runner, the compliance checker with the `spec unchanged`
   item, P0 and the model smoke — E3.S6–S9, D32); stub tests; a ≤ $0.20 bare smoke on task 1.
4. **Pre-registration (E4.S3)** committed and tagged before the A/A; then the bare A/A, the bare pilot pairs, the process
   pilot pairs, the cap and split derivation (dated amendment), the seed reveal.
5. **Series (E5):** six alternating rounds under the gate.
6. **Record (E6):** `research/lang-v2/RESULTS_<date>.md` generated from the records; blind adjudication; the references,
   the trap table and the specs' final state published; teardown, archive, retire; this plan promoted with its deviations.

## 15. Test plan

`node spikes/lang-bench/run-tests.mjs` (v1's runner shape: lowered priority, bounded workers; `BENCH_REQUIRE_SANDBOX=1`
turns host skips into failures).

| Id | Guarantee |
|---|---|
| T-L1 | the task repository fetch: a workspace file not matching `hashes-lang.txt` is refused; `workspace-diff` passes on the ten task pairs and fails on a planted extra file |
| T-L2 | the bare workspace of a session holds exactly the task folder's files (no root files, no other stack, no `todo/`) |
| T-L3 | the olympiad judge: fake programs that print wrong output, sleep past the limit, allocate past the limit, exit non-zero, each get their class; a right one passes; peak memory comes from `memory.peak` when present, from samples otherwise, never from `RLIMIT_AS` |
| T-L4 | the process driver: a stub daemon that ignores SIGTERM is killed after the grace period and reported `stop failed`; one that exits 0 passes; a stub that leaves a child is reported `leftover`; RSS samples are every second ± 100 ms |
| T-L5 | the TCP driver against a stub RESP server: partial frames, pipelined commands, a slow reader (backpressure) each scored by the scenario's expectation |
| T-L6 | the HTTP driver: a scenario with k concurrent identical requests checks the invariant once; a 5xx or a timeout is `fail`, never `not run` |
| T-L7 | limit calibration: given two reference measurements, D14's formula gives the registered limits; a reference failing under them is a calibration failure, not a passing suite |
| T-L8 | trap coverage: every mutant fails exactly its registered cases and no other; both references pass every case; a trap with no catching case is a failure of the table |
| T-L9 | the order: six rounds with arms strictly alternating and the lead drawn from the seed; every task once per round; rids distinct over arm, task, stack, repeat and attempt; masks swap per pair; a task's second repeat in an arm never precedes another task's first |
| T-L10 | the spend gate with per-arm caps and the one ceiling; the round rule refuses round k+1 while round k has an unscored pair; the ceiling cuts a round pair whole; **the budget frame:** pilot costs that break the cap rule or the projection make `run --what round:1` exit 3 with halt `budget-frame`, print every measured number and the resume command, start nothing and cut nothing; `resume --amendment <date>` is accepted only with a dated budget line of that date |
| T-L11 | the record has every per-kind and per-arm field known, `not captured` with a reason, or `n/a (<kind \| arm>)`; spawn and exit timestamps present |
| T-L12 | the contamination needles for the known tasks hit in tool inputs and outputs and are reported for a hand reading, never auto-voided |
| T-L13 | offline caches (effect, in a box): with the registries refused by the proxy, `npm install fastify@5.12.7` and `dotnet restore` of the pinned project succeed; a package outside the set fails naming it |
| T-L14 | D15's env keys are in the box and in the scoring box and in the record; `agentEnv` refuses a clash in any case |
| T-L15 | the classifier: `bash build.sh`, `bash run.sh`, `dotnet publish`, `node --test`, the drivers' commands land in `build_test`; the process categories as v1; the chain rule unchanged |
| T-L16 | `aaVerdicts` G-15 … G-19: each broken fixture refutes exactly its G; G-17 with one reference over a limit is NO GO; G-19 with a changed spec blob is NO GO |
| T-L17 | the record render: the generated blocks come from `data/*.json` only; a typed number is a failure; the licensing paragraph of §9 is present verbatim; known and original tasks are separate tables |
| T-L18 | **CPC:** three attempts costing 2, 3, 4 with one accepted → 9; none accepted → "no correct solution", cost 9, ratio a bound; a void attempt is excluded from CPC and included in `CPC_all`; a `killed: cap` attempt counts its censored cost |
| T-L19 | **the overall figures:** ten per-task ratios → the median, the geometric mean (a bound enters marked) and the count of C# higher; one task at 100× moves the geometric mean and not the median, and the test asserts both numbers |
| T-L20 | **the spec-freeze guard:** a session whose spec blob at the run base differs from the frozen tag is refused before launch; a process session whose PR changed the spec gets the compliance item `spec unchanged: not met` and the flag |
| T-L21 | the Python suite runner runs unchanged against a C# and a TS stub implementation of one fixture task and reports the same case list; it imports nothing outside the standard library (a test greps the imports) |
| T-L22 | the process-arm compliance checker (built here, E3.S8) computes 18 items for a synthetic run from GitHub JSON, a `coai.db` copy and a stream, the 18th from the spec blob |
| T-L23 | the coai bridge refuses a `tools/call` whose `repoPath`, `committedPlan` or `suspectedFiles` resolves outside the session (symlinks and `..` included) with a JSON-RPC error naming the rule, and passes one inside (v1's T-B1) |
| T-L24 | the process runner: a run base cut from the frozen tag, a session's clone single-branch with a restricted refspec and `--no-tags`, a push outside `run/<rid>/` flagged, `delivered` read from the merge into the base, an undelivered session scored on its PR head and marked |
| T-L25 | the calibration order: `register-limits` for a task is refused while that task's references are not final or a spec amendment is dated after the last calibration; a spec amendment after registration voids that task's limits in `inputs/limits-lang.json` (a test on the pure function) |
| T-L26 | **metric retention (D34):** the v2 record renderer, run over a fixture series, emits every v1 metric column (the four cost views, T1–T5, tokens with the cache split, wall, `model_api`, `active − model_api`, held-out counts/percent/defects, class, labels, flags, charges) beside the new ones; a test removes one v1 column from the renderer's list and fails |

Live: the bare smoke (E3.S5), P0 and the model smoke (v1's, on this repository), the A/A and the pilots (E4), the by-hand
G-15/G-16/G-19 lines, the round-1 read-back of one pair per arm.

## 16. Definition of Done

- [ ] Every input but the stack and the arm is pinned in the harness; one session per stack per arm in round 1 was read
      back by hand to prove the arms differ only there (measurement rule 1).
- [ ] The judge, the drivers, the suites and the process instruments were hand-checked (G-15 … G-19) before their numbers
      appear (rule 2); the specs were gated and frozen before any paid session.
- [ ] The conventions parity audit ran and its release is pinned in the task repository before the pre-registration (D23).
- [ ] `prediction.md` was committed before the pilot and `seed.txt` after the A/A; the report puts observation beside
      prediction (rule 4).
- [ ] T1–T5 held for every data session, or the breach is in the report with what was done.
- [ ] Every cost carries its basis; pilot, A/A, data and void spend on separate lines per arm; spend ≤ $1000 or the stop and
      the missing rounds named; every attempt, void and re-run included, is in the record (D26).
- [ ] CPC is the headline, computed as §9 says; "no correct solution" rows are present; the overall figures are the median,
      the geometric mean and the count, per arm; known and original tasks are shown apart.
- [ ] Time is partitioned (Σ = wall); no span sums; scoring time apart from agent wall; active time beside wall in the
      process arm.
- [ ] Every failing held-out case was classed blind; the trap coverage table, the references and the specs are published
      after the series.
- [ ] The report names the task repository sha and freeze tag, the parity release sha, the harness identity, the prereg tag,
      the CLI version, the toolchain checksums, the cache hashes, the machine and the dates (rule 6); its conclusion is no
      wider than §9.
- [ ] The public repository holds no secret, transcript, held-out case or link in a workspace (`leakcheck` clean on every
      push and PR); credentials torn down after the series.
- [ ] `research/module_tests.md` lists the harness's runner command and every scenario flow as covered or not covered with
      a reason, and says what the harness does not prove (E3.S2).
- [ ] Limits were registered only after the references were final, and every spec amendment after a registration voided
      and re-ran that task's calibration before the pre-registration (G-17).
- [ ] **Follow-up outside this plan's commits:** v1's parent plan [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md)
      carries a STATUS line saying its process arm moved to this plan (a status update, not a plan change), and v1 is closed
      after its bare arm (D32).
- [ ] `research/lang-v2/` and `research/README.md` updated; the module document describes the extension; this plan and its
      stories documents promoted with deviations; the worktree removed.

## Deviations

None yet. Filled at promotion: what shipped differently from this plan, and for every "impossible", what was CHECKED.

## 17. Open questions

Not settled by this plan; the operator or the named epic decides. No answer is invented here. Removed by the afternoon's
instructions: the former Q2 (public from day one — D31; the conventions pin — D23) and the former Q12 (the ceiling cuts
whole round pairs — D10, now with two arms). Removed by the coai plan-gate round on `aa5e413`: **Q-B** (v2 builds the
process-arm pieces itself; v1 closes after its bare arm — D32) and **Q3** (Werewolf option (a) — D33). The budget frame's
default when the operator has not answered is now fixed (§12); Q-A keeps only the operator's choice itself.

- **Q-A Does $1000 cover both arms, or does the process arm need its own limit?** 120 sessions; v1 predicted $30–70 per
  process cell on a smaller task. The pilot gives the numbers; the operator decides between fewer rounds, a smaller task
  set, a separate process limit or a raised ceiling (§12). This plan leaves $1000 as set.
- **Q-C The bias review sends held-out suites to coai's vendors** before sealing (D25). The agents under test never see
  them, but other vendors' models do. Accept the residual, or review the suites by an in-house Opus judge only and send the
  vendors the specs alone?
- **Q-D The process agent's clone** holds the whole repository at the frozen tag: both stack folders (empty templates) and
  all ten specs. Accept as a property of the arm (§7), or cut per-task run bases that remove the other tasks' specs?
- **Q-E Process-arm caps and walls.** v1 planned $35 / 240 min for minigit; v2's tasks are 10–60× larger in agent time. The
  pilot sets them (G-18); should the process wall have a ceiling of its own (for example 6 h) so one session cannot take a
  day?
- **Q1 Licences and credit.** The licence of the new public repository (our originals, the specs, the workspace templates,
  the visible suites), and the terms under which the known problems may be paraphrased: CSES statements, IOI 2018 Werewolf
  (the IOI's task licence to be read), RealWorld's spec (MIT), Redis's RESP (a protocol; the trademark). v1's D18 found
  upstream files with no licence; v2's credit text is written after these are read.
- **Q4 Memory limits in the box.** D14 assumes cgroup v2 `memory.max`/`memory.peak` is available to the scorer inside
  `bwrap` on this WSL2 host. If not, the fallback is RSS sampling with a kill above the limit, which is coarser; E3 checks
  this first and reports which one the series uses.
- **Q5 The cap formula** of §12 is a proposal; the operator confirms the formula and the share rule or replaces them before
  the pre-registration.
- **Q6 Any inferential statistic?** §9 reports the median, the geometric mean and the count. A pre-registered sign test over
  the ten task medians is possible as a secondary line with the "chosen set, not a sample" sentence; wanted or not?
- **Q7 The pilot's task set.** Bare: tasks 1, 3, 10 (D4). Process: tasks 1 and 3 proposed (D16). Adding one web API task
  (4 or 8) to the bare pilot would calibrate the HTTP driver before round 1 at ≈ 2 sessions' cost; adding task 10 to the
  process pilot would calibrate the dearest process sessions at a high cost. Which sets?
- **Q8 Culture for the Rule API (task 4).** The C# behaviour needs the host culture `de-DE` in the **scoring** box (and in
  the agent box, so the agent can meet it?) — setting it in both is the honest treatment; setting it only in scoring is a
  trap the agent could not have seen. Which, and does the spec say the culture is not invariant?
- **Q9 Offered test frameworks.** xUnit v3 for C# and `node:test` for TS are proposed; `node:test` is built in while xUnit
  is a package. Is a C# package-free option acceptable as equal footing, or is this asymmetry simply recorded (and read
  against the parity audit)?
- **Q10 Re-pin of the CLI (D8).** The plan re-pins at pre-registration to the then-current release. If the operator prefers
  to keep 2.1.310 for comparability with v1, say so before E4.S3.
- **Q11 Publication of references and specs' final state.** After the series, in the public repository (proposed) — or only
  in the record?

## Epics

Seven epics on the build order of §14, each one branch from the previous epic's commit (E0 in another repository, in
parallel), one reviewable diff and a usable state at its end. Stories are written one epic at a time into
`todo/PLAN_lang_comparison_v2_E<n>.md` when that epic starts (`common.planning-docs` § *A plan split into epics and
stories*); that document links back here. The intended stories with their acceptance criteria are listed per epic below so
the size is visible. Cornerstone areas are named per epic; the concrete marks are confirmed by the epics' stories documents,
at most Q = ⌈2·E/5⌉ = **3** for E = 7, at most one per epic. The three areas ranked first: (1) the gated, frozen specs with
the bias review — an ambiguous or biased spec makes every failure unadjudicable (E1); (2) the shared suites with the trap
coverage table and the limit calibration — the acceptance instrument of the whole experiment (E2); (3) the daemon and web
drivers with RSS and SIGTERM — a new instrument v1 never had (E3). The parity audit (E0) and the pilot's go/no-go (E4) get
strict specs without a mark. **Recounted after D32:** the process arm adds a fourth cornerstone area to E3 — the coai bridge
as the boundary on every `tools/call` (v1 named it so) — but E3 already holds its one mark and the ledger is full
(k = 3 = Q), so E3.S6 gets a strict spec without a mark; a fourth mark is the operator's to grant, as v1's rule says.
Nothing here or in a stories document names a held-out case, mutant or reference in clear.

Cornerstones: 3 of 3 — E1.S2, E2.S2, E3.S2

### E0 — Conventions parity audit and release (§14 step 0 · 3 stories · `dew_flow_conventions`, in parallel, now)

- **Goal:** every C#/.NET rule in `dew_flow_conventions` has a Node/TypeScript counterpart adapted to Node, and the reverse,
  released and pinned by the task repository before the v2 pre-registration (D23, T14).
- **Scope:** the rule inventories `csharp/*.md` and `typescript/*.md` at the current `release`, rule by rule (selection
  metadata, obligations, Definition of Done items); the always core and `common/*` read for stack-specific clauses; the
  gap table; the new or widened rules; a plan in `dew_flow_conventions/todo/` naming this boundary from that side
  (`common.planning-docs` § *Across repositories*).
- **Depends on:** nothing — it starts now.
- **Done when:** the gap table is empty or every remaining gap is a recorded decision with a reason; the release tag exists;
  `rules.mjs explain` for the process arm's task set gives rule counts and bytes per stack within 10 % of each other, or the
  difference is explained rule by rule in the audit record.
- S1 — **the audit** (runs now): the inventory and the gap table, per rule: id, stack, counterpart id or `missing`, the
  obligation that has no twin, bytes. *Accepted when:* every rule of both stack folders appears once; the table is in
  `dew_flow_conventions/research/` (the record) and cited here by path.
- S2 — the counterpart rules written and released: each gap closed by a new rule, a widening of an existing one, or a
  recorded "no counterpart needed" with its reason; released to `release` by that repository's process. *Accepted when:*
  that repository's own checks are green; the release sha is in this plan's pre-registration.
- S3 — the task repository pins the release on a snapshot branch (v1 D9); `pin-check` green; the process arm's `rules.mjs
  explain` byte counts per stack recorded. *Accepted when:* the pin and the counts are in `inputs/conventions-lang.json`.
- **Cornerstone area:** none marked (strict spec, as v1's conventions rows).

### E1 — The task repository: specs gated and frozen, workspaces, toolchains, caches, process templates (§14 step 1 · 7 stories)

- **Goal:** ten specs written by the team lead, gated and frozen; ten workspaces per stack an agent can start in, identical
  across stacks but for the stack, on pinned toolchains and offline caches; the process arm's run-base machinery; in a
  public repository that leaks nothing.
- **Scope:** D2, D5, D6, D11, D12, D19, D21, D25 (the spec half of the bias review), D31; §4.1, §4.5 (the stack sentences),
  §4.6, §5.
- **Depends on:** the operator creating the repository; the licence answers (Q1); E0.S3 for the pin; v1 §6's template
  design for S7 (D32: built here, nothing imported).
- **Done when:** T-L1, T-L2, T-L13, T-L20's guard half green; `workspace-diff` clean on ten tasks; both snapshots hashed; the
  preflight passes inside a box on SDK 10.0.403, Node 26.13.0, TS 7.0.2, the pinned Python; the ten specs carry a gate
  verdict and the freeze tag; the README carries the credits and the Node stack table; `leakcheck` clean.
- S1 — repository skeleton: layout of §5, conventions pin (E0.S3), README with credits and the §4.6 table, LICENSE (Q1), the
  `workspace-diff` check. *Accepted when:* the check passes on an empty task pair and fails on a planted difference; the
  README's links appear nowhere under `csharp/`, `node/`, `tasks/` or `todo/tasks/` (a test greps).
- S2 — **the ten task specs, written by the team lead, gated and frozen** (cornerstone area; D21, D25). Per task, in the
  order of §4.1: the spec at `todo/tasks/NN-slug.md`; the coai document gate with `purposeText` (who acts on it: the agents
  under test in both stacks and the suite's author; what they must be able to do: build a program that passes the hidden
  suite from the text alone; what it does not cover: the tests, the traps), a consultant, the bias review question of §4.3;
  every finding `accept`ed or rejected with a reason; the verdict and notes recorded in `inputs/bias-review/NN.md` (no
  case text). Then the tag `spec-frozen-<date>` on the commit holding all ten, their sha256 in `inputs/specs.json`.
  *Per-task acceptance:* the gate's verdict is `proceed`, `good_enough` or `continue_anyway`; the consultant answered; the
  bias question got an explicit answer per stack with no open finding; no id, title, site or trap in the text; a known
  task's needles (D18) find nothing; the limits, I/O and start contracts, frozen list and "hidden tests exist" are present;
  two readers (an Opus judge and the consultant) list no open question, or every one is resolved by a text change before
  the gate round that passed. *Whole-story acceptance:* the ten verdicts and the tag exist before E2.S1 starts writing a
  reference; after the tag a spec changes only by a dated amendment applied to both stacks and before the pre-registration.
- S3 — the workspaces: `STATEMENT.md` generated from the frozen spec plus the stack sentence, `build.sh`, `run.sh`,
  `PACKAGES.md`, pin files per stack, the C# `.csproj` skeleton and `nuget.config` offline source, the Node
  `package.json`/lockfile/`tsconfig.json`/`.npmrc offline`/`eslint.config.mjs`. *Accepted when:* in a box with no registry,
  each stack's empty workspace builds and `run.sh` starts a hello program; T-L2; the generated statements equal the frozen
  spec's text plus the sentence (a test); **task 9 (Werewolf, D33):** `csharp/09-*` and `node/09-*` hold exactly the same
  file set as every other olympiad workspace — no adapter, helper, parser or grader code — and the spec's stdin/stdout
  grammar is the only I/O contract (a test lists the files and greps for a parser).
- S4 — the package sets and cache snapshots: the recipes, the exact pinned sets with hashes in the cache manifest,
  `PACKAGES.md` generated from the manifest. *Accepted when:* T-L13; the manifest lists every package and version; a
  package outside the set fails naming it; both hashes in `inputs/cache-snapshots-lang.json`.
- S5 — toolchains pinned: SDK 10.0.403 and Node 26.13.0 installed from official artefacts into `~/rsd/lang-v2/toolchains/`,
  read-only, checksums recorded; Python's checksum; the preflight pins. *Accepted when:* the preflight refuses a box whose
  `dotnet --list-sdks`, `node --version` or Python checksum differs; the checksums are in `inputs/stacks-lang.json`.
- S6 — the visible samples (`tasks/<task>/samples/`, copied into both workspaces) as the visible part of E2's suites, in the
  **fixed format `samples/<id>.in` / `samples/<id>.out`, bytes exact** (change 1.2b; the spec's sample table shows the same
  bytes with the `⏎` glyph). E2.S2's runner reads this format; this story fixes it, E2.S2 does not choose it. *Accepted
  when:* every spec's sample table and its `samples/` files hold the same bytes (a test); the Python runner runs them
  against both references (E2.S1) and both pass; no sample reaches a trap (the trap table says which cases do, and none is
  a sample).
- S7 — the process-arm templates in this repository: CI with one job per stack, family checks, PR title; rulesets
  `run-main`, `template-frozen`, tags; the deploy key as the only bypass actor; the PAT; `GitHub settings` as v1 §6 —
  built here to v1 E2.S4's design (D32). *Accepted when:* v1's P0 checks pass here: a run base cut by the deploy key, a direct push
  with the PAT refused, CI green on a non-default base, a planted violation red per stack, merge blocked until threads
  resolve. Built here to v1 §6's design (D32), for one repository with two stack folders.
- **Cornerstone area:** the specs (S2).

### E2 — Shared acceptance suites, references, trap coverage, limits, bias review (§14 step 2 · 5 stories)

- **Goal:** the acceptance instrument calibrated before any model touches a task: one neutral Python suite per task that
  runs unchanged against both stacks, two references per task, suites that catch every designed trap, limits that reject
  neither runtime's baseline, and a bias review of each suite.
- **Scope:** D13, D14, D22, D25 (the test half); §4.3 calibration; the sealed archive; `leakcheck`.
- **Depends on:** E1.S2 frozen specs, E1.S5 toolchains, E1.S6 samples.
- **Done when:** T-L7, T-L8, T-L21, T-L25 green; G-17's conditions hold on the series machine (recorded, re-checked at the
  pilot), including the calibration order; the archive sealed with hashes and counts in `inputs/heldout-prereg-lang.json`;
  the ten bias-review verdicts recorded; `leakcheck` clean on the branch.
- **Order inside this epic, strict per task:** S1 final → S2 → S3. S3 registers a task's limits only when S1 is final for that
  task; a spec amendment dated after a task's registration voids that task's limits, and S3 runs again for it before the
  pre-registration (G-17).
- S1 — references: one C# and one TypeScript reference per task, written from the frozen spec only, by two different
  writers per task where possible. *Accepted when (= "final" for that task):* both pass every visible and every held-out
  case; every disagreement between them was traced to the spec and, where the spec was at fault, fixed by a dated amendment;
  **no spec amendment is pending** for the task; the dated list and the final mark per task are in `inputs/references.json`.
- S2 — **the shared suites and the trap coverage table** (cornerstone): per task one Python module (standard library only)
  with the stdin/stdout, HTTP or TCP/RESP + signals mode, **reading the fixed case format of E1.S6** (`<id>.in` /
  `<id>.out`, change 1.2b); the held-out cases (counts registered); the table task × stack × trap → cases; mutants one per
  trap per stack; **every case cites the requirement ids (R1 … Rn) it asserts** (change 1.2c; v1's "names the spec
  sentence" rule, now by id). *Accepted when:* T-L8, T-L21; every trap of §4.3 has at least one catching case; every case
  cites at least one R-id that exists in the frozen spec (a test against `inputs/specs.json`'s id lists); the table and the
  mutants' ids are sealed; no case appears in git in clear.
- S3 — calibration and limit registration by D14, **only after S1 is final for the task**: reference measurements in the
  scoring box, the formula applied, the limits registered per task in `inputs/limits-lang.json` with the spec hash they were
  calibrated against. *Accepted when:* T-L7, T-L25; the registration is refused for a task whose S1 is not final; both
  references pass under the limits in three consecutive runs under the quiet check; a spec amendment after registration
  voids that task's entry and the re-run re-registers it, before the pre-registration; every registered entry names a spec
  hash equal to the frozen (or last amended) spec.
- S4 — the bias review of each suite (D25): spec + suite in clear from a scratch dir to the coai document gate with
  `documentText` and a consultant, the §4.3 question; findings resolved; verdict and notes in `inputs/bias-review/NN.md`
  (no case text). *Accepted when:* ten verdicts, each `proceed`/`good_enough`/`continue_anyway`, every finding
  resolved; a finding that changed a case is traced to a spec sentence; Q-C's answer recorded.
- S5 — adjudication packets for v2: v1's `adjudicate.mjs` pointed at the v2 series with stack and arm masked. *Accepted
  when:* a synthetic series produces packets that show case, output and spec sentence and nothing of the stack or arm.
- **Cornerstone area:** the suites and the trap table (S2).

### E3 — Harness extension: two arms, long sessions, drivers, judge, scoring per kind, CPC in the record, the process arm built (§14 step 3 · 10 stories)

- **Goal:** one command runs one pair of any task in either arm under the v1 rules, scores it by its kind, computes CPC's
  inputs, and leaves a record the report can be built from; v1's instruments untouched but for named widenings; the
  process arm **built here to v1's design** (D32), never copied from code that does not exist.
- **Scope:** §6; D10, D15, D17, D18, D24, D27, D32; §11's G-15 … G-19; the smoke; the coai box and bridge, the process runner,
  the compliance checker, P0 and the model smoke on the task repository; `research/module_tests.md`.
- **Depends on:** E1 workspaces, caches and the repository-side templates, rulesets, PAT and deploy key (E1.S7); E2's sealed
  archive for the stub tests' shape.
- **Done when:** T-L3 … T-L6, T-L9 … T-L12, T-L14 … T-L16, T-L18 … T-L20, T-L22 … T-L24 green; a stub pair of each kind in
  each arm runs end to end in real boxes; the bare smoke (task 1, TS, ≤ $0.20) is read by E/O/T/B with T1–T5; P0 passes on
  the task repository's templates; the model smoke lists the vendors through the bridge; `research/module_tests.md` carries
  the harness's flow catalogue; the module document describes the extension.
- S1 — the arm, task and round dimensions of the runner: `ridOf` with arm and task and a `single` phase, six alternating
  rounds from the seed, the pair without barrier, the void schedule without v2 rows, the prereg guard's v2 list and the
  spec-freeze guard, `EGRESS_HOSTS.langBare` / `langProcess`, timestamps in the record. *Accepted when:* T-L9, T-L10,
  T-L12, T-L20; `run --dry-run` prints six rounds with their arms.
- S2 — **the process, TCP and HTTP drivers with RSS sampling and the SIGTERM check** (cornerstone), as thin wrappers over the
  Python suite runner, in a scoring cgroup (Q4 answered first). *Accepted when:* T-L4, T-L5, T-L6; a driver never leaves a
  process (the janitor finds none after each test); **and `research/module_tests.md` is updated** (`common.knowledge-base`,
  the scenario-tests rule) with: (i) the exact runner command (`node spikes/lang-bench/run-tests.mjs`, with
  `BENCH_REQUIRE_SANDBOX=1` and the glob for the driver tests); (ii) every scenario flow this epic adds, each marked
  `covered` or `not covered` with a reason — stdin/stdout case under time and memory limits, each verdict class, HTTP
  scenario step, HTTP concurrency scenario (k identical requests, one effect), TCP/RESP scenario with partial frames,
  pipelining and a slow reader, readiness wait, RSS sampling every second, the RSS bound, SIGTERM clean stop within the
  grace period, a program that ignores SIGTERM, a leftover child, cgroup `memory.peak` against the sampling fallback, the
  offline cache in the scoring box, the scoring-time record; (iii) **explicit limits on what the harness proves** — it
  proves a program's observable behaviour against the registered cases and scenarios on this machine under these limits,
  not correctness in general; a Python driver bounds the load it can apply; RSS is sampled, so a peak shorter than the
  sample interval can be missed unless `memory.peak` is present; the stop check sees the session's processes, not threads;
  a case classed ambiguous is a spec matter, not a harness proof. A test (T-L17's shape) asserts that every flow id named in
  the drivers' test files appears in the catalogue.
- S3 — the stdin/stdout judge with cgroup limits and verdict classes. *Accepted when:* T-L3.
- S4 — scoring per kind in the pipeline, `accepted`/`percent`/`defects` from the trap table, scoring time recorded; the
  process arm scored on the run base tip or the PR head with the `delivered` mark. *Accepted when:* T-L11; a stub tree of
  each kind in each arm gets a `score.json` with every step known.
- S5 — D15's environment, the widened classifier, `aaVerdicts` G-15 … G-19, CPC and the overall figures in the record
  tools, the bare smoke. *Accepted when:* T-L14, T-L15, T-L16, T-L18, T-L19; the smoke's record is complete and its cost is
  in the ledger.
- S6 — **coai in its own box and the stdio↔socket bridge** (v1 §7.2 and E2.S3's design; a cornerstone area, unmarked —
  see the recount above): one `coai-mcp` per process session as the operator, in a second `bwrap` that binds only the
  session directory (ro, plus `<session>/coai` rw), the vendor CLIs' own homes, the coai binary and the toolchains, `/home`
  and `/mnt` otherwise hidden; `COAI_DATA_DIR=<session>/coai`; the series' coai settings snapshot with `COAI_QCONSULT_ROOTS`
  unset (v1 D14); the bridge inside the agent box parses each JSON-RPC `tools/call` and refuses any path-shaped argument
  that does not resolve inside the session directory, answering a JSON-RPC error naming the rule; `--strict-mcp-config` and
  `mcp.json` pointing at the bridge; `ToolSearch` in the tool list for deferred schemas. *Accepted when:* T-L23; a
  `providers` call through the bridge from inside the agent box lists the vendors with their own sign-ins while the agent
  box holds none of them; a `consult` whose `repoPath` is this repository is refused; the held-out key is in neither box's
  environment (a test reads both `environ`s); a reviewer round runs end to end against a stub repository in the session.
- S7 — **the process runner**: the process session verbs over v1's bare runner (E3.S1's dimensions) — run base `run/<rid>/main`
  cut from the frozen tag with the deploy key, the single-branch clone with a restricted refspec and `--no-tags`, the PAT in
  the session gitconfig through `gh auth git-credential` (never on an argv), `GH_CONFIG_DIR`, `GH_PROMPT_DISABLED=1`, the
  egress list `langProcess`, `github.mjs` (PRs, checks, merges, intervals for `ci_wait`), `delivered` read from the merge
  into the base, an undelivered session scored on its PR head and marked, the audit's foreign-namespace rules (pushes and
  PRs outside `run/<rid>/`, other `run/*` reads), the token scan over pushed files, PR bodies, comments and commit messages,
  the void `no reviewer`, the open PR of a void session closed with a comment. *Accepted when:* T-L24; a stub process pair
  runs end to end against the task repository on a `run/smoke-*` base with the stub CLI (free): base cut, clone, a
  harness-authored PR, CI, merge, `delivered`, the record complete; a direct push with the PAT to a foreign `run/*/main` is
  refused by the rulesets (read back).
- S8 — **the compliance checker** over GitHub, `coai.db` and the stream (v1 §5.4's 17 items plus `spec unchanged`), with the
  stratified hand-check protocol (≥ 20 verdicts, every item and every process cell, every "not met"; a disagreement above
  1 in 20 fixes the checker before any number). *Accepted when:* T-L22; the checker computes every item for the S7 stub run
  and for a synthetic run with each item broken once; C#-only items are excluded from the cross-stack score by configuration,
  not by hand; the hand-check sheet is generated.
- S9 — **P0 (no model) and the model smoke** on the task repository (v1 §15 step 8's checks, adapted): a harness-authored
  PR per stack folder to `run/smoke-*/main`, base creation by the deploy key, a direct PAT push refused, CI on a non-default
  base, a planted lint or analyzer violation red per stack, merge blocked until threads resolve and a thread resolved with
  the PAT unblocks it, push CI green; from inside the agent box the operator's home and the held-out suite are unreadable;
  then the model smoke (≈ $0.10): one headless process session whose only prompt is to call coai `providers` through the
  bridge. *Accepted when:* every P0 check passes on both stack folders and is recorded; the smoke's init event shows coai
  connected and the vendors listed; the smoke's cost is in the ledger as pilot spend.
- S10 — the module document: the extension described (entry points, exit codes, the drivers' contracts, the judge's
  classes, the arms, the bridge's rule, the process verbs). *Accepted when:* every command in it runs as written.
- **Cornerstone areas:** the drivers (S2, marked); the bridge on every `tools/call` (S6, unmarked — ledger full).

### E4 — Pre-registration, A/A and the two pilots (§14 step 4 · 4 stories)

- **Goal:** the analysis fixed in public history before the first paid task session; the instruments proven on real trees in
  both arms; the caps and the split derived; the seed revealed.
- **Scope:** §9, §11, §12; D4, D8 (the CLI re-pin), D16, D26, D30.
- **Depends on:** E0.S3 (the parity release pinned), E1–E3 complete.
- **Done when:** the tag `prereg/lang-v2-<date>` exists before the A/A's first launch and after E0's release; `AA.md` says
  GO; the bare and process pilot pairs ran; the cap, split, stall and limit amendments are dated lines; `seed.txt` revealed;
  pilot spend in the ledger per arm.
- S1 — the day-1 price re-read and the CLI pin copy (D8, Q10). *Accepted when:* `prices-<date>.json` dated that day; the CLI
  copy's sha256 recorded.
- S2 — `prediction.md` (§9's draft, finalised), `statement.md`, `analysis-contract.md` (CPC, the per-task and overall
  figures, known/original, the void and re-run grounds), the outcome table, the limits, the package sets, the hashes, the
  specs' freeze tag and hashes, the parity release sha, `seed.sha256`. *Accepted when:* v1's prereg tests widened pass
  (verbatim-from-main, derived not typed, seed as hash only, no held-out needle).
- S3 — the commit and the tag, before any paid session. *Accepted when:* the guard peels the tag to the commit.
- S4 — the bare A/A, the bare pilot pairs (tasks 1, 3, 10; Q7), the process pilot pairs (tasks 1, 3; Q7), G-1 … G-19, the
  cap and split derivation (§12), the amendments, the seed reveal. **If the frame breaks** (§12: `capMax(arm)` < 1.5 × a
  pilot cost, or the projection exceeds the ceiling) **and the operator has not answered, the default is fixed:** the
  runner refuses round 1 with exit 3 and halt `budget-frame`, prints the measured numbers and the resume command, cuts
  nothing; the operator's dated budget amendment is the only way on (Q-A is then the operator's choice, not the runner's).
  *Accepted when:* GO; every amendment is instrument-only, a budget line, or applied to both stacks; the formula's inputs are
  in the ledger; T-L10's budget-frame case is observed once against the real pilot ledger (`run --what round:1 --dry-run`
  prints the refusal when the frame is broken, or the go when it holds).
- **Cornerstone area:** none marked (the go/no-go table is written strictly, as v1's E3).

### E5 — The paid rounds (§14 step 5 · 2 stories)

- **Goal:** the six alternating rounds run under the pre-registered contract, under the gate, every attempt kept.
- **Scope:** §4.4 order, §9 voids and re-runs, §12's gate and stop rules, the round-1 read-back per arm.
- **Depends on:** E4's GO and seed.
- **Done when:** six rounds ran, or the gate or a stop ended the series and the record names the missing round pairs; every
  session has `record.json` with its timestamps, every pair its pair record; the read-back lines are dated.
- S1 — rounds 1–6 with the stop rules; resume after any halt. *Accepted when:* every session is classed; voids re-run by
  the schedule; spend lines settled per arm; no round k+1 started with round k unscored.
- S2 — the round-1 and round-2 by-hand read-back of one pair per arm (argv, read-back, binds, cache hashes, package list,
  env keys, the spec blob, in the process arm the pin and coai's `providers`) by an Opus judge, as dated lines; a mismatch
  reclassifies through `handVerdicts`. *Accepted when:* the lines exist before round 3.
- **Cornerstone area:** none.

### E6 — The research record (§14 step 6 · 3 stories)

- **Goal:** a generated record in `research/lang-v2/` that types no number, puts CPC first per task and the pre-registered
  overall figures second, says what §9 licenses, and publishes what the series held back.
- **Scope:** §8's views, §9's tables, the blind adjudication, the replay of every void, the references, the trap table and
  the specs published, teardown, archive and retire, promotion.
- **Depends on:** E5.
- **Done when:** every §16 box is checked.
- S1 — `research/lang-v2/tools/` from `research/minigit/tools/` widened to arms, tasks and kinds; `data/<series>.json`;
  `RESULTS_lang_v2_<date>.md` with generated blocks: per task (CPC, success, tokens, time, both arms), the overall median,
  geometric mean and count per arm, known against original, process effect and interaction, every attempt listed.
  *Accepted when:* T-L17; the record renders from the data alone.
- S2 — the blind adjudication of every failing case; the voids replayed with the current scan; the prediction beside the
  observation. *Accepted when:* every failing case has a step-1 and step-2 verdict; the revised score sits beside the
  primary.
- S3 — publication (references, the trap table and the specs' final state in the task repository, Q11), credential
  teardown, archive, `retire.mjs`, `research/README.md`, promotion of this plan and its stories documents with deviations.
  *Accepted when:* `plan-lifecycle.mjs` clean; PATs revoked and the deploy key deleted; the worktree removed.
- **Cornerstone area:** none.
