// What the pinned runner's text may say (F1.S1 (f), parent T-L64): no comment or docstring of a `runner/*.py` file names a stack, the few string literals that are behaviour are exact approved entries, and the push check's
// agent-visible scan covers the runner of the task repository and every built workspace's copy. The Python reading is started from here under `python3 -I -B` (tokenize for comments, ast for docstrings and literals).
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { parseKnownNeedles } from '../lib/push-needles.mjs';
import { findVisibleRules } from '../lib/push-visible.mjs';
import { pythonTextItems } from '../lib/py-text.mjs';
import { needleScan } from '../lib/run-base-tree.mjs';
import { approvedLiterals, commentFindings, literalProblems, pythonReading, stackMatchers } from '../lib/runner-text.mjs';
import { helloWorkspace } from './support/workspace-fixtures.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const RUNNER = fileURLToPath(new URL('../runner', import.meta.url));
const known = parseKnownNeedles(JSON.parse(readFileSync(fileURLToPath(new URL('../inputs/needles-lang.json', import.meta.url)), 'utf8'))).value;
const matchers = stackMatchers().value;
const runnerFiles = () => readdirSync(RUNNER).filter(name => name.endsWith('.py')).sort();
const realReading = async () => {
  const read = await pythonReading('items', runnerFiles().map(name => path.join(RUNNER, name)));
  assert.equal(read.ok, true, read.reason);
  for (const [file, found] of Object.entries(read.value)) assert.equal(found.error, undefined, `${file} could not be read: ${found.error}`);
  return Object.fromEntries(Object.entries(read.value).map(([file, found]) => [path.basename(file), found]));
};
const scratch = (t, name, text) => {
  const dir = ownedFolder(t, 'rtext');
  mkdirSync(dir, { recursive: true });
  writeFileSync(path.join(dir, name), text);
  return path.join(dir, name);
};
const planted = async (t, name, text) => {
  const file = scratch(t, name, text);
  const read = await pythonReading('items', [file]);
  assert.equal(read.ok, true, read.reason);
  return { [name]: read.value[file] };
};

test('No_comment_or_docstring_of_a_runner_file_names_a_stack', async () => {
  const found = commentFindings(await realReading(), matchers);
  assert.deepEqual(found, [], 'a comment or a docstring that names a stack tells each agent what the other side is');
});

test('The_scan_of_comments_and_docstrings_finds_a_planted_stack_word_in_both_so_a_pass_is_not_an_empty_scan', async t => {
  const reading = await planted(t, 'rs_plant.py', '"""A module that says .NET in its docstring."""\n# and V8 in a comment\nx = 1\ndef f():\n    """TypeScript here."""\n');
  assert.deepEqual(commentFindings(reading, matchers).map(f => [f.path, f.line, f.what]), [
    ['rs_plant.py', 2, 'stack word in a comment'], ['rs_plant.py', 1, 'stack word in a docstring'], ['rs_plant.py', 5, 'stack word in a docstring'],
  ]);
});

test('The_detection_strings_are_kept_as_exact_approved_entries_and_nothing_else_is', async t => {
  const approved = approvedLiterals();
  assert.equal(approved.ok, true, approved.reason);
  const problems = literalProblems(await realReading(), matchers, approved.value);
  assert.deepEqual(problems.findings, [], 'a stack word in a string literal that is not an approved entry');
  assert.deepEqual(problems.stale, [], 'an approved entry that no literal equals any more');
  assert.ok(approved.value.length >= 3 && approved.value.every(e => e.reason.length > 0), 'every entry is exact and has its reason');
  const reading = await planted(t, 'rs_plant.py', 'LABEL = "dotnet"\nOTHER = "JavaScript"\nFINE = "plain"\n');
  const some = literalProblems(reading, matchers, [{ text: 'dotnet', reason: 'a label' }, { text: 'a text no file holds', reason: 'a stale entry' }]);
  assert.deepEqual(some.findings.map(f => [f.line, f.what]), [[2, 'stack word in a string literal']], 'only the literal that is not approved is a finding');
  assert.deepEqual(some.stale, ['a text no file holds'], 'a stale entry is a finding too, so the list cannot grow quietly');
});

const PLANTED = '"""A module."""\n\n# a plain comment\nx = 1  # the .NET side\n';

test('The_push_check_scans_the_pinned_runner_and_refuses_a_stack_name_in_its_comments', () => {
  for (const name of runnerFiles()) {
    const found = findVisibleRules(`runner/${name}`, readFileSync(path.join(RUNNER, name), 'utf8'), { known, stackWords: matchers });
    assert.deepEqual(found, [], `runner/${name}: the pinned runner gives none`);
  }
  const hits = findVisibleRules('runner/rs_plant.py', PLANTED, { known, stackWords: matchers });
  assert.deepEqual(hits.map(h => [h.path, h.line, h.what, h.group]), [['runner/rs_plant.py', 4, 'stack word in a comment', 'run-base-text']], 'a planted comment is reported with its path and line');
  assert.deepEqual(findVisibleRules('notes/plant.py', PLANTED, { known, stackWords: matchers }), [], 'the rule is for the runner folder');
  assert.deepEqual(findVisibleRules('runner/rs_plant.py', PLANTED, { known }), [], 'without the stack words the rule is off');
});

test('The_same_scan_finds_the_planted_line_in_a_workspace_copy_of_the_runner', t => {
  for (const stack of ['csharp', 'node']) {
    const w = helloWorkspace(t, { stack, kind: 'olympiad', source: null, label: `plant-${stack}` });
    const copy = path.join(w, 'runner');
    const names = readdirSync(copy).filter(name => name.endsWith('.py'));
    assert.deepEqual(names, runnerFiles(), `${stack}: the workspace holds the pinned runner`);
    for (const name of names) assert.deepEqual(findVisibleRules(`${stack}/01-x/runner/${name}`, readFileSync(path.join(copy, name), 'utf8'), { known, stackWords: matchers }), [], `${stack} ${name}`);
    const text = `${readFileSync(path.join(copy, 'rs_abort.py'), 'utf8')}\n# the .NET side\n`;
    const hits = findVisibleRules(`${stack}/01-x/runner/rs_abort.py`, text, { known, stackWords: matchers });
    assert.deepEqual(hits.map(h => h.what), ['stack word in a comment'], `${stack}: the planted line is found in the copy, at its real path in the task repository`);
    assert.equal(hits[0].line, text.split('\n').length - 1, `${stack}: at its line`);
  }
});

test('The_comment_reader_of_the_push_check_agrees_with_Pythons_tokenizer_on_every_runner_file', async () => {
  const read = await realReading();
  for (const name of runnerFiles()) {
    const mine = pythonTextItems(readFileSync(path.join(RUNNER, name), 'utf8'));
    assert.deepEqual(mine.comments.map(c => [c.line, c.text]), read[name].comments.map(c => [c.line, c.text]), `${name}: the comments`);
    const docs = new Set(mine.docstrings.map(d => d.line));
    assert.deepEqual(read[name].docstrings.filter(d => !docs.has(d.line)), [], `${name}: every docstring is found by the lexer`);
  }
});

test('The_lexer_reads_comments_strings_and_docstring_like_statements_the_way_Python_does', () => {
  const text = ['"""Module doc', 'on two lines."""', "x = '# not a comment'  # a comment", 'y = f"""a {x} # still not"""', 'def f(a,', '      b):  # after a bracket line', '    """Doc."""', '    s = "one" \\', '        "two"', '    return (', '        "inside brackets"', '    )', "'''a bare string statement'''", ''].join('\n');
  const found = pythonTextItems(text);
  assert.deepEqual(found.comments, [{ line: 3, text: '# a comment' }, { line: 6, text: '# after a bracket line' }]);
  assert.deepEqual(found.docstrings.map(d => [d.line, d.text]), [[1, 'Module doc\non two lines.'], [7, 'Doc.'], [13, 'a bare string statement']]);
});

test('The_run_base_scan_reads_the_runner_copies_of_the_task_repository_for_stack_words', t => {
  const root = ownedFolder(t, 'runbase');
  const copy = path.join(root, 'node', '01-x', 'runner');
  mkdirSync(copy, { recursive: true });
  writeFileSync(path.join(copy, 'rs_plant.py'), PLANTED);
  const found = needleScan({ root, known, stackWords: matchers });
  assert.deepEqual(found.value.map(h => [h.path, h.line, h.needle]), [['node/01-x/runner/rs_plant.py', 4, 'stack word in a comment']]);
  assert.deepEqual(needleScan({ root, known }).value, [], 'without the stack words the rule is off');
});
