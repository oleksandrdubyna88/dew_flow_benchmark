// The series run-map (E3b.S1 steps 1 and 11): one JSON file in the series root that says what the series is pinned to and where every session stands.
// A session moves `planned -> launching -> ran -> scored` (`launching -> planned` when its dir is gone and nothing was paid) and no other way; the
// file is written tmp + rename, so a crash leaves the old one or the new one, never half. Pure over values except `readRunMap` / `writeRunMap`.
// (`policy-ab-quick/lib/runmap.mjs` holds the earlier spike's arm <-> cell map: another shape, keyed by cell, written in place, so it is not widened.)
import { existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { parseAmendments } from './amendments.mjs';
import { fail, ok } from './result.mjs';

const NEXT = Object.freeze({ planned: ['launching'], launching: ['ran', 'planned'], ran: ['scored'], scored: [] });
const PINNED_FIELDS = Object.freeze(['pins', 'stacksSha256', 'pricesSha256', 'snapshots', 'preregTag', 'seedSha256']);
/** The two instrument inputs the run pins by hash, and the file each is. They may change after the A/A (contract section 10) only through a dated `input:` line, before any data session. */
const INPUT_FIELDS = Object.freeze({ quietSha256: 'quiet.json', packageFlagsSha256: 'package-flags.json' });

/** Does the series hold a data session (any status)? From then on the instrument is frozen: the A/A, the pilot and the rehearsal are instrument checks, not data. */
export const hasDataSession = map => Object.values(map.sessions ?? {}).some(s => s.kind === 'data');

/** A new series' map: its pins, caps and budgets as given, no stop, no session. */
export const openSeries = fields => Object.freeze({ ...fields, stopped: null, sessions: Object.freeze({}) });

/** `ok(map)` | `fail`: an absent or unreadable file is a value, never a thrown error. */
export function readRunMap(file) {
  if (!existsSync(file)) return fail(`no run-map at ${file}`);
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')));
  } catch (e) {
    return fail(`the run-map ${file} is unreadable: ${e.message}`);
  }
}

/** Write beside the file and move it over: a reader never sees half a map. */
export function writeRunMap(file, map) {
  const tmp = `${file}.${process.pid}.tmp`;
  writeFileSync(tmp, `${JSON.stringify(map, null, 2)}\n`);
  renameSync(tmp, file);
}

/** Add a `planned` session (`{ rid, pair, stack, phase, attempt, kind, label }`); a rid is never planned twice. -> `ok(map)` | `fail`. */
export function planSession(map, session) {
  if (map.sessions[session.rid] !== undefined) return fail(`the rid ${session.rid} is already in the run-map: a rid is never reused`);
  return ok(Object.freeze({ ...map, sessions: Object.freeze({ ...map.sessions, [session.rid]: Object.freeze({ ...session, status: 'planned' }) }) }));
}

/** Move a session to `to` (merging `patch`), if the table allows it. -> `ok(map)` | `fail` naming the transition. */
export function transition(map, rid, to, patch = {}) {
  const s = map.sessions[rid];
  if (s === undefined) return fail(`no session ${rid} in the run-map`);
  if (!NEXT[s.status].includes(to)) return fail(`session ${rid}: ${s.status} -> ${to} is not a legal transition (from ${s.status}: ${NEXT[s.status].join(', ') || 'none'})`);
  return ok(Object.freeze({ ...map, sessions: Object.freeze({ ...map.sessions, [rid]: Object.freeze({ ...s, ...patch, status: to }) }) }));
}

/** Record a series stop (`{ rule, reason, at }`): resume is refused from then on. */
export const markStopped = (map, stop) => Object.freeze({ ...map, stopped: Object.freeze(stop) });

/** What resume does with one session, from its status and what its directory holds (`dirs[rid] = { exists, hasMeta, retired, paidWork }`). */
function actionFor(session, dir) {
  const d = dir ?? { exists: false, hasMeta: false, retired: false, paidWork: false };
  if (session.status === 'planned') return { action: 'launch', reason: 'never started' };
  if (session.status === 'launching') return launchingAction(d);
  if (!d.exists) return { action: 'lost', reason: `the session is ${session.status} but its directory is gone` };
  if (session.status === 'ran' && session.data === true) return { action: 'score', reason: 'ended with data, not yet scored' };
  return { action: 'rebuild-record', reason: session.status === 'scored' ? 'scored' : 'ended without data to score' };
}

function launchingAction(d) {
  if (d.hasMeta) return { action: 'adopt', reason: 'meta.json was written: the launch ended, only the map was not told' };
  if (d.paidWork) return { action: 'void-interrupted', reason: 'the launch was cut short with paid work: classified void: harness, charged its settled spend and unsettled reservations' };
  return { action: 'reset', reason: 'nothing was paid: planned again under the same rid' };
}

/** `[{ rid, action: 'launch' | 'adopt' | 'void-interrupted' | 'reset' | 'score' | 'rebuild-record' | 'lost', reason }]`, one per session, in map order. */
export const resumePlan = (map, dirs) => Object.values(map.sessions).map(s => ({ rid: s.rid, ...actionFor(s, dirs[s.rid]) }));

const AMENDMENT_LINE = /^(\d{4}-\d{2}-\d{2}) \|/;
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

function harnessAccepted({ map, current, acceptHarness, amendments }) {
  if (!acceptHarness) return fail(`harnessIdentity changed (${map.harnessIdentity.slice(0, 12)} -> ${current.harnessIdentity.slice(0, 12)}): a harness change needs --accept-harness <identity> --amendment <date> naming a dated amendments.md line`);
  if (acceptHarness.identity !== current.harnessIdentity) return fail(`--accept-harness names ${acceptHarness.identity.slice(0, 12)}, but the harness running is ${current.harnessIdentity.slice(0, 12)}`);
  const line = (amendments ?? '').split('\n').find(l => AMENDMENT_LINE.exec(l)?.[1] === acceptHarness.amendmentDate && l.includes(`harness:${current.harnessIdentity.slice(0, 12)}`));
  if (!line) return fail(`amendments.md has no line dated ${acceptHarness.amendmentDate} that names harness:${current.harnessIdentity.slice(0, 12)}`);
  return ok({ from: map.harnessIdentity, to: current.harnessIdentity, amendmentDate: acceptHarness.amendmentDate });
}

/**
 * May this series resume with what is on disk NOW (`current`: the same fields the map pinned)? A series stop is never passed this way; a pin, the
 * stacks file, the prices, a snapshot, the prereg tag or the seed hash that differs is a refusal naming it; a changed quiet.json or package-flags.json only through a dated `input:` line and before any data session; a changed harness identity is accepted
 * only through `acceptHarness` and a dated amendment line (an identity accepted before, in `map.harnessAccepted`, needs no flag again). -> `ok({ accepted })` (`accepted` is the harness acceptance for the ledger, or null).
 */
export function checkResumable({ map, current, acceptHarness = null, amendments = '' }) {
  if (map.stopped) return fail(`the series is stopped (${map.stopped.rule}): resume is refused until the named fix and, for a stop of the first four, a new A/A`);
  const field = PINNED_FIELDS.find(f => !same(map[f], current[f]));
  if (field) return fail(`${field} differs from the run-map (${JSON.stringify(map[field]).slice(0, 80)} -> ${JSON.stringify(current[field]).slice(0, 80)}): the series was started under another one`);
  const inputs = inputsAccepted({ map, current, amendments });
  if (!inputs.ok) return inputs;
  if (map.harnessIdentity === current.harnessIdentity || (map.harnessAccepted ?? []).includes(current.harnessIdentity)) return ok({ accepted: null, inputs: inputs.value });
  const accepted = harnessAccepted({ map, current, acceptHarness, amendments });
  return accepted.ok ? ok({ accepted: accepted.value, inputs: inputs.value }) : accepted;
}

/**
 * The instrument inputs (quiet.json, package-flags.json) as the disk holds them against the run-map's pins (review N5). A content that differs is accepted only when a dated
 * `instrument | input:<file>:<12 hex>` line of amendments.md names it AND the series holds no data session yet; a content accepted before (`map.inputsAccepted`) stays accepted. -> `ok([newly accepted])` | `fail`.
 */
function inputsAccepted({ map, current, amendments }) {
  const named = parseAmendments(amendments ?? '').inputs;
  const accepted = [];
  for (const [field, file] of Object.entries(INPUT_FIELDS)) {
    const sha = current[field];
    if (same(map[field], sha) || (map.inputsAccepted ?? []).some(a => a.file === file && a.sha256 === sha)) continue;
    const hash12 = String(sha).slice(0, 12);
    const line = named.find(i => i.file === file && i.hash12 === hash12);
    if (!line) return fail(`${field} differs from the run-map (${String(map[field]).slice(0, 12)} -> ${hash12}): the series was started under another one, and amendments.md has no dated line \`YYYY-MM-DD | instrument | input:${file}:${hash12} | <reason, with its G-id>\``);
    if (hasDataSession(map)) return fail(`${file} changed to ${hash12} after a data session exists in this series: an input amendment is for the instrument after the A/A and before pair 1 (contract section 10), so it is refused. Start a new series`);
    accepted.push({ file, sha256: sha, hash12, date: line.date, reason: line.reason });
  }
  return ok(accepted);
}
