#!/usr/bin/env node
// Pins the CLI of Stage 1 (F3.S1 (b), PR 5; D8's re-pin, taken once: D47): copies the named CLI release to `~/rsd/lang-v2/toolchains/claude-<version>` (mode 0555, the hash re-checked: v1's copy step `installCli`, run over a temp file and
// renamed into place, because v1's own copy goes straight to the target and a kill in the middle would leave a truncated file at the pinned name; a file already there is the release (already pinned, nothing written) or is refused naming both hashes) and writes its version, its
// path and its sha256 into the `cli` entry of `inputs/explore.json`, whose model and effort stay. The olympiad series are closed, so nothing else reads that entry for a paid run. `--check` writes and copies nothing: it exits 1 when the
// entry does not name a copy that is there and hashes to it.
//   node spikes/lang-explore/prereg/pin-cli.mjs --version <x.y.z> [--versions-dir <dir>] [--explore <file>] [--home <dir>] [--check]
// Exit 0 pinned (or checked equal) | 1 `--check` differs | 4 usage | 5 the release is not there or the copy failed.
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, renameSync, rmSync, statSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { installCli } from '../../minigit-bench/lib/bare-init.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS } from '../lib/paths.mjs';
import { pinnedPaths } from '../lib/inputs.mjs';
import { fileProblem } from '../lib/pins.mjs';
import { EXIT, parseFlags, said, writeAtomically } from './generator.mjs';

const VERSION = /^\d+\.\d+\.\d+$/;
const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');
const defaultVersionsDir = home => path.join(home, '.local', 'share', 'claude', 'versions');
const copyPathParts = version => ['rsd', 'lang-v2', 'toolchains', `claude-${version}`];

/** The `cli` block of `explore.json` as text, in the layout the file has: one key a line, the path parts on one line. */
export const cliBlockText = cli => `"cli": {\n    "version": ${JSON.stringify(cli.version)},\n    "pathParts": ${JSON.stringify(cli.pathParts).replace(/","/g, '", "')},\n    "sha256": ${JSON.stringify(cli.sha256)},\n    "model": ${JSON.stringify(cli.model)},\n    "effort": ${JSON.stringify(cli.effort)}\n  }`;

/** `explore.json`'s text with its `cli` block replaced, every other byte as it was. -> `ok(text)` | `fail` when the text has no `cli` block or the result is not the same JSON but for that entry. */
export function withCli(text, cli) {
  const found = /"cli": \{[^{}]*\}/.exec(text);
  if (found === null) return fail('explore.json has no "cli" block of flat keys to replace');
  const next = `${text.slice(0, found.index)}${cliBlockText(cli)}${text.slice(found.index + found[0].length)}`;
  const [was, now] = [JSON.parse(text), JSON.parse(next)];
  return JSON.stringify({ ...was, cli: null }) === JSON.stringify({ ...now, cli: null }) && JSON.stringify(now.cli) === JSON.stringify(cli) ? ok(next) : fail('the rewritten explore.json differs from the old one in more than its cli entry');
}

/** The sentence for an explore.json with no `cli` block holding the model and the effort the pin keeps, or null. */
const keptProblem = existing => (existing?.model === undefined || existing.effort === undefined ? 'explore.json holds no "cli" block with the model and the effort to keep: nothing was pinned' : null);

/** The pin to make: the copy to install and the `cli` entry to write. -> `ok({ copy, cli })` | `fail`. */
export function pinPlan({ version, versionsDir, home, existing }) {
  const kept = keptProblem(existing);
  if (kept) return fail(kept);
  if (!VERSION.test(version ?? '')) return fail(`--version must be x.y.z, got '${version ?? ''}'`, { exit: EXIT.usage });
  const from = path.join(versionsDir, version);
  if (!existsSync(from)) return fail(`the release ${version} is not at ${from}: the operator's auto-update may have removed it`);
  const sha256 = sha256File(from);
  const parts = copyPathParts(version);
  return ok({ copy: { from, to: path.join(home, ...parts), sha256 }, cli: { version, pathParts: parts, sha256, model: existing.model, effort: existing.effort } });
}

/** The sentences where `explore.json`'s `cli` entry is not a copy that is there, read-only and hashes to it. `[]` when it is. */
export function cliPinProblems({ explore, home }) {
  const file = pinnedPaths(explore, home).claudeBinary;
  const mode = existsSync(file) && (statSync(file).mode & 0o777) !== 0o555 ? [`the pinned CLI copy ${file} is not mode 0555`] : [];
  return [fileProblem('the pinned CLI copy', file, explore.cli.sha256), ...mode].filter(Boolean);
}

/** `explore.json` as `{ text, json }`: `ok` | `fail` naming the file when it cannot be read or is not JSON. */
function readExplore(file) {
  try {
    const text = readFileSync(file, 'utf8');
    return ok({ text, json: JSON.parse(text) });
  } catch (e) {
    return fail(`${file} cannot be read: ${e.code ?? e.message}`);
  }
}

async function check({ values, say, home }) {
  const explore = readExplore(values.explore);
  if (!explore.ok) return said(say, explore.reason, EXIT.input);
  const problems = cliPinProblems({ explore: explore.value.json, home });
  for (const problem of problems) say(`check: ${problem}`);
  return problems.length === 0 ? said(say, `check ok ${values.explore}`, EXIT.done) : EXIT.differs;
}

/** Where a copy is made before it is renamed into place: a hidden sibling of the target, in the same folder (so the rename stays on one volume). */
export const copyTempPath = to => path.join(path.dirname(to), `.${path.basename(to)}.copying`);

/** The sentence for a file already at the copy's place that is not the release, read-only: its two hashes, or its mode. null when it is the release at mode 0555. */
function placedProblem({ to, sha256 }) {
  const there = sha256File(to);
  if (there !== sha256) return `${to} exists and is not the release: it hashes to ${there}, the release hashes to ${sha256}; a pinned copy is never overwritten`;
  const mode = statSync(to).mode & 0o777;
  return mode === 0o555 ? null : `${to} is the release but its mode is ${mode.toString(8)}, not 555: change it by hand; it is not rewritten`;
}

/** v1's copy step (`installFn`, which makes the copy read-only and re-checks its hash) over the temp name; a throw is a value. -> `fail` | null. */
function copiedToTemp({ copy, temp, installFn }) {
  try {
    return installFn({ copy: { ...copy, to: temp } });
  } catch (e) {
    return fail(`cannot copy ${copy.from} to ${temp}: ${e.code ?? e.message}`);
  }
}

/** The temp copy renamed to its place: `ok({ already: false })` | `fail`, the temp file removed when the rename fails. */
function renamedIntoPlace({ temp, to }) {
  try {
    renameSync(temp, to);
    return ok({ already: false });
  } catch (e) {
    rmSync(temp, { force: true });
    return fail(`cannot rename ${temp} to ${to}: ${e.code ?? e.message}`);
  }
}

/**
 * The copy of the release, made whole or not at all (the folders on the way too). v1's `installCli` copies straight to the target, so a kill in the middle leaves a truncated file at the pinned name: here it copies to a temp file of the same
 * folder (a stale one from a killed run is removed first), at mode 0555 with its hash checked, and renames it into place. A file already at the place is never written: the release itself is `already`, any other is a refusal naming both hashes.
 * -> `ok({ already })` | `fail`.
 */
function installed({ copy }, installFn) {
  mkdirSync(path.dirname(copy.to), { recursive: true });
  if (existsSync(copy.to)) {
    const problem = placedProblem(copy);
    return problem ? fail(problem) : ok({ already: true });
  }
  const temp = copyTempPath(copy.to);
  rmSync(temp, { force: true });
  const refused = copiedToTemp({ copy, temp, installFn });
  if (refused) rmSync(temp, { force: true });
  return refused ?? renamedIntoPlace({ temp, to: copy.to });
}

/** The line said for a pin: the copy's place and a short hash, and what was written. */
const pinnedLine = ({ plan, values, already, written }) => `${already ? 'already pinned' : 'pinned'} CLI ${values.version}: ${plan.copy.to} (sha256 ${plan.copy.sha256.slice(0, 12)}), ${written ? 'explore.json updated' : 'explore.json already holds it'}`;

/** The `cli` entry written into `explore.json` unless it holds it already: `ok(true)` written | `ok(false)` nothing to write | `fail`. */
function entryWritten({ explore, plan, values }) {
  const next = withCli(explore.text, plan.cli);
  if (!next.ok) return next;
  if (next.value === explore.text) return ok(false);
  const written = writeAtomically(values.explore, next.value);
  return written.ok ? ok(true) : written;
}

/** `explore.json` rewritten with the `cli` entry, unless it holds it already (nothing is written then): the exit code, the line said. */
function writePin({ explore, plan, values, say, already }) {
  const done = entryWritten({ explore, plan, values });
  return said(say, done.ok ? pinnedLine({ plan, values, already, written: done.value }) : done.reason, done.ok ? EXIT.done : EXIT.input);
}

function pin({ values, say, home, installFn }) {
  const explore = readExplore(values.explore);
  if (!explore.ok) return said(say, explore.reason, EXIT.input);
  const plan = pinPlan({ version: values.version, versionsDir: values['versions-dir'] ?? defaultVersionsDir(home), home, existing: explore.value.json.cli });
  if (!plan.ok) return said(say, plan.reason, plan.exit ?? EXIT.input);
  const placed = installed(plan.value, installFn);
  return placed.ok ? writePin({ explore: explore.value, plan: plan.value, values, say, already: placed.value.already }) : said(say, placed.reason, EXIT.input);
}

/** The command: -> the exit code. */
export function main(argv, say = line => process.stdout.write(`${line}\n`), { home = os.homedir(), installFn = installCli } = {}) {
  const flags = parseFlags(argv, { version: { type: 'string' }, 'versions-dir': { type: 'string' }, explore: { type: 'string' }, home: { type: 'string' } });
  if (!flags.ok) return said(say, flags.reason, EXIT.usage);
  const values = { ...flags.value, explore: flags.value.explore ?? path.join(INPUTS, 'explore.json') };
  const where = flags.value.home ?? home;
  return values.check ? check({ values, say, home: where }) : pin({ values, say, home: where, installFn });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
