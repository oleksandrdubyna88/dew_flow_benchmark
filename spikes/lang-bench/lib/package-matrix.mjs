// F1.S3 (a): the package matrix, `inputs/package-matrix.json` (written by `package-matrix.mjs`, never typed). One row per (stack, offered package, kind): `works` (the package added to the template of that kind builds offline over
// the registered snapshot) or `unsupported` with the code the build failed with. The matrix is the one source of which packages a kind offers: `PACKAGES.md` of a kind lists exactly the `works` rows, and the recipe's `kinds`
// of each package must equal them (`matrixProblems`, the writer's `--check`). A row that is `unsupported` takes its package out of that kind's offered set; the snapshot stays one per stack.
// Pure. A matrix is `{ decidedOn, reason, measuredAgainst: { snapshots: { <stack>: <tree sha256> }, templatesIdentity }, rows: { <stack>: { <package name>: { <kind>: { state, code? } } } } }`.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { KINDS } from './cache-recipe-lang.mjs';

export const STATES = Object.freeze(['works', 'unsupported']);
const isText = x => typeof x === 'string' && x.trim() !== '';

/** The row of (stack, package, kind), or `undefined`. */
export const rowOf = (matrix, stack, name, kind) => matrix?.rows?.[stack]?.[name]?.[kind];

/** True when the matrix says the package works in that kind. */
export const isOffered = (matrix, stack, name, kind) => rowOf(matrix, stack, name, kind)?.state === 'works';

/** The kinds a package works in, in the order of `KINDS`. */
export const kindsOf = (matrix, stack, name) => KINDS.filter(kind => isOffered(matrix, stack, name, kind));

/** The names the matrix says work in a kind of a stack. */
export const offeredNames = (matrix, stack, kind) => Object.keys(matrix?.rows?.[stack] ?? {}).filter(name => isOffered(matrix, stack, name, kind));

function rowProblems(at, row) {
  if (row === undefined) return [`${at} has no row`];
  if (!STATES.includes(row.state)) return [`${at}.state is ${JSON.stringify(row.state)}, not one of ${STATES.join(', ')}`];
  if (row.state === 'unsupported' && !isText(row.code)) return [`${at} is unsupported and must carry the code the build failed with`];
  return row.state === 'works' && row.code !== undefined ? [`${at} works and carries a code`] : [];
}

/** The rows a recipe stack needs, each against the matrix: a package of the recipe in each kind needs a row. */
function completeProblems(matrix, stack, spec) {
  const named = new Set(spec.packages.map(p => p.name));
  const missing = spec.packages.flatMap(p => KINDS.flatMap(kind => rowProblems(`rows.${stack}.${p.name}.${kind}`, rowOf(matrix, stack, p.name, kind))));
  const extra = Object.keys(matrix?.rows?.[stack] ?? {}).filter(name => !named.has(name)).map(name => `rows.${stack}.${name} is a package the recipe does not offer`);
  return [...missing, ...extra];
}

/** Each package's `kinds` in the recipe against the matrix's `works` rows: they are equal, in `KINDS` order. */
function kindsProblems(matrix, stack, spec) {
  return spec.packages.flatMap(p => {
    const [want, held] = [kindsOf(matrix, stack, p.name).join(','), (p.kinds ?? []).join(',')];
    return want === held ? [] : [`the recipe offers ${stack}/${p.name} in [${held}], the matrix says it works in [${want}]`];
  });
}

/** The snapshot each stack was measured against must be the registered one, and the templates must be the ones the rows were measured on (`templatesIdentity` is `lib/templates-identity.mjs`'s; a check that is not given it fails). */
function measuredProblems(matrix, record, templatesIdentity) {
  const seen = matrix.measuredAgainst?.snapshots ?? {};
  const stacks = new Set([...Object.keys(seen), ...Object.keys(record ?? {}).filter(k => k !== 'superseded')]);
  const snapshots = [...stacks].filter(stack => seen[stack] !== record?.[stack]?.treeSha256)
    .map(stack => `the matrix was measured against ${stack} snapshot ${seen[stack] ?? 'none'}, the registered one is ${record?.[stack]?.treeSha256 ?? 'none'}`);
  const measured = matrix.measuredAgainst?.templatesIdentity;
  if (typeof measured !== 'string') return [...snapshots, 'the matrix names no templates identity: it does not say which templates its rows were measured on'];
  return measured === templatesIdentity ? snapshots : [...snapshots, `the matrix was measured against templates ${measured}, the templates now are ${templatesIdentity}`];
}

/** Everything wrong with a matrix against the recipe and the registered record: a missing or ill-formed row, a package the recipe does not offer, a recipe `kinds` that is not the `works` rows, a stale snapshot, other templates than the rows were measured on. `[]` is clean. */
export function matrixProblems({ matrix, recipe, record, templatesIdentity }) {
  if (matrix === null || typeof matrix !== 'object') return ['the matrix is not an object'];
  const stacks = Object.entries(recipe.stacks);
  return [
    ...stacks.flatMap(([stack, spec]) => completeProblems(matrix, stack, spec)),
    ...stacks.flatMap(([stack, spec]) => kindsProblems(matrix, stack, spec)),
    ...measuredProblems(matrix, record, templatesIdentity),
  ];
}

/**
 * `ok(matrix)` when the matrix is complete, equals the recipe's kinds and was measured against the registered snapshots, else `fail` naming the problems. The templates' identity is not asked here (a workspace build must not
 * stop for a file of `templates/` that no probe reads): `package-matrix.mjs --check` asks it.
 */
export function loadMatrix({ json, recipe, record }) {
  const problems = matrixProblems({ matrix: json, recipe, record, templatesIdentity: json?.measuredAgainst?.templatesIdentity });
  return problems.length === 0 ? ok(json) : fail(`package-matrix: ${problems.join('; ')}`, { problems });
}

const OFFERED_HEADING = '## Offered';
const ROW_NAME = /^\| `([^`]+)` \|/;

/** The package names in the `Offered` table of a `PACKAGES.md`. */
export function offeredNamesIn(text) {
  const lines = text.split('\n');
  const start = lines.indexOf(OFFERED_HEADING);
  if (start < 0) return [];
  const end = lines.findIndex((line, i) => i > start && line.startsWith('## '));
  return lines.slice(start + 1, end < 0 ? undefined : end).map(line => ROW_NAME.exec(line)?.[1]).filter(name => name !== undefined);
}

/** A `PACKAGES.md` against the matrix: every name in its `Offered` table is a `works` row of that stack and kind, and every `works` row is in it. NuGet ids compare in lower case. */
export function packagesMdProblems({ text, matrix, stack, kind }) {
  const norm = name => (stack === 'csharp' ? name.toLowerCase() : name);
  const [listed, expected] = [offeredNamesIn(text).map(norm), offeredNames(matrix, stack, kind).map(norm)];
  return [
    ...listed.filter(name => !expected.includes(name)).map(name => `${name} is listed in the ${stack}/${kind} PACKAGES.md and the matrix does not say it works there`),
    ...expected.filter(name => !listed.includes(name)).map(name => `${name} works in ${stack}/${kind} and the PACKAGES.md does not list it`),
  ];
}

/** The matrix as the bytes `package-matrix.json` holds: two-space JSON, one final newline. */
export const matrixText = matrix => `${JSON.stringify(matrix, null, 2)}\n`;

/** A matrix on which every package of the recipe works in every kind: what every kind offered before the matrix existed (a series made then is judged against the file as it was rendered then). */
export const allWorksMatrix = recipe => ({
  rows: Object.fromEntries(Object.entries(recipe.stacks).map(([stack, spec]) => [stack, Object.fromEntries(spec.packages.map(p => [p.name, Object.fromEntries(KINDS.map(kind => [kind, { state: 'works' }]))]))])),
});
