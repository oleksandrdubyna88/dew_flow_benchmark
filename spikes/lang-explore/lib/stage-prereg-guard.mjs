// PR 5's guards, plugged into the seams PR 3 and PR 4 left (F3.S1 (b); stories document section 5, PR 4's point 10): ONE call of v1's guard over Stage 1's lists (`preregGuard({ config: STAGE_PREREG_CONFIG })`), for a paid launch and for
// the series' creation.
//   stagePreregGuard   what `stageGuard` asks of a paid launch (`deps.preregGuard`): the tag on the remote, the registered blobs, the clean tree, the harness identity and the series' own branch, and the seed for a data slot
//   stageInitGuard     what `stage-init` asks of a paid series (`io.stage.initGuardFn`): the same checks, the manifest, the registered prompts equal to what the engine sends, the registered price file and policy, the pilot of
//                      the schedule file; it returns what the series records (`preregTag`, `preregCommit`, `seedSha256`, `manifestSha256`, `pilot`, `harnessIdentity`, `policy`, `branch`)
//   preregDeps         the `deps` of `stageGuard` for a series: PR 5's guard, the harness reader and the harness identities a dated line accepts
// A harness identity other than the series' is accepted ONLY by a dated `instrument | harness:<12 hex> | commit:<40 hex> ...` line of the registered amendments (the series takes that commit's harness folders with `stage-sync --accept-harness`), and only until the first data session is planned: that plan records the accepted identity in the run-map
// (`harnessAtFirstData`) and from then on the guard accepts exactly it, never a line dated later (freeze, never forget): the lines are PR 3's reading
// (`parseStageAmendments`), one reading for every consumer. Every function answers a value (a list of sentences, or `ok` / `fail`); nothing here throws for an expected failure.
import { createHash } from 'node:crypto';
import { git, harnessIdentity, preregGuard, textAt } from '../../minigit-bench/lib/prereg-guard.mjs';
import { hasDataSession } from '../../minigit-bench/lib/run-map.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { quickExplore } from './quick.mjs';
import { seriesInputs } from './series.mjs';
import { parseStageAmendments } from './stage-amendments.mjs';
import { PREREG_DIR, STAGE_HARNESS_LISTS } from './stage-lists.mjs';
import { STAGE_PREREG_CONFIG } from './stage-prereg.mjs';
import { registeredInputs } from './stage-prereg-inputs.mjs';
import { registeredPromptProblems } from './stage-prompts.mjs';

const TAG_PATTERN = 'refs/tags/prereg/lang-v2-*';
const sha256 = text => createHash('sha256').update(text).digest('hex');
const inPrereg = name => `${PREREG_DIR}/${name}`;
const jsonOf = text => { try { return ok(JSON.parse(text)); } catch { return fail('it is not JSON'); } };

/** The Stage 1 harness identity of `ctx.repo`, as PR 4's reader gives it: the tree ids of `STAGE_HARNESS_DIRS` and the top-level files of the three spikes. */
export const stageHarnessReader = ctx => harnessIdentity(ctx.repo, STAGE_HARNESS_LISTS);
export const STAGE_READERS = Object.freeze({ harness: stageHarnessReader });

// ---- the harness a dated line accepts --------------------------------------------------------------------------------------------------------------------------------------------------------------

/**
 * The 12 hex of every harness identity the guard accepts besides the manifest's. Before the first data session is planned: every dated `instrument | harness:<12 hex>` line of `text` names one. From the plan that first plans a data session
 * the acceptance is FROZEN, never forgotten: `map.harnessAtFirstData` (written with that plan) is the one identity accepted, and no later line accepts another (the instrument is frozen with the first data session). A map with a data session that
 * has left `planned` and no record (a series made before the record existed) accepts none.
 */
export function acceptedHarness({ text, map }) {
  if (map?.harnessAtFirstData !== undefined) return [map.harnessAtFirstData];
  return hasDataSession(map ?? {}) ? [] : parseStageAmendments(text).v1.instrument.map(line => line.identity);
}

/** The full identities of `accepted` (12 hex) that `live` is, as `preregGuard` compares them (it holds full identities). */
const fullAccepted = ({ live, accepted }) => (live.ok && accepted.some(hex => live.value.startsWith(hex)) ? [live.value] : []);

// ---- the paid launch -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The problems `preregGuard` names (or its one reason), as a list. */
const problemsOf = answer => (answer.ok ? [] : (answer.problems ?? [answer.reason]));

const NO_TAG = 'the series names no pre-registration tag: a paid series is made by stage-init over the tag on the remote';

/** The run-map `preregGuard` reads for a series: its tag and commit, the harness identity it was made under and the identities a dated line accepts. */
async function runMapOf({ repo, series, inputs, text }) {
  const live = await harnessIdentity(repo, STAGE_HARNESS_LISTS);
  const accepted = fullAccepted({ live, accepted: acceptedHarness({ text, map: series.map }) });
  return { preregTag: series.state.preregTag, preregCommit: series.state.preregCommit, harnessIdentity: inputs.harnessIdentity, harnessAccepted: accepted, inputsAtFirstData: series.map.inputsAtFirstData };
}

/**
 * What `stageGuard` asks of a paid launch: `({ repo, series, text, requireSeed }) -> [sentence]`. `series` is the loaded series (`{ dir, state, map }`) and `text` the registered amendments. A series that names no tag, or whose inputs cannot be
 * read, is refused with a sentence.
 */
export async function stagePreregGuard({ repo, series, text, requireSeed }) {
  const inputs = seriesInputs(series.dir);
  if (!inputs.ok) return [inputs.reason];
  if (!series.state.preregTag) return [NO_TAG];
  const runMap = await runMapOf({ repo, series, inputs: inputs.value, text });
  return [...problemsOf(await preregGuard({ repo, runMap, paid: true, requireSeed, config: STAGE_PREREG_CONFIG })), ...await slicePromptProblems({ repo, inputs: inputs.value })];
}

/** The `deps` of `stageGuard` for `series` (its registered amendments `text` read just now): PR 5's guard, the harness reader and the harness identities a dated line accepts. */
export const preregDeps = ({ series, text }) => ({
  preregGuard: ({ repo, requireSeed }) => stagePreregGuard({ repo, series, text, requireSeed }),
  readers: STAGE_READERS,
  harnessAccepted: acceptedHarness({ text, map: series.map }),
});

// ---- the series' creation --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const TAG_NAME = /^prereg\/lang-v2-[^\s/]+$/;
const tagOfRef = ref => ref.replace(/^refs\/tags\//, '').replace(/\^\{\}$/, '');

const nameTheFlag = tags => (tags.length > 1 ? '; a later series leaves the older tags there: name the one this series is made under with --prereg-tag <name>' : '');
const onlyTag = tags => (tags.length === 1 ? ok(tags[0]) : fail(`origin holds ${tags.length} tags named prereg/lang-v2-* (${tags.join(', ') || 'none'}): a paid series is made under exactly one, pushed by step T${nameTheFlag(tags)}`));

/** The tag `--prereg-tag` names: of the series' name and on the remote. `ok(tag)` | `fail`. */
function namedTag({ tags, wanted }) {
  if (!TAG_NAME.test(wanted)) return fail(`--prereg-tag ${wanted} is not named prereg/lang-v2-<name>`);
  return tags.includes(wanted) ? ok(wanted) : fail(`origin holds no tag named ${wanted} (it holds: ${tags.join(', ') || 'none'})`);
}

/** The tag a series is made under, from the tags the remote holds: the one `wanted` names, or, with no flag, the only one. `ok(tag)` | `fail`. */
const chosenTag = ({ tags, wanted }) => (wanted === undefined ? onlyTag(tags) : namedTag({ tags, wanted }));

/**
 * The pre-registration tag on the remote and the commit it peels to: `ok({ tag, commit })` | `fail`. Without `wanted` a series is made under the ONE tag there is (none, or several, is a refusal naming them and the flag); with `wanted`
 * (`stage-init --prereg-tag <name>`) the named tag, which older series' tags on the remote do not disturb. Whether its commit is in this checkout's history is `ancestryProblem`'s question.
 */
async function remoteTag(repo, wanted) {
  const r = await git(repo, ['ls-remote', '--tags', 'origin', TAG_PATTERN]);
  if (!r.ok) return fail(`cannot read the tags of origin: ${r.reason}`);
  const rows = r.value.split('\n').filter(Boolean).map(line => line.split('\t'));
  const tags = [...new Set(rows.map(([, ref]) => tagOfRef(ref)))].sort();
  const tag = chosenTag({ tags, wanted });
  if (!tag.ok) return tag;
  const own = rows.filter(([, ref]) => tagOfRef(ref) === tag.value);
  return ok({ tag: tag.value, commit: (own.find(([, ref]) => ref.endsWith('^{}')) ?? own[0])[0] });
}

/** The sentence for a tagged commit that is not in the history of the series' branch, or null. */
async function ancestryProblem({ repo, commit }) {
  const r = await git(repo, ['merge-base', '--is-ancestor', commit, 'HEAD']);
  return r.ok ? null : `the tagged commit ${commit.slice(0, 12)} is not in the history of this checkout: cut the series' branch at the tag's commit`;
}

/** What a registered file holds at HEAD: `ok(text)` or a `fail` saying it is absent. */
async function registeredText(repo, name) {
  const text = await textAt(repo, 'HEAD', inPrereg(name));
  return text === null ? fail(`${inPrereg(name)} is not in this checkout: nothing is registered to make the series from`) : ok(text);
}

/** `ok(parsed)` of a registered JSON file, or a `fail` naming the file. */
async function registeredJson(repo, name) {
  const text = await registeredText(repo, name);
  if (!text.ok) return text;
  const parsed = jsonOf(text.value);
  return parsed.ok ? ok({ text: text.value, value: parsed.value }) : fail(`${inPrereg(name)} cannot be read: ${parsed.reason}`);
}

/** The pilot of the schedule file as the series records it (`{ aa: { task, stack }, daemon, web }`): `ok(pilot)` | `fail`. */
function pilotOf(schedule) {
  const pilot = schedule?.pilot;
  if (pilot?.aa === undefined || pilot.daemon === undefined || pilot.web === undefined) return fail(`${inPrereg('schedule.json')} holds no pilot (aa, daemon, web): the pilot's trees are registered`);
  return ok({ aa: { task: pilot.aa.task, stack: pilot.aa.stack }, daemon: pilot.daemon, web: pilot.web });
}

/** The sentences where the registered prompts are not what the engine sends (`registeredPromptProblems`), over the quick profile of `base`. */
async function promptProblems({ repo, base, home }) {
  const prompts = await registeredJson(repo, 'prompts-lang.json');
  return prompts.ok ? registeredPromptProblems({ registered: prompts.value.value, explore: quickExplore(base, home) }) : [prompts.reason];
}

/** The sentence for a stack of a task whose slice (what the unit sends) holds another text than the registered one, or null. */
const sliceTextProblem = ({ inputs, task, stack, text }) => (inputs.tasks?.[task]?.prompts?.prompts?.[stack]?.text === text ? null : `the prompt frozen into task ${task}'s slice for ${stack} is not the registered text of prompts-lang.json: a unit sends what its slice holds`);

/** The sentences where the slices of the series hold other texts than the registered `prompts-lang.json` (read at HEAD): a unit sends its slice's prompt (`seriesPrompts`), so the registered text is what was sent only when they are equal. */
async function slicePromptProblems({ repo, inputs }) {
  const registered = await registeredJson(repo, 'prompts-lang.json');
  if (!registered.ok) return [registered.reason];
  const asked = Object.entries(registered.value.value.tasks ?? {}).flatMap(([task, stacks]) => Object.entries(stacks).map(([stack, p]) => ({ inputs, task, stack, text: p.text })));
  return asked.map(sliceTextProblem).filter(Boolean);
}

/** The files the series is made from: `ok({ manifest, schedule, inputs, seed, pilot })` or a `fail` naming each that cannot be read. */
async function filesOf(repo) {
  const [manifest, schedule, seed, prompts] = await Promise.all([registeredJson(repo, 'manifest.json'), registeredJson(repo, 'schedule.json'), registeredText(repo, 'seed.sha256'), registeredJson(repo, 'prompts-lang.json')]);
  const inputs = registeredInputs(repo);
  const pilot = schedule.ok ? pilotOf(schedule.value.value) : schedule;
  const problems = [manifest, schedule, seed, prompts, inputs, pilot].filter(r => !r.ok).map(r => r.reason);
  return problems.length > 0 ? fail(problems.join('; '), { problems }) : ok({ manifest: manifest.value, inputs: inputs.value, seed: seed.value, prompts: prompts.value, pilot: pilot.value });
}

const relative = (repo, file) => file.slice(repo.length + 1);

/** What the series records of the pre-registration: the tag, the commit, the seed's hash, the manifest's sha256, the pilot, the manifest's harness identity and the repository-relative price file and policy. */
const recordOf = ({ tag, files, repo }) => ({
  preregTag: tag.tag, preregCommit: tag.commit, seedSha256: files.seed.trim(), manifestSha256: sha256(files.manifest.text), pilot: files.pilot,
  harnessIdentity: files.manifest.value.identities?.harness, policy: { prices: relative(repo, files.inputs.prices), policy: relative(repo, files.inputs.policy) }, registeredPrompts: files.prompts.value,
});

/** The local branch HEAD is on, which a paid series records as its own (`stage-sync` moves only that one): `ok(name)` | `fail` for a detached head. */
async function currentBranch(repo) {
  const r = await git(repo, ['rev-parse', '--abbrev-ref', 'HEAD']);
  if (!r.ok) return r;
  return r.value.trim() === 'HEAD' ? fail('stage-init makes a paid series on the series\' own local branch, not on a detached head: the branch is the one stage-sync moves') : ok(r.value.trim());
}

/** The record of a paid series: `recordOf` and the branch it is made on. */
async function recorded({ tag, files, repo }) {
  const branch = await currentBranch(repo);
  return branch.ok ? ok({ ...recordOf({ tag, files, repo }), branch: branch.value }) : branch;
}

/** The sentences of the paid series' checks that follow the tag: v1's guard over Stage 1's lists, the registered prompts and the tagged commit's place in the history. */
async function seriesProblems({ repo, base, home, text, tag, files }) {
  const runMap = await runMapOf({ repo, series: { state: { preregTag: tag.tag, preregCommit: tag.commit }, map: {} }, inputs: { harnessIdentity: files.manifest.value.identities?.harness }, text });
  const guard = problemsOf(await preregGuard({ repo, runMap, paid: true, requireSeed: false, config: STAGE_PREREG_CONFIG }));
  return [...guard, ...await promptProblems({ repo, base, home }), ...[await ancestryProblem({ repo, commit: tag.commit })].filter(Boolean)];
}

/**
 * What `stage-init` asks of a paid series (its `initGuardFn`): `({ repo, base, home, text, preregTag }) -> ok(pre) | fail(reason, { problems })`. `base` is `explore.json` as loaded, `text` the registered amendments, `preregTag` the optional `--prereg-tag`. A rehearsal is free and has
 * no pre-registration: `stage-init` answers it itself, so this function is called for a paid series only.
 */
export async function stageInitGuard({ repo, base, home, text, preregTag }) {
  const tag = await remoteTag(repo, preregTag);
  if (!tag.ok) return tag;
  const files = await filesOf(repo);
  if (!files.ok) return files;
  const problems = await seriesProblems({ repo, base, home, text, tag: tag.value, files: files.value });
  if (problems.length > 0) return fail(`${problems.length} problem(s) before a paid series: ${problems.join('; ')}`, { problems });
  return recorded({ tag: tag.value, files: files.value, repo });
}
