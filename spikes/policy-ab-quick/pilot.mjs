#!/usr/bin/env node
// The attended pilot driver: draw the order, plant canaries, run c1..c3 sequentially, score the reference, ALWAYS unplant.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseCli, requireValues } from './lib/args.mjs';
import { plant, unplant } from './lib/canaries.mjs';
import { DEFAULT_RUNS_ROOT, NEUTRAL_CELLS } from './lib/consts.mjs';
import { drawOrder } from './lib/order.mjs';
import { updateRunMap } from './lib/runmap.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SPEC = {
  order: { type: 'string', help: 'committed seed text; B position = sha256(seed) mod 3' },
  'runs-root': { type: 'string', default: DEFAULT_RUNS_ROOT, help: 'runs root' },
  cap: { type: 'string', default: '40', help: 'max budget USD per cell' },
  'wall-min': { type: 'string', default: '120', help: 'wall limit per cell, minutes' },
  'skip-npm': { type: 'boolean', help: 'skip npm ci (tests only)' },
};

/** Pure: the cell plan for a seed - neutral names in run order, each with its arm. */
export function planCells(seed) {
  const arms = drawOrder(seed);
  return NEUTRAL_CELLS.map((cell, i) => ({ cell, arm: arms[i] }));
}

function step(script, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [path.join(HERE, script), ...args], { stdio: 'inherit', shell: false });
    child.on('error', reject);
    child.on('close', code => (code === 0 ? resolve() : reject(new Error(`${script} exited ${code}`))));
  });
}

async function runCell({ cell, arm }, v, manifest) {
  const common = ['--cell', cell, '--runs-root', v['runs-root']];
  await step('prepare-cell.mjs', [...common, '--arm', arm, ...(v['skip-npm'] ? ['--skip-npm'] : [])]);
  await step('run-cell.mjs', [...common, '--cap', v.cap, '--wall-min', v['wall-min']]);
  await step('summarize-cell.mjs', [...common, '--canary-tokens', manifest]);
  await step('score-cell.mjs', common);
}

async function main(v) {
  const root = path.resolve(v['runs-root']);
  const manifest = path.join(root, 'canaries.json');
  fs.mkdirSync(root, { recursive: true });
  updateRunMap({ seed: v.order, order: null }); // the seed is on record BEFORE it is drawn from
  const plan = planCells(v.order);
  updateRunMap({ order: plan.map(p => p.arm), cells: Object.fromEntries(plan.map(p => [p.cell, { arm: p.arm }])) });
  console.log(`order for seed "${v.order}": ${plan.map(p => `${p.cell}=${p.arm}`).join(' ')}`);
  let planted = false;
  const cleanup = () => { if (planted) { planted = false; for (const r of unplant(manifest)) console.log(`unplant ${r.status}`); } };
  process.on('SIGINT', () => { cleanup(); process.exit(130); });
  try {
    plant(manifest);
    planted = true;
    for (const p of plan) await runCell(p, v, manifest);
    await step('score-cell.mjs', ['--cell', 'reference', '--runs-root', v['runs-root'], ...(v['skip-npm'] ? ['--skip-npm'] : [])]);
    await step('report.mjs', ['--runs-root', v['runs-root']]);
  } finally {
    cleanup();
  }
}

if (process.argv[1]?.endsWith('pilot.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node pilot.mjs --order <seed> [--runs-root D:/rsd/_q1] [--cap 40] [--wall-min 120]');
  if (help) console.log(help);
  else {
    requireValues(values, ['order']);
    main(values).catch(e => { console.error(e.message); process.exitCode = 1; });
  }
}
