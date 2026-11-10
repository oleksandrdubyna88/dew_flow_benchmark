// The leak checks and the gate records of a package (F2.S1, section 2.5, scenarios 26 to 29): the stack references and the witness overlays are needles, a template's line in a reference is a collision and no needle, the needle cap holds with
// two references per task, and a gate record holds no case id, no witness or trap id, no case line and no window of the private notes. INVENTED content in a temp home; the scans over repositories make temporary git repositories.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { leakGates } from '../lib/leak-gates.mjs';
import { leakIds, publicLines } from '../lib/leak.mjs';
import { loadHeldoutQuick } from '../lib/heldout-quick.mjs';
import { substringNeedles } from '../lib/leak-material.mjs';
import { scanRepository } from '../lib/leak-scan.mjs';
import { normLine } from '../lib/leak-text.mjs';
import { commitFiles, repoWith } from './leak-scan-fixture.mjs';
import { presentMaterial, quickHome, quickTask, writeQuickHeldout } from './quick-leak-fixture.mjs';

const CASES = [{ id: 'h001', input: 'scenario 2\ndefault within 2s\nconnect c tcp\n', output: 'transcript 2\nready\nrecv c "an-invented-answer-for-the-package-leak-QZ41"\nexit 0\n' }];
const CS_LINE = 'var invented = Reference.Fold(events, seedValue, packageLeakCsharpMarker);';
const TS_LINE = 'const invented: number = foldTheEvents(events, seedValue, packageLeakNodeMarker);';
const WITNESS_LINE = 'const defect: number = foldTheEventsWrongly(events, seedValue, packageLeakWitnessMarker);';

function homeWithPackage({ manifest } = {}) {
  const home = quickHome();
  const task = quickTask('05');
  const p = writeQuickHeldout({ home, task, cases: CASES, traps: ['h001'], refs: { 'csharp/src/X.cs': `${CS_LINE}\n`, 'node/src/main.ts': `${TS_LINE}\n` } });
  mkdirSync(path.join(p.heldoutDir, 'mutants', 'M99-alpha-n', 'src'), { recursive: true });
  writeFileSync(path.join(p.heldoutDir, 'mutants', 'M99-alpha-n', 'src', 'main.ts'), `${WITNESS_LINE}\n`);
  writeFileSync(path.join(p.heldoutDir, 'manifest.json'), `${JSON.stringify(manifest ?? { caseCount: 1, cases: {}, traps: { 'trap-zeta-invented': { stack: 'both', cases: ['h001'] } }, witnesses: { 'M99-alpha-n': { trap: 'trap-zeta-invented', stack: 'node', kind: 'race' } } })}\n`);
  return { home, p };
}

const materialOf = (home, over = {}) => { const m = presentMaterial({ home, publicText: '', ...over }); assert.equal(m.ok, true, m.reason); return m.value; };

test('leakcheck_reads_the_stack_reference_folders_and_the_witness_overlays_of_a_quick_task', async () => {
  const { home } = homeWithPackage();
  const m = materialOf(home);
  const csharp = await scanRepository({ repoRoot: repoWith({ 'src/copy.cs': `x = 1\n${CS_LINE}\n` }), material: m.material });
  assert.deepEqual(csharp.findings.map(f => [f.file, f.line, f.task, f.caseFile]), [['src/copy.cs', 2, '05', 'refs/csharp/src/X.cs']], 'a line of refs-NN/csharp is a finding naming the file');
  assert.doesNotMatch(csharp.reason, /packageLeak/, 'never the text');
  const node = await scanRepository({ repoRoot: repoWith({ 'src/copy.ts': `${TS_LINE}\n` }), material: m.material });
  assert.deepEqual(node.findings.map(f => f.caseFile), ['refs/node/src/main.ts']);
  const witness = await scanRepository({ repoRoot: repoWith({ 'src/witness.ts': `${WITNESS_LINE}\n` }), material: m.material });
  assert.deepEqual(witness.findings.map(f => [f.file, f.caseFile]), [['src/witness.ts', 'mutants/M99-alpha-n/src/main.ts']], 'a line of a witness overlay is a finding');
  assert.equal((await scanRepository({ repoRoot: repoWith(), material: m.material })).ok, true, 'control: a clean repository');
});

test('The_witness_ids_and_trap_names_of_a_quick_tasks_manifest_are_ids_to_keep_out_of_the_public_notes', () => {
  const { home } = homeWithPackage();
  const m = materialOf(home);
  assert.ok(m.ids.includes('M99-alpha-n') && m.ids.includes('trap-zeta-invented'), 'the manifest\'s witness id and trap name');
  const repo = repoWith({ 'research/lang-v2/note.md': 'the witness M99-alpha-n was written first\n', 'research/lang-v2/clean.md': 'nothing here\n' });
  assert.deepEqual(leakIds({ repo, ids: m.ids }), ['note.md'], 'a finding naming the file, never the id');
  const bad = homeWithPackage({ manifest: {} });
  writeFileSync(path.join(bad.p.heldoutDir, 'manifest.json'), '{ not json');
  const refused = presentMaterial({ home: bad.home, publicText: '' });
  assert.equal(refused.ok, false, 'a manifest that cannot be read is a refusal, never "no ids"');
  assert.match(refused.reason, /manifest\.json/);
});

/** A repository whose HEAD holds `spikes/lang-bench/templates/<rel>` with `text`. */
const withTemplate = text => repoWith({ 'spikes/lang-bench/templates/csharp/daemon/Program.cs': text, 'README.md': 'a readme\n' });

test('A_line_equal_to_a_template_files_line_is_a_collision_not_a_needle', async () => {
  const shared = 'var listener = new TcpListener(IPAddress.Loopback, int.Parse(portText));';
  const repo = withTemplate(`${shared}\n`);
  const grammarLines = await publicLines({ repoRoot: repo, io: { publicRef: 'HEAD' } });
  assert.equal(grammarLines.has(normLine(shared)), true, 'the template\'s lines are read through git');
  const home = quickHome();
  writeQuickHeldout({ home, task: quickTask('05'), cases: CASES, refs: { 'csharp/src/X.cs': `${shared}\n${CS_LINE}\n` } });
  const m = materialOf(home, { grammarLines });
  const texts = m.material.substrings.filter(n => n.kind === 'code').map(n => n.text);
  assert.equal(texts.includes(normLine(shared)), false, 'the template line is no needle');
  assert.equal(texts.includes(normLine(CS_LINE)), true, 'the reference\'s own line is');
  assert.equal(m.material.collisions['05'], 1, 'it is counted as a collision');
  assert.equal((await scanRepository({ repoRoot: withTemplate(`${shared}\n`), material: m.material })).ok, true, 'so the public templates are no finding');
  const changed = `${shared.slice(0, -1)}!`;
  const m2 = (() => { const h = quickHome(); writeQuickHeldout({ home: h, task: quickTask('05'), cases: CASES, refs: { 'csharp/src/X.cs': `${changed}\n` } }); return materialOf(h, { grammarLines }); })();
  assert.equal(m2.material.substrings.some(n => n.text === normLine(changed)), true, 'the same line with one character changed is a needle');
  assert.equal(m2.material.collisions['05'], 0);
});

test('The_substring_needle_cap_holds_with_two_stack_references_per_task', () => {
  const lines = (tag, n) => Array.from({ length: n }, (_, i) => `const invented${tag}Value${i} = computeInventedThing(${i}, '${tag}-${i}', seedValue);`).join('\n');
  const home = quickHome();
  const ids = ['04', '05', '06', '07', '08', '10'];
  for (const id of ids) writeQuickHeldout({ home, task: quickTask(id), cases: CASES.map(c => ({ ...c, output: `transcript 2\nrecv c "answer-${id}-unique-line-for-the-cap-test"\n` })), refs: { 'csharp/src/X.cs': lines(`c${id}`, 700), 'node/src/main.ts': lines(`n${id}`, 700) } });
  const m = presentMaterial({ home, publicText: '', grammarLines: new Set() });
  assert.equal(m.ok, true, m.reason);
  assert.ok(m.value.material.substrings.length >= 6 * 1400 && m.value.material.substrings.length < 16384, `six tasks of two 700-line references: ${m.value.material.substrings.length} needles, under the cap`);
  const tasks = ids.map(id => ({ id, quick: true, heldout: loadHeldoutQuick(home, quickTask(id)).value }));
  const tight = substringNeedles(tasks, new Set(), new Set(), 5000);
  assert.equal(tight.ok, false, 'over the cap the check fails closed, never a slow run');
  assert.match(tight.reason, /holds \d+ needles, over its cap of 5000/);
  assert.equal(substringNeedles(tasks, new Set(), new Set()).ok, true, 'control: at the real cap the same six tasks are fine');
});

// ---- the gate records (29) ----------------------------------------------------------------------------------------------------------------------------------

const GATES = 'spikes/lang-bench/inputs/gates';
const NOTE_WINDOW = 'the fixture note names six distinct invented words';

function gateRepo(records) {
  return repoWith({ 'README.md': 'a readme\n', ...Object.fromEntries(Object.entries(records).map(([name, text]) => [`${GATES}/${name}`, text])) });
}

test('A_gate_record_holds_only_the_verdict_the_reviewer_count_and_findings_that_name_no_case', () => {
  const { home } = homeWithPackage();
  const m = materialOf(home, { publicText: '' });
  const notes = path.join(home, 'rsd', 'lang-v2', 'test-design', '05.md');
  mkdirSync(path.dirname(notes), { recursive: true });
  writeFileSync(notes, `${NOTE_WINDOW} and more\n`);
  const withNotes = presentMaterial({ home, publicText: '' }).value;
  const ids = withNotes.ids;
  const clean = gateRepo({ 'spec-05.md': '# Gate of task 05\n\nverdict proceed, 3 reviewers\n\nthe findings are in words: the error handling is uneven, nothing more\n' });
  assert.deepEqual(leakGates({ repoRoot: clean, ids, material: withNotes.material }), [], 'a record of the verdict, the count and findings in words is green');
  const bad = gateRepo({
    'spec-04.md': 'verdict proceed\nthe case h012 is too small\nanother line\n',
    'spec-05.md': 'verdict proceed\nthe witness M99-alpha-n was weak\nthe trap trap-zeta-invented is wide\n',
    'spec-06.md': 'verdict proceed\nrecv c "an-invented-answer-for-the-package-leak-QZ41"\n',
    'spec-07.md': `verdict proceed\nwe read that ${NOTE_WINDOW} in the notes\n`,
  });
  const found = leakGates({ repoRoot: bad, ids, material: withNotes.material });
  assert.deepEqual(found.map(f => [f.file, f.line]).sort(), [['spec-04.md', 2], ['spec-05.md', 2], ['spec-05.md', 3], ['spec-06.md', 2], ['spec-07.md', 2]], 'each finding names the file and the line number');
  assert.equal(JSON.stringify(found).includes('M99-alpha-n') || JSON.stringify(found).includes('QZ41') || JSON.stringify(found).includes('h012'), false, 'never the text');
  assert.equal(m.ids.length > 0, true);
  assert.deepEqual(leakGates({ repoRoot: repoWith(), ids, material: withNotes.material }), [], 'a repository with no gate folder has nothing to scan');
  assert.equal(commitFiles.length >= 0, true);
});
