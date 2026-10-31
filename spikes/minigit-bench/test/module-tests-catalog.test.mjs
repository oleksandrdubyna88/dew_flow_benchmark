// research/module_tests.md names every flow of the sandbox's scenario harness, and the list is DERIVED (the shared rule common.scenario-tests, point 4):
// the verdict ids come from SMOKE_ASSUMPTIONS and the test files from the directory, so a verdict or a test file added without a row in the section
// turns this red, and a row that names a test file which does not exist cannot stay.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { SMOKE_ASSUMPTIONS } from '../lib/smoke-verdicts.mjs';
import { spikeDir } from './helpers.mjs';

const repoRoot = path.join(spikeDir, '..', '..');
const HEADING = '## The minigit stack bench scenarios';

/** The section of the catalogue that covers this spike: from its heading to the next level-2 heading. */
function section() {
  const text = readFileSync(path.join(repoRoot, 'research', 'module_tests.md'), 'utf8');
  const start = text.indexOf(HEADING);
  assert.ok(start >= 0, `research/module_tests.md has no section "${HEADING}…"`);
  const rest = text.slice(start + HEADING.length);
  const end = rest.search(/^## /m);
  return rest.slice(0, end < 0 ? undefined : end);
}

test('Every_test_file_of_the_spike_is_named_in_the_catalogue_section', () => {
  const body = section();
  const files = readdirSync(path.join(spikeDir, 'test')).filter(f => f.endsWith('.test.mjs'));
  assert.ok(files.length > 50, `${files.length} test files found`);
  const missing = files.filter(f => !body.includes(`test/${f}`));
  assert.deepEqual(missing, [], 'a test file that no row names is a flow nobody catalogued');
});

test('Every_verdict_id_of_the_smoke_is_named_in_the_catalogue_section', () => {
  const body = section();
  const missing = SMOKE_ASSUMPTIONS.map(([id]) => id).filter(id => !body.includes(`\`${id}\``));
  assert.deepEqual(missing, []);
});

test('A_row_that_names_a_test_file_names_one_that_exists', () => {
  const body = section();
  const named = [...body.matchAll(/`((?:spikes\/[^`\s*]+|test\/[^`\s*]+)\.test\.mjs)`/g)].map(m => m[1]);
  assert.ok(named.length > 50, `${named.length} test files named`);
  const absent = named.filter(f => !existsSync(f.startsWith('spikes/') ? path.join(repoRoot, f) : path.join(spikeDir, f)));
  assert.deepEqual(absent, []);
  assert.equal(/\bcovered\b/.test(body) && /\bnot covered\b/.test(body), true, 'the section marks flows both covered and not covered, with a reason for the latter');
});
