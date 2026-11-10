// The gate, the identity, the registry and the registration of a package (F2.S1, section 2.4, scenarios 21 to 25) and the plan round's findings 2, 3 and 4 (S apart from registration, the memory value apart from the entries, the run folders),
// over INVENTED packages in temp homes. Scenario 21 runs the real conformance checker over lang-bench's invented spec fixtures.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { formatOwner, ownedFor, thisOwner, writeOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { argsOf, fixtureRepo, recordFile } from '../../lang-bench/test/support/conformance-fixtures.mjs';
import { MEMORY_LIMIT_FILE, memoryLimit, registerMemoryValue } from '../lib/memory-limit.mjs';
import { gateItem, runConformance } from '../lib/package-gate.mjs';
import { materialOf, loadPackage, packageIdentity } from '../lib/package-quick.mjs';
import { incompleteRuns, latestRecord, openRun, packageRoot, recordS, sOfSuite, sweepOwnedTrees, writeHalt, writeRecord } from '../lib/package-record.mjs';
import { LATER_TASKS, registerPackage } from '../lib/package-registry.mjs';
import { quickHome, quickTask } from './quick-leak-fixture.mjs';
import { SPEC_TEXT, TOKEN, fakeWorld, sha, writePackage } from './quick-package-fixture.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const PINS = Object.freeze({ runnerSha256: 'a'.repeat(64), execLayerSha256: 'b'.repeat(64) });
const GATE = Object.freeze({ verdict: 'proceed', date: '2026-11-09', reviewers: 2 });
const LOAD = Object.freeze({ slowestWallMs: 450_000, twinWallMs: 20_000, durationS: 540 });
const MEMORY = memoryLimit().value.bytes;

/** A green record over `pkg`: every item ok, the material as it is now. */
const greenRecord = (pkg, over = {}) => ({
  items: { gate: { ok: true }, suite: { ok: true }, refs: { ok: true }, load: { ok: true }, witnesses: { ok: true } }, material: materialOf(pkg), pins: PINS, memoryBytes: MEMORY, referenceVerdicts: { csharp: 'accepted', node: 'accepted' }, load: LOAD, gate: GATE, ...over,
});
const packagesFile = () => path.join(tmpDir('pkg-registry'), 'packages.json');
const frozenFile = () => path.join(tmpDir('pkg-frozen'), 'heldout-frozen.json');
const load = (p, world = fakeWorld()) => { const r = loadPackage({ home: p.home, task: p.task, world }); assert.equal(r.ok, true, r.reason); return r.value; };
const register = ({ p, pkg, record, files = { packagesFile: packagesFile(), frozenFile: frozenFile() }, tasks }) => registerPackage({ home: p.home, task: p.task, tasks: tasks ?? [p.task], record: record ?? greenRecord(pkg), pkg, pinsFn: () => PINS, now: () => new Date('2026-11-09T10:00:00Z'), ...files });

test('The_package_is_red_while_the_conformance_check_fails_or_the_verdict_row_is_still_its_placeholder', async t => {
  const record = tmpDir('pkg-gate');
  const gateRecord = path.join(record, 'spec-02.md');
  writeFileSync(gateRecord, 'verdict proceed; 2 reviewers; the findings are in words\n');
  const run = fixture => runConformance({ repo: fixture.repo, slug: fixture.task, format: fixture.format, record: recordFile(fixture) });
  const broken = fixtureRepo(t, 'olympiad', { spec: [['`s02` | `1⏎` | `1⏎`', '`s02` | `2⏎` | `1⏎`']] });
  const red = gateItem({ specText: broken.read('spec'), conformance: await run(broken), task: quickTask('05'), recordFile: gateRecord });
  assert.equal(red.ok, false);
  assert.ok(/conformance check failed \(exit 1\): FAIL samples/.test(red.sentence), red.sentence);
  const placeholder = fixtureRepo(t, 'olympiad');
  const open = gateItem({ specText: placeholder.read('spec'), conformance: await run(placeholder), task: quickTask('05'), recordFile: gateRecord });
  assert.ok(open.ok === false && /still its placeholder/.test(open.sentence), open.sentence);
  const gated = fixtureRepo(t, 'olympiad', { spec: [['| Gate verdict | not yet gated |', '| Gate verdict | `proceed` · 2026-11-09 |']] });
  const green = gateItem({ specText: gated.read('spec'), conformance: await run(gated), task: quickTask('05'), recordFile: gateRecord });
  assert.equal(green.ok, true, green.sentence);
  assert.deepEqual(green.gate, { verdict: 'proceed', date: '2026-11-09', reviewers: 2 });
  assert.equal(gateItem({ specText: gated.read('spec'), conformance: await run(gated), task: quickTask('05'), recordFile: path.join(record, 'none.md') }).ok, false, 'a verdict with no gate record is red');
  assert.equal(gateItem({ specText: gated.read('spec'), conformance: null, task: quickTask('05'), recordFile: gateRecord }).notRun, true, 'a command that was not given the task repository says the checker was not run');
});

test('The_package_identity_moves_with_any_byte_of_the_material_and_with_nothing_else', () => {
  const identityOf = (p, world) => { const pkg = load(p, world); return packageIdentity({ ...materialOf(pkg), gate: GATE, memoryBytes: MEMORY, loadDurationS: 540, pins: PINS }); };
  const a = writePackage();
  const base = identityOf(a);
  assert.equal(identityOf(a), base, 'the same bytes twice');
  assert.match(base, /^[0-9a-f]{64}$/);
  const moved = [
    ['a witness overlay', p => writeFileSync(path.join(p.mutantsDir, 'MXX-alpha-c', 'Program.cs'), '// WITNESS:MXX-alpha-c\n// one byte more\n')],
    ['a case', p => writeFileSync(path.join(p.casesDir, 'h003.in'), 'scenario 2\nchanged\n')],
    ['a reference file', p => writeFileSync(path.join(p.refsDir, 'node', 'src', 'main.ts'), '// one byte more in the reference\n')],
    ['manifest.json', p => writeFileSync(p.manifestFile, `${JSON.stringify({ ...JSON.parse(readFileSync(p.manifestFile, 'utf8')), references: { note: 'x' } }, null, 2)}\n`)],
  ];
  for (const [what, edit] of moved) {
    const p = writePackage();
    edit(p);
    assert.notEqual(identityOf(p), base, `one byte of ${what} moves the identity`);
  }
  const changedSpec = `${SPEC_TEXT}one more sentence.\n`;
  const p = writePackage();
  const again = loadPackage({ home: p.home, task: { ...p.task, spec: { ...p.task.spec, sha256: sha(changedSpec) } }, world: fakeWorld({ specText: changedSpec }) });
  assert.notEqual(packageIdentity({ ...materialOf(again.value), gate: GATE, memoryBytes: MEMORY, loadDurationS: 540, pins: PINS }), base, 'the spec moves it');
  const pkg = load(a);
  const rerun = packageIdentity({ ...materialOf(pkg), gate: GATE, memoryBytes: MEMORY, loadDurationS: 540, pins: PINS, load: { slowestWallMs: 1, twinWallMs: 2 }, validatedOn: 'later' });
  assert.equal(rerun, base, 'a re-run\'s measured numbers are not in the identity');
});

test('The_registry_entry_holds_counts_hashes_and_durations_only', () => {
  const p = writePackage({ load: { cases: ['h003'], durationS: 540 } });
  const pkg = load(p);
  const files = { packagesFile: packagesFile(), frozenFile: frozenFile() };
  assert.equal(register({ p, pkg, files }).ok, true);
  const text = readFileSync(files.packagesFile, 'utf8');
  const entry = JSON.parse(text)['05'];
  assert.equal(/h\d{3}|MXX|trap-|alpha|beta|testsToDelete|\/home|\/tmp/.test(text), false, 'no case id, witness or trap id, no absolute path');
  const longest = JSON.stringify(entry, (k, v) => v, 0).match(/"[^"]{65,}"/g);
  assert.equal(longest, null, 'no text longer than a hash');
  assert.deepEqual(entry.counts, { cases: 4, traps: 2, rows: 3, witnesses: 3 }, 'the counts of the manifest');
  assert.equal(Number.isInteger(entry.load.slowestWallMs) && Number.isInteger(entry.load.twinWallMs) && Number.isInteger(entry.load.durationS), true, 'integers of milliseconds');
  assert.deepEqual(entry.pins, PINS, 'the runner and execution-layer pins the validation ran under');
  assert.equal(entry.memoryBytes, MEMORY);
  assert.equal(text.includes(TOKEN), false);
});

test('register_refuses_while_any_item_is_red_or_the_record_is_older_than_the_material', () => {
  const p = writePackage();
  const pkg = load(p);
  const files = { packagesFile: packagesFile(), frozenFile: frozenFile() };
  const red = register({ p, pkg, files, record: greenRecord(pkg, { items: { ...greenRecord(pkg).items, witnesses: { ok: false } } }) });
  assert.ok(red.ok === false && /witnesses is red or not run/.test(red.reason), red.reason);
  assert.equal(existsSync(files.packagesFile), false, 'nothing is written');
  const stale = greenRecord(pkg);
  writeFileSync(path.join(p.refsDir, 'csharp', 'Program.cs'), '// the reference changed after the run\n');
  const moved = register({ p, pkg: load(p), files, record: stale });
  assert.ok(moved.ok === false && /older than the material: a reference changed/.test(moved.reason), moved.reason);
  const fresh = load(p);
  const pinsMoved = registerPackage({ home: p.home, task: p.task, tasks: [p.task], record: greenRecord(fresh), pkg: fresh, pinsFn: () => ({ ...PINS, runnerSha256: 'c'.repeat(64) }), ...files });
  assert.ok(pinsMoved.ok === false && /runner or the execution-layer pin moved/.test(pinsMoved.reason), pinsMoved.reason);
  const first = register({ p, pkg: fresh, files });
  assert.deepEqual([first.ok, first.value?.state], [true, 'registered']);
  const bytes = readFileSync(files.packagesFile);
  const second = register({ p, pkg: fresh, files });
  assert.deepEqual([second.ok, second.value?.state, second.value?.identity], [true, 'unchanged', first.value.identity], 'a second write with the same identity is a no-op');
  assert.deepEqual(readFileSync(files.packagesFile), bytes);
});

test('A_packages_entry_whose_archive_hash_differs_from_the_registry_is_refused_naming_the_task_and_both_files', () => {
  const p = writePackage();
  const pkg = load(p);
  const files = { packagesFile: packagesFile(), frozenFile: frozenFile() };
  writeFileSync(files.packagesFile, JSON.stringify({ '05': { archiveSha256: 'a'.repeat(64) } }));
  writeFileSync(files.frozenFile, JSON.stringify({ tasks: { '05': { archiveSha256: 'd'.repeat(64), treeSha256: 'e'.repeat(64), keySha256: 'f'.repeat(64) } } }));
  const r = register({ p, pkg, files });
  assert.ok(r.ok === false && /task 05/.test(r.reason) && /packages\.json/.test(r.reason) && /heldout-frozen\.json/.test(r.reason), r.reason);
});

// ---- the registration of the memory value (P9) ------------------------------------------------------------------------------------------------------------

const TASKS = ['04', '05', '06', '07', '08', '10'];
const packagesOf = ids => Object.fromEntries(ids.map(id => [id, { memoryBytes: MEMORY, referenceVerdicts: { csharp: 'accepted', node: 'accepted' }, refsTreeSha256: { csharp: sha(`c${id}`), node: sha(`n${id}`) } }]));
const memoryCopy = () => { const file = path.join(tmpDir('pkg-memory'), 'memory-limit.json'); copyFileSync(MEMORY_LIMIT_FILE, file); return file; };

test('The_registration_field_is_written_only_when_all_twelve_references_pass_and_nothing_else_writes_it', () => {
  const file = memoryCopy();
  const before = memoryLimit({ file }).value;
  assert.equal(before.registration, null);
  assert.match(JSON.parse(readFileSync(file, 'utf8')).note, /chosen/, 'the note reads chosen until the field exists');
  const eleven = packagesOf(TASKS);
  eleven['08'].referenceVerdicts.node = 'rejected';
  const refused = registerMemoryValue({ packages: eleven, file });
  assert.ok(refused.ok === false && /node reference of task 08/.test(refused.reason), refused.reason);
  const other = packagesOf(TASKS);
  other['06'].memoryBytes = 268_435_456;
  assert.ok(registerMemoryValue({ packages: other, file }).ok === false, 'a reference that passed at another value does not count');
  assert.equal(memoryLimit({ file }).value.registration, null, 'nothing was written');
  const done = registerMemoryValue({ packages: packagesOf(TASKS), file, now: () => new Date('2026-11-12T08:00:00Z') });
  assert.equal(done.ok, true, done.reason);
  const after = memoryLimit({ file });
  assert.equal(after.ok, true, 'memoryLimit() still loads the file');
  assert.equal(after.value.bytes, before.bytes);
  assert.equal(after.value.registration.date, '2026-11-12');
  assert.equal(after.value.registration.references.length, 12);
  assert.deepEqual(after.value.registration.references.slice(0, 2), [{ task: '04', stack: 'csharp', treeSha256: sha('c04') }, { task: '04', stack: 'node', treeSha256: sha('n04') }]);
  const note = JSON.parse(readFileSync(file, 'utf8')).note;
  assert.ok(/registration field/.test(note) && !/chosen/.test(note), note);
  const bytes = readFileSync(file);
  assert.equal(registerMemoryValue({ packages: packagesOf(TASKS), file }).value, 'unchanged', 'a second write is a no-op');
  assert.deepEqual(readFileSync(file), bytes);
  const bad = JSON.parse(readFileSync(file, 'utf8'));
  bad.registration.references[1] = { ...bad.registration.references[0] };
  writeFileSync(file, JSON.stringify(bad));
  assert.equal(memoryLimit({ file }).ok, false, 'a registration whose twelve identities are not twelve distinct task and stack pairs is refused');
});

const WRITERS = /\bregistration\s*:\s*\{|\.registration\s*=[^=]/;
const libFiles = () => readdirSync(path.join(SPIKE, 'lib')).filter(f => f.endsWith('.mjs'));

test('No_module_but_memory_limit_writes_the_registration_field', () => {
  const writers = libFiles().filter(f => WRITERS.test(readFileSync(path.join(SPIKE, 'lib', f), 'utf8')));
  assert.deepEqual(writers, ['memory-limit.mjs'], 'the one writer');
  assert.equal(WRITERS.test('const x = { registration: { date } }'), true, 'the scan still finds a writer of the shape it looks for');
});

test('A_task_registers_before_the_memory_value_is_registered_and_register_memory_waits_for_all_six', () => {
  const p = writePackage();
  const pkg = load(p);
  const files = { packagesFile: packagesFile(), frozenFile: frozenFile() };
  assert.equal(register({ p, pkg, files }).ok, true, 'task 05 registers with no memory registration');
  const entry = JSON.parse(readFileSync(files.packagesFile, 'utf8'))['05'];
  assert.equal(entry.memoryBytes, MEMORY, 'the entry records the value its references passed at');
  const file = memoryCopy();
  const waiting = registerMemoryValue({ packages: JSON.parse(readFileSync(files.packagesFile, 'utf8')), file });
  assert.ok(waiting.ok === false && /task 04/.test(waiting.reason), waiting.reason);
  assert.equal(memoryLimit({ file }).value.registration, null);
  const all = { ...packagesOf(TASKS), '05': { ...packagesOf(['05'])['05'], refsTreeSha256: entry.refsTreeSha256, referenceVerdicts: entry.referenceVerdicts, memoryBytes: entry.memoryBytes } };
  assert.equal(registerMemoryValue({ packages: all, file }).ok, true, 'after all six it registers');
});

// ---- S, apart from registration (finding 2) -----------------------------------------------------------------------------------------------------------------

/** Five later tasks in one home, each a green package: `{ home, ps: { id: { p, pkg } } }`. */
function fiveTasks() {
  const home = quickHome();
  const ps = Object.fromEntries(LATER_TASKS.map(id => { const p = writePackage({ taskId: id, home }); return [id, { p, pkg: load(p) }]; }));
  return { home, ps, tasks: LATER_TASKS.map(id => ps[id].p.task) };
}
const registerLater = (five, id, files) => registerPackage({ home: five.home, task: five.ps[id].p.task, tasks: five.tasks, record: greenRecord(five.ps[id].pkg), pkg: five.ps[id].pkg, pinsFn: () => PINS, now: () => new Date('2026-11-09T10:00:00Z'), ...files });

test('Register_of_a_later_task_refuses_until_all_five_S_are_recorded_and_passes_after', () => {
  const five = fiveTasks();
  const files = { packagesFile: packagesFile(), frozenFile: frozenFile() };
  const none = registerLater(five, '04', files);
  assert.ok(none.ok === false && ['04', '06', '07', '08', '10'].every(id => none.reason.includes(id)), none.reason);
  for (const id of ['04', '06', '07', '08']) assert.equal(recordS({ home: five.home, task: five.ps[id].p.task, treeSha256: five.ps[id].pkg.held.treeSha256, sBytes: 100 }).ok, true);
  const four = registerLater(five, '04', files);
  assert.ok(four.ok === false && /task 10/.test(four.reason) && !/task 04/.test(four.reason), four.reason);
  assert.equal(recordS({ home: five.home, task: five.ps['10'].p.task, treeSha256: five.ps['10'].pkg.held.treeSha256, sBytes: 9000 }).ok, true);
  const all = registerLater(five, '04', files);
  assert.equal(all.ok, true, all.reason);
  assert.equal(recordS({ home: five.home, task: five.ps['10'].p.task, treeSha256: five.ps['10'].pkg.held.treeSha256, sBytes: 9000 }).value, 'unchanged', 'S is recorded once per suite');
});

test('An_S_recorded_for_an_older_suite_does_not_count', () => {
  const five = fiveTasks();
  const files = { packagesFile: packagesFile(), frozenFile: frozenFile() };
  for (const id of LATER_TASKS) recordS({ home: five.home, task: five.ps[id].p.task, treeSha256: five.ps[id].pkg.held.treeSha256, sBytes: 100 });
  assert.equal(registerLater(five, '04', files).ok, true, 'control: all five counted');
  const p10 = five.ps['10'].p;
  writeFileSync(path.join(p10.casesDir, 'h002.out'), 'transcript 2\na new reply\n');
  const changed = loadPackage({ home: five.home, task: p10.task, world: fakeWorld() }).value;
  assert.notEqual(changed.held.treeSha256, five.ps['10'].pkg.held.treeSha256);
  assert.equal(sOfSuite({ home: five.home, task: p10.task, treeSha256: changed.held.treeSha256 }), null, 'the older suite\'s S is not this suite\'s');
  const stale = registerLater(five, '06', { packagesFile: packagesFile(), frozenFile: frozenFile() });
  assert.ok(stale.ok === false && /task 10/.test(stale.reason), stale.reason);
  assert.equal(recordS({ home: five.home, task: p10.task, treeSha256: changed.held.treeSha256, sBytes: 12_000 }).value, 'recorded');
  assert.equal(JSON.parse(readFileSync(path.join(packageRoot(five.home, p10.task), 's.json'), 'utf8')).counts.length, 2, 'the earlier count is kept: the file is appended, never rewritten shorter');
});

// ---- the run folders (finding 4) -----------------------------------------------------------------------------------------------------------------------------

const clockAt = iso => () => new Date(iso);

test('A_killed_run_leaves_no_record_and_the_next_run_names_it_incomplete', () => {
  const home = quickHome();
  const task = quickTask('05');
  const first = openRun({ home, task, materialSha: 'm1', now: clockAt('2026-11-10T10:00:00Z'), genId: () => 'aaaa1111' }).value;
  mkdirSync(path.join(first.dir, 'refs-csharp', 'tree'), { recursive: true });
  writeFileSync(path.join(first.dir, 'refs-csharp', 'tree', 'Program.cs'), '// a built tree\n');
  mkdirSync(path.join(first.dir, 'refs-csharp', 'passes'), { recursive: true });
  writeFileSync(path.join(first.dir, 'refs-csharp', 'passes', 'p01.json'), '{}');
  assert.equal(latestRecord({ home, task }), null, 'a folder with no record.json is not a result');
  const second = openRun({ home, task, materialSha: 'm1', now: clockAt('2026-11-10T11:00:00Z'), genId: () => 'bbbb2222' }).value;
  assert.deepEqual([second.resumed, second.incomplete], [false, [first.name]], 'a killed run is named incomplete, and not resumed');
  assert.equal(existsSync(path.join(first.dir, 'refs-csharp', 'tree')), false, 'its built tree is removed');
  assert.equal(existsSync(path.join(first.dir, 'refs-csharp', 'passes', 'p01.json')), true, 'what it recorded stays: the folders are append only');
  assert.equal(writeRecord(second, { complete: true, incompleteRuns: second.incomplete }).ok, true);
  assert.deepEqual(latestRecord({ home, task }).incompleteRuns, [first.name]);
  assert.equal(writeRecord(second, { complete: true }).ok, false, 'a record is written once');
  assert.deepEqual(incompleteRuns(path.dirname(second.dir)).map(r => r.name), [first.name]);
});

test('A_halted_run_of_the_same_material_is_resumed_in_its_own_folder_and_another_material_is_not', () => {
  const home = quickHome();
  const task = quickTask('05');
  const first = openRun({ home, task, materialSha: 'm1', now: clockAt('2026-11-10T10:00:00Z'), genId: () => 'aaaa1111' }).value;
  writeHalt(first, { reason: 'the machine is not quiet', materialSha: 'm1' });
  const back = openRun({ home, task, materialSha: 'm1', now: clockAt('2026-11-10T11:00:00Z'), genId: () => 'bbbb2222' }).value;
  assert.deepEqual([back.name, back.resumed, back.incomplete], [first.name, true, []]);
  const other = openRun({ home, task, materialSha: 'm2', now: clockAt('2026-11-10T12:00:00Z'), genId: () => 'cccc3333' }).value;
  assert.deepEqual([other.resumed, other.incomplete], [false, [first.name]], 'a halt over other material is not resumed');
});

test('Temp_trees_of_a_dead_run_are_swept_at_the_start', () => {
  const parent = tmpDir('pkg-temp');
  const owned = ownedFor({ token: 'bench' });
  const dead = path.join(parent, owned.ownedName('tree'));
  const live = path.join(parent, owned.ownedName('tree'));
  const squatter = path.join(parent, 'some-other-folder');
  for (const dir of [dead, live, squatter]) { mkdirSync(path.join(dir, 'src'), { recursive: true }); writeFileSync(path.join(dir, 'src', 'main.ts'), 'x'); }
  const gone = spawnSync(process.execPath, ['-e', '']);
  writeFileSync(`${dead}.owner`, `${formatOwner({ ...thisOwner(), pid: gone.pid, start: 1 })}\n`);
  writeOwner(live);
  const r = sweepOwnedTrees({ parent });
  assert.deepEqual(r.removed, [dead], 'the tree of a dead run is removed');
  assert.deepEqual(r.live, [live], 'a live run\'s tree is named and kept');
  assert.equal(existsSync(dead), false);
  assert.equal(existsSync(`${dead}.owner`), false, 'its claim goes with it');
  assert.equal(existsSync(live) && existsSync(squatter), true, 'nothing else is touched');
});
