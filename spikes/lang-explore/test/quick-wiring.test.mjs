// The wiring of the quick seams (plan item 2): `lostCwdLow` and `skipBigOutside` reach the collect from the run context (`run-ctx.mjs`), from the settle of a live session (`bare-settle.mjs`) and from the re-collection of a
// rescore (`bare-records-io.mjs`), and the scan bound uses the REAL `isEvidence`. Removing any link of the chain goes red. Fakes for the collect; a temp session folder for the scan; nothing starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { collectAgain } from '../../minigit-bench/lib/bare-records-io.mjs';
import { OUTSIDE_BUDGET, contaminationPart, isEvidence, scanPart } from '../../minigit-bench/lib/bare-collect.mjs';
import { collectOne } from '../../minigit-bench/lib/bare-settle.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { contextOf } from '../lib/run-ctx.mjs';

const EXPLORE = loadExplore().value;
const snap = { dir: '/snap', parts: ['x'], record: {} };
const world = quick => ({ explore: EXPLORE, paths: { nodeDir: '/n', tsDir: '/t' }, snapshots: { csharp: snap, node: snap }, quick });
const contextFor = quick => contextOf({
  world: world(quick), calibration: {}, series: {}, seriesDir: tmpDir('wiring-series'), kind: 'pilot', pair: 1, token: 'fake', claudeBinary: '/c', fill: () => {}, abort: { reason: null, exit: 130 }, home: '/h',
  policy: { table: {}, policy: {} }, identity: 'id', prompts: {},
});

test('The_run_context_of_a_quick_world_carries_the_lost_directory_seam_and_an_olympiad_world_does_not', () => {
  assert.equal(contextFor(true).lostCwdLow, true);
  assert.equal('lostCwdLow' in contextFor(undefined), false);
  assert.equal('lostCwdLow' in contextFor(false), false);
});

const entry = { rid: 'a1', dummy: 'd', pair: 1 };
const layout = { rid: 'a1', dir: '/s/a1' };
const baseCtx = { token: 't', handVerdictsFor: () => [], table: {}, policy: {}, home: '/h', abortReason: () => null };

/** What `collect` was called with. */
const spy = () => { const calls = []; return { calls, collect: args => { calls.push(args); return ok({}); } }; };

test('A_live_sessions_collect_gets_both_seams_from_a_quick_context_and_neither_from_another', () => {
  for (const [name, ctx, expected] of [['quick', { ...baseCtx, lostCwdLow: true }, { lostCwdLow: true, skipBigOutside: true }], ['other', baseCtx, { lostCwdLow: undefined, skipBigOutside: undefined }]]) {
    const { calls, collect } = spy();
    const s = { layout, plan: { token: 't', dummy: 'd' }, launched: null, canary: { tokens: [] }, rid: 'a1' };
    collectOne({ ctx, phase: { sessions: [s] }, signal: null, s, collect });
    assert.deepEqual([calls[0].lostCwdLow, calls[0].skipBigOutside], [expected.lostCwdLow, expected.skipBigOutside], name);
  }
});

test('A_rescores_re_collection_gets_both_seams_from_a_quick_context_and_neither_from_another', () => {
  for (const [name, ctx, expected] of [['quick', { ...baseCtx, lostCwdLow: true }, [true, true]], ['other', baseCtx, [undefined, undefined]]]) {
    const { calls, collect } = spy();
    collectAgain({ ctx, entry, layout, collect });
    assert.deepEqual([calls[0].lostCwdLow, calls[0].skipBigOutside], expected, name);
  }
});

/** A session folder: an evidence file and a file outside the evidence paths, both over `perFileBytes`, each holding the real token. */
function sessionWith(token) {
  const dir = tmpDir('wiring-scan');
  for (const rel of [path.join('box', 'work', 'big.out'), path.join('box', 'tmp', 'big.out'), path.join('transcripts', 'big.out')]) {
    mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true });
    writeFileSync(path.join(dir, rel), `${token}${'x'.repeat(3000)}`);
  }
  return dir;
}

const TOKEN = ['sk-ant', 'oat01', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefgh'].join('-'); // built, so no key-shaped literal stands in the source
const LIMITS = { maxScanBytes: 1024, windowBytes: 512, overlapBytes: 128, maxStreamBytes: 1 << 20 };
const BUDGET = { perFileBytes: 1024, totalBytes: 1 << 20 };

test('With_the_seam_the_scan_skips_the_big_file_outside_the_evidence_paths_by_the_real_definition_of_evidence_and_reads_the_evidence_files', () => {
  const dir = sessionWith(TOKEN);
  const scan = scanPart({ layout: { dir }, plan: { token: TOKEN, dummy: 'sk-ant-oat01-dummy-x' }, canaryTokens: [], scanLimits: LIMITS, scanHooks: {}, skipBigOutside: true, budget: BUDGET });
  assert.deepEqual(scan.skippedOutside.map(f => f.rel), [path.join('box', 'tmp', 'big.out')]);
  assert.deepEqual(scan.hits.map(h => h.source).sort(), [path.join('box', 'work', 'big.out'), path.join('transcripts', 'big.out')].sort(), 'both evidence files were read and the real token found');
  for (const rel of [path.join('box', 'work', 'x'), path.join('transcripts', 'x'), 'stream.ndjson', path.join('box', 'out', 'x')]) assert.equal(isEvidence(rel), true, rel);
  assert.equal(isEvidence(path.join('box', 'tmp', 'x')), false);
});

test('Without_the_seam_nothing_is_skipped_so_the_olympiad_and_v1_scan_what_they_always_scanned', () => {
  const dir = sessionWith(TOKEN);
  const scan = scanPart({ layout: { dir }, plan: { token: TOKEN, dummy: 'sk-ant-oat01-dummy-x' }, canaryTokens: [], scanLimits: LIMITS, scanHooks: {}, budget: BUDGET });
  assert.deepEqual(scan.skippedOutside, []);
  assert.equal(scan.hits.length, 3);
});

test('The_registered_budget_is_64_MiB_a_file_and_1_GiB_in_all', () => {
  assert.deepEqual({ ...OUTSIDE_BUDGET }, { perFileBytes: 64 * 1024 * 1024, totalBytes: 1024 ** 3 });
});

// ---- the collect itself: the seam given to `collectSession` reaches the contamination rule --------------------------------------------------

const bashEvent = command => ({ type: 'assistant', message: { content: [{ type: 'tool_use', id: `t-${command.length}`, name: 'Bash', input: { command } }] } });
// a `cd` out of the tree, `cd -` (the scan cannot place it), then a tar walk of `.`: the shape of the first quick run's false positive
const LOST_EVENTS = ['cd /tmp/bt && true', 'cd - >/dev/null', 'rm -rf /tmp/p && mkdir /tmp/p && tar -cf - . | tar -xf - -C /tmp/p && ls'].map(bashEvent);

function contaminationOf(lostCwdLow) {
  const root = tmpDir('wiring-contam');
  const layout = { rid: 'r1', dir: root, work: path.join(root, 'box', 'work'), home: path.join(root, 'box', 'home'), harness: path.join(root, 'harness'), net: path.join(root, 'net') };
  return contaminationPart({
    scan: { hits: [], unreadable: [], extra: {} }, events: LOST_EVENTS, layout, plan: { token: 'real', dummy: 'dummy' }, home: '/h', partnerDirs: [], argv: { bwrap: [] }, ...(lostCwdLow === undefined ? {} : { lostCwdLow }),
  });
}

test('The_collect_forgives_a_relative_walk_in_a_lost_directory_only_when_the_seam_reaches_it_and_says_so_in_a_low_hit', () => {
  const quick = contaminationOf(true);
  assert.deepEqual(quick.contamination.high, []);
  assert.deepEqual(quick.lowHits.map(h => `${h.severity}:${h.rule}`), ['low:outside-path']);
  const plain = contaminationOf(undefined);
  assert.deepEqual(plain.contamination.high.map(h => h.rule), ['outside-path'], 'without the seam it is the contamination it always was');
  assert.deepEqual(plain.lowHits, []);
});

// ---- the quick run's flags: apart from the registered flags table ------------------------------------------------------------------------------

import { quickFlags, rescoreFlags } from '../../minigit-bench/lib/bare-quick-flags.mjs';

test('The_quick_flags_are_the_low_outside_path_count_the_skipped_large_files_and_a_rescore_and_nothing_otherwise', () => {
  assert.deepEqual(quickFlags({ scan: {}, lowHits: [] }), []);
  assert.deepEqual(quickFlags({ scan: { skippedOutside: [{ size: 5 }, { size: 7 }] }, lowHits: [1, 2] }), ['low outside-path: 2 relative walk(s) in an unknown directory (not contamination)', '2 large file(s) outside the evidence paths not scanned (12 bytes)']);
  assert.deepEqual(rescoreFlags({ reclassifiedFrom: [{}] }), ['reclassified by rescore']);
  assert.deepEqual(rescoreFlags({}), []);
});
