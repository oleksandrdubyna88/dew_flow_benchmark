// What v1's four optional seams (W1-W4 of the exploratory pilot, spikes/lang-explore) do TODAY with no seam value given, as plain JSON. `fixtures/explore-seams-baseline.json`
// was written from this module BEFORE any seam existed (run `node test/explore-seams-observe.mjs --write`); `explore-seams.test.mjs` compares the code as it is now to that file,
// so "the default stays byte-identical" is a comparison of JSON, not a memory. The module imports only what the seams touch and never a seam value.
import './fake-home.mjs'; // first: the phase reads os.homedir()
import { createHash } from 'node:crypto';
import { readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { cellOf } from '../lib/bare-record.mjs';
import { launchPhase } from '../lib/bare-phase.mjs';
import { scoreBoxArgv, scoreEnv } from '../lib/bwrap.mjs';
import { agentEnv, dummyToken, layoutPaths } from '../lib/sandbox.mjs';
import { context, items } from './bare-ctx.mjs';

const sha256 = text => createHash('sha256').update(text).digest('hex');

/** agentEnv with the inputs of a fixed layout: the base object, and the object with an `extra` key. */
export const observeAgentEnv = () => {
  const layout = layoutPaths('/r', 's1');
  const call = extra => agentEnv({ layout, dummyToken: dummyToken('d'), cellId: 'c', nodeDir: '/n', tsDir: '/ts', extra });
  return { base: call({}), withExtra: call({ GH_TOKEN: 'x' }) };
};

const SCORE_OPTS = { copyDir: '/w/copy', casesDir: '/w/cases', home: '/w/hm', tmp: '/w/tmp', toolchainDirs: ['/t/a', '/t/b'], nugetCache: '/w/nuget', npmCache: '/w/npm', roFiles: ['/w/copy/test.sh'], cwd: '/w/copy', operatorHome: '/opt/operator-home' };

/** scoreEnv and scoreBoxArgv for fixed inputs, with the home present and absent from the host. */
export const observeScoring = () => ({
  envMinimal: scoreEnv({ home: '/w/hm', tmp: '/w/tmp' }),
  envFull: scoreEnv(SCORE_OPTS),
  argvAllPresent: [...scoreBoxArgv({ ...SCORE_OPTS, exists: () => true })],
  argvNonePresent: [...scoreBoxArgv({ ...SCORE_OPTS, exists: () => false })],
  argvMinimal: [...scoreBoxArgv({ copyDir: '/w/copy', home: '/w/hm', tmp: '/w/tmp', operatorHome: '/opt/operator-home', exists: () => true })],
});

/** A session record with only the leaves cellOf reads. */
const record = ({ cls, label = 'paired', usd, stop = usd, wall, modelApi, active }) => ({
  ids: { label }, class: cls,
  cost: { headlineUsd: usd === null ? { known: false, why: 'x' } : { known: true, value: { usd } }, stopRuleUsd: stop === null ? { known: false, why: 'x' } : { known: true, value: stop } },
  time: { wallMs: { known: true, value: wall }, modelApiMs: { known: true, value: modelApi }, activeMs: { known: true, value: active }, activeMinusModelApiMs: { known: true, value: active - modelApi } },
});

/** cellOf over a completed pair of phases, a censored one, an unpaired one, a missing phase and an unknown cost. */
export const observeCells = () => {
  const done = (usd, label) => record({ cls: 'completed', label, usd, wall: 1000 * usd, modelApi: 400 * usd, active: 900 * usd });
  const killed = record({ cls: 'killed: cap', usd: 5, stop: 6, wall: 5000, modelApi: 2000, active: 4500 });
  return {
    completed: cellOf(done(0.25, 'paired'), done(0.5, 'paired')),
    censored: cellOf(done(0.25, 'paired'), killed),
    unpaired: cellOf(done(0.25, 'ran unpaired'), done(0.5, 'paired')),
    missing: cellOf(done(0.25, 'paired'), null),
    unknownCost: cellOf(done(0.25, 'paired'), record({ cls: 'completed', usd: null, stop: null, wall: 1, modelApi: 1, active: 1 })),
  };
};

/** One v1 phase with no `ctx.fill` and no `ctx.planFor`: what its workspaces hold and what its plans carry (paths and the token left out). */
export async function observePhase() {
  const ctx = context();
  const plans = [];
  const deps = {
    spawn: () => ({}), maskCheckMs: 5, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [],
    launchSession: async plan => { plans.push(plan); return { ok: true, value: { meta: { rid: plan.layout.rid }, exit: { code: 0 } } }; },
  };
  const r = await launchPhase({ ctx, items: items(ctx), deps });
  if (!r.ok) throw new Error(r.reason);
  const view = s => {
    const plan = s.plan;
    return {
      stack: s.session.stack, work: readdirSync(s.layout.work).sort(), seeded: { npm: readdirSync(s.layout.npm).sort(), nuget: readdirSync(s.layout.nuget).sort() },
      promptSha256: sha256(plan.prompt), arm: plan.arm, capUsd: plan.capUsd, wallMs: plan.wallMs, stallMs: plan.stallMs, allow: plan.allow, toolchains: plan.toolchains, tsDir: plan.tsDir, nodeDir: plan.nodeDir,
      envExtra: plan.envExtra, cellId: plan.cellId.replace(s.rid, '<rid>'), planKeys: Object.keys(plan).sort(),
    };
  };
  return r.value.sessions.map(view);
}

export async function observeAll() {
  return { agentEnv: observeAgentEnv(), scoring: observeScoring(), cells: observeCells(), phase: await observePhase() };
}

if (process.argv[1] === fileURLToPath(import.meta.url) && process.argv[2] === '--write') {
  const file = path.join(path.dirname(fileURLToPath(import.meta.url)), 'fixtures', 'explore-seams-baseline.json');
  writeFileSync(file, `${JSON.stringify(await observeAll(), null, 2)}\n`);
  process.stdout.write(`${file}\n`);
}
