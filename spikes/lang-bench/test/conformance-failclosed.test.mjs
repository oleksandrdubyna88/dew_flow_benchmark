// E1.S8 batch 3, the fail-closed items: a runner that exits 4 and says nothing (C1), a FORMAT that is not the recorded one or whose table or blocks do not fit what the reader expects (C2, C11),
// and the unreadable inputs that end in one sentence and exit 4 (C22). Nothing here may end in a pass.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { check, fixtureRepo } from './support/conformance-fixtures.mjs';

/** A stand-in runner (a Python script) written next to the fixture. */
function standIn(fixture, body) {
  const file = path.join(fixture.repo, 'fake_runner.py');
  writeFileSync(file, body);
  return file;
}

const SILENT = [
  ['says_nothing', 'import sys\nsys.exit(4)\n'],
  ['writes_only_to_standard_output', 'import sys\nprint("5 pairs parsed")\nsys.exit(4)\n'],
  ['writes_only_whitespace_to_standard_error', 'import sys\nsys.stderr.write("  \\n\\n \\t\\n")\nsys.exit(4)\n'],
];
for (const [name, body] of SILENT) {
  test(`A_runner_that_exits_4_and_${name}_is_exit_4_never_a_pass`, async t => {
    const fixture = fixtureRepo(t, 'daemon');
    const result = await check(fixture, [], { runner: standIn(fixture, body) });
    assert.equal(result.exit, 4, result.lines.join('\n'));
    assert.deepEqual(result.lines, []);
    assert.match(result.errors.join('\n'), /the runner exited 4 and named no problem/);
  });
}
