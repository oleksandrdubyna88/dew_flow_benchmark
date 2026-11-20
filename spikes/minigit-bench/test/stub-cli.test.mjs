// The stub `claude` (`lib/stub-cli.mjs`) reports the CLI version it is given in its init event, so a rehearsal runs under the version its pin names (F3 PR 5d). Without a version it is v1's own stub: 2.1.310.
// The stub is run as the box runs it (behind a loopback stub upstream), so the test reads the init event it really writes. No CLI, no network, no token.
import assert from 'node:assert/strict';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { stubClaude } from '../lib/stub-cli.mjs';
import { tmpDir } from './helpers.mjs';
import { stubEvents, stubInit } from './stub-cli-run.mjs';

test('A_stub_given_no_version_reports_2_1_310_as_v1_s_own_stub_always_did', async () => {
  const init = await stubInit(stubClaude(tmpDir('stub-cli-default')));
  assert.equal(init.claude_code_version, '2.1.310');
});

test('A_stub_given_a_version_reports_that_version_in_its_init_event', async () => {
  const init = await stubInit(stubClaude(tmpDir('stub-cli-version'), 'claude-stub', { version: '2.1.329' }));
  assert.equal(init.claude_code_version, '2.1.329', 'the init read-back compares this with the pin');
});

const NOT_X_Y_Z = [
  ['empty', ''], ['two_parts', '2.1'], ['a_suffix', '2.1.329-beta'], ['a_leading_space', ' 2.1.329'], ['a_trailing_newline', '2.1.329\n'], ['a_quote', "2.1.329'"], ['a_v_prefix', 'v2.1.329'], ['four_parts', '2.1.3.29'],
];

for (const [label, version] of NOT_X_Y_Z) {
  test(`A_version_that_is_${label}_is_refused_and_no_stub_file_is_written`, () => {
    const dir = tmpDir('stub-cli-bad');
    assert.throws(() => stubClaude(dir, 'claude-stub', { version }), /version/, 'the refusal names the version');
    assert.deepEqual(readdirSync(dir), [], 'no file was written');
  });
}

test('A_version_that_holds_code_never_runs_it_and_is_refused', async () => {
  const marker = path.join(tmpDir('stub-cli-marker'), 'ran');
  const dir = tmpDir('stub-cli-inject');
  const injected = `1.0.0', x: require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'ran'), y: '2`;
  const refusal = (() => { try { stubClaude(dir, 'claude-stub', { version: injected }); return null; } catch (e) { return e; } })();
  for (const file of readdirSync(dir)) await stubEvents(path.join(dir, file)).catch(() => null); // what a stub written from it would do, if one were written
  assert.equal(existsSync(marker), false, 'the injected statement did not run');
  assert.match(refusal?.message ?? '', /version/, 'the refusal names the version');
});
