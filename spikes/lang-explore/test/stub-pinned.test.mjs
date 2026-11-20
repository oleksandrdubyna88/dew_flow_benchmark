// The two rig seams that follow the CURRENT `explore.json` CLI pin (F3 PR 6 moved it from 2.1.310 to 2.1.329): the stub CLI reports the pinned version, and the rig home links the folder the pinned copy is in. Each still refuses what is
// not the pin: a stub of another shape is an error, a copy that is not there or is other bytes is a named pin problem. No box, no CLI, nothing paid.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { fileProblem } from '../lib/pins.mjs';
import { linkedParts, rigHome } from './olympiad-fixture.mjs';
import { STUB_VERSION_TEXT, stubClaudeForPins, withVersion } from './stub-pinned.mjs';

const EXPLORE = loadExplore().value;
const cliAt = (home, explore = EXPLORE) => path.join(home, ...explore.cli.pathParts);
/** A home holding `bytes` where `explore` pins the CLI copy. */
function homeWith(bytes, explore = EXPLORE) {
  const home = tmpDir('stub-pinned-home');
  mkdirSync(path.dirname(cliAt(home, explore)), { recursive: true });
  writeFileSync(cliAt(home, explore), bytes);
  return home;
}

test('The_stub_CLI_reports_the_version_the_pins_name_and_is_still_a_script_that_parses', () => {
  const other = { cli: { version: '9.8.7' } };
  const file = stubClaudeForPins(tmpDir('stub-pinned-stub'), { explore: other });
  const text = readFileSync(file, 'utf8');
  assert.ok(text.includes('claude_code_version: "9.8.7"'), 'the init event says the pinned version');
  assert.equal(text.includes(STUB_VERSION_TEXT), false, 'and no longer the version v1 wrote');
  execFileSync(process.execPath, ['--check', file]);
  const live = readFileSync(stubClaudeForPins(tmpDir('stub-pinned-live')), 'utf8');
  assert.ok(live.includes(`claude_code_version: ${JSON.stringify(EXPLORE.cli.version)}`), 'by default it follows the live explore.json');
});

test('A_stub_that_does_not_carry_the_version_marker_exactly_once_is_an_error_and_not_a_pass', () => {
  assert.throws(() => withVersion('no marker here', '1.2.3'), /has 0 version marker/);
  assert.throws(() => withVersion(`${STUB_VERSION_TEXT} ${STUB_VERSION_TEXT}`, '1.2.3'), /has 2 version marker/);
  assert.equal(withVersion(`a ${STUB_VERSION_TEXT} b`, '1.2.3'), 'a claude_code_version: "1.2.3" b');
  assert.throws(() => stubClaudeForPins(tmpDir('stub-pinned-bad'), { make: dir => { const f = path.join(dir, 'x'); writeFileSync(f, 'echo'); return f; } }), /v1's stub changed/);
});

test('The_rig_home_links_the_folder_of_the_pinned_CLI_copy_from_the_explore_json_and_refuses_a_missing_or_other_copy_by_the_pin', () => {
  const copy = Buffer.from('the pinned CLI bytes');
  const realHome = homeWith(copy);
  const rig = rigHome(realHome);
  assert.equal(existsSync(cliAt(rig)), true, 'the copy the live pins name is reachable from the rig home');
  assert.deepEqual(readFileSync(cliAt(rig)), copy, 'and it is the operator\'s own bytes, linked');
  assert.match(fileProblem('the pinned CLI copy', cliAt(rig), EXPLORE.cli.sha256), /hashes to .*, the pin says/, 'a copy of other bytes is refused by its sha256 (here the bytes are a test\'s)');
  const bare = rigHome(tmpDir('stub-pinned-empty'));
  assert.equal(existsSync(cliAt(bare)), false, 'a host without the copy gives the rig no copy: nothing is made up');
  assert.match(fileProblem('the pinned CLI copy', cliAt(bare), EXPLORE.cli.sha256), /the pinned CLI copy .* is missing/, 'and the pin check says it is missing');
  const moved = { ...EXPLORE, cli: { ...EXPLORE.cli, pathParts: ['rsd', 'elsewhere', 'claude-0.0.1'] } };
  const movedHome = homeWith(copy, moved);
  assert.equal(existsSync(cliAt(rigHome(movedHome, moved), moved)), true, 'a pin that moves to another folder moves the link with it');
  assert.equal(existsSync(cliAt(rigHome(movedHome), EXPLORE)), false, 'and the folder of the old pin is not linked for the new one');
});

test('The_rig_links_each_folder_once_whatever_the_pin_and_v1_s_folders_stay_linked', () => {
  const parts = linkedParts(EXPLORE).map(p => p.join('/'));
  assert.equal(new Set(parts).size, parts.length, 'no folder twice (a second link of one path would throw)');
  for (const kept of ['.nvm', 'rsd/minigit/toolchains', 'rsd/lang-v2/caches']) assert.ok(parts.includes(kept), kept);
  assert.ok(parts.includes(EXPLORE.cli.pathParts.slice(0, -1).join('/')), 'the folder of the pinned copy');
  const old = { cli: { pathParts: ['rsd', 'minigit', 'toolchains', 'claude-2.1.310'] } };
  assert.equal(linkedParts(old).filter(p => p.join('/') === 'rsd/minigit/toolchains').length, 1, 'the pin that was under v1\'s folder adds nothing');
});
