// The seams `scorePasses` takes (F1.S2), built for the QUICK world from what a scoring call is given: the clean copy of the agent's tree, the case folder, the build in the stack's box, the pass child under `taskset`, the
// affinity probe, the series state and the growth record. A test's `quickSeams` (`buildFn`, `runFn`, `probeFn`) replace the box, the layer and the probe: with a `runFn` the pass runs in this process (no child, no
// `taskset`), with none it runs in the pass child.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { holdForCaseWall } from '../../lang-bench/lib/exec-layer/prefix.mjs';
import { readRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { boxBuild } from './build-quick.mjs';
import { probeMask } from './affinity.mjs';
import { loadGrowth } from './growth.mjs';
import { passBody } from './pass-child.mjs';
import { runPassChild } from './pass-run.mjs';
import { layerDirs } from './quick.mjs';
import { OWNED_TOKEN, caseFolder, prepareTree, runPass } from './score-quick.mjs';
import { removeArea } from './score.mjs';
import { runMapFileOf } from './series.mjs';

/** The build of the stack's box (or the test's), as `({ tree }) -> ok({ ok, exit, timedOut, wallMs, tail }) | environment fail`. */
function buildSeam({ a, profile, seams, owned, dirs }) {
  const buildFn = seams.buildFn ?? boxBuild({ owned, dirs, snapshots: profile.snapshots, stack: a.stack });
  return async ({ tree }) => {
    const built = await buildFn({ tree, timeoutMs: a.limits.buildTimeoutMs });
    if (built.ok) (a.say ?? (() => {}))(`build ${built.value.ok ? 'passed' : `FAILED (exit ${built.value.exit}${built.value.timedOut ? ', timed out' : ''})`} in ${built.value.wallMs} ms`);
    return built;
  };
}

/** One pass: in the pass child under `taskset -c <profile>`, or (a test's `runFn`) in this process. -> the attempt body. */
function passSeam({ a, profile, seams, owned, dirs }) {
  if (seams.runFn === undefined) return spec => runPassChild({ spec, context: { stack: a.stack, kind: profile.kind, dirs } });
  const inProcess = { stack: a.stack, profile: { kind: profile.kind }, runFn: seams.runFn };
  return async spec => passBody({ ran: await runPass({ a: { ...inProcess, limits: spec.limits }, copy: spec.tree, cases: { dir: spec.samplesDir, ids: spec.caseIds }, owned, dirs }), observedCpus: spec.cpus });
}

/** The sessions of the series that are launching right now, from the run-map beside the session's folder (`<series>/run-map.json`). */
function launchingIn(seriesDir) {
  const file = runMapFileOf(seriesDir);
  const map = existsSync(file) ? readRunMap(file) : { ok: true, value: { sessions: {} } };
  return { launching: Object.values(map.ok ? map.value.sessions : {}).filter(s => s.status === 'launching').map(s => s.rid) };
}

/**
 * `a` = `{ stack, source, files, sets, limits, traps, driftPossible, say }` (the scoring call), `profile` the quick world's (`kind`, `paths`, `snapshots`, `caseWallMs`, `runCeilingMs`), `seams` the test seams, `seriesDir` the series.
 * -> the part of `scorePasses`'s argument that comes from the quick world (the caller adds `work`, `seriesDir`, `signal`, `now`).
 */
export function passSeams({ a, profile, seams = {}, seriesDir }) {
  const owned = ownedFor({ token: OWNED_TOKEN });
  const dirs = layerDirs(profile.paths);
  const args = { ...a, profile };
  return {
    prepare: async dest => prepareTree({ ...args }, dest),
    cases: ({ dir }) => { removeArea(dir); return caseFolder(args, path.dirname(dir)); },
    build: buildSeam({ a, profile, seams, owned, dirs }),
    runPass: passSeam({ a, profile, seams, owned, dirs }),
    affinity: { probe: seams.probeFn ?? (cpus => probeMask({ cpus })) },
    seriesState: () => launchingIn(seriesDir),
    growth: { loaded: seams.growthFn ?? (() => loadGrowth()) },
    limits: a.limits, traps: a.traps ?? [], say: a.say,
    spec: { caseWallMs: profile.caseWallMs, holdSeconds: holdForCaseWall(profile.caseWallMs), runCeilingMs: profile.runCeilingMs },
  };
}
