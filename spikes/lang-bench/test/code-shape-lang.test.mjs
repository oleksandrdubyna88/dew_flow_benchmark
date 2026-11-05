// The shape rule of common.coding-style on the language bench's FORMAT and conformance readers (E1.S8, parent E1 §3): every function has fewer than 50 lines and a cyclomatic complexity of at most 4,
// with no exemption, measured by v1's scanner (spikes/minigit-bench/test/code-shape.mjs: `if`, `for`, `while`, `case`, `catch`, `&&`, `||` and a ternary each count one). Each file is its own test and names
// its offenders. The files are found by pattern, so a new reader cannot escape the rule by not being listed.
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { functionShapes } from '../../minigit-bench/test/code-shape.mjs';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const names = (dir, pattern) => readdirSync(path.join(spikeDir, dir)).filter(name => pattern.test(name)).sort().map(name => path.posix.join(dir, name));
const FILES = ['conformance.mjs', ...names('lib', /^conformance.*\.mjs$/), ...names('lib', /^format-.*\.mjs$/), ...names('tools', /\.mjs$/)];

test('The_files_the_shape_rule_covers_are_found_by_pattern', () => {
  for (const expected of ['conformance.mjs', 'lib/conformance.mjs', 'lib/format-structure.mjs', 'tools/format-classes.mjs', 'tools/grammar-extract.mjs']) assert.ok(FILES.includes(expected), expected);
  assert.ok(FILES.length >= 18, `${FILES.length} files`);
});

for (const file of FILES) {
  test(`Every_function_of_${file.replace(/\W/g, '_')}_has_under_50_lines_and_a_complexity_of_at_most_4`, () => {
    const shapes = functionShapes(readFileSync(path.join(spikeDir, file), 'utf8'));
    assert.ok(shapes.length > 0 || file === 'conformance.mjs', `${file} has top-level functions`);
    assert.deepEqual(shapes.filter(shape => shape.lines >= 50 || shape.complexity > 4).map(shape => `${shape.name}: ${shape.lines} lines, complexity ${shape.complexity}`), [], 'split the function: one named helper per step, a rule table for a list of cases');
  });
}
