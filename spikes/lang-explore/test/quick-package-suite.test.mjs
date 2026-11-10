// The suite, the R-ids, the manifest and the load rule of a package (F2.S1, section 2.3, scenarios 16 to 20), over an INVENTED task in a temp home. The sentences the tool says name cases and stay on the host.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { loadPackage } from '../lib/package-quick.mjs';
import { loadRule } from '../lib/package-refs.mjs';
import { casesTxtProblems, folderProblems, largestReplyBody, specRIds, trapsTxtProblems } from '../lib/package-suite.mjs';
import { IDS, SPEC_TEXT, TOKEN, fakeWorld, sha, writePackage } from './quick-package-fixture.mjs';


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
