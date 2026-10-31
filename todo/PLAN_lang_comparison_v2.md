# PLAN — language comparison v2: C# / .NET 10 against TypeScript / Node 26 coding agents on ten harder tasks, bare and under the family's process

> Status: **plan only, nothing implemented yet; epoch 4 open since 2026-10-31 with 0 changes, approved by the owner —
> narrowed: the process arm's GitHub capability (D42, the run-bound broker with an isolated bare repository per run), as
> the epoch 4 re-review record says.** The owner's four decisions at that approval (2026-10-31, late evening), part of the
> approved baseline: (1) Q-F — the narrowing approved, D42 as written; every repository stays public, privacy was never
> the aim; the broker exists because the process agent could otherwise read the trap history in the public bench
> repository, which the bare agent cannot reach. (2) TypeScript **6.0.x**, not 7.0.2 (D6): the newest line the pinned
> `typescript-eslint` 8.72.x supports, so the TS agents get type-aware lint at the C# analyzers' strength; 7.0.2
> superseded. (3) Q-A — **no total ceiling**: the $1000 is removed, the six rounds of both arms run as planned; a fixed
> **$50 per-session cap** stays as a runaway guard (a capped session is censored, never hidden); the pilot's cap formula
> is a reported cross-check (D7; D30 superseded). (4) D35 confirmed: `de_DE.UTF-8` in both boxes, every task, both stacks.
> Epoch 3 (opened 2026-10-31) changed twice the same day — 3.1, the D40 pre-freeze gate needs the final scoring
> environment that E3 builds, and the driver↔program network layout is designed nowhere; 3.2, the bench repository is
> public and its plan carries the §4.3 trap table that a process agent can reach through GitHub — both found by the E1
> stories writer (`feat/lang-v2-e1` `51a80ba`, §9 OP-1 and OP-12) — and was closed by a whole re-review
> (`common.plan-change-control`): consult `16e5f408`, the step-4 revision (D41–D43, §6.1, E1.S8b, E3.S7a, T18–T19, G-20,
> T-L33–T-L36, the trap aid moved to the private area with its sha256 in §4.3), the E1 stories judged `fits` (`5995560`),
> the plan round PROCEED (codex) with its one finding applied (the per-task riskiest-scenario details moved to the private
> aid's sibling `PLAN-risky-scenarios.md`), the second full reading (the document gate, four reviewers) PROCEED with five
> findings applied (the isolated bare repository per run, D3 on the memory contract, two more fragments moved, the record's
> `Narrowed: yes`), and the owner's approval above. Work may start on E0 and E1. Operator actions the E1 stories found, recorded as status (not changes): `locale-gen de_DE.UTF-8` on the
> series host (today `locale -a` lists only `C`, `C.utf8` and `POSIX`; the boxes bind the host root read-only, so the
> locale needs root — D35); the licence decision Q1 before any known-task text is pushed; the creation of the public
> repository `dew_flow_ai_language_comparison` (D2, D31); the fine-grained PAT and the deploy key for the process arm
> (E1.S7). Epoch 2 (opened 2026-10-31
> after epoch 1's re-review) changed three times the same day — 2.1, 2.2 and 2.3, the daemon and web limits and timing
> contract, the agent's scenario runner and grammar, and the `exec` constraint on `run.sh`, all found by FORMAT.md v1
> (E1.S8) — and was closed by a whole re-review (`common.plan-change-control`): the two further pilot specs (task 5
> `30af434`, task 4 `0b69a50`) read, consult `3ea9ef36`, the step-4 revision (D35–D40), the four drafts judged `fits`
> (`1591786`), the plan round PROCEED (codex) and the document gate PROCEED (four reviewers) with its five findings applied
> as the baseline (one memory contract; the pre-freeze gate on tasks 4 and 5; the runner as an early E1 deliverable; draft
> references written in E1). Epoch 3 is approved by reviewers, not narrowed; work may start on E0 and E1; the budget frame
> of §12 is the operator's decision before round 1 (Q-A). Epoch 1 was approved
> 2026-10-31 by the coai plan gate (PROCEED on `aa5e413`, its five findings applied in `8e1a39a` as the baseline), changed
> twice the same day (1.1 D34 metrics only added; 1.2 the spec-format contract from the pilot spec 01) and closed by a whole
> re-review (`common.plan-change-control`): consult `68a33be2`, the step-4 revision, the done work judged (both `fits`),
> the plan round PROCEED on `c62514c` and the document gate PROCEED on `b0fb0f1` (interim path, four reviewers), their
> findings applied as the baseline. Epoch 2 is approved by reviewers, not narrowed; the one pilot artefact, the spec
> `01-descent-to-one.md` (`dew_flow_ai_language_comparison` `60e468a`, local), is kept as a draft. Work may start on E0 and
> E1; the budget frame of §12 is the operator's decision before round 1 (Q-A).
> Scope: a new public GitHub repository `dew_flow_ai_language_comparison` (gated and frozen task specs, task workspaces,
> visible samples, run bases of the process arm); a conventions parity audit and release in `dew_flow_conventions`
> (prerequisite, other repository); a new spike `spikes/lang-bench/` in this repository that imports the v1 harness
> `spikes/minigit-bench/` and extends it for two arms, long sessions, daemons and web APIs; shared black-box acceptance
> suites with a held-out part and raw records outside every git tree under `~/rsd/lang-v2/`; the series' record under
> `research/lang-v2/`.

## Plan changes

> Epoch 1 · approved 2026-10-31 · by reviewers · state: closed

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
| pilot spec 01 `todo/tasks/01-descent-to-one.md`, `60e468a` (E1.S2's first spec, not yet gated) | `dew_flow_ai_language_comparison`, local commit, no remote | fits | kept as a draft; brought to `FORMAT.md` (E1.S8) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| D34 and T-L26, `d8da443` | `dew_flow_benchmark` main (merged in #124, `f369e66`) | fits | kept: merged and compatible with the revised plan (judged by the team lead, Opus, 2026-10-31) |

> Epoch 2 · approved 2026-10-31 · by reviewers · state: closed

- 2.1 · 2026-10-31 · agent · contract — the daemon and web limits and timing contract is absent; found by FORMAT.md v1 (`dew_flow_ai_language_comparison` `d7c73ff`, `28bf777`)
  (One cause: D14 defines limits only for the olympiad kind. §4.1's spec rules say the spec states "the RSS floor for a
  daemon", but D14 names no daemon or web floor or rule; nothing in D13, §4.3 or T-L4 gives a source for the readiness
  deadline or the SIGTERM grace period, and no rule exists for scenario-step deadlines. FORMAT v1 used 256 MB as a
  placeholder RSS floor and made readiness and grace per-task spec values (F-READY `{T}`, F-STOP `{G}`, F-LIMITS-D,
  F-DEADLINES). The step-4 revision writes the contract for both kinds.)
- 2.2 · 2026-10-31 · agent · contract — the agent has no scenario runner and no grammar; found by FORMAT.md v1
  (One cause: D19 and §5's workspace listings put neither a runner nor the grammar in the workspace, while §4.3 says the
  agent "may run" the samples — for a daemon or web task it cannot. FORMAT v1 made every daemon and web spec copy a
  reading block of about twenty lines so the agent can read a sample by hand. Also open: where CI's visible-sample runner
  lives in the task repository (§5), given that D22 makes the suite the bench's, not the repository's. This second change
  stopped the plan.)
- 2.3 · 2026-10-31 · agent · contract — the stop and leftover-process checks assume `run.sh` ends with `exec`; found by FORMAT.md v1
  (One cause: D13's SIGTERM stop and D12's fixed `run.sh` are specified without the constraint that `run.sh` must `exec`
  the program, so that the signal reaches the program and not a bash parent; E1.S3's templates and F-RUN-D need it
  stated. Recorded after the stop as the rule asks: independent causes count one each, even when found in one reading.)
  (Resumed 2026-10-31 after the re-review: the four drafts judged `fits`, the plan round PROCEED, the document gate PROCEED
  with its five findings applied as the baseline; epoch 3 opens below at 0 changes.)

### Re-review for epoch 3

Narrowed: no — against the epoch 2 baseline every revision adds (a limits and timing contract for daemons and web APIs, the runner and grammar in the workspace, the `exec` rule, systemd-scope memory certification required before launch, the declared culture, attribution moved out of the agent's tree, the pre-freeze execution gate, FORMAT v2); no scope, Definition of Done, merge condition or release got smaller, checked against §9, §16 and the epics

| Work | Where | Verdict | Action |
|---|---|---|---|
| FORMAT.md v1, `d7c73ff` (E1.S8) | `dew_flow_ai_language_comparison`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| spec 01 conforms to FORMAT v1, `28bf777` (E1.S2) | `dew_flow_ai_language_comparison`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| pilot spec 05 with four scenario samples, `30af434` (E1.S2, E1.S6) | `dew_flow_ai_language_comparison` branch `docs/spec-05`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| pilot spec 04, `0b69a50` (E1.S2) | `dew_flow_ai_language_comparison` branch `docs/spec-04`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |

> Epoch 3 · approved 2026-10-31 · by reviewers · state: closed

- 3.1 · 2026-10-31 · agent · build-order, approach — the D40 pre-freeze gate needs the final scoring environment in E1, which the plan builds in E3, and the driver↔program network layout is designed nowhere; found by the E1 stories (`feat/lang-v2-e1` `51a80ba`, §9 OP-1)
  (One cause: D40, §14 step 1, E1.S2, S2a and S6 run the gate "in the final sandbox environment — the box, D15's
  environment with the culture, the program's own systemd scope" and need a layout in which the driver reaches a boxed
  daemon or web program, while the scoring box, the scope, the culture check and the drivers are E3 deliverables (E3.S2,
  S3, S5; T-L14, T-L30), and the network layout exists in no story: v1's scoring box has no network (`--unshare-net`,
  `spikes/minigit-bench/lib/bwrap.mjs:15`) and D36 puts the driver outside the program's scope, so a driver outside the
  box cannot reach the program's port. The options the stories name — a minimal launch layer built in E1 and widened by
  E3; a narrower E1 gate plus a second execution check in E3; the freeze waiting for E3 — are one change whichever is
  chosen. Until the step-4 revision, E1 proceeds except the gate run and the freeze.)
- 3.2 · 2026-10-31 · agent · contract — the bench repository `dew_flow_benchmark` is public and its plan carries the §4.3 trap table, which a process agent can reach through GitHub; found by the E1 stories (§9 OP-12)
  (One cause: D25 makes the trap list an internal test-design aid, "kept with the held-out material, never in a
  statement", yet this plan's §4.3 trap table and §4.1's per-task trap rows sit in a public repository — verified
  2026-10-31 with `gh repo view`: visibility PUBLIC — and are in merged history (#124, #127, #128); the process arm's
  egress reaches the GitHub hosts by SNI (§6, `EGRESS_HOSTS.langProcess`), so a process agent could read the aid. The
  options for the step-4 revision — move the aid to a private place and accept that the published history keeps the old
  rows; route the process agent's GitHub access through a broker that allows only the task repository's paths; accept
  and disclose it — go to a consult first. This second change stopped the plan.)
  (Step 4, 2026-10-31, after consult `16e5f408`: the aid moved to the private area with its hash in the plan (D41) and the
  process agent's GitHub access run-bound by a broker (D42) — both, with the public history disclosed as T18; the
  execution layer moved into E1 with the shared loopback namespace (D43, §6.1). Baseline, not entries.)

### Re-review for epoch 4

Narrowed: yes — the measured treatment of the process arm got smaller: by D42 it no longer has the family's open GitHub access (it cannot read other repositories' history, the task repository's `main` or other runs; it works in an isolated bare repository per run), so the process-arm result applies to "two stacks under the same run-bound GitHub policy", not under the family's open one; what did not get smaller: the tasks, the arms, the metrics, the acceptance criteria, the Definition of Done, the merge conditions and the release; the execution layer moves earlier and is shared (more is built in E1), and the trap aid changes place, not content (D1, D34); the merged public history cannot be unpublished, and making the bench repository private or rewriting its history is the owner's call — so the owner approves epoch 4 first (plan-change-control step 8), or chooses the alternative of keeping open GitHub access with the residual exposure accepted and disclosed, or a private bench repository

| Work | Where | Verdict | Action |
|---|---|---|---|
| the E1 stories document `todo/PLAN_lang_comparison_v2_E1.md`, `51a80ba` (planning, not on main) | `dew_flow_benchmark` branch `feat/lang-v2-e1`, pushed, unmerged | fits | kept; rewritten for E1.S8b, E3.S7a, OP-2 to OP-11 as text and the D41 rule (no trap content) before its own gate (judged by the team lead, Opus, 2026-10-31) |

> Epoch 4 · approved 2026-10-31 · by owner · state: open

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
| D3 | **Tasks: 5 known + 5 original; 4 olympiad, 3 daemons, 3 web APIs; difficulty from a few minutes to about one hour of agent work.** Traps are runtime traps on both sides across the suite. The list is §4.1 (order = expected difficulty; the times are guesses, calibrated by the pilot). Daemons and web APIs are scored by a harness that starts the program, drives held-out scenarios and checks replies, cgroup-accounted memory over time (`memory.peak`; RSS only as a descriptive curve — D13, D14, D36) and a clean SIGTERM stop. Consultant review on 2026-10-31 (codex gpt-6-astra, gemini) shaped the list: CSES 1712 dropped (a one-sided JS trap), CSES 1736 dropped (sums stay below 2^53 at CSES limits, ≈ 4e15, so its JS trap does not fire); gemini's claim that .NET console apps default to Server GC was **rejected** (they default to Workstation GC) — still, the harness fixes GC settings explicitly per stack and records them | operator, 2026-10-31 |
| D4 | **Method rules from the consultants, adopted:** the agent never sees a CSES/IOI id or title (paraphrased statement only); workspace samples are small and clean and the traps live in held-out tests, so the first-submission pass rate can show a difference; time and memory limits are calibrated on reference solutions in **both** languages, so no limit rejects one runtime's baseline footprint; a **paid pilot (tasks 1, 3, 10 at least)** measures real times before the full series; the bare session wall limit rises from v1's 20 min to **about 75 min**; v1's $6 per-session cap is **re-derived from the pilot** | operator, 2026-10-31 |
| D5 | **Packages: option A — a pinned offline package cache per stack**, the same discipline as v1's cache snapshots; **the agent has no general network** (the process arm reaches GitHub, as v1's D21–D22) | operator, 2026-10-31 |
| D6 | **Stacks: both current LTS lines, latest patch, checked 2026-10-31 against the official release indexes.** C#: .NET 10, SDK **10.0.403** (released 2026-10-25; the host has 10.0.114, so it must be upgraded and pinned), runtime 10.0.14, C# 14 (the default for .NET 10; C# 15 ships with .NET 11, RC3 on 2026-10-31, not stable, so out). Web APIs use ASP.NET Core **Minimal API**; daemons the Generic Host (`Microsoft.Extensions.Hosting`); JWT `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x; tests the agent's choice from the cache (xUnit v3 offered). TypeScript: Node **26.13.0** (LTS "Lithium", released 2026-10-28; the host has 26.12.1, so it must be upgraded and pinned), TypeScript **6.0.x** with `strict` — the newest line the pinned `typescript-eslint` 8.72.x supports (its peer range is `<6.1`), so the TS agents get **type-aware lint at the same strength as the C# analyzers**; this matches the conventions plan's D7 (`dew_flow_conventions · todo/PLAN_node_rule_parity.md`); the exact patch is pinned in E1.S5 from npm. **7.0.2 was the earlier pin (v1's toolchain line), superseded by the owner on 2026-10-31 (D1)**: under 7.0.2 `typescript-eslint` cannot parse `.ts` and the lint runs without types (v1's D4 deviation), which would have handicapped TS in the process arm. The Node web stack is the team lead's choice, with the reasons recorded (§4.6): **Fastify 5.12.7** with TypeBox 0.34.x (`@fastify/type-provider-typebox`), `@fastify/jwt` 10.x, pino, `node:test`, `node:net`. Toolchains are pinned in read-only copies with checksums (as `~/rsd/minigit/toolchains/`) and recorded in the pre-registration | operator, 2026-10-31 (stacks); team lead, 2026-10-31 (the Node web stack, reasons in §4.6) |
| D7 | **Budget: no total ceiling.** The $1000 total of the morning is **removed by the owner on 2026-10-31 (late evening)**; the six rounds of both arms run as planned (D10, D27). **A per-session cap of $50 stays as a runaway guard**, a fixed owner value ("I don't think $6 is enough; let's do 50"): a session is stopped at $50 and recorded as censored (`killed: cap`, `≥ $50`), never hidden; the pilot's cap formula (§12) is superseded for the cap's value and stays as a reported cross-check that does not bind. Pilot, A/A, data, void and re-run spend are still counted on separate lines. *The $1000 ceiling, the arm split, the `budget-frame` halt over the total and the budget approval step are superseded (D1); their text stays in §12 marked so* | operator, 2026-10-31 (the ceiling, the morning); owner, 2026-10-31 (no ceiling; the $50 cap) |
| D8 | **Same agent as v1: Claude Code (Opus, effort `high`), CLI pinned.** v1 pinned CLI **2.1.310** (`inputs/stacks.json`); v2 **re-pins at its pre-registration** to the newest CLI version installed on that day, copied read-only as v1's `init` does (today that is still 2.1.310); the v1 pin is not inherited. *Superseded in part by D20: v2 runs two arms, not the bare arm alone* | operator, 2026-10-31; the re-pin shape: this plan |
| D9 | **Unit of analysis = the task**, with repeated measures: per task, stack and arm, 3 sessions (D20). The 120 sessions are not 120 independent samples (D28) | this plan, per instruction E5 |
| D10 | **Order: six rounds, arms alternating.** A round runs every task once in one arm; the arm of round 1 is drawn from the seed and the arms alternate (B P B P B P or P B P B P B), so after any even number of rounds both arms hold the same n; the task order inside a round is drawn from the seed; a task's second repeat in an arm runs only after every task has run once in that arm. The two cells of a pair (C# ∥ TS, same task, same arm) start in the same tick on masks `0-5` / `8-13`, swapped per pair as `maskFor` does; the two arms never overlap. The ceiling (D7) cuts whole round pairs (one bare + one process), never one task's repeats | this plan, per instruction E4 |
| D11 | **The bare agent's workspace is a copy of one task folder** (`csharp/<task>/` or `node/<task>/`) at a pinned commit of the task repository, fetched and hash-checked as v1's `fetchInputs` does. The repository's root files, the other stack's folder, `tasks/` and `todo/` are never in the bare box. The bare agent gets **no** conventions. **The process agent's workspace is a single-branch clone of its run base `run/<rid>/main`** in the task repository (v1 §4.2), which carries the conventions, the hooks, the frozen spec and both stack folders; its prompt names its stack's folder | this plan |
| D12 | **Fixed per-stack scripts in every workspace, frozen inputs with hashes:** `build.sh` and `run.sh` (olympiad: reads stdin, writes stdout; daemon and web API: the long-running program, `PORT` and the task's inputs from the environment). **`run.sh` ends with `exec` of the program, so the started process IS the program (one process; SIGTERM reaches it, never a bash parent)** — change 2.3, D38. The agent fills the code; the harness drives only these two scripts, identically for both stacks. A `PACKAGES.md` lists the offered package set of the stack (D5); the spec tells the agent what is frozen | this plan |
| D13 | **Scoring per task kind** (§4.3): olympiad — held-out input/expected pairs under calibrated time and memory limits, each case `pass` / `wrong answer` / `time limit` / `memory limit` / `runtime error`; daemon and web API — held-out scenarios driven by the harness, readiness within T, a SIGTERM stop that must end the process within the grace period G with no child left (D14's series defaults and overrides), the **cgroup-accounted memory bound** certified by the program's own cgroup scope (D36). **Accepted** = clean build, every held-out functional case or scenario passed, every lifecycle check passed (readiness, the SIGTERM stop within G, no process left), **and the certified memory claim** (`memory.peak` of the program's own scope within the registered limit, no `oom_kill`). **One memory contract (D36): for a data session a readable `memory.peak` is a precondition — the runner refuses to start one on a host without it, with the named halt `memory-not-certifiable` — so `accepted` always includes the certified memory claim and no CPC denominator is ever touched by certification.** The flag `memory bound not certified` exists only for **non-data runs** (the smoke, the stub fixtures, and the pilot where the operator runs it on a host that cannot certify — allowed, since pilot sessions are void by D16): such a run is reported on its functional, lifecycle and stop checks with the flag and no memory claim. One suite per task runs unchanged against both stacks (D22) | this plan |
| D14 | **Limits, by kind (change 2.1).** *Olympiad:* time limit = **3 ×** the slower of the two reference solutions' wall, floor 1 s; memory limit = **2 ×** the larger reference peak, floor 256 MB. *Daemon and web API:* the **cgroup-accounted memory bound** = **2 ×** the larger reference peak over **all** the task's load scenarios (task 5 has two), floor **256 MB** — the same floor as olympiad; §4.1's "memory floor for a daemon" is this. **Semantic bounds live in each spec** (max connections, requests, payload bytes, clock semantics, expiry tolerance — spec 05's R32, R33, R23 are the shape). **Timing has series defaults with explicit per-task overrides:** readiness **T = 10 s**, grace **G = 5 s** (provisional), scenario-step deadlines as `FORMAT.md` defines; a spec states its T and G and may override with a reason. **Validation before the freeze:** T, G, the step deadlines and the semantic bounds are run against both draft references through the runner before the spec freezes (D40); a value a reference cannot meet is changed before the freeze, never after. **Calibration after the freeze:** execution time and memory by this rule (E2.S3). The spec states the floors and the rule as guarantees; the exact values go to the runner, never into the spec (change 1.2a). Peak memory is `memory.peak` of the program's own cgroup scope (D36; cgroup accounting includes page cache and kernel memory); **only that certifies a bound**. RSS is sampled — 100 ms for olympiad, 1 s for daemon and web — as a **descriptive curve only**, the one statement of the sampling rates; never `RLIMIT_AS`. **Where a spec states a tolerance around a time boundary, the held-out checks are placed against that tolerance as the private aid says** (D41; the placement rule moved there). The numbers are computed by E2 and pre-registered; a change after the pilot is a dated amendment applied to both stacks | this plan |
| D15 | **GC and runtime settings, fixed and recorded per stack:** C# `DOTNET_gcServer=0` (Workstation, set explicitly), `DOTNET_TieredCompilation` default, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`; Node: no `--max-old-space-size` flag from the harness (the memory limit is the cgroup's); `NODE_OPTIONS` empty. **Culture (D35): `LANG=de_DE.UTF-8` and `LC_ALL=de_DE.UTF-8` in both boxes, both stacks, every task; no `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` and no other invariant switch.** Every value is in `agentEnv`'s allow-list, the same in the agent box and the scoring box, and in every session record. **This row is the source of the base environment:** both stacks get the same base plus these documented per-stack variables; `FORMAT.md`'s "the same for both stacks" sentence follows this row (FORMAT v2, E1.S9) | this plan |
| D16 | **The pilot's sessions are void (never data)**, as v1's A/A and pilot were; they fix the stall timeouts, the time guesses of §4.1 and the limits of D14, and they measure real session costs (the formula's cap as a cross-check beside the owner's fixed $50 cap, D7; no split, D30 superseded). Bare pilot: tasks 1, 3, **4**, **5** and 10 as pairs (C# ∥ TS) plus one A/A pair (two identical bare C# sessions of task 3) — task 4 (the Rule API, the smallest web task) for the HTTP driver, task 5 (the RESP server) for the TCP/RESP driver, task 10 (the DAG scheduler) for the SIGTERM, RSS and timer checks, so G-16 checks every driver on a real tree before round 1; the pilot option is taken over a hand-check on a reference tree. **Pilot cost, re-derived at the §12 guesses:** 10 bare pilot sessions + 2 A/A at $5–20 → $60–240; 4 process pilot sessions at $30–70 → $120–280; **≈ $180–520 in all**, reported as pilot spend (no data share is computed since the owner removed the total ceiling, D7). Process pilot: tasks 1 and 3 as pairs (v1's P1 conditions, §11). The go/no-go table is v1's G-1 … G-14 plus §11's G-15 … G-19 | this plan |
| D17 | **Egress:** bare sessions — no registry, no host; the only road out is the API broker's door. Process sessions — **no direct `github.com` egress either (D42)**: `EGRESS_HOSTS.langProcess` is empty and the GitHub operations go through the local GitHub broker's door, bound to the current run; package installs in both arms resolve from the seeded offline caches (D5). A refused CONNECT is flagged `wanted network`, not voided | this plan |
| D18 | **Contamination needles for the known tasks** (id, title, site names; `CSES`, `IOI`, `Werewolf`, `RealWorld`, `Conduit`, the public repository's name and URL; **for task 5: the product name `Redis`, the protocol name `RESP` with its versions, `redis.io`, `redis-cli`, and the common client libraries `StackExchange.Redis`, `ioredis`, `node-redis`, `redis` as a package name**) join the scan's needle list, in tool inputs and outputs, and are run over every generated run base and its reachable history (D39). A hit is **read by hand**: the agent recalling a known problem from memory is a threat of the design (T1), not a void; a fetch or a path reaching outside the box is a void, as v1 | this plan |
| D19 | **Known-task statements name no source.** The link and the credit live in the task repository's README (D2), in an attribution section that is **outside every agent-visible tree** (D39); `NOTES.md` is dropped. The bare workspace holds the statement, the samples, `PACKAGES.md`, `build.sh`, `run.sh`, the stack's pin files, **and the read-only runner and grammar (`runner/run_samples.py`, `runner/GRAMMAR.md` — D37, change 2.2)**, nothing else | operator, 2026-10-31 (D2); the file list: this plan |
| D35 | **One declared non-invariant culture for the whole series: `de_DE.UTF-8`** (`LANG` and `LC_ALL`), in **both** the agent box and the scoring box, for both stacks and every task, and in CI's visible-sample runner — **confirmed by the owner, 2026-10-31** (en_US was considered and `de_DE` kept). Recorded in the pre-registration and in D15's allow-list; the locale is generated in the box image (`locale -a` lists it). No `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` and no other invariant switch: a missing `LANG` is not .NET's globalization-invariant mode, and neither is wanted. **Fairness is a declared environment plus the protocol's stated numeric syntax** (spec 04 states its grammar, R15 and 3.6), not equal trap counts. E3's acceptance runs a check inside the actual boxes (T-L32). Closes Q8 | this plan (epoch 3 re-review; consult `3ea9ef36`) |
| D36 | **Memory certification by the program's own systemd user scope, required before launch.** The scorer runs each program in its own `systemd-run --user --scope` with `MemoryMax=<limit>` and `MemorySwapMax=0` (the bound is on memory, never memory plus swap), launched **outside** `bwrap`; the driver and the build run outside that scope. It verifies that the program and every forked child belong to the scope (`cgroup.procs`), and it keeps `memory.peak`, `memory.events` (`oom_kill`, `max`) and the lifecycle evidence before the scope is removed. The host was probed on 2026-10-31: cgroup2 with the memory controller; `systemd-run --user --scope -p MemoryMax=64M` gives a readable `memory.peak` (2,396,160 B) and `memory.max` — **this host can certify**. The pre-registration records the certifying host; **a host that cannot certify runs no data session: the runner's certification probe runs at start and refuses with the named halt `memory-not-certifiable` (exit 3, resumable on a certifying host)** — so for every data session `accepted` includes the certified memory claim (D13). "RSS bound" is replaced by the **cgroup-accounted memory bound** everywhere, calibration included. The flag `memory bound not certified` is for non-data runs only (smoke, fixtures, a pilot the operator allows on a host that cannot certify); T-L29 is the refusal test; T-L30 adds the fixtures | this plan (epoch 3 re-review; the probe by the team lead) |
| D37 | **The agent has the scenario runner and the grammar.** The visible-sample runner — the bench's own Python standard-library program, **built early in E1 (E1.S8a) and pinned there**, implementing FORMAT v2's grammar and transcript comparison (D22) — and `FORMAT.md`'s grammar sections are in every workspace as read-only frozen files, `runner/run_samples.py` and `runner/GRAMMAR.md`; the task repository carries the pinned copy with its hash in `inputs/specs.json`; CI's visible-sample runner is that same file; FORMAT v2's examples and every pre-freeze check (D40) run through it; E2.S2 reuses it for the held-out suites and owns no runner; the agent may run the samples with it (`python3 -I runner/run_samples.py samples/`). The ~20-line reading block FORMAT v1 put in every daemon and web spec goes once the runner is present (FORMAT v2, E1.S9). Resolves change 2.2 | this plan (epoch 3 re-review) |
| D38 | **`run.sh` ends with `exec` of the program.** One process: the pid the harness starts is the program's, SIGTERM reaches it, and the stop and leftover checks are about the program. E1.S3's templates carry it; T-L31 checks it. Resolves change 2.3 | this plan (epoch 3 re-review) |
| D39 | **Attribution lives outside every agent-visible tree.** The cross-repository boundary (`common.planning-docs` § *Across repositories*) is named in the task repository's README attribution section — cut from every run base — and in this plan, never in a `todo/PLAN_*` of the task repository, which would enter every run base (OP-11). The public README keeps its attribution section (D2), and **the run base is an orphan commit of a generated tree**: the frozen tag's tree minus the README's attribution section and minus any credit file, so the process agent's single-branch clone holds exactly one commit of history before its own work. The check: the needle scan (D18) runs over the exact generated tree **and** `git rev-list` of the base (one commit), and the cut is refused on any hit. `NOTES.md` is dropped: credit is the README's section, design reasons live in the bench's internal test-design notes. `FORMAT.md`'s daemon example must not name the real protocol (FORMAT v2) | this plan (epoch 3 re-review) |
| D41 | **No new trap content anywhere public before the series ends** — plans, stories documents, review bodies, PR descriptions. The test-design aid lives in the private area `~/rsd/lang-v2/test-design/`; this plan's §4.3 trap table and the per-task trap columns are **replaced by a pointer** to `~/rsd/lang-v2/test-design/PLAN-trap-table.md` (mode 0600), moved verbatim on 2026-10-31, sha256 `4acd751a8c0514f8742ad3ecf60eed417a12c53d2eb07e785355b62f2b0a228c`; **and its sibling `PLAN-risky-scenarios.md` (mode 0600), sha256 `b8a6cb1bf845ac8f0c9a7a2d807928e3d81fe545c18978fd3edb7a5548ba731a`**, which holds, word for word, the per-task riskiest held-out-shaped scenarios that D40, E1.S2, D14's expiry placement, §4.3's web row and G-16 named until the epoch 4 round, and — added at the second full reading — §4.3's task-1 trap-balance sentence and D40's per-task count and kind breakdown; the content is kept, only its place changes (D1, D34). The merged history (#124, #127, #128) keeps the earlier rows — a disclosed residual risk (T18), named in the record. Resolves change 3.2's first half | this plan (epoch 4 re-review; consult `16e5f408`) |
| D42 | **The process agent gets NO direct `github.com` egress.** Its GitHub operations go through a **local GitHub broker** (the shape of the coai bridge) that **serves an isolated bare repository per run**: for each run the broker creates a local bare repository holding **only the orphan run-base commit (D39) and the run's own refs**; the agent's `git clone`, `fetch` and `push` go to that bare repository through the broker's smart-HTTP endpoint and never to GitHub; the broker **mirrors** the run's branch to GitHub and, on the agent's behalf through the GitHub API with the PAT, creates and updates the run's PR and reads its checks and comments — the allowed operations only. Ref filtering alone cannot hide objects that exist in the task repository's object database (a fetch by SHA, an unshallow fetch), so **no object outside the run base can reach the agent**: the objects are not there. **Allowed:** clone and fetch of the run base and the run's own branch; push to its own branch; create and update its own PR; read its own PR's checks and comments. **Denied (the retrieval probes):** the bench repository's history; the task repository's `main` attribution; another run's branch or PR; a fetch by the SHA of a task-repository `main` commit; an unshallow fetch (which gives only the run base); any redirect to a host or path outside the broker. The normal clone, push, PR and checks workflows must succeed while every denied probe fails (T-L35, pilot-tested in G-20). **This narrows the measured treatment:** the process arm no longer has the family's open GitHub access (it cannot read other repositories' history, the task repository's `main` or other runs) — a contract change recorded here, disclosed in T19 and in the record as *"two stacks under the same run-bound GitHub policy"*, and **a narrowing of the plan (the epoch 4 re-review record)**; tasks, arms, metrics and acceptance criteria stay. Resolves change 3.2's second half | this plan (epoch 4 re-review; consult `16e5f408`, verified on this host by the team lead; the bare-repository shape from the second full reading) |
| D43 | **One reusable execution layer, built in E1 (E1.S8b)** and consumed unchanged by E2's validation and calibration and by E3's scoring: launch; D15's environment with the culture; the program-only systemd scope (D36); the network layout below; deadlines; signals; cleanup. E3 keeps orchestration, scoring aggregation and records. **Network layout:** a private, loopback-only network namespace shared by the driver and the program, with separate filesystem and PID isolation (bwrap, as v1) and separate cgroups (the driver outside the program's scope). Probed on this host 2026-10-31: unprivileged `unshare -rn` gave `net:[4026532221]` against the host's `4026531833`; in it `ip link set lo up` worked and a Python server and a driver exchanged `ping→PING` over `127.0.0.1` with identical `/proc/<pid>/ns/net`; `--mount-proc` was refused, so PID and filesystem isolation come from bwrap. **D40 is not weakened:** the pre-freeze gate runs on this layer in E1. The producer/consumer table of §6.1 names the shared artefact, its probes and what invalidates earlier evidence. Resolves change 3.1 | this plan (epoch 4 re-review; consult `16e5f408`, verified on this host by the team lead) |
| D40 | **The freeze order (the root cause of the two stops):** before a spec freezes, **both draft references execute the visible samples through the distributed runner in the final sandbox environment**; draft references are allowed before that gate; calibration with held-out cases stays after the freeze. So E1.S6's sample files exist before each spec's conformance gate. **The draft references are written in E1 (E1.S2a) by the team lead with Sonnet coders per the role table, one per stack per task; they live in the bench's private area `~/rsd/lang-v2/refs/` — outside the task repository's git, beside the held-out material — and E2.S1 finalises, validates and calibrates them.** **The D40 gate also runs each task's riskiest held-out-shaped scenarios** through the runner, kept out of git and **listed in the private aid** (`~/rsd/lang-v2/test-design/PLAN-risky-scenarios.md`, D41): some tasks have private pre-freeze scenarios, listed in the private aid with their counts; the public text names none of them | this plan (epoch 3 re-review; consult `3ea9ef36`; the scenario list moved to the private aid at the epoch 4 round) |
| D20 | **Two arms, as v1's parent plan designed them.** **Bare:** 3 runs per task per stack on pure Opus with the task prompt only. **Process:** 3 runs per task per stack under our rules — the harness, the pinned conventions, the coai gates (plan gate, code gate, consultant), an Opus team lead and Sonnet coders. So each task has **6 repeats per stack**, 120 data sessions. v1's parent plan is reused for how the process arm is run and measured (§4.2, §6, §8); "bare first, process out of scope" is withdrawn everywhere | operator, 2026-10-31 (instruction A) |
| D21 | **Task specs are written by the team lead**, not by the agents under test (two kinds of "spec", OP-10: the team lead writes the *task* specs; a cornerstone story's *story* spec — the brief a Fable writes per the role table — is a different document). Each of the 10 specs goes through the **coai gates by all the rules, with a consultant**; the gated specs are stored in the new repository at **`todo/tasks/NN-slug.md`**; after the gate they are **FROZEN** (a tag `spec-frozen-<yyyymmdd>` and the hashes in this repository's pre-registration; the harness refuses a session whose spec blob differs). Then, per language and in isolation, the **process-arm agent makes its own plan** from the frozen spec; that plan goes through the gates in its run; **the task spec is never changed**. One story (E1.S2) covers the ten specs with per-task acceptance criteria | operator, 2026-10-31 (instruction B) |
| D22 | **Cross-language tests: one shared black-box acceptance suite per task** that runs unchanged against both implementations — stdin/stdout for olympiad tasks, HTTP for web APIs, TCP/RESP and signals for daemons. Written once, in one neutral harness language, **owned by the bench**, not by either stack. A **visible part** (samples in the workspace) and a **held-out part** (outside git). The harness language is **Python 3, standard library only, run with `-I`** — v1's held-out runner — because it is third to both stacks; the argument is §4.3 | operator, 2026-10-31 (instruction C); the language choice: this plan |
| D23 | **The conventions are pinned in the new repository, as in the other `dew_flow` repos, at a release that passes a conventions parity audit:** every C#/.NET rule in `dew_flow_conventions` has an equivalent Node/TypeScript rule adapted to Node, and the other way round. The audit **runs now, in parallel** (E0.S1); its result is a prerequisite story: missing counterpart rules are written and released in `dew_flow_conventions` **before the v2 pre-registration** (E0.S2), and the new repository pins that release (E0.S3). Threat T14 names the bias the audit controls | operator, 2026-10-31 (instruction D) |
| D24 | **Primary metric: cost per correct solution.** For task t, stack s and arm a: `CPC(t, s, a) = Σ cost of all data attempts of (t, s, a) / number of accepted solutions among them`. Beside it: success rate (accepted / attempts), tokens per model and kind, wall, `model_api`, `active − model_api`. A cheap unfinished run never looks like a winner: it adds to the numerator and nothing to the denominator. A (t, s, a) with **0 accepted** shows **"no correct solution"** plus its attempts and its total spend (labelled spend, not CPC) and is **not dropped**; its C#/TS ratio is **undefined** and reported as an outcome, never as a bound (§9, consult `68a33be2`). A second line counts void attempts in the numerator too (§9) | operator, 2026-10-31 (instruction E1); the void line: this plan |
| D25 | **The same functional requirements and the same checks for both stacks.** Statements describe the required behaviour neutrally. **The trap list is an INTERNAL test-design aid**, kept with the held-out material in the private area (D41), never in a statement **and, from 2026-10-31, never anywhere public before the series ends — not in this plan, a stories document, a review body or a PR description**; **every held-out test checks behaviour the statement requires**. **Review step:** a reviewer — the coai document gate plus a consultant — checks each statement and its tests for a bias toward or against one language's specifics, before the freeze (D21) and before the suite is sealed. The trap table is kept, on the test-design side (§4.3) | operator, 2026-10-31 (instruction E2) |
| D26 | **Freeze before any paid data run:** tasks (specs), tests (suites, hashes, counts), budgets (the $50 per-session cap, D7; no ceiling or split since the owner's decision), the analysis (§9), the exclusion grounds (voids, stops) and the re-run grounds — all in the pre-registration, tagged. **Every attempt is kept**, including failed, voided and re-run ones, in the records and the report | operator, 2026-10-31 (instruction E3) |
| D27 | **Spread the 6 repeats per task over time:** a seeded random task order per round; the paired concurrent C# ∥ TS start of v1 stays; the CPU masks alternate per pair as v1 did; **bare and process rounds interleave** (D10), so one good or bad API day does not decide the result; the start and end time of every session is recorded | operator, 2026-10-31 (instruction E4) |
| D28 | **Analysis is per task:** 10 tasks with repeated measures, not 120 independent samples. Each task is shown; the **overall result is the median of the per-task C#/TS ratios** (primary), the **geometric mean of the per-task ratios** beside it, and the **count of tasks where C# is higher** — per arm and per measure; pre-registered. No expensive task may hide the others: no pooled sum across tasks is a headline | operator, 2026-10-31 (instruction E5); the choice of the median as primary: this plan |
| D29 | **Known and original tasks are shown separately**, and the report discloses that the five public tasks may be familiar to the model | operator, 2026-10-31 (instruction E6) |
| D30 | *Superseded by the owner on 2026-10-31 (D1, D7):* the $1000 total is removed, so no split between arms is derived and Q-A is closed. Was: "The $1000 is not changed by this plan. With two arms (120 data sessions) and dearer process sessions, the split between arms is re-derived from the pilot (§12); whether $1000 covers both arms or the process arm needs its own limit is open (Q-A)." What stays: the pilot still measures real costs and reports the formula's cap as a cross-check beside the owner's $50 | operator, 2026-10-31 (instruction F); superseded by the owner, 2026-10-31 |
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
| 4 | O | *Rule API*: HTTP; rules call members by name over a supplied registry, with string arguments; a rule token and `GET /rules/<token>` (the spec's design, `0b69a50`) | web API | 20–30 min (flagged likely low by the pilot spec; the pilot calibrates) |
| 5 | K | *Key-value server with expiry* (a paraphrased subset of a well-known in-memory store): a framed TCP protocol, keys with expiry, counters, many clients; the command list is the spec's (`30af434`: nine commands), not this table's | daemon | 25–35 min |
| 6 | K | *RealWorld "Conduit" API subset*: users, JWT, articles, pagination | web API | 30–45 min |
| 7 | O | *Stream-processor daemon*: an event stream, a sliding window per key, a watermark that expires keys that never return, an RSS limit, graceful SIGTERM; the spec fixes event-time semantics, ordering, bounds, output cadence and EOF | daemon | 30–45 min |
| 8 | O | *Ledger API with idempotency keys*: money, concurrent retries of the same key | web API | 35–50 min |
| 9 | K | *Werewolf* (IOI 2018): the spec defines an exact stdin/stdout grammar, identical for both stacks, and the agent implements the parser; no adapter or helper code is supplied (D33) | olympiad | 45–60 min |
| 10 | O | *DAG job scheduler daemon*: retries with capped backoff, per-job timeouts, cancellation, one process-wide concurrency limit k over overlapping submitted DAGs, cycle detection, failed-dependency propagation, a deterministic final report; a supplied virtual clock and job fixture for deterministic grading plus a real-time smoke | daemon | 45–60 min |

Sum of the upper guesses: 370 min per stack per bare round; a process round is longer (v1 predicted 2–5 h per process
cell against minutes for bare); the pilot replaces both guesses.

**Spec rules (D4, D19, D21, D25).** One spec per task, `todo/tasks/NN-slug.md` in the task repository, written by the team
lead, stack-neutral: the required behaviour, the input and output contract, **the limits as guarantees** (change 1.2a: the
spec states D14's floors — 1 s of wall time and 256 MB per run for olympiad; the **256 MB cgroup-accounted memory floor**
for a daemon or web API — and the registration rule, at least 3 × the slower reference's wall and 2 × the larger
reference's peak over all load scenarios; the exact values are registered at calibration, E2.S3, and given to the runner,
never to the author, so a spec never needs an amendment to carry them), **the timing contract** (readiness T and grace G
— the series defaults 10 s and 5 s or a stated override — and the step deadlines of `FORMAT.md`; change 2.1), **the
semantic bounds** (connections, requests, payload bytes, clock semantics, expiry tolerance), **the protocol matters**
(HTTP statuses, content types and persistent connections for a web task; frame grammar and numeric syntax for a daemon —
these are the spec's, not the plan's), the start contract (`build.sh`, `run.sh` with `exec`, `PORT`), the frozen list,
"hidden tests exist", the visible samples named. A known task is paraphrased: no id, no title, no site, **and no mention
that the task is known**; the constraints may keep the original numbers. It never names a trap or a language specific. The
workspace's `STATEMENT.md` is the spec's text plus the stack sentence (as v1's `{STACK_SENTENCE}`), generated,
byte-identical across the two stacks except that sentence and, for a web task, the framework sentence (§4.5).

**Spec format (change 1.2c, adopted from the pilot spec 01, `dew_flow_ai_language_comparison` `60e468a`), binding for
all ten, and held in one versioned document — `todo/tasks/FORMAT.md` in the task repository (E1.S8), a prerequisite of
every spec (E1.S2): the specs conform to it; its sha256 is frozen with the specs in `inputs/specs.json`; a format change
after the freeze is an amendment of `FORMAT.md`, dated and recorded like a spec amendment, and voids the limits of every
task whose suite it touches (G-17). `FORMAT.md` carries one worked example per kind — the olympiad example from the pilot
spec 01; a daemon scenario over a framed TCP protocol that names no real product (D39: a split frame, pipelined requests, a slow reader — FORMAT's own illustration, public by construction); a web scenario (HTTP concurrency,
two clients racing on one idempotency key) — and defines the scenario grammar of `<id>.in` and the comparison semantics of
`<id>.out` (§4.3). The conventions:** a header table with the rows kind (what the program is: reads stdin once / a long-running server …), gate
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
| GitHub | none; `gh` hidden | **no direct egress (D42)**: `git` and `gh` in the box reach a local GitHub broker's door that serves an isolated bare repository per run (the orphan run base and the run's refs only), mirrors the run's branch to GitHub and does the own-PR operations on the agent's behalf with the fine-grained PAT (Contents/PR RW, Actions/Checks/Statuses R; no Workflows, v1 §4.2); no object outside the run base exists to be fetched; redirects outside the broker refused |
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
expected-output file per case, `samples/<id>.in` and `samples/<id>.out`**; for an olympiad task `.in` is the program's
stdin and `.out` its expected stdout, bytes exact. For a daemon or web task the same two files hold a **scenario** in the
grammar `FORMAT.md` defines (E1.S8): `.in` is the scenario script — steps in order; named connections (TCP or HTTP clients)
and which step runs on which; synchronisation and barriers (a step that waits for others, a start-together group for a
race); timing (a pause, a deadline per step, the readiness wait); **dynamic-value capture and reuse** (an id, a token or a
timestamp read from a reply, bound to a name, used in later steps); the SIGTERM step and the grace period. `.out` is the
expected transcript, with the **comparison semantics** named per step or field: byte-exact by default; a normalised field
(a captured id, a timestamp within a window, a numeric field with a tolerance the spec names); an **order-insensitive set**
where the spec says order is not defined (a listing endpoint, the replies of concurrent clients). **Held-out part** (outside
git, sealed as v1): the cases and scenarios that decide `accepted`, in the same `<id>.in` / `<id>.out` shape plus a
**`cases.txt` manifest** — one line per case naming the requirement ids (R-ids) it asserts and its kind of comparison. The
Python runner of E2.S2 **reads this format**; it is no longer E2.S2's to decide. Both parts use the same case format and
**the same runner file**: the bench's `run_samples.py` at a pinned version is also in every workspace as `runner/` (D37),
so the agent runs the visible samples the way the harness runs the held-out ones.

All scoring happens on a pristine copy of the result, in a scoring box with no network, offline from the session's own
caches, with temp HOME, as v1. `build.sh` first (bounded, 600 s; a failure fails every case). Then by kind:

| Kind | The harness does | A case passes when | Also recorded |
|---|---|---|---|
| Olympiad | runs `run.sh` once per held-out case with the case's stdin, in its own systemd user scope (D36) under the registered time limit and the registered memory limit (`MemoryMax`) | stdout equals the expected bytes **byte for byte** (the series default, change 1.2c; a normalization applies only where a requirement of the spec names it), exit 0, within the time limit, no `oom_kill` and `memory.peak` within the limit | per case: wall, CPU, certified peak memory, `memory.events`, verdict class; RSS every 100 ms as a descriptive curve |
| Daemon | starts `run.sh` (which `exec`s the program, D38) in its own scope with the task's environment; waits for readiness within T; drives scenarios over TCP or stdin with the step deadlines of the scenario; sends SIGTERM; waits G | every scenario reply equals the expected one (byte for byte unless a requirement names another comparison); the process exits 0 within G; no process of the scope is left; `memory.peak` within the cgroup-accounted bound and no `oom_kill` | certified peak memory and `memory.events`, RSS every 1 s descriptive, exit code and time, stdout/stderr tails |
| Web API | as daemon, over HTTP; scenario steps are requests with expected status, the headers the spec names and body; concurrency scenarios send requests from k clients at once | as daemon; concurrent scenarios check the invariant the spec states as a requirement (the per-task invariants are the specs' own text; which scenarios target them is the private aid's, D41) | as daemon, plus the request log |

HTTP statuses, content types, persistent connections, frame grammars and numeric syntax are **spec matters**: the plan
fixes none of them, every spec states its own (spec 04's R12–R15, spec 05's R8–R10), and the runner compares what the
spec names.

**The memory bound is the cgroup-accounted bound, certified by the program's own scope (D36).** The scorer starts each
program in a `systemd-run --user --scope` with `MemoryMax` at the registered limit and `MemorySwapMax=0`, outside `bwrap`,
the driver and the build outside that scope; it verifies that the program and its forked children are in the scope, reads
`memory.peak` and `memory.events` before the scope is removed, and keeps them with the lifecycle evidence. **Certification
is required before launch:** the pre-registration records the certifying host (this host was probed on 2026-10-31 and
can certify); **for a data session a readable `memory.peak` is a precondition** — the runner's probe runs at start and a
host without it gets the named halt `memory-not-certifiable` (exit 3, resumable on a certifying host), so no data
session is ever scored without its memory claim and `accepted` always includes it (D13); CPC denominators are never
touched by certification. **Non-data runs only** (the smoke, the stub fixtures, a pilot the operator allows on a host that
cannot certify — pilot sessions are void by D16) may run without `memory.peak`: the harness then samples RSS (100 ms
olympiad, 1 s daemon and web; descriptive only, never a verdict), records **no memory pass**, and the run carries the flag
`memory bound not certified` and is reported on its functional, lifecycle and stop checks alone. The pre-registration
names the certifying host (host, kernel, cgroup version, the scope probe). E3's acceptance checks the refusal and the flag
(T-L29).

**Measures per session:** `accepted` (D13); `percent` = cases passed / N (a blocked case counts as not passed); `defects` =
distinct **trap classes** failed, from the trap coverage table (one trap failing in three cases counts once); verdict class
counts. **First-submission** means: what the session left when it ended, scored once; no retry.

**The test-design side (internal, D25, D41).** The operator's trap design aid for the suites — the per-task, per-stack trap
rows that stood here through `b5582d7` — **is no longer in this public plan.** On 2026-10-31 it was moved **verbatim**
into the private area, `~/rsd/lang-v2/test-design/PLAN-trap-table.md` (mode 0600, outside every git tree), sha256
`4acd751a8c0514f8742ad3ecf60eed417a12c53d2eb07e785355b62f2b0a228c`; the content is kept and only its place changed (D1,
D34). Nothing of it is in any spec; every held-out case that catches a trap asserts a behaviour the spec requires. The
authoritative **trap coverage table** (task × stack × trap → the cases that catch it) is sealed with the suites and
published after the series, together with the moved aid. **The per-task riskiest held-out-shaped scenarios** that the
D40 gate runs were moved the same way at the epoch 4 round, into the sibling `PLAN-risky-scenarios.md` (mode 0600,
sha256 `b8a6cb1bf845ac8f0c9a7a2d807928e3d81fe545c18978fd3edb7a5548ba731a` after the second full reading added the task-1
balance sentence and D40's counts); the public text says only that the gate runs them. **The merged public history (#124, #127, #128) keeps the earlier
rows: a disclosed residual (T18).** Per-task trap balance is reported after the series (T2); the one per-task balance fact
that stood here moved to the private aid at the second full reading.

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
STATEMENT.md, samples/, runner/, build.sh, run.sh, PACKAGES.md and the toolchain pin files of the stack are frozen inputs:
do not change them. The program is built by `bash build.sh` and started by `bash run.sh` exactly as STATEMENT.md says;
`python3 -I runner/run_samples.py samples/` runs the visible samples the way the hidden tests will. Hidden tests will run
it under registered time and memory limits, never below the floors STATEMENT.md guarantees. Only the packages listed in
PACKAGES.md are available;
there is no network. Work autonomously; nobody answers questions. End with a summary of what you built and the checks
you ran.
```

**Process** (v1 §4.3's process prompt, with the task slot):

```
Implement the program specified in todo/tasks/{TASK}.md of this repository, in {STACK}, inside {STACK_FOLDER}/. {STACK_SENTENCE}
The spec, the samples, build.sh, run.sh and PACKAGES.md in that folder are frozen inputs: do not change them. The work is
done when, from a clean checkout of the base branch, `bash build.sh` succeeds and the visible samples pass under the
registered time and memory limits, which the test runner enforces and which are never below the floors the spec
guarantees. Hidden tests will run the program the same way.
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
TypeScript — *"Use TypeScript 6.0 with `strict`, run by Node 26 with native type stripping (no build step), with `tsc
--noEmit` as the type check."* (6.0.x by the owner's decision of 2026-10-31, D6; the exact patch from E1.S5 is written into
the registered sentence) **Web API tasks add a second varying sentence, the framework sentence**, one per stack with the
same shape: *"Use {FRAMEWORK} ({PACKAGE} {VERSION}, listed in PACKAGES.md) for the HTTP server."* — C#: `ASP.NET Core
Minimal API` (the shared framework of SDK 10.0.403); TypeScript: `Fastify` (`fastify` 5.12.7). So a web workspace's
`STATEMENT.md` differs across stacks in exactly two sentences, both registered in `prompts-lang.json` (E4.S2); every other
task differs in one.

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
version Fastify 5.12.7 resolves), `@types/node` for 26.x, `typescript@6.0.x` (the exact patch pinned in E1.S5; 7.0.2 superseded, D6), `typescript-eslint@8.72.x` with type-aware rules, and the lint packages the process arm's
template needs (v1 D4's shape); every Node workspace's `.npmrc` sets `offline=true`. **The exact package set per stack, with
pinned versions and the hashes in the cache manifest, is E1.S4's deliverable** and is pre-registered. `PACKAGES.md` in each
workspace lists it (D12).

**Toolchains**, pinned read-only with checksums under `~/rsd/lang-v2/toolchains/` and checked inside the box by the
preflight: .NET SDK 10.0.403 installed from the official install script into that directory (not apt, so an unattended
upgrade cannot move it, as happened to v1 on 2026-10-30), Node 26.13.0 from the official tarball, TypeScript 6.0.x (D6), the
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
  todo/README.md       the index; todo/tasks/FORMAT.md — the versioned spec and sample format (E1.S8); todo/tasks/NN-slug.md — the ten gated, FROZEN specs (D21); the process agent's own plans land in todo/ of its run branch
  research/            stubs (architecture, module_tests) as v1's templates; the references and the trap table arrive here after the series
  tasks/<nn>-<slug>/   samples/ (the visible part of the suite, D22: <id>.in / <id>.out); no NOTES.md (D39: credit is the README's attribution section, design reasons are the bench's internal notes)
  runner/              run_samples.py (the bench's visible-sample runner, Python stdlib, pinned version, hash in the bench's inputs/specs.json — D37) and GRAMMAR.md (FORMAT.md's grammar sections); CI's visible-sample runner is this file
  csharp/<nn>-<slug>/  the C# workspace: STATEMENT.md (generated from the spec + the stack sentence, + the framework sentence for a web task), samples/ (a copy), runner/ (a copy, read-only), PACKAGES.md, build.sh, run.sh (ends with exec, D38), global.json, nuget.config, Directory.Build.props, an empty .csproj skeleton
  node/<nn>-<slug>/    the Node workspace: STATEMENT.md, samples/, runner/, PACKAGES.md, build.sh, run.sh (ends with exec), package.json, package-lock.json, tsconfig.json, .npmrc, eslint.config.mjs
  run/<rid>/main       process-arm run bases: an ORPHAN commit of the generated tree (the frozen tag's tree minus the README's attribution section, no credit file — D39), cut by the deploy key (v1 §6); rulesets `run-main`, `template-frozen`, tag rulesets
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
| The CONNECT proxy and SNI check | **as is**; `EGRESS_HOSTS.langBare = []` **and `EGRESS_HOSTS.langProcess = []`** (D17, D42): no session reaches any host; the API broker's door and, in the process arm, the GitHub broker's door are the only roads out |
| The credential broker's shape (a host-side door that holds the secret, forwards an allowed request, refuses the rest, meters it) | **reused for the GitHub broker** (D42, E3.S7a): the same door pattern with a run-bound allow-list over refs, PRs, objects and redirects; the PAT never enters the box |
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

Order: E1.S7 (the repository side), then E3.S6–S9. Launch argv as v1

### 6.1 The shared execution layer — producers and consumers (D43)

One implementation, `spikes/lang-bench/lib/exec-layer/` (E1.S8b), runs every program the series judges: launch of
`run.sh` in a bwrap box with D15's environment and the culture, the program-only systemd scope with `MemoryMax` and
`MemorySwapMax=0`, the private loopback-only network namespace shared with the driver, the readiness deadline T, the step
deadlines, SIGTERM and the grace G, cleanup of the scope and the namespace. Its identity is the sha256 over its tree
(part of the harness identity); every record names it.

| Consumer | When | Uses | Acceptance probes it relies on | What invalidates earlier evidence |
|---|---|---|---|---|
| **E1's freeze** (the D40 gate) | before each spec freezes | the layer with the pinned runner (S8a): both draft references, every visible sample, the riskiest-contract scenarios | T-L33 (netns shared, cgroups separate), T-L34 (sentinel unreadable; scope kill removes descendants, driver survives; identical for both stacks), T-L31 (`exec`), T-L32 (culture) | a change to the layer or the runner **re-runs the D40 gate for every spec frozen on the old one** before the pre-registration; a spec amendment re-runs it for that task |
| **E2's validation and calibration** (S1, S3) | after the freeze | the same layer, the same runner, the held-out suites | the same probes, plus T-L7/T-L25 (the registration order) | a change to the layer **voids every registered limit** (G-17's rule widened: the calibration ran on another environment) and re-runs calibration; a spec or `FORMAT.md` amendment voids that task's |
| **E3's scoring** (S4) | every session | the same layer, orchestrated by the runner's phases; E3 adds only aggregation and records | the same probes, run once on the series host before the A/A (G-15/G-16 read them by hand) | a change to the layer after the pre-registration is an instrument amendment: dated, the A/A re-run (v1's rule); after round 1 it stops the series until the A/A is GO again |

Disjoint: the layer owns how a program runs; the runner owns what is sent and compared; E3 owns which sessions run, in
what order, and what is recorded. A defect found in the layer by any consumer is fixed in the one place and every
consumer's evidence is re-dated by the table's rule. (`claude -p --model opus … --max-budget-usd <cap> --tools
Task,Bash,Read,Edit,Write,Glob,Grep,TodoWrite[,ToolSearch]`), MCP only in the process arm, effort `high` pinned in the
session settings file, fresh `CLAUDE_CONFIG_DIR` per session, `gh` in the box only in the process arm.

## 7. Isolation and contamination

Prevented, as v1: the operator's home and `/mnt` hidden; the bare box has no network but the broker door; the held-out
suite sealed, its key never in any box's environment; the real token never in the box; coai's vendor sign-ins outside the
agent box in coai's own box. Audited, as v1: every tool call scanned for paths outside the box, walkers, links, token
shapes, canaries; in the process arm, pushes and PRs outside the session's `run/<rid>/` namespace, other `run/*` reads
(the repository is public, but the process agent has no direct GitHub egress: the GitHub broker **prevents** reads of other runs' branches and PRs, the task repository's `main` attribution and the bench's history, and the audit still records every attempt — D42; v1 §7.3's "audited, not prevented" no longer applies), the pin and the frozen spec
unchanged. New in v2:

- **Known-task needles (D18)** in the scan; a hit is read by hand and recorded as a dated line; it voids only when it shows
  a reach outside the box.
- **The bare box holds one task folder (D11)**; the process clone holds the **generated tree** of the frozen tag as one
  orphan commit (D39): both stack folders (empty templates), all ten specs, `FORMAT.md`, `runner/`, and a README without its
  attribution section — so a process agent can read the other stack's empty template and the other tasks' specs (recorded
  as a property of the arm, not a contamination) and **cannot** reach a credit, a link or the attribution history; the
  needle scan over the generated tree and its one-commit history guards the cut. It cannot read the held-out suite, the
  references or another run's branch without a fetch, which the audit flags.
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
**frozen before any paid data run (D26):** the ten specs' hashes, `FORMAT.md`'s hash and the freeze tag; the suites' hashes and counts; the runner's manifest hash (`runner/`, D37) and the execution layer's identity (D43, §6.1); the GitHub broker's allow-list shape (D42); the private trap aid's sha256 (D41); **the certifying host** (host, kernel, cgroup version, the systemd scope probe — D36) on which every data session runs; the declared culture `de_DE.UTF-8` (D35); the series defaults T = 10 s and G = 5 s with every per-task override (D14); the
trap coverage table's hash; the limits of D14; the prompts; the pins and checksums (CLI, SDK, Node, TS, Python); the
package sets and cache hashes; the conventions parity release sha (D23); the owner's fixed per-session cap of $50 and the pilot formula's cross-check cap per arm (D7; no ceiling, no split)
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

with cost = the headline recompute (§8). If no attempt of (t, s, a) is accepted, CPC(t, s, a) is **"no correct solution"**,
shown with its attempts and its total spend so far in a descriptive column labelled **spend**, never as a CPC. **Second
line, pre-registered:** `CPC_all`, the same with void attempts' cost added to the numerator — the "what the series actually
paid" view. Beside both: success rate = accepted / |A|; tokens per model and kind; wall; `model_api`; `active − model_api`;
in the process arm active time and the compliance score k/18. A single session's cost that a cap censored stays a bound on
**spend** (`≥` the cap) in the session tables; that is the only `≥` in the report.

**The CPC ratio and its undefined cases (consult `68a33be2`):** the spend of a stack with zero accepted solutions does not
bound the cost of a correct solution that was never produced, so **no ratio is derived from it**. For task t and arm a,
the C#/TS CPC ratio is **defined** only when both stacks have ≥ 1 accepted attempt. When either has none, the ratio is
**undefined** and reported separately as an outcome: which stack had no correct solution, its attempts, its spend. The
cases, orientation C#/TS fixed:

| Case | Reported as |
|---|---|
| both stacks ≥ 1 accepted | the ratio `CPC(C#) ÷ CPC(TS)`, a value |
| TS 0 accepted, C# ≥ 1 | `undefined (TS: no correct solution, k attempts, spend $x)`; CPC(C#) shown beside it |
| C# 0 accepted, TS ≥ 1 | `undefined (C#: no correct solution, k attempts, spend $x)`; CPC(TS) shown beside it |
| both 0 accepted | `undefined (neither: C# spend $x, TS spend $y)` |
| a stack with no attempt at all (a round that did not run — a series stop, never a ceiling since D7) | `not run`; the task is undefined for that arm and says so; a spend of $0 is "no attempt", never a zero cost |

**Per task (D28):** for each arm, the C#/TS ratio of CPC and of every secondary measure where defined, the two success
rates (k of 3), "C# > TS" per measure. **Overall, per arm and per measure, over the defined ratios only:** the **median of
the per-task ratios** (primary), the **geometric mean of the per-task ratios**, and the **count of tasks where C# is
higher**; every summary states its denominator — "median over k of 10 tasks; m tasks undefined: …" — and never stands for
all ten; the count reads "C# higher in j of k". The undefined outcomes get their own line: **"C# alone had no correct
solution in x tasks, TS alone in y, both in z"**, with the spends. Per kind (olympiad 4, daemon 3, web API 3), and **known
(5) against original (5) separately (D29)**, the same figures with their own denominators. The **process effect** per stack
(process/bare CPC per task, median over the tasks where both arms' CPC of that stack is defined) and the **interaction**
(C#/TS process ÷ C#/TS bare, median over the tasks where the ratio is defined in **both** arms) as v1 §11 defined them;
**a task whose ratio is undefined in either arm leaves the interaction for that measure, and the interaction line names
it** ("interaction over k of 10 tasks; left out: t4 (C# no correct solution in the process arm)"). Every line is given with
and without pairs holding a `ran unpaired` session or a `foreign load` flag.

**What this licenses:** per task, "accepted k of 3", "C# > TS in j of 3 pairs" where defined, whether ranges overlap
(overlap = not distinguished), and for an undefined task the outcome itself ("TS produced no correct solution in 3
attempts"). Over the tasks, the median and geometric mean over the k defined ratios and "C# higher in j of k", always with
k and the undefined line — descriptive summaries of a chosen set, not a sample; no p-value, interval or effect size is a
finding (whether a sign test is shown as a secondary line is Q6). The report discloses that the five known tasks may be familiar to the model (D29). Nothing licenses
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
   the wall; a process session stopped at the $50 cap; the settled total of the series above the morning's $1000.

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
| T12 | **The time guesses and the cap.** A fixed $50 cap (D7) may cut a long process session of a task the pilot did not run, and cuts both stacks alike only if their session costs are alike | the pilot formula's cap per arm is reported beside $50 as a cross-check (§12, G-18); a `killed: cap` session is a censored data attempt in CPC's numerator, never dropped; the report counts them per stack and arm. *"Capped sessions are reported as bounds on spend; no CPC ratio is derived from a bound."* |
| T13 | **Public repository, secret or held-out leak.** A link in a workspace or a case in a sample would reach the agent or the public; the process agent's PR bodies are public | D2's rules; `leakcheck` on every push and on every PR body and comment (v1 §7.6 token scan widened); `workspace-diff`; the needles. |
| T14 | **Rule asymmetry biases the process arm.** v1 measured 34 rules / 331,833 B for C# against 32 / 324,781 B for TS; a rule that exists for one stack and not the other (a build flag, a test runner shape, a logging sink) is a treatment difference the process arm applies to one side only | **The conventions parity audit (D23, E0):** every C#/.NET rule has a Node/TypeScript counterpart and the reverse, released before the pre-registration; rule bytes actually read per session are recorded as v1's V1 asks. *"The process arm compares C#-under-the-family-rules with TS-under-the-family-rules; the rules were audited for parity at release `<sha>`, and the bytes read per session are in the record."* |
| T15 | **The process arm is a bundle and its wall is mostly waiting** (v1 V1, V3): gates, consultants, CI, `sleep 300`; the bundle's C# half carries the analyzers, `-m:4`, MTP, warnings as errors | The partition; active time beside wall; the compliance score beside `accepted`; the interaction reported with the sentence v1 wrote. *"The process arm's C#/TS difference is C#-under-the-process against TS-under-the-process, not a language effect; process wall time is largely not agent time."* |
| T16 | **Arm against time.** The two arms never overlap; v1's bare-first run confounded arm with window | D10's strict alternation of arm per round and the seeded lead; after every even round both arms have equal n and spanned the same days. *"Arms alternate by round; a window effect meets both arms."* |
| T18 | **The public history keeps the trap rows.** This plan's §4.3 trap table and the per-task trap columns were public in #124, #127 and #128 before they were moved to the private area (D41); history is not rewritten | No new trap content anywhere public (D41); the process agent has no direct GitHub egress (D42), so the only road to that history is the model's memory of a public repository; the record discloses it and names the three merges. *"The trap design aid was public for one day in the bench's history; sessions could not fetch it, and whether a model recalls it is not controlled."* |
| T19 | **The GitHub broker narrows the process arm.** A developer under the family's real process reads anything on GitHub; the process agent reads only its run's refs, PR and objects (D42) | Disclosed as a contract (D42), in the record and in every process-arm table; the compliance score and the delivery check are unchanged; the pilot (G-20) shows the normal clone, push, PR and checks workflows succeed under it. *"The process arm's GitHub access is run-bound by a broker serving an isolated bare repository per run; the process-arm result applies to two stacks under the same run-bound GitHub policy, not under the family's open one."* |
| T17 | **Vendor reviewers saw the held-out cases** (the bias review of D25 sends suites to coai's vendors before sealing) | The agents under test have no network and the suite is sealed; the vendor models are not the agent under test; recorded as a residual (Q-C). *"Reviewers of other vendors read the suites before sealing; the agent under test never did."* |

## 11. Pilot and A/A (D16)

Void spend, before any data pair: the bare A/A (two identical bare C# sessions of task 3, concurrent), then the bare pilot
pairs of tasks 1, 3, 4, 5 and 10 (task 4 for the HTTP driver, task 5 for the RESP driver, task 10 for the daemon checks — G-16), then the **process pilot pairs of tasks 1 and 3** (v1's P1 conditions: merged with green
checks, coai rounds in its db, the scorer's numbers reproduced by hand, T1–T5, `num_turns` > 1, audit and canaries ran,
read-back matches the pins), after v1's P0 (no model) and model smoke have passed on this repository's templates. GO =
every row of v1's G-1 … G-14 holds (G-6 may be `explained`, G-9 never gates, the two `not exercised` cases as v1 with their
replacing evidence), plus:

| Id | Must show | A miss means | Then |
|---|---|---|---|
| G-15 | the olympiad judge by hand: one C# and one TS pilot tree, run by hand on three held-out cases, give the judge's verdicts and times within 10 % | the judge misreads a real tree | fix, re-score (no re-run) |
| G-16 | the drivers **each checked on the real pilot tree of the task that exercises them** (all three tasks are in the bare pilot set, D16): **task 5** (the daemon with a framed TCP protocol) for the TCP driver — the driver's scenario kinds for that task, driven by hand, give the driver's replies; **task 10** (the DAG scheduler) for the daemon checks that are its own — the cgroup `memory.peak` read by hand from the program's scope agrees with the driver's record, the RSS curve by hand (`ps` every second) agrees with the descriptive curve within 10 %, SIGTERM by hand ends the process within G as the driver says, the task's timer scenarios give the driver's final report; **task 4** (the web task) for the HTTP driver — one scenario of each kind the driver has (a concurrency scenario among them), and a captured dynamic value reused as `FORMAT.md` defines; which scenarios, and what they target, is the private aid's (D41); the culture check of T-L32 passes inside both boxes | a driver misreads a real program | fix, re-score |
| G-17 | **calibration order held:** every task's limits were registered only after the joint validation of that task (drafted samples, references and suites run against each other; both references then pass every visible and held-out case — E2.S1 final — with no spec or `FORMAT.md` amendment pending), and no spec or `FORMAT.md` amendment touching that task is dated after its limit registration; both references of every task pass 100 % under the registered limits **in the scoring box on the series machine**, under the quiet check; each mutant fails exactly its cases | a limit was registered before the joint validation, or an amendment voided it, or a limit rejects one runtime's baseline, or a trap is not caught | a spec or `FORMAT.md` amendment after registration **voids that task's limits**: E2.S3 runs again for it and re-registers (dated, both stacks) before the pre-registration; otherwise recompute by D14 or fix the suite; all before round 1 |
| G-18 | the pilot formula's cap per arm (§12) computed from the pilot's settled costs and written beside the owner's fixed **$50** cap as a reported cross-check (D7; no split, no ceiling); the stall timeout per arm larger than the longest quiet gap seen in any pilot session; every pilot session that reached $50 recorded `killed: cap` | the stall would cut sessions the task needs, or the $50 cap was not enforced | set the stall from the pilot; fix the cap enforcement; before round 1 |
| G-20 | **the GitHub broker in the process pilot (D42):** in both process pilot pairs the clone of the run base, the fetch and push of the run's own branch, the PR creation and update and the reads of its checks and comments all succeeded through the broker (the stream and the broker log agree); the denied probes — a fetch of the bench repository, a read of the task repository's `main` README, a fetch of the other pilot run's branch and PR, a redirect outside the allowed set — were each attempted by the harness from inside a pilot box and refused with the broker's named error; no `github.com` CONNECT appears in the egress log | the broker lets a forbidden read through, or blocks a needed workflow | **stop**; fix (E3.S7a); one more process pilot pair |
| G-19 | the process pilot: both pilot pairs delivered (merged, green), every compliance item computable, the frozen spec unchanged in both runs, the plan gate reached `proceed` before any code gate, CPC computed by hand for one pair equals the tool's | the process arm's instruments misread a real run | fix; one more process pilot pair before round 1 |

Stops as v1: G-2, G-8, G-12 refuted, and alias drift, stop the series until fixed and an A/A is GO.

## 12. Budget (D7; D30 superseded)

**The owner's frame (2026-10-31, late evening): no total ceiling; a fixed per-session cap of $50.** The six rounds of both
arms run as planned. Every session carries the cap **$50** (the broker's budget and the CLI's `--max-budget-usd`, as v1 put
the cap on both): a session that reaches it is stopped and recorded as **`killed: cap`, censored at `≥ $50`**, a data
attempt in CPC's numerator (§9), never hidden. Pilot, A/A, data, void and re-run spend stay on separate lines in the
ledger and the report; the spend gate before every pair keeps only its **per-session** part (the live O sum of a running
session against its cap) and the **round rule** of D10 (round k+1 starts when round k is scored); it has no ceiling line.
The pilot's cap formula below is **kept as a cross-check that is reported and does not bind**: the record shows, per arm,
the formula's cap beside the owner's $50, so a reader can see whether $50 was generous or tight for the task.

**Superseded (D1; the text stays so the decision's history is readable):** the $1000 total ceiling and the gate's ceiling
line; the data share and the split between arms; the `budget-frame` halt over the total; the budget approval step and its
options table. None of them binds anything. The paragraphs below are that history.

*Superseded — the split and the caps, derived from the pilot (D30, G-18), pre-registered as a formula (now a reported
cross-check only):* per arm, per task,
`cap = min(2 × the larger settled cost of its pilot pair, capMax(arm))` for the piloted tasks; for the others, the piloted
task of the nearest guess scaled by the ratio of guesses (the process arm has two pilot points, so its scaling is marked
weaker); `capMax(arm)` is set so that 1.1 × Σ over the arm's planned data sessions (60 across 6 rounds, or the amended round count of the accepted budget frame) of their caps ≤ the arm's share of the **data
share** = $1000 − the settled pilot and A/A spend (16 sessions, ≈ $180–520 at the guesses, D16) − a **re-run reserve** of
1.1 × 2 × (`cap(bare)` + `cap(process)`) (two void re-runs per arm at the largest derived cap); the **share** is proposed
as proportional to the arms' expected totals from the pilot (so neither arm is starved by the formula), and is the
operator's to confirm (Q-A). At the guesses the data share is ≈ $400–800 before the re-run reserve, so the frame check
below is expected to fire unless the pilot comes in low.

*Superseded — the deterministic default when the frame breaks* (the cap rule fails — `capMax(arm)` < 1.5 × any pilot
cost of that arm — or the projected total, Σ expected costs + 1.1 × the re-run reserve, exceeds the ceiling) *and the
operator has not answered:* the runner **refuses to start round 1**. It exits with the resumable-halt code **3** and the named halt
`budget-frame`, prints the measured numbers (per arm: every pilot session's settled cost, the derived caps, `capMax`, the
projected total, the ceiling) and the exact resume command, `lang-runner.mjs resume --series <dir> --amendment <date>`,
which is accepted only when a dated budget line of that date exists in `amendments.md`. **It never cuts rounds, tasks or
arms on its own.** Any cut — fewer rounds, a smaller task set, a process-arm limit of its own, a raised ceiling — is the
operator's dated amendment, applied before round 1 and recorded in the pre-registration's amendments. The gate's own
round-pair cut (D10) applies only inside a frame the operator has accepted.

*Superseded — the budget approval step.* At this plan's own estimates — bare $300–1,200, process $1,800–4,200, pilot and
A/A $180–520 — six rounds of both arms did **not** fit $1000; the owner's answer on 2026-10-31 was to remove the total
(D7), so no decision line is needed and the options below were never exercised. Was: after the pilot and before round 1,
the operator records a dated budget decision in `amendments.md`: either **(i)** a raised ceiling with its amount, or **(ii)** a named reduction
— the rounds per arm, the task set, or the arms — or both. The runner's `budget-frame` halt stays the default until that
line exists; the plan does not pick an option (Q-A). The options and what each does to the analysis:

| Option | Effect on n per task | Effect on the analysis (§9) |
|---|---|---|
| (i) raise the ceiling to the projected total + the re-run reserve | n = 3 per task per stack per arm, as planned | the full contract: per task "j of 3 pairs", overall "k of 10" per arm, process effect and interaction over 10 tasks |
| (ii-a) fewer rounds per arm: 4 (two per arm) | n = 2 | per task "j of 2 pairs", no within-task range worth the name; overall still "k of 10" per arm; the interaction keeps 10 tasks |
| (ii-a') fewer rounds per arm: 2 (one per arm) | n = 1 | one pair per task per arm: no repeat, no range; "C# > TS" per task is one observation; the median over tasks is the only summary |
| (ii-b) a smaller task set (named tasks dropped; kinds and the known/original balance stated) | n = 3 on the kept tasks | overall "k of m" with m < 10; per-kind and known/original lines shrink or vanish with the kinds dropped; the dropped tasks are `not run` |
| (ii-c) one arm only (the bare arm, the operator's choice of arm) | n = 3 in the kept arm | no process effect, no interaction; the question of §1 is answered for one arm; the process arm is `not run` |
| (ii) combinations, for example 4 rounds on 8 tasks | as the parts | as the parts, each stated in every summary's denominator |

Every option leaves the pre-registered measures and classes untouched; it changes only how many observations each has,
and every summary names its denominator anyway (D28). The decision line names the option, the resulting round count, task
set and arms, and the new `capMax` per arm.

**Projection, as a guess only:** v1's one-minute bare sessions cost ≈ $0.35 each; v2's bare sessions run 5–75 min with many
build and test loops, so $5–20 each → 60 bare sessions $300–1200. v1 predicted $30–70 per **process** cell on a far smaller
task → 60 process sessions $1800–4200 at that rate; the pilot itself ≈ $180–520 (D16). With no total ceiling (D7) the
projection is information for the owner, not a gate: the record shows the settled spend per arm against it. The $50 cap
bounds any one session; at the guesses no bare session and few process sessions approach it, so censoring should be
rare — the pilot shows whether that holds (Q-A closed).

## 13. Growth

| Surface | Size at this plan's volumes | Retired by / interrupted |
|---|---|---|
| Session dirs `~/rsd/lang-v2/<series>/s/<rid>/` with seeded caches (≈ 150–300 MB each), the stream and transcripts (bare ≈ 50–150 MB; process ≈ 300 MB raw as v1 §14 measured for a process cell, plus the coai dir ≤ 20 MB), the scoring copy | bare ≈ 0.4–0.6 GB, process ≈ 0.7–1 GB per session; 120 data + 16 pilot/A/A (one A/A pair, five bare and two process pilot pairs) + re-runs ≈ 145 sessions ≈ **70–110 GB** on `/home` (745 GB free, 2026-10-30); the pilot's measured sizes replace this estimate | `bin/ obj/ node_modules/` shed after scoring (safe-rm under the series root, by name); the rest kept until E6's archive and `retire.mjs` after the record is promoted |
| Toolchains `~/rsd/lang-v2/toolchains/` (SDK ≈ 1 GB, Node ≈ 100 MB, TS, the CLI copy 292 MB), two cache snapshots (≈ 0.5–1 GB) | ≈ 3 GB | kept for the series; `retire.mjs` |
| Held-out source, references, mutants and the test-design aid `~/rsd/lang-v2/{heldout-src,refs,mutants,test-design}/` (mode 0700 / 0600; `test-design/PLAN-trap-table.md` is D41's moved table) | MBs | kept (the operator's record); never in git |
| `spikes/lang-bench/heldout/suite.enc`, `inputs/*`, `research/lang-v2/data/*.json` | a few MB, committed | kept |
| The public task repository: specs, workspaces, ≈ 60 process run bases + pilot + re-runs ≈ 70 `run/*` branches and ≈ 70–200 PRs | small; the branches and PRs are the audit trail | kept permanently (v1 §14) |
| coai session dirs, vendor CLI stores | as v1 §14 | with the session dir; the vendor stores are the operator's, growth measured and named |
| Credentials: one fine-grained PAT, one deploy key | live for the series | **teardown** after the last cell is scored and archived, as v1 §14 |
| Scoring cgroups and the clear suite in the scoring temp dir | one per scoring run | removed by the scorer's finalizer; `heldout-tool.mjs sweep` for leftovers older than 24 h |
| In-flight states | `launching` sessions, a scorer run, a daemon left by a driver, an open PR of a void session | `retireStale` at every `run`/`resume`; the walls, the scorer's outer limit (≤ 90 min) and the quiet wait (30 min) bound every non-terminal state; the janitor ends any process carrying the cell's marker; an open PR of a void session is closed by the harness with a comment (v1 §14) |

## 14. Build order

0. **Conventions parity audit (E0, other repository, now, in parallel):** the audit table; the missing counterpart rules
   written and released in `dew_flow_conventions`; the snapshot branch cut at that release — before step 4.
1. **Task repository (E1):** the operator creates `dew_flow_ai_language_comparison` (public); **the baseline runner
   `run_samples.py` built and pinned first (E1.S8a), and the execution layer built and probed on the series host
   (E1.S8b, D43: the box, the culture, the program-only scope, the shared loopback namespace, T, G, cleanup)**;
   `FORMAT.md` v2 gated (E1.S8, S9) against the runner before any spec gate;
   the runner copied into `runner/` (D37); per task, in order: the samples drafted (E1.S6), the spec written by the team
   lead, **the draft references written in E1 by the team lead with Sonnet coders into `~/rsd/lang-v2/refs/` (E1.S2a)**,
   **the pre-freeze execution gate** — both draft references run every sample and the task's riskiest-contract scenarios
   through the runner in the final sandbox environment with T, G and the step deadlines in force (D40) — then the gate
   (document gate + consultant + bias review) and the freeze (tag, hashes); the workspaces with `build.sh`,
   `run.sh` ending with `exec` (D38), pins, `PACKAGES.md`, `runner/`; the Node stack rationale and the attribution section
   in the README (D39); toolchains pinned; the locale `de_DE.UTF-8` in the box image (D35); the two cache snapshots; the
   process-arm templates, CI, rulesets, PAT and deploy key, with run bases cut as orphan generated trees (E1.S7, D39, built
   here to v1 §6's design, D32).
2. **References and suites (E2), in this order per task (the circular "references final before suites, suites before
   references" of the first draft is gone):** `FORMAT.md` v2, the runner (E1.S8a), the frozen spec, the samples and the
   two draft references that passed the execution gate (E1.S2a) → **drafts** of the suite, mutants and trap table (E2.S2)
   from the frozen spec, run by the same runner →
   **joint validation**: the references run against the suite and the samples;
   every disagreement is traced to the spec, the suite or a reference and fixed where the fault is (a spec fault by a dated
   amendment) → **references final** (E2.S1: both pass every visible and held-out case, nothing pending; E1.S6's sample
   validation finishes here) → only then **calibration and limit registration** (E2.S3, by D14 on the series machine); a
   spec or `FORMAT.md` amendment after a task's registration voids that task's limits and its calibration runs again before
   the pre-registration; the bias review of each suite; sealed; `leakcheck` clean.
3. **Harness (E3), on E1's execution layer and runner, never a second launch path (D43, §6.1):** `spikes/lang-bench/`;
   drivers and judge as scenario logic over the layer, and `research/module_tests.md` updated with their flows; the
   GitHub broker (E3.S7a, D42) before the process runner; the
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
| T-L3 | the olympiad judge: fake programs that print wrong output, sleep past the limit, allocate past the limit (on a host with a readable cgroup `memory.peak`), exit non-zero, each get their class; a right one passes; a `memory limit` verdict needs a readable `memory.peak` — without it the allocating program gets no memory verdict, its sampled peak is recorded as descriptive data only and the session carries `memory bound not certified` (T-L29); never `RLIMIT_AS` |
| T-L4 | the process driver: a stub daemon that ignores SIGTERM is killed after G (default 5 s, or the spec's override) and reported `stop failed`; one that exits 0 passes; a stub that leaves a child is reported `leftover`; a stub not ready within T (default 10 s) is `not ready`; a step past its deadline is `deadline`; RSS samples are every second ± 100 ms and descriptive |
| T-L5 | the TCP driver against a stub framed-TCP server (the driver's generic capabilities, not a task's scenarios): split frames, pipelined requests, a slow reader (backpressure), each scored by the scenario's expectation |
| T-L6 | the HTTP driver: a scenario with k concurrent identical requests checks the invariant once; a 5xx or a timeout is `fail`, never `not run` |
| T-L7 | limit calibration: given two reference measurements, D14's formula gives the registered limits; a reference failing under them is a calibration failure, not a passing suite |
| T-L8 | trap coverage: every mutant fails exactly its registered cases and no other; both references pass every case; a trap with no catching case is a failure of the table |
| T-L9 | the order: six rounds with arms strictly alternating and the lead drawn from the seed; every task once per round; rids distinct over arm, task, stack, repeat and attempt; masks swap per pair; a task's second repeat in an arm never precedes another task's first |
| T-L10 | the spend gate with the owner's fixed per-session cap of **$50** for every session of both arms (the runner's one cap constant; the broker's budget and the CLI's `--max-budget-usd` both read it) and **no ceiling line**: a session whose settled spend reaches $50 is stopped and classed `killed: cap`, censored at `≥ $50`, and stays in every table; the round rule refuses round k+1 while round k has an unscored pair; six rounds are planned whatever the settled total; the pilot formula's cap is computed and recorded as a cross-check and binds nothing (a fixture where the formula gives $12 still runs the session to $50) |
| T-L11 | the record has every per-kind and per-arm field known, `not captured` with a reason, or `n/a (<kind \| arm>)`; spawn and exit timestamps present |
| T-L12 | the contamination needles for the known tasks hit in tool inputs and outputs and are reported for a hand reading, never auto-voided |
| T-L13 | offline caches (effect, in a box): with the registries refused by the proxy, `npm install fastify@5.12.7` and `dotnet restore` of the pinned project succeed; a package outside the set fails naming it |
| T-L14 | D15's env keys are in the box and in the scoring box and in the record; `agentEnv` refuses a clash in any case |
| T-L15 | the classifier: `bash build.sh`, `bash run.sh`, `dotnet publish`, `node --test`, the drivers' commands land in `build_test`; the process categories as v1; the chain rule unchanged |
| T-L16 | `aaVerdicts` G-15 … G-19: each broken fixture refutes exactly its G; G-17 with one reference over a limit is NO GO; G-19 with a changed spec blob is NO GO |
| T-L17 | the record render: the generated blocks come from `data/*.json` only; a typed number is a failure; the licensing paragraph of §9 is present verbatim; known and original tasks are separate tables |
| T-L18 | **CPC:** three attempts costing 2, 3, 4 with one accepted → 9; none accepted → "no correct solution", spend 9, ratio undefined (T-L27); a void attempt is excluded from CPC and included in `CPC_all`; a `killed: cap` attempt counts its censored cost |
| T-L19 | **the overall figures:** ten per-task ratios → the median, the geometric mean (over the defined ratios only, the denominator stated) and the count of C# higher; one task at 100× moves the geometric mean and not the median, and the test asserts both numbers |
| T-L20 | **the spec-freeze guard:** a session whose spec blob at the run base differs from the frozen tag is refused before launch; a process session whose PR changed the spec gets the compliance item `spec unchanged: not met` and the flag |
| T-L21 | the Python suite runner runs unchanged against a C# and a TS stub implementation of one fixture task and reports the same case list; it imports nothing outside the standard library (a test greps the imports) |
| T-L22 | the process-arm compliance checker (built here, E3.S8) computes 18 items for a synthetic run from GitHub JSON, a `coai.db` copy and a stream, the 18th from the spec blob |
| T-L23 | the coai bridge refuses a `tools/call` whose `repoPath`, `committedPlan` or `suspectedFiles` resolves outside the session (symlinks and `..` included) with a JSON-RPC error naming the rule, and passes one inside (v1's T-B1) |
| T-L24 | the process runner: a run base cut from the frozen tag, a session's clone single-branch with a restricted refspec and `--no-tags`, a push outside `run/<rid>/` flagged, `delivered` read from the merge into the base, an undelivered session scored on its PR head and marked |
| T-L25 | the calibration order: `register-limits` for a task is refused while that task's joint validation has not passed (a reference failing any visible or held-out case, or a sample still in draft) or a spec or `FORMAT.md` amendment touching the task is dated after the last validation; an amendment after registration voids that task's limits in `inputs/limits-lang.json` (a test on the pure function) |
| T-L26 | **metric retention (D34):** the v2 record renderer, run over a fixture series, emits every v1 metric column — the four cost views, T1–T5, tokens with the cache split, wall, `model_api`, `active − model_api` **and every category span of the partition**, **visible quality per script** and held-out counts/percent/defects, class, labels, flags, charges, **and the process arm's compliance items** — beside the new ones; a test removes one v1 column from the renderer's list and fails |
| T-L27 | **undefined CPC ratios (§9):** fixtures for the three cases — C# alone 0 accepted, TS alone 0 accepted, both 0 — each gives the task line `undefined (<stack>: no correct solution, k attempts, spend $x)` with no `≥`/`≤`, the overall median and geometric mean over the defined tasks only with the stated denominator, the count "j of k", the undefined line "C# alone … x, TS alone … y, both … z", and the interaction leaving that task out by name when either arm is undefined; the consultant's example (C# CPC = 2, TS spend 9 with 0 accepted) gives `undefined (TS: no correct solution, spend $9)`, never a bound; a stack with no attempt gives `not run` |
| T-L28 | **the registered limit, not the floor, judges a run:** a stub program taking 1.2 s against a registered 3 s limit (floor 1 s) passes; the same program against a registered 1 s limit is `time limit`; the limits come from `inputs/limits-lang.json`, never from the spec text |
| T-L29 | **memory certification refusal (D13, D36):** with no readable cgroup `memory.peak` (a fake cgroup root without the file), `run --what round:1` and every data-session launch are **refused** before any process starts, with the named halt `memory-not-certifiable`, exit 3, the probe's output printed; a **non-data run** (smoke, fixture, a pilot the operator allows) on the same host proceeds, carries the flag `memory bound not certified`, and its record holds **no** memory pass (the memory field is `not certified`, the sampled curve present and labelled descriptive); with `memory.peak` present, a data session records a certified memory pass or a `memory limit` verdict and `accepted` includes it; the series record states the certifying host; a CPC fixture with and without the flag on non-data runs gives the same denominators (data sessions only) |
| T-L30 | **the scope (D36), effect tests on this host:** the scorer starts a stub in its own `systemd-run --user --scope` with `MemoryMax` and `MemorySwapMax=0`, outside `bwrap`, and reads back that the stub and a child it forks are in the scope's `cgroup.procs` and that the driver is not; fixtures — an **allocating child** (the parent stays small, the child allocates past the limit: `memory.peak` and `memory.events` show it, the run is `memory limit`); an **OOM-killed child whose parent exits 0** (`oom_kill` ≥ 1 in `memory.events`: the run is `memory limit`, never a pass, whatever the exit status); a **lingering child** (the parent exits, the child stays: `leftover`, and the scope's `cgroup.procs` is the evidence); `memory.peak`, `memory.events` and the lifecycle record are kept before the scope is removed |
| T-L31 | **`run.sh` execs (D38):** for each stack's template, the pid the harness starts is the program (the process's `comm`/`exe` is `dotnet` or `node`, not `bash`), it has no bash parent inside the scope, and a SIGTERM sent to that pid is received by the program (a stub that logs the signal); a template whose `run.sh` lacks `exec` fails the test |
| T-L33 | **the execution layer's network layout (D43):** a driver and a stub program started by the layer have equal `/proc/<pid>/ns/net` ids, different from the host's, and different cgroup membership (`/proc/<pid>/cgroup`: the program in its scope, the driver not); `lo` is up and the driver reaches the program's port on `127.0.0.1`; no other interface exists in the namespace; the same test body runs for a C# stub and a TS stub and reports the same facts |
| T-L34 | **the execution layer's isolation and cleanup (D43):** the program cannot read a driver-only sentinel file (a path outside its bwrap binds); killing the program's scope removes every descendant (a forked child included) while the driver survives and collects `memory.peak`, `memory.events`, the exit status and the transcript; after cleanup no process, scope or namespace of the run is left; identical for both stacks |
| T-L35 | **the GitHub broker (D42):** from inside a process box, clone of the run base, fetch and push of the run's own branch, PR create and update, and reads of the own PR's checks and comments succeed through the broker, and the pushed branch is mirrored to GitHub; a fetch of the bench repository, a read of the task repository's `main` README (the attribution), a fetch of another run's branch, a read of another run's PR, and a redirect to a host or path outside the broker are each refused with a named error and logged; **a fetch by the SHA of a task-repository `main` commit fails (the object is not in the run's bare repository); an unshallow fetch (`--unshallow`, `--depth` widened) gives only the run base**; the PAT appears in no box environment or argv; a direct CONNECT to `github.com` is refused by the proxy |
| T-L36 | **the shared layer's invalidation rule (§6.1):** a changed layer identity marks every D40 gate record and every registered limit calibrated on the old identity as `stale`, and `register-limits` and `run` refuse until the gate is re-run and calibration re-registered (a test on the pure function over fixture records) |
| T-L32 | **the declared culture (D35), inside the actual boxes:** in the agent box and the scoring box, for both stacks, `LANG` and `LC_ALL` are `de_DE.UTF-8` and `locale -a` lists it; a .NET probe prints `CultureInfo.CurrentCulture.Name` = `de-DE`, `double.Parse("1.5")` = 15 under the current culture and 1.5 under `CultureInfo.InvariantCulture`; a Node probe prints `Intl.DateTimeFormat().resolvedOptions().locale` starting `de`; `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` is unset; the same values appear in the session record |

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
- [ ] One memory contract held (D13, D36, §4.3, T-L29): every data session ran on the certifying host named in the
      pre-registration, in its own systemd user scope, with `memory.peak` and `memory.events` kept (T-L30), and its
      `accepted` includes the certified memory claim; the halt `memory-not-certifiable` refused every data launch on a
      host without `memory.peak`; the flag `memory bound not certified` appears on non-data runs only, never on a data
      session, and no CPC denominator depends on it.
- [ ] The declared culture `de_DE.UTF-8` held in both boxes for every session (T-L32; D35), and `run.sh` exec'd the
      program in every workspace (T-L31; D38).
- [ ] CPC is the headline, computed as §9 says; "no correct solution" outcomes are present as spend, never as a CPC or a
      ratio bound; the overall figures are the median, the geometric mean and the count over the defined ratios with their
      denominators stated and the undefined line beside them, per arm; known and original tasks are shown apart.
- [ ] Every v1 metric is in every v2 record beside the new ones (D34, T-L26 green in E6.S1): the four cost views and T1–T5,
      tokens with the cache split, the time partition with every category span, visible and held-out quality, class, labels,
      flags, charges, the compliance items.
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
- [ ] Limits were registered only after the joint validation made the references final, and every spec or `FORMAT.md`
      amendment after a registration voided and re-ran that task's calibration before the pre-registration (G-17).
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

- **Q-F — closed by the owner, 2026-10-31: the narrowing is approved; the run-bound GitHub broker (D42) stands as
  written.** The owner noted that every repository stays public — privacy was never the aim; the broker exists because the
  process agent could otherwise read the trap history in the public bench repository, which the bare agent cannot reach.
  Epoch 4 is approved `by owner` (narrowed: the process arm's GitHub capability).
- **Q-A — closed by the owner, 2026-10-31: no total ceiling.** The $1000 is removed; the six rounds of both arms run as
  planned; a fixed per-session cap of **$50** stays as a runaway guard (a capped session is censored, never hidden); the
  pilot's cap formula is a reported cross-check that does not bind (D7, §12).
- **Q-C The bias review sends held-out suites to coai's vendors** before sealing (D25). The agents under test never see
  them, but other vendors' models do. Accept the residual, or review the suites by an in-house Opus judge only and send the
  vendors the specs alone?
- **Q-D The process agent's clone** holds the generated tree of the frozen tag as one orphan commit (D39: no attribution,
  no credit history): both stack folders (empty templates) and all ten specs. Accept the other tasks' specs as a property
  of the arm (§7), or generate per-task run bases that hold only the task's spec?
- **Q-E Process-arm caps and walls.** v1 planned $35 / 240 min for minigit; v2's tasks are 10–60× larger in agent time. The
  pilot sets them (G-18); should the process wall have a ceiling of its own (for example 6 h) so one session cannot take a
  day?
- **Q1 Licences and credit.** The licence of the new public repository (our originals, the specs, the workspace templates,
  the visible suites), and the terms under which the known problems may be paraphrased: CSES statements, IOI 2018 Werewolf
  (the IOI's task licence to be read), RealWorld's spec (MIT), Redis's RESP (a protocol; the trademark). v1's D18 found
  upstream files with no licence; v2's credit text is written after these are read.
- **Q4 — answered (D36).** The host was probed on 2026-10-31: cgroup2 with the memory controller; `systemd-run --user
  --scope -p MemoryMax=64M` gives a readable `memory.peak` (2,396,160 B) and `memory.max`. **This host can certify.** The
  scorer runs each program in its own user scope launched outside `bwrap` (D36); certification is required before launch
  and the pre-registration records the host. What stays to check in E3 (T-L30): that the scope holds the program and its
  forked children and not the driver, under the real `bwrap` layout.
- **Q5 The cap formula** of §12 is a proposal; the operator confirms the formula and the share rule or replaces them before
  the pre-registration.
- **Q6 Any inferential statistic?** §9 reports the median, the geometric mean and the count. A pre-registered sign test over
  the ten task medians is possible as a secondary line with the "chosen set, not a sample" sentence; wanted or not?
- **Q7 The process pilot's task set.** Bare: tasks 1, 3, 4, 5, 10 (D4, plus tasks 4 and 5 for the HTTP and RESP drivers'
  real-tree checks — decided, D16 and G-16). Process: tasks 1 and 3 proposed (D16). Adding task 10 to the process pilot
  would calibrate the dearest process sessions at a high cost. Which process set?
- **Q8 — closed (D35).** One declared non-invariant culture, `de_DE.UTF-8`, in both boxes, both stacks, every task and CI's
  runner; no invariant switch; fairness is the declared environment plus the protocol's stated numeric syntax (spec 04's
  grammar), not equal trap counts; T-L32 checks it inside the boxes.
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

### E1 — The task repository: the format document and its v2, the runner and the execution layer, specs gated and frozen after a pre-freeze execution gate, workspaces with the runner, toolchains, caches, process templates (§14 step 1 · 12 stories)

- **Goal:** ten specs written by the team lead, gated and frozen; ten workspaces per stack an agent can start in, identical
  across stacks but for the stack, on pinned toolchains and offline caches; the process arm's run-base machinery; in a
  public repository that leaks nothing.
- **Scope:** D2, D5, D6, D11, D12, D19, D21, D25 (the spec half of the bias review), D31; §4.1, §4.5 (the stack sentences),
  §4.6, §5.
- **Depends on:** the operator creating the repository; the licence answers (Q1); E0.S3 for the pin; v1 §6's template
  design for S7 (D32: built here, nothing imported).
- **Done when:** T-L1, T-L2, T-L13, T-L20's guard half green; `workspace-diff` clean on ten tasks; both snapshots hashed; the
  preflight passes inside a box on SDK 10.0.403, Node 26.13.0, TS 6.0.x (the pinned patch), the pinned Python; `FORMAT.md` is gated and its
  three examples run (S8); the ten specs conform to it, carry a gate verdict and the freeze tag; the README carries the credits and the Node stack table; `leakcheck` clean.
- S1 — repository skeleton: layout of §5, conventions pin (E0.S3), README with credits and the §4.6 table, LICENSE (Q1), the
  `workspace-diff` check. *Accepted when:* the check passes on an empty task pair and fails on a planted difference; the
  README's links and attribution appear nowhere under `csharp/`, `node/`, `tasks/`, `runner/` or `todo/tasks/`, and the
  generated run-base tree holds none of them (a test greps the tree; D39). The tree generator is this story's
  (`generateRunBaseTree`); the orphan commit and its one-commit `git rev-list` are S7's, tested there after the push (OP-8).
- S2 — **the ten task specs, written by the team lead, gated and frozen** (cornerstone area; D21, D25). Per task, in the
  order of §4.1: the spec at `todo/tasks/NN-slug.md`; the coai document gate with `purposeText` (who acts on it: the agents
  under test in both stacks and the suite's author; what they must be able to do: build a program that passes the hidden
  suite from the text alone; what it does not cover: the tests, the traps), a consultant, the bias review question of §4.3;
  every finding `accept`ed or rejected with a reason; the verdict and notes recorded in `inputs/bias-review/NN.md` (no
  case text). **Per task the spec is gated** — verdict, hash, the amendment rule in force from the gate on — and **one tag
  freezes all ten together after the tenth** (OP-2's reading, now the text): the tag `spec-frozen-<date>` on the commit holding all ten and `FORMAT.md`, their sha256 in
  `inputs/specs.json`. **Prerequisites:** `FORMAT.md` v2 (S8, S9) is gated before the first spec is gated; every spec
  conforms to it (a conformance check: header rows, R-ids, the fixed sentences with their slots, the sample table against
  the `samples/` files, the invalid-input and build-bound sentences, T and G stated); **the task's `samples/` files (S6)
  exist before its conformance gate** (D40).
  *Per-task acceptance:* the gate's verdict is `proceed`, `good_enough` or `continue_anyway`; the consultant answered; the
  bias question got an explicit answer per stack with no open finding; no id, title, site or trap in the text; a known
  task's needles (D18) find nothing; the spec conforms to `FORMAT.md` (the check passes); the limits as floors and rule,
  the timing contract (T, G, step deadlines) and the semantic bounds, I/O and start contracts, frozen list and "hidden
  tests exist" are present; **the pre-freeze execution gate (D40): both draft references (S2a, one per stack) execute
  every visible sample through the distributed runner (`runner/run_samples.py`, S8a) in the final sandbox environment (the
  box, the scope, the declared culture) and pass, with the spec's T and G and the step deadlines in force** — a value a
  reference cannot meet is changed before the freeze; the gate also runs **the task's riskiest held-out-shaped scenarios,
  listed in the private aid** (`PLAN-risky-scenarios.md`, D41), kept out of git, through the same runner — both draft
  references pass them (the count per task is in the aid; the public text names no scenario);
  two readers (an Opus judge and the consultant) list no open question, or
  every one is resolved by a text change before the gate round that passed. *Whole-story acceptance:* the ten verdicts and
  the tag exist before E2.S1 **finalises** a reference (the draft references exist earlier, S2a); after the
  tag a spec changes only by a dated amendment applied to both stacks and before the pre-registration.
- S2a — **the draft references, written in E1** (D40): one C# and one TypeScript reference per task, written by the team
  lead with Sonnet coders per the role table from the spec draft, living in the bench's private area `~/rsd/lang-v2/refs/`
  (outside the task repository's git, beside the held-out material; never in a workspace or a run base). They exist so
  the execution gate of S2 can run; E2.S1 finalises, validates and calibrates them. *Accepted when:* for each task both
  drafts build and run in the final sandbox environment through `runner/run_samples.py` (S8a) and pass every visible
  sample and the task's riskiest held-out-shaped scenarios listed in the private aid (D41); the drafts are hashed in `inputs/references.json` as
  `draft`; no draft or its hash leaks into the task repository (`leakcheck`).
- S3 — the workspaces: `STATEMENT.md` generated from the frozen spec plus the stack sentence (and the framework sentence
  for a web task), `build.sh`, **`run.sh` ending with `exec` of the program (D38)**, `PACKAGES.md`, **`runner/` — the
  pinned `run_samples.py` and `GRAMMAR.md`, read-only (D37)**, pin files per stack, the C# `.csproj` skeleton and
  `nuget.config` offline source, the Node `package.json`/lockfile/`tsconfig.json`/`.npmrc offline`/`eslint.config.mjs`.
  *Accepted when:* in a box with no registry, each stack's empty workspace builds and `run.sh` starts a hello program;
  T-L2, T-L31; `python3 -I runner/run_samples.py samples/` runs inside the box against the hello program and reports the
  sample verdicts; the `runner/` hash equals the bench's `inputs/specs.json` entry; the generated statements equal the frozen
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
  bytes with the `⏎` glyph; a daemon or web sample is a scenario in `FORMAT.md`'s grammar). E2.S2's runner reads this
  format; `FORMAT.md` (S8) fixes it, E2.S2 does not choose it. The samples are **drafted** here, **exist before the spec's
  conformance gate**, run against both draft references through the runner in the final sandbox environment **before the
  freeze** (D40, S2's execution gate), and are **validated** again with the held-out cases in E2's joint validation (§14
  step 2). *Accepted when:* every spec's sample table and its `samples/` files hold the same bytes (a test); the runner
  runs them against both draft references in the box and both pass before the freeze; the same holds against the final
  references (closes in E2); no sample reaches a trap — checked now against the private test-design notes
  (`~/rsd/lang-v2/test-design/`, D41) and formally in E2 once the trap table exists (OP-9).
- S7 — the process-arm templates in this repository: CI with one job per stack, family checks, PR title; rulesets
  `run-main`, `template-frozen`, tags; the deploy key as the only bypass actor; the PAT; `GitHub settings` as v1 §6 —
  built here to v1 E2.S4's design (D32). *Accepted when:* v1's P0 checks pass here: a run base cut by the deploy key, a direct push
  with the PAT refused, CI green on a non-default base, a planted violation red per stack, merge blocked until threads
  resolve. Built here to v1 §6's design (D32), for one repository with two stack folders.
- S8 — **`todo/tasks/FORMAT.md`, the versioned spec and sample format** (a prerequisite of S2): the spec conventions of §4.1
  (header rows and placeholders, R-ids, byte-exact default, the frozen-list wording, the limits as floors and rule, the
  build bound, the invalid-input sentence, no plan ids, the `⏎` glyph); the `<id>.in` / `<id>.out` pair; the **scenario
  grammar** of `.in` (steps, named connections, synchronisation and barriers, timing and deadlines, dynamic-value capture
  and reuse, the SIGTERM step) and the **comparison semantics** of `.out` (byte-exact default, normalised fields,
  order-insensitive sets where a spec says so); **one worked example per kind** — the olympiad example from the pilot spec
  01, a daemon scenario over a neutral framed TCP protocol (a split frame, pipelined requests, a slow reader; no real product named, D39), a web scenario (HTTP concurrency, two
  clients racing on one idempotency key); a version line. FORMAT v1 had no gate of its own: it is a draft re-conformed into
  v2, and **v2's gate (S9) is its gate** (OP-3). *Accepted when:* the
  three examples parse and run through S8a's runner (E2 owns no runner, D37) against stub programs (one per kind) with the expected verdicts; the
  conformance check of S2 is derived from this document; its sha256 is in `inputs/specs.json` and frozen with the specs; a
  change after the freeze is a dated amendment of `FORMAT.md` that names the tasks it touches (G-17 voids their limits).
  *Built so far (ungated drafts, sorted by the epoch-3 re-review):* v1 at `d7c73ff` with spec 01 conformed at `28bf777`;
  the pilot specs 05 (`30af434`) and 04 (`0b69a50`) written under it found the gaps S9 closes.
- S8a — **the baseline visible-sample runner, `runner/run_samples.py`** (D37, D22): the bench's Python standard-library
  program implementing FORMAT v2's grammar (`<id>.in`: steps, named connections, barriers, timing and deadlines, capture
  and reuse, the SIGTERM step) and its transcript comparison (`<id>.out`: byte-exact default, normalised fields,
  order-insensitive sets, exact-decimal JSON numbers, relational expectations), for the three kinds; **an early E1
  deliverable, built before FORMAT v2's gate so that FORMAT v2's examples and every pre-freeze check (D40) run through it**;
  pinned by version and sha256 in `inputs/specs.json`; the same file is CI's runner and E2.S2's held-out runner (E2 owns
  no runner). This breaks the FORMAT ↔ runner circularity: the runner is a program that exists, FORMAT v2 is checked
  against it, and the specs are checked against both. *Accepted when:* T-L21's shape holds for the three kinds against
  stub programs (stdlib-only imports; identical case lists for a C# and a TS stub); FORMAT v2's three examples and the
  three pilot specs' samples (01, 05, 04 — "four drafts" counted FORMAT v1, OP-4) run through it with the expected
  verdicts; the pinned version and hash are recorded before FORMAT v2's gate; the workspace copy equals the pinned tree
  (S3's check). **`runner/` is a directory** (OP-5, the 800-line rule): standard-library modules with `run_samples.py` as
  the entry, hashed as one manifest in `inputs/specs.json`, the frozen list naming `runner/`; under `-I` Python leaves
  the script's directory off `sys.path`, so the entry adds its own frozen, read-only directory itself.
- S8b — **the execution layer** (D43; a prerequisite of the D40 gate, of E2.S1/S3 and of E3.S4): `spikes/lang-bench/lib/exec-layer/`
  — launch of `run.sh` in a bwrap box (v1's binds) with D15's environment and the culture `de_DE.UTF-8`; the program-only
  `systemd-run --user --scope` with `MemoryMax` and `MemorySwapMax=0` (D36); the **private loopback-only network namespace
  shared by the driver and the program** (`unshare -rn` from the harness, `lo` up, the box joins it; PID and filesystem
  isolation from bwrap, the driver outside the scope); the readiness deadline T, the step deadlines, SIGTERM and the grace
  G; cleanup of the scope and the namespace; one identity (the tree's sha256) in every record. Consumed unchanged by the
  gate, by E2 and by E3 (§6.1). *Accepted when:* T-L33, T-L34, T-L30, T-L31, T-L32 green on the series host with
  `BENCH_REQUIRE_SANDBOX=1` and nothing skipped — equal netns ids and different cgroups for driver and program; the
  driver-only sentinel unreadable from the program; killing the scope removes the descendants while the driver survives
  and collects the evidence; the probes run identically for both stacks; the pre-freeze gate of S2 runs on this layer
  (D40 not weakened); the layer's identity is in `inputs/specs.json` beside the runner's.
- S9 — **`FORMAT.md` v2**, gated before any spec gate (a prerequisite of S2's gates, like S8), **after S8a exists**. The gaps found by the three
  pilot specs and the consult `3ea9ef36`, each closed in v2: **exact-decimal number comparison in JSON transcripts** (an
  integer and a decimal compared by exact value, `int`/`Decimal`, never float; `-0` equals `0`); an **F-LOAD fixed
  sentence with slots** for a load scenario's size and duration; **default request headers** of the HTTP driver and how a
  spec adds or overrides one; **connect retrying within the readiness deadline T**; **the runner's behaviour on a closed
  HTTP connection** (reconnect once within the step deadline, then `fail`); **relational expectations** (a final value
  equals the increments sent; a set of values is pairwise distinct) as comparison kinds in `.out`; **`{ref}` lists longer
  than two**; **tables belonging to their R paragraph** (a table is part of the requirement it follows); **the T and G
  binding** (the series defaults 10 s and 5 s, how a spec overrides, where the values are read by the runner — D14);
  **F-BYTES-W against F-JSON precedence** (which comparison wins when both apply to a reply); **`sleep` step deadlines**;
  **split offsets on joined bytes** (a partial-frame step names the byte offset of the split in the joined request bytes);
  **a neutral daemon example** that does not name the real protocol (D39); **removing the reading block** from every
  daemon and web spec once `runner/` and `GRAMMAR.md` are in the workspace (D37); **the base-environment sentence**
  following D15 — the same base plus the documented per-stack variables (not "the same for both stacks"); **the
  §1.5 credit sentence following D19 and D39 (no `NOTES.md`; credit is the README's attribution section — OP-7)**; **the
  maintainer sections 7–9 moved out of `FORMAT.md`** into an internal document (`FORMAT-MAINTENANCE.md` in the bench's
  `spikes/lang-bench/inputs/`), so no plan id stands in a file the process agent's run base carries; a version line `2`
  and a changelog entry per gap. *Accepted when:* every gap above has a v2 section or a fixed sentence, named in the
  changelog; the three examples and the three pilot specs' samples parse and run through the runner against stub programs
  with the expected verdicts; no plan story, epic, decision or test id remains anywhere in `FORMAT.md` (a grep); the
  daemon example names no real protocol or product (D18's needles find nothing); v2's gate verdict (document gate +
  consultant) is recorded before the first spec gate; its sha256 replaces v1's in `inputs/specs.json`; specs 01, 05 and 04
  are re-conformed to v2 before their own gates.
- **Cornerstone area:** the specs (S2).

### E2 — Shared acceptance suites, references, trap coverage, limits, bias review (§14 step 2 · 5 stories)

- **Goal:** the acceptance instrument calibrated before any model touches a task: one neutral Python suite per task that
  runs unchanged against both stacks, two references per task, suites that catch every designed trap, limits that reject
  neither runtime's baseline, and a bias review of each suite.
- **Scope:** D13, D14, D22, D25 (the test half); §4.3 calibration; the sealed archive; `leakcheck`.
- **Depends on:** E1.S2 frozen specs, E1.S2a draft references, E1.S5 toolchains, E1.S6 samples, **E1.S8a the runner**
  (E2 owns no runner: S2 reuses `run_samples.py` for the held-out suites and owns only the suites, the mutants, the trap
  table and the calibration).
- **Done when:** T-L7, T-L8, T-L21, T-L25 green; G-17's conditions hold on the series machine (recorded, re-checked at the
  pilot), including the calibration order; the archive sealed with hashes and counts in `inputs/heldout-prereg-lang.json`;
  the ten bias-review verdicts recorded; `leakcheck` clean on the branch.
- **Order inside this epic, per task (§14 step 2, D40):** **before the spec freezes** — the samples (E1.S6) and the two
  draft references (S1) exist and the references execute every sample through the runner in the final sandbox environment
  (E1.S2's execution gate; T, G, the step deadlines and the semantic bounds validated on both) → the spec freezes → the
  suite, mutants and trap table (S2) drafted from the frozen spec and `FORMAT.md` → **joint validation**: the references
  against the suite and the samples, every disagreement traced to its fault (spec: dated amendment; suite or reference:
  fixed) → S1 **final** → S3 (execution time and memory calibrated by D14's rule). S3 registers a task's limits only when
  S1 is final for that task; a spec or `FORMAT.md` amendment dated after a task's registration voids that task's limits,
  and S3 runs again for it before the pre-registration (G-17).
- S1 — references **finalised**: the two draft references of E1.S2a (one C# and one TypeScript per task, written by the
  team lead with Sonnet coders from the spec, living in `~/rsd/lang-v2/refs/`) are validated jointly with S2's suite and
  calibrated. *Accepted when (= "final" for that task):* **after the joint validation** both pass every visible and every held-out case; every disagreement between
  them, or with the suite, was traced to the spec, the suite or a reference and fixed where the fault was (a spec fault by a
  dated amendment); **no spec or `FORMAT.md` amendment is pending** for the task; the dated list and the final mark per
  task are in `inputs/references.json`.
- S2 — **the shared suites and the trap coverage table** (cornerstone): per task one Python module (standard library only)
  with the stdin/stdout, HTTP or TCP/RESP + signals mode, **reading the fixed case format of E1.S6** (`<id>.in` /
  `<id>.out`, change 1.2b) and `FORMAT.md`'s scenario grammar and comparison semantics; the held-out cases (counts
  registered), drafted from the spec and validated jointly with S1's references; the table task × stack × trap → cases;
  mutants one per trap per stack; **every case cites the requirement ids (R1 … Rn) it asserts** (change 1.2c; v1's "names
  the spec sentence" rule, now by id). *Accepted when:* T-L8, T-L21; the joint validation passed (both references pass
  every case; every disagreement traced and fixed at its fault); every trap of the private aid (D41) has at least one catching case; every case
  cites at least one R-id that exists in the frozen spec (a test against `inputs/specs.json`'s id lists); the table and the
  mutants' ids are sealed; no case appears in git in clear.
- S3 — calibration and limit registration by D14, **only after S1 is final for the task**: reference measurements in the
  scoring box, the formula applied, the limits registered per task in `inputs/limits-lang.json` with the spec hash they were
  calibrated against. *Accepted when:* T-L7, T-L25, T-L28; the registration is refused for a task whose S1 is not final
  (joint validation not passed); both references pass under the limits in three consecutive runs under the quiet check; a
  spec or `FORMAT.md` amendment after registration voids that task's entry and the re-run re-registers it, before the
  pre-registration; every registered entry names a spec hash and a `FORMAT.md` hash equal to the frozen (or last amended)
  documents; the registered values go to the runner (`inputs/limits-lang.json`) and never into a spec.
- S4 — the bias review of each suite (D25): spec + suite in clear from a scratch dir to the coai document gate with
  `documentText` and a consultant, the §4.3 question; findings resolved; verdict and notes in `inputs/bias-review/NN.md`
  (no case text). *Accepted when:* ten verdicts, each `proceed`/`good_enough`/`continue_anyway`, every finding
  resolved; a finding that changed a case is traced to a spec sentence; Q-C's answer recorded.
- S5 — adjudication packets for v2: v1's `adjudicate.mjs` pointed at the v2 series with stack and arm masked. *Accepted
  when:* a synthetic series produces packets that show case, output and spec sentence and nothing of the stack or arm.
- **Cornerstone area:** the suites and the trap table (S2).

### E3 — Harness extension: two arms, long sessions, drivers, judge, scoring per kind, CPC in the record, the process arm built with its GitHub broker (§14 step 3 · 11 stories)

- **Goal:** one command runs one pair of any task in either arm under the v1 rules, scores it by its kind, computes CPC's
  inputs, and leaves a record the report can be built from; v1's instruments untouched but for named widenings; the
  process arm **built here to v1's design** (D32), never copied from code that does not exist.
- **Scope:** §6; D10, D15, D17, D18, D24, D27, D32; §11's G-15 … G-19; the smoke; the coai box and bridge, the process runner,
  the compliance checker, P0 and the model smoke on the task repository; `research/module_tests.md`.
- **Depends on:** E1 workspaces, caches and the repository-side templates, rulesets, PAT and deploy key (E1.S7); **the
  execution layer (E1.S8b) and the runner (E1.S8a), consumed unchanged — E3 builds orchestration, aggregation and records
  on top of them, never a second launch path (D43, §6.1)**; E2's sealed archive for the stub tests' shape.
- **Done when:** T-L3 … T-L6, T-L9 … T-L12, T-L14 … T-L16, T-L18 … T-L20, T-L22 … T-L24, T-L28 … T-L32 green; the host's
  memory certification (the systemd user scope's `memory.peak` for the program and its children, D36) is checked first on
  the series host and recorded, and the runner refuses data sessions without it; the culture check (T-L32) passes inside
  both real boxes; a stub pair of
  each kind in each arm runs end to end in real boxes; the bare smoke (task 1, TS, ≤ $0.20) is read by E/O/T/B with T1–T5; P0 passes on
  the task repository's templates; the model smoke lists the vendors through the bridge; `research/module_tests.md` carries
  the harness's flow catalogue; the module document describes the extension.
- S1 — the arm, task and round dimensions of the runner: `ridOf` with arm and task and a `single` phase, six alternating
  rounds from the seed, the pair without barrier, the void schedule without v2 rows, the prereg guard's v2 list and the
  spec-freeze guard, `EGRESS_HOSTS.langBare` / `langProcess`, timestamps in the record. *Accepted when:* T-L9, T-L10,
  T-L12, T-L20; `run --dry-run` prints six rounds with their arms.
- S2 — **the process, TCP and HTTP drivers with RSS sampling and the SIGTERM check** (cornerstone), as thin wrappers over the
  Python suite runner, **started through E1.S8b's execution layer** (the scope, the shared loopback namespace, T and G —
  the drivers add the scenario logic and the records, not a launch path; D43). *Accepted when:* T-L4, T-L5, T-L6; a driver never leaves a
  process (the janitor finds none after each test); **and `research/module_tests.md` is updated** (`common.knowledge-base`,
  the scenario-tests rule) with: (i) the exact runner command (`node spikes/lang-bench/run-tests.mjs`, with
  `BENCH_REQUIRE_SANDBOX=1` and the glob for the driver tests); (ii) every scenario flow this epic adds, each marked
  `covered` or `not covered` with a reason — stdin/stdout case under time and memory limits, each verdict class, HTTP
  scenario step, HTTP concurrency scenario (k clients at once), TCP scenario with split frames, pipelined requests and a
  slow reader (the driver's capabilities, not a task's held-out content), readiness wait within T, step deadlines, RSS sampling every second (descriptive), the cgroup-accounted memory bound in the program's own scope with `memory.peak` and `memory.events`, SIGTERM clean stop within G and the
  grace period, a program that ignores SIGTERM, a leftover child, cgroup `memory.peak` against the sampling fallback, the
  offline cache in the scoring box, the scoring-time record; (iii) **explicit limits on what the harness proves** — it
  proves a program's observable behaviour against the registered cases and scenarios on this machine under these limits,
  not correctness in general; a Python driver bounds the load it can apply; RSS is sampled, so a peak shorter than the
  sample interval can be missed unless `memory.peak` is present; the stop check sees the session's processes, not threads;
  a case classed ambiguous is a spec matter, not a harness proof. A test (T-L17's shape) asserts that every flow id named in
  the drivers' test files appears in the catalogue.
- S3 — the stdin/stdout judge with cgroup limits and verdict classes, **on E1.S8b's layer** (the scope is the layer's; the
  judge adds the case loop and the verdicts); memory certified only by `memory.peak`, else the
  `memory bound not certified` flag and no memory pass (§4.3). *Accepted when:* T-L3, T-L28, T-L29.
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
- S7a — **the GitHub broker with an isolated bare repository per run** (D42; a door of the credential broker's shape, E3.S7
  depends on it): a host-side HTTP(S) door the box reaches as its `github.com` (the proxy allows no direct CONNECT). Per run
  it **creates a local bare repository** holding only the orphan run-base commit (D39) and the run's refs; the agent's
  `git clone`, `fetch` and `push` are served from that bare repository by the broker's smart-HTTP endpoint (no object
  outside it exists to be fetched — by name, by SHA or by unshallowing); after each push the broker **mirrors** the run's
  branch to GitHub with the PAT; the REST and GraphQL calls that create and update the own PR and read its checks and
  comments are made **by the broker on the agent's behalf**, for the allowed operations only; everything else — another
  repository, another run's ref or PR, the task repository's `main`, any redirect outside the broker — is refused with a
  named error and a log line. *Accepted when:* T-L35 (the SHA and unshallow probes included); a stub process session
  completes the normal clone → branch → push → PR → checks → merge workflow through the broker against a `run/smoke-*`
  base while every denied probe fails; the mirrored branch on GitHub equals the bare repository's; the PAT is in no box
  environment or argv; the broker's log is in the session record; G-20 pilot-tests it on both process pilot pairs.
- S7 — **the process runner**: the process session verbs over v1's bare runner (E3.S1's dimensions) — run base `run/<rid>/main`
  cut from the frozen tag with the deploy key, the single-branch clone with a restricted refspec and `--no-tags`, the PAT in
  the session gitconfig through `gh auth git-credential` (never on an argv), `GH_CONFIG_DIR`, `GH_PROMPT_DISABLED=1`, the
  GitHub broker of S7a as the box's only road to GitHub (D42; no direct egress), `github.mjs` (PRs, checks, merges, intervals for `ci_wait`) on the harness side with the PAT, `delivered` read from the merge
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
  GO; the bare and process pilot pairs ran; the stall and limit amendments are dated lines and the cap cross-check is in the record (the cap itself is the owner's $50, D7); `seed.txt` revealed;
  pilot spend in the ledger per arm.
- S1 — the day-1 price re-read and the CLI pin copy (D8, Q10). *Accepted when:* `prices-<date>.json` dated that day; the CLI
  copy's sha256 recorded.
- S2 — `prediction.md` (§9's draft, finalised), `statement.md`, `analysis-contract.md` (CPC, the per-task and overall
  figures, known/original, the void and re-run grounds), the outcome table, the limits, the package sets, the hashes, the
  specs' freeze tag and hashes, the parity release sha, `seed.sha256`. *Accepted when:* v1's prereg tests widened pass
  (verbatim-from-main, derived not typed, seed as hash only, no held-out needle).
- S3 — the commit and the tag, before any paid session. *Accepted when:* the guard peels the tag to the commit.
- S4 — the bare A/A, the bare pilot pairs (tasks 1, 3, 4, 5, 10; D16), the process pilot pairs (tasks 1, 3; Q7), G-1 … G-19, the
  cap cross-check (§12: the pilot formula's cap per arm reported beside the owner's fixed **$50** cap, D7), the amendments,
  the seed reveal. There is no total ceiling and no `budget-frame` halt (superseded, D7); the runner's cap constant is
  **$50** for every session of both arms, and `run --what round:1 --dry-run` prints the six planned rounds with that cap.
  *Accepted when:* GO; every amendment is instrument-only or applied to both stacks; the pilot's settled costs and the
  formula's cross-check cap per arm are in the ledger and the record; no pilot session's settled cost exceeded $50
  without the stop recorded as `killed: cap` (T-L10).
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
  `RESULTS_lang_v2_<date>.md` with generated blocks: per task (CPC or the undefined outcome, success, tokens, time, both
  arms), the overall median, geometric mean and count per arm over the defined ratios with their denominators and the
  undefined line, known against original, process effect and interaction with the tasks they leave out named, every
  attempt listed, **and every v1 metric beside the new ones (D34)**, **with the disclosures of D41 and D42**: the trap
  design aid was public in #124, #127 and #128 before it moved to the private area (T18), and the process arm's GitHub
  access was run-bound by the broker so that the process-arm result applies to "two stacks under the same run-bound
  GitHub policy" (T19, D42), each as a sentence in the record's threats and beside every process-arm
  table. *Accepted when:* T-L17, T-L26 (metric retention:
  the four cost views and T1–T5, tokens with the cache split, the partition with every category span, visible and held-out
  quality, class, labels, flags, charges, the compliance items), T-L27; the record renders from the data alone.
- S2 — the blind adjudication of every failing case; the voids replayed with the current scan; the prediction beside the
  observation. *Accepted when:* every failing case has a step-1 and step-2 verdict; the revised score sits beside the
  primary.
- S3 — publication (references, the trap table and the specs' final state in the task repository, Q11), credential
  teardown, archive, `retire.mjs`, `research/README.md`, promotion of this plan and its stories documents with deviations.
  *Accepted when:* `plan-lifecycle.mjs` clean; PATs revoked and the deploy key deleted; the worktree removed.
- **Cornerstone area:** none.
