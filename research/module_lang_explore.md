# Module — lang-explore: the exploratory pilot of the language-comparison olympiad tasks

> Status: **task 1 built and run, 2026-11-01; the olympiad harness part built, 2026-11-01 (no olympiad session has run).**
> `spikes/lang-explore/` exists with its suite; series `x-20261101-13cc` ran three paid pairs of task 1 (6 of 6 sessions
> accepted, $1.04). The harness now takes `--task NN` (tasks 01, 02, 03, 09) as the olympiad plan
> ([PLAN_lang_explore_olympiad.md](PLAN_lang_explore_olympiad.md), D-O2 to D-O8) decides; the private
> material of tasks 02, 03 and 09 is not part of this module (it lives outside git, D41). The design record of task 1 is
> [PLAN_lang_explore_task1.md](PLAN_lang_explore_task1.md); the results are
> [lang-v2/EXPLORE_task1_2026-11-01.md](lang-v2/EXPLORE_task1_2026-11-01.md) (generated) and
> [lang-v2/EXPLORE_task1_2026-11-01_analysis.md](lang-v2/EXPLORE_task1_2026-11-01_analysis.md) (the reading).
> **Exploratory, not evidence**: nothing here is part of v2's pre-registered series (`todo/PLAN_lang_comparison_v2.md`).
> This file describes what is built; a sentence here about something that does not run is a bug in the file.

## Purpose

Run olympiad tasks of the v2 language comparison (spec 01, and the ungated drafts 02, 03 and 09, in
`dew_flow_ai_language_comparison`) as bare C# and TypeScript sessions on the **v1 harness**, before v2's own harness
exists, to see v2's cost and time scale and to check that the harness behaves on a task that is not minigit. It imports `spikes/minigit-bench/` (never copies it) through four
optional seams whose defaults are byte-identical to v1 (W1–W4, described in [module_minigit_bench.md](module_minigit_bench.md)).

## Commands (`node spikes/lang-explore/explore.mjs <command>`)

`--task NN` (`01`, `02`, `03` or `09`, written exactly so) is required on every command that works on one task: a missing
or unknown task is exit 4, naming the legal ids. `prepare` and `quiet` do not work on a task and take none.

| command | money | what it does |
|---|---|---|
| `prepare` | free; the only network use | TypeScript 6.0.3 from the registry by integrity, the Node cache snapshot, the C# snapshot re-hashed, the spin calibration |
| `calibrate --task NN` | free | for an ungated draft (tasks 02, 03, 09) the independent oracle first (see "The oracle"); then both private references through the judge (30 s ceiling), the time and memory limits, every mutant against its predicted failing cases; writes the task's calibration file |
| `rehearse --task NN` | free | one pair end to end with v1's stub CLI and stub upstream, over that task's material; series `r-<yyyymmdd>-<4 hex>` |
| `init --task NN` | free | a series `x-<yyyymmdd>-<4 hex>` of that task with its CPU-mask draw |
| `quiet` | free | the machine-quiet check alone |
| `run --task NN --pair k --series <dir> [--rerun <stack>] [--allow-over "<text>"]` | **paid** | one pair: the pause-point check (olympiad tasks), the quiet check, two sessions launched together on disjoint masks, collect, classify, score, record; a halted or failed pair resumes (no new paid session) |
| `readback --task NN`, `status --task NN` | free | what each session was launched with against the registered prompt and environment; the series' state and spend |
| `note --task NN [--out] [--force]` | free | the public note of that task's series, generated from the records only |
| `leakcheck [--task NN] [--range A..B] [--task-repo <path>] [--task-base <ref>] [--candidate-repo <path> --candidate <commit>]` | free | the leak scan ("The leak check", below) over the bench repository AND the task repository, with the private material of EVERY task (`--task` limits it to one); `--task-repo`, else `LANG_V2_TASK_REPO`, else a refusal; `--candidate-repo` with `--candidate` scans one commit about to be published; exit 0 clean, 5 a finding, 4 an incomplete scan or a refusal; a finding names the repository and the place, never what ("Both repositories", below) |
| `heldout-open --task NN [--key-file <file>] [--key-backup <file> --share-mount <dir>]` | free | a quick task's clear held-out folder made from its sealed archive under the task's lock, held for the operator (`.open` = `{ operator, at }`) until `heldout-close`; the key's sha256 is checked before any decryption; exit 0 or 4 ("The sealed held-out folder", below) |
| `heldout-close --task NN --key-backup <file> --share-mount <dir> [--key-file <file>]` | free | the clear folder and its stamp removed, only after the key's backup is verified on the share's mount (no override); a folder held by another live process is refused; a second close is a no-op |
| `heldout-check --tasks NN,NN [--key-file <file>] [--key-backup <file> --share-mount <dir>]` | free, read only | per task: the registry entry, the archive's `archiveSha256`, `packages.json`'s hash when it is there, the key, the unsealed tree, the clear folder's state, the backup when named; counts only; exit 4 when any task refuses |
| `package --task NN [--only refs\|witnesses\|suite\|load\|all] [--register] [--seal] [--accept-noisy <why>] [--task-repo <path>]` | free; on the scoring CPUs | a quick task's validation package (F2.S1, "The package tool", below): the suite and the gate, S, both references through the ten-pass driver, F-LOAD's measured duration, the defect witnesses; `--register` writes `inputs/packages.json`, `--seal` then seals the archive (the seal is last); exit 0 green, 1 an item red, 3 a resumable halt, 4 a refusal, 5 a failure |
| `register-memory` | free | writes the memory value's `registration` field once all six packages have both references accepted at it (the one writer of the field) |
| `prune --task NN` | free | removes the seeded cache copies and scoring copies after the analysis; keeps the records, the ledger, the calibration and the held-out folder |
| `repartition --series DIR` | free | the time partition of every kept session of a quick-run series, recomputed from its retained `stream.stamped.ndjson`, `otel/`, `meta.json` and audit log with v1's classifier as it is now (`lib/repartition.mjs`; v1's `loadSessionDir`, `smokeViews` and `partition`, never a second partition). One new immutable file per run whose inputs changed, `<series>/partition-recomputed/<yyyymmddTHHMMSSZ>-<gen>.json`, holding per session the stack, the old categories (read from `record.json`, which is never changed), the new ones and whether they sum to the wall, with a fingerprint of the classifier's and the evidence's bytes; a run over unchanged inputs reports the existing result and writes nothing; a session without its evidence is listed with the reason, never as a zero. A temporary file of a killed run is never read as a result and is removed once its owner is gone |

## Tasks (olympiad plan D-O2)

`inputs/explore.json` keeps every task-1 field as recorded (its `specs.spec01`, `specs.format` and `privateNotes`) and
gains a `tasks` map, task `01` first and repeating those fields exactly (`loadExplore` refuses a file where it does not).
Each entry pins: the spec (`sha`, `path`, `sha256`), FORMAT (the same, plus a label: `v2` for task 1, `v2.1` for the
olympiad drafts), the private notes, the held-out folder and the calibration file as path parts under the home, the
`noteLabel` and a `draft` flag (an ungated draft spec: it gets the oracle and the draft sentences in its note). The spec
and FORMAT are read at their pins with `git show` and checked against the sha256 at every world load, like spec 01.
`lib/tasks.mjs` is the one road from `--task` to all of it; no other module names a task's folder or file (a test scans).

| task | held-out folder | calibration file | note |
|---|---|---|---|
| 01 | `~/rsd/lang-v2/explore/heldout/` | `calibration.json` | `research/lang-v2/EXPLORE_task1_<series date>.md` |
| 02, 03, 09 | `~/rsd/lang-v2/explore/heldout-NN/` | `calibration-NN.json` | `research/lang-v2/EXPLORE_taskNN_<series date>.md` |

A series records its task in `explore-series.json` and `inputs.json` (a series made before tasks existed, such as
`x-20261101-13cc`, records none and is task 1's; a series whose state file cannot be read is a refusal, never task 1's).
A command given a task that differs from its series' task refuses (exit 4); without `--series`, `status`, `readback`,
`note` and `prune` take the newest series of the task. `--series` must be a direct child of the explore root (links
resolved), for every command and for the pair runner itself. `init` is refused for a task that is not a task of the plan
(task 1's series is closed). `init` freezes the task's pins (spec, FORMAT, private notes) into the series' `inputs.json` (`taskPins`);
before a pair starts the runner compares them with the task's pins now and refuses a mismatch (exit 4): after an
amendment a new series is needed. A draft task's series that froze none is refused; task 1's closed series is not.

## Samples (D-O4)

`samples/` of a workspace is the spec's section 6 table, byte for byte (`lib/samples.mjs`): the header
`| Id | Standard input | Expected standard output |` once, inside section 6, each row an id (`s01`, `s02`, ... in order)
and two code spans (one backtick, or two with a margin space; `\|` read as `|`, `⏎` as a line feed). The parser refuses,
with a sentence, a missing or doubled table, a row it cannot read, a cell with a byte the table cannot show (anything but
printable ASCII, the space and `⏎`), and a `<size> bytes, see samples/...` cell. `loadSources` runs it, so a spec with a
bad table is refused when the world loads, before any workspace is filled.

## The oracle (D-O3)

An ungated draft's private material gets an independent check before its references are scored. Calibration runs
`python3 -I brute.py <case>.in` for every held-out case, inside a bwrap box (the whole filesystem read-only, the operator's home,
/mnt, /run and /tmp empty, the held-out folder bound read-only, one writable temp directory that is also HOME and TMPDIR, no
network, an allow-listed environment), printing `oracle: case k of n` before each: `brute.py` prints the answer and exits 0, or exits 3 to
mark a case it cannot run (too big); its output must equal the case's expected output (`gen.py`'s, pinned by the
manifest) on every case it ran. It also needs `handcheck.md` beside the manifest with at least five cases: the distinct case ids
(`h` and three digits) named by the data rows of its tables, the first id of each row; headings, bullets and prose count for
nothing (a guard against a forgotten file, not a proof).
Any disagreement, a crash, a brute that ran nothing, or a thin or missing hand check stops calibration naming the case id
only, before any reference is scored. `brute.py` and `handcheck.md` MUST be listed in the manifest like every other file of the
folder (the manifest is made after them, so an edit afterwards is a hash difference). The counts go into the calibration
file. Task 1 predates the oracle and has none.

The plan's minimums are enforced for the ungated drafts before the oracle runs: at least 15 held-out cases and at least 6
mutants (`olympiad.material` in the pins), or calibration refuses, naming the counts. **The hand check stays a person's
check**: the harness counts its cases and nothing else; it does not read their values, and no test can tell that a hand
derivation is right. The team lead's reading of `handcheck.md` against the spec is the control.

## Limits (D-O5)

Per task, as for task 1: time limit = max(2 s, 3 x the slower reference's slowest case wall), memory limit = max(512 MiB,
2 x the larger reference's peak), unconditionally, so the harness floors override the specs' lower ones (1 s, 256 MB).
They are written to the task's calibration file and copied into the series' `inputs.json` at `init`. A pair is scored
under the series' own copy, but it does read the live file too: it refuses (exit 4) when the live limits differ from the
series' copy, and when the held-out folder no longer hashes to the material the calibration recorded (below).

**A calibration is bound to the material it checked.** `calibration-NN.json` records `material`: the sha256 of the task's
`manifest.json`, `brute.py` and `handcheck.md` as they were when the oracle, the references and the mutants were checked.
`loadCalibration` (so `init`, `rehearse` and every pair) refuses when the folder on disk differs, and refuses a draft
task's calibration that records none (task 1's older file records none and is not refused). `calibrate` removes the task's
old file at its START, so a failed recalibration leaves none, and fails when the material changed while it ran.

## Mutants: strict and advisory (D-O3)

A mutant in `manifest.json` may carry `"strict": false`; an absent `strict` means true. A **strict** mutant must fail
exactly its `expectedFail` set. An **advisory** mutant (a verdict that depends on time, stack depth or pipe chunking, so its
exact set is not a property of the logic) is recorded, not matched: calibration writes its actual failing set beside the
prediction in `calibration-NN.json` (`mutants[].strict`, `expected`, `actual`) and requires only that it fails at least one
case; an advisory mutant with an empty prediction is just recorded. An advisory mismatch is a line in `notes` of the
calibration file and a `note:` line on the console, never a stop; an advisory mutant that fails nothing despite a
non-empty prediction stops calibration. A stop names the mutant and the id counts (predicted, failed, predicted-but-passed,
unpredicted-but-failed), never a case id or value. Any message that lists ids or files (the oracle's disagreements, unreadable series, the id scan)
shows the first ten, then `… and N more (T in all)`.

## The pause point (D-O7)

The explore root is scanned ONCE per check; the plan directories that scan collected are what is summed (a ledger that has
vanished or cannot be read since is a stop, not a thrown error, and says why: `the ledger of series X cannot be read (ENOENT)`). Applies to every direct child of the explore root whose `explore-series.json` names a plan task (`olympiad.planTasks` in the
pins: 02, 03, 09), whatever the directory is called: a copy or a rename is counted, a rehearsal's `r-` series is counted
(its ledger holds $0), task 1's is not. A child that holds a state file or a ledger but whose state cannot be read or
classified (no file, corrupt, not an object, a task nobody knows), or an entry that is not a real directory or a plain file (a link of any kind, a pipe, a socket, a device: the root is read with lstat,
links are never followed) or whose type cannot be told (a dangling or replaced link, or a directory that vanished or was replaced between the scan and its description, checked with lstat),, and a plan series whose ledger is missing (never
counted as $0), stop the check closed, naming it. A paid pair runs
only on a series named `x-yyyymmdd-4 hex` (a rehearsal keeps `r-`, and is refused as a rehearsal). `run` sums the broker-charged USD in the ledgers of those series, takes the most expensive session of them
(its charges summed per series and rid), and refuses before anything is launched, exit 2 naming the spend, when
`spent + projected > $60` with `projected = 2 x max($5, 3 x that session)`. `--allow-over "<the owner's words and
date>"` is the only way past; its text, with the spend, the projection and a timestamp, goes into `pairs/p<k>.json`
(`pauseOverrides`, kept by a resume). The check and the pair launch run under ONE plan-wide lock (`series.lock` in the
explore root, held until the pair ends), so two runs on different series cannot both pass the check; a second run is
refused with exit 4 while the first holds it. A series whose state file cannot be read, or a ledger line that is not a JSON object
(exit 2, naming the series and the PHYSICAL line number of the file, blank lines included; locks released) or whose amount is not a finite number of at least 0, stops the check (it fails closed: such a line is never counted as 0), whatever
`--allow-over` says. The $50 per-session cap, 75 min wall and 45 min stall are unchanged.

## Notes (D-O8)

The note's title and file name carry the task (`task 1` / `EXPLORE_task1_...` for task 1, `task 02` / `EXPLORE_task02_...`
for the others). Task 1's sentences are as recorded; tasks 02, 03 and 09 state the olympiad plan's section 4 predictions
for their task, a banner and closing sentence that name the ungated draft, and the two draft differences from v2.

## Flow of one pair

0. (Olympiad tasks) the pause point, `lib/pause.mjs`: after the barrier check and before the world, the calibration or the token
   is read, `run` refuses with exit 2 when the plan's spend plus a pair's projection is over $60 (see "The pause point").
1. `quiet` (v1's `lib/quiet.mjs`): foreign CPU, foreign agents, memory and the spin probe; it waits (up to 30 min) instead
   of launching on a loaded machine.
2. `lib/workspace.mjs` fills each box with exactly the frozen files of the stack's template (`templates/csharp/`,
   `templates/node/`), the statement and the agent-visible `runner/`.
3. v1 launches the sessions (`lib/session-launch.mjs`): the bwrap box, the CONNECT proxy (no host allowed), the
   credential broker (the only path to the model), the locale `de_DE.UTF-8` through seam W2.
4. v1 classifies and records each session (`record.json`: every v1 metric — the four cost views with T1–T5, tokens with the
   cache split, the time partition). `lib/records.mjs` adds the explore section (pass rates, trap hits as a count, the
   agent's own build/test time, CPC and CPC_all) and the pair record (`pairs/p<k>.json`).
5. Frozen files the agent changed are reported and put back in the scoring copy (`assertInside` refuses any path that
   resolves out of it). `lib/score.mjs` runs `runner/run_samples.py` in v1's scoring box (seam W3): a fresh copy per case,
   each run in its own `systemd-run --user --scope` with `MemoryMax`, `MemorySwapMax=0` and `OOMPolicy=continue`; memory
   is the scope's `memory.peak`.

## Scoring a quick-run session: ten passes, four states, one memory value (F1.S2)

A quick-run session (a daemon or web task) is scored AFTER it ended, never while a session of the series runs, by **ten scoring passes under two CPU profiles from one build**. `lib/passes.mjs` is the driver; it reads and writes
`s/<rid>/score/` and runs under the series lock that `run` and `rescore` take (it proves the lock is held and never takes it; `lib/pass-locks.mjs`) and its own `score/scorer.lock` (one scorer per session; a dead owner's lock is taken over).

- **The units.** A pass is one run of the runner over every visible and held-out case of the task on the built copy, with a fresh program per case. Passes 1, 3, 5, 7, 9 run on the 2-CPU profile (CPUs `6-7`), passes 2, 4, 6, 8, 10 on the
  8-CPU profile (CPUs `0-7`), for both stacks (`lib/verdict.mjs`: `PROFILES`, `profileOfPass`). Each pass runs in a **pass child** (`lib/pass-run.mjs`, `lib/pass-child.mjs`) started under `taskset -c <profile>`; it inherits the
  affinity, and so do the scope, the namespaces, the box and every program (the execution layer is not changed). The driver points at the live child in `score/child.json` (pid and start time); a scorer that starts kills and waits for a
  recorded child that still runs, before its first attempt, and says so in an `orphan-child` event, and the child's answer file carries a token of its own start, so a killed driver leaves nothing running and a new child never reads an orphan's answer. The mask is read back right after the spawn and by the child itself (`lib/affinity.mjs`: compared as a set, any spelling): another
  mask is a halt (`affinity-not-applied`), never an attempt. The whole child has a deadline (the layer's `runCeilingMs` plus `PASS_CHILD_MARGIN_MS`); at it the child's process group is killed and the attempt is an environment one (`pass-child timeout`).
- **Attempts.** A pass that ends in an environment failure (the layer or the runner failed, a record that does not judge every case, a missing memory reading, a halt) is tried again whole, at most twice: three attempts per pass, thirty at most per
  session, every one kept as its own file `passes/pNN-<profile>-aK.json` (written once, `wx`). A record is complete only when `lib/attempt-record.mjs` says so (the runner's completion keys with the pinned format and version, exactly the
  expected case ids, every required field); a malformed, truncated or partial record is an environment attempt, never a functional failure.
- **Records are published whole.** An attempt, an event, the completion record and the score are each written to a temporary name of their owner, flushed, and put at their final name by a hard link that fails when the name exists
  (`lib/publish-once.mjs`); a kill in the middle of a write leaves a `*.tmp` and no truncated record, a temporary file is never read, and the next scorer removes the ones a dead scorer left. A `build.json` that is not a whole record
  (truncated, or not a completion record) is a halt (`build-record-invalid`), never a passed build.
- **The build** happens once, at an atomic commit point (`lib/build-commit.mjs`): the clean copy is built in `score/build-<token>/`, validated, renamed to `score/tree/`, and only then `score/build.json` is written (`wx`) naming the tree's hash.
  The record also carries what the copy found (the frozen changes, the harness drift, the undecided files and the identity of the contract, see the submission contract below) and stamps the runner pin (format and version) the build was committed under, and every stored attempt is judged against THAT stamp, never against today's pin: a scoring resumed after the runner was re-pinned and
  still having passes to run halts with `runner-moved` (an event, nothing re-judged), and a scoring with no pass left to run finishes from its records whatever the runner is now. A pass runs only against a tree whose record exists and whose hash matches; anything uncommitted is discarded on a resume. A build that ran and failed is committed (`ok: false`: every case is `build failed`, no pass runs); a build that never
  began commits nothing and is tried up to three times. **Frozen files are put back after the build too** (final seam review of F1, parent D65): the build is the agent's own (an MSBuild target in `App.csproj` runs in the writable build box), so right after it the build step
  compares every frozen file with its frozen bytes, puts each changed one back (the same safe write as before the build) and records it as an agent deviation with the reason `changed during the build`, in `build.json`, `score.json` and `record.json`. The passes run the restored files; the build's output folder is not frozen and is not touched. **The build step owns the restore**: `buildAndRestore` in `lib/copy-quick.mjs` is the one function that builds a quick tree. It runs the build, restores, and returns the build's own answer with the deviations found during the build in `duringBuild` (always a list). `prepareTree` returns data only (the frozen set travels in `files`, no closure), and both paths use the step: `commitBuild` (the ten passes; it refuses a prepare that names no frozen files, so no result exists without the restore having run) and the one-pass `buildStep` in `lib/score-quick.mjs` (`replay`), each adding `duringBuild` to the frozen changes of the copy. Node is closed by `ignore-scripts`; `test/quick-build-frozen-real.test.mjs` is the C# twin of T-L62 in the real build box with the Node controls.
- **A series is run on one harness** (`lib/harness-moved.mjs`, final seam review of F1, parent D70). `run` cannot see drift, so before a build is made its scoring compares the runner pin (`manifestSha256`), the templates' identity, the generated files and the derived `PACKAGES.md` of the session's stack with what `init` recorded (`runnerPin`, `templatesIdentity`, `generatedSha256`, `packagesMdSha256`); a difference, or something the series did not record, is a halt `harness-moved`, whose sentence names each difference with the recorded and the current value by twelve-character short hashes and says what to do (run the series on the harness it recorded, its subject commit, or use `rescore`, which reads a kept tree against the series' history) (an event file, nothing built or scored, resumable: the same call goes on once the harness is back). `scoreSessionDep({ sameHarness: true })` is how `run` asks for it, and it also hands the scoring the same `{ driftPossible, history }` a rescore gets (`driftSeamsOf`, used by `run`, `rescore` and `replay`), so a rescore over moved bytes judges them by the series' history and is never halted.
  **`run` also checks before it launches a pair** (the same comparison, for both stacks of the series): a moved harness, or one the series did not record, is refused with exit 3 before the pair is planned into the run-map, so no session starts and nothing is spent; the reason is a `harness-moved` event line in the ledger (an event, never a charge). The scoring-step halt above stays as the second line of defence. Test: `test/quick-run-harness-gate.test.mjs` (the seams `io.runSeams.prerequisitesFn` and `loopFn` are for tests only).
  **The after-build check compares bytes only** (`frozenChanges`), so a change of a frozen file's mode alone is not caught; the build box gives no reason to change a mode (the build is `bash build.sh` in a box whose tools write files, not modes), and a restored file is written with its frozen mode.
- **The four states** (`verdictOf`): `accepted` (the build passed, ten passes completed, every case passed in all ten); `rejected` (the build failed, or a completed pass is failing and none is clean); `intermittently incorrect` (a completed
  pass is failing and one is clean); `unscored: environment` (no completed pass is failing and some pass has no completed attempt after its three). The terminal rule: a functional failure in any completed pass stands whatever any other pass
  did; a majority of clean passes never accepts. While a pass without a failure still has an attempt left the answer is `pending`, never recorded. `scoringState` is `decided` only when no attempt that can still be made can change
  the label: `rejected` while a pass is open is `open` (a clean pass to come makes it `intermittently incorrect`); `intermittently incorrect`, `accepted`, a failed build and a `rejected` with no pass open are `decided`.
- **Scoring exhaustion is not session finality.** `score/verdict.json` (rebuilt whole from `build.json`, `passes/` and `events/` on every write, never read) carries `scoringState` (`open`, `decided`, `exhausted`) and no finality field.
  Finality is the run-map's: a decided scoring makes v1's `scoreOne` move the session `ran -> scored`; an exhausted one leaves it `ran` with `data: false` and the class `void: unscored environment` (`VOID_UNSCORED`, `lib/series.mjs`), its old
  class appended to `reclassifiedFrom` and an `unscored` event line in the ledger, so the schedule offers the one re-run (`--rerun`). This is the one change to v1 (`spikes/minigit-bench/lib/bare-series.mjs`, 19 lines and its tests).
- **The rollup** (`rollupOf`) is the one score the record reads (`score/score.json`, written once): per case the first failing pass's verdict and evidence, the largest `peakBytes` and `wallMs`, the enforced `memoryMaxBytes`. The record's
  `explore.verdict`, `scoringState`, `passes`, `scoringMs` and `buildTreeSha256` come from it (`lib/records.mjs`); `explore.accepted` is `verdict === 'accepted'`; the note counts the four verdicts per stack from the records and the
  finality of the cells from the run-map (`lib/note.mjs`). The rehearsal keeps the single-pass scoring (`singlePass`): a free check of the pipeline, no verdict. `replay` stays a one-pass diagnostic on one case.
- **The one memory value** (parent D72). `inputs/memory-limit.json` holds it (a positive whole number of MiB, checked when the inputs load: `lib/memory-limit.mjs`). The generated `STATEMENT.md` (D72's sentence and the stack's heap sentence
  from the layer's own `heapKeys`), the frozen `samples/load.json` and the scorer's limit all read it; the runner's read-back of `prog/memory.max` is carried into every case (`memoryMaxBytes`) and a case enforced under another limit is an
  environment failure, not a verdict. A scan test holds that no other file writes the figure.
- **Growth.** `inputs/growth.json` (counts, times and sizes) is validated on write and on load (`lib/growth.mjs`); an ordinary scoring run refuses to start without a valid one, and `pairWorstCaseBytes` (2 trees + 2 worst-case retained
  sets) feeds the disk rule (`diskRuleVerdict`: halt below twice the pair's measured worst case). The first measurement is `node spikes/lang-explore/measure-growth.mjs` (below) under its own temporary guard (60 GiB free, 20 GiB write ceiling).
- **Before a paid launch** the preflight probes both masks of the pair with `taskset` and refuses the launch (exit 3, resumable) when one reads back another mask (`lib/mask-preflight.mjs`; a good read-back is kept as a file of its own under `<series>/readback/`, one new file per preflight, none rewritten).

Ten passes are a budget, not a proof: a race that shows often on the scoring CPUs is caught, a rarer one can pass all ten.

## The leak check (redesigned for size)

D41: no held-out case content, reference, mutant, trap or private-note sentence enters git. v1's `leakcheck` matched every needle against every file (`needles x bytes`); task 09's 5.5 million numeric case lines made the all-task run take
88 minutes without finishing. `lib/leak-scan.mjs` owns the file loop and reads each haystack text ONCE: every committable file, the commit messages and `git log -p` of the range (v1's `heldout-leak.mjs` is imported for the file list, the
history texts, the default range and the shingle matcher, and is unchanged). The private material is structured (`lib/leak-material.mjs`: keys and needles with their origin), never one joined string. Both sides are normalised the same
way (`lib/leak-text.mjs`): lower case; CR removed; pipes and tabs to spaces; trimmed; whitespace collapsed; a leading list marker (`-`, `*`, `>`) stripped; blank, separator-only (`---`, `|---|`) and one-repeated-character lines are
noise; length is tested after normalising. A finding is `{ tier, file, line, task, caseFile, fromLine, toLine }` and every message and log line names where, never the text.

| tier | what it matches | parameters |
|---|---|---|
| 1 substring (`leak-substring.mjs`) | private code lines (references, mutants, `gen.py`, oracle files) and short case lines, anywhere inside the haystack (a rolling 8-character prefix hash, verified; linear) | code lines of 30+ characters, minus the committed runner's lines; case lines of 8 to 199 characters, only for a task that has at most 2,000 of them (09 does not: reported with its count); more than 3,000 needles in all FAILS; a file with a NUL in its first 8 KB skips this tier and the shingles ("line tiers only", counted) |
| 2 exact line (`leak-lines.mjs`) | every case line of 30+ characters and every code needle, as a 53-bit key; each haystack line and each whitespace token of 30+ characters is looked up (a `git log -p` line as it is and without its `+`, `-` or space prefix) | verified against the case line before it is reported |
| 3 window (`leak-windows.mjs`) | three consecutive qualifying case lines (8+ characters, not one repeated character), in order, pasted anywhere: a code fence, a table, a commit message, a diff hunk | a sorted `Float64Array` of keys, a three-slot ring over the haystack's non-blank lines, verified against the three lines |
| 4 chunk (`leak-chunks.mjs`) | aligned 64-character chunks of case lines of 256+ characters, against a rolling 64-character hash over every haystack line of 64+ characters | verified against the chunk text; a superset of the brief's "haystack tokens" (a window of a token is a window of its line; long case lines with spaces are found) |
| shingles | the private notes' six-word windows (v1's matcher, minus the plans' public wording) | text files only |
| ids | the manifests' mutant and trap ids as whole words in `research/lang-v2/*.md` | |

`leakcheck` writes a progress line to STDERR before each phase, in counts only: `building the material of tasks ...`, `material: substring needles N, exact-line keys N, window keys N, chunk keys N, shingles N`, `scanning N files`, `scanning the git history of A..B (N texts)`, `verifying N candidate(s)`; the result line is on stdout.

A listed file that vanishes or cannot be examined or read (a failing lstat or read) does not crash the scan: it is recorded as unreadable and the result FAILS CLOSED as an incomplete scan (`incomplete: true`, exit 4; with a finding beside it, exit 5), never clean.

A hash hit is only a candidate: `lib/leak-verify.mjs` re-reads the case files and compares the text, so an equal hash with different text is never a finding (a clean run reads nothing a second time). One finding per place and private A haystack text holds at most ONE candidate per distinct key (the first place it stands), so a file that repeats a matching row thousands of times carries a handful, not thousands; the chunk tier's candidates are verified inside the file, the others at the end of the scan.
line. **Collision rules** (all four tiers): (a) a private CODE line that is also a line of the pilot's own agent-visible `spikes/lang-explore/runner/**` as committed at HEAD is dropped from the needles (never a case line, never a note
sentence; read through git, not the working copy; the runner is not pinned by sha, so it is HEAD's); (b) the PRE-MATERIAL BASELINE: each task of `inputs/explore.json` names a `baselineCommit` (a main commit made before its held-out
folder was born; `heldoutBorn` records the folder's birth time) and a finding in a file is exempt, and counted per task, when every one of its lines occurs byte for byte in the same path at that commit and the working file holds it no
more often than the baseline did (each path's working file is read once, as UTF-8 like git's blob, and only the finding's own lines are counted). A new file, a new or duplicated line, a changed line and a copy at another path stay hits; history findings and shingle findings are never exempt; a baseline that cannot be read gives no exemption. Every baseline is VERIFIED first: the commit exists, is an ancestor of HEAD, and its committer date is before the task's `heldoutBorn` (a UTC time); if any check fails that task gets no exemption and the result line says why (`baseline of task NN gives no exemption: ...`).

What it does NOT catch: one or two lines of a short numeric case (a single short line of a task that has more than 2,000 of them, and any run of fewer than three lines, is invisible: they are short numbers that occur everywhere); three
lines out of order, or three from two places; a 64-character piece of a long line that straddles two aligned chunks (a paste of 127 characters or more always holds one); content inside compressed archives (the .xz tarballs of the research
record are scanned by the line tiers as bytes, which can only match by accident); a paraphrase or a recomputed value; shingles in binary files; a shingle exemption by baseline (none exists).

**The quick tasks' FORMAT pins (F1.S1 (e), 2026-11-07).** The six quick tasks' `format` entries in `inputs/explore.json` pointed at the drafts' `v2.1` (`4a4e86c4…`); they now name the first amendment of FORMAT v2 on the task repository's `main` (`4ddb8f8`, sha256 `06c36608…`, label `v2, first amendment`), the text lang-bench pins, so the one engine does not read a draft FORMAT: every workspace's `runner/GRAMMAR.md` is cut from it. The specs stay at their drafts. The olympiad tasks' pins (01, 02, 03, 09) are the pilot's history and are not moved. F1.S2's memory amendment moves them once more. **Moved again (2026-11-07, D72):** the six now name FORMAT's second amendment on the task repository's `main` (`c204726`, sha256 `6c3546bb…`, label `v2, second amendment`); the specs and the olympiad pins stay, and `quick.reason` in `inputs/explore.json` carries the dated line.

**`build_test` counts the build, run and sample commands (F1.S1 (b), 2026-11-07).** v1's classifier (`spikes/minigit-bench/lib/classify.mjs`) now gives `build_test` to a shell, `source` or `.` running `build.sh` or `run.sh`, to `python3 [options] runner/run_samples.py` and to the same work in both stacks (a table of C# commands and their Node counterparts, `STACK_COMMAND_PAIRS`: build, restore, add a package, run the program, format and lint, test; running the built program directly counts for both), as `explore.buildTest` (`lib/records.mjs`) always counted the first ones; tests keep the two classifiers from drifting apart and the two stacks from being counted unequally. The first recompute of 2026-11-07 counted the C# side's restore, run and format but not the Node side's counterparts; it was redone the same day (see the analysis note). The olympiad pilot's note says the classifier "is not widened" for a note dated before 2026-11-07 (the recorded task-1 note is reproduced byte for byte) and what it counts from that date (`differencesOf(task, date)` in `lib/note.mjs`). Reading or editing a script (`cat build.sh`) is not running it. Because `classify.mjs` sits in a folder of v1's `harnessIdentity`, that identity moved: every record from this merge on carries a new `run.harnessIdentity`. Partitions made earlier read `build_test` as zero for these commands; `repartition` recomputes them.

### Both repositories, a candidate commit, and the sealed task (build step 2b, 2026-11-08)

`leakcheck` is two scans over ONE material (built once, from the private side): the bench half (`leak.mjs` `benchHalf`, unchanged: its files, its branch range, the id scan, the baseline exemption) and the task repository's half
(`lib/leak-task-repo.mjs`, through the same tiers, with NO baseline exemption: its text was never in a bench baseline commit, so every matching line is a finding). `lib/leak-repos.mjs` is the command and the ONLY entry point (`leakcheckRepos`, always `{ exit, message }`: the message holds every refusal and is empty when none was made); the bench half (`benchHalf` in `leak.mjs`) is no entry point of the library, and the tests reach it through `test/bench-half-seam.mjs` (`scanBenchHalf`), because a check that scans one repository is a pass it did not earn. Setup is `leak-repos-setup.mjs`, the report lines `leak-repos-lines.mjs`. Every line names its repository (`leakcheck [bench repository] ...`, `leakcheck [task repository <path>] ...`,
`leakcheck [candidate <sha12> of <repository>] ...`) and every finding its place (`HEAD:<path>`, `<path> (working tree)`, `staged changes`, `patches of the local branches`, `commit messages of the local branches`), never a text.

| Question | Answer |
|---|---|
| The task repository | `--task-repo <path>`, else `LANG_V2_TASK_REPO`; with neither, exit 4 (`no task repository is given`; the sentence also names `--candidate-repo <path> --candidate <commit>` alone as the other valid call), before any material is built. An empty value names nothing. The path must be the repository's top folder (a sub folder lists less) and must not be the bench repository or a worktree of it (`rev-parse --git-common-dir` compares). |
| What is scanned | its tracked files at `HEAD` (read from git by one `cat-file --batch`, never the disk); the commit messages and every patch (`log -p --cc`) of every local branch and HEAD over the commits its base does not hold; the staged changes (`diff --cached`); the working tree as git would commit it (`ls-files --cached --others --exclude-standard`, from the disk: modified and untracked files). With no commit, there is no tree and no history, and the line says `no commit yet`. |
| The base | `--task-base <ref>`, else `origin/main`, checked with `git rev-parse --verify <ref>^{commit}`, never assumed. When it cannot be resolved the history is every commit reachable from every local branch, and the line says `<ref> cannot be resolved: every commit reachable from every local branch was scanned` (a `FAILED` line carries `history read: ...` too). A ref that starts with `-` is a refusal. It never crashes on a missing ref. |
| Complete history or a refusal | a shallow repository (`rev-parse --is-shallow-repository` is not `false`) is a refusal, exit 4; so is a walk with an object missing (`rev-list --objects --missing=print` prints a `?`) and any git step that fails or is cut short. git runs with replace refs off and lazy fetch off, so what it reads is what the repository holds. |
| `--candidate-repo <path> --candidate <commit>` | one commit about to be published, in either repository: its message, its diff against each parent (against the EMPTY TREE when it has none: a root commit's whole content is added lines) and every file of its tree, from git. The commit is checked with `git rev-parse --verify <commit>^{commit}`; an unknown one, a tree id, an option-like value, a missing pair, or a path that is not a top folder is exit 4. No baseline exemption in either repository. With `--candidate` and no `--task-repo` ONLY the candidate is scanned (`--task-base` and `--range` are then refused); with `--task-repo` too, the whole scan and the candidate. The report names the repository `bench repository`, `task repository` (when the option or the variable names it) or `repository <path>`. |
| Canonical paths | the bench's `repoRoot`, the task repository, the candidate's repository and the variable's value are each resolved by `repoRootOf` (a top folder, real path) before any comparison; `sameRepository` never sees a raw path. A raw variable that is a sub folder names nothing (the candidate is reported as `repository <path>`); a bench path that is not its top folder is exit 4. |
| Option-like values | `--task-repo`, `--candidate-repo`, `--task-base`, `--candidate` and the variable are refused when they start with `-`; every git call that takes a revision or a path puts `--` before the positional arguments (`rev-parse --verify` uses `--end-of-options`); the base sha must match `/^[0-9a-f]{40,64}$/` before it reaches `rev-list`. |
| The git environment | git runs with `PATH`, `HOME`, `LANG`, `LC_ALL` and `TZ` (when set) and nothing else of the caller's: no `GIT_CONFIG_*`, `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES` or `GIT_COMMON_DIR`. It gets `GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL=/dev/null`, replace refs and lazy fetch off, no prompt, no optional lock, `--no-pager` and `core.quotePath=false` (`gitEnv`, `git` in `leak-git.mjs`). The task repository's working-tree list is made by that git too. |
| Bounded reads (`leak-git-read.mjs`) | a tree is read by `git cat-file --batch` in batches of at most `blobs` files and `blobBytes` bytes (sizes from `ls-tree -l`), a history by ranges of `commits` commits (`log --no-walk=unsorted --stdin`); a range whose output the read limit (`readBytes`) cut is split in halves and read again, down to one commit (one commit's output beyond the limit is a refusal naming that commit; a single file beyond the limit is `unreadable`, an incomplete scan). Defaults: 2000 files, 64 MiB, 200 commits, 256 MiB; `io.gitLimits` is the seam. A repository larger than one batch is never refused for its size alone. |
| Every object accounted for | `parseBatch` requires each requested id exactly once and refuses, with a reason and a `kind` of its own: `missing`, `truncated`, `ended-early`, `mismatch`, `extra`. A blob whose size git cannot give is a missing object. HEAD's tree and a candidate's tree are read whole whatever the base excludes, so a missing object there is exit 4. |
| Progress | one line at the start of each phase and per batch, counts only: `bench repository: scanning ...`, `task repository: reading the tree at HEAD (N files in B batch(es))`, `tree batch i of B`, `reading the history (N commits in B batch(es))`, `history batch i of B`, `reading the working tree (N files)`, `candidate <sha12>: reading its tree ...`. |
| A bench candidate's baseline | when the candidate's repository is the bench, the same baseline exemption as the full scan applies, by the same function (`applyBaseline`, `leak-baseline.mjs`; `lib/leak-candidate-baseline.mjs` feeds it): a finding in a file of the candidate's tree is a collision, counted per task in the line (`collisions with the baseline commit (not leaks): task NN: n`), when every line of it occurs byte for byte in the same path at the baseline commit and the candidate holds it no more often. The candidate's lines are the blob texts the bounded tree reader gave (`cat-file --batch` of ITS commit), never the working file, and the baseline must be an ancestor of the candidate (not of HEAD); a baseline that is not says so and exempts nothing. A file whose blob cannot be read has no finding to exempt and is exit 4. Findings in the message and in the diff are never exempt. The task repository's candidate has no baseline and gets no exemption. The baseline reads use the clean git environment too. |
| Exit codes | 0 clean; 5 a finding in any repository (a finding wins over a refusal beside it); 4 an incomplete scan (a listed file gone) or a refusal. |

**Fail closed when sealed (F2.S2 (d)).** `sealedAbsentProblem({ home, task })` in `lib/heldout-quick.mjs` is the one decision of every reader of a task's clear folder: with `heldout-NN/cases/` absent and the task listed in
`spikes/lang-explore/inputs/heldout-frozen.json` (either `{ "tasks": { "NN": entry } }` or `{ "NN": entry }`; an entry of any shape registers it), it answers `sealed: open it first (task NN)`; a task with no entry, or no record file, keeps
the old behaviour (null); a record that cannot be read (not JSON, not an object of tasks) is a refusal whenever the folder is absent. `leakcheck`'s task loading (`scanProblem`) and its link check (`buildMaterial`, through each task's
`pins`) both call it, so a sealed task is exit 4, never clean and never "absent". `scanProblem` no longer skips a task whose folder is absent while another task's folder is present: it refuses, naming the tasks (`--task NN` names one task and
reads it alone; the pins of a test name the tasks the test holds). The function now also answers for an archive with NO entry: `heldout-NN/suite.enc` and no entry is an interrupted seal (`interrupted seal: run \`package --task NN --seal\` again to resume`), never an absent task and never today's behaviour; the sealed sentence ends with the verb (`run \`explore.mjs heldout-open --task NN\``), and `loadHeldoutQuick` calls the function before its `absent` fail, so a sealed task never carries the `absent` flag. Until the first seal writes the record the function reads no file and returns null. The seal, the verbs and the readers are in the next section.

**Not covered by this step:** the mutant and trap id scan reads the bench's `research/lang-v2/*.md` only; the task repository's files are scanned for the needle tiers (the case lines, reference, mutant and oracle lines, the notes' shingles), not for ids as words. 

**The quick tasks' held-out reader takes regular files only (F1.S1 (d), 2026-11-07).** `lib/heldout-quick.mjs` reads `heldout-NN/cases/` and `traps.txt` by `lstat`, so nothing is followed. `caseEntriesProblem` refuses every entry of `cases/` that is not a regular file (a link, a folder, a fifo, a socket, a device), naming it; `cases/` itself and `traps.txt` are refused when they are a link (or not a folder, or not a regular file). `casesTreeHash` returns `ok(sha256)` or `fail(sentence)` and never throws; for a valid folder the bytes it hashes are the old ones, so a series made before the change still matches at its next pair. `loadHeldoutQuick` and the rehearsal's fixture take the value. The olympiad reader (`lib/heldout.mjs`) has the same shape and is outside the claim, so it is not changed. The leak check had the same blind spot the other way round: `lib/leak-material.mjs` skipped a linked file under `refs/`, `mutants/` or a quick task's `refs-NN/` in silence, so it did not read it. `buildMaterial` now refuses with the task and the path (`task NN: refs/ws/linked.py`), never the text, and `leakcheck` exits 4 on it.

## The package tool (F2.S1; `lib/package-*.mjs`)

A **package** is what makes a failure of a quick task adjudicable: its corrected and gated spec, two references (C# and Node) that pass every case ten of ten, a held-out suite whose every case cites a requirement and whose every trap
has a witness, and one identity F2.S2 can freeze. The tool checks the parts it can check and records the rest as counts, hashes and durations in `inputs/packages.json`. It writes nothing public but that file and the memory value's
registration field. Its records hold case ids and trap names and stay in the private area (D41).

| Module | What it holds |
|---|---|
| `lib/package-manifest.mjs` | the manifest's shape (`cases` with the sha256 of each pair, `traps` rows, `witnesses`, `load`; an unknown top-level key is refused naming it; a trap row takes ids `hNNN` or `sNN`, every other list `hNNN` only), `readManifest`, the rows of the trap table |
| `lib/package-suite.mjs` | text in, findings out: the R-ids of `cases.txt` against the corrected spec (read by the conformance checker's own `parseSpec`), the comparison words, the manifest against the folder, `traps.txt` against the table's held-out ids, the table's rows against the cases, the spec's samples and the witnesses, and `largestReplyBody` (S) |
| `lib/package-quick.mjs` | `loadPackage` (the held-out side, the manifest, the pinned spec text with its sha256 checked, the references and overlays listed by `lstat`), `assembleReference`, `materialOf` and `packageIdentity` |
| `lib/package-pass.mjs` | `wrapPass` (the start line, the 60 s heartbeat, the end line, the memory read-back guard) and `onePass` (`commitBuild`, then the pass child) |
| `lib/package-refs.mjs` | `validateReference` (the ten-pass driver, green only on `accepted`, the per-pass rows), `loadRule` (F-LOAD), `stageTwins` |
| `lib/package-witness.mjs` | `assembleWitness`, `witnessVerdict` (the two rules), `runWitness` |
| `lib/package-gate.mjs` | the conformance checker called in process (`runConformance`), the header's `Gate verdict` row and the gate record (`gateItem`) |
| `lib/package-record.mjs` | the private run folders, `openRun` (resume or new), the sweep of a dead run's owned trees, S's file |
| `lib/package-registry.mjs` | `registerPackage`, `registrationProblem`, `pinsNow`, the five-S gate |
| `lib/package-steps.mjs`, `lib/package-command.mjs`, `lib/package-finish.mjs` | the steps, the commands, and "register, then seal" |
| `lib/memory-limit.mjs` | `registerMemoryValue`, the one writer of the `registration` field |

**The reuse.** Builds go through `buildAndRestore` (inside `commitBuild`), every pass through `runPassChild` (it kills the whole process tree at its bound; the runner kills each case's cgroup), the ten passes through `scorePasses` (called as it is),
the seal through `package-seal.mjs` (bench PR 4a). No spawn path is written here, so a witness that hangs or leaves a grandchild leaves no process behind.

**The two references.** Each reference is the files an agent would write (`refs-NN/<stack>/`), laid on the stack's template; one that holds a frozen file or a link is refused, naming the path. Each is scored at the one memory value: green is
`accepted` (ten passes, five per profile, every case passed) with the enforced limit read back as that value; another read-back is a halt with both numbers. The record keeps, per reference and per pass, the largest peak, the slowest case wall,
the ten passes' wall and the retained size (the numbers *Growth* reads). **F-LOAD** is measured, not assumed: the slowest whole-scenario wall of the load cases (`manifest.load.cases`, each with a twin under `twins/` run once per reference) over every
pass of both references against 600 s minus 30 s and 600 s minus twice the twin's wall; a duration that leaves less is shortened in the spec first. (The rule is applied to the largest wall and the largest twin wall, which is the stricter reading.)

**The witnesses** (`heldout-NN/mutants/<id>/`, an overlay over a stack's reference; refused when empty, a whole tree, frozen, or equal to the reference): a **deterministic** witness gets one pass on the 2-CPU profile and is caught when it fails every
case registered for its trap and no other; a **race** witness gets ten passes like a session and is judged on its failing passes only (each fails registered cases only; at least one fails; the clean passes are kept). A timeout or a hang of a case counts
as that case failing; an extra failure (a case beyond the table, a hang outside the trap) is a mapping to fix and the witness is not caught. A witness that does not build is not caught. Every pass prints a start line, a heartbeat every 60 s
(the wall only, since the pass child shows nothing before it ends) and a closing line that gives the outcome and the number of cases that failed; the lines carry numbers only.

**The trap row and the visible samples** (parent entry 10.1, 2026-11-10): every witness pass also runs the task's visible samples (`s01`, ...), so a wide defect can fail a sample too. A trap row therefore names at least one held-out case (`hNNN`) and may name visible
samples (`sNN`) beside it. The package tool reads the sample ids from the pinned spec's own section 6 (`quickSamples`, the samples every pass runs): a row of sample ids only is red, a sample id the spec does not hold is red naming the row, the id and the samples the spec holds, an id of neither form is red as such, and a spec whose
samples cannot be read is a problem, never "no samples". `traps.txt` is the union of the rows' held-out ids only; a sample id is never in it. The load cases stay held-out ids. `witnessVerdict` is unchanged: it judges every case of a pass, so a witness that fails
its row's held-out case and its registered sample is caught, and one that also fails an unregistered sample is not (beyond the table).

**The records** (`~/rsd/lang-v2/explore/package-NN/<yyyymmddTHHMMSSZ>-<gen>/`): `record.json` (a few KiB, written once at the end), `step-<name>.json` for each finished step (written once), `halt-<n>.json` for a resumable halt, and the scoring
folders (attempt files, written once). The folders are append-only and never pruned (the repository's rule: results are only added); expect tens of runs per task, each well under 1 MiB, so a task stays in the low tens of MiB. A killed
run leaves no `record.json`: the next run names it `incomplete` in its record and never reads it as a result; a halted run of the same material is resumed in its own folder. Owned temporary trees of a dead run are swept at the start. **S** is counted
by `--only suite`, as the runner reads a reply body (`rs_lex.py`): a `body json V` is V's serialized length, a repeat count (`"x"*N`) multiplies, adjacent strings on a line are one body (the sum of each item's decoded length times its count), a `header` is none; a `${...}` field counts its literal bytes only. It runs over the held-out cases and, when the `--task-repo` checkout's spec passed the pin check, its visible samples (`tasks/<slug>/samples`), by a running maximum that folds file by file and line by line (no array of all lines). It is recorded only for a suite with no problem AND from such a pinned checkout (with no `--task-repo`, or a checkout whose spec is not the pinned one, S is counted and said on the line with the reason, and not recorded: the first value per suite stays for ever), in `package-NN/s.json` with the samples' tree hash beside the suite's (`samplesTreeSha256`, for audit), one entry per suite tree hash (a changed suite gets a new S); `register` for 04, 06, 07, 08 or 10 refuses until all five have their S for their current suites.

**Code round 1 (2026-11-10).** The conformance check is run only over a spec that hashes to the pin of `explore.json` (a missing pin is a refusal). Every one-pass attempt (a deterministic witness, a twin) starts clean, so a halted one resumes; the twin's trees are removed after its pass, and a twin case that does not pass makes the load item red. A halt line the command says holds counts and step names only; the full reason goes to the private `halt-N.json` (`detail`, with the `step` it halted in). A halted run is resumed only by a run whose steps include that step and whose material (the memory value included) is the same, and `--only suite` never resumes one; a suite-only run keeps the reference verdicts and a carried gate of the merged items (a gate that was not run is a note, not a red item). The plan lock is held through the registration and the seal (and by `--seal` alone), `inputs/packages.json` is written under its own guarded lock `packages.json.lock`, and `registerPackage` refuses a missing list of the quick tasks.

**The registry** (`inputs/packages.json`, `{ "NN": entry }`): the spec's and FORMAT's sha256, the held-out tree hash, the manifest's, the sealed archive's hash (once sealed), the references' tree hashes, the witnesses' and twins' tree hash, the counts, the memory
value both references passed at, the F-LOAD numbers, the gate verdict and date, **the runner and execution-layer pins the validation ran under** (the sha256 of `specs.d/runner.json` and `specs.d/exec-layer.json`) and the identity (`packageIdentity`: a sha256 over
the material and what it was validated under; measured walls are not in it, so a re-run of the same material has the same identity). `registerPackage` refuses a red or missing item, a record older than the material, moved pins, and an archive hash that differs
from `heldout-frozen.json` (`registryMismatchProblem`). **Order: validate everything, register, then seal.** Registration comes first, so the archive's hash joins the entry after the seal by the same function, as a new identity; a failed
seal leaves the clear cases exactly as they were and `package --task NN --seal` resumes it (it reads the latest complete record and takes no `--only`).

**The memory value's registration.** `register-memory` writes `registration` (`{ date, references: [twelve { task, stack, treeSha256 }] }`) into `inputs/memory-limit.json` only when all six packages have both references `accepted` at the file's `bytes`, and fixes the
note (`chosen` until then). A task's registration never waits for it. `memoryLimit()` returns the field (or `null`).

**The leak checks** read a quick task's `refs-NN/**` (both stacks) and `mutants/**` as needles; a code line that the runner or a template holds is a collision, counted per task and no needle; a manifest's witness ids and trap names are ids to keep out
of the public notes (a manifest that is not JSON is a refusal); and `lib/leak-gates.mjs` scans `spikes/lang-bench/inputs/gates/` for a case id, a witness or trap id, a case line or a window of the private notes, naming the file, the line and the kind.

## The sealed held-out folder (F2.S2 (d) and F2.S1 items 3 to 5, bench PR 4a)

A quick task's clear held-out cases (`~/rsd/lang-v2/explore/heldout-NN/cases/`) are sealed into ONE archive `heldout-NN/suite.enc` (AES-256-GCM: `MAGIC || IV || ciphertext || tag`, AAD = the magic; v1's `sealTree`, shared with `sealSuite`),
registered in `inputs/heldout-frozen.json` as counts and hashes (`{ "tasks": { "NN": { archive, archiveSha256, treeSha256, caseCount, trapCount, manifestSha256, keySha256, sealedOn } } }`; never a case id, a trap name, a text or a key),
and made again from the archive when a reader needs them. `traps.txt` stays clear beside `cases/` (a todo for F2.S1: whether the seal takes it). Nothing here is paid.

| Module | What it holds |
|---|---|
| `lib/package-seal.mjs` | `sealPackage` (the facade of the rest too; the module doc has the size and retention paragraph: six archives, kept with the results, a re-seal is a new identity, nothing prunes) |
| `lib/heldout-registry.mjs` | `writeEntry` (a temporary file of this process, flushed, renamed; an equal entry is a no-op, another one for the task is refused, a record that cannot be read is never overwritten), `registryMismatchProblem` (`packages.json`'s `archiveSha256` against the registry's; F2.S1's `registerPackage` calls it) |
| `lib/heldout-key.mjs` | the key (`readKey`: lstat, owner, mode 600, 700 folder, 32 bytes; only `keySha256` is ever written), `mountOf` (`findmnt --noheadings --output TARGET --target <real folder>`, exe and argv, a timeout, through `runBounded`), `backupProblem`, `makeBackup` |
| `lib/heldout-backup-config.mjs` | the optional private file `~/.config/lang-v2/heldout-backup.json` (`keyBackup`, `shareMount`; mode 600): flags win; there is no default path or mount in the code |
| `lib/heldout-context.mjs` | every seam as an argument with a default (`isAlive`, `mountOf`, `unseal`, `unpackTo`, `fs`, `now`, `hooks`, `pid`, `startTime`, `retry`); `withTaskLock` |
| `lib/heldout-unseal.mjs` | unsealing into an owned temporary folder inside `heldout-NN/` (`testsToDelete-<owner>-bench-clear-<id>` + its claim), mode 700, files 600, tree hash, ONE rename |
| `lib/heldout-lifecycle.mjs` | the stale decision, open, close, `withOpenedHeldout`, the `.open` stamp |
| `lib/heldout-check.mjs`, `lib/heldout-commands.mjs`, `lib/heldout-reader.mjs` | `heldout-check`; the three verbs and `heldoutAware`; the readers' one road in |

**The seal** (`sealPackage`, under the task's lock): the cases' shape is checked (a link or any non-regular entry in `cases/` is refused, naming it), `manifest.json` must exist (its hash goes into the entry), the key is copied to the private backup
and the copy verified BEFORE the archive is made (the backup folder is on the share's mount, `wx`, mode 600; a copy that is there and differs is refused, never overwritten), the archive is published once by `publishOnce` (temporary file, flushed,
hard link), then the entry is written. A kill between the two is resumed by the next seal when the archive opens to today's cases (otherwise it refuses naming the task; with no `cases/` it refuses: an archive with no entry is never trusted by its
authentication alone). An equal second seal is a no-op; a seal over an archive of other bytes, or over cases that changed, is refused (a re-seal is a new archive and a new identity, which F2.S1's package spec makes).

**The lock.** `heldout-NN/.clear.lock` with the takeover guard `.clear.lock.takeover` is v1's `acquireGuardedLock` (`minigit-bench/lib/series-lock.mjs`, the lock `acquireSeriesLock` is built on), not a third lock shape. The lock, the guard and
the `.open` stamp hold `{ pid, startTime, at }` as `takeScorerLock` writes them; "dead" is `isAlive(pid, startTime)` (`ownerAlive`) saying false and nothing else; an unreadable stamp is a refusal naming the file; a guard whose owner is gone is
refused with its path, pid, start time and creation time and "no takeover is in progress; remove <path> and run again".

**The order of an open** (lock held): the entry exists, the key file is valid, its sha256 is the entry's `keySha256` (before any decryption), the archive's sha256 is `archiveSha256`, it unseals into an owned temporary folder, the unsealed tree is
`treeSha256`, `cases/` is still absent, one rename, the stamp. Any refusal before the rename removes the temporary folder and its claim; a kill leaves it, and the next lock holder removes every owned temporary folder it finds (it is made only under the lock).

**The stale decision**, at every entry point (`readerHeldout`: `init`, `run`, `rescore`, `replay`; `leakcheck`; the verbs) under the lock; a task with no entry and no archive takes no lock:

| `cases/` | entry | stamp | decision |
|---|---|---|---|
| absent, no archive | none | any | today's behaviour; a stamp without a folder is removed |
| present | none | any | today's behaviour (an authoring folder) |
| present | yes | any | tree hash differs: refused, never removed |
| present, equal | yes | unreadable | refused, naming `.open` |
| present, equal | yes | operator, or a live pid | used as it is; nothing removed |
| present, equal | yes | none, or a dead pid | the key's backup is verified (exit 4 and nothing removed if it cannot be: no override), then `cases/` and `.open` are removed and the entry point goes on |

**Code round 1 (2026-11-09).** The registry's read-modify-write is under ONE registry lock (`heldout-frozen.json.lock`, the same guarded lock), taken inside the per-task lock, always in that order. The registry is read FIRST: a task with an entry is sealed even when `heldout-NN/` is gone (every reader refuses, naming the task and the archive path), and a registry that cannot be read is a refusal in `sweepClearFolder`, `openClearFolder`, `closeClearFolder` and `withOpenedHeldout` (never "unsealed"). A stamp is removed only after its folder; when the folder is already absent the stamp is judged (a dead process's is dropped; a live process's is refused; the operator's is refused at an entry point and dropped by the operator's own `heldout-close`). The key's backup copy is published whole or not at all (an owned temporary name beside it, mode 600, flushed, a hard link that fails when the name exists; the next run removes a temporary copy whose owner is dead; a copy that hashes to the key is accepted on a resume, another is refused naming it); the no-op seal verifies the backup too. `~/.config/lang-v2` must be the operator's and not writable by group or others; a malformed `heldout-backup.json` blocks only the quick tasks. `findmnt`'s first line is the mount; the warning of a folder a reader could not close names `explore.mjs heldout-close --task NN` with the backup named; `--tasks` rejects an empty entry before it resolves any task.

**Code round 2.** A clear folder is removed crash-safely: under the lock, `cases/` is renamed to an owned removing-folder (`testsToDelete-<owner>-bench-removing-<id>` with its claim) inside `heldout-NN/`, that folder is deleted, then its claim, then the stamp; the next lock holder finishes a killed removal (and its stamp) and never hashes the folder as `cases/`, so a crash never leaves a half-deleted `cases/` that reads as "changed after sealing". The key's backup is read through one handle (`O_NOFOLLOW`, `fstat` of that descriptor: a regular file of exactly 32 bytes, the bytes read from the same descriptor), after the mount check of its folder; a backup folder that vanishes during the copy is a refusal naming it. A stamp that already exists when the open writes its own is a refusal naming it and its owner (never an overwrite, never a crash). The open verb's hint prints the resolved `--key-backup` and `--share-mount` when they are known (flag or private file) and the placeholders when they are not.

`heldout-check` reports a stale or differing folder and removes nothing, and takes no lock (it unseals into an owned folder in the temp root). `heldout-close` refuses a folder held by another live process. `leakcheck` is the one reader that opens
a sealed task for itself: `withOpenedHeldout` holds the task's lock for the whole read, stamps the folder with this process, and closes it in a `finally` (also when the reader throws); `init`, `run`, `rescore` and `replay` refuse a sealed task
with no clear folder (`sealed: open it first (task NN); run explore.mjs heldout-open --task NN`) and read a held one as it is, after checking its tree hash against the registry (`openedFolderProblem`).

## The submission contract of the quick run (F1.S3 (b); parent D65, D66)

What the agent hands in is scored as it was handed in, after three things the harness does to the copy: it leaves out the stack's build-output folders, puts every frozen file back, and builds with the template's `build.sh`. The pieces:

- **One frozen list** (`spikes/lang-bench/lib/frozen-set.mjs`, beside the templates and `NPMRC_WORKSPACE`; no file of lang-bench imports lang-explore, and a test scans for it). The scripts, the stack's pin files, `STATEMENT.md`, `PACKAGES.md` and everything under `samples/` and `runner/` are frozen; `App.csproj`, `package.json` and `package-lock.json` are the agent's. The workspace builder (`lib/workspace-quick.mjs`), the clean copy (`lib/copy-quick.mjs`), the restore of the frozen files, `lang-bench`'s stack comparison and its template test all read this one file, and the folders a copy leaves out are the scorer's own `excludedDirs`, re-exported from it. A test scans both libraries for a second literal list of a stack's pin files.
- **The Node `.npmrc`** holds `offline=true`, `update-notifier=false`, `ignore-scripts=true` and `save-exact=true` (`NPMRC_WORKSPACE` in `lang-bench/lib/workspace-build.mjs`; the three templates follow it). With `ignore-scripts`, a `preinstall`, `install`, `postinstall` or `prepare` script of the agent's `package.json` cannot rewrite a frozen file during `npm ci` (T-L62 proves it in the build box, with the control that does).
- **The identity of the contract** (`lib/contract-identity.mjs`): a sha256 over the sorted `path<TAB>sha256` lines of the clean copy, the marked build, the frozen list, the build box and the offline settings, the excluded-folder list (`spikes/minigit-bench/lib/score-deps.mjs`) and the package matrix (the offered set is part of the contract), every file under `spikes/lang-bench/templates/`, and the tree hashes of the two registered snapshots. A scoring writes it into `score.json` as `explore.contractIdentity`; `record.json` carries the value read from the score, so a record finished later (`finish`) still names the identity it was scored under, and a score made before it existed reads `unknown`. It does not move with a fix to `score-quick.mjs` or a change to the statement's sentences. It moves with this story's own template edit, with the new C# snapshot, and with F1.S1's runner re-pin only if that touches a template.
- **Deviations** (`explore.deviations`, `{ count, paths }`; known unless a changed file could not be judged, see the drift bullet): the frozen files the agent changed, from the agent half of `splitFrozenChanges`. They sit beside `accepted` in the record and in the note's quality table (a column that is there when any session of the note has the field, so a note over older records is unchanged) and never decide it (`acceptedOf` does not read them); `explore.frozenChanges` is kept as it was. D65's lexical taxonomy is not built.
- **Template drift, judged per file by bytes** (`lib/template-drift.mjs`). A frozen file that is a template file (a script or a pin file), a file of the runner, the derived `PACKAGES.md` or one of the two files generated from the one memory value (`STATEMENT.md`, `samples/load.json`) can differ from today's bytes because the harness moved since the session began, or because the agent changed it. It is the harness's (`explore.harnessDrift`, also copied into `record.json`) only when the tree's bytes equal the bytes the file had when the series began; any other difference, a deletion included, is the agent's (`frozenChanges`, the deviations): a rewritten `build.sh` or `strict` turned off stays a deviation though the template moved. `init` records the sha256 of every template file (`templateHashes`, with `templatesIdentity` and the runner pin) and of the `PACKAGES.md` of each stack for the task's kind (`packagesMdSha256`). For a series made before that, the old bytes are read with `git show` at the commit its subject recorded (`inputs.subject.sha`); `PACKAGES.md` is rendered again from the record and the recipe of that commit, every package offered in every kind when there was no matrix. Where the old bytes cannot be read, the file is `explore.undecidedDrift`, charged to nobody, and `explore.deviations` is unknown (with what is decided in `counted`), never a known count. `PACKAGES.md` was being charged as a deviation on a rescore because it is rendered from today's record and matrix; it is judged like a template file now, for both stacks alike.
  **The runner files follow it too (final seam review of F1).** The blanket rule (every change under `runner/` is the harness's when the runner pin moved) filed an agent's real edit of a runner file as drift; it is gone. A runner file is the harness's only when the tree has the bytes the file had in the runner as pinned at the series' subject (`inputs/specs.d/runner.json` read with `git show` at `inputs.subject.sha`; a file that pin does not list did not exist then), the agent's when it has any other. `runner/GRAMMAR.md` is made from FORMAT and is not in that pin, so a change to it that differs from today's is undecided. Where the pin cannot be read, or the series has no history and its runner may have moved (`driftPossible`), the file is `undecidedDrift` and the deviations are unknown, never zero; with no history and an unmoved runner a difference is the agent's.
  **The generated files follow the same per-file rule** (`lib/generated-drift.mjs`, the merge of F1.S2 and F1.S3). `init` records the sha256 of `STATEMENT.md` and `samples/load.json` of each stack (`generatedSha256`; `null` for a file the workspace does not have), so a later change of the memory value cannot be mistaken for the agent's. For a series made before that, the load file is a known absence when the series' commit has no `inputs/memory-limit.json`, and the statement is made again as it was at that commit (the spec, the stack and framework sentences of that commit's pins and record, no memory and heap lines); when the commit already had the memory value, or the spec this host reads is not the one the series recorded, or the pins cannot be read, the file is undecided. **A file that did not exist when the series began and is missing now is the harness's change**, not an agent's deletion (a file the agent made where the series had none is still the agent's); any other missing file is the agent's deletion.
  **The ten-pass path judges the same way.** `scoreSessionDep` hands `history` and `driftPossible` to the quick world's seams (`passSeams`), `prepareTree` (`lib/score-quick.mjs`) splits the changes per file, the single-pass world (`quickScoreFn` in `lib/world.mjs`, what `replay` scores through) passes `history` on to `scoreQuick` as well, a replay line names `undecidedDrift` and `contractIdentity`, and `commitBuild` writes the undecided files (`undecidedDrift`) and the contract identity (`contractIdentity`) into `score/build.json` with the frozen changes and the harness drift, because that record is written once at the commit; `finishRollup` reads them from it into `score.json`, from where `record.json` takes them. A completion record written before this has neither field and finishes without them (no undecided file, an identity that reads `unknown`).

Tests: `test/quick-contract.test.mjs`, `test/quick-drift.test.mjs`, `test/quick-generated-drift.test.mjs`, `test/quick-passes-drift.test.mjs`, `test/note.test.mjs`, `test/quick-rescore-run.test.mjs`, `test/quick-replay.test.mjs`, `test/quick-build-frozen.test.mjs`, `test/quick-build-frozen-real.test.mjs`, `test/quick-same-harness.test.mjs`, and on the bench side `lang-bench/test/templates.test.mjs` and `lang-bench/test/scripts-stopped.test.mjs`.

## Data

- Private, outside every git tree (mode 0700): `~/rsd/lang-v2/explore/` — `package-NN/<run>/` (a package's records, `s.json`), `refs-NN/<stack>/` (the two references), `heldout/` and `heldout-NN/` (generator, cases,
  references, mutants, manifest, and for the olympiad tasks `brute.py` and `handcheck.md`), `calibration.json` and
  `calibration-NN.json`, `spin.json`, and one directory per series: `run-map.json`, `ledger.ndjson`,
  `pairs/`, `s/<rid>/` per session (records, stream, transcripts, egress and broker logs), `private/` (the hand check and
  any trap detail, D41).
- Public: the notes and the analyses under `research/lang-v2/`, counts and rates only.

## Tests

`node --test spikes/lang-explore/test/*.test.mjs` (use `--test-concurrency=1`: a leftover-scope check of `judge.test.mjs`
sees the live scope of another file's test when files run side by side). The olympiad plan's T-O1 and T-O2 are
`olympiad-tasks.test.mjs`, T-O3 `olympiad-samples`, T-O4 and T-O8 `olympiad-calibrate`, T-O5 `olympiad-leak` and `olympiad-leak-scan` (the tiers, the baseline), `olympiad-leak-perf` (5 million lines in under 60 s; skipped unless `LEAK_PERF=1`), T-O6
`olympiad-rehearse` (a toy task, so no real `heldout-NN/` is read), T-O7 `olympiad-pause`; `olympiad-note` pins the per-task
note. T-O2 copies the real series `x-20261101-13cc` into a temp home at run time (`hostLacks` without it) and compares
status, readback and note with hashes taken before the change; the real series is never written.

## Known limits (from the run; carried to v2 E1)

The per-case wall has about 50 ms resolution and includes the scope and box start-up; v1's descriptive `build_test`
category does not know this task's scripts (the explore section's own field does); the package list is the shared box
cache; the leak check's default history range starts before main's merges (use `--range origin/main..HEAD`). Details:
the analysis, §4.
