# PLAN — E3b stories: bare first — the thin bare-arm runner, the machine-quiet check, the bare pre-registration, the bare A/A and the three bare pairs of the minigit stack bench

> Status: **in progress — S1–S4 implemented 2026-10-30; S5's bare pairs ran 2026-10-30/31 in two series; not yet merged or
> promoted.** Written 2026-10-30 and revised the same day after the coai plan gate and an Opus review. S5: the pairs ran in
> series `s-20261030-bf89` (stopped by its registered rule, `second void: contaminated`, at pair 3) and in
> `s-20261030-b82e` (complete: pairs 1–3). The interim record is
> [`research/minigit/RESULTS_bare_2026-10-30.md`](../research/minigit/RESULTS_bare_2026-10-30.md), built from
> `research/minigit/tools/` (extract, render, replay); it stands in for the planned `bare-pairs-<date>.md` and
> `bare-report.mjs`, which were **not built** (see the S5 deviations). Scope: the slice of epic E3 of [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md) that D20 pulls
> forward — new modules and one CLI in `spikes/minigit-bench/`, three small named changes to E2-owned files (`sandbox.mjs`,
> `egress.mjs`) and one widening of `spikes/policy-ab-quick/lib/spend.mjs`, the bare-scope pre-registration under
> `spikes/minigit-bench/inputs/` with its tag, and the paid bare sessions (A/A, one Node pilot, three pairs) run under
> `~/rsd/minigit/<series>/`, outside every git tree. Five stories, E3b.S1–E3b.S5; **no cornerstone mark** — the parent's
> ledger is full (`Cornerstones: 2 of 2 — E1.S3, E2.S2`), see §3. Spend: inside the parent's $500 ceiling and this
> document's $40 bare budget (§5).

## 1. Goal, before any solution

D20 (operator, 2026-10-29): the first C# against TypeScript numbers come from the **bare** arm, in hours, before the
process arm is built. That needs four things E1 and E2.S1–S2 do not yet give: one command that runs a bare **pair** under
the parent's pair, barrier, cap, void and re-run rules and leaves a record a report can be built from; a guard that a pair
does not start on a machine other experiments are loading; the analysis pre-registered, committed and tagged **before**
the first paid minigit session; and a paid A/A that proves the instruments on the real task before any data pair. Then the
three bare pairs run.

What this document does **not** build, explicitly: the process arm's blocks and its arm-lead draw (`drawOrder` over 12
cells, parent §4.1, with its own seed), the compliance checker (§5.4), coai, the bridge, GitHub run bases, PATs, P0, the
model smoke through the bridge, P1, the process scope of the pre-registration (`checklist.json`'s 17 items), `report.mjs`
and the series report (E4), the `bare/<rid>` pushes (§7.7, after E2.S4), `retire.mjs` and credential teardown.

**Public repository, networked sessions (parent §5.1).** Nothing here names a held-out case, reference or mutant. This
document never reads `~/.config/minigit-bench/` or `~/rsd/minigit/heldout-src/`.

## 2. What exists — verified at `c4c985e`, 2026-10-30 (E2.S2 is under review: depend on these entry points, not internals)

| Fact | Where |
|---|---|
| `launchSession(plan, deps, { signal })` → `ok({ meta, exit })` or `fail(reason, { exit: 5 })`; `plan` carries `layout, arm, cellId, prompt, capUsd, wallMs, stallMs, claudeBinary, cliVersion, nodeDir, tsDir, toolchains, token, dummy, prices, allow, operatorHome, harnessSources`; defaults `tickMs 5000, sampleMs 1000, settleMs 3000, graceMs 5000, arm 'bare'` | `spikes/minigit-bench/lib/session-launch.mjs:223`, `:216` |
| The box is spawned through the injectable `deps.spawnBox(argv, env)` (default `spawn(argv[0], …, { detached: true })`) | `session-launch.mjs:41`, `:141` |
| `meta.json` records `stopped` (`'wall' \| 'cost' \| 'stall' \| 'readback' \| 'cancelled' \| 'disposed'`), `outcome`, `broker.{spentUsd, refused, overCap}`, `envelopeCostUsd`, samplers | `session-launch.mjs:145-150`, `:160`, `:195-206` |
| `sessionOutcome`'s labels (`void: stopped`, `void: api-error <n>`) are **not** §11.1's classes: a watchdog kill is a data point there | `lib/readback.mjs:43-49` |
| The broker answers its cap refusal with HTTP **429** `rate_limit_error`; `rateLimitSignal` voids an envelope with status 429 or text matching `rate.?limit` | `lib/broker.mjs:76`, `:176`; `spikes/policy-ab-quick/lib/ratelimit.mjs:6`, `:12` |
| **E2.S2 review changes this plan assumes** (in review 2026-10-30; seams, not internals): every broker `refused` line carries `reason: 'cap' \| 'path' \| …`; the cost watchdog kills only on SETTLED spend ≥ cap; a request's reservation is max($0.05, the largest settled charge seen in the cell) | `lib/broker.mjs`, `lib/watchdogs.mjs:16-24`, `lib/budget.mjs:9` after that review |
| `claudeArgv` puts the cap on the CLI too (`--max-budget-usd`); the broker's cap is soft, over 1.1 × cap is flagged | `lib/launch.mjs:46-51`; `lib/budget.mjs:10`, `:70` |
| `sessionLayout(root, rid)`: non-recursive claim, rid `^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$` (**no `.`**, so the parent's `.a<k>` is a record field, never a directory name), socket path ≤ 108 bytes | `lib/sandbox.mjs:38`, `:64`, `:19` |
| `agentEnv` sets `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `MSBUILDDISABLENODEREUSE=1`, `UseSharedCompilation=false` for every arm; `AGENT_ENV_KEYS` is the one literal key list; `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, `npm_config_cache` point at `box/home`, `box/nuget`, `box/npm` | `lib/sandbox.mjs:29-36`, `:85-100` (`:96-97`) |
| The egress proxy logs `allowed` / `refused` / `upstream-error` per CONNECT and **counts no bytes** | `lib/egress.mjs:44`, `:51`, `:55` |
| The smoke resolves the CLI by `realpathSync` of `claude` on PATH; `~/.local/share/claude/versions/` holds one version, `2.1.310` (292 MB) — an auto-update of the operator's CLI would change or remove what the next session runs | `lib/smoke-run.mjs:70`; `ls`, 2026-10-30 |
| The smoke's plan literal and `callsOf` (stream `tool_use` → contamination `calls`) | `lib/smoke-run.mjs:85-90`, `:35` |
| `preflight`, `retireStale`, `endCell`, `cellProcesses(cellId, procApi, { inner })` | `lib/preflight.mjs:55`; `lib/session-state.mjs:85`; `lib/janitor.mjs:34`, `:102` |
| `fetchInputs()` → the verified cache `~/.cache/minigit-bench/inputs/<sha>/`: `upstream/SPEC-v1.txt`, `upstream/SPEC-v2.txt`, the FIXED `test-v1.sh`, `test-v2.sh` | `lib/fetch-inputs.mjs:57`; `inputs/hashes.txt` |
| `score-session.mjs`: exit 0 scored · 3 environment, resumable · 4 usage · 5 scorer failed; builds **offline** from the caches it is given; each step bounded by `runBounded` (build 600 s, scripts, 10 s per invocation) but the process as a whole has no limit | `score-session.mjs:1-9`; `lib/score-steps.mjs:77-90`, `:141-147`; `lib/limits.mjs:3` |
| E1's readers: `envelopeView`, `streamView`, `streamMeterUsd`, `transcriptView`, `otelView`, `brokerView`, `crosscheck` (T1–T5), `sessionCost`, `aliasRecord`/`checkAliases`, `spansFrom`, `partition` | `lib/claude-usage.mjs:37`, `:109`, `:115`, `:147`; `lib/otel-decode.mjs:214`; `lib/crosscheck.mjs:234`; `lib/session-cost.mjs:36`; `lib/aliases.mjs:26`, `:43`; `lib/spans.mjs:179`; `lib/timeline.mjs:68` |
| The detection half: `contaminationHits`, `tokenHits`, `maskTokens`, `scanSessionDir`, `sessionCanaryTargets`, `plantTargets`/`unplant` | `lib/contamination.mjs`; `lib/token-scan.mjs`; `lib/session-scan.mjs:33`; `lib/canary-plan.mjs:16`; `policy-ab-quick/lib/canaries.mjs:32`, `:43` |
| `spendDecision(spentUsd, stopAfterUsd, capUsd)` takes ONE cap and no live sum | `spikes/policy-ab-quick/lib/spend.mjs:13-18` |
| `crosscheck-policy.json`: `t1 stop`, `t2 otel-incomplete`, `t3 investigate`, `t5 investigate`, "change only by a dated commit" | `spikes/minigit-bench/inputs/crosscheck-policy.json` |
| Held-out: the archive hash is a line of `hashes.txt`; `heldout/manifest.json` carries `plaintextSha256`, `n1 29`, `n2 29`, `m 14`; `calibration.json` is ids only | `spikes/minigit-bench/heldout/` |
| §11's bare prediction items (1, 2, 5, 7) on `origin/main` (`85c54b7`) are byte-equal to the branch's; only item 6 (process, D19) differs. The full prediction is public since `01b7459` | `git show origin/main:todo/PLAN_minigit_stack_bench.md`, diffed 2026-10-30 |
| banteg's prompt labels: `csharp` has `prompt_name: 'C#'`; `typescript` has none, so the prompt says `lang.capitalize` = `Typescript`; no `extra_prompt` for either | `banteg/minigit-bench@83644e0` `benchmark.rb:41`, `:147`, checked by the team lead 2026-10-30 |
| `template/bare-*` hold `SPEC-v1.txt`, `SPEC-v2.txt` and the UPSTREAM tests — the same files for both stacks | `todo/PLAN_minigit_stack_bench_E2.md:73` |
| PSI available; 14 CPUs, core n = CPUs 2n, 2n+1; 745 GB free on `/home` | host, 2026-10-30; parent §4.1 |
| Spend before E3b, all E1/E2 instrument work: E1.S3 capture $0.18 (`CAPTURE.md`), E2.S2 design $0.1119485, broker proof $0.018452, bare smoke $0.114845 (`research/module_minigit_bench.md` §5) — ≈ $0.43 | as cited |

**The template question, decided.** The thin runner does **not** need E2.S4's template clean-up. A bare workspace is an
empty directory the runner fills (parent §4.2), never a clone of `template/bare-*`, and the bare box reaches no GitHub
(D21, D22). The templates' risk was a **v1** session seeing SPEC-v2 and the unfixed tests; the SPECs are identical for both
stacks. The runner writes exactly the pinned files of the phase (S1 step 4), and a test proves a v1 workspace holds nothing
else. E2.S4 still cleans the templates before the `bare/<rid>` pushes (§7.7) and the process arm.

## 3. Boundaries (named on both sides, `common.planning-docs`)

| Item | Built here | The other document's part |
|---|---|---|
| Pair start, masks, barrier, voids and the one re-run, resume, `spendGate` (T-G1), records | E3b.S1, for the bare arm | E3.S1 **generalizes** this code for the process arm — never a second copy |
| Build servers left at their defaults in the bare box (moving three keys out of `agentEnv`'s fixed list) | E3b.S1 seam 2, a small change to E2.S2's `sandbox.mjs` | parent §8 keeps them off for the process arm until E3 decides |
| Per-tunnel byte counts in the egress log | E3b.S1 seam 3, a small change to E2.S2's `egress.mjs` | E3 reuses it |
| One hashed cache snapshot per stack (parent §7.1) | E3b.S1 step 2, made before S3 | E3 makes its own for the process templates |
| The CPU sampler, spin probe and machine-quiet check | E3b.S2 | E3.S1 reuses them |
| Pre-registration, bare scope, tag `prereg/minigit-bare-<date>`; seed as commit-reveal | E3b.S3, S4 | E3.S3 adds the process scope as a dated amendment; the process draw gets its own seed (parent §4.1) |
| Registered §11.1 extensions (`void: upstream`, `void: harness` interrupted/disposed, `stop: auth`) | E3b.S3's analysis contract | parent §11.1 names them; E3.S3 adopts or declines them for the process arm |
| A/A, Node pilot, T3 arming for bare data cells | E3b.S4 | E3.S4 keeps P1 and re-decides T3 for the process arm |
| The three bare pairs, their read-back, the interim bare record | E3b.S5 | E4 runs the process pairs; E4's report covers all twelve cells and links the interim record |

Order: E2.S1 and E2.S2 (with the review changes of §2) merged → E3b.S1, S2 (code, free) → cache snapshots made → S3
(commit, tag, push) → S4 (paid pilot) → S5 (paid data) → E2.S3, E2.S4, E3. **Cornerstone:** E3's cornerstone AREA is the
pilots' go/no-go; the A/A criteria are written here by Opus. A Fable mark would make k = 3 > Q = 2; the question is the
operator's.

## 4. Conventions for every story

E1 §3 and E2 §3 hold unchanged (plain ESM, Node 26, no dependency; values not throws; absent is not zero; secrets never on an
argv; exe + argv, no shell; one kill, one hide list, one secrets reader; `run-tests.mjs`; `BENCH_REQUIRE_SANDBOX=1`; RED
first). Added:

- **The runner calls E2.S2 only through its entry points** (§2) and the three named seams; nothing else of E2's changes.
- **Every paid launch passes the pre-registration guard** (S1 step 10). No override.
- **Harness identity** = sha256 over the git tree ids (`git rev-parse HEAD:<dir>`) of `spikes/minigit-bench/{lib,harness,
  capture,prereg}`, the spike's top-level `*.mjs`, `spikes/policy-ab-quick/{lib,hooks}` — never HEAD, so committing an
  amendment or the seed does not change it; a code change does.
- **The runner runs on the harness core**: `bare-runner.mjs` re-execs itself under `taskset -c 6,7` (an env marker stops a
  loop), so its broker, receiver, proxy and sampler sit on core 3 (parent §4.1).
- **No path the model sees names the arm or the stack.** Series root `~/rsd/minigit/<series-id>/`, `series-id =
  s-<yyyymmdd>-<4 hex>`; session dirs `<series>/s/<rid>/`. `a<k>` lives in records only.
- **Records are computed, never typed**; a by-hand verdict is a dated line of `inputs/amendments.md` that code reads.
- **Money:** the registered **primary cost view is the headline recompute** (`sessionCost`, published prices); the CLI
  total and `B` stand beside it. Every figure a STOP RULE reads is the largest of the three; a session without an envelope
  is `B`.

## 5. Spend plan and stop rules

| Item | Sessions | Expected | Worst under the caps (1.1 × $6 each, one re-run each) |
|---|---|---|---|
| A/A — two bare C# v1, concurrent | 2 | $1–4 | $26.40 |
| Node pilot — one bare Node v1 | 1 | $0.50–1.50 | $13.20 |
| Three bare pairs — 6 cells × (v1 + v2) | 12 | $5.40–18.00 (prediction 1: TS $0.8–2.5, C# $1.0–3.5 per cell) | $158.40 |
| **Bare-first total** | 15 (+ re-runs) | **≈ $7–24** | $198 — never reached: the bare budget stops first |

The ≈ $0.43 spent before E3b is E1/E2 instrument work: it counts in the $500 ceiling and **not** in the $40 bare budget. The
v2 rehearsal (S4) and every stub run are free.

**Stop rules**, checked by `spendGate` (`lib/spend-gate.mjs`) before every phase start (A/A, pilot, v1 pair, v2 pair,
re-run):

1. **Ceiling (parent §13):** settled data + void + pilot spend + the live O sum of running sessions + 1.1 × Σ caps of the
   sessions about to start ≤ **$500**.
2. **Bare budget: bare spend (A/A, pilot, data, voids) + 1.1 × Σ caps ≤ $40** — ≈ 1.7 × the expected top. Registered: it may
   be raised **only** to finish the re-runs of a pair already started, and only by the worst case of those re-runs (n ×
   1.1 × $6), as a dated amendment; otherwise hitting it ends the bare arm at the n reached ("in k of n pairs", §11).
   **The squeeze it causes, stated:** if the bare arm uses its whole $40 and process cells cost prediction 3's top ($70),
   the 1.1 × gate refuses the v2 phase of the third process pair (≈ $430 settled + 1.1 × 2 × $35 > $500), and the process
   arm reports at n = 2.
3. A phase admitted within a limit settles above it by at most the soft-cap overshoot, which the 1.1 factor budgets
   (D-17a). An interrupted session's spend is its settled `B` plus every forwarded-but-unsettled request at its reservation.

**Series stops** (`run-map.stopped` set; resume refused until the named fix and, for the first four, a new A/A): T1 breach;
alias drift (D12); `stop: security` (the real token counted anywhere); `stop: auth` (a 401/403 — the credential, not the
stack); two `void: contaminated` in the series. **Resumable halts** (exit 3): `void: rate limit` (the subscription window,
D16), the quiet wait timing out, a scorer environment failure or `scorer-timeout`.

**Pairs 1–3 run unless a named stop fires** (registered). The operator's go per pair chooses WHEN, never WHETHER.

---

## E3b.S1 — the thin bare runner: one command runs one bare PAIR, v1 then v2, barrier, $6 cap, scoring, voids and the one re-run, resume, records (T-G1, T-V1, T-R1 for the bare arm)

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus.

**Goal.** `bare-runner.mjs run --series <dir> --what pair:<k>` runs pair k: C# v1 ∥ TS v1 started together on their
masks, both collected and scored, then C# v2 ∥ TS v2 from their own v1 trees; outcomes classified by how each session
ENDED; voids re-run once by the schedule; every session's and the pair's record written; resumable from any crash. The A/A
and the Node pilot are the same machinery with other session lists.

**Files.**

| Path | Change |
|---|---|
| `spikes/minigit-bench/lib/bare-plan.mjs` | new, pure — `ridOf({ seed, pair, stack, phase, attempt })` (`sha256('<seed>:bare:p<pair>:<stack>:<phase>:a<attempt>')[0:6]`), `pilotRid(rand)`, `maskFor({ seed, pair, stack })`, `firstStack({ seed, pair, phase })` (launch and scoring order), `promptFor(…)`, `workspaceFiles(…)`, `bareSessionPlan(…)` |
| `spikes/minigit-bench/lib/bare-schedule.mjs` | new, pure — `nextStep(pairState)`: the barrier and the void/re-run table (step 8) |
| `spikes/minigit-bench/lib/bare-outcome.mjs` | new, pure — `bareOutcome({ meta, events, brokerLines, readback, scan, contamination, aliasCheck, interrupted, handVerdicts })` → `{ class, data, censoredAtUsd, flags, detail }` (step 7) |
| `spikes/minigit-bench/lib/bare-record.mjs` | new, pure — `sessionRecord(…)`, `pairRecord(…)`, `cellOf(v1, v2)` |
| `spikes/minigit-bench/lib/packages.mjs` | new, pure over a dir listing — `packagesOf({ npmCache, nugetDir, snapshot, flags })` (M10) |
| `spikes/minigit-bench/lib/cache-snapshot.mjs` | new — `makeSnapshot({ stack, recipe, out })` (runs inside an agent box through the proxy), `treeHash(dir)`, `seedCaches(layout, snapshots)` |
| `spikes/minigit-bench/lib/run-map.mjs` | new — `readRunMap`, `writeRunMap` (tmp + rename), `transition`, pure `resumePlan(map, dirs)` |
| `spikes/minigit-bench/lib/workspace.mjs` | new — `fillV1`, `fillV2` (copy minus `bin/ obj/ node_modules/`), `pinnedInputsChanged` |
| `spikes/minigit-bench/lib/prereg-guard.mjs` | new — `preregGuard({ repo, runMap, files: PREREG_FILES, paid })` (step 10), `harnessIdentity(repo)` |
| `spikes/minigit-bench/lib/spend-gate.mjs` | new, pure — `spendGate({ ledger, liveUsd, caps, ceilingUsd, bareBudgetUsd })`: two `spendDecision` calls |
| `spikes/minigit-bench/lib/score-run.mjs` | new — `scoreSession({ session, stack, phase, … , outerTimeoutMs })` → `{ kind: 'scored' \| 'environment' \| 'failed' \| 'scorer-timeout' }` |
| `spikes/minigit-bench/lib/stream-calls.mjs` | new — `callsOf` moved out of `smoke-run.mjs:35` (which imports it), after E2.S2 merges |
| `spikes/minigit-bench/lib/sandbox.mjs` (E2-owned, seam 2) | the three build-server keys leave `agentEnv`'s base and `AGENT_ENV_KEYS` and become an exported `BUILD_SERVERS_OFF` a caller may pass through `extra` |
| `spikes/minigit-bench/lib/egress.mjs` (E2-owned, seam 3) | on tunnel close, one log line `{ verdict: 'closed', host, bytesUp, bytesDown }`; `egressSummary` sums bytes per host |
| `spikes/policy-ab-quick/lib/spend.mjs` | `spendDecision(spent, ceiling, caps, { liveUsd = 0, factor = 1 })`; a number `caps` keeps today's behaviour |
| `spikes/minigit-bench/bare-runner.mjs` | new CLI — `init`, `snapshot --stack`, `seed-commit`, `seed-reveal`, `run --what aa\|pilot-node\|rehearse-v2\|pair:<k>`, `resume`, `status`, `aa-verdicts`, `report`; `--dry-run` on `init` and `run`; SIGINT/SIGTERM → abort with the signal recorded. Exit 0 done · 2 a series stop · 3 resumable halt · 4 usage · 5 runner failure · 130/143 cancelled |
| `spikes/minigit-bench/test/{bare-plan,bare-schedule,bare-outcome,bare-record,packages,cache-snapshot,run-map,workspace,prereg-guard,spend-gate,score-run,bare-runner,build-servers}.test.mjs`; `spikes/policy-ab-quick/test/spend.test.mjs`; additions to `test/egress.test.mjs`, `test/agent-box.test.mjs` | new / extended |

**The change — what and how.**

1. **`init`** copies the pinned CLI (`~/.local/share/claude/versions/<pin>`) to `~/rsd/minigit/toolchains/claude-<pin>`
   (mode 0555, sha256 recorded) — every session runs that copy, never the PATH symlink. It writes `run-map.json` `{ seriesId,
   seedSha256 (from inputs/seed.sha256), harnessIdentity, preregTag, preregCommit, pins, stacksSha256, pricesFile,
   snapshots, caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 }, bareBudgetUsd: 40, ceilingUsd: 500, spin }`, seeds
   `ledger.ndjson` from `inputs/spend-prior.json` (kind `prior`: in the $500, not in the $40), and calibrates S2's spin
   probe. `--dry-run` prints all of it and writes nothing.
2. **Cache snapshots (parent §7.1).** `snapshot --stack csharp` and `--stack node` run ONCE, before S3, each inside an agent
   box through the proxy with `EGRESS_HOSTS.bare`, from the registered `inputs/cache-recipe.json`: C# — `dotnet new console`
   then `dotnet build` with SDK 10.0.113, capturing `NUGET_PACKAGES` and the dotnet first-run state under
   `DOTNET_CLI_HOME`; Node — `npm install typescript@7.0.2 @types/node@<the 26.x version read at snapshot time, recorded>`,
   capturing the npm cache. Each lands read-only in `~/rsd/minigit/toolchains/cache-<stack>-<hash12>/`; `treeHash` (sha256
   over sorted relative path, mode and content hash) goes into `inputs/cache-snapshots.json` (pre-registered, S3). Before
   every launch `seedCaches` copies **both** snapshots into the session (`box/nuget`, `box/npm`, `box/home/.dotnet`), so
   the box is identical across stacks except the prompt; the copy is re-hashed and must equal the registered hash.
3. **One session = one `launchSession` call** with `bareSessionPlan`: `arm 'bare'`, `capUsd 6` (the broker's budget and the
   CLI's `--max-budget-usd`), `wallMs` 60 min, `stallMs` 45 min (parent §8), `allow: EGRESS_HOSTS.bare`, the dummy, the
   token from `readSecretFile` (broker only), toolchains `[tsDir]`. Three seams:
   - **Seam 1 — mask and spawn stamp:** `deps.spawnBox = (argv, env) => { stamp(stack); return spawn('taskset', ['-c',
     mask, 'nice', '-n', '19', ...argv], { env, stdio, detached: true }); }` (both exec, so the pid stays bwrap's; nice 19 =
     `common.local-cpu-budget`). The mask's EFFECT is observed by S2 (`maskObserved`).
   - **Seam 2 — build servers at their defaults (treatment, registered).** banteg's cells ran with the .NET defaults (build
     servers and node reuse on). The parent's reason to switch them off — no cell builds on its partner's node — is already
     met by the box's own pid and ipc namespaces, so the bare box keeps the defaults: the three keys move out of `agentEnv`
     (`sandbox.mjs:96-97`) and `AGENT_ENV_KEYS` into `BUILD_SERVERS_OFF`, passed through `extra` by whichever arm wants them
     (the process arm decides in E3). A build server left running ends with the box: `bwrap`'s init exits with the CLI and the
     pid namespace takes the rest (a test observes it).
   - **Seam 3 — download bytes:** `egress.mjs` logs bytes per tunnel at close; the record carries bytes down per host, with
     cache growth (cache bytes after − snapshot bytes) beside it as a cross-check.
4. **Workspaces.** v1: `box/work` holds exactly `SPEC-v1.txt` (= the cache's `upstream/SPEC-v1.txt`) and the FIXED
   `test-v1.sh`, re-hashed after writing. v2: a copy of the v1 session's `box/work` minus `bin/ obj/ node_modules/`, then
   `SPEC-v2.txt` and the fixed `test-v2.sh` added; v1 inputs the agent changed are kept and flagged `v1 inputs changed`.
5. **A phase, in order:** guard (step 10) → `retireStale(<series>/s)` → `preflight({ …, install: false })` (E2.S2; the pinned
   CLI copy, toolchains, token status) → quiet check (S2) → `spendGate` → canaries planted (`sessionCanaryTargets`,
   manifest first) → layouts claimed, caches seeded → both `launchSession` calls in one tick, **`firstStack` (seeded per
   pair and phase) called first** → `startSkewMs` = the difference of the two spawn stamps (flag `start skew` > 5 s) → one
   `AbortController` for the phase; SIGINT/SIGTERM aborts both and is recorded as the abort reason → after both close,
   canaries checked and unplanted. Canary visibility, stated: the operator's project and memory dirs and the partner's
   config dir are visible to no bare session by construction (the box hides home and binds no partner), so a canary hit
   is evidence of a hole (G-8), never of a reach the box allows.
6. **Scoring, after both sessions of a phase end and before anything after them starts (the barrier).** Sequentially, in
   `firstStack` order, after a quiet check that only flags (`scored under load`), each spawned `taskset -c 0-13` (the cells
   are done): `score-session.mjs --session <s> --source <s>/box/work --arm bare --stack <stack> --phase <phase> --toolchain
   <tsDir> --toolchain <nodeDir> --nuget-cache <s>/box/nuget --npm-cache <s>/box/npm` — the session's own caches are the
   offline seed. **Outer timeout** `SCORER_OUTER_MS` = 90 min over the whole scorer process (its own steps sum to ≤ ≈ 60 min
   in the worst case: build 600 s, two visible and two upstream scripts, the held-out cases at 10 s each, the latency runs):
   on expiry `killTree` (group), the session stays `ran`, the runner halts resumable (exit 3) and records
   **`scorer-timeout`**, apart from the scorer's exit codes and never retried automatically (a hang repeats). Scorer exit
   3 → retried 3 × 60 s apart, then a resumable halt; exit 5 → series halt (instrument). A session is never voided for a
   scorer fault.
7. **Outcome classes — how the session ENDED** (`bareOutcome`, first match wins; registered in S3, rows marked † are
   registered extensions of §11.1):

   | # | Evidence | Class | Data? |
   |---|---|---|---|
   | 1 | `checkAliases` → stop | `stop: alias drift` (D12) | — |
   | 2 | the real token counted by a sampler or `scanSessionDir` | `stop: security` | — |
   | 3 | the final failed request answered 401 or 403 (broker log or envelope `api_error_status`) | `stop: auth` † | — |
   | 4 | a high contamination hit, a canary found, a `tokenHits` hit | `void: contaminated` | no |
   | 5 | `meta.stopped = 'readback'`, or **no broker `settled` line with a 2xx status and parsed usage** (the model never answered) | `void: harness` | no |
   | 6 | `meta.stopped = 'cancelled'` (the abort reason recorded) or `'disposed'` (the stack ended a box with no recorded reason), or `state.json` retired by `retireStale` | `void: harness (interrupted \| disposed)` † | no |
   | 7 | `meta.stopped = 'cost'` (settled spend ≥ cap), or envelope `subtype: 'error_max_budget_usd'` (the CLI's own cap), or the **final** failed request was a broker refusal with `reason: 'cap'` | `killed: cap`, censored at the observed stop-rule figure (`≥` it) | yes |
   | 8 | `rateLimitSignal(events).void` — the session ENDED on the limit | `void: rate limit` | no |
   | 9 | the final failed request was an upstream 5xx or 529 (after the CLI's retries) | `void: upstream` † | no |
   | 10 | `meta.stopped = 'wall'` / `'stall'` | `killed: wall` / `killed: stall`, censored | yes |
   | 11 | envelope `subtype: 'success'`, `is_error` false | `completed` | yes |
   | 12 | anything else after the model answered | `crashed: <subtype>` | yes |

   **Flags, never classes:** `broker refused k` (earlier cap refusals the session survived, with their summed backoff — the
   time from each refusal to the next admitted request), `upstream 429 k` (transient upstream 429s), `model_refusal_fallback`,
   `over cap`, `v1 inputs changed`, `start skew`, `foreign load`, `started not quiet`, `scored under load`, package flags.
   Row 7 before row 8 is the point: the broker's cap answer is a 429 `rate_limit_error`, which `rateLimitSignal` alone would
   void and pay for twice. `handVerdicts` (dated amendments lines, S5 step 2) can reclassify a session to `void: harness`
   only.
8. **The schedule** (`nextStep`; re-runs after the pair's own phases and before the next pair; attempt `a2`, fresh rid):

   | After the v1 phase | Next steps, in order |
   |---|---|
   | both data | v2 pair |
   | both void | v1 re-run pair (paired) → v2 pair |
   | one void (say C#) | TS v2 alone (`ran unpaired`) → C# v1 `a2` alone (`ran unpaired`) → C# v2 alone (`ran unpaired`) |
   | a void `a2` | that cell `missing`; its v2 never runs |
   | one v2 void | that v2 `a2` alone, from the recorded v1 tree (`ran unpaired`) |
   | both v2 void | v2 re-run pair |

   A `stop:` class halts the series; the session is kept, unclassified as data.
9. **Records.** `record.json` per session: ids (`pair, stack, phase, attempt, rid`), class, flags, mask and `maskObserved`,
   spawn stamp and skew, quiet and spin records, harness identity, prereg tag, CLI version; **cost** — headline recompute
   (primary), CLI total, `B`, stop-rule figure, per model main / children / internal, tokens per model and kind, first-turn
   cache read; T1–T5 under the policy, alias record; **time** — wall, `model_api`, `active − model_api` (registered primary
   time measures, M5), every partition category and `unattributed`; `build_test` against `other_tool` descriptive only (a
   chained Bash call counts as its highest category, V16); turns checked against assistant messages; children per model and
   max concurrent; **quality** — build ok, visible per script, held-out `percent`, `defects`, blocked count,
   upstream-original, latency median; product and test LOC; egress summary with bytes; **packages** (`packagesOf`): every
   npm package and version in the cache index and every NuGet `<id>/<version>/`, those added over the snapshot, and the flags
   of `inputs/package-flags.json` (a name matching `/mini-?git/i`; `typescript` ≠ 7.0.2; another TS runner or compiler —
   `tsx`, `ts-node`, `esbuild`, `@swc/*`; a `microsoft.netcore.app.*` or `microsoft.aspnetcore.app.*` pack off the 10.0
   band). Process-only fields are `n/a (bare)`. `pairs/p<k>.json`: masks, spawn stamps and skews, barrier time, unpaired
   labels, the **cell** (v1 + v2) per stack, C#/TS for cost (primary), wall, `model_api`, `active − model_api` — a value or
   a bound (`≥`/`≤`) when a side is censored — **each given with and without cells holding a `ran unpaired` session**, and
   quality per phase side by side.
10. **The pre-registration guard** (`preregGuard`, every `run` and `resume`): the tag `prereg/minigit-bare-<date>` exists on
    origin (`git ls-remote --tags origin`) and peels to `run-map.preregCommit`; every `PREREG_FILES` blob at HEAD equals its
    blob at the tag, except `amendments.md` (the tag's content must be a prefix), `seed.txt` (absent at the tag; when
    present its sha256 must equal `seed.sha256`; required for `pair:<k>`) and `crosscheck-policy.json` (may differ only by
    S4's dated T3 commit); for a paid `run`, `git status --porcelain -- spikes/` is empty (untracked included) and
    `harnessIdentity` equals `run-map.harnessIdentity` or an accepted amendment (step 11). A refusal names the file or field.
11. **Resume.** First: unplant every canary of the manifest. Then `retireStale(<series>/s)`. Per session: `launching` with
    `meta.json` → `ran`; `launching` retired or failed with paid work → row 6, scheduled by step 8, its spend = settled `B` +
    forwarded-but-unsettled requests at their reservation; `launching` with its dir gone (nothing paid) → `planned`, same
    rid; `ran` → score; `scored` → record rebuilt. Refused when `stacks.json`, the prices file, the snapshots, the prereg
    tag or the seed hash differ from `run-map.json`; a harness change needs `--accept-harness <identity> --amendment <date>`
    naming a dated `amendments.md` line (recorded in the ledger). A halt is passed with `--after-stop <rule> --reason <text>`;
    the series stops of §5 cannot be passed this way.

**Test scenarios** (stub `claude` = `test/session-launch.test.mjs:59`'s `stubClaude`, a stub upstream behind the real broker;
scoring injected except in the local end-to-end test).

| Test | Input | Expected |
|---|---|---|
| `Rids_are_distinct_across_pair_stack_phase_and_attempt` (T-R1) | all 24 combinations, one seed | 24 distinct; each matches the layout regex |
| `No_path_the_model_sees_names_the_arm_or_the_stack` | series id, every rid | `box/work`, `box/config` contain no segment `bare`, `process`, `csharp`, `cs`, `node`, `ts`, `typescript` |
| `Masks_swap_between_pairs_and_order_alternates_by_seed` | `maskFor`, `firstStack` for pairs 1–3, phases v1/v2, two seeds | masks differ within a pair and alternate across pairs; `firstStack` is a seeded bit; the seed decides pair 1 |
| `A_v1_workspace_holds_exactly_the_pinned_v1_inputs` | `fillV1` from a fake cache | exactly `SPEC-v1.txt`, `test-v1.sh`, hashes as `hashes.txt` |
| `A_v2_workspace_is_the_v1_tree_minus_build_output_plus_v2_inputs` | a v1 tree with `bin/`, `obj/`, `node_modules/`, a nested `src/obj.ts`, an edited `test-v1.sh` | dirs absent, `src/obj.ts` kept, v2 inputs added, `v1 inputs changed`, edit kept |
| `The_prompt_is_the_registered_bytes` | `promptFor` × stack × phase vs `inputs/prompts-bare.json` | byte-equal; stacks differ only in the two slots |
| `The_snapshot_tree_hash_is_stable_and_the_seed_copy_matches` | a fixture tree; reorder, touch mtimes, change one byte, change a mode | equal, equal, differs, differs; `seedCaches` copy re-hashes to the registered value |
| `The_bare_env_leaves_build_servers_at_their_defaults` | `agentEnv` (bare); `agentEnv({ extra: BUILD_SERVERS_OFF })` | the three keys absent / present; `AGENT_ENV_KEYS` lists none of them |
| `A_build_server_left_running_ends_with_the_box` (effect) | a box running `sh -c 'sleep 300 & exit 0'` with a marker | within 3 s of the close no cell process; control: one is listed before the close |
| `The_egress_log_counts_bytes_per_tunnel` | a proxy with a local upstream; 10 KiB up, 100 KiB down | one `closed` line with those counts; `egressSummary` sums per host |
| `Packages_are_listed_and_flagged` | a fake npm cache index and NuGet dir with `minigit-core`, `typescript@6.0.3`, `tsx`, `typescript@7.0.2`, a snapshot | four packages, three flags, `added` excludes the snapshot's |
| `A_completed_session_with_a_transient_refusal_is_completed` | broker log: a cap refusal, an admitted request 4 s later, a `success` envelope | `completed`; flag `broker refused 1` with backoff 4 s |
| `A_final_cap_refusal_is_a_cap_kill_not_a_rate_limit_void` (RED first on the bare `rateLimitSignal` reading) | last request a `refused` line `reason: 'cap'`, envelope `is_error` status 429 | `killed: cap`, censored at the observed figure; `rateLimitSignal` alone says void — shaped |
| `A_path_refusal_is_not_a_cap` | final refusal `reason: 'path'` | not row 7 |
| `The_CLIs_own_budget_stop_is_a_cap_kill` | envelope `subtype: 'error_max_budget_usd'` | `killed: cap` |
| `A_settled_cost_kill_is_a_cap_kill` | `meta.stopped: 'cost'`, settled $6.12 | `killed: cap`, `censoredAtUsd` 6.12 |
| `Only_a_session_that_ended_on_the_limit_is_a_rate_limit_void` | (a) two upstream 429s then `success`; (b) `rateLimitSignal` void at the end | (a) `completed`, flag `upstream 429 2`; (b) `void: rate limit` |
| `A_final_5xx_is_an_upstream_void_and_401_403_stop` | final 529; final 503; final 401; final 403 | `void: upstream` ×2; `stop: auth` ×2 |
| `No_2xx_settled_line_means_the_model_never_answered` | broker `settled` lines only with 4xx; one 200 with no usage | `void: harness` both |
| `Cancelled_and_disposed_are_interrupted_harness_voids` | `meta.stopped` `'cancelled'` (abort reason SIGINT), `'disposed'` | `void: harness (interrupted)` with the reason; `void: harness (disposed)` |
| `A_watchdog_kill_is_a_data_point_whatever_sessionOutcome_says` | `meta.stopped: 'wall'`, label `void: stopped` | `killed: wall` |
| `Precedence_holds_on_overlaps` | contaminated + capped; readback + cancelled; auth + upstream | the higher row |
| `The_schedule_follows_the_void_table` (T-V1) | every row of step 8, and a second void | the steps in order with labels; `missing` after a second void |
| `The_barrier_holds_until_both_are_scored` | one v1 scored, one `ran` | `nextStep` = score, never a v2 phase |
| `Spend_decision_takes_several_caps_a_live_sum_and_a_factor` (T-G1) | `spendDecision(10, 40, [6, 6], { liveUsd: 3, factor: 1.1 })`; spent 24; a number cap | go (26.2); stop (40.2); today's form unchanged |
| `The_spend_gate_applies_both_limits_and_prior_spend_only_to_the_ceiling` | ledger: prior $0.43, bare $30; two sessions | stop naming the bare budget; the ceiling line counts the prior, the bare line does not |
| `The_stop_rule_figure_is_the_largest_basis` | E $1.00, recompute $1.02, B $1.01; then no envelope, B $2.10 | 1.02; 2.10 |
| `The_skew_is_measured_at_spawn` | two stub spawns stamped 30 ms apart, launch calls returning 2 s apart | `startSkewMs` 30 |
| `A_scorer_that_hangs_is_killed_at_the_outer_limit` | a stub scorer sleeping forever, `outerTimeoutMs` 500 | killed (no process left), `scorer-timeout`, session `ran`, exit 3, no retry |
| `A_scorer_environment_failure_halts_resumably_and_never_voids` | stub scorer exits 3 four times | three retries, exit 3, sessions `ran` |
| `Resume_unplants_canaries_first` | a manifest with two planted files and a run-map mid-phase | both removed before `retireStale` is called (call order spy) |
| `Resume_after_a_crash_continues_where_the_files_say` | run-maps cut at each transition | `resumePlan` names exactly the remaining steps; no data outcome re-run |
| `An_interrupted_session_is_charged_its_unsettled_reservations` | broker log: 3 settled, 2 `forwarding` without `settled` | spend = settled + 2 × reservation |
| `Resume_refuses_a_changed_pin_snapshot_or_seed_hash` | each field changed | refused naming it; harness change accepted only with `--accept-harness` + an amendment line |
| `The_guard_checks_tag_blobs_tree_and_harness` | temp repo + bare remote: no tag; tag on another commit; a PREREG blob changed; `amendments.md` appended; `seed.txt` with a wrong hash; an untracked file under `spikes/`; a harness identity changed | refused, refused, refused, ok, refused, refused (paid only), refused |
| `Harness_identity_ignores_inputs_and_follows_code` | commit to `inputs/amendments.md`; commit to `lib/x.mjs` | same; different |
| `Ratios_are_given_with_and_without_unpaired_cells` | a pair where one cell holds `ran unpaired` | two ratio sets; the second omits that cell |
| `A_censored_side_makes_the_pair_ratio_a_bound` | C# `killed: cap` at $6.30, TS completed at $3.00 | cost ratio `≥ 2.1`, a bound |
| `The_session_record_has_every_field_or_says_why` | the bare-smoke fixture as a session dir, a fake score | every field `known`, `not captured` with a reason, or `n/a (bare)` |
| `Init_pins_the_cli_copy_and_dry_run_writes_nothing` | `init --dry-run`, then `init` against a fake versions dir | dry run: the plan printed, no file written; `init`: the copy mode 0555 with its sha256, `run-map.json`, ledger prior lines |
| `Run_dry_run_prints_the_phase_and_spends_nothing` | `run --what pair:1 --dry-run` | the ordered steps, masks, rids, gate lines; no layout claimed, no broker started |
| `A_stub_pair_runs_end_to_end` (local; `BENCH_REQUIRE_SANDBOX=1`) | `run --what pair:1`, stub CLI in real boxes, the real scorer | both phases, the barrier held, records written, no socket or cell process left |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/bare-*.test.mjs test/packages.test.mjs test/cache-snapshot.test.mjs
test/run-map.test.mjs test/workspace.test.mjs test/prereg-guard.test.mjs test/spend-gate.test.mjs test/score-run.test.mjs
test/build-servers.test.mjs test/egress.test.mjs test/agent-box.test.mjs`; `node .agents/conventions/tools/low-priority.mjs
node --test spikes/policy-ab-quick/test/spend.test.mjs`; `node spikes/minigit-bench/bare-runner.mjs snapshot --stack
csharp` and `--stack node` (free: registries only); `… run --what pair:1 --dry-run`.

**Done when.** Every test green, `A_final_cap_refusal_…` recorded RED first; the stub pair end to end with no skip; both
snapshots made, hashed and sized (sizes into §8); `spendDecision`'s callers unchanged; E2's `sandbox.mjs` and `egress.mjs`
changes reviewed as E2 files; `research/module_minigit_bench.md` gains "The bare runner" (entry points, exit codes, the
outcome table, the schedule, the seams).

---

## E3b.S2 — the machine-quiet check, the CPU sampler and the spin probe: refuse to START a phase on a loaded machine, flag load DURING one

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus.

**The decision: refuse at the start, flag during.** Before a phase nothing is spent, so waiting for a quiet machine is free,
while a pair started under foreign load is a paid, confounded data point (V7). During a phase, killing on load would make a
void §11.1 does not list and discard paid work, so load is **flagged**, the pair stays in every table, and the
language-effect line is also given without flagged pairs. Other `claude -p` runs also share the subscription's usage
window (D16) — a further reason to refuse.

**Files.** `spikes/minigit-bench/lib/cpu-sampler.mjs` (new; parent §8 names it) — pure `hostBusy(prev, stat)`,
`foreignCpu({ procs, cellIds, harnessPids, prev })`, `foreignAgents(procs, patterns)`, `quietVerdict(samples, thresholds)`,
`maskObserved(pid, mask, procApi)`; `spinProbe({ iterations })` (a worker thread of the runner, so on CPUs 6–7),
`calibrateSpin({ runs: 30 })`; `startSampler({ cellIds, masks, out, intervalMs: 5000, procApi })` (the one I/O loop);
`spikes/minigit-bench/lib/quiet.mjs` (new) — `waitForQuiet({ thresholds, maxWaitMs, procApi, now, sleep })`;
`inputs/quiet.json` (S3 registers it); `test/{cpu-sampler,quiet}.test.mjs`.

**The change — what and how.**

1. **Pre-start check, per phase** (and before the A/A and the pilot): a 60 s window of 5 s samples. **Quiet** = (a) host CPU
   busy (`/proc/stat`, all 14 CPUs, user+nice+system+irq+softirq+steal) minus the harness's own ticks: mean ≤ **0.5 CPU**,
   no sample > **1.5 CPU**; (b) **zero foreign agent processes**: a Claude Code CLI (`argv[0]` basename `claude`, a path
   under `claude/versions/`, or `@anthropic-ai/claude-code`'s `cli.js`) with `-p`/`--print`, or `codex exec`, whose environ
   (when readable) holds no `BENCH_CELL` of this series — another user's process counts as foreign; (c) `MemAvailable` ≥
   25 % of `MemTotal`; (d) no live session of any series under `~/rsd/minigit/*/s/`; (e) **spin probe** median over the
   window ≤ `spinMaxRatio` (1.30) × the calibrated idle median — it sees contention the per-process view cannot, such as
   hypervisor steal or load on the harness core. Not quiet → re-check every 60 s up to **30 min**, then exit 3 with the
   verdict (what failed, the numbers, foreign cmdlines through `maskTokens`). `--accept-noisy <reason>` starts anyway with
   the flag `started not quiet`, and is refused for a v2 phase whose v1 was quiet.
2. **During a phase** the sampler appends to `pairs/p<k>-cpu.ndjson` every 5 s: per-CPU busy, busy per mask group (0-5,
   8-13, 6-7), host total, PSI `some avg10` (recorded, never a flag — the cells' own oversubscription raises it), foreign
   agent count, **foreign CPU** = Σ Δ(utime+stime) of processes with no cell marker that are not the harness (a LOWER bound:
   a foreign process born and dead between samples is missed), and one spin-probe run (alternately on CPU 6 and 7). Flags:
   `foreign load` when the 60 s mean of foreign CPU > **1.0 CPU** or a foreign agent appears; `harness core contended` when
   the 60 s spin median > `spinMaxRatio` × calibration.
3. **The mask, observed:** on the first sample after each spawn, `Cpus_allowed_list` of every inner cell process; `maskObserved`
   false is an instrument miss (G-11).
4. **Calibration:** `init` runs `calibrateSpin` only on a quiet verdict (a–d) and records the median and spread in
   `run-map.spin`; `iterations` is fixed in `quiet.json` so one probe takes ≈ 20 ms idle (≈ 0.4 % of a CPU at 5 s).
5. **Before scoring** the same check runs and only flags (`scored under load`): per-invocation latency is load-sensitive.

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `Host_busy_is_computed_from_two_stat_readings` | two `/proc/stat` fixtures | exact CPU-equivalents; steal in, idle and iowait out |
| `A_foreign_claude_print_process_is_found_and_ours_is_not` | `claude -p` without marker; with this series' marker; interactive `claude`; `node …/cli.js --print`; `codex exec`; another user's `claude -p` | foreign: 1st, 4th, 5th, 6th |
| `The_foreign_cmdline_is_masked_in_the_verdict` | a foreign cmdline carrying `sk-ant-…` | `[secret]`, never the value |
| `Quiet_needs_every_condition` | windows: idle; one 2-CPU sample; a foreign agent; 20 % memory; a live session elsewhere; spin at 1.5 × | quiet; then not, naming each condition |
| `The_spin_probe_calibrates_and_detects_contention` | an injected clock: 30 idle runs at 20 ms; then samples at 21 ms and 30 ms | median 20; quiet at 21 (1.05); not at 30 (1.5) |
| `The_wait_retries_then_gives_up_resumably` | fake clock; load never drops; load drops at 7 min | gives up at 30 min with the verdict; starts at ≈ 8 min |
| `Noisy_override_is_flagged_and_refused_for_a_v2_after_a_quiet_v1` | `--accept-noisy` on v1; on a v2 whose v1 was quiet | flagged; refused |
| `Foreign_cpu_excludes_our_cells_and_the_harness` | two cells, the harness, a foreign builder | the builder's delta only, labelled a lower bound |
| `The_cell_runs_on_its_mask` (effect) | a box through seam 1 with mask `0` running `grep Cpus_allowed_list /proc/self/status`; control without the prefix | `0`; control the full list; `maskObserved` true / false |
| `The_runner_reexecs_onto_the_harness_core` (effect) | `bare-runner.mjs status` started without the marker | its process reports `Cpus_allowed_list: 6-7`; no second re-exec |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/cpu-sampler.test.mjs test/quiet.test.mjs`.

**Done when.** The tests green with the effect tests not skipped; one 60 s pre-start window and one spin calibration run on the
series machine, pasted into the PR (the idle baseline the thresholds are read against); the runner calls `waitForQuiet` before
every phase and starts the sampler with every phase.

---

## E3b.S3 — the pre-registration, bare scope: committed, tagged and pushed before the first paid minigit session

**Model:** scenarios + brief Opus (this text) · text Opus (it fixes the analysis) · extraction scripts and tests Sonnet ·
review Opus.

**Goal.** Everything the bare analysis will do is fixed in the public history before any bare session of the task is paid
for, so no number can choose its own analysis (parent §11, cut to the bare arm).

**Files.** The `inputs/` files below; `spikes/minigit-bench/prereg/extract-prediction.mjs`, `prereg/extract-contract.mjs`
and `prereg/heldout-prereg.mjs` (new: quoted parts and hashes are never typed); `test/prereg.test.mjs` (new).

**Where it lives.** `spikes/minigit-bench/inputs/`, the paths parent §8 names. One commit, then the **annotated tag
`prereg/minigit-bare-<yyyymmdd>`** on it, pushed to origin and never deleted or moved; `init` records both. `PREREG_FILES`
(S1 step 10) is exactly:

| File | Content |
|---|---|
| `statement.md` | Parent §1's question, part (a), word for word; the arms (bare now, process later, D20); stacks and sentences; model, effort, cap, wall; what is measured; §11's "does not license" sentence verbatim; the bare-first weakenings (below). **The stack label:** banteg's prompt said `Typescript` (no `prompt_name`, so `lang.capitalize`; `csharp` has `prompt_name: 'C#'` — `banteg/minigit-bench@83644e0` `benchmark.rb:41`, `:147`, verified 2026-10-30); parent §4.3 change (1) deliberately makes it `TypeScript`. banteg's `extra_prompt` for C# and TypeScript: none; ours carries `{STACK_SENTENCE}` in that slot (§4.3 change 2). |
| `analysis-contract.md` | VERBATIM quotes of the parent **at a commit on `origin/main`** (named in the header; a test checks it is an ancestor of `origin/main`): §11's "Unit of analysis", "Censored and mixed outcomes", "What n = 3 licenses"; §11.1's void list, data points and re-run rule; §5.3's acceptance (`delivered` marked process-only). Then, marked **bare operationalizations decided before any cell**: S1 step 7's outcome table with precedence and flags, its † rows marked **registered extensions of §11.1** with their reasons (`void: upstream` — the provider, not the stack, ended the session; `void: harness (interrupted \| disposed)` — the harness or host ended it; `stop: auth` — the credential); S1 step 8's schedule; censoring at the observed stop-rule figure; the cell = v1 + v2 for cost and time; per-phase quality (prediction 5's medians are per phase); **primary measures** — cost: the headline recompute; time: wall, `model_api`, `active − model_api`; `build_test` vs `other_tool` descriptive only (V16); ratios with and without `ran unpaired` cells and flagged pairs; S2's quiet rule; §5's stop rules, the $40 budget and its one permitted raise; "pairs 1–3 run unless a named stop fires"; S4's GO definition and table; the T3 rule. |
| `checklist.json` | `{ "bare": { "compliance": "not applicable: §5.4 is the process arm's", "acceptance": [build clean, visible 100 % per script, held-out primary 100 % with blocked counted against], "instrumentChecks": [the G-ids of S4] }, "process": null }` |
| `heldout-prereg.json` | From `heldout/manifest.json`, `suite.enc.sha256`, `hashes.txt`, `calibration.json` by script: archive sha256, plaintext sha256, N1, N2, M, the calibration file's sha256 |
| `prediction.md` | Parent §11's prediction, bare parts only, extracted verbatim from `85c54b7` (on `origin/main`; its bare items equal the branch's), elisions marked; then the bare-first note |
| `prompts-bare.json` | The exact bare v1 and v2 prompt bytes per stack (parent §4.3) with `{STACK}` = `C#` and `TypeScript`, and each sha256 |
| `cache-recipe.json`, `cache-snapshots.json` | S1 step 2's recipe and the two snapshot tree hashes and sizes |
| `package-flags.json` | S1 step 9's flag rules |
| `quiet.json` | S2's thresholds: window 60 s, interval 5 s, 0.5 / 1.5 CPU, 1.0 CPU during, agent patterns, 25 % memory, `spinMaxRatio` 1.30, spin iterations, 30 min wait |
| `spend-prior.json` | The paid runs before E3b, `{ date, what, usd, evidence }`, read from their evidence files |
| `seed.sha256` | sha256 of the bare seed (commit-reveal): `bare-runner.mjs seed-commit` writes `mg-` + 16 random hex to `~/rsd/minigit/seeds/bare.seed` (mode 600, outside git) and only its hash here |
| `crosscheck-policy.json` | the existing file, frozen by the tag; only S4's dated T3 commit may change it |
| `amendments.md` | a header: "Dated amendments; instrument only after the A/A (parent §11). By-hand verdicts: `YYYY-MM-DD \| G-<n> \| holds\|refuted\|explained \| <rid> \| <evidence path> \| <who>`." |

**`prediction.md`, the text** (the header names `85c54b7` and `01b7459`, where the full prediction first became public):

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

Items 3, 4 and 6 are process items; they stand in the parent at the named commits.

**What the bare-first deviation weakens** (in `prediction.md` and `statement.md`):

1. **The interaction** (prediction 3) compares windows days apart instead of interleaved blocks: server-side model changes,
   API latency and time of day differ between arms. D12 catches an alias resolving to a new id, not a change behind one id.
2. **No arm-lead draw** for the bare cells: order between arms is confounded with time.
3. **T3 is armed for bare cells on bare evidence** (the A/A and the E2.S2 smoke); the process arm re-decides at P1.
4. **The process treatment is finalized after bare results are visible** (E2.S4). Parent §6 already fixes it; any change after
   the first bare pair is a dated amendment applied to both stacks, never motivated by one stack's bare result.
5. **The prediction is registered in two parts**; the process part is public since `01b7459` with its dated edits, all
   before any cell, so the split costs no evidence.

The C#/TS comparison **inside** the bare arm keeps its concurrent pairs and loses nothing.

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `The_prediction_is_verbatim_from_a_main_commit` | `prediction.md`, the header's commit | every non-elided fragment, unwrapped, is a substring of that commit's §11; the commit is an ancestor of `origin/main` |
| `The_analysis_contract_quotes_are_verbatim_from_a_main_commit` | the quote blocks | each a substring of the named commit, itself on `origin/main` |
| `The_extensions_are_marked_as_registered` | `analysis-contract.md` | `void: upstream`, `void: harness (interrupted \| disposed)`, `stop: auth` each under the "registered extension of §11.1" marker with a reason |
| `The_heldout_prereg_is_derived_not_typed` | `heldout-prereg.json` | equal to the script's output |
| `The_registered_prompts_differ_only_in_the_stack_slots` | `prompts-bare.json` | per phase equal after slot replacement; labels `C#` and `TypeScript`; sha256 fields match |
| `The_seed_is_committed_as_a_hash_only` | the commit's tree | `seed.sha256` present, `seed.txt` absent, no 16-hex `mg-` string anywhere in `inputs/` |
| `The_prereg_names_no_heldout_case` | `heldout-tool.mjs leakcheck --range origin/main..HEAD` | clean (key needed; local) |
| `The_guard_list_is_the_committed_list` | `PREREG_FILES` vs this table | equal |

**Commands.** `node spikes/minigit-bench/prereg/extract-prediction.mjs --parent-commit 85c54b7`, `… prereg/extract-contract.mjs
--parent-commit <a main commit>`, `… prereg/heldout-prereg.mjs`, `node spikes/minigit-bench/bare-runner.mjs seed-commit`,
`node spikes/minigit-bench/run-tests.mjs test/prereg.test.mjs`, `node spikes/minigit-bench/heldout-tool.mjs leakcheck
--range origin/main..HEAD`, `git tag -a prereg/minigit-bare-<yyyymmdd> -m <message> <commit>` and its push (the operator's).

**Done when.** One commit holding every file, tests green, leak check clean, tag pushed and peeling to that commit, `init`
recording both — all before the A/A's first launch. The PR names the commit and the tag.

---

## E3b.S4 — the bare A/A, the v2 rehearsal and the Node pilot: what they must show, what a miss means, what is amended, what stops; T3; the seed reveal

**Model:** criteria Opus (this text; a cornerstone area, unmarked — §3) · `lib/aa-verdicts.mjs` and the running Sonnet ·
review Opus.

**Goal.** Before any data pair: two identical bare C# v1 sessions concurrently on masks 0-5 and 8-13 (`run --what aa`; parent
§15 step 9), then a free v2 rehearsal on a twin's tree (`run --what rehearse-v2`: `fillV2` on twin 1's v1 tree, a stub-CLI
v2 launch, the real scorer at phase v2), then one bare Node v1 alone (`run --what pilot-node`). Pilot spend, inside the $40.
`aa-verdicts` (`lib/aa-verdicts.mjs`) computes `AA.md` from the records and the dated `amendments.md` verdict lines.

**The table** (‡ = also required of the Node pilot):

| Id | Must show | A miss means | Then |
|---|---|---|---|
| G-1 ‡ | read-back ok on every init field (E2.S2 step 6) | the launch is not the pinned treatment | fix; A/A again |
| G-2 ‡ | T1 | the envelope disagrees with itself | **stop**; fix; A/A again |
| G-3 ‡ | T2 per model and kind | O incomplete | investigate; no data pair until T2 holds on an A/A |
| G-4 | T3/T4 over matched sources: every main-role O record matched by a transcript message and none unmatched; every subagent-role record matched | T's coverage not established | **T3 rule:** main coverage here AND subagent coverage here or in the committed E2.S2 smoke fixture (B-T1, same CLI) → `t3: stop` for bare data cells; else `fallback-t2` (`t2: stop`, O and E reconciled per model within 1 %); O and E irreconcilable → no data pair. One dated commit of `crosscheck-policy.json` |
| G-5 ‡ | T5: `B = E` within $0.000001 | the broker prices differently | resolved (fixed, or explained by a dated line) before pair 1 |
| G-6 ‡ | `partition` Σ = wall to the ms and `unattributed` < 5 % of wall — or **`explained`**: a dated line attributes the unattributed intervals to a named cause with its evidence path, showing no span source is missing | the classifier or a span source is wrong | fix the reader and re-read the SAME records; re-run only if a record was missing |
| G-7 | **record kinds**: each twin carries the same set — stream, envelope, O, T, audit, broker, egress with bytes, CPU and spin samples, packages, `score.json` with every step known, its ledger line; **aliases per D12**: every alias either twin USED resolves to the same id as the baseline the first twin set (they may differ in which aliases they used) | an instrument sees one twin and not the other; or alias drift | fix, A/A again; drift → **stop** (D12) |
| G-8 ‡ | isolation: allowed CONNECTs ⊆ `EGRESS_HOSTS.bare`, refused ⊆ `KNOWN_REFUSED` or named by a dated line; real token 0; no high contamination, canary or token hit | a hole in the box | **stop**; fix (E2.S2's owner); A/A again |
| G-9 | the twin spread, RECORDED, never gating: cost, wall, `model_api`, `active − model_api` ratios, held-out `percent`/`defects` differences, CPU per mask | — | shown beside every bare pair ratio (n = 1: a reference, not a threshold) |
| G-10 ‡ | the scorer by hand: in a scratch copy of one twin's tree (and the Node tree) the build and the fixed visible script run by hand give the scorer's counts — a dated line | the bare scorer misreads a real tree | fix the scorer, re-score (no re-run) |
| G-11 ‡ | quiet check passed; sampler and spin probe wrote every 5 s; `maskObserved` true for every cell | the contention control is not in force | fix S2; A/A again |
| G-12 ‡ | the cap stops a session that reaches it, at settled spend ≥ $6 | the cap failed | **stop**; fix |
| G-13 ‡ | each session's class read by hand against its stream and broker log — a dated line | `bareOutcome` misreads an ending | fix S1, re-classify |
| G-14 | the v2 rehearsal: the v2 workspace as S1 step 4 says; the stub v2 session's records complete; `score.json` at v2 with every step known | the v2 path is broken | fix S1, rehearse again (free) |

**GO** = every G `holds` (G-6 `explained` counts as holds; G-9 never gates), except these, which may be **`not exercised`**,
each with its replacing evidence named in `AA.md`: **G-4's subagent half** → the E2.S2 smoke fixture's B-T1 on the same
CLI; **G-12** (no twin reached the cap) → E2.S2's broker proof P-6 and the budget tests green at the run's harness identity.
A by-hand G (G-10, G-13) with no dated line is `not exercised` and is **not** in that list, so it is NO GO. Any `refuted` is
NO GO.

**Amended, and how.** After the A/A only the **instrument** may change (harness, scorer, counters, read-back, sampler,
quiet thresholds, the evidence reading of the outcome table): a dated `amendments.md` line with its reason and G-id,
committed and pushed before pair 1, named by `--accept-harness`. **Frozen**: prompts, cap, wall, model, effort, tools and
allow-list, toolchain pins, snapshots, build-server setting, egress list, workspace contents — a change applies to both
stacks and needs another bare pilot **pair** (C# v1 ∥ TS v1), paid from the $40 (parent §11).

**Stops.** G-2, G-8, G-12 refuted, and alias drift, stop the series until fixed and an A/A is GO. A second NO GO on the same
G-id goes to the operator before a third A/A is paid for.

**Then — the seed reveal.** `bare-runner.mjs seed-reveal` copies `~/rsd/minigit/seeds/bare.seed` to `inputs/seed.txt`,
committed and pushed after the amendments and before pair 1; the guard checks its sha256 against the tagged `seed.sha256`.
It fixes the data rids, pair 1's masks and the launch and scoring order. The process draw gets its own seed (E3).

**Test scenarios** (`test/aa-verdicts.test.mjs`, before any paid run).

| Test | Input | Expected |
|---|---|---|
| `Every_G_verdict_is_computed_and_none_is_blank` | synthetic twin records: all good; each G broken once | 14 rows; each broken fixture refutes exactly its G |
| `GO_allows_only_the_listed_not_exercised_with_their_evidence` | G-4 sub half and G-12 `not exercised` with evidence; G-12 without evidence; G-10 without a line | GO; NO GO; NO GO |
| `By_hand_verdicts_are_read_from_dated_lines` | `amendments.md` with a malformed line, an undated one, a valid one | only the valid one counts; the others reported |
| `G6_explained_counts_as_holds` | unattributed 7 % with an explaining line; without | holds (`explained`); refuted |
| `Aliases_follow_D12_not_equal_model_sets` | twin A used opus + sonnet, twin B opus only, same ids; then B's opus a new id | holds; refuted with a stop |
| `The_T3_rule_arms_only_on_established_coverage` | main + sub here; main here + sub in the smoke; main only; neither | `stop`, `stop`, `fallback-t2`, `fallback-t2` |
| `A_twin_difference_is_recorded_not_refuted` | one twin capped, one completed | G-9 holds with the ratios |
| `Stops_are_named_as_stops` | G-2, G-8, G-12 refuted | `AA.md` verdict `STOP` naming them |
| `The_revealed_seed_must_match_its_hash` | a `seed.txt` that does not hash to `seed.sha256` | the guard refuses `pair:1` |

**Commands.** `node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what aa`, `--what rehearse-v2`, `--what
pilot-node`; `… aa-verdicts --series <dir>`; the policy commit; `… seed-reveal`.

**Built (deviations).** `lib/aa-verdicts.mjs` and its helpers (`aa-rules.mjs`, `aa-rules-twins.mjs`, `aa-check.mjs`, `aa-facts.mjs`, `aa-smoke.mjs`) compute the table; the command writes `<series>/aa-verdicts.json` (JSON, not `AA.md`: `AA.md` is
rendered from it by hand, with the replacing evidence named) and prints the table; exit 0 GO, 2 NO GO or STOP, 4 a refusal. G-12's replacing evidence is a dated `G-12 | holds | <rid> | <evidence path> | <who>` line (the budget tests green at
the run's harness identity cannot be computed offline). G-4's subagent half is the committed smoke fixture's B-T1, recomputed on the pinned CLI. A ‡ row with no Node pilot is `not exercised`. Tests: `test/aa-verdicts.test.mjs`.

**Done when.** `AA.md` says GO; `amendments.md`, `crosscheck-policy.json` and `seed.txt` committed and pushed as dated changes;
the pilot spend in the ledger.

---

## E3b.S5 — the three bare pairs, the by-hand read-back, and the interim bare record

**Model:** running Sonnet · `lib/bare-report.mjs` Sonnet · review Opus.

**Goal.** Pairs 1, 2, 3 in order (`run --what pair:<k>`), each run unless a named stop fires (§5; the operator's go chooses
only when); then a computed interim record.

**The change — what and how.**

1. Each pair goes through S1 step 5's phase sequence. A pair takes ≤ ≈ 2.5 h; prompt-cache warmth carries between pairs
   within the 1-hour TTL (V8) and meets both stacks alike; the first-turn cache read is flagged per session.
2. **By-hand read-back, pair 1** (parent §8): for the C# and TS v1 sessions, `argv.json`, `readback.json`, the bind list, the
   seeded snapshot hashes, the first stream lines and the **package list with its flags** are read against the pins by the
   operator or an Opus judge, and the verdict is a dated `amendments.md` line. **A mismatch:** if the launch itself differed
   from the pins, the affected sessions are reclassified `void: harness` through that line (`handVerdicts`, S1 step 7), the
   cause is fixed as an instrument amendment, and the sessions re-run by the schedule (it is their one re-run); if only the
   record was wrong, the record is rebuilt after the fix and the sessions stand. A flagged package is read by hand and its
   reading recorded the same way.
3. **Interim record** `research/minigit/bare-pairs-<date>.md` and `…-data.json`, rendered by `bare-report.mjs` (`bare-runner.mjs
   report`) from the records only: per session and per cell; per stack median and range; the three pair ratios (values or
   bounds) and their median, each with and without `ran unpaired` cells and flagged pairs; the primary measures first and the
   descriptive ones labelled; quality per phase; flags; spend as prior, pilot, data and void lines; the A/A spread beside the
   ratios (G-9); the prediction beside the observation (rule 4); §11's licensing paragraph and S3's weakenings verbatim; the
   harness identity, prereg tag, seed, CLI version, snapshots, pins and machine (rule 6). Held-out as counts, `percent` and
   `defects` only. Superseded by E4's report, which links it.

**Test scenarios** (`test/bare-report.test.mjs`).

| Test | Input | Expected |
|---|---|---|
| `The_interim_record_is_rendered_from_records_only` | three synthetic pair records: one censored side, one `foreign load`, one `ran unpaired` | tables match; the bound printed as a bound; with/without lines present |
| `Primary_measures_come_first_and_descriptive_ones_are_labelled` | the rendered file | cost (headline recompute), wall, `model_api`, `active − model_api` first; `build_test`/`other_tool` under "descriptive" with the V16 sentence |
| `The_interim_record_names_no_heldout_case` | the rendered file | leak check clean; only counts, `percent`, `defects` |
| `The_record_carries_the_licensing_and_the_weakenings` | the rendered file | §11's paragraph and S3's five weakenings verbatim |
| `A_read_back_mismatch_reclassifies_through_a_dated_line` | a pair-1 record and an amendments line `G-1 refuted <rid>` | that session `void: harness`; the schedule adds its re-run |

**Commands.** `node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what pair:1` (then 2, 3; `resume` after any
halt); `… report --series <dir> --out research/minigit/bare-pairs-<date>.md`; `node spikes/minigit-bench/heldout-tool.mjs
leakcheck --range origin/main..HEAD`.

**Built (deviations).** (1) `lib/bare-report.mjs`, `bare-runner.mjs report` and `test/bare-report.test.mjs` were **not built**, and
the record is not `research/minigit/bare-pairs-<date>.md` / `…-data.json`. In their place: `research/minigit/tools/extract.mjs`
reads the series' files (read-only) into `research/minigit/data/<series>.json`; `render.mjs` rewrites the generated blocks of
`research/minigit/RESULTS_bare_2026-10-30.md` from those files; `replay.mjs` replays the contamination scan over every recorded
stream into `data/replay.json`. The tools have no tests beyond `replay.mjs --self-check` and render's own failures on a missing
file or an unknown block, so the five `bare-report` test rows above are **not covered**; the record's numbers are checked
by `extract.mjs` against each pair's own `pairs/p<k>.json` instead. (2) The pairs ran in two series, not one: series
`s-20261030-bf89` stopped at pair 3 by its registered second `void: contaminated` (two false positives of the scan: defect 14,
fixed after it), and `s-20261030-b82e` ran pairs 1–3 on the fixed instrument; after a pair has started no harness change may be
accepted in that series. Their classes, flags, cells and the later defects (16–18) are in the record, not repeated here.

**Done when.** Three pairs ran, or a named stop ended the arm and the record says which cells are missing and why; every
session has `record.json`, every pair `pairs/p<k>.json`; the pair-1 read-back line is in `amendments.md`; the interim record
committed with its leak check clean; spend within $40 bare and $500 total, or the stop named.

---

## 6. Build order

E2.S1 → E2.S2 merged, with §2's review changes → **E3b.S1 and S2** (one branch each, free, stub CLI) → `snapshot` × 2 →
**S3** (commit, tag, push) → **S4** (A/A, rehearsal, pilot; amendments, T3 commit, seed reveal) → **S5** → E2.S3, E2.S4,
E3.

## 7. Test plan — ids by story

| Id / tests | Story |
|---|---|
| T-G1, T-V1, T-R1 (bare); outcome, schedule, record, packages, snapshot, run-map, workspace, guard, spend-gate, scorer-timeout, build-server, egress-bytes, init and dry-run tests; the stub pair | S1 |
| sampler, quiet, spin, mask-effect and re-exec tests | S2 |
| verbatim-from-main, extension-marker, derived-hash, prompt, seed-hash-only and leak tests | S3 |
| `aa-verdicts` tests; `AA.md` live | S4 |
| `bare-report` tests; three pairs live | S5 |

## 8. Growth

| Surface | Size at this document's volumes | Retired by / interrupted |
|---|---|---|
| Session dirs `<series>/s/<rid>/` **including `box/nuget`, `box/npm`, `box/home/.dotnet` and the scorer's `score/cache-*` copies, all kept until E4 retires the series** | per session ≈ 30 MB raw (parent §14) + 2 × the seeded snapshots (box + score copy; estimated 40–60 MB each, the measured sizes replace this estimate in S1's PR) + downloads + `score/copy` ≈ **150–200 MB**; 15 planned sessions ≈ 2.3–3 GB, with every re-run (30) ≈ 4.5–6 GB, on `/home` (745 GB free, 2026-10-30) | only `box/work/{bin,obj,node_modules}` and `score/copy/{bin,obj,node_modules}` are shed after scoring AND the v2 copy (safe-rm under the series root, by name); everything else kept until E4's `retire.mjs` |
| Snapshots `~/rsd/minigit/toolchains/cache-<stack>-<hash12>/`, the CLI copy (292 MB) | ≈ 0.4 GB together | kept for the series; `retire.mjs` |
| `ledger.ndjson`, `run-map.json`, `pairs/*.json`, `pairs/*-cpu.ndjson` | KBs; the CPU log ≈ 720 lines per pair-hour | kept with the series |
| `~/rsd/minigit/seeds/bare.seed` | one line, mode 600 | kept (it is revealed into git in S4) |
| Canary files in the operator's project dirs | one per dir per phase | unplanted after each phase from the manifest; `resume` unplants first |
| `inputs/*`, the tag, `research/minigit/bare-pairs-*` | KBs, committed | kept (the record); the tag is never deleted |
| In-flight states | `launching` sessions; `starting/running` `state.json`; a scorer run | `retireStale` at every `run`/`resume`; the session wall (60 min), the scorer's outer limit (90 min) and the quiet wait (30 min) bound every non-terminal state |

## 9. Definition of Done

- [ ] E3b.S1–S5 merged or run as their done-whens say; `BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs`
      green with no skip among the tests named here.
- [ ] `A_final_cap_refusal_is_a_cap_kill_not_a_rate_limit_void` observed RED on the bare `rateLimitSignal` reading, then green.
- [ ] Both cache snapshots made and hashed before S3; the pre-registration committed, tagged and pushed before the A/A's first
      launch; the tag in every session record.
- [ ] `AA.md` says GO; T3 decided by a dated commit; `seed.txt` revealed, matching its hash, before pair 1.
- [ ] Three bare pairs ran or the stop is named; spend ≤ $40 bare and ≤ $500 total, with prior, pilot, data and void spend apart.
- [ ] `research/module_minigit_bench.md` describes the runner, the seams in E2's files, the quiet check, the outcome table and
      the schedule; the parent's D20, §4.1, §8, §11.1, §13, E3 and E4 entries match this document; `todo/README.md` lists it.
- [ ] The summary names models and counts per story and says the A/A criteria were written by Opus because the cornerstone
      quota is full.
