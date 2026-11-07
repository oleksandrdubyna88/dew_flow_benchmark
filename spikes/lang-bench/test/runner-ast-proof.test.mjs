// The `ast` proof of a runner change (F1.S1 (f)): a change of comments and docstrings is shown to change nothing else, and a change of one literal is shown. The folders are invented scratch copies.
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { astProof, proofHolds, proofLine } from '../lib/runner-ast-proof.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const REAL_RUNNER = fileURLToPath(new URL('../runner', import.meta.url));
const MODULE = ['"""A module doc."""', '# a comment', 'RUNNER_VERSION = "0.3.9"', 'LIMIT = 5', '', 'def f(x):', '    """Doc."""', '    # inside', '    return x + LIMIT', ''].join('\n');

function folderWith(t, label, files) {
  const dir = ownedFolder(t, label);
  mkdirSync(dir, { recursive: true });
  for (const [name, text] of Object.entries(files)) writeFileSync(path.join(dir, name), text);
  return dir;
}

test('The_runner_change_is_comments_and_docstrings_only', async t => {
  const base = folderWith(t, 'astbase', { 'rs_a.py': MODULE, 'rs_b.py': 'y = 2\n' });
  const head = folderWith(t, 'asthead', {
    'rs_a.py': MODULE.replace('# a comment', '# another comment, longer').replace('"""A module doc."""', '"""A different doc."""').replace('"""Doc."""', '"""Other doc."""').replace('"0.3.9"', '"0.3.10"'),
    'rs_b.py': 'y = 2\n',
  });
  const proof = await astProof({ baseDir: base, headDir: head });
  assert.equal(proof.ok, true, proof.reason);
  assert.equal(proofHolds(proof.value), true, proofLine(proof.value));
  assert.equal(proof.value.modules, 2);
});

test('A_changed_literal_a_new_module_and_a_missing_module_are_shown_and_that_is_the_point', async t => {
  const base = folderWith(t, 'astbase', { 'rs_a.py': MODULE, 'rs_gone.py': 'z = 1\n' });
  const head = folderWith(t, 'asthead', { 'rs_a.py': MODULE.replace('LIMIT = 5', 'LIMIT = 6'), 'rs_new.py': 'w = 1\n' });
  const proof = await astProof({ baseDir: base, headDir: head });
  assert.equal(proof.ok, true, proof.reason);
  assert.deepEqual([proof.value.differ, proof.value.added, proof.value.removed], [['rs_a.py'], ['rs_new.py'], ['rs_gone.py']]);
  assert.equal(proofHolds(proof.value), false);
  const code = await astProof({ baseDir: base, headDir: folderWith(t, 'asthead2', { 'rs_a.py': MODULE.replace('return x + LIMIT', 'return x - LIMIT'), 'rs_gone.py': 'z = 1\n' }) });
  assert.deepEqual(code.value.differ, ['rs_a.py'], 'one operator on a code line is a difference too');
  const broken = await astProof({ baseDir: base, headDir: folderWith(t, 'asthead3', { 'rs_a.py': 'def f(:\n', 'rs_gone.py': 'z = 1\n' }) });
  assert.deepEqual(broken.value.differ, ['rs_a.py'], 'a module that does not parse is a difference, never a pass');
});

test('The_runner_compared_with_itself_holds_and_reads_every_module', async () => {
  const proof = await astProof({ baseDir: REAL_RUNNER, headDir: REAL_RUNNER });
  assert.equal(proof.ok, true, proof.reason);
  assert.equal(proofHolds(proof.value), true, proofLine(proof.value));
  assert.equal(proof.value.modules, readdirSync(REAL_RUNNER).filter(name => name.endsWith('.py')).length);
});
