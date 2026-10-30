// `bare-runner.mjs init` (E3b.S1 step 1): everything a series is pinned to, gathered, checked and written ONCE. `initPlan` computes it and writes nothing
// (`--dry-run` prints it); `applyInit` writes: the pinned CLI copy (every session runs that copy, never the PATH symlink, so an auto-update of the operator's CLI cannot change
// the treatment), `run-map.json`, the ledger seeded with the prior spend (in the $500 ceiling, not in the $40) and the series directories.
import { createHash } from 'node:crypto';
import { chmodSync, copyFileSync, existsSync, mkdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { treeHash } from './cache-snapshot.mjs';
import { appendLedger, priorLines, readLedger } from './ledger.mjs';
import { fail, ok } from './result.mjs';
import { openSeries, writeRunMap } from './run-map.mjs';
import { BARE_CAPS } from './bare-plan.mjs';

const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const SERIES = /^s-\d{8}-[0-9a-f]{4}$/;

function snapshotsOf({ inputsDir, toolchainsDir, verify }) {
  const file = path.join(inputsDir, 'cache-snapshots.json');
  if (!existsSync(file)) return fail(`${file} does not exist: make the cache snapshots (bare-runner.mjs snapshot) and register them before init`);
  const registered = readJson(file);
  const out = {};
  for (const stack of ['csharp', 'node']) {
    const r = registered[stack];
    if (!r?.treeSha256 || !r.parts) return fail(`cache-snapshots.json has no hashed snapshot for ${stack}`);
    const dir = path.join(toolchainsDir, `cache-${stack}-${r.treeSha256.slice(0, 12)}`);
    if (!existsSync(dir)) return fail(`the ${stack} snapshot directory ${dir} does not exist`);
    if (verify && treeHash(dir) !== r.treeSha256) return fail(`the ${stack} snapshot at ${dir} no longer hashes to ${r.treeSha256.slice(0, 12)}`);
    out[stack] = { dir, treeSha256: r.treeSha256, parts: r.parts, sizeBytes: r.sizeBytes ?? null };
  }
  return ok(out);
}

/**
 * Gather and check everything. `facts` = `{ preregTag, preregCommit, harnessIdentity }` (from git; injected). -> `ok(plan)` | `fail` naming the first missing or mismatched input.
 * `plan` carries the run-map as it would be written, the CLI copy to make, the ledger lines to seed and the directories to create.
 */
export function initPlan({ inputsDir, seriesRoot, seriesId, versionsDir, toolchainsDir, facts, verifySnapshots = true }) {
  if (!SERIES.test(seriesId)) return fail(`series id '${seriesId}' must look like s-yyyymmdd-<4 hex>`);
  const stacksFile = path.join(inputsDir, 'stacks.json');
  const absent = ['stacks.json', 'spend-prior.json', 'prices-2026-10-29.json', 'quiet.json', 'package-flags.json'].find(f => !existsSync(path.join(inputsDir, f)));
  if (absent) return fail(`${path.join(inputsDir, absent)} does not exist`);
  const stacks = readJson(stacksFile);
  const pin = stacks.pins.claude;
  const source = path.join(versionsDir, pin);
  if (!existsSync(source)) return fail(`the pinned CLI ${pin} is not at ${source}: the operator's auto-update may have removed it`);
  const seedFile = path.join(inputsDir, 'seed.sha256');
  if (!existsSync(seedFile)) return fail('inputs/seed.sha256 does not exist: run seed-commit and commit its hash with the pre-registration');
  const prices = ['prices-2026-10-29.json'].find(f => existsSync(path.join(inputsDir, f)));
  const snaps = snapshotsOf({ inputsDir, toolchainsDir, verify: verifySnapshots });
  if (!snaps.ok) return snaps;
  const seriesDir = path.join(seriesRoot, seriesId);
  const copy = { from: source, to: path.join(toolchainsDir, `claude-${pin}`), sha256: sha256File(source), sizeBytes: statSync(source).size };
  const runMap = openSeries({
    seriesId, seedSha256: readFileSync(seedFile, 'utf8').trim(), harnessIdentity: facts.harnessIdentity, preregTag: facts.preregTag, preregCommit: facts.preregCommit, pins: stacks.pins,
    stacksSha256: sha256File(stacksFile), pricesFile: prices, pricesSha256: sha256File(path.join(inputsDir, prices)),
    quietSha256: sha256File(path.join(inputsDir, 'quiet.json')), packageFlagsSha256: sha256File(path.join(inputsDir, 'package-flags.json')),
    snapshots: Object.fromEntries(Object.entries(snaps.value).map(([s, v]) => [s, v.treeSha256])), snapshotDirs: Object.fromEntries(Object.entries(snaps.value).map(([s, v]) => [s, v.dir])),
    claude: { path: copy.to, sha256: copy.sha256, version: pin }, caps: BARE_CAPS, bareBudgetUsd: 40, ceilingUsd: 500, spin: null,
  });
  const prior = priorLines(readJson(path.join(inputsDir, 'spend-prior.json')));
  return ok({ seriesDir, runMap, copy, prior, dirs: ['s', 'pairs', 'canaries'].map(d => path.join(seriesDir, d)) });
}

/** The plan as lines for `--dry-run`: all of it, nothing written. */
export const describePlan = plan => [
  `series ${plan.runMap.seriesId} at ${plan.seriesDir}`,
  `pinned CLI ${plan.runMap.pins.claude}: copy ${plan.copy.from} (${plan.copy.sizeBytes} bytes, sha256 ${plan.copy.sha256.slice(0, 12)}) to ${plan.copy.to}, mode 0555`,
  `prereg tag ${plan.runMap.preregTag} -> ${plan.runMap.preregCommit.slice(0, 12)}; harness identity ${plan.runMap.harnessIdentity.slice(0, 12)}; seed hash ${plan.runMap.seedSha256.slice(0, 12)}`,
  `snapshots ${Object.entries(plan.runMap.snapshots).map(([s, h]) => `${s} ${h.slice(0, 12)}`).join(', ')}`,
  `caps $${plan.runMap.caps.capUsd} / ${plan.runMap.caps.wallMs / 60000} min wall / ${plan.runMap.caps.stallMs / 60000} min stall; bare budget $${plan.runMap.bareBudgetUsd}, ceiling $${plan.runMap.ceilingUsd}`,
  `ledger seeded with ${plan.prior.length} prior line(s), $${plan.prior.reduce((a, l) => a + l.usd, 0).toFixed(4)} (in the ceiling, not in the bare budget)`,
  `spin probe: calibrated at init on a quiet machine (${plan.runMap.spin ? `median ${plan.runMap.spin.medianMs} ms` : 'not yet'})`,
];

/** Write the plan: the CLI copy (0555, hash re-checked), the directories, the ledger's prior lines, `run-map.json` (with `spin` as calibrated). -> `ok(runMap)` | `fail`. */
export function applyInit({ plan, spin }) {
  if (existsSync(plan.seriesDir)) return fail(`${plan.seriesDir} already exists: a series is initialised once`);
  mkdirSync(path.dirname(plan.copy.to), { recursive: true });
  if (existsSync(plan.copy.to)) {
    if (sha256File(plan.copy.to) !== plan.copy.sha256) return fail(`${plan.copy.to} exists and is not the pinned CLI (sha256 differs)`);
  } else {
    copyFileSync(plan.copy.from, plan.copy.to);
    chmodSync(plan.copy.to, 0o555);
    if (sha256File(plan.copy.to) !== plan.copy.sha256) return fail('the CLI copy does not hash to its source');
  }
  for (const d of plan.dirs) mkdirSync(d, { recursive: true, mode: 0o700 });
  const ledger = path.join(plan.seriesDir, 'ledger.ndjson');
  if (readLedger(ledger).length === 0) plan.prior.forEach(l => appendLedger(ledger, l));
  const runMap = { ...plan.runMap, spin };
  writeRunMap(path.join(plan.seriesDir, 'run-map.json'), runMap);
  return ok(runMap);
}
