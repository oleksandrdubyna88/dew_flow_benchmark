// The free v2 rehearsal (E3b.S4, G-14; review m4): `run --what rehearse-v2` fills a v2 workspace from an A/A twin's v1 tree, launches a STUB CLI in the real box behind the real broker
// (a fake token, a loopback stub upstream: nothing is paid and no real token is read), runs the real scorer at phase v2, and computes G-14 from the workspace, the record and the score.
import './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { cpSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../bare-runner.mjs';
import { EXIT } from '../lib/bare-series.mjs';
import { g14Verdict, rehearsalRefusal, rehearseV2, twinOf } from '../lib/rehearsal.mjs';
import { openSeries, planSession, readRunMap, transition, writeRunMap } from '../lib/run-map.mjs';
import { fillV1, fillV2 } from '../lib/workspace.mjs';
import { sha256Of } from '../lib/inputs.mjs';
import { write } from './bare-ctx.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

// ---- G-14, computed ---------------------------------------------------------------------------------------------------------------

function fakeCache() {
  const root = tmpDir('cache');
  const files = { 'upstream/SPEC-v1.txt': 's1\n', 'upstream/SPEC-v2.txt': 's2\n', 'test-v1.sh': 't1\n', 'test-v2.sh': 't2\n' };
  for (const [p, t] of Object.entries(files)) write(path.join(root, p), t);
  return { root, entries: Object.entries(files).map(([p, t]) => ({ path: p, sha256: sha256Of(Buffer.from(t)) })) };
}
const GOOD_RECORD = { run: { a: { known: true, value: 1 } }, cost: { b: { known: false, why: 'x' } }, time: {}, quality: {}, process: { c: 'n/a (bare)' }, loc: { known: true, value: 1 }, egress: { known: true, value: 1 }, packages: { known: true, value: 1 }, workspace: { known: true, value: 1 } };
const GOOD_SCORE = { build: { ok: true }, visible: [{ script: 'test-v2.sh', passed: 30, failed: 0 }, { script: 'test-v1.sh', passed: 11, failed: 0 }], heldout: { known: true }, upstream: [] };

function trees(stack = 'csharp') {
  const cache = fakeCache();
  const twin = tmpDir('twin');
  fillV1({ inputsRoot: cache.root, hashes: cache.entries, work: twin });
  for (const [p, t] of Object.entries({ 'Program.cs': 'class P {}', 'bin/Debug/x.dll': 'dll', 'obj/y.json': '{}', 'node_modules/z/i.js': '', 'src/obj.ts': 'x', 'sub/obj/n.txt': 'n' })) write(path.join(twin, p), t);
  const v2 = tmpDir('v2');
  const filled = fillV2({ inputsRoot: cache.root, hashes: cache.entries, v1Work: twin, work: v2, stack });
  assert.equal(filled.ok, true, filled.reason);
  return { cache, twin, v2, stack };
}
const verdict = (t, over = {}) => g14Verdict({ stack: t.stack, twinWork: t.twin, v2Work: t.v2, hashes: t.cache.entries, record: GOOD_RECORD, score: GOOD_SCORE, ...over });
const failed = v => v.checks.filter(c => !c.ok).map(c => c.id);

test('G14_holds_for_a_v2_workspace_that_is_the_twins_tree_minus_the_stacks_exclusion_plus_the_v2_inputs', () => {
  for (const stack of ['csharp', 'node']) {
    const v = verdict(trees(stack));
    assert.equal(v.holds, true, JSON.stringify(v.checks.filter(c => !c.ok)));
    assert.deepEqual(v.checks.map(c => c.id), ['workspace', 'record', 'score']);
  }
});

test('G14_is_refuted_by_a_wrong_workspace_an_incomplete_record_or_an_unknown_score_step', () => {
  const keptBuild = trees('csharp');
  write(path.join(keptBuild.v2, 'bin/Debug/x.dll'), 'dll');
  assert.deepEqual(failed(verdict(keptBuild)), ['workspace'], 'a C# v2 workspace that kept bin/');
  const lostSource = trees('node');
  write(path.join(lostSource.twin, 'bin/minigit'), '#!/usr/bin/env node');
  assert.deepEqual(failed(verdict(lostSource)), ['workspace'], 'a Node bin/ is source and must be in the v2 tree: the v2 copy was made before it existed');
  const noInput = trees('csharp');
  writeFileSync(path.join(noInput.v2, 'test-v2.sh'), 'tampered\n');
  assert.deepEqual(failed(verdict(noInput)), ['workspace'], 'a pinned v2 input that is not the pinned bytes');
  const edited = trees('csharp');
  writeFileSync(path.join(edited.v2, 'Program.cs'), 'class Q {}');
  assert.deepEqual(failed(verdict(edited)), ['workspace'], 'a twin file that was not copied byte for byte');
  const ok = trees('csharp');
  assert.deepEqual(failed(verdict(ok, { record: { ...GOOD_RECORD, run: { a: null } } })), ['record']);
  assert.deepEqual(failed(verdict(ok, { record: { ...GOOD_RECORD, cost: { b: { known: false } } } })), ['record'], 'an unknown with no reason');
  assert.deepEqual(failed(verdict(ok, { score: { ...GOOD_SCORE, heldout: { known: false, why: 'no key' } } })), ['score']);
  assert.deepEqual(failed(verdict(ok, { score: { ...GOOD_SCORE, visible: [GOOD_SCORE.visible[1]] } })), ['score'], 'the v2 script was not scored');
  assert.deepEqual(failed(verdict(ok, { score: null })), ['score']);
});

// ---- the command ------------------------------------------------------------------------------------------------------------------

function series({ twin = false } = {}) {
  const dir = path.join(tmpDir('rsd'), 's-20261030-ab12');
  mkdirSync(path.join(dir, 's'), { recursive: true });
  let map = openSeries({
    seriesId: 's-20261030-ab12', seedSha256: 'b'.repeat(64), harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030', preregCommit: 'c'.repeat(40), pins: { claude: '2.1.310' },
    stacksSha256: 'd', pricesFile: 'prices-2026-10-29.json', pricesSha256: 'e', snapshots: { csharp: 'f', node: '1' }, snapshotDirs: { csharp: '/nowhere/cs', node: '/nowhere/nd' },
    claude: { path: '/nowhere/claude-2.1.310', sha256: 'x', version: '2.1.310' }, caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 }, bareBudgetUsd: 40, ceilingUsd: 500, spin: { medianMs: 20 },
  });
  if (twin) {
    map = planSession(map, { rid: 'aaaaaa', pair: 0, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'aa', label: 'paired', mask: '0-5', twin: 'a' }).value;
    for (const to of ['launching', 'ran', 'scored']) map = transition(map, 'aaaaaa', to, to === 'ran' ? { class: 'completed', data: true } : {}).value;
  }
  writeRunMap(path.join(dir, 'run-map.json'), map);
  return { dir, map };
}
function inputsDir() {
  const dir = tmpDir('inputs');
  for (const f of ['stacks.json', 'prices-2026-10-29.json', 'crosscheck-policy.json', 'quiet.json', 'package-flags.json']) cpSync(path.join(spikeDir, 'inputs', f), path.join(dir, f));
  writeFileSync(path.join(dir, 'cache-snapshots.json'), JSON.stringify({ csharp: { treeSha256: 'f'.repeat(64), parts: { nuget: 'a' }, packages: [] }, node: { treeSha256: '1'.repeat(64), parts: { npm: 'b' }, packages: [] } }));
  return dir;
}
const run = async argv => { const lines = []; const code = await main(argv, l => lines.push(l)); return { code, out: lines.join('\n') }; };

test('A_rehearsal_needs_an_A_A_twin_and_names_the_one_it_uses', async () => {
  assert.match(rehearsalRefusal(series().map), /needs an A\/A twin/);
  const withTwin = series({ twin: true });
  assert.equal(rehearsalRefusal(withTwin.map), null);
  assert.equal(twinOf(withTwin.map).rid, 'aaaaaa');
  const none = await run(['run', '--series', series().dir, '--what', 'rehearse-v2', '--dry-run', '--inputs-dir', inputsDir()]);
  assert.equal(none.code, 4);
  assert.match(none.out, /needs an A\/A twin/);
  const s = series({ twin: true });
  const before = readdirSync(s.dir).sort();
  const dry = await run(['run', '--series', s.dir, '--what', 'rehearse-v2', '--dry-run', '--inputs-dir', inputsDir()]);
  assert.equal(dry.code, 0, dry.out);
  assert.match(dry.out, /rehearse-v2: would rehearse the v2 path on the tree of twin aaaaaa/);
  assert.match(dry.out, /stub CLI .* nothing is paid/);
  assert.deepEqual(readdirSync(s.dir).sort(), before, 'a dry run writes nothing');
});

test('A_rehearsal_that_stopped_at_the_gates_leaves_one_planned_session_and_the_next_run_reuses_it', async () => {
  const s = series({ twin: true });
  const ctx = { seriesDir: s.dir, sessionsRoot: path.join(s.dir, 's'), runMapFile: path.join(s.dir, 'run-map.json'), ledgerFile: path.join(s.dir, 'ledger.ndjson'), kind: 'aa', pair: 0, home: tmpDir('home'), caps: { capUsd: 6, wallMs: 1, stallMs: 1 }, harnessIdentity: 'a'.repeat(64) };
  const deps = { guard: async () => ({ ok: false, reason: 'the tag is not on origin' }), retireStale: async () => {}, preflight: async () => ({ ok: true }), now: Date.now, phase: {} };
  const rehearse = map => rehearseV2({ ctx, deps, map, say: () => {} });
  const first = await rehearse(s.map);
  assert.equal(first.exit, EXIT.config, first.message);
  const planned = () => Object.values(readRunMap(ctx.runMapFile).value.sessions).filter(x => x.kind === 'rehearsal');
  assert.equal(planned().length, 1);
  assert.equal(planned()[0].status, 'planned');
  const firstRid = planned()[0].rid;
  const again = await rehearse(readRunMap(ctx.runMapFile).value);
  assert.equal(again.exit, EXIT.config, again.message);
  assert.equal(planned().length, 1, 'the still-planned session is reused: no second rid, no leftover');
  assert.equal(planned()[0].rid, firstRid, 'the same rid');
});
