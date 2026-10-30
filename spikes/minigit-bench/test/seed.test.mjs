// The seed as commit-reveal: only a hash in git until the reveal, and the reveal must match the hash.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { SEED_SHAPE, commitSeed, revealSeed } from '../lib/seed.mjs';
import { tmpDir } from './helpers.mjs';
import { promptsText, stacksText } from './prereg-fixtures.mjs';

const setup = ({ promptSdk = '10.0.114', pin = '10.0.114' } = {}) => {
  const inputsDir = tmpDir('inputs');
  writeFileSync(path.join(inputsDir, 'stacks.json'), stacksText(pin));
  writeFileSync(path.join(inputsDir, 'prompts-bare.json'), promptsText(promptSdk));
  return { inputsDir, seedFile: path.join(tmpDir('seeds'), 'bare.seed') };
};

test('The_seed_is_committed_as_a_hash_in_inputs_and_a_mode_600_file_outside_git', () => {
  const s = setup();
  const r = commitSeed(s);
  assert.equal(r.ok, true, r.reason);
  const seed = readFileSync(s.seedFile, 'utf8').trim();
  assert.match(seed, SEED_SHAPE);
  assert.equal(statSync(s.seedFile).mode & 0o777, 0o600);
  const hash = readFileSync(path.join(s.inputsDir, 'seed.sha256'), 'utf8').trim();
  assert.equal(hash, createHash('sha256').update(seed).digest('hex'));
  assert.equal(readFileSync(path.join(s.inputsDir, 'seed.sha256'), 'utf8').includes(seed), false, 'the hash file never holds the seed');
  assert.equal(existsSync(path.join(s.inputsDir, 'seed.txt')), false);
  assert.equal(commitSeed(s).ok, false, 'committed once: neither file is overwritten');
});

test('Two_seed_draws_are_never_equal', () => {
  const a = setup();
  const b = setup();
  commitSeed(a);
  commitSeed(b);
  assert.notEqual(readFileSync(a.seedFile, 'utf8'), readFileSync(b.seedFile, 'utf8'));
});

test('The_revealed_seed_must_match_its_hash', () => {
  const s = setup();
  commitSeed(s);
  const r = revealSeed(s);
  assert.equal(r.ok, true, r.reason);
  assert.equal(readFileSync(path.join(s.inputsDir, 'seed.txt'), 'utf8').trim(), readFileSync(s.seedFile, 'utf8').trim());
  assert.equal(revealSeed(s).ok, false, 'revealed once');
  const t = setup();
  commitSeed(t);
  writeFileSync(t.seedFile, 'mg-ffffffffffffffff\n');
  const wrong = revealSeed(t);
  assert.equal(wrong.ok, false);
  assert.match(wrong.reason, /does not hash to the committed/);
  assert.equal(existsSync(path.join(t.inputsDir, 'seed.txt')), false, 'nothing is written for a seed that does not match');
  const u = setup();
  assert.match(revealSeed(u).reason, /nothing to reveal/);
});

test('A_seed_is_not_committed_while_the_csharp_prompt_names_another_sdk_than_the_pin', () => {
  const s = setup({ promptSdk: '10.0.113', pin: '10.0.114' });
  const r = commitSeed(s);
  assert.equal(r.ok, false, 'the last step before the tag refuses a prompt the pin contradicts');
  assert.match(r.reason, /prompts-bare\.json names SDK 10\.0\.113.*10\.0\.114/);
  assert.equal(existsSync(s.seedFile), false, 'no seed was drawn');
  assert.equal(existsSync(path.join(s.inputsDir, 'seed.sha256')), false);
});
