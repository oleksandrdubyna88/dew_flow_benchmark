// The suite, the R-ids, the manifest and the load rule of a package (F2.S1, section 2.3, scenarios 16 to 20), over an INVENTED task in a temp home. The sentences the tool says name cases and stay on the host.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { loadPackage } from '../lib/package-quick.mjs';
import { loadRule } from '../lib/package-refs.mjs';
import { shapeProblem } from '../lib/package-manifest.mjs';
import { casesTxtProblems, folderProblems, largestReplyBody, specRIds, suiteProblems, trapTableProblems, trapsTxtProblems } from '../lib/package-suite.mjs';
import { maxOf } from '../lib/package-max.mjs';
import { suiteStep } from '../lib/package-steps.mjs';
import { packageRoot, sOfSuite } from '../lib/package-record.mjs';
import { treeHashOf } from '../lib/package-quick.mjs';
import { IDS, SAMPLE_IDS, SPEC_TEXT, TOKEN, fakeWorld, sha, writePackage } from './quick-package-fixture.mjs';


test('Every_case_cites_at_least_one_R_id_that_exists_in_the_corrected_spec', () => {
  const rIds = specRIds(SPEC_TEXT);
  assert.deepEqual([...rIds], [1, 2, 3, 4, 5, 6, 7], 'the ids come from the spec by the conformance checker\'s own parser');
  assert.deepEqual(casesTxtProblems({ text: 'h001 bytes R1 R7\nh002 json+relational R3\n', rIds }), [], 'control: every line is well formed');
  const unknown = casesTxtProblems({ text: 'h001 bytes R1\nh002 bytes R9\n', rIds });
  assert.equal(unknown.length, 1);
  assert.ok(unknown[0].includes('h002') && unknown[0].includes('R9'), unknown[0]);
  const none = casesTxtProblems({ text: 'h003 bytes\n', rIds });
  assert.ok(none.length === 1 && none[0].includes('h003') && /names no requirement/.test(none[0]), none.join('|'));
  const word = casesTxtProblems({ text: 'h004 exact R1\n', rIds });
  assert.ok(word.length === 1 && word[0].includes('h004') && word[0].includes('exact'), word.join('|'));
  assert.deepEqual(casesTxtProblems({ text: '# a comment\n\nh001 normalised R2\n', rIds }), [], 'comments and blank lines are not cases');
});

test('The_manifest_the_folder_and_cases_txt_agree_byte_for_byte', async () => {
  const p = writePackage();
  const loaded = await loadPackage({ home: p.home, task: p.task, world: fakeWorld() });
  assert.equal(loaded.ok, true, loaded.reason);
  assert.deepEqual(folderProblems({ manifest: loaded.value.manifest, casesDir: p.casesDir }), [], 'control: the folder is what gen.py wrote');
  writeFileSync(path.join(p.casesDir, 'h002.out'), `transcript 2\n${TOKEN}-changed-after-gen\n`);
  const changed = folderProblems({ manifest: loaded.value.manifest, casesDir: p.casesDir });
  assert.equal(changed.length, 1);
  assert.ok(changed[0].includes('h002') && /differs from the manifest/.test(changed[0]), changed[0]);
  const extra = folderProblems({ manifest: { ...loaded.value.manifest, cases: { ...loaded.value.manifest.cases, h099: { in: sha('a'), out: sha('b') } }, caseCount: 5 }, casesDir: p.casesDir });
  assert.ok(extra.some(s => s.includes('h099') && /no pair/.test(s)), extra.join('|'));
  const unlisted = folderProblems({ manifest: { ...loaded.value.manifest, cases: Object.fromEntries(Object.entries(loaded.value.manifest.cases).slice(1)), caseCount: 3 }, casesDir: p.casesDir });
  assert.ok(unlisted.some(s => s.includes('h001') && /not in the manifest/.test(s)), unlisted.join('|'));
});

test('A_pair_missing_from_cases_txt_is_found_by_the_held_out_readers_own_findings_which_the_package_reuses', async () => {
  const p = writePackage();
  writeFileSync(path.join(p.casesDir, 'cases.txt'), 'h001 bytes R1\nh002 bytes R1\nh003 bytes R1\n');
  const loaded = await loadPackage({ home: p.home, task: p.task, world: fakeWorld() });
  assert.equal(loaded.ok, false);
  assert.ok(/cases\.txt does not list h004/.test(loaded.reason), loaded.reason);
});

test('traps_txt_is_exactly_the_union_of_the_trap_tables_cases', () => {
  const manifest = { traps: { a: { stack: 'both', cases: ['h001', 'h002'] }, b: { stack: 'node', cases: ['h002', 'h003'] } } };
  assert.deepEqual(trapsTxtProblems({ manifest, traps: ['h001', 'h002', 'h003'] }), [], 'control: the union');
  const extra = trapsTxtProblems({ manifest, traps: ['h001', 'h002', 'h003', 'h004'] });
  assert.ok(extra.length === 1 && extra[0].includes('h004') && /no row of the trap table names/.test(extra[0]), extra.join('|'));
  const missing = trapsTxtProblems({ manifest, traps: ['h001', 'h002'] });
  assert.ok(missing.length === 1 && missing[0].includes('h003') && /not in traps\.txt/.test(missing[0]), missing.join('|'));
});

// ---- a trap row may name visible samples beside a held-out case (parent entry 10.1) ---------------------------------------------------------------------

const ROW_WITNESSES = { Wc: { trap: 'a', stack: 'csharp', kind: 'deterministic' } };
const rowOf = cases => ({ traps: { a: { stack: 'csharp', cases } }, witnesses: ROW_WITNESSES });
const tableOf = (cases, sampleIds = SAMPLE_IDS) => trapTableProblems({ manifest: rowOf(cases), caseIds: IDS, sampleIds });

test('A_trap_row_may_name_a_visible_sample_the_pinned_spec_holds_beside_a_held_out_case', () => {
  assert.deepEqual(tableOf(['h001']), [], 'control: a row of held-out ids only');
  assert.deepEqual(tableOf(['h001', 's01']), [], 'a held-out case and a sample the spec holds');
  assert.deepEqual(tableOf(['h002', 's01', 's02']), [], 'two samples the spec holds');
});

test('A_sample_id_the_pinned_spec_does_not_hold_is_red_naming_the_row_and_the_id', () => {
  const unknown = tableOf(['h001', 's07']);
  assert.equal(unknown.length, 1, unknown.join('|'));
  assert.ok(unknown[0].includes('trap a on csharp') && unknown[0].includes('s07') && /not one of the samples/.test(unknown[0]), unknown[0]);
  assert.equal(tableOf(['h001', 's01'], ['s02']).length, 1, 'the same row is red when the spec holds another sample only');
  const unreadable = tableOf(['h001', 's01'], null);
  assert.ok(unreadable.length === 1 && /samples of the spec cannot be read/.test(unreadable[0]), `a spec whose samples cannot be parsed is a problem: ${unreadable.join('|')}`);
});

test('A_trap_row_of_sample_ids_only_is_red_because_a_trap_needs_a_held_out_case', () => {
  const only = tableOf(['s01', 's02']);
  assert.equal(only.length, 1, only.join('|'));
  assert.ok(only[0].includes('trap a on csharp') && /no held-out case/.test(only[0]), only[0]);
  assert.ok(tableOf([]).some(s => /names no case/.test(s)), 'an empty row stays red as before');
});

test('traps_txt_holds_the_held_out_ids_of_the_table_only_and_a_sample_id_is_never_in_it', () => {
  const manifest = { traps: { a: { stack: 'both', cases: ['h001', 's01'] }, b: { stack: 'node', cases: ['h002', 'h003'] } } };
  assert.deepEqual(trapsTxtProblems({ manifest, traps: ['h001', 'h002', 'h003'] }), [], 'the held-out union, while a row also names a sample: green');
  const sample = trapsTxtProblems({ manifest, traps: ['h001', 'h002', 'h003', 's01'] });
  assert.ok(sample.length === 1 && sample[0].includes('s01') && /sample/.test(sample[0]), sample.join('|'));
  const missing = trapsTxtProblems({ manifest, traps: ['h001', 'h002'] });
  assert.ok(missing.length === 1 && missing[0].includes('h003') && /not in traps\.txt/.test(missing[0]), missing.join('|'));
});

test('A_load_case_stays_a_held_out_id_and_the_manifest_shape_takes_a_sample_id_in_a_trap_row_only', () => {
  const base = { caseCount: 0, cases: {} };
  assert.equal(shapeProblem({ ...base, traps: { a: { stack: 'node', cases: ['h001', 's01'] } } }), null, 'a trap row may carry a sample id');
  assert.ok(/traps must map/.test(shapeProblem({ ...base, traps: { a: { stack: 'node', cases: ['s1'] } } })), 's1 is no sample id');
  assert.ok(/traps must map/.test(shapeProblem({ ...base, traps: { a: { stack: 'node', cases: ['S01'] } } })), 'no normalisation: S01 is refused');
  assert.ok(/traps must map/.test(shapeProblem({ ...base, traps: { a: { stack: 'node', cases: [' s01'] } } })), 'no trimming');
  assert.equal(shapeProblem({ ...base, load: { cases: ['h002'], durationS: 5 } }), null, 'control: a held-out load case');
  assert.ok(/load must be/.test(shapeProblem({ ...base, load: { cases: ['s01'], durationS: 5 } })), 'a load case s01 is still refused');
});

test('The_suite_takes_the_sample_ids_of_a_row_from_the_pinned_specs_own_samples_and_traps_txt_from_the_held_out_part', () => {
  const p = writePackage({ traps: { 'trap-alpha': { stack: 'both', cases: ['h001', 'h002', 's01'] }, 'trap-beta': { stack: 'node', cases: ['h003'] } } });
  const pkg = loadPackage({ home: p.home, task: p.task, world: fakeWorld() }).value;
  assert.deepEqual(suiteProblems(pkg), [], 'a row that names a sample of the spec, with traps.txt holding the held-out ids: the whole suite is green');
  const unknown = suiteProblems({ ...pkg, manifest: { ...pkg.manifest, traps: { ...pkg.manifest.traps, 'trap-beta': { stack: 'node', cases: ['h003', 's09'] } } } });
  assert.ok(unknown.length === 1 && unknown[0].includes('trap trap-beta on node') && unknown[0].includes('s09'), unknown.join('|'));
  const noSamples = suiteProblems({ ...pkg, specText: SPEC_TEXT.slice(0, SPEC_TEXT.indexOf('## 6.')) });
  assert.ok(noSamples.some(s => /samples of the spec cannot be read/.test(s)), `a spec with no readable samples is a problem, never no samples: ${noSamples.join('|')}`);
});

test('A_spec_whose_sha256_differs_from_the_pinned_one_is_refused', async () => {
  const p = writePackage();
  const refused = await loadPackage({ home: p.home, task: p.task, world: fakeWorld({ specText: `${SPEC_TEXT}\nAn edit after the pin.\n` }) });
  assert.equal(refused.ok, false);
  assert.ok(/sha256/.test(refused.reason) && refused.reason.includes(p.task.spec.sha256.slice(0, 12)), refused.reason);
  const same = await loadPackage({ home: p.home, task: p.task, world: fakeWorld() });
  assert.equal(same.ok, true, 'control: the pinned text loads');
  assert.equal(same.value.specSha256, p.task.spec.sha256);
});

// ---- the load rule ----------------------------------------------------------------------------------------------------------------------------------

/** Passes of two references under both profiles, the load case `h003` taking `walls[stack][profile]` ms. */
const passesWith = walls => ['csharp', 'node'].flatMap(stack => ['2cpu', '8cpu'].flatMap((profile, i) => [{ stack, profile, n: i + 1, cases: [{ id: 'h003', wallMs: walls[stack][profile] }, { id: 'h001', wallMs: 900_000 }] }]));
const WALLS = { csharp: { '2cpu': 400_000, '8cpu': 300_000 }, node: { '2cpu': 450_000, '8cpu': 350_000 } };

test('The_F_LOAD_duration_rule_is_measured_over_every_pass_of_both_references_and_540_is_not_assumed', () => {
  const green = loadRule({ passes: passesWith(WALLS), loadCases: ['h003'], twinWallMs: 20_000, durationS: 540 });
  assert.equal(green.ok, true, green.problems?.join('|'));
  assert.deepEqual([green.slowestWallMs, green.twinWallMs, green.durationS], [450_000, 20_000, 540], 'the three numbers, in milliseconds and seconds');
  assert.equal(Number.isInteger(green.slowestWallMs) && Number.isInteger(green.twinWallMs), true);
  const slow = loadRule({ passes: passesWith({ ...WALLS, node: { '2cpu': 575_000, '8cpu': 350_000 } }), loadCases: ['h003'], twinWallMs: 5_000, durationS: 540 });
  assert.equal(slow.ok, false);
  assert.ok(/600 s minus 30 s/.test(slow.problems[0]) && /at most 535 s/.test(slow.problems[0]), slow.problems[0]);
  assert.equal(slow.durationS, 535, 'the duration to write into the spec: shortened by the excess');
  const twinHeavy = loadRule({ passes: passesWith({ ...WALLS, node: { '2cpu': 530_000, '8cpu': 350_000 } }), loadCases: ['h003'], twinWallMs: 40_000, durationS: 540 });
  assert.equal(twinHeavy.ok, false);
  assert.ok(/twice the non-load part/.test(twinHeavy.problems[0]), twinHeavy.problems[0]);
  assert.equal(twinHeavy.durationS, 540 - Math.ceil((530_000 - (600_000 - 80_000)) / 1000));
  const noTwin = loadRule({ passes: passesWith(WALLS), loadCases: ['h003'], twinWallMs: null, durationS: 540 });
  assert.equal(noTwin.ok, false, 'never green by the spec\'s number');
  assert.ok(/no twin measurement/.test(noTwin.problems[0]), noTwin.problems[0]);
  assert.ok(loadRule({ passes: passesWith(WALLS), loadCases: [], twinWallMs: null, durationS: 540 }).ok === true, 'a package with no load case has nothing to measure');
});

test('The_largest_expected_reply_body_S_is_counted_from_the_suite_as_bytes_with_escapes_decoded', () => {
  const p = writePackage();
  writeFileSync(path.join(p.casesDir, 'h003.out'), `transcript 2\nrecv c "${'x'.repeat(30)}\\r\\n"\nrecv c "short"\n`);
  assert.equal(largestReplyBody(p.casesDir), 32, 'thirty bytes and a CR LF, the largest quoted string of any expected transcript');
  assert.equal(IDS.length, 4);
});

const mkdirSyncP = dir => mkdirSync(dir, { recursive: true });
const PINNED = { exit: 0, firstLine: '', pinned: true };
const outOf = (p, name, text) => writeFileSync(path.join(p.casesDir, name), text);

test('S_is_counted_as_the_runner_reads_a_body_a_json_value_a_repeat_count_and_adjacent_strings_and_a_header_is_no_body', () => {
  const p = writePackage();
  for (const id of IDS) outOf(p, `${id}.out`, 'transcript 2\nready\nexit 0\n');
  assert.equal(largestReplyBody(p.casesDir), 0, 'control: no body at all');
  outOf(p, 'h001.out', 'transcript 2\nreply a 200\n  header "content-type" "' + 'h'.repeat(500) + '"\n  body json {"items": [ "aaaaaaaaaa" , "bbbbbbbbbb" ]}\nexit 0\n');
  assert.equal(largestReplyBody(p.casesDir), JSON.stringify({ items: ['aaaaaaaaaa', 'bbbbbbbbbb'] }).length, 'a JSON body is its serialized value; a header is no body');
  outOf(p, 'h002.out', 'transcript 2\nrecv c "ab"*1000\n');
  assert.equal(largestReplyBody(p.casesDir), 2000, 'a repeat count multiplies the decoded length');
  outOf(p, 'h003.out', 'transcript 2\nreply a 200\n  body "abc" "defg\\n" "x"*3\n');
  assert.equal(largestReplyBody(p.casesDir), 2000);
  outOf(p, 'h002.out', 'transcript 2\n');
  outOf(p, 'h001.out', 'transcript 2\n');
  assert.equal(largestReplyBody(p.casesDir), 3 + 5 + 3, 'adjacent strings are one body: the sum of each item\'s decoded length times its count');
});

test('S_counts_the_visible_samples_too_and_takes_a_running_maximum_over_any_number_of_values', () => {
  const p = writePackage();
  const samples = path.join(p.dir, '..', 'samples-invented');
  writeFileSync(path.join(p.casesDir, 'h001.out'), 'transcript 2\nrecv c "short"\n');
  mkdirSyncP(samples);
  writeFileSync(path.join(samples, 's01.out'), 'transcript 2\nrecv c "' + 'v'.repeat(40) + '"\n');
  assert.equal(largestReplyBody([p.casesDir, samples]), 40, 'a visible sample is a case too');
  assert.equal(largestReplyBody(p.casesDir), 5);
  assert.equal(maxOf(Array.from({ length: 600_000 }, (_, i) => i)), 599_999, 'a running maximum: no argument list to overflow');
  assert.equal(maxOf([]), null);
});

test('S_is_recorded_only_for_a_suite_with_no_problem', () => {
  const bad = writePackage();
  writeFileSync(path.join(bad.casesDir, 'cases.txt'), 'h001 bytes R9\nh002 bytes R1\nh003 bytes R1\nh004 bytes R1\n');
  const badPkg = loadPackage({ home: bad.home, task: bad.task, world: fakeWorld() }).value;
  const r = suiteStep({ pkg: badPkg, home: bad.home, task: bad.task, conformance: null });
  assert.equal(r.item.suite.ok, false);
  assert.equal(sOfSuite({ home: bad.home, task: bad.task, treeSha256: badPkg.held.treeSha256 }), null, 'a suite with a problem has no S on record');
  const good = writePackage();
  const goodPkg = loadPackage({ home: good.home, task: good.task, world: fakeWorld() }).value;
  const samples = path.join(good.dir, '..', 'samples-for-s');
  mkdirSyncP(samples);
  writeFileSync(path.join(samples, 's01.out'), `transcript 2\nrecv c "${'v'.repeat(5000)}"\n`);
  const step = suiteStep({ pkg: goodPkg, home: good.home, task: good.task, conformance: PINNED, samplesDir: samples });
  assert.equal(step.item.suite.ok, true);
  assert.equal(step.item.suite.sBytes, 5000, 'the step counts the visible samples beside the held-out cases');
  assert.notEqual(sOfSuite({ home: good.home, task: good.task, treeSha256: goodPkg.held.treeSha256 }), null);
});

/** A good package with a sample folder holding one reply of `size` bytes. */
function withSamples(size = 5000) {
  const p = writePackage();
  const pkg = loadPackage({ home: p.home, task: p.task, world: fakeWorld() }).value;
  const samples = path.join(p.dir, '..', 'samples-for-l1');
  mkdirSyncP(samples);
  writeFileSync(path.join(samples, 's01.out'), `transcript 2\nrecv c "${'v'.repeat(size)}"\n`);
  return { p, pkg, samples };
}

test('S_is_not_recorded_without_a_task_repo', () => {
  const { p, pkg } = withSamples();
  const step = suiteStep({ pkg, home: p.home, task: p.task, conformance: null, samplesDir: null });
  assert.equal(step.item.suite.sRecorded, false);
  assert.ok(/no --task-repo/.test(step.item.suite.sNote), step.item.suite.sNote);
  assert.equal(sOfSuite({ home: p.home, task: p.task, treeSha256: pkg.held.treeSha256 }), null, 'a first wrong S would stay for ever: nothing is recorded');
  assert.equal(typeof step.item.suite.sBytes, 'number', 'S is still counted and said');
});

test('S_is_not_recorded_from_a_checkout_whose_spec_is_not_the_pinned_one', () => {
  const { p, pkg, samples } = withSamples();
  const step = suiteStep({ pkg, home: p.home, task: p.task, conformance: { exit: 4, firstLine: 'not the pinned spec', pinned: false }, samplesDir: samples });
  assert.equal(step.item.suite.sRecorded, false);
  assert.ok(/not the pinned one/.test(step.item.suite.sNote), step.item.suite.sNote);
  assert.equal(step.item.suite.sBytes < 5000, true, 'the samples of a checkout that is not the pinned one are not counted');
  assert.equal(sOfSuite({ home: p.home, task: p.task, treeSha256: pkg.held.treeSha256 }), null);
});

test('S_is_recorded_with_the_pinned_checkout_and_names_the_samples_hash', () => {
  const { p, pkg, samples } = withSamples();
  const step = suiteStep({ pkg, home: p.home, task: p.task, conformance: PINNED, samplesDir: samples });
  assert.equal(step.item.suite.sRecorded, true);
  assert.equal(step.item.suite.sBytes, 5000);
  const entry = JSON.parse(readFileSync(path.join(packageRoot(p.home, p.task), 's.json'), 'utf8')).counts.at(-1);
  assert.equal(entry.suiteTreeSha256, pkg.held.treeSha256);
  assert.equal(entry.samplesTreeSha256, treeHashOf(samples), 'the samples\' tree hash is beside the suite\'s, for audit');
  assert.equal(entry.sBytes, 5000);
});

