# PLAN — E3b stories: bare first — the thin bare-arm runner, the machine-quiet check, the bare pre-registration, the bare A/A and the three bare pairs of the minigit stack bench

> Status: **plan only, nothing implemented yet, 2026-10-30.** Scope: the slice of epic E3 of
> [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md) that D20 pulls forward — new modules and one CLI in
> `spikes/minigit-bench/`, one widening in `spikes/policy-ab-quick/lib/spend.mjs`, the bare-scope pre-registration under
> `spikes/minigit-bench/inputs/`, and the paid bare sessions (A/A, one Node pilot, three pairs) run under
> `~/rsd/minigit/<series>/`, outside every git tree. Five stories, E3b.S1–E3b.S5; **no cornerstone mark** — the parent's
> ledger is full (`Cornerstones: 2 of 2 — E1.S3, E2.S2`), see §3. Spend: pilot and data, all inside the parent's $500
> ceiling and this document's $40 bare budget (§5).

## 1. Goal, before any solution

D20 (operator, 2026-10-29): the first C# against TypeScript numbers come from the **bare** arm, in hours, before the
process arm is built. That needs four things E1 and E2.S1–S2 do not yet give: one command that runs a bare **pair** under
the parent's pair, barrier, cap, void and re-run rules and leaves a record a report can be built from; a guard that a pair
does not start on a machine other experiments are loading; the analysis pre-registered and committed **before** the first
paid minigit session; and a paid A/A that proves the instruments on the real task before any data pair. Then the three
bare pairs run.

What this document does **not** build, explicitly: the process arm's blocks and the arm-lead draw (`drawOrder` over 12
cells, parent §4.1), the compliance checker (§5.4), coai, the bridge, GitHub run bases, PATs, P0, the model smoke through
the bridge, P1, the process scope of the pre-registration (`checklist.json`'s 17 items), `report.mjs` and the series report
(E4), the `bare/<rid>` pushes (§7.7, after E2.S4), `retire.mjs` and credential teardown. Those stay in E3, E4 and E2.S3–S4.

**Public repository, networked sessions (parent §5.1).** Nothing here names a held-out case, reference or mutant. This
document never reads `~/.config/minigit-bench/` or `~/rsd/minigit/heldout-src/`.

## 2. What exists — verified at `c4c985e`, 2026-10-30 (E2.S2 is under review: depend on these entry points, not internals)

| Fact | Where |
|---|---|
| `launchSession(plan, deps, { signal })` → `ok({ meta, exit })` or `fail(reason, { exit: 5 })`; `plan` carries `layout, arm, cellId, prompt, capUsd, wallMs, stallMs, claudeBinary, cliVersion, nodeDir, tsDir, toolchains, token, dummy, prices, allow, operatorHome, harnessSources`; defaults `reserveUsd 0.05, tickMs 5000, sampleMs 1000, settleMs 3000, graceMs 5000, arm 'bare'` | `spikes/minigit-bench/lib/session-launch.mjs:223`, `:216` |
| The box is spawned through the injectable `deps.spawnBox(argv, env)` (default `spawn(argv[0], …, { detached: true })`) | `session-launch.mjs:41`, `:141` |
| `meta.json` records `stopped` (`'wall' \| 'cost' \| 'stall' \| 'readback' \| 'cancelled' \| 'disposed'`), `outcome`, `broker.{spentUsd, refused, overCap}`, `envelopeCostUsd`, samplers | `session-launch.mjs:145-150`, `:195-206` |
| `sessionOutcome` labels a session with no envelope `void: stopped` / `void: no result` and any `is_error` envelope with a status `void: api-error <n>` — **its labels are not §11.1's classes**: a watchdog kill is a data point there, not a void | `lib/readback.mjs:43-49` |
| The broker answers its own cap refusal with HTTP **429** and `rate_limit_error` ("broker: the cell's budget … is spent"); `rateLimitSignal` voids any envelope with status 429 or a text matching `rate.?limit` — so a capped session would read as `void: rate limit` | `lib/broker.mjs:76`, `:176`; `spikes/policy-ab-quick/lib/ratelimit.mjs:6`, `:12` |
| `claudeArgv` puts the cap on the CLI too (`--max-budget-usd <cap>`); the broker's cap is soft, over 1.1 × cap is flagged | `lib/launch.mjs:46-51`; `lib/budget.mjs:9-10`, `:70` |
| `sessionLayout(root, rid)`: non-recursive claim, rid `^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$` (**no `.`**, so the parent's `.a<k>` cannot be a directory name), socket path ≤ 108 bytes | `lib/sandbox.mjs:38`, `:64`, `:19` |
| The smoke resolves the CLI by `realpathSync` of `claude` on PATH — an auto-update of the operator's own CLI changes what the next session runs; the preflight then refuses (pin 2.1.310), halting the series | `lib/smoke-run.mjs:61-71` (`:70`); `inputs/stacks.json` |
| The smoke's plan literal and `callsOf` (stream `tool_use` → contamination `calls`) | `lib/smoke-run.mjs:85-90`, `:35` |
| `preflight({ home, nodeDir, tsDir, claudeBinary, stacks, install })`, `retireStale({ root, now, endCell })`, `endCell`, `cellProcesses(cellId, procApi, { inner })` | `lib/preflight.mjs:55`; `lib/session-state.mjs:85`; `lib/janitor.mjs:34`, `:102` |
| `fetchInputs()` → the verified cache `~/.cache/minigit-bench/inputs/<sha>/` holding `upstream/SPEC-v1.txt`, `upstream/SPEC-v2.txt` and the FIXED `test-v1.sh`, `test-v2.sh` (hashes in `inputs/hashes.txt`) | `lib/fetch-inputs.mjs:57`; `inputs/hashes.txt` |
| `score-session.mjs --session --source --arm bare --stack --phase --toolchain… [--nuget-cache] [--npm-cache]`; exit 0 scored · 3 environment, resumable · 4 usage · 5 scorer failed; it builds **offline** in its box | `spikes/minigit-bench/score-session.mjs:1-9`; `lib/score-steps.mjs:77-90`, `:141-147` |
| E1's readers: `envelopeView`, `streamView`, `streamMeterUsd`, `transcriptView`, `otelView`, `brokerView`, `crosscheck` (T1–T5), `sessionCost`, `aliasRecord`/`checkAliases`, `spansFrom`, `partition` | `lib/claude-usage.mjs:37`, `:109`, `:115`, `:147`; `lib/otel-decode.mjs:214`; `lib/crosscheck.mjs:234`; `lib/session-cost.mjs:36`; `lib/aliases.mjs:26`, `:43`; `lib/spans.mjs:179`; `lib/timeline.mjs:68` |
| The detection half: `contaminationHits`, `tokenHits`, `scanSessionDir`, `sessionCanaryTargets`, `plantTargets`/`unplant` | `lib/contamination.mjs`; `lib/token-scan.mjs`; `lib/session-scan.mjs:33`; `lib/canary-plan.mjs:16`; `policy-ab-quick/lib/canaries.mjs:32`, `:43` |
| `spendDecision(spentUsd, stopAfterUsd, capUsd)` takes ONE cap and no live sum (parent §8 and T-G1 extend it) | `spikes/policy-ab-quick/lib/spend.mjs:13-18` |
| `crosscheck-policy.json`: `t1 stop`, `t2 otel-incomplete`, `t3 investigate`, `t5 investigate` — "change only by a dated commit" | `spikes/minigit-bench/inputs/crosscheck-policy.json` |
| The held-out suite's archive hash is a line of `hashes.txt`; `heldout/manifest.json` carries `plaintextSha256`, `n1 29`, `n2 29`, `m 14`; `heldout/calibration.json` is ids only | `spikes/minigit-bench/heldout/` |
| The full §11 prediction is public since `01b7459` (2026-10-29); item 5's measure names and item 6's counts were edited the same day (the S2 review; D19), before any cell | `git log -S'Prediction (to be committed verbatim' -- todo/PLAN_minigit_stack_bench.md` |
| The template branches `template/bare-*` hold `SPEC-v1.txt`, `SPEC-v2.txt` and the UPSTREAM (unfixed) tests — the same files for both stacks | `todo/PLAN_minigit_stack_bench_E2.md:73` |
| `~/.local/share/claude/versions/` holds one version, `2.1.310` (292 MB) — the updater keeps no history to fall back on | `ls`, 2026-10-30 |
| PSI is available (`/proc/pressure/cpu`); 14 CPUs, core n = CPUs 2n, 2n+1 | host, 2026-10-30; parent §4.1 |
| Pilot spend so far: E1.S3 capture ($0.18, `test/fixtures/real/claude-2.1.310-capture/CAPTURE.md`), E2.S2 design run $0.1119485, broker proof $0.018452, bare smoke $0.114845 (`research/module_minigit_bench.md` §5) | as cited |

**The template question, decided.** The thin runner does **not** need E2.S4's template clean-up. A bare workspace is an
empty directory the runner fills (parent §4.2), never a clone of `template/bare-*`, and the bare box reaches no GitHub at all
(D21, D22). The templates' risk was a **v1** session seeing SPEC-v2 and the unfixed tests — the SPECs are identical for
both stacks, so "the other stack's task" is not what they carry. The runner writes exactly the pinned files of the phase
(S1 step 3), and a test proves a v1 workspace holds nothing else. E2.S4 still cleans the templates before the `bare/<rid>`
pushes (§7.7, E4) and the process arm.

## 3. Boundaries (named on both sides, `common.planning-docs`)

| Item | Built here | The other document's part |
|---|---|---|
| Pair start, `taskset` masks, barrier, voids and the one re-run, resume, the spend gate (T-G1), per-session and per-pair records | E3b.S1, for the bare arm | E3.S1 **generalizes** this code for the process arm's blocks and draw — never a second copy |
| The CPU and PSI sampler, the machine-quiet check | E3b.S2 | E3.S1 reuses both for process pairs |
| Pre-registration: statement, analysis contract, void rules, bare checklist, held-out hashes, prediction (bare part), prompts, quiet thresholds | E3b.S3, committed before the first paid minigit session | E3.S3 adds the process scope (17-item `checklist.json`, process prompts) as a dated amendment, before P1 |
| A/A (two bare C# v1) and the bare Node v1 pilot | E3b.S4 | E3.S4 keeps P1, the pilot v2 base and the process draw |
| T3 arming | E3b.S4 decides it for **bare data cells** | E3.S4 re-decides it at P1 for the process arm (more children, Fable) |
| The three bare pairs, the by-hand read-back of one bare cell per stack | E3b.S5 | E4.S1 runs the three process pairs; E4's report covers all twelve cells |
| An interim bare record | E3b.S5 (`research/minigit/bare-pairs-<date>.md`, computed) | E4.S3's report supersedes it and links it |

Order: E2.S1 and E2.S2 merged → E3b.S1, S2 (code, free) → S3 (commit) → S4 (paid pilot) → S5 (paid data) → E2.S3, E2.S4,
E3 (process). Disjoint otherwise. **Cornerstone:** E3's cornerstone AREA is the go/no-go criteria of the pilots; the A/A's
criteria are written here by Opus (S4). A Fable mark would make k = 3 > Q = 2, so it is not marked; the question goes to the
operator (open question in the summary).

## 4. Conventions for every story

E1 §3 and E2 §3 hold unchanged (plain ESM, Node 26, no dependency; values not throws; absent is not zero; secrets never on an
argv; exe + argv, no shell; one kill, one hide list, one secrets reader; `run-tests.mjs`; `BENCH_REQUIRE_SANDBOX=1`; RED
first). Added:

- **The runner never reaches inside E2.S2.** It calls `launchSession`, `sessionLayout`, `preflight`, `retireStale`, the
  E1 readers and `score-session.mjs` (as a process, by its exit-code contract). The two widenings it needs are seams named
  where used (S1 steps 2 and 4); if E2.S2's review changes one of the entry points above, the matching S1 step changes
  with it and nothing else.
- **Every paid launch passes the pre-registration guard** (S1 step 9). No `--force`.
- **No path the model sees names the arm or the stack.** The series root is `~/rsd/minigit/<series-id>/` with
  `series-id = s-<yyyymmdd>-<4 hex>`; session dirs are `<series>/s/<rid>/`. The parent's `.a<k>` lives in records only.
- **Records are computed, never typed** (the `verdicts` pattern): `record.json`, `pairs/p<k>.json`, `AA.md`, the interim
  report.
- **Money:** every figure a stop rule reads is the LARGEST of the CLI's `total_cost_usd`, the published-price recompute
  (`sessionCost`) and the broker's `B` (parent §9 plus D23); a session without an envelope is `B`.

## 5. Spend plan and stop rules

**Expected** (parent prediction 1 and §13; list-price API-equivalent, D16):

| Item | Sessions | Expected | Worst under the caps (1.1 × $6 each, one re-run each) |
|---|---|---|---|
| A/A — two bare C# v1, concurrent | 2 | $1–4 | $26.40 |
| Node pilot — one bare Node v1 | 1 | $0.50–1.50 | $13.20 |
| Three bare pairs — 6 cells × (v1 + v2) | 12 | $5.40–18.00 (TS $0.8–2.5, C# $1.0–3.5 per cell) | $158.40 |
| **Bare-first total** | 15 (+ re-runs) | **≈ $7–24** | $198 — never reached: the bare budget stops first |

Spent before E3b: ≈ $0.43 pilot (§2 last row), seeded into the ledger from `inputs/spend-prior.json` (S3) with each
figure's evidence path.

**Stop rules**, checked by `spendGate` before **every phase start** (a v1 pair, a v2 pair, a re-run, the A/A, the pilot):

1. **Ceiling (parent §13):** settled data + void + pilot spend + the live O sum of running sessions + 1.1 × Σ caps of the
   sessions about to start ≤ **$500**.
2. **Bare budget (this document): bare spend (A/A, pilot, data, voids) + 1.1 × Σ caps ≤ $40.** Reason: at the top of
   prediction 3 the process arm needs ≈ $40 + 6 × $70 ≈ $460 of the $500 (§13); $40 for the bare arm is ≈ 1.7 × its
   expected top and leaves the process arm its parent budget. Hitting it stops the bare arm (exit 2); the operator either
   raises it by a dated amendment or reports the bare arm at the n it reached ("in k of n pairs", §11).
3. A phase admitted at ≤ the limit cannot settle above it by more than the soft-cap overshoot, which the 1.1 factor already
   budgets (D-17a).

Other stops (series halts, `run-map.stopped` set, resume refused until the named fix): T1 breach; alias drift (D12); the real
token counted in any sampler or file (security); two `void: contaminated` in the series (a hole in the box, not a one-off).
Resumable halts (exit 3): `void: rate limit` (the subscription window, D16 — re-running at once would void again), the quiet
wait timing out, a scorer environment failure.

---

## E3b.S1 — the thin bare runner: one command runs one bare PAIR, v1 then v2, barrier, $6 cap, scoring, voids and the one re-run, resume, records (T-G1, T-V1, T-R1 for the bare arm)

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus.

**Goal.** `bare-runner.mjs run --series <dir> --what pair:<k>` runs pair k: C# v1 ∥ TS v1 started together on masks 0-5 and
8-13, both collected and scored, then C# v2 ∥ TS v2 from their own v1 results; voids classified by §11.1 and re-run once by
its schedule; every session's and the pair's record written; a crash at any point resumable. The A/A and the Node pilot are
the same machinery with other session lists.

**Files.**

| Path | Change |
|---|---|
| `spikes/minigit-bench/lib/bare-plan.mjs` | new, pure — `ridOf({ seed, pair, stack, phase, attempt })` (`sha256('<seed>:bare:p<pair>:<stack>:<phase>:a<attempt>')[0:6]`), `pilotRid(rand)`, `maskFor({ seed, pair, stack })`, `promptFor({ stack, phase, prompts })`, `workspaceFiles({ phase, inputsRoot })`, `bareSessionPlan({ … })` (the plan object `launchSession` takes) |
| `spikes/minigit-bench/lib/bare-schedule.mjs` | new, pure — `nextStep(pairState)` → the next phase to run (`{ kind: 'phase' \| 'rerun' \| 'done', sessions: [...], label }`), the barrier, the void/re-run table below |
| `spikes/minigit-bench/lib/bare-outcome.mjs` | new, pure — `bareOutcome({ meta, events, brokerLines, readback, scan, contamination, aliasCheck, interrupted })` → `{ class, data: bool, censored: bool, detail }` (the mapping table below) |
| `spikes/minigit-bench/lib/bare-record.mjs` | new, pure over a session dir's files — `sessionRecord(…)`, `pairRecord(…)`, `cellOf(v1, v2)` |
| `spikes/minigit-bench/lib/run-map.mjs` | new — `readRunMap`, `writeRunMap` (tmp + rename), `transition(map, rid, state)`, `resumePlan(map, dirs)` (pure) |
| `spikes/minigit-bench/lib/workspace.mjs` | new — `fillV1(work, inputsRoot)`, `fillV2(work, v1Work, inputsRoot)` (copy minus `bin/ obj/ node_modules/`), `pinnedInputsChanged(work, inputsRoot)` |
| `spikes/minigit-bench/lib/prereg-guard.mjs` | new — `preregGuard({ repo, files: PREREG_FILES })` (step 9) |
| `spikes/minigit-bench/lib/stream-calls.mjs` | new — `callsOf` moved out of `smoke-run.mjs:35` (which imports it): reuse-first move 2, once E2.S2 has merged |
| `spikes/policy-ab-quick/lib/spend.mjs` | `spendDecision(spent, ceiling, caps, { liveUsd = 0, factor = 1 })` — `caps` a number (today's callers unchanged) or a list (T-G1) |
| `spikes/minigit-bench/bare-runner.mjs` | new CLI — `init`, `run --what aa\|pilot-node\|pair:<k>`, `resume`, `status`, `aa-verdicts`, `report`; SIGINT/SIGTERM → abort |
| `spikes/minigit-bench/test/{bare-plan,bare-schedule,bare-outcome,bare-record,run-map,workspace,prereg-guard,bare-runner}.test.mjs`, `spikes/policy-ab-quick/test/spend.test.mjs` | new |

**The change — what and how.**

1. **Pins.** `init` copies the pinned CLI (`~/.local/share/claude/versions/<stacks.pins.claude>`) to
   `~/rsd/minigit/toolchains/claude-<pin>` (mode 0555, sha256 recorded) and every session runs that copy — never the PATH
   symlink (§2: an auto-update of the operator's CLI would otherwise halt or move the series). `init` writes `run-map.json`
   `{ seriesId, seed (null until S4 commits it), harnessSha (git HEAD), spikeTreeClean, preregCommit, pins, stacksSha256,
   pricesFile, caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 }, bareBudgetUsd: 40, ceilingUsd: 500 }` and seeds
   `ledger.ndjson` from `inputs/spend-prior.json`.
2. **One session = one `launchSession` call** with `bareSessionPlan`: `arm 'bare'`, `capUsd 6` (the broker's budget AND
   the CLI's `--max-budget-usd`), `wallMs` 60 min, `stallMs` 45 min (parent §8), `allow: EGRESS_HOSTS.bare`, the dummy from
   `dummyToken`, the token from `readSecretFile` (into the broker only), toolchains `[tsDir]`. **Seam 1 — the CPU mask:**
   `deps.spawnBox = (argv, env) => spawn('taskset', ['-c', mask, 'nice', '-n', '19', ...argv], { env, stdio, detached: true })`
   (both exec, so the pid stays bwrap's and `endCell` is unchanged; nice 19 = `common.local-cpu-budget`, parent §4.1). The
   mask is recorded in `record.json` and its EFFECT is observed by S2's sampler (`Cpus_allowed_list` of the cell's inner
   processes), never assumed from the argv.
3. **Workspaces.** v1: `box/work` holds exactly `SPEC-v1.txt` (= `upstream/SPEC-v1.txt` of the cache) and `test-v1.sh`
   (the FIXED script), each re-hashed against `hashes.txt` after writing. v2: a copy of the v1 session's `box/work` minus
   `bin/ obj/ node_modules/` (parent §4.2), then `SPEC-v2.txt` and the fixed `test-v2.sh` added; if the v1 tree's
   `SPEC-v1.txt` or `test-v1.sh` differ from the pins, the v2 record carries `v1 inputs changed` (nothing is overwritten:
   upstream's v2 starts from the v1 tree as left). Fresh `CLAUDE_CONFIG_DIR` per session by construction (a new layout).
4. **A pair phase.** Quiet check (S2) → `spendGate` (§5) → canaries planted (`sessionCanaryTargets` with each partner's
   config dir; manifest first) → both layouts claimed → `Promise.all([launch(C#), launch(TS)])` in one tick; `startSkewMs` =
   the difference of the two `meta.startedAt` (flag `start skew` above 5 s). **Seam 2 — the abort:** one `AbortController`
   for the pair; SIGINT/SIGTERM aborts both (`launchSession`'s `signal`). After both close: canaries checked and unplanted.
5. **Scoring, after both end, before any v2 starts (the barrier).** Sequentially, C# then TS, machine quiet-checked (flag
   only): `score-session.mjs --session <s> --source <s>/box/work --arm bare --stack <stack> --phase <phase> --toolchain
   <tsDir> --toolchain <nodeDir> --nuget-cache <s>/box/nuget --npm-cache <s>/box/npm` — the session's OWN caches are the
   offline seed, because they hold exactly what it restored. Exit 3 → retried 3 × 60 s apart, then the runner halts
   resumable (exit 3); a scorer exit 5 halts the series (an instrument failure). The pair is never voided for a scorer fault.
6. **Outcome classes** (`bareOutcome`, precedence top-down; the first match wins — pre-registered in S3):

   | # | Evidence | Class | Data point? |
   |---|---|---|---|
   | 1 | `checkAliases` → stop | `stop: alias drift` (series halts, D12) | — |
   | 2 | the real token counted by a sampler or `scanSessionDir` | `stop: security` | — |
   | 3 | a high `contaminationHits` hit, a canary found, a `tokenHits` hit | `void: contaminated` | no |
   | 4 | `meta.stopped = 'readback'`, or no envelope and no broker `settled` line (the model never answered) | `void: harness` | no |
   | 5 | `meta.stopped = 'cancelled'`, or the session's `state.json` retired by `retireStale` (the harness or host died) | `void: harness` (`interrupted`) | no |
   | 6 | `meta.stopped = 'cost'`, or a broker `refused` line for the cap, or settled spend ≥ cap | `killed: cap` | yes, censored `≥ $6` |
   | 7 | an upstream 429 in the broker log, or `rateLimitSignal` with **no** broker cap refusal | `void: rate limit` | no |
   | 8 | `meta.stopped = 'wall'` / `'stall'` | `killed: wall` / `killed: stall` | yes, censored |
   | 9 | envelope `subtype: success`, `is_error` false | `completed` (flag `model_refusal_fallback` when seen) | yes |
   | 10 | anything else after the model answered (an error envelope, upstream 5xx after the CLI's retries) | `crashed: <subtype \| api-error n>` | yes |

   Row 6 before row 7 is the point: the broker's own cap answer is a 429 shaped `rate_limit_error` (§2), and read by
   `rateLimitSignal` alone a capped session would be voided and paid for twice.
7. **The schedule** (`nextStep`; a "phase" starts its sessions together; re-runs come after the pair's own phases and before
   the next pair; attempt = `a2`, a fresh rid):

   | State after the v1 phase | Next steps, in order |
   |---|---|
   | both data | v2 pair |
   | both void | v1 re-run pair (`a2`, paired) → v2 pair |
   | one void (say C#) | TS v2 alone (`ran unpaired`) → C# v1 `a2` alone (`ran unpaired`) → C# v2 alone (`ran unpaired`) |
   | a void `a2` | that cell `missing`, reported as such; its v2 never runs |
   | one v2 void | that v2 `a2` alone, from the recorded v1 tree (`ran unpaired`) |
   | both v2 void | v2 re-run pair (`a2`) |

   This is the parent's §11.1 read literally: the barrier holds a partner's v2 only when both v1s voided.
8. **Records.** `record.json` per session (pure over its dir): ids (`pair, stack, phase, attempt, rid`, `<cell>-a<k>`),
   outcome class, mask and its observed effect, `startSkewMs`, quiet record, harness sha, prereg commit, CLI version; cost
   (`sessionCost` headline with basis, CLI total, `B`, the stop-rule figure, per model main / children / internal), tokens
   per model and kind, first-turn cache read (warmth flag), T1–T5 verdicts under `crosscheck-policy.json`, alias record;
   wall, API time, active, every partition category, `unattributed`; turns checked against assistant messages, children
   per model and max concurrent (from the agent spans); scorer summary (build ok, visible per script, held-out `percent`,
   `defects`, blocked count, upstream-original, latency median); product and test LOC (non-blank lines; test = path or
   name matching `test|spec`); egress summary; flags. Process-only fields (coai, CI, compliance, CodeRabbit) are
   `n/a (bare)`, never `not captured`. `pairs/p<k>.json`: masks, phase start times and skews, barrier time, unpaired
   labels, the **cell** (v1 + v2) per stack, and C#/TS for cost, wall, API time, active time — a value, or a bound (`≥`/`≤`)
   when a side is censored — with quality per phase side by side.
9. **The pre-registration guard** (`preregGuard`): every `PREREG_FILES` path (S3) is tracked, unmodified in the work tree,
   and its last commit is an ancestor of HEAD **and** reachable from a remote ref (`git branch -r --contains`, a public
   timestamp). `seed.txt` is required for `pair:<k>` only. A refusal names the file. No override exists.
10. **Resume.** `retireStale(<series>/s)` first. Per session in `run-map`: `launching` with `meta.json` → `ran`;
    `launching` retired or failed with paid work → class row 5, scheduled by step 7; `launching` with its dir gone (nothing
    paid: `launchSession` removed it) → `planned` again, same rid; `ran` → score; `scored` → record rebuilt. Refused when
    the harness sha, `stacks.json`, the prices file, the prereg commit or the seed differ from `run-map.json` — a harness
    change needs `--accept-harness <sha> --amendment <date>` naming a dated line of `inputs/amendments.md`, recorded in
    the ledger. A halt from §5's series stops is refused until the operator passes `--after-stop <rule> --reason <text>`;
    the security, T1 and alias stops cannot be passed this way (they need a fix and a new A/A, S4).

**Test scenarios** (names first; stub `claude` = `test/session-launch.test.mjs:59`'s `stubClaude`, a stub upstream behind the
real broker; scoring injected except in the one local end-to-end test).

| Test | Input | Expected |
|---|---|---|
| `Rids_are_distinct_across_pair_stack_phase_and_attempt` (T-R1, bare) | all 3 × 2 × 2 × 2 combinations, one seed | 24 distinct rids; each matches the layout regex; none contains `.` |
| `No_path_the_model_sees_names_the_arm_or_the_stack` | a series id and every rid of a series | `box/work` and `box/config` paths match none of `bare`, `process`, `csharp`, `cs`, `node`, `ts`, `typescript` as a path segment |
| `Masks_swap_between_pairs_and_one_stack_holds_one_twice` | `maskFor` for pairs 1–3, two seeds | within a pair the two masks differ; across pairs they alternate; the seed decides pair 1 |
| `A_v1_workspace_holds_exactly_the_pinned_v1_inputs` | `fillV1` from a fake cache | exactly `SPEC-v1.txt`, `test-v1.sh`; hashes equal `hashes.txt`'s `upstream/SPEC-v1.txt` and `test-v1.sh`; no SPEC-v2, no upstream test |
| `A_v2_workspace_is_the_v1_tree_minus_build_output_plus_v2_inputs` | a v1 tree with `bin/`, `obj/`, `node_modules/`, a nested `src/obj.ts`, an edited `test-v1.sh` | the three dirs absent, `src/obj.ts` present, SPEC-v2 and fixed test-v2 added, `v1 inputs changed` flagged, the edited file kept |
| `The_prompt_is_the_registered_bytes` | `promptFor` × stack × phase against `inputs/prompts-bare.json` | byte-equal; the two stacks' prompts differ only in `{STACK}` and `{STACK_SENTENCE}` |
| `A_broker_cap_refusal_is_a_cap_kill_not_a_rate_limit_void` (RED first: `rateLimitSignal` alone voids it) | an envelope `is_error`, status 429, text with `rate_limit_error`; broker log with a `refused` cap line | `killed: cap`, data, censored; `rateLimitSignal` on the same events says void — the refuted reading, shaped |
| `An_upstream_429_is_a_rate_limit_void` | broker log with an upstream 429, no cap refusal | `void: rate limit` |
| `Each_outcome_row_maps_and_precedence_holds` | one fixture per row 1–10, plus overlaps (contaminated + capped; readback + cancelled) | exactly the row's class; overlaps take the higher row |
| `A_watchdog_kill_is_a_data_point_whatever_sessionOutcome_says` | `meta.stopped: 'wall'`, `outcome.label: 'void: stopped'` | `killed: wall`, data |
| `The_schedule_follows_the_void_table` (T-V1, bare) | every row of step 7's table, and a second void | the listed steps in order with their labels; `missing` after a second void; no v2 after a missing v1 |
| `The_barrier_holds_v2_until_both_v1s_are_scored` | a pair state with one v1 scored, one `ran` | `nextStep` is `score`, never a v2 phase |
| `Spend_decision_takes_several_caps_a_live_sum_and_a_factor` (T-G1) | `spendDecision(10, 40, [6, 6], { liveUsd: 3, factor: 1.1 })`; the same with spent 24; a plain number cap | go (26.2 ≤ 40); stop (40.2 > 40); today's number form unchanged |
| `The_bare_budget_stops_before_the_ceiling` | ledger at $30 bare, $35 total; two sessions to start | stop naming the bare budget, the ceiling line still "within" |
| `The_stop_rule_figure_is_the_largest_basis` | E $1.00, recompute $1.02, B $1.01; then no envelope with B $2.10 | 1.02; 2.10 |
| `A_pair_starts_both_sessions_in_one_tick_and_records_the_skew` | stub launches recording their spawn time | both spawned; `startSkewMs` recorded; > 5 s flagged |
| `An_abort_stops_both_cells_and_leaves_the_run_map_resumable` | abort 1 s into a stub pair | both `meta.stopped = 'cancelled'`; run-map `launching` → resume classes both row 5 → `a2` pair |
| `Resume_after_a_crash_continues_where_the_files_say` | run-maps cut at each transition (planned, launching with/without meta, ran, scored, mid-v2) | `resumePlan` names exactly the remaining steps; no session re-run that has a data outcome |
| `Resume_refuses_a_changed_harness_or_pin` | run-map with another harness sha / stacks sha / seed | refused naming the field; accepted with `--accept-harness` + an existing amendment line |
| `A_scorer_environment_failure_halts_resumably_and_never_voids` | injected scorer exits 3 four times | three retries, exit 3, sessions stay `ran` |
| `The_session_record_has_every_field_or_says_why` | the committed bare-smoke fixture as a session dir, plus a fake score | every §11 per-session field `known`, `not captured` with a reason, or `n/a (bare)`; none blank |
| `A_censored_side_makes_the_pair_ratio_a_bound` | C# `killed: cap` at $6.30, TS completed at $3.00 | cost ratio `≥ 2.1`, labelled a bound |
| `The_guard_refuses_an_uncommitted_or_unpushed_preregistration` | a temp repo: file absent; modified; committed not pushed; pushed | refused ×3 naming the file; ok |
| `A_stub_pair_runs_end_to_end` (local; `BENCH_REQUIRE_SANDBOX=1`) | `bare-runner run --what pair:1` with the stub CLI in real boxes, the real scorer | both phases ran, the barrier held, two `record.json` per phase, `pairs/p1.json`; no socket, no cell process left |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/bare-plan.test.mjs test/bare-schedule.test.mjs
test/bare-outcome.test.mjs test/bare-record.test.mjs test/run-map.test.mjs test/workspace.test.mjs test/prereg-guard.test.mjs
test/bare-runner.test.mjs`; `node .agents/conventions/tools/low-priority.mjs node --test spikes/policy-ab-quick/test/spend.test.mjs`;
`node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what pair:1 --dry-run` (prints the plan, spends nothing).

**Done when.** Every test above green, `A_broker_cap_refusal_…` recorded RED on the bare `rateLimitSignal` reading; the stub
pair end to end with no skip; `spendDecision`'s existing callers unchanged; `callsOf` imported from one module;
`research/module_minigit_bench.md` gains "The bare runner" (entry points, exit codes, the outcome table, the schedule).

---

## E3b.S2 — the machine-quiet check and the CPU sampler: refuse to START a phase on a loaded machine, flag load DURING one

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus.

**The decision: refuse at the start, flag during the run.** Before a phase nothing is spent: waiting for a quiet machine is
free, and a pair started under foreign load is a paid, confounded data point (V7). During a phase, killing on load would
turn a load event into a void that §11.1's closed list does not contain and throw away paid work, so load is **flagged**
and the pair stays in every table, marked, with the language-effect line also given without flagged pairs. A further
reason to refuse: other `claude -p` runs share the subscription's usage window (D16), so they also raise the odds of a
`void: rate limit`.

**Files.** `spikes/minigit-bench/lib/cpu-sampler.mjs` (new; parent §8 names it) — pure `hostBusy(prevStat, stat)`,
`foreignCpu({ procs, cellIds, harnessPids, prev })`, `foreignAgents(procs, patterns)`, `quietVerdict(samples, thresholds)`,
`maskObserved(pid, mask, procApi)`, plus `startSampler({ cellIds, masks, out, intervalMs: 5000, procApi })` (the one I/O);
`spikes/minigit-bench/lib/quiet.mjs` (new) — `waitForQuiet({ thresholds, maxWaitMs, procApi, now, sleep })`;
`spikes/minigit-bench/inputs/quiet.json` (S3 commits it); `test/{cpu-sampler,quiet}.test.mjs`.

**The change — what and how.**

1. **Pre-start check, per phase** (also before the A/A and the pilot): a 60 s window of 5 s samples. **Quiet** = (a) host
   CPU busy (`/proc/stat`, all 14 CPUs, user+nice+system+irq+softirq+steal) mean ≤ **0.5 CPU** and no sample > **1.5 CPU**
   — exact here, because no cell runs yet and the harness's own ticks are subtracted; (b) **zero foreign agent processes**:
   a process whose cmdline is a Claude Code CLI (`argv[0]` basename `claude`, or a path under `claude/versions/`, or
   `cli.js` of `@anthropic-ai/claude-code`) with `-p`/`--print`, or `codex exec`, and whose environ (when readable) holds
   no `BENCH_CELL` of this series — other users' processes count as foreign; (c) `MemAvailable` ≥ 25 % of `MemTotal`;
   (d) no live session of ANY series under `~/rsd/minigit/*/s/` (`stateOf` → `live`). Not quiet → re-check every 60 s
   up to **30 min**, then exit 3 with the last verdict (what failed, the numbers, the foreign pids' cmdlines with any
   token-shaped string masked by `maskTokens`). `--accept-noisy <reason>` starts anyway, flags the phase
   `started not quiet`, and is refused for a v2 phase whose v1 was quiet (the pair would be measured under two regimes).
2. **During a phase** the sampler writes `pairs/p<k>-cpu.ndjson` every 5 s: per-CPU busy, busy per mask group (0-5,
   8-13, 6-7), host total, PSI `some avg10`, foreign agent count, and **foreign CPU = Σ Δ(utime+stime) of processes that
   carry no cell marker and are not the harness** — a LOWER bound (a foreign process born and dead between samples is
   missed; a sustained load is not), labelled so. Flag `foreign load` when the 60 s mean of foreign CPU exceeds **1.0 CPU**
   or any sample sees a foreign agent. PSI is recorded, never a flag: the two cells' own oversubscription raises it.
3. **The mask, observed.** On the first sample after each spawn the sampler reads `Cpus_allowed_list` of every inner cell
   process (`cellProcesses(…, { inner: true })`) and records `maskObserved: true | false`; `false` is an instrument miss the
   A/A gates on (G-11).
4. **Before scoring** the same check runs and only flags (`scored under load`): the held-out step's per-invocation latency
   is load-sensitive.

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `Host_busy_is_computed_from_two_stat_readings` | two `/proc/stat` fixtures | the exact CPU-equivalents; steal included; idle and iowait excluded |
| `A_foreign_claude_print_process_is_found_and_ours_is_not` | proc fixtures: `claude -p` without marker; `claude -p` with this series' marker; interactive `claude`; `node …/cli.js --print`; `codex exec`; another user's `claude -p` (environ unreadable) | foreign: 1st, 4th, 5th, 6th; not: 2nd, 3rd |
| `The_foreign_cmdline_is_masked_in_the_verdict` | a foreign cmdline carrying `sk-ant-…` | the verdict holds `[secret]`, never the value |
| `Quiet_needs_every_condition` (a table) | windows: idle; one 2-CPU sample; a foreign agent; 20 % memory; a live session elsewhere | quiet; not (b-max); not (agent); not (memory); not (live session) |
| `The_wait_retries_then_gives_up_resumably` | a fake clock and a load that never drops; one that drops at 7 min | gives up at 30 min with the verdict (exit 3 path); starts at ≈ 8 min |
| `Noisy_override_is_flagged_and_refused_for_a_v2_after_a_quiet_v1` | `--accept-noisy` on a v1; on a v2 whose v1 was quiet | flagged `started not quiet`; refused |
| `Foreign_cpu_excludes_our_cells_and_the_harness` | proc fixtures: two cells' processes, the harness, one foreign builder | foreign = the builder's delta only, labelled lower bound |
| `The_cell_runs_on_its_mask` (effect; `bwrap` and `taskset` needed) | a box spawned through seam 1 with mask `0` running `grep Cpus_allowed_list /proc/self/status`; control: no prefix | `0`; control: the full list; `maskObserved` true for the first, false against mask `0` for the control |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/cpu-sampler.test.mjs test/quiet.test.mjs`.

**Done when.** The tests green with the effect test not skipped (`BENCH_REQUIRE_SANDBOX=1`); one 60 s pre-start window run on
the series machine with its verdict pasted into the PR (the idle baseline the thresholds are read against); S1's runner
calls `waitForQuiet` before every phase and starts the sampler with every phase.

---

## E3b.S3 — the pre-registration, bare scope, committed and pushed before the first paid minigit session

**Model:** scenarios + brief Opus (this text) · text by Opus (a judge's work: it fixes the analysis) · the extraction
scripts and the guard test by Sonnet · review Opus.

**Goal.** Everything the bare analysis will do is fixed, in the public history, before any bare session of the task is paid
for — so no number can choose its own analysis. Parent §11 first paragraph, cut to the bare arm.

**Files.** The `inputs/` files below; `spikes/minigit-bench/prereg/extract-prediction.mjs` and `prereg/heldout-prereg.mjs`
(new: they write `prediction.md`'s quoted part and `heldout-prereg.json`, so neither is typed); `test/prereg.test.mjs` (new).

**Where it lives.** `spikes/minigit-bench/inputs/` — the paths parent §8 already names for these files. `PREREG_FILES`
(S1 step 9) is exactly this list:

| File | Content |
|---|---|
| `statement.md` | The question of parent §1, part (a) only, word for word, plus: the arms (bare now; process later, D20), the stacks and their sentences, the model and effort, the cap and wall, what is measured (cost with its basis, wall and its partition, held-out `percent` and `defects`), what is NOT claimed (§11's last paragraph, verbatim), and the bare-first deviation (below). |
| `analysis-contract.md` | Copied VERBATIM from the parent at a named commit: §11's paragraphs "Unit of analysis", "Censored and mixed outcomes", "What n = 3 licenses"; §11.1's void list, data-point list and re-run rule; §5.3's acceptance (with `delivered` marked process-only). Then, marked as **bare operationalizations decided before any cell**: S1 step 6's outcome table and its precedence; S1 step 7's schedule; per-phase quality (each session scored per phase; the pair table shows v1 and v2 side by side; prediction 5's medians are taken per phase, two per stack); the cell = v1 + v2 for cost and time, censored if either phase is; S2's quiet rule (refuse at start, flag during, and "with and without flagged pairs"); §5's stop rules and the $40 bare budget; S4's A/A go/no-go table; the T3 arming rule of S4. |
| `checklist.json` | `{ "bare": { "compliance": "not applicable: §5.4 is the process arm's", "acceptance": [build clean, visible 100 % per script, held-out primary 100 % with blocked counted against], "instrumentChecks": [read-back, T1–T5 per the policy, alias check, egress ⊆ EGRESS_HOSTS.bare, real token 0, contamination/canary/token scans, partition Σ = wall, scorer steps all known, mask observed] }, "process": null }` — the process arm's 17 items join by dated amendment in E3.S3. |
| `heldout-prereg.json` | Written by a script from `heldout/manifest.json`, `heldout/suite.enc.sha256`, `inputs/hashes.txt` and `heldout/calibration.json` (never typed): archive sha256, plaintext sha256, N1, N2, M, calibration file sha256. A test re-derives it. |
| `prediction.md` | Parent §11's prediction, bare parts only, extracted VERBATIM by a script from the parent at the commit named in its header (below), elisions marked; then the bare-first note. |
| `prompts-bare.json` | The exact bare v1 and v2 prompt bytes per stack (parent §4.3), `{STACK}` = `C#` and `Typescript` — checked 2026-10-30 against `banteg/minigit-bench@83644e0` `benchmark.rb`: `csharp` has `prompt_name: 'C#'`; `typescript` has none, so `prompt_language_name` falls back to `lang.capitalize` = `Typescript` (banteg's wording is the claim being re-measured, so its labels are used as they are, the odd case included) — and each prompt's sha256. |
| `quiet.json` | S2's thresholds (window, interval, 0.5 / 1.5 CPU, 1.0 CPU during, agent patterns, 25 % memory, 30 min wait). |
| `spend-prior.json` | The paid runs before E3b, each `{ date, what, usd, evidence }` (§2 last row), read from their evidence files. |
| `amendments.md` | A header only: "Dated amendments; instrument only after the A/A (parent §11)". |

`seed.txt` is NOT in this commit: S4 commits it after the A/A's amendments and before pair 1 (parent §4.1's order:
prediction, then seed, then the draw).

**`prediction.md`, the text** (from `todo/PLAN_minigit_stack_bench.md` §11 as of `c4c985e`; the header names that commit and
`01b7459`, where the full prediction first became public; items 5 and 6 were edited on 2026-10-29 before any cell — the
header says so, and git shows it):

> *(The ranges below are descriptive expectations about medians and ratios, stated before the run — not effect sizes
> claimed as findings.)*
>
> 1. Bare cost: C# > TS in ≥2 of 3 pairs; median C#/TS 1.15–1.5×. Basis: banteg's own tokens re-billed give 1.32×, and C#
>    adds a project file, a wrapper and build loops. Per cell (v1+v2): TS $0.8–2.5, C# $1.0–3.5.
> 2. Bare wall: C#/TS 1.3–1.8×; the API-time ratio smaller (1.1–1.4×) — the difference is dotnet tool time.
>
> 5. Quality: every bare session passes the visible tests; bare held-out median `percent` ≥ 90 % in both stacks with at least
>    one bare failure; […process clause — registered with the process scope…]; no stack difference > 1 `defect`.
>
> 7. Surprises named in advance: TS costing ≥ C# in ≥2 bare pairs; […two process clauses — registered with the process scope…].

Items 3, 4 and 6 are process-arm items and are not in this file; they stand in the parent at the named commits.

**What the bare-first deviation weakens** (the note at the end of `prediction.md` and in `statement.md`):

1. **The interaction** (prediction 3, process C#/TS ÷ bare C#/TS) compares windows days apart instead of interleaved blocks:
   server-side model changes, API latency and time of day now differ between the arms. D12 catches an alias resolving to a
   new id; it cannot catch a change behind the same id. The report says the interaction is weaker, as D20 requires.
2. **No arm-lead draw** for the bare cells: order between arms is confounded with time.
3. **T3 is armed for bare cells on bare evidence** (the A/A and the E2.S2 smoke): bare sessions spawn few children, so
   subagent coverage is thinner than the process arm will need; the process arm re-decides at P1.
4. **The process treatment is finalized after bare results are visible** (E2.S4's templates). Parent §6 already fixes it;
   any change after the first bare pair is a dated amendment applied to both stacks, with its reason — never a change
   motivated by one stack's bare result.
5. **The prediction is registered in two parts.** The process part is not in this commit, but it has been public since
   `01b7459` with its two dated edits, all before any cell — so the two-part commit costs no evidence.

The C#/TS comparison **inside** the bare arm keeps its concurrent pairs and loses nothing.

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `The_prediction_is_verbatim_from_the_parent` | `prediction.md`, the parent at the commit its header names | every non-elided fragment, line breaks unwrapped, is a substring of the parent's §11; the header's commit exists |
| `The_analysis_contract_quotes_are_verbatim` | the marked quote blocks of `analysis-contract.md` | each a substring of the parent at the named commit |
| `The_heldout_prereg_is_derived_not_typed` | `heldout-prereg.json` | equal to the script's output from the four source files |
| `The_registered_prompts_differ_only_in_the_stack_slots` | `prompts-bare.json` | per phase, the two stacks' prompts equal after replacing the two slot values; sha256 fields match |
| `The_prereg_names_no_heldout_case` | `heldout-tool.mjs leakcheck --range <base>..HEAD` | clean (needs the key; local) |
| `The_guard_list_is_the_committed_list` | `PREREG_FILES` vs the files in `inputs/` named above | equal |

**Commands.** The two extraction scripts (`node spikes/minigit-bench/prereg/extract-prediction.mjs --parent-commit <sha>`,
`… prereg/heldout-prereg.mjs`), `node spikes/minigit-bench/run-tests.mjs test/prereg.test.mjs`, `node
spikes/minigit-bench/heldout-tool.mjs leakcheck --range origin/main..HEAD`.

**Done when.** The files committed in one commit, its tests green, the leak check clean, the commit pushed to a remote branch
(the guard's condition), its sha in `run-map.json` at `init` — all before the A/A's first launch. The PR says it was
committed before any paid minigit session and names the commit.

---

## E3b.S4 — the bare A/A and the Node pilot: what they must show, what a miss means, what is amended, what stops; the T3 decision; the seed

**Model:** criteria Opus (this text; a cornerstone area, unmarked — §3) · running and the verdict code Sonnet · review Opus.

**Goal.** Before any data pair, two identical bare C# v1 sessions run concurrently on masks 0-5 and 8-13 (`run --what aa`;
parent §15 step 9 picks C# v1), then one bare Node v1 alone (`run --what pilot-node`; parent step 9's "bare Node v1 to
prove the bare scorer"). All pilot spend, inside the $40 bare budget. `aa-verdicts` computes `AA.md` from the records.

**The go/no-go table** (each verdict `holds` / `refuted` / `not exercised`; computed, never typed):

| Id | Must show (both A/A twins, and the Node pilot where marked ‡) | A miss means | Then |
|---|---|---|---|
| G-1 ‡ | read-back ok; init fields as E2.S2 step 6 | the launch differs from the pinned treatment | fix, A/A again |
| G-2 ‡ | T1 | the envelope disagrees with itself: instrument failure | **stop**; fix; A/A again |
| G-3 ‡ | T2 per model and kind | O incomplete | investigate; no data pair until T2 holds on an A/A |
| G-4 | T3 and T4 over matched sources: every main-role O record matched by a transcript message and none unmatched; every subagent-role record matched where present | T's coverage is not established | **T3 rule:** if main coverage holds and subagent coverage holds here OR in the committed E2.S2 smoke fixture (B-T1, same CLI) → `t3: stop` for bare data cells; otherwise the pre-registered fallback `fallback-t2` with `t2: stop` (O and E reconciled per model within 1 %); if O and E cannot be reconciled → no data pair until repaired. The policy change is one dated commit of `crosscheck-policy.json` |
| G-5 ‡ | T5: `B = E` within $0.000001 | the broker prices differently from the CLI | resolved (fixed or explained in `amendments.md`) before pair 1 |
| G-6 ‡ | `partition` Σ = wall to the ms; `unattributed` < 5 % of wall | the classifier or a span source is wrong | fix the reader and re-read the SAME records; the A/A is re-run only if a record was missing |
| G-7 | structural likeness: both twins carry every record kind (stream, envelope, O, T, audit, broker, egress, CPU samples, `score.json` with every step known), the same alias record and model set | an instrument sees one twin and not the other | fix, A/A again |
| G-8 ‡ | isolation: allowed CONNECTs ⊆ `EGRESS_HOSTS.bare`, refused ⊆ `KNOWN_REFUSED` or named by hand; real token 0 in samplers and scan; no high contamination, canary or token hit | a hole in the box | **stop**; fix (E2.S2's owner); A/A again |
| G-9 | the twin spread, RECORDED, not gating: cost, wall, API time and active ratios, held-out `percent` and `defects` differences, CPU per mask | — | shown beside every bare pair ratio in the interim record as the A/A's within-condition spread (n = 1, so a reference, not a threshold) |
| G-10 ‡ | the scorer by hand: in a scratch copy of one twin's tree (and the Node tree), build and the fixed visible script run by hand give the scorer's counts | the bare scorer misreads a real tree | fix the scorer, re-score (no re-run) |
| G-11 ‡ | quiet: the pre-start check passed; the sampler wrote every 5 s; `maskObserved` true for every cell | the contention control is not in force | fix S2; A/A again |
| G-12 ‡ | spend: each session ≤ 1.1 × $6; the ledger holds both basis figures and `B` | the cap failed | **stop**; fix |
| G-13 ‡ | the outcome class is read back by hand against the stream for each session | `bareOutcome` misclassifies | fix S1, re-classify |

A behavioural difference between the twins (one capped, different build paths, different held-out score) is **not** a miss:
it is G-9's data.

**Amended, and how.** After the A/A and the pilot, only the **instrument** may change: harness, scorer, counters, read-back,
sampler, quiet thresholds, the outcome table's evidence reading. Each change is a dated line in `inputs/amendments.md` with its
reason and the G-id that caused it, committed (and pushed) before pair 1; the runner's `--accept-harness` names it. **Frozen**:
prompts, cap, wall, model, effort, tools and allow-list, toolchain pins, egress list, workspace contents. A change to any of
them applies to both stacks and needs another pilot **pair** (bare C# v1 ∥ TS v1), paid from the bare budget (parent §11).

**Stops.** G-2, G-8, G-12 stop the series until fixed and an A/A passes again. A second failed A/A on the same G-id goes to
the operator before a third is paid for.

**Then.** `seed.txt` = `mg-` + 16 random hex, committed and pushed after the amendments and before pair 1 (the guard
requires it for `pair:<k>`); it fixes the data rids and pair 1's mask assignment.

**Test scenarios** (the verdict code, before any paid run).

| Test | Input | Expected |
|---|---|---|
| `Every_G_verdict_is_computed_and_none_is_blank` | synthetic twin records: all good; each G-id broken once | `AA.md` has 13 rows; each broken fixture refutes exactly its G-id |
| `The_T3_rule_arms_only_on_established_coverage` | coverage: main + sub here; main here + sub in the smoke; main only; neither | `stop`; `stop`; `fallback-t2`; `fallback-t2` |
| `A_twin_difference_is_recorded_not_refuted` | one twin capped, one completed | G-9 holds with the ratios; no G refuted by it |
| `Stops_are_named_as_stops` | G-2, G-8, G-12 refuted | `AA.md` verdict line `STOP` naming them |

**Commands.** `node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what aa`, then `--what pilot-node`, then
`… aa-verdicts --series <dir>`; the policy commit; `seed.txt`.

**Done when.** `AA.md` computed with every G holding or its miss handled as the table says; the Node pilot's ‡ rows hold;
`amendments.md` and `crosscheck-policy.json` committed as dated changes; `seed.txt` committed and pushed; the pilot spend in
the ledger.

---

## E3b.S5 — the three bare pairs, the by-hand read-back, and the interim bare record

**Model:** running Sonnet (the operator's go per pair) · the record renderer Sonnet · review Opus.

**Goal.** Pairs 1, 2, 3 run in order under S1–S4, each a separate `run --what pair:<k>`; then a computed interim record of
the bare arm.

**The change — what and how.**

1. Before each pair: the guard, the quiet check, the spend gate (§5). A pair takes ≤ ~2.5 h (two phases ≤ 60 min each plus
   scoring); prompt-cache warmth carries between pairs within the 1-hour TTL (V8) and meets both stacks alike — the
   first-turn cache read is flagged per session.
2. **By-hand read-back, pair 1** (parent §8: one cell per arm and stack): for the C# and the TS v1 session, the operator
   or an Opus judge reads `argv.json`, `readback.json`, the box's bind list and the first stream lines against the pins and
   writes the verdict into `amendments.md` (a dated "read back, matches" line or a fix).
3. **Interim record** `research/minigit/bare-pairs-<date>.md` and `…-data.json`, rendered by `bare-runner.mjs report`
   from the records only: per session and per cell; per stack median and range; the three pair ratios (values or bounds)
   and their median; quality per phase; flags; spend as data, void and pilot lines; the A/A spread beside the ratios (G-9);
   the prediction beside the observation (rule 4); the §11 licensing sentences verbatim and the five weakenings of S3; the
   harness sha, prereg commit, seed, CLI version, pins and machine (rule 6). Held-out results as counts, `percent` and
   `defects` only. Superseded by E4's report, which links it.

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `The_interim_record_is_rendered_from_records_only` | three synthetic pair records, one with a censored side, one flagged `foreign load` | tables match the records; the bound printed as a bound; the language-effect line also without the flagged pair |
| `The_interim_record_names_no_heldout_case` | the rendered file | the leak check clean; only counts, `percent`, `defects` |
| `The_record_carries_the_licensing_and_the_weakenings` | the rendered file | §11's "What n = 3 licenses" paragraph and S3's five weakenings present verbatim |

**Commands.** `node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what pair:1` (then 2, 3; `resume` after any
halt); `… report --series <dir> --out research/minigit/bare-pairs-<date>.md`; `node spikes/minigit-bench/heldout-tool.mjs
leakcheck --range origin/main..HEAD`.

**Done when.** Three pairs ran — or a stop rule halted the arm and the record says which cells are missing and why; every
session has `record.json`, every pair `pairs/p<k>.json`; the pair-1 read-back line is in `amendments.md`; the interim
record committed with its leak check clean; spend within the $40 bare budget and the $500 ceiling (or the stop named).

---

## 6. Build order

E2.S1 → E2.S2 merged (S1's `callsOf` move waits for it) → **E3b.S1 and E3b.S2** (one branch each off
`feat/minigit-bench-e2`, free, stub CLI only) → **E3b.S3** (the commit, pushed) → **E3b.S4** (paid, pilot) → amendments,
T3 policy, `seed.txt` → **E3b.S5** (paid, data) → E2.S3, E2.S4, E3 (process).

## 7. Test plan — ids by story

| Id / tests | Story |
|---|---|
| T-G1, T-V1 and T-R1 for the bare arm; the outcome, schedule, record, run-map, workspace and guard tests; the stub pair end to end | S1 |
| the sampler, quiet and mask-effect tests | S2 |
| the verbatim, derived-hash, prompt and leak tests | S3 |
| the G-verdict and T3-rule tests; `AA.md` live | S4 |
| the interim-record tests; three pairs live | S5 |

## 8. Growth

| Surface | Size at this document's volumes | Retired by / interrupted |
|---|---|---|
| Session dirs `<series>/s/<rid>/` | ≈ 30 MB raw each (parent §14); 15 planned + ≤ 15 re-runs → ≤ 0.9 GB peak | after a v1 is scored AND its v2 workspace copied, `box/work/{bin,obj,node_modules}`, `box/nuget`, `box/npm`, `score/copy`, `score/cache-*` are shed (safe-rm under the series root, by name); the rest kept until E4's `retire.mjs` |
| `ledger.ndjson`, `run-map.json`, `pairs/*.json`, `pairs/*-cpu.ndjson` | KBs; the CPU log ≈ 720 lines per pair-hour | kept with the series; `retire.mjs` |
| `~/rsd/minigit/toolchains/claude-2.1.310` | one binary (292 MB on 2026-10-30) | kept for the series; `retire.mjs` |
| Canary files in the operator's project dirs | one per dir per pair | unplanted after each pair from the manifest; a crash leaves the manifest, and `resume` unplants first |
| `inputs/*` pre-registration, `research/minigit/bare-pairs-*` | KBs, committed | kept (the record) |
| In-flight states | `launching` sessions; `starting/running` `state.json` | `retireStale` at every `run`/`resume`; a session's wall (60 min) and the scorer's limits bound every non-terminal state; the quiet wait is bounded at 30 min |

## 9. Definition of Done

- [ ] E3b.S1–S5 merged or run as their done-whens say; `BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs`
      green with no skip among the tests named here.
- [ ] `A_broker_cap_refusal_is_a_cap_kill_not_a_rate_limit_void` observed RED on the bare `rateLimitSignal` reading, then green.
- [ ] The pre-registration committed and pushed before the A/A's first launch; its commit sha in every session record.
- [ ] `AA.md` computed; every G holds or its miss was handled as S4 says; T3's policy decided by a dated commit.
- [ ] `seed.txt` committed after the amendments and before pair 1.
- [ ] Three bare pairs ran or the stop is named; spend ≤ $40 bare and ≤ $500 total, every table carrying data, void and pilot
      spend apart.
- [ ] `research/module_minigit_bench.md` describes the runner, the quiet check, the outcome table and the schedule; the
      parent's D20 row, E3 and E4 entries link this document; `todo/README.md` lists it (same commit).
- [ ] The summary names models and counts per story (Opus wrote this text; Sonnet coders; Opus reviewers) and says the A/A
      criteria were written by Opus because the cornerstone quota is full.
