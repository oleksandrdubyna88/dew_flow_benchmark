// Fixtures for the push check's tests: throw-away git repositories in owned folders (common.test-leftovers), synthetic content only.
// A task repository fixture has a bare `origin` on disk, so `--remotes=origin` means what it means on GitHub. No test here touches a
// network or the private area.
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const MOUNT_OWNED = new URL('../../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const owned = await import(MOUNT_OWNED.href).then(m => m.ownedFor({ token: 'bench' }), () => null);

/** A new empty folder, removed when the test `t` ends: from the owned-name helper when the conventions mount is present. */
export function ownedFolder(t, what) {
  if (owned !== null) return owned.ownedDir(t, what);
  // the mount is absent (CI checks no submodule out): a plain temp folder, still removed by the test
  const dir = mkdtempSync(path.join(os.tmpdir(), `testsToDelete-${what}-`));
  t.after(() => rmSync(dir, { recursive: true, force: true }));
  return dir;
}

const GIT_ENV = Object.freeze({
  PATH: process.env.PATH, HOME: os.homedir(), LANG: 'C.UTF-8', GIT_CONFIG_NOSYSTEM: '1', GIT_TERMINAL_PROMPT: '0',
  GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.invalid', GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.invalid',
});

/** `git -C dir args...` -> trimmed stdout; a non-zero exit throws with git's stderr (a fixture that cannot be built is a broken test). */
export function git(dir, ...args) {
  const r = spawnSync('git', ['-C', dir, '-c', 'commit.gpgsign=false', '-c', 'protocol.file.allow=always', ...args], { encoding: 'utf8', env: GIT_ENV, timeout: 60_000 });
  if (r.status !== 0) throw new Error(`git ${args.join(' ')} failed in ${dir}: ${r.stderr}`);
  return r.stdout.trim();
}

export function writeFiles(dir, files) {
  for (const [rel, content] of Object.entries(files)) {
    mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true });
    writeFileSync(path.join(dir, rel), content);
  }
}

/** Commit `files` (path -> text; null removes the path) on the current branch of `dir`; returns the new sha. */
export function commit(dir, message, files = {}) {
  for (const [rel, content] of Object.entries(files)) {
    if (content === null) git(dir, 'rm', '-q', '--', rel); else writeFiles(dir, { [rel]: content });
  }
  git(dir, 'add', '-A');
  git(dir, 'commit', '-q', '--allow-empty', '-m', message);
  return git(dir, 'rev-parse', 'HEAD');
}

/** A bare repository whose `main` holds one commit with a LICENSE, like the GitHub remote. Returns its path. */
export function makeOrigin(t) {
  const root = ownedFolder(t, 'origin');
  const seed = path.join(root, 'seed');
  mkdirSync(seed);
  git(seed, 'init', '-q', '-b', 'main');
  commit(seed, 'Initial commit', { LICENSE: 'MIT License\n\nSynthetic licence text.\n' });
  const bare = path.join(root, 'origin.git');
  git(root, 'clone', '-q', '--bare', seed, bare);
  return bare;
}

/** A local task repository with an UNRELATED history from `origin` (as the real ones), `origin` set and fetched. Returns its path. */
export function makeTaskRepo(t, { origin, files = { 'README.md': 'a task repository\n' } } = {}) {
  const dir = path.join(ownedFolder(t, 'task'), 'repo');
  mkdirSync(dir);
  git(dir, 'init', '-q', '-b', 'main');
  commit(dir, 'chore: skeleton', files);
  if (origin) { git(dir, 'remote', 'add', 'origin', origin); git(dir, 'fetch', '-q', 'origin'); }
  return dir;
}
