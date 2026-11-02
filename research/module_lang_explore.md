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
| `leakcheck [--task NN] [--range A..B]` | free | the leak scan ("The leak check", below) over the repository and the branch range, with the private material of EVERY task (`--task` limits it to one); exit 0 clean, 5 a finding, 4 the material could not be read; a finding names where, never what |
| `prune --task NN` | free | removes the seeded cache copies and scoring copies after the analysis; keeps the records, the ledger, the calibration and the held-out folder |

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

## Data

- Private, outside every git tree (mode 0700): `~/rsd/lang-v2/explore/` — `heldout/` and `heldout-NN/` (generator, cases,
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
