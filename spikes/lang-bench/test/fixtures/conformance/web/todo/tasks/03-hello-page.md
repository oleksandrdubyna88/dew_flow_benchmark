# Task 03 — Hello page

| | |
|---|---|
| Kind | a long-running HTTP server that serves until it is stopped by SIGTERM |
| Gate verdict | not yet gated |
| Freeze tag | none yet |
| Freeze rule | frozen: changes only by a dated amendment before the v2 pre-registration |
| Amendments | none yet |

Every numbered sentence (**R1** … **R16**) is a requirement. Hidden tests refer to these numbers; nothing outside them
is tested.

## 1. What the program does

**R1.** The program answers `GET /hello` with the text `hello` and `GET /items` with a JSON list.

## 2. Start, readiness and stop

**R2.** The environment variable `PORT` holds a TCP port number; the program listens on it, and the tests connect to
it at 127.0.0.1.

**R3.** The program is ready when it accepts a TCP connection on that port. A test waits at most 10 seconds for
readiness; a program that is not ready by then fails that test.

**R4.** A test ends by sending the signal SIGTERM to the program. The program must then exit with status 0 within 8
seconds, leaving no process it started still running. A program still running after 8 seconds is killed and fails that
test, and so does one that exits with another status or leaves a process running. The series default for the grace
period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.

## 3. Interface

**R5.** Every input that violates R1 is answered as R1 says, and hidden tests check it.

**R6.** Standard error is not compared and may hold anything; it is recorded, not judged. Only its size is bounded: a
run that writes more than 256 MiB to standard error is stopped, and that test fails.

**R7.** Standard output is compared only where a requirement of this spec says what it holds; otherwise it is
recorded, not judged.

**R8.** Every reply is compared on its status code, on each response header that a requirement of this spec names, and
on its body; other headers are not compared. The body is compared byte for byte, except where a requirement of this
spec names another comparison. Nothing else is normalized.

**R9.** The body of every reply to `GET /items` is compared as a JSON value: the members of an object may come in any
order, numbers are compared by their exact decimal value with no rounding (so `2` equals `2.0` and `-0` equals `0`,
and two numbers that differ in any digit differ), and whitespace outside strings is ignored; arrays keep their order
unless a requirement says otherwise.

## 4. Limits

**R10.** Each test starts the program once, in a fresh process, and drives one scenario against it under a bound on
the program's memory. The exact bound is registered when the hidden suite is calibrated against reference solutions in
both stacks, before any session, and is given to the test runner, not to the program's author. The registered bound is
never below a floor of **256 MB**. (The registration rule: at least twice the peak memory of the larger reference over
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
toolchain pin files of the stack. These are frozen inputs: do not change, rename or remove them. The program's sources
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

**R15.** The tests run offline, on a pristine copy of the workspace, after the build. The program must not depend on
the network, on files outside the workspace, or on anything written during an earlier run.

## 6. Visible samples

The same 2 scenarios are in `samples/`, one scenario file and one expected-transcript file per scenario
(`samples/<id>.in`, `samples/<id>.out`), shown below exactly as stored. `runner/GRAMMAR.md` describes their format,
and `python3 -I runner/run_samples.py samples/` builds the program and runs them.

### s01

`samples/s01.in`

```text
scenario 2
default within 2s
start ready port within 10s
connect a http
request a GET "/hello"
recv a
signal TERM
wait-exit within 8s
```

`samples/s01.out`

```text
transcript 2
ready
reply a 200
  header "content-type" "text/plain"
  body "hello\n"
exit 0
```

### s02

`samples/s02.in`

```text
scenario 2
default within 2s
start ready port within 10s
connect a http
request a GET "/items"
recv a
signal TERM
wait-exit within 8s
```

`samples/s02.out`

```text
transcript 2
ready
reply a 200
  body json {"items":[]}
exit 0
```

## 7. Hidden tests

**R16.** Hidden tests exist. They are scenarios that satisfy section 2. The visible samples are a small part of the
same suite; a program that passes them has not thereby passed the hidden tests.

## 8. Amendments

None yet.
