#!/usr/bin/env node
// Writes `inputs/prereg/manifest.json` (F3.S1 (b), PR 5): every identity the pre-registration freezes, read live from the place that owns it by PR 4's `liveIdentities()` (the Stage 1 harness identity among them), the registered floor of a
// session folder's size (the largest kept session folder of the quick run's CLOSED one-task series, measured now; their names are in the manifest, and a Stage 1 series never counts) and the disk projection for the 66 sessions, with what one re-run and one repeat pair of the pilot add. It is written when PRs 2 to 5 are
// all on `main`, and it runs last but for the seed, after `pin-cli.mjs`.
//   node spikes/lang-explore/prereg/write-manifest.mjs [--out <file>] [--check] [--task-repo <checkout of the task repository at W>]
// The manifest holds exactly PR 4's identity names (`IDENTITY_NAMES`, 41 of them) as the text each reader gave, and `sessionFloorBytes`, which is MEASURED: no flag types it. A name the guard never reads, a name it reads that the manifest lacks and an
// identity that cannot be read are each a refusal naming them.
// Exit 0 written (or checked equal) | 1 `--check` differs | 4 usage | 5 an input is unreadable (the identities, the growth file, the quick run's series).
import { lstatSync, readFileSync, readdirSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS, REPO, exploreRoot } from '../lib/paths.mjs';
import { CANONICAL_SERIES, MAX_ATTEMPTS, STACK_ORDER } from '../lib/series.mjs';
import { IDENTITY_NAMES, liveIdentities } from '../lib/stage-identity.mjs';
import { PREREG_DIR, STAGE_PILOT } from '../lib/stage-lists.mjs';
import { STAGE_READERS } from '../lib/stage-prereg-guard.mjs';
import { STAGE_ROUNDS, pilotPlan } from '../lib/stage-schedule.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';
import { runGenerator } from './generator.mjs';

const DATA_SESSIONS = STAGE_ROUNDS * FREEZE_TASKS.length * STACK_ORDER.length;
/** The pilot's own plan (PR 2's `pilotPlan` over the registered pilot trees): the A/A's twins and the sessions of the two pairs, and the sessions of one pair (a repeat pair adds that many). Never typed. */
const PLAN = pilotPlan({ pilot: STAGE_PILOT }).value;
export const PILOT_SESSIONS = PLAN.aa.sessions.length + PLAN.pairs.reduce((n, pair) => n + pair.sessions.length, 0);
export const REPEAT_PAIR = PLAN.pairs[0].sessions.length;

/** The bytes of every file under `dir`, links counted as themselves and never followed. */
function bytesUnder(dir) {
  return readdirSync(dir, { withFileTypes: true }).reduce((sum, entry) => {
    const full = path.join(dir, entry.name);
    return sum + (entry.isDirectory() ? bytesUnder(full) : lstatSync(full).size);
  }, 0);
}

/** Is the state file of a series that of a quick one-task series (readable, with no `stage` field)? A Stage 1 series, and one that cannot be read, is not. */
function isQuickState(file) {
  try {
    return JSON.parse(readFileSync(file, 'utf8')).stage === undefined;
  } catch {
    return false;
  }
}

/** The names of the quick run's one-task series under `root` (`x-<date>-<4 hex>` with a state that has no `stage` field), sorted: `ok(names)` | `fail`. A Stage 1 series never counts, so the manifest does not change when Stage 1 sessions exist. */
export function closedQuickSeries(root) {
  try {
    return ok(readdirSync(root, { withFileTypes: true }).filter(e => e.isDirectory() && CANONICAL_SERIES.test(e.name) && isQuickState(path.join(root, e.name, 'explore-series.json'))).map(e => e.name).sort());
  } catch (e) {
    return fail(`cannot list the series under ${root}: ${e.code ?? e.message}`);
  }
}

/** The sizes of the kept session folders of one series (`<series>/s/<rid>`). */
const sessionSizes = base => readdirSync(base, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => bytesUnder(path.join(base, e.name)));

/** The size of the largest kept session folder under the quick run's series (`closedQuickSeries`). -> `ok(bytes)` | `fail` when there is none. */
export function largestSessionBytes(root) {
  const series = closedQuickSeries(root);
  if (!series.ok) return series;
  try {
    const sizes = series.value.flatMap(name => sessionSizes(path.join(root, name, 's')));
    return sizes.length === 0 ? fail(`no kept session folder under ${root}: the floor of a session folder's size cannot be measured`) : ok(Math.max(...sizes));
  } catch (e) {
    return fail(`cannot measure the session folders under ${root}: ${e.code ?? e.message}`);
  }
}

/** The disk projection from the floor: the 66 sessions, every one re-run once, one re-run and a repeat pair of the pilot. Pure. */
export function diskProjection({ floorBytes }) {
  const times = sessions => ({ sessions, bytes: sessions * floorBytes });
  return {
    floorBytes, dataSessions: DATA_SESSIONS, pilotSessions: PILOT_SESSIONS, series: times(DATA_SESSIONS + PILOT_SESSIONS),
    withEveryRerun: times((DATA_SESSIONS + PILOT_SESSIONS) * MAX_ATTEMPTS), oneRerun: times(1), repeatPilotPair: times(REPEAT_PAIR),
  };
}

const listed = names => (names.length === 0 ? 'none' : names.join(', '));

/** The sentence for the names of `identities` that are not exactly PR 4's list, or null: a name the guard never reads, and a name it reads that the manifest lacks. */
function namesProblem(identities) {
  const names = Object.keys(identities);
  const [missing, extra] = [IDENTITY_NAMES.filter(name => !names.includes(name)), names.filter(name => !IDENTITY_NAMES.includes(name))];
  return missing.length + extra.length === 0 ? null : `the manifest's identities are not exactly the guard's ${IDENTITY_NAMES.length} names (missing: ${listed(missing)}; not in the guard's list: ${listed(extra)})`;
}

/** The sentence for a floor that is not a positive whole number, or null: a missing or zero size is a refusal. */
const floorProblem = floorBytes => (Number.isSafeInteger(floorBytes) && floorBytes > 0 ? null : `the floor of a session folder's size is not a positive whole number (${floorBytes}): a missing or zero size is a refusal`);

/** The manifest as an object. `identities` is `{ name: text }`, one text for each of PR 4's names; it is written with its keys in order. -> `ok(object)` | `fail`. */
export function buildManifest({ identities, floorBytes, floorSeries = [], growth }) {
  if (identities === null || typeof identities !== 'object') return fail('liveIdentities() gave no identities: the manifest would freeze nothing');
  const problem = namesProblem(identities) ?? floorProblem(floorBytes);
  if (problem) return fail(problem);
  return ok({
    source: 'generated by spikes/lang-explore/prereg/write-manifest.mjs from liveIdentities() and the quick run\'s kept session folders; nothing here is typed',
    identities: sortedKeys(identities), sessionFloorBytes: floorBytes, sessionFloorSeries: floorSeries, growth: { pairWorstCaseBytes: growth.pairWorstCaseBytes }, projection: diskProjection({ floorBytes }),
  });
}

/** `value` with the keys of every object in code-point order, as a new value: the file does not depend on the order an identity was read in. */
function sortedKeys(value) {
  if (value === null || typeof value !== 'object') return value;
  if (Array.isArray(value)) return value.map(sortedKeys);
  return Object.fromEntries(Object.keys(value).sort().map(key => [key, sortedKeys(value[key])]));
}

/** What `liveIdentities()` gave as the text of each identity: `ok({ name: text })`, or a `fail` naming every identity that cannot be read (the manifest never freezes less than the guard compares). */
export function identityTexts(live) {
  const unread = Object.entries(live).filter(([, read]) => read.value === undefined).map(([name, read]) => `${name} (${read.unknown ?? 'no value'})`);
  if (unread.length > 0) return fail(`${unread.length} identity(ies) cannot be read, so the manifest would freeze less than the guard compares: ${unread.join('; ')}`);
  return ok(Object.fromEntries(Object.entries(live).map(([name, read]) => [name, read.value])));
}

/** The growth file as an object: `ok({ pairWorstCaseBytes })` | `fail`. */
function readGrowth(file) {
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')));
  } catch (e) {
    return fail(`${file} cannot be read: ${e.code ?? e.message}`);
  }
}

/** PR 4's `liveIdentities()` over this checkout and the task repository, with PR 5's harness reader. */
const liveOf = values => () => liveIdentities({ repo: REPO, home: os.homedir(), taskRepo: values['task-repo'] ?? process.env.LANG_V2_TASK_REPO, readers: STAGE_READERS });

async function build({ values, seams }) {
  const texts = identityTexts(await (seams.liveIdentities ?? liveOf(values))());
  const root = seams.root ?? exploreRoot(os.homedir());
  const [floor, series] = [largestSessionBytes(root), closedQuickSeries(root)];
  const growth = readGrowth(seams.growthFile ?? path.join(INPUTS, 'growth.json'));
  const unread = [texts, floor, series, growth].find(r => !r.ok);
  if (unread) return unread;
  const built = buildManifest({ identities: texts.value, floorBytes: floor.value, floorSeries: series.value, growth: growth.value });
  return built.ok ? ok(`${JSON.stringify(built.value, null, 2)}\n`) : built;
}

export const main = (argv, say = line => process.stdout.write(`${line}\n`), seams = {}) => runGenerator({ argv, extra: { 'task-repo': { type: 'string' } }, build: values => build({ values, seams }), defaultOut: path.join(REPO, PREREG_DIR, 'manifest.json'), say });

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
