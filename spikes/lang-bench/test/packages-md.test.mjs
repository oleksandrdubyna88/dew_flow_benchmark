// E1.S4: PACKAGES.md (`lib/packages-md.mjs`) is derived, never typed: the versions come from the registered manifest, the purposes from the recipe, and a package the manifest does not hold is a refusal.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { renderPackagesMd } from '../lib/packages-md.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const HASH = 'a'.repeat(64);
const pkg = (name, version, ecosystem = 'npm') => ({ ecosystem, name, version, integrity: 'sha512-x' });

const recipe = () => ({
  stacks: {
    node: {
      packages: [
        { name: 'pino', spec: '{PINO}', purpose: 'Structured logging' },
        { name: 'fastify', spec: '5.12.7', purpose: 'HTTP routing; lifecycle hooks and close()' },
      ],
      packagesNote: 'Types come with the packages.',
    },
    csharp: { packages: [{ name: 'Microsoft.Extensions.Hosting', spec: '{HOSTING}', purpose: 'Generic Host' }] },
  },
});
const record = () => ({
  node: { treeSha256: HASH, madeOn: '2026-11-05', resolved: { PINO: '10.1.0' }, packages: [pkg('pino', '9.14.0'), pkg('fastify', '5.12.7'), pkg('pino', '10.1.0'), pkg('abstract-logging', '2.0.1')] },
  csharp: { treeSha256: 'b'.repeat(64), madeOn: '2026-11-05', resolved: { HOSTING: '10.0.14' }, packages: [pkg('microsoft.extensions.hosting', '10.0.14', 'nuget'), pkg('microsoft.extensions.logging', '10.0.14', 'nuget')] },
});

// fix batch 1 (M3): the sentence an agent reads, per stack, in the team lead's words
const CS_SENTENCE = 'Only the packages and versions listed here are available; there is no network. A reference to a lower version than the one listed resolves to the listed version, with the warning NU1603, which fails the build where warnings are errors; a higher version fails with NU1102, and a package not listed fails with NU1101; both errors name the package.';
const TS_SENTENCE = 'Only the packages and versions listed here are available; there is no network. A package or version not listed fails to install with ENOTCACHED; the message may name a dependency of the package rather than the package itself.';

const EXPECTED_NODE = `# Packages

${TS_SENTENCE}
Install a package with the exact version listed (C#: \`<PackageReference Include="NAME" Version="VERSION" />\`; Node: \`npm install NAME@VERSION\`).
Types come with the packages.

Cache snapshot: tree sha256 \`${HASH}\`, made 2026-11-05.

## Offered

| Package | Version | Purpose |
|---|---|---|
| \`fastify\` | 5.12.7 | HTTP routing; lifecycle hooks and close() |
| \`pino\` | 10.1.0 | Structured logging |

## In the cache as dependencies of the offered packages

| Package | Version |
|---|---|
| \`abstract-logging\` | 2.0.1 |
| \`pino\` | 9.14.0 |
`;

const render = (rec = record(), rcp = recipe(), stack = 'node') => renderPackagesMd({ stack, record: rec, recipe: rcp });
const refusal = (rec, rcp, stack) => { const r = render(rec, rcp, stack); assert.equal(r.ok, false, 'the render is refused'); return r.reason; };

test('PACKAGES_md_is_derived_from_the_manifest', () => {
  const r = render();
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value, EXPECTED_NODE, 'byte for byte');
  const noNote = recipe();
  delete noNote.stacks.node.packagesNote;
  assert.doesNotMatch(render(record(), noNote).value, /Types come with/, 'the note line is only there when the stack has one');
  // a dependency's version changes in the manifest: that row changes
  const dep = record();
  dep.node.packages[3].version = '2.0.2';
  assert.match(render(dep).value, /\| `abstract-logging` \| 2\.0\.2 \|/);
  // an x-range package shows the manifest's version, never the spec text
  const moved = record();
  moved.node.resolved.PINO = '10.2.0';
  moved.node.packages[2].version = '10.2.0';
  assert.match(render(moved).value, /\| `pino` \| 10\.2\.0 \| Structured logging \|/);
  assert.doesNotMatch(r.value, /\{PINO\}/, 'the spec text is never shown');
  // an offered package whose manifest version is not the one the recipe expects: refused, naming the package and both values
  const wrong = record();
  wrong.node.packages[1].version = '5.12.8';
  const why = refusal(wrong);
  assert.match(why, /fastify/);
  assert.match(why, /5\.12\.7/);
  assert.match(why, /5\.12\.8|not in the manifest/);
  // an offered package missing from the manifest: refused, naming it
  const missing = record();
  missing.node.packages.splice(1, 1);
  assert.match(refusal(missing), /fastify@5\.12\.7/);
  // NuGet names compare without case; the offered row keeps the recipe's spelling
  const cs = render(record(), recipe(), 'csharp');
  assert.equal(cs.ok, true, cs.reason);
  assert.match(cs.value, /\| `Microsoft\.Extensions\.Hosting` \| 10\.0\.14 \| Generic Host \|/);
  assert.match(cs.value, /\| `microsoft\.extensions\.logging` \| 10\.0\.14 \|/);
  assert.equal(render(record(), recipe(), 'ruby').ok, false, 'a stack with no record is refused');
  assert.match(refusal(record(), (() => { const x = recipe(); x.stacks.node.packages[0].spec = '{GHOST}'; return x; })()), /GHOST/, 'a placeholder the record did not resolve is refused');
});

test('The_real_recipe_renders_for_both_stacks_from_a_fixture_record', () => {
  const real = JSON.parse(readFileSync(path.join(SPIKE, 'inputs', 'cache-recipe-lang.json'), 'utf8'));
  for (const [stack, ecosystem] of [['csharp', 'nuget'], ['node', 'npm']]) {
    const spec = real.stacks[stack];
    const resolved = Object.fromEntries(Object.keys(spec.resolve).map(k => [k, '1.2.3']));
    const packages = spec.packages.map(p => pkg(p.name.toLowerCase() === p.name ? p.name : p.name.toLowerCase(), p.spec.replace(/\{([A-Z_]+)\}/, (m, key) => resolved[key]), ecosystem));
    const r = renderPackagesMd({ stack, record: { [stack]: { treeSha256: HASH, madeOn: '2026-11-05', resolved, packages } }, recipe: real });
    assert.equal(r.ok, true, r.reason);
    for (const p of spec.packages) assert.ok(r.value.includes(`| \`${p.name}\` |`), `${p.name} is in the offered table`);
  }
});

test('The_rendering_is_byte_stable_and_sorted', () => {
  const shuffled = record();
  shuffled.node.packages.reverse();
  const rcp = recipe();
  rcp.stacks.node.packages.reverse();
  assert.equal(render(shuffled, rcp).value, EXPECTED_NODE, 'the order of the manifest and of the recipe does not matter');
  const many = recipe();
  many.stacks.node.packages = [{ name: 'Zeta', spec: '1.0.0', purpose: 'z' }, { name: 'alpha', spec: '1.0.0', purpose: 'a' }, { name: 'Alpha', spec: '1.0.0', purpose: 'A' }];
  const rec = { node: { treeSha256: HASH, madeOn: '2026-11-05', resolved: {}, packages: [pkg('Zeta', '1.0.0'), pkg('alpha', '1.0.0'), pkg('Alpha', '1.0.0'), pkg('dep', '10.1.0'), pkg('dep', '9.14.0'), pkg('dep', '9.2.0')] } };
  const text = render(rec, many).value;
  const rows = text.split('\n').filter(l => l.startsWith('| `'));
  assert.deepEqual(rows.map(l => l.split('`')[1] + l.split('|')[2]), ['Alpha 1.0.0 ', 'alpha 1.0.0 ', 'Zeta 1.0.0 ', 'dep 9.2.0 ', 'dep 9.14.0 ', 'dep 10.1.0 '], 'lower-case name, then the name, then the version by its numeric parts');
  assert.ok(text.endsWith('|\n') && !text.endsWith('\n\n'), 'one final newline');
  assert.equal(text.includes('\r'), false, 'LF only');
  assert.equal(render(rec, many).value, text, 'the same input gives the same bytes');
});

test('A_purpose_with_a_table_breaking_character_is_refused', () => {
  for (const purpose of ['a | b', 'two\nlines', 'cr\rlf', '', '   ']) {
    const rcp = recipe();
    rcp.stacks.node.packages[1].purpose = purpose;
    const why = refusal(record(), rcp);
    assert.match(why, /fastify.*purpose|purpose.*fastify/, `${JSON.stringify(purpose)} names the package`);
  }
});

test('The_agent_visible_sentence_is_the_one_written_for_the_stack_and_the_old_one_is_gone', () => {
  const cs = render(record(), recipe(), 'csharp').value.split('\n');
  assert.equal(cs[2], CS_SENTENCE, 'C#: the third line, one paragraph');
  const ts = render().value.split('\n');
  assert.equal(ts[2], TS_SENTENCE, 'TypeScript: the third line, one paragraph');
  for (const text of [cs.join('\n'), ts.join('\n')]) assert.doesNotMatch(text, /This workspace builds offline/, 'the earlier wording is not rendered');
  assert.match(ts[3], /^Install a package with the exact version listed/, 'the instruction line stays');
});
