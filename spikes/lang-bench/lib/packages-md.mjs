// E1.S4: `PACKAGES.md` of a stack, DERIVED from the registered record and the recipe, never typed (parent D12; S3 writes it into the workspaces, `make-snapshots.mjs --print-packages-md <stack>` prints it).
// The version of every offered package is the one the MANIFEST holds; the expected version is the recipe's spec filled from the record's `resolved`, and a manifest that does not hold `name@expected` is a refusal naming
// the package and both values. The purpose is the recipe's. Every manifest package that is not offered goes to the second table. Pure: the same record and recipe give the same bytes.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { plainOrder, versionOrder } from './cache-store.mjs';

const ECOSYSTEM_OF = Object.freeze({ csharp: 'nuget', node: 'npm' });
const SPEC_TOKEN = /\{([A-Z_]+)\}/g;
/** The sentence an agent reads first, per stack, in the team lead's words (E1.S4 review, M3): what is available, what a version that is not listed does, and the error that names it. */
const SENTENCE_OF = Object.freeze({
  csharp: 'Only the packages and versions listed here are available; there is no network. A reference to a lower version than the one listed resolves to the listed version, with the warning NU1603; a higher version fails with NU1102, and a package not listed fails with NU1101; both errors name the package.',
  node: 'Only the packages and versions listed here are available; there is no network. A package or version not listed fails to install with ENOTCACHED; the message may name a dependency of the package rather than the package itself.',
});
const INSTALL_LINE = 'Install a package with the exact version listed (C#: `<PackageReference Include="NAME" Version="VERSION" />`; Node: `npm install NAME@VERSION`).';

const keyOf = (ecosystem, name) => (ecosystem === 'nuget' ? name.toLowerCase() : name);
const idOf = (ecosystem, name, version) => `${keyOf(ecosystem, name)}@${version}`;
const cellProblem = text => (typeof text === 'string' && text.trim() !== '' && !/[|\r\n]/.test(text) ? null : 'a non-empty text without `|` or a line break');
const rowOrder = (a, b) => plainOrder(a.name.toLowerCase(), b.name.toLowerCase()) || plainOrder(a.name, b.name) || versionOrder(a.version, b.version);

/** The spec filled from `resolved`: `ok(version)` | `fail` naming a placeholder the record did not resolve. */
function expectedVersion(spec, resolved) {
  const open = [...spec.matchAll(SPEC_TOKEN)].map(m => m[1]).filter(key => resolved[key] === undefined);
  return open.length > 0 ? fail(`the spec ${spec} holds {${open.join('}, {')}}, which the record's resolved does not hold`) : ok(spec.replace(SPEC_TOKEN, (m, key) => resolved[key]));
}

/** The purpose and the expected version of one offered package: `ok(version)` | `fail`. */
function checkOffered(p, resolved) {
  const problem = cellProblem(p.purpose);
  if (problem !== null) return fail(`${p.name}: the purpose must be ${problem}`);
  const expected = expectedVersion(String(p.spec), resolved);
  return expected.ok ? expected : fail(`${p.name}: ${expected.reason}`);
}

const missingText = ({ p, ecosystem, manifest }, expected) => {
  const held = manifest.filter(m => keyOf(ecosystem, m.name) === keyOf(ecosystem, p.name)).map(m => m.version);
  return `${p.name}@${expected} (spec ${p.spec}) is not in the manifest, which holds ${held.length > 0 ? held.join(', ') : 'no version of it'}`;
};

/** One offered package -> `ok({ row, id })` | `fail`. The row's version is the manifest entry's own. */
function offeredRow(args) {
  const { p, ecosystem, byId, resolved } = args;
  const expected = checkOffered(p, resolved);
  if (!expected.ok) return expected;
  const entry = byId.get(idOf(ecosystem, p.name, expected.value));
  return entry === undefined ? fail(missingText(args, expected.value)) : ok({ row: { name: p.name, version: entry.version, purpose: p.purpose }, id: idOf(ecosystem, p.name, entry.version) });
}

/** Every offered package of the recipe against the manifest -> `ok([{ row, id }])` | the first `fail`. */
function offeredRows({ packages, ecosystem, manifest, resolved }) {
  const byId = new Map(manifest.map(m => [idOf(ecosystem, m.name, m.version), m]));
  const found = [];
  for (const p of packages) {
    const r = offeredRow({ p, ecosystem, byId, manifest, resolved });
    if (!r.ok) return r;
    found.push(r.value);
  }
  return ok(found);
}

const table = (header, rows) => [header, header.replace(/[^|]+/g, '---'), ...rows];
const offeredTable = rows => table('| Package | Version | Purpose |', [...rows].sort(rowOrder).map(r => `| \`${r.name}\` | ${r.version} | ${r.purpose} |`));
const dependencyTable = rows => table('| Package | Version |', [...rows].sort(rowOrder).map(r => `| \`${r.name}\` | ${r.version} |`));

/** The record's and the recipe's part of a stack, or the refusal that one of them lacks it. */
function sourcesOf({ stack, record, recipe }) {
  const parts = { ecosystem: ECOSYSTEM_OF[stack], snap: record?.[stack], spec: recipe?.stacks?.[stack] };
  return Object.values(parts).includes(undefined) ? fail(`PACKAGES.md of stack '${stack}': the record and the recipe must both have it (legal stacks: ${Object.keys(ECOSYSTEM_OF).join(', ')})`) : ok(parts);
}

const noteLines = spec => (spec.packagesNote === undefined ? [] : [spec.packagesNote]);

/** `{ stack, record, recipe }` -> `ok(text)` | `fail(reason)`. `record` is the whole `inputs/cache-snapshots-lang.json`, `recipe` the loaded recipe. */
export function renderPackagesMd({ stack, record, recipe }) {
  const sources = sourcesOf({ stack, record, recipe });
  if (!sources.ok) return sources;
  const { ecosystem, snap, spec } = sources.value;
  const offered = offeredRows({ packages: spec.packages, ecosystem, manifest: snap.packages, resolved: snap.resolved ?? {} });
  if (!offered.ok) return offered;
  const used = new Set(offered.value.map(r => r.id));
  const rest = snap.packages.filter(m => !used.has(idOf(ecosystem, m.name, m.version)));
  return ok([
    '# Packages', '', SENTENCE_OF[stack], INSTALL_LINE, ...noteLines(spec), '',
    `Cache snapshot: tree sha256 \`${snap.treeSha256}\`, made ${snap.madeOn}.`, '',
    '## Offered', '', ...offeredTable(offered.value.map(r => r.row)), '',
    '## In the cache as dependencies of the offered packages', '', ...dependencyTable(rest),
  ].join('\n') + '\n');
}
