// Building a cell tree: base archive, conventions submodule, overlay. All git via exe+args.
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { CONV_CLONE, CREDS_CLONE, MOUNT, P, WEB_DIR } from './consts.mjs';
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

/** P's tree from the creds clone plus the conventions submodule at its pinned sha. Returns the pin. */
export async function extractBase(cellDir) {
  await extractArchive(CREDS_CLONE, P, cellDir, 'tree');
  const pin = await gitlinkAt(CREDS_CLONE, P, MOUNT);
  await extractArchive(CONV_CLONE, pin, cellDir, `tree/${MOUNT}`);
  return pin;
}

export function writeFileMaking(file, bytes) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, bytes);
}

/** `npm ci` in the extension folder (network; run AFTER the commit). */
export async function npmCi(treeDir) {
  const { exe, args } = npmInvocation(['ci', '--no-audit', '--no-fund']);
  await runOk(exe, args, { cwd: path.join(treeDir, WEB_DIR), env: process.env });
}

export const walkSha = (rel, bytes) => ({ [rel]: sha256(bytes) });
