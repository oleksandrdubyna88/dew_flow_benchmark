# E6.S6 brief — `spikes/`: both guards, the shipped helper in four spikes, the minigit runner's start sweep (E11-6 to E11-8; claim C6; row E6-i's spike half)

> Status: **BUILT 2026-11-18 (review fixes 2026-11-18: reuse-scan reads every module of `test/`, the runner names what it keeps, `start-sweep.test.mjs` hardened, the docs say worktrees and class `other`) on `feat/e6-s6-spikes` by the coder (the coordinator pushes and opens the PR): § 8's commits 1 to 7 and § 17's fixes 1, 3 and 4 are in, each RED, GREEN, RED again quoted in the coder's report; both guards are clean in `spikes/` but for § 3a's two markers (Node 87 to 2, default 3 to 2, C# 16 to 4); fix 2 is the owner's host change; the four whole suites and the dotnet-dependent flows wait for it (§ 8 item 8).** *Was:* **plan only; S-3 ANSWERED 2026-11-18 by the parent's entry 12.1 (the owner: every whole suite green; runs without an own `TMPDIR`; § 17 lists the five fixes). Next: the owner's dotnet pack downgrade (§ 17 fix 2), then this brief's plan gate.** *Was:* **plan only, nothing implemented yet; reworked 2026-11-17 to the parent's epoch 12 (opened by the owner, `Narrowed: yes`, 0 changes, so the next accepted change counts as 12.1): § 13 and § 14's Q1–Q3 are answered by the parent's items 4.1–4.3 and 7; *Files* and *Done when* in item 8.1's form (§ 0.1, § 9); the three base whole-suite runs made at `a8106448` (§ 15). **STOPPED before its plan gate on § 16 S-3: row E6-m cannot be met as written — its claimed `TMPDIR` under `/tmp` itself breaks nine tests (minigit 4, lang-explore 3, lang-bench 2; green without it), and minigit-bench (7, cap 0) and lang-bench (3, cap 1) are over their caps; approving any of it is the parent's 12.1, the owner's.** Next: the owner's answer to § 16, then § 15's lists confirmed, then the plan gate.** *Was (`d836de52`):* **plan only, nothing implemented yet, 2026-11-17; STOPPED before its plan gate: § 13's two findings (S-1: `runner/run_samples.py` cannot change without files outside S6's *Files*; S-2: an open plan's line on the old names) and § 14's Q1 and Q2 (Q2: the whole `spikes/lang-bench` suite runs the system interpreter through the layer, so no sandbox holds it; O-e stays **blocked**) wait for the coordinator and the owner.** The item 8.1 observations are made at `a8106448` (§ 11): the `.py` units run in a `wslc` container (814 of 829, the other 15 host-bound) and those 15 in an owned throwaway venv (61 of 61 in their three files). The brief of story S6 of `todo/PLAN_test_leftovers_E6.md` as the epoch-11 amendment writes it (§ 6 S6 `:664-709`; D7 `:337-343`; claim C6 `:164`; § 11 `:806-844`). Its scope is S6's *Files* line (`:697-705`) as named paths (§ 0.1). The parent was then at epoch 11 with ONE change (entry 11.1); this brief's S-1 became entry 11.2, the eleventh full stop. **Now the parent is at epoch 12 with 0 changes: the next accepted change counts as 12.1, and item 8.1 lists what still counts.**
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
>
> *Epoch 12 (2026-11-17), read first:* **the parent** is `todo/PLAN_test_leftovers_rollout.md` § *Epoch 12* on conventions PR #180 (`docs/rollout-epoch-12`
> `a38c11c`): the ledger's `> Epoch 12 · approved 2026-11-17 · by owner · state: open` `:1093` and entry 11.2 `:911`; item 1 (the *Goal*'s bounded
> exception for S6's claimed folders outside the temp root); item 3.1 (rows E6-i, **E6-m**, E6-g); items 4.1 (the two runner lines, a standing list of
> exactly two; S7 waits for the language-comparison plan), 4.2 (S6's *Files* and *Done when*), 4.3 (Q3; S-2's dated note; Python), 4.6 (what got
> smaller), 7 (the owner's answers: question 1 and (a) yes), 8.1 (the new form, "zero unexplained findings", what still counts), 8.3 (the notes this
> brief's PR writes); the re-review record's cut row for this brief (`:973`). **"Stories"** for epoch 12 is benchmark PR #224
> (`docs/e6-stories-epoch-12`, head `b3b02a5b`, open): S6 `:701-780`, its *Epoch 12* paragraph `:744-780`, row E6-m `:111`. When #224 merges, `main`
> is merged into `feat/e6-s6-spikes` (a normal merge) and these citations move to it. Every **[M]** of the base runs (§ 15) is at `a8106448` (still
> benchmark `main`, `git fetch` and `git rev-parse FETCH_HEAD`, 2026-11-17).

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

**Epoch 12: *Files* in item 8.1's form (the parent's item 4.2; the stories at #224 `:744-761`) — this paragraph is the scope; the table and lists
below are OBSERVATIONS at `a8106448`.** Bounded folders: `spikes/policy-ab-quick/test/**`; `spikes/minigit-bench/test/**` and
`spikes/minigit-bench/run-tests.mjs`; `spikes/lang-bench/test/**`; `spikes/lang-explore/test/**` — never the files F1 and F2 name
(`spikes/lang-bench/lib/exec-probe/lines.mjs`, `probe.mjs`, `test/exec-layer-isolation.test.mjs`; `spikes/minigit-bench/test/helpers.mjs`,
`test/fake-home.mjs`, `lib/cache-snapshot.mjs`; `spikes/lang-explore/test/commands.test.mjs`), and never any spike's `lib/`, `runner/`, `inputs/`,
`hooks/` or product file. Plus **the docs a `git grep` at the base finds that describe what S6 changes** — the old spellings S6 removes (§ 11 O11's
pattern) AND the names of the files and commands it changes (§ 11 O13), each with what it says now:
`research/module_tests.md` (§ *The minigit stack bench scenarios*, § *The language-comparison runner scenarios*, § *Test leftovers*: a living
document, edited); `research/module_minigit_bench.md:785` (the row of `node spikes/minigit-bench/run-tests.mjs [glob…]`, "the one way to run the
spike's tests: lowered priority, `node --test` …", which S6's start sweep makes incomplete: a living document, ONE sentence added: "At its start it
runs the conventions' `sweepLeftovers` over the caller's `bench` folders in the temp root (E6.S6)"); `todo/PLAN_minigit_stack_bench_E2.md:89` (an
open plan this one does not own: ONE dated line beside it, the old text kept, the parent's item 4.3 point 2 word for word); and
`todo/PLAN_bench_test_leftovers_followups.md`, ONE dated note beside its S1 (`:81-99`), the old text kept, the parent's item 8.3: "*2026-11-xx
(E6.S6, the test-leftovers rollout):* E6.S6 claims fake homes under `~` and decoys under `/var/tmp`, which no sweep reads after a kill (the
rollout's goal exception, epoch 12); S1's home sweep, and a `/var/tmp` root if its safety check allows one, would collect them." Plus the plan
records (this brief, its `todo/README.md` row and promotion, the stories' progress line and § 11 copies). The other grep hits are read and stay
true (§ 11 O11, O13). **What still counts as a change** is the parent's item 8.1 list (a file outside these folders that is not a grep-found doc;
a finding left standing without a marker beyond the standing list; a new disposition; a contract change; an inline exemption not in § 3a; a suite
failure not on the confirmed list; a whole-run interpreter or host action outside the owner's allowances; in doubt, a change): STOP, write the
finding here, hand it to the coordinator. A missed line inside the folders, a moved line number or a count is the brief's to read, not a change.

*Was (epoch 11, the observations it gave):* The stories' S6 *Files* (`:697-705`): `spikes/policy-ab-quick/test/**`; the files of both guards' findings (the 39 below); `spikes/minigit-bench/run-tests.mjs`;
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
| `spikes/lang-bench/runner/run_samples.py` | 2 `raw-temp` (C#) | **not touched** — never `runner/` (item 4.2); its two lines are on the approved standing list of exactly two (entry 11.2; the parent's item 4.1) |
| `research/module_tests.md` | the start sweep, the sandbox commands, the limits (K14) | § *The minigit stack bench scenarios* `:169-252` (its *How it is run* `:180-190` and the flow rows), § *The language-comparison runner scenarios* `:253-300` (the units' sandbox), § *Test leftovers* `:632-680` (a paragraph on `spikes/` and *The limits*) |
| the plan records: this brief, its `todo/README.md` row, its promotion (`research/PLAN_test_leftovers_E6_S6_brief.md`, `research/README.md`), the stories' progress line, the copy of § 3a's list into the stories' § 11 (item 4.3) | `common/planning-docs.md`; item 4.3 | those records |

**Not touched, by name:** the files F1 and F2 name (`spikes/lang-bench/lib/exec-probe/lines.mjs`, `lib/exec-probe/probe.mjs`,
`test/exec-layer-isolation.test.mjs`; `spikes/minigit-bench/test/helpers.mjs`, `test/fake-home.mjs`, `lib/cache-snapshot.mjs`,
`spikes/lang-explore/test/commands.test.mjs`: the followups plan's S1 and S2, D7); every `lib/` and product file of every spike (the exec-layer
identity covers 22 files, among them `spikes/policy-ab-quick/lib/proc.mjs` and four of `spikes/minigit-bench/lib/`, § 11 O4; the runner's pin
covers `spikes/lang-bench/runner/`); `spikes/lang-bench/inputs/**`; `tests/`, `src/`, `hosts/`, `tools/` (S4, S5); `.github/` (S7); every
`research/` and `todo/` document but the grep-found docs of the epoch-12 paragraph above. *Was (epoch 11):* "every `research/` and `todo/` document
but those in the table (§ 13 S-2). A file the build, a test or a check needs outside the table above, or a decision outside the parent's § *Epoch
11*, is a change of the parent's epoch 11, and after entry 11.1 the next one is the ELEVENTH FULL STOP" — replaced by the epoch-12 paragraph's
"what still counts".

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
2. **§ 13's findings and § 14's Q1–Q3: answered** (the parent's items 4.1–4.3; the owner, 2026-11-17: question 1 and question (a) yes).
   **Before the plan gate:** E6's stories amendment for epoch 12 (#224) merged and merged into this branch; **the owner confirms § 15's three base
   lists in one line each** (row E6-m (2)). *Was:* "§ 13's findings answered and § 14's Q1 and Q2 answered before the plan gate. Q2 is the
   parent's O-e: S6 stops before its gate unless the owner says how the whole `spikes/lang-bench` suite runs under this machine's rule for Python".
2b. **The owner's question (b), at S6's merge** (the parent's item 7): if the language-comparison plan has not recorded the hand-off of the two
   runner lines by then, the owner decides: wait, or a coordinated re-pin of `runner/` carrying a rule §7 marker. S6's PR body names the hand-off.
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
  `--tmpfs` pair is found by that string). *Epoch 12:* this is the parent's goal exception (item 1, item 4.6 (3)): a killed run's claimed fake homes under `~` and decoys under `/var/tmp`
  (and the two `/run/user/<uid>` sockets, names only) are LISTED, not collected, until F1/S1's home sweep — row E6-m (7) lists
  `~/testsToDelete-*-bench-*`, `/var/tmp/testsToDelete-*-bench-*` and `/run/user/<uid>/testsToDelete-*` by name before and after each whole run,
  `research/module_tests.md`'s limits name them, and the followups plan's S1 gets the dated note of § 0.1. *Was:* "Limit: a killed run's fake home
  under `~` is not collected (the start sweep reads the temp root only; F1/S1 of the followups plan is the home sweep) — § 12 N3, Q6."
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
- **K12 — `runner/run_samples.py:35`, `:306` are not changed; they stand on the approved standing list of exactly two** (entry 11.2; the parent's
  item 4.1, the owner approving with epoch 12's `Narrowed: yes`). They are the language-comparison plan's: S7 waits for that plan's disposition,
  with the repository guard clean on them (row E6-g); S6's PR body names the hand-off (item 8.3); question (b) is the owner's at S6's merge (§ 2
  point 2b). *Was:* "(§ 13 S-1, Q1)". Their lifetime: inside an owned folder (the runner's `TMPDIR` is
  given by whoever launches it: a test's owned folder, `test_run_samples.py`'s `TemporaryFolderTests` `:430-452` proving nothing is left, or the
  layer's box `tmp`).
- **K13 — The Python sandbox (the parent's O-e).** (a) **The units, in `wslc`:** `python:3.14-slim@sha256:9aaa474097d094040649f2162a627c0049b408f52138d980916788d80f04802f`
  (Python 3.14.10; the host's is 3.14.4), the spike mounted READ-ONLY through `\\wsl.localhost\Ubuntu\…`, `-u <uid>:<gid>`, `--cpus 2 -m 2G`,
  `-e TMPDIR=/tmp` (the container's own, gone with `--rm`), `sh` as PID 1 so orphans are reaped (§ 11 O6), the prefixed `--name` and the owner
  label from the helper, `--rm`; nothing installed anywhere. It runs 814 of the 829 units; the other 15 need the host's user systemd manager and
  `bwrap` and skip there. (b) **The 15 host-bound units** (`test_rs_scope.py`, `test_rs_owner.py`, `test_rs_interrupt.py`, the users of
  `box_support.Scope`, whose line `:192` S6 changes): an owned throwaway venv (`python3 -I -m venv --without-pip <owned folder>/v`, the machine
  rule's fallback), `BENCH_REQUIRE_SANDBOX=1`, `TMPDIR` an owned folder, removed by exact path after. (c) **The whole suites with the system interpreter — allowed by the owner, 2026-11-17, two runs each (base, head):** `spikes/lang-bench`
  (`BENCH_REQUIRE_SANDBOX=1 LANG_V2_TASK_REPO=/home/jinx/git/dew_flow_ai_language_comparison node spikes/lang-bench/run-tests.mjs`; its layer
  finds `python3` with `findOnPath` on the fixed `SBIN_PATH`, `lib/exec-layer/run.mjs:79`, `:84-91`), and, by the answer to the parent's item 7
  question (a), `spikes/minigit-bench` (`BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs`; `lib/heldout-run.mjs:19`, `:121` name
  `/usr/bin/python3`) and `spikes/lang-explore` (`node --test spikes/lang-explore/test/`; `lib/score.mjs:71`, `lib/heldout-quick.mjs:173`, and
  three judge tests through `PATH`), each on lang-bench's terms: the suite's own interpreter as it calls it, standard library, nothing installed,
  `nice -n 19`, one at a time, each under its OWN claimed, empty `TMPDIR` under `/tmp` made by the shipped helper (row E6-m: not `$HOME`, which
  the boxes hide), the outside roots listed before and after (row E6-m (7)). **No other whole run:** per commit of § 8 only the changed test
  files that start no interpreter run (K15). *Was:* "(c) The whole `spikes/lang-bench` suite with `BENCH_REQUIRE_SANDBOX=1` and
  `LANG_V2_TASK_REPO`: blocked. … Q2 is the owner's."
- **K15 — Per commit, only the files that start no interpreter** (row E6-m; the parent's S-3). A changed test file runs alone at its commit
  only when neither it nor what it imports starts `python3` or `/usr/bin/python3` and it starts no layer run (the layer starts the runner): the
  coder lists, per commit, the changed files run and those left to the head's whole run, from `git grep -n -E "python3|/usr/bin/python|openRun|runProbe"`
  over the file and its relative imports. Known to start one (the parent's E12-5, read **[R]**): `minigit-bench/test/heldout-run.test.mjs`
  (S6 changes it), `heldout-hostile.test.mjs`; `lang-explore/test/judge.test.mjs`, `olympiad-calibrate.test.mjs`; every lang-bench file that
  calls `pythonUnits` (32) and the exec-layer and probe tests that open a run. policy-ab-quick starts none.
- **K14 — The docs.** `research/module_tests.md`: in the minigit section's *How it is run*, that `run-tests.mjs` sweeps at its start (and that the
  conventions' `low-priority.mjs node --test` path named by `PLAN_minigit_stack_bench_E2.md:86-87` does not); the flow row of
  `start-sweep.test.mjs`; in the lang-bench section, the units' sandbox commands (K13 (a), (b)) and what they do not cover (K13 (c)); in
  § *Test leftovers*, a paragraph *The spikes* (the shipped helper per test file, the units inside their owned `TMPDIR`, the two listed
  exemptions) and, in *The limits*, § 10's lines and § 12's N1–N3. Plus `research/module_minigit_bench.md:785`'s ONE sentence and the two dated notes of § 0.1 (the grep's docs, item 8.1). *Was:* "No other
  `research/` file (§ 13)."

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
| 12 | `minigit-bench/test/agent-box.test.mjs` | `:200` N legacy; `:229` N legacy+raw; `:233`, `:238`, `:239` N raw | `:200` marker (a socket); `:229` run (decoys); the rest read only | K6: `${owned.ownedName('agentsock')}.sock`; `[owned.ownedDir(t, 'adecoy'), owned.ownedDir(t, 'vadecoy', { parent: '/var/tmp' })] (the goal's bounded exception, K6)`, the `writeFileSync`s and the `finally` removal go; `owned.tempRoot()` ×3 |
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
| (4) the suites | § 7's runs at the base and the head | each suite's FAILED list at the head equals the base's as row E6-m approves it (policy-ab-quick's three, § 11 O10; the others § 15's lists as the owner confirms them, § 9); a run under an owned `TMPDIR` leaves no entry there (§ 11 O10: none at the base for policy-ab-quick) | — |
| (5) the units | K13 (a) and (b) at the base and the head | (a) 829 units, the base's 814 pass and 15 skip, nothing failed; every unit of the six changed `.py` files passes; (b) 61 of 61 with `BENCH_REQUIRE_SANDBOX=1` | — |

## 6. The guards in S6 — both guards, a count per class, at the base `a8106448` (E11-6, E11-7; the parent's item 8.1)

| Guard | Base, all | Base, `spikes/`, per class | Head, all (Q1 (b)) | Head, `spikes/` |
|---|---|---|---|---|
| Node (`--profile node --token bench --identity "the measuring harness"`) | **87** (2 `research/`, 85 `spikes/`) **[M]** | **85**: 46 `raw-temp`, 31 `legacy-name`, 8 `owner-blind` — policy-ab-quick 30 (25, 5, 0), minigit-bench 41 (18, 21, 2), lang-bench 8 (3, 5, 0), lang-explore 6 (0, 0, 6) | **2** | **0** (§ 3a's `prove.sh` marker clears its line) |
| Node, default profile | **3** (2 `research/`, `prove.sh:7`) | 1 `raw-temp` | **2** | 0 |
| copied C# (`--explicit only --filter-class "*LeftoversRepositoryGuardTests"`, information; S7 turns it on) | **16** (2 `research/` `raw-worktree`, 14 `spikes/`) **[M]** | **14**, all in `lang-bench`: 10 `raw-temp` (9 `.py` `mkdtemp(`, `prove.sh:7`), 3 `legacy-name`, 1 `owner-blind` | **4** | **2**: `runner/run_samples.py:35`, `:306` (`raw-temp`), the approved standing list of exactly two (entry 11.2) *Was:* "unless Q1 (a), then 0" |

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
# ENTRY 12.1 (§ 17) REPLACES THIS FOR THE HEAD RUNS: the wrapper claims NO TMPDIR; TMPDIR, TEMP and TMP are unset (/tmp), and it lists this user's
# /tmp entries before and after (§ 17 *The runs*). The lines below are the base runs' form (§ 15), kept as the record.
# THE WHOLE RUNS (K13 (c), row E6-m): base and head only, one at a time, each through the wrapper of § 15 (s6run.mjs, a scratch file OUTSIDE the
# repository, never committed): it claims ONE empty TMPDIR under /tmp with the shipped helper (owner = the wrapper's own live pid), lists
# ~/testsToDelete-*-bench-*, /var/tmp/testsToDelete-*-bench-* and /run/user/<uid>/testsToDelete-* before and after, runs the suite under
# nice -n 19 with TMPDIR/TEMP/TMP set to the folder, lists what is left in it, then removes the folder and its claim by exact path.
# Then s6parse.mjs (§ 15) gives the totals, the FAILED list (file, test, assertion) and the skipped list; --names gives the by-name list.
node s6run.mjs s6headpab  pab.log  "$PWD" '{}' -- node spikes/minigit-bench/run-tests.mjs '../policy-ab-quick/test/*.test.mjs'
node s6run.mjs s6headmgb  mgb.log  "$PWD" '{"BENCH_REQUIRE_SANDBOX":"1"}' -- node spikes/minigit-bench/run-tests.mjs
node s6run.mjs s6headlex  lex.log  "$PWD" '{"BENCH_REQUIRE_SANDBOX":"1"}' -- node --test --test-concurrency=8 spikes/lang-explore/test/
node s6run.mjs s6headlb   lb.log   "$PWD" '{"BENCH_REQUIRE_SANDBOX":"1","LANG_V2_TASK_REPO":"/home/jinx/git/dew_flow_ai_language_comparison"}' -- node spikes/lang-bench/run-tests.mjs
node spikes/minigit-bench/run-tests.mjs '../policy-ab-quick/test/start-sweep.test.mjs'                 # (1), (1b); starts no interpreter
# PER COMMIT (K15): only the changed files that start no interpreter, e.g. node spikes/minigit-bench/run-tests.mjs test/ledger.test.mjs …
# K13 (a): the .py units in wslc. NAME and LABEL from the helper, never typed:
NAME=$(node -e "import('./.agents/conventions/tools/lib/owned.mjs').then(m=>console.log(m.ownedFor({token:'bench'}).ownedName('pyunits')))")
# the label names THIS shell ($), which outlives the container; no start, so a reader judges it unknown and keeps it (--rm removes it)
LABEL="$(node -e "import('./.agents/conventions/tools/lib/owned.mjs').then(m=>console.log(m.thisOwnerToken()+':'+m.hostId(require('node:os').hostname(), process.env.WSL_DISTRO_NAME ?? '')))")@linux:$"   # 12.1 gate fix (fc1cf87a): $, the shell's pid, not a literal $; the coder completes the owner form (pid:start:pidns, as § 15's claim shows) with the shipped helper's own function for a pid if it has one, else /proc/$/stat field 22 and the inode of /proc/$/ns/pid, and checks the label parses as that live owner
W='\\wsl.localhost\Ubuntu'$(pwd | tr / '\\')'\spikes\lang-bench'
wslc.exe run --rm --name "$NAME" --label "testsToDelete.owner=$LABEL" --cpus 2 -m 2G -u "$(id -u):$(id -g)" -e TMPDIR=/tmp \
  -v "$W:/w:ro" -w /w python:3.14-slim@sha256:9aaa474097d094040649f2162a627c0049b408f52138d980916788d80f04802f \
  sh -c 'python3 -I -B -m unittest discover -v -s test/py -p "test_*.py" & wait $!'                      # base: 829, 814 ok, 15 skipped, ~8 min
# K13 (b): the 15 host-bound units in an owned throwaway venv, then removed by exact path
V="$P/venv"; /usr/bin/python3 -I -m venv --without-pip "$V"; mkdir -m 700 "$P/t"
for f in test_rs_scope.py test_rs_owner.py test_rs_interrupt.py; do (cd spikes/lang-bench && BENCH_REQUIRE_SANDBOX=1 TMPDIR="$P/t" nice -n 19 "$V/bin/python3" -I -B -m unittest discover -v -s test/py -p "$f"); done   # 31 + 27 + 3
rm -rf "$P/venv" "$P/t"; systemctl --user list-units --all 'testsToDelete-*' --no-legend | wc -l    # 0
# K13 (c): the whole lang-bench suite is one of THE WHOLE RUNS above (allowed by the owner, 2026-11-17; was: "ONLY after the owner's answer to Q2")
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
3. **minigit-bench (K1–K3, K6–K8, § 4 rows 11–22):** Node 55 → 14; the commit's changed files that start no interpreter (K15).
4. **lang-explore (K4, § 4 rows 23–26):** Node 14 → 8; the changed files that start no interpreter (K15).
   *Was (`d836de52`):* "the whole minigit suite with `BENCH_REQUIRE_SANDBOX=1`", "the lang-explore suite" — the whole suites run at the base
   and the head only (K13 (c)).
5. **lang-bench Node and the shell script (K5, K11, § 4 rows 27–32):** Node 8 → 0, default profile 3 → 2, C# 16 → 15.
6. **lang-bench Python (K10, § 4 rows 33–38):** C# 15 → 4; K13 (a) and (b) at the head (the units in the sandbox; no whole run per commit); (3) RED, GREEN, RED again; `pin-runner.mjs --check`
   and `pin-exec-layer.mjs --check` exit 0 (nothing pinned changed).
7. **The docs commit:** K14 (`research/module_tests.md`, `research/module_minigit_bench.md:785`); the two dated notes (§ 0.1); § 3a's list and
   § 15's confirmed base lists copied into the stories' § 11; the stories' progress line; this brief's progress line.
8. **The head's whole runs** (K13 (c), § 7), one suite at a time, each under its own claimed `TMPDIR`, reconciled with § 15 by name (row E6-m);
   the policy-ab-quick run (it starts no interpreter) at the head too.

**Size.** About 40 changed files, most a few lines each; three new test files of about 120 lines in all. One Sonnet coder.

## 9. Done when, and what the PR body quotes (the stories' S6 *Done when* `:706-709`, E11-6, E11-7)

**Epoch 12 (the parent's item 4.2 and row E6-m; the stories at #224 `:762-780`), which this section now reads:**
- **Zero unexplained findings of both guards in `spikes/`:** each finding is gone, cleared by a rule §7 marker § 3a listed (`box_support.py`'s unit
  name, `legacy-name`; `prove.sh`'s `mktemp`, `raw-temp`; copied into the stories' § 11 by the PR), or on the approved standing list, which is
  exactly `runner/run_samples.py:35` and `:306` (C# `raw-temp`, entry 11.2); none for `owner-blind`; no finding added outside `spikes/`.
  *Observations at `a8106448`, not scope:* Node 85 → 0 in `spikes/`, C# 14 → 2; totals 87 → 2, default 3 → 2, C# 16 → 4.
- **Row E6-m, the suites:** (1) policy-ab-quick fails exactly its three named tests (§ 11 O10), each at the same assertion; (2) minigit-bench,
  lang-explore and lang-bench fail exactly § 15's base lists AS THE OWNER CONFIRMED THEM (caps: lang-explore ≤ 10, lang-bench ≤ 1, minigit-bench 0),
  each the same way; (3) both runs of each suite complete and print their totals; (4) executed and skipped tests reconciled BY NAME against § 15's
  lists: a test missing at the head without a reason S6's diff gives (a test S6 renames, named on the PR: K7's and K8's titles), or a test newly
  skipped at the head, stops S6; (5) a head failure not on the confirmed list is RED and S6's to fix — a timing test of the followups plan's S5 list
  included: it is recorded on that list by a dated line with `/proc/loadavg` (`todo/PLAN_bench_test_leftovers_followups.md:126`), S6 re-runs
  nothing, the owner decides on ONE repeat run; (6) each run's `TMPDIR` is empty after the run except entries F2's makers write
  (`test/helpers.mjs:42`, `test/fake-home.mjs:15`), listed by name at base and head, equal in kind, removed by exact path; (7) the outside roots
  listed by name before and after each run, equal at base and head, any entry a run adds named on the PR and removed by exact path. For an
  approved failure in a file S6 changes, the PR says whether its failing assertion runs before or after S6's changed line (§ 15).
- `pin-runner.mjs --check` and `pin-exec-layer.mjs --check` exit 0; `git diff --name-only <base>..HEAD` within § 0.1's folders and grep-found
  docs; `plan-lifecycle.mjs` clean; § 3a and § 15's confirmed lists copied into the stories' § 11; the two dated notes written.

*Was (`d836de52`, epoch 11), kept for the record:*
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
under the whole lang-bench suite with the layer's interpreter (K13 (c), Q2). The two runner lines (the standing list; the language-comparison plan's, entry 11.2). What the plain helper modules no guard
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
| O13 | item 8.1 (S-4): the docs that name the FILES and COMMANDS S6 changes | `git grep -n -E` of the 39 changed files' names, `minigit-bench/run-tests\.mjs` and the new test's runner over `*.md` but the E6 documents, at `a8106448`; then the hits that speak of temp folders, names, sweeps, decoys or sockets read one by one | — | — | hits in 14 files (`research/module_tests.md` 42, `todo/PLAN_minigit_stack_bench_E2.md` 13, `research/module_minigit_bench.md` 9, the followups plan 7, …). **On S6's docs scope:** `research/module_minigit_bench.md:785` ("the one way to run the spike's tests: lowered priority, `node --test` …": incomplete after K9, one sentence added) and `research/module_tests.md`. **Read and still true after S6:** `research/PLAN_lang_explore_task1.md:286` (lang-explore's tests take folders through `test/helpers.mjs` `tmpDir`: still so), `research/module_lang_bench.md:897` (a dated record of seven owner-blind lines at `469a5109`), `research/module_tests.md:211`, `:218` (flow rows: the products' behaviour, unchanged), `:346`, `:457`, `:487` (dated run records), `todo/PLAN_minigit_stack_bench_E2.md:517` (the decoy home's setup, unchanged in kind) and `:822` (a command), the followups plan's `:29`, `:33`, `:124-130`, `:144` (dated records) **[M]** |

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

## 13. STOP findings — before the plan gate — ANSWERED (the parent's epoch 12)

**Answered 2026-11-17.** S-1 became the parent's entry 11.2 (the eleventh full stop); the owner decided it with epoch 12: option (b) with an
explicit prerequisite — the two lines stand on an approved standing list of exactly two, S7 waits for the language-comparison plan's disposition
with the repository guard clean on them (the parent's items 4.1, 4.6; K12). S-2 is decided the other way from this brief's recommendation:
by item 8.1's *Files* form the line is on S6's docs scope, and S6's PR adds ONE dated line beside it, the old text kept (the parent's item 4.3
point 2; § 0.1). The text below is the finding as written at `d836de52`.

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
| Q1 | § 13 S-1: the two runner lines — (a) re-pin, (b) the runner's own product sites, carried to S7, or (c) S7's alone? | **(b)**, as § 13 argues; the coordinator says whether it counts as a change before the plan gate. **Answered** (the owner, epoch 12): (b), counted as entry 11.2, with S7's prerequisite (the parent's item 4.1) |
| Q2 | O-e, K13 (c): the whole `spikes/lang-bench` suite with `BENCH_REQUIRE_SANDBOX=1` and `LANG_V2_TASK_REPO` runs `/usr/bin/python3` through the layer's fixed path; no `wslc` image or venv holds it (O8). (A) the owner allows that interpreter for S6's two whole-suite runs (base, head) only — standard library, `-I -B`, nothing installed, lowered priority, the layer's own choice, pinned by its identity; or (B) S6's lang-bench evidence is K13 (a) and (b) only, and the *Done when*'s "lang-bench green as `module_tests.md` runs it" is not met (a narrowing: 11.2, the eleventh full stop) | **(A)**: S6 changes no layer file and no runner file, the units S6 changes are proved in the sandbox (O5–O7), and the whole run only shows nothing else moved. It is the owner's rule; the consultants cannot settle it. **Answered** (the owner, 2026-11-17): (A), and by the parent's item 7 question (a) the same for the minigit-bench and lang-explore whole suites, two runs each, each with an owned `TMPDIR` under `/tmp` (K13 (c)) |
| Q3 | The stories' "the suites … green": policy-ab-quick has three failures at the base (O10, Windows-path cases named as pre-existing in its own plan); read as "the FAILED list equal to the base's", as S5 read its Docker-bound 572? | yes; S6 changes none of those assertions (`review2.test.mjs:354` is a folder maker in a test that fails at `:359`). **Answered** (the parent's item 4.3 point 1, row E6-m (1)): exactly the three named, each at the same assertion; the other suites' standing failures are § 15's base lists as the owner confirms them, within #222's caps; Q4–Q6 stay brief-level (the parent's item 4.2) |
| Q4 | K6: the `/var/tmp` decoys of the same two statements (`agent-box.test.mjs:229`, `bwrap.test.mjs:184`), which no guard reads, become owned folders too? | yes: same statement, same lifetime, same helper (like S5's ninth site, a fix that only satisfies the plan as written); brief-level |
| Q5 | K8: `reuse-scan.test.mjs`'s structural test is rewritten to "no raw creator in a `*.test.mjs`" with its companion on `test/helpers.mjs:42` (F2's file, kept)? | yes; when the followups plan's S2 changes `helpers.mjs:42`, its PR moves the companion |
| Q6 | K6, N3: the fake homes under `~` are owned and claimed but not collected after a kill until F1/S1's home sweep — accepted as a listed limit? | yes, listed in `research/module_tests.md`'s limits (K14); the home sweep stays F1/S1's (D7) |

## 15. The base whole-suite runs (row E6-m) — made 2026-11-17 at `a8106448`

**How they ran.** Benchmark `main` is still `a8106448` (`git fetch`, `FETCH_HEAD`); the worktree's `spikes/` equals it (`git diff --quiet
a8106448 HEAD -- spikes`). One suite at a time, `nice -n 19`, each through the wrapper `s6run.mjs`: ONE `TMPDIR` claimed by the shipped helper
under `/tmp` (`ownedDir(scope, 's6base<suite>', { parent: '/tmp' })`, its claim naming the wrapper's own live pid, e.g.
`u1000:jinx~Ubuntu@linux:3390967:1794951867:4026532219`), `TMPDIR`, `TEMP`, `TMP` set to it, the outside roots listed before and after, the
folder's entries listed after, then the folder removed by exact path. The system interpreter ran only inside these whole runs (the owner's
allowance) and inside K13 (a)'s `wslc` container; the single-file comparisons below run files that start none (their relative imports walked:
`helpers.mjs:62` only checks that `/usr/bin/python3` exists; a sampler of `pgrep -u 1000 python3` saw none). The logs, the reports, the by-name
lists (`*.names`: status and name of every reported test, for the head's reconciliation) and the two scripts are kept OUTSIDE git in
`/home/jinx/rsd/e6s6/base-a8106448/` (960 KB). **[M]** all.

| Suite | Command (cwd: the worktree) | Totals | Wall, `/proc/loadavg` start → end | Left in the `TMPDIR` |
|---|---|---|---|---|
| policy-ab-quick | `node spikes/minigit-bench/run-tests.mjs '../policy-ab-quick/test/*.test.mjs'` | 156 tests, 152 pass, 3 fail, 1 skipped | 7 s, 21.12 → 20.80 | nothing |
| minigit-bench | `BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs` | **1259 tests, 1252 pass, 7 fail, 0 skipped** | 701 s, 14.56 → 13.03 | 9: `r-v1.json`, `r-v2.json`, `private-v1.json` and their `.stderr` (`test/heldout-run.test.mjs:241`, `:259` and `runToy` write `path.join(work, '..', …)`, the temp root itself; no guard reads it: a missed site inside S6's folders, S6 moves them into an owned folder), three `testsToDelete-mgb-ledger-*` (`test/ledger.test.mjs:12`, § 4 row 18). None from F2's makers. Removed with the folder |
| lang-explore | `BENCH_REQUIRE_SANDBOX=1 node --test --test-concurrency=8 spikes/lang-explore/test/` | **991 tests, 981 pass, 9 fail, 1 skipped** | 315 s, 11.91 → 11.30 | `testsToDelete-mgb-home-PdJWCc` (F2's maker, `test/helpers.mjs:42` `tmpDir('home')`, 24 MB, a read-only `typescript-6.0.3` toolchain tree inside: F2's own defect; the helper's removal failed with `EACCES`, so it was removed by hand by exact path after `chmod -R u+w`, no link followed, with the folder's claim); `node-compile-cache` (Node's, removed with the folder) |
| lang-bench | `BENCH_REQUIRE_SANDBOX=1 LANG_V2_TASK_REPO=/home/jinx/git/dew_flow_ai_language_comparison node spikes/lang-bench/run-tests.mjs` | **2399 tests, 2396 pass, 3 fail, 0 skipped** | 635 s, 13.70 → 23.80 | `testsToDelete-u1000-bench-lrun-r5dIM5` and its claim (the run folder `openRun` made because the owned `TMPDIR` was not refused, below). Removed with the folder |

**The outside roots** (row E6-m (7)), before and after EVERY run, equal: 18 entries, all F1's — nine
`~/testsToDelete-u1000-bench-sentinel-*` folders with their claims (2026-11-02 to 2026-11-04; the followups plan's F1, not removed: the owner's
word is needed); nothing under `/var/tmp` or `/run/user/1000` matching `testsToDelete-`. No run added or removed one.

**The failures, test by test** (file:line of the test · name · the assertion · `where` = the failing line). *Caused by the owned `TMPDIR`* is
shown by running the same file ALONE with the claimed folder and without it (`TMPDIR` unset, so `/tmp`): same result with both = the host's;
red only with the folder = the folder's.

| Suite | Test | Assertion | With the folder alone | Without it | Reading |
|---|---|---|---|---|---|
| minigit | `test/agent-box.test.mjs:226` `The_agent_box_sees_only_its_own_tmp` | "only box/tmp's content (control: it IS listed)" at `:234` | red | **green** | the folder's: the box puts its tmpfs on `/tmp`, the test lists `os.tmpdir()` (a file S6 changes; the assertion is after S6's `:229`, `:233`) |
| minigit | `test/bare-collect-allow.test.mjs:65` `The_pilot_commands_given_no_toolchain_flag_…_only_the_sessions_own_dirs_are_exempt` | "the config dir of the session itself is not among them" at `:69` | red | **green** | the folder's (S6 changes this file, `:30`; the assertion is in another test) |
| minigit | `test/bare-wiring.test.mjs:209` `The_sampler_counts_the_cells_of_the_phase_as_ours_and_a_stranger_as_foreign` | `ENOENT … <TMPDIR>/testsToDelete-mgb-rsd-…/pairs/p1-v1-cpu.ndjson` at `:205` | red | **green** | the folder's |
| minigit | `test/bwrap.test.mjs:182` `The_box_shows_only_its_own_bound_directories_under_tmp_and_var_tmp` | "--tmpfs <TMPDIR> is in the argv" (`withoutPair`, `:35`) | red | **green** | the folder's: the box's argv says `--tmpfs /tmp`, the test looks for `os.tmpdir()` (S6 changes `:184-190`; the assertion runs at `:190`) |
| minigit | `test/egress-box.test.mjs:88` `Plain_HTTP_is_refused_and_the_restore_does_not_need_it` | "the restore succeeds with every plain-HTTP request refused" at `:26` | red | red | the host's (a restore through the egress proxy; the box and network class) |
| minigit | `test/egress-box.test.mjs:107` `Npm_and_dotnet_restore_through_the_proxy_from_inside_the_box` | "did not match /restore=0/" at `:26` | red | red | the host's (the same) |
| minigit | `test/preflight.test.mjs:17` `The_preflight_reads_the_pinned_toolchain_from_inside_a_box_and_says_what_is_wrong_when_it_is_not` | `{"ok":false,…"problems":["claude is 2.1.326, the pin is 2.1.310"]}` at `:23` | red | red | the host's: the installed Claude CLI moved past the spike's pin (S6 changes this file at `:33`, in another test) |
| lang-explore | `test/boxes.test.mjs:42` `The_agent_box_gives_both_stacks_the_locale_of_the_plan` | `strictEqual` at `:37` | red | red | the host's (box tests: the locale) |
| lang-explore | `test/boxes.test.mjs:55` `The_scoring_box_gives_both_stacks_the_locale_too` | `strictEqual` at `:37` | red | red | the host's (the same) |
| lang-explore | `test/boxes.test.mjs:93` `The_process_run_sh_starts_is_the_program_itself_dotnet_or_node_and_not_bash` | `NU1101: Unable to find package …` at `:102` | red | red | the host's: the offline NuGet cache lacks `Microsoft.NETCore.App.Host.ubuntu.26.04-x64`, `…App.Ref`, `AspNetCore.App.Ref` for this host's RID |
| lang-explore | `test/e2e.test.mjs:45` `The_rehearsal_runs_a_pair_end_to_end_for_free_and_every_criterion_holds` | "the rehearsal is refuted" at `:51` (csharp "build FAILED", "both scored 100 %" false) | not run alone (starts the judge) | — | the host's by its message (the same NuGet gap; the rehearsal class) |
| lang-explore | `test/e2e.test.mjs:102` `A_pair_runs_through_the_pilots_loop_is_recorded_and_noted_and_the_barrier_refuses_what_is_not_next` | `5 !== 0` at `:126` | not run alone | — | the rehearsal class; reading: the same C# build (not proved) |
| lang-explore | `test/heldout-real.test.mjs:35` `Both_references_pass_every_case_and_each_mutant_fails_exactly_the_cases_its_manifest_predicts` | "the csharp reference does not build: …ubuntu.26.04-x64 wurde nicht gefunden" at `:43` | not run alone | — | the host's (the same NuGet gap, with the host's German `dotnet` locale) |
| lang-explore | `test/olympiad-rehearse.test.mjs:42` `The_rehearsal_runs_a_pair_end_to_end_for_free_for_each_of_tasks_02_03_and_09_over_that_task_s_own_material` | "socket path <TMPDIR>/…/net/proxy.sock is 143 bytes; a unix socket path holds at most 108" at `:50` | not run alone | — | **the folder's** by arithmetic: the claimed folder adds 43 bytes; under `/tmp` the path is 100 |
| lang-explore | `test/olympiad-rehearse.test.mjs:72` `A_stub_pair_past_the_pause_point_runs_only_with_allow_over_and_the_owner_s_text_is_in_the_pair_record` | the same, at `:97` | not run alone | — | **the folder's** (the same) |
| lang-explore | `test/quick-units.test.mjs:197` `The_held_out_reader_refuses_a_folder_a_fifo_and_a_socket_in_the_cases_folder_and_never_throws` | `listen EINVAL … <TMPDIR>/testsToDelete-mgb-quick-cases-…/heldout-05/cases/h002.out` at `:201` | not run alone (imports the judge's modules) | — | **the folder's** by arithmetic: 110 bytes with the folder, 67 under `/tmp` |
| lang-bench | `test/exec-layer-parent.test.mjs:12` `A_parent_that_cannot_be_swept_later_is_refused_naming_the_rule` | `<TMPDIR>` not refused at `:21` | red | **green** (3 of 3) | **the folder's**: the test expects `os.tmpdir()` (world-writable `/tmp`) refused; the claimed folder is the caller's own, `0700` (S6 changes `:19`; the assertion runs after it) |
| lang-bench | `test/exec-layer-parent.test.mjs:36` `openRun_refuses_a_parent_before_it_makes_anything` | `strictEqual` at `:38` | red | **green** | **the folder's** (S6 changes `:37`; the assertion is after it); with the folder `openRun` made `testsToDelete-u1000-bench-lrun-*` |
| lang-bench | `test/py/test_rs_session_net.py:179` (through `test/support/py-units.mjs:51`) `Bytes_still_unsent_at_the_deadline_end_the_group_with_a_timeout_naming_the_connection` | `b'' != b'aaaa' : the other writer completed` | — | — | timing under load (23.80 at the run's end): the same unit file passed 17 of 17 once in K13 (a)'s `wslc` container (2 CPUs, load 21.56) — a diagnosis, not a replacement. Not on the followups plan's S5 list |

**Skipped, by name:** minigit 0; lang-explore 1 — `The_whole_leak_check_over_5_million_case_lines_and_a_25_MB_haystack_finishes_in_under_60_seconds_and_finds_a_planted_window`
(skipped by its own option even under `BENCH_REQUIRE_SANDBOX=1`); lang-bench 0; policy-ab-quick 1 — `REAL conventions: both arms are built from
the pin, differ in exactly the declared files, in ONE identical-looking commit`.

### Base failures for the owner to confirm

- **policy-ab-quick** (approved by name in row E6-m (1)): 3 — `review2.test.mjs:300` "a runs root must be at least two folders deep" (`:301`), `review2.test.mjs:353` "shedding a real directory still works, and refuses a shallow root outright" (`:359`), `review4.test.mjs:149` "a report whose every cell is void never overwrites the results file; it goes to the runs root" (`:158`).
- **minigit-bench** (cap 0): **7 — OVER THE CAP.** 4 caused by the claimed `TMPDIR` itself (`agent-box.test.mjs:226`, `bare-collect-allow.test.mjs:65`, `bare-wiring.test.mjs:209`, `bwrap.test.mjs:182`; green without it), 3 the host's (`egress-box.test.mjs:88`, `:107`, the proxy restores; `preflight.test.mjs:17`, Claude CLI 2.1.326 against the pin 2.1.310).
- **lang-explore** (cap 10): 9 — within the count, but 3 are caused by the claimed `TMPDIR` (`olympiad-rehearse.test.mjs:42`, `:72`, `quick-units.test.mjs:197`: socket paths over 108 bytes), outside #222's classes; 6 the host's (`boxes.test.mjs:42`, `:55`, `:93`; `e2e.test.mjs:45`, `:102`; `heldout-real.test.mjs:35`: the locale and the NuGet gap for the `ubuntu.26.04-x64` RID).
- **lang-bench** (cap 1): **3 — OVER THE CAP.** 2 caused by the claimed `TMPDIR` (`exec-layer-parent.test.mjs:12`, `:36`; green without it), 1 a timing unit under load (`test_rs_session_net.py:179`).

## 16. STOP finding — the base runs (before the plan gate)

**S-3 — Row E6-m cannot be met as written; S6 stops before its plan gate.** Two causes, both observed (§ 15):

1. **The claimed `TMPDIR` under `/tmp` that row E6-m requires breaks nine tests in three suites** (minigit 4, lang-explore 3, lang-bench 2):
   the boxes put their tmpfs on the literal `/tmp` while the tests read `os.tmpdir()`; the layer refuses only a world-writable temp root; the
   folder's 43 extra bytes push two unix socket paths over 108. Row E6-m's own alternative — "or S6's brief shows at its base that one such box
   test file gives the same result with and without that folder" — was tried and is REFUTED (`bwrap.test.mjs`, `agent-box.test.mjs`,
   `exec-layer-parent.test.mjs`: green without, red with). None of the nine is a defect of the code under test.
2. **Even without those nine, two suites are over their caps or outside #222's classes:** minigit-bench keeps 3 host failures against a cap of 0
   (the egress proxy's restores; the Claude CLI moved past the spike's pin, 2.1.326 against 2.1.310); lang-bench keeps 1 timing failure (its cap
   of 1 holds, but the class is not one of #222's named ones unless it is that body's "one priority test", which was not identified).

Approving either is a counted change of the parent (item 8.1, kinds 6 and 7: a suite failure not on the approved list; a whole-run host action
outside the allowance if the `TMPDIR` rule changes) — **12.1**, not a full stop by itself. The owner decides; options, for the coordinator:
(a) the whole runs keep row E6-m's claimed `TMPDIR` and the nine folder-caused failures are approved by name as standing at base and head (the
cleanest evidence that S6 adds nothing; the lists above); (b) the whole runs go without a `TMPDIR` of their own (`/tmp`), each run's new `/tmp`
entries of this user listed by name before and after and removed by exact path when they are the run's (as the single-file runs above did:
the minigit six left none; lang-explore's `boxes.test.mjs` left six empty folders, removed with `rmdir`) — then minigit's list is 3 and
lang-bench's 1; (c) a shorter claimed folder (it would not fix the four box tests or the two layer tests). And in every option minigit-bench's 3
host failures need the owner's approval by name over its cap of 0 (or a fix of the host: the proxy, the CLI pin — not S6's). **Recommended:
(b) for the evidence, with minigit's 3 and lang-bench's 1 approved by name as the base lists**, because S6's own changes then run where the
suites were written to run, and the folder adds failures that would mask S6's own at the head. The head's whole runs wait for the answer.

**ANSWERED 2026-11-18 — the parent's entry 12.1** (conventions #181; E6's stories #225). The owner: "чинить как хочешь, но все должно быть
зеленым". So: option (b) for the runs, and NO standing test failure: every one of § 15's 22 failures is fixed or shown gone at the head (§ 17).
First change of epoch 12; the next accepted change is the twelfth full stop.

## 17. Entry 12.1 — the fixes, each RED → GREEN → RED again (written 2026-11-18, before the plan gate)

**The runs (row E6-m as 12.1 reads it).** Each whole suite at base and head as § 7, `nice -n 19`, one at a time, but WITHOUT a `TMPDIR` of its
own: `TMPDIR`, `TEMP`, `TMP` unset, so `/tmp`. Before and after each run, this user's entries directly under `/tmp` are listed by name
(`find /tmp -maxdepth 1 -user $(id -u) -printf '%f\n' | sort`). An entry that appeared during the run is removed by exact path ONLY when
it is verifiably the run's (gate `fc1cf87a`, finding 0: other sessions of this user write `/tmp` meanwhile): its claim (the shipped helper's
`.owner` record) names a pid that was in the run's process tree (the wrapper records the tree's pids every second into its log) and is now
dead, or its name carries a token only the suite makes (the `testsToDelete-mgb-…`, `testsToDelete-u<uid>-bench-…` and `pab-…` makers § 15
named) AND `fuser`/`/proc/*/fd` show no live process holding it. `rmdir` for an empty folder; `rm -r` only after `ls -la` shows the run's
files and no link followed. Anything ambiguous is left untouched and named in the PR body as "not attributed, not removed". Row E6-m (4), (5) and (7) stand unchanged. The base runs of § 15 stay the base evidence; the nine failures they show only with the
claimed folder are re-checked by the head runs (they must be green there; red = S6's to fix).

**Fix 1 — policy-ab-quick's three (`test/review2.test.mjs:300`, `:353`; `test/review4.test.mjs:149`).** Cause **[M]**: the tests hand
`D:/…` to `lib/safe-rm.mjs` (`depthOf`, `assertRunsRoot`), `shedCell` and `reportTarget`; on Linux `path.resolve('D:/')` is `<cwd>/D:`, deep and
relative. Fix, tests only (no `lib/` byte): a platform root `const ROOT = path.parse(process.cwd()).root` (`/` here, the drive on Windows),
and every literal built from it with `path.join` (`ROOT`, `ROOT/rsd`, `ROOT/rsd/_s2`, `ROOT/rsd/_s2c`, `ROOT/x/y.json`); the expected strings
compared after the same `replace(/\\/g, '/')`. RED: the three fail at the base (§ 15, `pab.log`). GREEN: the file green on Linux. RED again:
a scratch copy of `lib/safe-rm.mjs` with `depthOf(p) < 1` in `assertRunsRoot`, imported by a copy of the test, turns test `:300` red; the
scratch copy is deleted (never committed).

**Fix 2 — the C# builds and restores (8 tests: `lang-explore` `test/boxes.test.mjs:42`, `:55`, `:93`, `test/e2e.test.mjs:45`, `:102`,
`test/heldout-real.test.mjs:35`; `minigit-bench` `test/egress-box.test.mjs:88`, `:107`).** Cause **[M]**: host version skew. On 2026-11-14
apt upgraded `dotnet-apphost-pack-10.0`, `dotnet-targeting-pack-10.0`, `aspnetcore-targeting-pack-10.0` to 10.0.15 (`/var/log/apt/history.log`)
and kept `dotnet-sdk-10.0` 10.0.114, whose `Microsoft.NETCoreSdk.BundledVersions.props:22` says `BundledNETCoreAppPackageVersion` 10.0.14; the
SDK then asks NuGet for `Microsoft.NETCore.App.Ref`, `Microsoft.AspNetCore.App.Ref` and `Microsoft.NETCore.App.Host.ubuntu.26.04-x64` and gets
NU1101 (offline: no source; online: the Ubuntu-RID host pack is on no feed). Measured 2026-11-18 in the scratchpad: the template
`templates/csharp` builds offline with the 10.0.14 packs extracted from Launchpad's debs (`-p:NetCoreTargetingPackRoot=<extracted>`), prints its
line, and fails with NU1101 on the host's packs as they are; `apt-get -s install --allow-downgrades` of the three debs: exactly 3 downgraded,
0 removed. Fix, host only (the owner runs it; no repo byte; consult `1aaccdae`, option 1; option 2 — the pin to 10.0.115 — would change the
frozen comparison material `templates/csharp/global.json`): `sudo apt install --allow-downgrades` of the three debs kept in
`/home/jinx/rsd/e6s6/dotnet-10.0.14-packs/` (sha256 `2ae1bdc2…` apphost, `af49d999…` targeting, `728f8529…` aspnetcore), then `sudo apt-mark
hold` of those three and `dotnet-sdk-10.0`, `dotnet-runtime-10.0`, `dotnet-host-10.0`, `dotnet-hostfxr-10.0` (held so the nightly upgrade
cannot skew them again; moving them is the language-comparison plan's pin decision). RED: § 15. GREEN: the eight tests' files alone
(`boxes.test.mjs` first; `e2e.test.mjs` and `heldout-real.test.mjs` start the judge, so only inside the head's whole run), then the head runs.
RED again: the scratch template build with `NetCoreTargetingPackRoot` pointed at a copy holding only the 10.0.15 packs → NU1101. If a test of
the eight is still red after the downgrade, its own cause is measured and fixed before the merge (not approved).

**Fix 3 — `minigit-bench` `test/preflight.test.mjs:17`.** The pinned copy exists on this host **[M]** 2026-11-18: `ls -la ~/rsd/minigit/toolchains/` →
`-r-xr-xr-x … 291638640 Oct 30 15:25 claude-2.1.310` (gate `fc1cf87a`, finding 3), so the fix makes no new skip here. Cause **[M]**: the test takes `findOnPath('claude')` (2.1.326 today) while the
pilot runs the pinned copy `~/rsd/minigit/toolchains/claude-<pin>` (`lib/bare-init.mjs:182` makes it; `lib/bare-config.mjs:143` uses it); the
preflight rightly says "claude is 2.1.326, the pin is 2.1.310". Fix, test only: `claudeBinary` = `path.join(os.homedir(), 'rsd', 'minigit',
'toolchains', `claude-${stacks.pins.claude}`)`, and `hostLacks` naming that path when it is absent (as the test already does for the
TypeScript toolchain). RED: § 15. GREEN: the file alone. RED again: the same test with `stacks.pins.claude` set to `9.9.9` in a scratch
copy → its `hostLacks` skip names the missing path (not a pass); with the PATH binary put back → the base's red.

**Fix 4 — `lang-bench` `test/py/test_rs_session_net.py:179`.** Cause **[R]**, to measure in the coder's RED: `session(default_within_ms=100)`
starts the group's deadline before the writer threads start (`runner/rs_net.py:262`, `Gate(…, now + within)`); on a loaded host (load 23.80
at the base run's end) 100 ms can pass before writer `a` sends, so `a` sends nothing. Fix, test only (no `runner/` byte: the pin): the test's
window 100 → 2000 ms (the other `TogetherTests` use 1000). RED: in the `wslc` container (K13 (a)), the test with a `threading.Thread.start`
wrapper that sleeps 150 ms before starting reproduces `b'' != b'aaaa'` at 100 ms. GREEN: the same wrapper at 2000 ms passes. RED again: the
wrapper sleeping 2500 ms → red. Cost: writer `b` spins until the deadline, so the test takes about 2 s.

**Fix 5 — the nine folder-caused failures** (§ 15: `minigit-bench` 4, `lang-explore` 3, `lang-bench` 2). No code: the runs go without an own
`TMPDIR` (above). Six were shown green without the folder file by file (§ 15); the three socket-path ones by arithmetic (100 and 67 bytes under
`/tmp`, limit 108). All nine must be green in the head runs; a red one is measured and fixed before the merge.

**Done when (adds to § 9).** Each of the four whole suites at S6's head: 0 failed, both runs complete, executed and skipped tests reconciled
by name with the base; the `/tmp` entries listed and the run's removed; fixes 1, 3 and 4 each with its three runs in the PR body; fix 2 with
the owner's install line and `dpkg -l` of the seven held packages.
