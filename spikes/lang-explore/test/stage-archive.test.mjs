// The recovery path of Stage 1 that keeps the data (F3.S1 (a), PR 4, scenarios 54 to 54g of the stories document): `stage-archive` (a verified copy of the whole series to the private backup, with the sha256 list), `stage-free` (the ONE verb that frees
// local space, a closed list of three cache folders of final sessions of finished slots, each file proven by the verified archive) and `stage-restore` (the freed folders back from the archive, verified). Fakes and temporary folders only: the
// "backup" is an owned folder, the sessions are a few invented files, no held-out content exists here.
import assert from 'node:assert/strict';
import { appendFileSync, chmodSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { appendLedger, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { openSeries, planSession, readRunMap, transition, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { loadGrowth } from '../lib/growth.mjs';
import { ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { FREEABLE, archiveSeries, freeSeries, freedSessionProblem, restoreSeries } from '../lib/stage-archive.mjs';
import * as stageArchive from '../lib/stage-archive.mjs';
import { diskGate } from '../lib/stage-disk.mjs';
import { readbackSlots, unreadReadbackSlots } from '../lib/stage-schedule.mjs';
import { GOOD, PILOT, SLOTS, backupFolder, cacheFilesOf, finished, mapWith, ridOf, seriesFolder, sha256Text, snapshotOf, withPilot, writeSession } from './stage-fixture.mjs';

const NOW = () => new Date('2026-11-25T10:00:00.000Z');
const SKIP = ['series.lock', 'archive.json'];
const byCode = (a, b) => (a < b ? -1 : Number(a > b));
/** Slot 1 finished, slot 2 half done (C# scored, Node launching): one finished slot and one open one. */
const MAP = mapWith([...finished(1), [2, 'csharp', 1, GOOD], [2, 'node', 1, 'launching']]);
const archived = t => {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  const to = backupFolder(t);
  const made = archiveSeries({ series, to, now: NOW });
  assert.equal(made.ok, true, made.reason);
  return { series, to, made: made.value };
};
const archiveEntries = series => JSON.parse(readFileSync(path.join(series, 'archive.json'), 'utf8')).archives;
const flipByte = file => { const bytes = readFileSync(file); bytes[0] ^= 0xff; writeFileSync(file, bytes); };
const freedFiles = rids => rids.flatMap(cacheFilesOf);
/** The reading a caller gives `freeSeries`: the run-map as it is on disk and the read-back slots that wait. */
const readingOf = (series, unreadSlots = []) => () => { const map = readRunMap(runMapFileOf(series)); return map.ok ? ok({ map: map.value, unreadSlots }) : map; };
const freedFileOf = (series, rid) => JSON.parse(readFileSync(path.join(series, 's', rid, 'freed.json'), 'utf8'));

test('A_copy_that_differs_from_the_source_in_one_byte_fails_the_archives_verification', t => {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  symlinkSync('note.txt', path.join(series, 'private', 'link'));
  const to = backupFolder(t);
  const made = archiveSeries({ series, to, now: NOW });
  assert.equal(made.ok, true, made.reason);
  const expected = Object.entries(snapshotOf(series, { skip: SKIP })).sort(([a], [b]) => byCode(a, b)).map(([file, hash]) => `${hash}  ${file}`).join('\n').concat('\n');
  const entries = archiveEntries(series);
  assert.equal(entries.length, 1);
  assert.equal(entries[0].listSha256, sha256Text(expected), 'the list is one line per file, `sha256  path`, a link recorded by its target and never followed');
  assert.match(expected, /^link:[0-9a-f]{64} {2}private\/link$/m, 'the link is in the list as a link');
  assert.equal(entries[0].date, '2026-11-25T10:00:00.000Z');
  assert.deepEqual(entries[0].slotsFinished, [1], 'the slots finished at that time');
  assert.equal(entries[0].to, to);
  assert.equal(readFileSync(entries[0].listFile, 'utf8'), expected, 'the list is kept beside the copy');
  assert.deepEqual(snapshotOf(entries[0].copy), snapshotOf(series, { skip: SKIP }), 'the copy equals the source file by file');
  assert.equal(lstatSync(path.join(entries[0].copy, 'private', 'link')).isSymbolicLink(), true, 'a link is copied as a link');
  assert.equal(entries[0].bytes, Object.keys(snapshotOf(series, { skip: [...SKIP, 'private/link'] })).reduce((n, file) => n + statSync(path.join(series, file)).size, 0), 'the bytes are the size of the files');
  const target = `s/${ridOf(1, 'csharp')}/work/src/main.cs`;
  const bad = archiveSeries({ series, to: backupFolder(t), now: NOW, hooks: { afterCopy: copy => flipByte(path.join(copy, target)) } });
  assert.equal(bad.ok, false, 'a copy that differs in one byte is not an archive');
  assert.ok(bad.reason.includes(target), `the file is named: ${bad.reason}`);
  assert.equal(archiveEntries(series).length, 1, 'a failed archive appends nothing to archive.json');
});

test('stage_free_removes_only_the_three_cache_folders_of_final_sessions_in_finished_slots', t => {
  assert.deepEqual([...FREEABLE], ['box/nuget', 'box/npm', 'box/home/.dotnet'], 'the closed list');
  const { series } = archived(t);
  const before = snapshotOf(series, { skip: SKIP });
  const freed = freeSeries({ series, reading: readingOf(series) });
  assert.equal(freed.ok, true, freed.reason);
  const after = snapshotOf(series, { skip: SKIP });
  const gone = freedFiles([ridOf(1, 'csharp'), ridOf(1, 'node')]);
  const expected = { ...before };
  for (const file of gone) delete expected[file];
  for (const rid of [ridOf(1, 'csharp'), ridOf(1, 'node')]) expected[`s/${rid}/freed.json`] = after[`s/${rid}/freed.json`];
  assert.deepEqual(after, expected, 'every other file of the series has its bytes: the workspace trees, score, records, streams, the root files; the open slot\'s sessions are whole, the scored session of an unfinished slot included');
  const listing = JSON.parse(readFileSync(path.join(series, 's', ridOf(1, 'csharp'), 'freed.json'), 'utf8'));
  assert.deepEqual(listing.removed.map(f => f.path).sort(), ['box/home/.dotnet/d.dat', 'box/npm/_cacache/c.tgz', 'box/nuget/pkg-a/a.nupkg', 'box/nuget/pkg-a/b.dll'], 'freed.json lists what was removed');
  assert.equal(listing.archive.listSha256, archiveEntries(series)[0].listSha256, 'and the archive that holds it');
  assert.deepEqual(freed.value.sessions.sort(), [ridOf(1, 'csharp'), ridOf(1, 'node')].sort());
  assert.equal(freed.value.kept.length, 0);
});

test('stage_free_removes_nothing_that_is_not_in_the_verified_archive_with_the_same_hash_and_names_what_it_kept', t => {
  const { series, made } = archived(t);
  const rid = ridOf(1, 'csharp');
  const added = path.join(series, 's', rid, 'box', 'npm', 'late.tgz');
  writeFileSync(added, 'made after the archive');
  const changed = path.join(series, 's', rid, 'box', 'nuget', 'pkg-a', 'a.nupkg');
  appendFileSync(changed, 'changed after the archive');
  const copied = path.join(archiveEntries(series)[0].copy, 's', ridOf(1, 'node'), 'box', 'npm', '_cacache', 'c.tgz');
  flipByte(copied);
  const freed = freeSeries({ series, reading: readingOf(series) });
  assert.equal(freed.ok, true, freed.reason);
  assert.equal(existsSync(added), true, 'a file the archive does not list is kept');
  assert.equal(existsSync(changed), true, 'a file whose hash is not the listed one is kept');
  assert.equal(existsSync(path.join(series, 's', ridOf(1, 'node'), 'box', 'npm', '_cacache', 'c.tgz')), true, 'a file whose archived copy no longer has the listed hash is kept');
  const kept = freed.value.kept.map(k => k.path).sort();
  assert.deepEqual(kept, [`s/${rid}/box/npm/late.tgz`, `s/${rid}/box/nuget/pkg-a/a.nupkg`, `s/${ridOf(1, 'node')}/box/npm/_cacache/c.tgz`].sort(), 'and each is named');
  assert.ok(freed.value.kept.every(k => typeof k.why === 'string' && k.why.length > 0), 'with the reason');
  assert.equal(existsSync(path.join(series, 's', rid, 'box', 'nuget', 'pkg-a', 'b.dll')), false, 'what is proven is removed');
  assert.ok(made.listSha256.length === 64);
});

test('stage_free_refuses_when_the_archive_is_not_at_its_place_or_no_longer_verifies', t => {
  const bare = seriesFolder(t, MAP, { cacheBytes: 50 });
  const none = freeSeries({ series: bare, reading: readingOf(bare) });
  assert.equal(none.ok, false, 'no archive recorded');
  assert.match(none.reason, /stage-archive/, 'it names the verb that makes one');
  const cases = [
    ['the copy is gone', ({ copy }) => rmSync(copy, { recursive: true, force: true })],
    ['the list is gone', ({ listFile }) => rmSync(listFile)],
    ['the list is no longer the recorded one', ({ listFile }) => appendFileSync(listFile, `${'0'.repeat(64)}  s/extra/file\n`)],
  ];
  for (const [what, damage] of cases) {
    const { series } = archived(t);
    const before = snapshotOf(series, { skip: SKIP });
    damage(archiveEntries(series)[0]);
    const refused = freeSeries({ series, reading: readingOf(series) });
    assert.equal(refused.ok, false, `${what}: refused`);
    assert.deepEqual(snapshotOf(series, { skip: SKIP }), before, `${what}: nothing was removed and nothing was written`);
  }
});

test('stage_restore_brings_a_freed_session_back_to_its_listed_bytes_and_a_copy_that_differs_is_refused', t => {
  const { series } = archived(t);
  const before = snapshotOf(series, { skip: SKIP });
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true);
  const rid = ridOf(1, 'csharp');
  const restored = restoreSeries({ series, rid });
  assert.equal(restored.ok, true, restored.reason);
  assert.equal(existsSync(path.join(series, 's', rid, 'freed.json')), false, 'freed.json is removed once everything is back');
  assert.ok(existsSync(path.join(series, 's', ridOf(1, 'node'), 'freed.json')), 'a session that was not asked for stays freed');
  const mid = snapshotOf(series, { skip: SKIP });
  for (const file of cacheFilesOf(rid)) assert.equal(mid[file], before[file], `${file} is back with its listed bytes`);
  const bad = restoreSeries({ series, rid: ridOf(1, 'node') });
  assert.equal(bad.ok, true, 'a second session restores on its own');
  assert.deepEqual(snapshotOf(series, { skip: SKIP }), before, 'every byte of the series is as it was before anything was freed');
  const again = archived(t);
  assert.equal(freeSeries({ series: again.series, reading: readingOf(again.series) }).ok, true);
  const copy = path.join(archiveEntries(again.series)[0].copy, 's', rid, 'box', 'npm', '_cacache', 'c.tgz');
  flipByte(copy);
  const refused = restoreSeries({ series: again.series, rid });
  assert.equal(refused.ok, false, 'a copy that differs from its listed bytes is refused');
  assert.ok(refused.reason.includes('box/npm/_cacache/c.tgz'), `the file is named: ${refused.reason}`);
  assert.equal(existsSync(path.join(again.series, 's', rid, 'box', 'npm', '_cacache', 'c.tgz')), false, 'a copy that differs is never placed');
  assert.ok(existsSync(path.join(again.series, 's', rid, 'freed.json')), 'the session stays freed until it can be restored');
});

// ---- 54e: a series that halted, was archived, freed and resumed ends with the records of one that never halted -----------------------------------------------------------------------------------------------------------

/** The fake run of slots `from` to `to`: what the real loop leaves, with invented bytes (no time in them but the ledger's `t`). */
function fakeRun(series, from, to) {
  for (let slot = from; slot <= to; slot += 1) {
    let map = readRunMap(runMapFileOf(series)).value;
    for (const stack of ['csharp', 'node']) {
      const rid = ridOf(slot, stack);
      map = planSession(map, { rid, pair: slot, stack, phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: SLOTS[slot - 1].masks[stack], task: SLOTS[slot - 1].task, round: SLOTS[slot - 1].round }).value;
      for (const [to_, patch] of [['launching', {}], ['ran', { class: 'completed', data: true }], ['scored', {}]]) map = transition(map, rid, to_, patch).value;
      writeSession(series, rid, { cacheBytes: 50 });
      appendLedger(ledgerFileOf(series), { kind: 'data', usd: 1.25, what: 'completed', rid, t: `2026-11-25T10:0${slot}:00Z` });
    }
    writeRunMap(runMapFileOf(series), map);
    mkdirSync(path.join(series, 'pairs'), { recursive: true });
    writeFileSync(path.join(series, 'pairs', `p${slot}.json`), `${JSON.stringify({ slot, rids: ['csharp', 'node'].map(stack => ridOf(slot, stack)) })}\n`);
  }
}

/** The adjudication's reader, as a fake: it reads `score/passes/` and the records of every scored session and nothing under `box/`. */
const readerOf = series => Object.values(readRunMap(runMapFileOf(series)).value.sessions).filter(s => s.status === 'scored' && s.kind === 'data').map(s => {
  const dir = path.join(series, 's', s.rid);
  return [s.rid, readdirSync(path.join(dir, 'score', 'passes')).map(f => readFileSync(path.join(dir, 'score', 'passes', f), 'utf8')), readFileSync(path.join(dir, 'record.json'), 'utf8')];
});

const freshSeries = t => {
  const dir = seriesFolder(t, withPilot(openSeries({ seriesId: 'x-test', caps: {} })), { cacheBytes: 50 });
  return dir;
};
const chargesOf = series => readLedger(ledgerFileOf(series)).filter(l => l.event === undefined);

test('A_series_that_halted_on_the_disk_rule_was_archived_freed_and_resumed_ends_with_the_records_of_one_that_never_halted', t => {
  const straight = freshSeries(t);
  fakeRun(straight, 1, 4);
  const halted = freshSeries(t);
  fakeRun(halted, 1, 2);
  const map = readRunMap(runMapFileOf(halted)).value;
  const gate = { series: halted, map, pilot: PILOT, growth: loadGrowth().value, floorBytes: 1_400_000_000, ledgerFile: ledgerFileOf(halted), now: NOW, slot: 3 };
  const stopped = diskGate({ ...gate, freeBytes: 1_000_000_000 });
  assert.equal(stopped.go, false, 'the disk rule halts before slot 3');
  const to = backupFolder(t);
  assert.equal(archiveSeries({ series: halted, to, now: NOW }).ok, true);
  const freed = freeSeries({ series: halted, reading: readingOf(halted) });
  assert.equal(freed.ok, true, freed.reason);
  assert.equal(freed.value.sessions.length, 4, 'the four sessions of slots 1 and 2');
  assert.equal(diskGate({ ...gate, freeBytes: 10 ** 13 }).go, true, 'the space is there: the series goes on');
  fakeRun(halted, 3, 4);
  assert.deepEqual(readerOf(halted), readerOf(straight), 'the adjudication\'s reader needs no freed folder: it reads the same bytes');
  assert.equal(freedSessionProblem(halted) === null, false, 'the series still holds freed sessions until the restore');
  assert.equal(restoreSeries({ series: halted }).ok, true);
  assert.equal(freedSessionProblem(halted), null);
  assert.deepEqual(snapshotOf(halted, { skip: [...SKIP, 'ledger.ndjson'] }), snapshotOf(straight, { skip: SKIP.concat('ledger.ndjson') }), 'every record, pair record, the run-map and every session folder equal those of the straight run');
  assert.deepEqual(chargesOf(halted).map(({ t: _t, ...line }) => line), chargesOf(straight).map(({ t: _t, ...line }) => line), 'the ledger equals but for time stamps');
  assert.deepEqual(readLedger(ledgerFileOf(halted)).filter(l => l.event !== undefined).map(l => l.event), [stopped.halt], 'and for the event line of the halt');
});

test('The_final_hash_list_and_the_last_archive_refuse_a_series_that_still_holds_a_freed_session', t => {
  const { series, to } = archived(t);
  assert.equal(freedSessionProblem(series), null, 'nothing is freed yet');
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true);
  const problem = freedSessionProblem(series);
  assert.match(problem, /stage-restore/, 'it names the verb that brings the folders back');
  assert.ok(problem.includes(ridOf(1, 'csharp')) && problem.includes(ridOf(1, 'node')), `and the sessions: ${problem}`);
  const single = freedSessionProblem(series, ridOf(1, 'csharp'));
  assert.match(single, /stage-restore/, 'a command that rebuilds one finished session\'s record from its folder refuses a freed session the same way');
  assert.equal(freedSessionProblem(series, ridOf(2, 'csharp')), null, 'a session that was not freed is not refused');
  const last = archiveSeries({ series, to, now: NOW, final: true });
  assert.equal(last.ok, false, 'the last archive refuses a series that holds a freed session');
  assert.match(last.reason, /stage-restore/);
  assert.equal(archiveEntries(series).length, 1, 'and appends nothing');
  const middle = archiveSeries({ series, to, now: NOW });
  assert.equal(middle.ok, true, 'an archive at a disk halt is made while sessions are freed (the freed files stay in the earlier copy)');
  assert.equal(restoreSeries({ series }).ok, true);
  assert.equal(archiveSeries({ series, to, now: NOW, final: true }).ok, true, 'after the restore the last archive goes on');
});

test('stage_free_leaves_a_read_back_slot_alone_until_its_dated_lines_exist', t => {
  const rb = readbackSlots({ seed: 'lv2-0123456789abcdef' })[0];
  const map = mapWith(finished(rb.slot));
  const series = seriesFolder(t, map, { cacheBytes: 50 });
  assert.equal(archiveSeries({ series, to: backupFolder(t), now: NOW }).ok, true);
  const line = stack => ({ g: 'G-1', verdict: 'holds', date: '2026-11-26', rid: ridOf(rb.slot, stack) });
  const unread = unreadReadbackSlots({ map, slots: SLOTS, readbackLines: [] });
  assert.deepEqual(unread.map(u => u.slot), [rb.slot], 'the schedule says the read-back slot awaits its lines');
  const rbRids = [ridOf(rb.slot, 'csharp'), ridOf(rb.slot, 'node')];
  const ofSlot = snap => Object.fromEntries(Object.entries(snap).filter(([file]) => rbRids.some(rid => file.startsWith(`s/${rid}/`))));
  const before = snapshotOf(series, { skip: SKIP });
  const held = freeSeries({ series, reading: readingOf(series, unread.map(u => u.slot)) });
  assert.equal(held.ok, true, held.reason);
  assert.deepEqual(ofSlot(snapshotOf(series, { skip: SKIP })), ofSlot(before), 'no file of the read-back slot is touched: the judge\'s reading comes first');
  assert.equal(held.value.sessions.some(rid => rbRids.includes(rid)), false, 'and the verb says it freed none of its sessions');
  assert.equal(rb.slot > 1 ? held.value.sessions.length > 0 : true, true, 'the finished slots before it are freed as usual');
  const read = unreadReadbackSlots({ map, slots: SLOTS, readbackLines: [line('csharp'), line('node')] });
  assert.deepEqual(read, [], 'with both dated lines the slot no longer awaits');
  const freed = freeSeries({ series, reading: readingOf(series, read.map(u => u.slot)) });
  assert.deepEqual(freed.value.sessions.sort(), rbRids.sort(), 'and the verb frees its sessions');
});

// ---- coai code round 1 (PR 4), finding A: the archive's place may never be the series itself ------------------------------------------------------------------------------------------------------------------------

/** The files beside the series (in its parent) that an archive of it would have made: its copy and its list. */
const besideSeries = series => readdirSync(path.dirname(series)).filter(name => name.startsWith(path.basename(series)) && name !== path.basename(series) && !name.endsWith('.owner'));

/** An archive to `to` is refused with a sentence about the place, and nothing is copied, written beside the series or recorded. */
function assertPlaceRefused({ t, to, what }) {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  const target = to(series);
  const before = snapshotOf(series, { skip: SKIP });
  const refused = archiveSeries({ series, to: target, now: NOW });
  assert.equal(refused.ok, false, `${what}: the archive is refused`);
  assert.match(refused.reason, /series folder/, `${what}: the sentence names the series folder: ${refused.reason}`);
  assert.deepEqual(snapshotOf(series, { skip: SKIP }), before, `${what}: nothing was copied into the series`);
  assert.equal(existsSync(path.join(series, 'archive.json')), false, `${what}: nothing is recorded`);
  assert.deepEqual(besideSeries(series), [], `${what}: no copy and no list is written beside the series`);
  return series;
}

test('An_archive_whose_backup_folder_is_the_parent_of_the_series_is_refused_because_the_copy_would_be_the_series_itself', t => {
  assertPlaceRefused({ t, to: series => path.dirname(series), what: 'the parent folder' });
});

test('An_archive_whose_backup_folder_is_a_link_to_the_parent_of_the_series_is_refused', t => {
  assertPlaceRefused({ t, what: 'a link to the parent', to: series => { const link = path.join(backupFolder(t), 'link-to-parent'); symlinkSync(path.dirname(series), link); return link; } });
});

test('An_archive_whose_backup_folder_is_inside_the_series_or_a_link_into_it_is_refused', t => {
  assertPlaceRefused({ t, to: series => path.join(series, 'private'), what: 'a folder inside the series' });
  assertPlaceRefused({ t, what: 'a link into the series', to: series => { const link = path.join(backupFolder(t), 'link-into-series'); symlinkSync(path.join(series, 'private'), link); return link; } });
});

test('Two_paths_overlap_when_they_are_equal_or_one_is_inside_the_other_and_a_longer_name_is_not_inside', () => {
  assert.deepEqual([['/a/s', '/a/s'], ['/a/s/x', '/a/s'], ['/a/s', '/a/s/x'], ['/a/sx', '/a/s'], ['/a/t', '/a/s']].map(([a, b]) => stageArchive.pathsOverlap(a, b)), [true, true, true, false, false]);
});

// ---- coai code round 1 (PR 4), finding B: a session is freed once; its archive is the one `freed.json` names ------------------------------------------------------------------------------------------------------

test('A_session_that_is_already_freed_is_named_and_left_alone_by_the_next_free_and_stage_restore_still_brings_it_back_from_its_own_archive', t => {
  const { series } = archived(t);
  const before = snapshotOf(series, { skip: SKIP });
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true, 'archive A, then the first free');
  const rids = [ridOf(1, 'csharp'), ridOf(1, 'node')];
  const late = path.join(series, 's', rids[0], 'box', 'npm', 'late.tgz');
  mkdirSync(path.dirname(late), { recursive: true });
  writeFileSync(late, 'a cache file made after the first free');
  const second = archiveSeries({ series, to: backupFolder(t), now: NOW });
  assert.equal(second.ok, true, `archive B at another place: ${second.reason}`);
  const named = freedFileOf(series, rids[0]);
  const again = freeSeries({ series, reading: readingOf(series) });
  assert.equal(again.ok, true, again.reason);
  assert.equal(existsSync(late), true, 'the second free removes nothing in a session that is already freed');
  assert.deepEqual(again.value.sessions, [], 'it frees no session');
  assert.deepEqual([...again.value.alreadyFreed].sort(), [...rids].sort(), 'and names the sessions it left alone');
  assert.deepEqual(freedFileOf(series, rids[0]), named, 'freed.json still names archive A and its whole list');
  assert.equal(named.archive.listSha256, archiveEntries(series)[0].listSha256, 'the archive of the first free');
  assert.equal(restoreSeries({ series }).ok, true, 'the restore from A brings the sessions back');
  const restored = snapshotOf(series, { skip: SKIP });
  for (const file of rids.flatMap(cacheFilesOf)) assert.equal(restored[file], before[file], `${file} is back with its listed bytes`);
});

// ---- coai code round 1 (PR 4), C1: a free that is not told the unread slots is refused ------------------------------------------------------------------------------------------------------------------------------------

test('A_free_that_is_not_given_a_reading_with_the_unread_slots_is_refused_with_a_sentence_and_removes_nothing', t => {
  const { series } = archived(t);
  const before = snapshotOf(series, { skip: SKIP });
  const noList = () => ok({ map: readRunMap(runMapFileOf(series)).value, unreadSlots: '3' });
  for (const [what, args] of [['no reading', { series }], ['a reading that is not a function', { series, reading: [] }], ['a reading without a list of unread slots', { series, reading: noList }]]) {
    const refused = freeSeries(args);
    assert.equal(refused.ok, false, `${what}: refused`);
    assert.match(refused.reason, /reading|unreadSlots/, `${what}: the sentence names what is missing`);
    assert.deepEqual(snapshotOf(series, { skip: SKIP }), before, `${what}: nothing was removed and nothing was written`);
  }
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true, 'an empty list is an answer: no slot waits');
});

// ---- coai code round 1 (PR 4), C2: a long operation says its progress ----------------------------------------------------------------------------------------------------------------------------------------------------

const phaseLines = (lines, phase) => lines.filter(line => line.startsWith(`${phase}: `) && line.endsWith('file(s)'));
const indexOfPhase = (lines, phase) => lines.findIndex(line => line.startsWith(`${phase}: `));

test('Stage_archive_says_its_progress_in_the_listing_the_copying_and_the_verifying_phase_in_that_order_and_stage_restore_says_its_own', t => {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  const total = Object.keys(snapshotOf(series, { skip: SKIP })).length;
  const lines = [];
  const made = archiveSeries({ series, to: backupFolder(t), now: NOW, progress: line => lines.push(line), progressEvery: 10 });
  assert.equal(made.ok, true, made.reason);
  for (const phase of ['listing', 'copying', 'verifying']) {
    const own = phaseLines(lines, phase);
    assert.equal(own[0], `${phase}: 0 of ${total} file(s)`, `${phase} starts at 0`);
    assert.ok(own.includes(`${phase}: 10 of ${total} file(s)`), `${phase} says a line every 10 files`);
    assert.equal(own.at(-1), `${phase}: ${total} of ${total} file(s)`, `${phase} ends at the total`);
  }
  assert.ok(indexOfPhase(lines, 'listing') < indexOfPhase(lines, 'copying') && indexOfPhase(lines, 'copying') < indexOfPhase(lines, 'verifying'), 'in the order of the work');
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true);
  const rid = ridOf(1, 'csharp');
  const restoring = [];
  assert.equal(restoreSeries({ series, rid, progress: line => restoring.push(line), progressEvery: 3 }).ok, true);
  assert.deepEqual(restoring.filter(line => line.includes(rid)).filter(line => /of 4 file\(s\)$/.test(line)).filter(line => /^(verifying|restoring)/.test(line)).slice(0, 2), [`verifying ${rid} (archive copy): 0 of 4 file(s)`, `verifying ${rid} (archive copy): 3 of 4 file(s)`], 'the archive copy is verified first, a line every 3 files');
  assert.equal(restoring.includes(`restoring ${rid}: 0 of 4 file(s)`) && restoring.includes(`restoring ${rid}: 4 of 4 file(s)`), true, 'then the files are placed');
  assert.equal(restoring.includes(`verifying ${rid} (restored): 4 of 4 file(s)`), true, 'and the placed files are verified');
});

test('Without_a_progress_callback_a_long_operation_says_nothing_and_does_the_same_work', t => {
  const { series } = archived(t);
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true);
  assert.equal(restoreSeries({ series }).ok, true, 'a restore without a callback goes through');
});

// ---- Opus review (PR 4), O2: a cache folder that is a link out of the series is kept and named, never followed ------------------------------------------------------------------------------------------------

/** What a folder is: every entry under it with its mode, and its own mode. A link is not followed. */
const stateOf = dir => ({ own: lstatSync(dir).mode & 0o777, entries: readdirSync(dir, { recursive: true }).map(String).sort().map(name => [name, lstatSync(path.join(dir, name)).mode & 0o777]) });

test('A_cache_folder_that_is_a_link_out_of_the_series_or_lies_under_one_is_kept_and_named_and_nothing_outside_is_changed_or_removed', t => {
  for (const linked of ['box/home/.dotnet', 'box/home', 'box']) {
    const { series } = archived(t);
    const rid = ridOf(1, 'csharp');
    const inside = path.join(series, 's', rid, linked);
    const outside = path.join(backupFolder(t), 'moved-out');
    renameSync(inside, outside);
    symlinkSync(outside, inside);
    mkdirSync(path.join(outside, 'empty-folder'));
    for (const name of readdirSync(outside, { recursive: true }).map(String).filter(n => lstatSync(path.join(outside, n)).isFile())) chmodSync(path.join(outside, name), 0o444);
    const before = stateOf(outside);
    const bytesBefore = snapshotOf(outside);
    const freed = freeSeries({ series, reading: readingOf(series) });
    assert.equal(freed.ok, true, `${linked}: the free does not throw or fail: ${freed.reason}`);
    assert.deepEqual(stateOf(outside), before, `${linked}: nothing outside the series changed its mode, and its empty folder is still there`);
    assert.deepEqual(snapshotOf(outside), bytesBefore, `${linked}: and no file outside was removed`);
    assert.equal(lstatSync(inside).isSymbolicLink(), true, `${linked}: the link itself stays`);
    const kept = freed.value.kept.filter(k => k.path === `s/${rid}/box/home/.dotnet`);
    assert.equal(kept.length, 1, `${linked}: the cache root is named as kept`);
    assert.match(kept[0].why, /is a link/, `${linked}: with the reason: ${kept[0].why}`);
    const other = freed.value.kept.filter(k => !k.path.startsWith(`s/${rid}/box/`) || k.path.startsWith(`s/${rid}/${linked}`) === false);
    assert.deepEqual(other, [], `${linked}: only roots on or under the link are kept`);
  }
});

// ---- coai code round 2 (PR 4), R1: a copy is never written through a link in a backup that already exists ------------------------------------------------------------------------------------------------------------

test('A_backup_that_already_holds_a_link_on_the_way_to_a_listed_file_refuses_the_whole_archive_and_nothing_outside_changes', t => {
  for (const linked of ['s', `s/${ridOf(1, 'csharp')}/box`, `s/${ridOf(1, 'csharp')}/box/nuget`]) {
    const series = seriesFolder(t, MAP, { cacheBytes: 50 });
    const to = backupFolder(t);
    const dest = path.join(to, path.basename(series));
    const outside = path.join(backupFolder(t), 'outside');
    mkdirSync(path.join(outside, 'nuget', 'pkg-a'), { recursive: true });
    writeFileSync(path.join(outside, 'nuget', 'pkg-a', 'a.nupkg'), 'outside original');
    mkdirSync(path.dirname(path.join(dest, linked)), { recursive: true });
    symlinkSync(outside, path.join(dest, linked));
    const before = snapshotOf(outside);
    const refused = archiveSeries({ series, to, now: NOW });
    assert.equal(refused.ok, false, `${linked}: the archive is refused`);
    assert.ok(refused.reason.includes(path.join(dest, linked)), `${linked}: the link is named: ${refused.reason}`);
    assert.deepEqual(snapshotOf(outside), before, `${linked}: no file outside the backup was written or replaced`);
    assert.equal(existsSync(path.join(series, 'archive.json')), false, `${linked}: nothing is recorded`);
  }
});

test('A_target_that_is_itself_a_link_or_a_planted_temporary_link_is_replaced_and_never_written_through', t => {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  const to = backupFolder(t);
  const dest = path.join(to, path.basename(series));
  const outside = path.join(backupFolder(t), 'outside-file');
  writeFileSync(outside, 'outside original');
  const target = path.join(dest, 'private', 'note.txt');
  mkdirSync(path.dirname(target), { recursive: true });
  symlinkSync(outside, target);
  const planted = `${path.join(dest, 'pairs', 'p1.json')}.${process.pid}.tmp`;
  mkdirSync(path.dirname(planted), { recursive: true });
  symlinkSync(outside, planted);
  const made = archiveSeries({ series, to, now: NOW });
  assert.equal(made.ok, true, made.reason);
  assert.equal(readFileSync(outside, 'utf8'), 'outside original', 'nothing was written through a link, neither the target\'s nor a planted temporary one');
  assert.equal(lstatSync(target).isSymbolicLink(), false, 'the link target was replaced by the file');
  assert.equal(readFileSync(target, 'utf8'), 'private\n', 'with the listed bytes');
});

// ---- coai code round 2 (PR 4), R2: the final archive asks for freed sessions under the series lock ------------------------------------------------------------------------------------------------------------------

test('The_final_archive_refuses_a_session_that_a_free_made_between_its_first_look_and_its_lock', t => {
  const { series, to } = archived(t);
  const racing = archiveSeries({ series, to, now: NOW, final: true, hooks: { beforeLock: () => assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true, 'the free that comes in between') } });
  assert.equal(racing.ok, false, 'the final archive sees the freed sessions under the lock');
  assert.match(racing.reason, /stage-restore/, racing.reason);
  assert.equal(archiveEntries(series).length, 1, 'and appends nothing');
});

// ---- coai code round 2 (PR 4), R4: an archive.json that holds no list is a refusal, never a TypeError --------------------------------------------------------------------------------------------------------------------

test('An_archive_json_without_a_list_of_archives_is_refused_with_a_sentence_by_the_free_and_by_the_archive', t => {
  for (const text of ['{}', '{"archives": null}', '{"archives": "none"}']) {
    const { series } = archived(t);
    writeFileSync(path.join(series, 'archive.json'), `${text}\n`);
    const before = snapshotOf(series, { skip: ['series.lock'] });
    const freed = freeSeries({ series, reading: readingOf(series) });
    assert.equal(freed.ok, false, `${text}: the free is refused`);
    assert.match(freed.reason, /archive\.json.*no list of archives/, `${text}: ${freed.reason}`);
    const to = backupFolder(t);
    const made = archiveSeries({ series, to, now: NOW });
    assert.equal(made.ok, false, `${text}: the archive is refused`);
    assert.match(made.reason, /archive\.json.*no list of archives/, `${text}: ${made.reason}`);
    assert.deepEqual(readdirSync(to), [], `${text}: nothing was copied before the refusal`);
    assert.deepEqual(snapshotOf(series, { skip: ['series.lock'] }), before, `${text}: nothing in the series changed`);
  }
});

// ---- coai code round 2 (PR 4), R5 and R6: the free says its progress while it verifies and while it removes ------------------------------------------------------------------------------------------------------------

test('Stage_free_says_its_progress_per_session_while_it_verifies_the_caches_and_while_it_removes_them', t => {
  const { series } = archived(t);
  const [first, second] = [ridOf(1, 'csharp'), ridOf(1, 'node')];
  const lines = [];
  const freed = freeSeries({ series, reading: readingOf(series), progress: line => lines.push(line), progressEvery: 3 });
  assert.equal(freed.ok, true, freed.reason);
  for (const rid of [first, second]) {
    const own = lines.filter(line => line.includes(rid));
    assert.deepEqual(own.filter(line => line.startsWith('verifying')), [`verifying ${rid}: 0 of 4 file(s)`, `verifying ${rid}: 3 of 4 file(s)`, `verifying ${rid}: 4 of 4 file(s)`], `${rid}: verifying says a line every 3 files`);
    assert.deepEqual(own.filter(line => line.startsWith('removing')), [`removing ${rid}: 0 of 4 file(s)`, `removing ${rid}: 3 of 4 file(s)`, `removing ${rid}: 4 of 4 file(s)`], `${rid}: removing says a line every 3 files`);
    assert.ok(own.findIndex(line => line.startsWith('verifying')) < own.findIndex(line => line.startsWith('removing')), `${rid}: verify first, then remove`);
  }
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true, 'without a callback the free says nothing and does the same work');
});

// ---- coai code round 2 (PR 4), R7: the listing says it started and counts the entries while it walks ------------------------------------------------------------------------------------------------------------------

test('The_archive_listing_says_it_started_before_the_walk_and_a_line_every_N_entries_while_it_walks_before_the_hashing_lines', t => {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  const total = Object.keys(snapshotOf(series, { skip: SKIP })).length;
  const lines = [];
  assert.equal(archiveSeries({ series, to: backupFolder(t), now: NOW, progress: line => lines.push(line), progressEvery: 10 }).ok, true);
  assert.equal(lines[0], 'listing: started', 'the first line is said before the walk');
  const walked = lines.map((line, i) => [line, i]).filter(([line]) => /^listing: \d+ entries walked$/.test(line));
  assert.deepEqual(walked.map(([line]) => line), Array.from({ length: Math.floor(total / 10) }, (_, i) => `listing: ${(i + 1) * 10} entries walked`), 'a line every 10 entries');
  assert.ok(walked.every(([, i]) => i < lines.indexOf(`listing: 0 of ${total} file(s)`)), 'all of them before the first hashing line');
});

// ---- coai code round 3 (PR 4), the link audit: every site that writes under a root the operator does not control, with a planted link ------------------------------------------------------------------------------------

/** A file outside the series and the backup, with its bytes, for a link to point at. */
const outsideFile = t => { const file = path.join(backupFolder(t), 'outside-file'); writeFileSync(file, 'outside original'); return file; };
const outsideIntact = file => readFileSync(file, 'utf8') === 'outside original';

test('A_restore_refuses_a_link_planted_in_a_folder_on_the_way_to_a_freed_file_and_writes_nothing_outside_the_series', t => {
  const { series } = archived(t);
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true);
  const rid = ridOf(1, 'csharp');
  const outside = path.join(backupFolder(t), 'outside');
  mkdirSync(path.join(outside, '_cacache'), { recursive: true });
  writeFileSync(path.join(outside, '_cacache', 'c.tgz'), 'outside original');
  const inside = path.join(series, 's', rid, 'box', 'npm');
  mkdirSync(path.dirname(inside), { recursive: true });
  symlinkSync(outside, inside);
  const before = snapshotOf(outside);
  const refused = restoreSeries({ series, rid });
  assert.equal(refused.ok, false, 'the restore is refused');
  assert.ok(refused.reason.includes(inside), `the link is named: ${refused.reason}`);
  assert.deepEqual(snapshotOf(outside), before, 'nothing outside the series was written or replaced');
  assert.equal(existsSync(path.join(series, 's', rid, 'freed.json')), true, 'the session stays freed');
});

test('A_restore_replaces_a_link_planted_at_a_temporary_name_or_at_a_target_and_never_writes_through_it', t => {
  const { series } = archived(t);
  const before = snapshotOf(series, { skip: SKIP });
  assert.equal(freeSeries({ series, reading: readingOf(series) }).ok, true);
  const rid = ridOf(1, 'csharp');
  const outside = outsideFile(t);
  const [first, second] = cacheFilesOf(rid).map(rel => path.join(series, rel));
  mkdirSync(path.dirname(first), { recursive: true });
  symlinkSync(outside, `${first}.${process.pid}.tmp`);
  mkdirSync(path.dirname(second), { recursive: true });
  symlinkSync(outside, second);
  const restored = restoreSeries({ series, rid });
  assert.equal(restored.ok, true, restored.reason);
  assert.equal(outsideIntact(outside), true, 'nothing was written through a link');
  const after = snapshotOf(series, { skip: SKIP });
  for (const file of cacheFilesOf(rid)) assert.equal(after[file], before[file], `${file} is back with its listed bytes`);
});

test('The_hash_list_is_written_through_an_exclusive_temporary_file_so_a_link_at_its_predictable_name_is_replaced_and_never_followed', t => {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  const to = backupFolder(t);
  const outside = outsideFile(t);
  symlinkSync(outside, path.join(series, 'planted'));
  const text = Object.entries(snapshotOf(series, { skip: SKIP })).sort(([a], [b]) => byCode(a, b)).map(([file, hash]) => `${hash}  ${file}`).join('\n').concat('\n');
  const listFile = `${path.join(to, path.basename(series))}.${sha256Text(text).slice(0, 12)}.sha256list`;
  symlinkSync(outside, listFile);
  const made = archiveSeries({ series, to, now: NOW });
  assert.equal(made.ok, true, made.reason);
  assert.equal(outsideIntact(outside), true, 'the file the link pointed at was not truncated');
  assert.equal(lstatSync(listFile).isSymbolicLink(), false, 'the link was replaced');
  assert.equal(readFileSync(listFile, 'utf8'), text, 'by the list');
});

test('archive_json_and_freed_json_are_written_through_an_exclusive_temporary_file_and_a_link_at_a_temporary_name_is_never_followed', t => {
  const series = seriesFolder(t, MAP, { cacheBytes: 50 });
  const to = backupFolder(t);
  const outside = outsideFile(t);
  symlinkSync(outside, path.join(series, `archive.json.${process.pid}.tmp`));
  const made = archiveSeries({ series, to, now: NOW });
  assert.equal(made.ok, true, made.reason);
  assert.equal(outsideIntact(outside), true, 'archive.json: nothing written through the link');
  assert.equal(archiveEntries(series).length, 1, 'and the record is there');
  const rid = ridOf(1, 'csharp');
  symlinkSync(outside, path.join(series, 's', rid, `freed.json.${process.pid}.tmp`));
  const freed = freeSeries({ series, reading: readingOf(series) });
  assert.equal(freed.ok, true, freed.reason);
  assert.equal(outsideIntact(outside), true, 'freed.json: nothing written through the link');
  assert.equal(freedFileOf(series, rid).removed.length, 4, 'and the list is there');
});

test('A_session_folder_that_is_a_link_is_never_freed_into_or_restored_into_and_nothing_outside_changes', t => {
  const { series } = archived(t);
  const rid = ridOf(1, 'csharp');
  const folder = path.join(series, 's', rid);
  const moved = path.join(backupFolder(t), 'moved-session');
  renameSync(folder, moved);
  symlinkSync(moved, folder);
  const before = snapshotOf(moved);
  const freed = freeSeries({ series, reading: readingOf(series) });
  assert.equal(freed.ok, true, freed.reason);
  assert.deepEqual(snapshotOf(moved), before, 'a session folder that is a link: nothing behind it is freed or written');
  assert.ok(freed.value.kept.some(k => k.path.startsWith(`s/${rid}/`)), 'and its roots are named as kept');
});
