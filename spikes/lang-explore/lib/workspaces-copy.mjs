// The separate, reviewed copy step of the freeze (F2.S2 (d), amendment 2): `explore.mjs workspaces-copy --from <dir> --into <task repository worktree>` puts the twelve folders of `workspaces` into a worktree cut from the
// tag's commit and touches nothing else. It refuses a worktree with any other change, and a HEAD that is not the commit the folders were made from. It is RESUMABLE: a folder already there that equals the source by tree hash is
// skipped and named, one that differs is refused (and nothing is copied), the others are copied one by one, each through an owned staging folder inside the worktree (same file system), its tree hash checked against the
// manifest, then one rename. A restart first removes the staging folders whose owner is gone, which is why a kill between two folders leaves only whole folders and one removable staging folder.
import { cpSync, existsSync, lstatSync, mkdirSync, renameSync } from 'node:fs';
import path from 'node:path';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { gitText, repoRootOf } from './leak-git.mjs';
import { readManifest } from './workspaces-manifest.mjs';
import { runSteps } from './workspaces-pipeline.mjs';
import { sweepAndName } from './workspaces-stale.mjs';
import { owned } from './workspaces-owned.mjs';
import { compareFiles, readFolderFiles, treeHashOfFiles } from './workspaces-tree.mjs';

const under = (file, folders) => folders.some(f => file === f || file.startsWith(`${f}/`));

/** The paths `git status` lists, from its NUL-separated porcelain text (the origin of a rename is skipped). */
export function statusPaths(text) {
  const parts = text.split('\0').filter(p => p !== '');
  const paths = [];
  for (let i = 0; i < parts.length; i += 1) {
    paths.push(parts[i].slice(3));
    if (/^[RC]/.test(parts[i])) i += 1;
  }
  return paths;
}

/** The sentence refusing a worktree with a change outside the twelve folders (an untracked file, a staging folder of a live run, a modified file), or null. */
async function dirtyProblem({ into, folders }) {
  const status = await gitText(into, ['status', '--porcelain=v1', '-z', '--untracked-files=all'], 'the status of the worktree');
  if (!status.ok) return status;
  const other = statusPaths(status.value).filter(p => !under(p, folders));
  return other.length === 0 ? ok({}) : fail(`the worktree ${into} is not clean: ${other.slice(0, 5).join(', ')}${other.length > 5 ? ` and ${other.length - 5} more` : ''}: \`workspaces-copy\` touches nothing but the twelve workspace folders`);
}

/** HEAD of the worktree must be the commit the workspaces were made from. */
async function headProblem({ into, manifest }) {
  const head = await gitText(into, ['rev-parse', '--verify', 'HEAD^{commit}'], 'HEAD of the worktree');
  if (!head.ok) return head;
  return head.value.trim() === manifest.commit ? ok({}) : fail(`HEAD of ${into} is ${head.value.trim().slice(0, 12)}, not ${manifest.commit.slice(0, 12)}, the commit the workspaces were made from: cut the workspaces branch from that commit`);
}

/** Each source folder, read: it must still hash to the manifest's tree hash. -> `ok({ sources: { folder: files } })` | `fail`. */
function readSources({ from, manifest }) {
  const sources = {};
  for (const [folder, want] of Object.entries(manifest.trees)) {
    const read = readFolderFiles(path.join(from, folder));
    if (!read.ok) return read;
    if (treeHashOfFiles(read.value.files) !== want) return fail(`the source folder ${path.join(from, folder)} no longer hashes to the manifest's tree hash: it was changed after \`workspaces\` made it`);
    sources[folder] = read.value.files;
  }
  return ok({ sources });
}

/** What an existing target is: `skip` when equal to the source by tree hash, else a refusal. */
function existingPlan({ folder, target, source }) {
  const read = readFolderFiles(target);
  if (!read.ok) return { folder, refusal: `${folder}: ${read.reason}` };
  const differences = compareFiles(source, read.value.files);
  return differences.length === 0 ? { folder, action: 'skip' } : { folder, refusal: `${folder} is already in the worktree and differs from the workspace made for it (${differences.slice(0, 3).join('; ')}): nothing is copied` };
}

/** What the target of one folder is: `copy` (absent), `skip` (equal by tree hash) or a refusal sentence. */
function planOf({ into, folder, sources }) {
  const target = path.join(into, folder);
  if (!existsSync(target)) return { folder, action: 'copy' };
  return lstatSync(target).isDirectory() ? existingPlan({ folder, target, source: sources[folder] }) : { folder, refusal: `${folder} exists in the worktree and is not a folder` };
}

/** The plan of the twelve; one refusal refuses all. -> `ok({ plan })` | `fail`. */
function planAll({ into, manifest, sources }) {
  const plan = Object.keys(manifest.trees).map(folder => planOf({ into, folder, sources }));
  const refused = plan.filter(p => p.refusal !== undefined);
  return refused.length === 0 ? ok({ plan }) : fail(refused.map(p => p.refusal).join('\n'));
}

/** Copy one folder through an owned staging folder: copy, hash check, one rename. `hooks.beforeRename(i)` and `hooks.afterRename(i)` are the seams a test plants a kill at. -> `ok(folder)` | `fail`. */
async function copyOne({ from, into, manifest, folder, index, hooks }) {
  const scope = cleanupScope();
  try {
    const stage = owned.ownedDir(scope, 'stage', { parent: into });
    cpSync(path.join(from, folder), stage, { recursive: true, verbatimSymlinks: true });
    const read = readFolderFiles(stage);
    if (!read.ok || treeHashOfFiles(read.value.files) !== manifest.trees[folder]) return fail(`the copy of ${folder} does not hash to the manifest's tree hash: it was not put in place`);
    hooks.beforeRename?.(index);
    mkdirSync(path.dirname(path.join(into, folder)), { recursive: true });
    renameSync(stage, path.join(into, folder));
    hooks.afterRename?.(index);
    return ok(folder);
  } finally {
    await scope.close();
  }
}

/** The folders to copy, in order, each copied once. -> `ok({ copied: [folder] })` | `fail`. */
async function copyAll({ from, into, manifest, plan, hooks, log }) {
  const copied = [];
  for (const [index, p] of plan.entries()) {
    if (p.action === 'skip') { log(`skipped ${p.folder}: already in the worktree and equal to the workspace by tree hash`); continue; }
    const r = await copyOne({ from, into, manifest, folder: p.folder, index: copied.length, hooks });
    if (!r.ok) return r;
    log(`copied ${p.folder}`);
    copied.push(p.folder);
  }
  return ok({ copied, skipped: plan.filter(p => p.action === 'skip').map(p => p.folder) });
}

const sweep = ({ into, log }) => { sweepAndName({ parent: into, log }); return ok({}); };

const topOf = async ({ into }) => { const top = await repoRootOf(into); return top.ok ? ok({ into: top.value }) : top; };

/** `ok({ copied, skipped })` | `fail`. `from` is the folder `workspaces` made, `into` the top folder of a worktree of the task repository. */
export function copyWorkspaces({ from, into, hooks = {}, log = () => {} }) {
  const manifestStep = ({ from: dir }) => { const m = readManifest(dir); return m.ok ? ok({ manifest: m.value }) : m; };
  const folders = state => ({ folders: Object.keys(state.manifest.trees) });
  return runSteps([
    manifestStep, readSources, topOf, headProblem, sweep, s => ok(folders(s)), dirtyProblem, planAll,
    s => copyAll({ ...s, hooks, log }),
  ], { from: path.resolve(from), into: path.resolve(into) });
}
