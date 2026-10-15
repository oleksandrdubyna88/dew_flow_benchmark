// Building a cell tree: base archive, conventions mount, overlay. All git via exe+args.
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { CONV_CLONE } from './consts.mjs';
import { git, gitEnv, gitlinkAt } from './git.mjs';
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
// mount is a clone of the local conventions clone, detached at the ARM's commit, and the outer tree records that commit as a gitlink.
const noCrlf = ['-c', 'core.autocrlf=false'];

/** git env with no user/system config: a clone must not depend on this machine's autocrlf or credential helpers. */
export const cleanGitEnv = emptyConfig => gitEnv({ GIT_CONFIG_GLOBAL: emptyConfig });

/** Clone the conventions into the (empty or absent) mount directory and detach at `sha`. Never writes the original clone. */
export async function cloneMountAt(treeDir, story, sha, emptyConfig) {
  const dir = mountDirOf(treeDir, story);
  if (fs.existsSync(dir) && fs.readdirSync(dir).length > 0) throw new Error(`the mount ${dir} is not empty before the clone`);
  fs.mkdirSync(path.dirname(dir), { recursive: true });
  const env = cleanGitEnv(emptyConfig);
  await git(path.dirname(dir), [...noCrlf, 'clone', '-q', '--no-local', '--no-checkout', CONV_CLONE, dir], env);
  await git(dir, ['config', 'core.autocrlf', 'false'], env);
  await git(dir, ['checkout', '-q', '--detach', sha], env);
  await git(dir, ['remote', 'remove', 'origin'], env); // nothing in the session can fetch from the original
  return (await git(dir, ['rev-parse', 'HEAD'], env)).toString('utf8').trim();
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
