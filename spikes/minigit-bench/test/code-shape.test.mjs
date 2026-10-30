// The shape rule of common.coding-style on the runner's command path (E3b coai-1): a function of `bare-runner.mjs` has fewer than 50 lines and a cyclomatic complexity of at most 4.
// `runCommand` was 58 lines; it is split into one helper per step. The functions this round did not touch (`cmdSnapshot`, `cmdStatus`, `main`) are listed, so a new function is
// measured and an old one is not silently widened. The scanner itself is checked on a sample first: a measure that cannot tell a decision from a word in a message proves nothing.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { codeOnly, functionShapes } from './code-shape.mjs';
import { spikeDir } from './helpers.mjs';

/** Predate this round and are not changed by it. */
const NOT_TOUCHED = new Set(['cmdSnapshot', 'cmdStatus', 'main']);

test('The_shape_scanner_counts_decisions_in_code_and_not_words_in_messages', () => {
  const sample = [
    'function a(x) {',
    '  // if for while && || ? in a comment',
    '  const m = `if ${x ? "a" : `b ${x && 1}`} while`;',
    "  const q = 'if && || ?';",
    '  if (x && x.y) return x?.z ?? 1;',
    '  return m + q;',
    '}',
    'export async function b() { return 1; }',
  ].join('\n');
  assert.doesNotMatch(codeOnly(sample), /comment|while`|'if/, 'comments and string contents are blanked');
  assert.deepEqual(functionShapes(sample), [{ name: 'a', lines: 7, complexity: 1 + 1 + 1 + 1 + 1 }, { name: 'b', lines: 1, complexity: 1 }], 'a template expression ?: and &&, the if and its && count; ?. and ?? do not');
});

test('Every_function_of_the_command_path_of_the_runner_has_under_50_lines_and_a_complexity_of_at_most_4', () => {
  const shapes = functionShapes(readFileSync(path.join(spikeDir, 'bare-runner.mjs'), 'utf8'));
  assert.ok(shapes.some(s => s.name === 'runCommand'), 'runCommand is still the entry of the run and resume path');
  const bad = shapes.filter(s => !NOT_TOUCHED.has(s.name) && (s.lines >= 50 || s.complexity > 4));
  assert.deepEqual(bad, [], 'split the function: one helper per step');
});
