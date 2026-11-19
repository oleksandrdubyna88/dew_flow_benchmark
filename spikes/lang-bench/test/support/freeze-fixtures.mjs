// Fixtures of the freeze tools' tests (F2.S2 (d)): a synthetic task repository with the six quick tasks, a bare repository standing for the GitHub remote, and a stand-in for the bench's inputs folder. Everything is
// invented (the specs are the conformance fixture's olympiad spec under six numbers), lives in owned folders and touches no network. A squash merge is `git merge --squash` and a commit on `main`; the push is a push into
// the bare repository. The freeze commit and the workspace commits are made here by plain string edits, never by the tool under test, so the tool is judged against an independent construction.
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { sha256Of } from '../../../minigit-bench/lib/inputs.mjs';
import { runFreeze } from '../../lib/freeze-cli.mjs';
import { FIXTURES } from './conformance-fixtures.mjs';
import { commit, git, gitWithEnv, ownedFolder } from './push-fixtures.mjs';

/** The six quick tasks: number and slug. */
export const SIX = Object.freeze([['04', 'rule-api'], ['05', 'key-value-server'], ['06', 'blog-api'], ['07', 'stream-processor'], ['08', 'ledger-api'], ['10', 'dag-scheduler']]);
export const SLUGS = Object.freeze(SIX.map(([nn, slug]) => `${nn}-${slug}`));
export const specPath = slug => `todo/tasks/${slug}.md`;
export const FORMAT_PATH = 'todo/tasks/FORMAT.md';
export const TAG = 'spec-frozen-B-20261201';
export const LATER_TAG = 'spec-frozen-B-20261202';
export const MESSAGE = 'Tasks 04, 05, 06, 07, 08, 10.\n';
/** The day the tools think it is (their `now` seam), so a plan's `--date` is checked against a fixed UTC day. */
export const TODAY = new Date('2026-12-01T10:00:00Z');
export const NEXT_DAY = new Date('2026-12-02T10:00:00Z');

const SPEC = readFileSync(path.join(FIXTURES, 'olympiad/todo/tasks/01-count-down.md'), 'utf8');
const SAMPLES = path.join(FIXTURES, 'olympiad/tasks/01-count-down/samples');
const FORMAT_RAW = readFileSync(path.join(FIXTURES, 'FORMAT.md'), 'utf8');
const WORKED = '| 6.1 Olympiad — one sample of the pilot spec | olympiad |\n| 6.2 Daemon — a toy store over an invented framed protocol | daemon-web |\n| 6.3 Web — two clients racing on one idempotency key | daemon-web |\n';

/** The conformance fixture's FORMAT with a class table that passes the class check (its 6.x rows made one row for section 6); `brokenClasses` keeps the table as it was, which fails it. */
export const formatText = ({ brokenClasses = false } = {}) => (brokenClasses ? FORMAT_RAW : FORMAT_RAW.replace(WORKED, '| 6. Worked examples | common |\n'));
export const sha256 = text => sha256Of(Buffer.from(text));

/** One spec: the fixture spec under the number `nn`, gated (the verdict row filled) unless `gated` is false, and carrying `tag` in its freeze row when given. */
export function specText(nn, { gated = true, tag = null } = {}) {
  let text = SPEC.replace('# Task 01 —', `# Task ${nn} —`);
  if (gated) text = text.replace('| Gate verdict | not yet gated |', '| Gate verdict | `proceed` · 2026-11-05 |');
  if (tag !== null) text = text.replace('| Freeze tag | none yet |', `| Freeze tag | \`${tag}\` |`);
  return text;
}

/** The files of the task repository at its start: FORMAT, the six gated specs with their samples, and the empty roots. */
export function baseFiles({ format = formatText(), specs = {} } = {}) {
  const files = { [FORMAT_PATH]: format, 'csharp/.gitkeep': '', 'node/.gitkeep': '', 'tasks/.gitkeep': '' };
  for (const [nn, slug] of SIX) {
    files[specPath(`${nn}-${slug}`)] = specs[nn] ?? specText(nn);
    for (const name of readdirSync(SAMPLES)) files[`tasks/${nn}-${slug}/samples/${name}`] = readFileSync(path.join(SAMPLES, name), 'utf8');
  }
  return files;
}

/** The registry of packages the freeze reads the gated hashes from (`spikes/lang-explore/inputs/packages.json`, only the part the freeze reads): `{ NN: { specSha256 } }`, the sha256 of each of the six gated specs of `files`. */
export const pinsOf = files => Object.fromEntries(SIX.filter(([nn, slug]) => files[specPath(`${nn}-${slug}`)] !== undefined).map(([nn, slug]) => [nn, { specSha256: sha256(files[specPath(`${nn}-${slug}`)]) }]));

/** The record of the FORMATs, in the shape of `inputs/specs.d/format.json`; `over` replaces entries. */
export function formatRecord(format, over = {}) {
  const entry = tasks => ({ version: 2, sha256: sha256(format), gate: 'inputs/gates/FORMAT-v2.md', grammarSha256: '8'.repeat(64), tasks });
  return { A: entry(['01', '02', '03', '09']), B: entry(SIX.map(([nn]) => nn)), ...over };
}

/**
 * A task repository fixture: `repo` (a working clone on `main` with `origin` set), `origin` (the bare repository standing for the remote), `inputs` (a stand-in for the bench's `inputs/`: the FORMAT record, the six
 * gate records and an empty `specs.d/`), `base` (the commit of the six gated specs, pushed). `files` replaces the starting files; `record` replaces the FORMAT record.
 */
export function makeFreezeRepo(t, { files, format = formatText(), record, pinned } = {}) {
  const root = ownedFolder(t, 'freeze');
  const origin = path.join(root, 'origin.git');
  const repo = path.join(root, 'repo');
  const inputs = path.join(root, 'inputs');
  mkdirSync(origin);
  mkdirSync(repo);
  git(origin, 'init', '-q', '--bare', '-b', 'main');
  git(repo, 'init', '-q', '-b', 'main');
  git(repo, 'remote', 'add', 'origin', origin);
  const base = commit(repo, 'chore: the six specs', files ?? baseFiles({ format }));
  git(repo, 'push', '-q', 'origin', 'main');
  mkdirSync(path.join(inputs, 'specs.d'), { recursive: true });
  mkdirSync(path.join(inputs, 'gates'), { recursive: true });
  const recordFile = path.join(inputs, 'specs.d', 'format.json');
  writeFileSync(recordFile, `${JSON.stringify(record ?? formatRecord(format), null, 2)}\n`);
  for (const [nn] of SIX) writeFileSync(path.join(inputs, 'gates', `spec-${nn}.md`), `# The gate of task ${nn}'s spec\n`);
  const pins = path.join(root, 'packages.json');
  writeFileSync(pins, `${JSON.stringify(pinsOf(pinned ?? files ?? baseFiles({ format })), null, 2)}\n`);
  const fixture = { root, repo, origin, inputs, recordFile, pins, base, formatSha256: sha256(format) };
  return { ...fixture, run: (verb, flags = [], seams = {}) => runVerb(fixture, verb, flags, seams) };
}

/** One verb through `runFreeze` in this process, on the fixture's repository and inputs, with `now` fixed at `TODAY`. */
const runVerb = (fixture, verb, flags, seams) => runFreeze([verb, '--repo', fixture.repo, '--inputs', fixture.inputs, '--pins', fixture.pins, ...flags], { now: () => TODAY, ...seams });

/** The freeze commit on a branch cut from `from`: each spec's `Freeze tag` row set to `tag` by a plain string edit, plus `more` (path -> text, null removes) in the same commit. Returns the branch's head. */
export function freezeBranch(fixture, { tag = TAG, from = 'main', more = {}, rows = SLUGS } = {}) {
  git(fixture.repo, 'checkout', '-q', '-B', `freeze/${tag}`, from);
  const files = { ...more };
  for (const slug of rows) {
    const file = path.join(fixture.repo, specPath(slug));
    const before = readFileSync(file, 'utf8');
    const row = /^\| Freeze tag \| .* \|$/m;
    files[specPath(slug)] = files[specPath(slug)] ?? before.replace(row, `| Freeze tag | \`${tag}\` |`);
  }
  return commit(fixture.repo, `docs: freeze ${tag}`, files);
}

/** A squash merge of `branch` into `main` and a push to the bare repository, as GitHub does it: a new commit whose parent is main's previous tip. Returns it. */
export function squashMerge(fixture, branch, message = `squash ${branch}`) {
  git(fixture.repo, 'checkout', '-q', 'main');
  git(fixture.repo, 'merge', '--squash', branch);
  git(fixture.repo, 'commit', '-q', '-m', message);
  git(fixture.repo, 'push', '-q', 'origin', 'main');
  return git(fixture.repo, 'rev-parse', 'HEAD');
}

/** The usual freeze: the branch, then its squash merge; returns `{ head, c }` (the branch's head and the squash commit C). */
export function freezeAndMerge(fixture, options = {}) {
  const head = freezeBranch(fixture, options);
  return { head, c: squashMerge(fixture, `freeze/${options.tag ?? TAG}`, 'T1') };
}

/**
 * The intended annotation written to a file in the fixture's root and the annotated tag made locally on `at`; returns the message file. `date` fixes the tagger's date, so two tags with the same message can still be two
 * different tag objects (the object's id covers the date).
 */
export function annotate(fixture, { tag = TAG, at, message = MESSAGE, date } = {}) {
  const file = path.join(fixture.root, `${tag}.${sha256(message).slice(0, 8)}.message`);
  writeFileSync(file, message);
  gitWithEnv(fixture.repo, date === undefined ? {} : { GIT_COMMITTER_DATE: date }, 'tag', '-a', tag, at, '-F', file);
  return file;
}

export const pushTag = (fixture, tag = TAG) => git(fixture.repo, 'push', '-q', 'origin', `refs/tags/${tag}`);

/** The files of the twelve workspaces: a statement and one sample per stack and task. `over` replaces or removes (null) entries. */
export function workspaceFiles(over = {}, tasks = SLUGS) {
  const files = {};
  for (const slug of tasks) {
    for (const stack of ['csharp', 'node']) {
      files[`${stack}/${slug}/STATEMENT.md`] = `# Statement of ${slug}\n`;
      files[`${stack}/${slug}/samples/s01.in`] = '3\n';
    }
  }
  return { ...files, ...over };
}

/** The workspaces branch cut from `from` (a tag's commit C) and its commit of `files`; returns the branch name. */
export function workspacesBranch(fixture, { tag = TAG, from, files = workspaceFiles() } = {}) {
  git(fixture.repo, 'checkout', '-q', '-B', `workspaces/${tag}`, from);
  commit(fixture.repo, `feat: the workspaces of ${tag}`, files);
  return `workspaces/${tag}`;
}

/** The whole sequence of (e) in the fixture: T1 merged as C, the tag on C pushed, T2 merged as W. Returns `{ c, w, tag, messageFile }`. */
export function fullFreeze(fixture, { tag = TAG, files = workspaceFiles() } = {}) {
  const { c } = freezeAndMerge(fixture, { tag });
  const messageFile = annotate(fixture, { tag, at: c });
  pushTag(fixture, tag);
  const branch = workspacesBranch(fixture, { tag, from: c, files });
  return { c, w: squashMerge(fixture, branch, 'T2'), tag, messageFile };
}

/** `yyyy-mm-dd` of a freeze tag's own date, and of the day after it. */
export const tagDay = tag => `${tag.slice(-8, -4)}-${tag.slice(-4, -2)}-${tag.slice(-2)}`;
const nextDay = day => new Date(Date.parse(`${day}T00:00:00Z`) + 86_400_000).toISOString().slice(0, 10);
export const SCRATCH = 'f'.repeat(40);

/**
 * The evidence file `record` reads, with dates in the order the procedure makes them: the leak checks of C and of the scratch W″ and the push on the tag's day, the workspaces check and the leak check of W the day after.
 * `over` replaces keys of the file, `leak` replaces keys of its `leakcheck`.
 */
export function evidence(fixture, { c, w, tag = TAG, over = {}, leak = {} }) {
  const [day, next] = [tagDay(tag), nextDay(tagDay(tag))];
  const done = (commitSha, date) => ({ commit: commitSha, exit: 0, date });
  const leakcheck = { freezeCommit: done(c, day), scratchWorkspace: done(SCRATCH, day), workspaceCommit: done(w, next), ...leak };
  const body = { provisionalFrom: day, workspacesCheck: done(w, next), leakcheck, ...over };
  const file = path.join(fixture.root, 'evidence.json');
  writeFileSync(file, `${JSON.stringify(body, null, 2)}\n`);
  return file;
}

/** The engine's check output of W as `record` reads it: findings-free, its last line naming W. */
export function workspacesCheckFile(fixture, w, line = `workspaces-check ok commit ${w} tasks 04,05,06,07,08,10 stacks csharp,node`) {
  const file = path.join(fixture.root, 'workspaces-check.txt');
  writeFileSync(file, `checked 12 workspaces\n${line}\n`);
  return file;
}

export { git };
