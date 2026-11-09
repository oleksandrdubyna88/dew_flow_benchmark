// inputs/specs.json is generated from the writers' files in inputs/specs.d/ and never edited by hand (E1 plan section 3).
import assert from 'node:assert/strict';
import { cpSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { extractGrammar } from '../tools/grammar-extract.mjs';
import { assemble, canonicalJson, checkRecord, WRITER_FILES } from '../lib/specs-record.mjs';
import { renderRecord } from '../lib/conformance-record.mjs';
import { runSpecsRecord } from '../lib/specs-record-cli.mjs';
import { edit, FORMAT_TEXT, sha256 } from './support/format-fixtures.mjs';
import { owned } from './support/owned.mjs';
import { formatRow, git } from './support/workspace-fixtures.mjs';

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

test('The_writer_files_are_the_four_the_plan_names_and_the_memory_policy', () => {
  assert.deepEqual([...WRITER_FILES].sort(), ['exec-layer.json', 'format.json', 'memory-policy.json', 'runner.json', 'spec-states.json']);
});

test('The_memory_policy_file_keeps_its_own_shape_and_is_assembled_under_one_key', t => {
  const inputs = inputsFolder(t, { 'memory-policy.json': { policy: null, decided: null, by: null }, 'runner.json': { runner: {} } });
  const { text } = assemble(inputs);
  assert.deepEqual(JSON.parse(text), { memoryPolicy: { by: null, decided: null, policy: null }, runner: {} }, 'the file is { policy, decided, by }; the record holds it under memoryPolicy, not as three top-level keys');
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

// ---- the B entry of format.json and its writer (F2.S2 (a)) ------------------------------------------------------------------------------------------------

const SIX = Object.freeze(['04', '05', '06', '07', '08', '10']);
const formatFile = inputs => path.join(inputs, 'specs.d', 'format.json');
const readRecordIn = inputs => JSON.parse(readFileSync(formatFile(inputs), 'utf8'));
/** The bytes `A` had in `format.json` before `B` was added: the entry must stay exactly this. */
const A_BLOCK = `{
  "A": {
    "version": 2,
    "sha256": "6c3546bb03cdc70df32a6795b68c52d5c03d0c4efbb968824b1c177cc8cb5d47",
    "gate": "inputs/gates/FORMAT-v2.md",
    "grammarSha256": "8d87346f11d43d0ccdebec5d73116324fe3af43132f5887ff1fd89628c9d45f2",
    "tasks": ["01", "02", "03", "09"]
  }`;

test('The_B_entry_equals_A_but_for_its_tasks_and_A_is_unchanged', () => {
  const text = readFileSync(path.join(spikeDir, 'inputs', 'specs.d', 'format.json'), 'utf8');
  assert.ok(text.startsWith(A_BLOCK), 'A is byte for byte what it was');
  const record = JSON.parse(text);
  assert.deepEqual(Object.keys(record), ['A', 'B']);
  assert.equal(renderRecord(record), text, 'the file is what the writer renders: a second run of it changes no byte');
  assert.deepEqual(Object.keys(record.B), Object.keys(record.A), 'the same fields in the same order');
  const [{ tasks: aTasks, ...aRest }, { tasks: bTasks, ...bRest }] = [record.A, record.B];
  assert.deepEqual(bRest, aRest, 'the same version, sha256, gate and grammarSha256');
  assert.deepEqual(bTasks, SIX);
  assert.deepEqual(aTasks, ['01', '02', '03', '09']);
  assert.equal(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'specs.json'), 'utf8')).B.sha256, record.B.sha256, 'specs.json carries it');
});

const FORMAT_AT = 'todo/tasks/FORMAT.md';

/** A task repository of this test's own, one commit holding `bytes` as its FORMAT (or `extra` commits more): `{ repo, commit }`. */
async function taskRepo(t, { bytes = Buffer.from(FORMAT_TEXT), more = 0 } = {}) {
  const repo = owned.ownedDir(t, 'task');
  await git(repo, ['init', '-q', '-b', 'main']);
  mkdirSync(path.join(repo, 'todo', 'tasks'), { recursive: true });
  writeFileSync(path.join(repo, FORMAT_AT), bytes);
  await git(repo, ['add', '-A']);
  await git(repo, ['commit', '-q', '-m', 'format']);
  for (let n = 0; n < more; n += 1) await git(repo, ['commit', '-q', '--allow-empty', '-m', `more ${n}`]);
  return { repo, commit: await git(repo, ['rev-parse', 'HEAD']) };
}

/** An inputs folder whose `format.json` holds `A` only, for the synthetic FORMAT, and the specs.json assembled from it. */
function inputsWithA(t) {
  const inputs = path.join(owned.ownedDir(t, 'inp'), 'inputs');
  mkdirSync(path.join(inputs, 'specs.d'), { recursive: true });
  const { row } = formatRow(FORMAT_TEXT, ['01', '02', '03', '09']);
  const a = { version: row.version, sha256: row.sha256, gate: 'inputs/gates/FORMAT-v2.md', grammarSha256: row.grammarSha256, tasks: row.tasks };
  const fields = Object.entries(a).map(([key, value]) => `    ${JSON.stringify(key)}: ${Array.isArray(value) ? `[${value.map(item => JSON.stringify(item)).join(', ')}]` : JSON.stringify(value)}`);
  writeFileSync(formatFile(inputs), `{\n  "A": {\n${fields.join(',\n')}\n  }\n}\n`);
  writeFileSync(path.join(inputs, 'specs.json'), assemble(inputs).text);
  return inputs;
}

/** Every file under `dir` with its bytes, to say that a refusal changed nothing and left nothing behind. */
const snapshot = dir => readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => `${path.join(e.parentPath, e.name)}\n${readFileSync(path.join(e.parentPath, e.name), 'utf8')}`).sort().join('\n--\n');

const rewrite = (inputs, change) => {
  const record = readRecordIn(inputs);
  change(record);
  writeFileSync(formatFile(inputs), JSON.stringify(record, null, 2));
  writeFileSync(path.join(inputs, 'specs.json'), assemble(inputs).text);
};

test('The_B_entry_is_computed_from_FORMATs_bytes_and_a_typed_value_is_refused_by_check', async t => {
  const { repo, commit } = await taskRepo(t);
  const inputs = inputsWithA(t);
  const before = readFileSync(formatFile(inputs), 'utf8');
  const wrote = await runSpecsRecord(['--format-entry', 'B', '--task-repo', repo], { inputs, env: {}, commit });
  assert.equal(wrote.exit, 0, wrote.stderr);
  const record = readRecordIn(inputs);
  const bytes = Buffer.from(FORMAT_TEXT);
  assert.equal(record.B.sha256, sha256(bytes), 'from the FORMAT\'s bytes at the commit');
  assert.equal(record.B.grammarSha256, sha256(extractGrammar(bytes).bytes), 'through the grammar extractor');
  assert.deepEqual([record.B.version, record.B.gate, record.B.tasks], [record.A.version, record.A.gate, SIX]);
  assert.ok(readFileSync(formatFile(inputs), 'utf8').startsWith(before.replace(/\n}\n$/, ',\n')), 'A is byte for byte what it was');
  assert.deepEqual(JSON.parse(readFileSync(path.join(inputs, 'specs.json'), 'utf8')).B, record.B, 'specs.json is assembled from it');
  const again = snapshot(inputs);
  assert.equal((await runSpecsRecord(['--format-entry', 'B', '--task-repo', repo], { inputs, env: {}, commit })).exit, 0);
  assert.equal(snapshot(inputs), again, 'a second run changes nothing');
  const checked = await runSpecsRecord(['--format-entry', 'B', '--task-repo', repo, '--check'], { inputs, env: {}, commit });
  assert.equal(checked.exit, 0, checked.stderr);
  const other = await taskRepo(t, { bytes: Buffer.from(edit(FORMAT_TEXT, 'Strings and escapes.', 'Strings and escapes, as another FORMAT states them.')) });
  const recomputed = await runSpecsRecord(['--format-entry', 'B', '--task-repo', other.repo, '--check'], { inputs, env: {}, commit: other.commit });
  assert.equal(recomputed.exit, 2, 'the entry follows the FORMAT\'s bytes: other bytes are not the FORMAT A records');
  for (const [field, typed] of [['grammarSha256', 'f'.repeat(64)], ['sha256', 'e'.repeat(64)], ['tasks', ['04', '05']], ['gate', 'inputs/gates/other.md']]) {
    rewrite(inputs, edited => { edited.B[field] = typed; });
    const refused = await runSpecsRecord(['--format-entry', 'B', '--task-repo', repo, '--check'], { inputs, env: {}, commit });
    assert.equal(refused.exit, 1, `a typed ${field}`);
    assert.match(refused.stderr, new RegExp(field), `the check names ${field}`);
    rewrite(inputs, edited => { edited.B = record.B; });
  }
});

/** The task repository's checkout named by `LANG_V2_TASK_REPO`, or null when the case skipped (under BENCH_REQUIRE_SANDBOX=1 an unset variable throws). The writer reads the pinned commit, never the checkout's files. */
function realRepo(t) {
  const repo = process.env.LANG_V2_TASK_REPO;
  if (repo !== undefined && repo !== '') return repo;
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error('BENCH_REQUIRE_SANDBOX=1 and LANG_V2_TASK_REPO does not name the task repository checkout');
  t.skip('LANG_V2_TASK_REPO does not name the task repository checkout');
  return null;
}

test('The_committed_B_entry_is_what_the_writer_computes_from_the_task_repository', async t => {
  if (realRepo(t) === null) return;
  const checked = await runSpecsRecord(['--format-entry', 'B', '--check'], { env: process.env });
  assert.equal(checked.exit, 0, checked.stderr);
  const inputs = path.join(owned.ownedDir(t, 'real'), 'inputs');
  cpSync(path.join(spikeDir, 'inputs', 'specs.d'), path.join(inputs, 'specs.d'), { recursive: true });
  writeFileSync(path.join(inputs, 'specs.json'), assemble(inputs).text);
  rewrite(inputs, edited => { edited.B.grammarSha256 = '0'.repeat(64); });
  const typed = await runSpecsRecord(['--format-entry', 'B', '--check'], { inputs, env: process.env });
  assert.equal(typed.exit, 1);
  assert.match(typed.stderr, /grammarSha256/);
});

test('The_writer_refuses_without_the_task_repository_or_its_commit_and_writes_nothing', async t => {
  const inputs = inputsWithA(t);
  const good = await taskRepo(t);
  const full = await taskRepo(t, { more: 1 });
  const shallow = owned.ownedDir(t, 'shallow');
  const cloned = await runBounded('git', ['clone', '-q', '--depth', '1', `file://${full.repo}`, path.join(shallow, 'c')], { timeoutMs: 60_000, env: { PATH: '/usr/bin:/bin', HOME: '/nonexistent', GIT_CONFIG_GLOBAL: '/dev/null' } });
  assert.equal(cloned.ok && cloned.value.code, 0, 'the shallow clone is made');
  const plain = owned.ownedDir(t, 'plain');
  const changed = await taskRepo(t, { bytes: Buffer.from(edit(FORMAT_TEXT, 'Strings and escapes.', 'Strings and escapes, as a changed FORMAT states them.')) });
  const cases = [
    ['no repository named', [], {}, good.commit, /task repository is --task-repo or LANG_V2_TASK_REPO/],
    ['a folder that is not there', ['--task-repo', path.join(plain, 'none')], {}, good.commit, /none/],
    ['a folder that is no repository', ['--task-repo', plain], {}, good.commit, /not a git repository/],
    ['a commit the repository lacks', ['--task-repo', good.repo], {}, 'a'.repeat(40), /does not hold the commit/],
    ['a shallow repository', ['--task-repo', path.join(shallow, 'c')], {}, full.commit, /shallow/],
    ['a FORMAT that is not the one A records', ['--task-repo', changed.repo], {}, changed.commit, /which entry A records/],
    ['the repository of the environment, shallow', [], { LANG_V2_TASK_REPO: path.join(shallow, 'c') }, full.commit, /shallow/],
  ];
  const before = snapshot(inputs);
  for (const [why, flags, env, commit, sentence] of cases) {
    const result = await runSpecsRecord(['--format-entry', 'B', ...flags], { inputs, env, commit });
    assert.equal(result.exit, 2, why);
    assert.match(result.stderr, sentence, why);
    assert.equal(result.stderr.trim().split('\n').length, 1, `${why}: one sentence`);
    assert.equal(snapshot(inputs), before, `${why}: nothing was written or left behind`);
  }
  assert.equal((await runSpecsRecord(['--format-entry', 'C', '--task-repo', good.repo], { inputs, env: {}, commit: good.commit })).exit, 2, 'a label with no entry to write');
  const viaEnv = await runSpecsRecord(['--format-entry', 'B'], { inputs, env: { LANG_V2_TASK_REPO: good.repo }, commit: good.commit });
  assert.equal(viaEnv.exit, 0, 'the environment names the repository when the flag does not');
});

test('The_command_refuses_with_exit_2_when_no_task_repository_is_named_and_changes_nothing', async () => {
  const before = snapshot(path.join(spikeDir, 'inputs', 'specs.d')) + readFileSync(path.join(spikeDir, 'inputs', 'specs.json'), 'utf8');
  const ran = await runBounded(process.execPath, [path.join(spikeDir, 'specs-record.mjs'), '--format-entry', 'B'], { timeoutMs: 60_000, env: { PATH: '/usr/bin:/bin', HOME: '/nonexistent' } });
  assert.equal(ran.value.code, 2);
  assert.match(ran.value.stderr, /LANG_V2_TASK_REPO/);
  assert.equal(snapshot(path.join(spikeDir, 'inputs', 'specs.d')) + readFileSync(path.join(spikeDir, 'inputs', 'specs.json'), 'utf8'), before);
});
