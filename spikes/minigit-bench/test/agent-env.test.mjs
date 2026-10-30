import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { otelEnv } from '../lib/otel-receiver.mjs';
import { AGENT_ENV_KEYS, BOX_OTEL_PORT, agentEnv, dummyToken, layoutPaths } from '../lib/sandbox.mjs';
import { stripComments } from './scan.mjs';
import { spikeDir } from './helpers.mjs';

const layout = layoutPaths('/r', 's1');
const env = extra => agentEnv({ layout, dummyToken: dummyToken('d'), cellId: 'c', nodeDir: '/n', extra });

test('The_exporter_variables_of_the_box_are_otelEnv_of_the_box_port_and_not_a_retyped_copy', () => {
  for (const [k, v] of Object.entries(otelEnv(BOX_OTEL_PORT))) assert.equal(env({})[k], v, k);
  const src = stripComments(readFileSync(path.join(spikeDir, 'lib', 'sandbox.mjs'), 'utf8'));
  const body = src.slice(src.indexOf('export function agentEnv'), src.indexOf('export const forwarderSpecs'));
  assert.ok(body.length > 200, 'the scan found agentEnv');
  assert.match(body, /\.\.\.otelEnv\(BOX_OTEL_PORT\)/);
  assert.doesNotMatch(body, /'OTEL_|OTEL_[A-Z_]+:|CLAUDE_CODE_ENABLE_TELEMETRY/, 'no exporter key is typed inside agentEnv');
});

test('An_extra_key_that_is_already_in_the_base_is_refused_with_the_names_and_never_overrides_the_dummy', () => {
  assert.throws(() => env({ CLAUDE_CODE_OAUTH_TOKEN: 'sk-ant-oat01-REAL-looking' }), /CLAUDE_CODE_OAUTH_TOKEN/);
  assert.throws(() => env({ HOME: '/home/x', PATH: '/x', GH_TOKEN: 'ok' }), /HOME, PATH/);
  assert.throws(() => env({ OTEL_EXPORTER_OTLP_ENDPOINT: 'http://elsewhere' }), /OTEL_EXPORTER_OTLP_ENDPOINT/);
  for (const key of AGENT_ENV_KEYS) assert.throws(() => env({ [key]: 'x' }), new RegExp(key), `${key} is not an extra`);
  assert.equal(env({ GH_TOKEN: 'x' }).GH_TOKEN, 'x', 'control: a key of its own is the extra slot\'s business');
  assert.equal(env({}).CLAUDE_CODE_OAUTH_TOKEN, dummyToken('d'));
});
