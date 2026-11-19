// The recovery path of Stage 1 that keeps the data (F3.S1 (a), PR 4, scenarios 54 to 54g of the stories document): `stage-archive` (a verified copy of the whole series to the private backup, with the sha256 list), `stage-free` (the ONE verb that frees
// local space, a closed list of three cache folders of final sessions of finished slots, each file proven by the verified archive) and `stage-restore` (the freed folders back from the archive, verified). Fakes and temporary folders only: the
// "backup" is an owned folder, the sessions are a few invented files, no held-out content exists here.
import assert from 'node:assert/strict';
import { appendFileSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { appendLedger, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { openSeries, planSession, readRunMap, transition, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { loadGrowth } from '../lib/growth.mjs';
import { ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { FREEABLE, archiveSeries, freeSeries, freedSessionProblem, restoreSeries } from '../lib/stage-archive.mjs';
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
  const freed = freeSeries({ series });
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
  const freed = freeSeries({ series });
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
  const none = freeSeries({ series: bare });
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
    const refused = freeSeries({ series });
    assert.equal(refused.ok, false, `${what}: refused`);
    assert.deepEqual(snapshotOf(series, { skip: SKIP }), before, `${what}: nothing was removed and nothing was written`);
  }
});

test('stage_restore_brings_a_freed_session_back_to_its_listed_bytes_and_a_copy_that_differs_is_refused', t => {
  const { series } = archived(t);
  const before = snapshotOf(series, { skip: SKIP });
  assert.equal(freeSeries({ series }).ok, true);
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
  assert.equal(freeSeries({ series: again.series }).ok, true);
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
  const freed = freeSeries({ series: halted });
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
  assert.equal(freeSeries({ series }).ok, true);
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
  const held = freeSeries({ series, unreadSlots: unread.map(u => u.slot) });
  assert.equal(held.ok, true, held.reason);
  assert.deepEqual(ofSlot(snapshotOf(series, { skip: SKIP })), ofSlot(before), 'no file of the read-back slot is touched: the judge\'s reading comes first');
  assert.equal(held.value.sessions.some(rid => rbRids.includes(rid)), false, 'and the verb says it freed none of its sessions');
  assert.equal(rb.slot > 1 ? held.value.sessions.length > 0 : true, true, 'the finished slots before it are freed as usual');
  const read = unreadReadbackSlots({ map, slots: SLOTS, readbackLines: [line('csharp'), line('node')] });
  assert.deepEqual(read, [], 'with both dated lines the slot no longer awaits');
  const freed = freeSeries({ series, unreadSlots: read.map(u => u.slot) });
  assert.deepEqual(freed.value.sessions.sort(), rbRids.sort(), 'and the verb frees its sessions');
});
