# Task 02 — Echo line

| | |
|---|---|
| Kind | a long-running program that serves until it is stopped by SIGTERM |
| Gate verdict | not yet gated |
| Freeze tag | none yet |
| Freeze rule | this text changes only by a dated amendment from its authors |
| Amendments | none yet |

Every numbered sentence (**R1** … **R16**) is a requirement. Hidden tests refer to these numbers; nothing outside them
is tested.

## 1. What the program does

**R1.** The program reads a line from a client and answers with the line `pong` when the line is `ping`.

## 2. Start, readiness and stop

**R2.** The environment variable `PORT` holds a TCP port number; the program listens on it, and the tests connect to
it at 127.0.0.1.

**R3.** The program is ready when it accepts a TCP connection on that port. A test waits at most 10 seconds for
readiness; a program that is not ready by then fails that test.

**R4.** A test that does not end at the end of its input ends by sending the signal SIGTERM to the program. The
program must then exit with status 0 within 5 seconds, leaving no process it started still running. A program still
running after 5 seconds is killed and fails that test, and so does one that exits with another status or leaves a
process running.

**R5.** A test may instead end at the end of its input: it closes the program's standard input, and the program then
does what R1 says and must exit with status 0 within 5 seconds of the end of its input, leaving no process it started
still running. A program still running after 5 seconds is killed and fails that test, and so does one that exits with
another status or leaves a process running.

## 3. Interface

**R6.** A request or input that violates R1 does not occur in any visible or hidden test. What the program does with
it is unspecified and is never tested.

**R7.** Standard error is not compared and may hold anything; it is recorded, not judged. Only its size is bounded: a
run that writes more than 256 MiB to standard error is stopped, and that test fails.

**R8.** Standard output is compared only where a requirement of this spec says what it holds; otherwise it is
recorded, not judged.

**R9.** Every reply is compared with the expected reply byte for byte, except a field that a requirement of this spec
says is compared otherwise. Nothing else is normalized.

## 4. Limits

**R10.** Each test starts the program once, in a fresh process, and drives one scenario against it under a bound on
the program's memory. The exact bound is registered when the hidden suite is calibrated against reference solutions in
both stacks, before any session, and is given to the test runner, not to the program's author. The registered bound is
never below a floor of **256 MiB**. (The registration rule: at least twice the peak memory of the larger reference over
all the task's load scenarios.) The load scenarios are hidden scenarios of up to 50 connections, each lasting at most
30 seconds; the bound on memory holds in every test, whether it is a load scenario or not. Memory is measured as the
peak memory of the program's own control group (cgroup `memory.peak`), which counts the program and every process it
starts, including the page cache and kernel memory charged to them, with no swap. The program's memory reaches the
limit when the system kills a process of the program's control group at the limit, or when the program's own runtime
stops it for lack of memory; either way the run is stopped and fails that test. The test machine caps each runtime's
own heap at 90 % of the limit, by that runtime's own setting. The tests run only on a machine that can read this peak,
and no other measurement of memory passes or fails a test. The program and every process it starts may hold at most
512 processes and threads together. Beyond that the system refuses to create another, and a run that needs more fails
that test.

**R11.** Every step of a scenario that waits — for readiness, for a reply, for the exit — has a deadline, and a step
that does not complete by its deadline fails the test. The visible samples state their deadlines in their files; the
deadlines of the hidden scenarios are fixed when the suite is calibrated, so that the reference solutions in both
stacks meet each of them with margin.

## 5. Build and run — the workspace contract

**R12.** The workspace holds `STATEMENT.md`, `samples/`, `runner/`, `build.sh`, `run.sh`, `PACKAGES.md` and the
toolchain pin files named in `build.sh`. These are frozen inputs: do not change, rename or remove them. The program's sources
go where `build.sh` and `run.sh` expect them; the two scripts say where.

**R13.** `bash build.sh`, run in the workspace directory, builds the program with no network access and with no
package outside the set listed in `PACKAGES.md`. It must exit 0 within 600 seconds. It runs once before the tests; a
build that does not exit 0 fails every test.

**R14.** `bash run.sh`, run in the workspace directory with no arguments, starts the built program once per test, with
the environment section 2 describes. One fresh process per test; nothing carries over from one test to the next.
`run.sh` ends by replacing itself with the program (`exec`), so the process the tests start, signal and measure is the
program itself. Every test gives the program the same base environment, identical for both stacks apart from a few
documented runtime settings of each stack: in it `LANG` and `LC_ALL` are `de_DE.UTF-8`, nothing else selects a locale,
and `TZ` is `UTC`.

**R15.** The tests run offline. They use a clean copy of the workspace as you left it: build output and installed
packages are removed, and the frozen inputs are put back as they were given. Every other file is yours, including the
project files that declare or lock your packages, and is built as you left it, by `bash build.sh`. The program must not
depend on the network, on files outside the workspace, or on anything written during an earlier run.

## 6. Visible samples

The same two scenarios are in `samples/`, one scenario file and one expected-transcript file per scenario
(`samples/<id>.in`, `samples/<id>.out`), shown below exactly as stored. `runner/GRAMMAR.md` describes their format,
and `python3 -I runner/run_samples.py samples/` builds the program and runs them.

### s01

`samples/s01.in`

```text
scenario 2
default within 2s
start ready port within 10s
connect c1 tcp
send c1 "ping\n"
recv c1 until "\n"
close stdin
wait-exit within 5s
```

`samples/s01.out`

```text
transcript 2
ready
recv c1 "pong\n"
exit 0
```

### s02

`samples/s02.in`

```text
scenario 2
default within 2s
env TZ "Europe/Berlin"
start ready port within 10s
connect c1 tcp
send c1 "ping\n"
recv c1 until "\n"
signal TERM
wait-exit within 5s
```

`samples/s02.out`

```text
transcript 2
ready
recv c1 "pong\n"
exit 0
```

## 7. Hidden tests

**R16.** Hidden tests exist. They are scenarios that satisfy section 2. The visible samples are a small part of the
same suite; a program that passes them has not thereby passed the hidden tests.

## 8. Amendments

None yet.
