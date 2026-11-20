// The free rehearsal of a Stage 1 series runs under the CLI version its `explore` pins (F3 PR 5d): `rehearsalIo` gives the unit a stub CLI whose init event reports that version, so the init read-back holds.
// The stub is run behind a loopback stub upstream and its init event is read, as v1's read-back reads it. The world is a fake: nothing is loaded from the operator's home, no box starts, no CLI is called, nothing is paid.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { stubInit } from '../../minigit-bench/test/stub-cli-run.mjs';
import { rehearsalIo } from '../lib/stage-rehearsal.mjs';

const TASK = { id: '02', kind: 'web' };
const worldOf = version => ({ explore: { cli: { version } }, filesFor: () => ({}) });

/** The `io` a rehearsal unit runs with, made over a fake world: `worldFn` stands for `loadWorld`. -> `{ rig, init }`: `init` is the init event of the stub CLI the unit would launch. */
async function rehearsalOf(t, worldFn) {
  const rig = await rehearsalIo({ series: { dir: tmpDir('stage-rehearsal-series') }, io: { home: tmpDir('stage-rehearsal-home'), worldFn }, task: TASK });
  t.after(() => rig.close());
  return { rig, init: await stubInit(rig.io.seams.claudeBinary) };
}

test('The_stub_CLI_of_a_Stage_1_rehearsal_reports_the_version_its_explore_pins', async t => {
  const { init } = await rehearsalOf(t, async () => ok(worldOf('2.1.329')));
  assert.equal(init.claude_code_version, '2.1.329', 'the init read-back compares this with the pinned explore.json cli.version');
});

test('The_stub_CLI_follows_the_pin_whatever_it_is_so_the_version_is_the_explore_s_and_not_a_constant', async t => {
  const { init } = await rehearsalOf(t, async () => ok(worldOf('2.2.7')));
  assert.equal(init.claude_code_version, '2.2.7');
});

test('A_world_that_cannot_be_loaded_has_no_pin_to_follow_so_the_stub_is_v1_s_own', async t => {
  const { init } = await rehearsalOf(t, async () => fail('the pinned CLI copy is missing'));
  assert.equal(init.claude_code_version, '2.1.310');
});
