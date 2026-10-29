# PLAN — E1 stories: the measurement instruments of the minigit stack bench (held-out suite and scorer, counters, the time partition)

> Status: **plan only, nothing implemented yet, 2026-10-29.** Scope: epic E1 of
> [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md) (§15 steps 2–3) — a new spike folder `spikes/minigit-bench/`
> (inputs, scorer, held-out tooling, counters, partition, its own test runner) reusing `spikes/policy-ab-quick/lib` read-only.
> Nothing in `policy-ab-quick` changes in this epic (its fixes are E2.S1), only E1.S3's step 0 launches `claude` and spends money — one capture session, ≤ $1.50, counted as pilot spend.
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
- **The required environment for every done-when is Linux with `bwrap` and user namespaces** — the series machine, WSL2
  Ubuntu (verified 2026-10-29: `/usr/bin/bwrap`, `unshare -Ur` succeeds). Windows has no `bwrap`, so the spike is not
  supported there: `run-tests.mjs` with `BENCH_REQUIRE_SANDBOX=1` (set by the epic's completion run) turns every
  sandbox-needing skip into a failure, so a host without `bwrap` cannot report the epic done.
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
   0. *sweep* — before anything else, remove leftovers of an interrupted earlier scoring of THIS session: `<session>/score/copy`
      and per-script copies that carry the scorer's own marker file `.bench-score-owned` (written first at every copy,
      holding the session id and a start time) and are older than the scorer's wall limit. A directory without the marker is
      never touched, and is reported by name. Then the same sweep runs over every session under the runs root at
      `series.mjs` start (E3), so a host crash between sessions leaves nothing behind.
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
   --toolchain <dir>… [--cache <dir>…]`; prints one line per step and writes `score.json`. Exit codes follow the repository's CLI
   contract (CLAUDE.md, the `probes` verbs): 0 scored (whatever the score), 3 environment (bwrap missing, the key
   unreadable, a toolchain absent — resumable), 4 configuration (a usage error, a missing or malformed argument), 5 nothing
   produced (the scorer itself failed, named).

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

**Cornerstone — `model: fable`. Spec: Fable** (2026-10-29, replacing the Opus draft before any code). Area: the accounting and
time model (parent `## Epics`, E1, first-ranked). Marked here because the headline USD and the T1–T4 cross-checks with their
conditional T3 stop are the plan's first claim — "correctly counted cost" — and the exact place the original went wrong (O1).
Code Sonnet · review Opus.

**Goal.** From one session's records — **E** (the result envelope), **O** (OTel `claude_code.api_request`), **T** (the
transcripts, main + `subagents/*.jsonl`) — one headline USD at published prices with its basis stated, the CLI's own total
beside it, per-model tokens, T1–T4 with their verdicts and the pre-registered T3 stop, and the alias record of D12; never a
fitted rate, never a stream output token, never a zero where a number was not captured. A value that could not be measured is
`{ known: false, why }` and renders `unknown` (§3). Everything is pure over records, with I/O at two edges: the OTel receiver and
the step-0 capture.

**Verified before this spec, designed around and not re-derived** (sources: `tests/Bench.Tests/Fixtures/scouts/README.md`,
the two-children fixture, `ClaudeSubagents.cs:71-80`, `ScoutCliCostBasisTests.cs:65`, `prices.json:10`, the parent §9):

| Fact | Consequence in this story |
|---|---|
| Anthropic `input_tokens` excludes cache reads and writes; total input = input + cache_creation + cache_read | the headline prices the four kinds separately and never adds a cache count into `input` (the O1 trap, the other way round) |
| The envelope's `usage` is main-loop only; `total_cost_usd` and `modelUsage` include children; `modelUsage` has no 5m/1h split and no `query_source` | tokens come from `modelUsage`; the split comes from T; T is compared with O, never with E |
| Stream assistant `output_tokens` are placeholders; the stream repeats one response per content block | the stream is deduped by `message.id` and prices nothing but the watchdog's labelled estimate |
| The CLI bills a Sonnet 5.5 cache read at $0.20 (published $0.10). **Checked on the two-children fixture:** its Sonnet line is $0.0233382 at $0.20 and $0.0227431 at $0.10; its Opus line is $0.5697508 only with 45,937 of its 74,214 cache-write tokens at the 1h rate — exactly the envelope `usage.cache_creation.ephemeral_1h_input_tokens` of the main session; Haiku $0.001493; the three sum to `total_cost_usd` 0.594582 | the envelope is reproducible to the cent at published prices once the split is known — T-A3/T-A4/T-A5 run on real data, and `asTheCliBills` is the only way to reconcile T with the CLI's own figure |
| Prices: Opus 5.5 4/20/0.20/5/8; Sonnet 5.5 2/10/0.10/2.50/4 (in / out / read / 5m write / 1h write); **Fable 5.1** $10 in / $50 out / **$0.25 read** / $12.50 5m write / $20 1h write (parent §9, read 2026-10-29); the internal Haiku line is `auxiliary` in O | `inputs/prices-2026-10-29.json` carries the Fable row; a model outside the table is `unknown`, never 0 |
| OTel `claude_code.api_request` carries model, four token kinds, `cost_usd`, `duration_ms`, `query_source` (`main` / `subagent` / `auxiliary`), `request_id`, and `agent_id` / `parent_agent_id` on CLI ≥ 2.1.310 (installed: 2.1.310; the binary carries `query_source`, `parent_agent_id`, `OTEL_LOGS_EXPORTER`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `http/json`, `OTEL_LOG_TOOL_DETAILS` — grepped 2026-10-29) | the decoder reads them by name; the CAPTURE confirms their values before any test depends on them |
| `costFromMessages` (fitted) is called at `summary.mjs:142`, `parallel.mjs:90`, `watchdog.mjs:27` | T-C1 scans for every caller name; `resultEnvelope` may be imported from `summary.mjs` (the import is allowed, the call is not) |
| Stream assistant events carry `request_id` (two-children fixture, every assistant line) and child events carry the 5m/1h split too (`cache_creation.ephemeral_5m_input_tokens` 5,951 on the Sonnet child) | T↔O may join by `request_id` (A-J1 below); the split is expected on children as well, and the capture says whether the TRANSCRIPT files carry it |

### Step 0 — the real capture, before any parser is accepted

No OTel or transcript fixture exists in this repository (searched 2026-10-29), `policy-ab-quick` ran with
`--no-session-persistence`, and the operator's `~/.claude/projects` is unreadable from an agent session. So the first paid
artefact of this story is one tiny session on an UNRELATED task, kept as a sanitised replay fixture, and every assumption below
is a verdict in its `CAPTURE.md` before a parser is written against it. **Cost ≤ $1.50, counted as pilot spend (parent §13)**;
it is the one place in E1 that spends money.

**Files.** `spikes/minigit-bench/capture/{capture-session.mjs,otlp-sink.mjs,stamp-hook.mjs,sanitise.mjs,verdicts.mjs}` and
the fixture `spikes/minigit-bench/test/fixtures/real/claude-2.1.310-capture/` (named by the CLI version that produced it).

**The environment** (the coder runs this; the operator supplies one value in the shell, never on argv):

```bash
# 1. the operator, in the shell: a subscription token (D16), from `claude setup-token`; the script refuses to start without it
export CLAUDE_CODE_OAUTH_TOKEN=…           # never printed, never written, never on argv
# 2. the capture — out dir OUTSIDE every git tree and NOT under /home/jinx/rsd/minigit/heldout-src
node spikes/minigit-bench/capture/capture-session.mjs run --out ~/rsd/minigit/capture/$(date -u +%Y%m%dT%H%M%SZ)
# 3. the sanitised copy into the repository, then the verdict table
node spikes/minigit-bench/capture/capture-session.mjs sanitise --from <that dir> --to spikes/minigit-bench/test/fixtures/real/claude-2.1.310-capture
node spikes/minigit-bench/capture/capture-session.mjs verdicts --fixture spikes/minigit-bench/test/fixtures/real/claude-2.1.310-capture
```

`run` does, in order (I/O, each step recorded in `<out>/capture.log`):

1. Creates `<out>/tree/` with two throwaway files (`a.txt`, `b.txt`, fixed content), `<out>/config/` (a fresh
   `CLAUDE_CONFIG_DIR`, so the transcripts land under it and nothing of the operator's profile is read), `<out>/settings.json`
   = `{ "effortLevel": "high", "modelSettings": { "opus": { "effortLevel": "high" }, "sonnet": { "effortLevel": "high" },
   "fable": { "effortLevel": "high" } }, "hooks": { "PreToolUse": [{ "hooks": [{ "type": "command", "command": "node <abs
   spike>/capture/stamp-hook.mjs <out>/hooks.ndjson" }] }], "PostToolUse": [ the same ] } }` (D17; the hook gives the stream
   its `hook_started`/`hook_response` pairs and the audit a `tool_use_id` to pair by), `<out>/mcp.json` = `{ "mcpServers": {} }`.
2. Starts `otlp-sink.mjs` on `127.0.0.1:0` and reads its port: the **file sink**. It accepts `POST /v1/logs` and
   `POST /v1/metrics`, appends one line `{ "receivedAt": <ms>, "path", "contentType", "body": <the raw body as a string> }` to
   `<out>/otlp-raw.ndjson` and answers `200 {}`. It parses nothing — the raw payload is what the decoder is later tested on.
3. Spawns the CLI as exe + argv (no shell), cwd `<out>/tree`, the prompt on **stdin**, stdout → `<out>/stream.ndjson`, stderr →
   `<out>/stderr.txt`, with a 10-minute wall limit:

   ```
   claude -p --model opus --output-format stream-json --verbose --forward-subagent-text --include-hook-events
          --setting-sources project --settings <out>/settings.json --strict-mcp-config --mcp-config <out>/mcp.json
          --max-budget-usd 1.5 --tools Agent,Bash,Read,Glob
   ```

   Session persistence stays ON (no `--no-session-persistence`). Environment = a fixed allow-list, never the parent's:
   `PATH`, `HOME`, `LANG=C.UTF-8`, `CLAUDE_CODE_OAUTH_TOKEN` (passed through, unread), `CLAUDE_CONFIG_DIR=<out>/config`,
   `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1`, `DISABLE_AUTOUPDATER=1`, `CLAUDE_CODE_ENABLE_TELEMETRY=1`,
   `OTEL_LOGS_EXPORTER=otlp`, `OTEL_METRICS_EXPORTER=otlp`, `OTEL_EXPORTER_OTLP_PROTOCOL=http/json`,
   `OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:<port>`, `OTEL_LOGS_EXPORT_INTERVAL=1000`, `OTEL_METRIC_EXPORT_INTERVAL=1000`,
   `OTEL_LOG_TOOL_DETAILS=1`, `OTEL_LOG_USER_PROMPTS=0`. If the init event's `tools` list lacks `Agent`, the script stops and
   says so (the parent's launch line says `Task`; the capture settles the name — recorded in `CAPTURE.md`).

   The prompt (fixed text, committed in `capture-session.mjs`): *"In ONE message launch two subagents with the Agent tool:
   one with `model: "sonnet"` and one with `model: "fable"` (subagent_type general-purpose), each told to run `ls` in the
   current directory and reply with the file names only. When both have returned, run `echo capture-ok` with Bash yourself and
   reply DONE."* Two children, two models, one main tool call: enough to see `query_source` take all its values, a Fable id,
   two `parent_tool_use_id`s and the hook pairs. If the Fable launch is refused (spend or availability), the capture still
   counts; the Fable verdicts read `not exercised`.
4. After exit: waits 3 s for the last OTel batch, stops the sink, copies every `*.jsonl` under `<out>/config/projects/**` to
   `<out>/transcripts/` keeping the relative path (the layout is an assumption, A-T1, so the collector WALKS; it never builds a
   path), writes `<out>/argv.json` (argv and the env KEYS — no values) and `<out>/meta.json` (CLI version from the init
   event, date, `session_id`, exit code, wall ms).

`sanitise` copies `stream.ndjson`, `otlp-raw.ndjson`, `hooks.ndjson`, `transcripts/**`, `argv.json`, `meta.json`,
`stderr.txt` to the fixture directory through `sanitise.mjs` (pure `sanitiseText(text, map)` over every file, every JSON
spelling — plain, JSON-escaped, URL-escaped — and over path components): `<out>` → `/cap`, `os.homedir()` → `/home/user`,
`os.userInfo().username` → `user`, `os.hostname()` → `host`, `git config user.email` and the value of any `user.email` OTel
attribute → `user@example.invalid`, the values of `user.id`, `user.account_uuid`, `organization.id` → `redacted`
(`session.id` is kept — it is the join key between E and O and is random). Then `leakScan` re-reads every written file for
the home directory, the user name, the host name, the e-mail, the raw out path, and the credential shapes
`sk-ant-[A-Za-z0-9_-]{20,}` / the token's own value: any hit **fails** the sanitise and nothing is left in the fixture
directory. The same scan runs as `test/capture-sanitise.test.mjs` on every test run over `test/fixtures/real/**` (the
`ScoutScrubberTests` pattern), plus a self-check that the scanner still finds a planted token.

`verdicts` runs `verdicts.mjs` (pure, over the sanitised files) and writes the table below into `CAPTURE.md` with the
observed values; a verdict is `holds` / `refuted` / `not exercised`, never left blank. The coder fills nothing by hand.

**The assumptions the capture decides, and both branches of each.** "Holds" names what the code below does; "refuted" names
what changes in THIS document (a dated amendment under the assumption, written before the affected code) and in the code.

| Id | Assumption | How the capture tests it | Holds → | Refuted → |
|---|---|---|---|---|
| A-E1 | `modelUsage` carries every child's model and `usage` is main-only | the Sonnet (and Fable) child's `message.model` is a key of `modelUsage`; `usage.output_tokens` < Σ `modelUsage[*].outputTokens` | tokens from `modelUsage`, `usage` only for the main 5m/1h split (T-A3) | per-model tokens come from O (all sources) and E supplies only `total_cost_usd`; T2 degenerates to Σ O cost vs `total_cost_usd` and the report says so |
| A-E2 | Σ `modelUsage[*].costUSD` = `total_cost_usd` ± $0.01 | computed | T1 as specified | T1's threshold is set from the observed gap with its reason, and T1 is `investigate` (never `stop`) until the gap is explained |
| A-S1 | the stream repeats a message per content block with identical input/cache usage, placeholder output | some `message.id` has > 1 assistant event; duplicates agree; Σ last-event `output_tokens` per model < `modelUsage` output | `streamView` dedupes, output marked `placeholder`, nothing priced from it but the labelled meter (T-A2) | the stream's output is final: the meter drops its floor and is still labelled `estimate` (it has no envelope) |
| A-T1 | transcripts exist on disk: one main `<session_id>.jsonl` plus `…/<session_id>/subagents/agent-<id>.jsonl` per child, each assistant line with `message.id`, `message.model`, `message.usage` and the `cache_creation` 5m/1h split, on main AND child lines | the walk finds them; counts of split / unsplit lines per file | `transcriptView` reads main + subagents; `coverage = { main: true, subagent: true }` | no subagent files → coverage `{ main: true, subagent: false }`, T3 cannot be armed (parent §9) and the pre-registered fallback applies (`policy.t3 = 'fallback-t2'`); split absent on child lines → every child model is an interval; absent everywhere → every model is an interval and the headline is `{ low, high }` |
| A-T2 | transcript `output_tokens` are FINAL (the last line of a message id carries what the CLI bills) | per model: Σ over T (dedupe by id, last wins) of `output_tokens` vs `modelUsage[m].outputTokens` within 1 % | `policy.t3OutputSource = 'transcript'` | `'otel'`: T3 takes each message's output from its matched O record (parent §9's pre-registered switch), recorded before any data cell |
| A-J1 | T and O name the same request: the transcript line's `requestId` (or `request_id`) equals O's `request_id` | every T message's id matches exactly one O `api_request`; the unmatched O records are all `auxiliary` | `policy.join = 'request_id'`: T3/T4 compare T with exactly the O records T names, and list the rest by `query_source` | `policy.join = 'query_source'`: T3/T4 compare T with O restricted to `main` + `subagent` (parent §9's wording), and T must cover both |
| A-O1 | the CLI posts OTLP/HTTP **JSON** to `/v1/logs`: `resourceLogs[].scopeLogs[].logRecords[]`, a record named `claude_code.api_request` (body or `event.name`) with attributes `model`, `input_tokens`, `output_tokens`, `cache_read_tokens`, `cache_creation_tokens`, `cost_usd`, `duration_ms`, `query_source`, `request_id`, `agent_id`, `parent_agent_id`, `session.id`, and `timeUnixNano` | `contentType` of every sink line is `application/json`; the names present per record are listed | `otel-decode.mjs` reads these names | protobuf → the sink line's `contentType` says so: the fix is `OTEL_EXPORTER_OTLP_LOGS_PROTOCOL=http/json` and a re-capture; if no JSON transport exists, O is unavailable and the parent's last fallback holds (no series until the instrument is repaired). A missing attribute is `unknown` in every view, and the check that needs it is `unchecked` naming it |
| A-O2 | an `api_request` record's time marks the END of the request | for every matched request, `O.tsMs − duration_ms − 2000 ≤ streamTs ≤ O.tsMs + 2000` (the assistant event's `timestamp`) → `end`; `streamTs ≥ O.tsMs − 2000 && streamTs ≤ O.tsMs + duration_ms + 2000` only → `start`; neither → `unknown` | `OTEL_STAMP_MARKS = 'end'` exported by `otel-decode.mjs`; S4 builds `[tsMs − duration_ms, tsMs]` | `'start'` → S4 builds `[tsMs, tsMs + duration_ms]`; `unknown` → `model_api` spans are `not captured` in S4 until a second capture settles it |
| A-O3 | O's per-model sums over ALL sources equal E's `modelUsage` within max(0.5 %, 100 tokens) per kind, cost within 1 % | computed per model and kind | T2 as specified | the gap is explained before any test is accepted (a missing `auxiliary` line, a different cache-write accounting); T2 is restricted to the models O does carry and the report flags the rest |
| A-O4 | O's `cost_usd` is at the CLI's rates, so Σ O `cost_usd` = `total_cost_usd` ± 1 % | computed | T3 compares T at `asTheCliBills` with O's `cost_usd` | T3 compares T with O's TOKENS re-priced at `asTheCliBills` on both sides (then it is a token check, said so), and O's `cost_usd` is listed beside it |
| A-O5 | child requests carry `agent_id` and `parent_agent_id` (CLI ≥ 2.1.310) | present on every `subagent` record; `agent_id` equals the Agent result's `tool_use_result.agentId` | per-child O attribution by agent id | per-child attribution `unknown`; per-model totals unaffected |
| A-H1 | `hook_started`/`hook_response` appear in the stream with `hook_id` and no `timestamp`; the hook's stdin `tool_use_id` matches the stream's `tool_use` ids | counts; set inclusion | S4 pairs hooks by `hook_id` on arrival stamps (unchanged) | if the stream now carries a timestamp on hook events, S4 may use it; recorded |
| A-H2 | with `OTEL_LOG_TOOL_DETAILS=1`, O's `claude_code.tool_result` records carry `tool_use_id` and `duration_ms` | present on every tool_result record | S4's `crossCheckTools` pairs by `tool_use_id` | `crossCheckTools` returns `{ known: false, why: 'otel tool_result has no tool_use_id' }`; tool spans come from the audit alone |
| A-M1 | `init.model`, each child's `tool_use_result.resolvedModel` and the `modelUsage` keys are exact ids (`claude-opus-5-5`, `claude-sonnet-5-5`, `claude-fable-5-1`, `claude-haiku-…`) and `canonicalModel` is present | listed | `aliasRecord` as specified; the Fable row's id confirmed | an alias resolving to a dated or differently spelt id: the price table's ids follow the capture (one dated commit), never the other way round |
| A-P1 | the CLI bills Sonnet cache reads at $0.20 and everything else at published rates: the Sonnet child's `costUSD` is reproduced at `asTheCliBills` and not at published; the main (Opus, no Opus child in this prompt) `costUSD` is reproduced to 1e-6 at published with `usage.cache_creation`'s split; the Fable child's `costUSD` at the Fable row | computed | `cliRates` unchanged; T-A5 holds on 2.1.310 | the rate the CLI bills on 2.1.310 is re-derived from the capture and written into `prices-2026-10-29.json` `cliRates` with the capture named as `observed` (the shape `ScoutCost.cs:23-26` requires); never guessed |

**How the scenarios depend on it.** T-A1 and T-C1 depend on nothing captured. T-A2 depends on A-S1. T-A3 on A-E1, A-E2. T-A4
on A-T1 (split present) and A-P1 (the Opus line reproduces to 1e-6). T-A5 on A-P1. T-T3's decoder tests on A-O1, A-O2. T1–T4 on
A-E2, A-O3, A-O4, A-J1, A-T2 (the policy file carries the join and the output source the capture decided). T-M1 on A-M1. The
killed-session path on A-O1. **A refuted assumption is written into this document as a dated amendment under its row before
the affected code is written — the test's expected value changes with it; the test is never loosened to pass.**

### Files

| Path | Change |
|---|---|
| `spikes/minigit-bench/capture/capture-session.mjs` | new — `run` / `sanitise` / `verdicts` verbs (I/O at the edge; the pure parts live in the two modules below). Exit 0 captured · 3 environment (no token, `claude` missing, the out dir inside a git tree or under `heldout-src`) · 4 usage · 5 the capture itself failed (named) |
| `spikes/minigit-bench/capture/otlp-sink.mjs` | new — the raw file sink (≤ 60 lines, `node:http`, `127.0.0.1` only) |
| `spikes/minigit-bench/capture/stamp-hook.mjs` | new — reads the hook payload from stdin, appends `{ t: Date.now(), hook_event_name, tool_name, tool_use_id, agent_id, session_id }` to the file named on argv, exits 0 whatever happened |
| `spikes/minigit-bench/capture/sanitise.mjs` | new — pure `sanitiseText`, `sanitisePath`, `leakScan` |
| `spikes/minigit-bench/capture/verdicts.mjs` | new — pure: the A-* table from the sanitised files; `renderVerdicts(table)` → markdown |
| `spikes/minigit-bench/test/fixtures/real/claude-2.1.310-capture/{stream.ndjson,otlp-raw.ndjson,hooks.ndjson,transcripts/**,argv.json,meta.json,stderr.txt,CAPTURE.md}` | new — the sanitised capture; `CAPTURE.md` carries the date, CLI version, the command, the env KEYS, the cost, the sanitiser's sha256 and the verdict table |
| `spikes/minigit-bench/inputs/prices-2026-10-29.json` | new — `{ source, readOn: "2026-10-29", models: [ the three rows of research/scout-calibration/prices.json:5-7 as read 2026-10-12, + { "modelId": "claude-fable-5-1", "input": 10, "output": 50, "cacheRead": 0.25, "cacheWrite5m": 12.50, "cacheWrite1h": 20 } ], cliRates: [ prices.json:10 ], notes: [ which rows were read when ] }`. E3's day-1 re-read replaces it by a new dated file; the loader takes the path, nothing hard-codes a date |
| `spikes/minigit-bench/inputs/crosscheck-policy.json` | new — `{ "t1": "stop", "t2": "otel-incomplete", "t3": "investigate", "t3OutputSource": "transcript", "join": "request_id", "decidedOn": "2026-10-29", "reason": "…" }`; values changed only by a dated commit (E3 arms T3 after the A/A and P1: `"t3": "stop"`; the pre-registered fallback is `"t3": "fallback-t2", "t2": "stop"`) |
| `spikes/minigit-bench/lib/prices.mjs` | new — `loadPrices`, `priceFor`, `asTheCliBills`, `priceUsage`, `priceOpenAiShaped` |
| `spikes/minigit-bench/lib/claude-usage.mjs` | new — `envelopeView`, `streamView`, `streamMeterUsd`, `transcriptView`, `share1h`, `headlineUsd` |
| `spikes/minigit-bench/lib/otel-decode.mjs` | new — `decodeOtlpLogs`, `apiRequests`, `toolResults`, `otelView`, `OTEL_STAMP_MARKS` |
| `spikes/minigit-bench/lib/otel-receiver.mjs` | new — `startReceiver`, `otelEnv` (the one I/O module besides the capture) |
| `spikes/minigit-bench/lib/crosscheck.mjs` | new — `matchTO`, `crosscheck` (T1–T4), `loadPolicy` |
| `spikes/minigit-bench/lib/aliases.mjs` | new — `aliasRecord`, `checkAliases` |
| `spikes/minigit-bench/lib/session-cost.mjs` | new — `sessionCost` (headline from E, else from O flagged `no envelope`, else unknown) |
| `spikes/minigit-bench/test/fixtures/banteg-83644e0-csharp.json` | new — the six C# agent blocks (below) |
| `spikes/minigit-bench/test/fixtures/{otel-synthetic-*.json,stream-synthetic-*.ndjson,transcript-synthetic-*/}` | new — each file's first line or a `_label` key says `SYNTHETIC` and names the real file its shape copies |
| `spikes/minigit-bench/test/{capture-sanitise,prices,claude-usage,otel,crosscheck,aliases,session-cost,no-fitted-rates}.test.mjs` | new |

### Data shapes (pure core; every count a safe integer, every USD an unrounded number rounded only for rendering)

- `Tokens = { input, output, cacheRead, cacheWrite5m, cacheWrite1h }`; where the split is unknown the shape is
  `{ input, output, cacheRead, cacheWrite, split: { known: false, why } }` — a different shape, so a writer cannot price it by
  accident.
- `Price = { modelId, input, output, cacheRead, cacheWrite5m, cacheWrite1h }` (USD per million); `PriceTable = { source,
  readOn, models: Map<id, Price>, cliRates: [{ modelId, cacheRead, observed }] }`.
- `E = { known: true, totalCostUsd, durationMs, durationApiMs, numTurns, perModel: { [id]: { input, output, cacheRead,
  cacheWrite, costUsd } }, main: Tokens (from usage, split from usage.cache_creation) } | { known: false, why }`.
- `Msg = { id, model, source: 'main' | 'subagent', agentId | null, requestId | null, input, cacheRead, cacheWrite5m,
  cacheWrite1h, splitKnown, output, outputPlaceholder, events }`; `T = { messages: Msg[], disagreements: [{ id, field, values }],
  coverage: { main, subagent }, files: n }`; `S` (the stream view) has the same `Msg` with `outputPlaceholder: true`.
- `OReq = { name: 'api_request', tsMs, model, input, output, cacheRead, cacheCreation, costUsd, durationMs, querySource,
  requestId, agentId, parentAgentId, sessionId, missing: [attrNames] }`; `O = { present, requests: OReq[], toolResults: [...],
  perModel: { [id]: { bySource: { [querySource]: Tokens-without-split + costUsd } } }, misconfigured: n }`.
- `Check = { id: 'T1'|'T2'|'T3'|'T4', status: 'pass' | 'breach' | 'unchecked', verdict: 'ok' | 'investigate' |
  'otel incomplete' | 'stop' | 'unchecked', computed, reference, gap, threshold, detail, why? }`.
- `Headline = { known: true, usd, low, high, exact: boolean, basis: string, perModel: { [id]: { usd | { low, high },
  share1h: { known, value }, basis } } } | { known: false, why, perModel }`.

### The change — what and how

1. **`prices.mjs`.** `loadPrices(json)` validates as `ScoutCost.cs:41-48` (`https://` source, ≥ 1 model, ids unique,
   every rate ≥ 0, `:9-15`) and `cliRates` as `:36-39` (names a priced model, non-negative, non-empty `observed`) →
   `{ ok: true, value: PriceTable } | { ok: false, reason }`. `priceFor(table, id)` → `{ known: true, value: Price } |
   { known: false, why: "no price for '<id>'; priced: a, b, c" }` — exact id, no prefix matching, no default row (the thing
   `pricing.mjs:15` does and this spike must not). `asTheCliBills(table)` mirrors `:51-52`. `priceUsage(price, tokens)` =
   `(input·Pin + output·Pout + cacheRead·Pcr + cacheWrite5m·P5m + cacheWrite1h·P1h) / 1e6`; given the unsplit shape it
   returns `{ low, high }` (all writes at 5m / all at 1h). `priceOpenAiShaped({ input, cached, output }, { input, cached,
   output })` = `((input − cached)·in + cached·cachedRate + output·out) / 1e6`, refusing `cached > input` as a value — T-A1 only.
2. **E view** — `envelopeView(events)` over `resultEnvelope` (`summary.mjs:40`): `perModel` from `modelUsage` (`inputTokens`,
   `outputTokens`, `cacheReadInputTokens`, `cacheCreationInputTokens`, `costUSD`), `main` from `usage` with
   `usage.cache_creation` as its split. No `result` event → `{ known: false, why: 'no result envelope' }`. The `usage` block
   is never summed into a total.
3. **Stream view** — `streamView(events)`: assistant events grouped by `message.id` (an event without an id stands alone),
   the LAST event of an id stands for the message; a duplicate whose `input_tokens`, `cache_read_input_tokens` or
   `cache_creation_input_tokens` differs from the first is a `disagreement` (kept, reported, never silently picked);
   `outputPlaceholder: true` always. `streamMeterUsd(view, table, { outputFloor })` prices every message at **published**
   rates with `max(output, outputFloor)` and returns `{ usd, estimate: true, basis: 'published prices; stream output
   floored to <n> per message' }` — E2's watchdog meter, the replacement for `CostMeter`. Nothing else prices from the stream.
4. **T view** — `transcriptView(files)` with `files = [{ relPath, text }]` from the walk: `source` is `subagent` when the
   path contains `/subagents/` (the `agentId` from `agent-<id>.jsonl`), else `main`; the same grouping and disagreement
   rules as the stream; `splitKnown` per message from `message.usage.cache_creation`; `coverage.subagent` is true only when
   at least one subagent file holds an assistant line. `share1h(T, model)` = Σ 1h / Σ (5m + 1h) over that model's split
   messages → `{ known: false, why: 'no split message for <model>' }` when there is none, `{ known: true, value: 0 }` only
   when split messages exist and carry zero 1h tokens (zero is a measurement, not a default).
5. **Headline** — `headlineUsd(E, T, table)`: `E.known` false → `{ known: false, why: 'no envelope' }`. Per model in
   `E.perModel`: price unknown → the headline is `{ known: false, why }` with the other models still priced in `perModel`
   (never a 0 line, never a partial total presented as the total); split known → `w1 = round(cacheWrite × share1h)`,
   `w5 = cacheWrite − w1`, `usd` exact; split unknown → `{ low, high }` and the headline becomes `{ usd: Σ(exact + low),
   low, high, exact: false }`. `basis` = `'published list prices (<readOn>); tokens from the result envelope; 5m/1h split
   from transcripts'`. The CLI's internal line (a model T never saw) is always an interval: parent §9's "an interval, never
   a guess".
6. **OTel** — `otel-decode.mjs`: `decodeOtlpLogs(text)` → `{ ok, value: [{ name, tsMs, attrs, scope }] }` over
   `resourceLogs[].scopeLogs[].logRecords[]`; `intValue` strings → safe integers (`> 2^53 − 1` → `{ ok: false, reason }`),
   `doubleValue`, `stringValue`, `boolValue`; `timeUnixNano` (string or number) → ms; the record name from `body.stringValue`
   or `attrs['event.name']`, whichever the capture showed (A-O1). `apiRequests(records)` → `OReq[]`: an attribute the plan
   names but the record lacks → `null` plus its name in `missing` — **absent is not zero**; `toolResults(records)` the same
   for `claude_code.tool_result`. `otelView(requests)` → per model, per `querySource`, sums over records with no `missing`
   token field, plus `incomplete: n`. `OTEL_STAMP_MARKS` is the capture's A-O2 verdict as a constant with the capture named
   in its comment. `otel-receiver.mjs`: `startReceiver({ dir, port = 0 })` binds `127.0.0.1` only, `POST /v1/logs` and
   `/v1/metrics`; `content-type` not `application/json` → 415 and a `misconfigured` line; body > 8 MB → 413; each body is
   appended raw to `<dir>/otel-raw.ndjson` (the sink's line shape) and its decoded records to `<dir>/otel.ndjson`; `liveSum()`
   → `{ usd: Σ cost_usd of api_request, requests, lastAt }` (E2's cost watchdog reads this: final per request, children
   included). `otelEnv(port)` → the five variables of step 0 plus `OTEL_LOG_TOOL_DETAILS=1`; `env.mjs:4` drops `^OTEL_`, so E2
   adds them after the scrub. `close()` resolves after the listening socket is closed.
7. **T1–T4** — `matchTO(T, O, policy.join)`: `'request_id'` → `matched = O.requests.filter(r => T ids has r.requestId)`,
   `unmatchedT = T messages with no O record` (→ T3/T4 `unchecked: 'otel incomplete: <n> transcript messages have no
   api_request'` when > 0), `unmatchedO` grouped by `querySource` (reported; expected `auxiliary`); `'query_source'` →
   `matched = O.requests.filter(r => ['main','subagent'].includes(r.querySource))` and T3/T4 are `unchecked: 'T covers only
   main'` unless `T.coverage.subagent`. `crosscheck({ E, O, T, table, policy })` → `{ t1, t2, t3, t4, join }`:
   - **T1** `|Σ E.perModel[*].costUsd − E.totalCostUsd| ≤ 0.01` → else `policy.t1` (`stop`). No E → `unchecked`.
   - **T2** per model and kind (`input`, `output`, `cacheRead`, `cacheCreation`): `|O_all − E| ≤ max(0.005·E, 100)`; per
     model cost `|O_all cost − E.costUsd| ≤ 0.01·E.costUsd` → else `otel incomplete` (the cell is kept; S4's time decomposition
     reads `not captured` for it); under `policy.t2 = 'stop'` → `stop`. No O → `unchecked: 'no otel'`.
   - **T3** `Σ_T priceUsage(asTheCliBills(table)[m], tokens)` where `output` is the transcript's (`t3OutputSource:
     'transcript'`) or the matched O record's (`'otel'`), against `Σ_matched O costUsd`: `|T − O| ≤ 0.02·O` (`ScoutCost.cs:187`)
     → else `policy.t3`: `investigate` (default), `stop` (armed by E3 after the A/A and P1 showed `coverage.main &&
     coverage.subagent` and `unmatchedT = 0`), `fallback-t2` (T3 informational; T2 is the stop).
   - **T4** per model and kind (`input`, `cacheRead`, `cacheWrite = 5m + 1h`): `|T − O_matched| ≤ 0.001·O_matched` — exact
     equality in practice, as token counts should be. Breach → `investigate`, with the per-kind table in `detail`.
   Every `Check` carries `computed`, `reference`, `gap`, `threshold` as numbers and a one-line `detail`; a `stop` verdict is
   a value the caller (E3's `series.mjs`) acts on — nothing in this story exits a process.
8. **Aliases (D12)** — `aliasRecord({ events, argvModel = 'opus' })` → `{ aliases: { [alias]: id }, internal: [ids],
   fallbacks: [{ parent, original, fallback, category }] }`: `argvModel` → `initInfo(events).model` (`summary.mjs:121`); each
   Agent launch's `requestedModel` → `resolvedModel` (`agentTimeline`, `parallel.mjs:16`; a launch that resolved nothing is
   listed under `unresolved`, not mapped); `internal` = `modelUsage` keys minus the main id minus every resolved child id;
   `fallbacks` from `system/model_refusal_fallback` events (`ClaudeSubagents.cs:66-70`). Two children on one alias resolving to
   two ids inside one session → `{ ok: false, reason }`. `checkAliases(baseline, current)` → `{ verdict: 'ok' | 'stop' |
   'flag', reasons, extend }`: an alias both used with different ids → `stop`; an alias only `current` used → `extend` (the
   baseline grows, no verdict); an alias only `baseline` used → nothing; an `internal` id in `current` absent from the
   baseline → `stop`; any `fallbacks` → `flag` (never `stop`). `extend` is applied by the caller in a dated write of the
   series' `aliases.json`, never silently.
9. **`session-cost.mjs`** — `sessionCost({ E, O, T, table })`: `E.known` → `headlineUsd` with `basis` as in step 5 and
   `cliTotalUsd: E.totalCostUsd` beside it; else `O.present` → per model over ALL sources from `otelView`, priced at published
   rates with the 5m/1h split from T where T has it (interval otherwise), `flags: ['no envelope']`, `basis: 'otel api_request
   tokens at published prices'`, `cliTotalUsd: { known: false, why: 'no envelope' }`; else `{ known: false, why: 'no envelope
   and no otel records' }`. This module imports nothing from `policy-ab-quick/lib/pricing.mjs`, directly or through
   `summary.mjs`; `envelopeView` is the only consumer of `resultEnvelope`.

### Test scenarios

Fixtures: `banteg-83644e0-csharp.json` = `{ "source": { "repo": "banteg/minigit-bench", "sha": "83644e0", "path":
"results/results.json", "resultsSha256": "3e7580d500813590dc04f35bf231667b9350d4b5561522ff2dcf5840e0e9089f", "formula":
"benchmark.rb:162-167 (codex_cost_usd)", "rates": { "tier": "fast", "input": 5, "cached": 0.50, "output": 30 } }, "rows": [
{ "trial": 1, "phase": "v1", "input_tokens": 173658, "cache_read_tokens": 168320, "output_tokens": 4318, "cost_usd": 1.08199 },
{ "trial": 1, "phase": "v2", "input_tokens": 352992, "cache_read_tokens": 337920, "output_tokens": 5132, "cost_usd": 2.08788 },
{ "trial": 2, "phase": "v1", "input_tokens": 296962, "cache_read_tokens": 288384, "output_tokens": 5308, "cost_usd": 1.788242 },
{ "trial": 2, "phase": "v2", "input_tokens": 206415, "cache_read_tokens": 190080, "output_tokens": 5091, "cost_usd": 1.279845 },
{ "trial": 3, "phase": "v1", "input_tokens": 113144, "cache_read_tokens": 102656, "output_tokens": 3105, "cost_usd": 0.710198 },
{ "trial": 3, "phase": "v2", "input_tokens": 209360, "cache_read_tokens": 197632, "output_tokens": 4743, "cost_usd": 1.287906 } ],
"totals": { "v1_total_count": 11, "v2_total_count": 30 } }` (read from the raw file at that sha, 2026-10-29; the sha256 lets a
reader re-fetch and compare). The two-children fixture is read at
`tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-two-children-models.ndjson` (resolved from `import.meta.url`); the real
capture at `test/fixtures/real/claude-2.1.310-capture/`. USD assertions use `Math.abs(a − b) < 5e-7` unless a cent is named.

| Id | Test | Input | Expected | Pins |
|---|---|---|---|---|
| T-A1 | `The_upstream_formula_bills_cached_input_twice_and_that_is_the_point` | the six rows at 5/0.50/30 through `input·5 + cached·0.5 + output·30` | each row reproduces its `cost_usd` within 1e-6; Σ/3 = **2.7453537** (renders `$2.75`) | O1 has a shape: the published number is the formula's, not a typo |
| T-A1 | `Cached_input_is_billed_once` | the same rows through `priceOpenAiShaped` | Σ/3 = **0.6037003** (renders `$0.60`); ratio 4.55 | the corrected figure the parent §2 states |
| T-A1 | `Cached_above_input_is_refused_as_a_value` | `{ input: 10, cached: 20, output: 1 }` | `{ ok: false, reason }` naming `cached > input`; no throw | the reader cannot be handed O1's inverse |
| T-A1 | `The_fixture_names_its_source_and_the_totals_match_the_scripts` | the fixture's `source` and `totals` | `sha` is `83644e0`, `resultsSha256` present; `v1_total_count` 11, `v2_total_count` 30 (E1.S1's `expectedTotal` test cites the same values) | the fixture is traceable |
| T-A2 | `A_stream_repeating_one_message_three_times_is_one_message_with_placeholder_output` | SYNTHETIC stream: one `message.id` × 3 events (shape of the two-children fixture's `msg_011CfxBB18iebCHsyfRHSjea`, which appears 5 times), identical input/cache, `output_tokens` 16 | `messages.length === 1`, `events === 3`, `outputPlaceholder === true`, `disagreements` empty | dedupe by id |
| T-A2 | `Duplicates_that_disagree_on_input_or_cache_are_a_finding_not_a_pick` | the same with `cache_read_input_tokens` 100 on the second event | one `disagreement` `{ id, field: 'cacheRead', values: [0, 100] }`; the message is still one | nothing silent |
| T-A2 | `The_headline_refuses_a_stream_without_an_envelope` | the SYNTHETIC stream (no `result`) | `headlineUsd` → `{ known: false, why: 'no envelope' }`; `streamMeterUsd` → `{ estimate: true, basis: /floored/ }` | the stream prices nothing but the labelled meter |
| T-A3 | `Totals_come_only_from_modelUsage_and_a_child_only_model_is_not_lost` | the two-children fixture | `perModel` keys exactly `claude-haiku-4-5-20251001`, `claude-opus-5-5`, `claude-sonnet-5-5`; Sonnet `output 118`, `cacheRead 5951`, `cacheWrite 8384`; `main.output 356` is not in any per-model total; `main` split `{ cacheWrite5m: 0, cacheWrite1h: 45937 }`; `totalCostUsd 0.594582` | A-E1 on real data |
| T-A3 | `T1_holds_on_the_real_envelope` | the same E, `crosscheck` with no O and no T | `t1.status 'pass'`, `gap < 1e-6`; `t2..t4` `unchecked` with a `why` each | T1; unchecked is not pass |
| T-A4 | `The_real_opus_line_is_reproduced_only_with_the_true_1h_share` | E from the fixture; a T built from the fixture's assistant lines (dedupe by id; the fixture's children carry the split) | Opus `share1h.known === true`; Opus `usd` within 5e-7 of **0.5697508**; the interval that an unknown split would give is `{ low: 0.4319398, high: 0.6545818 }` and `high − low` = 74214 × (8 − 5) / 1e6 = 0.222642 | the split is load-bearing, and the fixture proves the published Opus row |
| T-A4 | `An_unsplit_model_is_an_interval_never_a_guess` | E with one model `cacheWrite 1000`, T without that model | `perModel[m]` is `{ low, high }`, `high − low` = 1000 × (P1h − P5m) / 1e6; headline `exact: false`, `low ≤ usd ≤ high` | parent §9's interval |
| T-A4 | `Zero_1h_tokens_on_split_messages_is_a_measurement` | T with split messages, all 1h = 0 | `share1h` `{ known: true, value: 0 }`; priced exactly at 5m | zero vs absent |
| T-A5 | `The_cli_billed_view_reproduces_the_pilots_sonnet_line_while_the_published_view_stays_at_ten_cents` | `{ input: 104, output: 31113, cacheRead: 1749236, cacheWrite5m: 176496, cacheWrite1h: 0 }` | published **0.9275016**; `asTheCliBills` **1.1024252**; gap = 1,749,236 × 0.10 / 1e6 = **0.1749236** | `ScoutCliCostBasisTests.cs:65` in the spike |
| T-A5 | `The_fixtures_sonnet_child_was_billed_at_twenty_cents_too` | the fixture's Sonnet `modelUsage` line | published 0.0227431; CLI 0.0233382 = its `costUSD` | the rate is a fact about the CLI, not about one pilot |
| T-A5 | `A_cli_rate_for_an_unpriced_model_is_refused` | `cliRates` naming `claude-x` | `loadPrices` → `{ ok: false, reason }` naming it | `ScoutCost.cs:36-39` |
| prices | `An_unpriced_model_reads_unknown_never_zero` | `priceFor(table, 'claude-fable-5')` (not in the table) | `{ known: false, why }` listing the priced ids; `headlineUsd` with that model in E → `known: false`, other models still priced in `perModel`; rendering gives `unknown` | absent is not zero |
| prices | `The_dated_table_loads_and_carries_the_fable_row` | `inputs/prices-2026-10-29.json` | `ok`; four models; `claude-fable-5-1` = 10/50/0.25/12.50/20; `cliRates[0].modelId === 'claude-sonnet-5-5'` with `cacheRead 0.20` | the series does not price Fable as unknown |
| T-T3 | `The_real_capture_decodes_and_names_every_attribute_the_plan_reads` | `otlp-raw.ndjson` of the real capture | every line `contentType` is JSON; `apiRequests` ≥ 3 (main turns + two children); `missing` empty on every record for the names A-O1 lists — or, per the capture's amendment, exactly the names it recorded as absent | A-O1 as captured |
| T-T3 | `Int_values_arrive_as_strings_and_decode_to_safe_integers` | SYNTHETIC record with `"intValue": "1749236"` and one with `"9007199254740993"` | 1749236; the second → `{ ok: false, reason }` naming the attribute | OTLP JSON encodes int64 as strings |
| T-T3 | `A_missing_token_attribute_is_unknown_not_zero` | SYNTHETIC `api_request` without `cache_read_tokens` | `cacheRead: null`, `missing: ['cache_read_tokens']`; `otelView` counts it `incomplete`, excludes it from sums | absent is not zero |
| T-T3 | `The_stamp_verdict_is_a_named_constant_and_matches_the_capture` | `OTEL_STAMP_MARKS` vs `verdicts.mjs` over the real capture | equal (`'end'` expected; whichever the capture decided) | S4 builds spans from a decided direction |
| T-T3 | `The_receiver_replays_the_capture_and_sums_cost_live` | `startReceiver` on port 0; every sink line of the capture POSTed back with its `contentType` | `otel.ndjson` holds the decoded records; `liveSum().usd` = Σ `cost_usd` within 1e-9 and equals `otelView`'s; `server.address().address === '127.0.0.1'` | the receiver is tested on the real wire format |
| T-T3 | `A_protobuf_post_is_refused_415_and_recorded_as_misconfigured` | POST `application/x-protobuf` | 415; `misconfigured === 1`; nothing in `otel.ndjson` | a wrong exporter is loud |
| T-T3 | `A_body_over_eight_megabytes_is_refused_413` | 9 MB JSON | 413; the server stays up | bounded |
| T1–T4 | `T2_passes_when_otel_matches_the_envelope_within_its_thresholds` | SYNTHETIC O built from the fixture's `modelUsage` (per model: one `main` record with the main tokens, one `subagent` record with the rest, one `auxiliary` Haiku record), E from the fixture | `t2.status 'pass'` per model and kind; `detail` lists each kind's gap | T2 |
| T1–T4 | `T2_breach_marks_the_cell_otel_incomplete_and_keeps_it` | the same O minus the Sonnet records | `t2` `{ status: 'breach', verdict: 'otel incomplete' }`, `detail` names `claude-sonnet-5-5`; `crosscheck` still returns `t1 pass` | a missing line never voids data |
| T1–T4 | `T2_is_the_stop_under_the_pre_registered_fallback` | the same breach with `policy.t2 = 'stop'` | `t2.verdict 'stop'` | the fallback is a value in the policy file |
| T1–T4 | `T3_matches_T_against_the_O_records_it_names_and_lists_the_rest_by_source` | the fixture's assistant lines as T (with their `request_id`s), O with one record per T request at the CLI's rates plus one `auxiliary` record | `join.by 'request_id'`, `unmatchedT 0`, `unmatchedO { auxiliary: 1 }`; `t3.status 'pass'`, `gap < 0.02`; the `auxiliary` cost is not in `t3.reference` | T3's matched set |
| T1–T4 | `T3_is_investigate_until_armed_then_stop` | O's `cost_usd` inflated by 5 % | `policy.t3 'investigate'` → `t3.verdict 'investigate'`; `'stop'` → `'stop'`; `'fallback-t2'` → `'investigate'` with `why` naming the fallback | the conditional stop |
| T1–T4 | `T3_takes_output_from_otel_when_the_policy_says_so` | T with `outputPlaceholder` outputs, O with the billed outputs, `t3OutputSource 'otel'` | `t3.computed` equals the CLI-rate price of T's input/cache with O's output; `'transcript'` gives a different `computed` | parent §9's pre-registered switch |
| T1–T4 | `T3_and_T4_are_unchecked_when_a_transcript_message_has_no_otel_record` | T with one `requestId` absent from O | both `unchecked`, `why` `otel incomplete: 1 transcript message…` | unchecked is not pass |
| T1–T4 | `T4_holds_on_the_real_capture` | the real capture's T and O | `t4.status 'pass'` per model and kind (`gap 0`), `t3.status 'pass'` under the policy the capture decided | the instrument agrees with itself on 2.1.310 |
| T1–T4 | `The_query_source_join_requires_subagent_coverage` | `policy.join 'query_source'`, T with `coverage.subagent false` | `t3`, `t4` `unchecked: 'T covers only main'` | the arming condition |
| killed | `An_envelope_less_session_gets_no_number_from_fitted_rates` | a stream cut before `result` (the fixture truncated at its last `user` event), no O, no T | `sessionCost` → `{ known: false, why: 'no envelope and no otel records' }`; the module graph of `session-cost.mjs` (`node --experimental-import-meta-resolve` not needed: the T-C1 scan) contains no `pricing.mjs` call | parent §9: never a fitted rate |
| killed | `A_killed_session_with_otel_is_priced_from_otel_and_flagged` | the same stream, O from the real capture (or the SYNTHETIC O above), T absent | `known: true`, `flags` includes `'no envelope'`, `basis` names otel; per model an interval where T has no split; `cliTotalUsd { known: false }` | the fallback basis is stated |
| T-M1 | `A_session_resolving_an_alias_it_used_to_a_new_id_stops_the_series` | baseline from the fixture (`opus → claude-opus-5-5`, `sonnet → claude-sonnet-5-5`, internal `[claude-haiku-4-5-20251001]`); current = the same stream with the Sonnet child's `resolvedModel` and `message.model` rewritten to `claude-sonnet-5-6` | `verdict 'stop'`, `reasons[0]` names `sonnet`, both ids | D12 |
| T-M1 | `A_session_that_launched_no_sonnet_child_is_not_drift` | current = the fixture with the Sonnet launch removed | `verdict 'ok'` | sessions may differ in which aliases they used |
| T-M1 | `A_first_use_of_an_alias_extends_the_baseline_and_does_not_stop` | current with a `fable → claude-fable-5-1` child, baseline without `fable` | `verdict 'ok'`, `extend { fable: 'claude-fable-5-1' }` | the record grows by a dated write |
| T-M1 | `A_new_internal_id_stops` | current `internal` `[claude-haiku-5-5]` | `verdict 'stop'` | the CLI's internal line is an alias too |
| T-M1 | `A_model_refusal_fallback_flags_and_does_not_stop` | `claude-2.1.289-model-refusal-fallback.ndjson` as current | `verdict 'flag'`, `fallbacks[0]` `{ original: 'claude-opus-5-5', fallback: 'claude-opus-4-8', category: 'cyber' }` | §11.1: a data point, not a void |
| T-M1 | `The_real_capture_resolves_every_alias_it_used_to_an_exact_id` | the real capture | `aliases.opus === initInfo.model`; `aliases.sonnet` present; `aliases.fable` present or the test says `not exercised` by reading `CAPTURE.md`'s A-M1 row; every id in the price table | A-M1 |
| T-C1 | `The_spike_never_calls_a_fitted_rate_path` | every `spikes/minigit-bench/**/*.mjs` outside `test/` | none of the names `costFromMessages`, `CostMeter`, `summarize`, `parallelism`, `childCosts`, `OUTPUT_FLOOR_PER_MESSAGE` appears as an imported or called identifier (a word-bounded regex per name), and no `import … from '…/pricing.mjs'` | the rule in §3 |
| T-C1 | `The_fitted_rate_scan_still_finds_its_known_callers` | the same scan over `spikes/policy-ab-quick/lib/{summary,parallel,watchdog}.mjs` | hits at `summary.mjs:142`, `parallel.mjs:90`, `watchdog.mjs:27` (line numbers asserted) | the scan is alive |
| sanitise | `The_committed_capture_is_clean_and_the_scanner_still_bites` | `leakScan` over `test/fixtures/real/**`; then over a temp copy with a planted `sk-ant-` token and the home path | clean; the plant is named by file and pattern | the fixture can be public |
| sanitise | `Every_spelling_of_a_path_is_replaced` | a JSON line holding `/cap-real/x`, `\/cap-real\/x` and `%2Fcap-real%2Fx` | all three → `/cap/x` forms | the fixtures README's rule |
| verdicts | `Every_assumption_has_a_verdict_and_none_is_blank` | `verdicts.mjs` over the real capture | every A-* row is `holds`, `refuted` or `not exercised` with an observed value; `CAPTURE.md` equals `renderVerdicts(table)` byte for byte | the table is computed, not typed |

### Commands

`node spikes/minigit-bench/capture/capture-session.mjs run|sanitise|verdicts …` (step 0, once; `run` needs the token);
`node spikes/minigit-bench/run-tests.mjs test/capture-sanitise.test.mjs test/prices.test.mjs test/claude-usage.test.mjs
test/otel.test.mjs test/crosscheck.test.mjs test/aliases.test.mjs test/session-cost.test.mjs test/no-fitted-rates.test.mjs`.

### Done when

- Step 0 ran once on CLI 2.1.310: `test/fixtures/real/claude-2.1.310-capture/` is committed, `CAPTURE.md` carries the
  command, the env keys, the cost (≤ $1.50, listed as pilot spend) and every A-* verdict; `capture-sanitise.test.mjs` is green
  on it; no credential, home path, user name, host name or e-mail is in the fixture (the scan says so, and a reviewer reads
  `argv.json` and `meta.json` by hand).
- Every refuted assumption has its dated amendment under its row in this section, written before the affected code, and the
  policy file's `join` and `t3OutputSource` carry the values the capture decided with `decidedOn`.
- T-A1 prints 2.7453537 → 0.6037003; T-A2–T-A5, T-T3 (S3 half), T1–T4, the killed path, T-M1, T-C1, the sanitise and
  verdict tests green with none skipped; `T4_holds_on_the_real_capture` green.
- `prices-2026-10-29.json` loads with the Fable row; no module under `spikes/minigit-bench/lib/` imports `pricing.mjs`; the
  only I/O modules are `otel-receiver.mjs` and `capture/*`.
- The summary names the models that ran (spec Fable; code Sonnet; review Opus — or who stood in) and the two observations
  the RED-first rule requires where a defect was reproduced (none is expected here; T-C1's second test is the alive-check).

---

### Amendment 2026-10-29 — what the step 0 capture decided (CLI 2.1.310, $0.181, fixture `test/fixtures/real/claude-2.1.310-capture/`)

13 of 15 assumptions held as written; the policy file and `OTEL_STAMP_MARKS` already carried the capture's three decisions
(`join = request_id`, `t3OutputSource = transcript`, `OTEL_STAMP_MARKS = end`). What the capture changed:

- **query_source spellings (A-O5, A-J1).** Headless `-p` reports main requests as `sdk` and child requests as
  `agent:builtin:general-purpose`, not the documented `main` / `subagent`. `roleOf` in `otel-decode.mjs` maps them; an
  absent source is unknown, never auxiliary.
- **A-O5, refuted as worded.** Child requests carry `agent_id` (4 of 4, equal to the Agent results' `agentId`) but no
  `parent_agent_id` (0 of 4) at depth one. `agent_id` alone attributes per child; `REQUIRED_FOR_SUBAGENT = ['agent_id']`.
- **A-P1, decided.** CLI 2.1.310 bills Sonnet 5.5 cache reads at the published $0.10; the $0.20 belonged to 2.1.289.
  `cliRates` entries name their CLI version and apply only to it.
- **A-M1.** The init event reports the subagent tool as `Task` while `--tools Agent` is accepted; the guard accepts both.
- **A-O1.** `api_request` also carries `cache_creation_5m_tokens`, `cache_creation_1h_tokens` and `cost_usd_micros`;
  `duration_ms` is an integer on `api_request` and a string on some other events.
- **Permissions.** Headless `-p` needs an explicit allow-list for every tool the prompt uses; the capture's is
  `Bash(ls)`, `Bash(ls:*)`, `Bash(echo capture-ok)`, `Read`, `Glob`, `Agent`.

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
   `active = build_test + model_api + other_tool + hooks` — the sum of the WORK categories, so `unattributed` is never
   counted as work; `unattributed` is reported as its own bucket beside `active` and the waits. `inclusive(spans)` = per category
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
| `Active_is_the_sum_of_work_never_wall_minus_waits` (T-T1) | wall 100 s: work spans 40 s, wait spans 50 s, nothing in the remaining 10 s | `active` 40 s, `unattributed` 10 s, waits 50 s; the report renders all three |
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
| `<session>/score/` (copy, per-script copies, case repos, `score.json`, `result.json`) | ≈ 50–150 MB per C# session while scoring (bin/obj), ≈ 5 MB kept after shedding the copies; 24 sessions + pilot | the copies are removed by the scorer at its end through `removeUnder`; **interrupted:** step 0's sweep removes marked copies older than the wall limit at the next scoring of that session and at every `series.mjs` start; unmarked directories are never removed; the rest rides with the session dir (parent §14) |
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
