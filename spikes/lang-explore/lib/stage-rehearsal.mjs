// The free rehearsal of a Stage 1 series (F3.S1 (a), PR 3): `stage-run` on an `r-` series runs the unit through the REAL loop, box, broker, proxy, build box, execution layer and runner, with v1's stub CLI as the CLI and v1's stub upstream as the API, a fake
// token (the real token file is never read), each workspace pre-filled with the hello program of its stack and kind, the FIXTURE held-out cases of the explore root (never the real ones, D41) and a scripted quiet machine. It costs $0. The scoring
// is one pass (a rehearsal is a check of the pipeline and gives no verdict). `rehearsalIo` returns the `io` the unit runs with and the function that closes the stub.
import { randomBytes } from 'node:crypto';
import { mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { stubClaude } from '../../minigit-bench/lib/stub-cli.mjs';
import { chunksOf, sseAnswer, sseBody, stubUpstream } from '../../minigit-bench/lib/stub-upstream.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { allOk } from './chain.mjs';
import { calibrationFor } from './limits.mjs';
import { machineFor, pilotQuiet } from './machine.mjs';
import { pinsOk } from './pins.mjs';
import { exploreRoot } from './paths.mjs';
import { quickReferenceFill, rehearsalHeldout } from './rehearsal-quick.mjs';
import { fakeTokenHome } from './rehearse.mjs';
import { seriesPrompts } from './series.mjs';
import { loadWorld } from './world.mjs';

const QUIET = Object.freeze({ hostCpus: 0.1, harnessCpus: 0.05, foreignCpus: 0, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 });
const PLAN_OVER = { tickMs: 500, sampleMs: 500, settleMs: 500, graceMs: 2000 };

/** A machine that is quiet whenever it is asked: a rehearsal measures nothing, and a scripted sample keeps the loop's gates the real ones. */
function scriptedMachine(seriesDir) {
  let clock = 0;
  return machineFor({ thresholds: pilotQuiet().value, calibratedMs: 20, seriesDir, samplerFn: async () => async () => QUIET, sleepFn: async () => {}, now: () => (clock += 7000) });
}

/** The prerequisites of a rehearsal unit: the series' own limits and prompts, the world of the task (loaded once), the FIXTURE held-out cases and a fake token. -> `ok({ calibration, heldout, world, token, prompts })` | `fail`. */
const prerequisitesOf = ({ io, series, task, heldout, loaded }) => async () => {
  const got = await allOk([() => calibrationFor({ home: io.home, seriesDir: series.dir, task }), () => loaded, () => seriesPrompts(series.dir, task.id)]);
  if (!got.ok) return got;
  const [calibration, world, prompts] = got.value;
  const pins = pinsOk({ explore: world.explore, task, home: io.home, snapshots: world.snapshots });
  return pins.ok ? ok({ calibration, heldout, world: { ...world, calibration }, token: `sk-ant-fake-${randomBytes(8).toString('hex')}`, prompts }) : pins;
};

/** The stub rig: v1's stub CLI (reporting the CLI version the series pins, when its world loaded) behind a loopback stub upstream, a fake token home, the scripted quiet machine, the hello program as the work and one pass of scoring. */
function seamsOf({ series, stub, fake, fill, cliVersion }) {
  return { claudeBinary: stubClaude(path.join(series.dir, 'rehearsal'), 'claude-stub', { version: cliVersion }), planOver: { upstream: stub.upstream, ...PLAN_OVER }, preflightHome: fake, stub: true, machine: scriptedMachine(series.dir), scoreOver: { singlePass: true }, ...(fill ? { fill } : {}) };
}

/** The `io` of a rehearsal unit and its `close`. `task` is the pins entry of the unit's task. */
export async function rehearsalIo({ series, io, task }) {
  mkdirSync(path.join(series.dir, 'rehearsal'), { recursive: true });
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  const fake = fakeTokenHome();
  const heldout = rehearsalHeldout({ dir: path.join(exploreRoot(io.home), 'rehearsal-fixtures', task.id), task });
  const loaded = await (io.worldFn ?? loadWorld)({ home: io.home, work: path.join(series.dir, 'private'), task });
  const fill = loaded.ok ? quickReferenceFill({ world: loaded.value, task }) : null;
  const cliVersion = loaded.ok ? loaded.value.explore.cli.version : undefined;
  return {
    io: { ...io, seams: { ...seamsOf({ series, stub, fake, fill, cliVersion }), ...(io.seams ?? {}) }, runSeams: { prerequisitesFn: prerequisitesOf({ io, series, task, heldout, loaded }), ...(io.runSeams ?? {}) } },
    close: async () => { await stub.close(); rmSync(fake, { recursive: true, force: true }); },
  };
}
