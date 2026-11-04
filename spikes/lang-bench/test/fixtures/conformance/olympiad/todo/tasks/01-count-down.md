# Task 01 — Count down

| | |
|---|---|
| Kind | a program that reads standard input once and writes standard output once |
| Gate verdict | not yet gated |
| Freeze tag | none yet |
| Freeze rule | frozen: changes only by a dated amendment before the v2 pre-registration |
| Amendments | none yet |

Every numbered sentence (**R1** … **R14**) is a requirement. Hidden tests refer to these numbers; nothing outside them
is tested.

## 1. What the program does

**R1.** The program reads one integer n and prints the integers from n down to 1, separated by single spaces.

**R2.** For n = 1 the output is the single term 1.

## 2. Input

**R3.** Standard input holds one line: the decimal digits of n, with 1 ≤ n ≤ 100, then one line feed.

**R4.** Input that violates R3 does not occur in any visible or hidden test. What the program does with such input is
unspecified and is never tested.

## 3. Output

**R5.** Standard output holds one line. Two examples:

| n | output |
|---|---|
| 1 | `1` |
| 2 | `2 1` |
**R6.** The output is compared with the expected output byte for byte. Nothing is normalized: a missing or extra line
feed, a trailing space or a carriage return makes the test fail.

**R7.** Standard error is not compared and may hold anything; it is recorded, not judged. Only its size is bounded: a
run that writes more than 256 MiB to standard error is stopped, and that test fails.

**R8.** The process exits with status 0 after writing its output. Any other exit status fails the test, whatever was
written.

## 4. Limits

**R9.** Each test runs the program once, in a fresh process, under a limit on wall-clock time and a limit on memory.
The exact values are registered when the hidden suite is calibrated against reference solutions in both stacks, before
any session, and are given to the test runner, not to the program's author. The registered values are never below
these floors: **1 second** of wall-clock time per run and **256 MB** of memory. (The registration rule: at least three
times the wall time of the slower reference, and at least twice the peak memory of the larger reference.) Memory is
measured as the peak memory of the program's own control group (cgroup `memory.peak`), which counts the program and
every process it starts, including the page cache and kernel memory charged to them, with no swap. The program's
memory reaches the limit when the system kills a process of the program's control group at the limit, or when the
program's own runtime stops it for lack of memory; either way the run is stopped and fails that test. The test machine
caps each runtime's own heap at 90 % of the limit, by that runtime's own setting. The tests run only on a machine that
can read this peak, and no other measurement of memory passes or fails a test. The program and every process it starts
may hold at most 512 processes and threads together. Beyond that the system refuses to create another, and a run that
needs more fails that test.

## 5. Build and run — the workspace contract

**R10.** The workspace holds `STATEMENT.md`, `samples/`, `runner/`, `build.sh`, `run.sh`, `PACKAGES.md` and the
toolchain pin files of the stack. These are frozen inputs: do not change, rename or remove them. The program's sources
go where `build.sh` and `run.sh` expect them; the two scripts say where.

**R11.** `bash build.sh`, run in the workspace directory, builds the program with no network access and with no
package outside the set listed in `PACKAGES.md`. It must exit 0 within 600 seconds. It runs once before the tests; a
build that does not exit 0 fails every test.

**R12.** `bash run.sh`, run in the workspace directory with no arguments, starts the built program once: the test's
input is its standard input, its standard output is collected whole, its exit status is read. One fresh process per
test; nothing carries over from one run to the next. `run.sh` ends by replacing itself with the program (`exec`), so
the process the tests start, signal and measure is the program itself. Every test gives the program the same base
environment, identical for both stacks apart from a few documented runtime settings of each stack: in it `LANG` and
`LC_ALL` are `de_DE.UTF-8`, nothing else selects a locale, and `TZ` is `UTC`.

**R13.** The tests run offline, on a pristine copy of the workspace, after the build. The program must not depend on
the network, on files outside the workspace, or on anything written during an earlier run.

## 6. Visible samples

The same two cases are in `samples/`, one input file and one expected-output file per case (`samples/<id>.in`,
`samples/<id>.out`). The bytes below are exact; `⏎` marks the single line feed that ends each line. `python3 -I
runner/run_samples.py samples/` builds the program and runs them.

| Id | Standard input | Expected standard output |
|---|---|---|
| `s01` | `3⏎` | `3 2 1⏎` |
| `s02` | `1⏎` | `1⏎` |

## 7. Hidden tests

**R14.** Hidden tests exist. They are inputs that satisfy section 2, chosen across the whole range. The visible
samples are a small part of the same suite; a program that passes them has not thereby passed the hidden tests.

## 8. Amendments

None yet.
