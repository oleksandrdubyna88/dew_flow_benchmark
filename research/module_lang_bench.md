# Language comparison v2 — the bench module (`spikes/lang-bench`)

> One `##` section per E1 story; each story writes only under its own heading (`todo/PLAN_lang_comparison_v2_E1.md` § 3, "Conventions for every story").

## The runner (E1.S8a)

**Purpose.** `runner/` is the one Python standard-library program the agent, CI, the pre-freeze gate and E2's held-out scoring share (D37). It
reads a folder of `<id>.in` / `<id>.out` pairs, builds with `bash build.sh`, runs `bash run.sh` per case and reports `PASS` / `FAIL` with the
first mismatch under FORMAT v2's rules (grammar 2). It knows nothing of boxes, scopes or namespaces: the execution layer hands it a launch prefix.
**Built so far: part i** (olympiad runs, parsing and validation of daemon and web samples, the matcher, the report, the pin). **Part ii** (process
control by host pid, sockets, the daemon and web runs, signals, the cgroup path) is not built; a daemon or web run ends with exit 5 and says so.

**Files** (`spikes/lang-bench/`):

| File | Role |
|---|---|
| `runner/run_samples.py` | the entry: arguments, kind, validation of every pair, build, olympiad cases, report, exit codes |
| `runner/rs_lex.py` | lines, tokens, durations, strings and escapes, variables, normalised fields; problems are `(file, line, message)` |
| `runner/rs_model.py`, `rs_script.py`, `rs_check.py` | the `.in` parser into frozen dataclasses, its validation, the entries a script produces |
| `runner/rs_transcript.py`, `rs_pair.py`, `rs_assign.py` | the `.out` parser, script/transcript correspondence, one-to-one assignment |
| `runner/rs_json.py`, `rs_fields.py`, `rs_jmatch.py`, `rs_match.py` | exact-decimal JSON, fields and the backtracking matcher, JSON bodies with `$unordered`, entries and V6 checks |
| `runner/rs_exec.py`, `rs_olympiad.py` | the build, the launch argv, the pidfd wait, the lifecycle seam; the streaming olympiad case |
| `runner/rs_samples.py`, `rs_report.py` | pairs, `cases.txt`, limits; report lines and the JSON result |
| `pin-runner.mjs`, `lib/runner-pin.mjs` | the pin: writes `inputs/specs.d/runner.json` (`runner: { version, files, manifestSha256 }`), `--check` |
| `specs-record.mjs`, `lib/specs-record.mjs` | assembles `inputs/specs.json` from the writers' files in `inputs/specs.d/`; `--check`; never edit `specs.json` by hand |

**CLI.** `python3 -I runner/run_samples.py [--workspace DIR] [--kind olympiad|daemon|web] [--no-build] [--limits FILE] [--launch-prefix JSON]
[--json OUT] [--only ID] [--parse-only] SAMPLES_DIR`. The kind is read from the `Kind` row of the workspace's `STATEMENT.md` unless `--kind` is given.
`--limits` is a JSON object with `timeS`, `readyS`, `graceS` (exact seconds, from 1 to 600; unknown keys refused); without it the olympiad limit is the
floor (1 s) and every time verdict says `against the floor`. `--launch-prefix` is a JSON argv put before `bash run.sh`, `{case}` replaced.
`--parse-only` parses and validates every pair and starts nothing. A `cases.txt` beside the pairs attaches its R-ids to each case. Part ii's
`--cgroup-of`, `--launch-status` and `--case-view` are **refused with exit 4** until they exist, never ignored. Without a cgroup every case says
`memory: not certified`.

**Exit codes.** `0` every case passed (or `--parse-only` found no problem) · `1` a case failed, a failing build counts · `3` the environment (no
workspace, no `bash`) · `4` configuration: usage, a malformed sample (file and line), a version 1 file, a script/transcript mismatch, a bad limits
file or `cases.txt` · `5` the runner itself failed (a traceback, or a daemon/web run before part ii).

**Result format.** `--json OUT` writes `{ format: 1, runner, grammar: 2, kind, build, cases: [{ id, status, reason, rIds, entries, firstMismatch,
lifecycle: { stop, leftover, leftoverEvidence, memory, stderr }, wallMs }], python }`; what was not measured is `{ known: false, why }`, never zero.

**Bounds.** An olympiad case's stdout is compared as a stream and stops at the first difference; at most 16 MiB of it is kept for the report, and a
truncated capture is never what decides a `PASS`; stderr goes to a file (killed past 256 MiB); a case is cut off at its limit and the group is
killed; a wall time over the limit is `time limit` even when the program exited by itself; the build has a 600 s ceiling; a transcript's
`unordered` group holds 2 to 64 entries; a regex or field match is cut off after 500,000 steps; a string expression expands to at most 200 parts.

**Encoding (B10).** `python3 -I` ignores `PYTHONIOENCODING` and `PYTHONUTF8`, so the entry reconfigures stdout and stderr to UTF-8 with
`backslashreplace`; every file is read as bytes; a file name that is not UTF-8 is printed as `\xHH`; an invalid-UTF-8 JSON body is the verdict `bad-utf8`.

**Seams for part ii.** `rs_exec.PlainLifecycle` (`memory`, `kill`, `leftover`) is what a scope-based lifecycle replaces through `Config.lifecycle`; the
pidfd wait is `rs_exec.wait_exit`; `run_samples._after_parsing` is where daemon and web dispatch; `rs_transcript.Actual` and `rs_match.compare` take the
observations part ii records; `Actual.step_line` and `step_text` feed the report's `step` line; `rs_script.Step` carries what each step needs.
The tests are in [module_tests.md](module_tests.md), section "The language-comparison runner scenarios".

## The execution layer (E1.S8b)

not built yet

## FORMAT v2 (E1.S9)

not built yet

## The conformance check (E1.S8)

not built yet

## Toolchains and the preflight (E1.S5)

not built yet

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
