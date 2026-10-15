import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { gitDirFor } from '../hooks/audit.mjs';
import { ARM_SHAS, CONV_CLONE } from '../lib/consts.mjs';
import { buildChildEnv } from '../lib/env.mjs';
import { outcomeOfCell, reportFindings } from '../lib/findings.mjs';
import { parseJunit } from '../lib/junit.mjs';
import { assertArmsDifferOnlyInPolicy, assertPolicyDelta, guardEntriesOf, mapDelta, parseLsTree, withGuardEntries } from '../lib/mount.mjs';
import { ownSuiteVsReference } from '../lib/ownsuite.mjs';
import { agentTimeline, inseparableGroups } from '../lib/parallel.mjs';
import { compareProbes, probeGate, rerunTargets, settleFailures } from '../lib/probe.mjs';
import { readerId, readingKeyFile, scrubReaderText, shuffleSeeded } from '../lib/reader.mjs';
import { assertRemovable, assertRunsRoot, depthOf, removeUnder } from '../lib/safe-rm.mjs';
import { shedCell } from '../lib/shed.mjs';
import { spendDecision } from '../lib/spend.mjs';
import { loadStory, parseStory } from '../lib/story.mjs';
import { extractConventionsInto, mountFileMap } from '../lib/tree.mjs';
import { gitlinkAt } from '../lib/git.mjs';
import { CostMeter, OVER_CAP_FACTOR, outcomeOf, splitLines } from '../lib/watchdog.mjs';
import { buildArmMount } from '../lib/tree.mjs';
import { assembleReport } from '../report.mjs';
import { runAll } from '../pilot.mjs';

const POLICY = ['common/subagent-models.md', 'settings/hooks/subagent-model.mjs', 'settings/settings.json'];

// ---- the arms' mounts -----------------------------------------------------------------------------------------------

test('mount delta: arm A is the base byte for byte, arm B differs in exactly the declared policy files', () => {
  const base = parseLsTree(['100644 blob ' + 'a'.repeat(40) + '\tcommon/subagent-models.md', '100644 blob ' + 'b'.repeat(40) + '\tcommon/planning-docs.md', '040000 tree ' + 'c'.repeat(40) + '\tcommon', '160000 commit ' + 'd'.repeat(40) + '\tsub'].join('\n'));
  assert.deepEqual([...base.keys()], ['common/subagent-models.md', 'common/planning-docs.md']);
  assert.deepEqual(assertPolicyDelta('A', [], POLICY), []);
  assert.deepEqual(assertPolicyDelta('B', [...POLICY].reverse(), POLICY), [...POLICY].reverse());
  assert.throws(() => assertPolicyDelta('A', ['common/planning-docs.md'], POLICY), /unexpected: \[common\/planning-docs\.md\]/);
  assert.throws(() => assertPolicyDelta('B', [...POLICY, 'common/planning-docs.md'], POLICY), /unexpected: \[common\/planning-docs\.md\]/);
  assert.throws(() => assertPolicyDelta('B', POLICY.slice(1), POLICY), /missing: \[common\/subagent-models\.md\]/);
  const a = new Map([['x', '1'], ['y', '1']]);
  assert.deepEqual(mapDelta(a, new Map([['x', '1'], ['y', '2'], ['z', '1']])), ['y', 'z']);
  assert.deepEqual(assertArmsDifferOnlyInPolicy(new Map([['p', '1']]), new Map([['p', '2']]), ['p']), ['p']);
  assert.throws(() => assertArmsDifferOnlyInPolicy(new Map([['p', '1'], ['q', '1']]), new Map([['p', '2'], ['q', '2']]), ['p']), /differ in \[p, q\]/);
});

test('the guard entries are lifted from the release settings; the user-guard installer is not', () => {
  const release = { hooks: { SessionStart: [{ hooks: [{ command: 'node', args: ['load-instructions.mjs'] }, { command: 'node', args: ['ensure-user-guard.mjs'] }] }], PreToolUse: [{ matcher: 'Bash', hooks: [{ command: 'node', args: ['build-flags.mjs'] }] }, { matcher: 'Agent|Task', hooks: [{ command: 'node', args: ['${CLAUDE_PROJECT_DIR}/.claude/hooks/subagent-model.mjs'] }] }] } };
  assert.deepEqual(Object.keys(guardEntriesOf(release)), ['PreToolUse']);
  const base = { permissions: { defaultMode: 'x' }, hooks: { SessionStart: [{ hooks: [{ command: 'node', args: ['load-instructions.mjs'] }] }], PreToolUse: [{ matcher: 'Bash', hooks: [{ command: 'node', args: ['build-flags.mjs'] }] }] } };
  const next = withGuardEntries(base, release);
  assert.deepEqual(next.hooks.PreToolUse.map(e => e.matcher), ['Bash', 'Agent|Task']);
  assert.deepEqual(next.hooks.SessionStart, base.hooks.SessionStart);
  assert.equal(JSON.stringify(next).includes('ensure-user-guard'), false);
  assert.equal(base.hooks.PreToolUse.length, 1, 'the base is not mutated');
  assert.deepEqual(withGuardEntries(base, { hooks: {} }), base);
});

test('REAL conventions: both arms are built from the pin, differ in exactly the declared files, in ONE identical-looking commit', { skip: !fs.existsSync(path.join(CONV_CLONE, '.git')) }, async () => {
  const story = loadStory('coai-751');
  assert.deepEqual([...story.policyFiles].sort(), POLICY);
  const pin = await gitlinkAt(story.repoClone, story.P, story.mount);
  const dirs = {};
  const built = {};
  try {
    for (const arm of ['A', 'B']) {
      dirs[arm] = fs.mkdtempSync(path.join(os.tmpdir(), `pab-mount-${arm}-`));
      await extractConventionsInto(dirs[arm], story.mount, pin);
      built[arm] = await buildArmMount(dirs[arm], story, arm, pin, ARM_SHAS);
    }
    assert.deepEqual(built.A.delta, []);
    assert.deepEqual([...built.B.delta].sort(), POLICY);
    const mount = arm => path.join(dirs[arm], ...story.mount.split('/'));
    const git = (arm, ...args) => execFileSync('git', args, { cwd: mount(arm), encoding: 'utf8', env: { ...process.env, GIT_CONFIG_NOSYSTEM: '1' } }).trim();
    for (const arm of ['A', 'B']) {
      assert.equal(git(arm, 'rev-list', '--count', 'HEAD'), '1', 'one commit, no history');
      assert.equal(git(arm, 'tag', '-l'), '');
      assert.equal(git(arm, 'log', '-1', '--format=%an|%ae|%aI|%s'), 'bench|bench@invalid|2000-01-01T00:00:00Z|conventions');
      assert.equal(git(arm, 'status', '--porcelain'), '');
    }
    assert.deepEqual(assertArmsDifferOnlyInPolicy(await mountFileMap(mount('A')), await mountFileMap(mount('B')), story.policyFiles).sort(), POLICY);
    const read = (arm, f) => fs.readFileSync(path.join(mount(arm), f), 'utf8');
    assert.equal(read('A', 'common/planning-docs.md'), read('B', 'common/planning-docs.md'), 'the second loaded rule is the same in both arms');
    for (const arm of ['A', 'B']) {
      assert.ok(!fs.existsSync(path.join(mount(arm), 'settings/hooks/ensure-user-guard.mjs')));
      assert.ok(!fs.existsSync(path.join(mount(arm), 'settings/hooks/lib/user-guard-install.mjs')));
      assert.equal(read(arm, 'settings/settings.json').includes('ensure-user-guard'), false);
    }
    assert.ok(read('B', 'settings/settings.json').includes('subagent-model.mjs'));
    assert.ok(!read('A', 'settings/settings.json').includes('subagent-model.mjs'));
    assert.ok(fs.existsSync(path.join(mount('B'), 'settings/hooks/subagent-model.mjs')) && !fs.existsSync(path.join(mount('A'), 'settings/hooks/subagent-model.mjs')));
  } finally {
    for (const d of Object.values(dirs)) fs.rmSync(d, { recursive: true, force: true });
  }
});

test('a resolver story must declare its policy files', () => {
  const ok = { repoClone: 'x', P: 'a'.repeat(40), S: 'b'.repeat(40), mount: 'm', layout: 'resolver', planFile: 'p', inputs: 'i', srcDir: 's', struckFrom: 'statement.md', defaultRunsRoot: 'r', prompt: 'prompt.md', canarySlug: 'slug' };
  assert.throws(() => parseStory('t', ok, 'D:/spike'), /declares its policyFiles/);
  assert.deepEqual(parseStory('t', { ...ok, policyFiles: ['a'] }, 'D:/spike').policyFiles, ['a']);
  assert.deepEqual(loadStory().policyFiles, []);
});

// ---- the probe ------------------------------------------------------------------------------------------------------

test('the parity probe: same failing set and the declared mount delta pass; anything else says why', () => {
  const mapA = new Map([['p', '1'], ['q', '1']]);
  const mapB = new Map([['p', '2'], ['q', '1']]);
  const suite = (failed, tests = 100) => ({ tests, failedCases: failed });
  const ok = compareProbes({ suiteA: suite(['t::a', 't::b']), suiteB: suite(['t::b', 't::a']), mapA, mapB, policyFiles: ['p'] });
  assert.equal(ok.pass, true);
  assert.deepEqual(ok.mountDelta, ['p']);
  const split = compareProbes({ suiteA: suite(['t::a']), suiteB: suite(['t::a', 't::z']), mapA, mapB, policyFiles: ['p'] });
  assert.equal(split.pass, false);
  assert.match(split.reasons[0], /only in A \[\], only in B \[t::z\]/);
  assert.match(compareProbes({ suiteA: suite([], 100), suiteB: suite([], 99), mapA, mapB, policyFiles: ['p'] }).reasons.join(), /100 \(A\) and 99 \(B\)/);
  assert.match(compareProbes({ suiteA: suite([]), suiteB: suite([]), mapA, mapB: new Map([['p', '2'], ['q', '9']]), policyFiles: ['p'] }).reasons.join(), /differ in \[p, q\]/);
  assert.match(compareProbes({ suiteA: { tests: 0 }, suiteB: suite([]), mapA, mapB, policyFiles: ['p'] }).reasons.join(), /arm A: the suite produced no report/);
  assert.equal(probeGate(null, ['p']).ok, false);
  assert.equal(probeGate({ pass: false, reasons: ['x'], policyFiles: ['p'] }, ['p']).ok, false);
  assert.equal(probeGate({ pass: true, policyFiles: ['q'] }, ['p']).ok, false);
  assert.equal(probeGate({ pass: true, policyFiles: ['p'] }, ['p']).ok, true);
});

test('a failure that does not repeat when its file runs alone is flaky and does not split the arms', () => {
  assert.deepEqual(rerunTargets(['test/a.test.js::::x', 'test/a.test.js::s::y', 'test/b.test.js::::z']), ['out/test/a.test.js', 'out/test/b.test.js']);
  const s = settleFailures(['test/a.test.js::::x', 'test/b.test.js::::z'], ['test/b.test.js::::z']);
  assert.deepEqual(s, { failedCases: ['test/b.test.js::::z'], flaky: ['test/a.test.js::::x'] });
  assert.deepEqual(settleFailures([], []), { failedCases: [], flaky: [] });
});

// ---- blinding ---------------------------------------------------------------------------------------------------------

test('the child environment has the SAME variable names in both arms, whatever the parent carries', () => {
  const parent = { Path: 'C:\\a', SUBAGENT_MODEL_GUARD: 'off', HOME: 'h', CLAUDECODE: '1' };
  const names = arm => Object.keys(buildChildEnv(parent, { arm, emptyGitConfig: 'E', hasGh: () => false })).sort();
  assert.deepEqual(names('A'), names('B'));
  assert.ok(!names('A').includes('SUBAGENT_MODEL_GUARD'));
});

test('the reading key is outside the runs root; the shuffle is a reproducible permutation that reaches every order', () => {
  assert.ok(!readingKeyFile('coai-751', 'D:/spike').replace(/\\/g, '/').includes('/_s2'));
  assert.match(readingKeyFile('coai-751', 'D:/spike').replace(/\\/g, '/'), /D:\/spike\/reading-key\.coai-751\.json$/);
  const items = ['c1', 'c2', 'c3', 'c4', 'reference'];
  assert.deepEqual(shuffleSeeded(items, 's'), shuffleSeeded(items, 's'));
  assert.deepEqual([...shuffleSeeded(items, 's')].sort(), [...items].sort());
  assert.deepEqual(items, ['c1', 'c2', 'c3', 'c4', 'reference'], 'the input is not mutated');
  const first = new Map();
  for (let i = 0; i < 1200; i++) { const f = shuffleSeeded(['a', 'b', 'c'], `seed-${i}`)[0]; first.set(f, (first.get(f) ?? 0) + 1); }
  for (const n of first.values()) assert.ok(n > 330 && n < 470, `each item leads about a third of the time, got ${n}`);
  assert.equal(readerId('s', 'c1'), readerId('s', 'c1'));
  assert.notEqual(readerId('s', 'c1'), readerId('s', 'c2'));
});

test('the reader scrub removes model names, labels and the policy\'s own wording from a summary', () => {
  const r = scrubReaderText('c2 (Opus) delegated T1 to a Sonnet coder; the edit-bound denied 1 file / 50 lines; see subagent-models.md and the PreToolUse hook.', { policy: true });
  assert.equal(/opus|sonnet|c2|delegat|edit.bound|subagent-model|PreToolUse|50 lines/i.test(r.text), false, r.text);
  assert.ok(r.counts.policy >= 4 && r.counts.model === 2 && r.counts.label === 1);
  assert.ok(scrubReaderText('we delegate to a helper', { policy: false }).text.includes('delegate'), 'code diffs keep their words');
});

// ---- accounting ---------------------------------------------------------------------------------------------------------

const span = (id, model, from, to) => [
  { type: 'assistant', timestamp: `2026-10-15T10:00:${String(from).padStart(2, '0')}Z`, message: { id: `m-${id}`, content: [{ type: 'tool_use', id, name: 'Agent', input: { model } }] } },
  { type: 'user', timestamp: `2026-10-15T10:00:${String(to).padStart(2, '0')}Z`, tool_use_result: { agentId: `g-${id}`, resolvedModel: `claude-${model}-x` }, message: { content: [{ type: 'tool_result', tool_use_id: id, content: 'ok' }] } },
];

test('inseparable cost groups are connected components: A overlaps B, B overlaps C -> one group of three', () => {
  const events = [...span('a', 'sonnet', 0, 20), ...span('b', 'sonnet', 10, 40), ...span('c', 'sonnet', 30, 50), ...span('d', 'sonnet', 55, 58), ...span('e', 'opus', 5, 45)];
  const groups = inseparableGroups(agentTimeline(events));
  assert.deepEqual(groups.get('a'), ['a', 'b', 'c']);
  assert.deepEqual(groups.get('c'), ['a', 'b', 'c']);
  assert.equal(groups.has('d'), false, 'a later child on the same model is separable');
  assert.equal(groups.has('e'), false, 'a different model overlapping is separable');
});

test('junit cases are keyed by file and suite: the same name in two files is two tests', () => {
  const xml = `<testsuites>
	<testcase name="same" time="0" classname="test" file="C:\\x\\out\\test\\a.test.js"/>
	<testcase name="same" time="0" classname="test" file="C:\\x\\out\\test\\b.test.js" failure="boom > it">
		<failure type="testCodeFailure" message="boom"/>
	</testcase>
	<testsuite name="outer" tests="1">
		<testsuite name="inner" tests="1">
			<testcase name="deep" time="0" classname="test" file="C:\\x\\out\\test\\b.test.js"/>
		</testsuite>
	</testsuite>
</testsuites>`;
  const p = parseJunit(xml);
  assert.deepEqual(p.cases.map(c => c.key), ['test/a.test.js::::same', 'test/b.test.js::::same', 'test/b.test.js::outer > inner::deep']);
  assert.deepEqual(p.cases.map(c => c.status), ['pass', 'fail', 'pass']);
  const ref = { tests: 3, failedCases: ['test/b.test.js::::same'] };
  assert.equal(ownSuiteVsReference({ tests: 3, failedCases: ['test/a.test.js::::same'] }, ref).verdict, 'fail', 'a failure of the same name in ANOTHER file is new');
  assert.equal(ownSuiteVsReference({ tests: 3, failedCases: ['test/b.test.js::::same'] }, ref).verdict, 'pass');
});

test('blinding tripwires: a guard denial in an arm A cell, or a plugin in the session, is a finding', () => {
  const cells = [
    { cell: 'c1', arm: 'A', summary: { denials: { modelGuard: 2 }, init: { plugins: [] } } },
    { cell: 'c2', arm: 'B', summary: { denials: { modelGuard: 2 }, init: { plugins: ['feature-dev'] } } },
  ];
  assert.deepEqual(reportFindings({ cells }).map(f => `${f.kind}:${f.cell}`), ['guard-denial-in-arm-A:c1', 'plugins-in-session:c2']);
});

test('fewer tests than the reference is a finding of its own', () => {
  const r = ownSuiteVsReference({ tests: 90, failedCases: [] }, { tests: 100, failedCases: [] });
  assert.equal(r.verdict, 'pass');
  assert.equal(r.fewerTestsThanReference, true);
  assert.deepEqual(reportFindings({ cells: [{ cell: 'c1', ownSuiteVsReference: r }] }), [{ kind: 'fewer-tests-than-reference', cell: 'c1', candidateTests: 90, referenceTests: 100 }]);
});

test('the audit hook\'s git-dir parser reads "gitdir:" with or without a space, and a path that begins with s', () => {
  const at = text => gitDirFor('D:/t', { statSync: () => ({ isFile: () => true }), readFileSync: () => text }).replace(/\\/g, '/');
  assert.match(at('gitdir:sub/real\n'), /D:\/t\/sub\/real$/);
  assert.match(at('gitdir: ../real\n'), /D:\/real$/);
});

// ---- the spend stop, the watchdog, the outcome ---------------------------------------------------------------------------

test('stop-after is a ceiling: a cell starts only when spent + cap stays within it', () => {
  assert.equal(spendDecision(70, 100, 30).stop, false);
  assert.equal(spendDecision(70.01, 100, 30).stop, true);
  assert.match(spendDecision(75, 100, 30).line, /spent \$75 \+ cap \$30 would pass --stop-after \$100/);
});

const plan = [{ cell: 'c1', arm: 'A' }, { cell: 'c2', arm: 'B' }, { cell: 'c3', arm: 'B' }];
const recorder = () => { const calls = { stop: [], crash: [] }; return { calls, record: { stop: s => calls.stop.push(s), crash: (c, m) => calls.crash.push([c, m]) } }; };

test('runAll stops BEFORE the first cell when the ceiling would be passed, and records why', async () => {
  const { calls, record } = recorder();
  const ran = [];
  const ended = await runAll(plan, { cap: 30, stopAfter: 100, spent: () => 80, runCell: async p => ran.push(p.cell), record });
  assert.deepEqual(ran, []);
  assert.equal(ended.stoppedBefore, 'c1');
  assert.equal(calls.stop.length, 1);
  assert.equal(calls.stop[0].before, 'c1');
  assert.match(calls.stop[0].line, /STOP/);
});

test('runAll stops between cells as the spend grows, not before', async () => {
  const { calls, record } = recorder();
  const ran = [];
  let spent = 0;
  const ended = await runAll(plan, { cap: 30, stopAfter: 100, spent: () => spent, runCell: async p => { ran.push(p.cell); spent += 40; }, record });
  assert.deepEqual(ran, ['c1', 'c2']);
  assert.equal(ended.stoppedBefore, 'c3');
  assert.equal(calls.stop[0].before, 'c3');
});

test('a throwing step is recorded as a crash, ends the loop, and does not throw over the series', async () => {
  const { calls, record } = recorder();
  const ran = [];
  const logs = [];
  const ended = await runAll(plan, { cap: 30, stopAfter: 1000, spent: () => 0, log: l => logs.push(l), record, runCell: async p => { ran.push(p.cell); if (p.cell === 'c2') throw new Error('run-cell.mjs exited 1'); } });
  assert.deepEqual(ran, ['c1', 'c2']);
  assert.deepEqual(ended, { stoppedBefore: '', crashedAt: 'c2' });
  assert.deepEqual(calls.crash, [['c2', 'run-cell.mjs exited 1']]);
  assert.ok(logs.some(l => /CRASHED/.test(l)));
});

test('the cost meter prices each message once (the stream repeats it per block) and trips at cap x 1.1', () => {
  const m = new CostMeter();
  const msg = (id, out) => JSON.stringify({ type: 'assistant', message: { id, model: 'claude-opus-x', usage: { input_tokens: 0, output_tokens: out } } });
  m.feed(msg('a', 100_000)); // $7.50 at the fallback rate
  m.feed(msg('a', 100_000));
  assert.equal(m.usd, 7.5);
  m.feed('not json');
  m.feed(JSON.stringify({ type: 'user' }));
  assert.equal(m.usd, 7.5);
  assert.equal(m.overCap(7.5 / OVER_CAP_FACTOR + 0.01), false);
  assert.equal(m.overCap(6.8), true);
  assert.deepEqual(splitLines('par', 'tial\nwhole\nnext'), { lines: ['partial', 'whole'], carry: 'next' });
});

test('every cell ends with an explicit outcome; a kill outranks the exit code it caused', () => {
  assert.equal(outcomeOf({ exitCode: 0 }), 'completed');
  assert.equal(outcomeOf({ exitCode: 1 }), 'crashed');
  assert.equal(outcomeOf({ exitCode: 1, timedOut: true }), 'killed: wall');
  assert.equal(outcomeOf({ exitCode: 1, overCap: true, timedOut: true }), 'killed: over cap');
  assert.equal(outcomeOf({ exitCode: 0, threw: true }), 'crashed');
});

test('the report carries the stop record, each cell\'s outcome, a crashed cell with no summary, and the estimated costs', () => {
  const files = {
    'r/c1/summary.json': { usd: 4, costSource: 'envelope', run: { outcome: 'completed' } },
    'r/c2/summary.json': { usd: 36.5, costSource: 'messages', outcome: 'killed: over cap' },
    'r/c1/score.json': { ownSuite: { tests: 100, failedCases: [] } }, 'r/c2/score.json': { ownSuite: { tests: 80, failedCases: ['x'] } },
    'r/reference/score.json': { ownSuite: { tests: 100, failedCases: [] } },
  };
  const read = file => files[file.replace(/\\/g, '/')] ?? null;
  const runMap = { seed: 's', stop: { before: 'c4', line: 'STOP: spent' }, cells: { c1: { arm: 'A' }, c2: { arm: 'B' }, c3: { arm: 'A', outcome: 'crashed', crash: 'run-cell.mjs exited 1' } } };
  const report = assembleReport({ story: loadStory(), runMap, root: 'r', read, now: 'T' });
  assert.deepEqual(report.stop, runMap.stop);
  assert.deepEqual(report.cells.map(c => [c.cell, c.outcome]), [['c1', 'completed'], ['c2', 'killed: over cap'], ['c3', 'crashed'], ['reference', null]]);
  const kinds = report.findings.map(f => `${f.kind}:${f.cell ?? f.before}`);
  assert.deepEqual(kinds.sort(), ['cell-outcome:c2', 'cell-outcome:c3', 'estimated-cost:c2', 'fewer-tests-than-reference:c2', 'new-test-failures:c2', 'series-stopped:c4'].sort());
  assert.equal(outcomeOfCell({ outcome: 'crashed' }, { outcome: 'completed' }), 'crashed');
  assert.equal(outcomeOfCell(undefined, undefined), null);
});

// ---- hygiene ----------------------------------------------------------------------------------------------------------------

test('a runs root must be at least two folders deep', () => {
  assert.equal(depthOf('D:/'), 0);
  assert.equal(depthOf('D:/rsd'), 1);
  assert.equal(depthOf('D:/rsd/_s2'), 2);
  assert.throws(() => assertRunsRoot('D:/'), /two folders deep/);
  assert.throws(() => assertRunsRoot('D:/rsd'), /two folders deep/);
  assert.throws(() => assertRunsRoot('/'), /two folders deep/);
  assert.doesNotThrow(() => assertRunsRoot('D:/rsd/_s2'));
});

/** A junction (or directory symlink where junctions do not exist) - null when this machine will not make one. */
function link(target, at) {
  try { fs.symlinkSync(target, at, 'junction'); return true; } catch { return false; }
}

test('shedding refuses a junction at the target and leaves what it points to untouched', t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-junc-'));
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-outside-'));
  try {
    fs.writeFileSync(path.join(outside, 'precious.txt'), 'keep');
    fs.mkdirSync(path.join(root, 'c1'), { recursive: true });
    if (!link(outside, path.join(root, 'c1', 'score'))) return t.skip('cannot create a junction here');
    assert.throws(() => assertRemovable(path.join(root, 'c1', 'score'), root), /link or junction/);
    const { removed, refused } = shedCell(root, 'c1', { srcDir: 'src_vs_code' });
    assert.deepEqual(removed, []);
    assert.equal(refused.length, 1);
    assert.match(refused[0].reason, /link or junction/);
    assert.equal(fs.readFileSync(path.join(outside, 'precious.txt'), 'utf8'), 'keep');
  } finally {
    fs.rmSync(path.join(root, 'c1', 'score'), { force: true });
    fs.rmSync(root, { recursive: true, force: true });
    fs.rmSync(outside, { recursive: true, force: true });
  }
});

test('shedding refuses a target whose PARENT is a junction that leaves the runs root', t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-junc2-'));
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-outside2-'));
  try {
    fs.mkdirSync(path.join(outside, 'src_vs_code', 'node_modules', 'pkg'), { recursive: true });
    fs.mkdirSync(path.join(root, 'c1'), { recursive: true });
    if (!link(outside, path.join(root, 'c1', 'tree'))) return t.skip('cannot create a junction here');
    const { removed, refused } = shedCell(root, 'c1', { srcDir: 'src_vs_code' });
    assert.deepEqual(removed, []);
    assert.ok(refused.some(r => /outside/.test(r.reason)), JSON.stringify(refused));
    assert.ok(fs.existsSync(path.join(outside, 'src_vs_code', 'node_modules', 'pkg')));
  } finally {
    fs.rmSync(path.join(root, 'c1', 'tree'), { force: true });
    fs.rmSync(root, { recursive: true, force: true });
    fs.rmSync(outside, { recursive: true, force: true });
  }
});

test('shedding a real directory still works, and refuses a shallow root outright', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-real-'));
  try {
    fs.mkdirSync(path.join(root, 'c1', 'score', '_own'), { recursive: true });
    assert.equal(removeUnder(path.join(root, 'c1', 'score'), root), true);
    assert.equal(removeUnder(path.join(root, 'c1', 'score'), root), false);
    assert.throws(() => shedCell('D:/', 'c1', {}), /two folders deep/);
    fs.writeFileSync(path.join(root, 'c1', 'file'), 'x');
    assert.throws(() => assertRemovable(path.join(root, 'c1', 'file'), root), /not a directory/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
