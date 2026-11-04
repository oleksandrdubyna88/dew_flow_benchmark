// A synthetic FORMAT.md for the FORMAT tools' tests (E1.S9): the shape of the real document (preamble with the three header rows and the class table,
// seven sections, section 2 mixed, fixed-sentence table in 2.1, definitions in fenced blocks, a scenario comment that looks like a heading inside a
// fence, a changelog), with none of its content. A refusal case edits this text with `edit`, which throws when the needle is not there exactly once:
// a fixture the tool never saw as edited would prove nothing.
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { owned } from './owned.mjs';

export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
export const TOOLS = fileURLToPath(new URL('../../tools/', import.meta.url));

const PREAMBLE = `# Task spec and sample format

Format version: 2
Gate verdict: not yet gated
Freeze tag: none yet

This document fixes the shape of every spec. It is a synthetic stand-in.

**Section classes.** Every section below has one class, or, where a section mixes kinds of task, every subsection has
one: \`common\`, \`olympiad\`, \`daemon\`, \`web\`, or \`daemon-web\`.

| Section | Class |
|---|---|
| 1. The spec file | common |
| 2.1 How they are used | common |
| 2.2 Every kind | common |
| 2.3 Olympiad | olympiad |
| 2.4 Daemon and web | daemon-web |
| 2.5 The timing contract | daemon-web |
| 2.6 Olympiad output | olympiad |
| 3.1 Files and ids | common |
| 3.2 Olympiad samples | olympiad |
| 3.3 Daemon and web samples | daemon-web |
| 4. The scenario script | daemon-web |
| 5. The expected transcript | daemon-web |
| 6. Worked examples | daemon-web |
| 7. Changelog | common |

`;

const BODY = `## 1. The spec file

The file name and the header table.

### 1.1 Name and title

One name.

## 2. The fixed sentences

The lead text of section 2.

### 2.1 How they are used

The sentences are said identically.

| Id | Kinds | Where | Slots |
|---|---|---|---|
| F-STDERR | all | section 3, in an R | none |
| F-READY | daemon, web | section 2, in an R | \`{T}\` |

Text after the table.

### 2.2 Every kind

F-STDERR:

\`\`\`text
Standard error is not compared. Only its size is bounded: a run that writes more than 256 MiB to standard error is stopped, and that test fails.
\`\`\`

F-PROCS, the process bound:

\`\`\`text
The program and every process it starts may hold at most 512 processes and threads together.
\`\`\`

F-ENV, the base environment:

\`\`\`text
Every test gives the program the same base environment: \`TZ\` is \`UTC\`.
\`\`\`

### 2.3 Olympiad

Olympiad sentences.

### 2.4 Daemon and web

F-READY. The slot \`{T}\` is whole seconds.

\`\`\`text
The program is ready when it accepts a TCP connection. A test waits at most {T} seconds.
\`\`\`

### 2.5 The timing contract

The defaults T and G.

### 2.6 Olympiad output

The closed list.

## 3. Visible samples

Samples are pairs of files.

### 3.1 Files and ids

The ids.

### 3.2 Olympiad samples

Pairs of files, olympiad.

### 3.3 Daemon and web samples

Scenario pairs.

## 4. The scenario script

The grammar.

### 4.1 Lexical rules

Strings and escapes.

### 4.2 The file's head

- \`env <NAME> <str>\` sets one environment variable of the program: a variable the base environment (F-ENV) does not
  have, or \`TZ\`. \`PORT\` is reserved. No other variable of the base environment, in any spelling of its case, and not
  the per-test setting by which the test machine caps a runtime's heap, may be set: a scenario that sets one is refused.

### 4.3 Steps

The steps.

### 4.4 Groups and the end

The end.

### 4.5 Keywords

The closed list.

### 4.6 The runner's bounds

- A program that writes more than 256 MiB to standard error is stopped, and the test fails.
- A connection's bytes are held up to 16 MiB.

## 5. The expected transcript

The transcript.

### 5.1 Entries

The entries.

## 6. Worked examples

Examples that illustrate the grammar.

### 6.1 Olympiad

\`\`\`text
# 1. a scenario comment that looks like a heading
## 9. inside a fence, not a section
### 9.9 nor this one
\`\`\`

### 6.2 Daemon

More text.

## 7. Changelog

The changelog only grows.

- v2 · 2026-11-04 · draft — the first line.
- v2 · 2026-11-04 · draft — the second line.
`;

/** The synthetic FORMAT as text. */
export const FORMAT_TEXT = PREAMBLE + BODY;

/** `text` with `from` replaced by `to`; throws unless `from` occurs exactly once (a needle that is not there would make the case prove nothing). */
export function edit(text, from, to) {
  const count = text.split(from).length - 1;
  if (count !== 1) throw new Error(`the fixture needle ${JSON.stringify(from)} occurs ${count} times, not once`);
  return text.replace(from, () => to);
}

/** The folder of this test's own files (owned, removed when the test ends). */
export const folder = (t, what) => owned.ownedDir(t, what);

/** Writes `content` to `<owned folder>/<name>` and returns its path. */
export function put(dir, name, content) {
  mkdirSync(dir, { recursive: true });
  const file = path.join(dir, name);
  writeFileSync(file, content);
  return file;
}

/** A record file `{ A: { sha256 } }` for `bytes`, the shape of inputs/specs.d/format.json, in `dir`. */
export const recordFor = (dir, bytes) => put(dir, 'format.json', JSON.stringify({ A: { version: 2, sha256: sha256(bytes), gate: null, grammarSha256: null, tasks: [] } }));

const spikeDir = fileURLToPath(new URL('../..', import.meta.url));
const REAL_RECORD = path.join(spikeDir, 'inputs', 'specs.d', 'format.json');

/**
 * The real FORMAT draft as bytes, for the cases that run on it: `LANG_V2_TASK_REPO` names the task repository's checkout. Unset, the case skips with its reason
 * (`t.skip`), and under BENCH_REQUIRE_SANDBOX=1 it throws. The checkout's file must be the one the record names (`A.sha256`), else the case fails: a draft that moved
 * must not be checked silently against another reference. Returns `null` when the case was skipped.
 */
export function realDraft(t) {
  const repo = process.env.LANG_V2_TASK_REPO;
  if (repo === undefined || repo === '') {
    const why = 'LANG_V2_TASK_REPO does not name the task repository checkout';
    if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
    t.skip(why);
    return null;
  }
  const bytes = readFileSync(path.join(repo, 'todo', 'tasks', 'FORMAT.md'));
  const recorded = JSON.parse(readFileSync(REAL_RECORD, 'utf8')).A.sha256;
  if (sha256(bytes) !== recorded) throw new Error(`the checkout's todo/tasks/FORMAT.md has sha256 ${sha256(bytes)}, but inputs/specs.d/format.json records ${recorded}`);
  return { bytes, recorded, record: REAL_RECORD };
}
