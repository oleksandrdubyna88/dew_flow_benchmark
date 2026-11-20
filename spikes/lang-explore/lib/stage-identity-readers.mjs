// The places the identities of Stage 1's pre-registration come from, one reader each (F3.S1 (a), PR 4; stories document section 5, brief item 1, `liveIdentities()`). A reader gets the context `{ repo, home, explore, record, taskRepo, readers }` and
// answers a text (a hash, or a short fact such as `512`); one that cannot answer THROWS, and `liveIdentities` turns the throw into "cannot be read: why" for that identity alone (a unit of its own). The order is the order the guard names them.
//   harness            the Stage 1 harness identity (PR 5's list of folders, `STAGE_HARNESS_DIRS`): no default; PR 5 hands its reader in as `readers.harness`
//   composite values   (`heldout.NN`, `memoryValue`, `policy`, `locale`, `seriesDefaults`, `toolchains`) are the sha256 of a stated text, so a difference shows as two short hashes whatever it was in
//   amendable inputs   are never an identity: the caps' wall and stall, `quiet.json`, `package-flags.json` and the policy's free fields (`t3`, `t5`, `POLICY_FREE_FIELDS`) change by a dated line
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { runnerPinNow } from './harness-pin.mjs';
import { templatesIdentity, contractIdentity } from './contract-identity.mjs';
import { ALLOW_LIST, BASE_ENV } from '../../lang-bench/lib/exec-layer/env.mjs';
import { LAYER_DIR, computeIdentity } from '../../lang-bench/lib/exec-layer/identity.mjs';
import { POLICY_FILE } from '../../lang-bench/lib/exec-layer/policy.mjs';
import { FORMAT_PATH } from '../../lang-bench/lib/freeze-git.mjs';
import { blobAtTag } from '../../lang-bench/lib/spec-freeze.mjs';
import { treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { underHome } from './paths.mjs';
import { quickSnapshots } from './quick.mjs';
import { registeredEntry } from './heldout-registry.mjs';
import { PREREG_DIR, RECORD_PATHS } from './stage-record.mjs';
import { resolveTask, taskPaths } from './tasks.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';
import { sha256OfFile } from './workspaces-tree.mjs';

/** The policy's free fields: the A/A may set them by a dated line (parent, `:1562`, `:1611`, `:1510`), so they are not part of the policy's identity. PR 5's `STAGE_PREREG` list of free fields is this list. */
export const POLICY_FREE_FIELDS = Object.freeze(['t3', 't5']);

const sha256Text = text => createHash('sha256').update(text).digest('hex');
const sorted = value => (Array.isArray(value) ? value.map(sorted) : value !== null && typeof value === 'object' ? Object.fromEntries(Object.keys(value).sort().map(k => [k, sorted(value[k])])) : value);
/** The text of a value with its keys in code-point order at every depth: the same value is always the same text. */
const canonical = value => JSON.stringify(sorted(value));
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const inRepo = (ctx, rel) => path.join(ctx.repo, rel);
const preregFile = (ctx, name) => path.join(ctx.repo, PREREG_DIR, name);

function need(value, what) {
  if (value === undefined || value === null) throw new Error(what);
  return value;
}

const taskOf = (ctx, id) => {
  const task = resolveTask(need(ctx.explore, 'explore.json cannot be loaded'), id);
  if (!task.ok) throw new Error(task.reason);
  return task.value;
};

/** The sha256 of the blob of `file` at the freeze tag of the record, in the task repository. */
async function blobSha(ctx, file) {
  const repo = need(ctx.taskRepo, 'no task repository was given (--task-repo or LANG_V2_TASK_REPO)');
  const blob = await blobAtTag({ repo, tag: need(ctx.record?.specStates?.freeze?.tag, 'the frozen record names no freeze tag'), file });
  if (!blob.ok) throw new Error(blob.reason);
  return sha256Text(blob.value);
}

const specPath = (ctx, id) => need(ctx.record?.specStates?.specs?.[id]?.path, `the frozen record has no spec row for task ${id}`);

function heldoutText(ctx, id) {
  const task = taskOf(ctx, id);
  const entry = registeredEntry({ frozenFile: inRepo(ctx, RECORD_PATHS.heldoutFrozen), task });
  if (!entry.ok) throw new Error(entry.reason);
  const archive = path.join(taskPaths(task, ctx.home).heldoutDir, 'suite.enc');
  const e = need(entry.value, `task ${id} has no registered entry`);
  return `archive:${sha256OfFile(archive)}|tree:${e.treeSha256}|manifest:${e.manifestSha256}|cases:${e.caseCount}|traps:${e.trapCount}`;
}

/** The memory limit's bytes of the record file. A missing value is no identity. */
const memoryBytes = limit => need(limit.bytes, 'memory-limit.json holds no bytes');

function memoryText(ctx) {
  const limit = readJson(inRepo(ctx, RECORD_PATHS.memoryLimit));
  const refs = need(limit.registration?.references, 'the memory value has no registration');
  return `bytes:${memoryBytes(limit)}|date:${limit.registration.date}|refs:${sha256Text(canonical(refs))}`;
}

function pidsMax() {
  const found = /echo (\d+) > "\$cg\/prog\/pids\.max"/.exec(readFileSync(path.join(LAYER_DIR, 'scope-entry.sh'), 'utf8'));
  return need(found?.[1], 'scope-entry.sh sets no pids.max');
}

function execLayerTree() {
  const identity = computeIdentity(LAYER_DIR);
  if (!identity.ok) throw new Error(identity.reason);
  return identity.value.execLayer.treeSha256;
}

function toolchains(ctx) {
  const stacks = readJson(inRepo(ctx, 'spikes/lang-bench/inputs/stacks-lang.json'));
  return sha256Text(canonical({ pins: stacks.pins, directories: stacks.directories, treeSha256: stacks.treeSha256 }));
}

function snapshotHash(ctx, stack) {
  const snapshots = quickSnapshots({ base: need(ctx.explore, 'explore.json cannot be loaded'), home: ctx.home, recordFile: inRepo(ctx, 'spikes/lang-bench/inputs/cache-snapshots-lang.json') });
  if (!snapshots.ok) throw new Error(snapshots.reason);
  return treeHash(snapshots.value[stack].dir);
}

function pricesHash(ctx) {
  const dir = path.join(ctx.repo, PREREG_DIR);
  const found = existsSync(dir) ? readdirSync(dir).filter(name => /^prices-.*\.json$/.test(name)) : [];
  if (found.length !== 1) throw new Error(`${dir} holds ${found.length} price files (prices-<date>.json): exactly one is registered`);
  return sha256OfFile(path.join(dir, found[0]));
}

function policyHash(ctx) {
  const policy = readJson(preregFile(ctx, 'crosscheck-policy.json'));
  return sha256Text(canonical(Object.fromEntries(Object.entries(policy).filter(([key]) => !POLICY_FREE_FIELDS.includes(key)))));
}

function seriesDefaults(ctx) {
  const { quick, tasks } = need(ctx.explore, 'explore.json cannot be loaded');
  const hashed = { limits: need(quick.limits, 'explore.json holds no quick.limits'), caseWallMs: need(quick.caseWallMs, 'explore.json holds no quick.caseWallMs'), runCeilingMs: need(quick.runCeilingMs, 'explore.json holds no quick.runCeilingMs'), memory: memoryBytes(readJson(inRepo(ctx, RECORD_PATHS.memoryLimit))) };
  return sha256Text(canonical({ ...hashed, overrides: Object.fromEntries(FREEZE_TASKS.map(id => [id, tasks[id]?.limits ?? null])) }));
}

const hostText = () => sha256Text([os.hostname(), os.release(), existsSync('/sys/fs/cgroup/cgroup.controllers') ? 'cgroup2' : 'cgroup1'].join('|'));

const perTask = (prefix, read) => FREEZE_TASKS.map(id => [`${prefix}.${id}`, ctx => read(ctx, id)]);

/** `[[name, reader]]`: every identity of the pre-registration, in the order the guard names them. */
export const READERS = Object.freeze([
  ['harness', () => { throw new Error('the Stage 1 harness identity is PR 5\'s list of folders (STAGE_HARNESS_DIRS): no reader was given'); }],
  ['runnerManifest', ctx => need(runnerPinNow(inRepo(ctx, 'spikes/lang-bench/inputs/specs.d/runner.json')).manifestSha256, 'runner.json holds no manifestSha256: the runner pin is not complete')],
  ['execLayerTree', () => execLayerTree()],
  ['memoryPolicy', () => sha256OfFile(POLICY_FILE)],
  ['pidsMax', () => pidsMax()],
  ['cliSha256', ctx => sha256OfFile(underHome(need(ctx.explore, 'explore.json cannot be loaded').cli.pathParts, ctx.home))],
  ['toolchains', ctx => toolchains(ctx)],
  ['snapshot.csharp', ctx => snapshotHash(ctx, 'csharp')],
  ['snapshot.node', ctx => snapshotHash(ctx, 'node')],
  ['formatSha256', ctx => blobSha(ctx, FORMAT_PATH)],
  ...perTask('spec', (ctx, id) => blobSha(ctx, specPath(ctx, id))),
  ...perTask('heldout', (ctx, id) => sha256Text(heldoutText(ctx, id))),
  ['memoryValue', ctx => sha256Text(memoryText(ctx))],
  ['contract', ctx => contractIdentity({ root: ctx.repo })],
  ['templates', ctx => templatesIdentity({ root: ctx.repo })],
  ['prompts', ctx => sha256OfFile(preregFile(ctx, 'prompts-lang.json'))],
  ['statement', ctx => sha256OfFile(preregFile(ctx, 'statement.md'))],
  ['prices', ctx => pricesHash(ctx)],
  ['policy', ctx => policyHash(ctx)],
  ['caps', ctx => `capUsd:${need(need(ctx.explore, 'explore.json cannot be loaded').caps?.capUsd, 'explore.json holds no caps.capUsd')}`],
  ['locale', ctx => sha256Text(canonical({ locale: need(ctx.explore, 'explore.json cannot be loaded').locale, base: BASE_ENV, allowList: ALLOW_LIST }))],
  ['seriesDefaults', ctx => seriesDefaults(ctx)],
  ...perTask('trapAid', (ctx, id) => sha256OfFile(taskPaths(taskOf(ctx, id), ctx.home).notesFile)),
  ['pilotRules', ctx => sha256OfFile(preregFile(ctx, 'pilot-rules.json'))],
  ['adjudicationRules', ctx => sha256OfFile(preregFile(ctx, 'adjudication-rules.json'))],
  ['host', () => hostText()],
]);
