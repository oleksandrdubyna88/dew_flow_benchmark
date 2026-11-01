# PLAN — exploratory pilot: language-comparison task 1 on the v1 bare harness, 3 C# ∥ TypeScript pairs, to see v2's cost and time scale early

> Status: **IMPLEMENTED and RUN, 2026-11-01.** Built on `feat/lang-explore-task1` (coai code gate PROCEED, re-review PROCEED;
> pilot suite 150/150, v1 1232/1232 with 0 skipped under `BENCH_REQUIRE_SANDBOX=1` at `6191377`), run as series `x-20261101-13cc`:
> 3 pairs, 6 of 6 sessions completed and accepted, 0 voids, $1.04 in all. Results: `research/lang-v2/EXPLORE_task1_2026-11-01.md`
> (generated) and `research/lang-v2/EXPLORE_task1_2026-11-01_analysis.md` (the reading, with seven findings for v2 E1). Promoted
> from `todo/` on 2026-11-01; the code comments and the pinned `spikes/lang-explore/inputs/explore.json` still name the plan at its
> `todo/` path, as they were when the series ran, and are not edited after it. The approved body below is unchanged.
>
> Status when approved: plan only, epoch 1 open since 2026-10-31 with 0 changes, approved by the coai plan round
> (PROCEED from both reviewers on `f858222`, its five findings applied as the baseline). One unit, no epics. Owner-authorised (2026-10-31), **exploratory, not
> evidence**: it is NOT part of `todo/PLAN_lang_comparison_v2.md`'s pre-registered series and replaces none of its steps (its D16
> pilot still runs task 1 on v2's harness). Scope: a new spike `spikes/lang-explore/` that imports `spikes/minigit-bench/`; four
> named widenings of v1 files (§2.2); held-out cases, references and raw records outside git under `~/rsd/lang-v2/explore/`; one
> public note `research/lang-v2/EXPLORE_task1_<run date>.md`. Budget: no total set by the owner; **$50 per session**. The coai plan
> round ran before any code; the ledger below opened at that approval (`common.plan-change-control`). Updated the same day,
> before that round: the v2 plan's epoch 4 is on `main` (#129, `90d0395`); TypeScript pinned at 6.0.3; spec 01 re-pinned at its
> FORMAT v2 conformance. This pilot touches no approved plan's text: the boundary with v1 and v2 is recorded here only (§9).

> Deviation note, 2026-11-01 (code review F2; the approved body above is unchanged): §7's recovery sentence "a crashed pair is re-run under the next
> pair number" is REPLACED by a resume. `explore.mjs run --pair k` on a pair whose sessions a halt or a failure left open (planned; `launching` after a crash;
> or ended with data and not yet scored) plans NO new session: v1's `reconcile` first settles a `launching` session (charged once from the broker ledger, class
> `void: harness`, or planned again when nothing was paid), then v1's loop launches what is planned and scores what is unscored. A new pair number is never
> spent on a crash, so the paid session of a crashed run is not lost to the barrier. Rehearsal series are named `r-<yyyymmdd>-<4 hex>`, so no command picks one
> by mistake; `run` takes `--series` (required).

## Plan changes

> Epoch 1 · approved 2026-10-31 · by reviewers · state: open

  (The coai plan round on `f858222`, PROCEED from both reviewers. Its five accepted findings are the baseline, applied in the
  approving commit: a fresh workspace per case, one time-limit definition, a kill path that tolerates a missing unit, the 30 s
  calibration ceiling, and no edit to the v2 plan. The coder brief at the end restates the approved plan and changes nothing.)

## 1. The goal, before any solution

v2 guesses its costs and times (§4.1: task 1 "2–5 min" per bare session; D16: "$5–20" per bare pilot session) and will learn
them only at its own pilot, after E1–E3 are built. The owner wants the scale **now**, on the harness that exists: run spec 01
("Descent to one", `dew_flow_ai_language_comparison` branch `docs/format-v2` `d622768`, `todo/tasks/01-descent-to-one.md`) as three bare pairs, C# ∥
TS, and check that everything behaves as we expect — cost views that agree, a time partition that sums, no false voids, a
scorer that scores — before v2 depends on it. The answer is a short note generated from records, banner *exploratory, not
evidence*, stating every result against the prediction in §5.

What it does **not** settle: anything about C# against TypeScript. Three pairs of the easiest task on a harness that differs
from v2's (§8) license sentences about this run only (`common.measurement` rule 3).

## 2. What exists — verified at `53f248e` (this worktree's base)

### 2.1 Reused by import, unchanged

| Need | v1 piece | Note |
|---|---|---|
| One session in the box, broker, proxy, OTel, watchdogs | `lib/session-launch.mjs:252` `launchSession(plan)` | takes a plain plan object: prompt, caps, `allow`, `toolchains`, `envExtra` are fields (`:142-143`) |
| Pairs on CPU masks, same tick, canaries | `lib/bare-phase.mjs:136` `launchPhase`, `:24` `maskedSpawn` | needs seam W1 (below): its workspace fill (`:32`) and plan (`:75`) are minigit's |
| Classify by how it ended, scans, cost | `lib/bare-collect.mjs:89` `collectSession` → `bareOutcome`, `sessionCost`, `crosscheck` (E/O/T/B, T1–T5), aliases | generic; its text rules (minigit's names, the bench's name) stay as they are |
| Time partition | `lib/timeline.mjs:68` `partition` via `smokeViews`; wall spawn→exit, `model_api`, `active − model_api` | v1's categories as is (see §3 D-X9) |
| Records | `lib/bare-record.mjs:124` `sessionRecord`, `:185` `ratioOf`, `:218` `leafProblems` | the pilot hands it a score in v1's shape (`:98` `qualityPart`) |
| Pins in a box, TS toolchain install | `lib/preflight.mjs:56` `preflight({ stacks })` | the pilot passes its own `stacks` object |
| Offline caches | `lib/cache-snapshot.mjs:162` `makeSnapshot`, `:58` `seedCaches`; C# snapshot `cache-csharp-0a5733e2d4f1` (made under SDK 10.0.114) | the Node snapshot is new (D-X4) |
| Quiet check, sampler, spin | `lib/quiet.mjs`, `lib/cpu-sampler.mjs:227` `createSampler`, `inputs/quiet.json` | `liveSessions` is a function: the pilot passes v1's root and its own |
| Stub rehearsal | `lib/stub-cli.mjs:85` `stubClaude`, `lib/stub-upstream.mjs:35` | $0 |
| Lock, ledger, stale sessions | `lib/series-lock.mjs:115`, `lib/ledger.mjs:25` `appendChargeOnce`, `lib/session-state.mjs:85` `retireStale` | |
| Leak check | `lib/heldout-leak.mjs:111` `leakcheck({ tokensText })` | needles from the pilot's held-out files |
| Tests | `run-tests.mjs` (lowered, `min(8, CPUs − 1)` workers; cwd is its spike, so the glob is `../lang-explore/test/*.test.mjs`), `test/helpers.mjs` `tmpDir`, `test/fake-home.mjs` | one temp helper, as v1's (its legacy prefix changes with the bench's producer stage, `common.test-leftovers` § barrier) |
| Pinned agent | `~/rsd/minigit/toolchains/claude-2.1.310` (sha256 `b7367643…7df56`), `lib/launch.mjs:46` `claudeArgv` (`opus`), `:22` `settingsFor` (effort `high`) | v1's prices `inputs/prices-2026-10-29.json` and policy `inputs/crosscheck-policy.json` as is |

### 2.2 Minigit-specific — not reused, or reused through a named widening

| v1 piece | Why it does not fit | The pilot's answer |
|---|---|---|
| `lib/workspace.mjs` (SPEC-v1.txt, test-v1.sh), `lib/bare-plan.mjs:64` (minigit prompt, `:9` $6 caps), `lib/egress-hosts.mjs:12` (registries allowed) | minigit inputs, v1 caps, network to registries | new `lib/workspace.mjs`, `lib/plan.mjs`; `allow: []` (v2 D17) |
| **W1** `lib/bare-phase.mjs:32,75` | the fill and the plan are hard-wired | optional `ctx.fill` and `ctx.planFor`, defaulting to today's functions |
| **W2** `lib/sandbox.mjs:97` `LANG: 'C.UTF-8'`, `extra` may not replace it | D35 wants `de_DE.UTF-8` | optional `locale` argument to `agentEnv`: sets `LANG` and `LC_ALL`; default unchanged |
| **W3** `lib/bwrap.mjs:37` (scoring box, no `extra` at all) | same | the same optional `locale` on `scoreEnv` / `scoreBoxArgv` (`:58`) |
| **W4** `lib/bare-record.mjs` `cellOf` (`:169`) sums a v1 and a v2 phase | the pilot has one phase | export the existing private `sessionMeasure`; the pilot's single-phase cell feeds `ratioOf` and `pairRecord` |
| `score-pipeline.mjs`, `heldout-run.mjs`, the sealed suite, `test-v*.sh`, adjudication | minigit's binary, scripts and archive | new `runner/run_samples.py` (Python stdlib, the one judge, §4) and `lib/score.mjs` |
| the v1→v2 schedule, prereg guard, spend gate's $40 / $500 | v1's series contract | none: one phase, no pre-registration, no total |
| `lib/classify.mjs:91` knows `test-v*.sh`, `run-tests.sh`, not `build.sh` / `run.sh` | widening it would re-class v1's recorded sessions on replay | not widened (D-X9) |

Every default stays byte-identical for v1 callers; each widening has a RED-GREEN-RED test and v1's suite stays green under
`BENCH_REQUIRE_SANDBOX=1`. v1's harness identity changes (its lib tree id); v1's bare arm is closed (v2 D32), so no v1 record
or series is affected, and a v1 re-run would need `--accept-harness` as for any instrument change.

## 3. Decisions

| # | Decision |
|---|---|
| D-X1 | **Arm: bare only** (the process arm needs v2's GitHub broker, not built). **3 pairs** C# ∥ TS, started in the same tick on masks `0-5` / `8-13`, swapped every pair (a random draw, recorded, decides pair 1); harness on CPUs 6-7 as v1. **Barrier:** pair k+1 is refused until both sessions of pair k are collected, scored and recorded. One command per pair, so the operator reads pair 1 before paying for pair 2. A void is re-run once, alone (`ran unpaired`) |
| D-X2 | **Agent:** CLI 2.1.310 (v1's pinned copy), `--model opus`, effort `high`, v1's tool list. **Caps = v2's:** $50 (broker budget and `--max-budget-usd`), wall 75 min (v2 D4), stall 45 min |
| D-X3 | **Stacks:** host .NET SDK **10.0.114**, runtime 10.0.14 — *deviation: v2 D6 pins SDK 10.0.403, not installed here*; Node **26.12.1** (v1's, `~/.nvm/versions/node/v26.12.1`; v2 pins 26.13.0); TypeScript **6.0.3** (the owner's 6.0.x decision, v2 D6 and its epoch 4 status on `main` since #129, `90d0395`; npm lists only 6.0.2 and 6.0.3 in 6.0.x, and `typescript-eslint` 8 peers `>=4.8.4 <6.1.0`) |
| D-X4 | **TS 6.0.3 offline.** Checked 2026-10-31: v1's toolchains hold only `typescript-7.0.2`, v1's Node snapshot carries 7.0.2; the operator's `~/.npm` cache holds `typescript-6.0.3.tgz` (fetched 2026-10-25) and is NOT used. `prepare` — **the only step with network** — resolves `typescript@6.0.3` from the registry, records its integrity hash, installs it read-only at `~/rsd/lang-v2/explore/toolchains/typescript-6.0.3/` through `preflight`'s install path, and makes the Node cache snapshot (`typescript@6.0.3`, `@types/node@26` highest) with `makeSnapshot` in a box behind the proxy with the registry list, read-only and tree-hashed, as v1 did. Sessions then run with **no host allowed** |
| D-X5 | **Locale `de_DE.UTF-8`** (D35; generated on the host — `locale -a` lists `de_DE.utf8`) in the agent box and the scoring box, `LANG` and `LC_ALL`, through W2/W3; D15's other keys (`DOTNET_gcServer=0`, `NODE_OPTIONS` empty) through `extra` |
| D-X6 | **Workspace per stack, as spec 01's R13 and FORMAT v2 (`1531de8`) require:** `STATEMENT.md` (spec 01's bytes plus the stack sentence, v2 §4.1; the spec carries FORMAT v2's fixed sentences, F-LIMITS-O, F-RUN-O and F-SAMPLES-O among them), `samples/s01…s03.{in,out}` (FORMAT v2 §3.2: raw bytes, no header; the same bytes as the spec's §6 table, §3.4), `runner/` (below), `build.sh`, `run.sh` (last command `exec`, D38), `PACKAGES.md` (spec R13: the SDK or the standard library only), the pins (C#: `global.json` 10.0.114 `rollForward: disable`, `nuget.config` offline, a `src/App.csproj` skeleton; Node: `package.json`, v2 §4.6's `tsconfig.json`, `.npmrc` `offline=true`; `build.sh` = `tsc --noEmit`, `run.sh` = `exec node src/main.ts`). Templates in git (`spikes/lang-explore/templates/`); the spec and FORMAT texts are read from the local repository at their pinned sha256 and never committed here. **`runner/`**: `GRAMMAR.md` = FORMAT v2's sections 3 to 5, verbatim (its §0 rule); `run_samples.py` = the pilot's one judge (§4), byte-identical to the harness's copy, **olympiad only** — `python3 -I runner/run_samples.py samples/` builds with `bash build.sh` and prints one `PASS`/`FAIL` line per case (FORMAT v2 §3.1, §5.6), time judged against the 1 s floor, no memory. It is **not** v2's E1.S8a runner (not built; §9) |
| D-X7 | **Prompt:** v2 §4.5's bare prompt as written (its `runner/` sentence holds, D-X6); v2's stack sentences with SDK 10.0.114 and TypeScript 6.0.3. Bytes and sha256 in `spikes/lang-explore/inputs/prompts.json`; read back from one cell per stack (`common.measurement` rule 1) |
| D-X8 | **Scoring** (§4): Python stdlib, visible + held-out, a fresh copy of the built workspace per case; **time limit: 2 s is the floor, the effective TL = max(2 s, 3 × the slower reference's slowest case wall)**, measured under the 30 s calibration ceiling and recorded in the run's inputs; memory certified by a systemd scope (probed easy, §4; required, spec 01 R11) |
| D-X9 | **Time:** every v1 measure (wall, `model_api`, `active − model_api`, every category). v1's classifier does not know `build.sh` / `run.sh`, and is not widened (it would re-class v1's sessions on replay), so **build/test time** is reported beside it, labelled: the summed spans of Bash calls whose command runs `build.sh`, `run.sh` or `tsc`/`dotnet build`, from v1's `callsOf`. v2 E3's widening decides how that lands for v2 |
| D-X10 | **Records:** v1's session and pair records (D34: every v1 metric), plus an `explore` section: visible and held-out pass rates, verdict counts by class, per-case wall and certified peak, build time, the limits used, trap hits (count only; detail private). **CPC** per stack (D24): Σ cost of the stack's sessions / accepted, `no correct solution` with its spend when 0 |

## 4. Scoring

- **Who writes the held-out side, and where.** A Sonnet coder, briefed by an Opus judge, writes in `~/rsd/lang-v2/explore/heldout/`
  (mode 0700, outside every git tree, never bound into the agent box): a Python reference `gen.py` that computes every expected
  output; one C# and one TS reference in workspace shape; the mutants of the private notes' §2. The cases are
  the held-out ideas of `~/rsd/lang-v2/test-design/01.md` (read for this design; sha256 `0eb31164…`, recorded here before any run;
  nothing of it is copied into this plan, D41). `gen.py` writes `<id>.in`, `<id>.out` and `cases.txt` (one line per case: id, the
  R-ids of spec 01 it asserts, `byte-exact`) — v2 §4.3's format — and asserts the private notes' fact table, so the expected bytes
  have a second, independent derivation. Every case maps to at least one R-id; the visible samples map too.
- **The judge, `spikes/lang-explore/runner/run_samples.py`** — one file, two modes, as v2 D37's "same runner file": the visible mode
  in the workspace (above), and the held-out mode the harness runs on the host (`python3 -I`, `/usr/bin/python3` = 3.14.4, sha256
  `52e0a13e…`; `-I` adds no script directory to the import path, so the file is self-contained). In held-out mode it is handed by
  `lib/score.mjs` a pristine copy of
  `box/work` (v1's `excludedDirs`), the cases directory and the scoring-box argv (v1's `scoreBoxArgv` with W3, offline caches,
  no network): `bash build.sh` once, 600 s (R14), in a scoring copy that is then made read-only (the built tree); then, **per case,
  a fresh copy of the built tree** (copy-on-run, removed after the case) in which `bash run.sh` runs with the case's stdin, so
  nothing a run writes reaches the next case (R15, R16). The harness scores the visible samples the same way. A case passes when stdout
  equals the expected bytes (R8), the exit is 0 (R10), the wall is within the limit and the memory claim holds; stderr is
  recorded, not judged (R9). Verdicts: `pass`, `wrong answer`, `time limit`, `memory limit`, `runtime error`; a failed build
  fails every case. Output is read up to a cap; a timeout kills the whole scope (`systemctl --user kill --signal=KILL <unit>`) and then
  proves it is gone (`common.security`). **The kill path tolerates a missing unit:** a unit that already ended answers "not loaded"
  or "not found" (exit 5, or exit 1 with that message), which is not a harness failure; after the kill the judge checks
  `systemctl --user is-active --quiet <unit>` and asserts no process remains (the scope's `cgroup.procs` empty, or the unit gone).
  A process that remains is a harness failure that stops the pilot. The cases are piped from the host; the box never sees them.
- **Memory.** Each run is a `systemd-run --user --scope -p MemoryMax=<ML> -p MemorySwapMax=0` around a `sh` wrapper that starts
  the box and, after the program ends, reads its own scope's `memory.peak` and `memory.events`. Probed 2026-10-31 on this host: a
  50 MiB allocation read `memory.peak` 56,082,432 B and `oom_kill 0` after the program had exited. The peak includes the wrapper
  and `bwrap`; an empty-program control measures that overhead and the note reports it. Spec 01's R11 (FORMAT v2) runs the tests only
  on a machine that can read this peak, so there is no descriptive fallback: if the build-order probe fails on the real path, the
  pilot halts before pair 1 with `memory-not-certifiable` (v2 D36's halt), and `ru_maxrss` from `wait4` is kept beside the peak as
  description only.
- **Limits, and how they were chosen.** One definition, D-X8's: the time limit's floor is 2 s and the **effective TL = max(2 s,
  3 × the slower reference's slowest case wall)**; memory limit **ML = max(512 MB, 2 × the larger reference peak)**, both measured
  through the same scope and box on this host before pair 1 and written into the run's inputs (`<series>/inputs.json`), which
  every record names. **The calibration runs each reference case under a fixed ceiling of 30 s**; a reference case that exceeds it, or
  fails, stops the pilot before any paid session.
  2 s and 512 MB are twice spec 01's floors (R11: 1 s, 256 MB) because the pilot has no calibration (v2 E2.S3) and every run pays
  a fixed start-up (scope, `bwrap`, the runtime's host) that only a calibration can measure; doubling the floor keeps a correct
  program in either runtime from failing on start-up noise. Every case's wall and peak are recorded, so the note states how many
  passing runs would have failed at the floors.
- **Validated before it reports anything** (`common.measurement` rule 2): both references pass 100 %; each mutant fails exactly
  the cases the private notes predict for it; the first paid pair's verdicts are read by hand (all visible cases and at least 20
  held-out verdicts across the pair) before any pass rate appears in the note.
- **Accepted** (for CPC) = a clean build, every visible and held-out case passed, the memory claim certified.

## 5. Prediction — recorded before the run

| Measure | Predicted | Why |
|---|---|---|
| cost per session | $0.15–0.60, median ≈ $0.25 | v1's paid bare sessions cost $0.17–0.30 (`research/minigit/RESULTS_bare_2026-10-30.md` §5) on a larger spec; this one is shorter but the prompt asks for checks |
| wall per session | 1–4 min | v1's 38–77 s per session; v2 §4.1 guesses 2–5 min |
| `active − model_api` | C# > TS in most sessions | v1 series B: C# > TS in 3 of 3 (the build) |
| visible pass rate | 100 % in every session | three clean samples |
| held-out | accepted in ≥ 5 of 6 sessions; trap hits 0–1 per stack | an easy task for this model; a count only (D41) |
| harness | 0 voids; T1–T5 hold; the partition sums to the wall; ≤ $50 in every session by two orders of magnitude | v1's last series |

## 6. Cost and time

**Paid:** 6 sessions × $0.15–0.60 ≈ **$1–4**, plus at most one re-run per void (≈ $0.25 each). Free: `prepare`, the rehearsal,
the references, the scoring. The bound the cap allows is 12 × $50 = $600 (every session and re-run running away); nothing in
v1 came within a factor of 100 of a cap, and the operator reads each pair before the next. **Elapsed:** ≈ 1 hour for the three
pairs (sessions, a 60 s quiet window per pair, scoring), plus the build steps.

## 7. Growth

| Surface | Projected size | Retired by | Interrupted |
|---|---|---|---|
| Session dirs `~/rsd/lang-v2/explore/x-<yyyymmdd>-<4 hex>/s/<rid>/` | ≈ 80 MB each (≈ 40–70 MB of seeded cache copies, the rest stream, transcripts, scoring copy); ≤ 16 dirs (6 + 6 re-runs + 4 rehearsal) ≈ 1.3 GB | `explore.mjs prune` after the note commits: removes the cache copies and scoring copies (reproducible), keeps the records (≈ 5 MB each, ≈ 80 MB) — kept until v2's record is written, then the owner decides | `retireStale` at the next command (owner by pid + start time); a crashed pair is re-run under the next pair number, recorded |
| Toolchain + Node snapshot `~/rsd/lang-v2/explore/toolchains/` | ≈ 30 MB + ≈ 40 MB | removed with the records; v2 pins its own under `~/rsd/lang-v2/toolchains/` | a half install is detected by `preflight` and redone |
| Held-out side `~/rsd/lang-v2/explore/heldout/` | < 1 MB plus two reference trees | kept, private, until v2's series ends (D41) | — |
| systemd scopes | one per scoring run, transient | end with their processes; a timeout kills the scope by its unit name | the kill is checked |

## 8. Risks

1. **Spec 01 is spent on a run that was not pre-registered.** The model does not learn from a session (its weights do not change
   during the series); whether the account's conversations may be used for future training is an account setting the owner
   confirms (Q-1; not blocking, as v1's paid sessions ran under the same setting). The pilot publishes no spec text (STATEMENT.md lives only in session dirs), no held-out case, no reference, no
   agent code or transcript; the public note gives counts and rates only, and v1's `leakcheck` runs over the repository and the
   branch range with the held-out expected outputs as needles before the note is committed. What becomes public that was not:
   that task 1 was run, and how easy it was (task 1's identity is already in the public v2 plan §4.1). A residual: "trap hits:
   k per stack" with k > 0 says a stack-specific failure exists — the owner's chosen form; detail stays private.
2. **Tuning after looking.** What the pilot shows could tempt a change to spec 01 or to the private trap list that produces a
   difference. Guard: the private notes' sha256 is in this plan before the run; any change to either after the pilot is a dated
   amendment citing the pilot's finding; spec 01's gate (v2 E1.S2) reads the note as an input; v2's pre-registration lists this
   pilot as an input to its predictions, as D16 does for its own pilot.
3. **The pilot's held-out files are not v2's suite.** v2 E2 writes its own references and suite (two stacks, calibration, sealing);
   it may read these as a draft, recorded as such.
4. **Harness differences from v2** — each is named in the note: no execution layer (D43) and no shared netns (not needed for
   olympiad); `runner/` is the pilot's olympiad-only judge, not E1.S8a's (D37); SDK 10.0.114 (not 10.0.403), Node 26.12.1 (not 26.13.0); limits 2 s / 512 MB uncalibrated (D14);
   build/test time beside the partition, not in it (D-X9); no pre-registration, no seal, no A/A; CLI 2.1.310 not re-pinned (D8);
   v1's contamination rules (minigit-named text rules idle; the path rules active).
5. **A session that runs away** costs up to $50 and 75 min; the cap is the guard, and the pair-by-pair command is the brake.
6. **Unattended upgrades** move the SDK (v1, 2026-10-30): the preflight refuses on a mismatch; the operator holds `dotnet-*` for
   the pilot's day.

## 9. Boundaries

| Item | v1 (`todo/PLAN_minigit_stack_bench.md`) | v2 (`todo/PLAN_lang_comparison_v2.md`) | This pilot |
|---|---|---|---|
| W1–W4 | gains four optional seams, defaults unchanged | reuses them in E3 (its §6 already calls for named widenings) | adds them |
| Task 1 pilot sessions | — | D16 runs task 1 again on v2's harness, void | exploratory only; no v2 step is skipped |
| Spec 01, the private aid | — | owns them | reads them at pinned hashes, changes neither |
| Held-out side, records | — | — | outside git under `~/rsd/lang-v2/explore/`, disjoint from v2's `~/rsd/lang-v2/` series dirs |
| `runner/run_samples.py` | — | E1.S8a builds the series runner (all three kinds, FORMAT v2's grammar) and pins it; it may read the pilot's as a draft, recorded as such | an olympiad-only judge for this pilot |

Order: this pilot first; v2's E1 runs in parallel and is not blocked. **This table lives only in this plan:** the pilot edits no
approved plan's text, neither v2's nor v1's. Only v1's module document (`research/module_minigit_bench.md`, a description of
the code, not a plan) gains the four seams when they land.

## 10. Build order

1. **Gate and probes (no paid call).** The coai plan round on this file (passed, `f858222`). Pins: `typescript@6.0.3`'s integrity from the registry
   (network), `python3` sha256, spec 01 at `d622768` (sha256 `d890bb3fab5f9b4e6899a8578e852526cdab25a9316eda0860f21f6c3706742e`),
   FORMAT v2 at `1531de8` (sha256 `5b476946bf9f0a61f87845bead56043953777d038269a117000af149ec8f3d9c`) — a later amendment moves a pin
   in one dated line before pair 1, never between pairs; the scope probe on the real path.
2. **W1–W4**, RED-GREEN-RED each; v1's suite green under `BENCH_REQUIRE_SANDBOX=1`.
3. **`prepare`** (the one networked step): the TS toolchain, the Node snapshot, the C# snapshot re-hashed; spin calibration.
4. **Held-out side** (Sonnet, outside git): `gen.py`, the cases and `cases.txt`, the two references, the mutants.
5. **`runner/run_samples.py` + `lib/score.mjs`**, RED-GREEN-RED; references 100 %, mutants exact; TL and ML computed and recorded.
6. **`lib/workspace.mjs`, `lib/plan.mjs`, `lib/records.mjs`, `lib/note.mjs`, `explore.mjs`**, RED-GREEN-RED.
7. **$0 rehearsal:** a stub CLI in the real box behind the real broker (stub upstream), workspaces pre-filled with the reference
   trees, one pair on the masks: launch, collect, classify, score, record and note run end to end; `leafProblems` is `[]`.
8. **Pair 1** after the quiet check; the hand check of §4; the prompt and env read back from one cell per stack.
9. **Pairs 2 and 3**; voids re-run once.
10. **The note**, the leak check, the commit; `prune`; this plan promoted with its deviations.

Code is written by Sonnet coders from Opus briefs and reviewed by Opus (`common.subagent-models`); the coai code round runs
before the merge.

## 11. Test plan

| Id | Guarantee |
|---|---|
| T-X1 | A workspace holds exactly the frozen files of R13 per stack; `STATEMENT.md` is the spec's bytes plus the stack sentence; the samples equal the spec's §6 table; `runner/GRAMMAR.md` is FORMAT v2's sections 3–5 byte for byte; the pid `run.sh` starts is `dotnet` or `node`, not `bash` (D38) |
| T-X2 | The session plan: `allow` empty, $50 / 75 min / 45 min, the registered prompt bytes, `opus`, effort `high`, the 2.1.310 binary; `over` refuses any key outside v1's seams |
| T-X3 | W1–W4: v1's defaults byte-identical (env, argv, records of a v1 fixture); the explore values reach the box |
| T-X4 | Inside both boxes, both stacks: `LANG`/`LC_ALL` `de_DE.UTF-8`, .NET `CurrentCulture` `de-DE`, Node `Intl` locale `de…` (v2 T-L32's shape) |
| T-X5 | The judge, both modes: a trailing space, a missing line feed, `\r\n`, a non-zero exit, a timeout (and the scope is gone after it), an over-limit allocation (`oom_kill`) each fail with their class; stderr is ignored; a failed build fails every case; output past the cap is cut and fails; the visible mode prints FORMAT v2's `PASS`/`FAIL` lines and the workspace copy is byte-identical to the harness's |
| T-X6 | Calibration: both references 100 %; each mutant fails exactly its predicted cases (the sets are read from the private files at test time; absent `~/rsd/lang-v2/explore/heldout/` is `hostLacks`, a failure under `BENCH_REQUIRE_SANDBOX=1`) |
| T-X7 | Records: every v1 metric present (D34 — the four cost views, T1–T5, tokens with the 5m/1h split, wall, `model_api`, `active − model_api` and every partition category, visible and held-out counts, `percent`, `defects`, class, flags, charges) beside the explore fields; removing one fails the test |
| T-X8 | CPC: Σ cost / accepted; 0 accepted gives `no correct solution` with its spend; a `killed: cap` session counts in the numerator |
| T-X9 | The note: the banner, every §5 row against its observation, every §8 difference; no case id, R-id breakdown, input or expected value (a fixture's needles are asserted absent); trap hits as counts |
| T-X10 | The barrier: pair k+1 refused until pair k is recorded; one re-run per void |
| T-X11 | The $0 rehearsal end to end (step 7) |
| T-X12 | A fresh workspace per case: a stub submission whose `run.sh` writes a marker into its directory during case 1, and answers differently when the marker exists, gets the same verdict on case 2 as when case 2 runs alone (watched red with one shared copy) |
| T-X13 | The kill path with a unit that never started: `systemctl --user kill` answering "not loaded" / "not found" is no harness failure, the residual-process check passes; a unit with a process left is a failure |
| T-X14 | Calibration: a reference case over the 30 s ceiling, or a failing one, stops the pilot before any paid launch; the effective TL is the larger of 2 s and 3 × the slowest reference wall, and the run's inputs record it |

Every test runs through v1's `run-tests.mjs`; Python is driven from `node:test` with a temp directory from v1's helper, so
the judge creates no temp path of its own.

## 12. Definition of Done

- [x] The coai plan round passed (PROCEED on `f858222`); the ledger opened at that approval.
- [ ] W1–W4 merged with their tests; v1's suite green with 0 skipped under `BENCH_REQUIRE_SANDBOX=1`.
- [ ] Network was used only by `prepare`; every session's egress log shows no allowed host.
- [ ] The scorer was validated (references, mutants, the hand check) before any rate was reported.
- [ ] Three pairs ran (voids re-run once); every session and pair has a record with every v1 metric and the explore fields.
- [ ] `research/lang-v2/EXPLORE_task1_<run date>.md` committed, generated from the records: per session and per pair the four
      cost views, tokens, the partition, build/test time, pass rates, trap hits as counts; against v2 §4.1, v1's per-session
      costs and the $50 cap; voids, contamination false positives (each hand-judged), timeouts; the banner; names the subject
      shas, the harness by path and identity, the date and the pins (`common.measurement` rule 6). Indexed in `research/README.md`.
- [ ] The private detail (trap hits by case, the agents' failing outputs) is in `~/rsd/lang-v2/explore/<series>/private/`.
- [ ] The leak check is clean over the repository and the branch range.
- [ ] `prune` ran; the growth table's sizes were checked against the disk.
- [ ] The seams are in `research/module_minigit_bench.md`; no approved plan's text was edited.
- [ ] This plan is promoted with its deviations; `plan-lifecycle.mjs` is clean.

## 13. Open questions (the owner's)

- **Q-1** — does the account's data-use setting exclude the pilot's conversations from training? (Risk 1.) Not blocking: v1's
  paid sessions ran under the same setting.
- **Q-2** — closed 2026-10-31: TypeScript 6.0.3 (D-X3, D-X4).

## 14. Brief for the Sonnet coder

Self-contained: everything you need is here or at a path named here. Written by the team lead (Opus) from the approved plan;
where this brief and §1–§13 differ, §1–§13 win and you stop and ask.

**Before any edit.** Worktree `/home/jinx/git/wt/benchmark-explore-t1`, branch `feat/lang-explore-task1`. Read `AGENTS.md`,
`.agents/conventions/ENTRY.md`, `.agents/PROJECT.md`; run `node .agents/conventions/tools/rules.mjs check --repo .` and
`… explain --repo . --task implement --task test --file <each file below>`, then read every selected rule. Read
`research/module_minigit_bench.md` §5–§6 (the box, the broker, the runner). Style: plain ESM `.mjs` on Node 26, no dependency
and no `package.json`, as v1; every function ≤ 50 lines and complexity ≤ 4; failures as values (`ok` / `fail` of v1's
`lib/result.mjs`); an unknown is `{ known: false, why }`, never 0; external processes as exe + argv with a timeout that kills
the whole tree (v1's `runBounded`, `spawnTracked`), never a shell string. Tests: `node:test`, run only through
`node spikes/minigit-bench/run-tests.mjs ../lang-explore/test/*.test.mjs` (and v1's own suite with
`BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs`); temp folders only through v1's `test/helpers.mjs` `tmpDir`;
a test that drives the runner imports v1's `test/fake-home.mjs` first. **RED-GREEN-RED** for every new behaviour: write the
test, watch it fail for the real reason, make it pass, then break the code once more (revert the line) and watch the same test
fail, restore; report both observations.

**The ten steps, and your files.** You do steps 1–7 and the code of step 10; the team lead does 8–10's runs.

| Step | You do | Files (all paths from the repository root unless `~`) |
|---|---|---|
| 1 | Write the pins file; run the probes and record their output: the scope probe on the real path (`systemd-run --user --scope -p MemoryMax=… -p MemorySwapMax=0` → `sh` wrapper → v1's scoring `bwrap` → `bash run.sh`, then `memory.peak` and `memory.events` read inside the scope after the program ends), the locale probe in both boxes (T-X4), `python3` sha256 | `spikes/lang-explore/inputs/explore.json`: CLI 2.1.310 path and sha256 `b73676430af12f97316a798b8371f81d640f90a58bdcb8f3ce56a835bb47df56`; node 26.12.1 dir; SDK 10.0.114; `typescript` 6.0.3 (integrity filled by step 3); `/usr/bin/python3` sha256 `52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41`; spec 01 `d622768` sha256 `d890bb3f…706742e`, FORMAT v2 `1531de8` sha256 `5b476946…f8c3d9c` (full values in §10 step 1), read from `/home/jinx/git/dew_flow_ai_language_comparison` with `git show <sha>:<path>`; the private notes' sha256 `0eb311642c70460d07fa5d32e57a741decbe61cd8f99ba0e4705831e77d8e89e`; caps $50 / 75 min / 45 min; TL floor 2 s, ML floor 512 MB, calibration ceiling 30 s |
| 2 | The four v1 seams (below) | `spikes/minigit-bench/lib/{bare-phase,sandbox,bwrap,bare-record}.mjs`; tests `spikes/minigit-bench/test/explore-seams.test.mjs`; extend `spikes/minigit-bench/test/no-shell.test.mjs` to scan `spikes/lang-explore/` too |
| 3 | `explore.mjs prepare` (below) | `spikes/lang-explore/explore.mjs`, `lib/prepare.mjs`, `inputs/cache-recipe.json`, `inputs/cache-snapshots.json` |
| 4 | The held-out side (below) | `~/rsd/lang-v2/explore/heldout/` only |
| 5 | The judge and its Node side; `explore.mjs calibrate` | `spikes/lang-explore/runner/run_samples.py`, `lib/score.mjs`, `lib/calibrate.mjs`; tests `test/judge.test.mjs`, `test/calibrate.test.mjs` |
| 6 | Workspace, session plan, records, CPC, note, the CLI | `templates/csharp/` (`build.sh`, `run.sh` ending in `exec dotnet out/App.dll`, `global.json`, `nuget.config`, `src/App.csproj`, `PACKAGES.md`), `templates/node/` (`build.sh` = `tsc --noEmit`, `run.sh` = `exec node src/main.ts`, `package.json`, `tsconfig.json`, `.npmrc`, `PACKAGES.md`), `inputs/prompts.json`, `lib/workspace.mjs`, `lib/plan.mjs`, `lib/records.mjs`, `lib/note.mjs`, `explore.mjs` (`run --pair k [--rerun <stack>]`, `status`, `note`, `prune`); tests `test/{workspace,plan,records,note,barrier}.test.mjs` |
| 7 | `explore.mjs rehearse` (below), and run it | `lib/rehearse.mjs`; test `test/rehearse.test.mjs` |
| 8–9 | — (the team lead: quiet check, pairs 1–3) | — |
| 10 | — (the team lead: the note, `leakcheck`, `prune`, the promotion) | — |

**The four v1 seams — each default byte-identical, each RED-GREEN-RED.** Before the change, capture today's outputs as the
regression fixture (a test that compares JSON, not your memory).
- **W1** `lib/bare-phase.mjs`: `ctx.fill` and `ctx.planFor`, optional; when absent, `fillFor` (`:32`) and `sessionPlan`
  (`:75`) run exactly as today. Tests: with neither, a v1 phase fills SPEC-v1.txt and gets the minigit prompt (unchanged); with
  both, the given functions are called once per item and their results used.
- **W2** `lib/sandbox.mjs` `agentEnv({ …, locale })`: absent → the object is byte-identical to today's (`LANG: 'C.UTF-8'`, no
  `LC_ALL`); given → `LANG` and `LC_ALL` are that value; `extra` still may not replace either (the clash rule). `AGENT_ENV_KEYS`
  gains `LC_ALL` only if its own test demands the list match; say which.
- **W3** `lib/bwrap.mjs` `scoreEnv` / `scoreBoxArgv({ …, locale })`: the same rule; the default argv is byte-identical.
- **W4** `lib/bare-record.mjs`: export `sessionMeasure` unchanged; `cellOf`'s results identical on a fixture.
Then v1's whole suite: 0 failed, 0 skipped, under `BENCH_REQUIRE_SANDBOX=1`.

**What `prepare` downloads — the only networked step.** `typescript@6.0.3` from `registry.npmjs.org`: read `npm view
typescript@6.0.3 dist.integrity` and record it in `inputs/explore.json`; install it read-only at
`~/rsd/lang-v2/explore/toolchains/typescript-6.0.3/` through v1's `preflight({ stacks, install: true })` with the pilot's
`stacks` object, and check the installed tarball's integrity equals the recorded one (do NOT use the copy in `~/.npm`). Then
the Node cache snapshot with v1's `makeSnapshot` and the pilot's recipe (`node --version` = `v26.12.1`; `npm init -y`;
`npm install --no-audit --no-fund typescript@6.0.3 @types/node@{TYPES_NODE}`, `TYPES_NODE` = highest `@types/node@26`), in a box
behind the proxy with the registry list, read-only under `~/rsd/lang-v2/explore/toolchains/`, its tree hash registered in
`inputs/cache-snapshots.json`. The C# part is v1's registered `cache-csharp-0a5733e2d4f1`, re-hashed. Then the spin calibration.
Nothing else reaches the network, ever: sessions run with `allow: []`.

**The held-out side — private, never in git.** Read `~/rsd/lang-v2/test-design/01.md` §1–§2. Write under
`~/rsd/lang-v2/explore/heldout/` (directories mode 0700, files 0600): `gen.py` (Python stdlib) that writes `cases/<id>.in`,
`cases/<id>.out` and `cases/cases.txt` (`<id> <R-ids> byte-exact`, every case at least one R-id of spec 01) and asserts the
notes' fact table; `refs/csharp/` and `refs/node/` in workspace shape; `mutants/<id>/` as the notes list them; `manifest.json`
with the case count, each mutant's expected failing set and every file's sha256. No case value, mutant, trap name or
reference code goes into git, a test fixture in git, a commit message, a PR body, a prompt or a workspace; tests in git read
these files at run time and are `hostLacks` when they are absent.

**The judge.** `runner/run_samples.py`, Python stdlib, self-contained (`-I` imports nothing beside it), two modes.
*Visible* (the copy in every workspace, byte-identical): `python3 -I runner/run_samples.py samples/` builds with
`bash build.sh`, runs every pair, prints one `PASS`/`FAIL` line per case (FORMAT v2 §3.1, §5.6), time against the 1 s floor,
no memory. *Held-out* (the harness, on the host, handed by `lib/score.mjs`: the pristine copy, the cases dir, the scoring-box
argv with W3, the limits): build once (600 s) in a copy, make it read-only; **per case, a fresh copy of that built tree**,
removed after; the run in its own scope as step 1 probed; stdin piped from the host; stdout read up to 1 MiB; verdict `pass` /
`wrong answer` / `time limit` / `memory limit` / `runtime error`; per case wall, certified peak, `memory.events`, exit code,
stderr tail; one JSON result file. **Kill path:** on timeout `systemctl --user kill --signal=KILL <unit>`; "not loaded" / "not
found" (exit 5, or 1 with that message) is not an error; then `systemctl --user is-active --quiet <unit>` and an empty
`cgroup.procs` (or the unit gone); a process left stops the pilot. No temp path of its own: it writes only under the
directories it is given. `lib/score.mjs` maps the result to v1's score shape (`bare-record.mjs:98`) plus the explore fields.
`explore.mjs calibrate`: both references through the held-out mode with a **30 s ceiling per case**; any case over it, or
failing, stops with a sentence; then TL = max(2 s, 3 × slower reference's slowest wall), ML = max(512 MB, 2 × larger peak),
and every mutant must fail exactly its manifest set; the limits go to the run's inputs.

**The $0 rehearsal** (`explore.mjs rehearse`). v1's `stubClaude` as the CLI and `stubUpstream` as the API, in the real box
behind the real broker and proxy, a fake token (the real token file is never read), one C# ∥ TS pair on the masks, each
workspace pre-filled with its reference tree; then collect, classify, score, record and render the note exactly as a paid
pair. Pass: both sessions `completed`, both scored 100 %, `leafProblems` of every record is `[]`, T1–T5 as v1's stub gives
them, and the note renders with its banner. Its ledger line is a $0 `rehearsal`.

**The machine-quiet check** (before every pair; you build it, the team lead runs it). v1's `inputs/quiet.json` thresholds,
`quietWindow` and `waitForQuiet` (`lib/quiet.mjs`), a sampler from `createSampler` (`lib/cpu-sampler.mjs:227`) whose
`liveSessions` lists live sessions under both `~/rsd/minigit` and `~/rsd/lang-v2/explore`, and the spin probe calibrated at
`prepare`. Not quiet: re-check every 60 s for 30 min, printing each verdict, then exit 3; `--accept-noisy <reason>` starts
anyway with the flag `started not quiet`. During a pair the phase sampler writes its file and flags, never kills.

**You must NOT:**
- start any paid session or read the real token file (`~/.config/minigit-bench/claude-oauth-token`) — the team lead starts
  pair 1 after the coai code gate;
- use the network outside `prepare`;
- put any held-out or private material in git (above);
- edit any plan's text (this one included, beyond ticking boxes the team lead asks for);
- `git add -A`, `git add .` or `git commit -a` — add files by name; never push;
- change a v1 default, a v1 registered input under `spikes/minigit-bench/inputs/`, or v1's classifier.
