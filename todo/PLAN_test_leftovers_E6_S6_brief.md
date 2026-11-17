# E6.S6 brief — `spikes/`: both guards, the shipped helper in four spikes, the minigit runner's start sweep (E11-6 to E11-8; claim C6; row E6-i's spike half)

> Status: **plan only, nothing implemented yet, 2026-11-17; STOPPED before its plan gate: § 13's two findings (S-1: `runner/run_samples.py` cannot change without files outside S6's *Files*; S-2: an open plan's line on the old names) and § 14's Q1 and Q2 (Q2: the whole `spikes/lang-bench` suite runs the system interpreter through the layer, so no sandbox holds it; O-e stays **blocked**) wait for the coordinator and the owner.** The item 8.1 observations are made at `a8106448` (§ 11): the `.py` units run in a `wslc` container (814 of 829, the other 15 host-bound) and those 15 in an owned throwaway venv (61 of 61 in their three files). The brief of story S6 of `todo/PLAN_test_leftovers_E6.md` as the epoch-11 amendment writes it (§ 6 S6 `:664-709`; D7 `:337-343`; claim C6 `:164`; § 11 `:806-844`). Its scope is S6's *Files* line (`:697-705`) as named paths (§ 0.1). The parent is at epoch 11 with ONE change (entry 11.1): **the next accepted change is the parent's ELEVENTH FULL STOP.**
>
> Judge's brief (Opus) for a Sonnet coder. Written 2026-11-17. Read-only inputs, all verified: benchmark worktree `/home/jinx/git/wt/bench-e6s6`, branch
> `feat/e6-s6-spikes` from `main` `a8106448` (#223: E6.S5; #222: lang-bench gate specs; #221: E6.S4; #220: the stories amended to epoch 11),
> mount `.agents/conventions` at `b1746499`. **The parent** is `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` at conventions `main`
> `22351c2` (#178): ledger entry 11.1 `:900` (state open); § *Epoch 11* item 3.2 (E11-6 `:3749`, E11-7 `:3750`, E11-8 `:3751`), item 3.3 (O-e
> `:3780`), item 4.3 (the brief lists its exemptions), item 8.1 (`:4088-4108`), item 8.2 (S6's row `:4116`). **"Stories"** is
> `todo/PLAN_test_leftovers_E6.md` on `main` `a8106448`. **D7's followups plan** is this repository's `todo/PLAN_bench_test_leftovers_followups.md`
> (F1 `:17-30`, F2 `:32-44`, its stories S1 and S2). Every code `file:line` is at `a8106448` (between `b348bb33` and `a8106448` only `spikes/`'s
> `inputs/` files changed, no test or script, so the parent's and the stories' line numbers hold, § 11 O1). **[M]** measured for this brief, read-only; **[O]** a claim OBSERVED in a scratch clone with item 8.1's six things
> (§ 11); **[P]** observed by the parent (its row named); **[R]** read from code and NOT run (written open-ended, "the run says which").

## 0. What you build, in one paragraph

Every test in `spikes/` that makes a folder, a file or a process marker outside its process names it through the SHIPPED Node helper,
`ownedFor({ token: 'bench' })` from the mount (`.agents/conventions/tools/lib/owned.mjs`), as `spikes/lang-bench` already does: `ownedDir(t, what)`
for a folder the test makes and removes, `ownedName(what)` for a marker, a socket or a fixture name, `tempRoot()` for a path only read or never
made, `ownedPrefix`/`ownedPattern`/`classifyName` for a test that reads names. 25 + 5 policy-ab-quick lines, 41 minigit-bench lines, 6
lang-explore listings and 8 lang-bench Node lines change that way (K1–K8). The lang-bench `.py` units make their folders inside the owned
`TMPDIR` their Node launcher already gives them (`test/support/py-units.mjs:32-34`), through two small helpers (`box_support.scratch_dir`,
`Box.tmp`), never a temp API (K10); the run-time owner of the scope unit (`box_support.py:192`) and the by-hand `prove.sh` keep their lines
under ONE listed exemption each (§ 3a). `spikes/minigit-bench/run-tests.mjs`, the runner policy-ab-quick's tests go through, gains the start
sweep in lang-bench's shape (K9), proved by a killed test that the next runner start collects. `runner/run_samples.py` is NOT changed: any byte
of it moves the runner pin (§ 13, S-1). Then a docs commit (`research/module_tests.md`, the stories' § 11 list). ONE pull request, in this
repository only, as a commit series by spike folder.

## 0.1 Scope — S6's *Files* as named paths, each with the dependency that needs it

The stories' S6 *Files* (`:697-705`): `spikes/policy-ab-quick/test/**`; the files of both guards' findings (the 39 below); `spikes/minigit-bench/run-tests.mjs`;
`research/module_tests.md`; the plan records; and, for a test helper a listed test imports, `spikes/lang-bench/test/**`,
`spikes/lang-explore/test/**`, `spikes/minigit-bench/test/**`, never the files F1 and F2 name. Every path below is inside it.

| Path | Needed for | Bound |
|---|---|---|
| `spikes/policy-ab-quick/test/{audit,canaries,edit-bound,proc,review,review2,review3,review4,series,spike}.test.mjs` (10 files) | 25 `raw-temp`, 5 `legacy-name` (Node) | the sites of § 4 rows 1–10, their makers' callers, one import and one `owned` constant per file |
| NEW `spikes/policy-ab-quick/test/start-sweep.test.mjs`, `spikes/policy-ab-quick/test/fixtures/sweep-noop.fixture.mjs`, `spikes/policy-ab-quick/test/fixtures/killed-owned.fixture.mjs` | scenarios (1), (1b) (K9) | new files only (a new file inside a *Files* glob is brief-level, the coordinator's ruling for S4) |
| `spikes/minigit-bench/test/{agent-box-fs,agent-box,bare-collect-allow,bwrap,capture-session,fake-home,heldout-run,ledger,preflight,reuse-scan,secrets-opt-in,secrets}.test.mjs` (12 files) | 21 `legacy-name`, 18 `raw-temp`, 2 `owner-blind` (Node) | § 4 rows 11–22 |
| `spikes/minigit-bench/run-tests.mjs` | E11-8: the start sweep (K9) | one block after `:13` (`lower-priority.mjs`) and before `:33` (the spawn), lang-bench's `run-tests.mjs:19-26` in shape |
| `spikes/lang-explore/test/quick-heldout-{key,lock,round2,seal}.test.mjs` (4 files) | 6 `owner-blind` (Node) | § 4 rows 23–26 |
| `spikes/lang-bench/test/{exec-layer-cleanup-stop,exec-layer-parent,exec-probe-stop,preflight-lang,sweep}.test.mjs` (5 files), `test/hello/prove.sh` | 5 `legacy-name`, 3 `raw-temp` (Node; `prove.sh:7` in both guards) | § 4 rows 27–32 |
| `spikes/lang-bench/test/py/{box_support,test_burner,test_rs_abort,test_rs_kill,test_rs_launch,test_run_samples}.py` (6 files) | 7 `raw-temp`, 3 `legacy-name`, 1 `owner-blind` (C#) | § 4 rows 33–38; `scratch_dir` added to `box_support.py`, `Box.tmp` to `test_run_samples.py` (K10) |
| `spikes/lang-bench/runner/run_samples.py` | 2 `raw-temp` (C#) | **not touched** — § 13 S-1 |
| `research/module_tests.md` | the start sweep, the sandbox commands, the limits (K14) | § *The minigit stack bench scenarios* `:169-252` (its *How it is run* `:180-190` and the flow rows), § *The language-comparison runner scenarios* `:253-300` (the units' sandbox), § *Test leftovers* `:632-680` (a paragraph on `spikes/` and *The limits*) |
| the plan records: this brief, its `todo/README.md` row, its promotion (`research/PLAN_test_leftovers_E6_S6_brief.md`, `research/README.md`), the stories' progress line, the copy of § 3a's list into the stories' § 11 (item 4.3) | `common/planning-docs.md`; item 4.3 | those records |

**Not touched, by name:** the files F1 and F2 name (`spikes/lang-bench/lib/exec-probe/lines.mjs`, `lib/exec-probe/probe.mjs`,
`test/exec-layer-isolation.test.mjs`; `spikes/minigit-bench/test/helpers.mjs`, `test/fake-home.mjs`, `lib/cache-snapshot.mjs`,
`spikes/lang-explore/test/commands.test.mjs`: the followups plan's S1 and S2, D7); every `lib/` and product file of every spike (the exec-layer
identity covers 22 files, among them `spikes/policy-ab-quick/lib/proc.mjs` and four of `spikes/minigit-bench/lib/`, § 11 O4; the runner's pin
covers `spikes/lang-bench/runner/`); `spikes/lang-bench/inputs/**`; `tests/`, `src/`, `hosts/`, `tools/` (S4, S5); `.github/` (S7); every
`research/` and `todo/` document but those in the table (§ 13 S-2). **A file the build, a test or a check needs outside the table above, or a
decision outside the parent's § *Epoch 11*, is a change of the parent's epoch 11, and after entry 11.1 the next one is the ELEVENTH FULL STOP:
STOP, write the finding into this brief, and hand it to the coordinator; do not add the file.**

## 1. Rules you must read first

- `.agents/PROJECT.md`; the shared entry's procedure with `--task plan --task test --task implement` and the files above.
- `.agents/conventions/common/test-leftovers.md`, its detail (§1 the name and its last item: what lives INSIDE an owned folder carries no prefix of
  its own; §2 the helper: claim first, cleanup registered next, never at module load, a child gets an owned `TMPDIR`; §4 the sweep and its 10-minute
  age; §7 the classes and the exemption grammar), `common/test-leftovers-exemptions.md`, `common/testing.md` (RED first; revert the line the
  behaviour depends on; a structural test needs a live companion).
- The stories (at `a8106448`): D7 `:337-343`; § 1 row E6-i and claim C6 `:164`; § 6 S6 `:664-709`; § 11 `:806-844`.
- The parent § *Epoch 11*: E11-6 to E11-8, O-e, items 4.3, 8.1 (lifetime first; both guards with counts; the brief lists its exemptions), 8.2.
- The S5 brief `todo/PLAN_test_leftovers_E6_S5_brief.md` (§ 11: how an observation is written) and lang-bench's own shapes:
  `spikes/lang-bench/run-tests.mjs:19-26` (the start sweep), `test/sweep.test.mjs:19-31` (`leftBehind`, `age`), `:67-75` and `:118-126` (the
  runner text check and the killed-run collection), `test/support/owned.mjs:3-5` (the binding).
- **Python only in the sandbox of K13.** Never the system interpreter for anything else, not even a text edit (use `node`). No Docker (no CLI here;
  containers go through `wslc`). No heavy CPU job: one suite at a time, through its runner (lowered priority, at most 8 workers). No `git stash`.
  Commit only; the coordinator pushes and opens the PR.

## 2. Preconditions and merge conditions

1. S5 merged (#223, `a8106448`): met **[M]**. The stories amendment (#220) merged: met.
2. **§ 13's findings answered and § 14's Q1 and Q2 answered before the plan gate.** Q2 is the parent's O-e: S6 stops before its gate unless the
   owner says how the whole `spikes/lang-bench` suite runs under this machine's rule for Python (§ 14).
3. At most three open PRs here, or the owner's allowance for E6 PRs over the cap (2026-11-16).
4. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`, `workflows`; no CI job runs a spike suite **[M]**:
   `grep -n spikes .github/workflows/*.yml` finds only a comment); the coai code gate and the Opus review are done; every RED of § 5 quoted on the PR.
5. The four spike suites' runs of § 7, each at the base and at the head, quoted on the PR with `/proc/loadavg` at start and end.

## 3. Decisions of this brief (each with its reason)

- **K1 — One helper, the shipped one, bound per test file.** Each changed Node test file that has no binding yet imports `ownedFor` from the mount
  and binds `const owned = ownedFor({ token: 'bench' });` (the shape of `spikes/lang-explore/test/quick-build-frozen-real.test.mjs:11`, `:44`);
  lang-bench's files already import `owned` from `./support/owned.mjs`. No new helper module: a plain `.mjs` is read by NO guard (§ 11 O3), and
  minigit's shared `test/helpers.mjs` is F2's (D7). `what` is at most 17 characters, no separator (the helper refuses a longer one before
  anything exists); § 4 names each. Every call shape is clean in both guards (§ 11 O9).
- **K2 — Lifetime first, ONE disposition per class** (the parent's item 4.2 table; every site of § 4 carries its class):
  **run** (the test makes it and removes it in a `finally` or hook) → `owned.ownedDir(t, what)`, and the `finally` removal goes (the helper's
  `t.after` removes it on every path); **inside** (a path inside a folder the test or its process already owns) → a plain name made there
  with `mkdirSync`, never a temp API; **read only / never made** (a working directory, a parent a check must refuse, a path asserted absent) →
  `owned.tempRoot()` (the helper's one temp-root reader, `owned.mjs:66-69`, the same value as `os.tmpdir()`) or a neutral literal; **marker**
  (an argv[0], a socket name, a fixture name a product lists) → `owned.ownedName(what)`; **names read by a test** (a filter, an assertion) →
  `owned.ownedPrefix(what)`, `owned.ownedPattern()` or `owned.classifyName(name)`; **outlive** → none in S6; **old name** → none in S6 (the one
  test about prefixed names, the held-out listing, takes owner-form fixtures, K7).
- **K3 — `t` is threaded, never module load.** A module-level maker (`audit.test.mjs:14`, `canaries.test.mjs:9`, `ledger.test.mjs:12`,
  `bare-collect-allow.test.mjs:28-33`'s `series()` keeps its plain-name shape, K2 **inside**) becomes `t => owned.ownedDir(t, what)`; each caller
  passes its test context; a test callback written `() =>` becomes `t =>`. Rule §2: the cleanup is registered inside a test, never at import.
- **K4 — The listings that match the bare prefix (`owner-blind`, no exemption).** `quick-heldout-key.test.mjs:80`, `quick-heldout-lock.test.mjs:166`,
  `quick-heldout-seal.test.mjs:23` → `owned.ownedPattern().test(n)` (anchored, this owner, token `bench`, all three spellings); `quick-heldout-round2.test.mjs:20`,
  `:21`, `quick-heldout-seal.test.mjs:327` → `n.startsWith(owned.ownedPrefix('removing'))` / `('clear')` (the product names them with
  `ownedFor({ token: OWNED_TOKEN })`, `OWNED_TOKEN = 'bench'`, `spikes/lang-explore/lib/score-quick.mjs:24`); `fake-home.test.mjs:33` →
  `assert.notEqual(owned.classifyName(path.basename(fakeHome)).form, 'none', …)` (the fake home is F2's legacy-named folder, `fake-home.mjs:15`,
  which S6 does not touch: the test keeps asserting "prefixed", not "owner form").
- **K5 — Process markers.** `exec-probe-stop.test.mjs:230` → `const marker = owned.ownedName('hangingchild');`; `:86` (a line of a child's script
  template) → the parent computes `const trackedMarker = owned.ownedName('trackedchild');` before the template and the template line becomes
  `const marker = ${JSON.stringify(trackedMarker)};`. A marker is unique per call (random id); `pidsMentioning(marker)` and the kill hook read the
  same string.
- **K6 — The box tests' decoys, fake homes and sockets.** The decoy in the host's temp root (`agent-box.test.mjs:229`, `bwrap.test.mjs:184`) must
  BE in the temp root (the test proves the box hides it): `owned.ownedDir(t, 'adecoy')` / `('decoy')`, a folder instead of a file (the box's
  `ls -A` lists either); the `/var/tmp` decoy of the same statement → `owned.ownedDir(t, 'vadecoy', { parent: '/var/tmp' })` / `('vdecoy', …)` (Q4: no guard reads
  `/var/tmp`, but it is the same statement and lifetime). The fake operator home (`agent-box-fs.test.mjs:15`, `bwrap.test.mjs:116`) must lie
  OUTSIDE the temp root (the box mounts a tmpfs there, which would hide it for the wrong reason): `owned.ownedDir(t, 'ahome', { parent: os.homedir() })`
  / `('home', …)`; the `finally` that removed it goes. The socket under `/run/user/<uid>` (`agent-box.test.mjs:200`, `bwrap.test.mjs:159`) →
  `path.join(runDir, \`${owned.ownedName('agentsock')}.sock\`)` / `('testsock')`, closed and unlinked by the test as today. The other `os.tmpdir()`
  reads of those tests (`agent-box.test.mjs:233`, `:238`, `:239`; `bwrap.test.mjs:188`, `:190`) → `owned.tempRoot()` (read only: the box's
  `--tmpfs` pair is found by that string). Limit: a killed run's fake home under `~` is not collected (the start sweep reads the temp root only;
  F1/S1 of the followups plan is the home sweep) — § 12 N3, Q6.
- **K7 — The held-out listing test** (`heldout-run.test.mjs:140-161`, minigit's OWN `sweepLeftovers` of `lib/heldout-run.mjs:95`, which lists,
  never removes): the two fixtures inside the test's work root take helper names, `const leftOver = owned.ownedName('leftover');`,
  `const inScore = owned.ownedName('inscore');`, and every literal use of `testsToDelete-left-over` / `testsToDelete-in-score` reads them; the
  expected order of `:155` holds (`score-tmp/old-1` < `score-tmp/testsToDelete-u…` < `score-tmp/young-2` < `testsToDelete-u…`). The title's
  `testsToDelete_root_entries` → `prefixed_root_entries`. The other `os.tmpdir()` lines of the file (`:83`, `:84`, `:103`, `:111`, `:113`: a
  root the check must refuse, a link target, a path asserted never made) → `owned.tempRoot()`.
- **K8 — `reuse-scan.test.mjs:61-69`, the structural test.** Today: every `mkdtempSync|writeFileSync|mkdirSync(path.join(os.homedir()|os.tmpdir() …)`
  in `test/*.mjs` must carry the bare prefix (`:65`, `owner-blind`), companion: the scan finds one in `bwrap.test.mjs` (which S6 empties). After
  S6 the guarantee is the helper: **no such creator in any `*.test.mjs`** (`unprefixed` becomes `raw`, every match pushed, `assert.deepEqual(raw, [])`),
  and the companion is the creator that stays in `test/helpers.mjs:42` (`tmpDir`, F2's file, kept by D7): `assert.ok([...code(helpers).matchAll(creators)].length >= 1)`.
  The title → `Every_directory_a_test_file_creates_outside_its_own_process_comes_from_the_owned_helper`. The third assertion (`:68`) stays. (Q5.)
- **K9 — The start sweep in `spikes/minigit-bench/run-tests.mjs` (E11-8), reuse-first.** lang-bench's block (`run-tests.mjs:19-26`: import the
  mount's `sweepLeftovers`; `repo` = `fileURLToPath(new URL('../..', import.meta.url))`; `token: 'bench'`; ONE stderr line
  `run-tests: leftovers — removed N, preserved M…`; a throw is one stderr line; the exit code is the tests') is written after `:13` and before
  `:33`, with the same comment. It sweeps the caller's own `bench` folders in the temp root on the rule's two keys — lang-bench's and S2–S5's
  included, which is right: one token, one owner. Proved by `start-sweep.test.mjs` (§ 5 (1), (1b)), lang-bench's `sweep.test.mjs:118-126` shape
  with a REAL killed test: `fixtures/killed-owned.fixture.mjs` is a `node:test` file that makes `owned.ownedDir(t, 'pabkilled')`, writes its
  path to stdout and `SIGKILL`s itself.
- **K10 — The `.py` sites (the C# copy's 11 in `test/py/`; the runner's two are K12).** No Python name-maker is added: the units already run with `TMPDIR` = an owned
  folder per unit file (`test/support/py-units.mjs:32-34`), so what they make there dies with it. `box_support.py` gains
  `scratch_dir(test)` (a fresh `scratch-<12 hex>` folder made with `os.mkdir(…, 0o700)` inside `os.environ["TMPDIR"]`, removed by
  `test.addCleanup(shutil.rmtree, path, True)`; `test.fail` naming `TMPDIR` when it is unset, so a unit run outside its launcher never falls back
  to the system temp root); `test_run_samples.Box` gains `tmp()` (a fresh `tmp-<8 hex>` child of the box's own root). Then: `test_rs_kill.py:152`,
  `test_rs_launch.py:478` → `scratch_dir(self)` / `bs.scratch_dir(self)`; `test_run_samples.py:174`, `:192`, `:241`, `:434`, `:449` → `box.tmp()`
  (`import uuid` added); `test_rs_abort.py:98` (a path never made) → `os.path.join(bs.scratch_dir(self), "no-such-stderr")`;
  `test_burner.py:39` (`owner-blind`) → `not re.match(r"^testsToDelete-[uw][0-9]+-bench-burner-", name)`, the owner-grammar pattern the same
  file already asserts at `:72`; `box_support.py:187` (a docstring) → "named in the owner form (the prefix, `u<uid>`, `bench`, `scope`, an id)";
  `box_support.py:192` (the systemd unit name, owner built at run time from `os.getuid()`) keeps its line under § 3a's listed `legacy-name`
  exemption, E11-7's second route (the first, the shipped helper, would need the Node launcher to pass a prefix in the environment for one unit
  name; not taken). All planned lines clean in the C# copy (§ 11 O9).
- **K11 — `prove.sh:7`** (a by-hand proof, no test runs it; `research/module_tests.md:564`): its `mktemp -d` stays under § 3a's listed `raw-temp`
  exemption on a new line directly above it; the `EXIT` trap on the same line removes the folder. Observed clean in both Node profiles and in the
  C# copy (§ 11 O9).
- **K12 — `runner/run_samples.py:35`, `:306` are not changed** (§ 13 S-1, Q1). Their lifetime: inside an owned folder (the runner's `TMPDIR` is
  given by whoever launches it: a test's owned folder, `test_run_samples.py`'s `TemporaryFolderTests` `:430-452` proving nothing is left, or the
  layer's box `tmp`).
- **K13 — The Python sandbox (the parent's O-e).** (a) **The units, in `wslc`:** `python:3.14-slim@sha256:9aaa474097d094040649f2162a627c0049b408f52138d980916788d80f04802f`
  (Python 3.14.10; the host's is 3.14.4), the spike mounted READ-ONLY through `\\wsl.localhost\Ubuntu\…`, `-u <uid>:<gid>`, `--cpus 2 -m 2G`,
  `-e TMPDIR=/tmp` (the container's own, gone with `--rm`), `sh` as PID 1 so orphans are reaped (§ 11 O6), the prefixed `--name` and the owner
  label from the helper, `--rm`; nothing installed anywhere. It runs 814 of the 829 units; the other 15 need the host's user systemd manager and
  `bwrap` and skip there. (b) **The 15 host-bound units** (`test_rs_scope.py`, `test_rs_owner.py`, `test_rs_interrupt.py`, the users of
  `box_support.Scope`, whose line `:192` S6 changes): an owned throwaway venv (`python3 -I -m venv --without-pip <owned folder>/v`, the machine
  rule's fallback), `BENCH_REQUIRE_SANDBOX=1`, `TMPDIR` an owned folder, removed by exact path after. (c) **The whole `spikes/lang-bench` suite
  with `BENCH_REQUIRE_SANDBOX=1` and `LANG_V2_TASK_REPO`: blocked.** The layer resolves `python3` on a FIXED system path
  (`lib/exec-layer/run.mjs:79`, `:84-91`: `/usr/sbin:/usr/bin:/bin:/sbin`), so its tests run `/usr/bin/python3` whatever is on `PATH`; changing
  that is a change of the pinned layer identity. Q2 is the owner's.
- **K14 — The docs.** `research/module_tests.md`: in the minigit section's *How it is run*, that `run-tests.mjs` sweeps at its start (and that the
  conventions' `low-priority.mjs node --test` path named by `PLAN_minigit_stack_bench_E2.md:86-87` does not); the flow row of
  `start-sweep.test.mjs`; in the lang-bench section, the units' sandbox commands (K13 (a), (b)) and what they do not cover (K13 (c)); in
  § *Test leftovers*, a paragraph *The spikes* (the shipped helper per test file, the units inside their owned `TMPDIR`, the two listed
  exemptions) and, in *The limits*, § 10's lines and § 12's N1–N3. No other `research/` file (§ 13).

## 3a. The inline exemptions S6 adds (listed here before the plan gate; the PR copies this list into the stories' § 11, item 4.3)

| File:line | Class | Reason (after `testsToDelete-exempt: <class> — `) |
|---|---|---|
| `spikes/lang-bench/test/py/box_support.py`, a new comment line directly above today's `:192` (`self.unit = f"testsToDelete-u{os.getuid()}-bench-scope-…"`); the head's line number recorded on the PR | `legacy-name` | `the owner segment u<uid> of this unit name is built at run time from os.getuid()` |
| `spikes/lang-bench/test/hello/prove.sh`, a new comment line directly above today's `:7` (`work="$(mktemp -d)"; trap 'rm -rf "$work"' EXIT`) | `raw-temp` | `a by-hand proof the operator runs, never a test; the EXIT trap on the next line removes the folder` |

Each marker stands on its own line directly above its line (a marker clears its own line and the next only). Both texts were observed through
the guards that read their file (§ 11 O9). No other exemption: none for `owner-blind` (it takes none), none in a `*.test.mjs`, none in the runner
(§ 13 S-1).

## 4. The changes, file by file (every guard finding in `spikes/`, each with its lifetime)

`N` / `C` = the finding is in the Node guard's (`--profile node`) / the C# copy's list at `a8106448` (§ 11 O1, O2). Lifetime classes as K2.
86 site lines in 39 files: Node 85 findings (46 `raw-temp`, 31 `legacy-name`, 8 `owner-blind`), C# 14 (10 `raw-temp`, 3 `legacy-name`, 1 `owner-blind`); `prove.sh:7` is in both.

| # | File (under `spikes/`) | Lines, classes | Lifetime | S6 |
|---|---|---|---|---|
| 1 | `policy-ab-quick/test/audit.test.mjs` | `:14` N legacy+raw; `:15`, `:55` N raw | `:14` run; `:15`, `:55` read only (a child's working directory; the hook writes only its `--out`) | `:14` `const tmp = t => owned.ownedDir(t, 'pabaudit');`, callers pass `t`, the four `finally` removals go; `:15`, `:55` `owned.tempRoot()` |
| 2 | `policy-ab-quick/test/canaries.test.mjs` | `:9` N legacy+raw | run | `mk = t => owned.ownedDir(t, 'pabcanary')`; the `finally` removals (`:40-41`, `:52-53`) go |
| 3 | `policy-ab-quick/test/edit-bound.test.mjs` | `:14`, `:46`, `:72` N raw | `:14`, `:46` never made (a path `decide()` only compares); `:72` run | `:14` `path.resolve(owned.tempRoot(), 'pab-tree')`; `:46` `path.join(owned.tempRoot(), 'elsewhere', 'x.ts')`; `:72` `owned.ownedDir(t, 'pabeb')`, its `finally` removal goes |
| 4 | `policy-ab-quick/test/proc.test.mjs` | `:28`, `:94`, `:149` N legacy+raw | run | `owned.ownedDir(t, 'pabproc')` ×2, `('pabcli')`; `:27`, `:148` callbacks gain `t` |
| 5 | `policy-ab-quick/test/review.test.mjs` | `:177` N raw | run | `owned.ownedDir(t, 'pabfresh')` |
| 6 | `policy-ab-quick/test/review2.test.mjs` | `:66`, `:316`, `:317`, `:336`, `:337`, `:354` N raw | run (`:317`, `:337` the "outside" folder a junction points at, a second folder of its own) | `owned.ownedDir(t, \`pabmount${arm}\`)`, `('pabjunc')`, `('paboutside')`, `('pabjunc2')`, `('paboutside2')`, `('pabreal')`; `:58`, `:353` callbacks gain `t`. `:353`'s test fails at the base (§ 11 O10) for a reason at `:359`, not at `:354` |
| 7 | `policy-ab-quick/test/review3.test.mjs` | `:100`, `:101` N raw | run | `('pabinner')`, `('pabinnerout')` |
| 8 | `policy-ab-quick/test/review4.test.mjs` | `:109` N raw | run | `('pabresume')` |
| 9 | `policy-ab-quick/test/series.test.mjs` | `:127`, `:275` N raw | run | `('pabshed')`, `('pabtrim')`; callbacks gain `t` |
| 10 | `policy-ab-quick/test/spike.test.mjs` | `:130`, `:149` N raw; `:136` N raw | `:130`, `:149` run; `:136` read only (a child's cwd) | `('pabhook')`, `('pabhome')`; `:136` `owned.tempRoot()` |
| 11 | `minigit-bench/test/agent-box-fs.test.mjs` | `:15` N legacy+raw | run, under the HOME (K6) | `owned.ownedDir(t, 'ahome', { parent: os.homedir() })`; its `finally` removal goes |
| 12 | `minigit-bench/test/agent-box.test.mjs` | `:200` N legacy; `:229` N legacy+raw; `:233`, `:238`, `:239` N raw | `:200` marker (a socket); `:229` run (decoys); the rest read only | K6: `${owned.ownedName('agentsock')}.sock`; `[owned.ownedDir(t, 'adecoy'), owned.ownedDir(t, 'vadecoy', { parent: '/var/tmp' })]`, the `writeFileSync`s and the `finally` removal go; `owned.tempRoot()` ×3 |
| 13 | `minigit-bench/test/bare-collect-allow.test.mjs` | `:30` N legacy+raw | inside (the fake home of `fake-home.mjs`) | `path.join(home, 'rsd', 'minigit', \`series-${randomUUID()}\`, 's')`, made by the `mkdirSync(root, { recursive: true })` of `:31` |
| 14 | `minigit-bench/test/bwrap.test.mjs` | `:116`, `:184` N legacy+raw; `:159` N legacy; `:188`, `:190` N raw | as row 12 | `owned.ownedDir(t, 'home', { parent: os.homedir() })`; `${owned.ownedName('testsock')}.sock`; `[owned.ownedDir(t, 'decoy'), owned.ownedDir(t, 'vdecoy', { parent: '/var/tmp' })]`; `owned.tempRoot()` ×2 |
| 15 | `minigit-bench/test/capture-session.test.mjs` | `:148` N raw | never made (`outProblems` judges a path) | `path.join(owned.tempRoot(), 'mgb-out-fresh')` |
| 16 | `minigit-bench/test/fake-home.test.mjs` | `:33` N owner-blind | names read | K4 (`classifyName(…).form !== 'none'`) |
| 17 | `minigit-bench/test/heldout-run.test.mjs` | `:83`, `:84`, `:103`, `:111`, `:113` N raw; `:140`, `:147`, `:149`, `:151`, `:152`, `:155`, `:158`, `:160`, `:161` N legacy | read only / never made; the fixtures inside the work root | K7 |
| 18 | `minigit-bench/test/ledger.test.mjs` | `:12` N legacy+raw | run | `const ledgerFile = t => path.join(owned.ownedDir(t, 'mgbledger'), 'ledger.ndjson');` callers (`:17`, `:25`, `:34`, …) pass `t` |
| 19 | `minigit-bench/test/preflight.test.mjs` | `:33` N legacy+raw | never made (asserted absent, `:37`) | `path.join(owned.tempRoot(), \`mgb-no-ts-${process.pid}\`)` |
| 20 | `minigit-bench/test/reuse-scan.test.mjs` | `:61` N legacy (title); `:65` N owner-blind | a structural test | K8 |
| 21 | `minigit-bench/test/secrets-opt-in.test.mjs` | `:12` N legacy | never made (a path under the real home the reader must not find) | `['.config', 'minigit-bench', 'no-such-secret-of-a-test']` |
| 22 | `minigit-bench/test/secrets.test.mjs` | `:78` N legacy | never made | as row 21 |
| 23 | `lang-explore/test/quick-heldout-key.test.mjs` | `:80` N owner-blind | names read (a listing of the test's own folder) | K4 |
| 24 | `lang-explore/test/quick-heldout-lock.test.mjs` | `:166` N owner-blind | names read | K4 |
| 25 | `lang-explore/test/quick-heldout-round2.test.mjs` | `:20`, `:21` N owner-blind | names read | K4 |
| 26 | `lang-explore/test/quick-heldout-seal.test.mjs` | `:23`, `:327` N owner-blind | names read | K4 |
| 27 | `lang-bench/test/exec-layer-cleanup-stop.test.mjs` | `:97` N legacy | never made (a stub's report entry) | `name: 'stub-scope'` (the shape `:191` already uses) |
| 28 | `lang-bench/test/exec-layer-parent.test.mjs` | `:19`, `:37` N raw | read only (a parent `checkParent`/`openRun` must refuse) | `owned.tempRoot()` |
| 29 | `lang-bench/test/exec-probe-stop.test.mjs` | `:86`, `:230` N legacy | marker | K5 |
| 30 | `lang-bench/test/preflight-lang.test.mjs` | `:327` N legacy | names read | `assert.ok(seen[0].name.startsWith(owned.ownedPrefix('apphost')), …)` |
| 31 | `lang-bench/test/sweep.test.mjs` | `:111` N legacy | names read | `assert.ok(path.basename(left).startsWith(owned.ownedPrefix('private')), …)` |
| 32 | `lang-bench/test/hello/prove.sh` | `:7` N raw, C raw | run (a by-hand script) | K11, § 3a |
| 33 | `lang-bench/test/py/box_support.py` | `:187`, `:192` C legacy | `:187` a docstring; `:192` marker (a systemd unit, stopped by `addCleanup`) | K10; § 3a for `:192`; `scratch_dir` added |
| 34 | `lang-bench/test/py/test_burner.py` | `:39` C owner-blind | names read (argv[0] of the burner the test started) | K10 |
| 35 | `lang-bench/test/py/test_rs_abort.py` | `:98` C legacy | never made | K10 |
| 36 | `lang-bench/test/py/test_rs_kill.py` | `:152` C raw | inside (the unit's owned `TMPDIR`) | K10 |
| 37 | `lang-bench/test/py/test_rs_launch.py` | `:478` C raw | inside | K10 |
| 38 | `lang-bench/test/py/test_run_samples.py` | `:174`, `:192`, `:241`, `:434`, `:449` C raw | inside (the box's own root) | K10; `Box.tmp` added |
| 39 | `lang-bench/runner/run_samples.py` | `:35`, `:306` C raw | inside (the runner's given `TMPDIR`) | **not changed** (K12, § 13 S-1) |

## 5. The scenarios — RED, GREEN, RED again

"RED again" is the defect put back once after GREEN, by hand, and quoted on the PR. Every child a scenario starts gets an owned `TMPDIR`.

| # | Test (file) | GREEN asserts | RED (first) / RED again |
|---|---|---|---|
| (1) E11-8 | `The_minigit_runner_start_retires_a_killed_policy_ab_quick_tests_folder_and_is_red_without_the_sweep` (`policy-ab-quick/test/start-sweep.test.mjs`) | a private root `owned.ownedDir(t, 'pabsweep')`; `node --test fixtures/killed-owned.fixture.mjs` under `TMPDIR` = that root is killed (exit by `SIGKILL`) and leaves ONE `testsToDelete-<owner>-bench-pabkilled-<id>` with its `.owner` naming the dead pid; both aged one hour (`utimesSync`, lang-bench's `age`); then `node spikes/minigit-bench/run-tests.mjs ../policy-ab-quick/test/fixtures/sweep-noop.fixture.mjs` under the same `TMPDIR` exits 0, its stderr says `run-tests: leftovers — removed 1`, folder and claim are gone | RED first: today's runner leaves both, no stderr line. RED again: delete the sweep block from `run-tests.mjs` → red |
| (1b) E11-8 | `The_minigit_runner_calls_the_conventions_sweep_with_the_bench_token_and_keeps_no_copy_of_its_rules` (same file) | `run-tests.mjs`'s text imports `leftovers-sweep.mjs` from the mount, calls `sweepLeftovers` with `token: 'bench'`, and holds no `readdirSync`/`rmSync` of its own (lang-bench `sweep.test.mjs:67-75` shape) | RED first: today's file has no call. RED again: change the token to `'mgb'` → red |
| (1c) by hand | the same kill with TODAY's maker (`mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-pab-proc-'))`), then (1)'s runner after S6 | — | the legacy folder is listed, never removed (rule §1): the reason S6 moves the makers; quoted on the PR |
| (2) both guards | the guards over the head (§ 6) | per class, per guard, as § 6; § 3a's markers valid | RED again, one per class and guard: `os.tmpdir()` back at `audit.test.mjs:15` → 1 Node `raw-temp`; `'testsToDelete-pab-audit-'` back at `:14` → Node `legacy-name` + `raw-temp`; `/^testsToDelete-/` back at `quick-heldout-lock.test.mjs:166` → 1 Node `owner-blind`; § 3a's `prove.sh` marker removed → 1 Node `raw-temp` (both profiles) and 1 C# `raw-temp`; § 3a's `box_support.py` marker removed → 1 C# `legacy-name`; `test_burner.py:39`'s `startswith("testsToDelete-")` back → 1 C# `owner-blind`; a `tempfile.mkdtemp(dir=box.root)` back at `test_run_samples.py:174` → 1 C# `raw-temp` |
| (3) K10 | `scratch_dir` refuses an unset `TMPDIR` | a unit run with `TMPDIR` unset fails the `scratch_dir` users naming `TMPDIR`; with it set, they pass | RED first: today's `tempfile.mkdtemp(dir=os.environ.get("TMPDIR"))` with `TMPDIR` unset makes its folder in the system temp root (`dir=None`) and passes — quoted from the `wslc` run (K13 (a)) with `-e TMPDIR` dropped. RED again: drop the `test.fail` → the unit passes with `TMPDIR` unset |
| (4) the suites | § 7's runs at the base and the head | each suite's FAILED list at the head equals the base's (policy-ab-quick's three, § 11 O10; the others as the coder's base runs find them; Q3); a run under an owned `TMPDIR` leaves no entry there (§ 11 O10: none at the base for policy-ab-quick) | — |
| (5) the units | K13 (a) and (b) at the base and the head | (a) 829 units, the base's 814 pass and 15 skip, nothing failed; every unit of the six changed `.py` files passes; (b) 61 of 61 with `BENCH_REQUIRE_SANDBOX=1` | — |

## 6. The guards in S6 — both guards, a count per class, at the base `a8106448` (E11-6, E11-7; the parent's item 8.1)

| Guard | Base, all | Base, `spikes/`, per class | Head, all (Q1 (b)) | Head, `spikes/` |
|---|---|---|---|---|
| Node (`--profile node --token bench --identity "the measuring harness"`) | **87** (2 `research/`, 85 `spikes/`) **[M]** | **85**: 46 `raw-temp`, 31 `legacy-name`, 8 `owner-blind` — policy-ab-quick 30 (25, 5, 0), minigit-bench 41 (18, 21, 2), lang-bench 8 (3, 5, 0), lang-explore 6 (0, 0, 6) | **2** | **0** (§ 3a's `prove.sh` marker clears its line) |
| Node, default profile | **3** (2 `research/`, `prove.sh:7`) | 1 `raw-temp` | **2** | 0 |
| copied C# (`--explicit only --filter-class "*LeftoversRepositoryGuardTests"`, information; S7 turns it on) | **16** (2 `research/` `raw-worktree`, 14 `spikes/`) **[M]** | **14**, all in `lang-bench`: 10 `raw-temp` (9 `.py` `mkdtemp(`, `prove.sh:7`), 3 `legacy-name`, 1 `owner-blind` | **4** | **2**: `runner/run_samples.py:35`, `:306` (`raw-temp`), unless Q1 (a), then 0 |

**The read matrix** (§ 11 O3, `isScanned` and the C# finding lists): the Node guard reads `*.test.mjs` and `.sh` under `spikes/`, never a plain
`.mjs` or a `.py`; the C# copy reads `.py` and `.sh` there, never a `.mjs`. So `run-tests.mjs`, `test/support/*.mjs`, `test/helpers.mjs` and
`test/fake-home.mjs` are read by NO guard, and of Python's temp spellings the C# copy reads only `mkdtemp(`: `tempfile.TemporaryDirectory()`,
`tempfile.gettempdir()` and `os.environ["TMPDIR"]` are unread (§ 11 O9, control C2). No finding added anywhere; a count that differs is read
finding by finding before anything else.

## 7. Commands (from the worktree root; WSL; one suite at a time; Python only as K13)

```bash
git diff --check
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" | head -1   # 87 → 2
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness" | head -1                  # 3 → 2
# the C# copy, from an EXISTING Release build whose tree equals the head's outside spikes/ (no build is needed: S6 changes no .cs), or one nice'd build:
nice -n 19 dotnet build tests/Bench.Tests/Bench.Tests.csproj -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers
nice -n 19 ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --no-ansi --explicit only --filter-class "*LeftoversRepositoryGuardTests"   # 16 → 4
cat /proc/loadavg
P=$HOME/.cache/e6s6-$(head -c4 /dev/urandom | od -An -tx1 | tr -d ' \n'); mkdir -m 700 "$P"         # new, empty, outside /tmp; removed by exact path after
TMPDIR="$P" node spikes/minigit-bench/run-tests.mjs '../policy-ab-quick/test/*.test.mjs'; ls -A "$P"   # base: 156 tests, 152 pass, 3 fail, 1 skip; nothing left
node spikes/minigit-bench/run-tests.mjs '../policy-ab-quick/test/start-sweep.test.mjs'                 # (1), (1b)
BENCH_REQUIRE_SANDBOX=1 TMPDIR="$P" node spikes/minigit-bench/run-tests.mjs; ls -A "$P"                # ~30 min (research/module_tests.md:180)
TMPDIR="$P" node .agents/conventions/tools/low-priority.mjs node --test spikes/lang-explore/test/; ls -A "$P"
# K13 (a): the .py units in wslc. NAME and LABEL from the helper, never typed:
NAME=$(node -e "import('./.agents/conventions/tools/lib/owned.mjs').then(m=>console.log(m.ownedFor({token:'bench'}).ownedName('pyunits')))")
# the label names THIS shell ($), which outlives the container; no start, so a reader judges it unknown and keeps it (--rm removes it)
LABEL="$(node -e "import('./.agents/conventions/tools/lib/owned.mjs').then(m=>console.log(m.thisOwnerToken()+':'+m.hostId(require('node:os').hostname(), process.env.WSL_DISTRO_NAME ?? '')))")@linux:$"
W='\\wsl.localhost\Ubuntu'$(pwd | tr / '\\')'\spikes\lang-bench'
wslc.exe run --rm --name "$NAME" --label "testsToDelete.owner=$LABEL" --cpus 2 -m 2G -u "$(id -u):$(id -g)" -e TMPDIR=/tmp \
  -v "$W:/w:ro" -w /w python:3.14-slim@sha256:9aaa474097d094040649f2162a627c0049b408f52138d980916788d80f04802f \
  sh -c 'python3 -I -B -m unittest discover -v -s test/py -p "test_*.py" & wait $!'                      # base: 829, 814 ok, 15 skipped, ~8 min
# K13 (b): the 15 host-bound units in an owned throwaway venv, then removed by exact path
V="$P/venv"; /usr/bin/python3 -I -m venv --without-pip "$V"; mkdir -m 700 "$P/t"
for f in test_rs_scope.py test_rs_owner.py test_rs_interrupt.py; do (cd spikes/lang-bench && BENCH_REQUIRE_SANDBOX=1 TMPDIR="$P/t" nice -n 19 "$V/bin/python3" -I -B -m unittest discover -v -s test/py -p "$f"); done   # 31 + 27 + 3
rm -rf "$P/venv" "$P/t"; systemctl --user list-units --all 'testsToDelete-*' --no-legend | wc -l    # 0
# K13 (c): the whole lang-bench suite — ONLY after the owner's answer to Q2:
#   BENCH_REQUIRE_SANDBOX=1 LANG_V2_TASK_REPO=/home/jinx/git/dew_flow_ai_language_comparison node spikes/lang-bench/run-tests.mjs
node spikes/lang-bench/pin-runner.mjs --check; node spikes/lang-bench/pin-exec-layer.mjs --check           # both exit 0, base and head
node .agents/conventions/tools/plan-lifecycle.mjs
git diff --name-only a8106448..HEAD                                                                      # within § 0.1
```

## 8. The commit series

**One PR, one story, a commit per spike folder** (the parent's item 8.2 for S6: 99 findings in 39 files). Each commit's guard counts fall by
exactly its folder's findings, and its folder's suite passes as at the base.

1. **The minigit runner's start sweep (K9):** `start-sweep.test.mjs` and its two fixtures, (1) and (1b) RED first (quoted), then
   `spikes/minigit-bench/run-tests.mjs`; GREEN; both RED again; (1c) by hand.
2. **policy-ab-quick (K1–K3, § 4 rows 1–10):** Node 85 → 55; the policy-ab-quick run (§ 7) before and after.
3. **minigit-bench (K1–K3, K6–K8, § 4 rows 11–22):** Node 55 → 14; the whole minigit suite with `BENCH_REQUIRE_SANDBOX=1`.
4. **lang-explore (K4, § 4 rows 23–26):** Node 14 → 8; the lang-explore suite.
5. **lang-bench Node and the shell script (K5, K11, § 4 rows 27–32):** Node 8 → 0, default profile 3 → 2, C# 16 → 15.
6. **lang-bench Python (K10, § 4 rows 33–38):** C# 15 → 4; K13 (a) and (b) at the head; (3) RED, GREEN, RED again; `pin-runner.mjs --check`
   and `pin-exec-layer.mjs --check` exit 0 (nothing pinned changed).
7. **The docs commit:** K14 (`research/module_tests.md`); § 3a's list copied into the stories' § 11; the stories' progress line; this brief's
   progress line.

**Size.** About 40 changed files, most a few lines each; three new test files of about 120 lines in all. One Sonnet coder.

## 9. Done when, and what the PR body quotes (the stories' S6 *Done when* `:706-709`, E11-6, E11-7)

- Both guards against the base `a8106448`, in `spikes/`: the Node guard 85 → 0 and the C# guard 14 → 2 (`run_samples.py:35`, `:306`, per Q1 (b);
  0 if Q1 (a)), every class, except § 3a's two listed exemptions; totals 87 → 2 (Node), 3 → 2 (default profile), 16 → 4 (C#); none added elsewhere.
- (1), (1b), (3): GREEN, each RED first and RED again quoted; (1c) quoted; (2)'s RED again from each guard.
- The suites of `policy-ab-quick`, `minigit-bench` and `lang-explore` as `research/module_tests.md` runs them: each FAILED list equal to its base
  run's (Q3), and nothing left in the owned `TMPDIR` of each run. `lang-bench`: K13 (a) and (b) at the head equal to the base; the whole suite
  per the owner's answer to Q2.
- `pin-runner.mjs --check` and `pin-exec-layer.mjs --check` exit 0; `git diff --name-only a8106448..HEAD` within § 0.1; `plan-lifecycle.mjs`
  clean; § 3a copied into the stories' § 11.

## 10. What this story does NOT prove

Windows (no spike suite runs there). That a path read by `owned.tempRoot()` or a neutral literal is never created (a guard reads spellings,
not data flow; the base runs' empty `TMPDIR`, § 11 O10, are the evidence for policy-ab-quick). That a killed run's folders outside the temp
root are collected: the fake homes under `~` (K6) and the `/run/user` sockets are not swept (N3). That the units S6 changes behave the same
under the whole lang-bench suite with the layer's interpreter (K13 (c), Q2). The two runner lines (S-1). What the plain helper modules no guard
reads still make (`test/helpers.mjs:42`, `test/fake-home.mjs:15`: legacy names, F2's, N1).

## 11. Observations — made before this brief's plan gate (the parent's item 8.1)

**Common to O1–O12:** consumer **benchmark `a8106448`** (`main`, #223); the mount **`b1746499`**; Node v26.12.1, WSL Ubuntu, uid 1000, 14 CPUs,
`/proc/loadavg` 9 to 12 from other sessions throughout. The C# runs use the EXISTING Release build of the worktree `wt/bench-e6s5` (`ad43346e`,
built 2026-11-17 19:22): `git diff --stat ad43346e a8106448` prints NOTHING (the whole trees are equal), its worktree is clean, so the copy's
guard files and the scanned tree are `a8106448`'s; no build ran for this brief. The plants (O9) ran in a scratch clone (`git clone --shared`
of `/home/jinx/git/dew_flow_benchmark` at `a8106448`, in the session's scratchpad, the mount and that build output copied in as files), never
committed and never pushed; upstream pin of the copy mcp `53f65130` (the manifest's commit). The scratch is not kept; the results are here.

| # | Claim (where) | Invocation | Fixture path, plant | Expected | Observed |
|---|---|---|---|---|---|
| O1 | the stories' counts and line numbers hold at the new base | `git diff --name-only b348bb33 a8106448 -- spikes`; both guards at `a8106448` | — | Node 85, C# 14 in `spikes/` | 7 files changed, all inputs (`lang-bench/inputs/checkpoints.md`, `inputs/gates/spec-{04,06,07,08,10}.md`, #222; `lang-explore/inputs/explore.json`), no test or script, so every finding line stands; Node **87** (2 `research/`, **85** `spikes/`: policy-ab-quick 30 = 25 `raw-temp` + 5 `legacy-name`; minigit-bench 41 = 18 + 21 + 2 `owner-blind`; lang-bench 8 = 3 + 5; lang-explore 6 `owner-blind`); default profile **3**; C# **16** (2 `research/` `raw-worktree`; **14** `spikes/lang-bench`: the nine `.py` `mkdtemp(` lines and `prove.sh:7` `raw-temp`, `box_support.py:187`, `:192`, `test_rs_abort.py:98` `legacy-name`, `test_burner.py:39` `owner-blind`) **[M]**. The scratch clone gives the same lists |
| O2 | the site lines | the two lists merged by file and line | — | — | **86** lines in **39** files (`prove.sh:7` in both); none on a line F1 or F2 names (F1: `lines.mjs:38`, `probe.mjs:51`, `exec-layer-isolation.test.mjs:58`; F2: `helpers.mjs:30-37`, `fake-home.mjs:26`, `cache-snapshot.mjs:42-45`, `commands.test.mjs:64-65`) **[M]**. (The conventions follow-ups plan's F1 and F2 are other items, a Node sweep's temp-root trust and a janitor exit code; D7's F1 and F2 are this repository's followups plan) |
| O3 | which guard reads which spike file | the mount's `isScanned(file, 'node')` and `isScanned(file)`; the C# lists | `run-tests.mjs` ×2, `test/support/{owned,py-units,probe-scope}.mjs`, `toolchain-fixtures.mjs`, `helpers.mjs`, `fake-home.mjs`, a `*.test.mjs`, `prove.sh`, two `.py` | — | Node: `*.test.mjs` and `.sh` true, every plain `.mjs` and `.py` false; C#: `.py` and `.sh` read (O1), no `.mjs` in `spikes/` **[M]** |
| O4 | nothing S6 touches is pinned, and the runner is | `pin-runner.mjs --check`, `pin-exec-layer.mjs --check`; the identity's file list; `git grep` of the pin's readers | — | — | both exit 0; the layer identity is 22 files: `lang-bench/lib/exec-layer/*`, `inputs/specs.d/memory-policy.json`, `lib/preflight-box.mjs`, `minigit-bench/lib/{bwrap,find-on-path,result,run}.mjs`, `policy-ab-quick/lib/proc.mjs` — none on § 0.1. The runner pin (version 0.3.12, manifest `c2d4c072…`, `inputs/specs.d/runner.json`) hashes every file of `runner/`; `test/runner-pin.test.mjs:103` (`The_committed_runner_pin_equals_the_runner_tree_so_a_runner_edit_without_a_new_pin_fails_here`) reds any edit; `lang-explore/lib/harness-pin.mjs:8` and the gated specs (`inputs/gates/spec-04.md:83`, …) read it **[M]** |
| O5 | K13 (a): the units in `wslc` | `wslc.exe pull python:3.14-slim`; `wslc.exe run --rm --name testsToDelete-u1000-bench-pyunits-d4e5f6 --label testsToDelete.owner=… --cpus 2 -m 2G -u 1000:1000 -v '\\wsl.localhost\Ubuntu\home\jinx\git\wt\bench-e6s6\spikes\lang-bench:/w:ro' -w /w python:3.14-slim python3 -I -B -m unittest discover -v -s test/py -p 'test_*.py'` | the worktree's `spikes/lang-bench` at `a8106448`, read-only | the units run; the host-bound ones skip | image `sha256:d8f0d517…` (digest `9aaa4740…`), Python **3.14.10**; the mount reads; **829 units: 811 ok, 3 failed, 15 skipped** (`no systemd-run with a user manager (FileNotFoundError)`: `test_rs_scope` 10, `test_rs_owner` 3, `test_rs_interrupt` 2), **463.9 s** (wall 473 s); the three failures are group-kill checks (`test_rs_launch` `A_launcher_that_never_reports_and_never_exits_is_killed_at_the_ceiling_and_nothing_is_left`, `test_rs_olympiad` `A_build_that_times_out_or_is_interrupted_…`, `The_group_kill_returns_only_when_nothing_of_the_group_is_left`: `60 != 0`, "the launcher's own child died with its group") **[M]** |
| O6 | the three failures are the container's PID 1, not the code | the same run with `sh -c '… -k group_kill -k launcher_that_never_reports & wait $!'` (sh is PID 1 and reaps orphans) | as O5 | pass | **11 tests, 10 ok, 1 skipped, 1.6 s**, the three among the ok (wall 10 s) **[O]**. So K13 (a) runs `sh` as PID 1 |
| O7 | K13 (b): the 15 host-bound units | `/usr/bin/python3 -I -m venv --without-pip <scratch>/s6venv/v` (the machine rule's fallback, an owned throwaway folder), then per file `BENCH_REQUIRE_SANDBOX=1 TMPDIR=<owned> nice -n 19 <venv>/bin/python3 -I -B -m unittest discover -v -s test/py -p <file>` | `test_rs_scope.py`, `test_rs_owner.py`, `test_rs_interrupt.py` | pass, none skipped | **31 OK (42.5 s), 27 OK (0.9 s), 3 OK (2.5 s)**, wall 47 s; the `TMPDIR` empty after; `systemctl --user list-units --all 'testsToDelete-*'` → 0; the venv removed by exact path **[O]**. (Creating the venv ran `/usr/bin/python3` once, as the fallback allows) |
| O8 | K13 (c): why no sandbox holds the whole suite | read `lib/exec-layer/run.mjs` | — | — | `TOOLS.python = 'python3'` resolved by `findOnPath` on `SBIN_PATH = '/usr/sbin:/usr/bin:/bin:/sbin'` (`:79`, `:84-91`): the layer's tests run `/usr/bin/python3` whatever `PATH` holds; the layer is in the pinned identity (O4) **[R]**. *Checked:* `python:3.14-slim` has no user systemd manager (O5's skip reason) and no `bwrap`, `nsenter` or `systemd-run`. *Not checked:* a privileged systemd image under `wslc`; the toolchains under `~/rsd` mounted into a container |
| O9 | every planned line shape through both guards; claim C6 at the head | the scratch clone with 42 plants (each replaces its base line, an import or helper appended at the file's end) and 3 controls; then both guards over the whole clone; a second round for `box_support.py:192` and `test_burner.py:39` | P1–P5 policy-ab-quick (`t => owned.ownedDir(t, 'pabaudit')`, `owned.tempRoot()` as default `cwd` and in `path.resolve`, `owned.ownedDir(t, \`pabmount${arm}\`)`); P6–P9 lang-explore (`owned.ownedPattern().test(n)`, `n.startsWith(owned.ownedPrefix('removing'))`); P10–P24 minigit (`ownedDir(t, 'ahome', { parent: os.homedir() })`, `` `${owned.ownedName('agentsock')}.sock` ``, `[owned.ownedDir(t, 'adecoy'), …]`, `` `ls -A ${owned.tempRoot()}` ``, `` `series-${randomUUID()}` ``, `classifyName(…).form`, the K7 title and fixtures, `t => path.join(owned.ownedDir(t, 'mgbledger'), …)`, the K8 title and `raw.push(…)`, `'no-such-secret-of-a-test'`); P25–P30 lang-bench (`'stub-scope'`, `owned.tempRoot()`, `${JSON.stringify(trackedMarker)}`, `owned.ownedName('hangingchild')`, `startsWith(owned.ownedPrefix('apphost'))`); P31 § 3a's `prove.sh` marker; P32–P42 Python (the docstring, `scratch_dir` and its body, `bs.scratch_dir(self)`, `box.tmp()` and its body, § 3a's `box_support.py` marker, the `:72` regex at `:39`). Controls: C1 `f"testsToDelete-u{os.getuid()}-bench-scope-x"` (`.py`), C2 `os.environ.get("TMPDIR")` (`.py`), C3 `/^testsToDelete-/.test(n)` (`*.test.mjs`) | planned lines clean; C1, C3 reported | **Node:** 38 base findings cleared by the 30 Node plants, the only new finding is C3 (`owner-blind`); the remaining 47 are unplanted lines of the same shapes; the default profile no longer lists `prove.sh:7`. **C#:** in `spikes/` only `run_samples.py:35`, `:306` and C1 (`legacy-name`); C2 NOT reported (the copy does not read `TMPDIR` in a `.py`); no `exemption` finding, so both § 3a markers are valid as written **[O]**. (The scratch's default profile also printed 11–12 `token` lines naming the copied mount's own rule file, on the pristine clone too: an artefact of copying the mount as files, whose `.git` file does not resolve there; none in the worktree) |
| O10 | policy-ab-quick at the base, and what it leaves | `TMPDIR=<owned, empty> node spikes/minigit-bench/run-tests.mjs '../policy-ab-quick/test/*.test.mjs'`, then `ls -A` | the worktree at `a8106448` | — | **156 tests, 152 pass, 3 fail, 1 skipped, 6.4 s**; the owned `TMPDIR` holds **nothing** after. The three failures, all Windows-path cases on Linux: `a runs root must be at least two folders deep` (`review2.test.mjs:300-301`, `8 !== 0`), `shedding a real directory still works, and refuses a shallow root outright` (`review2.test.mjs:353`, "Missing expected exception" at `:359`), `a report whose every cell is void never overwrites the results file; it goes to the runs root` (`review4.test.mjs:158`, `D:/x/y.json` resolved under the spike) — `PLAN_minigit_stack_bench_E2.md:86-88` names "four pre-existing Linux failures" **[M]** |
| O11 | item 8.1's doc lines | `git grep -n -E` of the old spellings (`testsToDelete-(pab|mgb|series|stub|left-over|in-score|no-such)`, the 17 `pab-…` leaves, `mgb-(adecoy|decoy|ahome|agent|ledger|no-ts|out-fresh|clear)`, `trackedchild`, `hangingchild`, `bench-scope`, `runner-pyc`, the two old titles) over `*.md` but the E6 documents | — | — | `todo/PLAN_bench_test_leftovers_followups.md:32`, `:102-103`, `:151`, `:156` (F2's `testsToDelete-mgb-*` folders of `helpers.mjs`/`fake-home.mjs`, which S6 does not change: still true); `todo/PLAN_minigit_stack_bench_E2.md:89` (§ 13 S-2) and `:500` (a socket under a `helpers.mjs` folder: still true) **[M]** |
| O12 | `LANG_V2_TASK_REPO` on this machine | `ls`, `git -C … log -1`, `git status --short` | — | — | `/home/jinx/git/dew_flow_ai_language_comparison`, clean, `c204726` (2026-11-07), `todo/tasks/FORMAT.md` present **[M]**. Not checked: that its FORMAT's sha256 equals the record's `A.sha256` (the suite checks it) |

**Written open-ended, not observed here (red or not, the PR's run says which):** the RED of (1), (1b), (3) on today's code; the minigit and
lang-explore suites at the base and the head (not run for this brief: about 30 minutes of the host for minigit, § 7); K13 (a)'s result with
`-e TMPDIR=/tmp` at the head; the whole lang-bench suite (Q2).

## 12. Notes for the parent and the stories (findings, not changes)

- **N1 — Two plain helper modules still make legacy names, and no guard reads them.** `spikes/minigit-bench/test/helpers.mjs:42` (`tmpDir`:
  `testsToDelete-mgb-<prefix>-`) and `test/fake-home.mjs:15` (`testsToDelete-mgb-fakehome-`) are F2's files (D7), plain `.mjs` (O3). After S6
  most minigit and lang-explore test folders still come from them; the start sweep lists them as legacy, never removes them. For the followups
  plan's S2 and the parent's *Found on the way*.
- **N2 — The C# copy reads one Python temp spelling.** `mkdtemp(` only (O9, control C2; `box_support.py:27` and `test_run_samples.py:53`'s
  `tempfile.TemporaryDirectory()` are unread today). K10's helpers keep every unit folder inside the owned `TMPDIR`, but the guard cannot tell;
  `research/module_tests.md`'s limits say so (K14).
- **N3 — Owned folders outside the temp root are not collected after a kill.** The fake homes under `~` (K6) and the `/run/user/<uid>`
  sockets: the start sweep reads the temp root only (`sweepLeftovers`' `tmp`); F1/S1 of the followups plan decides the home sweep.
- **N4 — `test/support/py-units.mjs:32` makes its owned folder at module load.** `pythonUnits(file)` is called at the top level of 32 Node
  test files and calls `ownedDir` then (`after(() => scope.close())` is registered first, `:41-42`); rule §2 asks for a hook or a test. Not a
  guard finding (a plain `.mjs`); not changed by S6; for the parent's *Found on the way*.
- **N5 — Two ways run policy-ab-quick's tests.** `research/module_tests.md:185` (the minigit runner, which sweeps after S6) and
  `PLAN_minigit_stack_bench_E2.md:86-87` (the conventions' `low-priority.mjs node --test`, which does not). K14 names the difference.
- **N6 — The units are slow in `wslc`.** 464 s for 829 units on 2 CPUs through the 9p mount of `\\wsl.localhost` (O5); the host time of the
  same units was not measured (no system interpreter).

## 13. STOP findings — before the plan gate

**S-1 — `runner/run_samples.py:35`, `:306` cannot change without files outside S6's *Files*.** Both are on the *Files* line, and both are C#
`raw-temp` findings S6's *Done when* takes to zero. But `runner/` is pinned (O4): ANY byte, an inline exemption comment included, makes
`test/runner-pin.test.mjs:103` red until the runner is re-pinned, and a re-pin writes `spikes/lang-bench/inputs/specs.d/runner.json`,
`inputs/specs.json`, `inputs/runner-versions.json`, bumps `RUNNER_VERSION` in `runner/rs_report.py:18` (0.3.12), and, as the comment-only change
of 2026-11-07 did, needs a CP2 addendum in `inputs/checkpoints.md` and a probe re-run; `lang-explore/lib/harness-pin.mjs` then reads the runner
as moved for every earlier series, and the gated specs of #222 record the check at their pins. None of these is on S6's line, and they are the
language comparison's (`todo/PLAN_lang_comparison_v2.md`, D59's re-pin). The lines themselves are the runner's own temp folders, removed at exit
(`:36` `atexit`, `:309-310` `finally`), inside the `TMPDIR` its launcher gives (K12) — product sites of the scored runner, like S5's N3
(`FileSystemGateAssessmentFiles.cs:33`, "stays the product's"). The options (the coordinator decides, the owner where it counts):
(a) re-pin, adding those files to S6's line: a change, **11.2, the eleventh full stop**, and a cross-plan re-pin; (b) read the two lines as the
runner's own product sites, unchanged by S6, and record that S6's C# count in `spikes/` ends at 2: if that reading narrows S6's *Done when*
("C# 14 → 0"), it is a change too; (c) leave the disposition to S7, which switches the C# copy on and must meet them anyway (the walk's reach over
`runner/` is S7's text). **Recommendation: (b), with the two lines carried to S7 by name** (no file outside the line, no byte of a pinned tree;
S7 decides how the switched-on guard treats product code it reads) — and the coordinator says whether (b) counts as a change.

**S-2 — An open plan's line names the old form for every test's leftovers.** `todo/PLAN_minigit_stack_bench_E2.md:89` (status "in progress —
E2.S3 and E2.S4 open"): "Leftovers a test creates on disk keep E1's `testsToDelete-mgb-…` names (the pinned rule's form — §2: the release has no
owner segment)." After S6 the policy-ab-quick and minigit test files S6 converts make owner-form names; the line's reason was already false
before S6 (the mount's helper makes owner-form names — lang-bench's tests assert it, `preflight-lang.test.mjs:327`, `sweep.test.mjs:111` — and
E6.S2–S5 made owner-form names in `tests/`; when the pin first carried the owner form was not read).
**Recommendation: not on S6's line** — S6 does not make it false, S6 needs no edit of it, and its plan's next story is where it is corrected;
the coordinator carries it to that plan as a note. If the coordinator reads it as a description S6 changes, adding it is a change (11.2).

## 14. Questions — each with a recommendation

| Q | Question | Recommendation |
|---|---|---|
| Q1 | § 13 S-1: the two runner lines — (a) re-pin, (b) the runner's own product sites, carried to S7, or (c) S7's alone? | **(b)**, as § 13 argues; the coordinator says whether it counts as a change before the plan gate |
| Q2 | O-e, K13 (c): the whole `spikes/lang-bench` suite with `BENCH_REQUIRE_SANDBOX=1` and `LANG_V2_TASK_REPO` runs `/usr/bin/python3` through the layer's fixed path; no `wslc` image or venv holds it (O8). (A) the owner allows that interpreter for S6's two whole-suite runs (base, head) only — standard library, `-I -B`, nothing installed, lowered priority, the layer's own choice, pinned by its identity; or (B) S6's lang-bench evidence is K13 (a) and (b) only, and the *Done when*'s "lang-bench green as `module_tests.md` runs it" is not met (a narrowing: 11.2, the eleventh full stop) | **(A)**: S6 changes no layer file and no runner file, the units S6 changes are proved in the sandbox (O5–O7), and the whole run only shows nothing else moved. It is the owner's rule; the consultants cannot settle it |
| Q3 | The stories' "the suites … green": policy-ab-quick has three failures at the base (O10, Windows-path cases named as pre-existing in its own plan); read as "the FAILED list equal to the base's", as S5 read its Docker-bound 572? | yes; S6 changes none of those assertions (`review2.test.mjs:354` is a folder maker in a test that fails at `:359`) |
| Q4 | K6: the `/var/tmp` decoys of the same two statements (`agent-box.test.mjs:229`, `bwrap.test.mjs:184`), which no guard reads, become owned folders too? | yes: same statement, same lifetime, same helper (like S5's ninth site, a fix that only satisfies the plan as written); brief-level |
| Q5 | K8: `reuse-scan.test.mjs`'s structural test is rewritten to "no raw creator in a `*.test.mjs`" with its companion on `test/helpers.mjs:42` (F2's file, kept)? | yes; when the followups plan's S2 changes `helpers.mjs:42`, its PR moves the companion |
| Q6 | K6, N3: the fake homes under `~` are owned and claimed but not collected after a kill until F1/S1's home sweep — accepted as a listed limit? | yes, listed in `research/module_tests.md`'s limits (K14); the home sweep stays F1/S1's (D7) |
