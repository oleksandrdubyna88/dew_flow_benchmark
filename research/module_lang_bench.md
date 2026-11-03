# Language comparison v2 — the bench module (`spikes/lang-bench`)

> One `##` section per E1 story; each story writes only under its own heading (`todo/PLAN_lang_comparison_v2_E1.md` § 3, "Conventions for every story").

## The runner (E1.S8a)

**Purpose.** `runner/` is the one Python standard-library program the agent, CI, the pre-freeze gate and E2's held-out scoring share (D37). It
reads a folder of `<id>.in` / `<id>.out` pairs, builds with `bash build.sh`, runs `bash run.sh` per case and reports `PASS` / `FAIL` with the
first mismatch under FORMAT v2's rules (grammar 2). It knows nothing of boxes, scopes or namespaces: the execution layer hands it a launch prefix.
**Built so far: part i** (olympiad runs, parsing and validation of daemon and web samples, the matcher, the report, the pin) and **part ii-a** (what the
olympiad path needs from the execution layer: the launch prefix and status, the case clock, the pidfd waits, the cgroup read and kill, the case view, the
program's environment). **Part ii-b** (sockets, the daemon and web runs, readiness, `signal TERM` to the program's host pid, V3, V5, V7, `overflow <conn>`)
is not built; a daemon or web run ends with exit 5 and says so.

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
process counts for `fork` only when it has been in the cgroup for 100 ms (looks at least 100 ms apart), or is still alive at the program's end (the leftover read). **A property of the rule, measured and not tuned around:** under a CPU load a forked short tool that the scheduler does not run for 100 ms IS a process that stayed 100 ms, so a loaded host can print a false `fork:` line — about 1 case in 160 with every core burning; the test asserts the rate (at most 2 false forks in 20 rounds under half the cores burning, a false red about once in 3000 runs) and a broken rule, which counts a second process in every round, is red. The end of the
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

**The probe's stop, its end and the run marker (fix batches 4 to 9).** A SIGINT or SIGTERM makes the probe say `exec-probe: stopping — cleaning up N open run(s)` at once, release its record placeholder, end every child it started (`trackChild`: the C# build, the driver, the server; the build box also has `--die-with-parent`) and every open run (scopes, holder, views), and say when it ended. From that moment the layer starts nothing: `launchPrefix`, a new run folder, a build and a program tree refuse (`stopping.mjs`), and `scope-entry.sh` fails when its run folder is gone (it never recreates it). The exit is the signal's own code (130, 143) ONLY for a cleanup AND an owned-folders close that both finished with nothing left; a cleanup that hits its 20 s bound, leaves something, or a close that hangs or rejects is **exit 75** with the runs, scopes, holders and views still there named. **A cleanup that leaves something keeps the run folder and its claim** (`cleanup.mjs`: the `run` step is skipped and the folder is reported as kept), because the next start's sweep finds a run only through its folder. The timeout timer is not unref'd, so a stuck cleanup still reaches 75; a failure of the stop itself is one stderr line and 75. Every refusal inside the probe's `try` is thrown (`Refusal`) so the `finally` closes the owned folders. **The end of a probe — the record is the LAST write (S1, K1–K4).** In order: (1) the **stamp lock** `inputs/.stamp.lock` (`stamp-lock.mjs`). Its content is `<pid> <start time>`, and a second line `committing` once the holder is about to commit (written through the same temporary file and a rename, so a reader sees the old or the new lock, never a partial one) (the start time is `/proc/<pid>/stat` field 22 as `owned.mjs`'s `processProbe` reads it); it is created by `link()` of a COMPLETE temporary file `inputs/.exec-probe-tmp/.stamp.lock.link-<pid>-<start>`, so a reader never sees it empty, and EEXIST means held. A holder is **live** when its pid exists AND its start time is the one in the lock (a reused pid is stale); a holder that cannot be judged is live; a live holder refuses the probe (its record says exit 2, naming the holder's pid and the lock path). **A stale lock — a pid that is gone or reused, or a lock that cannot be parsed — is NEVER removed, moved or taken over by a probe** (D1: every round of an automatic retire opened a new interleaving, so there is none). The probe refuses: its record says exit 2 and `stampError` names the lock path and the dead holder, and stderr says the exact manual step: "a probe that was killed left the stamp lock `<path>` (pid `<p>`, start time `<t>`, now gone). If no probe is running, remove it: `rm <path>`". A stale lock whose second line says `committing` (its holder had reached the commit) adds on stderr that the pins MAY be stamped without a record (a SIGKILL between the commit and the record's rename), names the layer pin's `probedOn` (read only then) and says what to do: check `git diff -- spikes/lang-bench/inputs/specs.json spikes/lang-bench/inputs/specs.d/exec-layer.json`; if the pins name a probe with no record, run the probe again (it re-stamps them) or restore them with `git checkout -- <pins>`. **Only the holder ever changes the lock** (`markCommitting`: a temporary file renamed over its OWN lock; `release`: its own lock only), so a check followed by a rename has no other writer to race. The lock's content is compared through `parse` (pid, start time, the `committing` flag), never as a string (a holder's lock written with a trailing newline is still its own). N probes started together against one stale lock: none stamps, every record says 2, the lock is unchanged; N probes with no lock: exactly one stamps (`exec-probe-stamp-many.test.mjs`, real processes). Before its commit a holder checks that the lock is still ITS content (`holdsMine`); if the lock was removed by hand and taken by another probe, the record says exit 2 ("the stamp lock was lost") and nothing is committed. A `take()` whose temporary file cannot be removed after a successful `link()` still holds the lock. **Every temporary file lives in a `.exec-probe-tmp/` folder BESIDE its target** (same file system, so the rename still works): `inputs/.exec-probe-tmp/` (the lock's link file), `inputs/specs.d/.exec-probe-tmp/` and `inputs/.exec-probe-tmp/` (the two pins' `<pin>.stamp-<pid>-<start>` and `<pin>.restore-<pid>-<start>`), and `<out dir>/.exec-probe-tmp/` (the record's `<record>.record-<pid>-<start>`); the name carries the owner's pid and start time (`tmp-files.mjs`), and a tmp folder is removed when it is empty (`specs.d` must hold no stray folder). **At the probe's start** the command sweeps those folders explicitly, in its start sequence AFTER `guardStreams` and the stop handlers are installed (`strays.mjs`): it lists ONLY the `.exec-probe-tmp/` folders beside `inputs/`, `inputs/specs.d/` and the `--out` folder — never `inputs/` itself or a shared `--out` folder — and removes the files whose owner is dead (the pid is gone, or alive with another start time: pid reuse); a live probe's files and every file outside a tmp folder are never touched. The folder of the pins and records is `--inputs-dir <folder>` (a seam: the tests never sweep the real `inputs/`). An EPIPE on stdout or stderr (`| head`) is ignored (`guardStreams`), so a closed pipe never turns the record's 0 into exit 1. `release()` removes the lock only when its content is still the probe's own. (2) the stamp PREPARED (`probedOn` and `host` in `specs.d/exec-layer.json` and the re-assembled `specs.json`) in temporary files in a `.exec-probe-tmp/` folder beside each pin, assembled from a staged copy of ONLY the files `assemble` reads; (3) the stamp COMMITTED (two renames; if the second fails the first pin is put back; the outcome names exactly which pin is stamped, and a failed put-back is never called "both stay as they were"); (4) the record written ONCE with its final content to the temporary file `<out dir>/.exec-probe-tmp/<record>.record-<pid>-<start>` and renamed over the placeholder (no cut-off dated record; the writer itself refuses a missing reservation or a placeholder that is not empty, T-L53); (5) only then `onWritten`, the reservation is taken, the report printed, and the **lock is released LAST**; the exit is the record's code. **Which failure is what:** every failure of (1) to (3) — a lock that is held, unparsable, or cannot be created (EACCES, ENOSPC), a prepare or a commit that fails — is a RECORD that says exit 2 with `stampError` naming the lock path or the pin and the error (the file never says 0 when the command says 2); the holder's pid and the lock path are on stderr. A failure of (4) is stderr lines and exit 2 with no record: both pins that are stamped are put back from their saved contents (also after a failed commit: the prepared stamp and the pins still stamped are kept for this), the placeholder is released, and every pin that could not be put back is named with "it points at a probe with NO record"; the messages come from the real state. A lock release that fails is ONE stderr line and the exit stays the record's code. A SIGINT or SIGTERM after the record is written only closes the owned folders and exits with the record's code (a close that hangs or fails is one stderr line naming what may be left); a real SIGTERM after the record is tested through the command's own wiring (`wiring.mjs`: `endArguments`, `stopArguments`, `installStopHandlers`). A cleanup of a run already in flight is not started twice (the second caller shares the first one's report). A run stays named in the layer's registry until its cleanup finished clean, so the exit-75 message of a hung cleanup names its id, scope units and folder (S2); the run folder is reported as kept only when it still exists. A child's owned marker (`…-probedriver-<id>`) is made by the layer's naming helper `childMarkerOf`, never by `replace` on the holder's name. The token guard of the tests finds every form of a token binding (S3), and the suite start's sweep is shown to retire what a killed probe test really leaves (a `private-<id>` root with runs inside).
One token only: every test and tool uses the registered token `bench`; the tests tell their units apart by the ids of their runs, in a private temp root and runs parent per test, and the suite start calls the conventions' `sweepLeftovers({ token: 'bench' })`.

**A note for CP2 — a loaded data host can print a false `fork:` line.** The `fork` flag rests on the 100 ms rule above: with the host's cores all busy, about 1 case in 160 can show a forked short tool as having stayed 100 ms. The line goes to the blind adjudication like every flag; it is not a verdict.

**A note for CP2 — the holder's ready limit under load.** The netns holder must be ready within 5000 ms. It failed once on a loaded host: `inputs/exec-probe-2026-11-02-3.json` (layer `ef97f03d…`) ended exit 3 `memory-view-unobserved` because the holder was not ready in time while other work ran; it is kept as evidence and never overwritten (T-L53). The same layer passed on a quiet host (`-4`, exit 0). A data run on a loaded host can meet the same limit: CP2 should say whether 5000 ms is enough for the series host under its worst expected load. The records `-2` (layer `52c80acb…`), `-4`, `-5` (`71e8b99a…`) and `-6` (`35fb2317…`) stay as the earlier passing records of earlier layers. `-7` (layer `730f8fee…`, runner 0.2.4) is the record of the layer before fix batch 5. `inputs/exec-probe-2026-11-03.json` (layer `aa5a4471…`, runner 0.2.4) is the record of the layer before fix batch 6. `inputs/exec-probe-2026-11-03-2.json` (layer `3e120252…`, runner 0.2.4) is the record of the layer before fix batch 7. **The record of the FINAL layer is `inputs/exec-probe-2026-11-03-3.json` (layer `2cba87c276de32401d451be1ff8585a588a9ec705f71f103511b93b97fa1c6ad`, runner 0.2.4, exit 0).**

**The identity and its invalidation rule.** `identity.mjs` hashes every file of `lib/exec-layer/` (`scope-entry.sh` included), the memory policy file AND every module outside the layer that the layer imports, found by walking the static imports (v1's `bwrap.mjs` builds the box's argv, `run.mjs` launches every child, `find-on-path.mjs`, `result.mjs`, `preflight-box.mjs`; a test finds an unlisted import); `runCases` puts the live `treeSha256` in its result and a DATA run refuses (exit 3, `layer-identity-mismatch`) when it differs from the pinned identity; `pin-exec-layer.mjs` writes it to `inputs/specs.d/exec-layer.json`
(`treeSha256`, and `probedOn` and `host` after a passing probe) and `specs-record.mjs` assembles `inputs/specs.json`. A change to ANY file of the tree, of a module it imports or of `inputs/specs.d/memory-policy.json` (parent §6.1: the series' one memory policy) changes the identity and re-dates every gate and
limit that ran on the layer (parent §6.1: E1's D40 gate, E2's validation and calibration, E3's scoring). The probe's files are outside the tree, so a probe change does not.

**Never** run `dotnet build` (or any SDK command) outside the box with `DOTNET_ROOT` on the sealed SDK: it writes a `metadata` folder into the SDK's root and breaks the toolchain store's seal and tree
hash (2026-11-02). The tests and the probe build their C# programs inside the box, where the SDK is read-only.

## FORMAT v2 (E1.S9)

not built yet

## The conformance check (E1.S8)

not built yet

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

not built yet

## Workspaces (E1.S3)

not built yet

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
