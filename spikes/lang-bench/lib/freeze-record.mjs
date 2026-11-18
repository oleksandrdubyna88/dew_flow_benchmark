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
import { blobsOf, commitOf, isSha, parentOf, remoteTag, treeAt } from './freeze-git.mjs';
import { HASHES_FILE, hashList } from './freeze-hashes.mjs';
import { isEnv, loadPin, pinTaskProblem, problemsOf, runSteps, specSet, unless } from './freeze-inputs.mjs';
import { tagDate } from './freeze-rows.mjs';
import { checkRemoteTag, mainProblems } from './freeze-tags.mjs';
import { writeOutput } from './freeze-write.mjs';
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
  const names = supersedes.map(entry => entry.tag).filter(name => name !== undefined);
  const twice = [...new Set(names.filter((name, index) => names.indexOf(name) !== index))];
  return [...(bad.length > 0 ? ['every --supersedes <old tag> needs its --why <sentence>, and every --why a --supersedes'] : []), ...twice.map(name => `the old tag ${name} is named twice: each superseded tag is named once, with one sentence why`)];
}

const olderThan = (old, tag) => tagDate(old) !== null && tagDate(old) < tagDate(tag);

/** The sentence when an old tag cannot be superseded by `tag` (itself, absent from the remote, not older), else null. `remote` is what the remote answered for the old tag: `{ object, peeled }` or null. */
function supersedeProblem({ old, tag, remote }) {
  if (old === tag) return `${old} cannot supersede itself`;
  if (remote === null) return `the old tag ${old} does not exist on the remote (git ls-remote --tags origin): only a pushed tag is superseded; a tag that never reached the remote is deleted and made again instead`;
  return olderThan(old, tag) ? null : `the old tag ${old} is not older than ${tag}: a superseding tag has a later date (an earlier or equal one cannot replace it)`;
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

/** The requirement ids of a spec (`R1`, `R2`, ...), `[]` for a spec the reader refuses. */
const rIdsOf = bytes => {
  const parsed = parseSpec(bytes);
  return parsed.ok ? parsed.value.requirements.map(requirement => `R${requirement.n}`) : [];
};

/** The sentence when a spec's gate record is not in the bench's inputs or no requirement id could be read from it, else null. */
function rowProblem({ spec, gate, present, rIds }) {
  if (!present) return `${spec.path}: its gate record ${gate} is not in the bench's inputs`;
  return rIds.length > 0 ? null : `${spec.path}: no requirement ids (R1, R2, …) could be read from it: a frozen spec has them`;
}

/** One spec's row (the blob's sha256 at C, its requirement ids, its gate record) and the sentence when its gate record is not in the bench's inputs (else null). */
function rowOf({ spec, bytes, c, tagObject, inputs }) {
  const gate = `${GATE_FOLDER}/spec-${spec.task}.md`;
  const present = existsSync(path.join(inputs, 'gates', `spec-${spec.task}.md`));
  return {
    task: spec.task,
    problem: rowProblem({ spec, gate, present, rIds: rIdsOf(bytes) }),
    row: { path: spec.path, sha256: createHash('sha256').update(bytes).digest('hex'), state: 'frozen', gate, rIds: rIdsOf(bytes), commitSha: c, tagObjectSha: tagObject },
  };
}

/** ok(Map(path -> bytes)) of the six specs at C. */
async function specBlobs({ repo, specs, c }) {
  const tree = await treeAt(repo, c);
  return tree.ok ? blobsOf(repo, tree.value, specs.map(spec => spec.path)) : tree;
}

/** The six rows keyed by task, or the refusal naming each gate record the inputs lack: both derived from the per-spec results. */
function rowsOrRefusal(results) {
  const problems = results.flatMap(result => (result.problem === null ? [] : [result.problem]));
  return problems.length > 0 ? refusal(problems, 3) : ok(Object.fromEntries(results.map(result => [result.task, result.row])));
}

/** The six spec rows, keyed by task. */
async function specRows({ repo, specs, c, tagObject, inputs }) {
  const blobs = await specBlobs({ repo, specs, c });
  return blobs.ok ? rowsOrRefusal(specs.map(spec => rowOf({ spec, bytes: blobs.value.get(spec.path), c, tagObject, inputs }))) : blobs;
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

/** The sentences when W's parent is not C (or W has no single parent). */
function parentProblem({ parent, c }) {
  if (!parent.ok) return [`it is not a squash commit: ${parent.reason}`];
  return unless(parent.value === c, `its parent is ${parent.value}, not C ${c}: W is the squash of a workspaces branch cut from C, with no other merge between`);
}

/** ok([sentence]): W is the commit of the squash merge of the workspaces pull request: on the remote's main (asked now) and with C as its one parent. */
async function placementProblems({ repo, commit, c }) {
  const main = await mainProblems({ repo, commit });
  const parent = await parentOf(repo, commit);
  if (!main.ok) return main;
  return isEnv(parent) ? parent : ok([...main.value, ...parentProblem({ parent, c })]);
}

/** The sentence when the hash list on disk (`<inputs>/hashes-lang.txt`) is absent or is not W's list, else null: `record` only names what is already written. */
function listOnDiskProblem({ inputs, commit, text }) {
  const file = path.join(inputs, HASHES_FILE);
  const held = existsSync(file) ? readFileSync(file, 'utf8') : null;
  return held === text ? null : `${file} ${held === null ? 'is absent' : 'is not the hash list of W'}: write it first with freeze.mjs hashes --write --repo <task repository> --commit ${commit}, then record`;
}

/** ok({ checked }) once `check-tag` is clean for C; a red one is a refusal with exit 1. */
async function checkTagStep({ repo, tag, pin, pins, c }) {
  const checked = await checkRemoteTag({ repo, tag, commit: c, pin, pins });
  return checked.ok ? ok({ checked: checked.value }) : redOf(checked, 'check-tag');
}

/** ok({}) when W's placement is right (on the remote's main, C its parent). */
async function placementStep({ repo, commit, c }) {
  const placed = await placementProblems({ repo, commit, c });
  if (!placed.ok) return placed;
  return placed.value.length > 0 ? refusal(placed.value.map(problem => `W: ${problem}`), 1) : ok({});
}

/** ok({}) when W changes only the twelve workspace folders and keeps every spec and FORMAT blob and mode. */
async function workspaceStep({ repo, commit, c }) {
  const base = await workspaceProblems({ repo, base: c, commit });
  if (!base.ok) return base;
  return base.value.length > 0 ? refusal(base.value.map(problem => `check-workspace: ${problem}`), 1) : ok({});
}

/** ok({ specs }) when W holds exactly the twelve workspace folders. */
async function foldersStep({ repo, commit }) {
  const tree = await treeAt(repo, commit);
  const specs = tree.ok ? specSet(tree.value) : tree;
  if (!specs.ok) return redOf(specs, 'the twelve workspaces');
  const folders = folderProblems({ tree: tree.value, specs: specs.value });
  return folders.length > 0 ? refusal(folders.map(problem => `the twelve workspaces: ${problem}`), 1) : ok({ specs: specs.value });
}

/** ok({ list }) when W's hash list can be made and is the one on disk. */
async function listStep({ repo, commit, inputs }) {
  const list = await hashList({ repo, commit });
  if (!list.ok) return redOf(list, 'hashes');
  const onDisk = listOnDiskProblem({ inputs, commit, text: list.value.text });
  return onDisk === null ? ok({ list: list.value }) : refusal([onDisk], 1);
}

/** What the verifications give (C, the checked tag, W's specs and the hash list); a red one is a refusal with exit 1 naming the verb. `commit` is W. */
const verified = args => runSteps([checkTagStep, placementStep, workspaceStep, foldersStep, listStep], args);

/** The inputs that need no repository answer: the evidence, the workspaces check and the supersedes pairs. -> ok(evidence) | refusal(…, 3). */
function givenInputs({ flags, commit, c, tag }) {
  const evidence = readEvidence({ file: flags.evidence, commit, freeze: c, tag });
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

/** ok({ pin }) when the FORMAT record gives the pin for exactly the six tasks. */
async function pinStep({ record }) {
  const pin = loadPin(record);
  const problem = pin.ok ? pinTaskProblem(pin.value) : pin.reason;
  return problem === null ? ok({ pin: pin.value }) : refusal([problem], 3);
}

/** ok({ w }): the workspace commit as a 40-hex commit of the repository. */
async function commitStep({ repo, commit }) {
  const w = await commitOf(repo, commit);
  if (!w.ok) return isEnv(w) ? w : refusal([w.reason], 3);
  return ok({ w: w.value });
}

/** ok({ c }): the commit the remote's tag points at (peeled for an annotated tag); the tag must be on the remote. */
async function remoteStep({ repo, tag }) {
  const remote = await remoteTag(repo, tag);
  if (!remote.ok) return remote;
  if (remote.value === null) return refusal([`check-tag: the remote has no tag ${tag}`], 1);
  const c = remote.value.peeled ?? remote.value.object;
  return isSha(c) ? ok({ c }) : refusal(['the remote tag names no commit'], 3);
}

/** ok({ evidence }) from the evidence file, the workspaces check and the supersedes pairs. */
async function evidenceStep({ flags, w, c, tag }) {
  const evidence = givenInputs({ flags, commit: w, c, tag });
  return evidence.ok ? ok({ evidence: evidence.value }) : evidence;
}

/** ok({ superseded }): the old tags the remote holds. */
async function supersededStep({ repo, tag, flags }) {
  const superseded = await supersededEntries({ repo, tag, supersedes: flags.supersedes });
  return superseded.ok ? ok({ superseded: superseded.value }) : superseded;
}

/** ok({ file }): the record's file, which may hold no record of another freeze. */
async function fileStep({ inputs, tag }) {
  const file = path.join(inputs, 'specs.d', STATES_FILE);
  const existing = existingProblem({ file, tag });
  return existing === null ? ok({ file }) : refusal([existing], 3);
}

/** Everything the record needs that is given or asked of the remote, before any verification: ok({ pin, w, c, evidence, superseded, file }) | a refusal (exit 3 or 1) | an environment fail. */
const preflight = args => runSteps([pinStep, commitStep, remoteStep, evidenceStep, supersededStep, fileStep], args);

/** ok({ parts }): the verifications of W (see `verified`), with W as the commit asked about. */
async function verifiedStep(state) {
  const parts = await verified({ ...state, commit: state.w });
  return parts.ok ? ok({ parts: parts.value }) : parts;
}

/** ok({ rows }): the six spec rows. */
async function rowsStep({ repo, parts, c, inputs }) {
  const rows = await specRows({ repo, specs: parts.specs, c, tagObject: parts.checked.tagObject, inputs });
  return rows.ok ? ok({ rows: rows.value }) : rows;
}

/** ok({ text }): the record's bytes, which a record already in the file for the tag must equal. */
async function textStep({ tag, w, parts, evidence, superseded, rows, file }) {
  const freeze = freezeEntry({ tag, commit: w, parts, evidence, superseded, acceptedOn: evidence.leakcheck.workspaceCommit.date });
  const text = renderRecord({ rows, freeze });
  const differs = rewriteProblem({ file, text });
  return differs === null ? ok({ text }) : refusal([differs], 3);
}

/**
 * `record`: ok({ file, text, notes }) once the record is written; a fail carries `problems` and `exit` (1 a verification is red, 3 an input is refused; an environment failure has `env: true`).
 * `flags`: `{ evidence, check, supersedes: [{ tag, why }] }`. `inputs` is the bench's inputs folder, `record` its FORMAT record; `io` is the writer's seam.
 */
export async function recordFreeze(args) {
  const done = await runSteps([preflight, verifiedStep, rowsStep, textStep], args);
  if (!done.ok) return done;
  const { file, text, inputs, io } = done.value;
  const written = await writeOutput({ dir: inputs, target: file, text, what: 'record', validate: validateRecord(inputs), io });
  return written.ok ? ok({ file, text, notes: written.value.notes }) : fail(written.reason, { ...written, problems: [written.reason], exit: 1 });
}
