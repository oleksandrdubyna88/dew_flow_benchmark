// The shape rule of common.coding-style on the pilot's modules: every top-level function has fewer than 50 lines and a cyclomatic complexity of at most 4, with no exemption (v1's scanner, used as it is).
// A source file of this spike that is in neither list fails the second test, so a new module cannot escape the rule by not being listed.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { functionShapes } from '../../minigit-bench/test/code-shape.mjs';
import { SPIKE } from '../lib/paths.mjs';

const MEASURED = ['explore.mjs', ...readdirSync(path.join(SPIKE, 'lib')).filter(f => f.endsWith('.mjs')).map(f => `lib/${f}`)];

for (const file of MEASURED) {
  test(`Every_function_of_${file.replace(/\W/g, '_')}_has_under_50_lines_and_a_complexity_of_at_most_4`, () => {
    const shapes = functionShapes(readFileSync(path.join(SPIKE, file), 'utf8'));
    assert.deepEqual(shapes.filter(s => s.lines >= 50 || s.complexity > 4), [], 'split the function: one helper per step');
  });
}

test('The_scan_still_sees_the_functions_of_a_module_so_a_pass_is_not_an_empty_scan', () => {
  const shapes = functionShapes(readFileSync(path.join(SPIKE, 'lib', 'prepare.mjs'), 'utf8'));
  assert.ok(shapes.some(s => s.name === 'prepare') && shapes.length >= 8, shapes.map(s => s.name).join(','));
});
