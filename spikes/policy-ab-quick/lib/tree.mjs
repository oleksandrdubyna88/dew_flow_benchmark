// Building a cell tree: base archive, conventions mount, overlay. All git via exe+args.
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { CONV_CLONE } from './consts.mjs';
import { AUTHOR_ENV, git, gitEnv, gitShow, gitlinkAt } from './git.mjs';
import { assertPolicyDelta, mapDelta, parseLsTree, withGuardEntries } from './mount.mjs';
import { npmInvocation, runOk } from './proc.mjs';

export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

async function extractArchive(clone, rev, cellDir, intoRel) {
  const tarPath = path.join(cellDir, `_archive-${rev.slice(0, 8)}.tar`);
  await git(clone, ['archive', '--format=tar', `--output=${tarPath}`, rev]);
  fs.mkdirSync(path.join(cellDir, intoRel), { recursive: true });
  // cwd = cell dir and only relative paths: GNU tar reads "D:" as a host name.
  await runOk('tar', ['-xf', path.basename(tarPath), '-C', intoRel], { cwd: cellDir, env: gitEnv() });
  fs.rmSync(tarPath);
}

/**
 * P's tree from the story's clone plus the conventions at the commit P's gitlink pins (a plain archive: the reference tree and the
 * scoring copies use it). Returns the pin. `mount: false` extracts P alone and leaves the mount for the caller to make a checkout.
 */
export async function extractBase(cellDir, story, { mount = true } = {}) {
  await extractArchive(story.repoClone, story.P, cellDir, 'tree');
  const pin = await gitlinkAt(story.repoClone, story.P, story.mount);
  if (mount) await extractArchive(CONV_CLONE, pin, cellDir, `tree/${story.mount}`);
  return pin;
}

/** The conventions' files at `rev`, as a plain archive, into `<rootDir>/<mountRel>` (a scoring copy's mount). */
export async function extractConventionsInto(rootDir, mountRel, rev) {
  await extractArchive(CONV_CLONE, rev, rootDir, mountRel);
}

export function writeFileMaking(file, bytes) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, bytes);
}

/** `npm ci` in the story's source folder (network; run AFTER the commit). */
export async function npmCi(treeDir, story) {
  const { exe, args } = npmInvocation(['ci', '--no-audit', '--no-fund']);
  await runOk(exe, args, { cwd: path.join(treeDir, story.srcDir), env: process.env });
}

/** Pure: the argv of the resolver's own dependency install (the SessionStart hook runs the resolver with node and needs it). */
export const resolverInstallArgs = () => ['ci', '--ignore-scripts', '--no-audit', '--no-fund'];

export const mountDirOf = (treeDir, story) => path.join(treeDir, ...story.mount.split('/'));

/** Resolver layout: the conventions' own `npm ci --ignore-scripts` inside the mount (network; run AFTER the commit). */
export async function installResolver(treeDir, story) {
  const { exe, args } = npmInvocation(resolverInstallArgs());
  await runOk(exe, args, { cwd: mountDirOf(treeDir, story), env: process.env });
}

export const walkSha = (rel, bytes) => ({ [rel]: sha256(bytes) });

// ---- the resolver layout's mount is a real git checkout -------------------------------------------------------
// The conventions' resolver (`rules.mjs`, which the story's own `npm run compile` runs through prepare:gate) refuses unless the
// mount is a checkout whose HEAD equals the outer index's gitlink and which has no uncommitted change. So in this layout the
// mount is its own repository - but a SYNTHETIC one, built so that the arms differ in the policy and nothing else:
//   base  = P's pinned conventions (what P's own tests read, and what scoring uses), archived;
//   arm B = the base plus the release's guard files (declared in story.policyFiles); arm A = the base unchanged;
//   one commit, fixed author, date and message in both arms - no history, no tags, no other release's files.
const noCrlf = ['-c', 'core.autocrlf=false'];
export const FIXED_DATE = '2000-01-01T00:00:00Z';
export const MOUNT_COMMIT_MESSAGE = 'conventions';

/** git env with no user/system config: nothing in the commit may depend on this machine. */
export const cleanGitEnv = emptyConfig => gitEnv({ GIT_CONFIG_GLOBAL: emptyConfig });
const commitEnv = emptyConfig => gitEnv({ ...AUTHOR_ENV, GIT_CONFIG_GLOBAL: emptyConfig, GIT_AUTHOR_DATE: FIXED_DATE, GIT_COMMITTER_DATE: FIXED_DATE });

const readIfFile = file => (fs.existsSync(file) ? fs.readFileSync(file) : null);
const showOrNull = async (rev, file) => { try { return await gitShow(CONV_CLONE, rev, file); } catch { return null; } };
const sameBytes = (a, b) => (a === null || b === null ? a === b : sha256(a) === sha256(b));

/** Arm A is DEFINED as the base: its policy files must be byte-identical to the arm A release's. */
export async function assertBaseIsArmA(mountDir, policyFiles, armASha) {
  for (const file of policyFiles) {
    const base = readIfFile(path.join(mountDir, file));
    const released = await showOrNull(armASha, file);
    if (!sameBytes(base, released)) throw new Error(`the base mount's ${file} is not arm A's release (${armASha.slice(0, 8)}): the arms' definitions no longer match the base`);
  }
}

/** Arm B: the release's bytes for each declared policy file; settings.json gets only the guard's hook entries added to the base's. */
export async function overlayArmB(mountDir, policyFiles, armBSha) {
  for (const file of policyFiles) {
    const target = path.join(mountDir, file);
    if (file === 'settings/settings.json') {
      const armSettings = JSON.parse((await gitShow(CONV_CLONE, armBSha, file)).toString('utf8'));
      const baseSettings = JSON.parse(fs.readFileSync(target, 'utf8'));
      writeFileMaking(target, `${JSON.stringify(withGuardEntries(baseSettings, armSettings), null, 2)}\n`);
    } else {
      writeFileMaking(target, await gitShow(CONV_CLONE, armBSha, file));
    }
  }
}

/** The mount's committed file map (path -> mode:blob), and the base pin's, for the delta check. */
const lsTree = async (dir, rev, env) => parseLsTree((await git(dir, ['ls-tree', '-r', rev], env)).toString('utf8'));

/**
 * Overlay the arm's policy onto the archived base mount, make it a one-commit repository, and prove the result: against the base
 * pin, arm A differs in nothing and arm B in exactly the declared policy files. Returns { head, delta }.
 */
export async function buildArmMount(treeDir, story, arm, pin, armShas) {
  const dir = mountDirOf(treeDir, story);
  if (arm === 'A') await assertBaseIsArmA(dir, story.policyFiles, armShas.A);
  else await overlayArmB(dir, story.policyFiles, armShas.B);
  const env = commitEnv(path.join(dir, '.git', 'empty.gitconfig'));
  await git(dir, ['init', '-q', '-b', 'main'], gitEnv());
  fs.writeFileSync(path.join(dir, '.git', 'empty.gitconfig'), '');
  await git(dir, ['config', 'core.autocrlf', 'false'], env);
  await git(dir, [...noCrlf, 'add', '-A'], env);
  await git(dir, [...noCrlf, 'commit', '-q', '-m', MOUNT_COMMIT_MESSAGE], env);
  const head = (await git(dir, ['rev-parse', 'HEAD'], env)).toString('utf8').trim();
  const delta = assertPolicyDelta(arm, mapDelta(await lsTree(CONV_CLONE, pin, gitEnv()), await lsTree(dir, 'HEAD', env)), story.policyFiles);
  return { head, delta };
}

/** The committed file map of a prepared mount (probe: arm A's against arm B's). */
export async function mountFileMap(mountDir) {
  return lsTree(mountDir, 'HEAD', cleanGitEnv(path.join(mountDir, '.git', 'empty.gitconfig')));
}

/** Pure: the sha of the 160000 stage-0 entry in `git ls-files --stage` output, or ''. */
export function gitlinkFromStage(stageText) {
  return /^160000 ([0-9a-f]{40}) 0\t/m.exec(stageText)?.[1] ?? '';
}

/** Record the mount as a gitlink to its HEAD in the tree's index (what `git submodule` would have staged). */
export async function stageMountGitlink(treeDir, story, emptyConfig) {
  const env = cleanGitEnv(emptyConfig);
  const sha = (await git(mountDirOf(treeDir, story), ['rev-parse', 'HEAD'], env)).toString('utf8').trim();
  await git(treeDir, ['update-index', '--add', '--cacheinfo', `160000,${sha},${story.mount}`], env);
  return sha;
}

/** The tree's index gitlink for the mount must be the mount's HEAD and the mount must be clean - exactly the resolver's own check. */
export async function assertMountPinned(treeDir, story, emptyConfig) {
  const dir = mountDirOf(treeDir, story);
  const env = cleanGitEnv(emptyConfig);
  const pinned = gitlinkFromStage((await git(treeDir, ['ls-files', '--stage', '--', story.mount], env)).toString('utf8'));
  const actual = (await git(dir, ['rev-parse', 'HEAD'], env)).toString('utf8').trim();
  if (pinned === '' || pinned !== actual) throw new Error(`the tree's gitlink for ${story.mount} is ${pinned || 'missing'}, the mount's HEAD is ${actual}`);
  const dirty = (await git(dir, [...noCrlf, 'status', '--porcelain', '--untracked-files=normal'], env)).toString('utf8').trim();
  if (dirty !== '') throw new Error(`the mount has uncommitted changes:\n${dirty.slice(0, 600)}`);
  return actual;
}
