// The contamination seam of the quick run (a relative walk in a directory the scan lost is LOW, not high), the bound on the files outside the evidence paths, and the units of `rescore`. The whole path is proved by running
// `rescore` on a rehearsal series (see the report); here are the parts that need no box.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../../minigit-bench/lib/contamination.mjs';
import { scanSessionDir } from '../../minigit-bench/lib/session-scan.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { appendLedger, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { planSpend } from '../lib/pause.mjs';
import { quickPlan } from '../lib/quick.mjs';
import { createSeries, ledgerFileOf } from '../lib/series.mjs';
import { reclassifiedEntry, rescoreEvent, sessionProblem } from '../lib/rescore.mjs';

const TREE = '/h/s/r1/box/work';
const ctx = extra => ({ rid: 'r1', treeRoot: TREE, home: '/h', boxHome: '/h/s/r1/box/home', harnessPaths: ['/h/s/r1/harness'], partnerDirs: [], partnerPaths: [], allow: [], arm: 'bare', ...extra });
const bash = command => ({ agent: 'main', tool: 'Bash', input: { command } });
// the shape of the first quick run's false positive: a `cd` out of the tree, `cd -` (the scan cannot place it), then a tar walk of `.`
const LOST = [bash('cd /tmp/bt && true'), bash('cd - >/dev/null'), bash('rm -rf /tmp/p && mkdir /tmp/p && tar --exclude=node_modules -cf - . | tar -xf - -C /tmp/p && cd /tmp/p && ls')];
const rules = hits => hits.map(h => `${h.severity}:${h.rule}`);

test('A_relative_walk_in_a_lost_directory_is_high_as_it_always_was_and_low_only_under_the_quick_seam', () => {
  assert.deepEqual(rules(contaminationHits({ calls: LOST, ctx: ctx({}) })), ['high:outside-path']);
  assert.deepEqual(rules(contaminationHits({ calls: LOST, ctx: ctx({ lostCwdLow: true }) })), ['low:outside-path']);
});

test('An_absolute_walk_of_a_protected_root_and_a_real_outside_path_stay_high_under_the_quick_seam', () => {
  const quick = ctx({ lostCwdLow: true });
  assert.deepEqual(rules(contaminationHits({ calls: [bash('find / -name "*.json" | head')], ctx: quick })), ['high:outside-path']);
  assert.deepEqual(rules(contaminationHits({ calls: [bash('ls /h/.config')], ctx: quick })), ['high:outside-path']);
  assert.equal(contaminationHits({ calls: [...LOST, bash('cat /h/secret.txt')], ctx: quick }).some(h => h.severity === 'high'), true);
});

test('A_file_outside_the_evidence_paths_that_is_big_is_not_read_under_the_budget_and_an_evidence_file_always_is', t => {
  const root = tmpDir('quick-scan');
  mkdirSync(path.join(root, 'box', 'tmp'), { recursive: true });
  mkdirSync(path.join(root, 'box', 'work'), { recursive: true });
  const token = ['sk-ant', 'oat01', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefgh'].join('-'); // built, so no key-shaped literal stands in the source
  writeFileSync(path.join(root, 'box', 'tmp', 'big.out'), `${token}${'x'.repeat(2000)}`);
  writeFileSync(path.join(root, 'box', 'work', 'big.out'), `${token}${'x'.repeat(2000)}`);
  const isEvidence = rel => rel.startsWith(path.join('box', 'work'));
  const limits = { maxScanBytes: 1024, windowBytes: 512, overlapBytes: 128, maxStreamBytes: 1 << 20 };
  const budget = { isEvidence, perFileBytes: 1024, totalBytes: 1 << 20 };
  const scanned = scanSessionDir(root, { real: token, limits, outsideBudget: budget });
  assert.deepEqual(scanned.skippedOutside.map(f => f.rel), [path.join('box', 'tmp', 'big.out')]);
  assert.deepEqual(scanned.hits.map(h => h.source), [path.join('box', 'work', 'big.out')], 'the evidence file was read and the real token in it found');
  assert.deepEqual(scanSessionDir(root, { real: token, limits }).skippedOutside, [], 'without the budget nothing is skipped: the olympiad and v1 are as they were');
});

const entry = over => ({ rid: 'a1', pair: 1, stack: 'node', kind: 'pilot', status: 'ran', class: 'void: contaminated', data: false, ...over });

test('A_session_is_rescored_only_when_it_ended_unscored_kept_its_workspace_and_its_cell_has_no_other_data', t => {
  const root = tmpDir('quick-rescore');
  mkdirSync(path.join(root, 'work'));
  const layout = { work: path.join(root, 'work') };
  const map = over => ({ sessions: { a1: entry(over), b2: entry({ rid: 'b2', stack: 'csharp', class: 'completed', data: true, status: 'scored' }) } });
  assert.equal(sessionProblem({ map: map(), entry: map().sessions.a1, layout, rid: 'a1' }), null);
  assert.match(sessionProblem({ map: map(), entry: undefined, layout, rid: 'zz' }), /no session zz/);
  assert.match(sessionProblem({ map: map({ status: 'scored' }), entry: map({ status: 'scored' }).sessions.a1, layout, rid: 'a1' }), /is scored/);
  assert.match(sessionProblem({ map: map({ status: 'launching' }), entry: map({ status: 'launching' }).sessions.a1, layout, rid: 'a1' }), /is launching/);
  assert.match(sessionProblem({ map: map(), entry: map().sessions.a1, layout: { work: path.join(root, 'gone') }, rid: 'a1' }), /no workspace/);
  const rival = { sessions: { a1: entry(), a2: entry({ rid: 'a2', attempt: 2, data: true, class: 'completed' }) } };
  assert.match(sessionProblem({ map: rival, entry: rival.sessions.a1, layout, rid: 'a1' }), /already holds the data of this cell/);
});

test('The_rescored_entry_keeps_what_it_was_appended_and_a_second_rescore_appends_again', () => {
  const first = reclassifiedEntry(entry(), { class: 'completed', data: true }, '2026-11-05T17:00:00.000Z');
  assert.deepEqual([first.class, first.data], ['completed', true]);
  assert.deepEqual(first.reclassifiedFrom.map(x => [x.class, x.data]), [['void: contaminated', false]]);
  assert.match(first.reclassifiedFrom[0].reason, /was void: contaminated, it is completed|it was void: contaminated, it is completed/);
  const second = reclassifiedEntry({ ...first, status: 'ran' }, { class: 'void: harness', data: false }, '2026-11-05T18:00:00.000Z');
  assert.deepEqual(second.reclassifiedFrom.map(x => x.class), ['void: contaminated', 'completed']);
  assert.throws(() => { first.class = 'x'; }, TypeError);
});

test('Only_an_ended_void_contaminated_session_is_rescored_every_other_class_is_refused_by_name', () => {
  const root = tmpDir('quick-rescore-classes');
  mkdirSync(path.join(root, 'work'));
  const layout = { work: path.join(root, 'work') };
  const problemOf = over => { const e = entry(over); return sessionProblem({ map: { sessions: { a1: e } }, entry: e, layout, rid: 'a1' }); };
  assert.equal(problemOf({}), null, 'the one class the rescore exists for passes');
  for (const [cls, data] of [['stop: security', false], ['stop: alias drift', false], ['stop: auth', false], ['void: rate limit', false], ['void: upstream', false], ['void: harness', false], ['void: harness (interrupted)', false], ['void: harness (disposed)', false], ['completed', true], ['killed: cap', true]]) {
    assert.match(problemOf({ class: cls, data }), new RegExp(`is ${cls.replace(/[()]/g, '\\$&')}: only a void: contaminated session`), cls);
  }
});

test('A_rescore_removes_nothing_from_the_entry_and_adds_one_ledger_event_that_is_no_charge', () => {
  const before = entry({ extra: { kept: [1, 2] }, run: { flags: ['x'] }, reclassifiedFrom: [{ class: 'void: harness', data: false, reason: 'earlier', at: 't0' }] });
  const after = reclassifiedEntry(before, { class: 'completed', data: true }, '2026-11-05T17:00:00.000Z');
  for (const key of Object.keys(before).filter(k => !['class', 'data', 'reclassifiedFrom'].includes(k))) assert.deepEqual(after[key], before[key], `${key} survives`);
  assert.equal(after.reclassifiedFrom.length, before.reclassifiedFrom.length + 1);
  assert.deepEqual(after.reclassifiedFrom[0], before.reclassifiedFrom[0], 'the earlier record is kept as it was');

  const explore = loadExplore().value;
  const plan = quickPlan(explore);
  const home = tmpDir('quick-rescore-ledger');
  const made = createSeries({ home, explore, calibration: { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } }, task: plan.planTasks[0] });
  const ledger = ledgerFileOf(made.value.dir);
  appendLedger(ledger, { kind: 'pilot', usd: 3.5, what: 'completed', rid: 'a1', t: '2026-11-05T10:00:00Z' });
  const spent = planSpend({ home, explore, plan });
  appendLedger(ledger, rescoreEvent({ entry: before, now: { class: 'completed', data: true }, at: '2026-11-05T17:00:00.000Z' }));
  assert.deepEqual(planSpend({ home, explore, plan }), spent, 'the event is no charge: the plan spends the same');
  assert.deepEqual(readLedger(ledger).map(l => l.event ?? l.what), ['completed', 'rescore']);
});
