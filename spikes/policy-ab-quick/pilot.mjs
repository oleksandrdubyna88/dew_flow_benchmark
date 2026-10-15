#!/usr/bin/env node
// The attended driver: draw the order, plant canaries, run the cells sequentially, score each (and shed its disk), score the
// reference, ALWAYS unplant. Two shapes: the quick pilot's 3 cells (one B among [A,A,B]) or `--blocks N` adjacent A/B pairs.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseCli, requireValues } from './lib/args.mjs';
import { plant, unplant } from './lib/canaries.mjs';
import { DEFAULT_CAP_USD, DEFAULT_STOP_AFTER_USD, NEUTRAL_CELLS } from './lib/consts.mjs';
import { drawBlocks, drawOrder } from './lib/order.mjs';
import { updateRunMap } from './lib/runmap.mjs';
import { costOfSummary, spendDecision } from './lib/spend.mjs';
import { loadStory, runsRootOf, STORY_OPTION } from './lib/story.mjs';
import os from 'node:os';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SPEC = {
  order: { type: 'string', help: 'committed seed text; 3-cell mode: B position = sha256(seed) mod 3; --blocks: per-block lead = parity of sha256(seed:block)' },
  blocks: { type: 'string', help: 'adjacent A/B blocks (1..4): cells c1..c(2N), the arm map outside the runs root. Omit for the 3-cell mode' },
  story: STORY_OPTION,
  'runs-root': { type: 'string', help: "runs root (default: the story's defaultRunsRoot)" },
  cap: { type: 'string', default: String(DEFAULT_CAP_USD), help: 'max budget USD per cell' },
  'stop-after': { type: 'string', default: String(DEFAULT_STOP_AFTER_USD), help: 'before each cell: stop the series when the spend so far exceeds this many USD' },
  'wall-min': { type: 'string', default: '120', help: 'wall limit per cell, minutes' },
  'keep-trees': { type: 'boolean', help: "do not delete a scored cell's score/ copies and node_modules" },
  'skip-npm': { type: 'boolean', help: 'skip npm ci (tests only)' },
};

/** Pure: the cell plan for a seed - neutral names in run order, each with its arm (and block/position in block mode). */
export function planCells(seed, blocks) {
  if (blocks === undefined) {
    const arms = drawOrder(seed);
    return NEUTRAL_CELLS.map((cell, i) => ({ cell, arm: arms[i] }));
  }
  return drawBlocks(seed, blocks);
}

function step(script, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [path.join(HERE, script), ...args], { stdio: 'inherit', shell: false });
    child.on('error', reject);
    child.on('close', code => (code === 0 ? resolve() : reject(new Error(`${script} exited ${code}`))));
  });
}

/** USD spent so far in this runs root: every c1..c9 summary.json's cost (a cell without one cost nothing we can see). */
export function spentInRoot(root, read = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null)) {
  return Array.from({ length: 9 }, (_, i) => costOfSummary(read(path.join(root, `c${i + 1}`, 'summary.json')))).reduce((a, b) => a + b, 0);
}

async function runCell({ cell, arm }, v, manifest) {
  const common = ['--cell', cell, '--story', v.story, '--runs-root', v['runs-root']];
  await step('prepare-cell.mjs', [...common, '--arm', arm, ...(v['skip-npm'] ? ['--skip-npm'] : [])]);
  await step('run-cell.mjs', [...common, '--cap', v.cap, '--wall-min', v['wall-min']]);
  await step('summarize-cell.mjs', [...common, '--canary-tokens', manifest]);
  await step('score-cell.mjs', [...common, ...(v['keep-trees'] ? [] : ['--shed'])]);
}

async function runAll(plan, v, manifest, stopAfter) {
  for (const p of plan) {
    const decision = spendDecision(spentInRoot(v['runs-root']), stopAfter);
    console.log(`${p.cell}: ${decision.line}`);
    if (decision.stop) { updateRunMap({ stop: { before: p.cell, line: decision.line } }, v.runMapFile); return p.cell; }
    await runCell(p, v, manifest);
  }
  return '';
}

function parseBlocks(text) {
  if (text === undefined) return undefined;
  const n = Number(text);
  if (!Number.isInteger(n)) throw new Error(`--blocks must be an integer, got ${text}`);
  return n;
}

async function main(v) {
  const story = loadStory(v.story);
  v['runs-root'] = runsRootOf(v, story);
  v.runMapFile = story.runMapFile;
  const stopAfter = Number(v['stop-after']);
  if (!Number.isFinite(stopAfter)) throw new Error(`--stop-after must be a number, got ${v['stop-after']}`);
  const manifest = path.join(v['runs-root'], 'canaries.json');
  fs.mkdirSync(v['runs-root'], { recursive: true });
  updateRunMap({ seed: v.order, order: null, story: story.id, stop: null }, story.runMapFile); // the seed is on record BEFORE it is drawn from
  const plan = planCells(v.order, parseBlocks(v.blocks));
  updateRunMap({ order: plan.map(p => p.arm), blocks: v.blocks === undefined ? null : plan, cells: Object.fromEntries(plan.map(p => [p.cell, { arm: p.arm }])) }, story.runMapFile);
  console.log(`order for seed "${v.order}" (${story.id}): ${plan.map(p => `${p.cell}=${p.arm}`).join(' ')}`);
  let planted = false;
  const cleanup = () => { if (planted) { planted = false; for (const r of unplant(manifest)) console.log(`unplant ${r.status}`); } };
  process.on('SIGINT', () => { cleanup(); process.exit(130); });
  try {
    plant(manifest, os.homedir(), story.canarySlug);
    planted = true;
    const stoppedBefore = await runAll(plan, v, manifest, stopAfter);
    await step('score-cell.mjs', ['--cell', 'reference', '--story', v.story, '--runs-root', v['runs-root'], ...(v['skip-npm'] ? ['--skip-npm'] : []), ...(v['keep-trees'] ? [] : ['--shed'])]);
    await step('report.mjs', ['--story', v.story, '--runs-root', v['runs-root']]);
    if (stoppedBefore) console.log(`series stopped before ${stoppedBefore}: ${spendDecision(spentInRoot(v['runs-root']), stopAfter).line}`);
  } finally {
    cleanup();
  }
}

if (process.argv[1]?.endsWith('pilot.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node pilot.mjs --order <seed> [--blocks 3] [--story coai-751] [--runs-root D:/rsd/_s2] [--cap 30] [--stop-after 100] [--wall-min 120]');
  if (help) console.log(help);
  else {
    requireValues(values, ['order']);
    main(values).catch(e => { console.error(e.message); process.exitCode = 1; });
  }
}
