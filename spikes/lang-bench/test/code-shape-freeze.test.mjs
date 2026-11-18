// The shape rule of common.coding-style on the freeze tools (F2.S2 (d)): every function has fewer than 50 lines and a cyclomatic complexity of at most 4, with no exemption, measured by v1's scanner
// (spikes/minigit-bench/test/code-shape.mjs). The files are found by pattern, so a new freeze module cannot escape the rule by not being listed.
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { functionShapes } from '../../minigit-bench/test/code-shape.mjs';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const FILES = ['freeze.mjs', ...readdirSync(path.join(spikeDir, 'lib')).filter(name => /^(freeze-.*|spec-freeze)\.mjs$/.test(name)).sort().map(name => `lib/${name}`)];
const offenders = source => functionShapes(source).filter(shape => shape.lines >= 50 || shape.complexity > 4).map(shape => `${shape.name}: ${shape.lines} lines, complexity ${shape.complexity}`);

test('The_files_the_freeze_shape_rule_covers_are_found_by_pattern', () => {
  for (const expected of ['freeze.mjs', 'lib/freeze-cli.mjs', 'lib/freeze-record.mjs', 'lib/freeze-write.mjs', 'lib/spec-freeze.mjs']) assert.ok(FILES.includes(expected), expected);
  assert.ok(FILES.length >= 14, `${FILES.length} files`);
  assert.deepEqual(offenders('function f(a) {\n  if (a) return 1;\n  if (a > 1) return 2;\n  if (a > 2) return 3;\n  if (a > 3) return 4;\n  return 5;\n}\n'), ['f: 7 lines, complexity 5']);
});

for (const file of FILES) {
  test(`Every_function_of_${file.replace(/\W/g, '_')}_has_under_50_lines_and_a_complexity_of_at_most_4`, () => {
    const source = readFileSync(path.join(spikeDir, file), 'utf8');
    assert.ok(functionShapes(source).length > 0 || file === 'freeze.mjs', `${file} has top-level functions`);
    assert.deepEqual(offenders(source), [], 'split the function: one named helper per step, a rule table for a list of cases');
  });
}
