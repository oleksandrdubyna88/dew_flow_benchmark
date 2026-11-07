// `verdict.json` as a derived snapshot (section 2.10): rebuilt from the attempt and event files alone, written whole to a temp name and renamed, never opened for reading and never merged from itself, so a resume cannot lose
// an entry of its lists and a corrupted or invented snapshot cannot survive. The attempt files are written through the library's own writers.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import * as nodeFs from 'node:fs';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { attemptFile, readAttempts, readEvents, rebuildVerdict, writeAttempt, writeEvent } from '../lib/score-files.mjs';
import { profileOfPass } from '../lib/verdict.mjs';
import { BUILD_RECORD, PINNED, completedBody, environmentBody, sortedIds } from './quick-passes-fixture.mjs';

const T = i => () => new Date(Date.parse('2026-11-07T10:00:00.000Z') + i * 1000);
const ARGS = Object.freeze({ expectedIds: sortedIds(), pinnedRunner: PINNED });

function scoredSession() {
  const work = path.join(tmpDir('quick-snapshot'), 'score');
  mkdirSync(work, { recursive: true });
  writeFileSync(path.join(work, 'build.json'), `${JSON.stringify(BUILD_RECORD)}\n`);
  for (let n = 1; n <= 10; n += 1) writeAttempt({ work, n, profile: profileOfPass(n).name, attempt: 1, body: completedBody({ observedCpus: profileOfPass(n).cpus }) });
  return work;
}

const rebuilt = work => { rebuildVerdict(work, ARGS); return readFileSync(path.join(work, 'verdict.json'), 'utf8'); };
const recordingFs = opens => ({ ...nodeFs, readFileSync: (p, ...a) => { opens.push(String(p)); return nodeFs.readFileSync(p, ...a); }, openSync: (p, ...a) => { opens.push(String(p)); return nodeFs.openSync(p, ...a); }, existsSync: p => { opens.push(String(p)); return nodeFs.existsSync(p); } });

test('verdict_json_is_rebuilt_from_the_attempt_and_event_files_alone_and_a_deleted_or_corrupted_one_comes_back_identical', () => {
  const work = scoredSession();
  const original = rebuilt(work);
  assert.equal(JSON.parse(original).verdict, 'accepted');
  nodeFs.rmSync(path.join(work, 'verdict.json'));
  assert.equal(rebuilt(work), original, 'deleted');
  writeFileSync(path.join(work, 'verdict.json'), '{{ garbage');
  assert.equal(rebuilt(work), original, 'corrupted');
  writeFileSync(path.join(work, 'verdict.json'), JSON.stringify({ ...JSON.parse(original), events: [{ file: 'invented.json', kind: 'halt' }] }));
  const afterInvented = rebuilt(work);
  assert.equal(afterInvented, original, 'an event with no event file is gone');
  assert.equal(JSON.parse(afterInvented).events.length, 0);
  const opens = [];
  rebuildVerdict(work, { ...ARGS, fs: recordingFs(opens) });
  assert.deepEqual(opens.filter(p => p.endsWith('verdict.json')), [], 'the rebuild never opens verdict.json for reading');
  assert.ok(opens.some(p => p.endsWith('build.json')), 'the recording fs sees the files the rebuild does read');
});

test('An_event_is_a_file_written_once_and_a_resume_that_rebuilds_the_snapshot_loses_none', () => {
  const work = scoredSession();
  const kinds = ['halt', 'affinity', 'build-record-mismatch'];
  kinds.forEach((kind, i) => { writeEvent({ work, now: T(i), kind, detail: { reason: `invented ${kind}` } }); rebuildVerdict(work, ARGS); });
  writeEvent({ work, now: T(0), kind: 'halt', detail: { reason: 'a second halt in the same instant' } });
  const files = readdirSync(path.join(work, 'events'));
  assert.equal(new Set(files).size, files.length, 'distinct names');
  assert.equal(files.length, 4);
  assert.ok(files.every(f => /^\d{8}T\d{9}Z-[a-z-]+(-\d+)?\.json$/.test(f)), files.join(' '));
  for (let restart = 0; restart < 3; restart += 1) rebuildVerdict(work, ARGS);
  const events = JSON.parse(readFileSync(path.join(work, 'verdict.json'), 'utf8')).events;
  assert.deepEqual(events.map(e => e.file), [...files].sort(), 'every event, in file order, after any number of restarts');
  assert.throws(() => writeAttempt({ work, n: 1, profile: '2cpu', attempt: 1, body: completedBody() }), /EEXIST/, 'an attempt file is written once');
});

test('The_attempt_files_are_named_by_pass_profile_and_attempt_and_read_back_as_the_passes_the_verdict_rule_takes', () => {
  const work = scoredSession();
  assert.equal(path.basename(attemptFile({ work, n: 2, profile: '8cpu', attempt: 3 })), 'p02-8cpu-a3.json');
  writeAttempt({ work, n: 4, profile: '8cpu', attempt: 2, body: environmentBody('x') });
  const passes = readAttempts({ work, ...ARGS });
  assert.equal(passes.length, 10);
  assert.deepEqual(passes[0].attempts.map(a => [a.attempt, a.kind]), [[1, 'completed']]);
  assert.equal(passes[0].attempts[0].cases.length, 3);
  assert.deepEqual([passes[3].n, passes[3].profile, passes[3].cpus], [4, '8cpu', '0-7']);
  assert.deepEqual(passes[3].attempts.map(a => [a.attempt, a.kind]), [[1, 'completed'], [2, 'environment']]);
});

test('A_stored_completed_attempt_whose_record_is_no_longer_complete_reads_back_as_an_environment_attempt_and_the_file_is_untouched', () => {
  const work = scoredSession();
  const file = attemptFile({ work, n: 5, profile: '2cpu', attempt: 2 });
  const body = completedBody();
  const cases = structuredClone(body.record.cases);
  delete cases[0].lifecycle.leftover; // the cases are all there and the shape is whole to the verdict rule's mapping: only the complete validator sees the gap
  writeAttempt({ work, n: 5, profile: '2cpu', attempt: 2, body: { ...body, record: { ...body.record, cases } } });
  const before = readFileSync(file, 'utf8');
  const read = readAttempts({ work, ...ARGS }).find(p => p.n === 5);
  assert.equal(read.attempts.find(a => a.attempt === 2).kind, 'environment');
  assert.match(read.attempts.find(a => a.attempt === 2).reason, /^record-invalid: /);
  assert.equal(readFileSync(file, 'utf8'), before);
  assert.deepEqual(readEvents({ work }), []);
});
