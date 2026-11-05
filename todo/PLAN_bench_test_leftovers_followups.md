# PLAN — test-suite leftovers in the spikes: the home sentinel, read-only fixture folders, the Roslyn compiler server, and the size of `push-check.test.mjs`

> Status: **plan only, nothing implemented yet, 2026-11-03; S5 added and S2's and S3's evidence recorded 2026-11-04; S5's list widened 2026-11-05.** Scope: `spikes/lang-bench/` (the exec probe and its tests),
> `spikes/minigit-bench/test/` (the shared test helpers), `spikes/lang-explore/test/`. Four small stories, each on its own branch
> and pull request. The owner asked for them on 2026-11-03, after E1.S8b of
> [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) was merged (#153).
>
> Related: [PLAN_lang_comparison_v2_E1.md](PLAN_lang_comparison_v2_E1.md) (E1.S8b owns the probe that S1 fixes),
> [module_lang_bench.md](../research/module_lang_bench.md), [module_tests.md](../research/module_tests.md),
> the shared rule `common.test-leftovers` (pinned conventions release `cc24f9e`).

## 1. The symptom, before any solution

Found during E1.S8b's full test runs on 2026-11-02 and 2026-11-03, on the series host (WSL2, user `u1000`). Every item says what
was checked and what was not.

**F1 — sentinel folders stay in the operator's home.** Seven folders `~/testsToDelete-u1000-bench-sentinel-<rand>` with their
`.owner` files exist. They were made between 2026-11-02 18:33 and 2026-11-03 11:17. Each holds `secret.txt` with the text
`HOME-SENTINEL`, which is a test value and not a secret. Two places make such a folder under the home:

- the probe: `spikes/lang-bench/lib/exec-probe/lines.mjs:38` (`ownedDirRunning(owned, scope, 'sentinel', { parent: sentinelHome })`),
  called from `spikes/lang-bench/lib/exec-probe/probe.mjs:51` with `sentinelHome: home`;
- a test: `spikes/lang-bench/test/exec-layer-isolation.test.mjs:58` (`owned.ownedDir(t, 'sentinel', { parent: HOME })`, with
  `HOME = os.homedir()` at `:15`).

The suite's start sweep (`spikes/lang-bench/run-tests.mjs:19-23`) calls the shared `sweepLeftovers` with its default root, the
temp root. It never looks in the home, so these folders are never retired. *Checked:* the `.owner` file of one folder
(`L61JQm`) names pid 42029 with start time 1793697595, and that process is gone. *Not checked:* which test or probe run had that
pid, and which path skipped the scope's close (a killed probe in `exec-probe-stop.test.mjs` is the first candidate, because a
SIGKILL runs no cleanup).

**F2 — `testsToDelete-mgb-*` folders with read-only folders stay in the temp root.** After the v1 suite
(`spikes/minigit-bench/run-tests.mjs`) and the pilot suite (`spikes/lang-explore/test/`) ran on 2026-11-03, four such folders
were left, and one of them had read-only folders inside. They were removed by hand on 2026-11-03. Both exit cleanups call
`rmSync(dir, { recursive: true, force: true })`:

- `spikes/minigit-bench/test/helpers.mjs:30-37` prints `could not remove test directory …` and goes on;
- `spikes/minigit-bench/test/fake-home.mjs:26` swallows the error with no line at all.

`rmSync` cannot remove the entries of a folder that has no write bit. Two places that make read-only trees:
`spikes/minigit-bench/lib/cache-snapshot.mjs:42-45` (`chmodTree`) and `spikes/lang-explore/test/commands.test.mjs:64-65`.
*Not confirmed:* which test left the folder.

**F3 — a Roslyn compiler server (`VBCSCompiler`) stays running after the suites.** After the same runs on 2026-11-03, one
`VBCSCompiler` process of this user was still running. It was ended by hand. The boxes end every process they start
(`--unshare-pid --die-with-parent`, `spikes/minigit-bench/lib/bwrap.mjs:15`), so the server came from a `dotnet` started outside
a box, or from a box without these flags. *Not traced:* which test started it. *Not checked:* whether a process outside the
suites started it. No `VBCSCompiler` was running at 2026-11-03 18:30.

**F4 — `spikes/lang-bench/test/push-check.test.mjs` has 564 lines.** That is within the 800-line limit and above the 200–400
lines that PROJECT §6 calls typical. This is not a defect. A split by subject makes the file easier to read and review.

**Out of scope:** eight `/tmp/testsToDelete-u1000-conventions-consumers-*` folders (2026-11-03). Their token is the conventions
repository's, so they are that repository's to trace, and a story for them goes into that repository's plans.

## 2. Boundaries with other plans

| Item | Built by | The other plan's part |
|---|---|---|
| F1: the probe's sentinel and its sweep | this plan, S1 | [PLAN_lang_comparison_v2_E1.md](PLAN_lang_comparison_v2_E1.md) E1.S8b built the probe and is merged; S1 makes its cleanup hold on every path, as E1.S8b already requires. No E1 story changes. |
| F2, F3: the v1 and pilot test helpers | this plan, S2 and S3 | `PLAN_minigit_stack_bench.md` owns `spikes/minigit-bench/`; no story of that plan changes. The fixes are test hygiene under `common.test-leftovers`. |
| F4: the split of one test file | this plan, S4 | E1.S1 wrote the file; no E1 story changes. |

The list is complete: nothing else in these plans is touched.

## 3. Build order

1. **S1 (F1) first.** The probe certifies the execution layer, which every scored v2 run goes through, and a leak in the
   operator's home is the most visible of the four.
2. **S2 (F2) and S3 (F3) in parallel.** They touch different files.
3. **S4 (F4) last, low priority.** It changes no behaviour, and it can wait until the group-A work in `spikes/lang-bench/` is
   quiet, so that no other branch edits the file at the same time.

Every story follows the family's flow: its own worktree and branch; the coai plan round on that branch, then the code round;
an Opus review at the same time; a Sonnet coder with a brief and scenarios written by an Opus judge; RED, GREEN, RED for every
defect; `research/module_tests.md` updated with the run's numbers; the pull request; merge after green checks.

## 4. The stories

### S1 — the probe's home sentinel is removed on every path, and the start sweep retires a stale one

- **Find the leak first.** List `~/testsToDelete-u1000-bench-sentinel-*` before and after a full `spikes/lang-bench` run, plain
  and with `BENCH_REQUIRE_SANDBOX=1`, then after each test file that makes a probe or a sentinel. Name the path that skips the
  close in the brief.
- **Reuse first.** `sweepLeftovers` takes its root as a parameter (`.agents/conventions/tools/lib/leftovers-sweep.mjs:645`,
  `tmp`). Before anything new is written, check whether a second call with the home as the root is safe: it must touch only
  this token's claimed folders, judged on the rule's two keys. If it is not safe, the sentinel's parent becomes one fixed
  folder under the home that the start sweep can scan, and the brief says why.
- **Scenarios.** (1) A probe killed with SIGKILL after it made the sentinel: the next suite start removes the folder. (2) A
  live probe's sentinel is never touched by the sweep. (3) A normal probe run and a normal test run leave no sentinel folder.
  (4) A folder of the same shape without a valid `.owner`, or with a live owner, is left alone.
- **Done when:** the seven folders listed in F1 are retired by the fixed sweep (that is the observed GREEN on this host), and a
  full run leaves none.
- **The layer identity stays as it is (CP2, condition 4, 2026-11-03).** S1 changes nothing in `spikes/lang-bench/lib/exec-layer/` or in
  any module the layer identity covers: `node spikes/lang-bench/pin-exec-layer.mjs --check` exits 0 on the commit that lands S1. If the fix
  cannot stay outside it, the probe is re-run and a CP2 addendum is recorded in `spikes/lang-bench/inputs/checkpoints.md` before the first
  D40 gate (the v2 plan's E1.S8b text names the same boundary).

### S2 — the exit cleanups remove read-only trees, and say so when they cannot

- **Find the folder first.** List `testsToDelete-mgb-*` in the temp root before and after the v1 and the pilot suites; name the
  test that leaves the read-only tree. *Evidence 2026-11-04:* each v1 run leaves one read-only `~/.cache/minigit-bench/test-work/testsToDelete-snap-out-*` from `A_step_that_prints_another_version_than_the_recipe_pins_makes_no_snapshot` (it never makes the tree writable again); `mgb-home`, `mgb-ledger`, `leak` and `real-cal` folders stay after a killed run, because those helpers clean up only at process exit and the start sweep looks only in the temp root. 107 such folders had built up since 2026-10-30; the owner allowed their removal, and they were removed on 2026-11-04 (they come back with each run until S2 is done).
- **Reuse first.** `makeWritable` (`spikes/minigit-bench/lib/cache-snapshot.mjs:47`) already gives a tree `u+w` before it is removed. One remover
  that restores the write bit and then removes the tree serves both exit cleanups; no second copy.
- **Scenarios.** (1) A tracked folder with a `0o555` sub-folder and a `0o444` file is gone after the process exits. (2) A
  folder that still cannot be removed gives one stderr line naming it, in `helpers.mjs` AND in `fake-home.mjs` (which today says
  nothing). (3) A symlink inside the tree is never followed by the chmod.

### S3 — no compiler server outlives a suite

- *Evidence 2026-11-04:* the processes come from the distribution's SDK (`/usr/lib/dotnet/sdk/10.0.114/…`), parent `/init`, started during the lang-explore and lang-bench suites; the pinned SDK with build servers off is not the source. They exit when idle.
- **Reproduce first.** `pgrep -u "$USER" -x VBCSCompiler` before and after each of the three suites, then per test file in the
  suite that starts it. If three full runs of each suite start no server, the story records *not reproduced*, with the method
  and the runs, and closes with no code change.
- **If reproduced:** the `dotnet` launch that starts it gets the build-server keys off (`BUILD_SERVERS_OFF`,
  `spikes/minigit-bench/lib/sandbox.mjs:44`) or runs in a box with `--unshare-pid --die-with-parent`; a test asserts that no new
  `VBCSCompiler` of this user exists after that test file.

### S5 — timing tests that fail under a heavy host load

Added 2026-11-04. **The symptom:** under a host load of 20–38 from other sessions, existing timing tests fail now and then and pass alone or on a re-run: `A_run_sh_that_starts_a_short_tool_and_then_execs_is_not_fork…` (`test/py/test_rs_scope.py`, whose comment says it must never flake; seen in about four full runs), `A_fork_bomb_in_the_box_stops_at_the_pids_bound…`, `A_scope_around_the_driver_would_charge_its_memory_to_the_program…`, `A_case_with_a_cgroup_is_not_slower_than_the_plain_one…`, and in v1 `A_cancel_during_the_quiet_wait_of_the_real_wiring_ends_it_at_once`, `A_wait_for_a_quiet_machine_tells_the_operator…`, `The_proxy_allows_only_the_listed_hosts_on_443`; in the pilot `Output_past_the_cap_is_cut_and_fails…`. *Checked:* each passes alone. *Not checked:* whether each fails on `main` under the same load, or whether any is a real race rather than a margin. **Why it matters for v2:** a completion run that goes red for no cause costs a re-run, and a real race would be hidden. **What to do:** per test, decide margin or race; widen a margin only with the measurement that justifies it, or make the test wait on the event. Never weaken what it asserts.

*Added 2026-11-05, from the language comparison's epoch-8 re-review; the list is closed and only a dated line here widens it.* Also on the list: `The_marker_kill_hook_really_kills_a_hanging_child_before_its_folder_is_removed` (`spikes/lang-bench/test/exec-probe-stop.test.mjs`; failed in E1.S3's final runs at a load of 4 to 14 on 14 CPUs and passed alone; it checks at once that a SIGKILLed child is gone, so this one may be a race, not a margin); `A_fork_bomb_in_the_box_stops_at_the_pids_bound_and_nothing_survives` (the full name of the entry above; `research/module_tests.md` on `feat/lang-v2-e1-s3`, it also failed while another session ran the minigit suite); and the cases `research/module_tests.md` names for the same runs: a cgroup case of the runner's python units, a `run.sh` case, and `A_case_with_a_cgroup_is_not_slower_than_the_plain_one_by_the_cost_of_looking` (the full name). **This story stays a todo, not a build for the language comparison (the owner's rule).** The boundary, both sides: until it lands, [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) D68 says how a v2 merge treats a failure of a test on this list, and its CP4 and CP4-B read this list for tests on the layer's or the runner's verdict path. This plan decides margin or race for each test; that plan owns the merge condition.

*Dated 2026-11-05, after the judge of that re-review (consultants `fa11693d`), which supersedes the sentences above on how a merge treats a listed test:* D68 now needs one whole completion run with every test passed; no re-run of a test or a file replaces it; every failure goes on this list by a dated line, with `/proc/loadavg`; and the same test failing in two consecutive whole runs stops the story until it is diagnosed. The list is a record now, never a way past a red run. **The two unnamed cases.** "A `run.sh` case" is `A_run_sh_that_starts_a_short_tool_and_then_execs_is_not_fork_and_the_pid_is_the_programs` (`test_rs_scope.ForkTests`, the first entry of this story; `fork` count 2 at round 3 in `s3a-h1.txt`), read from the name; the output of the run that `research/module_tests.md` describes was not kept. **"A cgroup case of the runner's python units" cannot be named.** *Checked:* `research/module_tests.md:370`, `s9-reds.txt`, `s9-bench-plan.md`, and the run and reds files in `/home/jinx/rsd/lang-v2/briefs/`: no kept file of those E1.S9 runs names it. It stands here as unnamed, and it gets its name at its next failure. **Others the kept runs do name, added:** `The_group_kill_returns_only_when_nothing_of_the_group_is_left` (`test_rs_olympiad.ProcessTests`, `2 != 0`, `s3a-h1.txt`); `A_V8_heap_limit_abort_through_the_real_layer_is_memory_limit_with_the_runtime_abort_recorded` (`s3a-h2.txt`); `A_cancel_during_the_quiet_wait_of_the_real_wiring_ends_it_at_once` also failed in `s3a-h3.txt` and `s3a-i3.txt`. **The marker test is diagnosed first, and not by this story's order:** under D68 it stopped E1.S3 (two consecutive whole runs, `s3a-j1.txt`, `s3a-j2.txt`). *Read, not run:* the kill hook in `test/support/probe-scope.mjs:56` sends `SIGKILL` and does not wait, and the test's next `after` hook (`test/exec-probe-stop.test.mjs:229-232`) scans `/proc` at once. The test touches no code of the execution layer. So it reads as a race in the test helper, not a defect of the stop path. It is the first small item in the language comparison's §14 after its epoch 8 opens. First the two existing failures are read and the diagnosis is recorded here: under D68 no further whole run comes before it. Only then, if the reading shows the helper's race: a test that forces the race, red first; then the helper waits with a bound, with no change to the layer; then a fresh whole green completion run before #167 merges. If the reading shows the layer is at fault, it stops and goes to the team lead.

*Dated 2026-11-05, §14 A1 step 0: `The_marker_kill_hook_really_kills_a_hanging_child_before_its_folder_is_removed` is diagnosed and fixed; the other tests on this list stay.* It was a race in the test's own checks, not the layer (the test runs no layer code). The failures of E1.S3's two consecutive whole runs are `/home/jinx/rsd/lang-v2/briefs/s3a-j1.txt:1956` and `s3a-j2.txt:1944` (the check at `test/exec-probe-stop.test.mjs:232`, one pid, the child, left at 27 ms and 44 ms). Two races, both found by running the test alone, 14 busy loops on 14 CPUs: (1) `process.kill(pid, 'SIGKILL')` returns before the target has left `/proc`, and the next `after` hook scanned at once (9 of 120 runs); (2) in the body, `bash -c 'exec -a MARKER sleep 30'` carries the marker in bash's argv before the exec, and `/proc/PID/cmdline` reads empty during the `execve`, so one scan saw the pid and the next did not (3 of 120). The fix is in the test and its helper only: `awaitNoPids` in `test/support/probe-scope.mjs` (bound 2 s, polling at 20 ms, the scan as a seam, the error names the pids; tests in `test/probe-scope-wait.test.mjs`, red with no wait, green, red with the wait out), and the body waits for the child's `argv[0]` to be the marker. After: 300 of 300 runs pass under the same load. The reds are in `/home/jinx/rsd/lang-v2/briefs/step0-reds.txt`.

### S4 — `push-check.test.mjs` split by subject

- The tests move into files named for their subject, each within 200–400 lines. No test is deleted or renamed. The list of
  test names before and after is compared, and the count of `spikes/lang-bench` tests is the same.

## 5. Test plan

- Each story's own tests, as listed above, each watched failing for the right reason first (RED), passing after the fix
  (GREEN), and failing again when the fix is taken out (RED).
- All three suites after each story: `node spikes/lang-bench/run-tests.mjs` (plain and with `BENCH_REQUIRE_SANDBOX=1`),
  `BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs`, `node --test spikes/lang-explore/test/`, with a listing of
  `testsToDelete-*` in the temp root and the home, and of `VBCSCompiler`, before and after.
- `node .agents/conventions/tools/plan-lifecycle.mjs` clean.

## 6. Growth

None of the stories adds a store, a folder kind or a process. S1 and S2 remove leftovers that grow today: about one sentinel
folder (one 13-byte file) per leaking run in the home, and one `testsToDelete-mgb-*` folder per leaking run in the temp root.

## 7. Definition of Done

- [ ] S1: no sentinel folder after a full run; the seven F1 folders retired by the fixed sweep; the four scenarios pass.
- [ ] S2: no `testsToDelete-mgb-*` folder after the v1 and pilot suites; a folder that cannot be removed is named on stderr by
      both cleanups.
- [ ] S3: either the test that starts `VBCSCompiler` is fixed and guarded by a test, or *not reproduced* is recorded with the
      method.
- [ ] S4: the file is split, the test names and the count are unchanged.
- [ ] Each story went through the coai plan and code rounds and an Opus review, and its pull request records the observed
      test numbers.
- [ ] `research/module_tests.md` carries each run's numbers; `research/module_lang_bench.md` describes the sentinel's cleanup as
      it is after S1.
- [ ] When the four stories are merged, this plan is promoted to `research/` with its deviations.
