// The hash pins of `inputs/explore.json`, checked against the disk before anything runs (plan risk 2: a pin that moved is a dated amendment, never a quiet change): the CLI copy, python3, the private test
// notes, the installed TypeScript's integrity and the two cache snapshots' tree hashes. The version pins (node, SDK, TypeScript, CLI) are probed inside a box by v1's preflight; the spec and FORMAT
// texts are checked by `loadSources`. A sentence per problem; `[]` means every pin holds.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import os from 'node:os';
import { treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { installedIntegrity } from './prepare.mjs';
import { pinnedPaths } from './inputs.mjs';
import { underHome } from './paths.mjs';
import { pythonProblem } from './world.mjs';

const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');

/** `null` when the file is there and hashes to `want`, else the sentence. */
function fileProblem(what, file, want) {
  if (!existsSync(file)) return `${what} ${file} is missing`;
  const got = sha256File(file);
  return got === want ? null : `${what} ${file} hashes to ${got.slice(0, 12)}, the pin says ${want.slice(0, 12)}`;
}

const treeProblem = (what, dir, want) => (treeHash(dir) === want ? null : `the ${what} snapshot ${dir} no longer hashes to its registered ${want.slice(0, 12)}`);

/**
 * -> the sentences of every pin that does not hold. `snapshots` is `world.snapshots` (the registered dirs and records); `explore` the pins. Reads files; the snapshot hashing walks both trees (about a second).
 */
export function pinProblems({ explore, home = os.homedir(), snapshots }) {
  const paths = pinnedPaths(explore, home);
  return [
    fileProblem('the pinned CLI copy', paths.claudeBinary, explore.cli.sha256),
    pythonProblem(explore),
    fileProblem('the private test notes', underHome(explore.privateNotes.relativePath.split('/'), home), explore.privateNotes.sha256),
    installedIntegrity(paths.tsDir) === explore.typescript.integrity ? null : `the installed TypeScript's integrity is not the recorded one (${explore.typescript.integrity?.slice(0, 20)}...)`,
    treeProblem('node', snapshots.node.dir, snapshots.node.record.treeSha256),
    treeProblem('csharp', snapshots.csharp.dir, explore.csharpSnapshot.treeSha256),
  ].filter(Boolean);
}

/** `ok(null)` when every hash pin holds, else a `fail` naming every one that does not. */
export function pinsOk(args) {
  const problems = pinProblems(args);
  return problems.length === 0 ? ok(null) : fail(`${problems.length} pin(s) do not hold: ${problems.join('; ')}. A pin moves by a dated amendment before pair 1, never between pairs`);
}
