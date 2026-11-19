// What the agents under test are given in a quick-run workspace (plan item 5): the same bytes in both stacks wherever the file is not the stack's own, and nothing that names the comparison, a plan, the private side or a held-out
// case. The workspace is built from the committed templates and runner with an invented spec and the committed FORMAT fixture; nothing starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { realRepo } from '../../lang-bench/test/support/real-repo.mjs';
import { resolveCommit } from '../lib/leak-git.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { packagesInputs, quickWorkspaceFiles } from '../lib/workspace-quick.mjs';
import { blobAt } from '../lib/workspaces-sources.mjs';
import { EXPLORE, quickTask } from './quick-leak-fixture.mjs';

const FORMAT = readFileSync(fileURLToPath(new URL('../../lang-bench/test/fixtures/conformance/FORMAT.md', import.meta.url)), 'utf8');
const SPEC = ['# Task 05 — An invented service', '', '## 5. Build', '', '## 6. Visible samples', '', '### s01', '', '`samples/s01.in`', '', '```text', 'scenario 2', 'start ready port within 10s', '```', '', '`samples/s01.out`', '', '```text', 'transcript 2', 'ready', '```', '', '## 7. Hidden tests', '', 'text', ''].join('\n');
const EXPLORE_Q = quickExplore(EXPLORE, '/home/nobody');

const filesOf = (stack, id) => {
  const r = quickWorkspaceFiles({ explore: EXPLORE_Q, task: quickTask(id), stack, sources: { spec: SPEC, format: FORMAT }, inputs: packagesInputs().value });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};

/** The paths whose bytes the two stacks share, and the paths that differ or exist in one only. */
function compare(a, b) {
  const paths = [...new Set([...Object.keys(a), ...Object.keys(b)])].sort();
  return { same: paths.filter(p => a[p] && b[p] && a[p].bytes.equals(b[p].bytes)), other: paths.filter(p => !(a[p] && b[p] && a[p].bytes.equals(b[p].bytes))) };
}

for (const id of ['05', '10']) {
  test(`The_workspace_of_task_${id}_gives_both_stacks_the_same_samples_runner_grammar_and_statement_up_to_the_stack_sentence`, () => {
    const cs = filesOf('csharp', id);
    const node = filesOf('node', id);
    const { same, other } = compare(cs.files, node.files);
    for (const prefix of ['samples/', 'runner/']) assert.ok(same.some(p => p.startsWith(prefix)), `${prefix} is shared`);
    // the one file under samples/ that differs is the load configuration, by its per-stack heap block (the plan's one memory value; quick-memory-value.test.mjs holds the rest of it)
    assert.deepEqual(other.filter(p => (p.startsWith('samples/') && p !== 'samples/load.json') || p.startsWith('runner/')), [], 'no sample and no runner file differs between the stacks');
    assert.ok(same.includes('runner/GRAMMAR.md'), 'the grammar of the scenarios is the same file');
    const statement = f => f.files['STATEMENT.md'].bytes.toString('utf8');
    assert.ok(statement(cs).startsWith(SPEC) && statement(node).startsWith(SPEC), 'both statements begin with the spec, byte for byte');
    assert.notEqual(statement(cs), statement(node), 'and differ in the stack sentence that follows it');
  });

  test(`The_frozen_files_of_task_${id}_are_a_subset_of_the_files_with_the_same_bytes`, () => {
    for (const stack of ['csharp', 'node']) {
      const { files, frozen } = filesOf(stack, id);
      for (const [rel, f] of Object.entries(frozen)) assert.ok(files[rel] && files[rel].bytes.equals(f.bytes), `${stack}: ${rel}`);
      assert.ok(Object.keys(frozen).some(p => p.startsWith('samples/')) && Object.keys(frozen).includes('STATEMENT.md'));
    }
  });
}

const NAMES_THE_STUDY = /language comparison|\bC# (?:against|versus|vs)|(?:against|versus|vs) (?:C#|TypeScript|Node)|PLAN_lang|todo\/|explore\.mjs|rsd\/|lang-v2|heldout-\d|refs-\d|traps\.txt|owner's quick run/i;

test('No_file_of_a_quick_workspace_names_the_comparison_a_plan_the_private_side_or_a_held_out_case', () => {
  for (const stack of ['csharp', 'node']) {
    for (const [rel, f] of Object.entries(filesOf(stack, '05').files)) {
      const hit = NAMES_THE_STUDY.exec(f.bytes.toString('utf8'));
      assert.equal(hit, null, `${stack}: ${rel} names ${hit?.[0]}`);
    }
  }
});

test('The_scan_for_study_words_does_find_one_so_a_clean_result_is_not_an_empty_scan', () => {
  assert.ok(NAMES_THE_STUDY.test('this is the language comparison of the owner'));
  assert.ok(NAMES_THE_STUDY.test('see rsd/lang-v2/explore'));
});

// ---- the spec pins of the engine and the freeze record (F2.S2 (f) item 2): the pins live in two files, so a test holds them equal ----------------------------------------------------------------------------------------

const readData = rel => JSON.parse(readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8'));
const exploreData = () => readData('../inputs/explore.json');
const pinsOfEngine = () => exploreData().tasks;
const recordOfFreeze = () => readData('../../lang-bench/inputs/specs.d/spec-states.json');

const sorted = ids => [...ids].sort();

/** The sentence when the three lists of the freeze's tasks disagree (the quick plan's `quick.planTasks`, the tasks marked `quick`, the record's specs), else null. */
function taskListProblem(explore, record) {
  const planned = sorted(explore.quick.planTasks);
  const marked = sorted(Object.entries(explore.tasks).filter(([, task]) => task.quick === true).map(([id]) => id));
  const recorded = sorted(Object.keys(record.specs));
  if (JSON.stringify(planned) !== JSON.stringify(recorded)) return `the quick plan's tasks ${planned} are not the record's specs ${recorded}`;
  return JSON.stringify(planned) === JSON.stringify(marked) ? null : `the quick plan's tasks ${planned} are not the tasks marked quick ${marked}`;
}

/** The six tasks of the freeze, from the quick plan's list (`quick.planTasks`, the catalog of the quick tasks); a list that disagrees with the record or the marks fails the test that asks. */
function freezeTasks() {
  const problem = taskListProblem(exploreData(), recordOfFreeze());
  assert.equal(problem, null, problem ?? '');
  return exploreData().quick.planTasks;
}

test('The_tasks_of_the_freeze_are_the_quick_plan_the_quick_marks_and_the_records_specs_and_a_list_that_disagrees_is_a_finding', () => {
  const explore = exploreData();
  const record = recordOfFreeze();
  assert.equal(taskListProblem(explore, record), null);
  assert.equal(explore.quick.planTasks.length, 6, 'the six');
  const fewer = structuredClone(record);
  delete fewer.specs['10'];
  assert.match(taskListProblem(explore, fewer), /not the record's specs/, 'a spec missing from the record');
  const more = structuredClone(explore);
  more.tasks['01'].quick = true;
  assert.match(taskListProblem(more, record), /not the tasks marked quick/, 'a task marked quick that the plan does not list');
});

test('The_engine_pins_of_the_six_specs_equal_the_freeze_record', () => {
  const engine = pinsOfEngine();
  const { freeze, specs } = recordOfFreeze();
  assert.equal(freeze.state, 'accepted', 'the record says the freeze is accepted');
  for (const id of freezeTasks()) {
    const pin = engine[id].spec;
    assert.equal(pin.sha256, specs[id].sha256, `task ${id}: the engine's spec sha256 is the record's`);
    assert.equal(pin.path, specs[id].path, `task ${id}: the engine's spec path is the record's`);
    assert.equal(pin.sha, specs[id].commitSha.slice(0, 7), `task ${id}: the engine names the commit by its seven-character prefix, as every other pin, and it is the record's commit`);
    assert.equal(engine[id].draft, false, `task ${id}: a frozen spec is not a draft`);
    assert.equal(engine[id].format.sha256, freeze.formatSha256, `task ${id}: the engine's FORMAT pin is the frozen FORMAT's sha256`);
  }
});

test('The_frozen_spec_blobs_at_the_freeze_commit_hash_to_the_record_and_to_the_engine_pins', async t => {
  const repo = realRepo(t);
  if (repo === null) return;
  const engine = pinsOfEngine();
  const { freeze, specs } = recordOfFreeze();
  for (const id of freezeTasks()) {
    const pin = engine[id].spec;
    const blob = await blobAt({ repo, sha: freeze.commitSha, file: specs[id].path });
    assert.equal(blob.ok, true, blob.reason);
    const hash = createHash('sha256').update(blob.value).digest('hex');
    assert.equal(hash, specs[id].sha256, `task ${id}: the blob at C hashes to the record's sha256`);
    assert.equal(hash, pin.sha256, `task ${id}: the blob at C hashes to the engine's sha256`);
    const resolved = await resolveCommit(repo, pin.sha);
    assert.equal(resolved.ok, true, resolved.reason);
    assert.equal(resolved.value, freeze.commitSha, `task ${id}: the engine's spec.sha ${pin.sha} resolves to the freeze commit C`);
  }
});
