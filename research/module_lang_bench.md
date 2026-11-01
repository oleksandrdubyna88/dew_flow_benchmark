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
`exit-code`, no `child-pid` before the launcher exits (or within 60 s), or a cgroup that does not exist at `child-pid`, is `LaunchFailed`: exit 3 `launch failed` with the
bounded tail (2 KiB) of the prefix's standard error, never a case verdict. After `child-pid` everything is the case (an immediate exit is an ordinary end; junk lines are
ignored). `child-pid` is the launch-success signal and **the start of the case clock**, not the pid authority; `exit-code` is the program's status (without one the
launcher's own is used). *bwrap writes `child-pid` right after it clones the sandbox, BEFORE the box's mounts are made, so a few milliseconds of box set-up are inside the case
clock (the numbers measured for the plan stay as they are); the plan fixes the start at `child-pid` and it is not moved.* **`--cgroup-of TEMPLATE`** (B1, B4; `{case}` filled
in) names the program's cgroup2 directory. With a launch prefix it needs `--launch-status` (exit 4). The runner owns what it measures and kills: before the spawn the
directory must not exist or must hold no process (else exit 3: it never measures or kills a cgroup this case does not own). It reads `cgroup.procs`, `memory.peak` and
`memory.events` after the program's end and BEFORE any kill, then ALWAYS writes `1` to `cgroup.kill` (a leftover found or not) and waits until `cgroup.procs` is empty (2 s
ceiling, else exit 5); where the cgroup is missing or the write fails the launcher's process group is killed as well. **The rule for the program's pid (B1, P4):** a bwrap
process is known by IDENTITY (`/proc/<pid>/exe` is the file, by device and inode, of the bwrap binary the prefix names; a copy of bash named bwrap is not one, and `comm` is
never used); the program is the process of `cgroup.procs` whose PARENT is a bwrap process (`bash run.sh` execs it: D38), the launcher pid is never a candidate, a short tool
bash starts has bash for a parent. Host pids only; a pid seen inside the box is never used; the pidfd is checked against the cgroup and the bwrap parent after it is taken
(a recycled pid is neither). The look is part of the pump's loop (every millisecond until the program shows, then every 20 ms), so the program's stdin and stdout flow from
`child-pid` on. Two candidates with a bwrap parent are `fork` and no pid is chosen. **`fork`** is judged over the whole case from the processes that are not bwrap, not the
launcher, not the wrapper and not the program: the most seen in two successive looks, and what a leftover read (repeated 20 ms later, so a process caught exiting is not one)
finds after the exit; it is `known: false` when no pid was settled, and it never decides the pid. **`--wrapper-pid-file TEMPLATE`** (with `--cgroup-of`; `{case}` filled in) is
the file in which the layer's wrapper writes its pid; without the flag nothing is excluded as a wrapper (E1.S8b's delegated scope has none; its fallback `scope-wrap.sh`
must pass the flag). `oom_kill` above 0 in `memory.events` is the verdict `memory limit` whatever the exit status and is read from `memory.events` alone, even when
`memory.peak` cannot be read; it is judged before `time limit`. **`--case-view TEMPLATE`** (B7; `{case}` filled in) names the case's fresh writable view of the built tree,
which the layer made before the case starts (a missing view is exit 3): the program starts in it, a scenario's `.scenario/` fixtures are written into it first (every fixture
path is relative, free of `..`, and made one component at a time with `O_NOFOLLOW`, so a link on the way refuses it; a `.scenario` already there is refused), and the samples
directory may not lie in a view nor a view in the samples (exit 4). **With more than one case, a `--cgroup-of`, `--case-view` or `--wrapper-pid-file` template without `{case}` is
exit 4.** Nothing is read back from a view, and the runner never assumes the filesystem persists from one case to the next.

*Host prerequisites (P1).* At the start of a run the host must be Linux 5.14 or newer and have a working `os.pidfd_open`, and cgroup v2 at `/sys/fs/cgroup` when `--cgroup-of`
is given; each missing one is named in an exit 3, never a crash.

*The clock and the waits (B6, P3).* One clock, `time.monotonic_ns`. The case clock starts at the instant `child-pid` is read (at the spawn without `--launch-status`). The end of a
case is the instant of the FIRST of: the program's own pidfd turning readable (a grandchild behind a box, so `Popen.wait` could not do it), bwrap's `exit-code` record being
read from the status pipe; the launcher's own exit is never program time (without a box the launcher is the program). With neither a pid nor a record the case still ends
when the launcher goes, but its wall is unknown (`wallMs: { known: false }`) and the verdict comes from the output and the deadline kill only. Every wait is `os.pidfd_open` +
`select` with the exact remaining time; `Popen.wait(timeout=…)` is not used (a scan test says so). The wall rule, in nanoseconds: a wall over the limit is `time limit` even when
the program exited by itself, exactly the limit passes, one millisecond over does not. Once per run, before the cases, an empty program (`bash -c :`) is timed through the same
prefix, clock and pump (`emptyProgramMs`, and `emptyProgram: { ms, launchMs, pid, found, comm }` says which pid it waited on); its `{case}` is `empty-program`, so a layer whose
prefix binds a per-case view or cgroup must provide one by that name. **V11:** the program's environment is a copy of the runner's own, plus (for the daemon and web kinds,
part ii-b) `PORT` and the scenario's `env` lines; nothing else is added or removed (the layer sets it; the runner does not sanitise it).

*Being stopped.* SIGTERM, SIGINT and SIGHUP stop the runner with exit 5 and no verdict. The stopping signals are held off while a launcher is spawned and the launcher is
registered the moment it exists, so every launcher still alive is stopped (`cgroup.kill`, or the group on the plain path, and never a group whose leader was already reaped)
whatever instruction the signal lands on; the pump is made inside the `try`.

**Exit codes.** `0` every case passed (or `--parse-only` found no problem) · `1` a case failed, a failing build counts · `3` the environment (no workspace, no `bash`, a
host that lacks a prerequisite, a case view that is missing, a cgroup that is not this case's, **a launch that failed**) · `4` configuration: usage, a malformed sample (file and
line), a version 1 file, a script/transcript mismatch, a bad limits file or `cases.txt`, the layer options that contradict each other · `5` the runner itself failed (a traceback,
a daemon/web run before part ii-b, a process of the case that could not be killed, a launcher that could not be stopped, a stop by a signal).

**Result format.** `--json OUT` writes `{ format: 1, runner, grammar: 2, kind, build, launch: { failed, stderr }, emptyProgramMs, emptyProgram, cases: [{ id, status, reason,
rIds, entries, firstMismatch, lifecycle: { stop, signalled: { signal, pid, found, comm, launcher }, fork, leftover, leftoverEvidence, kill, memory, stderr, capture }, wallMs,
launchMs }], python }` (runner version 0.2.0); what was not measured is `{ known: false, why }`, never zero. `wallMs` is the program's wall from the case clock (or unknown),
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

not built yet

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

not built yet
