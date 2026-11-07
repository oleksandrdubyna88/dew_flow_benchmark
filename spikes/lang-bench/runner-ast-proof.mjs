#!/usr/bin/env node
// The `ast` proof of a runner change against its merge base (F1.S1 (f)): run it in the pull request, record its line in the pull request and in research/module_tests.md.
//
//   node spikes/lang-bench/runner-ast-proof.mjs --base <ref> [--repo <dir>]
//
// It writes the base's `spikes/lang-bench/runner/*.py` (`git show <ref>:<path>`) to an owned scratch folder and compares them with this working copy's runner by `ast.dump` with the docstrings out and the version constant masked.
// Exit 0 equal | 1 a difference (the modules are named) | 2 usage or a git or Python failure.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { cleanupScope, ownedFor } from '../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../minigit-bench/lib/result.mjs';
import { runBounded } from '../minigit-bench/lib/run.mjs';
import { astProof, proofHolds, proofLine } from './lib/runner-ast-proof.mjs';

const SPIKE = fileURLToPath(new URL('.', import.meta.url));
const RUNNER_PATH = 'spikes/lang-bench/runner';
const GIT_MS = 60_000;

async function git(repo, args) {
  const ran = await runBounded('git', ['-C', repo, ...args], { timeoutMs: GIT_MS });
  return ran.ok && ran.value.code === 0 ? ok(ran.value.stdout) : fail(`git ${args[0]} failed: ${ran.ok ? ran.value.stderr.trim().slice(0, 200) : ran.reason}`);
}

/** Write the base's runner modules into `dir`. -> `ok(dir)` | `fail`. */
async function extractBase({ repo, ref, dir }) {
  const listed = await git(repo, ['ls-tree', '--name-only', ref, `${RUNNER_PATH}/`]);
  if (!listed.ok) return listed;
  mkdirSync(dir, { recursive: true });
  for (const file of listed.value.split('\n').filter(name => name.endsWith('.py'))) {
    const shown = await git(repo, ['show', `${ref}:${file}`]);
    if (!shown.ok) return shown;
    writeFileSync(path.join(dir, path.basename(file)), shown.value);
  }
  return ok(dir);
}

/** The proof of the base against this working copy, with the base written to `dir`. -> `ok(proof)` | `fail`. */
async function proofAgainst({ repo, ref, dir }) {
  const base = await extractBase({ repo, ref, dir });
  return base.ok ? astProof({ baseDir: dir, headDir: path.join(SPIKE, 'runner') }) : base;
}

async function main() {
  const { values } = parseArgs({ options: { base: { type: 'string' }, repo: { type: 'string' } } });
  if (!values.base) { process.stderr.write('runner-ast-proof: --base <ref> is required\n'); return 2; }
  const scope = cleanupScope();
  try {
    const proof = await proofAgainst({ repo: values.repo ?? path.resolve(SPIKE, '..', '..'), ref: values.base, dir: ownedFor({ token: 'bench' }).ownedDir(scope, 'astproof') });
    if (!proof.ok) { process.stderr.write(`runner-ast-proof: ${proof.reason}\n`); return 2; }
    process.stdout.write(`${proofLine(proof.value)}\n`);
    return proofHolds(proof.value) ? 0 : 1;
  } finally {
    scope.close();
  }
}

process.exitCode = await main();
