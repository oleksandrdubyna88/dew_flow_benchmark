// The pure planning of a bare session (E3b.S1): rids, masks, launch order, prompts, workspace files, the launch plan.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { bareSessionPlan, firstStack, maskFor, promptFor, pilotRid, ridOf, seriesId, workspaceFiles } from '../lib/bare-plan.mjs';
import { layoutPaths } from '../lib/sandbox.mjs';
import { spikeDir } from './helpers.mjs';

const REGISTERED = JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'prompts-bare.json'), 'utf8'));
const RID = /^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$/;
const STACKS = ['csharp', 'node'];
const sha256 = text => createHash('sha256').update(text).digest('hex');

test('Rids_are_distinct_across_pair_stack_phase_and_attempt', () => {
  const rids = [];
  for (const pair of [1, 2, 3]) for (const stack of STACKS) for (const phase of ['v1', 'v2']) for (const attempt of [1, 2]) rids.push(ridOf({ seed: 'mg-0123456789abcdef', pair, stack, phase, attempt }));
  assert.equal(rids.length, 24);
  assert.equal(new Set(rids).size, 24);
  rids.forEach(r => assert.match(r, RID));
  assert.equal(ridOf({ seed: 'mg-0123456789abcdef', pair: 1, stack: 'csharp', phase: 'v1', attempt: 1 }), createHash('sha256').update('mg-0123456789abcdef:bare:p1:csharp:v1:a1').digest('hex').slice(0, 6), 'the registered formula');
  assert.notEqual(ridOf({ seed: 'mg-aaaaaaaaaaaaaaaa', pair: 1, stack: 'csharp', phase: 'v1', attempt: 1 }), ridOf({ seed: 'mg-bbbbbbbbbbbbbbbb', pair: 1, stack: 'csharp', phase: 'v1', attempt: 1 }));
});

test('A_pilot_rid_is_fresh_per_random_value_and_a_valid_rid', () => {
  assert.match(pilotRid('a1b2c3d4'), RID);
  assert.notEqual(pilotRid('a1b2c3d4'), pilotRid('a1b2c3d5'));
});

test('No_path_the_model_sees_names_the_arm_or_the_stack', () => {
  const series = seriesId({ date: new Date('2026-10-30T10:00:00Z'), rand: 'ab12' });
  assert.equal(series, 's-20261030-ab12');
  const banned = new Set(['bare', 'process', 'csharp', 'cs', 'node', 'ts', 'typescript']);
  for (const pair of [1, 2, 3]) for (const stack of STACKS) for (const phase of ['v1', 'v2']) for (const attempt of [1, 2]) {
    const rid = ridOf({ seed: 'mg-0123456789abcdef', pair, stack, phase, attempt });
    const layout = layoutPaths(path.join('/home/x/rsd/minigit', series, 's'), rid);
    for (const p of [layout.work, layout.config, layout.home, layout.tmp]) {
      const hit = p.split('/').find(seg => banned.has(seg.toLowerCase()));
      assert.equal(hit, undefined, `${p} names '${hit}'`);
    }
  }
});

test('Masks_swap_between_pairs_and_order_alternates_by_seed', () => {
  const seeds = Array.from({ length: 16 }, (_, i) => `mg-${i.toString(16).padStart(16, '0')}`);
  const maskOfCs = (seed, pair) => maskFor({ seed, pair, stack: 'csharp' });
  for (const seed of seeds) {
    for (const pair of [1, 2, 3]) {
      const a = maskFor({ seed, pair, stack: 'csharp' });
      const b = maskFor({ seed, pair, stack: 'node' });
      assert.deepEqual([a, b].sort(), ['0-5', '8-13'], 'within a pair the two stacks hold the two masks');
    }
    assert.notEqual(maskOfCs(seed, 1), maskOfCs(seed, 2), 'the masks swap between pairs');
    assert.equal(maskOfCs(seed, 1), maskOfCs(seed, 3), 'with three pairs one stack holds a mask twice');
  }
  assert.equal(new Set(seeds.map(s => maskOfCs(s, 1))).size, 2, 'the seed decides pair 1: both assignments occur');
  const firsts = seeds.flatMap(seed => [1, 2, 3].flatMap(pair => ['v1', 'v2'].map(phase => firstStack({ seed, pair, phase }))));
  assert.ok(firsts.every(f => STACKS.includes(f)));
  assert.equal(new Set(firsts).size, 2, 'a seeded bit: both stacks lead somewhere');
  assert.equal(firstStack({ seed: seeds[0], pair: 1, phase: 'v1' }), firstStack({ seed: seeds[0], pair: 1, phase: 'v1' }), 'and it is a function of the seed');
});

test('The_prompt_is_the_registered_bytes', () => {
  for (const stack of STACKS) for (const phase of ['v1', 'v2']) {
    const r = REGISTERED.prompts[stack][phase];
    assert.equal(promptFor({ stack, phase }), r.text);
    assert.equal(sha256(r.text), r.sha256, 'the registered hash is the hash of the registered text');
  }
});

test('The_registered_prompts_differ_only_in_the_stack_slots', () => {
  assert.equal(REGISTERED.slots.csharp.STACK, 'C#');
  assert.equal(REGISTERED.slots.node.STACK, 'TypeScript', 'banteg said Typescript; parent 4.3 change (1) makes it TypeScript');
  for (const phase of ['v1', 'v2']) {
    const fill = stack => REGISTERED.templates[phase].replaceAll('{STACK_SENTENCE}', REGISTERED.slots[stack].STACK_SENTENCE).replaceAll('{STACK}', REGISTERED.slots[stack].STACK);
    for (const stack of STACKS) assert.equal(REGISTERED.prompts[stack][phase].text, fill(stack));
    assert.equal(REGISTERED.prompts.csharp[phase].text.replaceAll(REGISTERED.slots.csharp.STACK_SENTENCE, '{S}').replaceAll('C#', '{L}'),
      REGISTERED.prompts.node[phase].text.replaceAll(REGISTERED.slots.node.STACK_SENTENCE, '{S}').replaceAll('TypeScript', '{L}'), 'equal once the two slots are blanked');
  }
  assert.match(REGISTERED.slots.csharp.STACK_SENTENCE, /^Use C# 14 on \.NET 10 \(SDK 10\.0\.113 is installed\)\.$/);
  assert.match(REGISTERED.slots.node.STACK_SENTENCE, /^Use TypeScript run directly by Node 26 \(native type stripping, no build step\), with `tsc --noEmit` as the type check\.$/);
});

test('A_prompt_for_an_unknown_stack_or_phase_names_the_legal_values', () => {
  assert.throws(() => promptFor({ stack: 'rust', phase: 'v1' }), /csharp, node/);
  assert.throws(() => promptFor({ stack: 'node', phase: 'v3' }), /v1, v2/);
});

test('The_workspace_files_of_a_phase_are_the_pinned_inputs_and_nothing_else', () => {
  assert.deepEqual(workspaceFiles('v1'), ['SPEC-v1.txt', 'test-v1.sh']);
  assert.deepEqual(workspaceFiles('v2'), ['SPEC-v1.txt', 'test-v1.sh', 'SPEC-v2.txt', 'test-v2.sh']);
});

test('The_session_plan_carries_the_registered_caps_and_hosts_and_a_neutral_cell_id', () => {
  const layout = layoutPaths('/home/x/rsd/minigit/s-20261030-ab12/s', 'abc123');
  const plan = bareSessionPlan({
    layout, stack: 'csharp', phase: 'v1', claudeBinary: '/x/claude-2.1.310', cliVersion: '2.1.310', nodeDir: '/n', tsDir: '/ts', token: 'real', dummy: 'dummy', prices: {}, operatorHome: '/home/x',
    harnessSources: { boxInit: 'a', audit: 'b' },
  });
  assert.equal(plan.arm, 'bare');
  assert.equal(plan.capUsd, 6);
  assert.equal(plan.wallMs, 3_600_000);
  assert.equal(plan.stallMs, 2_700_000);
  assert.deepEqual(plan.allow, ['registry.npmjs.org', 'api.nuget.org']);
  assert.deepEqual(plan.toolchains, ['/ts']);
  assert.equal(plan.prompt, promptFor({ stack: 'csharp', phase: 'v1' }));
  assert.equal(plan.cellId, 'cell-abc123');
  assert.doesNotMatch(JSON.stringify({ cell: plan.cellId }), /csharp|bare|node/);
  const stubbed = bareSessionPlan({ layout, stack: 'node', phase: 'v2', claudeBinary: '/x/c', cliVersion: '2.1.310', nodeDir: '/n', tsDir: '/ts', token: 'real', dummy: 'dummy', prices: {}, operatorHome: '/h', harnessSources: {}, over: { upstream: { host: '127.0.0.1', port: 1, tls: false }, tickMs: 5 } });
  assert.deepEqual([stubbed.upstream.port, stubbed.tickMs, plan.upstream], [1, 5, undefined], 'the test seam reaches the plan; a real run has no upstream override');
});

test('The_plan_override_takes_only_the_test_seams_and_never_a_security_field', () => {
  const given = { layout: { rid: 'abc123' }, stack: 'csharp', phase: 'v1', claudeBinary: '/x/c', cliVersion: '2.1.310', nodeDir: '/n', tsDir: '/ts', token: 'REAL-TOKEN-VALUE', dummy: 'dummy', prices: {}, operatorHome: '/h', harnessSources: {} };
  const real = bareSessionPlan(given);
  for (const [key, value] of Object.entries({ token: 'other', dummy: 'x', layout: { rid: 'zzz' }, allow: ['evil.example'], operatorHome: '/', claudeBinary: '/bin/sh', toolchains: ['/'], envExtra: { A: '1' }, cellId: 'cell-x', capUsd: 600, wallMs: 1, prompt: 'x', harnessSources: {} })) {
    assert.throws(() => bareSessionPlan({ ...given, over: { [key]: value } }), new RegExp(`over may not set '${key}'`), `${key} is a security or treatment field`);
  }
  assert.throws(() => bareSessionPlan({ ...given, over: { upstream: { host: 'evil.example', port: 443, tls: true } } }), /upstream must be a loopback address/, 'the real token is sent to the upstream: a seam may point it at the machine itself only');
  try { bareSessionPlan({ ...given, over: { token: 'LEAKED' } }); } catch (e) { assert.doesNotMatch(e.message, /LEAKED|REAL-TOKEN-VALUE/, 'a refusal names the key, never a value'); }
  const ok = bareSessionPlan({ ...given, over: { upstream: { host: '127.0.0.1', port: 9, tls: false }, tickMs: 5, sampleMs: 5, settleMs: 5, graceMs: 5, drainMs: 5 } });
  assert.deepEqual([ok.tickMs, ok.upstream.port, ok.token, ok.allow], [5, 9, real.token, real.allow], 'the seams pass, everything else is the real plan');
});
