// The package check through the REAL box (F2.S1, section 2.6, scenarios 30 to 33, and the plan round's finding 1): invented toy daemons on the real templates (the C# one built by the template's own build.sh in the build box, the Node one
// started by run.sh), an invented held-out folder, an invented manifest, witnesses by overlay. No task, no reference, no private file, no paid session. Behind `needs` (bwrap, the user bus, the pinned toolchains): skipped with the reason,
// a failure under BENCH_REQUIRE_SANDBOX=1. The real HOME is kept (the toolchains are found under it): the package's own folders are in a temp home.
import { needs, toolchains } from '../../lang-bench/test/support/exec-layer-host.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { main } from '../explore.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { memoryLimit } from '../lib/memory-limit.mjs';
import { packageRoot } from '../lib/package-record.mjs';
import { quickCalibration, quickExplore, quickPaths, quickSnapshots } from '../lib/quick.mjs';
import { passSeams } from '../lib/pass-seams.mjs';
import { refsDirOf } from '../lib/heldout-quick.mjs';
import { taskPaths } from '../lib/tasks.mjs';
import { quickHome, quickTask } from './quick-leak-fixture.mjs';
import { SPEC_TEXT, commandEnv, sha } from './quick-package-fixture.mjs';
import { toyFile, wordScenario, wordTranscript } from './quick-package-toys.mjs';

const put = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };
const STACKS = ['csharp', 'node'];
assert.ok(toolchains, 'the pinned toolchain directories are read before anything else sets a home');

/** The quick world over the REAL box for an invented package: the pass seams of the real world (`pass-seams.mjs`), with no frozen files (the template's own files are laid by the package). */
function realWorld() {
  const base = loadExplore().value;
  const explore = quickExplore(base, os.homedir());
  const snapshots = quickSnapshots({ base, home: os.homedir() });
  assert.equal(snapshots.ok, true, snapshots.reason);
  const profile = { explore, paths: quickPaths(explore, os.homedir()), snapshots: snapshots.value, kind: 'daemon', caseWallMs: explore.quick.caseWallMs, runCeilingMs: explore.quick.runCeilingMs };
  return {
    sources: { spec: SPEC_TEXT, format: 'invented' }, limits: quickCalibration(explore).limits, explore,
    passSeams: args => passSeams({ a: { ...args, files: {} }, profile, seams: {}, seriesDir: args.seriesDir }),
  };
}

/**
 * An invented package on disk. `cases` is `[{ id, word, scenario? }]` (each case sends its word and expects the echo), `traps` and `witnesses` as the manifest, `overlays` `{ witness id: toy options }`, `refs` the reference's
 * toy options per stack. -> `{ home, task, dir, casesDir, manifestFile }`.
 */
function realPackage({ cases, traps, witnesses, overlays, refs = {} }) {
  const home = quickHome();
  const task = { ...quickTask('05'), spec: { ...quickTask('05').spec, sha256: sha(SPEC_TEXT) } };
  const dir = taskPaths(task, home).heldoutDir;
  const casesDir = path.join(dir, 'cases');
  const files = cases.map(c => ({ id: c.id, input: c.scenario ?? wordScenario(c.word), output: wordTranscript(c.word) }));
  for (const f of files) { put(path.join(casesDir, `${f.id}.in`), f.input); put(path.join(casesDir, `${f.id}.out`), f.output); }
  put(path.join(casesDir, 'cases.txt'), `${files.map(f => `${f.id} bytes R1`).join('\n')}\n`);
  put(path.join(dir, 'traps.txt'), `${[...new Set(Object.values(traps).flatMap(t => t.cases))].sort().join('\n')}\n`);
  const manifestFile = path.join(dir, 'manifest.json');
  put(manifestFile, `${JSON.stringify({ caseCount: files.length, cases: Object.fromEntries(files.map(f => [f.id, { in: sha(f.input), out: sha(f.output) }])), traps, witnesses }, null, 2)}\n`);
  for (const stack of STACKS) { const t = toyFile(stack, refs[stack] ?? {}); put(path.join(refsDirOf(dir), stack, t.rel), t.text); }
  for (const [id, options] of Object.entries(overlays)) { const t = toyFile(witnesses[id].stack, options); put(path.join(dir, 'mutants', id, t.rel), t.text); }
  put(taskPaths(task, home).notesFile, 'invented private notes\n');
  return { home, task, dir, casesDir, manifestFile };
}

/** The command over a real package: `main(argv, io)` with the real world. -> `{ code, lines, run }` (`run`: the newest run folder). */
async function runCommand({ built, argv, over = {} }) {
  const env = commandEnv({ built, over: { worldFn: async () => ok(realWorld()), ...over } });
  const code = await main(['package', '--task', '05', ...argv], env.io);
  const root = packageRoot(env.io.home, built.task);
  const runs = readdirSync(root).filter(n => /^\d{8}T/.test(n)).sort();
  return { code, lines: env.lines, env, run: path.join(root, runs.at(-1)) };
}

const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const TEST = { timeout: 1_800_000 };
const ECHO_CASES = [{ id: 'h001', word: 'hi' }, { id: 'h002', word: 'ho' }, { id: 'h003', word: 'hu' }];
const WRONG_ON_HO = { csharp: { wrongWord: 'ho' }, node: { wrongWord: 'ho' } };
const TRAP_HO = { 'trap-ho': { stack: 'both', cases: ['h002'] } };
const WITNESS_HO = { 'MR-ho-c': { trap: 'trap-ho', stack: 'csharp', kind: 'deterministic' }, 'MR-ho-n': { trap: 'trap-ho', stack: 'node', kind: 'deterministic' } };

let shared;
/** The first package run, made once and shared by scenarios 30 and 33 (two references, two witnesses, a record, a registration). */
const firstRun = () => {
  shared ??= (async () => {
    const built = realPackage({ cases: ECHO_CASES, traps: TRAP_HO, witnesses: WITNESS_HO, overlays: { 'MR-ho-c': WRONG_ON_HO.csharp, 'MR-ho-n': WRONG_ON_HO.node } });
    const first = await runCommand({ built, argv: ['--register'] });
    return { built, first };
  })();
  return shared;
};

test('A_toy_reference_pair_on_the_real_templates_goes_through_the_package_check_end_to_end', TEST, async t => {
  if (!needs(t)) return;
  const { built, first } = await firstRun();
  assert.equal(first.code, 0, first.lines.join('\n'));
  const record = readJson(path.join(first.run, 'record.json'));
  assert.deepEqual([record.items.refs.csharp.verdict, record.items.refs.node.verdict], ['accepted', 'accepted'], 'both references accepted, ten of ten');
  assert.equal(record.items.refs.csharp.rows.passes.length, 10);
  assert.equal(record.items.witnesses.caught, 2, 'both witnesses are caught');
  const registered = readJson(first.env.files.packagesFile)['05'];
  assert.equal(registered.referenceVerdicts.node, 'accepted');
  // the identity is stable across two runs: the references run again (their walls differ), the witnesses are carried over from the same material
  const second = await runCommand({ built, argv: ['--only', 'refs', '--register'], over: { package: { packagesFile: first.env.files.packagesFile, frozenFile: first.env.files.frozenFile } } });
  assert.equal(second.code, 0, second.lines.join('\n'));
  assert.equal(readJson(first.env.files.packagesFile)['05'].identity, registered.identity, 'the identity of a re-run of the same material is the same');
  assert.ok(second.lines.some(l => /registered: the package of task 05 \(unchanged\)/.test(l)), second.lines.join('\n'));
});

test('The_toy_references_are_scored_at_the_chosen_limit_read_back_from_the_cgroup', TEST, async t => {
  if (!needs(t)) return;
  const { first } = await firstRun();
  const limit = memoryLimit().value.bytes;
  for (const stack of STACKS) {
    const passes = readdirSync(path.join(first.run, `refs-${stack}`, 'passes')).filter(f => f.endsWith('.json'));
    assert.equal(passes.length, 10);
    for (const f of passes) {
      const body = readJson(path.join(first.run, `refs-${stack}`, 'passes', f));
      assert.equal(body.record.cases.every(c => c.lifecycle.memory.maxBytes === limit), true, `${stack} ${f}: the limit read back from the cgroup is the registered value`);
    }
  }
});

test('A_witness_that_only_the_memory_bound_catches_is_caught_through_the_certified_box', TEST, async t => {
  if (!needs(t)) return;
  const cases = [{ id: 'h001', word: 'hi' }, { id: 'h002', word: 'boom' }, { id: 'h003', word: 'boomobj' }];
  const traps = { 'trap-typed': { stack: 'both', cases: ['h002'] }, 'trap-object': { stack: 'both', cases: ['h003'] } };
  const witnesses = {
    'MM-typed-c': { trap: 'trap-typed', stack: 'csharp', kind: 'deterministic' }, 'MM-typed-n': { trap: 'trap-typed', stack: 'node', kind: 'deterministic' },
    'MM-object-c': { trap: 'trap-object', stack: 'csharp', kind: 'deterministic' }, 'MM-object-n': { trap: 'trap-object', stack: 'node', kind: 'deterministic' },
  };
  const overlays = { 'MM-typed-c': { boom: true }, 'MM-typed-n': { boom: true }, 'MM-object-c': { boomObj: true }, 'MM-object-n': { boomObj: true } };
  const built = realPackage({ cases, traps, witnesses, overlays });
  const r = await runCommand({ built, argv: ['--only', 'witnesses'] });
  assert.equal(r.code, 0, r.lines.join('\n'));
  const record = readJson(path.join(r.run, 'record.json'));
  assert.equal(record.items.refs.ok, true, 'the references pass the same cases');
  assert.equal(record.items.witnesses.caught, 4, 'all four witnesses are caught: the typed-buffer path and the object path, in both stacks');
  for (const id of Object.keys(witnesses)) {
    const step = readJson(path.join(r.run, `step-witness-${id}.json`));
    const failing = Object.values(step.record.passes[0].verdicts);
    assert.deepEqual(failing, ['memory limit'], `${id}: the case reads memory limit`);
  }
});

test('A_race_toy_witness_is_caught_on_its_failing_passes_and_its_clean_passes_are_kept', TEST, async t => {
  if (!needs(t)) return;
  const cases = [{ id: 'h001', word: 'hi', scenario: wordScenario('hi', { wrongOnTwo: true }) }, { id: 'h002', word: 'ho' }];
  const witnesses = { 'MR-two-n': { trap: 'trap-two', stack: 'node', kind: 'race' } };
  const overlays = { 'MR-two-n': { wrongOnTwo: true } };
  const built = realPackage({ cases, traps: { 'trap-two': { stack: 'node', cases: ['h001'] } }, witnesses, overlays });
  const r = await runCommand({ built, argv: ['--only', 'witnesses'] });
  assert.equal(r.code, 0, r.lines.join('\n'));
  const step = readJson(path.join(r.run, 'step-witness-MR-two-n.json'));
  assert.equal(step.caught, true, step.sentence);
  const failing = step.record.passes.filter(p => p.failing.length > 0);
  assert.deepEqual(failing.map(p => p.n), [1, 3, 5, 7, 9], 'the five 2-CPU passes fail');
  assert.equal(failing.every(p => p.failing.length === 1 && p.profile === '2cpu'), true, 'that case only');
  assert.deepEqual(step.record.passes.filter(p => p.failing.length === 0).map(p => p.n), [2, 4, 6, 8, 10], 'the five 8-CPU passes are clean and kept');
  // the same program registered to another case is an unregistered failure: not caught
  const other = realPackage({ cases, traps: { 'trap-two': { stack: 'node', cases: ['h002'] } }, witnesses, overlays });
  const again = await runCommand({ built: other, argv: ['--only', 'witnesses'] });
  assert.equal(again.code, 1, again.lines.join('\n'));
  const notCaught = readJson(path.join(again.run, 'step-witness-MR-two-n.json'));
  assert.equal(notCaught.caught, false);
  assert.match(notCaught.sentence, /one case beyond the table/);
});

/** The processes whose command line holds `marker`: pids. */
const processesWith = marker => readdirSync('/proc').filter(n => /^\d+$/.test(n)).filter(n => { try { return readFileSync(`/proc/${n}/cmdline`, 'latin1').includes(marker); } catch { return false; } });

test('A_witness_whose_grandchild_hangs_leaves_no_process_after_its_pass', TEST, async t => {
  if (!needs(t)) return;
  const marker = `grandchild-marker-${process.pid}-${Date.now()}`;
  const cases = [{ id: 'h001', word: 'hi' }, { id: 'h002', word: 'spawn' }];
  const witnesses = { 'MG-spawn-n': { trap: 'trap-spawn', stack: 'node', kind: 'deterministic' } };
  const built = realPackage({ cases, traps: { 'trap-spawn': { stack: 'node', cases: ['h002'] } }, witnesses, overlays: { 'MG-spawn-n': { grandchildMarker: marker } } });
  const r = await runCommand({ built, argv: ['--only', 'witnesses'] });
  assert.equal(r.code, 0, r.lines.join('\n'));
  const step = readJson(path.join(r.run, 'step-witness-MG-spawn-n.json'));
  assert.equal(step.caught, true, `a hang of the registered case is that case failing: ${step.sentence}`);
  assert.deepEqual(processesWith(marker), [], 'no process of the witness is left: the runner kills the program\'s whole cgroup');
});
