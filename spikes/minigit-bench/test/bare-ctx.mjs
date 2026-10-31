// Shared fixtures of the bare runner's tests: a run context over a fake inputs cache, tiny cache snapshots, a fake operator home with one project folder, and the two
// sessions of a v1 phase. The cache is a stand-in with the real layout and real hashes (workspace.mjs verifies them), not a fixture the code could reject.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { treeHash } from '../lib/cache-snapshot.mjs';
import { sha256Of } from '../lib/inputs.mjs';
import { ridOf } from '../lib/bare-plan.mjs';
import { tmpDir } from './helpers.mjs';

export const SEED = 'mg-0123456789abcdef';
export const write = (p, text) => { mkdirSync(path.dirname(p), { recursive: true }); writeFileSync(p, text); };

export function context() {
  const cache = tmpDir('cache');
  const files = { 'upstream/SPEC-v1.txt': 's1\n', 'upstream/SPEC-v2.txt': 's2\n', 'test-v1.sh': 't1\n', 'test-v2.sh': 't2\n' };
  for (const [p, t] of Object.entries(files)) write(path.join(cache, p), t);
  const csharp = tmpDir('snap-cs');
  write(path.join(csharp, 'nuget', 'a', 'f'), 'n');
  write(path.join(csharp, 'dotnet', 's'), 'd');
  const node = tmpDir('snap-nd');
  write(path.join(node, 'npm', 'i'), 'i');
  const home = tmpDir('home');
  mkdirSync(path.join(home, '.claude', 'projects', 'p1', 'memory'), { recursive: true });
  const seriesDir = tmpDir('series');
  return {
    seriesDir, sessionsRoot: path.join(seriesDir, 's'), home, inputsRoot: cache, hashes: Object.entries(files).map(([p, t]) => ({ path: p, sha256: sha256Of(Buffer.from(t)) })),
    snapshots: {
      csharp: { dir: csharp, parts: { nuget: treeHash(path.join(csharp, 'nuget')), dotnet: treeHash(path.join(csharp, 'dotnet')) } },
      node: { dir: node, parts: { npm: treeHash(path.join(node, 'npm')) } },
    },
    claudeBinary: '/x/claude', cliVersion: '2.1.310', nodeDir: '/n', tsDir: '/ts', token: 'real-token-value', prices: {}, harnessSources: { boxInit: 'a', audit: 'b' }, caps: { capUsd: 6, wallMs: 1, stallMs: 1 },
    v1WorkOf: () => { throw new Error('a v1 phase has no v1 tree'); },
  };
}
export const items = (ctx, seed = SEED) => ['csharp', 'node'].map((stack, i) => ({ session: { stack, phase: 'v1', attempt: 1 }, rid: ridOf({ seed, pair: 1, stack, phase: 'v1', attempt: 1 }), mask: ['0-5', '8-13'][i] }));
export const sleep = ms => new Promise(r => setTimeout(r, ms));

