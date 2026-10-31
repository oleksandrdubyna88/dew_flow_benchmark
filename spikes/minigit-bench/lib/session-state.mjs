// A session says what it is doing, and a crashed one is retired before anything starts (E2.S2 D-21, D-21a).
// `state.json`: { phase: starting | running | closed | failed | retired, hostPid, hostStartTime, cell, startedAt, heartbeatAt }.
// LIVENESS IS BY OWNER IDENTITY, never by wall clock: the owner is the (pid, start time) pair (field 22 of /proc/<pid>/stat, immune to pid
// reuse); a session is stale only when that exact process is gone. The heartbeat is an observation for the report, not a test, so a clock
// jump cannot retire a live session. The retirement ends the cell's survivors by marker, unlinks the sockets and marks the dir; it deletes
// nothing: a stale dir's artefacts are evidence of what a crash cost, and `retire.mjs` (E4) is the one deleter.
import { existsSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';

export const HEARTBEAT_MS = 5000;
const OPEN_PHASES = Object.freeze(['starting', 'running']);
const CLOSED_PHASES = Object.freeze(['closed', 'failed', 'retired']);

/** Field 22 of a /proc/<pid>/stat line (the process start time, in clock ticks since boot), counted after the LAST ')'; null if unreadable. */
export function parseStartTime(statLine) {
  const rest = String(statLine).split(/\)\s/).at(-1).split(' ');
  return rest.length > 19 && /^\d+$/.test(rest[19]) ? rest[19] : null; // field 3 is rest[0]
}

/** The start time of a live pid, or null when it does not exist. */
export function processStartTime(pid, read = p => readFileSync(`/proc/${p}/stat`, 'latin1')) {
  try {
    return parseStartTime(read(pid));
  } catch (e) {
    if (['ENOENT', 'ESRCH'].includes(e.code)) return null;
    throw e;
  }
}

/** Is this exact process (pid AND start time) still running? */
export const ownerAlive = (pid, startTime) => processStartTime(pid) === startTime;

const wellFormed = j => j !== null && typeof j === 'object' && [...OPEN_PHASES, ...CLOSED_PHASES].includes(j.phase)
  && Number.isInteger(j.hostPid) && typeof j.hostStartTime === 'string' && typeof j.cell === 'string';

/** `'live' | 'stale' | 'closed' | 'malformed'`. `ownerAlive(pid, startTime)` is injected. `now` is for callers that want the heartbeat's age; it decides nothing. */
export function stateOf(json, { ownerAlive: isAlive }) {
  if (!wellFormed(json)) return 'malformed';
  if (CLOSED_PHASES.includes(json.phase)) return 'closed';
  return isAlive(json.hostPid, json.hostStartTime) ? 'live' : 'stale';
}

/** Write `state` atomically (a temp file, then rename). */
export function writeState(file, state) {
  const tmp = `${file}.${process.pid}.tmp`;
  writeFileSync(tmp, `${JSON.stringify(state)}\n`);
  renameSync(tmp, file);
}

/** `{ ok, value } | { ok: false, reason }`. */
export function readState(file) {
  if (!existsSync(file)) return fail(`no state file at ${file}`);
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')));
  } catch (e) {
    return fail(`the state file ${file} is unreadable: ${e.message}`);
  }
}

/** Stamp `heartbeatAt` (an observation, every watchdog tick) and nothing else. */
export function heartbeat(file, nowMs) {
  const current = readState(file);
  if (!current.ok) return current;
  const next = { ...current.value, heartbeatAt: new Date(nowMs).toISOString() };
  writeState(file, next);
  return ok(next);
}

const socketsOf = dir => {
  const net = path.join(dir, 'net');
  return existsSync(net) ? readdirSync(net).filter(n => n.endsWith('.sock')).map(n => path.join(net, n)) : [];
};

async function retire({ dir, state, now, endCell }) {
  await endCell({ cellId: state.cell, bwrapPid: null, graceMs: 2000 });
  socketsOf(dir).forEach(s => rmSync(s, { force: true }));
  writeState(path.join(dir, 'state.json'), { ...state, phase: 'retired', retiredAt: new Date(now).toISOString(), retiredBy: process.pid });
}

/**
 * Before any session starts under a runs root: retire every stale one. -> `[{ rid, verdict, heartbeatAgeMs? }]`, one per directory.
 * A directory without a readable state file is `malformed` and never touched.
 */
export async function retireStale({ root, now, endCell, isOwnerAlive = ownerAlive }) {
  if (!existsSync(root)) return [];
  const report = [];
  for (const rid of readdirSync(root).sort()) {
    const dir = path.join(root, rid);
    const read = readState(path.join(dir, 'state.json'));
    const verdict = read.ok ? stateOf(read.value, { ownerAlive: isOwnerAlive }) : 'malformed';
    if (verdict === 'stale') await retire({ dir, state: read.value, now, endCell });
    report.push({ rid, verdict, ...(read.ok && Date.parse(read.value.heartbeatAt) ? { heartbeatAgeMs: now - Date.parse(read.value.heartbeatAt) } : {}) });
  }
  return report;
}
