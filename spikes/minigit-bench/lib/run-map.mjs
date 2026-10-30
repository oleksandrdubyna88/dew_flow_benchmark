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

/** Has the series STARTED a data session (any status but `planned`)? From then on the instrument is frozen (contract section 10: "after pair 1 starts"): the A/A, the pilot and the rehearsal are instrument checks, not data, and a data session that is only planned has paid nothing (the loop plans a pair's sessions before its gates run, so a halt there leaves them planned). */
export const hasDataSession = map => Object.values(map.sessions ?? {}).some(s => s.kind === 'data' && s.status !== 'planned');

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

const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

/**
 * A changed harness identity is accepted by `--accept-harness <identity> --amendment <date>` only when a valid INSTRUMENT line of amendments.md (`parseAmendments().instrument`: date, `instrument`,
 * `harness:<12 hex>`, a reason; review N6: no other kind of line and no prose counts, whatever it mentions) carries that date and the 12 hex of the identity, and only while the series holds no
 * data session: the instrument may change after the A/A and BEFORE pair 1 (contract section 10). An identity accepted earlier is in `map.harnessAccepted` and needs neither.
 */
function harnessAccepted({ map, current, acceptHarness, amendments }) {
  if (!acceptHarness) return fail(`harnessIdentity changed (${map.harnessIdentity.slice(0, 12)} -> ${current.harnessIdentity.slice(0, 12)}): a harness change needs --accept-harness <identity> --amendment <date> naming a dated amendments.md line`);
  if (acceptHarness.identity !== current.harnessIdentity) return fail(`--accept-harness names ${acceptHarness.identity.slice(0, 12)}, but the harness running is ${current.harnessIdentity.slice(0, 12)}`);
  const identity = current.harnessIdentity.slice(0, 12);
  const line = parseAmendments(amendments ?? '').instrument.find(i => i.date === acceptHarness.amendmentDate && i.identity === identity);
  if (!line) return fail(`amendments.md has no instrument line dated ${acceptHarness.amendmentDate} that names harness:${identity} (a line \`YYYY-MM-DD | instrument | harness:<12 hex> | <reason, with its G-id>\`; a verdict, input or budget line, or prose, does not count)`);
  if (hasDataSession(map)) return fail(`the harness changed (${map.harnessIdentity.slice(0, 12)} -> ${identity}) after a data session exists in this series: the instrument may change after the A/A and before pair 1 (contract section 10), so the acceptance is refused. Start a new series`);
  return ok({ from: map.harnessIdentity, to: current.harnessIdentity, amendmentDate: acceptHarness.amendmentDate });
}

/**
 * May this series resume with what is on disk NOW (`current`: the same fields the map pinned)? A series stop is never passed this way; a pin, the
 * stacks file, the prices, a snapshot, the prereg tag or the seed hash that differs is a refusal naming it; a changed quiet.json or package-flags.json only through a dated `input:` line and before any data session; a changed harness identity is accepted
 * only through `acceptHarness` and a dated amendment line (an identity accepted before, in `map.harnessAccepted`, needs no flag again). -> `ok({ accepted })` (`accepted` is the harness acceptance for the ledger, or null).
 */
export function checkResumable({ map, current, acceptHarness = null, amendments = '', settleOnly = false }) {
  if (settleOnly) return settleCheck({ map, current });
  if (map.stopped) return fail(`the series is stopped (${map.stopped.rule}): resume is refused for good: a series stop is final for its series. Fix the named cause and start a NEW series (init, then an A/A that is GO)`);
  const field = PINNED_FIELDS.find(f => !same(map[f], current[f]));
  if (field) return fail(`${field} differs from the run-map (${JSON.stringify(map[field]).slice(0, 80)} -> ${JSON.stringify(current[field]).slice(0, 80)}): the series was started under another one`);
  const inputs = inputsAccepted({ map, current, amendments });
  if (!inputs.ok) return inputs;
  if (map.harnessIdentity === current.harnessIdentity || (map.harnessAccepted ?? []).includes(current.harnessIdentity)) return ok({ accepted: null, inputs: inputs.value });
  const accepted = harnessAccepted({ map, current, acceptHarness, amendments });
  return accepted.ok ? ok({ accepted: accepted.value, inputs: inputs.value }) : accepted;
}

const anyLaunching = map => Object.values(map.sessions ?? {}).some(s => s.status === 'launching');
const acceptedInput = (map, current, f) => f in INPUT_FIELDS && (map.inputsAccepted ?? []).some(a => a.file === INPUT_FIELDS[f] && a.sha256 === current[f]);
const acceptedHarness = (map, current, f) => f === 'harnessIdentity' && (map.harnessAccepted ?? []).includes(current.harnessIdentity);

/**
 * `resume --settle-only` (review N12 and MINOR-4): settles and charges the sessions a series left `launching`, and launches nothing; it is refused when no session is `launching` (nothing to settle). It is
 * allowed on a stopped series and accepts nothing (no harness, no input amendment). On a STOPPED series it reads evidence a run already paid for, so a reader fix after the data sessions must not leave a paid
 * session uncharged and the harness identity is not compared; on a series that is not stopped the harness that settles is the one that classes the session, so its identity must be the pinned (or an accepted) one.
 * Every other pin is compared on both, because the prices, the snapshots and the seed decide what a session cost and how it is read.
 */
function settleCheck({ map, current }) {
  if (!anyLaunching(map)) return fail('nothing to settle: no session of the series is launching. `resume --settle-only` settles and charges a session that was left launching (its evidence could not be read before) and launches nothing');
  const field = driftedField({ map, current });
  return field ? fail(`${field} differs from the run-map (${JSON.stringify(map[field]).slice(0, 80)} -> ${JSON.stringify(current[field]).slice(0, 80)}): the series was started under another one`) : ok({ accepted: null, inputs: [] });
}

/** The first pin a settle compares that differs from the run-map and is not an accepted change, or undefined. */
function driftedField({ map, current }) {
  const fields = [...PINNED_FIELDS, ...Object.keys(INPUT_FIELDS), ...(map.stopped ? [] : ['harnessIdentity'])];
  return fields.find(f => !same(map[f], current[f]) && !acceptedInput(map, current, f) && !acceptedHarness(map, current, f));
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
