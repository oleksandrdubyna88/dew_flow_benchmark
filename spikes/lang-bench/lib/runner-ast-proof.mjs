// The `ast` proof of a runner change (F1.S1 (f)): a change that is meant to touch comments and docstrings only is shown to touch nothing else. For every module of two runner folders, `ast.dump` of the tree with the docstrings
// taken out and the value of `RUNNER_VERSION` masked (tools/runner_text.py `dump`) must be equal. Comments are not in the tree, so they cannot differ. It is a tool, not a permanent test: the base it compares with moves.
import { readdirSync } from 'node:fs';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { pythonReading } from './runner-text.mjs';

const modulesIn = dir => readdirSync(dir).filter(name => name.endsWith('.py')).sort();

/** `ok({ dumps: { "<name>": dump | { error } } })` of the modules of `dir`. */
async function dumpsOf(dir) {
  const names = modulesIn(dir);
  const read = names.length === 0 ? ok({}) : await pythonReading('dump', names.map(name => path.join(dir, name)));
  return read.ok ? ok(Object.fromEntries(names.map(name => [name, read.value[path.join(dir, name)]]))) : read;
}

const same = (a, b) => a.error === undefined && b.error === undefined && a.dump === b.dump;

/** `ok({ modules, differ, added, removed })` comparing the modules of `baseDir` with those of `headDir`: `differ` lists a module whose dump differs (or does not parse), `added` and `removed` the modules in one folder only. */
export async function astProof({ baseDir, headDir }) {
  const [base, head] = [await dumpsOf(baseDir), await dumpsOf(headDir)];
  if (!base.ok || !head.ok) return base.ok ? head : base;
  const both = Object.keys(head.value).filter(name => name in base.value);
  return ok({
    modules: both.length,
    differ: both.filter(name => !same(base.value[name], head.value[name])),
    added: Object.keys(head.value).filter(name => !(name in base.value)),
    removed: Object.keys(base.value).filter(name => !(name in head.value)),
  });
}

export const proofHolds = result => result.differ.length === 0 && result.added.length === 0 && result.removed.length === 0;
export const proofLine = result => `runner-ast-proof: ${result.modules} module(s) compared; differ: ${result.differ.join(', ') || 'none'}; added: ${result.added.join(', ') || 'none'}; removed: ${result.removed.join(', ') || 'none'}`;
