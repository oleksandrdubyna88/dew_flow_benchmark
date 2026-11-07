// The one memory value (section 2.4): the statement, the visible load test's configuration and the scorer's limit read ONE number from ONE file, each stack's statement discloses its heap setting as the execution layer derives
// it, a registered value that is not a whole number of MiB is refused, and the score reads the enforced limit of every case back. The workspace is built from the committed templates with an invented spec; nothing starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { heapKeys } from '../../lang-bench/lib/exec-layer/env.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { loadCalibration } from '../lib/limits.mjs';
import { MEMORY_LIMIT_FILE, heapSentence, loadTestConfig, memoryLimit, memorySentence } from '../lib/memory-limit.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { packagesInputs, quickWorkspaceFiles } from '../lib/workspace-quick.mjs';
import { EXPLORE, quickTask } from './quick-leak-fixture.mjs';
import { MIB, ran, rec, score } from './quick-score-fixture.mjs';

const SPIKES = fileURLToPath(new URL('../../', import.meta.url));
const FORMAT = readFileSync(path.join(SPIKES, 'lang-bench', 'test', 'fixtures', 'conformance', 'FORMAT.md'), 'utf8');
const SPEC = ['# Task 05 — An invented service', '', '## 5. Build', '', '## 6. Visible samples', '', '### s01', '', '`samples/s01.in`', '', '```text', 'scenario 2', 'start ready port within 10s', '```', '', '`samples/s01.out`', '', '```text', 'transcript 2', 'ready', '```', '', '## 7. Hidden tests', '', 'text', ''].join('\n');
const EXPLORE_Q = quickExplore(EXPLORE, '/home/nobody');
const LIMIT = new Proxy({}, { get: (_, key) => memoryLimit().value[key] }); // read when a test uses it, so an unbuilt module fails the test that needs it

const filesOf = (stack, id = '05', limit = LIMIT) => {
  const r = quickWorkspaceFiles({ explore: EXPLORE_Q, task: quickTask(id), stack, sources: { spec: SPEC, format: FORMAT }, inputs: packagesInputs().value, limit });
  assert.equal(r.ok, true, r.reason);
  return r.value.files;
};
const text = (files, rel) => files[rel].bytes.toString('utf8');

test('The_statement_the_load_test_configuration_and_the_scorers_limit_read_one_number_from_the_one_file_and_it_is_536870912', () => {
  assert.equal(LIMIT.bytes, 536870912, 'the one literal, here and in the registered file only');
  for (const stack of ['csharp', 'node']) {
    for (const id of ['05', '10']) {
      const files = filesOf(stack, id);
      assert.ok(text(files, 'STATEMENT.md').includes(memorySentence(LIMIT)), `${stack} ${id}: the statement carries D72's sentence`);
      assert.deepEqual(JSON.parse(text(files, 'samples/load.json')), loadTestConfig(stack, LIMIT));
    }
    assert.equal(loadTestConfig(stack, LIMIT).memoryLimitBytes, LIMIT.bytes);
  }
  assert.equal(memorySentence(LIMIT), 'The program and its descendants run under a 512 MiB (536,870,912-byte) cgroup memory limit, including runtime overhead. Build resources are separate.');
  const calibration = loadCalibration('/nonexistent-home', quickTask('07'), () => ok(EXPLORE));
  assert.equal(calibration.ok, true, calibration.reason);
  assert.equal(calibration.value.limits.memoryBytes, LIMIT.bytes, 'the scorer reads the same value');
  assert.equal(LIMIT.bytes, 512 * MIB);
});

/** Every file under the named places that can carry the number, as `[relative path, text]`; a binary or build output is not source. */
function sourcesUnder(root, places) {
  const walk = rel => (statSync(path.join(root, rel)).isDirectory() ? readdirSync(path.join(root, rel)).flatMap(n => walk(path.join(rel, n))) : [rel]);
  return places.flatMap(walk).filter(rel => !/node_modules|\/bin\/|\/obj\/|\.(png|dll|exe)$/.test(rel)).map(rel => [rel, readFileSync(path.join(root, rel), 'utf8')]);
}
const LITERAL = /536870912|536_870_912|0x20000000|512 ?MiB/;
// the probe's own test values (the limits it probes the layer at, 256 and 512 MiB) are not the registered limit; it is the one folder left out, by name
const PROBE = path.join('lang-bench', 'lib', 'exec-probe');
const scanPlaces = () => {
  const own = path.join(SPIKES, 'lang-explore');
  const quickBlock = JSON.stringify(JSON.parse(readFileSync(path.join(own, 'inputs', 'explore.json'), 'utf8')).quick);
  return [...sourcesUnder(SPIKES, ['lang-explore/lib', 'lang-explore/explore.mjs', 'lang-bench/lib', 'lang-bench/templates']).filter(([rel]) => !rel.startsWith(PROBE)), ['inputs/explore.json (quick)', quickBlock]];
};

test('The_memory_figure_has_exactly_one_source_in_the_tree', () => {
  const hits = scanPlaces().filter(([, content]) => LITERAL.test(content)).map(([rel]) => rel);
  assert.deepEqual(hits, [], 'the figure is written in inputs/memory-limit.json and nowhere else');
});

test('The_scan_still_finds_the_literal_in_the_registered_file_so_a_pass_is_not_an_empty_scan', () => {
  const found = sourcesUnder(path.dirname(MEMORY_LIMIT_FILE), [path.basename(MEMORY_LIMIT_FILE)]).filter(([, content]) => LITERAL.test(content));
  assert.equal(found.length, 1);
  assert.ok(scanPlaces().length > 20, 'the scan reads the places it names');
});

test('Each_stacks_statement_discloses_its_heap_setting_derived_from_the_value_by_the_layers_own_function', () => {
  const cs = text(filesOf('csharp'), 'STATEMENT.md');
  const node = text(filesOf('node'), 'STATEMENT.md');
  const csHeap = Number.parseInt(heapKeys('csharp', LIMIT.bytes).DOTNET_GCHeapHardLimit, 16);
  const nodeHeap = /--max-old-space-size=(\d+)/.exec(heapKeys('node', LIMIT.bytes).NODE_OPTIONS)[1];
  assert.equal(csHeap, 483_183_820);
  assert.ok(cs.includes(heapSentence('csharp', LIMIT)) && cs.includes(`DOTNET_GCHeapHardLimit=${heapKeys('csharp', LIMIT.bytes).DOTNET_GCHeapHardLimit}`) && cs.includes('483,183,820'));
  assert.equal(nodeHeap, '460');
  assert.ok(node.includes(heapSentence('node', LIMIT)) && node.includes(`--max-old-space-size=${nodeHeap}`));
  for (const statement of [cs, node]) assert.ok(statement.includes('Build resources are separate.'));
  assert.ok(!cs.includes('--max-old-space-size') && !node.includes('DOTNET_GCHeapHardLimit'), 'each statement names its own stack only');
});

test('Both_stacks_get_the_same_memory_sentence_and_the_same_load_configuration_and_differ_only_in_the_stack_heap_and_framework_sentences', () => {
  const marked = /<!-- (?:stack|framework|heap)-sentence -->\n[^\n]*\n/g;
  const strip = stack => text(filesOf(stack, '10'), 'STATEMENT.md').replaceAll(marked, '');
  assert.equal(strip('csharp'), strip('node'), 'byte for byte, the marked sentences left out');
  const heapless = stack => { const { heap, ...rest } = JSON.parse(text(filesOf(stack), 'samples/load.json')); return JSON.stringify({ rest, hasHeap: typeof heap }); };
  assert.equal(heapless('csharp'), heapless('node'));
  assert.notDeepEqual(JSON.parse(text(filesOf('csharp'), 'samples/load.json')).heap, JSON.parse(text(filesOf('node'), 'samples/load.json')).heap);
});

test('A_registered_value_that_is_not_a_positive_whole_number_of_MiB_is_refused_when_the_inputs_load_and_names_the_file', () => {
  const dir = tmpDir('quick-memory-file');
  for (const [bytes, mib] of [[536870913, 512], [0, 0], ['536870912', 512], [-1048576, -1], [1.5 * MIB, 1.5]]) {
    const file = path.join(dir, 'memory-limit.json');
    writeFileSync(file, JSON.stringify({ bytes, mib, decidedOn: '2026-11-07', by: 'invented' }));
    const direct = memoryLimit({ file });
    assert.equal(direct.ok, false, String(bytes));
    assert.ok(direct.reason.includes(file) && /whole number of MiB/.test(direct.reason));
    const loaded = loadExplore(undefined, file);
    assert.equal(loaded.ok, false);
    assert.ok(loaded.reason.includes(file));
  }
  writeFileSync(path.join(dir, 'memory-limit.json'), JSON.stringify({ bytes: 268435456, mib: 256, decidedOn: '2026-11-07', by: 'invented' }));
  assert.equal(memoryLimit({ file: path.join(dir, 'memory-limit.json') }).ok, true, 'a different whole number of MiB is a good value: the file decides, not the code');
  mkdirSync(path.join(dir, 'unused'));
  assert.equal(loadExplore(undefined, path.join(dir, 'memory-limit.json')).ok, true);
});

test('The_score_records_the_enforced_limit_of_every_case_read_back_from_the_cgroup_and_a_case_whose_read_back_differs_from_the_value_is_an_environment_failure_not_a_verdict', async () => {
  const good = await score({}, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal(good.ok, true, good.reason);
  assert.deepEqual(good.value.explore.cases.map(c => c.memoryMaxBytes), [LIMIT.bytes, LIMIT.bytes, LIMIT.bytes], 'each case carries the limit the runner read from prog/memory.max');
  const odd = rec('h001');
  odd.lifecycle.memory.maxBytes = 268_435_456;
  const bad = await score({}, { runFn: ran([rec('s01'), odd, rec('h002')]) });
  assert.equal(bad.ok, false);
  assert.equal(bad.kind, 'environment');
  assert.match(bad.reason, /h001/);
  assert.ok(bad.reason.includes('268435456') && bad.reason.includes(String(LIMIT.bytes)), 'both numbers are named');
  const unread = rec('h002');
  delete unread.lifecycle.memory.maxBytes;
  const missing = await score({}, { runFn: ran([rec('s01'), rec('h001'), unread]) });
  assert.equal(missing.kind, 'environment', 'a case with no read-back is no evidence either');
});

test('The_calibrate_command_tells_a_quick_task_where_its_limits_live_the_memory_limit_in_its_own_file_and_not_in_the_pins', async () => {
  const { main } = await import('../explore.mjs');
  const said = [];
  const code = await main(['calibrate', '--task', '05'], { say: line => said.push(line), home: '/nonexistent-home' });
  assert.equal(code, 4);
  const text = said.join('\n');
  assert.match(text, /memory-limit\.json/, 'the memory limit is named at its one source');
  assert.match(text, /quick\.limits/, 'the time and build limits are named where they are');
  assert.doesNotMatch(text, /its limits are fixed/);
});
