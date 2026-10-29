import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { plantTargets, unplant } from '../../policy-ab-quick/lib/canaries.mjs';
import { NEUTRAL_NOTE, sessionCanaryTargets } from '../lib/canary-plan.mjs';
import { tmpDir } from './helpers.mjs';

const fixed = { rand: (() => { let n = 0; return () => `r${n++}`; })(), token: (() => { let n = 0; return () => `t${n++}`; })() };

function layout() {
  const base = tmpDir('canary');
  const operatorHome = path.join(base, 'home');
  const projects = ['-home-u-git-a', '-home-u-git-b'].map(slug => path.join(operatorHome, '.claude', 'projects', slug));
  const partnerConfigDir = path.join(base, 'partner', 'config');
  for (const p of [...projects, ...projects.map(d => path.join(d, 'memory')), path.join(partnerConfigDir, 'projects'), path.join(base, 'session', 'box')]) mkdirSync(p, { recursive: true });
  return { base, operatorHome, projects, partnerConfigDir, sessionBox: path.join(base, 'session', 'box') };
}

test('Session_canaries_cover_the_operator_projects_and_the_partner', () => {
  const l = layout();
  const targets = sessionCanaryTargets({ operatorHome: l.operatorHome, partnerConfigDir: l.partnerConfigDir, existingProjectDirs: l.projects, ...fixed });
  assert.equal(targets.length, 5, '2 project dirs + 2 memory dirs + 1 partner projects dir');
  assert.deepEqual(targets.map(t => t.dir).sort(), [
    ...l.projects, ...l.projects.map(d => path.join(d, 'memory')), path.join(l.partnerConfigDir, 'projects')].sort());
  for (const t of targets) assert.equal(t.status, 'pending');
  assert.equal(new Set(targets.map(t => t.token)).size, 5, 'one token per canary');
  assert.equal(targets.some(t => t.path.startsWith(l.sessionBox)), false, 'none under the session\'s own box');
});

test('A_target_list_is_a_value_and_no_input_is_changed', () => {
  const l = layout();
  const dirs = [...l.projects];
  const frozen = Object.freeze({ operatorHome: l.operatorHome, partnerConfigDir: l.partnerConfigDir, existingProjectDirs: Object.freeze(dirs), ...fixed });
  assert.doesNotThrow(() => sessionCanaryTargets(frozen));
  assert.deepEqual(dirs, l.projects);
  assert.deepEqual(sessionCanaryTargets({ ...frozen, existingProjectDirs: [], partnerConfigDir: undefined }), [], 'nothing passed, nothing planned');
});

test('The_canary_text_names_nothing_about_the_bench_and_the_round_trip_removes_every_canary', () => {
  assert.doesNotMatch(NEUTRAL_NOTE, /bench|policy|a\/b|canary|minigit|series|cell/i);
  const l = layout();
  const targets = sessionCanaryTargets({ operatorHome: l.operatorHome, partnerConfigDir: l.partnerConfigDir, existingProjectDirs: l.projects, ...fixed });
  const manifest = path.join(l.base, 'canaries.json');
  const rows = plantTargets(manifest, targets, { note: NEUTRAL_NOTE });
  assert.deepEqual(rows.map(r => r.status), Array(5).fill('planted'));
  for (const r of rows) assert.equal(readFileSync(r.path, 'utf8'), `${r.token}\n${NEUTRAL_NOTE}\n`);
  const removed = unplant(manifest, l.operatorHome, { roots: [l.partnerConfigDir] });
  assert.deepEqual(removed.map(r => r.status), Array(5).fill('removed'));
  for (const r of rows) assert.equal(existsSync(r.path), false);
});
