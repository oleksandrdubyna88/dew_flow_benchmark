// Fixtures of the two-repository leak check (step 2b): a private side of INVENTED content in a temp home, a bench repository and a task repository made with `git init` in temp folders (never the real ones, never the private
// folders), and one way to run `leakcheck` over them with its output captured. Not a test file.
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { cmdLeakcheck } from '../lib/commands.mjs';
import { exploreOf, quickHome, quickTask, writeQuickHeldout } from './quick-leak-fixture.mjs';

/** The invented needle: two lines of one case's answer, with a distinctive token each. */
export const NEEDLE_LINES = Object.freeze(['recv c "invented-answer-one-for-the-repos-XYZZY-5550123"', 'recv c "invented-answer-two-for-the-repos-QWERTY-8870456"']);
export const NEEDLE = `${NEEDLE_LINES.join('\n')}\n`;
export const TOKENS = Object.freeze(['XYZZY', 'QWERTY', '5550123', '8870456']);

/** Fails when the text holds the needle, any line of it, or a distinctive token of it. */
export const holdsNeedle = text => NEEDLE_LINES.some(l => text.includes(l)) || TOKENS.some(t => text.includes(t));

/** `git` in `repo`, returning stdout (a fixed identity, so a commit needs no configuration). */
export const git = (repo, ...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });

/** Write `files` (`{ rel: text | null }`, null removes) into `repo` and commit them. -> the new commit's sha. */
export function commit(repo, files, message = 'a commit') {
  for (const [rel, content] of Object.entries(files)) {
    const target = path.join(repo, rel);
    if (content === null) git(repo, 'rm', '-q', '-f', rel);
    else { mkdirSync(path.dirname(target), { recursive: true }); writeFileSync(target, content); }
  }
  git(repo, 'add', '-A');
  git(repo, 'commit', '-q', '-m', message);
  return git(repo, 'rev-parse', 'HEAD').trim();
}

/** A temp repository on branch `main` with one commit; `origin/main` is that commit unless `published` is false. -> the path. */
export function newRepo(files = { 'README.md': 'a public readme\n' }, { published = true } = {}) {
  const repo = tmpDir('r-repo');
  git(repo, 'init', '-q', '-b', 'main');
  commit(repo, files, 'init');
  if (published) git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD');
  return repo;
}

/** The private side: task 04 with the invented needle as its case answer, in a temp home, and the pins that name only it. */
export function privateSide() {
  const home = quickHome();
  writeQuickHeldout({ home, task: quickTask('04'), cases: [{ id: 'h001', input: 'scenario 2\n', output: `transcript 2\n${NEEDLE}exit 0\n` }], traps: ['h001'] });
  return { home, explore: exploreOf(['04']) };
}

/** `leakcheck` over the bench repository `benchRepo` with `values` (the parsed options) and `env`: `{ exit, said, progress }`, the two outputs as text. */
export async function runCheck({ home, explore, benchRepo, values = {}, env = {} }) {
  const said = [];
  const progress = [];
  const io = { home, repoRoot: benchRepo, say: l => said.push(l), progress: l => progress.push(l), exploreFn: () => ok(explore), env };
  const exit = await cmdLeakcheck(values, io);
  return { exit, said: said.join('\n'), progress: progress.join('\n') };
}
