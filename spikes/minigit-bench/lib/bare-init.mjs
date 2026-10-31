// `bare-runner.mjs init` (E3b.S1 step 1): everything a series is pinned to, gathered, checked and written ONCE. `initPlan` computes it and writes nothing
// (`--dry-run` prints it); `applyInit` writes: the pinned CLI copy (every session runs that copy, never the PATH symlink, so an auto-update of the operator's CLI cannot change
// the treatment), `run-map.json`, the ledger seeded with the prior spend (in the $500 ceiling, not in the $40) and the spend of every earlier series under the runs root (in both) and the series directories.
import { createHash } from 'node:crypto';
import { chmodSync, copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { treeHash } from './cache-snapshot.mjs';
import { appendLedger, priorLines, readLedger } from './ledger.mjs';
import { fail, ok } from './result.mjs';
import { openSeries, pendingStops, readRunMap, writeRunMap } from './run-map.mjs';
import { LEDGER_KINDS } from './spend-gate.mjs';
import { BARE_CAPS } from './bare-plan.mjs';

const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const SERIES = /^s-\d{8}-[0-9a-f]{4}$/;

const hashed = r => Boolean(r?.treeSha256 && r.parts);

/** The sentence for a snapshot directory that is missing or no longer hashes to the registered tree, or null. */
function snapshotDirProblem({ stack, r, dir, verify }) {
  if (!existsSync(dir)) return `the ${stack} snapshot directory ${dir} does not exist`;
  return verify && treeHash(dir) !== r.treeSha256 ? `the ${stack} snapshot at ${dir} no longer hashes to ${r.treeSha256.slice(0, 12)}` : null;
}

/** One registered snapshot, found in the toolchains folder and (when `verify`) re-hashed. -> `ok({ dir, treeSha256, parts, sizeBytes })` | `fail`. */
function snapshotOf({ stack, registered, toolchainsDir, verify }) {
  const r = registered[stack];
  if (!hashed(r)) return fail(`cache-snapshots.json has no hashed snapshot for ${stack}`);
  const dir = path.join(toolchainsDir, `cache-${stack}-${r.treeSha256.slice(0, 12)}`);
  const problem = snapshotDirProblem({ stack, r, dir, verify });
  return problem ? fail(problem) : ok({ dir, treeSha256: r.treeSha256, parts: r.parts, sizeBytes: r.sizeBytes ?? null });
}

function snapshotsOf({ inputsDir, toolchainsDir, verify }) {
  const file = path.join(inputsDir, 'cache-snapshots.json');
  if (!existsSync(file)) return fail(`${file} does not exist: make the cache snapshots (bare-runner.mjs snapshot) and register them before init`);
  const registered = readJson(file);
  const out = {};
  for (const stack of ['csharp', 'node']) {
    const one = snapshotOf({ stack, registered, toolchainsDir, verify });
    if (!one.ok) return one;
    out[stack] = one.value;
  }
  return ok(out);
}

/** The other series directories of the runs root, by name. */
const seriesNames = ({ seriesRoot, seriesId }) => (existsSync(seriesRoot) ? readdirSync(seriesRoot).filter(n => SERIES.test(n) && n !== seriesId && statSync(path.join(seriesRoot, n)).isDirectory()).sort() : []);

/** The ledger of an earlier series: `ok(lines)`, or a `fail` for one that is missing or unreadable. */
function loadLedger(seriesRoot, name) {
  const file = path.join(seriesRoot, name, 'ledger.ndjson');
  if (!existsSync(file)) return fail(`the earlier series ${name} has no ledger.ndjson: its spend cannot be counted, so a new series is not started over it`);
  try {
    return ok(readLedger(file));
  } catch (e) {
    return fail(`the ledger of the earlier series ${name} is unreadable (${e.message}): its spend cannot be counted`);
  }
}

/** An earlier series' ledger, checked: `fail` for a line of an unknown kind. */
function earlierLedger(seriesRoot, name) {
  const loaded = loadLedger(seriesRoot, name);
  if (!loaded.ok) return loaded;
  const bad = loaded.value.find(l => l.event === undefined && !LEDGER_KINDS.includes(l.kind));
  return bad ? fail(`the ledger of the earlier series ${name} holds a line of kind '${bad.kind}': the legal kinds are ${LEDGER_KINDS.join(', ')}`) : loaded;
}

const alreadyCarried = (lines, l) => lines.some(x => x.fromSeries === l.fromSeries && x.rid === l.rid);

/** A line this series itself CARRIED from another (`fromSeries`) is a copy: its origin series is read on its own, and counting both would double it; a copy whose origin directory is gone stays. -> the line to add, or null. */
const carried = (l, name, names, lines) => {
  if (l.fromSeries === undefined) return { ...l, fromSeries: name };
  return !names.includes(l.fromSeries) && !alreadyCarried(lines, l) ? l : null;
};

/** Add the spend lines of one earlier ledger (event lines are not spend; the registered prior spend is seeded by the new series itself) to `lines`. */
function addSpend({ lines, ledger, name, names }) {
  for (const l of ledger.filter(x => x.event === undefined && x.kind !== 'prior')) {
    const line = carried(l, name, names, lines);
    if (line) lines.push(line);
  }
}

/**
 * The spend lines of every EARLIER series under the runs root, to be carried into a new series' ledger (review N12): the $40 and the $500 are the bare arm's and the project's, not one
 * series', so a new series (after a stop, say) starts with what the earlier ones spent. Each line keeps its kind (an A/A or data line counts in the $40 too) and is tagged `fromSeries`;
 * event lines are not spend, and an earlier series' `prior` lines are the registered prior spend, which the new series seeds itself (counting them again would double them).
 * A series directory whose ledger is absent, unreadable or holds a line of an unknown kind is a refusal naming it: a spend nobody can read is a spend nobody can count.
 */
function earlierSpend({ seriesRoot, seriesId }) {
  const names = seriesNames({ seriesRoot, seriesId });
  const lines = [];
  for (const name of names) {
    const ledger = earlierLedger(seriesRoot, name);
    if (!ledger.ok) return ledger;
    addSpend({ lines, ledger: ledger.value, name, names });
  }
  return ok({ lines, series: names });
}

const RUNNER = 'node spikes/minigit-bench/bare-runner.mjs';

/** The sentence for sessions an earlier series left `launching`, or null: they may be paid for and are not in its ledger. */
function launchingSentence({ name, dir, map }) {
  const launching = Object.values(map.sessions ?? {}).filter(s => s.status === 'launching').map(s => s.rid);
  return launching.length === 0 ? null : `the earlier series ${name} has a session left launching (${launching.join(', ')}): it may be paid for and is not settled, so its spend is not in the ledger a new series starts from. Settle it first: ${RUNNER} resume --series ${dir} --settle-only`;
}

/** The sentence for a series stop an ended session carries and nobody recorded, or null. */
function waitingStopSentence({ name, dir, map }) {
  const [stop] = map.stopped ? [] : pendingStops(map);
  return stop ? `the earlier series ${name} holds a series stop nobody recorded (${stop.rule} in ${stop.rid}): record it first with ${RUNNER} resume --series ${dir} (it records the stop and launches nothing), then start the new series` : null;
}

/** A sentence when the earlier series `name` has work nobody settled (review MINOR-5), else null. Both kinds are settled by a resume. */
function openWorkOf({ seriesRoot, name }) {
  const dir = path.join(seriesRoot, name);
  const read = readRunMap(path.join(dir, 'run-map.json'));
  if (!read.ok) return `the earlier series ${name} has no readable run-map (${read.reason}): whether it has unsettled work cannot be told, so a new series is not started over it`;
  return [launchingSentence, waitingStopSentence].map(f => f({ name, dir, map: read.value })).find(Boolean) ?? null;
}

const REQUIRED_INPUTS = ['stacks.json', 'spend-prior.json', 'prices-2026-10-29.json', 'quiet.json', 'package-flags.json'];

const idProblem = ({ seriesId }) => (SERIES.test(seriesId) ? null : `series id '${seriesId}' must look like s-yyyymmdd-<4 hex>`);
const inputsProblem = ({ inputsDir }) => {
  const absent = REQUIRED_INPUTS.find(f => !existsSync(path.join(inputsDir, f)));
  return absent ? `${path.join(inputsDir, absent)} does not exist` : null;
};
const cliProblem = ({ inputsDir, versionsDir }) => {
  const pin = readJson(path.join(inputsDir, 'stacks.json')).pins.claude;
  const source = path.join(versionsDir, pin);
  return existsSync(source) ? null : `the pinned CLI ${pin} is not at ${source}: the operator's auto-update may have removed it`;
};
const seedHashProblem = ({ inputsDir }) => (existsSync(path.join(inputsDir, 'seed.sha256')) ? null : 'inputs/seed.sha256 does not exist: run seed-commit and commit its hash with the pre-registration');

/** The sentence for the first input the plan needs that is not there (or a series id that is not one), or null. In this order: the id, the registered inputs, the pinned CLI, the seed hash. */
function planProblem(args) {
  for (const check of [idProblem, inputsProblem, cliProblem, seedHashProblem]) {
    const problem = check(args);
    if (problem) return problem;
  }
  return null;
}

/** The run-map a new series starts with: what it is pinned to, from the inputs on disk. */
function newRunMap({ inputsDir, seriesId, facts, stacks, snaps, copy, pin }) {
  const file = name => path.join(inputsDir, name);
  const prices = 'prices-2026-10-29.json';
  return openSeries({
    seriesId, seedSha256: readFileSync(file('seed.sha256'), 'utf8').trim(), harnessIdentity: facts.harnessIdentity, preregTag: facts.preregTag, preregCommit: facts.preregCommit, pins: stacks.pins,
    stacksSha256: sha256File(file('stacks.json')), pricesFile: prices, pricesSha256: sha256File(file(prices)),
    quietSha256: sha256File(file('quiet.json')), packageFlagsSha256: sha256File(file('package-flags.json')),
    snapshots: Object.fromEntries(Object.entries(snaps).map(([s, v]) => [s, v.treeSha256])), snapshotDirs: Object.fromEntries(Object.entries(snaps).map(([s, v]) => [s, v.dir])),
    claude: { path: copy.to, sha256: copy.sha256, version: pin }, caps: BARE_CAPS, bareBudgetUsd: 40, ceilingUsd: 500, spin: null,
  });
}

/** The spend of the earlier series, unless one of them holds work nobody settled. -> `ok({ lines, series })` | `fail`. */
function earlierChecked({ seriesRoot, seriesId }) {
  const earlier = earlierSpend({ seriesRoot, seriesId });
  if (!earlier.ok) return earlier;
  const open = earlier.value.series.map(name => openWorkOf({ seriesRoot, name })).find(Boolean);
  return open ? fail(open) : earlier;
}

/**
 * Gather and check everything. `facts` = `{ preregTag, preregCommit, harnessIdentity }` (from git; injected). -> `ok(plan)` | `fail` naming the first missing or mismatched input.
 * `plan` carries the run-map as it would be written, the CLI copy to make, the ledger lines to seed and the directories to create.
 */
export function initPlan({ inputsDir, seriesRoot, seriesId, versionsDir, toolchainsDir, facts, verifySnapshots = true }) {
  const problem = planProblem({ seriesId, inputsDir, versionsDir });
  if (problem) return fail(problem);
  const stacks = readJson(path.join(inputsDir, 'stacks.json'));
  const pin = stacks.pins.claude;
  const source = path.join(versionsDir, pin);
  const snaps = snapshotsOf({ inputsDir, toolchainsDir, verify: verifySnapshots });
  if (!snaps.ok) return snaps;
  const seriesDir = path.join(seriesRoot, seriesId);
  const copy = { from: source, to: path.join(toolchainsDir, `claude-${pin}`), sha256: sha256File(source), sizeBytes: statSync(source).size };
  const runMap = newRunMap({ inputsDir, seriesId, facts, stacks, snaps: snaps.value, copy, pin });
  const earlier = earlierChecked({ seriesRoot, seriesId });
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

/** The pinned CLI already at its place must be the pinned one. -> `fail` or null. */
function existingCliProblem(plan) {
  return sha256File(plan.copy.to) === plan.copy.sha256 ? null : fail(`${plan.copy.to} exists and is not the pinned CLI (sha256 differs)`);
}

/** Copy the pinned CLI to its place, read-only and executable, and re-check its hash. -> `fail` or null. */
function copyCli(plan) {
  copyFileSync(plan.copy.from, plan.copy.to);
  chmodSync(plan.copy.to, 0o555);
  return sha256File(plan.copy.to) === plan.copy.sha256 ? null : fail('the CLI copy does not hash to its source');
}

const installCli = plan => (existsSync(plan.copy.to) ? existingCliProblem(plan) : copyCli(plan));

/** The ledger starts with the prior lines (and the earlier series' spend); a ledger that already has lines is left alone. */
function seedLedger(plan) {
  const ledger = path.join(plan.seriesDir, 'ledger.ndjson');
  if (readLedger(ledger).length === 0) plan.prior.forEach(l => appendLedger(ledger, l));
}

/** Write the plan: the CLI copy (0555, hash re-checked), the directories, the ledger's prior lines, `run-map.json` (with `spin` as calibrated). -> `ok(runMap)` | `fail`. */
export function applyInit({ plan, spin }) {
  if (existsSync(plan.seriesDir)) return fail(`${plan.seriesDir} already exists: a series is initialised once`);
  mkdirSync(path.dirname(plan.copy.to), { recursive: true });
  const installed = installCli(plan);
  if (installed) return installed;
  for (const d of plan.dirs) mkdirSync(d, { recursive: true, mode: 0o700 });
  seedLedger(plan);
  const runMap = { ...plan.runMap, spin };
  writeRunMap(path.join(plan.seriesDir, 'run-map.json'), runMap);
  return ok(runMap);
}
