import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { INJECTED_SOURCES } from '../lib/consts.mjs';
import { costFromMessages } from '../lib/pricing.mjs';
import { compareProbes, probeGate, probeSubject } from '../lib/probe.mjs';
import { findLinkInside, removeUnder } from '../lib/safe-rm.mjs';
import { shedCell } from '../lib/shed.mjs';
import { overlayEntries } from '../lib/settings.mjs';
import { parseNdjson, summarize } from '../lib/summary.mjs';
import { CostMeter, OVER_CAP_FACTOR } from '../lib/watchdog.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, '..');
const recorded = name => parseNdjson(fs.readFileSync(path.join(here, 'fixtures', name), 'utf8'));

// ---- the probe ------------------------------------------------------------------------------------------------------

test('the parity probe also needs the same rule-load signal in both arms', () => {
  const mapA = new Map([['p', '1']]);
  const mapB = new Map([['p', '2']]);
  const suite = { tests: 10, failedCases: ['t::a'] };
  const base = { suiteA: suite, suiteB: suite, mapA, mapB, policyFiles: ['p'] };
  const ok = compareProbes({ ...base, sessionStartA: { mentionsRule: false }, sessionStartB: { mentionsRule: false } });
  assert.equal(ok.pass, true);
  assert.deepEqual(ok.sessionStartMentionsRule, { A: false, B: false });
  assert.match(compareProbes({ ...base, sessionStartA: { mentionsRule: false }, sessionStartB: { mentionsRule: true } }).reasons.join(), /rule-load signal differs.*A=false, B=true/);
  assert.match(compareProbes({ ...base, sessionStartA: null, sessionStartB: { mentionsRule: false } }).reasons.join(), /SessionStart probe is missing/);
});

test('the probe gate checks the story, P, the mount pin, the arm commits, the policy files and the harness version', () => {
  const story = { id: 's', P: 'p'.repeat(40), policyFiles: ['b', 'a'] };
  const armShas = { A: 'a'.repeat(40), B: 'b'.repeat(40) };
  const subject = probeSubject({ story, pin: 'x'.repeat(40), armShas, harness: { spikeTree: 'T1' } });
  assert.deepEqual(subject.policyFiles, ['a', 'b']);
  const record = { pass: true, subject, harness: { dirty: false } };
  assert.equal(probeGate(record, subject).ok, true);
  assert.equal(probeGate(null, subject).ok, false);
  assert.equal(probeGate({ ...record, pass: false, reasons: ['x'] }, subject).ok, false);
  const changes = [{ storyId: 'other' }, { P: 'q'.repeat(40) }, { mountPin: 'y'.repeat(40) }, { armShas: { ...armShas, B: 'c'.repeat(40) } }, { policyFiles: ['a'] }, { harnessSpikeTree: 'T2' }];
  for (const change of changes) {
    const g = probeGate(record, { ...subject, ...change });
    assert.equal(g.ok, false, JSON.stringify(change));
    assert.match(g.why, /made for something else/);
  }
  assert.match(probeGate({ ...record, harness: { dirty: true } }, subject).why, /uncommitted/);
  assert.match(probeGate(record, subject, { dirty: true }).why, /uncommitted/);
});

// ---- the watchdog's meter against recorded envelopes ----------------------------------------------------------------------

for (const [name, cell] of [['usage-q3-c3.ndjson', 'Opus main plus three Sonnet children'], ['usage-q2-c2.ndjson', 'Opus only']]) {
  test(`the meter replays a recorded stream (${cell}) and agrees with that cell's envelope within 15 percent`, () => {
    const events = recorded(name);
    const envelope = events.findLast(e => e.type === 'result').total_cost_usd;
    const meter = new CostMeter();
    for (const e of events) meter.feed(JSON.stringify(e));
    assert.ok(Math.abs(meter.usd - envelope) / envelope <= 0.15, `meter ${meter.usd} against envelope ${envelope}`);
    assert.equal(meter.overCap(30), false, 'a $5 cell is nowhere near the kill line of a $30 cap');
    assert.equal(meter.overCap(envelope / 2 / OVER_CAP_FACTOR), true, 'a cap of half its real cost does trip it');
    assert.equal(costFromMessages(events).estimated, true);
  });
}

test('the fixtures hold counts only: no message text, no tool input', () => {
  for (const name of ['usage-q3-c3.ndjson', 'usage-q2-c2.ndjson']) {
    for (const e of recorded(name)) assert.deepEqual(Object.keys(e.message ?? {}).filter(k => !['id', 'model', 'usage'].includes(k)), [], name);
  }
});

test('a summary without an envelope says its cost is an estimate and why', () => {
  const s = summarize({ events: recorded('usage-q2-c2.ndjson').filter(e => e.type === 'assistant'), audit: [], cellJson: {}, treeDir: '' });
  assert.equal(s.costSource, 'messages');
  assert.equal(s.estimated, true);
  assert.match(s.estimateNote, /fitted fallback rates/);
});

// ---- nothing injected into a cell names the experiment ---------------------------------------------------------------------

const TELL = /\barm\b|spike|A\/B|policy-ab|experiment/i;

test('every file copied into a tree, and every hook entry written into its settings, is free of experiment wording (both arms)', () => {
  for (const rel of INJECTED_SOURCES) {
    const text = fs.readFileSync(path.join(root, rel), 'utf8');
    assert.equal(TELL.test(text), false, `${rel}: ${text.match(TELL)?.[0]}`);
  }
  for (const arm of ['A', 'B']) for (const guardWired of [false, true]) assert.equal(TELL.test(JSON.stringify(overlayEntries(arm, { guardWired }))), false);
});

// ---- recursive deletes follow no link ---------------------------------------------------------------------------------------

function link(target, at) {
  try { fs.symlinkSync(target, at, 'junction'); return true; } catch { return false; }
}

test('a junction INSIDE a tree stops the delete, and what it points to survives', t => {
  const base = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-inner-'));
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-inner-out-'));
  const jct = path.join(base, 'c1', 'score', 'a', 'b', 'jct');
  try {
    fs.writeFileSync(path.join(outside, 'precious.txt'), 'keep');
    fs.mkdirSync(path.join(base, 'c1', 'score', 'a', 'b'), { recursive: true });
    fs.writeFileSync(path.join(base, 'c1', 'score', 'a', 'file.txt'), 'x');
    assert.equal(findLinkInside(path.join(base, 'c1', 'score')), '', 'a plain tree has none');
    if (!link(outside, jct)) return t.skip('cannot create a junction here');
    assert.equal(findLinkInside(path.join(base, 'c1', 'score')), jct);
    assert.throws(() => removeUnder(path.join(base, 'c1', 'score'), base), /contains a link or junction/);
    const shed = shedCell(base, 'c1', {});
    assert.deepEqual(shed.removed, []);
    assert.match(shed.refused[0].reason, /contains a link or junction/);
    assert.equal(fs.readFileSync(path.join(outside, 'precious.txt'), 'utf8'), 'keep');
    assert.ok(fs.existsSync(path.join(base, 'c1', 'score', 'a', 'file.txt')), 'nothing of the tree was deleted either');
  } finally {
    fs.rmSync(jct, { force: true });
    fs.rmSync(base, { recursive: true, force: true });
    fs.rmSync(outside, { recursive: true, force: true });
  }
});

test('no script or library deletes recursively on its own: only safe-rm does', () => {
  const files = ['score-cell.mjs', 'prepare-cell.mjs', 'pilot.mjs', 'run-cell.mjs', 'summarize-cell.mjs', 'report.mjs', 'probe-series.mjs', 'canaries.mjs',
    ...fs.readdirSync(path.join(root, 'lib')).filter(f => f !== 'safe-rm.mjs').map(f => `lib/${f}`)];
  for (const rel of files) assert.equal(/rmSync\([^)]*recursive:\s*true/.test(fs.readFileSync(path.join(root, rel), 'utf8')), false, `${rel} deletes recursively on its own`);
});
