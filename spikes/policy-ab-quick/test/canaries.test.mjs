import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { DEFAULT_SLUG, PREFIX, canaryTargets, isDeletable, plantTargets, unplant } from '../lib/canaries.mjs';

const fixed = { rand: () => 'abcd1234', token: () => 'tok' };
const mk = () => fs.mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-pab-canary-'));

test('canaryTargets_keeps_its_three_default_dirs_and_takes_an_explicit_list', () => {
  const home = path.join(path.sep, 'h');
  const base = path.join(home, '.claude');
  const defaults = canaryTargets(home, fixed.rand, fixed.token).map(t => t.dir);
  assert.deepEqual(defaults, [path.join(base, 'projects', DEFAULT_SLUG), path.join(base, 'projects', DEFAULT_SLUG, 'memory'), path.join(base, 'file-history')]);
  const explicit = canaryTargets(home, fixed.rand, fixed.token, undefined, [path.join(path.sep, 'x', 'projects')]);
  assert.deepEqual(explicit.map(t => t.path), [path.join(path.sep, 'x', 'projects', `${PREFIX}abcd1234.md`)]);
  assert.equal(explicit[0].status, 'pending');
});

test('plantTargets_writes_the_manifest_first_a_neutral_note_and_unplant_honours_extra_roots', () => {
  const root = mk();
  try {
    const home = path.join(root, 'home');
    const partner = path.join(root, 'partner', 'projects');
    fs.mkdirSync(path.join(home, '.claude'), { recursive: true });
    fs.mkdirSync(partner, { recursive: true });
    const manifest = path.join(root, 'canaries.json');
    const targets = canaryTargets(home, fixed.rand, fixed.token, undefined, [partner, path.join(partner, 'missing')]);
    const rows = plantTargets(manifest, targets, { note: 'Scratch note.' });
    assert.deepEqual(rows.map(r => r.status), ['planted', 'skipped-no-folder']);
    assert.equal(fs.readFileSync(rows[0].path, 'utf8'), 'tok\nScratch note.\n');
    assert.equal(JSON.parse(fs.readFileSync(manifest, 'utf8')).planted.length, 2);
    // outside ~/.claude: refused unless its root is named
    assert.equal(isDeletable(rows[0].path, home), false);
    assert.equal(isDeletable(rows[0].path, home, [path.join(root, 'partner')]), true);
    assert.deepEqual(unplant(manifest, home).map(r => r.status), ['refused', 'refused']);
    assert.ok(fs.existsSync(rows[0].path));
    assert.deepEqual(unplant(manifest, home, { roots: [path.join(root, 'partner')] }).map(r => r.status), ['removed', 'absent']);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('plantTargets_defaults_to_the_historic_note', () => {
  const root = mk();
  try {
    const dir = path.join(root, 'home', '.claude', 'x');
    fs.mkdirSync(dir, { recursive: true });
    const [row] = plantTargets(path.join(root, 'm.json'), canaryTargets(path.join(root, 'home'), fixed.rand, fixed.token, undefined, [dir]));
    assert.match(fs.readFileSync(row.path, 'utf8'), /^tok\nPolicy A\/B canary\. Not part of any task\.\n$/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
