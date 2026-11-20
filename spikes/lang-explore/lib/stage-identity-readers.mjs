// The places the identities of Stage 1's pre-registration come from, one reader each (F3.S1 (a), PR 4; stories document section 5, brief item 1, `liveIdentities()`). A reader gets the context `{ repo, home, explore, record, taskRepo, readers }` and
// answers `ok(text)` (a hash, or a short fact such as `512`); one that cannot answer gives `fail(why)`, a VALUE, and `liveIdentities` passes it on as "cannot be read: why" for that identity alone (a unit of its own). Nothing here throws for an expected
// failure: a missing field is `need`, and the one boundary where a system exception (a missing file, JSON that does not parse) becomes a value is `guarded`. The order is the order the guard names them.
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
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
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
export const isResult = value => typeof value === 'object' && value !== null && typeof value.ok === 'boolean';

/** `ok(value)`, or `fail(what)` for a value that is missing. */
const need = (value, what) => (value === undefined || value === null ? fail(what) : ok(value));

/** What `fn` gives, as a value: a result stays as it is, any other answer is `ok(answer)`, and a system exception (a missing file, JSON that does not parse) is `fail(its message)`. The one boundary of this module. */
function guarded(fn) {
  try {
    const answer = fn();
    return isResult(answer) ? answer : ok(answer);
  } catch (e) {
    return fail(e.message);
  }
}

const next = (acc, step) => (acc.ok ? step(acc.value) : acc);

/** `start` (a result, or a promise of one) threaded through `steps`: each takes the value of the one before and gives a result (or a promise of one); the first `fail` ends it. */
const pipe = (start, ...steps) => steps.reduce((acc, step) => (acc instanceof Promise ? acc.then(r => next(r, step)) : next(acc, step)), start);

/** `ok([values])` when every result is ok, else the first `fail`. */
const all = results => results.find(r => !r.ok) ?? ok(results.map(r => r.value));

const readJson = file => guarded(() => JSON.parse(readFileSync(file, 'utf8')));
const hashOfFile = file => guarded(() => sha256OfFile(file));
const inRepo = (ctx, rel) => path.join(ctx.repo, rel);
const preregFile = (ctx, name) => path.join(ctx.repo, PREREG_DIR, name);
const exploreOf = ctx => need(ctx.explore, 'explore.json cannot be loaded');
const taskOf = (ctx, id) => pipe(exploreOf(ctx), explore => resolveTask(explore, id));

/** The sha256 of the blob of `file` at the freeze tag of the record, in the task repository. */
const blobSha = (ctx, file) => pipe(all([need(ctx.taskRepo, 'no task repository was given (--task-repo or LANG_V2_TASK_REPO)'), need(ctx.record?.specStates?.freeze?.tag, 'the frozen record names no freeze tag')]),
  ([repo, tag]) => blobAtTag({ repo, tag, file }), blob => ok(sha256Text(blob)));

const specPath = (ctx, id) => need(ctx.record?.specStates?.specs?.[id]?.path, `the frozen record has no spec row for task ${id}`);

const heldoutText = (ctx, id) => pipe(taskOf(ctx, id),
  task => pipe(guarded(() => registeredEntry({ frozenFile: inRepo(ctx, RECORD_PATHS.heldoutFrozen), task })), entry => need(entry, `task ${id} has no registered entry`),
    e => pipe(hashOfFile(path.join(taskPaths(task, ctx.home).heldoutDir, 'suite.enc')), archive => ok(`archive:${archive}|tree:${e.treeSha256}|manifest:${e.manifestSha256}|cases:${e.caseCount}|traps:${e.trapCount}`))));

/** The memory limit's bytes of the record file. A missing value is no identity. */
const memoryBytes = limit => need(limit.bytes, 'memory-limit.json holds no bytes');

const memoryText = ctx => pipe(readJson(inRepo(ctx, RECORD_PATHS.memoryLimit)),
  limit => pipe(all([need(limit.registration?.references, 'the memory value has no registration'), memoryBytes(limit)]), ([refs, bytes]) => ok(`bytes:${bytes}|date:${limit.registration.date}|refs:${sha256Text(canonical(refs))}`)));

const pidsMax = () => pipe(guarded(() => readFileSync(path.join(LAYER_DIR, 'scope-entry.sh'), 'utf8')), text => need(/echo (\d+) > "\$cg\/prog\/pids\.max"/.exec(text)?.[1], 'scope-entry.sh sets no pids.max'));

const execLayerTree = () => pipe(guarded(() => computeIdentity(LAYER_DIR)), identity => ok(identity.execLayer.treeSha256));

const toolchains = ctx => pipe(readJson(inRepo(ctx, 'spikes/lang-bench/inputs/stacks-lang.json')), stacks => ok(sha256Text(canonical({ pins: stacks.pins, directories: stacks.directories, treeSha256: stacks.treeSha256 }))));

const snapshotHash = (ctx, stack) => pipe(exploreOf(ctx),
  base => guarded(() => quickSnapshots({ base, home: ctx.home, recordFile: inRepo(ctx, 'spikes/lang-bench/inputs/cache-snapshots-lang.json') })),
  snapshots => guarded(() => treeHash(snapshots[stack].dir)));

const priceFiles = dir => guarded(() => (existsSync(dir) ? readdirSync(dir).filter(name => /^prices-.*\.json$/.test(name)) : []));

const pricesHash = ctx => pipe(priceFiles(path.join(ctx.repo, PREREG_DIR)),
  found => (found.length === 1 ? hashOfFile(path.join(ctx.repo, PREREG_DIR, found[0])) : fail(`${path.join(ctx.repo, PREREG_DIR)} holds ${found.length} price files (prices-<date>.json): exactly one is registered`)));

const policyHash = ctx => pipe(readJson(preregFile(ctx, 'crosscheck-policy.json')),
  policy => ok(sha256Text(canonical(Object.fromEntries(Object.entries(policy).filter(([key]) => !POLICY_FREE_FIELDS.includes(key)))))));

const seriesDefaults = ctx => pipe(exploreOf(ctx), explore => need(explore.quick, 'explore.json holds no quick profile'),
  quick => pipe(all([need(quick.limits, 'explore.json holds no quick.limits'), need(quick.caseWallMs, 'explore.json holds no quick.caseWallMs'), need(quick.runCeilingMs, 'explore.json holds no quick.runCeilingMs'), pipe(readJson(inRepo(ctx, RECORD_PATHS.memoryLimit)), memoryBytes)]),
    ([limits, caseWallMs, runCeilingMs, memory]) => ok(sha256Text(canonical({ limits, caseWallMs, runCeilingMs, memory, overrides: Object.fromEntries(FREEZE_TASKS.map(id => [id, ctx.explore.tasks?.[id]?.limits ?? null])) })))));

const hostText = () => ok(sha256Text([os.hostname(), os.release(), existsSync('/sys/fs/cgroup/cgroup.controllers') ? 'cgroup2' : 'cgroup1'].join('|')));

const perTask = (prefix, read) => FREEZE_TASKS.map(id => [`${prefix}.${id}`, ctx => read(ctx, id)]);

/** `[[name, reader]]`: every identity of the pre-registration, in the order the guard names them. A reader answers `ok(text)` or `fail(why)`; the `harness` reader has no default. */
export const READERS = Object.freeze([
  ['harness', () => fail('the Stage 1 harness identity is PR 5\'s list of folders (STAGE_HARNESS_DIRS): no reader was given')],
  ['runnerManifest', ctx => pipe(guarded(() => runnerPinNow(inRepo(ctx, 'spikes/lang-bench/inputs/specs.d/runner.json'))), pin => need(pin.manifestSha256, 'runner.json holds no manifestSha256: the runner pin is not complete'))],
  ['execLayerTree', () => execLayerTree()],
  ['memoryPolicy', () => hashOfFile(POLICY_FILE)],
  ['pidsMax', () => pidsMax()],
  ['cliSha256', ctx => pipe(exploreOf(ctx), explore => hashOfFile(underHome(explore.cli.pathParts, ctx.home)))],
  ['toolchains', ctx => toolchains(ctx)],
  ['snapshot.csharp', ctx => snapshotHash(ctx, 'csharp')],
  ['snapshot.node', ctx => snapshotHash(ctx, 'node')],
  ['formatSha256', ctx => blobSha(ctx, FORMAT_PATH)],
  ...perTask('spec', (ctx, id) => pipe(specPath(ctx, id), file => blobSha(ctx, file))),
  ...perTask('heldout', (ctx, id) => pipe(heldoutText(ctx, id), text => ok(sha256Text(text)))),
  ['memoryValue', ctx => pipe(memoryText(ctx), text => ok(sha256Text(text)))],
  ['contract', ctx => guarded(() => contractIdentity({ root: ctx.repo }))],
  ['templates', ctx => guarded(() => templatesIdentity({ root: ctx.repo }))],
  ['prompts', ctx => hashOfFile(preregFile(ctx, 'prompts-lang.json'))],
  ['statement', ctx => hashOfFile(preregFile(ctx, 'statement.md'))],
  ['prices', ctx => pricesHash(ctx)],
  ['policy', ctx => policyHash(ctx)],
  ['caps', ctx => pipe(exploreOf(ctx), explore => need(explore.caps?.capUsd, 'explore.json holds no caps.capUsd'), capUsd => ok(`capUsd:${capUsd}`))],
  ['locale', ctx => pipe(exploreOf(ctx), explore => ok(sha256Text(canonical({ locale: explore.locale, base: BASE_ENV, allowList: ALLOW_LIST }))))],
  ['seriesDefaults', ctx => seriesDefaults(ctx)],
  ...perTask('trapAid', (ctx, id) => pipe(taskOf(ctx, id), task => hashOfFile(taskPaths(task, ctx.home).notesFile))),
  ['pilotRules', ctx => hashOfFile(preregFile(ctx, 'pilot-rules.json'))],
  ['adjudicationRules', ctx => hashOfFile(preregFile(ctx, 'adjudication-rules.json'))],
  ['host', () => hostText()],
]);
