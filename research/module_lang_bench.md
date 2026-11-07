# Language comparison v2 — the bench module (`spikes/lang-bench`)

> One `##` section per E1 story; each story writes only under its own heading (`todo/PLAN_lang_comparison_v2_E1.md` § 3, "Conventions for every story").
>
> *(2026-11-07: the plan's revision for epoch 9 replaces its epics with F1 to F3 (`todo/PLAN_lang_comparison_v2.md`; in force from the epoch 9 line); the sections below keep their E1 names as built, and Stage 1's scoring engine is `spikes/lang-explore/` (`research/module_lang_explore.md`).)*

## The runner (E1.S8a)

**Purpose.** `runner/` is the one Python standard-library program the agent, CI, the pre-freeze gate and E2's held-out scoring share (D37). It
reads a folder of `<id>.in` / `<id>.out` pairs, builds with `bash build.sh`, runs `bash run.sh` per case and reports `PASS` / `FAIL` with the
first mismatch under FORMAT v2's rules (grammar 2). It knows nothing of boxes, scopes or namespaces: the execution layer hands it a launch prefix.
**Built so far: part i** (olympiad runs, parsing and validation of daemon and web samples, the matcher, the report, the pin) and **part ii-a** (what the
olympiad path needs from the execution layer: the launch prefix and status, the case clock, the pidfd waits, the cgroup read and kill, the case view, the
program's environment). **Part ii-b** (sockets, the daemon and web runs, readiness, `signal TERM` to the program's host pid, V3, V5, V7, `overflow <conn>`)
was built for the owner's quick run (2026-11-05): see *The daemon and web runner* below.

**Files** (`spikes/lang-bench/`):

| File | Role |
|---|---|
| `runner/run_samples.py` | the entry: arguments, kind, validation of every pair, build, olympiad cases, report, exit codes |
| `runner/rs_lex.py` | lines, tokens, durations, strings and escapes, variables, normalised fields; problems are `(file, line, message)` |
| `runner/rs_model.py`, `rs_script.py`, `rs_check.py` | the `.in` parser into frozen dataclasses, its validation, the entries a script produces |
| `runner/rs_transcript.py`, `rs_pair.py`, `rs_assign.py` | the `.out` parser, script/transcript correspondence, one-to-one assignment |
| `runner/rs_json.py`, `rs_fields.py`, `rs_jmatch.py`, `rs_match.py` | exact-decimal JSON, fields and the backtracking matcher, JSON bodies with `$unordered`, entries and V6 checks |
| `runner/rs_exec.py`, `rs_olympiad.py` | the build, the launch argv, the program's environment (V11), the pidfd wait, the plain lifecycle; the streaming olympiad case |
| `runner/rs_launch.py`, `rs_status.py` | starting a case through the prefix, the `child-pid` wait and `LaunchFailed`, the empty program; bwrap's JSON status lines and the per-case pipe |
| `runner/rs_scope.py`, `rs_cgroup.py` | the lifecycle through `--cgroup-of`: ownership, memory, the always-written `cgroup.kill`; bwrap by identity, `cgroup.procs`, `memory.peak` and `memory.events` |
| `runner/rs_track.py`, `rs_pump.py` | which process is the program and what else the case leaves (`fork`); the pump: stdin, stdout, the look, the end on the pidfd or the `exit-code` record |
| `runner/rs_stop.py` | the runner stopped by SIGTERM, SIGINT or SIGHUP: the handler, and the stop of every launcher still registered |
| `runner/rs_view.py`, `rs_layer.py` | the case view and its `.scenario` fixtures; the layer's options as the command line takes them, `RunInfo` |
| `runner/rs_samples.py`, `rs_report.py` | pairs, `cases.txt`, limits; report lines and the JSON result |
| `pin-runner.mjs`, `lib/runner-pin.mjs` | the pin: writes `inputs/specs.d/runner.json` (`runner: { version, files, manifestSha256 }`), `--check` |
| `specs-record.mjs`, `lib/specs-record.mjs` | assembles `inputs/specs.json` from the writers' files in `inputs/specs.d/`; `--check`; never edit `specs.json` by hand |

**CLI.** `python3 -I runner/run_samples.py [--workspace DIR] [--kind olympiad|daemon|web] [--no-build] [--limits FILE] [--launch-prefix JSON]
[--launch-status FD] [--cgroup-of TEMPLATE] [--case-view TEMPLATE] [--json OUT] [--only ID] [--parse-only] SAMPLES_DIR`. The kind is read from the `Kind` row of the workspace's `STATEMENT.md` unless `--kind` is given.
`--limits` is a JSON object with `timeS`, `readyS`, `graceS` (exact seconds, from 1 to 600; unknown keys refused); without it the olympiad limit is the
floor (1 s) and every time verdict says `against the floor`. `--launch-prefix` is a JSON argv put before `bash run.sh`, `{case}` replaced.
`--parse-only` parses and validates every pair and starts nothing. A `cases.txt` beside the pairs attaches its R-ids to each case. Without a cgroup every case says
`memory: not certified`.

*The execution layer's options (part ii-a).* **`--launch-prefix`** is passed as ONE JSON array of strings (the plan's form); `{case}` in any element is the case
id (letters, digits, `_`, `-`; the id `empty-program` is reserved for the control below and a sample that takes it is exit 4). **`--launch-status FD`** (B5) is the
descriptor number the prefix gives bwrap as `--json-status-fd FD`: the runner makes one pipe per case and puts its write end at `FD` in the launcher (so the layer does not
pass an open descriptor; it names a number, 3 or more and not open in the runner, and needs `--launch-prefix`). bwrap writes `{"child-pid": P}` and later `{"exit-code": C}`.
**The status rules (P2):** before `child-pid`, a line that is not one JSON object, a record that is neither `child-pid` nor `exit-code`, a doubled `child-pid` or
`exit-code`, no `child-pid` before the launcher exits (or within 60 s), or an incomplete status line longer than 64 KiB, or a cgroup that does not exist at `child-pid`, is `LaunchFailed`: exit 3 `launch failed` with the
bounded tail (2 KiB) of the prefix's standard error, never a case verdict. After `child-pid` everything is the case (an immediate exit is an ordinary end; junk lines, over-long ones included, are
ignored). `child-pid` is the launch-success signal and **the start of the case clock**, not the pid authority; `exit-code` is the program's status (without one the
launcher's own is used). *bwrap writes `child-pid` right after it clones the sandbox, BEFORE the box's mounts are made, so a few milliseconds of box set-up are inside the case
clock (the numbers measured for the plan stay as they are); the plan fixes the start at `child-pid` and it is not moved.* **`--cgroup-of TEMPLATE`** (B1, B4; `{case}` filled
in) names the program's cgroup2 directory. With a launch prefix it needs `--launch-status` (exit 4). The runner owns what it measures and kills: before the spawn the
directory must not exist or must hold no process (else exit 3: it never measures or kills a cgroup this case does not own). It reads `cgroup.procs`, `memory.peak` and
`memory.events` after the program's end and BEFORE any kill, then ALWAYS writes `1` to `cgroup.kill` (a leftover found or not) and waits until `cgroup.procs` is empty (2 s
ceiling, else exit 5). **The order of the kill:** the launcher is never reaped before the kill and its fallback are done (it is awaited on its pidfd, which does not reap; a group signal always goes to an unreaped leader, whose pid is still reserved). Where `cgroup.kill` cannot be written, the fallback kills the launcher's process group, then SIGKILLs each host pid `cgroup.procs` still lists and checks the cgroup is empty, else CleanupFailed. *Empty* means: its directory is gone, or the kernel took it offline (ENODEV), or it had been readable before the kill and its files are gone while the directory lingers (a dying cgroup), or every listed pid is a zombie. In a box (with `--launch-status`) a cgroup directory that vanishes after `child-pid` is exit 3, never a case status (the launcher's group is killed first). A `cgroup.procs` with a line that is no pid is unreadable, and unreadable is never empty. On the plain path (no cgroup) the stragglers are looked for in the launcher's group before the launcher is reaped (a zombie leader does not count), and the group is signalled before the reap. **The rule for the program's pid (B1, P4):** a bwrap
process is known by IDENTITY (`/proc/<pid>/exe` is the file, by device and inode, of the bwrap binary the prefix names; a copy of bash named bwrap is not one, and `comm` is
never used); the program is the process of `cgroup.procs` whose PARENT is a bwrap process (`bash run.sh` execs it: D38), the launcher pid is never a candidate, a short tool
bash starts has bash for a parent. Host pids only; a pid seen inside the box is never used; the pidfd is checked against the cgroup and the bwrap parent after it is taken
(a recycled pid is neither). The look is part of the pump's loop (backing off from 1 ms to 20 ms while the program is not found, then every 20 ms; the end still comes from the pidfd or the record, so a late discovery costs no wall time), so the program's stdin and stdout flow from
`child-pid` on. Two candidates with a bwrap parent are `fork` and no pid is chosen. **`fork`** is judged over the whole case from the processes that are not bwrap, not the
launcher, not the wrapper and not the program: the most seen in two successive looks, and what a leftover read (repeated 20 ms later, so a process caught exiting is not one)
finds after the exit; it is `known: false` when no pid was settled, and it never decides the pid. **`--wrapper-pid-file TEMPLATE`** (with `--cgroup-of`; `{case}` filled in) is
the file in which the layer's wrapper writes its pid; without the flag nothing is excluded as a wrapper (E1.S8b's delegated scope has none; its fallback `scope-wrap.sh`
must pass the flag). `oom_kill` above 0 in `memory.events` is the verdict `memory limit` whatever the exit status and is read from `memory.events` alone, even when
`memory.peak` cannot be read; it is judged before `time limit`. **A runtime's own out-of-memory abort is `memory limit` too** (change 5.1, runner 0.2.3, `rs_abort.py`): .NET exit status 134 AND a stderr
line exactly `Out of memory.`, V8 exit status 134 AND a stderr line holding `JavaScript heap out of memory`; `memory` then carries `runtimeAbort: "dotnet" | "v8" | null` beside `oomKill`. **The memory record has six fields (parent D56):** `peakBytes`, `maxBytes`, `oomKill`, `runtimeAbort`, `abortBelowEvidence` and `childAbortLine` (what was not read is `null`, never zero; the two flags are `false` unless raised; `known`, `why` and `events` stay beside them). **Only WITH evidence (fix batch 1, O7):** `memory.peak` at least half of `memory.max` (both are in the record as `peakBytes` and `maxBytes`), so a faked line at a low peak stays a runtime failure. **The boundary is tested at the byte (T-L50), on a FAKE cgroup node** (`test/py/test_rs_abort.py`: the test writes `memory.peak` and `memory.max` itself): L is a whole number of MiB, so L/2 is whole; a peak of L/2 is evidence (`peak * 2 >= max`) and L/2 − 1 byte is not. Through the real prefix (`test/exec-layer-runner.test.mjs`) the test is COARSE — the box's own processes add some bytes to `memory.peak`, so a stub holds memory well above and well below L/2. **Below the threshold** an exit-134 abort with the line is `exit status 134` (the runner's name for a runtime error) with `abortBelowEvidence: true` (also when the peak could not be read), a report line, and the line, status and peak kept; the line with exit 1, or exit 134 with another message, carries no `abortBelowEvidence` flag. **A child's abort line (`childAbortLine`):** the line in the stderr of a program that exits 0, or exits with ANY status other than 134 (exit 1 included), means a child runtime aborted and the parent went on or failed on its own; the case is judged on its output and status, never `memory limit` by the abort rule, and flagged; a kernel `oom_kill` is `memory limit` by its own cause and raises no flag. Both flags are counted per stack by the report's reader and go to the blind adjudication. The abort rule's module `rs_abort.py` is in the olympiad module set that T-L38 compares across runner manifests (`lib/runner-pin.mjs`: `OLYMPIAD_MODULES`, `olympiadDifferences`, symmetric: a module only the second manifest has is a difference). Each runner manifest change gets a new `RUNNER_VERSION`: `inputs/runner-versions.json` records one manifest per version and `pin-runner.mjs` refuses a second one (Q6); .NET's unhandled `System.OutOfMemoryException` (also wrapped in an `AggregateException`: exit 134, an `Unhandled exception.` line, the exception text anywhere in the tail) counts like the `Out of memory.` line. *Historical (policy C, the view alone, before D55):* a V8 abort at its own default heap limit (about 268 MiB, whatever the cgroup) is judged memory limit only when the peak reaches half of `memory.max`: Node `objects-70` at 512 MiB aborted at a peak of about 280 MiB, 54 % of the limit (`opk-experiment-2026-11-02.json`, followUp3). Under D55 the flag makes V8's limit about 0.9·L and its aborts land at 97–98 % of L (`exec-probe-2026-11-02-2.json`). The verdict table
of `memory limit`: kernel `oom_kill` ≥ 1 → memory limit; `runtimeAbort` set → memory limit; exit 134 alone, or either message without exit 134, or `abort()` with another message → a runtime failure
(`exit status 134`, `exit status 1`). It is read from the end of the stderr file (64 KiB), because V8 prints its stack trace after the message. **`--case-view TEMPLATE`** (B7; `{case}` filled in) names the case's fresh writable view of the built tree,
which the layer made before the case starts (a missing view is exit 3): the program starts in it, a scenario's `.scenario/` fixtures are written into it first (every fixture
path is relative, free of `..`, and made one component at a time with `O_NOFOLLOW`, so a link on the way refuses it; a `.scenario` already there is refused), and the samples
directory may not lie in a view nor a view in the samples (exit 4). **With more than one case, a `--cgroup-of`, `--case-view` or `--wrapper-pid-file` template without `{case}` is
exit 4.** Nothing is read back from a view, and the runner never assumes the filesystem persists from one case to the next.

*What a cgroup must be (round 4).* The directory named by `--cgroup-of` must be a cgroup2 node (`cgroup.controllers` is there) or the spawn is refused (exit 3). Its counters are
not reset between cases, so an existing cgroup whose `memory.peak` or `oom_kill` is above zero before the spawn is refused too (exit 3); the runner does not judge an *increase*:
a stale counter would be a verdict the program never earned. After `child-pid` the box must BELONG to the cgroup: `/proc/<child-pid>/cgroup` names it or a descendant of it, else the
box is stopped through a pidfd of `child-pid` (and the launcher's group), nothing is written to the wrong cgroup, and the launch has failed (exit 3). A box that is already gone at
that look cannot be confirmed: that is no failure, but its memory is then unknown (`known: false`; the counters of a cgroup the box was never seen in are not this case's, and no
`memory limit` is judged from them). `cgroup.kill` is opened and never created. For emptiness the runner trusts `populated 0` of `cgroup.events` when it is there; a pid of 0, or a
pid whose stat cannot be read, counts as alive (only a stat that says zombie is dead). The wait for emptiness decides on the FIRST look that finds the cgroup empty: a cgroup the
kernel is taking down answers differently from one read to the next, and a second look to confirm the first once made the real-scope tests fail 5 times in 20. A process taken for the program
that is still alive when the program has ended was an orphan (the program was gone before the first look and its child became a child of bwrap's init): it is a leftover and its pid is
not reported as the program's. The look into the cgroup is paced by TIME (1 ms backing off to 20 ms while the program is not found, then every 20 ms), never by I/O: a look costs about
200 µs and a program printing 64 MiB used to pay it on every read (79 ms became 190 ms). `wallMs`, `launchMs` and `emptyProgramMs` are rounded UP, all three, so the F1 subtraction
mixes no two roundings. An `exit-code` record that arrives in the same read as `child-pid` is the end of the case at the instant it was read, never the launcher's teardown.

*Round 5.* The directory named by `--cgroup-of` must lie under a cgroup2 MOUNT (found in `/proc/self/mountinfo`, the nearest mount above the resolved path must be cgroup2) as well
as hold `cgroup.controllers`; a controllers file in a plain folder is refused (exit 3) and nothing is written to it. The box must be IN the named cgroup, not in a descendant of it
(`/proc/<child-pid>/cgroup` equals the directory's path from the mount root, both resolved), and a launch failure for a box in the wrong cgroup names the expected cgroup and the one the
box was seen in. The pidfd of `child-pid` is opened FIRST and the box is judged and, if need be, stopped through it; the fallback kill signals each listed pid through a pidfd opened for it
after re-checking that the pid is still listed. An existing cgroup is free only when `populated` of `cgroup.events` says 0 (else its listing is read). On the plain path (no box, no
`child-pid`) the LAUNCHER must have been seen in the cgroup (listed, or `/proc/<pid>/cgroup` names it) or memory is unknown: a cgroup the program does not run in has counters of 0 that mean
nothing. A `--cgroup-of` that never existed on the plain path is exit 3 (after the launcher's group was killed); one that was there and went is still a cleanup failure (exit 5). An extra
process counts for `fork` only when it has been in the cgroup for 100 ms (looks at least 100 ms apart), or is still alive at the program's end (the leftover read). **A property of the rule, measured and not tuned around:** under a CPU load a forked short tool that the scheduler does not run for 100 ms IS a process that stayed 100 ms, so a loaded host can print a false `fork:` line — about 1 round in 160 standalone with every core busy (measured in E1.S8a part ii-a, #147; the F2 test burns half the cores itself), and about 0.014 per round under the full suite's own load; the test asserts the rate (at most 2 false forks in 20 rounds under half the cores burning: a false red about once in 3000 standalone runs, about 2.8e-3 under the suite's load, once in about 360 suite runs) and a broken rule, which counts a second process in every round, is red in most runs but not in all; the guard that reliably catches a removed 100 ms rule is the no-load test `test_A_run_sh_that_starts_a_short_tool_and_then_execs_is_not_fork_and_the_pid_is_the_programs`. **A known limit of the temporary-file safety (P5):** `--out` in a shared, writable, NON-sticky folder is not supported — a folder swapped for a link after the sweep's `lstat` (or `writeTmp`'s) check can still be followed; `inputs/` and a sticky `/tmp` are safe. The end of the
program (for the orphan rule) is judged by its pidfd, not by `/proc`. Waiting for a group to go is a scan of `/proc` paced from 1 ms to 20 ms. The stop handler blocks every stopping
signal at once before it sets them to ignore, and unblocks them again once they are ignored (no blocked mask outlives the handler: a child of the teardown would inherit it). The fallback kill
works in batches of at most 64 pids: a pidfd is opened for each pid of the batch, `cgroup.procs` is read ONCE, the pidfds whose pid is still listed are signalled, and all are closed; a pidfd that
cannot be opened for lack of descriptors (EMFILE, ENFILE) ends the batch, what is open is signalled and closed, and the pids not tried go on in the next batch. A mountinfo line that is malformed
(too few fields, no ` - ` part) is skipped. A `--cgroup-of` that holds a controllers file but lies under no cgroup2 mount is reported as exactly that ("is not under a cgroup2 mount"). Limit
(G-round, fail closed): a cgroup2 mounted as a SUBTREE (its mountinfo root is not `/`) is no cgroup2 root for the runner and a `--cgroup-of` under it is refused; the mount layout is S8b's.
Limit (F8, very low probability, not fixed): the box is judged by `/proc/<child-pid>/cgroup` at one instant; a box that moves itself into
another cgroup later is not followed.

*Host prerequisites (P1).* At the start of a run the host must be Linux 5.14 or newer and have a working `os.pidfd_open`, and cgroup v2 at `/sys/fs/cgroup` when `--cgroup-of`
is given; each missing one is named in an exit 3, never a crash.

*The clock and the waits (B6, P3).* One clock, `time.monotonic_ns`. The case clock starts at the instant `child-pid` is read (at the spawn without `--launch-status`). The end of a
case is the instant of the FIRST of: the program's own pidfd turning readable (a grandchild behind a box, so `Popen.wait` could not do it), bwrap's `exit-code` record being
read from the status pipe; the launcher's own exit is never program time (without a box the launcher is the program). With neither a pid nor a record the case still ends
when the launcher goes, but its wall is unknown (`wallMs: { known: false }`) and the verdict comes from the output and the deadline kill only. Every wait is `os.pidfd_open` +
`select` with the exact remaining time; `Popen.wait(timeout=…)` is not used (a scan test says so). The wall rule, in nanoseconds: a wall over the limit is `time limit` even when
the program exited by itself, exactly the limit passes, one millisecond over does not. Once per run, before the cases, an empty program (`bash -c :`) is timed through the same
prefix, clock and pump (`emptyProgramMs`, and `emptyProgram: { ms, launchMs, pid, found, comm }` says which pid it waited on), in the view of `empty-program` when `--case-view` is set; progress lines on standard error announce it and tell its time; its `{case}` is `empty-program`, so a layer whose
prefix binds a per-case view or cgroup must provide one by that name. **V11:** the program's environment is a copy of the runner's own, plus (for the daemon and web kinds,
part ii-b) `PORT` and the scenario's `env` lines; nothing else is added or removed (the layer sets it; the runner does not sanitise it).

*Being stopped.* SIGTERM, SIGINT and SIGHUP stop the runner with exit 5 and no verdict. The stopping signals are held off while a launcher is spawned and the launcher is
registered the moment it exists, so every launcher still alive is stopped (`cgroup.kill`, or the group on the plain path, and never a group whose leader was already reaped)
whatever instruction the signal lands on; the pump is made inside the `try`.

**Exit codes.** `0` every case passed (or `--parse-only` found no problem) · `1` a case failed, a failing build counts · `3` the environment (no workspace, no `bash`, a
host that lacks a prerequisite, a case view that is missing, a cgroup that is not this case's or that vanished after `child-pid`, **a launch that failed**) · `4` configuration: usage, a malformed sample (file and
line), a version 1 file, a script/transcript mismatch, a bad limits file or `cases.txt`, the layer options that contradict each other · `5` the runner itself failed (a traceback,
a daemon/web run before part ii-b, a process of the case that could not be killed, a launcher that could not be stopped, a stop by a signal).

**Result format.** `--json OUT` writes `{ format: 1, runner, grammar: 2, kind, build, launch: { failed, stderr }, emptyProgramMs, emptyProgram, cases: [{ id, status, reason,
rIds, entries, firstMismatch, lifecycle: { stop, signalled: { signal, pid, found, comm, launcher }, fork, leftover, leftoverEvidence, kill, memory, stderr, capture }, wallMs,
launchMs }], python }` (runner version 0.2.1); what was not measured is `{ known: false, why }`, never zero. `wallMs` is the program's wall from the case clock (or unknown),
`launchMs` the time from the spawn to `child-pid`, `emptyProgramMs` the empty program's time (milliseconds, or `{ known: false }`). `lifecycle.signalled` names the pid the
runner knows as the program's, how it found it, its name as last seen alive and the launcher's pid (`signal` is null: an olympiad program is never signalled);
`lifecycle.fork` is `{ known, found, count }`; `lifecycle.kill` and `leftoverEvidence` say which road ran (`killpg` and the process group on the plain path, `cgroup.kill` and
`cgroup.procs` on the scope path); `memory` is `{ known: true, peakBytes, events, oomKill }` from the cgroup, or `known: false` with whichever part was read. A failed launch writes
the file with `launch.failed` true, the cases that had finished, and exits 3.

**Bounds.** An olympiad case's stdout is compared as a stream and stops at the first difference; at most 16 MiB of it is kept for the report, and a
truncated capture is never what decides a `PASS`; stderr goes to a file (never a pipe; its record says `truncated` past 16 MiB; the case is killed past 256 MiB); a case is cut off at its limit and the group (the plain path) or
the cgroup (`cgroup.kill`) is killed; one test is at most 600 s (`--limits` refuses more) and a launch at most 60 s; a wall time over the limit is `time limit` even when the program exited by itself; the build has a 600 s ceiling; a transcript's
`unordered` group holds 2 to 64 entries; a regex or field match is cut off after 500,000 steps; a string expression expands to at most 200 parts.

**Encoding (B10).** `python3 -I` ignores `PYTHONIOENCODING` and `PYTHONUTF8`, so the entry reconfigures stdout and stderr to UTF-8 with
`backslashreplace`; every file is read as bytes; a file name that is not UTF-8 is printed as `\xHH`; an invalid-UTF-8 JSON body is the verdict `bad-utf8`.

**Seams for part ii-b.** A lifecycle has `memory`, `kill`, `present`, `check_free` and `settle`: `rs_exec.PlainLifecycle` for the plain path, `rs_scope.ScopeLifecycle` for
`--cgroup-of`, chosen per case by `rs_scope.lifecycle_for`; the tracker (`rs_track.Tracker`) holds the program's pid; `rs_launch.start` is the one way a program is started and `Launched.tracker.pid` is the pid `signal TERM`
will use; the pidfd wait is `rs_exec.wait_exit`; `run_samples._after_parsing` is where daemon and web dispatch; `rs_transcript.Actual` and `rs_match.compare` take the
observations part ii records; `Actual.step_line` and `step_text` feed the report's `step` line; `rs_script.Step` carries what each step needs.
The tests are in [module_tests.md](module_tests.md), section "The language-comparison runner scenarios".

## The daemon and web runner (the owner's quick run, runner 0.3.6)

**Purpose.** `run_samples.py --kind daemon|web` runs every pair of scenario samples against a program the runner starts (`bash run.sh` with `PORT` and the scenario's environment), drives
the scenario's steps over sockets and standard input, records the actual transcript and judges it against the expected one with the matcher of part i. The olympiad path is unchanged.
The runner is pinned as `0.3.6` (`node spikes/lang-bench/pin-runner.mjs --check`); the earlier versions stay in `inputs/runner-versions.json`.

| File | Role |
|---|---|
| `runner/rs_session.py` | one daemon or web case: start, the wait loop, the program's lifecycle, the steps, the judgement; one thread drives everything, the scenario's sockets are registered in the pump's selector |
| `runner/rs_net.py` | the connections of a case as a mixin of the session: connect and retry, writes, a start-together group, the reads (`recv` modes, one HTTP response, `quiet`, `closed`) |
| `runner/rs_base.py` | what the pieces share: `RunnerFault`, `Abort`, the case, the standard-output sink, the together gate, the constants |
| `runner/rs_conn.py` | a connection's buffer, the chunked reads and the HTTP/1.1 response parser (`DONE`, `CONTINUE`, `BAD`) |
| `runner/rs_capture.py` | the `capture` of a step: a regular expression or a JSON path, checked against its declared type |
| `runner/rs_evidence.py` | what a failing case keeps (see below) |
| `runner/rs_launch.py` | `_unblock_all`: the program starts with the signals the runner held off around the spawn unblocked, or it would never see a scenario's `signal TERM` |

**The shape rule holds for the whole runner** (no exemption: functions under 50 lines and a complexity of at most 4, files under 800 lines). The split of `rs_session.py` and `rs_conn.py` into small functions changed no behaviour; it was checked outside git against the module before the split on thousands of generated cases (the wait loop and its guard, the judgement, the finish, the stop, the reads, the request bytes), and the toy-program tests (`test_rs_session.py`) were not run for it: they start programs and wait for the end of the series.

**A harness failure is never a verdict.** What a program does wrong is an entry of the actual transcript (`not-ready`, `refused`, `timeout`, `eof`, `early-exit`, `exit-signal`, ...). A runner
fault (`RunnerFault`, or any exception that is not an observation of the program) ends the runner with exit 5 and no result for the case; a launch that the layer cannot confirm is exit 3. The
connect step follows this rule exactly: only a refused, reset, aborted or timed-out connect means the program is not listening yet (it is retried until the step's deadline and then recorded as
`refused`); any other error of the socket (no descriptors, ports, buffers or memory, no route) is the host's own and is a `RunnerFault`. The same holds for a read: only a reset, an abort, a broken pipe or a timeout of the peer's end is the end of a connection (`eof`); any other error of `recv` is a `RunnerFault` (runner 0.3.6). The readiness probe of `ready port` follows the same rule (2026-11-06): only the program's not listening yet is `not-ready`; a host error of the probe is a `RunnerFault`. A process found in the cgroup after the exit counts as a leftover only when it is still there after 2 s (`LEFTOVER_SETTLE_MS`, 2026-11-06): one recheck after 20 ms read a process still exiting on a loaded machine as a leftover, a false reject seen once at load 22.

**What a reject keeps.** A failing case's `firstMismatch` holds, next to `entry`, `of`, `part` and `detail`, an `evidence` object: the expected entry as the report prints it, and the observations that explain the
difference: the one differing observation of an ordered transcript, every observation of an unordered group that no assignment matched (with the connection names), or the last eight of a session that ended with
another count. Every string is cut the way the report's lines are (`abbreviate`), at most 64 observations and 16 headers are kept. It is a program's output: the harness writes it into the session's `score.json` under the
series folder, never into git. No verdict reads it; `test_rs_evidence.py` holds the verdict fields of every kind of mismatch as goldens taken before the evidence existed.

**Scoring a quick task** (`spikes/lang-explore/lib/score-quick.mjs`, `build-quick.mjs`): the agent's tree is copied cleanly (its build output removed, every frozen file put back), the frozen `build.sh` runs once in the
stack's build box (all quick scoring runs on CPUs 6 and 7, in both stacks: `run`, `rehearse`, `rescore` and `replay` each re-execute under `taskset -c 6,7`, so a re-score and a diagnostic are measured in the same conditions as the score they are compared with), then the layer runs the runner over the visible samples and the held-out pairs. Anything thrown while scoring, including the making of the scratch folder, is a void (`environment`), and a scratch folder that cannot be removed afterwards never loses the result: a score keeps `explore.cleanupFailed` with the reason, a void says it in its own reason. Everything that is the harness's failure is a void (`environment`): the layer halting or failing, a pair the runner
refuses, a case missing from the record, a launch that failed, a case whose peak memory was not read, a build box that never started the build, a build killed from outside. The build runs under a marker: the command
prints a fixed line to standard error and then `exec`s `build.sh`; no marker means `nsenter`, `bwrap` or the seed step failed, so it is a void; a timeout and a marked non-zero exit are submissions. A host fault
*during* a marked build (a full disk, for example) is a non-zero exit of `build.sh` and still reads as a submission: the code cannot tell the two apart; the kept tail is how a person can.

## The quick run's code on `main` (exploratory)

**What it is.** The owner's two-day quick run of 2026-11-05: bare C# (.NET 10) and Node/TypeScript sessions on six daemon and web tasks (04 web, 05 daemon, 06 web, 07 daemon, 08 web, 10 daemon), scored by the visible samples and a
held-out set, with real costs. The results are **exploratory**: the specs are ungated drafts, the held-out sets were written quickly and not validated against two references or mutants, there is no oracle and no calibration
(`explore.json` `quick.limits` are fixed), and nothing in them is the language comparison's verdict. The series of the parent plan stays paused. The code lives in `spikes/lang-explore/` (the harness) and
`spikes/lang-bench/runner/` (the runner above); the notes generated from the olympiad records are `research/lang-v2/EXPLORE_*`.

**Where the private material lives (never in git).** `~/rsd/lang-v2/explore/heldout-NN/` (`cases/` with `hNNN.in`, `hNNN.out`, `cases.txt`, an optional `traps.txt`, the generator), `~/rsd/lang-v2/explore/refs-NN/` (the
reference solutions), `~/rsd/lang-v2/test-design/NN.md` (the private test notes), and the series folders under `~/rsd/lang-v2/explore/` (the records, the ledgers, `score.json` with the evidence of rejects). D41 holds: no held-out
scenario, expected output, reference line or trap name enters git. `explore.mjs leakcheck` reads `heldout-NN/` and `refs-NN/` of the quick tasks as it reads the olympiad tasks' folders; a held-out line that the committed runner,
templates, FORMAT examples, hello programs or rehearsal programs also hold, **as `origin/main` holds them** (never as the branch under scan does: a line committed on the branch cannot exempt itself, the runner directory included), is FORMAT grammar or boilerplate by construction, and is a collision by that rule (quick tasks only), not by a list of lines. A separate line-match scan
outside git runs before any diff leaves the machine.

**The person-reviewed list of `leakcheck`.** Some private lines are generic text that no rule can tell from a leak without a person: a FORMAT grammar line, Python boilerplate, a public URL, a code idiom, a number sequence. A person who
has read such a match can put it on a list: `~/rsd/lang-v2/explore/leak-reviewed.json` (`lib/leak-reviewed.mjs`), which lives outside git beside the held-out folders. A person's reading is the only way in: no rule, pattern, length or
location adds to it, and no tool of the check does. The file is `{ "entries": [{ "sha256", "kind": "line" | "shingle", "reason", "reviewedAt", "by" }] }`: a hash and the words of a reason, never a text. A field beyond those five is refused,
so the list cannot grow into a copy of the private side. The hash is the sha256 of the normal form the tiers match (for a line `normLine`'s result, for a phrase its six normalised words); `reviewHash` is that function. Before the
needles and keys of any tier are built (substring, exact line, window, chunk, shingle), `leakMaterial` drops a private line or phrase whose hash is on the list. A window that holds a reviewed line is still a key if any other line of it is
neither reviewed nor FORMAT grammar; a window of only such lines is dropped. A new text, or the same text with one word changed, is still found, and a line committed on the branch under `test/hello` is still found unless it is on the list
(the grammar rule reads `origin/main`, the list is read from the file, and neither reads the branch). An absent file exempts nothing and is not an error; one that cannot be read or understood (not JSON, a bad hash or kind, a time that is
not a UTC instant, a repeated entry, an extra field) is a refusal, exit 4, never an empty list. Every run with an exemption says `N private line(s) and M phrase(s) exempt by a person's reading` in its result line, success or failure, and
never what they were. The file is written from the private findings by a helper that sits outside git too (`~/rsd/lang-v2/briefs/leak-review-write.mjs`), which keeps the entries already there and prints counts only.

**`explore.mjs rescore --task NN --series DIR --rid R [--wait S]`** (free). It re-classifies one session that ended as `void: contaminated` from its own evidence under the quick run's rule (a relative walk in a directory the scan
lost is a low hit, not contamination) and, only when the evidence now reads as data, scores its kept tree. It refuses every other class by name, a session that is not `ran`, one without a workspace, a cell that already holds another
data session, and a series whose lock a live run holds. It is append only: the run-map entry keeps every field and gains `reclassifiedFrom: [{ class, data, reason, at }]`, the record carries it, the ledger gets an `event` line (no charge,
so the pause point's spend is unchanged), and a scoring failure leaves every file as it was (exit 3). `rescore` and `replay` can be stopped (SIGINT, SIGTERM): `rescore` ends its wait for the series lock at once and, when the signal comes during the scoring, ends before any write (exit 130 or 143, nothing changed); `replay` ends between two runs, removes its `work-*` folder and writes no file, partial or whole (a signal after the last run leaves the complete file).

**`run` finishes a pair that stopped between `scored` and its records.** A run that ended after a session was marked `scored` but before its `record.json` (or before the pair's record) used to leave a pair that `run` refused as already run and `rescore` refused as scored. Now the next `run --pair k` finishes it by itself, free (`lib/finish.mjs`): it writes the missing records from the kept `score/score.json` and the session's evidence by the series' own code, then the pair record, and appends a ledger `event` line (`finished after interruption`) for each record as it is written (with the `rid`, or the `pair` when only its record was lacking), so a finish that stops after the first of two records has still logged the first. No session is launched, nothing is scored and nothing is charged: every reader of the ledger skips event lines, and the plan's spend is unchanged (tested). It acts only on a pair with nothing open; a pair with ANY scored session that kept no score (checked for every scored session, also one whose record is there, because the pair is finalized from the scores and a missing one would replace the record's `explore` section with "not scored"), a moved pin, a complete pair, a void and a `--rerun` request are refused as before.

**Other seams of the quick run.** `--accept-noisy <why>` starts at once, flagged `started not quiet`, instead of waiting out the quiet window first. A rehearsal series (`r-...`) never counts toward the plan's spend. Task 01 may have
a second series. The scan of a session directory bounds the files outside the evidence paths (64 MiB each, 1 GiB in all; the evidence paths are always read) because one session wrote 9.6 GB of load-test output into its `/tmp`.
The box is unchanged: `/home`, `/mnt`, `/run` and `/var/tmp` stay empty tmpfs, the network namespace is its own and the broker the only way out; the quick run adds the pinned .NET SDK read-only and hides the distribution's dotnet.

**The comments of `runner/` name no plan item.** The whole folder is copied into the agents' workspaces, so a comment says what the code does and an id of the plan, a decision or a review finding does not stand in it
(`quick-agent-visible.test.mjs` reads every agent-visible runner, template and olympiad-runner file for such an id). The reasons that were behind the old ids: the program's environment is the runner's own plus the documented per-stack
variables (decision D15, rule V11); a runtime's own out-of-memory abort is a memory-limit verdict like a kernel kill (D15's OP-K, plan change 5.1, D56); the program is found through its cgroup and never through the launcher's process
group (D38, findings B1 and B4); the launch status is the layer's `--json-status-fd` (B5, B6). The olympiad runner (`lang-explore/runner/run_samples.py`) holds no such id; it does say "the exploratory pilot" and describes the
harness's held-out mode, and it is left as it was because the olympiad workspaces are pinned with it.

## The execution layer (E1.S8b)

`spikes/lang-bench/lib/exec-layer/` starts a program's `run.sh` for the runner (S8a part ii-a) in a bwrap box with D15's environment and `de_DE.UTF-8`, inside a delegated systemd user scope
that holds only the program, in a loopback-only network namespace it shares with the driver. Plan: `todo/PLAN_lang_comparison_v2_E1.md` lines 715–972; parent D43, D51.

**The shape as built.** One run is one runner process and one holder. `runCases` (run.mjs): the startup sweep, then the run folder (owned `…-lrun-<id>`, its claim the run marker), the netns holder
(`netns.mjs`: `unshare -rn`, `lo` up, then `exec -a <owned name> sleep <bound>`), the case placeholders (`case-view.mjs`), the launch check and the memory certification (`certify.mjs`), the driver
`nsenter -t <holder> -U -n --preserve-credentials python3 -I run_samples.py … --no-build --cgroup-of <scope>/prog --case-view <run>/views/{case}` outside every scope, and one cleanup
(`cleanup.mjs`) whatever happened. The launch prefix (`prefix.mjs`) is `systemd-run --user --scope --quiet --unit=<owned>-{case} -p Delegate=yes -p OOMPolicy=continue -p MemorySwapMax=0 scope-entry.sh
<out>/{case} <memory.max> <holder seconds> bwrap --json-status-fd 7 <box> --`. `scope-entry.sh` (POSIX sh, 21 lines) makes `holder/` and `prog/`, parks a bounded `sleep` (the scope holder) in
`holder/`, moves itself into `prog/`, enables `+memory`, writes `memory.max` and `memory.swap.max` = 0 in `prog/`, writes the scope's path and the holder's pid to `<out>`, and execs. The scope holder
keeps the scope alive, so `prog/memory.peak`, `memory.events` and `cgroup.procs` are read after the program's exit; the kill is `prog/cgroup.kill`. A fresh scope serves each case, the
`empty-program` control and the `certify` launch. `box.mjs` is v1's primitives (`tmpfs`, `roBind`, `bind`, `devProc`, `setenvAll`): `--tmpfs /` base, `/usr` with its links, four `/etc` files, the
pinned toolchain trees by full path, `/dev`, `/proc`, a fresh `/tmp`, the case view writable, and the OWN-CGROUP VIEW (`--unshare-cgroup`, `prog/` read-only at `/sys/fs/cgroup`; OP-K below); no home, no `/mnt`, no `/run`, no `/var/tmp`, no other cgroup or `/sys` path, no `--unshare-net`, no `--die-with-parent`.
`env.mjs` is the one allow-list; `--clearenv` then exactly those keys; `DOTNET_GCHeapHardLimit` is a key of the .NET stack only (the case's limit in bytes, hexadecimal; `stack` is an option of `openRun`/`runCases`).

**Where the spec met the merged runner (brief A; the runner is unchanged).** (1) The runner is the driver and already sits in the holder's namespaces, so the prefix it gets has no `nsenter`: a second
`nsenter -U` fails with EINVAL; a launch from outside (the certification launch, the tests) takes the same prefix with `nsenter` put in (`programArgv`). (2) bwrap writes `child-pid` before it makes
its mounts, so a missing bind reaches the runner as `exit status 1`, a program verdict; the layer launches an empty program through the prefix before any case and reports `launch failed`, exit 3.
(3) On systemd 259 a `Delegate=yes` scope already has `OOMPolicy=continue`; a plain scope has `stop` and fails with oom-kill. The prefix keeps the property as a pin. (4) The user manager's cgroups
are the user's, so `holder/` and `prog/` can be made without `Delegate=yes`; the property is for systemd, which then leaves the subtree alone.

**Additions to the spec.** `--cap-drop ALL` (inside the holder's user namespace the program is uid 0 with every capability and could take `lo` down); `scope-entry.sh` takes a third argument, the holder's
bound, and the holder closes descriptors 3–9 and redirects 0–2 (else the runner's pipes stay open until it ends); `--quiet` on systemd-run (its line would head every case's recorded stderr);
`/usr/lib/dotnet` hidden (E1.S5); `--no-build`: the layer takes the BUILT tree. **The workspace is a copy-on-write overlay, never a copy (fix batch 1, K1/K2):** the box mounts `--overlay-src <built tree> --overlay-src <placeholder> --tmp-overlay <placeholder>`, so each
launch gets a fresh writable layer over the read-only built tree on a tmpfs inside the box, discarded at exit; the built tree is hashed before and after a run that writes and never changes; no case sees another's files (B7, test 11).
The runner still takes one `--case-view` template: it names a per-case EMPTY placeholder folder on the host (`<run>/views/{case}`), the overlay is mounted at that path, and the placeholder is the overlay's second source, so a
scenario's `.scenario/` fixtures the runner writes into it show in the workspace. **Disk budget:** a few empty folders; the `/tmp` of this host is a tmpfs and no copy lands there. **The cost is memory, by design:** the overlay's
upper layer is a tmpfs in the box, so a program's file writes are CHARGED TO ITS CGROUP (a test: 64 MiB written in the workspace raised `memory.peak` by about 64 MiB), exactly as writes to `/tmp` always were: a program's file writes
count against its memory limit. **Hardening (the Opus review, fix batch 1):** `--unshare-user --disable-userns` (a nested `unshare` fails with ENOSPC, so no writable cgroup2 can be mounted to hide a child in `prog/`; the runner
also reads `prog/` recursively for leftovers), `pids.max` = 512 in `prog/` (a fork bomb stops at the bound; D15's spirit: fixed, recorded, the same for every program), and the scope holder closes EVERY inherited descriptor above 2
(a bash does it: dash can close only 0–9). **Holder bounds:** the scope holder lives T + G of the runner's limits (series defaults T = 10 s, G = 5 s) plus one margin (60 s); a `caseWallMs` shorter than T + G is refused; the netns
holder lives the run ceiling plus the same margin. **The runs parent:** the startup sweep reads the run folder's parent at every run's start, so callers pass a STABLE parent (`stableRunsParent()`: `~/.cache/dew_flow_bench/lang-bench-runs`,
private); the layer refuses a relative, missing, foreign or other-writable one (the shared temp root). **Known, accepted leak:** `/proc/self/mountinfo` in the box shows host paths (the user name in the toolchain and cache bind
sources): paths only, nothing readable.

**Cleanup and the sweep.** `cleanup(run)` never throws and is idempotent: the driver if it still runs; per scope the live evidence, `prog/cgroup.kill`, `holder/cgroup.kill`, `systemctl --user stop` only
for a scope that does not end; the holder (SIGTERM, SIGKILL after 2 s, by pid + start time + owned name); the views; the run folder; a resource it cannot remove stays named in `left` and the folder
stays claimed. `sweepStale` at every run's start retires this user's `lrun` folders whose claim says `gone` and that are older than 10 minutes (`minSweepAgeMs`): their scopes, holder, orphaned driver,
views and folder, by owned name and run marker, never by age alone, never a live, foreign or unmarked one.

**Certification.** A data run (`data: true`) halts `memory-not-certifiable`, exit 3, when `prog/memory.peak` cannot be read after the certification launch; any other run goes on with `memoryCertified: false`.

**The probe** (`exec-probe.mjs`, `lib/exec-probe/`) runs through this layer and records, then exits 3 naming the first failed line: shared netns not the host's, the program in `prog/` and the driver not,
ping over 127.0.0.1, the bind list, the `$HOME` sentinel, `/mnt/c`, `cmd.exe`, the user bus, the empty program's launch time (the pilot's F1 control), `bash -c true` silent, allow-listed keys only, line (f)
(`prog/` and `memory.max`/`memory.swap.max` = 0, a workload, `memory.peak` read above zero after its exit), T-L32 (the culture env, `locale -a`, .NET `de-DE`, Node `de…`, no invariant switch). Record:
`inputs/exec-probe-<date>[-<n>].json`: **a record is never overwritten (T-L53)** — the first of a day has no suffix, a second run is `-2`, a third `-3`; `--out` pointed at an existing file is exit 3 and changes no byte, and the write
is exclusive. The two records of 2026-11-02 (`exec-probe-2026-11-02.json`, layer `2697…`, policy at 100 %; `opk-experiment-2026-11-02.json`, layer `79b85a37…`) stay byte-equal. **T-L43 under D55:** at `MemoryMax` 256 and 512 MiB it records
whether a cgroup is visible in the box, .NET's `TotalAvailableMemoryBytes`, Node's `constrainedMemory()` and V8's heap limit, against `prog/memory.max`, and runs through the runner: the churn program (bounded live data,
4 GiB allocated), `medium-buffers`, `medium-objects` and `objects-70` (the object-graph regression: live ordinary objects at 70 % of L plus 1 GiB of churn), all asserted `PASS` in both stacks (Node `medium-buffers` at 256 MiB is
RECORDED, not asserted: T21), the `mixed` workload (objects 45 % + typed arrays 35 % + churn), RECORDED in both stacks and never asserted, and two over-limit fixtures (`overlimit`, live byte arrays growing to 3 GiB; `overlimit-objects`,
live objects growing to about 4 GiB), asserted `memory limit` with `memory.peak`, `memory.events` and `runtimeAbort` kept. **The peak/L of every over-limit fixture is recorded per stack and limit (J2)**, and a ratio within 10 points of
50 % (`5·|2·peak − L| ≤ L`) is listed under `findings` for the team lead, never tuned around. `inputs/specs.d/memory-policy.json` (`{ policy, decided, by, evidence }`) holds the decision: `null` would be exit 3 `memory-policy-undecided`,
a policy not met is exit 3 `memory-view-mismatch`, and the probe never exits 0 while printing a mismatch. Policies the validator knows: `runtime-view-accepted` (behaviour only), `dotnet-gc-hard-limit-percent:<n>`, `cgroup-view-readonly`,
the first decision's `cgroup-view-own+gchl-limit` (100 %, superseded, kept so its record stays judgeable), and the DECIDED one, `cgroup-view-own+gchl-90+node-old-space-90` (D55): the cgroup visible in the box is only the program's own
(`/proc/self/cgroup` is `0::/`, no child cgroup), .NET's `TotalAvailableMemoryBytes` = ⌊0.9·L⌋, Node's `constrainedMemory()` = L and V8's `heap_size_limit` in [⌊0.9·L⌋, L), and the behaviour above holds. The policy file is part of the
layer's identity, and a data run refuses a policy other than the pinned one (exit 3, `memory-view-mismatch`; `lib/exec-layer/policy.mjs`). **The probe asserts the pinned policy only (Q2):** any other name in the file is exit 3 `memory-view-mismatch`; the other names stay in `verdictFor`, which judges OLD records by their own policy. The record's `layerIdentity` is computed from the tree that ran (exit 3 `layer-identity-mismatch` when it is not the pinned one, before probing), it names the runner's version and `manifestSha256`, `v8Heap[].asserted` is true under the pinned policy, and `probedOn` is stamped only by a probe that exited 0. A data run names its stack, `csharp` or `node` (exit 3 `stack-not-named`), and `heapKeys` throws for any other name.

**OP-K — policy A at 90 % (decided 2026-11-02 by the team lead, parent D15; moved from 100 % to 90 % by D55, the epoch-6 re-review; evidence `inputs/exec-probe-2026-11-02.json`, `inputs/opk-experiment-2026-11-02.json` and the new
`inputs/exec-probe-2026-11-02-2.json`).** Each program sees its own cgroup read-only. Both runtimes are capped at 90 % of the case's limit L, by two per-case keys of the SCORING box that `env.mjs` derives from L (`heapKeys`, one pure function;
never typed by hand, never in the agent box, never in the build box; L is a whole MiB and a missing or non-positive limit throws for both stacks): .NET `DOTNET_GCHeapHardLimit` = ⌊0.9·L⌋ bytes in hexadecimal (`0xe666666` at 256 MiB,
`0x1ccccccc` at 512 MiB; the percent form cannot express it: `DOTNET_GCHeapHardLimitPercent=0x64` is IGNORED by .NET, still 75 %), Node `NODE_OPTIONS=--max-old-space-size=⌊0.9·L / 1 MiB⌋` (230 and 460). A runtime's own out-of-memory abort is
`memory limit` (change 5.1). **Why 90 %:** at 100 % the GC's budget equals the cgroup, so the runtime's own memory (code, stacks, GC bookkeeping, off-heap buffers) has no room and the kernel kills before the GC compacts. **Why the Node flag:**
with the view alone V8 sizes its heap to about 268 MiB whatever the cgroup, so an object graph at 70 % of 512 MiB aborts at 54 %: a false `memory limit` for Node only. **The measured table** (`opk-experiment-2026-11-02.json`, `followUp5`;
toys, three runs per cell, peaks in MiB; settings: A = view + .NET hard limit = L + `--max-old-space-size` = L in MiB, C = the view alone, A@90 and A@85 = A with both keys at that share of L; "oom" = the kernel's kill, so a peak of L):

| workload | stack | limit | A (100 %) | C (view only) | A@90 (the decision) | A@85 |
|---|---|---|---|---|---|---|
| mixed | .NET | 256 | memory limit (oom, 256) | memory limit (199) | PASS (238) | PASS (226) |
| mixed | .NET | 512 | PASS (505) | memory limit (374) | PASS (453) | PASS (427) |
| mixed | Node | 256 and 512 | memory limit (oom) | memory limit (oom) | memory limit (oom) | memory limit (oom) |
| objects-70 | .NET | 256 / 512 | PASS (254 / 508) | PASS (200 / 390) | PASS (233 / 454) | PASS (225 / 427) |
| objects-70 | Node | 256 / 512 | PASS (206 / 399) | 256 PASS (206); 512 memory limit (278) | PASS (205 / 399) | PASS (206 / 399) |

90 % is kept as the larger of the two shares that passed .NET's mixed workload at 256 MiB. **A property of the Node runtime, recorded and not tuned around (T21):** Node's typed arrays (`Uint8Array`, `Buffer`) live outside V8's heap, so V8's
heap limit and its GC do not see them: the `mixed` workload ends in the KERNEL's oom_kill in Node at 256 and 512 MiB under every setting it was run with (policy C and A at 100, 90 and 85 %; it was never run with no view), and live typed arrays at
45 % of the limit plus 2 GiB of short-lived ones end in a kernel kill at 256 MiB (Node `medium-buffers`) and pass at 512 MiB. Neither cell is asserted; a reference that meets it is calibration's to find (D58). **The peak/L of the over-limit
fixtures under D55 (J2, `exec-probe-2026-11-02-2.json`):** .NET `overlimit` 0.774 at 256 MiB and 0.889 at 512, .NET `overlimit-objects` 0.865 and 0.843 (its own abort, evidence kept); Node `overlimit` 1.0 and 1.0 (kernel kill), Node
`overlimit-objects` 0.981 and 0.974 (V8's abort); no ratio is within 10 points of 50 %, so there is no finding. The experiment scripts (`opk-experiment*.mjs`) are the record of measurements made BEFORE the view became the layer's box and
before D55 (the "today" policy there is what the box was, and "decided" in them is the 100 % box): they are not re-runnable as written.

**The probe's stop, its end and the run marker (fix batches 4 to 9).** A SIGINT or SIGTERM makes the probe say `exec-probe: stopping — cleaning up N open run(s)` at once, release its record placeholder, end every child it started (`trackChild`: the C# build, the driver, the server; the build box also has `--die-with-parent`) and every open run (scopes, holder, views), and say when it ended. From that moment the layer starts nothing: `launchPrefix`, a new run folder, a build and a program tree refuse (`stopping.mjs`), and `scope-entry.sh` fails when its run folder is gone (it never recreates it). The exit is the signal's own code (130, 143) ONLY for a cleanup AND an owned-folders close that both finished with nothing left; a cleanup that hits its 20 s bound, leaves something, or a close that hangs or rejects is **exit 75** with the runs, scopes, holders and views still there named. **A cleanup that leaves something keeps the run folder and its claim** (`cleanup.mjs`: the `run` step is skipped and the folder is reported as kept), because the next start's sweep finds a run only through its folder. The timeout timer is not unref'd, so a stuck cleanup still reaches 75; a failure of the stop itself is one stderr line and 75. Every refusal inside the probe's `try` is thrown (`Refusal`) so the `finally` closes the owned folders. **The end of a probe — the record is the LAST write (S1, K1–K4).** In order: (1) the **stamp lock** `inputs/.stamp.lock` (`stamp-lock.mjs`). Its content is `<pid> <start time>`, and a second line `committing` once the holder is about to commit (written through the same temporary file and a rename, so a reader sees the old or the new lock, never a partial one) (the start time is `/proc/<pid>/stat` field 22 as `owned.mjs`'s `processProbe` reads it); it is created by `link()` of a COMPLETE temporary file `inputs/.exec-probe-tmp/.stamp.lock.link-<pid>-<start>`, so a reader never sees it empty, and EEXIST means held. A holder is **live** when its pid exists AND its start time is the one in the lock (a reused pid is stale); a holder that cannot be judged is live; a live holder refuses the probe (its record says exit 2, naming the holder's pid and the lock path). **A stale lock — a pid that is gone or reused, or a lock that cannot be parsed — is NEVER removed, moved or taken over by a probe** (D1: every round of an automatic retire opened a new interleaving, so there is none). The probe refuses: its record says exit 2 and `stampError` names the lock path and the dead holder, and stderr says the exact manual step: "a probe that was killed left the stamp lock `<path>` (pid `<p>`, start time `<t>`, now gone). If no probe is running, remove it: `rm <path>`". **The owner accepted this manual step on 2026-11-03**: no automatic retire is planned (one on a kernel lock, `flock`, is the alternative if it is ever wanted). A stale lock whose second line says `committing` (its holder had reached the commit) adds on stderr that the pins MAY be stamped without a record (a SIGKILL between the commit and the record's rename), names the layer pin's `probedOn` (read only then) and says what to do: check `git diff -- spikes/lang-bench/inputs/specs.json spikes/lang-bench/inputs/specs.d/exec-layer.json`; if the pins name a probe with no record, run the probe again (it re-stamps them) or restore them with `git checkout -- <pins>`. **Only the holder ever changes the lock** (`markCommitting`: a temporary file renamed over its OWN lock; `release`: its own lock only), so a check followed by a rename has no other writer to race. The lock's content is compared through `parse` (pid, start time, the `committing` flag), never as a string (a holder's lock written with a trailing newline is still its own). N probes started together against one stale lock: none stamps, every record says 2, the lock is unchanged; N probes with no lock: exactly one stamps (`exec-probe-stamp-many.test.mjs`, real processes). Before its commit a holder checks that the lock is still ITS content (`holdsMine`); if the lock was removed by hand and taken by another probe, the record says exit 2 ("the stamp lock was lost") and nothing is committed. A `take()` whose temporary file cannot be removed after a successful `link()` still holds the lock (the holder's `release` removes the leftover temporary files). When the retry after a vanished lock fails with an error, the refusal says that error, not "held by another probe" (N4). **Every temporary file lives in a `.exec-probe-tmp/` folder BESIDE its target** (same file system, so the rename still works): `inputs/.exec-probe-tmp/` (the lock's link file), `inputs/.exec-probe-tmp/` for BOTH pins (`exec-layer.json.stamp-<pid>-<start>`, `specs.json.stamp-<pid>-<start>` and the `.restore-` files: N1, so `specs.d` never holds a temporary entry and a crash can never leave a stray that makes `specs-record --check` fail; no `.gitignore` entry, a leftover stays visible in `git status`), the lock's committing mark in `inputs/.exec-probe-tmp/.stamp.lock.mark-<pid>-<start>` (its own kind, created with `wx`, never the link file's name, so it can never write through a hard link to the lock; N5), and `<out dir>/.exec-probe-tmp/` (the record's `<record>.record-<pid>-<start>`); the name carries the owner's pid and start time (`tmp-files.mjs`), and a tmp folder is removed when it is empty (`specs.d` must hold no stray folder). Probes share the folder, so another probe's removal can land between a probe's `mkdir` and its `open`: every writer goes through `writeTmp`, which makes the folder again (at most 5 tries) when it vanished. `writeTmp` is also the only writer: it always uses the flag `wx` (an existing name, a planted symlink included, is refused) and refuses a tmp folder that is a symlink, not a directory or not this user's (the sweep's own `whyUnsafeFolder` check), so a refusal is the probe's exit 2 naming the folder (P3); the pins' temporaries come from one helper, `pinTmpPath`, which always uses `inputs/.exec-probe-tmp/` (P4). **At the probe's start** the command sweeps those folders explicitly, in its start sequence AFTER `guardStreams` and the stop handlers are installed (`strays.mjs`): it lists ONLY the `.exec-probe-tmp/` folders beside `inputs/` and the `--out` folder — never `inputs/` itself, `specs.d` or a shared `--out` folder — and removes the files whose owner is dead (the pid is gone, or alive with another start time: pid reuse); a live probe's files and every file outside a tmp folder are never touched. **The sweep never follows a symlink (N2):** each `.exec-probe-tmp` is `lstat`-checked to be a real directory owned by this user, else it is skipped and named on stderr; each removal is `lstat`-checked to be a regular file inside that folder. The folder of the pins and records is `--inputs-dir <folder>` (a seam: the tests never sweep the real `inputs/`). The streams are guarded FIRST (D3): `guardStreams` is the first thing the command does, called from `installStopHandlers` inside `startCommand` (guard, stop handlers, release on exit, then the start sweep), so a test turns red when it is removed. An EPIPE on stdout or stderr (`| head`) is always ignored; after the record is written ANY error on either stream keeps the record's exit code (never an uncaught exception); before the record a non-EPIPE error is the command's failure — one stderr line `exec-probe: stream error: <message>` (best effort, written to the OTHER stream, once), then the stop (the cleanup), then exit 2 — and the listener never rethrows. `release()` removes the lock only when its content is still the probe's own. (2) the stamp PREPARED (`probedOn` and `host` in `specs.d/exec-layer.json` and the re-assembled `specs.json`) in temporary files in a `.exec-probe-tmp/` folder beside each pin, assembled from a staged copy of ONLY the files `assemble` reads; (3) the stamp COMMITTED (two renames; if the second fails the first pin is put back; the outcome names exactly which pin is stamped, and a failed put-back is never called "both stay as they were"); (4) the record written ONCE with its final content to the temporary file `<out dir>/.exec-probe-tmp/<record>.record-<pid>-<start>` and renamed over the placeholder (no cut-off dated record; the writer itself refuses a missing reservation or a placeholder that is not empty, T-L53); (5) only then `onWritten`, the reservation is taken, the report printed, and the **lock is released LAST**; the exit is the record's code. **Which failure is what:** every failure of (1) to (3) — a lock that is held, unparsable, or cannot be created (EACCES, ENOSPC), a prepare or a commit that fails — is a RECORD that says exit 2 with `stampError` naming the lock path or the pin and the error (the file never says 0 when the command says 2); the holder's pid and the lock path are on stderr. A failure of (4) is stderr lines and exit 2 with no record: both pins that are stamped are put back from their saved contents (also after a failed commit: the prepared stamp and the pins still stamped are kept for this), the placeholder is released, and every pin that could not be put back is named with "it points at a probe with NO record"; the messages come from the real state. A lock release that fails is ONE stderr line and the exit stays the record's code. A SIGINT or SIGTERM after the record is written only closes the owned folders and exits with the record's code (a close that hangs or fails is one stderr line naming what may be left); a real SIGTERM after the record is tested through the command's own wiring (`wiring.mjs`: `endArguments`, `stopArguments`, `installStopHandlers`). A cleanup of a run already in flight is not started twice (the second caller shares the first one's report). A run stays named in the layer's registry until its cleanup finished clean, so the exit-75 message of a hung cleanup names its id, scope units and folder (S2); the run folder is reported as kept only when it still exists. A child's owned marker (`…-probedriver-<id>`) is made by the layer's naming helper `childMarkerOf`, never by `replace` on the holder's name. The token guard of the tests finds every form of a token binding (S3), and the suite start's sweep is shown to retire what a killed probe test really leaves (a `private-<id>` root with runs inside).
One token only: every test and tool uses the registered token `bench`; the tests tell their units apart by the ids of their runs, in a private temp root and runs parent per test, and the suite start calls the conventions' `sweepLeftovers({ token: 'bench' })`.

**A note for CP2 — a loaded data host can print a false `fork:` line.** The `fork` flag rests on the 100 ms rule above: with the host's cores all busy, about 1 case in 160 can show a forked short tool as having stayed 100 ms. The line goes to the blind adjudication like every flag; it is not a verdict.

**A note for CP2 — the holder's ready limit under load.** The netns holder must be ready within 5000 ms. It failed once on a loaded host: `inputs/exec-probe-2026-11-02-3.json` (layer `ef97f03d…`) ended exit 3 `memory-view-unobserved` because the holder was not ready in time while other work ran; it is kept as evidence and never overwritten (T-L53). The same layer passed on a quiet host (`-4`, exit 0). A data run on a loaded host can meet the same limit: CP2 should say whether 5000 ms is enough for the series host under its worst expected load. The records `-2` (layer `52c80acb…`), `-4`, `-5` (`71e8b99a…`) and `-6` (`35fb2317…`) stay as the earlier passing records of earlier layers. `-7` (layer `730f8fee…`, runner 0.2.4) is the record of the layer before fix batch 5. `inputs/exec-probe-2026-11-03.json` (layer `aa5a4471…`, runner 0.2.4) is the record of the layer before fix batch 6. `inputs/exec-probe-2026-11-03-2.json` (layer `3e120252…`, runner 0.2.4) is the record of the layer before fix batch 7. **The record of the FINAL layer is `inputs/exec-probe-2026-11-03-3.json` (layer `2cba87c276de32401d451be1ff8585a588a9ec705f71f103511b93b97fa1c6ad`, runner 0.2.4, exit 0).**

**The identity and its invalidation rule.** `identity.mjs` hashes every file of `lib/exec-layer/` (`scope-entry.sh` included), the memory policy file AND every module outside the layer that the layer imports, found by walking the static imports (v1's `bwrap.mjs` builds the box's argv, `run.mjs` launches every child, `find-on-path.mjs`, `result.mjs`, `preflight-box.mjs`; a test finds an unlisted import); `runCases` puts the live `treeSha256` in its result and a DATA run refuses (exit 3, `layer-identity-mismatch`) when it differs from the pinned identity; `pin-exec-layer.mjs` writes it to `inputs/specs.d/exec-layer.json`
(`treeSha256`, and `probedOn` and `host` after a passing probe) and `specs-record.mjs` assembles `inputs/specs.json`. A change to ANY file of the tree, of a module it imports or of `inputs/specs.d/memory-policy.json` (parent §6.1: the series' one memory policy) changes the identity and re-dates every gate and
limit that ran on the layer (parent §6.1: E1's D40 gate, E2's validation and calibration, E3's scoring). The probe's files are outside the tree, so a probe change does not.

**Never** run `dotnet build` (or any SDK command) outside the box with `DOTNET_ROOT` on the sealed SDK: it writes a `metadata` folder into the SDK's root and breaks the toolchain store's seal and tree
hash (2026-11-02). The tests and the probe build their C# programs inside the box, where the SDK is read-only.

## FORMAT v2 (E1.S9)

**Purpose.** The bench's part of the story: the code that checks the task format document (`todo/tasks/FORMAT.md` of the task repository), a record that says
which version and bytes are A's, and the maintainers' document. The format text itself is the team lead's; nothing here edits it. Built 2026-11-04 against the
draft at commit `0a95d31` of the task repository (`Format version: 2`, sha256 `27e79c06…695f`); since fix batch 2 the record named the draft at `593fb7a` (layout only: F-TIMING's fence outside the list in 2.5, a wrapped line of 5.3 that no longer starts with a tag-like span; sha256 `a1990215…c161`), and since fix batch 4 it names the draft at `2683747` (layout only: 3.4's two-backtick example sits in a three-backtick span, so the code spans after it pair as written; sha256 `6a526107…81b3`).

**Common reading** (`lib/format-structure.mjs`). FORMAT is read as raw bytes: UTF-8, LF only; a carriage return anywhere or a byte order mark is a format error
(exit 4) that names the first line; nothing is normalised and the sha256 is over the raw bytes. A heading is a line that starts with `## ` or `### ` at column 0 outside a
fenced code block, so the `# 1.` comments and lookalike headings of a scenario are never headings. A fence opens with three or more backticks or tildes (at column 0; inside a list item
at the item's content column and up to three more) and closes with a line of the same character, at least as long, indented at most three columns, nothing else on it (CommonMark's
closing rule); a fence in a list item ends where the item ends. The structure is exact: `## 1.` to `## 7.` once each, ascending; under `## k`, `### k.1`, `k.2` … with no gap or
duplicate; anything else is exit 4 naming the heading and the line. **The reader must not see another document than a CommonMark renderer** (fix batch 1, after the Opus review's
fence and HTML tricks): in either file, in both tools, these lines are exit 4 naming the line: a backtick fence opener with a backtick in its info string; a fence line with 1 to 3 spaces
of indent outside a list item (a fence inside a list item is allowed: the real draft has one); an HTML block start at columns 0 to 3 outside a fence (`<!--`, `<?`, `<!X`, `<![CDATA[`,
the script/pre/style/textarea kinds, the block-level tag names, and a complete tag alone on a line where no paragraph is open); an ATX heading with a tab after the `#`s, with 1 to 3 leading
spaces or with nothing after the `#`s; a setext heading (a paragraph line, then a line of only `=` or only `-` indented at most three spaces; a `---` after a blank line stays a thematic break);
an ATX heading or a fence opener inside a block quote or after a list marker on the same line (fix batch 1b: a heading the reader does not see but a renderer shows is the same spoof as the fence trick).
Not looked at by the reader (listed in `inputs/FORMAT-MAINTENANCE.md`): character references and link labels that spell a heading.

**What the grammar defends.** Agents read FORMAT and `GRAMMAR.md` as raw bytes; the byte comparison protects the bytes of the protected parts (the exceptions are listed here: the header rows, the class-table rows after the last row, the changelog's appended text, the free rows of 2.1), and it does not judge what an unprotected part says. A free part can still contradict protected text in plain words (`Notwithstanding 2.6, any normalisation is allowed for olympiad tasks.` in 2.5 passes), which remains a point for human review of any amendment. The restricted grammar and the inline-HTML rule defend a human reviewer who reads a RENDERED page against a misleading amendment; they were checked with markdown-it in CommonMark mode and are not a proof about every renderer. A blank line before an appended changelog entry makes that list "loose" when rendered (spacing only; the words do not change).

Direct evidence on the real draft (fix batch 5): three changes to every line (delete the line, append ` zz` to it, insert an empty line after it), 3,435 candidates; the only passes in protected parts are the `Gate verdict` value, the 14 free 2.1 rows, and one extra line feed at the end of the file. `test/format-evidence.test.mjs` runs a sample of this (every 53rd line and the lines that matter, about 3 seconds); the full sweep takes about two minutes and is recorded here only.

Fix batch 5: the code-span scan (`closerOf`, `runEnd`) is a loop, not a recursion (a paragraph of 20,000 backtick runs no longer overflows the stack); an ordered item whose number is not 1 directly after a paragraph line or a bullet item is refused; a table row that holds `\\|` is refused (whole file).

**The restricted grammar** (`lib/format-grammar.mjs`, fix batch 2). Patching the reader one form at a time cannot close the class (a deletion can turn a literal inside a fence into a heading; a reference
definition acts at a distance), so the guarantee is by construction. Every line of every unprotected part of the CANDIDATE (and, as a sanity check, of the frozen file: a failure there is exit 4) is a blank
line, a heading in the exact structure form (no closing `#`s; an added heading may not carry the title of a protected heading), a paragraph line (no block starter at column 0; `**` and one or two backticks
may start a line; a digit marker needs a space after it), a list item (`- ` or one to three digits and `. `, text obeying the paragraph rule, continuation lines indented by exactly the marker width, no nested
list, no fence or heading inside), a table row, or a fenced block (opener three backticks and lower-case letters, closer three backticks, both in the part, no other fence marker line inside). Two whole-file
rules, protected parts too, outside fences: no link reference definition (at any indent, an escaped bracket or a footnote form included) and no line whose first non-space character is `<`. Fix batch 4 adds a third: no
raw inline HTML, a `<` followed by a letter, `/`, `!` or `?` outside a code span and not written `\<` (the fourth review's finding: `Read on <details>` in an unprotected part leaves the rest of a rendered page inside a closed element). Code spans follow CommonMark's equal-run rule over a PARAGRAPH (not across a heading, a list item, a quote, a thematic break or a fence); a table is scanned in two readings, as text of a paragraph and as rows cell by cell, and a tag left outside a span by either is refused. Also in batch 4: a tab after an ordered marker or a dash is a list marker form the grammar refuses; the look-alike check takes its titles from every heading inside a protected part and runs on headings appended to the changelog; the changelog must be one whole `common` row in the class table (`--self` and `--frozen`). Fix batch 3: the
text appended to the changelog must start after a blank line and obey the same line forms; `--self` also reports the grammar (exit 1); the refusal arrays are built without `push`. The argument: every protected part starts at a column-0 heading, where CommonMark
starts a fresh block; no unprotected construct stays open across a part boundary; nothing acts at a distance. Refusals are exit 1 naming the part, its class, the line and the form. The kinds cell of a changed
or added free 2.1 row holds only `daemon`, `web`, `and`, `or`, commas and the words the frozen free rows already use (read from the frozen file), as plain ASCII. The `Gate verdict` also accepts the spec-header
form (a back-quoted word, optionally ` · yyyy-mm-dd`). Exit codes of both tools: 0 success, 1 a refusal (one line each: heading, line, rule), 4 usage or a format error.

| File | Role |
|---|---|
| `tools/grammar-extract.mjs` | `grammar-extract.mjs <FORMAT.md> <out>`: the header `# Scenario and sample grammar (from FORMAT.md v<version>, sha256 <hex>)`, one empty line, then the raw bytes from the `## 3.` line to the byte before `## 6.`. Deterministic. Written to a temporary file in the output's folder and linked into place (`wx`, then `link`): an existing output is exit 4 and never overwritten, and no temporary file stays. |
| `tools/format-classes.mjs` | T-L56. `--self <FORMAT>` and `--frozen <A> --candidate <B> [--record <format.json>]`. No `--reference-sha` option: the frozen file must be the file whose sha256 is the record's `A.sha256` (default record `inputs/specs.d/format.json`), else exit 1 "the frozen reference is not A's recorded FORMAT". |
| `lib/format-structure.mjs`, `lib/format-class-table.mjs`, `lib/format-grammar.mjs`, `lib/format-compare.mjs` | the reading above; the class table, its rules and the parts; the comparison. `checkFrozenCandidate({ frozen, candidate, reference })` is the exported function the tests call with an explicit reference. |
| `test/grammar-extract.test.mjs`, `test/format-classes.test.mjs`, `test/format-classes-frozen.test.mjs`, `test/format-classes-review.test.mjs`, `test/format-reading.test.mjs`, `test/format-grammar.test.mjs`, `test/format-bounds.test.mjs` | the tests; `test/support/format-fixtures.mjs` (a synthetic FORMAT with the shape of the real one, and the real-draft helper) and `test/support/format-bounds-readers.mjs` (the strict readers) |
| `inputs/specs.d/format.json` | the record `{ "A": { version, sha256, gate: null, grammarSha256, tasks } }`; `specs-record.mjs` assembles it into `inputs/specs.json` |
| `inputs/FORMAT-MAINTENANCE.md` | the maintainers' document: the conformance list, the tools, the class rule, the amendments (written from FORMAT v1's sections 7 to 9, public on the task repository's `main`) |

**The class table (`--self`).** The table directly after the `**Section classes.**` paragraph of the preamble, rows `| <heading without ##/###> | <class> |`. Every row names
an existing heading by its whole text (a heading inside a fence is none), the class is one of `common`, `olympiad`, `daemon`, `web`, `daemon-web`, every `##` section has
exactly one row or, for a mixed section, each of its `###` has exactly one row and the section none; a heading with two rows, a malformed row and a paragraph or table that cannot
be found are refused (the last is exit 4, the others exit 1). The lead text of a mixed section is `common` by construction.

**The parts and the comparison (`--frozen`).** The protected set (`common`, `olympiad`) comes from the FROZEN file's table only. A section classed as a whole is one part, from its
`##` line to the byte before the next `##` (its `###` belong to it); a mixed section is its lead text (class `common`) and each `###` (to the byte before the next `###` or `##`); the
preamble is byte 0 to the byte before `## 1.`. Boundary blank lines belong to the part before them, so no byte is in two parts or in none. A frozen part is found in the candidate
by its heading NUMBER and compared as raw bytes, except: the preamble's three header rows (whole lines; each must exist once, on the line the frozen file has it, with a value
that matches a strict pattern: `Format version: <digits>[.<digits>]`; `Gate verdict:` one to four lower-case words of `[a-z0-9_.-]`, at most 80 characters, no colon; `Freeze tag:` `none yet` or
`spec-frozen[-<capital>]-<yyyymmdd>`) and class-table rows after the frozen last row (a row of an unprotected part may change or go, and is matched to the frozen row by the number of
the heading it names, so an unprotected heading can be renamed with its row; a protected row must stay byte for byte, in place); section 7 must START with the frozen bytes; 2.1's
fixed-sentence table is compared row by row, matched by the F-id. **2.1's rows are only the contiguous `|` block under the `| Id |` header row and its separator**; any other `| F-` line is plain
text of its part and compared as such. An F-id with more than one row in either file is refused (exit 1, naming the id and both lines); the protected rows keep the frozen order; a row may differ
only when its sentence is defined only in unprotected parts, and a free or added row whose kinds cell names `all` or `olympiad` is refused. **A sentence is defined where its label stands** (`F-ID` at
the start of a line outside fences) and, when it has no label (F-ERRORS, F-TIMING, F-BYTES-O-N), where the paragraph that introduces a fenced block names it; checked on the draft, the 14 daemon and web
sentences are free (F-PORT, F-READY, F-STOP, F-EOF, F-TIMING, F-STDOUT-D, F-BYTES-D, F-BYTES-W, F-JSON, F-LIMITS-D, F-LOAD, F-DEADLINES, F-RUN-D, F-SAMPLES-D) and the other 17 are protected (a test
pins the split). Refusals name the heading and the FROZEN class (`none` for a part the frozen file did not have): protected bytes differ (with the first differing candidate line; for a retitled
heading `heading text changed`), a protected class row relabelled or gone, a protected part missing, a part added as `common` or `olympiad` (the lead text of a section that was one unprotected whole
is the one exception, and only when it is the `##` line and blank lines, or the bytes the frozen section had before its first subsection), a heading number that changed (also when the part moved to
another section; a `###` inserted inside a protected mixed section moves the numbers after it), a candidate whose own class table breaks the rules. An append at the end of a mixed section with
the next free number, classed `daemon`, `web` or `daemon-web`, passes; so does turning a `daemon-web` whole section into subsections of those classes. Format errors in either file are exit 4; a frozen
file that breaks its own class rules is exit 4. `grammar-extract.mjs` ignores only ENOENT when it removes its temporary file; any other error is one line on stderr and the exit code stays the write's.

**The bounds (T-L58).** `format-bounds.test.mjs` reads each figure through a strict reader: a regular expression that tolerates whitespace, wrapping, code spans and quoting variants and
still demands exactly ONE match (zero or two is the reader's error). F-STDERR (2.2) and 4.6's `writes more than 256 MiB to standard error` against `STDERR_KILL_BYTES` in `runner/rs_exec.py`; F-ENV's
`TZ` against the base environment of `lib/exec-layer/env.mjs`; 4.2's one settable name (`TZ`) against the layer's real `boxEnv`: every key of the layer's own exports (`ALLOW_LIST`, `CASE_KEYS`),
in three spellings, is refused except `TZ`, which is accepted (the test derives the keys, it does not retype them; the layer is not changed); F-PROCS's 512 against `pids.max` in
`lib/exec-layer/scope-entry.sh`. For every mapping there is a case with the value changed in FORMAT (red), one with it changed in the source (red), and cases with the line removed or
duplicated on each side (the reader's error). The source readers also fail when the value is assigned or written a second time anywhere in the file (`STDERR_KILL_BYTES *= 2`, an annotated or tuple
assignment, a `tee`, `printf` or `>>` to `pids.max`; comments are not counted); what another module does to the value cannot be seen from the one file. The `boxEnv` side is behaviour, so its source-side cases are stand-ins for `boxEnv` that accept a base key, refuse `TZ` or accept a heap key.

**The real draft in tests.** The positive cases and `--self` also run on the real draft when `LANG_V2_TASK_REPO` names the task repository's checkout; they first check that the checkout's
`todo/tasks/FORMAT.md` has the sha256 of the record's `A.sha256` and fail otherwise. Without the variable they skip with the reason; under `BENCH_REQUIRE_SANDBOX=1` they throw. Refusal cases use the
synthetic fixture. **The gated record (2026-11-04):** FORMAT v2 on the task repository's `main` (`40e678fe`), sha256 `d293bafc…`, grammar sha256 `95f3c26e…`, gate `inputs/gates/FORMAT-v2.md`. `inputs/specs.d/format.json` was bootstrapped first on drafts (three times: at `0a95d31`, at `593fb7a` and at `2683747`, each with its own commit) in this order: the extractor on the draft (`grammarSha256`), the record, `specs-record.mjs`, the checks.

**Not done here.** The format's own document gate, the freeze, and `runner/GRAMMAR.md` in the workspaces (the extractor makes the text; E1.S3 places it). The record's `gate` stays `null` until
the gate passes.

## The conformance check (E1.S8)

Built 2026-11-04 (`todo/PLAN_lang_comparison_v2_E1.md` § E1.S8). One command reads a spec, its visible samples and FORMAT.md and names every deviation:
`node spikes/lang-bench/conformance.mjs --repo <task repository> --task NN-slug --format <FORMAT.md> [--needles <file>] [--record <format.json>]`. The spec is `<repo>/todo/tasks/<task>.md` and its samples are
`<repo>/tasks/<task>/samples/`. One line per check on standard output, `ok <check>` or `FAIL <check> — <what, where>` (one line per finding). **Exit** 0 every check ok; 1 a deviation; 4 the
checker cannot run, and nothing is passed: a FORMAT that is not in the record (`inputs/specs.d/format.json`, read as the FORMAT tools read it, `--record` overriding), not Format version 2, or whose
2.1 table or `text` blocks do not fit what the reader names (a row the code names missing, a part count other than one or F-HIDDEN's two, a labelled block with no row, overlapping variants, a kinds cell or a
kind label it cannot read, a slot phrase no longer in 2.4 or 2.5); a runner that exits 4 and names no problem; a usage error, a missing or unresolvable `--task`, spec or samples folder, a FORMAT it cannot read or that lacks a sentence, a placement or a slot it
expects, `python3` or the runner missing, the runner not finished in 60 s, or ended in a way other than 0 or 4. A spec the block reader refuses (CR, byte order mark, a form CommonMark reads otherwise)
is one `FAIL file` line and exit 1. In group B, check each olympiad spec of A under A's FORMAT and under B's (`An_olympiad_spec_conforms_under_A_and_B_FORMAT`).

**FORMAT is the source.** `lib/conformance-sentences.mjs` reads 2.1's table (id, kinds, where) and takes each sentence's text from the `text` block of section 2 that follows its label: the
paragraph the block follows names the id (it starts with it, or the last id it names is the one; `F-INVALID (olympiad):` gives the kinds of a variant; two blocks under one label are the parts
of one sentence, F-HIDDEN). `lib/conformance-facts.mjs` reads the file-name pattern (1.1), the header rows, the verdict words and the kind phrases (1.2), the eight headings of each kind (1.4),
the series T and G (2.5) and the keyword list (4.5). Nothing is typed again, but the slots' allowed values (`SLOT_PATTERNS`, from 2.1 to 2.5) and the forms of 2.1's `Where` column are the
checker's own, and a FORMAT that uses one it does not know is refused (exit 4). Sentences are compared after whitespace is collapsed; a sentence not found whole whose first or last words stand
in the spec is `altered`, with the line. Optional sentences (F-PORT, F-EOF, F-TIMING, F-JSON, F-ERRORS) are checked where they stand; F-INVALID xor F-ERRORS, F-TIMING once per overridden value,
F-STOP's `{which}` by F-EOF, `{k}` by the samples shown, `{ref}` by the ids that exist; F-BYTES-O-N is refused for this series.

| Check | What it reads |
|---|---|
| `file`, `header`, `sections` | the name, the title, the five rows and their later forms, the eight headings, section 8 against the `Amendments` row |
| `r-ids` | `**R<n>.**` unique and exactly R1 … Rn, ascending before the freeze, F-REQ's `{n}`, the ids amendments name, a table or a fence outside every requirement span |
| `sentences`, `T/G` | the fixed sentences with their places and slots; T and G against every visible scenario's `start … within` and first `wait-exit within` |
| `samples`, `sample-forms`, `sample-env` | the folder against section 6; the olympiad cells (FORMAT 3.4's code-span forms) or the daemon and web blocks against the bytes; the runner's parse; CP1's F11 forms; the `env` keys against `ALLOW_LIST` and `CASE_KEYS` of `lib/exec-layer/env.mjs` |
| `no-needle` | E1.S1's rules (`findVisibleRules`, the known-task file `inputs/needles-lang.json`), the stack words and the author's literals (`--needles`, one per line, the leak-token reader; a hit is named by its number) over the spec and the sample files; `⏎` outside section 6; a paragraph with eight or more script keywords in code spans (the reading block) |

**The runner** is `spikes/lang-bench/runner/run_samples.py`, found from the checker's own place (never from `--repo`), run as `python3 -I -B <runner> --kind <daemon|web> --parse-only <samples>` through
v1's `runBounded` (60 s, TMPDIR a folder of its own, removed after); its `(file, line, message)` lines are quoted in the `samples` findings. `runBounded` already ends the whole process tree at its
ceiling (`A_runner_that_hangs_…` shows a `setsid` child of the runner gone when the checker returns), so it is not widened. An olympiad task needs neither python nor the runner.

**Stack words.** `inputs/stack-words-lang.json` (public, schema 1: `words`, `substrings`, `caseSensitive`) lists the implementation languages, runtimes, frameworks, build tools and flags of the series; check 10 always
reads it with its own matcher (`lib/conformance-needles.mjs`: case-insensitive, `Node` excepted, over the whitespace-collapsed text, so a needle wrapped over a line is found; the known-task needles and the
author's `--needles` literals are matched the same way) and names a hit by the word. Neither the real FORMAT's fixed sentences nor the real spec 01 holds one of them. The shared push rules `lib/push-*.mjs` are unchanged.

**Batch 3 (the review's fixes, 2026-11-04).** Fail-closed: C1 (silent runner exit 4), C2 (the record and the reader's structure), C11 (slot phrases read from FORMAT). Structure: C5 (section 8), C6 (a table found
by its delimiter row), C7 (ids in any form, every `R<n>` in range, a blank line above each opening), C8 (a sentence the row does not list for the kind), C9 (F-PORT), C10 (every F-TIMING, paired), C12 (olympiad
section 6), C15 (heading levels), C16 (triggers of the samples), C19 (a sentence in the preamble), C20 (real dates, the tag, `Withdrawn by amendment`, `{k}`). Text: C3, C4, C13, C14, C17, C18. C22: exit 4 with a
sentence for an unknown kind label and for an unreadable sample. Not built: a fence info string other than `text` in a spec (FORMAT 3.4 gives none).

**Not covered, and why.** The body-forms rule is coarse (a JSON comparison with no F-JSON anywhere). A normative sentence outside a span
that is not a fixed sentence is for the reviewer. Shape: every function of these files and of the FORMAT readers and tools (`lib/format-*.mjs`, `tools/*.mjs`) has under 50 lines and a complexity of at most 4, measured by v1's scanner (`spikes/minigit-bench/test/code-shape.mjs`) and held by `test/code-shape-lang.test.mjs`, with no exemption; the E1.S9 files were split in batch 2 without a change in behaviour (see `module_tests.md`).

**Files.** `conformance.mjs` (command), `lib/conformance.mjs` (the run), `lib/conformance-{blocks,spec,facts,sentences,locate,shown,runner}.mjs` (readers), `lib/conformance-check-{structure,sentences,samples,text}.mjs`
(the checks); `lib/format-structure.mjs` takes a `structure` rule (a spec has eight sections, FORMAT seven) and `lib/format-compare.mjs` exports `tableOf`. Tests: `test/conformance*.test.mjs`, the synthetic
repositories and the FORMAT stand-in in `test/fixtures/conformance/`, `test/support/conformance-fixtures.mjs`. Scenarios: [module_tests.md](module_tests.md), section *The conformance checker scenarios (E1.S8)*.

## Toolchains and the preflight (E1.S5)

Built 2026-11-01 (`todo/PLAN_lang_comparison_v2_E1.md` § E1.S5). The gate, the references and every later session run on the toolchains below, from
read-only directories under the private store `~/rsd/lang-v2/toolchains/` (outside every git tree; the store root is refused inside a worktree). An unattended
`apt upgrade` cannot move them: v1's SDK pin was moved that way on 2026-10-30.

| Toolchain | Pin | Directory in the store | Official source | Checksum kept in `inputs/stacks-lang.json` |
|---|---|---|---|---|
| .NET SDK (bundles runtime 10.0.14) | 10.0.403 | `dotnet-sdk-10.0.403/` | `dotnet-install.sh` pinned at commit `5147e32300a8e908f5d737c8cff63a76b4b63531` of `dotnet/install-scripts` (`https://raw.githubusercontent.com/dotnet/install-scripts/<commit>/src/dotnet-install.sh`, the file `https://dot.net/v1/dotnet-install.sh` served on 2026-11-01; its sha256 is recorded and checked before it runs, and a moving URL is refused by the loader); the archive it downloads, `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz` | sha512, equal to the hash in `https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json` |
| Node | 26.13.0 | `node-v26.13.0-linux-x64/` | `https://nodejs.org/dist/v26.13.0/node-v26.13.0-linux-x64.tar.xz` | sha256, equal to its line in `https://nodejs.org/dist/v26.13.0/SHASUMS256.txt` |
| TypeScript | 6.0.3 (newest 6.0 patch on npm, read 2026-11-01; 7.0.2 is superseded, parent D6) | `typescript-6.0.3/` (`node_modules/.bin/tsc`) | `npm install --prefix` run by the pinned Node against `https://registry.npmjs.org/` | `dist.integrity` (sha512, base64) of the packument, equal to the integrity in the installed `node_modules/.package-lock.json` |
| Python 3 | 3.14.4, the distribution's | `/usr/bin/python3` (not copied) | the host | sha256 of the real file, `52e0a13e…cb41` |

**Layout and permissions.** The store root is mode 0755 (the installer writes into it); each toolchain directory is sealed `chmod -R a-w` (`dr-xr-xr-x` directories,
`-r--r--r--` or `-r-xr-xr-x` files), so `find <dir> -perm /222 ! -type l` lists nothing. The SDK directory holds `sdk/10.0.403` only and `shared/*/10.0.14` only. Each
directory's tree hash (v1's `treeHash`: type, path, mode without write bits, content hash; never mtimes) is recorded in `inputs/stacks-lang.json` under `treeSha256`.

**Install.** `node spikes/lang-bench/install-toolchains.mjs [--only dotnetSdk,node,typescript] [--record]`. Per toolchain: the official source is read first and must list the
checksum the pin holds (otherwise nothing is downloaded and the refusal names both values); the artefact is downloaded with `curl -fsSL --proto =https` into an owned
folder and checked against the pin (Node sha256; the SDK archive that `dotnet-install.sh --keep-zip` downloaded, sha512; the integrity in npm's lock); the tree is built in
an owned staging folder beside its final place (`testsToDelete-u<uid>-bench-stage-…`), sealed, and only then renamed into the store, so a half-written directory is never
registered. The TypeScript install starts from a fixed `package.json` (`lang-v2-toolchain`), because npm otherwise names the project after the random staging folder in `package-lock.json` and `node_modules/.package-lock.json`, which made the tree hash differ on every install; the one recorded hash that was ever corrected: `typescript-6.0.3` was first recorded (commit `a2628cf4`) as `36dbb26c8a1afe6edbd919d6f121f8d7579e36d07d14f5a8eed4cf21062b7649`, a hash that had the random staging folder's name (`testsToDelete-u1000-bench-stage-typescript-jXe53n`) inside `package-lock.json` and could not be reproduced by any other install; after the fixed manifest it was replaced, by hand and once (commit `916d5979`), by `31b71ea9cbabcce7cf1b7936368be664b1f7e21840f02c0c11c59fffef1a8c6b`, which a second install under a different staging name and umask 077 reproduced. The package bytes were never in question: the install's own integrity check (the pinned `dist.integrity`) passed both times, and Node's and the SDK's recorded hashes never changed. Every install child (tar, `dotnet-install.sh`, npm) is started through `/bin/sh -c 'umask 022 && exec "$@"' sh <exe> <argv>` (`underInstallUmask` in `lib/toolchain-io.mjs`; the argv travels as arguments, never in the shell string), and the manifest the install writes itself is chmod 644. Library code never calls `process.umask` (a scan test pins that); only the command `install-toolchains.mjs` sets 022 for itself, once, before any async work. This is needed because the tree hash covers read and execute bits and the same archive extracted under 077 would hash differently. When `stacks-lang.json` already records a tree hash for the toolchain, the staged tree is hashed and compared BEFORE it is sealed and renamed: a difference discards the tree and fails naming both hashes, so a tree that is not the recorded one is never registered. An install that was killed leaves its staging folder (possibly sealed) in the store root; the next install first collects this user's `testsToDelete-u<uid>-bench-stage-*` folders whose owner record says the process is gone (`lib/toolchain-stale.mjs`: `lstat` first, only a real directory directly in the store root is touched - a symlink named like a staging folder is never followed; unseal, remove, name each on stderr), and leaves a live owner's, an unmarked and anyone else's folder alone. The tree is hashed before it is sealed and sealed only just before the rename. A directory already in the store is left alone (`present`). Each step prints one line to stderr before it starts, `<toolchain>: <step>` (checking the official source, downloading, verifying, extracting or installing, hashing, sealing, and recording for `--record`), so a long SDK install is not silent; stdout stays one JSON line per toolchain. Never apt, never sudo. `--record` writes the hashes of what the run installed; a recorded hash is
never moved. `inputs/stacks-lang.json` is `JSON.stringify(json, null, 2)` plus a newline and `--record` rewrites it by parse, set, stringify (no text patching); a test pins that the committed file round-trips byte for byte.

**Verify.** `node spikes/lang-bench/install-toolchains.mjs --verify [--deep]`: every directory present and sealed, the SDK directory holding only the pinned SDK, with
`--deep` every tree hash equal to the record. Exit 1 names the problem; exit 2 is usage or a store inside a git worktree.

**Preflight.** `lib/preflight-lang.mjs`, `preflightLang({ stacks, storeRoot, boxPath? })`. It first reads the store back deep (`verifyStore({ deep: true })`, about 1.2 s: sealed, one SDK, every tree hash equal to the record), because a binary replaced in place still answers `--version` with the pin; a toolchain with no recorded hash is refused. The probes run inside a bwrap box built from v1's primitives (root read-only, home /mnt
/run /tmp hidden, the distribution's dotnet hidden and `DOTNET_ROOT` set, the three directories bound back read-only, no network, pid namespace, cleared environment); S8b's `box.mjs` replaces it once it exists. It reads back
`node --version`, `dotnet --list-sdks`, `dotnet --list-runtimes`, `tsc --version`, the sha256 of the pinned `python.path`'s real file (the path is written once, in `stacks-lang.json`; the probe argv appends it), and `locale -a`, and refuses, naming the pin and
the value seen: another Node patch, a second SDK or the wrong one, another runtime, a TypeScript outside 6.0.x or another 6.0 patch, a changed Python binary, and a
missing `de_DE.utf8` (with `sudo locale-gen de_DE.UTF-8`, the remedy derived from the pinned locale name). It also refuses a box PATH that reaches a second `dotnet`. That scan runs INSIDE the box (`which -a dotnet` under the box's environment, then `readlink -f` of each hit and of the pinned one, in the box), so a distribution SDK the box hides is not a second dotnet, a PATH entry that exists only on the host is not counted, and one real file reached through two names is one dotnet. The probes' executables are looked up on the box's PATH by bwrap, not on the host's. Every probe executable is resolved INSIDE the box before it runs (`resolveProbeExecutables`: `which node|tsc|dotnet`, then `readlink -f` of the hit and of the pinned file in the box); one that resolves to another real file than the pinned one (a fake `node` that prints `v26.13.0`, earlier on a caller-supplied `boxPath`) is refused naming both and is never trusted; the probes then run the pinned files by absolute path, and a refused `node` also refuses `tsc` (a Node script). The pinned Python path is resolved in the box too and must be the file the host resolves (`facts.pythonReal`); its checksum is the pin. The default box PATH is the pinned directories alone. The host PATH `bwrap` is found on is a parameter (`hostPath`, default this process's), so no test changes `process.env`.

**Tests** (`node spikes/lang-bench/run-tests.mjs test/toolchain-*.test.mjs test/preflight-lang.test.mjs`). The pins and the three official-source readers
(`toolchain-pins`), digests / seal / tree hash / SDK versions / store root (`toolchain-store`), the installer through a fixture `io` (`toolchain-install`: a download that
is not the published bytes, an official source that disagrees with the pin, an install script that is not the recorded one, a second SDK, an integrity that is not the pinned
one - each leaves nothing in the store), the store read back and the hash record (`toolchain-verify`, including the real store), the preflight (`preflight-lang`: the pure
judgement, `A_PATH_that_still_finds_the_apt_SDK_is_refused`, `A_TypeScript_outside_the_pinned_line_is_refused`, and three cases inside a real box). A test that needs bwrap or the installed
store skips with its reason; `BENCH_REQUIRE_SANDBOX=1` turns the skip into a failure.

**Notes for later stories.**
- **Contract for S8b and E3 (the apphost root).** A program launched through its own apphost executable does not use PATH: it takes its dotnet root from `DOTNET_ROOT`, then
  `/etc/dotnet/install_location`, then the default install locations. With the distribution's SDK (`/usr/lib/dotnet`, 10.0.114) on the host that root was `/usr/lib/dotnet`
  (verified with `COREHOST_TRACE=1`: `Using dotnet root path [/usr/lib/dotnet]`), whatever the PATH said. Every box that runs a pinned-toolchain program (the execution layer's, the agent's,
  the scoring box) must therefore **hide `/usr/lib/dotnet`, `/usr/share/dotnet` and `/etc/dotnet` (an empty tmpfs over each that exists) AND set `DOTNET_ROOT` to the pinned SDK directory**;
  either alone leaves a road to the distribution's runtime. The preflight's box does both (`lib/preflight-box.mjs`, `DISTRIBUTION_DOTNET_PATHS`), and an **apphost probe** (`lib/apphost-probe.mjs`)
  builds a tiny console app with the pinned SDK inside the box (its scratch folder comes from the owned-name helper, `testsToDelete-u<uid>-bench-apphost-…` with an owner claim beside it, so a run killed during the build leaves something the next sweep collects) (offline, about 3 s: the SDK bundles the targeting and apphost packs), runs its apphost with `COREHOST_TRACE=1` and refuses
  any dotnet root other than the pinned directory. A test shows the open road: the same probe in a box without the hiding resolves `/usr/lib/dotnet`.
- `dotnet-install.sh` does not verify what it downloads; the check is ours (the archive's sha512 against the pin, which the release metadata confirms). The script's sha256 is pinned
  too, so a newer script makes the installer refuse until the pin is moved deliberately.
- Node's `SHASUMS256.txt` is read over HTTPS from nodejs.org; its GPG signature is not checked (no keyring is pinned).
- The Claude CLI copy (parent D8) is not part of this story.

## Package sets and snapshots (E1.S4)

Built 2026-11-04 (`todo/PLAN_lang_comparison_v2_E1.md` § E1.S4; parent D5, D6, §4.6). Each stack's offered package set is ONE hashed offline snapshot with a manifest; `PACKAGES.md` is generated from the registered record; a build in a box that holds only the seeded snapshot restores exactly that set and fails, naming it, on anything else (T-L13). **Registered 2026-11-04** by the team lead with `make-snapshots.mjs` (its defaults) after the code review: `inputs/cache-snapshots-lang.json` holds both records and the folders are in `~/rsd/lang-v2/caches/` (see "The registered snapshots" below).

**The recipe** (`inputs/cache-recipe-lang.json`, checked by `lib/cache-recipe-lang.mjs`). Each x-range is resolved ONCE, at build time, by `pickHighest` (v1) with a `line`, inside the box, and the exact version is recorded in the record's `resolved` and in the filled step argv. NuGet: the flat-container index of `api.nuget.org`; npm: `npm view <name>@<range> version --json`.

| Stack | Package | Version | Purpose (as `PACKAGES.md` shows it) |
|---|---|---|---|
| C# | `Microsoft.Extensions.Hosting` | `{HOSTING}`: highest of line 10.0 | Generic Host for daemons: lifetime and graceful stop, dependency injection, configuration, ILogger |
| C# | `Microsoft.AspNetCore.Authentication.JwtBearer` | `{JWT_BEARER}`: highest of 10.0 | JWT bearer authentication for ASP.NET Core Minimal API |
| C# | `xunit.v3` | `{XUNIT}`: highest of 4 | Test framework (xUnit v3), run on Microsoft Testing Platform: the test project is an executable |
| C# | `xunit.runner.visualstudio` | `{XUNIT_VS}`: highest of 4 | xUnit adapter for the VSTest runner |
| C# | `Microsoft.NET.Test.Sdk` | `{TEST_SDK}`: highest of 18 | VSTest host for dotnet test in VSTest mode |
| Node | `fastify` | 5.12.7 | HTTP routing; JSON-schema validation and fast serialization; lifecycle hooks and close() |
| Node | `@fastify/jwt` | 10.2.6 | JWT sign and verify as a Fastify plugin |
| Node | `@fastify/type-provider-typebox` | 6.3.0 | TypeBox schemas as Fastify route types |
| Node | `typebox` | `{TYPEBOX}`: highest of 1.1 | JSON-schema builder with static TypeScript types: the renamed TypeBox 1.x line, imported as `typebox` (it replaces `@sinclair/typebox`, which is not in the set) |
| Node | `pino` | `{PINO}`: highest of `^9.14.0 \|\| ^10.1.0` (what Fastify 5.12.7 resolves) | Structured logging |
| Node | `typescript` | 6.0.3 (S5's pin) | Type check and the compiler API the type-aware lint uses |
| Node | `@types/node` | `{TYPES_NODE}`: highest of 26 | Types of the Node 26 API |
| Node | `typescript-eslint` | `{TS_ESLINT}`: highest of 8.72 | TypeScript parser and type-aware lint rules |
| Node | `eslint`, `@eslint/js` | `{ESLINT}`: 10, `{ESLINT_JS}`: 10 | The lint runner and its recommended rules |

TypeBox (the owner's decision of the epoch-7 re-review, 2026-11-03, after `ask_consultants` `40498063`): `@fastify/type-provider-typebox` 6.3.0 peers `typebox ^1.1.14`, so the set holds `typebox` 1.1.x and no `@sinclair/typebox`. The patch is resolved once at the build and installed as that exact version (`typebox@{TYPEBOX}`): the provider's peer range alone would let npm take 1.4.x. A test proves there is exactly one `pino` and one `typebox` version in the manifest and that each equals `resolved`. The ASP.NET Core shared framework (Minimal API, its validation, System.Text.Json) comes with the SDK and is not a package.

A placeholder name is `[A-Z_]+` only, with no digit: v1's `fill` and `unresolved` use that pattern, so `{XUNIT_V3}` would reach an argv as text without being filled or noticed. `loadRecipeLang` refuses any brace token that is not such a name and a key of the stack's `resolve`, and a test runs v1's step runner on the refuted shape. The loader also refuses: a stack without a non-empty `packages` list, an entry without name, spec and a `purpose` free of `|` and line breaks, a `resolve` entry without `pick: "highest"`, a `csharp.project` that is absolute, has `..` or is not a folder, a stack called `superseded`, and any toolchain pin that differs from `inputs/stacks-lang.json` (the `node --version`, `dotnet --version` and `tsc --version` steps, the `typescript` package spec, and the project's `global.json`).

**The three C# projects** (`inputs/cache-projects/csharp/`, copied into the box's work folder by v1's `workSeed`): a daemon (console + Generic Host), a web app (Web SDK + JWT bearer) and a test project that references both. With .NET 10's package pruning, which packages a restore downloads depends on the project's SDK and framework references, so the three shapes the agents write are all restored. The test project is restored, built AND run once (`dotnet tests/SnapTests/bin/Release/net10.0/SnapTests.dll`, never `dotnet test`), so every asset an offline test run needs (xunit.v3, the Microsoft Testing Platform pieces, the VSTest host) is in the snapshot. `global.json` pins 10.0.403 with `rollForward: disable`; the online `nuget.config` clears every inherited source and adds nuget.org. All three projects set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (PROJECT section 1): the real build is green with it, offline restore included. The npm install steps of the recipe run with `--ignore-scripts` (the cache needs the tarballs only; the manifest is identical with and without it). Deviation from the brief's text: the test project carries `<Using Include="Xunit" />`, because without it `SmokeTests.cs` does not compile (CS0246 on `Fact`).

**The box** (v1's agent box, `agentBoxArgv`; `boxOptionsFor(dirs)` in `lib/cache-offline.mjs` is the one function that builds its options for the snapshot build and every offline box). S5's pinned directories are bound read-only at their full host paths (`dirs.dotnetSdk`, `dirs.typescript`; Node is `nodeDir: dirs.node`); `PATH` starts `<node>/bin : <dotnetSdk> : <typescript>/node_modules/.bin`; the distribution's dotnet is hidden (`DISTRIBUTION_DOTNET_PATHS`, an empty tmpfs over each that exists) AND `DOTNET_ROOT` is the pinned SDK (the contract of S5 for every box that runs a pinned-toolchain program). The widenings of v1 that make this possible (all optional parameters, the defaults keep every v1 byte): `makeSnapshot({ box, workSeed, dirName })`, the exported `inBox`, `pickHighest(stdout, { line })` (also reads NuGet's `{ versions }` and keeps a version only when it equals the line or starts with `<line>.`), `agentEnv({ dotnetDir })`, `agentBoxPrefix({ hide })`, `readNpmPackages` / `readNugetPackages(dir, { hashes })`. Each carries its reason in a comment.

**Licence and maintenance of every offered package** (PROJECT section 10; the recipe's `licence` and `maintenance` fields, required by `loadRecipeLang`). The licence is an SPDX expression and is COMPARED, by `test/cache-lang-offline.test.mjs`, with what the snapshot's own metadata declares (the `.nuspec` `<license>` of the NuGet package, the `license` of the `package.json` inside the npm tarball in the cache; `lib/package-metadata.mjs`), so a typed value cannot drift. The maintenance state is the latest release the registry metadata showed on the day it was read (2026-11-04: npm `time` of `dist-tags.latest`, NuGet's registration `published`); it is not in the snapshot and is not compared, only shaped (a date). How the dates were read (2026-11-04): one box opened by `openBox` on the built snapshot with `allow: ['registry.npmjs.org', 'api.nuget.org']`, i.e. through the egress proxy and nowhere else, ran for each npm package `npm view <name> time dist-tags.latest --json` (the release date of `dist-tags.latest` is its entry in `time`) and `npm view <name>@<version> license --json`, and for each NuGet package `curl -fsSL --compressed --max-time 60 https://api.nuget.org/v3/registration5-gz-semver2/<lower-case id>/index.json` and, where the index does not inline the last page, the same `curl` on that page's `@id`; the date is the `published` of the newest stable listed version on that page (ties: the highest version). The commands ran with the snapshot's npm cache and global packages folder as the cache, with `npm_config_update_notifier=false` and `npm_config_logs_max=0`, and the egress log of that box showed only those two hosts. The licence column was checked the same way against the registries and again, by test, against the snapshot's own metadata. No offered package is unmaintained: the oldest latest release is `typescript` 7.0.2 on 2026-07-08 (the offered 6.0.3 is the line `typescript-eslint` supports) and `@eslint/js` 10.0.1 on 2026-02-06.

| Stack | Package | Offered | Licence | Latest release the registry showed | Released |
|---|---|---|---|---|---|
| C# | `Microsoft.Extensions.Hosting` | {HOSTING} | MIT | 10.0.14 | 2026-10-25 |
| C# | `Microsoft.AspNetCore.Authentication.JwtBearer` | {JWT_BEARER} | MIT | 10.0.14 | 2026-10-25 |
| C# | `xunit.v3` | {XUNIT} | Apache-2.0 | 4.0.2 | 2026-10-09 |
| C# | `xunit.runner.visualstudio` | {XUNIT_VS} | Apache-2.0 | 4.0.1 | 2026-10-09 |
| C# | `Microsoft.NET.Test.Sdk` | {TEST_SDK} | MIT | 18.11.0 | 2026-10-13 |
| Node | `fastify` | 5.12.7 | MIT | 5.12.8 | 2026-11-02 |
| Node | `@fastify/jwt` | 10.2.6 | MIT | 10.2.7 | 2026-11-02 |
| Node | `@fastify/type-provider-typebox` | 6.3.0 | MIT | 6.3.0 | 2026-10-12 |
| Node | `typebox` | {TYPEBOX} | MIT | 1.4.3 | 2026-10-22 |
| Node | `pino` | {PINO} | MIT | 10.4.0 | 2026-10-02 |
| Node | `typescript` | 6.0.3 | Apache-2.0 | 7.0.2 | 2026-07-08 |
| Node | `@types/node` | {TYPES_NODE} | MIT | 26.6.6 | 2026-10-28 |
| Node | `typescript-eslint` | {TS_ESLINT} | MIT | 8.72.1 | 2026-10-26 |
| Node | `eslint` | {ESLINT} | MIT | 10.14.0 | 2026-10-30 |
| Node | `@eslint/js` | {ESLINT_JS} | MIT | 10.0.1 | 2026-02-06 |

`Offered` is the recipe's spec: a `{NAME}` is the highest version of its line on the build day (the versions are in the record's `resolved`).

**The record** (`inputs/cache-snapshots-lang.json`, written only by `make-snapshots.mjs` through v1's `registerSnapshot`, which refuses a stack that is already there). Per stack: `treeSha256` (v1's `treeHash` of the whole folder), `dirName` (`<stack>-<whole hash>`), `parts` (a hash per captured part: `nuget` and `dotnet`, or `npm`), `sizeBytes`, `packages` (THE MANIFEST: `{ ecosystem, name, version, integrity }` for every package, sorted by ecosystem, name in plain string order, then version by numeric parts; the integrity is the npm index entry's `sha512-...` or `sha512-` plus the text of `<id>.<version>.nupkg.sha512`; a package without one fails the build naming it), `resolved`, `steps` (argv and the pinned stdout only; no path of the operator's home), `manifestSha256` (sha256 of the sorted manifest rows, the identity a rebuild can match), `recipeSha256` (the recipe file's bytes), `projectSha256` (the C# project folder's tree hash, `null` for node), `madeOn` (UTC date). `superseded` is the append-only list of § The store. The manifest's name@version set must equal the list v1's maker returned, or the build fails naming the difference. The folder is `~/rsd/lang-v2/caches/<stack>-<treeSha256>/`, read-only.

**The offline box** (`openBox` in `lib/cache-offline.mjs`). Every restore and outside-package case runs in a FRESH box that holds only the seeded snapshot: a new layout, v1's `seedCaches` (it copies and re-hashes every part against the record), a fresh `box/home` for `HOME` and `DOTNET_CLI_HOME`, and the egress proxy with `allow: []`, so every registry is refused and the layout's `egress.ndjson` says so. `run` returns the whole output and exit code (a fixed `sh -c` wrapper writes them into `box/out`; v1's `inBox` keeps only the last 600 characters of a failure). Registry reachability is the refuted shape, built beside the right one with `allow`.
- **npm, the exact invocation.** Copying the cache is not enough: npm still asks the registry unless told not to. The workspace has `package.json` and `.npmrc` = `offline=true`; the command is `npm install --offline --no-audit --no-fund --no-update-notifier <name@version>...` (`npmOfflineArgv`) with the extra environment `npm_config_offline=true`, `npm_config_update_notifier=false`, `npm_config_logs_max=0` (`NPM_OFFLINE_ENV`); `npm_config_cache` is the seeded `box/npm`. A bare `npm install typescript` offline resolves the cached packument's `latest` (7.0.2), which is not in the cache: `PACKAGES.md` says to install the exact version listed.
- **NuGet.** The workspace `nuget.config` (`offlineNugetConfig(<box/nuget>)`, which S3 reuses) clears `packageSources` and `fallbackPackageFolders` and adds one local source, the seeded global packages folder; `NUGET_PACKAGES` is that folder. The extra environment: `DOTNET_ROOT`, the build servers off, `NUGET_CERT_REVOCATION_MODE=offline` (the proxy carries only `CONNECT :443`; the revocation hosts are plain HTTP), `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1`, `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`.
- **What a package or version that is not in the set does, as `PACKAGES.md` tells the agent** (the sentence is per stack, the team lead's wording, tested in `test/packages-md.test.mjs` and shown by `test/cache-lang-offline.test.mjs`): npm, a package or version not listed fails `ENOTCACHED` (`request to https://registry.npmjs.org/left-pad failed: cache mode is 'only-if-cached' but no cached response is available`; the message may name a dependency of the package rather than the package itself; `pino@9.14.0` is the version case). NuGet, a reference to a LOWER version than the one listed resolves to the listed version with the warning `NU1603`, which FAILS THE BUILD where warnings are errors (every seed project and, by PROJECT section 1, every workspace sets `TreatWarningsAsErrors`; with the setting the restore exits 1 with `error NU1603`; without it exit 0 and the listed version is what `project.assets.json` holds); a HIGHER version fails `NU1102`; a package not listed fails `NU1101` (`Unable to find package Newtonsoft.Json. No packages exist with this id in source(s): offline`); both errors name the package. Without `--offline` the proxy's refusal is a network error without `ENOTCACHED`; without `<clear />` the default nuget.org source comes back and the restore fails `NU1301`, not `NU1101`.
- **The manifest check.** `usedNpm(package-lock.json)` and `usedNuget(obj/project.assets.json, type package only, lower-case)` against the registered manifest through `outsideManifest`: a non-empty answer fails naming each package. `manifest - used` is printed as a diagnostic.

**`PACKAGES.md`** (`lib/packages-md.mjs`, `renderPackagesMd({ stack, record, recipe })`; `make-snapshots.mjs --print-packages-md <stack>` prints it). The first paragraph is the agent-visible sentence, per stack (the team lead's wording: see the bullet on what a package or version that is not in the set does). Derived, never typed: the version of each offered package is the manifest's, the expected version is the recipe's spec filled from `resolved` (a manifest without `name@expected` fails naming the package and both values), the purpose is the recipe's, a purpose with `|` or a line break is refused, and every other manifest package is in the second table ("in the cache as dependencies of the offered packages"). LF, one final newline, rows sorted by lower-case name, then name, then version. S3 writes it into the workspaces; this story writes no workspace. The rendered files are put here when the snapshots are registered.

**The store** (`lib/cache-store.mjs`). *Staging claim and sweep:* `make-snapshots.mjs` writes `writeOwner(<caches>/.building-<stack>-<pid>)` BEFORE it calls v1's maker (an `EEXIST` is a refusal, exit 3) and removes the claim in `finally`. At its start, after the gate lock, it sweeps the work root (the conventions' `sweepLeftovers`, token `bench`) and then each `.building-(csharp|node)-<pid>` folder by a fixed rule, in this order: not a real folder of this user: keep; no readable claim: keep; another owner, host or pid namespace: keep; the conventions' own `ownerState` (`owned.mjs`) says `gone` (no process has the pid): **remove**; `unknown` (a live pid with another start time, a pid that cannot be asked): **keep and say so on stderr** (an earlier draft of this story removed a live pid with another start time; the code round replaced that rule by the conventions' three-valued state, because deleting on anything but a proven `gone` is how another run's live folder is lost); anything else: keep. A **stranded snapshot folder** (`<stack>-<hash>` that the record this run read, registered or superseded, does not hold, left by a maker killed after it named the folder and before the registration) is only LISTED on stderr, when the claim of its build is proven gone and no other claim of that stack is alive, with the manual step (`chmod -R u+w <dir> && rm -rf <dir>`, after checking that it is registered nowhere, in no record and not on the NAS). **The sweep never removes a `<stack>-<hash>` folder:** the record path is per checkout and the caches folder is global, so an absent, older or wrong-shaped record would otherwise license deleting a registered, irreplaceable snapshot. Only the orphan claim file is removed. A record that is not a JSON object (`null`, `[]`, not JSON) is exit 2 with one line, before anything is swept or built. An **orphan claim** (`.building-<stack>-<pid>.owner` whose folder is absent, left by a build killed between the claim and the folder) is judged by the same rules after the claim is read, and removed only on `gone`. The pid in the name is never used. Every folder and claim looked at is one stderr line. *After the maker:* if anything fails after v1's maker returned (the manifest has a package without an integrity, the manifest differs from the maker's list, the registration is refused, or an exception is thrown inside these steps: the discard runs in a `finally`), the made folder is removed (`makeWritable`, then removal) unless its own (stack, hash) is in the record, registered or superseded, so a failed build leaves no read-only folder in the caches root; the made folder is discarded only on the word of a record that READS CORRECTLY and does not hold the pair; with an absent, unreadable or wrong-shaped record it is KEPT and named on stderr with the manual step (a C# rebuild can equal a registered folder, because C# is reproducible; the Node cache cannot be rebuilt). *Supersede* (`--supersede <stack> --reason <text>`): moves the stack's whole record into `superseded[]` as `{ stack, supersededOn, reason, record }` and drops the key, so the next build may register again; append only, written beside (a temporary name with a random suffix) and renamed; refused (exit 3) for no record, an empty reason, a `prereg/lang-v2-A-*` tag that exists or that git cannot list. *The tag reader* (`tagsOf`) reads the local tags AND the remote's (`git ls-remote --tags <url>`), and a git that cannot answer either question (no remote, no network) is a refusal, never "no tag". The url is `--tags-remote <url>`, else `remote.origin.url`; on this host `origin` is SSH (`git@github.com:<owner>/<repo>.git`) and no key is available to the command, so a GitHub SSH form (`git@github.com:...`, `ssh://git@github.com/...`) is read over `https://github.com/<owner>/<repo>.git`, and any other url is used as it is. *Prune* (`--prune-superseded`): refused until that tag exists; after it, the record AT THE TAG (`git show <tag>:spikes/lang-bench/inputs/cache-snapshots-lang.json`) is the authority, not the working tree's; a folder directly under the caches root is removed only when its name is `<stack>-<64 hex>`, ITS OWN (stack, hash) pair is a superseded record's and not the registered one (a hash registered under another stack neither keeps it nor licenses removing it), it is a real folder owned by this user, and `treeHash(folder)` equals the hash; every other entry is named as kept with its reason (`registered`, `not a snapshot of this series`, `hash differs`, `not this user's`). Prune first classifies and re-hashes EVERY candidate (each re-hash is announced as `checking snapshot <name> ...` on stderr, because a re-hash of 180 MB takes a while), and only then removes them; a removal that fails is reported (`could not remove <name>: <why>`, exit 1) and does not stop the others. The registered snapshots are never touched.

**The gate lock** (`lib/gate-lock.mjs`; no earlier story built it). `takeGateLock({ file })` first checks the lock's folder (a real directory of this user, not a link, else refused naming it), creates the file with `open(..., 'wx')` and writes the owner's record in the conventions' grammar; a held lock is refused at once naming its owner; an owner whose state is `unknown` (another host, a pid that cannot be asked, a live pid with another start time) is refused with "owner state unknown", the file to remove by hand and no "held by"; a lock whose owner is this user's, on this host and namespace, with no process, is reclaimed by renaming the stale file to a unique sibling, comparing the sibling's text with the text that was judged, and removing it only if they are the same (otherwise it is put back, and if a third taker took the path meanwhile the put-back is a plain refusal, never a throw), and said; an unreadable lock is refused and left in place; a failed write removes the lock only while the path still names the file this taker created (device and inode); `release()` removes the file only while it still holds the text this taker wrote, and a lock that still exists but cannot be read is said on stderr with the manual step. A release that meets a lock already gone (ENOENT) says nothing. A build, `--supersede` and `--prune-superseded` all run under it. The residual is stated in the file's header (an empty lock after a crash between the create and the write; liveness is a pid probe, not a heartbeat).

**The command** (`node spikes/lang-bench/make-snapshots.mjs`): `[--stack csharp] [--stack node]` builds and registers (default both, csharp first), `--supersede`, `--prune-superseded`, `--print-packages-md <stack>`; `--caches` (default `~/rsd/lang-v2/caches`, refused inside a git worktree), `--record`, `--recipe`, `--stacks`, `--work` (default `~/.cache/lang-v2/w`), `--gate-lock` (default `~/.cache/lang-v2/gate.lock`). Exit 0 done, 1 a build failed, 2 usage (or a caches folder inside a worktree), 3 refused (gate lock held, already registered, preflight, tag rule, claim exists). The command lowers its own priority first, as `run-tests.mjs` does. `--tags-remote <url>` names the remote the tag reader asks (see the store). A missing or unparsable `--recipe` or `--stacks` file is exit 2 with one line naming the flag. A build, a supersede and a prune each take the gate lock, released in `finally`. A build sweeps, refuses a registered stack BEFORE any build, runs `preflightLang`, makes one snapshot per stack in one owned work folder, and prints `egress <stack>: <JSON>` (the hosts the proxy allowed and refused), `registered <stack> <dirName> (<bytes> bytes, <n> packages)` and `built <stack> in <s> s`.

**Measured** (one host, 2026-11-04, the registries as they were that day; coder test builds before the registration): the whole command (both stacks, preflight included) 45-51 s over three runs; the C# build 18-21 s (`api.nuget.org`, 33-35 connections, about 44 MB down), the Node build 21 s (`registry.npmjs.org`, 36 connections, about 51 MB down); no host refused. C#: 174,187,832 bytes, 60 packages (resolved HOSTING 10.0.14, JWT_BEARER 10.0.14, XUNIT 4.0.2, XUNIT_VS 4.0.1, TEST_SDK 18.11.0); the tree hash was identical in every build observed (three coder builds, and the Opus reviewer's build). Node: 180,129,178 bytes, 164 packages (resolved TYPES_NODE 26.6.6, TS_ESLINT 8.72.1, ESLINT 10.14.0, ESLINT_JS 10.0.1, PINO 10.4.0, TYPEBOX 1.1.39); its tree hash differed in each build, while the manifest was identical (the same 164 name, version and integrity rows, the same `manifestSha256`; checked between the coder's builds before and after the fix batch). The reason: every `_cacache/index-v5` entry holds `time`, `metadata.time` and the registry's `date` header, so the index bytes change on every build; the tarballs under `content-v2` are content-addressed and immutable. **Consequence: the registered Node folder is irreplaceable.** After registration it is backed up to the operator's NAS (`V:\dew_flow_backup`, copy and verify by hash) and is never "repaired" by a rebuild, which would give another tree hash. The identity a rebuild CAN match is the manifest (name, version, integrity), so the record also carries `manifestSha256` (sha256 of the sorted rows `[ecosystem, name, version, integrity]`). The Node manifest has one `pino` (10.4.0) and one `typebox` (1.1.39); the C# manifest also holds `microsoft.extensions.logging.abstractions` 8.0.0 beside 10.0.14 (the restore does not use 8.0.0: it is the only manifest-minus-used entry). The network test file takes about 92 s (the build 45 s; the offered set offline: Node 9.6 s, C# 12.5 s; the two missing-package cases 3.3 s and 6.6 s; the lint 12.8 s).

**Coupling.** This story builds on v1's modules (`spikes/minigit-bench/lib/cache-snapshot.mjs`, `sandbox.mjs`, `packages.mjs`, `egress.mjs`, `bwrap.mjs`): the snapshot maker, the agent box and the package readers are v1's, widened by optional parameters. A change to those files can change the v2 snapshot build, and v1's own code-shape test (`test/code-shape.test.mjs`) pins their function size and complexity; `research/architecture.md` names the dependency.

**The registered snapshots** (`inputs/cache-snapshots-lang.json`, written only by `make-snapshots.mjs`; the folders are read-only under `~/rsd/lang-v2/caches/`, never to be touched by a test or a rebuild). The Node folder is irreplaceable (see Measured): it is backed up to the NAS and verified by hash, and the identity a rebuild could match is the manifest. The backup is a tar, because the NAS keeps no file modes and `treeHash` covers the read and execute bits, so a plain copy would not re-hash: `/mnt/v/dew_flow_backup/lang-v2/caches/node-bebdc7a2….tar` with its `.tar.sha256` (run `sha256sum -c` inside that folder). To restore: `tar -xpf <tar> -C ~/rsd/lang-v2/caches`, then check it with `treeHash` against the record; a restore from the NAS copy re-hashed equal on 2026-11-04.

| Stack | Folder | Tree sha256 | Manifest sha256 | Packages | Bytes | Made on |
|---|---|---|---|---|---|---|
| csharp | `csharp-452c7a571ec0d11b2ab9d8fe4b038b9d3076374f5d0503d94a8e596378cf0cee` | `452c7a571ec0d11b2ab9d8fe4b038b9d3076374f5d0503d94a8e596378cf0cee` | `2e897fc5db4ded367bced1478f4d0a260d1b3277f75bbf6cd58c9d0b03acc002` | 60 | 174,187,832 | 2026-11-04 |
| node | `node-bebdc7a20541b446c4efd09099a58354aa877e8137956fa66a67afad969fb2c6` | `bebdc7a20541b446c4efd09099a58354aa877e8137956fa66a67afad969fb2c6` | `a21813acac9013bc8869d4a11e2e49e93ed2c6bfd60591606b85e2da1b40ab61` | 164 | 180,132,320 | 2026-11-04 |

The versions resolved once at the build (`resolved`):
- csharp: HOSTING 10.0.14, JWT_BEARER 10.0.14, XUNIT 4.0.2, XUNIT_VS 4.0.1, TEST_SDK 18.11.0
- node: TYPES_NODE 26.6.6, TS_ESLINT 8.72.1, ESLINT 10.14.0, ESLINT_JS 10.0.1, PINO 10.4.0, TYPEBOX 1.1.39

The licence and maintenance table is above (the recipe's `licence` and `maintenance` fields, the licence compared by test with the snapshot's own metadata). The recipe's `recipeSha256` and the C# `projectSha256` are in the record.

**`PACKAGES.md` as derived from the record**, quoted as of 2026-11-04 (`node spikes/lang-bench/make-snapshots.mjs --print-packages-md csharp` and `... node`; derived output, never typed: it is regenerated from the record and the recipe, and S3 writes it into the workspaces).

C#:

````markdown
# Packages

Only the packages and versions listed here are available; there is no network. A reference to a lower version than the one listed resolves to the listed version, with the warning NU1603, which fails the build where warnings are errors; a higher version fails with NU1102, and a package not listed fails with NU1101; both errors name the package.
Install a package with the exact version listed: `<PackageReference Include="NAME" Version="VERSION" />`.
ASP.NET Core (Minimal API, its built-in validation, System.Text.Json) and the .NET base library come with SDK 10.0.403 as shared frameworks; they are not packages.

Cache snapshot: tree sha256 `452c7a571ec0d11b2ab9d8fe4b038b9d3076374f5d0503d94a8e596378cf0cee`, made 2026-11-04.

## Offered

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.14 | JWT bearer authentication for ASP.NET Core Minimal API |
| `Microsoft.Extensions.Hosting` | 10.0.14 | Generic Host for daemons: lifetime and graceful stop (IHostApplicationLifetime), dependency injection, configuration, ILogger |
| `Microsoft.NET.Test.Sdk` | 18.11.0 | VSTest host for dotnet test in VSTest mode |
| `xunit.runner.visualstudio` | 4.0.1 | xUnit adapter for the VSTest runner (dotnet test in VSTest mode) |
| `xunit.v3` | 4.0.2 | Test framework (xUnit v3), run on Microsoft Testing Platform: the test project is an executable |

## In the cache as dependencies of the offered packages

| Package | Version |
|---|---|
| `microsoft.applicationinsights` | 2.23.0 |
| `microsoft.bcl.asyncinterfaces` | 6.0.0 |
| `microsoft.bcl.cryptography` | 10.0.2 |
| `microsoft.codecoverage` | 18.11.0 |
| `microsoft.extensions.configuration` | 10.0.14 |
| `microsoft.extensions.configuration.abstractions` | 10.0.14 |
| `microsoft.extensions.configuration.binder` | 10.0.14 |
| `microsoft.extensions.configuration.commandline` | 10.0.14 |
| `microsoft.extensions.configuration.environmentvariables` | 10.0.14 |
| `microsoft.extensions.configuration.fileextensions` | 10.0.14 |
| `microsoft.extensions.configuration.json` | 10.0.14 |
| `microsoft.extensions.configuration.usersecrets` | 10.0.14 |
| `microsoft.extensions.dependencyinjection` | 10.0.14 |
| `microsoft.extensions.dependencyinjection.abstractions` | 10.0.14 |
| `microsoft.extensions.diagnostics` | 10.0.14 |
| `microsoft.extensions.diagnostics.abstractions` | 10.0.14 |
| `microsoft.extensions.fileproviders.abstractions` | 10.0.14 |
| `microsoft.extensions.fileproviders.physical` | 10.0.14 |
| `microsoft.extensions.filesystemglobbing` | 10.0.14 |
| `microsoft.extensions.hosting.abstractions` | 10.0.14 |
| `microsoft.extensions.logging` | 10.0.14 |
| `microsoft.extensions.logging.abstractions` | 8.0.0 |
| `microsoft.extensions.logging.abstractions` | 10.0.14 |
| `microsoft.extensions.logging.configuration` | 10.0.14 |
| `microsoft.extensions.logging.console` | 10.0.14 |
| `microsoft.extensions.logging.debug` | 10.0.14 |
| `microsoft.extensions.logging.eventlog` | 10.0.14 |
| `microsoft.extensions.logging.eventsource` | 10.0.14 |
| `microsoft.extensions.options` | 10.0.14 |
| `microsoft.extensions.options.configurationextensions` | 10.0.14 |
| `microsoft.extensions.primitives` | 10.0.14 |
| `microsoft.identitymodel.abstractions` | 8.19.2 |
| `microsoft.identitymodel.jsonwebtokens` | 8.19.2 |
| `microsoft.identitymodel.logging` | 8.19.2 |
| `microsoft.identitymodel.protocols` | 8.19.2 |
| `microsoft.identitymodel.protocols.openidconnect` | 8.19.2 |
| `microsoft.identitymodel.tokens` | 8.19.2 |
| `microsoft.testing.extensions.telemetry` | 2.5.1 |
| `microsoft.testing.extensions.trxreport.abstractions` | 2.5.1 |
| `microsoft.testing.platform` | 2.5.1 |
| `microsoft.testing.platform.msbuild` | 2.5.1 |
| `microsoft.testplatform.objectmodel` | 18.11.0 |
| `microsoft.testplatform.testhost` | 18.11.0 |
| `microsoft.win32.registry` | 5.0.0 |
| `system.diagnostics.eventlog` | 10.0.14 |
| `system.identitymodel.tokens.jwt` | 8.19.2 |
| `system.security.accesscontrol` | 6.0.1 |
| `xunit.analyzers` | 2.2.0 |
| `xunit.v3.assert` | 4.0.2 |
| `xunit.v3.common` | 4.0.2 |
| `xunit.v3.core.mtp-v2` | 4.0.2 |
| `xunit.v3.extensibility.core` | 4.0.2 |
| `xunit.v3.mtp-v2` | 4.0.2 |
| `xunit.v3.runner.common` | 4.0.2 |
| `xunit.v3.runner.inproc.console` | 4.0.2 |
````

Node:

````markdown
# Packages

Only the packages and versions listed here are available; there is no network. A package or version not listed fails to install with ENOTCACHED; the message may name a dependency of the package rather than the package itself.
Install a package with the exact version listed: `npm install NAME@VERSION`.

Cache snapshot: tree sha256 `bebdc7a20541b446c4efd09099a58354aa877e8137956fa66a67afad969fb2c6`, made 2026-11-04.

## Offered

| Package | Version | Purpose |
|---|---|---|
| `@eslint/js` | 10.0.1 | ESLint's recommended JavaScript rules |
| `@fastify/jwt` | 10.2.6 | JWT sign and verify as a Fastify plugin |
| `@fastify/type-provider-typebox` | 6.3.0 | TypeBox schemas as Fastify route types |
| `@types/node` | 26.6.6 | Types of the Node 26 API |
| `eslint` | 10.14.0 | The lint runner |
| `fastify` | 5.12.7 | HTTP routing; JSON-schema validation and fast serialization; lifecycle hooks and close() |
| `pino` | 10.4.0 | Structured logging (Fastify's logger), the version Fastify 5.12.7 resolves |
| `typebox` | 1.1.39 | JSON-schema builder with static TypeScript types: the renamed TypeBox 1.x line, imported as `typebox` (it replaces `@sinclair/typebox`, which is not in the set) |
| `typescript` | 6.0.3 | Type check (tsc --noEmit) and the compiler API the type-aware lint uses |
| `typescript-eslint` | 8.72.1 | TypeScript parser and type-aware lint rules for ESLint |

## In the cache as dependencies of the offered packages

| Package | Version |
|---|---|
| `@cacheable/memory` | 2.2.0 |
| `@cacheable/utils` | 2.5.0 |
| `@eslint-community/eslint-utils` | 4.10.1 |
| `@eslint-community/regexpp` | 4.12.2 |
| `@eslint/config-array` | 0.23.5 |
| `@eslint/config-helpers` | 0.7.0 |
| `@eslint/core` | 1.2.1 |
| `@eslint/object-schema` | 3.0.5 |
| `@eslint/plugin-kit` | 0.7.3 |
| `@fastify/ajv-compiler` | 4.0.7 |
| `@fastify/deepmerge` | 3.2.4 |
| `@fastify/error` | 4.2.1 |
| `@fastify/fast-json-stringify-compiler` | 5.1.1 |
| `@fastify/forwarded` | 3.1.2 |
| `@fastify/merge-json-schemas` | 0.2.2 |
| `@fastify/proxy-addr` | 5.1.2 |
| `@humanfs/core` | 0.19.2 |
| `@humanfs/node` | 0.16.8 |
| `@humanfs/types` | 0.15.0 |
| `@humanwhocodes/module-importer` | 1.0.1 |
| `@humanwhocodes/retry` | 0.4.3 |
| `@keyv/bigmap` | 1.3.1 |
| `@keyv/serialize` | 1.1.1 |
| `@lukeed/ms` | 2.0.2 |
| `@pinojs/redact` | 0.4.0 |
| `@types/esrecurse` | 4.3.1 |
| `@types/estree` | 1.0.10 |
| `@types/json-schema` | 7.0.15 |
| `@typescript-eslint/eslint-plugin` | 8.72.1 |
| `@typescript-eslint/parser` | 8.72.1 |
| `@typescript-eslint/project-service` | 8.72.1 |
| `@typescript-eslint/scope-manager` | 8.72.1 |
| `@typescript-eslint/tsconfig-utils` | 8.72.1 |
| `@typescript-eslint/type-utils` | 8.72.1 |
| `@typescript-eslint/types` | 8.72.1 |
| `@typescript-eslint/typescript-estree` | 8.72.1 |
| `@typescript-eslint/utils` | 8.72.1 |
| `@typescript-eslint/visitor-keys` | 8.72.1 |
| `abstract-logging` | 2.0.1 |
| `acorn` | 8.19.0 |
| `acorn-jsx` | 5.3.2 |
| `ajv` | 6.15.0 |
| `ajv` | 8.20.0 |
| `ajv-formats` | 3.0.1 |
| `asn1.js` | 5.4.1 |
| `atomic-sleep` | 1.0.0 |
| `avvio` | 9.3.1 |
| `balanced-match` | 4.0.4 |
| `bn.js` | 4.12.5 |
| `brace-expansion` | 5.0.12 |
| `cacheable` | 2.5.0 |
| `cookie` | 2.0.1 |
| `cross-spawn` | 7.0.6 |
| `debug` | 4.4.3 |
| `deep-is` | 0.1.4 |
| `dequal` | 2.0.3 |
| `ecdsa-sig-formatter` | 1.0.11 |
| `escape-string-regexp` | 4.0.0 |
| `eslint-scope` | 9.1.2 |
| `eslint-visitor-keys` | 3.4.3 |
| `eslint-visitor-keys` | 5.0.1 |
| `espree` | 11.2.0 |
| `esquery` | 1.7.0 |
| `esrecurse` | 4.3.0 |
| `estraverse` | 5.3.0 |
| `esutils` | 2.0.3 |
| `fast-decode-uri-component` | 1.0.1 |
| `fast-deep-equal` | 3.1.3 |
| `fast-json-stable-stringify` | 2.1.0 |
| `fast-json-stringify` | 7.2.1 |
| `fast-jwt` | 6.3.6 |
| `fast-levenshtein` | 2.0.6 |
| `fast-querystring` | 1.1.2 |
| `fast-uri` | 3.1.9 |
| `fast-uri` | 4.2.3 |
| `fastfall` | 1.5.1 |
| `fastify-plugin` | 6.0.1 |
| `fastparallel` | 2.4.1 |
| `fastq` | 1.20.3 |
| `fastseries` | 1.7.2 |
| `fdir` | 6.5.0 |
| `file-entry-cache` | 11.1.5 |
| `find-my-way` | 9.10.0 |
| `find-up` | 5.0.0 |
| `flat-cache` | 6.1.23 |
| `flatted` | 3.4.4 |
| `glob-parent` | 6.0.2 |
| `hashery` | 1.5.1 |
| `hookified` | 1.15.1 |
| `hookified` | 2.2.0 |
| `ignore` | 5.3.2 |
| `ignore` | 7.0.14 |
| `imurmurhash` | 0.1.4 |
| `inherits` | 2.0.4 |
| `ipaddr.js` | 2.5.0 |
| `is-extglob` | 2.1.1 |
| `is-glob` | 4.0.3 |
| `isexe` | 2.0.0 |
| `json-schema-ref-resolver` | 3.0.1 |
| `json-schema-traverse` | 0.4.1 |
| `json-schema-traverse` | 1.0.0 |
| `json-stable-stringify-without-jsonify` | 1.0.1 |
| `keyv` | 5.6.0 |
| `levn` | 0.4.1 |
| `light-my-request` | 6.8.0 |
| `locate-path` | 6.0.0 |
| `minimalistic-assert` | 1.0.1 |
| `minimatch` | 10.2.6 |
| `mnemonist` | 0.40.7 |
| `ms` | 2.1.3 |
| `natural-compare` | 1.4.0 |
| `obliterator` | 2.0.5 |
| `on-exit-leak-free` | 2.1.2 |
| `optionator` | 0.9.4 |
| `p-limit` | 3.1.0 |
| `p-locate` | 5.0.0 |
| `path-exists` | 4.0.0 |
| `path-key` | 3.1.1 |
| `picomatch` | 4.0.7 |
| `pino-abstract-transport` | 3.0.0 |
| `pino-std-serializers` | 7.1.0 |
| `prelude-ls` | 1.2.1 |
| `process-warning` | 5.1.1 |
| `punycode` | 2.3.1 |
| `qified` | 0.10.1 |
| `quick-format-unescaped` | 4.0.4 |
| `real-require` | 1.0.0 |
| `require-from-string` | 2.0.2 |
| `ret` | 0.5.1 |
| `reusify` | 1.1.0 |
| `rfdc` | 1.4.1 |
| `safe-buffer` | 5.2.1 |
| `safe-regex2` | 5.1.2 |
| `safe-stable-stringify` | 2.5.0 |
| `safer-buffer` | 2.1.2 |
| `secure-json-parse` | 4.1.1 |
| `semver` | 7.8.6 |
| `set-cookie-parser` | 3.2.2 |
| `shebang-command` | 2.0.0 |
| `shebang-regex` | 3.0.0 |
| `sonic-boom` | 4.2.1 |
| `split2` | 4.2.0 |
| `steed` | 1.1.3 |
| `thread-stream` | 4.2.1 |
| `tinyglobby` | 0.2.17 |
| `toad-cache` | 3.7.4 |
| `ts-api-utils` | 2.5.0 |
| `type-check` | 0.4.0 |
| `undici-types` | 8.9.0 |
| `uri-js` | 4.4.1 |
| `which` | 2.0.2 |
| `word-wrap` | 1.2.5 |
| `xtend` | 4.0.2 |
| `yocto-queue` | 0.1.0 |
````


**Notes for later stories.**
- S3 reuses `offlineNugetConfig`, `NPMRC_OFFLINE`, `NPM_OFFLINE_ENV` and `npmOfflineArgv` for the workspaces, and `renderPackagesMd` for `PACKAGES.md`.
- The offered web set has no in-memory test host for C# web tests: ASP.NET Core's `Microsoft.AspNetCore.Mvc.Testing` is not in the set, while Fastify has `inject()` built in (a fairness question for group B, not decided here).
- If a workspace sets `IsAotCompatible`, `EnableTrimAnalyzer`, `PublishTrimmed` or `PublishAot`, the SDK restores `Microsoft.NET.ILLink.Tasks` from NuGet (not in the set); with `TreatWarningsAsErrors`, NuGet's audit warnings in an offline box (a local source has no vulnerability data) may become errors. S3 must check both.
- The lint packages are v1 D4's shape (`eslint`, `@eslint/js`, `typescript-eslint`); if S7's template needs more (for example `globals`), the snapshot is superseded before the pre-registration.
- The conventions' sweeper scan lists seven owner-blind lines in files this story did not touch (`spikes/lang-bench/test/py/test_burner.py`, `test/support/probe-scope.mjs`, `spikes/minigit-bench/lib/heldout-run.mjs` and four v1 tests); the same seven are listed at `main` 469a5109. This story adds none.

## Workspaces (E1.S3)

Built 2026-11-04, phase A (`todo/PLAN_lang_comparison_v2_E1.md` § E1.S3; parent D37, D38, D61, T-L1, T-L2, T-L31, T-L57). Phase A is the templates, the builder, the task-repository fetch and the tests on the execution layer. The ten task workspaces, the real statements and the real samples are phase B.

**The templates** (`spikes/lang-bench/templates/<stack>/<kind>/`, kinds `olympiad`, `daemon`, `web`). A template is the agent's stand-alone project, like `inputs/cache-projects/csharp/`, so it carries inline package versions (the central-package rule of this repository's own projects does not apply). It holds no program source: an empty workspace does not build, and every build case adds the hello fixture. C#: `App.csproj`, `Directory.Build.props`, `global.json`, `nuget.config`, `build.sh`, `run.sh`. Node: `package.json`, `package-lock.json`, `tsconfig.json`, `.npmrc`, `eslint.config.mjs`, `build.sh`, `run.sh`. `build.sh` is the agent-visible contract: C# `dotnet build App.csproj -c Release --nologo -m:4` followed by the warning policy below (`-m:4` is mandatory under `csharp.dotnet-build`), Node `npm ci --offline` then `npx --offline tsc --noEmit -p tsconfig.json`. `run.sh` ends with `exec dotnet bin/Release/net10.0/App.dll` or `exec node src/main.ts`, so the shell is replaced by the program and a stop signal reaches it (T-L31). Neither script sets a variable. A comment line of `build.sh` names the stack's frozen pin files; a test compares it with `templates/stack-files.json`.

What is frozen and what the agent may edit is **decided in parent D65** (the frozen list, the restore of the frozen files by the scorer, the folders each stack's copy drops) and D66 (the one acceptance policy for both stacks); this module records what was built and measured, and where it differs from those decisions, D65 and D66 win. The list as built (OP-4, first decided by the team lead): frozen are `global.json`, `nuget.config`, `Directory.Build.props` (C#) and `.npmrc`, `tsconfig.json`, `eslint.config.mjs` (Node); the agent edits `App.csproj`, `package.json` and `package-lock.json`, because it may add a listed package. For that reason `RestoreLockedMode` and `RestorePackagesWithLockFile` are not set. `TargetFramework` and `Nullable` are in `Directory.Build.props` (a test project gets the same settings). `NuGetAudit` is off: a local source has no vulnerability data and the warning would become an error. No ILLink property (`IsAotCompatible`, `EnableTrimAnalyzer`, `PublishTrimmed`, `PublishAot`) is set, because each would restore `Microsoft.NET.ILLink.Tasks`, which is outside the set; this answers the note of the Package sets section, whose text stays as it is. There is no `InvariantGlobalization` (D35).

**The warning policy (fix round, 2026-11-04).** The policy is **parent D66's** and the build contract **D65's**; what follows is the measurement behind them. The C# build fails on a type-safety error (nullable), a package version problem (`NU1603`, `NU1605`) and a use of `BinaryFormatter` (`SYSLIB0011`), and on nothing else: a slip (an unused local `CS0219`, a call not awaited `CS4014`, unreachable code `CS0162`) is a warning and exit 0, the same rule as `tsc` strict in the Node stack. `TreatWarningsAsErrors` is not in the props file. `build.sh` forces the policy on the command line as MSBuild global properties, which a plain property in a project file cannot override. The final line is `dotnet build App.csproj -c Release --nologo -m:4 --no-incremental -p:TreatWarningsAsErrors=false "-p:WarningsAsErrors=nullable%3BNU1603%3BNU1605%3BSYSLIB0011" -p:Nullable=enable -p:WarningsNotAsErrors= -p:WarningLevel=10 "-p:NoWarn=1701%3B1702"`. The semicolon is `%3B` (the comma form `nullable,NU1603` made a clean project fail with `NU1603`; `-warnaserror:NU1603` with `-p:WarningsAsErrors=nullable` gave the same results as `%3B`). `WarningLevel=10` and `NoWarn=1701;1702` are the SDK's own values, read from a clean build's evaluated properties (`dotnet msbuild -getProperty:NoWarn,WarningLevel`, the same for all three kinds): a global value stops the SDK from appending, so the defaults are written out, and a clean hello of all three kinds still has 0 warnings. `NU1102` (a higher version) is an error by default and still fails; so does `NU1605`. `--no-incremental` is part of the line because a plain Release build that compiled the sources with a nullable warning leaves them up to date, and the next `bash build.sh` would skip the compile and exit 0 while a clean copy fails; with the flag the compile always runs (measured, and pinned by a test). The policy stays on the command line and does not move back into the props file, because a test project must not inherit it. Measured offline against the final `build.sh`, with `string s = null;` in a minimal program and each override in `App.csproj`:

| Override in `App.csproj` | `bash build.sh` exit | Stopped by the command line |
|---|---|---|
| `<Nullable>disable</Nullable>` | 1 (CS8600) | yes |
| `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` | 1 (CS8600) | yes |
| `<WarningsAsErrors />` | 1 (CS8600) | yes |
| `<WarningsNotAsErrors>nullable</WarningsNotAsErrors>` | 1 (CS8600) | yes |
| `<NoWarn>CS8600</NoWarn>` | 1 (CS8600) | yes |
| `<WarningLevel>0</WarningLevel>` | 1 (CS8600) | yes |
| `TreatAsLocalProperty="Nullable"` on `<Project>` with `<Nullable>disable</Nullable>` | 0 | no |
| `<Target BeforeTargets="CoreCompile">` setting `Nullable` to `disable` | 0 | no |
| `<CompilerResponseFile>` naming a file with `-nowarn:CS8600` | 0 | no |
| `NoWarn="NU1603"` metadata on a `<PackageReference>` of a lower version | 0 | no |
| `NoWarn="NU1605"` metadata on a `<PackageReference>` that downgrades | 0 | no |
| `.editorconfig` with `dotnet_diagnostic.CS8600.severity = none` | 1 (CS8600) | yes |
| `.globalconfig` with `dotnet_diagnostic.CS8600.severity = none` | 1 (CS8600) | yes |
| `Directory.Build.targets` with a static `<Nullable>disable</Nullable>` | 1 (CS8600) | yes |
| `Directory.Build.rsp` with `-p:Nullable=disable` and `-p:NoWarn=CS8600` | 1 (CS8600) | yes |
| `#pragma warning disable CS8600` at the top of `Program.cs` | 0 | no |
| `Directory.Build.targets` with a `<Target BeforeTargets="CoreCompile">` setting `Nullable` to `disable` | 0 | no |
| `App.csproj.user` with the same target | 0 | no |
| `.editorconfig` with `[*.cs] generated_code = true` | 0 | no |
| a file named `Logic.g.cs` holding `string s = null;` in a method | 0 | no |
| a `// <auto-generated/>` header at the top of `Logic.cs`, which holds `string s = null;` in a method | 0 | no |

With the first line (`TreatWarningsAsErrors` and `WarningsAsErrors` only) the last four of the old order were not stopped: `Nullable=disable`, `WarningsNotAsErrors=nullable`, `NoWarn=CS8600` and `WarningLevel=0` exited 0. The rows after the six were each measured in the offline box (G2 of the second fix round). Not stopped from `App.csproj` alone: a local `Nullable` (`TreatAsLocalProperty`), a target that sets the property before `CoreCompile`, a compiler response file, `NoWarn` metadata on a `<PackageReference>`, and a source pragma (symmetric with `@ts-ignore` in the Node stack). Stopped, but only for the form that was measured, not as a class: an `.editorconfig` or `.globalconfig` with `dotnet_diagnostic.CS8600.severity = none`, a `Directory.Build.targets` with a static property, and a `Directory.Build.rsp` with `-p:` switches. The same files in another form are not stopped: a `Directory.Build.targets` or an `App.csproj.user` with a `<Target BeforeTargets="CoreCompile">` that sets the property, and an `.editorconfig` with `[*.cs] generated_code = true`, all exit 0. A file named `Logic.g.cs` and a `// <auto-generated/>` header are source-level, the same class as the pragma: C# treats such a file as generated code and does not report nullable warnings in it. **The rule for E3's scorer** (the team lead records it): `App.csproj` may differ from the template only by `<PackageReference Include Version>` items and nothing else. *(Superseded by parent D65: the scorer scores as submitted, restores the frozen files and regenerates nothing; D65's reason is that regeneration could fail a correct program. The text is kept as it was written.)* No `Directory.Build.targets` is added by us, because it would change the plan's pin list. The global `WarningsAsErrors` replaces the SDK's own default list (`NU1605;SYSLIB0011`), so the line names both again: a package downgrade (`NU1605`) stays an error (a test downgrades `Microsoft.Extensions.Logging.Abstractions` below what Hosting needs; the manifest holds two versions of that package). The test `The_command_line_policy_stops_all_six_project_file_overrides_with_the_nullable_error` runs the six; an older test with a first-measurement name reads this table, so the two cannot differ. **Tests live in `tests/`**, outside this build: `Directory.Build.props` has `<DefaultItemExcludes>$(DefaultItemExcludes);tests/**;**/bin/**;**/obj/**;**/node_modules/**</DefaultItemExcludes>` (a folder named `bin`, `obj` or `node_modules` at any depth is not compiled, which is what the scorer's clean copy leaves out, so a build in place and a build on the copy agree; a second project built first no longer breaks the root build with CS0579), and `global.json` names the `Microsoft.Testing.Platform` runner, so `bash build.sh` passes with a `tests/Tests.csproj` present and `dotnet test --project tests/Tests.csproj` runs it with `xunit.v3` offline (both proven in the box).

**What each kind adds (D47).** The set OFFERED is one set for all kinds: one registered snapshot per stack, and `PACKAGES.md` is byte-equal across the kinds of a stack. A kind differs only in what the template already references, and only in `App.csproj`, `package.json` and `package-lock.json`. Olympiad: nothing but the lint set and types (every olympiad workspace of a stack holds the same file set with the same bytes, D33). Daemon: `Microsoft.Extensions.Hosting` (C#), `pino` (Node). Web: `Sdk.Web` and `JwtBearer` (C#), `fastify`, `@fastify/jwt`, `@fastify/type-provider-typebox`, `typebox` and `pino` (Node), because the framework sentence of a web task orders these frameworks. An unused reference costs nothing at run time. `xunit.v3` and its partners are never referenced: tests are the agent's choice. Each version is exactly one row of the registered manifest; a test reads the template and looks the version up. The three locks are generated, never typed (`build-workspaces.mjs --make-locks`, offline, in the build box of the Node snapshot, checked against the manifest's integrity; `--ignore-scripts` is for the generation only, and the locks record no install script).

**The `%NUGET_PACKAGES%` source.** The template's `nuget.config` is the offline config over `%NUGET_PACKAGES%`, which NuGet expands from the environment on Linux. Its text is therefore fixed and holds no path of a home or of a session. `cache-offline.mjs` gained `WORKSPACE_NUGET_SOURCE` and `workspaceNugetConfig()`; `offlineNugetConfig` keeps its check and bytes, and `offlineNugetConfig('%NUGET_PACKAGES%')` stays refused. A lower version gives `NU1603` and a package outside the set `NU1101` naming the package and the source `offline`, so the source really is read.

**The build box of a workspace** (`lib/workspace-box.mjs`): the layer's own box (`boxArgv` with `prog: null`, a fresh `/tmp`, the pinned toolchains read-only, `--die-with-parent`) inside the loopback-only namespace of a holder started for the call, so there is no registry to fall back on (`/proc/net/dev` lists `lo` only). The registered snapshot is bound read-only at its own path; the writable copies are made inside the box before the command is `exec`ed, by a fixed script whose values are positional arguments.

| Stack | Bound read-only | Copied inside the box | Environment besides the layer's |
|---|---|---|---|
| C# | `<snap>/nuget`, `<snap>/dotnet` | `<snap>/dotnet` to `/tmp/.dotnet` (the CLI home) | `NUGET_PACKAGES=<snap>/nuget`, the build servers off, no first-run noise, certificate revocation offline |
| Node | `<snap>/npm` | `<snap>/npm` to `/tmp/npm` | `npm_config_cache=/tmp/npm` and the offline npm environment |

Measured facts behind it: the snapshot's `nuget` part works read-only as `NUGET_PACKAGES` (nothing written); the `dotnet` part must be in `DOTNET_CLI_HOME`, or each build ends with a stderr line about verifying workloads; npm reads tarballs from a read-only cache by integrity, but anything that reads a packument (a lock generation, `npm install`) fails with `ENOTCACHED` for a package that IS in the cache, so the whole `npm` part is copied (174 MB, about 1 s, on the box's tmpfs) and made writable. The copy is used for every Node command, the same writable cache the agent's box has. No host copy of a cache is ever made, and the snapshot is never changed (every test file re-hashes it once). The build environment sets no heap key (T-L14). The messages in the box are German (`LANG=de_DE.UTF-8`): the tests match codes (`NU1603`, `NU1101`, `ENOTCACHED`, `TS2322`) and package names, never sentences.

**The scoring box** is the layer's `runCases` on the built tree, unchanged, with no cache bound. The pinned runner 0.2.4 judges only olympiad (`run_samples.py` exits 5 for a daemon or web run: "part ii of this story"), so until runner part ii-b lands the tests show daemon and web with `--parse-only` and the test's own driver through the same chain (a client that retries the connect, SIGTERM to the program's host pid, exit `{ code: 0, signal: null }`). A daemon or web verdict from the runner waits for part ii-b.

**The builder** (`build-workspaces.mjs`, `lib/workspace-build.mjs`). `node spikes/lang-bench/build-workspaces.mjs --stack <csharp|node> --kind <olympiad|daemon|web> --task <name> --group <A|B> --out <dir> --statement <file> --samples <dir> [--task-repo <dir>] [--hello]`, plus `--make-locks [--force-locks] [--caches <dir>]` and `--print-stack-files`. Exit 0 done, 1 a box command failed, a git failure or timeout in the fetch, or a malformed `inputs/hashes-lang.txt`, 2 usage (a missing, unreadable or wrong flag, a statement that is not a file, samples that are not a folder: one line, no stack trace), 3 refused (a rule, a hash, a pin, a destination, a group). Everything is checked before the first write: the legal names, the group rule, the destination, the local runner against the pin, FORMAT's hash and the hash of its grammar, the packages list, and a `run.sh` that ends in `exec`. The workspace holds the template, the derived `PACKAGES.md`, the pinned `runner/*.py`, `runner/GRAMMAR.md` extracted from the group's FORMAT, `STATEMENT.md` and `samples/`. The builder adds no stack word, no plan word and no path of this host. Two refusals. The destination (`lib/workspace-destination.mjs`, shared with the bare copy): any path inside a git worktree is refused, whatever its state (clean or dirty, the folder itself or a path nested under it, existing or not, a main checkout or a linked worktree whose `.git` is a file); a git that cannot answer is a refusal too; so is a path with a `.git` component, and any folder above it that holds a `.git` entry by `lstat` or is a bare repository, and the same walk is made over the real path of the deepest existing part of the destination (a link into a repository is a way in); a bare repository is a `HEAD` that is a regular file reading as a git HEAD (a `ref:` line or a commit id) beside `objects` and `refs` folders, so three plain folders with those names, or a text file called `HEAD`, are not refused (the walk catches a linked worktree whose admin folder is gone, a path inside `.git/` and a bare repository, which `git rev-parse` calls "not a worktree"); a path the filesystem cannot answer for (permission, a link loop, a name too long) is a refusal that says "cannot be judged", not a throw; outside every repository, an existing folder must be empty and a link is refused. The workspace, and the bare copy of a task folder, are written by one helper into a unique sibling staging folder (made with an exclusive `mkdir`, and only a folder the call made is ever removed) and renamed onto the destination when complete, so a failure leaves no destination and no staging folder and a retry is not refused; parent folders made for the destination stay. The group rule (T-L57, D61): the group must be a row of FORMAT's record, and a task of another group is refused naming the task and both groups, before any folder exists; a task in no row is refused unless `--hello`. Phase B adds its own option for a dedicated worktree of the task repository (OP-2); phase A has none.

**The task repository** (`lib/task-repo.mjs`). `fetchTaskRepo` clones a LOCAL repository (an absolute path to an existing folder; a URL, a relative path and `file://` are refused: no fetch reaches the network) at `PINNED_TASK_SHA` into `<cache>/<sha>/` through a scratch folder, removes `.git`, verifies, and renames. A cache hit is verified and used; a mismatch is a failure naming the file, never a silent re-fetch. The verification (T-L1) has two rules: every entry of `inputs/hashes-lang.txt` exists with its bytes, and every file under `csharp/`, `node/`, `runner/` and `tasks/` is on the list (a link or other entry is `not a regular file`; a workspace root that is itself a link or a regular file is `not a regular file` too, never a throw); files outside these roots are checked only when listed. In phase A the list has four lines: the three `.gitkeep` files (the empty blob) and `todo/tasks/FORMAT.md`; phase B replaces the placeholders by the workspaces' files and moves the sha. `copyTaskFolder` (T-L2) copies exactly one `<stack>/<task>/` folder for the bare workspace (the task is one folder name, the destination passes the same guard, a link inside is refused, modes are kept).

**Coupling.** `build.sh` is the agent-visible contract. `templates/stack-files.json` is the push check's list of file patterns in a stack folder; `lib/push-inputs.mjs` already reads it, and it is derived by `stackFilePatterns()` (a test keeps file and function equal; when `rs_net.py` joins the pin, it turns red and the file is regenerated). `workspace-diff` now counts `build.sh`, `run.sh` and `PACKAGES.md` as each stack's own files. The pinned runner is not in the scan of the push check on purpose: twelve lines of `runner/*.py` hold plan ids that the `run-base-text` rule refuses; a separate small story removes them and re-pins the runner before the first D40 gate (OP-3).

**Measured** (2026-11-04, this host): a hello build in the build box takes about 3 s per stack and kind (C# olympiad 2.8 to 3.4 s, Node olympiad 3.0 to 3.3 s); the whole layer file set (`workspace-exec*.test.mjs`) runs in about 77 s; `--make-locks` takes about 8 s for the three locks.

**Input for E3's plan round (not a built rule; E3 decides).** *(Superseded by parent D65: the scorer scores as submitted, restores the frozen files and regenerates nothing; D65's reason is that regeneration could fail a correct program. The paragraph is kept as it was written; the nested `node_modules` limit in it is D65's own known limit.)* The rows above show that the command line holds against a plain property but not against every file or target a project can carry, and a pin that only compares bytes of the frozen files misses them. The following is offered as input. (1) Outside `tests/`, the scorer's copy holds no `Directory.Build.*`, `*.user`, `*.props`, `*.targets`, `*.rsp`, `.editorconfig` or `.globalconfig` that the template does not hold. (2) Do not refuse an agent for an edit nobody told it was illegal: the scorer regenerates `App.csproj` as the template plus the agent's package items (compared on parsed XML) and then builds, so a hidden suppression fails honestly and a harmless edit still passes. An edit the code really needs would fail too (for example `AllowUnsafeBlocks`; reasoned, not measured). (3) The Node peer: `package.json` is regenerated the same way, keeping only `dependencies` and `devDependencies`, because a `preinstall` script can change a frozen file during the build (measured by the team lead: `sed` on `tsconfig.json`, exit 0 with `const s: string = null;`); the frozen files are restored before the build, not only compared. (4) A known limit, left as it is because the frozen Node `build.sh` gets no `find`: a hand-made nested `node_modules` such as `src/node_modules/foo` passes in place and fails on the clean copy (exit 0, then TS2307 on the copy).

**A known limit (not changed here).** In the olympiad kind a listed package can still fail: adding `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14 to the olympiad `App.csproj` pulls `Microsoft.Extensions.Logging.Abstractions` 8.0.0, which needs `Microsoft.Extensions.DependencyInjection.Abstractions` >= 8.0.0; only 10.0.14 of that package is in the cache, so it is resolved instead and the build stops with `NU1603` (measured offline). The input is that one reference in the olympiad kind. The team lead measures every offered package in every kind at the Mvc.Testing supersede, where the recipe can change. A staging folder left by a killed build stays, as the fetch folder of OP-8 does.

**Findings.** (1) In the agent's box, `npm ci --offline` made one registry request, refused and logged, unless `npm_config_update_notifier=false` was set (the update check). Fixed in the fix round: the Node templates' `.npmrc` is `offline=true` and `update-notifier=false` (the constant `NPMRC_WORKSPACE`; `NPMRC_OFFLINE` of the snapshot stays as it was), and the agent-box test runs with no extra environment and asserts no refused request. (2) `RestoreLockedMode` is rejected for a reason that holds once a lock file exists: with `RestorePackagesWithLockFile` on, the first build writes `packages.lock.json`, and then a package added with `RestoreLockedMode` on fails with `NU1004`; two builds in a permanent case show this beside the passing shape. (3) `--die-with-parent` is not what ends a hung build when the timeout fires (the kill of the process group reaches the box's init); it is what ends the box when the process that started it is killed with SIGKILL and cannot clean up. A test kills that process alone: with the flag no sleep is left, without it the sleeps live on.

## Visible samples (E1.S6)

not built yet

## Specs, gates and the freeze (E1.S2)

not built yet

## Draft references and the pre-freeze tool (E1.S2a)

not built yet

## Process-arm templates and run bases (E1.S7)

not built yet

## Skeleton and the push path (E1.S1)

Built: the pre-push check, the credit terms, the needle list, `generateRunBaseTree` with `needleScan`, and `workspace-diff`. Not built here: the task
repository's README, adapters and research stubs (`chore/skeleton`, the team lead's text) and the pushes themselves (the team lead's, with the owner's go).

**The command.** `node spikes/lang-bench/push-check.mjs` never pushes and never writes to GitHub; its modes are
`--repo <task repo> --branch <b>` (the commits `git rev-list <b> --not --remotes=origin` would add), `--text <file>` (a PR or review body; with
`--self`, a body of a bench PR: the leak and secret scans only), `--self --branch <b>` (the bench's own branch: the leak and secret scans only) and
`--setup-remote --repo <task repo>` (origin set idempotently, `fetch --prune --no-tags` over HTTPS with gh's credential helper for that one command,
`origin/main` checked to hold `LICENSE`). Exit 0 clean, 1 refused, 3 environment (git, the network, an aid or a named secret file that cannot be
read, public plans unreadable at `origin/main`: the check never passes without them), 4 configuration (no such branch, an origin that is not the task
repository, a remote-tracking ref that is not what `git ls-remote` says now). A report line is `<commit> <path>:<line> <reason>`; it never carries the
text that matched (a secret in a file NAME is masked to `[secret]`). The report also prints `leak tokens N (source)`, the `tip` sha it checked and
`push git push <url> <tip>:refs/heads/<branch>`: **publish that sha, not the branch name**, so what was checked is what is published.

**The enforced push path (task repository only).** `push-check.mjs --install-hook --repo <task repo>` writes `<repo>/.git/hooks/pre-push` (where git
looks), a small POSIX `sh` script. For every ref of a push it reads git's stdin line and runs `node <this checker> --repo <repo> --branch <ref>
--tip <local sha>` (with `--origin-url` and any options given at install baked in), so the commit checked is exactly the commit pushed, and
`--tip` makes a branch that moved off that sha exit 4. It refuses the push when a check fails or the bench checkout cannot be found, and for
a ref outside `refs/heads` (a tag is checked by hand, then pushed with `--no-verify`); a deleted ref passes. Installing twice changes nothing; a
hook that is not ours is refused (exit 4) unless `--force-hook`, which keeps it as `pre-push.foreign`; `--uninstall-hook` removes only a hook
with our marker line (`# lang-v2-push-check-hook v1`). **It is never installed into the bench repository**: the bench is shared by other
sessions, so its rule stays the manual `push-check.mjs --self --branch <b>` run before a push (and `--text --self` for a body); the install
refuses a repository that holds `spikes/lang-bench/push-check.mjs` or whose origin is not the task repository. The team lead installs it in
`/home/jinx/git/dew_flow_ai_language_comparison` after review.

**What the hook cannot do (N5), and where to install it from.** The hook is **client-side only**: it runs on this machine for a person or agent who pushes with
git here; it does not stop `git push --no-verify`, a push from another clone or machine, or a write made through the GitHub API or web UI. That is inherent in a
git hook, so the rest of the defence stays what it is (the PAT without Workflows, rulesets, the review of every PR, the check on PR bodies). **Install it from
the MAIN bench checkout (`/home/jinx/git/dew_flow_benchmark`), once this story is merged and that checkout is updated, not from a worktree:** the hook bakes the absolute
path of the `push-check.mjs` it was installed from, and a worktree is removed when its branch is done, which would leave a hook that refuses every push (it fails closed:
"bench checkout not found").

**The named checks** (one line each): `leakcheck` (six-word shingles of the two private aids, read from `~/rsd/lang-v2/test-design/` at run time,
and the literal tokens of `leak-tokens.txt` in that folder when it is present, or of `--tokens`), `secrets` (named shapes: fine-grained token,
`gh[pousr]_`, Anthropic key, any `BEGIN … PRIVATE KEY`, AWS key id; and the values of the two real secret files `~/.config/lang-v2/github-pat` and
`deploy_key_lang_v2`, or of `--secret-file`), `attribution` (the README closes with one `## Attribution` section; no link, known-task needle or plan id
elsewhere in it), `run-base-text` (links and the D18 needles in every file's text and path, `.gitmodules` excepted and pin files excepted from links;
plan, probe, gate and decision ids in `csharp/ node/ tasks/ runner/ todo/tasks/` and the README), `family-names` (`dew_flow_*` other than the
repository's own and `PLAN_lang_comparison`, in every file's text and path at the tip, in commit messages, `.gitmodules` excepted),
`known-task-credits` (a known-task text — the spec, or a task folder under `tasks/ csharp/ node/`, anywhere in the range or anywhere in the tip tree — needs its entry
in `inputs/credits-lang.json` and, in the README's attribution section outside HTML comments (an unclosed `<!--` hides the rest of the README, as CommonMark renders it), its credit as a substring, the CC BY-NC-SA 4.0
sentence naming both files, and RealWorld's MIT notice), `stack-folders` (a `.gitkeep` is the empty blob only, and only at the root of `csharp/` and
`node/` until E1.S3 writes `templates/stack-files.json`, a JSON list of path patterns with `<t>` for the task folder) and `tree-shape` (no symlink,
no submodule but `.agents/conventions`, no `.gitmodules` path or URL but the conventions repository's, no file with a NUL byte in a task repository;
UTF-16 is decoded and other binaries are read as latin1 for every scan).

**The whole tip tree, bounded and visible.** `stack-folders`, the symlink rule and the credit terms read the whole tip tree (`scan.tree`), not only the
range's files, because a run base carries the whole tree. `verifyFresh` compares the local `refs/remotes/origin/*` with `git ls-remote --heads origin`
both ways (a ref the remote lacks or has elsewhere, and a remote branch never fetched). Every external process goes through the one shared launcher
(`runBounded`: exe + argv, timeout, process-tree kill; `input` and `bytes` options added for the archive extraction and the byte reads); the blobs of one
check are read by one `git cat-file --batch` under an aggregate ceiling (256 MiB, one blob 16 MiB; exit 3 beyond it). The command prints progress
(`push-check: scanning N commits`, `reading N blobs`, ...) on stderr; stdout is the report.

**What is new, in one rule.** The published base is the remote ref of the branch being updated (`--dest`, which the hook passes from the pushed remote ref), otherwise
`origin/main`, never "any remote ref". The commits of a check are `git rev-list <tip> --not <base>` (their messages in one `git log --no-walk --stdin`), and the blobs it
scans and counts are exactly `git rev-list --objects <tip> --not <base>`: every parent of every merge is covered, and only what the destination already holds is skipped (a merge
of `origin/main` into an older branch is not charged for main's content; a blob that only another branch holds is scanned when a new branch adds it). The batch readers work in chunks
of at most 500 objects with the 16 MiB and 256 MiB ceilings kept across the chunks. `workspace-diff` treats only `ENOENT` as an empty folder: any other read error is a finding.

**Tags.** `push-check.mjs --tag <name> --repo <task repo>` is what the freeze procedure runs BEFORE it pushes a tag (a tag is pushed alone, the hook refuses a ref outside `refs/heads`,
and no branch check reads a tag): an annotated tag's message gets the task-repository body rules (leak, secrets, needles, family names; never printed), and the commit the tag points at
(annotated or lightweight) must be an ancestor of, or equal to, a commit the remote has NOW - `git ls-remote --heads --tags origin`, never the local tracking refs, which go stale when the remote
moves or deletes a branch - so it was checked when its branch was pushed; otherwise `tag-target` refuses it, naming the commit. A current remote commit this clone lacks cannot be tested: when nothing
present holds the tag's commit and some are absent, the finding says "fetch first" (the check never fetches for you); a failing `ls-remote` is exit 3. Then the tag is pushed with
`git push --no-verify` on the team lead's word.

**Residuals the reviewers accepted (named so nobody mistakes them for coverage).**
- `--dest <other>` trusts the other branch's history: the base is that branch's remote ref, so what it holds is not scanned again. A wrong or omitted `--dest` therefore changes what is
  "new"; the hook passes the pushed remote ref itself, and by hand `--dest` must name the branch really being updated.
- `--self` freshness: the bench's own check compares against the bench's `origin/main` as fetched; it does not run the task repository's freshness check against `ls-remote`, does not
  run the placement rules and does not refuse binaries. A stale local `origin/main` makes it read more or less than the push adds; fetch before running it.
- The own-value scan finds a real secret's literal text and its base64 forms (full, and the three alignments). It does not find a base64 of anything that is not one of the two real secret
  files, a JSON-escaped spelling (`\/`, `\u00xx`), or a partial token (a prefix or a few characters of it): those are covered only by the secret SHAPES, which need the whole token.

**What reads what.** History is published, so the leak and secret scans read every commit and every message. A run base carries the tip tree and
never the history, so every other rule reads the files as they stand at the tip (credited to the commit that brought the blob). Needles match by
D18's rule (change 4.1): case-sensitive, on word boundaries with ASCII letters and digits as the word characters, plus exact substrings
(`inputs/needles-lang.json`, the public list); a multi-word needle is matched over the whole text with any whitespace between its words. Shingles
tokenise like v1's `normalise` and drop nothing, and **no window is exempt for lying inside one whitespace token**: a passage joined with `/` or `-`
is one token and still a hit (the risk consult's D52(3) closed the earlier "one-token" exemption, which was a bypass). A path or URL is not a hit only
because the approved public texts hold its words. A finding says how many tokens its window spans (`[window spans N tokens]`), never which words. In the bench's own branch check (`--self`, and a bench body) a six-word run that the approved public texts already hold is not an aid hit. **In the task
repository (a branch, and a task PR body: `--text` without `--self`) only a window that spans at most two whitespace tokens and that the public texts hold
verbatim is exempt** (a path, a URL, the README's bench pointer): an aid sentence that also stands in a public plan is a hit there (re-review N2). The **approved public texts** are an explicit, reviewed set (D52(3)): the two plan files and the files named by
`spikes/lang-bench/inputs/public-texts.json` (schema 1, a `texts` list; absent at the ref means no extras), read at `origin/main` of the bench
repository with one `git cat-file --batch` per stage, never from the working tree and never "every markdown file" (a private paragraph spread over
two unlisted public files stays a hit); an unreadable text, an invalid list or a missing listed file is exit 3 (`--public <file>` names the public texts
instead, in tests); an aid left with no run of its own is exit 3, because it would check nothing.

**The credit terms** (`inputs/credits-lang.json`, the owner's of 2026-11-01): CSES problem pages and licence page read on that date, RealWorld's
notice copied verbatim from its `LICENSE` as it stood then (two copyright lines), the protocol page, the olympiad's statistics page for task 09. The
`credit` lines are checked as substrings of the README's squashed attribution section; the README sentence of task 09 ("… is based on IOI 2018
Werewolf (…)") carries the recorded credit.

**The run base's tree.** `lib/run-base-tree.mjs`: `generateRunBaseTree({ repo, ref, out, needles })` unpacks `git archive <ref>` into an empty
folder outside every worktree, cuts the README from `## Attribution` to the end (a README without the section, with it twice, or with a section after
it is refused), removes credit files (`CREDITS*`, `credits*`, `ATTRIBUTION*`) and runs `needleScan` over every entry; any hit (a symlink, a binary and
a file name count) refuses the cut, removes the folder and names `path:line needle`. E1.S7 commits that tree as the orphan run base.
`workspace-diff.mjs --root <checkout>`: per task, `csharp/<t>` and `node/<t>` hold the same files byte for byte except each stack's own pin files
(C#: `global.json`, `nuget.config`, `Directory.Build.props`, `*.csproj`; Node: `package.json`, `package-lock.json`, `tsconfig.json`, `.npmrc`,
`eslint.config.mjs`) and the one line after `<!-- stack-sentence -->` and `<!-- framework-sentence -->` in `STATEMENT.md`.

**Tests.** `node spikes/lang-bench/run-tests.mjs 'test/push-*.test.mjs' test/run-base-tree.test.mjs test/workspace-diff.test.mjs`. Every test builds
throw-away repositories with a bare `origin` in owned folders (`test/support/push-fixtures.mjs`) and a synthetic aid; no test reads the real aids,
the real secret files or the network.
