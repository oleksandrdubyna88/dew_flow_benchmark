// `leakcheck` over a SEALED quick task, end to end (found 2026-11-12 on the first real sealed task): the command opens the sealed task for the WHOLE scan, so the verification of a candidate, which reads the case files again
// after the material is built, still finds them; the clear folder is closed after the command, as before. Invented cases, keys and shares in temp homes; the real private folders are never read here.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { NEEDLE, holdsNeedle, newRepo, runCheck } from './quick-leak-repos-fixture.mjs';
import { exploreOf } from './quick-leak-fixture.mjs';
import { sealedClosed } from './quick-heldout-fixture.mjs';

const NEEDLE_CASE = Object.freeze([{ id: 'h001', input: 'scenario 2\n', output: `transcript 2\n${NEEDLE}exit 0\n` }]);
const sealedWithNeedle = () => sealedClosed({ cases: NEEDLE_CASE, traps: ['h001'] });
const check = (w, taskFiles) => runCheck({ home: w.home, explore: exploreOf(['04']), benchRepo: newRepo(), values: { 'task-repo': newRepo(taskFiles) }, io: { frozenFile: w.frozenFile, heldout: w.args } });

test('A_line_of_a_sealed_task_in_the_task_repository_is_verified_while_the_task_is_open_and_the_folder_is_closed_after', async () => {
  const w = await sealedWithNeedle();
  const r = await check(w, { 'README.md': 'a readme\n', 'tasks/04/notes.md': `a note\n${NEEDLE}` });
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /\[task repository [^\]]*\] FAILED: held-out text found in/);
  assert.match(r.said, /tasks\/04\/notes\.md/, 'the place is named');
  assert.equal(holdsNeedle(r.said), false, 'never the text');
  assert.equal(existsSync(w.casesDir), false, 'the clear folder is closed after the whole command');
});

test('A_sealed_task_that_cannot_be_opened_is_exit_4_with_the_reason_and_leaves_no_clear_folder', async () => {
  const w = await sealedWithNeedle();
  const keyFile = path.join(tmpDir('q-no-key'), 'heldout.key');
  const r = await runCheck({ home: w.home, explore: exploreOf(['04']), benchRepo: newRepo(), values: { 'task-repo': newRepo({ 'README.md': 'a readme\n', 'tasks/04/notes.md': `a note\n${NEEDLE}` }) }, io: { frozenFile: w.frozenFile, heldout: { ...w.args, keyFile } } });
  assert.equal(r.exit, 4, r.said);
  assert.ok(r.said.includes(keyFile), `the reason names the key that could not be read: ${r.said}`);
  assert.doesNotMatch(r.said, /clean/, 'never clean');
  assert.equal(existsSync(w.casesDir), false, 'nothing was opened');
});

test('A_sealed_task_and_a_clean_task_repository_give_a_clean_check_and_leave_no_clear_folder', async () => {
  const w = await sealedWithNeedle();
  const r = await check(w, { 'README.md': 'a readme\n', 'tasks/04/notes.md': 'a public note\n' });
  assert.equal(r.exit, 0, r.said);
  assert.match(r.said, /\[task repository [^\]]*\] clean/);
  assert.equal(existsSync(w.casesDir), false, 'no clear folder after the command');
});
