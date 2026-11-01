// workspace-diff: per task, `csharp/<t>/` and `node/<t>/` differ only in each stack's pin files and in the marked sentences of STATEMENT.md.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { diffAllTasks, diffTaskPair, NODE_PINS, CSHARP_PINS } from '../lib/workspace-diff.mjs';
import { ownedFolder } from './support/push-fixtures.mjs';

const STATEMENT = (stack, framework) => ['# Task', '', 'The program reads a number.', '', '<!-- stack-sentence -->', stack, '', 'More shared text.', '', '<!-- framework-sentence -->', framework, '', 'Closing shared text.', ''].join('\n');
const write = (dir, rel, content) => { mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true }); writeFileSync(path.join(dir, rel), content); };

/** A task pair with identical shared files and each stack's own pin files. */
function pair(t) {
  const root = ownedFolder(t, 'wsdiff');
  const [cs, nd] = [path.join(root, 'csharp', '01-x'), path.join(root, 'node', '01-x')];
  for (const [dir, stack, framework] of [[cs, 'Write it in C# on .NET 10.', 'Use the ASP.NET minimal API.'], [nd, 'Write it in TypeScript on Node 26.', 'Use Fastify.']]) {
    write(dir, 'STATEMENT.md', STATEMENT(stack, framework));
    write(dir, 'samples/s01.in', '5\n');
    write(dir, 'run-samples.sh', '#!/bin/sh\n');
  }
  write(cs, 'global.json', '{}'); write(cs, 'src/App.csproj', '<Project/>'); write(cs, 'nuget.config', '<c/>'); write(cs, 'Directory.Build.props', '<p/>');
  write(nd, 'package.json', '{}'); write(nd, 'package-lock.json', '{}'); write(nd, 'tsconfig.json', '{}'); write(nd, '.npmrc', ''); write(nd, 'eslint.config.mjs', '');
  return { root, cs, nd };
}
const paths = result => result.findings.map(f => [f.path, f.what]);

test('An_empty_task_pair_passes', t => {
  const root = ownedFolder(t, 'wsempty');
  mkdirSync(path.join(root, 'csharp', '01-x'), { recursive: true });
  mkdirSync(path.join(root, 'node', '01-x'), { recursive: true });
  assert.deepEqual(diffAllTasks({ root }), { ok: true, findings: [] });
});

test('A_pair_that_differs_only_in_pin_files_and_the_marked_sentences_passes', t => {
  const { cs, nd } = pair(t);
  assert.deepEqual(diffTaskPair({ csharp: cs, node: nd }).findings, []);
});

test('An_extra_file_in_one_stack_fails_naming_the_path', t => {
  const { cs, nd } = pair(t);
  write(nd, 'src/helper.ts', 'export {};\n');
  assert.deepEqual(paths(diffTaskPair({ csharp: cs, node: nd })), [['src/helper.ts', 'only in node']]);
  write(cs, 'Notes.md', 'x');
  assert.deepEqual(paths(diffTaskPair({ csharp: cs, node: nd })), [['Notes.md', 'only in csharp'], ['src/helper.ts', 'only in node']]);
});

test('Workspace_diff_fails_on_one_byte_outside_the_stack_sentence', t => {
  const { cs, nd } = pair(t);
  write(nd, 'samples/s01.in', '6\n');
  assert.deepEqual(paths(diffTaskPair({ csharp: cs, node: nd })), [['samples/s01.in', 'bytes differ']]);
  write(nd, 'samples/s01.in', '5\n');
  write(nd, 'STATEMENT.md', STATEMENT('Write it in TypeScript on Node 26.', 'Use Fastify.').replace('More shared text.', 'More shared text!'));
  assert.deepEqual(paths(diffTaskPair({ csharp: cs, node: nd })), [['STATEMENT.md', 'differs outside a marked sentence, line 8']]);
});

test('A_statement_whose_marker_is_missing_or_moved_or_whose_sentence_line_changes_count_is_refused', t => {
  const { cs, nd } = pair(t);
  write(nd, 'STATEMENT.md', STATEMENT('Write it in TypeScript on Node 26.', 'Use Fastify.').replace('<!-- framework-sentence -->\n', ''));
  assert.match(diffTaskPair({ csharp: cs, node: nd }).findings[0].what, /^differs outside a marked sentence|marker/);
  write(nd, 'STATEMENT.md', STATEMENT('Write it in TypeScript\non Node 26.', 'Use Fastify.'));
  assert.equal(diffTaskPair({ csharp: cs, node: nd }).findings.length, 1, 'a sentence that grows a line is a line outside the marked one');
});

test('A_pin_file_of_the_other_stack_is_not_a_pin_file_here', t => {
  const { cs, nd } = pair(t);
  write(cs, 'package.json', '{}');
  assert.deepEqual(paths(diffTaskPair({ csharp: cs, node: nd })), [['package.json', 'only in csharp']]);
  assert.ok(CSHARP_PINS.includes('global.json') && NODE_PINS.includes('eslint.config.mjs'));
});

test('A_symlink_or_a_task_folder_present_on_one_side_only_is_refused', t => {
  const { root, cs, nd } = pair(t);
  symlinkSync('/etc/hostname', path.join(nd, 'link'));
  assert.deepEqual(paths(diffTaskPair({ csharp: cs, node: nd })), [['link', 'symlink']]);
  mkdirSync(path.join(root, 'node', '02-y'), { recursive: true });
  const all = diffAllTasks({ root });
  assert.ok(all.findings.some(f => f.path === 'node/02-y' && f.what === 'task folder only in node'));
});

test('The_scan_still_finds_a_difference_in_a_nested_file_two_levels_down', t => {
  const { cs, nd } = pair(t);
  write(cs, 'a/b/c.txt', 'one'); write(nd, 'a/b/c.txt', 'two');
  assert.deepEqual(paths(diffTaskPair({ csharp: cs, node: nd })), [['a/b/c.txt', 'bytes differ']]);
});

test('The_command_prints_each_difference_and_exits_1_and_0_when_the_pairs_agree', t => {
  const { root, nd } = pair(t);
  const CLI = path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'workspace-diff.mjs');
  const run = args => spawnSync(process.execPath, [CLI, ...args], { encoding: 'utf8' });
  assert.equal(run(['--root', root]).status, 0);
  write(nd, 'extra.txt', 'x');
  const r = run(['--root', root]);
  assert.equal(r.status, 1);
  assert.match(r.stdout, /^01-x\/extra\.txt: only in node$/m);
  assert.equal(run([]).status, 4);
});
