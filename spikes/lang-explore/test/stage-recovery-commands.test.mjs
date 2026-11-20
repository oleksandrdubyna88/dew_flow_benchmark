// The recovery path of Stage 1 on the command line (F3.S1 (a), PR 4): `stage-archive --series <dir> --to <backup dir>`, `stage-free --series <dir>` and `stage-restore --series <dir> [--rid <rid>]` through `explore.mjs`'s command table, over a Stage 1 series made
// with PR 3's fakes (the fake loop, a registered amendments file in a throw-away folder) whose sessions hold invented cache folders. Each verb takes the series lock. The modules under them (`stage-archive.mjs`) are proven in `stage-archive.test.mjs`; here the
// command layer is: the table and the options, what each verb says and exits with, the read-back slot that waits for its dated lines, the last archive that refuses a freed session, the lock. Nothing is paid, no held-out folder or key is read.
import assert from 'node:assert/strict';
import { appendFileSync, existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../explore.mjs';
import { readRunMap, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { runMapFileOf } from '../lib/series.mjs';
import { stageRun } from '../lib/stage-run.mjs';
import { GO_ALL, GO_PILOT, PILOT_GO_LINE, readbackHolds, runRig } from './stage-run-fixture.mjs';
import { backupFolder, cacheFilesOf, writeSession } from './stage-fixture.mjs';

/** One command through `main`: `{ exit, lines }`. The seams are the rig's own (its home, its registered amendments file). */
async function verb(rig, argv) {
  const lines = [];
  const exit = await main(argv, { home: rig.io.home, say: line => lines.push(line), stage: rig.io.stage, now: () => new Date('2026-11-25T10:00:00.000Z') });
  return { exit, text: lines.join('\n') };
}

/** A series with `slots` slots run by the fake loop (slot 3 is the first read-back slot) and a session folder, with its cache folders, for every session the run-map holds. */
async function seriesWith(slots) {
  const rig = await runRig({ lines: [GO_PILOT, GO_ALL, PILOT_GO_LINE] });
  for (let i = 0; i < slots; i += 1) assert.equal((await stageRun({ values: { series: rig.dir, what: 'next' }, io: rig.io })).exit, 0, `slot ${i + 1}`);
  const rids = Object.keys(readRunMap(runMapFileOf(rig.dir)).value.sessions);
  rids.forEach(rid => writeSession(rig.dir, rid, { cacheBytes: 40 }));
  return { rig, rids, sessionsOf: pair => Object.values(readRunMap(runMapFileOf(rig.dir)).value.sessions).filter(s => s.pair === pair).map(s => s.rid) };
}

const cacheLeft = (dir, rid) => cacheFilesOf(rid).filter(rel => existsSync(path.join(dir, rel))).length;

test('The_three_recovery_verbs_are_commands_of_explore_and_each_names_what_it_needs', async t => {
  const rig = await runRig();
  const backup = backupFolder(t);
  for (const name of ['stage-archive', 'stage-free', 'stage-restore']) {
    const none = await verb(rig, [name]);
    assert.equal(none.exit, 4, `${name} without --series is a refusal`);
    assert.match(none.text, new RegExp(`^${name} needs --series <dir>`), name);
    const foreign = await verb(rig, [name, '--series', path.join(backup, 'not-a-series')]);
    assert.equal(foreign.exit, 4, `${name} on a folder that is no series under the explore root is a refusal`);
  }
  const noTo = await verb(rig, ['stage-archive', '--series', rig.dir]);
  assert.equal(noTo.exit, 4);
  assert.match(noTo.text, /^stage-archive needs --to <backup folder>/);
  const unknown = await verb(rig, ['stage-archive-all']);
  assert.equal(unknown.exit, 4);
});

test('stage_archive_copies_the_series_to_the_backup_and_records_it_and_stage_free_names_what_it_needs_first', async t => {
  const { rig } = await seriesWith(2);
  const backup = backupFolder(t);
  const early = await verb(rig, ['stage-free', '--series', rig.dir]);
  assert.equal(early.exit, 4, 'no archive, nothing is freed');
  assert.match(early.text, /has no archive recorded: run stage-archive --series .* --to <backup folder> first/);
  const archived = await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backup]);
  assert.equal(archived.exit, 0, archived.text);
  assert.match(archived.text.split('\n').at(-1), /^archived \d+ file\(s\), \d+ bytes, to .*; the list [0-9a-f]{12} is .*\.sha256list; slots finished: 1, 2$/, 'the summary is the last line, after the progress lines');
  const entries = JSON.parse(readFileSync(path.join(rig.dir, 'archive.json'), 'utf8')).archives;
  assert.equal(entries.length, 1, 'the archive is recorded once');
  assert.equal(existsSync(path.join(entries[0].copy, 'run-map.json')), true, 'the copy holds the series');
  assert.equal(existsSync(path.join(rig.dir, 'series.lock')), false, 'the lock was released');
});

test('stage_free_through_the_command_layer_frees_the_closed_list_of_finished_slots_and_a_read_back_slot_waits_for_its_dated_lines', async t => {
  const { rig, sessionsOf } = await seriesWith(3);
  const backup = backupFolder(t);
  assert.equal((await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backup])).exit, 0);
  const freed = await verb(rig, ['stage-free', '--series', rig.dir]);
  assert.equal(freed.exit, 0, freed.text);
  assert.match(freed.text, /^freed 16 file\(s\), 640 bytes, of 4 session\(s\)/, 'the four sessions of slots 1 and 2 (four files each, 40 bytes each)');
  const [slot1, slot3] = [sessionsOf(1), sessionsOf(3)];
  assert.deepEqual(slot1.map(rid => cacheLeft(rig.dir, rid)), [0, 0], 'a finished slot is freed');
  assert.deepEqual(slot3.map(rid => cacheLeft(rig.dir, rid)), [4, 4], 'the read-back slot is no finished slot for this verb until its lines exist: the judge\'s reading comes first');
  assert.equal(existsSync(path.join(rig.dir, 's', slot1[0], 'work', 'src', 'main.cs')), true, 'the workspace of a freed session stays');
  appendFileSync(rig.files.amendmentsFile, `${readbackHolds().filter(line => slot3.some(rid => line.includes(rid))).join('\n')}\n`);
  const again = await verb(rig, ['stage-free', '--series', rig.dir]);
  assert.equal(again.exit, 0, again.text);
  assert.match(again.text, /^freed 8 file\(s\), 320 bytes, of 2 session\(s\)/, 'with its dated lines the read-back slot is freed too');
  assert.deepEqual(slot3.map(rid => cacheLeft(rig.dir, rid)), [0, 0]);
});

test('stage_restore_through_the_command_layer_brings_one_session_or_all_back_and_the_last_archive_refuses_a_series_that_still_holds_a_freed_session', async t => {
  const { rig, sessionsOf } = await seriesWith(2);
  const backup = backupFolder(t);
  assert.equal((await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backup])).exit, 0);
  assert.equal((await verb(rig, ['stage-free', '--series', rig.dir])).exit, 0);
  const [first, second] = [...sessionsOf(1), ...sessionsOf(2)];
  const one = await verb(rig, ['stage-restore', '--series', rig.dir, '--rid', first]);
  assert.equal(one.exit, 0, one.text);
  assert.equal(one.text.split('\n').at(-1), `restored 1 session(s): ${first}`);
  assert.equal(cacheLeft(rig.dir, first), 4, 'the cache folders of that session are back');
  assert.equal(cacheLeft(rig.dir, second), 0, 'another freed session stays freed');
  const wrong = await verb(rig, ['stage-restore', '--series', rig.dir, '--rid', first]);
  assert.equal(wrong.exit, 4, 'a session that is not freed is refused');
  assert.match(wrong.text, /is not freed: there is no freed\.json in it/);
  const ended = readRunMap(runMapFileOf(rig.dir)).value;
  writeRunMap(runMapFileOf(rig.dir), { ...ended, stopped: { rule: 'test', at: '2026-11-25T10:00:00.000Z' } });
  const last = await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backup]);
  assert.equal(last.exit, 4, 'the series ended: this archive is the last one, and a freed session is not in the tree');
  assert.match(last.text, /session\(s\) of this series are freed .*run stage-restore --series .* first/);
  const all = await verb(rig, ['stage-restore', '--series', rig.dir]);
  assert.equal(all.exit, 0, all.text);
  assert.match(all.text.split('\n').at(-1), /^restored 3 session\(s\): /);
  assert.equal((await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backup])).exit, 0, 'with every session whole the last archive goes through');
});

test('stage_free_through_the_command_layer_names_the_sessions_that_are_already_freed_and_leaves_them_alone', async t => {
  const { rig, rids } = await seriesWith(2);
  assert.equal((await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backupFolder(t)])).exit, 0);
  assert.equal((await verb(rig, ['stage-free', '--series', rig.dir])).exit, 0);
  const freedRids = rids.filter(rid => existsSync(path.join(rig.dir, 's', rid, 'freed.json'))).sort();
  assert.ok(freedRids.length > 0, 'the first free freed sessions');
  const again = await verb(rig, ['stage-free', '--series', rig.dir]);
  assert.equal(again.exit, 0, again.text);
  assert.equal(again.text, `freed 0 file(s), 0 bytes, of 0 session(s)\nalready freed, left alone (stage-restore first to free again): ${freedRids.join(', ')}`);
});

test('stage_archive_and_stage_restore_print_one_progress_line_per_phase_before_their_last_line', async t => {
  const { rig } = await seriesWith(2);
  const archived = await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backupFolder(t)]);
  assert.equal(archived.exit, 0, archived.text);
  const lines = archived.text.split('\n');
  for (const phase of ['listing', 'copying', 'verifying']) assert.match(archived.text, new RegExp(`^${phase}: (\\d+) of \\1 file\\(s\\)$`, 'm'), `${phase} ends at its total`);
  assert.match(lines.at(-1), /^archived \d+ file\(s\)/, 'the summary is still the last line');
  assert.equal((await verb(rig, ['stage-free', '--series', rig.dir])).exit, 0);
  const restored = await verb(rig, ['stage-restore', '--series', rig.dir]);
  assert.equal(restored.exit, 0, restored.text);
  assert.match(restored.text, /^restoring [0-9a-f]{6}: 4 of 4 file\(s\)$/m, 'a restore says its progress per session');
  assert.match(restored.text.split('\n').at(-1), /^restored 4 session\(s\)/, 'and the summary is the last line');
});

test('Each_recovery_verb_takes_the_series_lock_and_a_held_lock_refuses_it_naming_the_holder', async t => {
  const { rig } = await seriesWith(1);
  const backup = backupFolder(t);
  const held = acquireSeriesLock({ seriesDir: rig.dir });
  assert.equal(held.ok, true, held.reason);
  try {
    for (const argv of [['stage-archive', '--to', backup], ['stage-free'], ['stage-restore']]) {
      const refused = await verb(rig, [argv[0], '--series', rig.dir, ...argv.slice(1)]);
      assert.equal(refused.exit, 4, `${argv[0]} is refused while another process holds the series`);
      assert.match(refused.text, new RegExp(`pid ${process.pid}`), `${argv[0]} names the holder`);
    }
    assert.equal(existsSync(path.join(rig.dir, 'archive.json')), false, 'a refused archive wrote nothing');
  } finally {
    held.value.release();
  }
  assert.equal((await verb(rig, ['stage-archive', '--series', rig.dir, '--to', backup])).exit, 0, 'the control: with the lock free the same call goes through');
});
