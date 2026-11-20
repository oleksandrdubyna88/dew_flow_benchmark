// The disk side of a paid launch of Stage 1 (F3.S1 (a), PR 4, scenarios 51 to 54h of the stories document): the reserve recomputed before every launch over what is still to run, the per-pair rule before every slot with its `disk-rule`
// event line, the refusal of a missing or zero size, and the session size read live (the larger of the registered floor and this series' own largest ended session) and written nowhere. Fakes and temporary folders only.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readLedger, isOwnCharge } from '../../minigit-bench/lib/ledger.mjs';
import { loadGrowth } from '../lib/growth.mjs';
import { HALT_EXIT, diskGate, folderBytes, reserveGate, ruleGate, sessionBytesOf, sessionFloor, sessionsToRun } from '../lib/stage-disk.mjs';
import { GOOD, MIB, PILOT, VOID, finished, mapWith, ridOf, seriesFolder, withPilot, writeSession } from './stage-fixture.mjs';

const growth = loadGrowth().value;
const FLOOR = 1_400_000_000;
/** The numbers of the registered growth record (117,395,352 bytes a pair) and a 1.4 GB floor, worked by hand: a pair is 117,395,352 + 2 * 1,400,000,000, a session half of it. */
const PAIR = 2_917_395_352;
const SESSION = 1_458_697_676;
/** The count of sessions still to run, asserted ok. */
const toRun = args => { const r = sessionsToRun(args); assert.equal(r.ok, true, r.reason); return r.value; };
const slotsDone = last => withPilot(mapWith(finished(last)));
const gateArgs = (t, map, over = {}) => ({ series: seriesFolder(t, map, { cacheBytes: 100 }), map, pilot: PILOT, growth, floorBytes: FLOOR, ...over });
const sumFiles = dir => readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).reduce((n, e) => n + statSync(path.join(e.parentPath, e.name)).size, 0);
const listing = dir => readdirSync(dir, { recursive: true }).map(String).sort();

test('The_reserve_refuses_the_first_launch_below_the_series_bound_and_names_both_numbers', t => {
  const map = mapWith([]);
  assert.equal(toRun({ map, pilot: PILOT }), 132, 'before the A/A: the 66 sessions of the series, each with its one void re-run');
  const bound = 132 * SESSION;
  assert.equal(bound, 192_548_093_232);
  const args = gateArgs(t, map);
  const refused = reserveGate({ ...args, freeBytes: bound - 1 });
  assert.equal(refused.go, false);
  assert.equal(refused.halt, 'disk-reserve');
  assert.equal(refused.exit, HALT_EXIT);
  assert.equal(HALT_EXIT, 3, 'resumable: exit 3');
  assert.ok(refused.reason.includes(String(bound - 1)) && refused.reason.includes(String(bound)), `both numbers: ${refused.reason}`);
  assert.equal(reserveGate({ ...args, freeBytes: bound }).go, true, 'free at the bound goes on');
});

test('The_reserve_is_recomputed_before_every_launch_over_the_sessions_still_to_run', t => {
  assert.equal(toRun({ map: withPilot(mapWith([])), pilot: PILOT }), 120, 'the pilot is done: 60 data sessions with a re-run each');
  assert.equal(toRun({ map: slotsDone(3), pilot: PILOT }), 108, 'three slots done: 54 cells left');
  assert.equal(toRun({ map: slotsDone(4), pilot: PILOT }), 104, 'every launch counts what is still to run');
  const voided = withPilot(mapWith([...finished(3), [4, 'csharp', 1, VOID], [4, 'node', 1, GOOD]]));
  assert.equal(toRun({ map: voided, pilot: PILOT }), 105, 'a void awaiting its one re-run holds one session; the scored stack none');
  const open = withPilot(mapWith([...finished(3), [4, 'csharp', 1, 'launching']]));
  assert.equal(toRun({ map: open, pilot: PILOT }), 108, 'an open session is still to run with its re-run, as is the stack not yet planned: 2 + 2 + 26 slots x 4');
  const need = 108 * SESSION;
  const args = gateArgs(t, slotsDone(3));
  assert.equal(reserveGate({ ...args, freeBytes: need - 1 }).go, false);
  assert.equal(reserveGate({ ...args, freeBytes: need }).go, true);
  const before = reserveGate({ ...gateArgs(t, slotsDone(4)), freeBytes: need - 1 });
  assert.equal(before.go, true, 'after slot 4 the same free space is enough: the bound fell with the sessions that ended');
});

test('A_pilot_repeat_counts_its_own_two_sessions_and_is_refused_when_the_rest_of_the_series_would_not_fit', t => {
  const map = slotsDone(29);
  const args = gateArgs(t, map);
  assert.equal(toRun({ map, pilot: PILOT }), 4, 'slot 30 only');
  assert.equal(toRun({ map, pilot: PILOT, extraCells: 2 }), 8, 'a repeat pair adds its two sessions, each with its re-run');
  const free = 8_000_000_000;
  assert.equal(reserveGate({ ...args, freeBytes: free }).go, true);
  const refused = reserveGate({ ...args, freeBytes: free, extraCells: 2 });
  assert.equal(refused.go, false);
  assert.ok(refused.reason.includes(String(8 * SESSION)) && refused.reason.includes(String(free)), `both numbers: ${refused.reason}`);
  assert.match(refused.reason, /repeat/, 'it says that a pilot repeat is in the count');
});

test('A_launch_after_a_repeat_counts_what_is_really_left_and_what_the_ended_sessions_really_hold', t => {
  const map = slotsDone(2);
  const series = seriesFolder(t, map, { cacheBytes: 100 });
  writeSession(series, ridOf(1, 'csharp'), { cacheBytes: 2 * MIB });
  const largest = folderBytes(path.join(series, 's', ridOf(1, 'csharp')));
  assert.ok(largest > 8 * MIB && largest < 9 * MIB, `the big session holds about 8 MiB (${largest})`);
  const floor = sessionFloor({ floorBytes: MIB, series, map });
  assert.deepEqual(floor, { bytes: largest, source: 'series' }, 'what the ended session really holds replaces the smaller registered floor');
  const session = 58_697_676 + largest;
  const left = toRun({ map, pilot: PILOT });
  assert.equal(left, 112, 'slots 3 to 30 are left: 56 cells with a re-run each');
  const refused = reserveGate({ series, map, pilot: PILOT, growth, floorBytes: MIB, freeBytes: left * session - 1 });
  assert.equal(refused.go, false);
  assert.ok(refused.reason.includes(String(left * session)), `the bound is the sessions left times the real size: ${refused.reason}`);
  assert.equal(reserveGate({ series, map, pilot: PILOT, growth, floorBytes: MIB, freeBytes: left * session }).go, true);
});

test('The_disk_rule_halts_a_slot_below_twice_its_worst_case_resumably_and_records_why_and_goes_on_when_the_space_is_there', t => {
  const map = withPilot(mapWith([...finished(29), [30, 'node', 1, GOOD]]));
  const ledgerFile = path.join(seriesFolder(t, map, { cacheBytes: 100 }), 'ledger.ndjson');
  const series = path.dirname(ledgerFile);
  const args = { series, map, pilot: PILOT, growth, floorBytes: FLOOR, ledgerFile, now: () => new Date('2026-11-25T10:00:00.000Z') };
  assert.equal(toRun({ map, pilot: PILOT }), 2, 'one cell left: the reserve (2 sessions) is under the rule');
  const rule = 2 * PAIR;
  const halted = diskGate({ ...args, freeBytes: rule - 1, slot: 30 });
  assert.equal(halted.go, false);
  assert.equal(halted.halt, 'disk-rule');
  assert.equal(halted.exit, HALT_EXIT);
  assert.ok(halted.reason.includes(String(rule - 1)) && halted.reason.includes(String(rule)), `both numbers: ${halted.reason}`);
  const lines = readLedger(ledgerFile);
  assert.equal(lines.length, 1, 'one event line');
  assert.equal(lines[0].event, 'disk-rule');
  assert.equal(lines[0].usd, undefined, 'an event, never a charge');
  assert.equal(isOwnCharge(lines[0]), false);
  assert.ok(lines[0].t === '2026-11-25T10:00:00.000Z' && lines[0].reason === halted.reason);
  assert.equal(ruleGate({ ...args, freeBytes: rule - 1 }).go, false);
  const goes = diskGate({ ...args, freeBytes: rule, slot: 30 });
  assert.equal(goes.go, true, 'it goes on when the space is there');
  assert.equal(readLedger(ledgerFile).length, 1, 'and records nothing');
});

test('A_missing_or_zero_size_is_a_refusal_and_never_a_rule_that_passes', t => {
  const map = withPilot(mapWith(finished(29)));
  const args = { ...gateArgs(t, map), freeBytes: 10 ** 13 };
  assert.equal(diskGate({ ...args, slot: 30 }).go, true, 'the control: with every size given the gate goes on');
  for (const [name, over] of [['freeBytes 0', { freeBytes: 0 }], ['freeBytes missing', { freeBytes: undefined }], ['freeBytes not a number', { freeBytes: Number.NaN }], ['floorBytes 0', { floorBytes: 0 }], ['floorBytes missing', { floorBytes: undefined }], ['growth missing', { growth: undefined }], ['growth without its pair figure', { growth: { ...growth, pairWorstCaseBytes: 0 } }]]) {
    for (const gate of [reserveGate, ruleGate, diskGate]) {
      const verdict = gate({ ...args, ...over, slot: 30 });
      assert.equal(verdict.go, false, `${name}: ${gate.name} refuses`);
      assert.equal(verdict.exit, HALT_EXIT, `${name}: resumable`);
      assert.ok(typeof verdict.reason === 'string' && verdict.reason.length > 0, `${name}: it says why`);
      if (name.startsWith('freeBytes')) assert.match(verdict.reason, /cannot be read/, `${name}: it says the free disk could not be read, not that the space is short`);
    }
  }
});

test('The_session_size_the_disk_rule_reads_is_the_larger_of_the_registered_floor_and_this_series_own_largest_and_no_file_is_written', t => {
  const map = slotsDone(2);
  const series = seriesFolder(t, map, { cacheBytes: 100 });
  const big = ridOf(2, 'node');
  writeSession(series, big, { cacheBytes: MIB });
  const own = folderBytes(path.join(series, 's', big));
  assert.equal(own, sumFiles(path.join(series, 's', big)), 'the folder size is the sum of its files');
  const files = listing(series);
  const growthBytes = readFileSync(new URL('../inputs/growth.json', import.meta.url));
  assert.deepEqual(sessionFloor({ floorBytes: 10 * MIB, series, map }), { bytes: 10 * MIB, source: 'floor' }, 'a floor above every session is the size');
  assert.deepEqual(sessionFloor({ floorBytes: MIB, series, map }), { bytes: own, source: 'series' }, 'this series\' largest ended session above the floor is the size');
  assert.deepEqual(listing(series), files, 'no file was written under the series');
  assert.deepEqual(readFileSync(new URL('../inputs/growth.json', import.meta.url)), growthBytes, 'growth.json is not amended');
  const freedRid = ridOf(2, 'csharp');
  const freedFolder = path.join(series, 's', freedRid);
  const freedText = `${JSON.stringify({ removed: [{ path: 'box/nuget/pkg-a/a.nupkg', bytes: 6 * MIB, sha256: 'a'.repeat(64), mode: 420 }, { path: 'box/npm/_cacache/c.tgz', bytes: 100, sha256: 'b'.repeat(64), mode: 420 }] })}\n`;
  writeFileSync(path.join(freedFolder, 'freed.json'), freedText);
  assert.deepEqual(sessionFloor({ floorBytes: 1, series, map }), { bytes: own, source: 'series' }, 'a listed file that is still on disk is counted once, by what is on disk');
  rmSync(path.join(freedFolder, 'box/nuget/pkg-a/a.nupkg'));
  rmSync(path.join(freedFolder, 'box/npm/_cacache/c.tgz'));
  const withFreed = sessionFloor({ floorBytes: 1, series, map });
  assert.ok(withFreed.bytes > 6 * MIB && withFreed.bytes < 7 * MIB && withFreed.source === 'series', `a freed session counts at the size its freed.json lists (${withFreed.bytes}) for the files that left the disk`);
  const planned = mapWith([[1, 'csharp', 1, 'planned']]);
  const folder = seriesFolder(t, planned, { cacheBytes: 4 * MIB });
  assert.deepEqual(sessionFloor({ floorBytes: MIB, series: folder, map: planned }), { bytes: MIB, source: 'floor' }, 'a session that has not ended is not read');
});

// ---- coai code round 1 (PR 4), C3: one gate call sizes each ended session once ------------------------------------------------------------------------------------------------------------------------------------------

test('One_disk_gate_call_walks_each_ended_session_folder_once_for_the_reserve_and_the_rule_together', t => {
  const map = slotsDone(2);
  const ended = Object.values(map.sessions).filter(s => s.status === 'ran' || s.status === 'scored').length;
  assert.ok(ended >= 10, `the fixture holds ended sessions: ${ended}`);
  const walked = [];
  const verdict = diskGate({ ...gateArgs(t, map), freeBytes: 10 ** 13, bytesOf: dir => { walked.push(dir); return sessionBytesOf(dir); } });
  assert.equal(verdict.go, true, verdict.reason);
  assert.equal(walked.length, ended, 'every ended session folder is sized once per gate call');
  assert.equal(new Set(walked).size, walked.length, 'and no folder twice');
});
