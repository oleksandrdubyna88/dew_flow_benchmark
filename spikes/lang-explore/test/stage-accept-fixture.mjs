// The fixtures of `stage-sync --accept-harness` (F3.S1 (b), PR 5; epoch 11): a series whose tag holds `lib/old.mjs`, the harness fix as a commit of `main`, its dated line taken onto the series' branch, and the helpers that read what an
// acceptance leaves behind. Shared by the guard's tests and by the take's own (a kill at every step). Every repository is a temporary one with a bare origin; nothing is real.
import { readFileSync, rmSync } from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { commit } from '../../minigit-bench/test/prereg-fixtures.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { STAGE_PREREG_CONFIG } from '../lib/stage-prereg.mjs';
import { stageSync } from '../lib/stage-branch.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { PRE, makeSeries } from './stage-series-fixture.mjs';
import { cmdStageSync } from '../lib/stage-prereg-commands.mjs';
import { AMENDMENTS, AMENDMENTS_FILE, git, onMain, stageFixture, write } from './stage-prereg-fixture.mjs';

export const FIX_FILE = 'spikes/lang-explore/lib/x.mjs';
export const OLD_FILE = 'spikes/lang-explore/lib/old.mjs';
export const NEW_FILE = 'spikes/lang-bench/tools/new.mjs';
export const NOTES_FILE = 'docs/notes.md';
export const REASON = 'the fix before the first data session (G-3)';

/** What the fix does to the harness (three ways: a file edited, a file deleted, a file added) and, outside it, to a note. */
export const fixChange = repo => () => { write(repo, FIX_FILE, '// the fix, reviewed on main\n'); rmSync(path.join(repo, OLD_FILE)); write(repo, NEW_FILE, '// added by the fix\n'); write(repo, NOTES_FILE, 'notes changed on main\n'); };

/** The fix as a commit on a branch of the origin that is not main (an unmerged pull request), back on the series' branch. */
export function onWip(f, change) {
  git(f.repo, 'checkout', '-q', '-b', 'wip', 'origin/main');
  change();
  const sha = commit(f.repo, 'a fix that is not merged');
  git(f.repo, 'push', '-q', 'origin', 'wip');
  git(f.repo, 'checkout', '-q', 'series');
  return sha;
}

export const datedLine = ({ identity, commit: sha }, reason = `commit:${sha} ${REASON}`) => `2026-11-26 | instrument | harness:${identity.slice(0, 12)} | ${reason}`;

/**
 * A series whose tag holds `lib/old.mjs`, the fix as a commit of `main` (or of `wip` with `merged: false`), and, on main after it, the dated line (`line(fix)` is its text) which `stage-sync` has already taken onto the series' branch.
 * -> `{ f, commit, identity, hex }`: `identity` is the harness identity of the fix's folders and `hex` its 12 hex.
 */
export async function fixedOnMain({ line = datedLine, merged = true, plus = () => {} } = {}) {
  const f = await stageFixture({ extra: repo => write(repo, OLD_FILE, '// old, deleted by the fix\n') });
  const change = () => { fixChange(f.repo)(); plus(f.repo); };
  const sha = merged ? onMain(f, change, 'the harness fix') : onWip(f, change);
  const identity = (await harnessIdentity(f.repo, STAGE_PREREG_CONFIG, sha)).value;
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}${line({ identity, commit: sha })}\n`), 'the dated line on main');
  assert.equal((await stageSync({ repo: f.repo })).ok, true, 'the dated line is taken like any listed file');
  return { f, commit: sha, identity, hex: identity.slice(0, 12) };
}

/** `stage-sync --accept-harness <value>` through the command: `{ code, lines }` (the lines joined with `|`). */
export async function accept(f, value, io = {}) {
  const lines = [];
  const code = await cmdStageSync({ 'accept-harness': value }, { repo: f.repo, say: l => lines.push(l), ...io });
  return { code, lines: lines.join('|') };
}

/** The state a refused acceptance must leave: the branch where it was, nothing staged, a clean tree, the fix's file as the tag had it. */
export const stateOf = f => [git(f.repo, 'rev-parse', 'HEAD'), git(f.repo, 'diff', '--cached', '--name-only'), git(f.repo, 'status', '--porcelain', '--untracked-files=all'), git(f.repo, 'show', `${f.sha}:${FIX_FILE}`) === readFileSync(path.join(f.repo, FIX_FILE), 'utf8').trim()];

export async function submoduleFixture() {
  const sub = tmpDir('rules-sub');
  git(sub, 'init', '-q', '-b', 'main');
  write(sub, 'rules.md', 'the shared rules\n');
  commit(sub, 'the rules');
  const f = await stageFixture({ extra: repo => {
    rmSync(path.join(repo, '.agents', 'conventions'), { recursive: true });
    git(repo, '-c', 'protocol.file.allow=always', 'submodule', 'add', '-q', sub, '.agents/conventions');
  } });
  return { f, sub: path.join(f.repo, '.agents', 'conventions') };
}

/** A series whose harness fix moves the shared rules' submodule to a new commit, the dated line taken, the checkout still at the old commit: `{ f, sub, old, fixed, identity, hex }`. */
export async function fixedSubmodule() {
  const { f, sub } = await submoduleFixture();
  const old = git(sub, 'rev-parse', 'HEAD');
  const next = onMain(f, () => { write(sub, 'rules2.md', 'a new rule\n'); commit(sub, 'a new rules commit'); git(f.repo, 'add', '.agents/conventions'); }, 'main records the new rules commit');
  const fixed = git(sub, 'rev-parse', 'HEAD');
  git(sub, 'checkout', '-q', '--detach', old);
  const identity = (await harnessIdentity(f.repo, STAGE_PREREG_CONFIG, next)).value;
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}${datedLine({ identity, commit: next })}\n`), 'the dated line');
  assert.equal((await stageSync({ repo: f.repo })).ok, true);
  return { f, sub, old, fixed, identity, hex: identity.slice(0, 12) };
}

/** A paid Stage 1 series whose state records the branch `branch` (default: the fixture's `series`) and the tag's commit `f.sha`; `home` puts it under an existing explore root. -> `{ io, dir }`. */
export const seriesOf = (f, { branch = 'series', home, commit = f.sha, rand = () => 'ab12' } = {}) => makeSeries({ ...(home === undefined ? {} : { home }), stage: { rand, initGuardFn: async () => ok({ ...PRE, branch, preregCommit: commit }) } });
