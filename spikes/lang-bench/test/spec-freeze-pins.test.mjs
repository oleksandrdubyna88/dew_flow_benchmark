// Where the freeze reads the GATED bytes of the six specs from (F2.S2 (f), 2026-11-19): the registered packages' `specSha256` (`spikes/lang-explore/inputs/packages.json`), never the engine's pins in `explore.json`, whose spec
// sha256 is the FROZEN text once the freeze is accepted. Synthetic repositories for the refusals (test/support/freeze-fixtures.mjs); the real freeze commit through `LANG_V2_TASK_REPO` (skipped without it, a failure under BENCH_REQUIRE_SANDBOX=1).
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { runFreeze } from '../lib/freeze-cli.mjs';
import { DEFAULT_PINS, gatedProblems, loadSpecPins } from '../lib/freeze-pins.mjs';
import { git } from './support/push-fixtures.mjs';
import { realRepo } from './support/real-repo.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const EXPLORE = fileURLToPath(new URL('../../lang-explore/inputs/explore.json', import.meta.url));
const PACKAGES = fileURLToPath(new URL('../../lang-explore/inputs/packages.json', import.meta.url));
const STATES = fileURLToPath(new URL('../inputs/specs.d/spec-states.json', import.meta.url));
const SIX = ['04', '05', '06', '07', '08', '10'];
const json = file => JSON.parse(readFileSync(file, 'utf8'));

test('The_default_source_of_the_gated_hashes_is_the_registered_packages_and_it_differs_from_the_engine_pin_after_the_freeze', () => {
  assert.equal(path.resolve(DEFAULT_PINS), PACKAGES, 'the default file is packages.json, not explore.json');
  const pins = loadSpecPins();
  assert.equal(pins.ok, true, pins.reason);
  const packages = json(PACKAGES);
  const engine = json(EXPLORE).tasks;
  const { specs } = json(STATES);
  for (const id of SIX) {
    assert.equal(pins.value.specs.get(id), packages[id].specSha256, `task ${id}: the gated hash is the package's specSha256`);
    assert.notEqual(pins.value.specs.get(id), engine[id].spec.sha256, `task ${id}: the engine's pin is the frozen text, not the gated one`);
    assert.equal(engine[id].spec.sha256, specs[id].sha256, `task ${id}: and the engine's pin is the frozen text the record holds`);
  }
});

/** A packages-shaped file in an owned folder: -> its path. */
const packagesFile = (t, name, value) => {
  const file = path.join(ownedFolder(t, 'pins'), name);
  writeFileSync(file, typeof value === 'string' ? value : JSON.stringify(value));
  return file;
};
const HEX = 'a'.repeat(64);

test('A_package_without_a_well_formed_specSha256_refuses_the_whole_read_naming_the_task', t => {
  for (const bad of [undefined, 'zz', HEX.toUpperCase(), HEX.slice(1), 7, null]) {
    const r = loadSpecPins(packagesFile(t, 'bad.json', { '04': { specSha256: HEX }, '05': { specSha256: bad } }));
    assert.equal(r.ok, false, `a specSha256 of ${JSON.stringify(bad)}`);
    assert.match(r.reason, /specSha256 for task 05\b/);
    assert.doesNotMatch(r.reason, /task 04/);
  }
  assert.equal(loadSpecPins(packagesFile(t, 'good.json', { '04': { specSha256: HEX } })).ok, true, 'the control: a well-formed hash reads');
});

test('The_file_in_the_engines_shape_is_no_source_and_there_is_no_fallback_to_the_engine_pin', t => {
  const asEngine = packagesFile(t, 'engine.json', json(EXPLORE));
  const r = loadSpecPins(asEngine);
  assert.equal(r.ok, false, 'explore.json read as a registry names no package with a specSha256');
  assert.match(r.reason, /specSha256/);
  for (const [name, text] of [['empty.json', '[]'], ['text.json', '"x"'], ['broken.json', '{']]) assert.equal(loadSpecPins(packagesFile(t, name, text)).ok, false, name);
  assert.equal(loadSpecPins(path.join(ownedFolder(t, 'pins'), 'absent.json')).ok, false);
});

test('A_task_with_no_registered_package_has_no_gated_bytes_and_its_spec_is_refused', () => {
  const pins = { file: 'packages.json', specs: new Map([['05', HEX]]) };
  const problems = gatedProblems({ spec: { task: '04', path: 'todo/tasks/04-rule-api.md' }, bytes: Buffer.from('x'), facts: { header: { placeholders: { 'Freeze tag': 'none yet' } } }, pins });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /todo\/tasks\/04-rule-api\.md: packages\.json registers no package for task 04/);
});

// ---- the real freeze commit --------------------------------------------------------------------------------------------------------------------------

test('The_real_freeze_commit_passes_check_commit_with_the_default_gated_hashes_and_not_with_the_engines_frozen_pins', async t => {
  const repo = realRepo(t);
  if (repo === null) return;
  const c = json(STATES).freeze.commitSha;
  const before = git(repo, 'rev-parse', `${c}^`);
  const ran = (pins = []) => runFreeze(['check-commit', '--repo', repo, '--before', before, '--after', c, ...pins]);
  const real = await ran();
  assert.equal(real.exit, 0, `${real.stdout}${real.stderr}`);
  const engine = packagesFile(t, 'engine-pins.json', Object.fromEntries(SIX.map(id => [id, { specSha256: json(EXPLORE).tasks[id].spec.sha256 }])));
  const wrong = await ran(['--pins', engine]);
  assert.equal(wrong.exit, 1, 'the engine\'s frozen hashes are not the gated text: the control that the check can fail');
  assert.match(wrong.stderr, /not the gated text/);
});
