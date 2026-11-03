// Counting what ONE probe (or one run) left, with the repository's single registered token `bench` (M1, fix batch 5). Tests used their own tokens (`benchsig`, `benchbld`, `benchwfour`), which are no row of the
// conventions' `## Tokens` table: neither the suite-start sweep nor `sweepStale` could collect what a killed run of such a test left. Now the child gets a PRIVATE temp root and runs parent (an owned folder of
// the test, removed with it), so every folder it makes is the test's own, and the ids of its runs and builds — the random part of the owned names — pick out its units and processes among the others'.
import { spawnSync } from 'node:child_process';
import { mkdirSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { owned } from './owned.mjs';

/** `{ env, tmp, runs }`: an owned folder with `tmp/` (the child's TMPDIR) and `cache/` (its XDG_CACHE_HOME; the runs parent is `cache/dew_flow_bench/lang-bench-runs`). */
export function privateProbeEnv(t) {
  const root = owned.ownedDir(t, 'private');
  const tmp = path.join(root, 'tmp');
  const cache = path.join(root, 'cache');
  mkdirSync(tmp);
  mkdirSync(cache);
  return { root, tmp, runs: path.join(cache, 'dew_flow_bench', 'lang-bench-runs'), env: { ...process.env, TMPDIR: tmp, XDG_CACHE_HOME: cache } };
}

const ID = /-([A-Za-z0-9]{6})$/;

const idsIn = folder => { try { return readdirSync(folder).map(name => ID.exec(name)?.[1]).filter(Boolean); } catch { return []; } };

/** The ids of the runs and builds the child has made so far (its private runs parent and temp root); call it again and again while the child lives and keep the union. */
export const idsOf = ({ tmp, runs }) => new Set([...idsIn(runs), ...idsIn(tmp)].filter(id => !/^(private|tmp|cache)$/.test(id)));

/** The units and processes whose names carry one of `ids`: `{ units, procs }` (argv joined, so a build's `-p:BenchBuildMarker=…` is found as well). */
export function thingsOf(ids) {
  if (ids.size === 0) return new Set();
  const carries = text => [...ids].some(id => text.includes(`-${id}`));
  const units = spawnSync('systemctl', ['--user', 'list-units', '--all', '--plain', '--no-legend', '--full'], { encoding: 'utf8' }).stdout.split('\n').map(line => line.trim().split(/\s+/)[0]).filter(name => name.startsWith('testsToDelete-') && carries(name));
  const procs = readdirSync('/proc').filter(entry => /^\d+$/.test(entry)).flatMap(pid => {
    try {
      const argv = readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0').join(' ');
      return argv.includes('testsToDelete-') && carries(argv) ? [`${pid}: ${argv.slice(0, 80)}`] : [];
    } catch {
      return [];
    }
  });
  return new Set([...units, ...procs]);
}

/** Every pid whose argv mentions `text` (a build's marker). */
export const pidsMentioning = text => readdirSync('/proc').filter(entry => /^\d+$/.test(entry)).filter(pid => { try { return readFileSync(`/proc/${pid}/cmdline`, 'utf8').includes(text); } catch { return false; } });

/** The entries of `dir` whose name matches `pattern` (a test's check that a folder kind was not left in the child's OWN temp root). */
export const leftNamed = (dir, pattern) => { try { return readdirSync(dir).filter(name => pattern.test(name)); } catch { return []; } };

/**
 * `{ markerFile }` in a fresh owned folder of the test, with a hook that kills every process whose argv mentions the marker written there (K5, Opus M2). `node:test` runs `t.after` hooks in REGISTRATION order, so the
 * kill hook is registered BEFORE the folder: the folder's own removal hook (registered by `ownedDir`) then runs after it, and the marker file is still there when the kill hook reads it.
 */
export function markerFolder(t, what) {
  let markerFile = null;
  t.after(() => {
    try {
      for (const pid of pidsMentioning(readFileSync(markerFile, 'utf8').trim())) process.kill(Number(pid), 'SIGKILL');
    } catch { /* no marker was written, or the child is gone */ }
  });
  markerFile = path.join(owned.ownedDir(t, what), 'marker.txt');
  return { markerFile };
}
