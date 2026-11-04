# Task spec and sample format

Format version: 2
Gate verdict: `proceed` · 2026-11-04
Freeze tag: none yet

This document fixes the shape of every task spec in `todo/tasks/` and of every visible sample beside it: the file and its
header, the requirement ids, the sentences every spec carries word for word, the sample files of each kind, and the
scenario grammar of the daemon and web samples with its comparison rules. It is stack-neutral: nothing in it favours or
names an implementation language.

Sections 1 to 6 are the format; section 7 is its changelog. **Sections 3 to 5 are copied verbatim into every workspace as
`runner/GRAMMAR.md`**, beside the runner that implements them, so they are written to be read on their own. The rules for
checking a spec against this format and for amending it are kept in a separate maintainers' document that no workspace
and no run base carries.

**Section classes.** Every section below has one class, or, where a section mixes kinds of task, every subsection has
one: `common` (it serves olympiad tasks and another kind, or every kind), `olympiad`, `daemon`, `web`, or `daemon-web` (it
serves daemon and web tasks and no olympiad task). The lead text of a section, before its first subsection, is `common`. In
2.1's table, a row whose sentence stands in a `daemon-web` subsection has that subsection's class. After the freeze of the
first group of tasks, the `common` and `olympiad` parts do not change and keep their numbers; a `daemon`, `web` or
`daemon-web` part may change before the freeze of a later group, as a new dated version that names the tasks it touches, and
such a change touches no olympiad task. The changelog only grows.

| Section | Class |
|---|---|
| 1. The spec file | common |
| 2.1 How they are used | common |
| 2.2 Every kind | common |
| 2.3 Olympiad | olympiad |
| 2.4 Daemon and web | daemon-web |
| 2.5 The timing contract of daemon and web tasks | daemon-web |
| 2.6 Olympiad output: the closed list of normalisations | olympiad |
| 3.1 Files and ids | common |
| 3.2 Olympiad samples | olympiad |
| 3.3 Daemon and web samples: the scenario pair | daemon-web |
| 3.4 Showing samples in the spec | common |
| 4. The scenario script (`.in`) | daemon-web |
| 5. The expected transcript (`.out`) | daemon-web |
| 6.1 Olympiad — one sample of the pilot spec | olympiad |
| 6.2 Daemon — a toy store over an invented framed protocol | daemon-web |
| 6.3 Web — two clients racing on one idempotency key | daemon-web |
| 7. Changelog | common |

## 1. The spec file

### 1.1 Name and title

A spec is `todo/tasks/NN-slug.md`: `NN` is the task number with two digits (`01` … `10`), `slug` is lower-case ASCII
letters and digits in words joined by single hyphens (pattern `^[0-9]{2}-[a-z0-9]+(-[a-z0-9]+)*\.md$`). This file,
`FORMAT.md`, is not a spec. The first line of a spec is its title, `# Task NN — <Title>`, with the same `NN` and an em
dash (U+2014). The workspace's `STATEMENT.md` is generated from the spec; the spec is written so that it reads correctly
there.

### 1.2 The header table

Directly under the title, a two-column table with an empty header row and exactly these five rows, in this order:

| Row | Value before the gate and the freeze | Value afterwards |
|---|---|---|
| `Kind` | the kind phrase of the task (below) | unchanged |
| `Gate verdict` | `not yet gated` | `` `<verdict>` · <YYYY-MM-DD> ``, the verdict being `proceed`, `good_enough` or `continue_anyway` |
| `Freeze tag` | `none yet` | `` `spec-frozen-<yyyymmdd>` `` |
| `Freeze rule` | `frozen: changes only by a dated amendment before the v2 pre-registration` | unchanged |
| `Amendments` | `none yet` | `A1 (section 8)`, or `A1–A<k> (section 8)` once there are k |

The kind phrases form a closed list; the phrase is the whole value of the `Kind` row:

| Kind | Phrase |
|---|---|
| olympiad | `a program that reads standard input once and writes standard output once` |
| daemon | `a long-running program that serves until it is stopped by SIGTERM` |
| web | `a long-running HTTP server that serves until it is stopped by SIGTERM` |

The kind decides the required sections (1.4), the fixed sentences (section 2) and how the samples are read (section 3).

### 1.3 The requirement sentences (R-ids)

Every requirement opens with its id in bold, `**R<n>.**`, followed by one or more sentences. The ids are `R1` … `Rn`:
unique, contiguous (no id missing between 1 and n) and, until the freeze, in ascending order through the document.
Held-out cases cite these ids; nothing outside them is tested.

**A requirement's span.** A requirement runs from its `**R<n>.**` to the next requirement or the next heading of any
level. Every paragraph, list, table and fenced code block in that span belongs to it: a table or a code block (a
grammar, say) is part of the requirement it follows, and so is a lead-in line before it (`Type gauge:`) or a sentence
after it. Outside every span stand only F-REQ (before section 1), the lead of section 6 with the samples and the prose
that explains how a sample is built, and section 8. A table, a code block or a normative sentence anywhere else is a
defect of the spec; prose outside a span explains and never adds a requirement.

**Stable after the freeze: append only.** An id is never renumbered and never reused. An amendment that adds a
requirement gives it the next free id, `R(n+1)`, placed in the section it belongs to, so after an amendment the ids may
stand out of order in the document. An amendment that withdraws a requirement keeps its paragraph as
`**R<n>.** Withdrawn by amendment A<k>.`

### 1.4 The sections, in order

Each spec has exactly these eight `##` sections, numbered and titled as shown, in this order, and no other `##` section.
Sub-sections (`###`) are free inside sections 1 to 3; section 6 of a daemon or web spec has one per sample (3.4).

| # | olympiad | daemon and web |
|---|---|---|
| 1 | `## 1. What the program does` | `## 1. What the program does` |
| 2 | `## 2. Input` | `## 2. Start, readiness and stop` |
| 3 | `## 3. Output` | `## 3. Interface` |
| 4 | `## 4. Limits` | `## 4. Limits` |
| 5 | `## 5. Build and run — the workspace contract` | `## 5. Build and run — the workspace contract` |
| 6 | `## 6. Visible samples` | `## 6. Visible samples` |
| 7 | `## 7. Hidden tests` | `## 7. Hidden tests` |
| 8 | `## 8. Amendments` | `## 8. Amendments` |

Section 8 holds `None yet.` until the first amendment, then one list item per amendment, oldest first:
`- A<k> · <YYYY-MM-DD> · <R-ids added, changed or withdrawn> — <what changed and why>`.

### 1.5 What a spec never contains

- An identifier of the bench's plan: no story, epic, decision, gate or test id.
- For a task that paraphrases a known problem: its id, its title, its site or source, a link, or any mention that the task
  is known. Its credit lives only in the attribution section of the repository README, which no workspace and no run base
  carries; there is no credit file beside the task.
- A link of any kind.
- A trap, a hint at one, or a language, runtime, framework or library specific. The spec names no implementation
  language; the stack sentence (and, for a web task, the framework sentence) is added only when `STATEMENT.md` is
  generated.
- Held-out content, and the exact registered limit values (they go to the test runner, never into a spec).
- The `⏎` glyph anywhere but section 6.

## 2. The fixed sentences

### 2.1 How they are used

The sentences below are said identically by every spec of the kinds named. Each is written once here; a spec copies it
word for word into the section given, inside a requirement's span wherever the table says "in an R". A conformance check
compares them after collapsing every run of spaces and line breaks to one space, so a spec may wrap them at any width. A
slot in braces is the only part that varies, and only with the values the slot allows.

**`{ref}` slots** take one R-id (`R5`), or `any of` followed by a range or a list: a range is `R5–R7` (with an en
dash), and a list holds ids and ranges of any length, separated by commas with `and` before the last. So `R5`,
`any of R5–R7`, `any of R5 and R7`, `any of R8, R11, R14, R17 and R32`, `any of R5–R7, R9 and R12`. `{eref}` slots take
the same forms without `any of`.

| Id | Kinds | Where | Slots |
|---|---|---|---|
| F-REQ | all | before section 1, its own paragraph | `{n}`: the highest R-id number |
| F-INVALID | all, unless the task defines errors for every invalid input | section 2 (olympiad) or 3 (daemon, web), in an R | `{ref}` |
| F-ERRORS | all, instead of F-INVALID when the task defines errors for every invalid input | as F-INVALID | `{ref}`, `{eref}` |
| F-BYTES-O | olympiad, the default | section 3, in an R | none |
| F-BYTES-O-N | olympiad, instead of F-BYTES-O when the output is normalised | section 3, in an R | `{normalisations}`: from the closed list of 2.6 |
| F-STDERR | all | section 3, in an R | none |
| F-EXIT-O | olympiad | section 3, in an R | none |
| F-PORT | daemon and web that listen on a port | section 2, in an R | none |
| F-READY | daemon, web | section 2, in an R | `{readiness}`: one of two phrases (2.4); `{T}`: whole seconds (2.5) |
| F-STOP | daemon, web | section 2, in an R | `{which}` (2.4); `{G}`: whole seconds (2.5) |
| F-EOF | daemon, web whose spec defines an end of the test at the end of its input | section 2, in an R | `{eref}`: what the program does at the end of its input; `{G}` as in F-STOP |
| F-TIMING | daemon, web whose T or G is not the series default | section 2, in the same R as F-READY or F-STOP | `{what}`, `{D}`, `{V}`, `{reason}` (2.5) |
| F-STDOUT-D | daemon, web | section 3, in an R | none |
| F-BYTES-D | daemon | section 3, in an R | none |
| F-BYTES-W | web | section 3, in an R | none |
| F-JSON | daemon, web whose replies, or some of them, are compared as JSON values | section 3, in an R | `{subject}` (2.4) |
| F-LIMITS-O | olympiad | section 4, in an R | none |
| F-LIMITS-D | daemon, web | section 4, in an R | none |
| F-LOAD | daemon, web | section 4, in the same R as F-LIMITS-D | `{size}`, `{duration}` (2.4) |
| F-MEMORY | all | section 4, in the same R as F-LIMITS-O or F-LIMITS-D | none |
| F-PROCS | all | section 4, in the same R as F-MEMORY, after it | none |
| F-DEADLINES | daemon, web | section 4, in an R | none |
| F-FROZEN | all | section 5, in an R | none |
| F-BUILD | all | section 5, in an R | none |
| F-RUN-O | olympiad | section 5, in an R | none |
| F-RUN-D | daemon, web | section 5, in an R | none |
| F-ENV | all | section 5, in the same R as F-RUN-O or F-RUN-D | none |
| F-OFFLINE | all | section 5, in an R | none |
| F-SAMPLES-O | olympiad | section 6, first paragraph | `{k}`: the number of samples, in words or digits |
| F-SAMPLES-D | daemon, web | section 6, first paragraph | `{k}` as above |
| F-HIDDEN | all | section 7, in an R: the first sentence opens the R, the second closes it | none |

### 2.2 Every kind

F-REQ:

```text
Every numbered sentence (**R1** … **R{n}**) is a requirement. Hidden tests refer to these numbers; nothing outside them is tested.
```

F-INVALID (olympiad):

```text
Input that violates {ref} does not occur in any visible or hidden test. What the program does with such input is unspecified and is never tested.
```

F-INVALID (daemon, web):

```text
A request or input that violates {ref} does not occur in any visible or hidden test. What the program does with it is unspecified and is never tested.
```

A task that defines error behaviour for some invalid input states that behaviour in its own requirements and uses
F-INVALID for the rest, `{ref}` naming what stays undefined. A task that defines error behaviour for every invalid
input carries F-ERRORS instead:

```text
Every input that violates {ref} is answered as {eref} says, and hidden tests check it.
```

F-STDERR:

```text
Standard error is not compared and may hold anything; it is recorded, not judged. Only its size is bounded: a run that writes more than 256 MiB to standard error is stopped, and that test fails.
```

F-MEMORY, memory as the program's own control group counts it:

```text
Memory is measured as the peak memory of the program's own control group (cgroup `memory.peak`), which counts the program and every process it starts, including the page cache and kernel memory charged to them, with no swap. The program's memory reaches the limit when the system kills a process of the program's control group at the limit, or when the program's own runtime stops it for lack of memory; either way the run is stopped and fails that test. The test machine caps each runtime's own heap at 90 % of the limit, by that runtime's own setting. The tests run only on a machine that can read this peak, and no other measurement of memory passes or fails a test.
```

F-PROCS, the process bound (it follows F-MEMORY in the same requirement):

```text
The program and every process it starts may hold at most 512 processes and threads together. Beyond that the system refuses to create another, and a run that needs more fails that test.
```

F-FROZEN, the frozen list:

```text
The workspace holds `STATEMENT.md`, `samples/`, `runner/`, `build.sh`, `run.sh`, `PACKAGES.md` and the toolchain pin files of the stack. These are frozen inputs: do not change, rename or remove them. The program's sources go where `build.sh` and `run.sh` expect them; the two scripts say where.
```

F-BUILD, the build bound:

```text
`bash build.sh`, run in the workspace directory, builds the program with no network access and with no package outside the set listed in `PACKAGES.md`. It must exit 0 within 600 seconds. It runs once before the tests; a build that does not exit 0 fails every test.
```

F-ENV, the base environment (it follows F-RUN-O or F-RUN-D in the same requirement):

```text
Every test gives the program the same base environment, identical for both stacks apart from a few documented runtime settings of each stack: in it `LANG` and `LC_ALL` are `de_DE.UTF-8`, nothing else selects a locale, and `TZ` is `UTC`.
```

F-OFFLINE:

```text
The tests run offline, on a pristine copy of the workspace, after the build. The program must not depend on the network, on files outside the workspace, or on anything written during an earlier run.
```

F-HIDDEN — the first sentence opens the requirement, the task's own sentences about what the hidden tests cover stand
between, and the second closes it:

```text
Hidden tests exist.
```

```text
The visible samples are a small part of the same suite; a program that passes them has not thereby passed the hidden tests.
```

### 2.3 Olympiad

F-LIMITS-O, the limits as floors and the registration rule (F-MEMORY and F-PROCS follow it in the same requirement):

```text
Each test runs the program once, in a fresh process, under a limit on wall-clock time and a limit on memory. The exact values are registered when the hidden suite is calibrated against reference solutions in both stacks, before any session, and are given to the test runner, not to the program's author. The registered values are never below these floors: **1 second** of wall-clock time per run and **256 MB** of memory. (The registration rule: at least three times the wall time of the slower reference, and at least twice the peak memory of the larger reference.)
```

F-BYTES-O, byte-exact output, the default:

```text
The output is compared with the expected output byte for byte. Nothing is normalized: a missing or extra line feed, a trailing space or a carriage return makes the test fail.
```

F-EXIT-O:

```text
The process exits with status 0 after writing its output. Any other exit status fails the test, whatever was written.
```

F-RUN-O (F-ENV follows it in the same requirement):

```text
`bash run.sh`, run in the workspace directory with no arguments, starts the built program once: the test's input is its standard input, its standard output is collected whole, its exit status is read. One fresh process per test; nothing carries over from one run to the next. `run.sh` ends by replacing itself with the program (`exec`), so the process the tests start, signal and measure is the program itself.
```

F-SAMPLES-O:

```text
The same {k} cases are in `samples/`, one input file and one expected-output file per case (`samples/<id>.in`, `samples/<id>.out`). The bytes below are exact; `⏎` marks the single line feed that ends each line. `python3 -I runner/run_samples.py samples/` builds the program and runs them.
```

### 2.4 Daemon and web

F-PORT:

```text
The environment variable `PORT` holds a TCP port number; the program listens on it, and the tests connect to it at 127.0.0.1.
```

F-READY. `{readiness}` is one of `it accepts a TCP connection on that port` or `it has written the line {line} to
standard output`, where `{line}` is the line in a code span; `{T}` is whole seconds (2.5).

```text
The program is ready when {readiness}. A test waits at most {T} seconds for readiness; a program that is not ready by then fails that test.
```

F-STOP. `{which}` is `A test`, or, in a task that also carries F-EOF, `A test that does not end at the end of its
input`; `{G}` is the grace period in whole seconds (2.5).

```text
{which} ends by sending the signal SIGTERM to the program. The program must then exit with status 0 within {G} seconds, leaving no process it started still running. A program still running after {G} seconds is killed and fails that test, and so does one that exits with another status or leaves a process running.
```

F-EOF, for a program that reads standard input and whose spec defines what it does when that input ends. `{eref}` names
the requirements that say what the program does then; `{G}` is the same grace period as F-STOP's.

```text
A test may instead end at the end of its input: it closes the program's standard input, and the program then does what {eref} says and must exit with status 0 within {G} seconds of the end of its input, leaving no process it started still running. A program still running after {G} seconds is killed and fails that test, and so does one that exits with another status or leaves a process running.
```

F-STDOUT-D:

```text
Standard output is compared only where a requirement of this spec says what it holds; otherwise it is recorded, not judged.
```

F-BYTES-D:

```text
Every reply is compared with the expected reply byte for byte, except a field that a requirement of this spec says is compared otherwise. Nothing else is normalized.
```

F-BYTES-W:

```text
Every reply is compared on its status code, on each response header that a requirement of this spec names, and on its body; other headers are not compared. The body is compared byte for byte, except where a requirement of this spec names another comparison. Nothing else is normalized.
```

F-JSON, the one other comparison a spec may name. `{subject}` says what it covers: for a web task `The body of` and the
replies in words (`The body of every reply`); for a daemon the replies or output lines in words (`Each line of standard
output that R12 describes`).

```text
{subject} is compared as a JSON value: the members of an object may come in any order, numbers are compared by their exact decimal value with no rounding (so `2` equals `2.0` and `-0` equals `0`, and two numbers that differ in any digit differ), and whitespace outside strings is ignored; arrays keep their order unless a requirement says otherwise.
```

**F-BYTES-W against F-JSON.** For every reply that F-JSON's `{subject}` covers, F-JSON's comparison replaces the
byte-for-byte body comparison of F-BYTES-W (or of F-BYTES-D); the status and the named headers are still compared as
F-BYTES-W says. Every other reply's body stays byte for byte. In the scenario files the covered HTTP replies carry
`body json`, every other reply `body <str>` or `body empty`; a covered daemon reply or output line is the entry
`recv <conn> json <JSON text>`, every other one `recv <conn> <str>`: the transcript's form is the comparison (5.2).

A field whose value the program chooses (an id, a token, a timestamp) is named in a requirement with the form it must
have — for example "the `id` member is a UUID; it is compared by its form, and every later reply that names the same
order carries the same value". The scenario files express that with the normalised fields of 5.3, and relations between
such values (pairwise distinct, equal to a count) with the checks of 5.5.

F-LIMITS-D (F-LOAD, F-MEMORY and F-PROCS follow it in the same requirement):

```text
Each test starts the program once, in a fresh process, and drives one scenario against it under a bound on the program's memory. The exact bound is registered when the hidden suite is calibrated against reference solutions in both stacks, before any session, and is given to the test runner, not to the program's author. The registered bound is never below a floor of **256 MB**. (The registration rule: at least twice the peak memory of the larger reference over all the task's load scenarios.)
```

F-LOAD. `{size}` says in words how large a load scenario gets — counts of connections, requests or bytes, or the R-ids
of the bounds it reaches (`the bounds of R32 and R33`); `{duration}` is whole seconds, and the whole scenario, its start and
its stop included, stays inside the test bound of 4.6 (600 seconds).

```text
The load scenarios are hidden scenarios of up to {size}, each lasting at most {duration} seconds; the bound on memory holds in every test, whether it is a load scenario or not.
```

F-DEADLINES:

```text
Every step of a scenario that waits — for readiness, for a reply, for the exit — has a deadline, and a step that does not complete by its deadline fails the test. The visible samples state their deadlines in their files; the deadlines of the hidden scenarios are fixed when the suite is calibrated, so that the reference solutions in both stacks meet each of them with margin.
```

F-RUN-D (F-ENV follows it in the same requirement):

```text
`bash run.sh`, run in the workspace directory with no arguments, starts the built program once per test, with the environment section 2 describes. One fresh process per test; nothing carries over from one test to the next. `run.sh` ends by replacing itself with the program (`exec`), so the process the tests start, signal and measure is the program itself.
```

F-SAMPLES-D:

```text
The same {k} scenarios are in `samples/`, one scenario file and one expected-transcript file per scenario (`samples/<id>.in`, `samples/<id>.out`), shown below exactly as stored. `runner/GRAMMAR.md` describes their format, and `python3 -I runner/run_samples.py samples/` builds the program and runs them.
```

### 2.5 The timing contract of daemon and web tasks

- **T**, the readiness deadline, and **G**, the grace period after SIGTERM, have series defaults: **T = 10 seconds** and
  **G = 5 seconds**. A spec states its own T in F-READY and its own G in F-STOP, always, also when they are the defaults.
- A task that needs another value states it there and adds F-TIMING, once per overridden value, in the same requirement.

F-TIMING:

```text
The series default for {what} is {D} seconds; this task uses {V} seconds because {reason}.
```

`{what}` is `readiness` or `the grace period`; `{D}` is `10` or `5`; `{V}` is the task's value; `{reason}` is a clause
that names the task's own cause, never a stack.

- **Where the runner reads them.** Every scenario of the task, visible or hidden, writes T as the `within` of its `start`
  and G as the `within` of the first `wait-exit` after its end trigger — `signal TERM`, or `close stdin` in a task that
  carries F-EOF (4.4); that deadline is measured from the trigger, not from the start of the `wait-exit` step. The runner
  takes them from there; when
  it is also given the task's registered T and G, a scenario whose values differ is refused as a format error and never
  re-timed. A conformance check requires every visible scenario's values to equal the spec's.
- Every other deadline is a step deadline: the step's own `within`, or the file's `default within` (4.3).

### 2.6 Olympiad output: the closed list of normalisations

Byte-exact output (F-BYTES-O) is the default. A task whose output is compared otherwise carries F-BYTES-O-N instead,
naming one or more items of this closed list, joined by `; ` in the order of the list:

| Item | The phrase in `{normalisations}` |
|---|---|
| final line feed | `a missing final line feed is ignored` |
| carriage returns | `a carriage return directly before a line feed is ignored` |
| line-end spaces | `spaces at the end of a line are ignored` |
| tokens | `the two are compared as sequences of tokens separated by runs of spaces and line feeds` |
| numeric tolerance (with tokens only) | `a token that is a decimal number matches an expected decimal number that differs from it by at most {a}`, `{a}` a decimal |

```text
The output is compared with the expected output byte for byte, except that {normalisations}. Nothing else is normalized.
```

The runner applies the named items to both outputs in the order of the list. In the numeric tolerance, a decimal number is an
optional `-`, digits, and optionally `.` and digits; a token with an exponent is not one.

No other normalisation exists. The test runner takes the task's normalisation from its registered entry, which is
copied from this sentence; the sample files stay exact bytes either way.

## 3. Visible samples

A stand-in for the real section: the conformance tests read the sections before and after it.

### 3.1 Files and ids

The ids.

### 3.2 Olympiad samples

Pairs of files.

### 3.3 Daemon and web samples: the scenario pair

Scenario pairs.

### 3.4 Showing samples in the spec

Section 6 of a spec shows every sample.

## 4. The scenario script (`.in`)

A stand-in.

### 4.1 Lexical rules

Strings.

### 4.2 The file's head

Head.

### 4.3 Steps

Steps.

### 4.4 Groups and the end

End.

### 4.5 Keywords

The complete list of the script's words: `scenario`, `default`, `within`, `env`, `file`, `start`, `ready`, `port`,
`stdout`, `stdin`, `connect`, `tcp`, `http`, `rcvbuf`, `send`, `split`, `pause`, `request`, `header`, `body`, `json`,
`recv`, `bytes`, `until`, `count`, `lines`, `eof`, `chunk`, `capture`, `regex`, `as`, `expect`, `quiet`, `for`, `closed`,
`close`, `shutdown`, `sleep`, `together`, `repeat`, `end`, `signal`, `TERM`, `wait-exit`.

### 4.6 The runner's bounds

Bounds.

## 5. The expected transcript (`.out`)

A stand-in.

### 5.1 Entries

Entries.

## 6. Worked examples

A stand-in.

### 6.1 Olympiad

Example.

## 7. Changelog

- v2 · 2026-11-04 · draft — a synthetic stand-in for the conformance tests.
