import assert from 'node:assert/strict';
import { existsSync, readFileSync, realpathSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { findOnPath } from '../lib/find-on-path.mjs';
import { secretStatus } from '../lib/secrets.mjs';
import { loadStacks } from '../lib/host-checks.mjs';
import { preflight } from '../lib/preflight.mjs';
import { TS_DIR, nodeDir } from './box-helpers.mjs';
import { hostHas } from './box-helpers.mjs';
import { sandboxGate, spikeDir } from './helpers.mjs';

const stacks = loadStacks(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'stacks.json'), 'utf8'))).value;

test('The_preflight_reads_the_pinned_toolchain_from_inside_a_box_and_says_what_is_wrong_when_it_is_not', async t => {
  if (!sandboxGate(t)) return;
  const claude = findOnPath('claude');
  if (!claude || !hostHas('dotnet') || !existsSync(path.join(TS_DIR, 'node_modules', '.bin', 'tsc'))) { t.skip('needs claude, dotnet and the TypeScript toolchain on this host'); return; }
  if (!secretStatus({ home: os.homedir(), parts: ['.config', 'minigit-bench', 'claude-oauth-token'] }).present) { t.skip('the operator\'s token file is absent or has the wrong mode on this host'); return; }
  const good = await preflight({ home: os.homedir(), nodeDir, tsDir: TS_DIR, claudeBinary: realpathSync(claude), stacks, install: false });
  assert.equal(good.ok, true, JSON.stringify(good));
  assert.deepEqual(good.value.facts, { claude: stacks.pins.claude, node: stacks.pins.node, dotnetSdks: [stacks.pins.dotnetSdk], typescript: stacks.pins.typescript });
  assert.equal(good.value.bwrap.ok, true);
  const wrong = await preflight({ home: os.homedir(), nodeDir, tsDir: TS_DIR, claudeBinary: realpathSync(claude), stacks: { ...stacks, pins: { ...stacks.pins, node: '25.0.0' } }, install: false });
  assert.equal(wrong.ok, false);
  assert.ok(wrong.problems.some(p => /node.*25\.0\.0/.test(p)), wrong.problems.join('; '));
});

test('A_missing_toolchain_is_installed_only_when_asked_and_reported_otherwise', async t => {
  if (!sandboxGate(t)) return;
  const absent = path.join(os.tmpdir(), `testsToDelete-mgb-no-ts-${process.pid}`);
  const r = await preflight({ home: os.homedir(), nodeDir, tsDir: absent, claudeBinary: process.execPath, stacks, install: false });
  assert.equal(r.ok, false);
  assert.ok(r.problems.some(p => /TypeScript toolchain.*absent|install/.test(p)), r.problems.join('; '));
  assert.equal(existsSync(absent), false, 'nothing was installed');
});
