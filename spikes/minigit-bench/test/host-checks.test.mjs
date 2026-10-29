import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { bwrapFacts, loadStacks, toolchainFacts, toolchainInstallPlan } from '../lib/host-checks.mjs';
import { spikeDir } from './helpers.mjs';

const stacks = loadStacks(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'stacks.json'), 'utf8')));
const good = { claude: '2.1.310 (Claude Code)\n', node: 'v26.12.1\n', dotnetSdks: '10.0.113 [/usr/lib/dotnet/sdk]\n', tsc: 'Version 7.0.2\n' };

test('The_committed_pins_load_and_name_the_probe_of_each_tool', () => {
  assert.equal(stacks.ok, true, stacks.reason);
  assert.deepEqual(stacks.value.pins, { claude: '2.1.310', node: '26.12.1', dotnetSdk: '10.0.113', typescript: '7.0.2' });
  assert.deepEqual(Object.keys(stacks.value.probes).sort(), ['claude', 'dotnetSdks', 'node', 'tsc']);
  assert.equal(loadStacks({}).ok, false);
  assert.equal(loadStacks({ ...stacks.value, pins: { ...stacks.value.pins, node: undefined } }).ok, false);
  assert.match(loadStacks({ ...stacks.value, probes: { claude: 'claude --version' } }).reason, /argv array/, 'a probe is exe + argv, never a shell string');
});

test('The_toolchain_is_probed_inside_the_box_and_installed_when_absent', () => {
  const pins = stacks.value.pins;
  assert.deepEqual(toolchainFacts(good, pins), { ok: true, facts: { claude: '2.1.310', node: '26.12.1', dotnetSdks: ['10.0.113'], typescript: '7.0.2' }, mismatches: [] });
  const rows = [
    ['two SDK lines', { dotnetSdks: '9.0.100 [/a]\n10.0.113 [/a]\n' }, 'dotnetSdk', /exactly one/],
    ['another SDK', { dotnetSdks: '10.0.200 [/a]\n' }, 'dotnetSdk', /10\.0\.113/],
    ['node 25', { node: 'v25.1.0\n' }, 'node', /26\.12\.1/],
    ['tsc 6', { tsc: 'Version 6.0.3\n' }, 'typescript', /7\.0\.2/],
    ['claude 2.1.311', { claude: '2.1.311 (Claude Code)\n' }, 'claude', /2\.1\.310/],
    ['an unreadable claude', { claude: 'bash: claude: command not found\n' }, 'claude', /could not read/],
    ['a tool that printed nothing', { tsc: '' }, 'typescript', /could not read/],
  ];
  for (const [name, over, tool, why] of rows) {
    const r = toolchainFacts({ ...good, ...over }, pins);
    assert.equal(r.ok, false, name);
    const m = r.mismatches.find(x => x.tool === tool);
    assert.ok(m, `${name}: names ${tool}`);
    assert.match(`${m.expected} ${m.observed} ${m.why}`, why, name);
  }
  assert.equal(toolchainInstallPlan({ dir: '/t/ts', exists: () => true, version: pins.typescript }), null, 'present: nothing to install');
  const plan = toolchainInstallPlan({ dir: '/t/ts', exists: () => false, version: pins.typescript });
  assert.deepEqual(plan.steps, [{ exe: 'npm', argv: ['install', '--prefix', '/t/ts', 'typescript@7.0.2'] }, { exe: 'chmod', argv: ['-R', 'a-w', '/t/ts'] }]);
  assert.deepEqual(toolchainInstallPlan({ dir: '/t/ts', exists: p => p === '/t/ts', version: pins.typescript }).steps.length, 2, 'a dir without the tsc binary still needs the install');
});

test('The_bwrap_facts_say_what_is_missing', () => {
  assert.deepEqual(bwrapFacts({ bwrap: { code: 0, stdout: 'bubblewrap 0.11.1\n' }, unshare: { code: 0 } }), { ok: true, version: '0.11.1', problems: [] });
  assert.match(bwrapFacts({ bwrap: null, unshare: { code: 0 } }).problems[0], /bwrap is not installed/);
  assert.match(bwrapFacts({ bwrap: { code: 0, stdout: 'bubblewrap 0.11.1\n' }, unshare: { code: 1 } }).problems[0], /user namespaces/);
});
