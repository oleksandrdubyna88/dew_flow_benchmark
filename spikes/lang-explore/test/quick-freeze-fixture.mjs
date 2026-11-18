// The world of the freeze-workspace tests (F2.S2 (d), the lang-explore half): an INVENTED task repository (`git init` in an owned folder) holding six invented specs and the committed synthetic FORMAT, tagged like a freeze, pins whose
// FORMAT hash is that FORMAT's, and a runner of the verbs through `main`. Nothing real is read: no spec draft, case, reference or private file, and no real task repository. Not a test file.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { cpSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { main } from '../explore.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { packagesInputs, quickWorkspaceFiles } from '../lib/workspace-quick.mjs';
import { FREEZE_TASKS, slugOf } from '../lib/workspaces-sources.mjs';

process.setMaxListeners(0); // `main` arms two signal handlers per call; a test file calls it many times

export const HOME = '/home/nobody';
export const FORMAT = readFileSync(fileURLToPath(new URL('../../lang-bench/test/fixtures/conformance/FORMAT.md', import.meta.url)), 'utf8');
export const sha256 = text => createHash('sha256').update(text).digest('hex');
export const TAG = 'spec-frozen-B-20261118';
const owned = ownedFor({ token: 'bench' });
const EXPLORE = loadExplore().value;

/** An invented spec of task `id`: a title, a build section and one visible sample, which is all the workspace builder reads. */
export const specOf = id => ['# Task ' + id + ' — An invented service', '', '## 5. Build', '', '## 6. Visible samples', '', '### s01', '', '`samples/s01.in`', '', '```text', 'scenario 2', 'start ready port within 10s', '```', '', '`samples/s01.out`', '', '```text', 'transcript 2', 'ready', '```', '', '## 7. Hidden tests', '', 'text', ''].join('\n');

/** The pins as the commands load them, with the FORMAT hash of the six tasks set to `format`'s (the step-2a pin of the real FORMAT is not the invented one's). */
export function pinsFor(format = FORMAT) {
  const tasks = Object.fromEntries(Object.entries(EXPLORE.tasks).map(([id, t]) => [id, FREEZE_TASKS.includes(id) ? { ...t, format: { ...t.format, sha256: sha256(format) } } : t]));
  return { ...EXPLORE, tasks };
}

/** The six tasks as the commands resolve them, from `pinsFor`. */
export const freezeTaskList = (pins = pinsFor()) => FREEZE_TASKS.map(id => ({ id, ...pins.tasks[id] }));

/** A git command in `dir`, in a clean environment (no global configuration), its output trimmed. */
export function git(dir, ...args) {
  const env = { PATH: process.env.PATH, HOME: HOME, GIT_CONFIG_GLOBAL: '/dev/null', GIT_CONFIG_NOSYSTEM: '1', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.invalid', GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.invalid' };
  return execFileSync('git', ['-C', dir, ...args], { env, encoding: 'utf8' }).trim();
}

/** A new owned folder of this test (removed with its claim when the test ends). */
export const folder = (t, what) => owned.ownedDir(t, what);

const put = (root, rel, text) => { mkdirSync(path.dirname(path.join(root, rel)), { recursive: true }); writeFileSync(path.join(root, rel), text); };

/**
 * An invented task repository: `main` holds the six specs and FORMAT (`format` replaces the committed one), and the annotated tag `TAG` is on its tip. -> `{ dir, commit, tag }`. `spec` replaces the spec text of every task.
 */
export function taskRepo(t, { format = FORMAT, spec = specOf } = {}) {
  const dir = folder(t, 'wsrepo');
  git(dir, 'init', '-q', '-b', 'main');
  for (const task of freezeTaskList()) put(dir, task.spec.path, spec(task.id));
  put(dir, 'todo/tasks/FORMAT.md', format);
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '-m', 'the invented task repository');
  git(dir, 'tag', '-a', TAG, '-m', 'the intended annotation');
  return { dir, commit: git(dir, 'rev-parse', 'HEAD'), tag: TAG };
}

/** Run a verb through `main` with the invented pins. -> `{ code, lines }`; `extra` are `main`'s seams. */
export async function run(argv, { pins = pinsFor(), ...extra } = {}) {
  const lines = [];
  const code = await main(argv, { home: HOME, say: line => lines.push(line), exploreFn: () => ok(pins), ...extra });
  return { code, lines };
}

/** The twelve workspaces of the invented repository as a session would get them: `{ 'csharp/04-rule-api': files }`, built straight from `quickWorkspaceFiles`, never through the verbs. */
export function sessionWorkspaces({ format = FORMAT, spec = specOf, pins = pinsFor(format) } = {}) {
  const explore = quickExplore(pins, HOME);
  const inputs = packagesInputs().value;
  const built = {};
  for (const task of freezeTaskList(pins)) {
    for (const stack of ['csharp', 'node']) {
      const r = quickWorkspaceFiles({ explore, task, stack, sources: { spec: spec(task.id), format }, inputs });
      assert.equal(r.ok, true, r.reason);
      built[`${stack}/${slugOf(task)}`] = r.value.files;
    }
  }
  return built;
}

/** `workspaces` into a new folder of the test: -> `{ out, repo, code, lines }`. */
export async function made(t, repoOptions = {}) {
  const repo = taskRepo(t, repoOptions);
  const out = path.join(folder(t, 'wsout'), 'ws');
  const r = await run(['workspaces', '--tag', repo.tag, '--task-repo', repo.dir, '--out', out]);
  return { out, repo, ...r };
}

/** A detached owned worktree of the task repository at its tag commit, as the freeze's workspaces branch is cut from C. -> its path. */
export const worktreeAt = (t, repo) => owned.ownedWorktree(t, repo.dir, 'wswt', { ref: repo.commit }).path;

/** A checkout of the task repository with the twelve workspaces committed on top (what W is): -> `{ dir, repo, head }`. The workspaces are copied by the verb itself. */
export async function committedCheckout(t) {
  const m = await made(t);
  assert.equal(m.code, 0, m.lines.join('\n'));
  const dir = worktreeAt(t, m.repo);
  const copied = await run(['workspaces-copy', '--from', m.out, '--into', dir]);
  assert.equal(copied.code, 0, copied.lines.join('\n'));
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '-m', 'the twelve workspaces');
  return { dir, repo: m.repo, out: m.out, head: git(dir, 'rev-parse', 'HEAD') };
}

/** Change one file of a checkout and commit it. */
export function changeAndCommit(dir, rel, change) {
  const file = path.join(dir, rel);
  writeFileSync(file, change(readFileSync(file)));
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '-m', `a change to ${rel}`);
  return git(dir, 'rev-parse', 'HEAD');
}

/** The copy of a folder tree (for planting a second copy of the workspaces). */
export const copyTree = (from, to) => cpSync(from, to, { recursive: true });
