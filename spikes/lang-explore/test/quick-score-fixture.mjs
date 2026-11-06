// The scoring area and the fake seams the quick-run scoring tests share: a tree, a held-out folder of invented pairs, a runner record, a build. No box, no layer: `scoreQuick` takes `buildFn` and `runFn`.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { scoreQuick } from '../lib/score-quick.mjs';

export const MIB = 1024 * 1024;

export function frozenFiles() {
  const f = text => ({ bytes: Buffer.from(text), mode: 0o644 });
  return { 'samples/s01.in': f('scenario 2\n'), 'samples/s01.out': f('transcript 2\n'), 'build.sh': { bytes: Buffer.from('#!/bin/bash\n'), mode: 0o755 } };
}

export function scoringArea(t) {
  const root = tmpDir('quick-score');
  const source = path.join(root, 'tree');
  mkdirSync(source, { recursive: true });
  writeFileSync(path.join(source, 'build.sh'), '#!/bin/bash\n', { mode: 0o755 });
  writeFileSync(path.join(source, 'tampered'), 'x');
  mkdirSync(path.join(source, 'samples'));
  writeFileSync(path.join(source, 'samples', 's01.in'), 'scenario 2\n');
  writeFileSync(path.join(source, 'samples', 's01.out'), 'transcript 2\n');
  const held = path.join(root, 'held');
  mkdirSync(held);
  for (const id of ['h001', 'h002']) { writeFileSync(path.join(held, `${id}.in`), 'i'); writeFileSync(path.join(held, `${id}.out`), 'o'); }
  writeFileSync(path.join(held, 'cases.txt'), 'h001 bytes R1\nh002 bytes R2\n');
  const work = path.join(root, 'work');
  mkdirSync(work);
  return { source, held, work };
}

export const rec = (id, status = 'PASS', reason = '', peak = 100 * MIB) => ({ id, status, reason, rIds: ['R1'], comparison: 'bytes', entries: 3, firstMismatch: null, lifecycle: { memory: { known: true, peakBytes: peak, oomKill: 0 }, leftover: { known: true, found: false } }, wallMs: 321, launchMs: 5 });
export const ran = (cases, over = {}) => async () => ({ exitCode: cases.every(c => c.status === 'PASS') ? 0 : 1, stdout: '', stderr: '', record: { runner: '0.x', launch: { failed: false, stderr: '' }, cases }, memoryCertified: true, ...over });
export const built = (okBuild = true) => async () => ok({ ok: okBuild, exit: okBuild ? 0 : 1, wallMs: 1234, timedOut: false, tail: okBuild ? '' : 'error CS1002' });

/** `scoreQuick` over a ready scoring area `a`, with the fakes of `score`. */
export function scoreWith(a, { buildFn = built(), runFn, traps = [], files = frozenFiles(), ...more } = {}) {
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, caseWallMs: 1, runCeilingMs: 1 };
  return scoreQuick({ stack: 'node', source: a.source, files, sets: [{ name: 'heldout', dir: a.held }], limits: { memoryBytes: 512 * MIB, buildTimeoutMs: 600_000, timeMs: 300_000 }, traps, work: a.work, profile, buildFn, runFn, ...more });
}

export function score(t, { buildFn = built(), runFn, traps = [] } = {}) {
  const a = scoringArea(t);
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, caseWallMs: 1, runCeilingMs: 1 };
  return scoreQuick({ stack: 'node', source: a.source, files: frozenFiles(), sets: [{ name: 'heldout', dir: a.held }], limits: { memoryBytes: 512 * MIB, buildTimeoutMs: 600_000, timeMs: 300_000 }, traps, work: a.work, profile, buildFn, runFn });
}

