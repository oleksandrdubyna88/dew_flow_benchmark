// Fixtures of the leak-scan tests: a temp git repository with committed files and history, and the structured material of fixture held-out folders. Not a test file.
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadHeldout } from '../lib/heldout.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { buildMaterial } from '../lib/leak-material.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { handcheckOf, toyCases, writeHeldout } from './olympiad-fixture.mjs';

export const EXPLORE = loadExplore().value;
export const task = id => resolveTask(EXPLORE, id).value;

/** `git` in `repo`, returning stdout. */
export const git = (repo, ...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });

/** A temp repository with one commit holding `files` (`{ rel: text | Buffer }`). -> the path. */
export function repoWith(files = { 'README.md': 'a public readme\n' }) {
  const repo = tmpDir('o-scan-repo');
  git(repo, 'init', '-q');
  commitFiles(repo, files, 'init');
  return repo;
}

/** Write `files` into `repo` (a `null` removes the file) and commit them with `message`. -> the new commit's sha. */
export function commitFiles(repo, files, message) {
  for (const [rel, content] of Object.entries(files)) {
    const target = path.join(repo, rel);
    if (content === null) git(repo, 'rm', '-q', '-f', rel);
    else { mkdirSync(path.dirname(target), { recursive: true }); writeFileSync(target, content); }
  }
  git(repo, 'add', '-A');
  git(repo, 'commit', '-q', '-m', message);
  return git(repo, 'rev-parse', 'HEAD').trim();
}

/**
 * The structured material of fixture held-out folders: `specs` is `{ taskId: writeHeldout options }`. Each task gets a home of its own folder; `shingles` maps a task to its shingles text. -> `{ material, tasks }`.
 */
export function materialOf(specs, { shingles = {}, runnerLines = new Set(), hash = undefined, baselines = {}, born = {} } = {}) {
  const home = tmpDir('o-scan-home');
  const tasks = Object.entries(specs).map(([id, over]) => {
    writeHeldout({ home, task: task(id), cases: toyCases(3), brute: 'x = 1\n', handcheck: handcheckOf(5), mutants: {}, ...over });
    const held = loadHeldout(home, task(id));
    if (!held.ok) throw new Error(held.reason);
    return { id, heldout: held.value, shinglesText: shingles[id] ?? '', baselineCommit: baselines[id] ?? null, heldoutBorn: born[id] ?? '2999-01-01T00:00:00.000Z' };
  });
  const built = buildMaterial({ tasks, runnerLines, ...(hash ? { hash } : {}) });
  return { built, material: built.ok ? built.value : null, tasks };
}

/** A case of a fixture held-out folder: `lines` become the lines of `cases/<id>.out` (with a trailing newline); the input is a harmless short line. */
export const caseWith = (id, lines) => ({ id, input: 'short\n', output: `${lines.join('\n')}\n` });
