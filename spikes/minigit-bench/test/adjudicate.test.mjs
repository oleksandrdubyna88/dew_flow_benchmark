import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { classifyAmbiguity, classifyOrigin, packetsFor, revisedFor, revealFor, maskRules } from '../lib/adjudicate.mjs';
import { tmpDir } from './helpers.mjs';

const json = (file, doc) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, JSON.stringify(doc)); };
const failing = (id, stdout, stderr) => ({ id, group: 'H1', scored: true, status: 'fail', reason: 'mismatch', spec: `TOY:${id}`,
  captured: [{ argv: ['sum', '1'], exit: 1, stdout, stderr }] });
const passing = id => ({ id, group: 'H1', scored: true, status: 'pass', spec: `TOY:${id}`, captured: [] });

const C_SHARP_TRACE = [
  'Unhandled exception. System.NullReferenceException: Object reference not set',
  '   at Minigit.Program.Main(String[] args) in /home/jinx/rsd/minigit/process-csharp-1/Program.cs:line 12',
  'log written to /home/jinx/rsd/minigit/process-csharp-1/out.log',
].join('\n');
const NODE_TRACE = [
  "TypeError: Cannot read properties of undefined (reading 'x')",
  '    at Object.<anonymous> (/tmp/mgb-score/bare-node-2/minigit.mjs:3:9)',
  '    at node:internal/modules/cjs/loader:1554:14',
  'node: bare-node-2 crashed',
].join('\n');

/** Two cells (different arm and stack), failing a shared case and one case each, one case passing. */
function toySeries() {
  const series = tmpDir('series');
  const cell = (name, arm, stack, cases) => {
    json(path.join(series, name, 'score', 'score.json'), { format: 1, sessionId: name, arm, stack, phase: 'v1' });
    json(path.join(series, name, 'score', 'heldout-v1.json'), { format: 1, phase: 'v1', cases, latency: { known: false, why: 'x' } });
  };
  cell('process-csharp-1', 'process', 'csharp', [failing('H1-01', 'wrong\n', C_SHARP_TRACE), failing('H1-02', 'x', ''), passing('H1-03')]);
  cell('bare-node-2', 'bare', 'node', [failing('H1-01', 'wrong\n', NODE_TRACE), passing('H1-02'), passing('H1-03')]);
  return series;
}

const DOTNET_TRACE = [
  "Unhandled exception. System.IO.FileNotFoundException: Could not find file '/home/u/run/a.txt'.",
  "File name: '/home/u/run/a.txt'",
  '   at Interop.ThrowExceptionForIoErrno(ErrorInfo errorInfo, String path, Boolean isDirError)',
  '   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String path, OpenFlags flags, Int32 mode)',
  '   at Minigit.Program.Main(String[] args) in /src/minigit/Program.cs:line 12',
  'Aborted (core dumped)',
].join('\n');
const NODE_ENOENT = [
  'node:fs:441',
  '    return binding.open(',
  '                   ^',
  '',
  "Error: ENOENT: no such file or directory, open 'a.txt'",
  '    at Object.openSync (node:fs:573:18)',
  '    at file:///home/u/run/minigit.mjs:3:9 {',
  '  errno: -2,',
  "  syscall: 'open',",
  "  code: 'ENOENT',",
  "  path: 'a.txt'",
  '}',
  '',
  'Node.js v26.12.1',
].join('\n');

const readAll = dir => readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile())
  .map(e => readFileSync(path.join(e.parentPath, e.name), 'utf8')).join('\n');

test('Adjudication_packets_carry_no_stack_or_arm', () => {
  const series = toySeries();
  const made = packetsFor({ series });
  assert.equal(made.ok, true, made.reason);
  const packetsDir = path.join(series, 'adjudication-packets');
  const files = readdirSync(packetsDir);
  assert.equal(files.length, 3, 'one packet per failing scored case of every cell');
  assert.ok(files.every(f => /^[0-9a-f]{8}\.json$/.test(f)));
  const everything = readAll(packetsDir);
  for (const forbidden of ['csharp', 'node', 'process', 'bare', 'System.', 'Program.cs', '/home/jinx', '/tmp/mgb-score', 'TypeError', 'NullReference', 'node:internal', 'process-csharp-1', 'bare-node-2']) {
    assert.equal(everything.includes(forbidden), false, `a packet still holds ${forbidden}`);
  }
  assert.ok(everything.includes('<runtime output>'), 'the replaced runtime output is visible as such');
  const packet = JSON.parse(readFileSync(path.join(packetsDir, files[0]), 'utf8'));
  assert.deepEqual(Object.keys(packet).sort(), ['blindId', 'captured', 'caseId', 'spec']);
  assert.deepEqual(made.value.rules.map(r => r.name), maskRules.map(r => r.name), 'the masking rules are listed');
  const map = JSON.parse(readFileSync(path.join(series, 'adjudication', 'map.json'), 'utf8'));
  const entries = Object.values(map.entries);
  assert.equal(entries.length, 3);
  assert.deepEqual(entries.map(e => e.arm).sort(), ['bare', 'process', 'process']);
  assert.deepEqual(entries.map(e => e.stack).sort(), ['csharp', 'csharp', 'node']);
  assert.deepEqual(Object.keys(map.entries).sort(), files.map(f => f.replace('.json', '')).sort());
});

test('Packets_are_not_listed_in_cell_order_and_a_second_run_does_not_reroll_the_blind_ids', () => {
  const series = toySeries();
  const first = packetsFor({ series });
  const order = first.value.blindIds;
  assert.deepEqual(order, [...order].sort(), 'listed by blind id, so the listing order says nothing about the cell');
  const again = packetsFor({ series });
  assert.equal(again.ok, false);
  assert.match(again.reason, /already exist/);
});

test('Reveal_is_refused_until_every_packet_has_its_step_one_verdict', () => {
  const series = toySeries();
  const ids = packetsFor({ series }).value.blindIds;
  assert.equal(classifyAmbiguity({ series, blindId: ids[0], verdict: 'not-ambiguous', reason: 'the SPEC is plain' }).ok, true);
  assert.equal(classifyAmbiguity({ series, blindId: ids[1], verdict: 'maybe', reason: 'x' }).ok, false);
  assert.equal(classifyAmbiguity({ series, blindId: 'deadbeef', verdict: 'not-ambiguous', reason: 'x' }).ok, false);
  assert.equal(classifyAmbiguity({ series, blindId: ids[1], verdict: 'not-ambiguous', reason: '' }).ok, false, 'a reason is required');
  const refused = revealFor({ series });
  assert.equal(refused.ok, false);
  assert.ok(refused.reason.includes(ids[1]) && refused.reason.includes(ids[2]) && !refused.reason.includes(ids[0]), refused.reason);
  assert.equal(existsSync(path.join(series, 'adjudication', 'reveal.json')), false);
  for (const id of ids.slice(1)) classifyAmbiguity({ series, blindId: id, verdict: 'not-ambiguous', reason: 'plain' });
  const revealed = revealFor({ series });
  assert.equal(revealed.ok, true, revealed.reason);
  assert.deepEqual(revealed.value.map(r => r.cell).sort(), ['bare-node-2', 'process-csharp-1', 'process-csharp-1']);
  assert.ok(revealed.value.every(r => r.codePath.includes(r.cell)));
  assert.equal(classifyOrigin({ series, blindId: ids[0], klass: 'product', reason: 'it prints the wrong thing' }).ok, true);
  assert.equal(classifyOrigin({ series, blindId: ids[0], klass: 'whatever', reason: 'x' }).ok, false);
});

test('Step_two_is_refused_before_the_reveal', () => {
  const series = toySeries();
  const ids = packetsFor({ series }).value.blindIds;
  const r = classifyOrigin({ series, blindId: ids[0], klass: 'harness', reason: 'x' });
  assert.equal(r.ok, false);
  assert.match(r.reason, /reveal/);
});

test('A_case_classed_ambiguous_is_dropped_for_every_cell_and_the_primary_is_kept', () => {
  const series = toySeries();
  const made = packetsFor({ series });
  const map = JSON.parse(readFileSync(path.join(series, 'adjudication', 'map.json'), 'utf8'));
  const idOf = (cell, caseId) => made.value.blindIds.find(b => map.entries[b].cell === cell && map.entries[b].caseId === caseId);
  const primaryBefore = ['process-csharp-1', 'bare-node-2'].map(c => readFileSync(path.join(series, c, 'score', 'heldout-v1.json'), 'utf8'));
  // H1-01 fails in BOTH cells; the judge calls it ambiguous from ONE packet only.
  classifyAmbiguity({ series, blindId: idOf('process-csharp-1', 'H1-01'), verdict: 'spec-ambiguity', reason: 'the SPEC allows two readings' });
  classifyAmbiguity({ series, blindId: idOf('bare-node-2', 'H1-01'), verdict: 'not-ambiguous', reason: 'plain' });
  classifyAmbiguity({ series, blindId: idOf('process-csharp-1', 'H1-02'), verdict: 'not-ambiguous', reason: 'plain' });
  const revised = revisedFor({ series });
  assert.equal(revised.ok, true, revised.reason);
  assert.deepEqual(revised.value.droppedCases, ['H1-01']);
  const byCell = Object.fromEntries(revised.value.cells.map(c => [c.cell, c]));
  assert.deepEqual(byCell['process-csharp-1'].totals, { total: 2, passed: 1, failed: 1, blocked: 0, percent: 50 });
  assert.deepEqual(byCell['bare-node-2'].totals, { total: 2, passed: 2, failed: 0, blocked: 0, percent: 100 });
  assert.equal(byCell['bare-node-2'].primary100, true, 'dropped from the cell that did NOT call it ambiguous too');
  const primaryAfter = ['process-csharp-1', 'bare-node-2'].map(c => readFileSync(path.join(series, c, 'score', 'heldout-v1.json'), 'utf8'));
  assert.deepEqual(primaryAfter, primaryBefore, 'the primary score is never rewritten');
  assert.equal(existsSync(path.join(series, 'bare-node-2', 'score', 'heldout-revised-v1.json')), true, 'the revised score sits beside the primary');
});

test('Revised_is_refused_while_a_packet_has_no_step_one_verdict', () => {
  const series = toySeries();
  packetsFor({ series });
  const r = revisedFor({ series });
  assert.equal(r.ok, false);
  assert.match(r.reason, /step-1|step one|verdict/);
});

test('A_series_without_a_scored_cell_is_a_failure_not_an_empty_adjudication', () => {
  const r = packetsFor({ series: tmpDir('empty-series') });
  assert.equal(r.ok, false);
  assert.match(r.reason, /no scored cell/);
});

test('The_command_line_drives_the_two_steps_and_prints_the_masking_rules_with_the_packets', async () => {
  const { main } = await import('../adjudicate.mjs');
  const series = toySeries();
  const run = async (...argv) => { const out = []; const code = await main(argv, l => out.push(l)); return { code, out: out.join('\n') }; };
  const packets = await run('packets', '--series', series);
  assert.equal(packets.code, 0, packets.out);
  for (const rule of maskRules) assert.ok(packets.out.includes(rule.name), `the rule ${rule.name} is printed`);
  const ids = [...packets.out.matchAll(/^packet ([0-9a-f]{8})$/gm)].map(m => m[1]);
  assert.equal(ids.length, 3);
  assert.equal(packets.out.includes('process-csharp-1'), false, 'the listing does not name a cell');
  assert.equal((await run('reveal', '--series', series)).code, 1);
  for (const id of ids) assert.equal((await run('classify', '--series', series, '--blind', id, '--verdict', 'not-ambiguous', '--reason', 'plain')).code, 0);
  const revealed = await run('reveal', '--series', series);
  assert.equal(revealed.code, 0, revealed.out);
  assert.equal((await run('class', '--series', series, '--blind', ids[0], '--class', 'product', '--reason', 'it prints the wrong thing')).code, 0);
  const revised = await run('revised', '--series', series);
  assert.equal(revised.code, 0, revised.out);
  assert.equal((await run('classify', '--series', series)).code, 4);
  assert.equal((await run('nonsense')).code, 4);
});

const FORBIDDEN_RUNTIME = ['Unhandled exception', 'Could not find file', 'minigit.dll', 'ENOENT', 'syscall', 'errno', 'file://', '^^^', 'v26.12.1', 'Node.js', 'core dumped',
  'Aborted', 'FileNotFound', 'Interop.', 'SafeFileHandle', 'openSync', 'node:fs'];

test('Real_dotnet_and_node_shaped_traces_leave_nothing_of_the_runtime_in_a_packet', () => {
  const series = tmpDir('series-runtime');
  const cell = (name, arm, stack, stderr, exit, stdout = 'Committed deadbeefdeadbeef\n') => {
    json(path.join(series, name, 'score', 'score.json'), { format: 1, sessionId: name, arm, stack, phase: 'v1' });
    const c = failing('H1-07', stdout, stderr);
    json(path.join(series, name, 'score', 'heldout-v1.json'), { format: 1, phase: 'v1', cases: [{ ...c, captured: [{ ...c.captured[0], exit }] }] });
  };
  cell('cell-a', 'process', 'csharp', DOTNET_TRACE, 134);
  cell('cell-b', 'bare', 'node', NODE_ENOENT, 1);
  cell('cell-c', 'bare', 'csharp', 'File not found\n', 1, '');
  cell('cell-d', 'process', 'node', 'Segmentation fault', 139);
  const made = packetsFor({ series });
  assert.equal(made.ok, true, made.reason);
  const dir = path.join(series, 'adjudication-packets');
  const packets = readdirSync(dir).map(f => JSON.parse(readFileSync(path.join(dir, f), 'utf8')));
  const text = JSON.stringify(packets);
  for (const word of FORBIDDEN_RUNTIME) assert.equal(text.includes(word), false, `a packet still holds ${word}`);
  const byExit = packets.map(p => p.captured[0]);
  assert.deepEqual(byExit.map(c => c.exit).sort(), [1, 1, '<abnormal>', '<abnormal>'].sort(), 'exit codes outside 0 and 1 are normalised');
  const stderrs = byExit.map(c => c.stderr).sort();
  assert.deepEqual(stderrs, ['<runtime output>', '<runtime output>', '<runtime output>', 'File not found\n'].sort(), 'a plain one-line message survives; runtime output is replaced whole');
});

test('The_map_is_not_in_the_directory_the_blind_judge_reads', () => {
  const series = toySeries();
  packetsFor({ series });
  assert.equal(existsSync(path.join(series, 'adjudication', 'map.json')), true);
  assert.deepEqual(readdirSync(path.join(series, 'adjudication-packets')).filter(f => !/^[0-9a-f]{8}\.json$/.test(f)), [], 'only packets sit there');
  assert.equal(readdirSync(path.join(series, 'adjudication-packets'), { recursive: true }).some(f => /map|step|reveal/.test(f)), false);
});

test('A_series_inside_a_git_worktree_is_refused', async () => {
  const { spawnSync } = await import('node:child_process');
  const series = toySeries();
  assert.equal(spawnSync('git', ['init', '-q', series]).status, 0);
  for (const r of [packetsFor({ series }), revealFor({ series }), revisedFor({ series })]) {
    assert.equal(r.ok, false);
    assert.match(r.reason, /git worktree/);
  }
  assert.equal(existsSync(path.join(series, 'adjudication-packets')), false, 'nothing was written');
});

test('A_series_given_as_a_relative_or_unnormalised_path_is_normalised_once_and_messages_name_the_absolute_one', () => {
  const series = toySeries();
  const relative = path.relative(process.cwd(), series);
  assert.equal(path.isAbsolute(relative), false);
  const made = packetsFor({ series: relative });
  assert.equal(made.ok, true, made.reason);
  const again = packetsFor({ series: `${series}${path.sep}.${path.sep}` });
  assert.equal(again.ok, false);
  assert.ok(again.reason.startsWith(`${path.join(series, 'adjudication', 'map.json')} already exists`), `the refusal names the absolute map path: ${again.reason}`);
  const unknown = classifyAmbiguity({ series: relative, blindId: 'deadbeef', verdict: 'not-ambiguous', reason: 'x' });
  assert.equal(unknown.ok, false);
  const bare = tmpDir('bare-series');
  const empty = revealFor({ series: path.relative(process.cwd(), bare) });
  assert.ok(empty.reason.startsWith(`no packets yet under ${bare}:`), `no-packets refusal names the absolute series: ${empty.reason}`);
});
