// E1.S4: `inputs/cache-recipe-lang.json` -> `ok(recipe)` | `fail` naming the field. v1's `loadRecipe` first (hosts, steps, capture), then the v2 checks: every stack offers a non-empty `packages` list (name, spec, a purpose
// that fits a table cell), every placeholder is `{[A-Z_]+}` and names a key of that stack's `resolve`, `csharp.project` is a relative folder that exists, and the toolchain pins of the recipe are the ones of
// `inputs/stacks-lang.json` (each compared on its own, so each refusal names what differs). Pure but for the look at the project folder.
import { existsSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadRecipe } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const RESERVED_STACK = 'superseded';
/** Any brace token at all: `{XUNIT_V3}` is found (and refused) here although v1's filler, which knows only `{[A-Z_]+}`, would leave it in the argv unnoticed. */
const BRACE_TOKEN = /\{([A-Za-z0-9_]*)\}/g;
const PLACEHOLDER_NAME = /^[A-Z_]+$/;
const isText = x => typeof x === 'string' && x.trim() !== '';
const cellProblem = text => (isText(text) && !/[|\r\n]/.test(text) ? null : 'a non-empty text without `|` or a line break');

const DATE = /^\d{4}-\d{2}-\d{2}$/;

const METADATA_CHECKS = Object.freeze([
  ['licence', p => isText(p?.licence), 'must be a non-empty text (the SPDX expression the package declares)'],
  ['maintenance.latestVersion', p => isText(p?.maintenance?.latestVersion), 'must be a non-empty text (the latest release the registry shows)'],
  ['maintenance.releasedOn', p => DATE.test(p?.maintenance?.releasedOn ?? ''), 'must be a date YYYY-MM-DD (the release date of that version)'],
  ['maintenance.readOn', p => DATE.test(p?.maintenance?.readOn ?? ''), 'must be a date YYYY-MM-DD (the day the registry was read)'],
]);

/** The licence (an SPDX expression the snapshot's own metadata is compared with) and the maintenance state (the latest release the registry showed, and the day it was read) of one package (PROJECT section 10). */
const metadataProblems = (at, p) => METADATA_CHECKS.filter(([, passes]) => !passes(p)).map(([field, , why]) => `${at}.${field} ${why}`);

/** The problems of one `packages[i]` entry: a name, a spec, a purpose that fits a table cell, the licence and the maintenance state. */
function entryProblems(at, p) {
  const purpose = cellProblem(p?.purpose);
  return [
    ...(isText(p?.name) ? [] : [`${at}.name must be a non-empty text`]),
    ...(isText(p?.spec) ? [] : [`${at}.spec must be a non-empty text`]),
    ...(purpose === null ? [] : [`${at}.purpose must be ${purpose}`]),
    ...metadataProblems(at, p),
  ];
}

function packageProblems(stack, spec) {
  if (!Array.isArray(spec.packages) || spec.packages.length === 0) return [`stacks.${stack}.packages must list every offered package`];
  return spec.packages.flatMap((p, i) => entryProblems(`stacks.${stack}.packages[${i}]`, p));
}

function resolveProblems(stack, spec) {
  return Object.entries(spec.resolve ?? {}).flatMap(([key, r]) => [
    ...(PLACEHOLDER_NAME.test(key) ? [] : [`stacks.${stack}.resolve.${key} is not a legal name: a placeholder name is [A-Z_]+ only (no digit), because the filler of v1 would leave it in the argv`]),
    ...(r?.pick === 'highest' ? [] : [`stacks.${stack}.resolve.${key}.pick must be "highest"`]),
  ]);
}

/** Every brace token of one text, as a problem when its name is not a key of `resolve` (a name with a digit or lower case is not even a legal key). */
function tokenProblems(text, at, known) {
  return [...text.matchAll(BRACE_TOKEN)].map(m => m[1]).filter(name => !(PLACEHOLDER_NAME.test(name) && known.has(name)))
    .map(name => `${at} holds {${name}}, which is not a key of the stack's resolve (legal keys: ${[...known].join(', ') || 'none'}; a placeholder name is [A-Z_]+ only)`);
}

function placeholderProblems(stack, spec) {
  const known = new Set(Object.keys(spec.resolve ?? {}));
  const argvs = [...spec.steps.map((s, i) => [`stacks.${stack}.steps[${i}].argv`, s.argv]), ...Object.entries(spec.resolve ?? {}).map(([k, r]) => [`stacks.${stack}.resolve.${k}.argv`, r?.argv ?? []])];
  const specs = (spec.packages ?? []).map((p, i) => [`stacks.${stack}.packages[${i}].spec`, [String(p?.spec ?? '')]]);
  return [...argvs, ...specs].flatMap(([at, texts]) => texts.flatMap(t => tokenProblems(String(t), at, known)));
}

const badPath = rel => !isText(rel) || path.isAbsolute(rel) || rel.split('/').includes('..');
const folderProblem = (dir, rel) => (existsSync(dir) && statSync(dir).isDirectory() ? [] : [`stacks.csharp.project ${rel} must exist as a folder under the spike`]);

function projectProblems(spec, spikeDir) {
  if (spec.project === undefined) return [];
  if (badPath(spec.project)) return ['stacks.csharp.project must be a relative path inside the spike'];
  return folderProblem(path.join(spikeDir, spec.project), spec.project);
}

const pinRow = ({ stack, spec, label, matches, expected }) => {
  const i = spec.steps.findIndex(s => matches(s.argv));
  if (i < 0) return [`stacks.${stack} has no \`${label}\` step: the toolchain pin cannot be checked`];
  return spec.steps[i].expectStdout === expected ? [] : [`stacks.${stack}.steps[${i}] (\`${label}\`) pins ${JSON.stringify(spec.steps[i].expectStdout)}, inputs/stacks-lang.json pins ${JSON.stringify(expected)}`];
};
const isVersionOf = exe => argv => argv[0] === exe && argv[1] === '--version';
const isTscVersion = argv => argv.at(-1) === '--version' && String(argv.at(-2)).endsWith('typescript/bin/tsc');

function pinProblems(stack, spec, pins) {
  if (stack === 'csharp') return pinRow({ stack, spec, label: 'dotnet --version', matches: isVersionOf('dotnet'), expected: pins.dotnetSdk });
  if (stack !== 'node') return [];
  const typescript = (spec.packages ?? []).find(p => p?.name === 'typescript');
  return [
    ...pinRow({ stack, spec, label: 'node --version', matches: isVersionOf('node'), expected: `v${pins.node}` }),
    ...pinRow({ stack, spec, label: 'tsc --version', matches: isTscVersion, expected: `Version ${pins.typescript}` }),
    ...(typescript?.spec === pins.typescript ? [] : [`stacks.node.packages typescript spec ${JSON.stringify(typescript?.spec)} is not the pin ${pins.typescript} of inputs/stacks-lang.json`]),
  ];
}

/** The SDK version a global.json pins: `{ version }`, or `{ problem }` when it is not JSON. A missing file is a version of `undefined`. */
function readSdkVersion(file) {
  if (!existsSync(file)) return { version: undefined };
  try { return { version: JSON.parse(readFileSync(file, 'utf8')).sdk?.version }; } catch (error) { return { problem: `is not JSON (${error.message.split('\n')[0]})` }; }
}

/** The global.json of the project must pin the SDK of stacks-lang.json too: a restore under another SDK is another restore. */
function globalJsonProblems(spec, pins, spikeDir) {
  if (spec.project === undefined) return [];
  const file = path.join(spikeDir, spec.project, 'global.json');
  const read = readSdkVersion(file);
  if (read.problem !== undefined) return [`${spec.project}/global.json ${read.problem}`];
  const seen = read.version;
  return seen === pins.dotnetSdk ? [] : [`${spec.project}/global.json pins SDK ${JSON.stringify(seen)}, inputs/stacks-lang.json pins ${pins.dotnetSdk}`];
}

function stackProblems(stack, spec, pins, spikeDir) {
  return [
    ...(stack === RESERVED_STACK ? [`stacks.${RESERVED_STACK}: a stack may not be called ${RESERVED_STACK}, the record keeps its superseded list under that key`] : []),
    ...packageProblems(stack, spec), ...resolveProblems(stack, spec), ...placeholderProblems(stack, spec),
    ...projectProblems(spec, spikeDir), ...pinProblems(stack, spec, pins), ...globalJsonProblems(spec, pins, spikeDir),
  ];
}

/** `json` = the recipe, `stacks` = the checked `inputs/stacks-lang.json` (`loadStacksLang(...).value`). `spikeDir` is a seam for the project path. */
export function loadRecipeLang(json, stacks, { spikeDir = SPIKE } = {}) {
  const base = loadRecipe(json);
  if (!base.ok) return base;
  const problems = Object.entries(json.stacks).flatMap(([name, spec]) => stackProblems(name, spec, stacks.pins, spikeDir));
  return problems.length === 0 ? ok(json) : fail(`cache-recipe-lang: ${problems.join('; ')}`, { problems });
}
