// What the agents under test are given in a quick-run workspace (plan item 5): the same bytes in both stacks wherever the file is not the stack's own, and nothing that names the comparison, a plan, the private side or a held-out
// case. The workspace is built from the committed templates and runner with an invented spec and the committed FORMAT fixture; nothing starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { quickExplore } from '../lib/quick.mjs';
import { packagesInputs, quickWorkspaceFiles } from '../lib/workspace-quick.mjs';
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
    assert.deepEqual(other.filter(p => p.startsWith('samples/') || p.startsWith('runner/')), [], 'no sample and no runner file differs between the stacks');
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
