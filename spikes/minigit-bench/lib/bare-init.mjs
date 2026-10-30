// `bare-runner.mjs init` (E3b.S1 step 1): everything a series is pinned to, gathered, checked and written ONCE. `initPlan` computes it and writes nothing
// (`--dry-run` prints it); `applyInit` writes: the pinned CLI copy (every session runs that copy, never the PATH symlink, so an auto-update of the operator's CLI cannot change
// the treatment), `run-map.json`, the ledger seeded with the prior spend (in the $500 ceiling, not in the $40) and the spend of every earlier series under the runs root (in both) and the series directories.
import { createHash } from 'node:crypto';
import { chmodSync, copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { treeHash } from './cache-snapshot.mjs';
import { appendLedger, priorLines, readLedger } from './ledger.mjs';
import { fail, ok } from './result.mjs';
import { openSeries, writeRunMap } from './run-map.mjs';
import { LEDGER_KINDS } from './spend-gate.mjs';
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
 * The spend lines of every EARLIER series under the runs root, to be carried into a new series' ledger (review N12): the $40 and the $500 are the bare arm's and the project's, not one
 * series', so a new series (after a stop, say) starts with what the earlier ones spent. Each line keeps its kind (an A/A or data line counts in the $40 too) and is tagged `fromSeries`;
 * event lines are not spend, and an earlier series' `prior` lines are the registered prior spend, which the new series seeds itself (counting them again would double them).
 * A series directory whose ledger is absent, unreadable or holds a line of an unknown kind is a refusal naming it: a spend nobody can read is a spend nobody can count.
 */
function earlierSpend({ seriesRoot, seriesId }) {
  const names = existsSync(seriesRoot) ? readdirSync(seriesRoot).filter(n => SERIES.test(n) && n !== seriesId && statSync(path.join(seriesRoot, n)).isDirectory()).sort() : [];
  const lines = [];
  for (const name of names) {
    const file = path.join(seriesRoot, name, 'ledger.ndjson');
    if (!existsSync(file)) return fail(`the earlier series ${name} has no ledger.ndjson: its spend cannot be counted, so a new series is not started over it`);
    let ledger;
    try { ledger = readLedger(file); } catch (e) { return fail(`the ledger of the earlier series ${name} is unreadable (${e.message}): its spend cannot be counted`); }
    const bad = ledger.find(l => l.event === undefined && !LEDGER_KINDS.includes(l.kind));
    if (bad) return fail(`the ledger of the earlier series ${name} holds a line of kind '${bad.kind}': the legal kinds are ${LEDGER_KINDS.join(', ')}`);
    for (const l of ledger.filter(x => x.event === undefined && x.kind !== 'prior')) {
      // a line this series itself CARRIED from another (`fromSeries`) is a copy: its origin series is read on its own, and counting both would double it; a copy whose origin directory is gone stays
      if (l.fromSeries === undefined) lines.push({ ...l, fromSeries: name });
      else if (!names.includes(l.fromSeries) && !lines.some(x => x.fromSeries === l.fromSeries && x.rid === l.rid)) lines.push(l);
    }
  }
  return ok({ lines, series: names });
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
  const earlier = earlierSpend({ seriesRoot, seriesId });
  if (!earlier.ok) return earlier;
  const prior = [...priorLines(readJson(path.join(inputsDir, 'spend-prior.json'))), ...earlier.value.lines];
  return ok({ seriesDir, runMap, copy, prior, earlier: { series: earlier.value.series, lines: earlier.value.lines.length }, dirs: ['s', 'pairs', 'canaries'].map(d => path.join(seriesDir, d)) });
}

/** The plan as lines for `--dry-run`: all of it, nothing written. */
export const describePlan = plan => [
  `series ${plan.runMap.seriesId} at ${plan.seriesDir}`,
  `pinned CLI ${plan.runMap.pins.claude}: copy ${plan.copy.from} (${plan.copy.sizeBytes} bytes, sha256 ${plan.copy.sha256.slice(0, 12)}) to ${plan.copy.to}, mode 0555`,
  `prereg tag ${plan.runMap.preregTag} -> ${plan.runMap.preregCommit.slice(0, 12)}; harness identity ${plan.runMap.harnessIdentity.slice(0, 12)}; seed hash ${plan.runMap.seedSha256.slice(0, 12)}`,
  `snapshots ${Object.entries(plan.runMap.snapshots).map(([s, h]) => `${s} ${h.slice(0, 12)}`).join(', ')}`,
  `caps $${plan.runMap.caps.capUsd} / ${plan.runMap.caps.wallMs / 60000} min wall / ${plan.runMap.caps.stallMs / 60000} min stall; bare budget $${plan.runMap.bareBudgetUsd}, ceiling $${plan.runMap.ceilingUsd}`,
  `ledger seeded with ${plan.prior.length - plan.earlier.lines} prior line(s), $${plan.prior.filter(l => l.kind === 'prior').reduce((a, l) => a + l.usd, 0).toFixed(4)} (in the ceiling, not in the bare budget)`,
  `and ${plan.earlier.lines} spend line(s) of ${plan.earlier.series.length} earlier series, $${plan.prior.filter(l => l.kind !== 'prior').reduce((a, l) => a + l.usd, 0).toFixed(4)} (kept as they were earned: bare lines count in the $40 and in the $500)`,
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
