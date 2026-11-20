// The seed as commit-reveal: only a hash in git until the reveal, and the reveal must match the hash.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
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

// ---- the prefix, the files that must be in place first and the check of a drawn seed are arguments (language comparison v2's Stage 1) --------------------------------------

const STAGE = { prefix: 'lv2-', shape: { regex: /^lv2-[0-9a-f]{16}$/, words: 'lv2- and 16 hex' }, consistency: () => null };
const bare = () => ({ inputsDir: tmpDir('inputs'), seedFile: path.join(tmpDir('seeds'), 'stage1.seed') });
const sequence = (...suffixes) => { let i = 0; return () => suffixes[Math.min(i++, suffixes.length - 1)]; };

test('The_prefix_and_the_files_that_must_be_in_place_are_arguments_and_the_default_is_v1s', () => {
  const s = bare();
  const missing = commitSeed({ ...s, ...STAGE, files: [path.join(s.inputsDir, 'one.json')] });
  assert.match(missing.reason, /one\.json does not exist: the seed is committed last, with every other registered file in place/);
  writeFileSync(path.join(s.inputsDir, 'one.json'), '{}\n');
  const r = commitSeed({ ...s, ...STAGE, files: [path.join(s.inputsDir, 'one.json')], random: () => '0123456789abcdef' });
  assert.equal(r.ok, true, r.reason);
  assert.equal(readFileSync(s.seedFile, 'utf8'), 'lv2-0123456789abcdef\n');
  assert.equal(readFileSync(path.join(s.inputsDir, 'seed.sha256'), 'utf8').trim(), createHash('sha256').update('lv2-0123456789abcdef').digest('hex'));
  const v1 = setup();
  assert.equal(commitSeed(v1).ok, true, 'with no argument v1 commits as before');
  assert.match(readFileSync(v1.seedFile, 'utf8').trim(), SEED_SHAPE);
});

test('A_drawn_seed_the_check_refuses_is_dropped_and_another_is_drawn_before_anything_is_written', () => {
  const s = bare();
  const asked = [];
  const check = seed => { asked.push(seed); return seed.endsWith('1111111111111111') ? 'the first seed collides' : null; };
  const r = commitSeed({ ...s, ...STAGE, files: [], check, random: sequence('1111111111111111', '2222222222222222', '3333333333333333') });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(asked, ['lv2-1111111111111111', 'lv2-2222222222222222'], 'each drawn seed is asked, in order, and the second is the one kept');
  assert.equal(readFileSync(s.seedFile, 'utf8'), 'lv2-2222222222222222\n');
  const files = readFileSync(path.join(s.inputsDir, 'seed.sha256'), 'utf8');
  assert.equal(files.trim(), createHash('sha256').update('lv2-2222222222222222').digest('hex'));
  assert.equal(files.includes('1111'), false, 'nothing of the refused seed is on disk');
});

test('A_check_that_refuses_every_draw_ends_the_commit_with_nothing_written', () => {
  const s = bare();
  const r = commitSeed({ ...s, ...STAGE, files: [], check: () => 'always collides', random: sequence('1111111111111111'), maxDraws: 5 });
  assert.equal(r.ok, false);
  assert.match(r.reason, /no seed passed the check in 5 draws: always collides/);
  assert.equal(existsSync(s.seedFile), false);
  assert.equal(existsSync(path.join(s.inputsDir, 'seed.sha256')), false);
});

test('A_drawn_seed_that_is_not_of_the_shape_is_never_committed_because_the_reveal_would_refuse_it', () => {
  const s = bare();
  const r = commitSeed({ ...s, ...STAGE, files: [], random: () => 'not-hex-at-all!!' });
  assert.equal(r.ok, false);
  assert.match(r.reason, /no seed passed the check in 64 draws: the seed lv2-not-hex-at-all!! is not lv2- and 16 hex/);
  assert.equal(existsSync(s.seedFile), false);
});

test('The_reveal_takes_the_shape_as_an_argument_and_names_it', () => {
  const s = bare();
  commitSeed({ ...s, ...STAGE, files: [], random: () => '0123456789abcdef' });
  const wrong = revealSeed({ ...s, shape: { regex: /^zz-/, words: 'zz-' } });
  assert.match(wrong.reason, /the seed in .* is not zz-$/);
  const r = revealSeed({ ...s, ...STAGE });
  assert.equal(r.ok, true, r.reason);
  assert.equal(readFileSync(path.join(s.inputsDir, 'seed.txt'), 'utf8'), 'lv2-0123456789abcdef\n');
});

// ---- the partial states of the commit and the reveal (coai plan round, PR 5, finding S) ----------------------------------------------------------------------------------------------------------

const drawing = suffix => ({ ...STAGE, files: [], random: () => suffix });
const hashPath = s => path.join(s.inputsDir, 'seed.sha256');
const killed = () => { throw new Error('killed'); };

test('A_seed_file_with_no_hash_file_was_never_committed_so_the_retry_refuses_keeps_it_and_names_the_way_out', () => {
  const s = bare();
  assert.equal(commitSeed({ ...s, ...drawing('0123456789abcdef') }).ok, true);
  rmSync(hashPath(s)); // what a kill between the two writes leaves
  const retry = commitSeed({ ...s, ...drawing('5ad3c08e1f7b6924') });
  assert.equal(retry.ok, false);
  assert.match(retry.reason, /exists but .*seed\.sha256 does not: that seed was never committed and is not reused; the private seed file at .*stage1\.seed can be removed by the operator, then run seed-commit again/);
  assert.equal(readFileSync(s.seedFile, 'utf8'), 'lv2-0123456789abcdef\n', 'the file is kept as it was');
  assert.equal(existsSync(hashPath(s)), false, 'no hash is written for a seed nobody registered');
  rmSync(s.seedFile); // the operator removes it, as the sentence says
  const again = commitSeed({ ...s, ...drawing('5ad3c08e1f7b6924') });
  assert.equal(again.ok, true, again.reason);
  assert.equal(readFileSync(s.seedFile, 'utf8'), 'lv2-5ad3c08e1f7b6924\n', 'the retry draws a new seed');
});

test('A_hash_file_with_no_seed_file_is_a_lost_seed_and_the_commit_and_the_reveal_both_refuse_loudly', () => {
  const s = bare();
  commitSeed({ ...s, ...drawing('0123456789abcdef') });
  rmSync(s.seedFile);
  const retry = commitSeed({ ...s, ...drawing('5ad3c08e1f7b6924') });
  assert.match(retry.reason, /seed\.sha256 exists but .*stage1\.seed does not: the committed seed is LOST.*frozen by the pre-registration tag/);
  const hash = readFileSync(hashPath(s), 'utf8');
  assert.match(revealSeed({ ...s, ...STAGE }).reason, /the committed seed is LOST/);
  assert.equal(readFileSync(hashPath(s), 'utf8'), hash, 'the committed hash is never touched');
  assert.equal(existsSync(path.join(s.inputsDir, 'seed.txt')), false);
});

test('A_kill_while_the_hash_is_written_leaves_no_hash_file_and_no_temp_file_and_the_retry_names_the_seed_state', () => {
  const s = bare();
  const stopped = commitSeed({ ...s, ...drawing('0123456789abcdef'), rename: killed });
  assert.equal(stopped.ok, false);
  assert.match(stopped.reason, /cannot write .*seed\.sha256: killed/);
  assert.equal(existsSync(hashPath(s)), false, 'a hash file is whole or absent');
  assert.deepEqual(readdirSync(s.inputsDir), [], 'no temporary file stays behind');
  assert.match(commitSeed({ ...s, ...drawing('5ad3c08e1f7b6924') }).reason, /that seed was never committed/);
});

test('The_hash_file_is_written_by_a_rename_of_a_whole_temp_file_so_a_reader_sees_it_whole_or_not_at_all', () => {
  const s = bare();
  const seen = [];
  const rename = (from, to) => { seen.push([path.dirname(from) === path.dirname(to), readFileSync(from, 'utf8').length, existsSync(to)]); return renameSync(from, to); };
  assert.equal(commitSeed({ ...s, ...drawing('0123456789abcdef'), rename }).ok, true);
  assert.deepEqual(seen, [[true, 65, false]], 'one rename, from a temp file of the same folder that already held the whole hash line, onto a target that was not there');
});

test('A_reveal_that_runs_again_on_the_same_seed_says_already_revealed_and_writes_nothing_and_a_different_seed_txt_is_refused', () => {
  const s = bare();
  commitSeed({ ...s, ...drawing('0123456789abcdef') });
  assert.equal(revealSeed({ ...s, ...STAGE }).ok, true);
  const out = path.join(s.inputsDir, 'seed.txt');
  const mtime = statSync(out).mtimeMs;
  const again = revealSeed({ ...s, ...STAGE });
  assert.deepEqual([again.ok, again.alreadyRevealed], [false, true], 'a refusal value, so v1\'s "revealed once" holds; it names itself');
  assert.match(again.reason, /already revealed: .*seed\.txt holds the committed seed; nothing was written/);
  assert.equal(statSync(out).mtimeMs, mtime);
  writeFileSync(out, 'lv2-ffffffffffffffff\n');
  const differs = revealSeed({ ...s, ...STAGE });
  assert.equal(differs.ok, false);
  assert.equal(differs.alreadyRevealed, undefined);
  assert.match(differs.reason, /seed\.txt exists and differs from the committed seed: refused, nothing was written/);
  assert.equal(readFileSync(out, 'utf8'), 'lv2-ffffffffffffffff\n', 'a file that differs is never replaced');
  assert.equal(differs.reason.includes('ffffffffffffffff'), false, 'a refusal never prints a seed');
});

test('A_kill_while_seed_txt_is_written_leaves_no_seed_txt_and_the_reveal_runs_again', () => {
  const s = bare();
  commitSeed({ ...s, ...drawing('0123456789abcdef') });
  const stopped = revealSeed({ ...s, ...STAGE, rename: killed });
  assert.match(stopped.reason, /cannot write .*seed\.txt: killed/);
  assert.equal(existsSync(path.join(s.inputsDir, 'seed.txt')), false);
  assert.equal(readdirSync(s.inputsDir).filter(n => n !== 'seed.sha256').length, 0, 'no temporary file stays behind');
  assert.equal(revealSeed({ ...s, ...STAGE }).ok, true, 'the retry reveals');
});
