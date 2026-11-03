# PLAN — test-suite leftovers in the spikes: the home sentinel, read-only fixture folders, the Roslyn compiler server, and the size of `push-check.test.mjs`

> Status: **plan only, nothing implemented yet, 2026-11-03.** Scope: `spikes/lang-bench/` (the exec probe and its tests),
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

### S2 — the exit cleanups remove read-only trees, and say so when they cannot

- **Find the folder first.** List `testsToDelete-mgb-*` in the temp root before and after the v1 and the pilot suites; name the
  test that leaves the read-only tree.
- **Reuse first.** `makeWritable` (`spikes/minigit-bench/lib/cache-snapshot.mjs:47`) already gives a tree `u+w` before it is removed. One remover
  that restores the write bit and then removes the tree serves both exit cleanups; no second copy.
- **Scenarios.** (1) A tracked folder with a `0o555` sub-folder and a `0o444` file is gone after the process exits. (2) A
  folder that still cannot be removed gives one stderr line naming it, in `helpers.mjs` AND in `fake-home.mjs` (which today says
  nothing). (3) A symlink inside the tree is never followed by the chmod.

### S3 — no compiler server outlives a suite

- **Reproduce first.** `pgrep -u "$USER" -x VBCSCompiler` before and after each of the three suites, then per test file in the
  suite that starts it. If three full runs of each suite start no server, the story records *not reproduced*, with the method
  and the runs, and closes with no code change.
- **If reproduced:** the `dotnet` launch that starts it gets the build-server keys off (`BUILD_SERVERS_OFF`,
  `spikes/minigit-bench/lib/sandbox.mjs:44`) or runs in a box with `--unshare-pid --die-with-parent`; a test asserts that no new
  `VBCSCompiler` of this user exists after that test file.

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
