// The shape rule of common.coding-style on the runner-text files of F1.S1 (f): every function has fewer than 50 lines and a complexity of at most 4, with no exemption, measured by v1's scanner. Each file is its own test.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { functionShapes } from '../../minigit-bench/test/code-shape.mjs';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const FILES = Object.freeze(['lib/py-text.mjs', 'lib/push-runner-text.mjs', 'lib/runner-text.mjs', 'lib/runner-ast-proof.mjs', 'runner-ast-proof.mjs']);

for (const file of FILES) {
  test(`Every_function_of_${file.replace(/\W/g, '_')}_has_under_50_lines_and_a_complexity_of_at_most_4`, () => {
    const shapes = functionShapes(readFileSync(path.join(spikeDir, file), 'utf8'));
    assert.ok(shapes.length > 0, `${file} has top-level functions`);
    assert.deepEqual(shapes.filter(shape => shape.lines >= 50 || shape.complexity > 4).map(shape => `${shape.name}: ${shape.lines} lines, complexity ${shape.complexity}`), [], 'split the function: one named helper per step');
  });
}
