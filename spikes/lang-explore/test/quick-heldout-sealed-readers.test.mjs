// F2.S2 (d): every reader of a task's clear held-out folder fails closed (bench PR 4a). With the folder absent and a sealed archive registered, a reader refuses with `sealed: open it first (task NN)` and the verb that opens it, and
// never passes, reports clean, empty or scores visible-only; the control is the same task opened by `heldout-open`, which reads. The six tests of `quick-leak-sealed.test.mjs` (build step 2b) stay green and are not written again.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { withOpenedHeldout } from '../lib/package-seal.mjs';
import { sealedClosed } from './quick-heldout-fixture.mjs';

const lockOf = w => path.join(w.heldoutDir, '.clear.lock');
const stampOf = w => path.join(w.heldoutDir, '.open');

test('A_reader_that_opens_the_archive_closes_the_folder_after_it_also_when_it_fails', async () => {
  const failing = await sealedClosed();
  const seenInside = [];
  await assert.rejects(withOpenedHeldout({ ...failing.args, fn: () => { seenInside.push(existsSync(failing.casesDir)); throw new Error('the reader failed'); } }), /the reader failed/);
  assert.deepEqual(seenInside, [true], 'the folder was there for the reader');
  assert.deepEqual([existsSync(failing.casesDir), existsSync(stampOf(failing)), existsSync(lockOf(failing))], [false, false, false], 'no cases/, no .open, the lock released');
  const fine = await sealedClosed();
  const value = await withOpenedHeldout({ ...fine.args, fn: () => 'the reader\'s value' });
  assert.equal(value, 'the reader\'s value', 'control: the value is returned');
  assert.deepEqual([existsSync(fine.casesDir), existsSync(stampOf(fine)), existsSync(lockOf(fine))], [false, false, false], 'and the same cleanup');
  const refused = await withOpenedHeldout({ ...fine.args, keyFile: path.join(path.dirname(fine.keyFile), 'absent.key'), fn: () => { throw new Error('the reader must not run'); } });
  assert.equal(refused.ok, false, 'a folder that cannot be opened is a refusal value, and the reader never runs');
});
