// `explore.mjs rehearse` (plan section 10 step 7, T-X11): the whole pair path for $0. v1's stub CLI as the CLI and v1's stub upstream as the API, in the real box behind the real broker and proxy, a fake token
// (the real token file is never read), one C# and TypeScript pair on the masks, each workspace pre-filled with its reference tree; then collect, classify, score, record and render the note exactly as a paid pair.
// Pass: both sessions `completed`, both scored 100 %, every record's sections free of unknowns without a reason, T1-T5 as v1's stub gives them, and the note renders with its banner.
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { randomBytes } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import { leafProblems } from '../../minigit-bench/lib/bare-record.mjs';
import { runLoop } from '../../minigit-bench/lib/bare-series.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { stubClaude } from '../../minigit-bench/lib/stub-cli.mjs';
import { chunksOf, sseAnswer, sseBody, stubUpstream } from '../../minigit-bench/lib/stub-upstream.mjs';
import { TOKEN_FILE_PARTS } from '../../minigit-bench/capture/capture-env.mjs';
import { loadHeldout, writeSolution } from './heldout.mjs';
import { BANNER, renderNote } from './note.mjs';
import { cpcOf } from './records.mjs';
import { finalizePair, loadCalibration } from './run.mjs';
import { runContext, runDeps, scoreSessionDep } from './run-ctx.mjs';
import { allOk } from './chain.mjs';
import { pinsOk } from './pins.mjs';
import { createSeries, drawMask, planPair, runMapFileOf } from './series.mjs';

/** The rehearsal's `ctx.fill`: the item's stack workspace with the REFERENCE source on top, so a stub session that changes nothing leaves a correct solution. */
export const referenceFill = ({ world, heldout }) => ({ item, layout }) => {
  const stack = item.session.stack;
  writeSolution({ files: world.filesFor(stack), overlayDir: heldout.refDir(stack), dest: layout.work });
  return ok({ files: Object.keys(world.filesFor(stack)).sort(), flags: [] });
};

/** A throw-away home holding a fake token file (mode 600 in a 700 directory): the preflight looks that a token file exists; the pilot never touches the real one. */
function fakeTokenHome() {
  const home = mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-lex-rehearse-'));
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, 'sk-ant-oat01-not-a-real-token\n', { mode: 0o600 });
  chmodSync(path.dirname(file), 0o700);
  return home;
}

const readJsonText = file => readFileSync(file, 'utf8');
const readRecord = (seriesDir, rid) => JSON.parse(readJsonText(path.join(seriesDir, 's', rid, 'record.json')));
const RECORD_SECTIONS = Object.freeze(['run', 'cost', 'time', 'quality', 'process', 'loc', 'egress', 'packages', 'workspace', 'explore']);

const sectionProblems = (e, r) => (r ? RECORD_SECTIONS.flatMap(sec => (r[sec] === undefined ? [`${e.rid}.${sec}: absent`] : leafProblems(r[sec], `${e.rid}.${sec}`))) : [`${e.rid}: no record`]);
const crosscheckProblems = (e, r) => Object.entries(r?.cost?.checks?.value ?? {}).filter(([, c]) => c.status !== 'pass').map(([k, c]) => `${e.rid} ${k} is ${c.status}`);
const acceptedLine = ({ e, r }) => `${e.stack}: ${e.status}, accepted ${r?.explore?.accepted?.value}`;
const isAccepted = ({ e, r }) => e.status === 'scored' && r?.explore?.accepted?.value === true;

const check = (id, ok, detail) => ({ id, ok, detail });
const completedCheck = entries => check('both sessions completed', entries.length === 2 && entries.every(e => e.class === 'completed'), entries.map(e => `${e.stack}: ${e.class}`).join(', '));
const scoredCheck = per => check('both scored 100 %', per.length === 2 && per.every(isAccepted), per.map(acceptedLine).join(', '));
const leavesCheck = problems => check('every record leaf is known or says why', problems.length === 0, problems.slice(0, 5).join('; ') || 'no unknown without a reason');
const stubCheck = checks => check('T1-T5 as v1\'s stub gives them', checks.length === 0, checks.join('; ') || 'T1 to T5 pass in both records');
const noteCheck = noteText => check('the note renders with its banner', typeof noteText === 'string' && noteText.includes(BANNER), noteText ? `${noteText.length} characters` : 'no note');

/**
 * The pass criteria, computed from the run-map entries and the records (never typed): `[{ id, ok, detail }]`. The cross-checks must pass as v1's stub gives them (a status other than `pass` is named).
 */
export function rehearsalChecks({ entries, records, noteText }) {
  const per = entries.map(e => ({ e, r: records[e.rid] }));
  return [completedCheck(entries), scoredCheck(per), leavesCheck(per.flatMap(({ e, r }) => sectionProblems(e, r))), stubCheck(per.flatMap(({ e, r }) => crosscheckProblems(e, r))), noteCheck(noteText)];
}

/** The note of a series from its records (a real pair's, or the rehearsal's). */
export function noteFor({ series, seriesDir, map, world, pins, rehearsal, subject }) {
  const entries = Object.values(map.sessions).sort((a, b) => a.pair - b.pair || a.attempt - b.attempt);
  const sessions = entries.map(entry => ({ entry, record: readRecord(seriesDir, entry.rid) }));
  const pairs = [...new Set(entries.map(e => e.pair))].map(p => JSON.parse(readJsonText(path.join(seriesDir, 'pairs', `p${p}.json`))));
  const cpc = Object.fromEntries(['csharp', 'node'].map(stack => [stack, cpcOf(sessions.filter(s => s.entry.stack === stack).map(s => ({ record: s.record, accepted: s.record.explore?.accepted?.value === true })))]));
  return renderNote({ date: new Date().toISOString().slice(0, 10), seriesId: series.seriesId, rehearsal, subject, pins, limits: world.calibration.limits, sessions, pairs, cpc });
}

/** What a rehearsal needs on disk: the calibration, the held-out side and the world. -> `ok({ calibration, heldout, world })` | the first `fail`. */
async function prerequisites({ home, worldFn }) {
  const got = await allOk([() => loadCalibration(home), () => loadHeldout(home), () => worldFn({ home, work: path.join(home, 'rsd', 'lang-v2', 'explore') })]);
  if (!got.ok) return got;
  const [calibration, heldout, world] = got.value;
  const pins = pinsOk({ explore: world.explore, home, snapshots: world.snapshots });
  return pins.ok ? ok({ calibration, heldout, world: { ...world, calibration } }) : pins;
}

/**
 * Rehearse. `io` = `{ home, say, abort, signal, worldFn, subject }`. -> `{ exit, checks, message }`; exit 0 when every criterion holds.
 */
export async function rehearse({ io }) {
  const need = await prerequisites({ home: io.home, worldFn: io.worldFn });
  if (!need.ok) return { exit: 4, message: need.reason };
  const made = createSeries({ home: io.home, explore: need.value.world.explore, calibration: need.value.calibration, inputsExtra: { rehearsal: true }, draw: drawMask() });
  const lock = made.ok ? acquireSeriesLock({ seriesDir: made.value.dir }) : made;
  if (!lock.ok) return { exit: 4, message: lock.reason };
  try {
    return await rehearseIn({ io, ...need.value, series: made.value, say: io.say });
  } finally {
    lock.value.release();
  }
}

async function rehearseIn({ io, world, calibration, heldout, series, say }) {
  const planned = planPair({ map: loadMap(series.dir), pair: 1, pairs: 3, draw: series.state.draw, kind: 'rehearsal' });
  if (!planned.ok) return { exit: 4, message: planned.reason };
  writeRunMap(runMapFileOf(series.dir), planned.value.map);
  const stubDir = path.join(series.dir, 'rehearsal');
  mkdirSync(stubDir, { recursive: true });
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  const fake = fakeTokenHome();
  try {
    return await runStubPair({ io, world, calibration, heldout, series, planned: planned.value, stub, stubDir, fake, say });
  } finally {
    await stub.close();
    rmSync(fake, { recursive: true, force: true });
  }
}

const loadMap = dir => JSON.parse(readJsonText(runMapFileOf(dir)));

async function runStubPair({ io, world, calibration, heldout, series, planned, stub, stubDir, fake, say }) {
  const planOver = { upstream: stub.upstream, tickMs: 500, sampleMs: 500, settleMs: 500, graceMs: 2000 };
  const ctx = await runContext({ world, calibration, series: series.state, seriesDir: series.dir, kind: 'rehearsal', pair: 1, token: `sk-ant-fake-${randomBytes(8).toString('hex')}`, claudeBinary: stubClaude(stubDir), fill: referenceFill({ world, heldout }), planOver, abort: io.abort, home: io.home });
  if (!ctx.ok) return { exit: 4, message: ctx.reason };
  const deps = runDeps({ ctx: ctx.value, world, scoreDep: scoreSessionDep({ world, calibration, heldout, home: io.home }), machine: null, preflightHome: fake, stub: true });
  const result = await runLoop({ ctx: ctx.value, deps, map: planned.map, signal: io.signal, say });
  if (result.exit !== 0) return { exit: result.exit, message: `the stub pair did not finish: ${result.message}` };
  return finishRehearsal({ io, world, series, map: result.map });
}

function finishRehearsal({ io, world, series, map }) {
  const fin = finalizePair({ seriesDir: series.dir, map, pair: 1, kinds: ['rehearsal'] });
  const entries = Object.values(map.sessions).filter(s => s.kind === 'rehearsal');
  const records = Object.fromEntries(entries.map(e => [e.rid, readRecord(series.dir, e.rid)]));
  const noteText = noteFor({ series: series.state, seriesDir: series.dir, map, world, pins: world.explore, rehearsal: true, subject: io.subject });
  writeFileSync(path.join(series.dir, 'rehearsal-note.md'), noteText);
  const checks = rehearsalChecks({ entries, records, noteText });
  writeFileSync(path.join(series.dir, 'rehearsal.json'), `${JSON.stringify({ checks, problems: fin.problems, at: new Date().toISOString() }, null, 2)}\n`);
  const holds = checks.every(c => c.ok) && fin.problems.length === 0;
  return { exit: holds ? 0 : 5, checks, problems: fin.problems, seriesDir: series.dir, message: holds ? 'the rehearsal passes every criterion' : 'the rehearsal is refuted: fix the path and rehearse again (free)' };
}

