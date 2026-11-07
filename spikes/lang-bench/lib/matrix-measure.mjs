// F1.S3 (a): the rows of the package matrix, measured. Every package of the recipe in every kind of its stack, one probe at a time (`probe({ stack, kind, name, version })` is the seam: the real one builds in the box, a test passes a fake).
// The first probe that cannot run ends the measurement; nothing partial is returned. -> `ok(rows)` with `rows[stack][name][kind] = { state, code? }`, or `fail`.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { KINDS } from './cache-recipe-lang.mjs';
import { recipeVersions } from './packages-md.mjs';

/** The probes of one package in every kind: `ok([[kind, row]])` | `fail`. */
async function rowsOfPackage({ stack, name, version, probe, log }) {
  const found = [];
  for (const kind of KINDS) {
    const result = await probe({ stack, kind, name, version });
    if (!result.ok) return fail(`${stack}/${kind}/${name}@${version}: ${result.reason}`);
    log(`${stack} ${kind} ${name}@${version}: ${result.value.state}${result.value.code === undefined ? '' : ` ${result.value.code}`}`);
    found.push([kind, result.value]);
  }
  return ok(found);
}

/** One stack's packages: `ok([[name, { kind: row }]])` | `fail`. */
async function rowsOfStack({ stack, record, recipe, probe, log }) {
  const versions = recipeVersions({ stack, record, recipe });
  if (!versions.ok) return versions;
  const found = [];
  for (const { name, version } of versions.value) {
    const rows = await rowsOfPackage({ stack, name, version, probe, log });
    if (!rows.ok) return rows;
    found.push([name, Object.fromEntries(rows.value)]);
  }
  return ok(found);
}

/** Every row of the recipe's packages: `ok({ <stack>: { <name>: { <kind>: row } } })` | `fail`. */
export async function measureRows({ recipe, record, probe, log = () => {} }) {
  const found = [];
  for (const stack of Object.keys(recipe.stacks)) {
    const rows = await rowsOfStack({ stack, record, recipe, probe, log });
    if (!rows.ok) return rows;
    found.push([stack, Object.fromEntries(rows.value)]);
  }
  return ok(Object.fromEntries(found));
}

/** The matrix a measurement makes: the day, the reason, the snapshot each stack and the templates it was measured against, the rows. */
export const matrixOf = ({ rows, record, today, reason, templatesIdentity }) => ({
  decidedOn: today,
  reason,
  measuredAgainst: { snapshots: Object.fromEntries(Object.keys(rows).map(stack => [stack, record[stack].treeSha256])), templatesIdentity },
  rows,
});
