// The stub `claude` (`lib/stub-cli.mjs`) reports the CLI version it is given in its init event, so a rehearsal runs under the version its pin names (F3 PR 5d). Without a version it is v1's own stub: 2.1.310.
// The stub is run as the box runs it (behind a loopback stub upstream), so the test reads the init event it really writes. No CLI, no network, no token.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { stubClaude } from '../lib/stub-cli.mjs';
import { tmpDir } from './helpers.mjs';
import { stubInit } from './stub-cli-run.mjs';

test('A_stub_given_no_version_reports_2_1_310_as_v1_s_own_stub_always_did', async () => {
  const init = await stubInit(stubClaude(tmpDir('stub-cli-default')));
  assert.equal(init.claude_code_version, '2.1.310');
});

test('A_stub_given_a_version_reports_that_version_in_its_init_event', async () => {
  const init = await stubInit(stubClaude(tmpDir('stub-cli-version'), 'claude-stub', { version: '2.1.329' }));
  assert.equal(init.claude_code_version, '2.1.329', 'the init read-back compares this with the pin');
});
