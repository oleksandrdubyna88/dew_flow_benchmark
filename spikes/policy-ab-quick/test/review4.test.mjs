import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { allVoid, reportFindings, voidSpend } from '../lib/findings.mjs';
import { rateLimitSignal, VOID_OUTCOME } from '../lib/ratelimit.mjs';
import { assertResumable, attemptEntry, classifyCell, planResume, setAsideAttempts } from '../lib/resume.mjs';
import { loadStory } from '../lib/story.mjs';
import { parseNdjson, summarize } from '../lib/summary.mjs';
import { outcomeOf } from '../lib/watchdog.mjs';
import { runAll } from '../pilot.mjs';
import { assembleReport, reportTarget } from '../report.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const fixture = name => parseNdjson(fs.readFileSync(path.join(here, 'fixtures', name), 'utf8'));

// ---- detection ------------------------------------------------------------------------------------------------------

test('a recorded 429 stream is void: rate limit, with the CLI\'s own markers named', () => {
  const s = rateLimitSignal(fixture('ratelimit-429.ndjson'));
  assert.equal(s.void, true);
  assert.deepEqual(s.via, ['result', 'assistant-error']);
  assert.equal(s.status, 429);
  assert.match(s.message, /^You've hit your individual spend limit/);
  assert.equal(outcomeOf({ exitCode: 1, rateLimited: s.void }), VOID_OUTCOME);
  assert.equal(VOID_OUTCOME, 'void: rate limit');
});

test('void outranks a crash, a kill and the wall limit; a plain failure is still a crash', () => {
  assert.equal(outcomeOf({ exitCode: 1, timedOut: true, overCap: true, rateLimited: true }), VOID_OUTCOME);
  assert.equal(outcomeOf({ exitCode: 1 }), 'crashed');
});

test('only the CLI\'s markers count: a model writing about rate limits, or an ordinary error result, is not void', () => {
  const talk = [{ type: 'assistant', message: { content: [{ type: 'text', text: 'I will add a rate limit and a spend limit check' }] } }, { type: 'result', is_error: false, result: 'done: spend limit added' }];
  assert.equal(rateLimitSignal(talk).void, false);
  assert.equal(rateLimitSignal([{ type: 'result', is_error: true, api_error_status: 529, result: 'overloaded' }]).void, false);
  assert.equal(rateLimitSignal([{ type: 'result', is_error: true, api_error_status: 429, result: 'x' }]).void, true);
  assert.equal(rateLimitSignal([{ type: 'result', is_error: true, result: "You've hit your weekly limit" }]).void, true);
  assert.equal(rateLimitSignal([{ type: 'assistant', error: 'rate_limit', message: {} }]).void, true);
  assert.equal(rateLimitSignal([]).void, false);
});

test('a void cell\'s summary says so, keeps its spend and flags it', () => {
  const s = summarize({ events: fixture('ratelimit-429.ndjson'), audit: [], cellJson: {}, treeDir: '', run: { outcome: VOID_OUTCOME, wallMs: 4000 } });
  assert.equal(s.void, true);
  assert.equal(s.outcome, VOID_OUTCOME);
  assert.equal(summarize({ events: [], audit: [], cellJson: {}, treeDir: '', run: { outcome: 'completed' } }).void, false);
});

// ---- the stop ---------------------------------------------------------------------------------------------------------

const plan = [{ cell: 'c1', arm: 'A' }, { cell: 'c2', arm: 'B' }, { cell: 'c3', arm: 'B' }];
const recorder = () => {
  const calls = { stop: [], crash: [], void: [], done: [] };
  return { calls, record: { stop: s => calls.stop.push(s), crash: (c, m) => calls.crash.push([c, m]), void: (c, r) => calls.void.push([c, r.outcome]), done: (c, r) => calls.done.push([c, r.outcome]) } };
};

test('a void cell stops the series at once, with the reason, and no later cell starts', async () => {
  const { calls, record } = recorder();
  const ran = [];
  const ended = await runAll(plan, { cap: 30, stopAfter: 1000, spent: () => 0, record, runCell: async p => { ran.push(p.cell); return p.cell === 'c2' ? { outcome: VOID_OUTCOME, void: true, spendUsd: 6.5 } : { outcome: 'completed', void: false, spendUsd: 4 }; } });
  assert.deepEqual(ran, ['c1', 'c2']);
  assert.deepEqual(ended, { stoppedBefore: '', crashedAt: '', voidAt: 'c2' });
  assert.deepEqual(calls.done, [['c1', 'completed']]);
  assert.deepEqual(calls.void, [['c2', VOID_OUTCOME]]);
  assert.equal(calls.stop.length, 1);
  assert.equal(calls.stop[0].reason, 'rate limit');
  assert.equal(calls.stop[0].cell, 'c2');
  assert.equal(calls.stop[0].before, 'c3');
});

// ---- resume -----------------------------------------------------------------------------------------------------------

const mapOf = cells => ({ story: 'coai-751', seed: 's', order: ['A', 'B', 'B'], cells, series: { runsRoot: 'R', harnessSpikeTree: 'T1', probeCreatedUtc: 'P1' } });

test('resume keeps every data point (a real crash too) and re-runs void and never-started cells in the original order', () => {
  const map = mapOf({ c1: { outcome: 'crashed', ranModel: true }, c2: { outcome: VOID_OUTCOME }, c3: {} });
  const rp = planResume(plan, map, { storyId: 'coai-751', seed: 's' });
  assert.deepEqual(rp.keep.map(p => p.cell), ['c1']);
  assert.deepEqual(rp.run.map(p => p.cell), ['c2', 'c3']);
  for (const outcome of ['completed', 'killed: wall', 'killed: over cap', 'crashed']) assert.equal(classifyCell({ outcome }), 'keep', outcome);
  assert.equal(classifyCell({ outcome: VOID_OUTCOME }), 'run');
  assert.equal(classifyCell(undefined), 'run');
  assert.equal(classifyCell({ outcome: 'crashed', ranModel: false }), 'run', 'a harness failure before the session ran is not a data point');
});

test('resume refuses another story, another seed, or another arm order', () => {
  const map = mapOf({});
  assert.throws(() => planResume(plan, map, { storyId: 'other', seed: 's' }), /not other/);
  assert.throws(() => planResume(plan, map, { storyId: 'coai-751', seed: 'x' }), /not the seed/);
  assert.throws(() => planResume([...plan].reverse(), map, { storyId: 'coai-751', seed: 's' }), /do not reproduce/);
  assert.throws(() => planResume(plan, {}, { storyId: 'coai-751', seed: 's' }), /no run map/);
});

test('resume is refused when the harness, the probe or the runs root changed since the series started', () => {
  const ok = { runMap: mapOf({}), harness: { spikeTree: 'T1' }, probeRecord: { createdUtc: 'P1' }, runsRoot: 'R' };
  assert.doesNotThrow(() => assertResumable(ok));
  assert.throws(() => assertResumable({ ...ok, harness: { spikeTree: 'T2' } }), /harness changed.*T1 -> T2/);
  assert.throws(() => assertResumable({ ...ok, probeRecord: { createdUtc: 'P2' } }), /probe changed/);
  assert.throws(() => assertResumable({ ...ok, probeRecord: null }), /probe changed/);
  assert.throws(() => assertResumable({ ...ok, runsRoot: 'R2' }), /ran in R, not R2/);
  assert.throws(() => assertResumable({ ...ok, runMap: { cells: {} } }), /no series record/);
});

test('setting a void attempt aside renames its folder into void/, keeps the record, and clears the cell for a re-run', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-resume-'));
  try {
    for (const c of ['c1', 'c2']) fs.mkdirSync(path.join(root, c, 'tree', 'src_vs_code', 'node_modules', 'pkg'), { recursive: true });
    fs.writeFileSync(path.join(root, 'c2', 'summary.json'), JSON.stringify({ usd: 6.57 }));
    fs.writeFileSync(path.join(root, 'c2', 'run.json'), '{}');
    const map = mapOf({ c1: { outcome: 'completed' }, c2: { outcome: VOID_OUTCOME } });
    const patch = setAsideAttempts(root, [{ cell: 'c2' }, { cell: 'c3' }], map, { srcDir: 'src_vs_code', mount: '.agents/conventions' });
    assert.deepEqual(Object.keys(patch), ['c2'], 'c3 never started: nothing to move');
    assert.equal(patch.c2.outcome, null);
    assert.deepEqual(patch.c2.attempts, [{ n: 1, outcome: VOID_OUTCOME, void: true, spendUsd: 6.57, movedTo: 'void/c2-a1' }]);
    assert.ok(!fs.existsSync(path.join(root, 'c2')));
    assert.ok(fs.existsSync(path.join(root, 'void', 'c2-a1', 'summary.json')), 'the record stays');
    assert.ok(!fs.existsSync(path.join(root, 'void', 'c2-a1', 'tree', 'src_vs_code', 'node_modules')), 'its node_modules are freed');
    assert.ok(fs.existsSync(path.join(root, 'c1', 'tree', 'src_vs_code', 'node_modules')), 'a kept cell is untouched');
    assert.equal(attemptEntry({ attempts: [{}], outcome: VOID_OUTCOME }, 1, n => `a${n}`).n, 2);
  } finally {
    fs.rmSync(path.join(root, 'void'), { recursive: true, force: true });
    fs.rmSync(root, { recursive: true, force: true });
  }
});

// ---- the report -------------------------------------------------------------------------------------------------------

test('the report lists attempts per cell, the final outcome, and the void spend apart', () => {
  const files = {
    'r/c1/summary.json': { usd: 4, costSource: 'envelope', run: { outcome: 'completed' } },
    'r/c2/summary.json': { usd: 6.5, costSource: 'envelope', outcome: VOID_OUTCOME, void: true },
  };
  const read = file => files[file.replace(/\\/g, '/')] ?? null;
  const runMap = { seed: 's', cells: { c1: { arm: 'A', attempts: [{ n: 1, outcome: VOID_OUTCOME, void: true, spendUsd: 7, movedTo: 'void/c1-a1' }] }, c2: { arm: 'B' } }, stop: { reason: 'rate limit', cell: 'c2', before: 'c3', line: 'x' } };
  const report = assembleReport({ story: loadStory(), runMap, root: 'r', read, now: 'T' });
  assert.deepEqual(report.cells.map(c => [c.cell, c.outcome, c.attempts.length]), [['c1', 'completed', 1], ['c2', VOID_OUTCOME, 0]]);
  assert.equal(report.voidSpendUsd, 13.5, 'c1\'s void attempt (7) plus c2, currently void (6.5)');
  assert.equal(report.stop.reason, 'rate limit');
  const kinds = report.findings.map(f => `${f.kind}:${f.cell ?? f.before}`);
  assert.ok(kinds.includes('attempts:c1') && kinds.includes('cell-outcome:c2') && kinds.includes('series-stopped:c3'));
  assert.equal(voidSpend([{ cell: 'reference', outcome: VOID_OUTCOME, summary: { usd: 99 } }]), 0);
  assert.deepEqual(reportFindings({ cells: [] }), []);
});

test('a report whose every cell is void never overwrites the results file; it goes to the runs root', () => {
  const story = loadStory('coai-751');
  const allVoidReport = { cells: [{ cell: 'c1', outcome: VOID_OUTCOME }, { cell: 'c2', outcome: VOID_OUTCOME }, { cell: 'reference', outcome: null }] };
  assert.equal(allVoid(allVoidReport.cells), true);
  assert.equal(reportTarget({ story, report: allVoidReport, root: 'D:/rsd/_s2c' }).replace(/\\/g, '/'), 'D:/rsd/_s2c/report-void.json');
  const mixed = { cells: [{ cell: 'c1', outcome: 'completed' }, { cell: 'c2', outcome: VOID_OUTCOME }] };
  assert.equal(allVoid(mixed.cells), false);
  assert.match(reportTarget({ story, report: mixed, root: 'D:/rsd/_s2c' }).replace(/\\/g, '/'), /research\/bench\/policy-ab-coai-751-data\.json$/);
  assert.equal(allVoid([]), false, 'no cells is not "all void"');
  assert.equal(reportTarget({ out: 'D:/x/y.json', story, report: allVoidReport, root: 'D:/rsd/_s2c' }).replace(/\\/g, '/'), 'D:/x/y.json', 'an explicit --out is the operator\'s call');
});
