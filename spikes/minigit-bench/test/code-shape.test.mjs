// The shape rule of common.coding-style on the runner's command path (E3b coai-1): a function of `bare-runner.mjs` has fewer than 50 lines and a cyclomatic complexity of at most 4.
// The scanner itself is checked on a sample first: a measure that cannot tell a decision from a word in a message proves nothing.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { codeOnly, functionShapes } from './code-shape.mjs';
import { spikeDir } from './helpers.mjs';

/**
 * The files E3b owns: the runner and everything the bare arm added around it. Each is measured whole, with no exemption. A file joins the list in the commit that splits its last function.
 * Files NOT listed are owned by E1 or E2 (the counters, the broker, the egress proxy, the box, the launch, the smoke, the held-out suite) and are left as they are.
 */
const E3B_FILES = ['bare-runner.mjs', 'lib/bare-series.mjs', 'lib/bare-exit.mjs', 'lib/bare-gates.mjs', 'lib/bare-settle.mjs', 'lib/bare-resume.mjs', 'lib/bare-collect.mjs', 'lib/bare-records-io.mjs', 'lib/bare-record.mjs', 'lib/bare-reclass.mjs', 'lib/bare-pins.mjs', 'lib/bare-limits.mjs', 'lib/bare-schedule.mjs', 'lib/bare-outcome.mjs', 'lib/amendments.mjs', 'lib/alias-store.mjs', 'lib/seed.mjs', 'lib/quiet.mjs', 'lib/cpu-sampler.mjs', 'lib/cache-snapshot.mjs', 'lib/packages.mjs', 'lib/rehearsal.mjs', 'lib/score-run.mjs'];

test('The_shape_scanner_counts_decisions_in_code_and_not_words_in_messages', () => {
  const sample = [
    'function a(x) {',
    '  // if for while && || ? in a comment',
    '  const m = `if ${x ? "a" : `b ${x && 1}`} while`;',
    "  const q = 'if && || ?';",
    '  if (x && x.y) return x?.z ?? 1;',
    '  const r = /a(b)?[?/]c/.test(x) ? 1 / 2 : 0;',
    '  return m + q;',
    '}',
    'export async function b() { return 1; }',
  ].join('\n');
  assert.doesNotMatch(codeOnly(sample), /comment|while`|'if/, 'comments and string contents are blanked');
  assert.deepEqual(functionShapes(sample), [{ name: 'a', lines: 8, complexity: 1 + 1 + 1 + 1 + 1 + 1 }, { name: 'b', lines: 1, complexity: 1 }], 'a template expression ?: and &&, the if and its && and a ternary count; ?. and ?? and a ? inside a regex literal do not, and a division is not a regex');
});

for (const file of E3B_FILES) {
  test(`Every_function_of_${file.replace(/\W/g, '_')}_has_under_50_lines_and_a_complexity_of_at_most_4`, () => {
    const shapes = functionShapes(readFileSync(path.join(spikeDir, file), 'utf8'));
    assert.ok(shapes.length > 0, `${file} has top-level functions`);
    assert.deepEqual(shapes.filter(s => s.lines >= 50 || s.complexity > 4), [], 'split the function: one helper per step');
  });
}
