// E1.S3: the workspace builder. `buildWorkspace` writes the file set of one workspace (the template of its stack and kind, the derived `PACKAGES.md`, the pinned runner, the grammar of its group's FORMAT, the statement
// and the samples) and checks everything BEFORE the first write: the legal names, the group rule (T-L57), the destination, the pin of the runner, the hash of FORMAT and of its grammar, the packages list and the template's
// `run.sh`. It adds no stack word, no plan word and no path of this host. The rest of this file is the check of a generated `package-lock.json` against the registered manifest and the list of file patterns the
// push check accepts in a stack folder. The destination guard that the builder and the bare copy share is in `workspace-destination.mjs`.
import { chmodSync, lstatSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gitWorktreeOf } from '../../minigit-bench/lib/git-guard.mjs';
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { extractGrammar } from '../tools/grammar-extract.mjs';
import { NPMRC_OFFLINE, outsideManifest, usedNpm } from './cache-offline.mjs';
import { loadRecipeLang } from './cache-recipe-lang.mjs';
import { renderPackagesMd } from './packages-md.mjs';
import { computePin, verifyPin } from './runner-pin.mjs';
import { loadStacksLang } from './toolchain-pins.mjs';
import { checkDestination, writeAtomically } from './workspace-destination.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
export const TEMPLATES_DIR = path.join(SPIKE, 'templates');
export const RUNNER_DIR = path.join(SPIKE, 'runner');
export const STACKS = Object.freeze(['csharp', 'node']);
export const KINDS = Object.freeze(['olympiad', 'daemon', 'web']);
const SCRIPTS = Object.freeze(['build.sh', 'run.sh']);
const GRAMMAR = 'GRAMMAR.md';
const refuse = reason => fail(reason, { refusal: true });

const MODULES = 'node_modules/';
const nameOf = key => key.slice(key.lastIndexOf(MODULES) + MODULES.length);
const isPackage = ([key, entry]) => key.includes(MODULES) && entry.link !== true && typeof entry.version === 'string';
const sameKeys = (a, b) => JSON.stringify(Object.entries(a ?? {}).sort()) === JSON.stringify(Object.entries(b ?? {}).sort());

/**
 * What is wrong with a `package-lock.json` (parsed) against the registered npm manifest and the template's `package.json` (parsed): a package outside the manifest, an integrity that is not the manifest's,
 * an install script, a root that does not list what `package.json` lists. `[]` is clean; each problem names the package.
 */
export function lockProblems(lock, manifest, packageJson) {
  const npm = manifest.filter(row => row.ecosystem === 'npm');
  const integrity = new Map(npm.map(row => [`${row.name}@${row.version}`, row.integrity]));
  const entries = Object.entries(lock.packages ?? {}).filter(isPackage);
  const wrongIntegrity = entries.filter(([key, entry]) => integrity.has(`${nameOf(key)}@${entry.version}`) && integrity.get(`${nameOf(key)}@${entry.version}`) !== entry.integrity).map(([key, entry]) => `${nameOf(key)}@${entry.version} has the integrity ${entry.integrity}, not the manifest's`);
  const scripts = entries.filter(([, entry]) => entry.hasInstallScript === true).map(([key, entry]) => `${nameOf(key)}@${entry.version} has an install script`);
  const root = lock.packages?.[''] ?? {};
  const rootProblems = ['dependencies', 'devDependencies'].filter(field => !sameKeys(root[field], packageJson[field])).map(field => `the lock's root ${field} differ from package.json`);
  return [...outsideManifest(usedNpm(lock), npm, 'npm').map(id => `${id} is not in the registered manifest`), ...wrongIntegrity, ...scripts, ...rootProblems];
}

// ---- the inputs -------------------------------------------------------------------------------------------------------------------------

/** `ok({ specs, format, record, recipe })` read from the real input files (a test passes fixtures instead): the spec record (its `runner` pin), the FORMAT rows, the snapshot record and the checked recipe. */
export function loadInputs(spike = SPIKE) {
  try {
    const read = rel => JSON.parse(readFileSync(path.join(spike, rel), 'utf8'));
    const stacks = loadStacksLang(read('inputs/stacks-lang.json'));
    const recipe = stacks.ok ? loadRecipeLang(read('inputs/cache-recipe-lang.json'), stacks.value) : stacks;
    return recipe.ok ? ok({ specs: read('inputs/specs.json'), format: read('inputs/specs.d/format.json'), record: read('inputs/cache-snapshots-lang.json'), recipe: recipe.value }) : recipe;
  } catch (error) {
    return fail(`an input file cannot be read: ${error.message}`);
  }
}

// ---- the small checks -------------------------------------------------------------------------------------------------------------------

/** The sentence about a `run.sh` whose last non-empty line does not start with `exec `, else null: without `exec` a bash stays the parent and a stop signal never reaches the program. */
export function runShProblem(text) {
  const last = text.split('\n').map(line => line.trim()).filter(line => line !== '').at(-1);
  return last !== undefined && last.startsWith('exec ') ? null : 'run.sh must end with a line that starts with `exec `: the program has to replace the shell, or a stop signal sent to the process never reaches it';
}

/** The `.npmrc` of every Node workspace template: the offline file and no update check (the check asks the registry once per npm call, which an agent box without a registry logs as a refused request). */
export const NPMRC_WORKSPACE = `${NPMRC_OFFLINE}update-notifier=false\n`;

/** The pin of a built workspace's `runner/` (the same pin as the bench's own folder, the grammar file left out: the pin refuses any non-.py file). */
export const runnerHash = dir => computePin(dir, folder => readdirSync(folder).filter(name => name !== GRAMMAR));

const legalProblem = (stack, kind) => {
  if (!STACKS.includes(stack)) return `the stack ${JSON.stringify(stack)} is not one of ${STACKS.join(', ')}`;
  return KINDS.includes(kind) ? null : `the kind ${JSON.stringify(kind)} is not one of ${KINDS.join(', ')}`;
};

/** The group rule (T-L57, D61): the group is a row of FORMAT's record, and the task is in that row (or in no row at all for the hello fixture), never in another group's. */
function groupProblem({ format, group, task, helloFixture }) {
  const groups = Object.keys(format).filter(key => Array.isArray(format[key]?.tasks));
  if (!groups.includes(group)) return `the group ${JSON.stringify(group)} is not one of ${groups.join(', ')}`;
  const owner = groups.find(key => format[key].tasks.includes(task));
  return owner === undefined ? rowlessProblem(task, helloFixture) : foreignProblem({ owner, group, task });
}

const rowlessProblem = (task, helloFixture) => (helloFixture ? null : `the task ${JSON.stringify(task)} is in no group's row of FORMAT's record`);
const foreignProblem = ({ owner, group, task }) => (owner === group ? null : `the build of ${group} refuses to write into ${owner}'s tasks: the task ${task} belongs to group ${owner}, not to group ${group}`);

/** FORMAT's bytes and their grammar against the group's record: `ok(grammar bytes)` | a refusal naming both hashes. */
function grammarOf({ formatBytes, format, group }) {
  const row = format[group];
  const got = sha256Of(formatBytes);
  if (got !== row.sha256) return refuse(`FORMAT.md has sha256 ${got}, but the record of group ${group} has ${row.sha256}`);
  const grammar = extractGrammar(formatBytes);
  if (!grammar.ok) return refuse(grammar.reason);
  const grammarHash = sha256Of(grammar.bytes);
  return grammarHash === row.grammarSha256 ? ok(grammar.bytes) : refuse(`the grammar of FORMAT.md has sha256 ${grammarHash}, but the record of group ${group} has ${row.grammarSha256}`);
}

const relativeOf = (base, abs) => path.relative(base, abs).split(path.sep).join('/');

/** The regular files under `abs` (a file is one) as `{ rel, abs }`; a link or another kind of entry is a `fail` naming it (the builder copies regular files only). */
function entryFiles(abs, base) {
  const stat = lstatSync(abs);
  if (stat.isDirectory()) return listFiles(abs, base);
  return stat.isFile() ? ok([{ rel: relativeOf(base, abs), abs }]) : refuse(`${abs} is a link or another kind of entry: a workspace is made of regular files`);
}

/** The regular files under `dir`, or a `fail` for a folder that cannot be read or an entry that is not a regular file. */
function listFiles(dir, base = dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch (error) {
    return fail(`${dir} cannot be read (${error.code ?? error.message})`);
  }
  const found = [];
  for (const entry of entries) {
    const inner = entryFiles(path.join(dir, entry.name), base);
    if (!inner.ok) return inner;
    found.push(...inner.value);
  }
  return ok(found);
}

// ---- the plan: everything checked before the first write -----------------------------------------------------------------------------------------

/** The checks that need no file at all but the destination: names, group, destination. -> `ok(resolved out)` | `fail`. The command runs it before it fetches anything. */
export function preCheck({ stack, kind, task, group, out, inputs, worktreeOf = gitWorktreeOf, helloFixture = false }) {
  const problem = legalProblem(stack, kind) ?? groupProblem({ format: inputs.format, group, task, helloFixture });
  return problem === null ? checkDestination(out, worktreeOf) : refuse(problem);
}

/** All the checks before the first write. -> `ok({ out, grammar })` | `fail`. */
function checkRequest(args) {
  const { group, formatBytes, inputs, runnerDir = RUNNER_DIR } = args;
  const destination = preCheck(args);
  if (!destination.ok) return destination;
  const pin = verifyPin(runnerDir, inputs.specs);
  if (!pin.ok) return refuse(pin.reason);
  const grammar = grammarOf({ formatBytes, format: inputs.format, group });
  return grammar.ok ? ok({ out: destination.value, grammar: grammar.value }) : grammar;
}

const modeOfTemplateFile = rel => (SCRIPTS.includes(rel) ? 0o755 : 0o644);
const copies = (list, prefix, modeOf = () => 0o644) => list.map(file => ({ rel: `${prefix}${file.rel}`, abs: file.abs, mode: modeOf(file.rel) }));

/** The template, the runner and the samples as `{ rel, abs, mode }`. -> `ok(files)` | `fail`. */
function sourceFiles({ stack, kind, samplesDir, templatesDir, runnerDir }) {
  const found = [listFiles(path.join(templatesDir, stack, kind)), listFiles(runnerDir), listFiles(samplesDir)];
  const failed = found.find(result => !result.ok);
  if (failed !== undefined) return failed;
  const [template, runner, samples] = found.map(result => result.value);
  return ok([...copies(template, '', modeOfTemplateFile), ...copies(runner, 'runner/'), ...copies(samples, 'samples/')]);
}

/** A refusal when the template's `run.sh` does not end in `exec`, else null. */
function scriptRefusal({ stack, kind, templatesDir }) {
  const problem = runShProblem(readFileSync(path.join(templatesDir, stack, kind, 'run.sh'), 'utf8'));
  return problem === null ? null : refuse(`the template ${stack}/${kind}: ${problem}`);
}

/** The files that are written from bytes: the derived packages list, the grammar and the statement. -> `ok(files)` | `fail`. */
function derivedFiles({ stack, inputs, statement, grammar }) {
  const packages = renderPackagesMd({ stack, record: inputs.record, recipe: inputs.recipe });
  if (!packages.ok) return refuse(packages.reason);
  return ok([{ rel: 'PACKAGES.md', bytes: packages.value, mode: 0o644 }, { rel: `runner/${GRAMMAR}`, bytes: grammar, mode: 0o644 }, { rel: 'STATEMENT.md', bytes: statement, mode: 0o644 }]);
}

const byPath = (a, b) => (a.rel < b.rel ? -1 : 1);

/** Every file of the workspace, sorted by path. -> `ok(files)` | `fail`. */
function gatherFiles(args) {
  const { templatesDir = TEMPLATES_DIR, runnerDir = RUNNER_DIR } = args;
  const sources = sourceFiles({ ...args, templatesDir, runnerDir });
  if (!sources.ok) return sources;
  const script = scriptRefusal({ ...args, templatesDir });
  if (script !== null) return script;
  const derived = derivedFiles(args);
  return derived.ok ? ok([...sources.value, ...derived.value].sort(byPath)) : derived;
}

// ---- the write -----------------------------------------------------------------------------------------------------------------------------------

function writeFiles(dir, files) {
  for (const file of files) {
    const target = path.join(dir, file.rel);
    mkdirSync(path.dirname(target), { recursive: true });
    writeFileSync(target, file.bytes ?? readFileSync(file.abs));
    chmodSync(target, file.mode);
  }
}

/**
 * `buildWorkspace({ stack, kind, task, group, out, statement, samplesDir, formatBytes, inputs, worktreeOf, helloFixture })` -> `ok({ out, files })` | `fail(reason, { refusal: true })`. Every check runs before the first
 * write, so a refusal leaves nothing behind. `inputs` is `loadInputs()`'s value; `templatesDir` and `runnerDir` default to this spike's, a test passes a copy.
 */
export function buildWorkspace(args) {
  const request = checkRequest(args);
  if (!request.ok) return request;
  const files = gatherFiles({ ...args, grammar: request.value.grammar });
  if (!files.ok) return files;
  const written = writeAtomically(request.value.out, stage => writeFiles(stage, files.value));
  return written.ok ? ok({ out: written.value, files: files.value.map(file => file.rel) }) : written;
}

// ---- the push check's list ----------------------------------------------------------------------------------------------------------------------

/** The patterns the push check accepts in a stack folder (`<t>` stands for one path segment), sorted: the template files of each stack, its derived files, the pinned runner files, the grammar and the samples. */
export function stackFilePatterns({ specs, templatesDir = TEMPLATES_DIR }) {
  const runnerFiles = specs.runner.files.map(f => f.path);
  return STACKS.flatMap(stack => {
    const templateFiles = [...new Set(KINDS.flatMap(kind => readdirSync(path.join(templatesDir, stack, kind))))];
    const own = ['PACKAGES.md', 'STATEMENT.md', ...runnerFiles.map(name => `runner/${name}`), `runner/${GRAMMAR}`, 'samples/<t>'];
    return [...templateFiles, ...own].map(file => `${stack}/<t>/${file}`);
  }).sort();
}
