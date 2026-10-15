#!/usr/bin/env node
// The attended driver: gate on the arm-parity probe (resolver layout), draw the order, plant canaries, run the cells sequentially,
// score each (and shed its disk), score the reference, report, ALWAYS unplant. Two shapes: the quick pilot's 3 cells (one B among
// [A,A,B]) or `--blocks N` adjacent A/B pairs. A step that throws is RECORDED (run map, outside the runs root), the remaining cells
// are not started, and the reference scoring and the report still run - so a series that dies leaves its data and says how it died.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseCli, requireValues } from './lib/args.mjs';
import { plant, unplant } from './lib/canaries.mjs';
import { DEFAULT_CAP_USD, DEFAULT_STOP_AFTER_USD, NEUTRAL_CELLS } from './lib/consts.mjs';
import { drawBlocks, drawOrder } from './lib/order.mjs';
import { ARM_SHAS } from './lib/consts.mjs';
import { gitlinkAt } from './lib/git.mjs';
import { harnessIdentity } from './lib/harness.mjs';
import { probeGate, probeSubject } from './lib/probe.mjs';
import { isVoid, planResume, assertResumable, setAsideAttempts } from './lib/resume.mjs';
import { VOID_OUTCOME } from './lib/ratelimit.mjs';
import { readRunMap, updateRunMap } from './lib/runmap.mjs';
import { costOfSummary, spendDecision } from './lib/spend.mjs';
import { loadStory, runsRootOf, STORY_OPTION } from './lib/story.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SPEC = {
  order: { type: 'string', help: 'committed seed text; 3-cell mode: B position = sha256(seed) mod 3; --blocks: per-block lead = parity of sha256(seed:block)' },
  blocks: { type: 'string', help: 'adjacent A/B blocks (1..4): cells c1..c(2N), the arm map outside the runs root. Omit for the 3-cell mode' },
  story: STORY_OPTION,
  'runs-root': { type: 'string', help: "runs root (default: the story's defaultRunsRoot)" },
  cap: { type: 'string', default: String(DEFAULT_CAP_USD), help: 'max budget USD per cell (the cost watchdog kills a cell at cap x 1.1)' },
  'stop-after': { type: 'string', default: String(DEFAULT_STOP_AFTER_USD), help: 'a CEILING in USD: a cell is not started when spent so far + cap would pass it' },
  'wall-min': { type: 'string', default: '120', help: 'wall limit per cell, minutes' },
  'probe-root': { type: 'string', help: "resolver layout: where probe-series.mjs wrote probe.json (default: the story's runs root + '-probe')" },
  'skip-probe': { type: 'boolean', help: 'resolver layout: start without a passing arm-parity probe (not for a measured series)' },
  resume: { type: 'boolean', help: 'continue a series the account limit voided: same --runs-root, --order, --blocks; keeps every data point, re-runs void and never-started cells' },
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

/**
 * USD spent so far in this runs root: every c1..c9 summary.json's cost; a cell that ran but was never summarised (it crashed) counts at
 * its run.json meter reading, an ESTIMATE - better than counting a cell that may have spent \$30 as free.
 */
export function spentInRoot(root, read = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null)) {
  return Array.from({ length: 9 }, (_, i) => {
    const dir = path.join(root, `c${i + 1}`);
    const summary = read(path.join(dir, 'summary.json'));
    return summary ? costOfSummary(summary) : Number(read(path.join(dir, 'run.json'))?.meterUsdEstimated ?? 0) || 0;
  }).reduce((a, b) => a + b, 0);
}

/**
 * The cell loop, with every effect injected. Before each cell: stop when spent + cap would pass the ceiling. A cell that throws is
 * recorded as crashed and ends the loop (not the process). Returns how the loop ended.
 */
export async function runAll(plan, { cap, stopAfter, spent, runCell, record, log = () => {} }) {
  for (const [i, p] of plan.entries()) {
    const decision = spendDecision(spent(), stopAfter, cap);
    log(`${p.cell}: ${decision.line}`);
    if (decision.stop) { record.stop({ before: p.cell, line: decision.line }); return { stoppedBefore: p.cell, crashedAt: '', voidAt: '' }; }
    let result;
    try {
      result = (await runCell(p)) ?? {};
    } catch (e) {
      record.crash(p.cell, e.message);
      log(`${p.cell}: CRASHED - ${e.message}; no further cell is started`);
      return { stoppedBefore: '', crashedAt: p.cell, voidAt: '' };
    }
    // The account's limit ended the session: that cell measured the account, not the policy. Every cell after it would hit the same wall.
    if (result.void) {
      record.void(p.cell, result);
      const line = `${p.cell}: ${VOID_OUTCOME} - the series stops here`;
      record.stop({ before: plan[i + 1]?.cell ?? '', cell: p.cell, reason: 'rate limit', line });
      log(line);
      return { stoppedBefore: '', crashedAt: '', voidAt: p.cell };
    }
    record.done?.(p.cell, result);
  }
  return { stoppedBefore: '', crashedAt: '', voidAt: '' };
}

async function runCell({ cell, arm }, v, manifest) {
  const common = ['--cell', cell, '--story', v.story, '--runs-root', v['runs-root']];
  await step('prepare-cell.mjs', [...common, '--arm', arm, ...(v['skip-npm'] ? ['--skip-npm'] : [])]);
  await step('run-cell.mjs', [...common, '--cap', v.cap, '--wall-min', v['wall-min']]);
  await step('summarize-cell.mjs', [...common, '--canary-tokens', manifest]);
  const dir = path.join(v['runs-root'], cell);
  const outcome = readJsonIf(path.join(dir, 'run.json'))?.outcome ?? 'crashed';
  const spendUsd = costOfSummary(readJsonIf(path.join(dir, 'summary.json')));
  // A void cell is never a data point: no scoring (no score copy, no tree install). Its spend is recorded and flagged.
  if (isVoid(outcome)) return { outcome, void: true, spendUsd };
  await step('score-cell.mjs', [...common, ...(v['keep-trees'] ? [] : ['--shed'])]);
  return { outcome, void: false, spendUsd };
}

function parseBlocks(text) {
  if (text === undefined) return undefined;
  const n = Number(text);
  if (!Number.isInteger(n)) throw new Error(`--blocks must be an integer, got ${text}`);
  return n;
}

const readJsonIf = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null);

const probeRootOf = (story, v) => (v['probe-root'] ? path.resolve(v['probe-root']) : `${story.defaultRunsRoot}-probe`);

async function requireProbe(story, v, harness) {
  if (story.layout !== 'resolver' || v['skip-probe']) return null;
  const probeRoot = probeRootOf(story, v);
  const pin = await gitlinkAt(story.repoClone, story.P, story.mount);
  const gate = probeGate(readJsonIf(path.join(probeRoot, 'probe.json')), probeSubject({ story, pin, armShas: ARM_SHAS, harness }), harness);
  if (!gate.ok) throw new Error(`arm-parity probe: ${gate.why} (node probe-series.mjs --story ${story.id})`);
  return readJsonIf(path.join(probeRoot, 'probe.json'));
}

/** The reference and the report run whatever happened to the cells; a failure there is recorded, not thrown over the series. */
async function finish(v, failures) {
  const attempts = [
    ['score-cell.mjs', ['--cell', 'reference', '--story', v.story, '--runs-root', v['runs-root'], ...(v['skip-npm'] ? ['--skip-npm'] : []), ...(v['keep-trees'] ? [] : ['--shed'])]],
    ['report.mjs', ['--story', v.story, '--runs-root', v['runs-root']]],
  ];
  for (const [script, args] of attempts) {
    try { await step(script, args); } catch (e) { failures.push(`${script}: ${e.message}`); console.error(`${script} failed: ${e.message}`); }
  }
}

async function main(v) {
  const story = loadStory(v.story);
  v['runs-root'] = runsRootOf(v, story);
  const harness = await harnessIdentity();
  const resuming = Boolean(v.resume);
  const rawProbe = story.layout === 'resolver' && !v['skip-probe'] ? readJsonIf(path.join(probeRootOf(story, v), 'probe.json')) : null;
  // Resuming says WHY first (the harness or the probe moved), then the probe gate says whether the probe still fits.
  if (resuming) assertResumable({ runMap: readRunMap(story.runMapFile), harness, probeRecord: rawProbe, runsRoot: v['runs-root'] });
  const probeRecord = await requireProbe(story, v, harness);
  const stopAfter = Number(v['stop-after']);
  const cap = Number(v.cap);
  if (![stopAfter, cap].every(Number.isFinite)) throw new Error('--cap and --stop-after must be numbers');
  const manifest = path.join(v['runs-root'], 'canaries.json');
  fs.mkdirSync(v['runs-root'], { recursive: true });
  const fullPlan = planCells(v.order, parseBlocks(v.blocks));
  let plan = fullPlan;
  if (resuming) {
    const map = readRunMap(story.runMapFile);
    const rp = planResume(fullPlan, map, { storyId: story.id, seed: v.order });
    // History first: the void attempts are renamed aside (nothing deleted), their node_modules freed, their spend kept apart.
    const aside = setAsideAttempts(v['runs-root'], rp.run, map, { srcDir: story.srcDir, mount: story.mount });
    updateRunMap({ cells: aside, stop: null, resumes: [...(map.resumes ?? []), { utc: new Date().toISOString(), kept: rp.keep.map(p => p.cell), rerun: rp.run.map(p => p.cell) }] }, story.runMapFile);
    plan = rp.run;
    console.log(`resuming ${story.id}: keeping ${rp.keep.map(p => p.cell).join(' ') || 'none'}; running ${rp.run.map(p => `${p.cell}=${p.arm}`).join(' ') || 'nothing'}`);
  } else {
    // A fresh series starts a fresh run map; the previous one (another runs root's, or a voided one) is kept beside it, not merged into it.
    if (fs.existsSync(story.runMapFile)) fs.renameSync(story.runMapFile, story.runMapFile.replace(/.json$/, `.prev-${Date.now()}.json`));
    updateRunMap({ seed: v.order, order: null, story: story.id, stop: null, resumes: [], series: { runsRoot: v['runs-root'], harnessSpikeTree: harness.spikeTree, harnessCommit: harness.commit, probeCreatedUtc: probeRecord?.createdUtc ?? null, startedUtc: new Date().toISOString() } }, story.runMapFile); // the seed is on record BEFORE it is drawn from
    updateRunMap({ order: fullPlan.map(p => p.arm), blocks: v.blocks === undefined ? null : fullPlan, cells: Object.fromEntries(fullPlan.map(p => [p.cell, { arm: p.arm }])) }, story.runMapFile);
    console.log(`order for seed "${v.order}" (${story.id}): ${fullPlan.map(p => `${p.cell}=${p.arm}`).join(' ')}`);
  }
  let planted = false;
  const cleanup = () => { if (planted) { planted = false; for (const r of unplant(manifest)) console.log(`unplant ${r.status}`); } };
  process.on('SIGINT', () => { cleanup(); process.exit(130); });
  const failures = [];
  try {
    plant(manifest, os.homedir(), story.canarySlug);
    planted = true;
    const ended = await runAll(plan, {
      cap, stopAfter, spent: () => spentInRoot(v['runs-root']), runCell: p => runCell(p, v, manifest), log: console.log,
      record: {
        stop: s => updateRunMap({ stop: s }, story.runMapFile),
        crash: (cell, message) => {
          failures.push(`${cell}: ${message}`);
          // ranModel: did the session ever start? A crash before it is a harness failure and a resume runs the cell again.
          updateRunMap({ cells: { [cell]: { outcome: 'crashed', crash: message, ranModel: fs.existsSync(path.join(v['runs-root'], cell, 'run.json')) } } }, story.runMapFile);
        },
        void: (cell, r) => { failures.push(`${cell}: ${VOID_OUTCOME}`); updateRunMap({ cells: { [cell]: { outcome: r.outcome, voidSpendUsd: r.spendUsd } } }, story.runMapFile); },
        done: (cell, r) => updateRunMap({ cells: { [cell]: { outcome: r.outcome, ranModel: true } } }, story.runMapFile),
      },
    });
    await finish(v, failures);
    if (ended.stoppedBefore) console.log(`series stopped before ${ended.stoppedBefore}`);
  } finally {
    cleanup();
  }
  if (failures.length > 0) throw new Error(`the series did not run clean: ${failures.join(' | ')}`);
}

if (process.argv[1]?.endsWith('pilot.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node pilot.mjs --order <seed> [--blocks 3] [--story coai-751] [--runs-root D:/rsd/_s2] [--cap 30] [--stop-after 100] [--wall-min 120]');
  if (help) console.log(help);
  else {
    requireValues(values, ['order']);
    main(values).catch(e => { console.error(e.message); process.exitCode = 1; });
  }
}
