// inputs/specs.json is generated from the writers' files in inputs/specs.d/ and never edited by hand (E1 plan section 3).
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { assemble, canonicalJson, checkRecord, WRITER_FILES } from '../lib/specs-record.mjs';
import { owned } from './support/owned.mjs';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));

/** An inputs folder with the writers' files given, in an owned folder removed when the test ends. */
function inputsFolder(t, writers) {
  const inputs = path.join(owned.ownedDir(t, 'specs'), 'inputs');
  mkdirSync(path.join(inputs, 'specs.d'), { recursive: true });
  for (const [name, value] of Object.entries(writers)) writeFileSync(path.join(inputs, 'specs.d', name), JSON.stringify(value));
  return inputs;
}

test('The_specs_record_is_the_assembly_of_its_writers', () => {
  const verdict = checkRecord(path.join(spikeDir, 'inputs'));
  assert.equal(verdict.ok, true, verdict.reason);
});

test('A_hand_edit_of_the_specs_record_is_found_by_the_companion_check', t => {
  const inputs = inputsFolder(t, { 'runner.json': { runner: { version: '1' } } });
  writeFileSync(path.join(inputs, 'specs.json'), assemble(inputs).text);
  assert.equal(checkRecord(inputs).ok, true);
  writeFileSync(path.join(inputs, 'specs.json'), assemble(inputs).text.replace('"1"', '"2"'));
  const verdict = checkRecord(inputs);
  assert.equal(verdict.ok, false);
  assert.match(verdict.reason, /hand|regenerate|differs/);
});

test('The_assembly_merges_the_writers_top_level_keys_with_sorted_keys_LF_and_a_final_newline', t => {
  const inputs = inputsFolder(t, { 'runner.json': { runner: { z: 1, a: 2 } }, 'format.json': { format: { v: 2 }, grammar: 2 } });
  const { text } = assemble(inputs);
  assert.equal(text, '{\n  "format": {\n    "v": 2\n  },\n  "grammar": 2,\n  "runner": {\n    "a": 2,\n    "z": 1\n  }\n}\n');
  assert.ok(!text.includes('\r'));
});

test('Two_writers_that_claim_one_key_are_refused_instead_of_one_silently_winning', t => {
  const inputs = inputsFolder(t, { 'runner.json': { runner: 1 }, 'format.json': { runner: 2 } });
  const result = assemble(inputs);
  assert.equal(result.ok, false);
  assert.match(result.reason, /runner/);
});

test('A_file_in_specs_d_that_is_no_writers_is_refused_and_a_writer_not_yet_written_is_skipped', t => {
  assert.equal(assemble(inputsFolder(t, { 'surprise.json': {} })).ok, false);
  assert.equal(assemble(inputsFolder(t, { 'runner.json': { runner: {} } })).ok, true);
});

test('The_writer_files_are_the_four_the_plan_names', () => {
  assert.deepEqual([...WRITER_FILES].sort(), ['exec-layer.json', 'format.json', 'runner.json', 'spec-states.json']);
});

test('Canonical_JSON_sorts_keys_at_every_depth_and_keeps_array_order', () => {
  assert.equal(canonicalJson({ b: [{ y: 1, x: 2 }, 3], a: null }), '{\n  "a": null,\n  "b": [\n    {\n      "x": 2,\n      "y": 1\n    },\n    3\n  ]\n}\n');
});

test('A_malformed_writer_file_is_refused_naming_it', t => {
  const inputs = inputsFolder(t, {});
  writeFileSync(path.join(inputs, 'specs.d', 'runner.json'), '{nope');
  const result = assemble(inputs);
  assert.equal(result.ok, false);
  assert.match(result.reason, /runner\.json/);
});

test('The_committed_record_file_exists_and_is_LF_only', () => {
  const text = readFileSync(path.join(spikeDir, 'inputs', 'specs.json'), 'utf8');
  assert.ok(!text.includes('\r') && text.endsWith('\n'));
});
