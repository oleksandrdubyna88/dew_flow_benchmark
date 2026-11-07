#!/usr/bin/env node
// F1.S3 (a): the package matrix of the language comparison v2. Every package of the recipe is added, one at a time, to the template of every kind of its stack and built in the build box over the registered snapshot with no
// registry; the rows (`works`, or `unsupported` with the code) are written to `inputs/package-matrix.json`. The matrix is the one source of what a kind offers: `PACKAGES.md` lists the `works` rows, and the recipe's `kinds`
// of each package must be equal to them.
//
//   node spikes/lang-bench/package-matrix.mjs            measure every row and write inputs/package-matrix.json (under the gate lock)
//   node spikes/lang-bench/package-matrix.mjs --check    no box: the matrix has a row for every package and kind, the recipe's kinds equal its works rows, and it was measured against the registered snapshots
//     --caches <dir>    default ~/rsd/lang-v2/caches       --record <file>   default inputs/cache-snapshots-lang.json     --recipe <file>   default inputs/cache-recipe-lang.json
//     --stacks <file>   default inputs/stacks-lang.json    --matrix <file>   default inputs/package-matrix.json           --work <dir>      default ~/.cache/lang-v2/w
//     --gate-lock <file> default ~/.cache/lang-v2/gate.lock
// Exit 0 done / clean · 1 --check found a problem, or a measurement could not be made · 2 usage (an unreadable input) · 3 refused (the gate lock is held)
// Lowered before anything else runs (common.local-cpu-budget), as run-tests.mjs does.
await import('./lib/lower-priority.mjs');

import { mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { ownedFor } from '../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../minigit-bench/lib/result.mjs';
import { loadRecipeLang } from './lib/cache-recipe-lang.mjs';
import { takeGateLock } from './lib/gate-lock.mjs';
import { matrixOf, measureRows } from './lib/matrix-measure.mjs';
import { probeConfirmed } from './lib/matrix-probe.mjs';
import { matrixProblems, matrixText } from './lib/package-matrix.mjs';
import { storeDirs } from './lib/preflight-box.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';

const SPIKE = fileURLToPath(new URL('.', import.meta.url));
const EXIT = Object.freeze({ done: 0, failed: 1, usage: 2, refused: 3 });
const OPTIONS = { check: { type: 'boolean' }, caches: { type: 'string' }, record: { type: 'string' }, recipe: { type: 'string' }, stacks: { type: 'string' }, matrix: { type: 'string' }, work: { type: 'string' }, 'gate-lock': { type: 'string' } };
const REASON = 'Measured by package-matrix.mjs: each package of the recipe added alone to the template of each kind and built offline over the registered snapshot; an unsupported row carries the code the build failed with.';
const say = line => process.stdout.write(`${line}\n`);
const complain = line => process.stderr.write(`${line}\n`);
const stop = (code, line) => { complain(line); return code; };

/** The flags with their defaults: `ok(config)` | `fail` (a usage line). */
function readArgs(argv) {
  let v;
  try { v = parseArgs({ args: argv, options: OPTIONS, strict: true }).values; } catch (e) { return fail(e.message); }
  const home = os.homedir();
  const at = (value, ...fallback) => path.resolve(value ?? path.join(...fallback));
  return ok({
    check: v.check === true, caches: at(v.caches, home, 'rsd', 'lang-v2', 'caches'), record: at(v.record, SPIKE, 'inputs', 'cache-snapshots-lang.json'), recipe: at(v.recipe, SPIKE, 'inputs', 'cache-recipe-lang.json'),
    stacks: at(v.stacks, SPIKE, 'inputs', 'stacks-lang.json'), matrix: at(v.matrix, SPIKE, 'inputs', 'package-matrix.json'), work: at(v.work, home, '.cache', 'lang-v2', 'w'), gateLock: at(v['gate-lock'], home, '.cache', 'lang-v2', 'gate.lock'),
  });
}

/** A JSON file: `ok(json)` | `fail` naming the flag and the file. */
function readJsonFlag(flag, file) {
  try { return ok(JSON.parse(readFileSync(file, 'utf8'))); } catch (error) { return fail(`${flag} ${file}: ${error.message.replace(/\s+/g, ' ').slice(0, 300)}`); }
}

/** The recipe (checked against the toolchain pins), the record and the toolchain pins. -> `ok({ recipe, record, stacks })` | `fail`. */
function readInputs(config) {
  const [stacksJson, recipeJson, record] = [readJsonFlag('--stacks', config.stacks), readJsonFlag('--recipe', config.recipe), readJsonFlag('--record', config.record)];
  const failed = [stacksJson, recipeJson, record].find(x => !x.ok);
  if (failed !== undefined) return failed;
  const stacks = loadStacksLang(stacksJson.value);
  const recipe = stacks.ok ? loadRecipeLang(recipeJson.value, stacks.value) : stacks;
  return recipe.ok ? ok({ recipe: recipe.value, record: record.value, stacks: stacks.value }) : recipe;
}

function checkMode(config, inputs) {
  const matrix = readJsonFlag('--matrix', config.matrix);
  if (!matrix.ok) return stop(EXIT.usage, matrix.reason);
  const problems = matrixProblems({ matrix: matrix.value, recipe: inputs.recipe, record: inputs.record });
  problems.forEach(complain);
  if (problems.length === 0) say(`package-matrix: ${config.matrix} is complete, equals the recipe's kinds and was measured against the registered snapshots`);
  return problems.length === 0 ? EXIT.done : EXIT.failed;
}

/** The real probe: one package in one kind in the build box, in the work folder. */
const probeFor = ({ config, inputs }) => {
  const storeRoot = path.join(os.homedir(), ...inputs.stacks.storeRoot);
  const [owned, dirs] = [ownedFor({ token: 'bench' }), storeDirs(inputs.stacks, storeRoot)];
  return args => probeConfirmed({ owned, work: config.work, dirs, record: inputs.record, caches: config.caches, ...args });
};

/** Write beside and rename: a reader sees the old matrix or the new one, never half a file. */
function writeMatrix(file, matrix) {
  const temp = `${file}.tmp-${process.pid}`;
  writeFileSync(temp, matrixText(matrix), { flag: 'wx' });
  renameSync(temp, file);
}

async function writeMode(config, inputs) {
  mkdirSync(config.work, { recursive: true, mode: 0o700 });
  const rows = await measureRows({ recipe: inputs.recipe, record: inputs.record, probe: probeFor({ config, inputs }), log: say });
  if (!rows.ok) return stop(EXIT.failed, `package-matrix: ${rows.reason}; no matrix is written`);
  const matrix = matrixOf({ rows: rows.value, record: inputs.record, today: new Date().toISOString().slice(0, 10), reason: REASON });
  writeMatrix(config.matrix, matrix);
  say(`package-matrix: wrote ${config.matrix}`);
  return checkMode(config, inputs);
}

async function underGateLock(config, run) {
  const lock = takeGateLock({ file: config.gateLock });
  if (!lock.ok) return stop(EXIT.refused, lock.reason);
  try { return await run(); } finally { lock.value.release(); }
}

async function main(argv) {
  const config = readArgs(argv);
  if (!config.ok) return stop(EXIT.usage, config.reason);
  const inputs = readInputs(config.value);
  if (!inputs.ok) return stop(EXIT.usage, inputs.reason);
  return config.value.check ? checkMode(config.value, inputs.value) : underGateLock(config.value, () => writeMode(config.value, inputs.value));
}

process.exitCode = await main(process.argv.slice(2));
