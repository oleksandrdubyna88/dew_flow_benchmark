// E1.S8b: the delegated scope (D43, the plan's "The shape, delegated scope"). One scope per case, and one for the run's `empty-program` control, named by an owned unit name:
//   systemd-run --user --scope --quiet --unit=<unit> -p Delegate=yes -p OOMPolicy=continue -p MemorySwapMax=0 <scope-entry.sh> <out-dir> <memory.max> <holder seconds> <chain…>
// `--quiet` (not in the spec's text): without it systemd-run's "Running as unit" line would be the first line of every case's recorded stderr, which is the program's. `Delegate=yes` lets the unprivileged `scope-entry.sh` make the leaves `holder/` and `prog/` (the program's cgroup holds the launch chain and nothing else; the scope holder, a bounded
// sleep, is the only process outside it and keeps the scope alive after the program's exit so `prog/memory.peak`, `memory.events` and `cgroup.procs` can be read). `OOMPolicy=continue`:
// this host's DefaultOOMPolicy is `stop`, which would remove the whole scope on an OOM kill. The cgroup path is known BEFORE the spawn from the unit name: the user manager's
// `app.slice`. `--cgroup-of` names `prog/`; the runner accepts a directory that does not exist yet and `scope-entry.sh` creates it.
import { closeSync, constants, openSync, readFileSync, writeSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';

const CGROUP_ROOT = '/sys/fs/cgroup';
const SYSTEMCTL_MS = 15_000;
export const ENTRY = fileURLToPath(new URL('./scope-entry.sh', import.meta.url));

/** The user manager's `app.slice` cgroup: where every `systemd-run --user --scope` lands. */
export const appSlice = (uid = process.getuid()) => `${CGROUP_ROOT}/user.slice/user-${uid}.slice/user@${uid}.service/app.slice`;
export const scopeCgroup = unit => `${appSlice()}/${unit}.scope`;
export const progCgroup = unit => `${scopeCgroup(unit)}/prog`;
export const holderCgroup = unit => `${scopeCgroup(unit)}/holder`;
/** `--cgroup-of`: `{case}` stands in the unit name, as it does in the prefix. */
export const progTemplate = unitPrefix => progCgroup(`${unitPrefix}-{case}`);

/** The environment a `systemctl --user` or `systemd-run --user` child needs to find the user manager. */
export const userEnv = () => ({ PATH: '/usr/sbin:/usr/bin:/bin', XDG_RUNTIME_DIR: `/run/user/${process.getuid()}` });

/**
 * The scope's part of the launch prefix (the chain `nsenter … bwrap …` follows it). `unit` may hold `{case}` (the runner fills it), `outDir` too; `holdSeconds` bounds the scope holder:
 * the case wall plus a margin, so a crashed layer's holder still ends.
 */
export function scopePrefix({ unit, outDir, memoryMaxBytes, holdSeconds, systemdRun = 'systemd-run', entry = ENTRY }) {
  return [systemdRun, '--user', '--scope', '--quiet', `--unit=${unit}`, '-p', 'Delegate=yes', '-p', 'OOMPolicy=continue', '-p', 'MemorySwapMax=0', entry, outDir, String(memoryMaxBytes), String(Math.ceil(holdSeconds))];
}

const text = file => {
  try {
    return readFileSync(file, 'utf8');
  } catch {
    return null;
  }
};

const whole = value => (value !== null && /^\d+$/.test(value.trim()) ? Number(value.trim()) : null);

function eventsOf(body) {
  if (body === null) return null;
  const pairs = body.trim().split('\n').map(line => line.split(' '));
  return pairs.every(pair => pair.length === 2 && whole(pair[1]) !== null) ? Object.fromEntries(pairs.map(([name, value]) => [name, Number(value)])) : null;
}

/** What a leaf of the scope says: `{ peakBytes, events, procs }`; a field that cannot be read is null — absent is not zero. */
export function readProg(dir) {
  const procs = text(path.join(dir, 'cgroup.procs'));
  return {
    peakBytes: whole(text(path.join(dir, 'memory.peak'))),
    events: eventsOf(text(path.join(dir, 'memory.events'))),
    procs: procs === null ? null : procs.split('\n').filter(Boolean).map(Number),
  };
}

/** `1` into `<dir>/cgroup.kill`: every process of the leaf gets SIGKILL, whatever its group or session. Opened, never created; throws when it cannot be written. */
export function killCgroup(dir) {
  const fd = openSync(path.join(dir, 'cgroup.kill'), constants.O_WRONLY);
  try {
    writeSync(fd, '1');
  } finally {
    closeSync(fd);
  }
}

async function systemctl(args) {
  const run = await runBounded('systemctl', ['--user', ...args], { timeoutMs: SYSTEMCTL_MS, env: userEnv() });
  return run.ok ? { ok: true, code: run.value.code, text: `${run.value.stdout}${run.value.stderr}`.trim() } : { ok: false, code: null, text: run.reason };
}

/** `'active'`, `'inactive'`, `'failed'`, `'activating'`, … as systemd says it; `'unknown'` when systemctl could not be asked. */
export async function unitState(unit) {
  const asked = await systemctl(['is-active', `${unit}.scope`]);
  return asked.ok ? asked.text.split('\n')[0] || 'unknown' : 'unknown';
}

/**
 * `systemctl --user stop <unit>.scope` (the last resort for a scope that does not end), then `reset-failed` so a scope that failed leaves no record behind: `{ ok, text }`;
 * a unit that is not loaded is stopped. Never throws.
 */
export async function stopUnit(unit) {
  const asked = await systemctl(['stop', `${unit}.scope`]);
  await systemctl(['reset-failed', `${unit}.scope`]);
  return { ok: asked.ok && (asked.code === 0 || /not loaded/.test(asked.text)), text: asked.text };
}

/** The scope units whose name starts with `prefix` (loaded or not yet collected), without the `.scope` suffix. */
export async function listScopes(prefix) {
  const asked = await systemctl(['list-units', '--all', '--plain', '--no-legend', '--type=scope', `${prefix}*`]);
  return asked.ok ? asked.text.split('\n').map(line => line.trim().split(/\s+/)[0]).filter(name => name.startsWith(prefix) && name.endsWith('.scope')).map(name => name.slice(0, -'.scope'.length)) : [];
}
