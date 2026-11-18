// `freeze.mjs record` (F2.S2 (d), amendments 1, 3 and 6): writes `inputs/specs.d/spec-states.json`, and only that file, once the freeze is accepted. It VERIFIES before it writes: it runs `check-tag`, `check-workspace` and
// the hash list of W itself, requires the engine's clean workspaces check at W and the three clean leak-check results, and checks that W holds exactly the twelve workspace folders, none empty. The file holds one state,
// `accepted`; the provisional state lives only in a dated line of `inputs/checkpoints.md`. The shape (the other half's consumers test reads the same):
//   { specs: { "<NN>": { path, sha256, state: "frozen", gate, rIds, commitSha, tagObjectSha } }, freeze: { tag, tagObjectSha, commitSha, workspaceCommit, formatSha256, hashesLangSha256,
//     leakcheck: { freezeCommit, scratchWorkspace, workspaceCommit }, workspacesCheck: { commit, exit, date }, state: "accepted", provisionalFrom, acceptedOn, superseded: [{ tag, tagObjectSha, why }] } }
// Exit: 1 a verification is red (check-tag, check-workspace, the twelve folders, the hash list); 3 the record's own inputs are refused (the evidence, the workspaces check, --supersedes, a record of another freeze).
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { parseSpec } from './conformance-spec.mjs';
import { readEvidence, readWorkspacesCheck } from './freeze-evidence.mjs';
import { blobsOf, commitOf, isSha, remoteTag, treeAt } from './freeze-git.mjs';
import { hashList } from './freeze-hashes.mjs';
import { isEnv, loadPin, pinTaskProblem, problemsOf, specSet } from './freeze-inputs.mjs';
import { tagDate } from './freeze-rows.mjs';
import { checkRemoteTag } from './freeze-tags.mjs';
import { writeOutput } from './freeze-write.mjs';
import { runGit } from './push-git.mjs';
import { workspaceProblems } from './spec-freeze.mjs';
import { assemble, canonicalJson } from './specs-record.mjs';

export const STATES_FILE = 'spec-states.json';
const STACKS = Object.freeze(['csharp', 'node']);
const GATE_FOLDER = 'inputs/gates';

const refusal = (problems, exit) => fail(problems[0], { problems, exit });
const redOf = (result, label) => (isEnv(result) ? result : refusal(problemsOf(result).map(problem => `${label}: ${problem}`), 1));

/** The sentences when `--supersedes` and `--why` do not pair up, or the same tag is named twice: each old tag needs its sentence. */
export function supersedesProblems(supersedes) {
  const bad = supersedes.filter(entry => entry.tag === undefined || entry.why === undefined || entry.why.trim() === '');
  return bad.length > 0 ? ['every --supersedes <old tag> needs its --why <sentence>, and every --why a --supersedes'] : [];
}

/** The sentence when an old tag cannot be superseded by `tag` (itself, absent from the remote, not older), else null. `remote` is what the remote answered for the old tag: `{ object, peeled }` or null. */
function supersedeProblem({ old, tag, remote }) {
  if (old === tag) return `${old} cannot supersede itself`;
  if (remote === null) return `the old tag ${old} does not exist on the remote (git ls-remote --tags origin): only a pushed tag is superseded; a tag that never reached the remote is deleted and made again instead`;
  return tagDate(old) !== null && tagDate(old) < tagDate(tag) ? null : `the old tag ${old} is not older than ${tag}: a superseding tag has a later date (an earlier or equal one cannot replace it)`;
}

/** ok([{ tag, tagObjectSha, why }]): each old tag exists on the remote (the remote is asked, never the local refs) and is older than `tag`; the tag object's id is the remote's. */
async function supersededEntries({ repo, tag, supersedes }) {
  const entries = [];
  for (const { tag: old, why } of supersedes) {
    const remote = await remoteTag(repo, old);
    if (!remote.ok) return remote;
    const problem = supersedeProblem({ old, tag, remote: remote.value });
    if (problem !== null) return refusal([problem], 3);
    entries.push({ tag: old, tagObjectSha: remote.value.object, why });
  }
  return ok(entries);
}

/** The sentence when a record already in the file is of another freeze (another tag) or cannot be read, else null. The same tag is judged by its bytes once they are rendered (`rewriteProblem`). */
function existingProblem({ file, tag }) {
  if (!existsSync(file)) return null;
  try {
    const found = JSON.parse(readFileSync(file, 'utf8'))?.freeze?.tag;
    return found === tag ? null : `${file} already holds a record of another freeze (${found ?? 'no freeze.tag'}): this tool writes the first freeze's record only`;
  } catch (error) {
    return `${file} exists and is not a readable record (${error.message}): remove it by hand if it is a leftover`;
  }
}

/** The sentence when a record of this tag is already in the file and the new bytes are not identical to it (a different W, other hashes, other evidence), else null: a recorded freeze is rewritten only with the same bytes. */
function rewriteProblem({ file, text }) {
  if (!existsSync(file)) return null;
  const old = readFileSync(file, 'utf8');
  if (old === text) return null;
  const [before, after] = [JSON.parse(old).freeze ?? {}, JSON.parse(text).freeze];
  const fields = Object.keys({ ...before, ...after }).filter(key => JSON.stringify(before[key]) !== JSON.stringify(after[key]));
  return `${file} already holds this tag's record and the new one differs from it (${fields.join(', ') || 'the spec rows'}): a recorded freeze is rewritten only with identical bytes, so nothing is written`;
}

/** The workspace folders (`<stack>/<NN-slug>`) a tree holds a file in. */
const workspaceFolders = tree => new Set([...tree.keys()].flatMap(file => {
  const parts = file.split('/');
  return STACKS.includes(parts[0]) && parts.length > 2 ? [`${parts[0]}/${parts[1]}`] : [];
}));

/** The sentences about W's workspace folders: exactly `csharp/<NN-slug>/` and `node/<NN-slug>/` of the six, each holding a file. */
function folderProblems({ tree, specs }) {
  const wanted = new Set(specs.flatMap(spec => STACKS.map(stack => `${stack}/${spec.slug}`)));
  const present = workspaceFolders(tree);
  const missing = [...wanted].filter(folder => !present.has(folder));
  const extra = [...present].filter(folder => !wanted.has(folder));
  const absent = missing.length > 0 ? [`W holds ${wanted.size - missing.length} of the twelve workspace folders; missing or empty: ${missing.map(folder => `${folder}/`).join(', ')}`] : [];
  return [...absent, ...extra.map(folder => `${folder}/ is a workspace folder of none of the six tasks`)];
}

/** ok(the day a commit was made, UTC, `yyyy-mm-dd`). */
async function dayOf(repo, commit) {
  const shown = await runGit(repo, ['show', '-s', '--format=%ct', commit]);
  return shown.ok ? ok(new Date(Number(shown.value.stdout.trim()) * 1000).toISOString().slice(0, 10)) : shown;
}

/** The six spec rows: the blob's sha256 at C, its requirement ids and its gate record (which must exist in the bench's inputs). */
async function specRows({ repo, specs, c, tagObject, inputs }) {
  const tree = await treeAt(repo, c);
  const blobs = tree.ok ? await blobsOf(repo, tree.value, specs.map(spec => spec.path)) : tree;
  if (!blobs.ok) return blobs;
  const rows = {};
  const problems = [];
  for (const spec of specs) {
    const bytes = blobs.value.get(spec.path);
    const gate = `${GATE_FOLDER}/spec-${spec.task}.md`;
    if (!existsSync(path.join(inputs, 'gates', `spec-${spec.task}.md`))) problems.push(`${spec.path}: its gate record ${gate} is not in the bench's inputs`);
    const parsed = parseSpec(bytes);
    const rIds = parsed.ok ? parsed.value.requirements.map(requirement => `R${requirement.n}`) : [];
    rows[spec.task] = { path: spec.path, sha256: createHash('sha256').update(bytes).digest('hex'), state: 'frozen', gate, rIds, commitSha: c, tagObjectSha: tagObject };
  }
  return problems.length > 0 ? refusal(problems, 3) : ok(rows);
}

/** ok(text): the record of the freeze as canonical JSON (sorted keys, two-space indent, LF), the same bytes for the same inputs. */
function renderRecord({ rows, freeze }) {
  return canonicalJson({ specs: rows, freeze });
}

/** The validation of the new file's text, read back from the temporary file, before its rename: it parses, and `specs-record`'s assembly of the writers' files takes it. */
const validateRecord = inputs => text => {
  try {
    JSON.parse(text);
  } catch (error) {
    return fail(`it is not JSON (${error.message})`);
  }
  const built = assemble(inputs, { [STATES_FILE]: text });
  return built.ok ? ok(true) : fail(`specs-record cannot assemble it: ${built.reason}`);
};

/** What the verifications give: C, the checked tag, W's specs and the hash list; a red one is a refusal with exit 1 naming the verb. */
async function verified({ repo, tag, commit, pin, c }) {
  const checked = await checkRemoteTag({ repo, tag, commit: c, pin });
  if (!checked.ok) return redOf(checked, 'check-tag');
  const base = await workspaceProblems({ repo, base: c, commit });
  if (!base.ok) return base;
  if (base.value.length > 0) return refusal(base.value.map(problem => `check-workspace: ${problem}`), 1);
  const tree = await treeAt(repo, commit);
  const specs = tree.ok ? specSet(tree.value) : tree;
  if (!specs.ok) return redOf(specs, 'the twelve workspaces');
  const folders = folderProblems({ tree: tree.value, specs: specs.value });
  if (folders.length > 0) return refusal(folders.map(problem => `the twelve workspaces: ${problem}`), 1);
  const list = await hashList({ repo, commit });
  return list.ok ? ok({ c, checked: checked.value, specs: specs.value, list: list.value }) : redOf(list, 'hashes');
}

/** The inputs that need no repository answer: the evidence, the workspaces check and the supersedes pairs. -> ok(evidence) | refusal(…, 3). */
function givenInputs({ flags, commit, c }) {
  const evidence = readEvidence({ file: flags.evidence, commit, freeze: c });
  const check = readWorkspacesCheck({ file: flags.check, commit });
  const problems = [...(evidence.ok ? [] : problemsOf(evidence)), ...(check.ok ? [] : [check.reason]), ...supersedesProblems(flags.supersedes)];
  return problems.length > 0 ? refusal(problems, 3) : ok(evidence.value);
}

/** One `{ commit, exit, date }` result as the record holds it: those three keys, nothing else the evidence file carried. */
const resultOf = entry => ({ commit: entry.commit, exit: entry.exit, date: entry.date });

/** The freeze entry from what was verified and what was given. */
function freezeEntry({ tag, commit, parts, evidence, superseded, acceptedOn }) {
  const { checked, list, c } = parts;
  const leak = evidence.leakcheck;
  return {
    tag, tagObjectSha: checked.tagObject, commitSha: c, workspaceCommit: commit, formatSha256: checked.formatSha256, hashesLangSha256: createHash('sha256').update(list.text).digest('hex'),
    leakcheck: { freezeCommit: resultOf(leak.freezeCommit), scratchWorkspace: resultOf(leak.scratchWorkspace), workspaceCommit: resultOf(leak.workspaceCommit) },
    workspacesCheck: resultOf(evidence.workspacesCheck), state: 'accepted', provisionalFrom: evidence.provisionalFrom, acceptedOn, superseded,
  };
}

/** Everything the record needs that is given or asked of the remote, before any verification: ok({ pin, w, c, evidence, superseded, file }) | a refusal (exit 3 or 1) | an environment fail. */
async function preflight({ repo, tag, commit, flags, inputs, record }) {
  const pin = loadPin(record);
  const taskProblem = pin.ok ? pinTaskProblem(pin.value) : pin.reason;
  if (taskProblem !== null) return refusal([taskProblem], 3);
  const w = await commitOf(repo, commit);
  if (!w.ok) return isEnv(w) ? w : refusal([w.reason], 3);
  const remote = await remoteTag(repo, tag);
  if (!remote.ok) return remote;
  if (remote.value === null) return refusal([`check-tag: the remote has no tag ${tag}`], 1);
  const c = remote.value.peeled ?? remote.value.object;
  const evidence = isSha(c) ? givenInputs({ flags, commit: w.value, c }) : refusal(['the remote tag names no commit'], 3);
  const superseded = evidence.ok ? await supersededEntries({ repo, tag, supersedes: flags.supersedes }) : evidence;
  const file = path.join(inputs, 'specs.d', STATES_FILE);
  const existing = superseded.ok ? existingProblem({ file, tag }) : null;
  if (!superseded.ok || existing !== null) return superseded.ok ? refusal([existing], 3) : superseded;
  return ok({ pin: pin.value, w: w.value, c, evidence: evidence.value, superseded: superseded.value, file });
}

/**
 * `record`: ok({ file, text, notes }) once the record is written; a fail carries `problems` and `exit` (1 a verification is red, 3 an input is refused; an environment failure has `env: true`).
 * `flags`: `{ evidence, check, supersedes: [{ tag, why }] }`. `inputs` is the bench's inputs folder, `record` its FORMAT record; `io` is the writer's seam.
 */
export async function recordFreeze({ repo, tag, commit, flags, inputs, record, io }) {
  const given = await preflight({ repo, tag, commit, flags, inputs, record });
  if (!given.ok) return given;
  const { pin, w, c, evidence, superseded, file } = given.value;
  const parts = await verified({ repo, tag, commit: w, pin, c });
  if (!parts.ok) return parts;
  const [day, rows] = [await dayOf(repo, w), await specRows({ repo, specs: parts.value.specs, c, tagObject: parts.value.checked.tagObject, inputs })];
  if (!rows.ok || !day.ok) return rows.ok ? day : rows;
  const freeze = freezeEntry({ tag, commit: w, parts: parts.value, evidence, superseded, acceptedOn: day.value });
  const text = renderRecord({ rows: rows.value, freeze });
  const differs = rewriteProblem({ file, text });
  if (differs !== null) return refusal([differs], 3);
  const written = await writeOutput({ dir: inputs, target: file, text, what: 'record', validate: validateRecord(inputs), io });
  return written.ok ? ok({ file, text, notes: written.value.notes }) : fail(written.reason, { ...written, problems: [written.reason], exit: 1 });
}
