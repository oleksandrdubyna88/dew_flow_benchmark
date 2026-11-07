// The files of one session's scoring under `s/<rid>/score/` and the derived snapshot (F1.S2, section 3.1). Everything that happened is a file written ONCE (`wx`): an attempt `passes/pNN-<profile>-aK.json`, an event
// `events/<utc>-<kind>.json`, the completion record `build.json`. `verdict.json` is the one derived file: rebuilt WHOLE from those files on every write, written to a temp name and renamed, NEVER opened for reading and never
// merged from itself, so a crash between two writes cannot lose an entry of its lists and a corrupted or invented snapshot cannot survive. `fs` is the seam of the tests (a recording one proves the snapshot is not read).
import * as nodeFs from 'node:fs';
import path from 'node:path';
import { systemClock } from './clock.mjs';
import { attemptRecordProblem } from './attempt-record.mjs';
import { casesOfRecord } from './score-quick.mjs';
import { PASS_COUNT, profileOfPass, verdictOf } from './verdict.mjs';

export const BUILD_ENVIRONMENT_EVENT = 'build-environment';
const ATTEMPT_NAME = /^p(\d\d)-(\w+)-a(\d+)\.json$/;
const pad = n => String(n).padStart(2, '0');

const passesDir = work => path.join(work, 'passes');
const eventsDir = work => path.join(work, 'events');
/** `score/passes/pNN-<profile>-aK.json`. */
export const attemptFile = ({ work, n, profile, attempt }) => path.join(passesDir(work), `p${pad(n)}-${profile}-a${attempt}.json`);

/** Write one attempt, once (`wx`): an attempt that exists is never overwritten (the error is EEXIST). -> the file. */
export function writeAttempt({ work, n, profile, attempt, body, fs = nodeFs }) {
  fs.mkdirSync(passesDir(work), { recursive: true });
  const file = attemptFile({ work, n, profile, attempt });
  fs.writeFileSync(file, `${JSON.stringify({ n, profile, cpus: profileOfPass(n).cpus, attempt, ...body }, null, 2)}\n`, { flag: 'wx' });
  return file;
}

const stampOf = now => now().toISOString().replaceAll(/[-:.]/g, '');
const eventName = ({ now, kind, n }) => `${stampOf(now)}-${kind}${n === 1 ? '' : `-${n}`}.json`;

/** `true` when the file was created, `false` when the name is taken (`wx`). */
function createdOnce(file, body, fs) {
  try { fs.writeFileSync(file, body, { flag: 'wx' }); return true; } catch (e) { if (e.code === 'EEXIST') return false; throw e; }
}

/** Write one event once (`wx`) as `events/<utc>-<kind>.json`; a name taken in the same millisecond gets `-2`, `-3`... -> the file name. */
export function writeEvent({ work, now = systemClock, kind, detail = {}, fs = nodeFs }) {
  fs.mkdirSync(eventsDir(work), { recursive: true });
  const body = `${JSON.stringify({ kind, at: now().toISOString(), ...detail }, null, 2)}\n`;
  let n = 1;
  while (!createdOnce(path.join(eventsDir(work), eventName({ now, kind, n })), body, fs)) n += 1;
  return eventName({ now, kind, n });
}

const jsonFiles = (dir, fs) => (fs.existsSync(dir) ? fs.readdirSync(dir).filter(f => f.endsWith('.json')).sort() : []);
const readJson = (file, fs) => { try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch (e) { return { unreadable: e.message }; } };

/** The events, in file order: `[{ file, kind, at, ... }]`. */
export const readEvents = ({ work, fs = nodeFs }) => jsonFiles(eventsDir(work), fs).map(file => ({ file, ...readJson(path.join(eventsDir(work), file), fs) }));

const envAttempt = ({ attempt, file, body, reason }) => ({ attempt, kind: 'environment', file, startedAt: body.startedAt ?? null, ms: body.ms ?? 0, observedCpus: body.observedCpus ?? null, reason });

/** Why a stored completed attempt is not, today, the complete judgement of the expected cases, or `{ cases }`. */
function judgement({ body, expectedIds, pinnedRunner }) {
  const problem = attemptRecordProblem({ text: JSON.stringify(body.record), expected: expectedIds, pinnedRunner });
  if (problem) return { reason: problem };
  const judged = casesOfRecord({ record: body.record, expected: expectedIds });
  return judged.ok ? { cases: judged.value } : { reason: `record-invalid: ${judged.reason}` };
}

const completedAttempt = ({ base, body, cases }) => ({ attempt: base.attempt, kind: 'completed', file: base.file, startedAt: body.startedAt ?? null, ms: body.ms ?? 0, observedCpus: body.observedCpus ?? null, runner: body.runner, memoryCertified: body.memoryCertified === true, cases });

/** The reason a stored attempt is an environment one, or null when it is a completed one. */
function environmentReason(body) {
  if (body.unreadable) return `attempt-file-invalid: ${body.unreadable.slice(0, 120)}`;
  return body.kind === 'completed' ? null : (body.reason ?? 'no reason was recorded');
}

/** One stored attempt as the verdict rule reads it: a completed one only when its record is, today, the complete judgement of exactly the expected cases. */
function attemptOf({ name, body, expectedIds, pinnedRunner }) {
  const base = { attempt: Number(ATTEMPT_NAME.exec(name)[3]), file: name };
  const early = environmentReason(body);
  if (early) return envAttempt({ ...base, body, reason: early });
  const judged = judgement({ body, expectedIds, pinnedRunner });
  return judged.cases ? completedAttempt({ base, body, cases: judged.cases }) : envAttempt({ ...base, body, reason: judged.reason });
}

/** The ten passes `[{ n, profile, cpus, attempts }]` as the verdict rule reads them, from the attempt files alone (a pass without a file has no attempt). */
export function readAttempts({ work, expectedIds, pinnedRunner, fs = nodeFs }) {
  const names = jsonFiles(passesDir(work), fs).filter(f => ATTEMPT_NAME.test(f));
  const read = names.map(name => ({ n: Number(ATTEMPT_NAME.exec(name)[1]), attempt: attemptOf({ name, body: readJson(path.join(passesDir(work), name), fs), expectedIds, pinnedRunner }) }));
  return Array.from({ length: PASS_COUNT }, (_, i) => ({ n: i + 1, profile: profileOfPass(i + 1).name, cpus: profileOfPass(i + 1).cpus, attempts: read.filter(r => r.n === i + 1).map(r => r.attempt) }));
}

/** The build step as the verdict rule reads it: the completion record, or `{ exhausted: true }` when the build never began on its three tries, or `null` (no build yet). */
function buildOf({ work, events, fs }) {
  const file = path.join(work, 'build.json');
  if (fs.existsSync(file)) return readJson(file, fs);
  return events.filter(e => e.kind === BUILD_ENVIRONMENT_EVENT).length >= 3 ? { exhausted: true } : null;
}

const attemptSummary = a => ({ attempt: a.attempt, kind: a.kind, file: a.file, startedAt: a.startedAt, ms: a.ms });
const buildSummary = b => (b?.treeSha256 ? { treeSha256: b.treeSha256, ok: b.ok, exit: b.exit, wallMs: b.wallMs, timedOut: b.timedOut } : null);

/** The snapshot's content, from the immutable files. */
function snapshotOf({ build, passes, events }) {
  const answer = verdictOf({ build: build ?? { ok: true }, passes });
  const buildMs = build?.wallMs ?? 0;
  const attemptMs = passes.flatMap(p => p.attempts).reduce((sum, a) => sum + (a.ms ?? 0), 0);
  return { ...answer, build: buildSummary(build), buildMs, passes: passes.map(p => ({ n: p.n, profile: p.profile, cpus: p.cpus, attempts: p.attempts.map(attemptSummary) })), events: events.map(e => ({ file: e.file, kind: e.kind, at: e.at })), scoringMs: buildMs + attemptMs };
}

/**
 * Rebuild `verdict.json` from `build.json`, `passes/` and `events/` alone: whole, to a temp name, then renamed. It takes no previous snapshot and never opens `verdict.json` (D1, D2). -> the snapshot's content.
 */
export function rebuildVerdict(work, { expectedIds, pinnedRunner, fs = nodeFs }) {
  const events = readEvents({ work, fs });
  const content = snapshotOf({ build: buildOf({ work, events, fs }), passes: readAttempts({ work, expectedIds, pinnedRunner, fs }), events });
  const tmp = path.join(work, `verdict.json.${process.pid}.tmp`);
  fs.writeFileSync(tmp, `${JSON.stringify(content, null, 2)}\n`);
  fs.renameSync(tmp, path.join(work, 'verdict.json'));
  return content;
}
