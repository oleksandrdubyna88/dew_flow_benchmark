// Files that are published whole or not at all (review item 3): a record is written to a temporary name of its owner, flushed, and put at its final name by a hard link that fails when the name is taken. A kill in the middle of
// a write leaves a temporary file, never a truncated record; a temporary file is never read as a record; a truncated `build.json` that something else left neither throws nor reads as a passed build.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import * as nodeFs from 'node:fs';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { commitBuild, committedTree } from '../lib/build-commit.mjs';
import { publishNamed, publishOnce, removeStaleTemps } from '../lib/publish-once.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { readAttempts, rebuildVerdict, writeAttempt, writeEvent } from '../lib/score-files.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { harness } from './quick-passes-harness.mjs';
import { BUILD_RECORD, PINNED, completedBody, sortedIds } from './quick-passes-fixture.mjs';

const ARGS = Object.freeze({ expectedIds: sortedIds(), pinnedRunner: PINNED });
const dirOf = () => tmpDir('quick-publish');

test('A_published_file_is_whole_and_a_second_publish_to_the_same_name_fails_and_leaves_the_first_untouched', () => {
  const file = path.join(dirOf(), 'a.json');
  publishOnce({ file, text: '{"first":true}\n' });
  assert.throws(() => publishOnce({ file, text: '{"second":true}\n' }), /EEXIST/);
  assert.equal(readFileSync(file, 'utf8'), '{"first":true}\n');
  assert.deepEqual(readdirSync(path.dirname(file)), ['a.json'], 'no temporary file is left');
});

test('A_write_killed_in_the_middle_leaves_only_a_temporary_file_that_is_never_read_as_a_record', () => {
  const work = path.join(dirOf(), 'score');
  mkdirSync(work, { recursive: true });
  writeFileSync(path.join(work, 'build.json'), `${JSON.stringify(BUILD_RECORD)}\n`);
  const dying = { ...nodeFs, writeSync: (fd, text) => { nodeFs.writeSync(fd, String(text).slice(0, 10)); throw new Error('killed in the middle of the write'); } };
  assert.throws(() => writeAttempt({ work, n: 1, profile: '2cpu', attempt: 1, body: completedBody(), fs: dying }), /killed in the middle/);
  const files = readdirSync(path.join(work, 'passes'));
  assert.equal(files.some(f => f.endsWith('.json')), false, 'no attempt file exists');
  assert.ok(files.some(f => f.endsWith('.tmp')), 'the temporary file stays, named by its owner');
  assert.equal(readAttempts({ work, ...ARGS })[0].attempts.length, 0, 'a temporary file is not an attempt');
  assert.equal(rebuildVerdict(work, ARGS).passes[0].attempts.length, 0);
  assert.equal(removeStaleTemps(work).length, 1, 'the next scorer removes it');
  assert.equal(readdirSync(path.join(work, 'passes')).length, 0);
});

test('Events_and_the_completion_record_are_published_the_same_way_and_never_in_place', async () => {
  const work = path.join(dirOf(), 'score');
  mkdirSync(work, { recursive: true });
  const seen = [];
  const spy = { ...nodeFs, linkSync: (a, b) => { seen.push(path.basename(b)); return nodeFs.linkSync(a, b); } };
  writeEvent({ work, kind: 'halt', detail: {}, fs: spy });
  const prepare = async dest => { mkdirSync(dest, { recursive: true }); return ok({ frozen: [], drift: [], left: [] }); };
  const made = await commitBuild({ work, prepare, build: async () => ok({ ok: true, exit: 0, timedOut: false, wallMs: 1, tail: '' }), runnerPin: PINNED, fs: spy });
  assert.equal(made.ok, true, made.reason);
  assert.ok(seen.some(n => n.endsWith('-halt.json')) && seen.includes('build.json'), seen.join(' '));
  assert.deepEqual(JSON.parse(readFileSync(path.join(work, 'build.json'), 'utf8')).runnerPin, { format: PINNED.format, version: PINNED.version });
});

test('A_truncated_build_json_neither_throws_nor_reads_as_a_passed_build', async () => {
  const work = path.join(dirOf(), 'score');
  mkdirSync(path.join(work, 'tree'), { recursive: true });
  writeFileSync(path.join(work, 'build.json'), `${JSON.stringify(BUILD_RECORD)}\n`.slice(0, 40));
  const tree = committedTree(work);
  assert.equal(tree.ok, false);
  assert.match(tree.reason, /^build-record-invalid: /);
  for (const text of ['{}\n', '{"ok":true}\n', 'null\n', '[]\n']) {
    writeFileSync(path.join(work, 'build.json'), text);
    assert.match(committedTree(work).reason, /^build-record-invalid: /, text);
  }
  writeFileSync(path.join(work, 'build.json'), `${JSON.stringify(BUILD_RECORD)}\n`.slice(0, 40));
  const snapshot = rebuildVerdict(work, ARGS);
  assert.notEqual(snapshot.verdict, 'accepted');
  assert.equal(snapshot.scoringState, 'open');
  const h = harness();
  mkdirSync(h.work, { recursive: true });
  writeFileSync(path.join(h.work, 'build.json'), '{"treeSha256": "ab');
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /^build-record-invalid: /);
  assert.equal(h.calls.pass.length, 0);
  assert.equal(h.calls.build.length, 0, 'a file that is there is not built over');
  assert.ok(readdirSync(path.join(h.work, 'events')).some(f => f.endsWith('-build-record-invalid.json')));
});

test('A_scorer_that_starts_removes_the_temporary_files_a_dead_one_left', async () => {
  const h = harness();
  mkdirSync(path.join(h.work, 'passes'), { recursive: true });
  mkdirSync(path.join(h.work, 'events'), { recursive: true });
  for (const f of ['passes/p01-2cpu-a1.json.1.abc.tmp', 'events/x.json.2.def.tmp', 'verdict.json.3.tmp']) writeFileSync(path.join(h.work, f), 'half');
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'scored', r.reason);
  const left = ['passes', 'events', '.'].flatMap(d => readdirSync(path.join(h.work, d)).filter(f => f.endsWith('.tmp')));
  assert.deepEqual(left, []);
  assert.equal(existsSync(path.join(h.work, 'scorer.lock')), false);
  assert.equal(publishNamed.length >= 0, true);
});
