// F2.S2 (b): the task repository's rulesets as data, the pure compare of a read-back against them, the reads through the owner's `gh`, and the apply that only ever creates.
//
// The owner approved two things on the task repository: `main`'s protection and the tag ruleset. GitHub holds them as THREE rulesets, because a bypass applies to every rule of its ruleset: the tag ruleset is
// `spec-frozen-tags` (update, deletion, force push: no bypass) and `spec-frozen-tags-create` (creation: the admin role may bypass). Every `gh` call goes through the `runBounded` seam (exe and argv, a timeout, no
// shell, no token in the child's environment: the owner's own `gh` login does the write); a test hands it a fake. Nothing here updates or deletes a ruleset: that needs its own go of the owner.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

export const TASK_REPO = 'oleksandrdubyna88/dew_flow_ai_language_comparison';
/** The repository admin role, per the integrations provider docs: GitHub's own REST text gives no mapping. */
export const ADMIN_ROLE_ACTOR_ID = 5;

/** `value` and everything inside it, frozen. */
function freeze(value) {
  if (value !== null && typeof value === 'object' && !Object.isFrozen(value)) {
    for (const inner of Object.values(value)) freeze(inner);
    Object.freeze(value);
  }
  return value;
}
const TAG_CONDITIONS = { ref_name: { include: ['refs/tags/spec-frozen-*'], exclude: [] } };

/** The rulesets exactly as they are created: the body of each POST is one of these, nothing else. */
export const RULESETS = freeze([
  {
    name: 'template-frozen',
    target: 'branch',
    enforcement: 'active',
    conditions: { ref_name: { include: ['refs/heads/main'], exclude: [] } },
    bypass_actors: [],
    rules: [
      { type: 'pull_request', parameters: { required_approving_review_count: 0, dismiss_stale_reviews_on_push: false, require_code_owner_review: false, require_last_push_approval: false, required_review_thread_resolution: true } },
      { type: 'required_linear_history' },
      { type: 'non_fast_forward' },
      { type: 'deletion' },
    ],
  },
  { name: 'spec-frozen-tags', target: 'tag', enforcement: 'active', conditions: structuredClone(TAG_CONDITIONS), bypass_actors: [], rules: [{ type: 'update' }, { type: 'deletion' }, { type: 'non_fast_forward' }] },
  {
    name: 'spec-frozen-tags-create',
    target: 'tag',
    enforcement: 'active',
    conditions: structuredClone(TAG_CONDITIONS),
    bypass_actors: [{ actor_id: ADMIN_ROLE_ACTOR_ID, actor_type: 'RepositoryRole', bypass_mode: 'always' }],
    rules: [{ type: 'creation' }],
  },
]);

/** The repository's merge settings, reported and never enforced (a repository setting is not a ruleset). */
export const MERGE_SETTINGS = freeze({ allow_merge_commit: false, allow_squash_merge: true });

const TOKENS = [/gh[pousr]_[A-Za-z0-9]{16,}/g, /github_pat_[A-Za-z0-9_]{16,}/g, /Bearer\s+[A-Za-z0-9._~+/=-]{16,}/g];
const ENV_TOKENS = ['GH_TOKEN', 'GITHUB_TOKEN'];
const REMOVED = '<token removed>';

/** `text` without any token of git's or GitHub's words, and without the value of an environment token this process holds. Every reason this library returns went through it. */
export function scrub(text) {
  let out = String(text);
  for (const name of ENV_TOKENS) {
    const value = process.env[name];
    if (typeof value === 'string' && value.length >= 8) out = out.split(value).join(REMOVED);
  }
  for (const pattern of TOKENS) out = out.replace(pattern, REMOVED);
  return out;
}

// ---- the pure compare -------------------------------------------------------------------------------------------------------------------

const DEFAULT_BRANCH_REF = 'refs/heads/main';
const literalPrefix = ref => ref.split('*')[0];

/** Whether the ref pattern `pattern` takes in `ref` (itself a ref or a pattern). A prefix reading of GitHub's globs: it errs toward "takes in", which only changes a report's wording and which rulesets are named as strangers. */
function covers(pattern, ref) {
  if (pattern === ref || pattern === '~ALL') return true;
  if (pattern === '~DEFAULT_BRANCH') return covers(DEFAULT_BRANCH_REF, ref) || covers(ref, DEFAULT_BRANCH_REF);
  return pattern.includes('*') && literalPrefix(ref).startsWith(literalPrefix(pattern));
}
const overlaps = (a, b) => covers(a, b) || covers(b, a);

const show = value => (value === undefined ? 'absent' : JSON.stringify(value));
const sameValue = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const refs = (ruleset, which) => ruleset?.conditions?.ref_name?.[which] ?? [];

/** The include refs of a read-back against the data. A read ref is `extra` when it is not one of the data's and it takes in a data ref or sits beside a data ref that is covered; a data ref no read ref takes in is `missing`. A ruleset pointed
 * wholly elsewhere is therefore reported by what it lacks, and one that was widened by what it adds. */
function includeProblems(read, expected) {
  const want = refs(expected, 'include');
  const have = refs(read, 'include');
  const covered = ref => have.some(candidate => covers(candidate, ref));
  const anyCovered = want.some(covered);
  const missing = want.filter(ref => !covered(ref)).map(ref => `missing include ref: ${ref}`);
  const extra = have.filter(ref => !want.includes(ref) && (anyCovered || want.some(wanted => covers(ref, wanted)))).map(ref => `extra include ref: ${ref}`);
  return [...missing, ...extra];
}

function excludeProblems(read, expected) {
  const want = refs(expected, 'exclude');
  const have = refs(read, 'exclude');
  return [...want.filter(ref => !have.includes(ref)).map(ref => `missing exclude ref: ${ref}`), ...have.filter(ref => !want.includes(ref)).map(ref => `extra exclude ref: ${ref}`)];
}

const countTypes = rules => rules.reduce((counts, rule) => counts.set(rule.type, (counts.get(rule.type) ?? 0) + 1), new Map());

/** A rule the data does not state is `extra`, one it states and the read-back lacks is `missing`; a parameter is compared only where the data states it. */
function ruleProblems(read, expected) {
  const have = countTypes(read.rules ?? []);
  const want = countTypes(expected.rules);
  const missing = [...want.keys()].filter(type => !have.has(type)).map(type => `missing rule: ${type}`);
  const extra = [...have].flatMap(([type, count]) => Array.from({ length: Math.max(0, count - (want.get(type) ?? 0)) }, () => `extra rule: ${type}`));
  const parameters = expected.rules.flatMap(rule => {
    const found = (read.rules ?? []).find(candidate => candidate.type === rule.type);
    return found === undefined ? [] : Object.entries(rule.parameters ?? {}).filter(([key, value]) => !sameValue(found.parameters?.[key], value)).map(([key, value]) => `rule ${rule.type} parameter ${key} is ${show(found.parameters?.[key])}, not ${show(value)}`);
  });
  return [...missing, ...extra, ...parameters];
}

const actorKey = actor => `${actor.actor_type} ${actor.actor_id}`;
const actorText = actor => `${actorKey(actor)} ${actor.bypass_mode}`;

/** An actor whose type and id the data does not name is `missing` when the data names it and `extra` when only the read-back does; one with the right type and id and another mode is reported by what GitHub holds (`extra`). */
function bypassProblems(read, expected) {
  const have = read.bypass_actors ?? [];
  const want = expected.bypass_actors;
  const missing = want.filter(actor => !have.some(found => actorKey(found) === actorKey(actor))).map(actor => `missing bypass actor: ${actorText(actor)}`);
  const extra = have.filter(found => !want.some(actor => actorText(actor) === actorText(found))).map(found => `extra bypass actor: ${actorText(found)}`);
  return [...missing, ...extra];
}

/** What differs between a ruleset as GitHub returns it and the data, in sentences (`[]` is equal). What GitHub adds (ids, dates, links, a parameter the data does not state) is not compared. `null` (not read) is not equal. */
export function rulesetProblems(read, expected) {
  if (read === null || typeof read !== 'object') return ['absent: nothing was read'];
  const same = field => (read[field] === expected[field] ? [] : [`${field} is ${show(read[field])}, not ${show(expected[field])}`]);
  return [...same('target'), ...same('enforcement'), ...includeProblems(read, expected), ...excludeProblems(read, expected), ...ruleProblems(read, expected), ...bypassProblems(read, expected)];
}

/** What differs in the repository's merge settings, in sentences. Only the keys of the data are compared. */
export function mergeSettingsProblems(read) {
  return Object.entries(MERGE_SETTINGS).filter(([key, value]) => read?.[key] !== value).map(([key, value]) => `merge setting ${key} is ${show(read?.[key])}, not ${show(value)}`);
}

// ---- the calls through gh ---------------------------------------------------------------------------------------------------------------

const ACCEPT = 'Accept: application/vnd.github+json';
const TIMEOUT_MS = 60_000;
const PAGE = 100;
const REPO_NAME = /^[A-Za-z0-9][A-Za-z0-9_.-]*\/(?!\.{1,2}$)[A-Za-z0-9_.-]+$/;
/** The variables the child may inherit: where to find `gh`'s own login and its settings. Never a token, a key or a password. */
const KEPT_ENV = ['PATH', 'HOME', 'USERPROFILE', 'APPDATA', 'LOCALAPPDATA', 'SystemRoot', 'TMPDIR', 'TEMP', 'TMP', 'XDG_CONFIG_HOME', 'XDG_DATA_HOME', 'XDG_STATE_HOME', 'XDG_CACHE_HOME', 'GH_CONFIG_DIR', 'GH_HOST', 'LANG', 'LC_ALL', 'HTTPS_PROXY', 'HTTP_PROXY', 'NO_PROXY'];

const ghEnv = () => ({ ...Object.fromEntries(KEPT_ENV.filter(name => process.env[name] !== undefined).map(name => [name, process.env[name]])), GH_PROMPT_DISABLED: '1' });
const envFail = reason => fail(scrub(reason), { kind: 'env' });
const sentence = items => (items.length === 0 ? 'none' : items.join(', '));

/** One `gh api` call: -> `ok(parsed JSON)` | `fail(reason, { kind: 'env' })`. A runner that throws, a launch failure, a timeout, a truncated or non-JSON answer and a non-zero exit are all a failure to read, never an empty answer. */
async function ghApi(run, args, input) {
  let result;
  try {
    result = await run('gh', ['api', ...args], { env: ghEnv(), timeoutMs: TIMEOUT_MS, ...(input === undefined ? {} : { input }) });
  } catch (error) {
    return envFail(`gh could not be run: ${error?.message ?? error}`);
  }
  if (!result.ok) return envFail(result.reason);
  const { code, stdout, stderr, timedOut, truncated } = result.value;
  if (timedOut) return envFail(`gh did not answer within ${TIMEOUT_MS / 1000} seconds`);
  if (code !== 0) return envFail(`gh exited with code ${code}: ${String(stderr).trim().slice(0, 300)}`);
  if (truncated) return envFail('gh printed more than the reader keeps');
  try {
    return ok(JSON.parse(stdout));
  } catch {
    return envFail('gh did not print JSON');
  }
}

const getJson = (run, path) => ghApi(run, ['-H', ACCEPT, path]);
const postJson = (run, path, body) => ghApi(run, ['--method', 'POST', '-H', ACCEPT, path, '--input', '-'], JSON.stringify(body));
const badRepo = repo => (REPO_NAME.test(String(repo)) ? null : fail(`repository ${JSON.stringify(String(repo))} is not owner/name`, { kind: 'config' }));

/** Every ruleset of the repository as GitHub returns it: the list, then each by id. -> `ok(rulesets)` | `fail(reason, { kind })`. A list as long as one page may have another page, and is refused. */
export async function readRulesets(repo, run = runBounded) {
  const refused = badRepo(repo);
  if (refused !== null) return refused;
  const list = await getJson(run, `repos/${repo}/rulesets?per_page=${PAGE}&includes_parents=false`);
  if (!list.ok) return list;
  if (!Array.isArray(list.value)) return envFail('the list of rulesets is not a list');
  if (list.value.length >= PAGE) return envFail(`the list holds ${list.value.length} rulesets, one page: there may be more, so it is not trusted`);
  if (!list.value.every(entry => Number.isSafeInteger(entry?.id))) return envFail('the list holds an entry without an id');
  const rulesets = [];
  for (const entry of list.value) {
    const one = await getJson(run, `repos/${repo}/rulesets/${entry.id}`);
    if (!one.ok) return one;
    if (one.value === null || typeof one.value !== 'object') return envFail(`ruleset ${entry.id} is not an object`);
    rulesets.push(one.value);
  }
  return ok(rulesets);
}

/** The merge settings against the data: -> `ok(problems)` (report only) | `fail`. */
export async function readMergeSettings(repo, run = runBounded) {
  const refused = badRepo(repo);
  if (refused !== null) return refused;
  const read = await getJson(run, `repos/${repo}`);
  if (!read.ok) return read;
  return read.value !== null && typeof read.value === 'object' ? ok(mergeSettingsProblems(read.value)) : envFail('the repository is not an object');
}

// ---- the read-back ----------------------------------------------------------------------------------------------------------------------

const stateOf = (rulesets, expected) => {
  const found = rulesets.filter(ruleset => ruleset.name === expected.name);
  if (found.length === 0) return { name: expected.name, status: 'absent', problems: [] };
  const problems = found.length > 1 ? [`${found.length} rulesets are named ${expected.name}`] : rulesetProblems(found[0], expected);
  return { name: expected.name, status: problems.length === 0 ? 'equal' : 'different', problems, id: found[0].id };
};

/** A ruleset that is not in the data and targets the same refs as one that is: named so a reader sees it, never touched. */
const isStranger = ruleset => !RULESETS.some(own => own.name === ruleset.name) && RULESETS.some(own => own.target === ruleset.target && refs(ruleset, 'include').some(ref => refs(own, 'include').some(wanted => overlaps(ref, wanted))));

/** The line that names a stranger, for `--check` and `--apply` alike. */
export const strangerLine = ({ name, id }) => `note: ruleset "${name}" (id ${id}) is not in the data and targets the same refs; it is left alone`;

/** Reads every ruleset and says, for each of the data's, `equal`, `absent` or `different`. -> `ok({ equal, states, strangers })` | `fail`. */
export async function rulesetsReadBack({ repo, run = runBounded }) {
  const read = await readRulesets(repo, run);
  if (!read.ok) return read;
  const states = RULESETS.map(expected => stateOf(read.value, expected));
  const strangers = read.value.filter(isStranger).map(({ id, name, target }) => ({ id, name, target }));
  return ok({ equal: states.every(state => state.status === 'equal'), states, strangers });
}

// ---- the apply --------------------------------------------------------------------------------------------------------------------------

/** The ruleset named `name`, read back after its creation: -> `ok()` | `fail(reason, { kind })`. It reads the list and then the one entry, so what GitHub stored is what is compared. */
async function readBackOne(repo, run, expected) {
  const list = await getJson(run, `repos/${repo}/rulesets?per_page=${PAGE}&includes_parents=false`);
  if (!list.ok) return list;
  const named = Array.isArray(list.value) ? list.value.filter(entry => entry?.name === expected.name) : [];
  if (named.length === 0) return fail(`${expected.name} is absent after the write`, { kind: 'differs' });
  if (named.length > 1) return fail(`${expected.name} differs after the write: ${named.length} rulesets are named so`, { kind: 'differs' });
  const one = await getJson(run, `repos/${repo}/rulesets/${named[0].id}`);
  if (!one.ok) return one;
  const problems = rulesetProblems(one.value, expected);
  return problems.length === 0 ? ok() : fail(`${expected.name} differs after the write: ${problems.join('; ')}`, { kind: 'differs' });
}

/** One create, then its read-back. A failure of either is `uncertain`: the ruleset may or may not exist, and only `--check` says. */
async function createOne(repo, run, expected) {
  const posted = await postJson(run, `repos/${repo}/rulesets`, expected);
  if (!posted.ok) return posted;
  return readBackOne(repo, run, expected);
}

/** The words after a stop: what is confirmed created, what is uncertain, what was not attempted, and how to go on. */
const stopped = (failure, { made, uncertain, rest }) => fail(scrub(`${failure.reason}. Confirmed created: ${sentence(made)}. Uncertain: ${sentence(uncertain)}. Not attempted: ${sentence(rest)}. Run --check to see what exists, then --apply again: it creates only the rulesets still absent.`), { kind: failure.kind });

/**
 * Creates the data's rulesets that are absent and nothing else. It reads every ruleset first and writes only when every present one equals the data (a present different one, or two of one name, is refused before any write: an update
 * needs the owner's own go). It then creates the absent ones one by one, reading each back, and ends with a read-back of all three. It never updates or deletes. `say` gets one line per ruleset and one per stranger.
 * -> `ok({ outcomes, strangers, readBack })` | `fail(reason, { kind: 'refused' | 'differs' | 'env' | 'config' })`.
 */
export async function applyRulesets({ repo, run = runBounded, say = () => {} }) {
  const before = await rulesetsReadBack({ repo, run });
  if (!before.ok) return before;
  const { states, strangers } = before.value;
  const different = states.filter(state => state.status === 'different');
  if (different.length > 0) return fail(scrub(`refused: ${different.map(state => `${state.name} is present and differs (${state.problems.join('; ')})`).join('; ')}. Updating a ruleset needs its own go of the owner, so nothing was written.`), { kind: 'refused' });
  for (const stranger of strangers) say(strangerLine(stranger));
  for (const state of states.filter(entry => entry.status === 'equal')) say(`unchanged ${state.name}`);
  const absent = RULESETS.filter(ruleset => states.find(state => state.name === ruleset.name).status === 'absent');
  const made = [];
  for (const [index, ruleset] of absent.entries()) {
    const created = await createOne(repo, run, ruleset);
    if (!created.ok) return stopped(created, { made, uncertain: [ruleset.name], rest: absent.slice(index + 1).map(entry => entry.name) });
    made.push(ruleset.name);
    say(`created ${ruleset.name}`);
  }
  const after = await rulesetsReadBack({ repo, run });
  if (!after.ok) return stopped(after, { made, uncertain: [], rest: [] });
  const problems = after.value.states.filter(state => state.status !== 'equal');
  if (problems.length > 0) return stopped(fail(`read back after the writes, ${problems.map(state => `${state.name} is ${state.status}`).join('; ')}`, { kind: 'differs' }), { made, uncertain: problems.map(state => state.name), rest: [] });
  const outcomes = RULESETS.map(({ name }) => ({ name, outcome: made.includes(name) ? 'created' : 'unchanged' }));
  return ok({ outcomes, strangers, readBack: after.value });
}
