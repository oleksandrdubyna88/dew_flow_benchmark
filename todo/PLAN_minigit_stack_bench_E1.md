# PLAN — E1 stories: the measurement instruments of the minigit stack bench (held-out suite and scorer, counters, the time partition)

> Status: **plan only, nothing implemented yet, 2026-10-29.** Scope: epic E1 of
> [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md) (§15 steps 2–3) — a new spike folder `spikes/minigit-bench/`
> (inputs, scorer, held-out tooling, counters, partition, its own test runner) reusing `spikes/policy-ab-quick/lib` read-only.
> Nothing in `policy-ab-quick` changes in this epic (its fixes are E2.S1), nothing launches `claude`, and no story spends money.
> Four stories, E1.S1–E1.S4; cornerstone **E1.S3** (`model: fable`); the parent's ledger reads `Cornerstones: 1 of 2 — E1.S3`.

## 1. Goal, before any solution

Before any model touches the task, three instruments must give numbers that are right by construction and checked by test:
**correctness** (did the cell's minigit do what the SPEC says — measured on a pristine copy, by tests the agent never saw, with
one defect counted once), **cost** (what the session cost at published prices, cross-checked three ways, never double-billing
a cache and never priced from fitted rates) and **time** (where the wall went, partitioned so the categories sum to the wall
and a wait never hides work). The original benchmark failed on all three (parent §2: O1 double-billed cache, O3 tests that
pass a wrong parent, O4 a swallowed build, O5 a test the agent could edit, O7 a crash reading as "no tests"). E1 builds the
instruments and proves them on fixtures; E2 isolates sessions, E3 runs them.

**Public repository, networked sessions (parent §5.1).** Nothing in this document, in code comments or in test fixtures names
a held-out case, a reference implementation or a mutant. E1.S2 is specified here by SHAPE; its concrete list lives outside
git at `/home/jinx/rsd/minigit/heldout-src/BRIEF_S2.md`, beside the clear sources it describes.

## 2. What exists — verified at `01b7459`, 2026-10-29

| Fact | Where |
|---|---|
| The envelope reader: last `result` event → `total_cost_usd`, `duration_ms`, `duration_api_ms`, `num_turns`, `modelUsage` | `spikes/policy-ab-quick/lib/summary.mjs:40-48` (`resultEnvelope`) |
| `summarize()` prices an envelope-less cell from FITTED rates: `costOf` calls `costFromMessages` | `summary.mjs:6` (import), `:140-145` (call at `:142`) |
| The child timeline (Agent launch → its `tool_result`), and the span sweep (max concurrent, overlap, union — never a sum) | `spikes/policy-ab-quick/lib/parallel.mjs:16-36` (`agentTimeline`), `:55-64` (`sweepSpans`) |
| `parallelism()` / `childCosts()` also call `costFromMessages` | `parallel.mjs:4` (import), `:88-98` (call at `:90`) |
| The fitted table: Opus `[2.5, 12.5, 0.25, 7]`, `costFromMessages` | `spikes/policy-ab-quick/lib/pricing.mjs:13`, `:26-40` |
| The stream cost meter also calls it | `spikes/policy-ab-quick/lib/watchdog.mjs:3`, `:26-28` (`CostMeter.usd`) |
| Seeded order, rate-limit markers, spend stop | `order.mjs:4` (`drawOrder`); `ratelimit.mjs:12` (`rateLimitSignal`); `spend.mjs:13` (`spendDecision`) |
| A single-PID kill on Linux (fixed in E2.S1, not here) | `spikes/policy-ab-quick/lib/proc.mjs:32-36` |
| Guarded recursive delete under a boundary | `spikes/policy-ab-quick/lib/safe-rm.mjs:52` (`removeUnder`) |
| Published prices (read 2026-10-12): Opus 5.5 4/20/0.20/5/8, Sonnet 5.5 2/10/0.10/2.50/4, Haiku 4.5; `cliRates` Sonnet cacheRead 0.20. **No Fable row** | `research/scout-calibration/prices.json:3-11` |
| The cost check's tolerance (2 %) and the CLI-billed table | `src/Bench.Domain/Scouts/ScoutCost.cs:187` (`Tolerance`), `:51-52` (`AsTheCliBills`); validation shape `:9-15`, `:41-48` |
| The pilot's Sonnet line: tokens 104 in / 31,113 out / 1,749,236 cache read / 176,496 cache write → $1.10243 at cacheRead 0.20 | `tests/Bench.Tests/Scouts/ScoutCliCostBasisTests.cs:72-73`, `:86` |
| Real streams with a 5m/1h split, two children on two models, an internal Haiku line, `hook_started`/`hook_response` (with `hook_id`, **no timestamp**) | `tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-two-children-models.ndjson` (README `:28`) |
| A real `model_refusal_fallback` (opus-5-5 → opus-4-8) | `tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-model-refusal-fallback.ndjson` |
| 1h-only cache writes from the CLI's main session | `tests/Bench.Tests/Fixtures/probes/claude-2.1.258-*.ndjson` |
| Spike stream fixtures (synthetic and real-shaped) | `spikes/policy-ab-quick/test/fixtures/{parallel.stream,usage-q2-c2,usage-q3-c3,stream,ratelimit-429}.ndjson` |
| **No** transcript (`projects/*.jsonl`, `subagents/*.jsonl`) and **no** OTel fixture exists anywhere in the repository | searched 2026-10-29 |
| The banteg C# rows: six blocks, Σ/3 = 2.7453537 at its formula, 0.6037003 corrected; v1/v2 total counts 11 / 30 | read-only clone of `banteg/minigit-bench@83644e0`, `results/results.json` (recomputed 2026-10-29); formula `benchmark.rb:162-167`, sums `:213-215` |
| Upstream tests: `set -e` `test-v*.sh:2`; swallowed build `:25-31`; both branches `pass` at `test-v2.sh:319-324`, `:381-385`; scripts call `../minigit` | the same clone |
| coai ledger line (camelCase): `utc, provider, model, role, stage, seconds, tokensIn, tokensOut, costUsd?, outcome, …, tokensCached, costNote, tokensReasoning, usageNote` | `dew_flow_connect_other_ais · src_mcp/runners/Reviewers/UsageLedger.cs:76-145`, `LedgerJsonContext.cs:15` |
| coai adds claude's cache tokens INTO `tokensIn` (codex's are already inside), so in the ledger `tokensCached ⊆ tokensIn` for both | `dew_flow_connect_other_ais · src_mcp/runners/Reviewers/ClaudeRuntime.cs:196-235` |
| coai `rounds(stage, number, verdict, started_utc, completed_utc, …)` | `dew_flow_connect_other_ais · src_mcp/src/Store/Schema.cs:52-77` |
| The lowering tool and its runner shape: `lowerOwnPriority()`, `testConcurrency = max(1, min(8, threads − 1))`, `exitCodeFor` | `.claude/rules/shared/tools/low-priority.mjs:43-48`, `:94`; `.claude/rules/shared/tools/run-tests.mjs:23-25`, `:36-43`; preload template `.claude/rules/shared/tools/templates/lower-priority-preload.mjs:14-26` |
| Toolchain on this machine: dotnet `/usr/lib/dotnet` (SDK 10.0.113), node 26.12.1 under `/home/jinx/.nvm/…`, python 3.14.4 `/usr/bin`, `/usr/bin/bwrap`, `/usr/bin/timeout` | checked 2026-10-29 |

## 3. Conventions for every story

- Plain ESM `.mjs`, Node 26, **no dependency** (`node:crypto`, `node:zlib`, `node:sqlite`, `node:http`, `node:test`). No
  `package.json`; CI does not run spikes.
- Functions < 50 lines, files ≤ 400 typical / 800 max, no mutation of inputs, expected failures as values
  (`{ ok: true, value } | { ok: false, reason }`), no silent `catch`, **absent is not zero** — a value that could not be
  measured is `{ known: false, why }` and renders `unknown`, never `0`.
- The new spike **never calls** `costFromMessages`, `summarize`, `parallelism`, `childCosts` or `CostMeter` (T-C1). It may
  import `resultEnvelope`, `agentTimeline`, `sweepSpans`, `drawOrder`, `rateLimitSignal`, `spendDecision`, `removeUnder`.
- Every test runs through `node spikes/minigit-bench/run-tests.mjs [glob…]` (created in E1.S1), lowered, capped at
  `max(1, min(8, availableParallelism() − 1))`. A test that needs `bwrap`, `python3` or the held-out key and cannot find it
  SKIPS WITH ITS REASON (`t.skip('…')`); a done-when that names such a test requires a run where it did not skip.
- Bug-fix order where a defect is reproduced: RED first, watched failing for the real symptom, then green; both observations
  in the story's summary (`common.testing`).

---

## E1.S1 — pinned inputs, the visible-test edits with their header, the scorer pipeline on a pristine copy (T-S1)

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus.

**Goal.** One command scores one finished session the same way for both arms: input hashes checked, a loud build from clean,
the harness's own fixed visible tests, the held-out slot (E1.S2 fills it), the upstream originals as reference only — every
agent-written process running in a no-network scoring sandbox that cannot see the operator's home.

**Files.**

| Path | Change |
|---|---|
| `spikes/minigit-bench/run-tests.mjs` | new — the spike's test runner |
| `spikes/minigit-bench/lib/lower-priority.mjs` | new — verbatim copy of `.claude/rules/shared/tools/templates/lower-priority-preload.mjs`, only the `MOUNT` line changed to `new URL("../../../.claude/rules/shared/", import.meta.url)` |
| fork `oleksandrdubyna88/minigit-bench`, branch **`inputs`** (from `main` = `mame@7f6ffdc`) | new — `upstream/{SPEC-v1.txt,SPEC-v2.txt,test-v1.sh,test-v2.sh}` byte for byte, and at the root the fixed visible `test-v1.sh`, `test-v2.sh` plus the SPECs; the upstream harness, results and `CLAUDE.md` removed. The four `template/*` branches merge it (E2). **Nothing from upstream is committed to `dew_flow_benchmark`** (parent D18) |
| `spikes/minigit-bench/inputs/hashes.txt` | new — `sha256sum` format: the four upstream files and the two fixed scripts (6 lines; E1.S2 appends the suite's) |
| `spikes/minigit-bench/inputs/UPSTREAM.md` | new — the fork, the pinned `inputs` sha, the upstream sha, the licence finding and the operator's D18 decision, the three edits in words |
| `spikes/minigit-bench/lib/fetch-inputs.mjs` | new — `fetchInputs({ sha, cacheDir })`: a single-branch clone of the fork's `inputs` branch into `~/.cache/minigit-bench/inputs/<sha>/` (outside every repo), checked out at the pinned sha, every file verified against `hashes.txt`; a mismatch is a `fail` naming the file, never a silent re-fetch |
| `spikes/minigit-bench/lib/inputs.mjs` | new — `parseHashes`, `verifyFiles`, `expectedTotal` |
| `spikes/minigit-bench/lib/visible.mjs` | new — `parseVisibleOutput`, `installShim`, `shimIntact` |
| `spikes/minigit-bench/lib/bwrap.mjs` | new — pure argv primitives and `scoreBoxArgv` (E2's `sandbox.mjs` composes the same primitives) |
| `spikes/minigit-bench/lib/score-pipeline.mjs` | new — `scoringPlan` (pure) and `runScoring(plan, deps)` (deps injected) |
| `spikes/minigit-bench/score-session.mjs` | new — CLI, writes `<session>/score/score.json` |
| `spikes/minigit-bench/test/{inputs,visible,bwrap,score-pipeline}.test.mjs`, `test/fixtures/stub-trees/…` | new |

**The change — what and how.**

1. **`run-tests.mjs`.** `await import('./lib/lower-priority.mjs')` first (the rule's runner-script shape,
   `common.local-cpu-budget` §2), then `spawnSync(process.execPath, ['--test', \`--test-concurrency=${n}\`, ...globs],
   { cwd: <spike dir from import.meta.url>, stdio: 'inherit' })` with `n = max(1, min(8, availableParallelism() − 1))`
   and default glob `test/**/*.test.mjs`; exit with the child's code, or 128 + signal number (the shape of
   `.claude/rules/shared/tools/run-tests.mjs:36-43`; write it locally, do not import the mount's runner).
2. **Inputs.** In the fork (worktree of branch `inputs`): `upstream/` gets the four files byte for byte from
   `mame@7f6ffdc`; the root gets the SPECs and the fixed scripts (step 3). `UPSTREAM.md` in this repository records the
   licence finding and the operator's decision (parent D18: the files live only in the fork, under GitHub ToS D.5).
   Tests that need the scripts call `fetchInputs` (network on first use, then the cache).
3. **Visible edits** (each new file starts, after the shebang, with a header comment naming `mame/ai-coding-lang-bench@7f6ffdc` (byte-identical to `banteg@83644e0`),
   the upstream file's sha256, and the edits):
   - *Build loud* — replace the build block (`:25-31` in both) with
     `make -s || { echo "BUILD FAILED"; echo "PASSED: 0"; echo "FAILED: 1"; exit 1; }` and the same for `bash build.sh`;
     `2>/dev/null` removed so stderr shows; the `chmod +x minigit` line kept.
   - *Crash-safe* — delete `set -e` (`:2`); add `EXPECTED_TOTAL=<n>` near the top (11 / 30, written by hand **and**
     asserted equal to `expectedTotal(upstream)` in a test); before the summary, if `PASS_COUNT + FAIL_COUNT <
     EXPECTED_TOTAL`, print `FAIL: <k> tests did not run` and add `k` to `FAIL_COUNT`.
   - *Parent* (v2 only) — in Test 20's and Test 24's inner `else` (`:321-324`, `:383-385`) replace `pass "…"` with
     `fail "… (parent is not $COMMIT1)"` / `fail "… (parent is not $COMMIT2)"`.
4. **`lib/inputs.mjs`.** `parseHashes(text)` → `ok` with `[{ sha256, path }]`, or `fail` naming the first malformed line or
   a duplicate path. `verifyFiles(root, entries, readFile)` → per entry `{ path, expected, actual|null, ok }` (a missing
   file is `actual: null`, not a mismatch with `''`). `expectedTotal(scriptText)` = the count of `^# Test \d+:` headers.
5. **`lib/bwrap.mjs`.** Pure builders returning argv arrays: `roRoot()` → `['--ro-bind','/','/']`, `tmpfs(p)`, `bind(p)`,
   `roBind(p)`, `devProc()` → `['--dev','/dev','--proc','/proc']`, `isolation()` →
   `['--unshare-net','--unshare-pid','--die-with-parent','--new-session']`, `setenvAll(env)` (`--clearenv` then one
   `--setenv` per key). `scoreBoxArgv({ copyDir, casesDir, home, tmp, toolchainDirs, cacheDirs })` =
   `bwrap` + roRoot + devProc + `tmpfs('/home/jinx' → os.homedir())`, `tmpfs('/mnt')`, `tmpfs('/run/WSL')` (when present) +
   `roBind` each toolchain dir (node lives under the home) + `bind` copyDir, casesDir, home, tmp, each cache dir +
   isolation + setenvAll(scoreEnv) + `--`. `scoreEnv` is a fixed allow-list, never the operator's environment: `PATH`
   (toolchain bins, `/usr/bin:/bin`), `HOME`, `TMPDIR`, `LANG=C.UTF-8`, `DOTNET_CLI_HOME`, `DOTNET_NOLOGO=1`,
   `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `MSBUILDDISABLENODEREUSE=1`,
   `UseSharedCompilation=false`, `NUGET_PACKAGES`, `npm_config_cache`, `npm_config_offline=true`. **Why a sandbox here:**
   the scorer runs agent-written code as the operator after the session, possibly while the partner cell still runs;
   without it the code could read the held-out suite, the key or another cell's tree and reach the network.
   **Why `--unshare-pid --die-with-parent`:** a SIGKILL of the `bwrap` PID (what `proc.mjs:35` sends) ends the namespace's
   init and with it every grandchild — a timed-out `dotnet` child cannot keep a pipe open. No `proc.mjs` change in E1.
6. **`lib/visible.mjs`.** `parseVisibleOutput(stdout, expectedTotal)` → `{ summaryCaptured, passed, failed, total,
   buildFailed, failLines }`; no summary → `summaryCaptured: false, passed: 0, failed: expectedTotal` (scored as all failed,
   **never** "0 of 0"). `installShim(copyDir)`: rename `minigit` → `.minigit-scored-real`, write `minigit` =
   `#!/bin/sh\nexec timeout -k 2 ${s} "$(dirname "$0")/.minigit-scored-real" "$@"\n` (mode 0755), return the shim's
   sha256; `shimIntact(copyDir, sha)` after the run (the scripts re-run `make -s`, and a phony target can overwrite the
   shim — then the whole-script bound below still holds and `score.json` says `perInvocationTimeout: "not enforced"`).
7. **`lib/score-pipeline.mjs`.** `scoringPlan({ arm, stack, phase, sourceDir, sessionDir })` → the ordered steps; the
   executor takes `deps = { run, copyTree, readFile, now, heldout }` so tests inject stubs. Steps, each recorded, none
   skipped silently:
   1. *copy* — `sourceDir` → `<session>/score/copy` minus `bin/ obj/ node_modules/` (process: E3 passes a fresh clone of
      the base tip; bare: the workspace).
   2. *inputs* — `verifyFiles` over the SPEC/test files the copy carries vs `hashes.txt` → `inputsTampered: [paths]`.
   3. *build* — in the box, cwd = copy: `make -s` if `Makefile|makefile`, then `bash build.sh` if present, then
      `chmod +x minigit`; 600 s; `{ ok, exitCode, timedOut, ms, logTail }`. `ok` also requires an executable `minigit`.
   4. *visible* — fresh `cp -a` of the built copy per script, the HARNESS copy of `test-v1.sh` (v1) or `test-v2.sh` then
      `test-v1.sh` (v2) dropped in, shim installed (10 s), `bash <script>` in the box with a whole-script limit of
      `600 + 10 × (count of '../minigit' in the script)` s.
   5. *heldout* — `deps.heldout({ binary, phase, box })` (E1.S2); before S2 lands a stub returning `{ known: false }`.
   6. *upstream* — the same as step 4 with the upstream originals; marked `referenceOnly: true`.
   7. *verdict* — `accepted` = build ok ∧ every visible script 100 % ∧ held-out primary 100 % ∧ (process) delivered.
      Delivery is E3's (GitHub); until supplied, `accepted: { known: false, missing: ['delivered'] }` — never `false`.
   A failed build short-circuits steps 4–6 to "every case failed, reason build failed" with their expected totals.
8. **`score-session.mjs`.** `--session <dir> --source <dir> --arm process|bare --stack csharp|node --phase v1|v2
   --toolchain <dir>… [--cache <dir>…]`; prints one line per step and writes `score.json`. Exit 0 when scored (whatever
   the score), 2 on a usage error, 3 when the scorer itself failed (named).

**Test scenarios (T-S1)** — stub trees are a few lines of bash each, never an implementation of the SPEC.

| Test | Input | Expected |
|---|---|---|
| `The_upstream_build_block_swallows_a_failing_make_and_that_is_the_point` | upstream `test-v1.sh` in a tree whose `Makefile` exits 2 and has no `minigit` | the script runs on and prints `FAIL:` lines; no `BUILD FAILED` |
| `A_failing_build_is_loud_in_the_harness_scripts` | the same tree, harness `test-v1.sh` | stdout has `BUILD FAILED`, `PASSED: 0`, `FAILED: 1`; exit 1; stderr shows make's message |
| `The_upstream_set_e_reports_nothing_after_a_crash_and_that_is_the_point` | stub `minigit` that exits 139 on `add` | upstream: no `PASSED:` summary |
| `A_crash_mid_script_still_counts_every_test` | same stub, harness script | summary present, `TOTAL` ≥ 11 counting the not-run line, `FAILED` ≥ 1 |
| `The_fixed_test_v2_fails_a_commit_whose_parent_is_wrong` — **RED first** | the Test 20 block extracted (between its header and the next) and run with `COMMIT1=aaaa…`, a stub `minigit` whose `commit` writes `.minigit/HEAD` and `.minigit/commits/<h>` with `parent: bbbb…` | RED: run against an unedited copy of upstream → `PASS:`; GREEN after the edit → `FAIL: … (parent is not aaaa…)`; same for Test 24 with `COMMIT2` |
| `Expected_totals_are_derived_from_the_scripts_and_match_upstream_results` | `expectedTotal` of upstream v1/v2; `v1_total_count`/`v2_total_count` of the T-A1 fixture rows | 11 and 30, from both sources; the harness `EXPECTED_TOTAL` literals equal them |
| `Every_pinned_input_matches_hashes_txt` | `inputs/` vs `hashes.txt` | every entry ok; a one-byte edit in a temp copy → that path `ok: false` |
| `A_hashes_file_with_a_malformed_or_duplicate_line_is_refused_naming_it` | `"zz  a"`, two lines for one path | `fail` naming line 1 / the path |
| `The_scorer_runs_its_own_test_scripts_not_the_trees_copy` | tree whose own `test-v1.sh` prints `PASSED: 11 FAILED: 0`; stub `minigit` that prints nothing | `inputsTampered: ['test-v1.sh']`; visible `passed < 11` (the harness script ran) |
| `A_build_failure_fails_every_case_and_the_session` | failing `Makefile` | build `ok: false`; visible `failed == expectedTotal`; held-out "build failed"; `accepted.value` false |
| `A_missing_summary_scores_as_all_failed_never_zero_of_zero` | stdout without `PASSED:` | `summaryCaptured: false`, `failed == expectedTotal` |
| `An_invocation_that_hangs_is_ended_at_its_limit_with_its_grandchildren` (needs bwrap) | stub `minigit` = `sleep 300 & wait`, env `BENCH_SCORE_MARK=<rand>`, limit 1 s (the 10 s is a parameter) | ended within 4 s; no `/proc/*/environ` carries the mark afterwards |
| `A_rebuilt_shim_is_reported_not_hidden` | `Makefile` with a phony target that rewrites `minigit` | `shimIntact: false`, `perInvocationTimeout: 'not enforced'` |
| `The_scoring_box_has_no_network_and_no_operator_home` (needs bwrap) | in the box: `ls ~` (real home), a read of `~/.config/minigit-bench/heldout.key`, `bash -c 'exec 3<>/dev/tcp/1.1.1.1/443'` | all three fail |
| `The_scoring_box_can_build_and_write_where_it_must` (the positive companion) | in the box: write a file in copyDir and casesDir, run `node --version` | succeed |
| `The_score_env_is_the_allow_list_not_the_parents` | parent env with `GH_TOKEN`, `CLAUDE_CODE_OAUTH_TOKEN` | neither in `scoreBoxArgv`'s `--setenv` pairs |
| `Accepted_is_unknown_not_false_while_delivery_is_unknown` | process arm, everything green, no delivery | `{ known: false, missing: ['delivered'] }` |

**Commands.** `node spikes/minigit-bench/run-tests.mjs` (whole suite);
`node spikes/minigit-bench/run-tests.mjs test/visible.test.mjs` (one file).

**Done when.** All T-S1 tests green with none skipped, the RED observation of the parent test recorded with its message;
`hashes.txt` lists the six files and `fetchInputs` verifies them from the fork at the pinned `inputs` sha; the licence
finding and D18 are in `UPSTREAM.md`; no upstream byte is in `dew_flow_benchmark`'s diff (a test greps the spike for the
SPECs' first lines and finds none).

---

## E1.S2 — the held-out suite, encryption at rest, references, mutants, the adjudication procedure (T-S2)

**Model:** scenarios + brief Opus (this text and the outside brief) · code Sonnet · review Opus.

**Goal.** A held-out suite written from the SPEC before any cell, stored in git ONLY as ciphertext, decrypted only into a
scoring directory the session sandboxes cannot see, calibrated on two reference readings and planted mutants, and an
adjudication tool that classes failures blind before it reveals the code.

**The suite, by shape** (parent §5.3; the case list, the two references and the mutants are specified in
`/home/jinx/rsd/minigit/heldout-src/BRIEF_S2.md`, outside git, and live as clear sources only in that directory):

- A Python runner (`python3 -I`), one temp repository per case, every commit staging every working file so both readings of
  "commit = index" give one tree. Each case names the SPEC sentence it asserts (file and line range) and checks stdout, exit
  code and the resulting `.minigit` state; crash, timeout and a missing binary each count as that case failed.
- **H1** — at least one case per SPEC-v1 command and per SPEC-v1 error message, MiniHash on edge inputs, exact output and file
  formats, state kept per working directory. **H2** — at least one case per SPEC-v2 command and error message, plus all of
  H1 against the v2 binary. **Secondary, not scored** — robustness inputs outside the SPEC and per-invocation latency
  (median of 100 runs of one read-only command, beside the same box's baseline on `/bin/true`).
- **No format the SPEC leaves open is assumed:** state is built with the cell's own `init`/`add`/`commit`, hashes are taken
  from the cell's own output, and where the SPEC leaves a detail open (trailing newlines and raw-blob bytes among them, parent
  §5.3) the case accepts every reading; the full list of open details is in the outside brief. A case declares which core H1 cases its setup needs; when
  one of those failed on this binary the case is **`blocked by H1-<k>`**: reported apart, neither passed nor failed. Blocking
  applies only when the binary ran — a missing binary or a failed build fails every case.
- Two Python references (one per commit reading) and M ≥ 8 mutants of one reference, each a plausible defect of a different
  SPEC clause with its pre-registered failing (and blocked) set per phase.

**Files.**

| Path | Change |
|---|---|
| `/home/jinx/rsd/minigit/heldout-src/` | OUTSIDE git: `runner.py`, `cases/`, `refs/a/`, `refs/b/`, `mutants/*.json` (one find-and-replace edit of reference A each, `find` occurring exactly once), `registry.json`, `leak-tokens.txt` — written per `BRIEF_S2.md` |
| `spikes/minigit-bench/lib/heldout-crypto.mjs` | new — key file checks, `seal`, `unseal` |
| `spikes/minigit-bench/lib/heldout-archive.mjs` | new — deterministic `packDir`, safe `unpackTo` |
| `spikes/minigit-bench/lib/heldout-run.mjs` | new — runs the decrypted runner, `heldoutScore` |
| `spikes/minigit-bench/lib/adjudicate.mjs`, `spikes/minigit-bench/adjudicate.mjs` | new — the two-step blind classing |
| `spikes/minigit-bench/heldout-tool.mjs` | new — `keygen`, `seal`, `calibrate`, `leakcheck`, `counts`, `sweep` |
| `spikes/minigit-bench/heldout/{suite.enc,suite.enc.sha256,manifest.json,calibration.json}` | new — ciphertext, its hash, `{ format, plaintextSha256, n1, n2, m, sealedOn }`, per-reference and per-mutant booleans (ids only) |
| `spikes/minigit-bench/inputs/hashes.txt` | append `heldout/suite.enc` |
| `spikes/minigit-bench/lib/score-pipeline.mjs` | wire `deps.heldout` to `heldout-run.mjs` |
| `spikes/minigit-bench/test/{heldout-crypto,heldout-archive,heldout-run,heldout-calibration,adjudicate,repo-scan}.test.mjs` | new |

**The change — what and how.**

1. **Key.** `heldout-tool.mjs keygen` writes `randomBytes(32)` to `~/.config/minigit-bench/heldout.key` with
   `{ mode: 0o600, flag: 'wx' }` (never overwrites; the directory created `0o700`). `readKey(path)` →
   `ok(Buffer)` only when it is a regular file, owned by the current uid, `mode & 0o077 === 0`, exactly 32 bytes; otherwise a
   `fail` naming which. **The key is never read from, or written to, an environment variable** — no `process.env` access in
   `heldout-crypto.mjs`, `heldout-tool.mjs` or `heldout-run.mjs`; the scorer reads the file itself, outside every sandbox. A
   `--key-file` argument exists for tests only.
2. **Seal / unseal** — AES-256-GCM via `node:crypto`: `suite.enc` = magic `MGHO1\n` (6 bytes) ‖ 12-byte random IV ‖
   ciphertext ‖ 16-byte tag, AAD = the magic. `unseal` returns `fail('authentication failed')` on any tamper or wrong key —
   a value, never a throw past the boundary.
3. **Archive** — `packDir(dir)`: walk, sort relative POSIX paths, entries `{ path, mode: 0o755|0o644, data: base64 }`, JSON
   with no timestamps, `gzipSync` → identical bytes for identical trees (`plaintextSha256` is reproducible; the ciphertext is
   not, because of the IV). `unpackTo(buf, dir)` refuses an absolute path, `..`, a symlink entry, or a target outside `dir`.
4. **Where the clear suite exists during scoring** — `heldout-run.mjs` decrypts into
   `<workRoot>/score-tmp/<rid>-<rand>/` (`workRoot` defaults to `~/rsd/minigit`), created `0o700`; it refuses a work root
   under `os.tmpdir()` or outside `os.homedir()` (the session sandbox replaces the home with tmpfs, parent §7.1, so a running
   partner cannot see it; `/tmp` is not promised hidden). Removed in `finally` through `removeUnder` (`safe-rm.mjs:52`) even
   when the runner throws; `heldout-tool.mjs sweep` removes leftovers older than 24 h after a crash.
5. **Runner protocol** (the contract the clear runner implements; the toy test suite implements it too):
   `python3 -I <dir>/runner.py --phase v1|v2 --binary <abs> --work <casesDir> --box <json argv prefix> --timeout 10
   --out <result.json>`. Each minigit invocation is `box + [binary, ...args]` with cwd = the case's repo; on timeout the
   runner kills the `bwrap` PID (the namespace collapses, E1.S1 §5). `result.json` =
   `{ format: 1, phase, python, cases: [{ id, group: 'H1'|'H2'|'R', scored, status: 'pass'|'fail'|'blocked',
   reason?: 'mismatch'|'crash'|'timeout'|'missing'|'setup', blockedBy?, spec, captured: [{ argv, exit, stdout, stderr }] }],
   latency: { command, runs, medianMs, boxBaselineMedianMs } }`. It stays in `<session>/score/`, outside git.
6. **`heldoutScore(result)`** (pure): per group `{ total, passed, failed, blocked }` over `scored` cases;
   `primary100 = failed === 0 && blocked === 0` (blocked counts against acceptance, never as a second failure); secondary
   cases and latency beside it, never in the percentage. An unreadable `result.json` → `{ known: false, why }`.
7. **`calibrate`** — decrypt; for each reference and each mutant (a copy of reference A with its one edit applied; an edit whose `find` does not occur exactly once is refused), run both phases and the harness visible scripts through the E1.S1 pipeline;
   compare `{ failing, blocked }` per phase with `registry.json`; write `heldout/calibration.json` with ids and booleans only
   and print the diff for any mismatch. A mismatch is resolved by fixing the case or the mutant and re-sealing, and the
   resolution is written in the clear source dir — never by copying the observed set into the registry.
8. **`leakcheck`** — decrypt; read `leak-tokens.txt` (case literals, reference identifiers, mutant markers, ≥ 8 chars each);
   scan every file `git ls-files --cached --others --exclude-standard` lists in this repository; any hit fails naming the
   file (never printing the token). Run before every commit of E1.S2 and in its done-when.
9. **Adjudication (pre-registered, parent §5.3).** `adjudicate.mjs packets --series <dir>` builds, for every failing scored
   case of every cell, a packet `{ blindId, caseId, spec, captured }` with stack and arm removed and stack-revealing
   strings in captured text masked by named rules (runtime names, stack-trace frames, paths) — the list printed with the
   packets. `blindId` = 8 random hex; the map to cells lives in `<series>/adjudication/map.json`. Step 1 (`classify`)
   records `spec-ambiguity | not-ambiguous` + a reason per blindId from the packet alone. `reveal` refuses until every
   packet has a step-1 verdict, then adds the cell's code path; step 2 classes `product | harness`. `revised` drops every
   case classed ambiguous for ALL cells and is written beside the primary score, which is never rewritten. Packets, map and
   verdicts live in the series directory outside git (they contain case text); the committed report (E4) carries ids and
   counts until the case list is published after the series.
10. **Authoring order (outside git):** write the clear sources per `BRIEF_S2.md`; `calibrate` until both references are
    100 % and every mutant matches; hand-read 20 case verdicts against the SPEC (measurement rule 2) and note them in the
    clear dir; `seal`; `leakcheck`; commit only `heldout/*` and the `hashes.txt` line.

**Test scenarios (T-S2)** — a TOY suite (a three-line program, unrelated to minigit) is generated inside each test's temp
dir from JS strings, so no `.py` file is committed and nothing about the real suite is.

| Test | Input | Expected |
|---|---|---|
| `A_sealed_suite_round_trips_byte_for_byte` | toy dir → pack → seal → unseal → unpack | identical tree; `plaintextSha256` equal across two packs, ciphertexts differ |
| `A_single_flipped_byte_or_another_key_fails_authentication_as_a_value` | flip one ciphertext byte; a second key | `fail('authentication failed')`, nothing unpacked |
| `A_key_file_readable_by_group_or_others_is_refused` | modes 0644, 0640; 31 and 33 bytes; a symlink | each `fail` naming mode / size / not a regular file |
| `The_key_is_never_taken_from_the_environment` | key hex in `process.env.MINIGIT_HELDOUT_KEY`, no key file | `fail` naming the missing file |
| `No_heldout_module_reads_process_env` + `The_env_scan_still_finds_a_known_reader` | scan of the three modules; same scan over `lib/score-pipeline.mjs` | none; one hit (the scan is alive) |
| `An_archive_entry_escaping_its_directory_is_refused` | entries `../x`, `/etc/x`, a symlink | `fail` each; nothing written outside |
| `The_clear_suite_is_decrypted_only_under_the_home_and_removed_after_scoring` | work root under `os.tmpdir()`; a valid root with a runner that throws | first refused; second: the dir was `0o700` while running and is gone after |
| `A_missing_binary_fails_every_case_and_blocks_none` | toy runner, `--binary` pointing nowhere | every scored case `fail: missing`, `blocked == 0` |
| `A_hanging_binary_fails_its_case_on_timeout` (needs bwrap, python3) | toy binary sleeping 60 s, timeout 1 s | that case `fail: timeout`; the run ends |
| `A_blocked_case_is_neither_passed_nor_a_second_failure_and_still_denies_acceptance` | result with 1 fail (core), 3 blocked | `failed 1, blocked 3, primary100 false` |
| `Both_references_pass_and_every_mutant_fails_exactly_its_registered_cases` (toy) | toy suite: 2 refs, 2 mutants, registry | calibration all true |
| `A_mutant_registered_with_the_wrong_set_fails_calibration_naming_it` | toy registry with one case swapped | `fail` naming the mutant, the missing and the extra case |
| `The_real_suite_calibrates` (needs the key; local only) | `suite.enc` | both references 100 % per phase; every mutant exact; counts equal `manifest.json` |
| `Leakcheck_finds_a_planted_token_and_passes_a_clean_tree` | temp git repo, toy suite's token planted in one file | the file named; after removal, clean |
| `No_clear_heldout_source_is_in_the_spike` + `The_scan_sees_the_sealed_suite` | walk `spikes/minigit-bench/` | no `*.py`, no `heldout/**` but the four committed files; the walk lists `heldout/suite.enc` |
| `Adjudication_packets_carry_no_stack_or_arm` | two cells' toy failures | no `csharp`/`node`/`process`/`bare` or masked marker in packets; the map file holds them |
| `Reveal_is_refused_until_every_packet_has_its_step_one_verdict` | one packet unclassified | `fail` naming the blindId |
| `A_case_classed_ambiguous_is_dropped_for_every_cell_and_the_primary_is_kept` | one case ambiguous, failing in one of two cells | revised drops it in both; primary unchanged |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/heldout-*.test.mjs test/adjudicate.test.mjs
test/repo-scan.test.mjs`; `node spikes/minigit-bench/heldout-tool.mjs calibrate` and `… leakcheck` (key present).

**Done when.** T-S2 green, `The_real_suite_calibrates` run (not skipped) and green; `leakcheck` clean on the commit;
`manifest.json` carries `plaintextSha256`, N1, N2, M; `hashes.txt` carries `suite.enc`; the 20-verdict hand read is noted
in the clear dir and its count in the PR; no clear source, reference or mutant in any commit.

---

## E1.S3 — Claude counters: E/O/T views, dated prices, OTel receiver, T1–T4, the alias record (T-A1–T-A5, T-M1, T-C1)

**Cornerstone — `model: fable`.** Area: the accounting and time model (parent `## Epics`, E1, first-ranked). Marked here
because the headline USD and the T1–T4 cross-checks with their conditional T3 stop are the plan's first claim — "correctly
counted cost" — and the exact place the original went wrong (O1). **The scenarios and brief below are an Opus DRAFT; Fable
rewrites them before any code.** Code Sonnet · review Opus.

**Goal.** From one session's records — E (result envelope), O (OTel `api_request`), T (transcripts) — one headline USD at
published prices with its basis, the CLI's own total beside it, per-model tokens, T1–T4 with their verdicts, and the alias
record of D12; never a fitted rate, never stream output tokens, never a zero for an unpriced model.

**Files (draft).** `spikes/minigit-bench/lib/prices.mjs`, `lib/claude-usage.mjs`, `lib/otel-receiver.mjs` (server + live
sum) with `lib/otel-decode.mjs` (pure), `lib/crosscheck.mjs` (T1–T4), `lib/aliases.mjs` (D12);
`spikes/minigit-bench/inputs/prices-2026-10-12.json` (dated copy of `research/scout-calibration/prices.json`; the series'
day-1 re-read is E3's and must add every model the A/A and P1 used — Fable has no row today);
`test/fixtures/banteg-83644e0-csharp.json` (the six C# blocks' four token fields, `cost_usd`, `v*_total_count`, with source
sha and path); synthetic `test/fixtures/otel-*.json` and `test/fixtures/transcript-*/` labelled SYNTHETIC with the source of
their shape; `test/{prices,claude-usage,otel,crosscheck,aliases,no-fitted-rates}.test.mjs`.

**The change (draft).**

1. **Prices** — `loadPrices(json)` validates as `ScoutCost.cs:9-15`, `:41-48` (https source, ≥ 1 model, unique ids,
   non-negative) and `cliRates` as `:36-39`; `asTheCliBills(table)` mirrors `:51-52`. Lookup by exact model id; an
   unpriced id → `{ known: false, why: 'no price for <id>; priced: …' }`. `priceOpenAiShaped({ input, cached, output },
   rates)` = `(input − cached)·in + cached·cachedRate + output·out` (cached ⊆ input) — used by T-A1 only.
2. **E view** — `envelopeView(events)` over `resultEnvelope` (`summary.mjs:40`): per model `inputTokens, outputTokens,
   cacheReadInputTokens, cacheCreationInputTokens, costUSD`; envelope `usage` is never used for totals.
3. **T view** — `transcriptView(files)`: main + `subagents/*.jsonl`, group assistant lines by `message.id`, assert
   duplicates agree on input and cache fields (disagreement → a finding, not a silent pick), read
   `usage.cache_creation.ephemeral_5m_input_tokens` / `…_1h_…`; per model the 1h share of cache writes. T's
   `output_tokens` are used only after the smoke shows they are final (parent §9).
4. **Stream view** — `streamView(events)`: deduped by message id, `output` marked `placeholder`; nothing prices from it
   except `streamMeterUsd` (published prices, output floor per message, labelled `estimate`) for E2's cost watchdog.
5. **Headline** — `headlineUsd(E, T, prices)`: Σ models `(in·Pin + out·Pout + cr·Pcr + w5·P5m + w1·P1h)/1e6`, tokens from E,
   `w1 = cacheCreation × share1h(T)`; a model with no split in T → `{ low: all at 5m, high: all at 1h, exact: false }`. No E
   → `fail('no envelope')`; E2/E3 then price from O at published prices and flag `no envelope`.
6. **OTel** — `otel-decode.mjs`: OTLP/HTTP JSON `resourceLogs[].scopeLogs[].logRecords[]` → `{ name, tsMs, attrs }`
   (`intValue` strings → safe integers, `doubleValue`, `stringValue`, `boolValue`; `timeUnixNano` → ms). Attributes named
   by the plan (`query_source`, `agent_id`) are read when present and `unknown` otherwise — their existence is verified at
   E3's smoke, not assumed. `otel-receiver.mjs`: `127.0.0.1` only, `POST /v1/logs` and `/v1/metrics`, JSON only (415
   otherwise, recorded as `otel misconfigured`), body ≤ 8 MB, appends decoded records to `<session>/otel.ndjson`, keeps the
   running Σ `cost_usd` of `api_request`, exports `otelEnv(port)` for E2's argv (`CLAUDE_CODE_ENABLE_TELEMETRY=1`,
   `OTEL_LOGS_EXPORTER=otlp`, `OTEL_EXPORTER_OTLP_PROTOCOL=http/json`, `OTEL_EXPORTER_OTLP_ENDPOINT`,
   `OTEL_LOGS_EXPORT_INTERVAL=1000`). `env.mjs:4` drops `^OTEL_`, so E2 adds these after the scrub.
7. **T1–T4** — `crosscheck(E, O, T, prices, { t3Armed, t3OutputSource })`: T1 Σ`costUSD` vs `total_cost_usd` ± $0.01;
   T2 O vs E per model and kind ≤ max(0.5 %, 100 tokens), cost ≤ 1 % → breach = `otel incomplete`, cell kept; T3 T at
   `asTheCliBills` vs E ≤ `ScoutCost.cs:187`'s 2 %, T4 T input/cache vs E ≤ 0.1 % — both over the models and
   `query_source`s T records only; T3 is `investigate` until `t3Armed` (E3 arms it after the A/A and P1), then `stop`.
8. **Aliases (D12)** — `aliasRecord(session)` maps `opus` (main), each child's requested alias → resolved id
   (`agentTimeline`, `parallel.mjs:16`), `internal` (modelUsage ids neither main nor a child answered with);
   `checkAliases(record, session)` → `stop` on a new id for an alias the session used, nothing for an alias it did not use,
   `flag` on `system/model_refusal_fallback`.

**Test scenarios (draft).** T-A1 `The_upstream_formula_bills_cached_input_twice_and_that_is_the_point` (fixture rows at
5/0.50/30 with `input·5 + cached·0.5 + out·30` → Σ/3 = 2.74535, renders $2.75) and `Cached_input_is_billed_once` (the same
rows through `priceOpenAiShaped` → 0.6037, $0.60). T-A2 a stream repeating one `message.id` ×3 with placeholder output
dedupes to one message and `headlineUsd` refuses it without E. T-A3 on the two-children fixture: totals from `modelUsage`
only, the Sonnet child's model present, envelope `usage` ignored. T-A4 a split message prices exactly; an unsplit model
yields `{ low, high }` with `high − low = w·(P1h − P5m)`. T-A5 the pilot's Sonnet line (104 / 31,113 / 1,749,236 / 176,496)
gives $1.10243 at `asTheCliBills` and $0.92750 at published, the gap exactly 1,749,236 × $0.10/M. T-M1 a second session
whose `sonnet` child resolved to a new id stops; one that launched no `sonnet` child does not; the fallback fixture flags
and does not stop. T-C1 `The_spike_never_calls_a_fitted_rate_path` (scan of `spikes/minigit-bench/**/*.mjs` outside `test/`
for `costFromMessages|CostMeter|summarize|parallelism|childCosts` as imported or called names) +
`The_fitted_rate_scan_still_finds_its_known_caller` (the same scan over `policy-ab-quick/lib/summary.mjs` hits `:142`) +
`An_envelope_less_session_gets_no_number_from_fitted_rates`. Also: an unpriced model reads `unknown`, never 0; OTLP
`intValue` strings decode; a protobuf POST is refused 415 and recorded.

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/prices.test.mjs test/claude-usage.test.mjs test/otel.test.mjs
test/crosscheck.test.mjs test/aliases.test.mjs test/no-fitted-rates.test.mjs`.

**Done when (draft).** T-A1–T-A5, T-M1, T-C1 and the S3 half of T-T3 green; T-A1 prints 2.74535 → 0.6037; every synthetic
fixture labelled and its shape's source named; Fable's rewritten spec replaced this draft before the first line of code.

---

## E1.S4 — the coai usage ledger and the exclusive work-before-wait time partition (T-A6, T-T1–T-T3)

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus. Not marked: the area's one mark is on E1.S3; this
story gets the strict spec instead.

**Goal.** (a) The reviewers' consumption per provider, model, kind, role and stage, in tokens, with cost either a number or
`unknown` — never 0, never added to Claude's dollars; (b) the wall partitioned into categories that sum to it exactly, work
before waits, across all agents, with inclusive unions, agent-seconds and the pairwise overlap beside it — never a sum of
spans.

**Files.**

| Path | Change |
|---|---|
| `spikes/minigit-bench/lib/coai-usage.mjs` | new — `readLedger`, `ledgerSummary`, `roundsSpans` |
| `spikes/minigit-bench/lib/classify.mjs` | new — `classifyCommand`, `classifyTool` |
| `spikes/minigit-bench/lib/timeline.mjs` | new — `spansFrom`, `partition`, `inclusive`, `agentSeconds`, `overlapMatrix`, `crossCheckTools`, `mapClock` |
| `spikes/minigit-bench/test/{coai-usage,classify,timeline}.test.mjs`, `test/fixtures/coai-usage-*.jsonl`, `test/fixtures/timeline-*.json` | new (synthetic, labelled) |

**The change — what and how.**

1. **Ledger** — `readLedger(text)`: one JSON object per line, camelCase as `UsageLedger.cs:76-145`. Per row:
   `cached = tokensCached` when the field is present, else `unknown` (an old line, not 0); `uncached = tokensIn − cached`
   when known; `cached > tokensIn` → row flag `cached exceeds input` (the O1 trap; coai's own normalisation makes
   `cached ⊆ in` for claude and codex rows, `ClaudeRuntime.cs:196-235`). `usageNote === 'usage not captured'` → counted
   apart, its tokens a lower bound. Unparseable line → a finding with its line number, never dropped.
   `ledgerSummary(rows)` groups by `provider, model, kind, role, stage`: launches, Σ seconds, tokens in / out / cached /
   reasoning, and USD: every `costUsd` null → `'unknown (subscription)'`; mixed → `{ knownUsd, unknownCount }`; never a 0
   for a null. The summary's type has no field that could be added to a Claude dollar.
2. **coai rounds** — `roundsSpans(dbPath)`: `new DatabaseSync(dbPath, { readOnly: true })` (`node:sqlite`) over the
   `VACUUM INTO` copy E3 makes; `SELECT stage, number, verdict, started_utc, completed_utc FROM rounds` → `coai_gate` spans
   (`Schema.cs:52-77`). A missing table or file → `{ known: false }`.
3. **Classifier** — `classifyCommand(cmd)`: split on `&&`, `||`, `;`, `|` at top level; `build_test` when any segment is
   `dotnet build|restore|run|test|format`, a `*.Tests` executable, `bash test-v*.sh`, `make`, `npm ci|test|run
   (test|typecheck|lint)`, `node --test`, `node scripts/run-tests.mjs`, `tsc`; `sleep300` only for a command that is exactly
   `sleep 300` after trimming; `wait` for `gh pr checks … --watch`, `gh run watch`, `sleep <n>` and `until|while … sleep`
   loops; otherwise `other_tool`. `classifyTool(name, input)`: `mcp__coai__review_plan|review_code|resolve` →
   `coai_gate`, `mcp__coai__consult|ask_consultants|close_consult` → `consultant`, `Bash` → `classifyCommand`, `Agent`/`Task`
   → `container` (excluded), else `other_tool`. A generic `wait` becomes `ci_wait` where it intersects GitHub's in-progress
   CI intervals and `coderabbit_wait` where it intersects push → review intervals (both inputs from E3's `github.mjs`;
   absent → the wait stays `unattributed` and the report says why).
4. **Spans** — `spansFrom({ otel, audit, stamped, rounds, github })`: `model_api` = O `api_request` `[tsMs − duration_ms,
   tsMs]` (that the record time is the request's END is checked at the smoke); tool spans = audit `PreToolUse` →
   `PostToolUse|PostToolUseFailure` paired by `tool_use_id` (the record shape E2.S1's audit hook writes), category from
   `classifyTool`; `hooks` = stream `hook_started` → `hook_response` paired by `hook_id`, timed by the harness's ARRIVAL
   stamp of each stream line (the stream carries no timestamp for them — verified on the two-children fixture; E3's runner
   stamps every line `{ t, line }` through `mapClock`). `mapClock({ hrAtSpawn, epochAtSpawn })` maps the launcher's
   monotonic clock to epoch; a skew > 1 s between the mapping at spawn and at exit is a flag.
5. **Partition** — `partition(wall, spans)`: clip every span to `[wall.start, wall.end]`; collect boundaries; for each
   elementary interval pick the highest-priority category active in it by
   `build_test > model_api > other_tool > hooks > coai_gate > consultant > ci_wait > coderabbit_wait > sleep300`, else
   `unattributed`; integer milliseconds; Σ categories = `wall.end − wall.start` exactly. `container` spans never enter.
   `active = wall − (coai_gate + consultant + ci_wait + coderabbit_wait + sleep300)`. `inclusive(spans)` = per category
   `sweepSpans(...).unionMs` (`parallel.mjs:55`); `agentSeconds` = per category Σ over agents of that agent's own union,
   labelled `work volume`; `overlapMatrix` = pairwise intersection of category unions. No output key is a span sum.
6. **Tool cross-check** — `crossCheckTools(toolSpans, otelToolResults)`: by `tool_use_id`, `|Δ| ≤ max(1000 ms, 2 %)`;
   mismatches listed, never averaged away.

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `Codex_shaped_rows_never_count_cached_input_twice` (T-A1, coai half) | a row `{ tokensIn: 173658, tokensCached: 168320 }` (banteg's C# v1 trial-1 numbers) | uncached 5,338; input total 173,658, not 341,978 |
| `A_null_cost_reads_unknown_never_zero` (T-A6) | three rows, `costUsd: null` | `'unknown (subscription)'` |
| `Mixed_costs_give_the_known_sum_and_the_unknown_count` | 0.12, null, 0.30 | `{ knownUsd: 0.42, unknownCount: 1 }` |
| `Usage_not_captured_gives_a_count_and_a_lower_bound` | a row with `usageNote: 'usage not captured'`, 40 tokens in | counted apart; 40 is a lower bound |
| `Cached_above_input_is_flagged` | `{ tokensIn: 10, tokensCached: 20 }` | row flag `cached exceeds input` |
| `An_old_line_without_tokensCached_is_unknown_not_zero` | line without the field | `cached: unknown`, `uncached: unknown` |
| `A_ledger_summary_cannot_be_added_to_claude_dollars` | summary object | no numeric `usd` field at its top level |
| `Rounds_become_gate_spans_from_a_real_schema` | a temp db created with `Schema.cs`'s `rounds` columns, two rows | two `coai_gate` spans at the stored instants |
| `The_literal_sleep_300_is_its_own_category` (T-T2) | `sleep 300`; `sleep 300 && gh pr checks 7 --watch`; `sleep 30` | `sleep300`; `wait`; `wait` |
| `Build_and_test_commands_are_work` (T-T2) | `dotnet build -c Release -m:4`, `./tests/X.Tests/bin/Release/net10.0/X.Tests`, `npm run typecheck`, `node --test`, `bash test-v2.sh`, `make -s` | each `build_test` |
| `A_watch_on_ci_is_a_wait` (T-T2) | `gh pr checks 12 --watch`, `gh run watch 99` | `wait` |
| `Agent_containers_are_excluded` (T-T1) | an Agent span 0–100 s, a child `build_test` 10–40 s | partition has no `container`; 10–40 is `build_test`; the rest of 0–100 is `unattributed` |
| `One_childs_sleep_cannot_erase_another_childs_build` (T-T1) | child A `sleep300` 0–300 s; child B `build_test` 100–160 s, `model_api` 200–230 s | `build_test` 60 s, `model_api` 30 s, `sleep300` 210 s |
| `Categories_sum_to_the_wall_exactly` (T-T1) | 200 random overlapping spans of all categories, seeded | Σ = wall to the millisecond, every run |
| `Spans_outside_the_wall_are_clipped` | a span starting before spawn | counted only inside the wall |
| `Inclusive_unions_and_the_overlap_matrix_are_not_sums` | two overlapping `model_api` spans 0–10, 5–15 | union 15 s; agent-seconds labelled; no key named `sum` |
| `Hook_spans_use_arrival_stamps` | stamped `hook_started`/`hook_response` with one `hook_id`, 120 ms apart | one `hooks` span of 120 ms |
| `Decoded_otel_requests_become_model_api_spans` (T-T3, S4 half) | an `api_request` record from E1.S3's decoder, `tsMs` 10,000, `duration_ms` 2,500 | span 7,500–10,000 |
| `A_tool_span_disagreeing_with_otel_is_listed` | audit span 4.0 s vs O 6.0 s | one mismatch; 4.0 vs 4.05 s passes |
| `A_clock_skew_above_one_second_is_flagged` | mapping at spawn vs at exit 1.2 s apart | flag |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/coai-usage.test.mjs test/classify.test.mjs test/timeline.test.mjs`.

**Done when.** T-A6, T-T1, T-T2 and the S4 half of T-T3 green, plus the coai half of T-A1; the property test of Σ = wall ran
over its seeded cases; no function in `timeline.mjs` returns a sum of spans.

---

## 4. Build order inside the epic

S1 first (the runner, the inputs, the box every later scoring run uses). S2 after S1 (it plugs into the pipeline and uses the
box). S3 and S4 are independent of S1/S2 and of each other except that S4's T-T3 half consumes S3's decoder — so S3's
`otel-decode.mjs` lands before S4's span builder. One branch per story off `feat/minigit-bench-e1`, merged in that order.

## 5. Test plan — the parent's ids, by story

| Id | Story |
|---|---|
| T-S1 | S1 |
| T-S2 | S2 |
| T-A1 | S3 (banteg re-pricing) and S4 (the coai reader's half) |
| T-A2, T-A3, T-A4, T-A5, T-M1, T-C1 | S3 |
| T-A6, T-T1, T-T2 | S4 |
| T-T3 | S3 (decoding at the receiver) and S4 (decoded records → spans) |

## 6. Growth

| Surface | Size | Retired by / interrupted |
|---|---|---|
| `<session>/score/` (copy, per-script copies, case repos, `score.json`, `result.json`) | ≈ 50–150 MB per C# session while scoring (bin/obj), ≈ 5 MB kept after shedding the copies; 24 sessions + pilot | the copies are removed by the scorer at its end through `removeUnder`; the rest rides with the session dir (parent §14) |
| `<workRoot>/score-tmp/<rid>-<rand>/` (the clear suite) | < 1 MB each, one at a time per scoring run | removed in `finally`; `heldout-tool.mjs sweep` ends leftovers > 24 h after a crash |
| `<session>/otel.ndjson` | ≈ 1 KB per record, ≈ 2–4 MB per process session | with the session dir (parent §14) |
| `heldout/` in git | one sealed archive, ≈ 50 KB, replaced only by a re-seal before pre-registration | kept |

## 7. Definition of Done

- [ ] E1.S1–E1.S4 merged; `node spikes/minigit-bench/run-tests.mjs` green with no skip among the tests the done-whens name.
- [ ] T-A1 reproduces 2.74535 → 0.6037; T1–T4, the alias record and T-C1 green.
- [ ] Both references 100 % and every mutant exact on the real suite; `manifest.json` carries the plaintext sha256, N1, N2,
      M; `hashes.txt` carries `suite.enc`; `leakcheck` clean; the suite exists in clear only under the operator's home and in
      a scoring temp dir.
- [x] The licence of the upstream inputs decided by the operator (2026-10-29, parent D18): the files live only in the fork.
- [ ] E1.S3's spec was rewritten by Fable before its code, and the summary says so (or that Opus stood in).
- [ ] The parent's E1 entry and ledger match this document; `todo/README.md` lists it.
