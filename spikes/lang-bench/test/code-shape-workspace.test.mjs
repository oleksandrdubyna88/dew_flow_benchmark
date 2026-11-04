// The shape rule of common.coding-style on the workspace files of E1.S3 (parent E1 §3): every function has fewer than 50 lines and a cyclomatic complexity of at most 4, with no exemption, measured by v1's scanner
// (spikes/minigit-bench/test/code-shape.mjs: `if`, `for`, `while`, `case`, `catch`, `&&`, `||` and a ternary each count one). Each file is its own test and names its offenders. Test helpers are not in this rule.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { functionShapes } from '../../minigit-bench/test/code-shape.mjs';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const FILES = Object.freeze(['build-workspaces.mjs', 'lib/task-repo.mjs', 'lib/workspace-box.mjs', 'lib/workspace-build.mjs', 'lib/workspace-destination.mjs']);

const offenders = source => functionShapes(source).filter(shape => shape.lines >= 50 || shape.complexity > 4).map(shape => `${shape.name}: ${shape.lines} lines, complexity ${shape.complexity}`);

test('The_shape_rule_names_a_function_that_is_too_long_or_too_branched', () => {
  const branched = `function f(a) {\n  if (a) return 1;\n  if (a > 1) return 2;\n  if (a > 2) return 3;\n  if (a > 3) return 4;\n  return 5;\n}\n`;
  assert.deepEqual(offenders(branched), ['f: 7 lines, complexity 5']);
  const long = `function g() {\n${'  const x = 1;\n'.repeat(50)}}\n`;
  assert.deepEqual(offenders(long), ['g: 52 lines, complexity 1']);
});

for (const file of FILES) {
  test(`Every_function_of_${file.replace(/\W/g, '_')}_has_under_50_lines_and_a_complexity_of_at_most_4`, () => {
    const shapes = functionShapes(readFileSync(path.join(spikeDir, file), 'utf8'));
    assert.ok(shapes.length > 0, `${file} has top-level functions`);
    assert.deepEqual(offenders(readFileSync(path.join(spikeDir, file), 'utf8')), [], 'split the function: one named helper per step, a rule table for a list of cases');
  });
}

const importsOf = file => [...readFileSync(path.join(spikeDir, file), 'utf8').matchAll(/^import .* from '([^']+)';$/gm)].map(match => match[1]);

test('The_destination_guard_is_below_both_the_builder_and_the_task_repository_and_imports_neither', () => {
  assert.ok(importsOf('lib/task-repo.mjs').includes('./workspace-destination.mjs'));
  assert.ok(importsOf('lib/workspace-build.mjs').includes('./workspace-destination.mjs'));
  assert.deepEqual(importsOf('lib/task-repo.mjs').filter(name => name.includes('workspace-build')), [], 'the task repository does not import the builder');
  assert.deepEqual(importsOf('lib/workspace-destination.mjs').filter(name => name.startsWith('./')), [], 'the guard imports no module of this story');
});
