// T-L58 (E1.S9): the bounds FORMAT states and the bounds the runner and the execution layer enforce are written down twice, and a green suite on each side proves
// nothing about the pair. Each mapping below reads FORMAT's figure and the source's figure through a strict reader (one match, or the reader's own error) and compares them.
// Every mapping is also run on a copy with the value changed (red), and with the line removed or duplicated (the reader's error, never a silent pick). The layer's
// refusal of environment keys is read against the layer's real `boxEnv`, no copy of its list.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ALLOW_LIST, boxEnv, CASE_KEYS } from '../lib/exec-layer/env.mjs';
import { edit, FORMAT_TEXT, realDraft } from './support/format-fixtures.mjs';
import {
  compareEnvRefusals, compareReads, formatEnvSettable, formatProcs, formatStderrBound, formatStderrSentence, formatTz, sourceBaseTz, sourcePidsMax, sourceStderrKill,
} from './support/format-bounds-readers.mjs';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const read = (...parts) => readFileSync(path.join(spikeDir, ...parts), 'utf8');
const RS_EXEC = read('runner', 'rs_exec.py');
const ENV_MJS = read('lib', 'exec-layer', 'env.mjs');
const SCOPE_ENTRY = read('lib', 'exec-layer', 'scope-entry.sh');
const DIRS = { dotnetSdk: '/pinned/dotnet', node: '/pinned/node', typescript: '/pinned/typescript' };

/** The text without the one line that holds `needle` / with that line twice; throws unless exactly one line holds it. */
function lineEdit(text, needle, twice) {
  const lines = text.split('\n');
  const at = lines.flatMap((line, index) => (line.includes(needle) ? [index] : []));
  if (at.length !== 1) throw new Error(`the fixture needle ${JSON.stringify(needle)} is on ${at.length} lines, not one`);
  return twice ? lines.toSpliced(at[0], 0, lines[at[0]]).join('\n') : lines.toSpliced(at[0], 1).join('\n');
}

// One entry per mapping: how FORMAT's side and the source's side are read, and the edits that must turn it red or make a reader fail. `formatNeedle` is the line of FORMAT
// that holds the figure; `changed` is a function from the original text to the text with the value changed.
const MAPPINGS = [
  {
    name: 'F_STDERR_and_STDERR_KILL_BYTES', label: 'F-STDERR', format: formatStderrSentence, source: sourceStderrKill, sourceText: RS_EXEC,
    formatNeedle: 'Standard error is not compared.', formatChange: text => edit(text, 'writes more than 256 MiB to standard error is stopped, and that test', 'writes more than 255 MiB to standard error is stopped, and that test'),
    sourceNeedle: 'STDERR_KILL_BYTES = 256', sourceChange: text => edit(text, 'STDERR_KILL_BYTES = 256 * 1024 * 1024', 'STDERR_KILL_BYTES = 255 * 1024 * 1024'),
  },
  {
    name: '4_6_and_STDERR_KILL_BYTES', label: '4.6', format: formatStderrBound, source: sourceStderrKill, sourceText: RS_EXEC,
    formatNeedle: '- A program that writes more than 256 MiB', formatChange: text => edit(text, 'A program that writes more than 256 MiB', 'A program that writes more than 512 MiB'),
    sourceNeedle: 'STDERR_KILL_BYTES = 256', sourceChange: text => edit(text, 'STDERR_KILL_BYTES = 256 * 1024 * 1024', 'STDERR_KILL_BYTES = 128 * 1024 * 1024'),
  },
  {
    name: 'F_ENV_TZ_and_the_base_environment', label: 'F-ENV TZ', format: formatTz, source: sourceBaseTz, sourceText: ENV_MJS,
    formatNeedle: 'Every test gives the program the same base environment', formatChange: text => edit(text, '`TZ` is `UTC`', '`TZ` is `Europe/Berlin`'),
    sourceNeedle: "  TZ: 'UTC',", sourceChange: text => edit(text, "  TZ: 'UTC',", "  TZ: 'Europe/Berlin',"),
  },
  {
    name: 'F_PROCS_and_pids_max', label: 'F-PROCS', format: formatProcs, source: sourcePidsMax, sourceText: SCOPE_ENTRY,
    formatNeedle: 'may hold at most 512 processes', formatChange: text => edit(text, 'at most 512 processes', 'at most 1024 processes'),
    sourceNeedle: 'prog/pids.max"', sourceChange: text => edit(text, 'echo 512 > "$cg/prog/pids.max"', 'echo 1024 > "$cg/prog/pids.max"'),
  },
];

for (const mapping of MAPPINGS) {
  const verdict = (formatText = FORMAT_TEXT, sourceText = mapping.sourceText) => compareReads(mapping.label, mapping.format(formatText), mapping.source(sourceText));

  test(`${mapping.name}_agree_in_the_synthetic_FORMAT_and_the_real_source`, () => {
    const result = verdict();
    assert.equal(result.ok, true, result.reason);
  });

  test(`${mapping.name}_agree_in_the_real_draft_and_the_real_source`, t => {
    const draft = realDraft(t);
    if (draft === null) return;
    const result = verdict(draft.bytes.toString('utf8'));
    assert.equal(result.ok, true, result.reason);
  });

  test(`${mapping.name}_a_changed_value_in_FORMAT_turns_the_assertion_red`, () => {
    const result = verdict(mapping.formatChange(FORMAT_TEXT));
    assert.equal(result.ok, false);
    assert.equal(result.kind, 'mismatch', result.reason);
  });

  test(`${mapping.name}_a_changed_value_in_the_source_turns_the_assertion_red`, () => {
    const result = verdict(FORMAT_TEXT, mapping.sourceChange(mapping.sourceText));
    assert.equal(result.ok, false);
    assert.equal(result.kind, 'mismatch', result.reason);
  });

  for (const [what, twice] of [['removed', false], ['duplicated', true]]) {
    test(`${mapping.name}_the_FORMAT_line_${what}_is_the_readers_error`, () => {
      const result = verdict(lineEdit(FORMAT_TEXT, mapping.formatNeedle, twice));
      assert.equal(result.ok, false);
      assert.equal(result.kind, 'reader', result.reason);
      assert.match(result.reason, /exactly one is required/);
    });

    test(`${mapping.name}_the_source_line_${what}_is_the_readers_error`, () => {
      const result = verdict(FORMAT_TEXT, lineEdit(mapping.sourceText, mapping.sourceNeedle, twice));
      assert.equal(result.ok, false);
      assert.equal(result.kind, 'reader', result.reason);
      assert.match(result.reason, /exactly one is required/);
    });
  }
}

test('The_readers_tolerate_whitespace_wrapping_code_spans_and_quoting_variants', () => {
  const wrapped = edit(FORMAT_TEXT, 'writes more than 256 MiB to standard error is stopped, and that', 'writes\nmore than `256 MiB`\nto standard error is stopped, and that');
  assert.equal(formatStderrSentence(wrapped).value, 256 * 1024 * 1024);
  assert.equal(formatStderrSentence(edit(FORMAT_TEXT, 'writes more than 256 MiB to standard error is stopped, and that', 'writes more than `256` MiB to standard error is stopped, and that')).value, 256 * 1024 * 1024);
  assert.equal(formatStderrBound(edit(FORMAT_TEXT, 'more than 256 MiB to standard error is stopped, and the test', 'more than `256 MiB` to  standard   error is stopped, and the test')).value, 256 * 1024 * 1024);
  assert.equal(formatProcs(edit(FORMAT_TEXT, 'at most 512 processes and threads together', 'at most `512`\nprocesses and threads together')).value, 512);
  for (const spelled of ['STDERR_KILL_BYTES=256*1024*1024', 'STDERR_KILL_BYTES  =  256 * 1024 * 1024  # the stop', 'STDERR_KILL_BYTES = 268_435_456', 'STDERR_KILL_BYTES = 268435456']) {
    assert.equal(sourceStderrKill(`import os\n${spelled}\nOTHER = 1\n`).value, 256 * 1024 * 1024, spelled);
  }
  for (const spelled of ['echo 512 > "$cg/prog/pids.max"', "echo 512 > '$cg/prog/pids.max'", 'echo 512 > $cg/prog/pids.max', 'echo 512 >"$cg/prog/pids.max"', '  echo   512   >   ${cg}/prog/pids.max']) {
    assert.equal(sourcePidsMax(`set -e\n${spelled}\n`).value, 512, spelled);
  }
  for (const spelled of ["TZ: 'UTC',", 'TZ: "UTC",', "  TZ : 'UTC'"]) {
    assert.equal(sourceBaseTz(`const BASE = {\n${spelled}\n};\n`).value, 'UTC', spelled);
  }
});

test('The_readers_do_not_take_a_comment_or_another_figure_for_the_subject', () => {
  assert.equal(sourcePidsMax('# echo 512 > "$cg/prog/pids.max"\n').ok, false, 'a commented line is not the write');
  assert.equal(sourcePidsMax('echo 512 > "$cg/prog/memory.max"\n').ok, false);
  assert.equal(sourceStderrKill('# STDERR_KILL_BYTES = 256 * 1024 * 1024\n').ok, false);
  assert.equal(formatStderrSentence(FORMAT_TEXT.replace('256 MiB to standard error is stopped, and that', '256 GiB to standard error is stopped, and that')).ok, false);
});

test('The_text_reader_of_env_mjs_reads_what_the_module_itself_exports', async () => {
  const { BASE_ENV } = await import('../lib/exec-layer/env.mjs');
  assert.equal(sourceBaseTz(ENV_MJS).value, BASE_ENV.TZ);
});

// The layer's refusal of keys: derived from the layer's own exports, spelled in every case, and read against the real `boxEnv`.
const KEYS = [...new Set([...ALLOW_LIST, ...CASE_KEYS])].flatMap(key => [key, key.toLowerCase(), `${key[0]}${key.slice(1).toLowerCase()}`]);
const envVerdict = (formatText = FORMAT_TEXT, fn = boxEnv) => compareEnvRefusals(formatEnvSettable(formatText), KEYS, fn, DIRS);

test('4_2_names_TZ_and_the_layers_boxEnv_refuses_every_other_base_and_heap_key_in_any_spelling_and_accepts_TZ', () => {
  assert.equal(formatEnvSettable(FORMAT_TEXT).value, 'TZ');
  const result = envVerdict();
  assert.equal(result.ok, true, result.reason);
  assert.ok(KEYS.includes('lc_all') && KEYS.includes('DOTNET_GCHeapHardLimit') && KEYS.includes('COMPlus_GCHeapHardLimitPercent'), 'the keys come from the layer and include the heap keys');
});

test('4_2_names_TZ_and_the_layers_boxEnv_does_so_in_the_real_draft', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const result = envVerdict(draft.bytes.toString('utf8'));
  assert.equal(result.ok, true, result.reason);
});

test('A_changed_name_in_4_2_turns_the_refusal_assertion_red', () => {
  const result = envVerdict(edit(FORMAT_TEXT, 'does not\n  have, or `TZ`.', 'does not\n  have, or `LANG`.'));
  assert.equal(result.ok, false);
  assert.equal(result.kind, 'mismatch');
  assert.match(result.reason, /LANG is refused/);
  assert.match(result.reason, /TZ is accepted/);
});

test('A_boxEnv_that_accepts_a_base_key_or_refuses_TZ_turns_the_refusal_assertion_red', () => {
  const lax = envVerdict(FORMAT_TEXT, ({ dirs, extra }) => boxEnv({ dirs, extra: Object.fromEntries(Object.entries(extra).filter(([key]) => key.toLowerCase() !== 'lang')) }));
  assert.equal(lax.ok, false);
  assert.match(lax.reason, /LANG is accepted/);
  const strict = envVerdict(FORMAT_TEXT, ({ dirs, extra }) => boxEnv({ dirs, extra: { ...extra, LANG: 'x' } }));
  assert.equal(strict.ok, false);
  assert.match(strict.reason, /TZ is refused/);
  const heap = envVerdict(FORMAT_TEXT, ({ dirs, extra }) => boxEnv({ dirs, extra: Object.fromEntries(Object.entries(extra).filter(([key]) => !/heaphardlimit/i.test(key))) }));
  assert.equal(heap.ok, false);
  assert.match(heap.reason, /GCHeapHardLimit/);
});

const CLAUSES = [
  ['the_settable_name', 'a variable the base environment (F-ENV) does not\n  have, or `TZ`.'],
  ['the_any_spelling_clause', 'No other variable of the base environment, in any spelling of its case'],
  ['the_heap_clause', "and not\n  the per-test setting by which the test machine caps a runtime's heap, may be set"],
];
for (const [what, clause] of CLAUSES) {
  test(`4_2_with_${what}_removed_is_the_readers_error`, () => {
    const result = envVerdict(edit(FORMAT_TEXT, clause, ''));
    assert.equal(result.ok, false);
    assert.equal(result.kind, 'reader');
    assert.match(result.reason, /exactly one is required/);
  });

  test(`4_2_with_${what}_duplicated_is_the_readers_error`, () => {
    const result = envVerdict(edit(FORMAT_TEXT, clause, `${clause} ${clause}`));
    assert.equal(result.ok, false);
    assert.equal(result.kind, 'reader');
    assert.match(result.reason, /2 matches/);
  });
}

test('A_FORMAT_without_the_subsection_a_reader_needs_is_the_readers_error', () => {
  const result = formatStderrBound(FORMAT_TEXT.replace('### 4.6 The runner\'s bounds', '### 4.6 Another title').replace(/^- A program that writes more than 256 MiB.*\n/m, ''));
  assert.equal(result.ok, false);
  assert.match(result.reason, /0 matches/);
  assert.equal(formatTz('not a format').ok, false);
});
