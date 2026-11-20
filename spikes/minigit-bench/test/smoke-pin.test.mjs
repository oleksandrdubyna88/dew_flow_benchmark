// The smoke on a CLI other than v1's own pin (the seam `smokeInputs({ claude, cliPin })`): a version and a binary given to it reach the preflight, the plan and meta.json; given nothing, v1 is what it was.
// A stub `claude` and a stub upstream stand in for the CLI and the API: nothing here is paid, and no real token, network or CLI is used.
import assert from 'node:assert/strict';
import { chmodSync, cpSync, existsSync, mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { TOKEN_FILE_PARTS } from '../capture/capture-env.mjs';
import { loadStacks } from '../lib/host-checks.mjs';
import { runSmoke, smokeInputs } from '../lib/smoke-run.mjs';
import { main } from '../smoke-session.mjs';
import { fakeToken, prices, sseAnswer, sseBody, chunksOf, stubUpstream } from './broker-stub.mjs';
import { DOTNET_PIN, TS_DIR } from './box-helpers.mjs';
import { sandboxGate, spikeDir, tmpDir } from './helpers.mjs';
import { smokeStub } from './smoke-stub.mjs';

const PINNED = '2.1.329';
const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
const V1_PIN = JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'stacks.json'), 'utf8')).pins.claude;

/** A home that holds a token file the way v1's reader wants it (mode 600 in a directory of mode 700): the fake token, never a real one. */
function homeWithToken(token) {
  const home = tmpDir('pin-home');
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  mkdirSync(path.dirname(file), { recursive: true });
  chmodSync(path.dirname(file), 0o700);
  writeFileSync(file, `${token}\n`, { mode: 0o600 });
  return home;
}

/** An executable file in a folder of its own: the stub's source, or an empty script when the test never runs it. */
function binaryNamed(name, source = '#!/bin/sh\n') {
  const file = path.join(tmpDir('pin-bin'), name);
  writeFileSync(file, source, { mode: 0o755 });
  return file;
}

test('A_CLI_pin_and_a_binary_given_to_the_smoke_inputs_are_the_pin_and_the_binary_the_preflight_receives', async () => {
  const home = homeWithToken(fakeToken());
  const binary = binaryNamed(`claude-${PINNED}`);
  const inputs = smokeInputs({ home, env: { PATH: '' }, claude: binary, cliPin: PINNED });
  assert.equal(inputs.ok, true, inputs.reason);
  const seen = [];
  const refused = { ok: false, reason: 'the preflight stops this test before any launch', problems: ['none is real'] };
  const r = await runSmoke({ out: path.join(tmpDir('pin-out'), 'r1'), inputs: inputs.value, home, preflightFn: async args => { seen.push(args); return refused; } });
  assert.equal(r.ok, false, 'the stub preflight refused, so nothing launched');
  assert.equal(seen.length, 1, 'the preflight ran once');
  assert.equal(seen[0].stacks.pins.claude, PINNED, 'the preflight checks the binary against the given version, not v1\'s pin');
  assert.equal(seen[0].claudeBinary, realpathSync(binary), 'the preflight probes the given binary');
});

test('Without_a_CLI_pin_or_a_binary_the_smoke_inputs_are_v1s_own_stacks_and_the_claude_on_PATH', () => {
  const home = homeWithToken(fakeToken());
  const binary = binaryNamed('claude');
  const inputs = smokeInputs({ home, env: { PATH: path.dirname(binary) } });
  assert.equal(inputs.ok, true, inputs.reason);
  const v1 = loadStacks(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'stacks.json'), 'utf8')));
  assert.deepEqual(inputs.value.stacks, v1.value, 'the stacks are v1\'s file as loaded, not one byte different');
  assert.equal(inputs.value.stacks.pins.claude, V1_PIN);
  assert.equal(inputs.value.claudeBinary, realpathSync(binary));
});

test('A_binary_alone_does_not_move_the_pin_and_a_pin_alone_does_not_move_the_binary', () => {
  const home = homeWithToken(fakeToken());
  const onPath = binaryNamed('claude');
  const given = binaryNamed(`claude-${PINNED}`);
  const binaryOnly = smokeInputs({ home, env: { PATH: '' }, claude: given });
  assert.deepEqual([binaryOnly.value.stacks.pins.claude, binaryOnly.value.claudeBinary], [V1_PIN, realpathSync(given)]);
  const pinOnly = smokeInputs({ home, env: { PATH: path.dirname(onPath) }, cliPin: PINNED });
  assert.deepEqual([pinOnly.value.stacks.pins.claude, pinOnly.value.claudeBinary], [PINNED, realpathSync(onPath)]);
});

test('A_smoke_session_on_the_given_CLI_records_that_version_in_its_plan_and_in_meta_json', async t => {
  if (!sandboxGate(t)) return;
  const token = fakeToken();
  const home = homeWithToken(token);
  const binary = binaryNamed(`claude-${PINNED}`, smokeStub(PINNED));
  const upstream = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 10));
  t.after(() => upstream.close());
  const made = smokeInputs({ home, env: { PATH: '' }, claude: binary, cliPin: PINNED });
  assert.equal(made.ok, true, made.reason);
  const inputs = { ...made.value, prices: prices(), tsDir: TS_DIR, stacks: { ...made.value.stacks, pins: { ...made.value.stacks.pins, dotnetSdk: DOTNET_PIN } } };
  const r = await runSmoke({
    out: path.join(tmpDir('pin-run'), 'r1'), inputs, home, preflightFn: async () => ({ ok: true, value: {} }), deps: {},
    over: { upstream: upstream.upstream, settleMs: 200, tickMs: 200, sampleMs: 50, toolchains: [] },
  });
  assert.equal(r.ok, true, r.reason);
  const { layout, plan, result } = r.value;
  assert.equal(result.ok, true, result.reason);
  assert.equal(plan.cliVersion, PINNED, 'the plan carries the given version: the broker prices and the read-back checks by it');
  assert.equal(result.value.meta.cliVersion, PINNED, 'meta.json says the version the CLI itself reported');
  assert.equal(result.value.meta.outcome.label, 'ok', 'the read-back found the init event equal to the plan, so the pin and the stub agree');
  assert.equal(existsSync(path.join(layout.dir, 'checks.json')), true, 'the session wrote its checks like any smoke');
});

/** The io of the CLI as strings: what it said to the one stream and to the other. */
const ioOf = () => {
  const said = { out: '', err: '' };
  return { said, io: { out: { write: s => { said.out += s; } }, err: { write: s => { said.err += s; } } } };
};

test('The_smoke_CLI_given_a_home_a_pin_and_a_binary_plans_with_all_three_and_launches_nothing_on_a_dry_run', async () => {
  const home = homeWithToken(fakeToken());
  const binary = binaryNamed(`claude-${PINNED}`);
  const out = path.join(tmpDir('pin-dry'), 'r1');
  const { said, io } = ioOf();
  const code = await main(['--out', out, '--dry-run'], { io, home, pin: { claude: binary, cliPin: PINNED } });
  assert.equal(code, 0, said.err);
  const planned = JSON.parse(said.out);
  assert.deepEqual([planned.claude, planned.pins.claude], [realpathSync(binary), PINNED]);
  assert.equal(existsSync(out), false, 'a dry run claims no session directory');
});

test('The_verdicts_verb_writes_the_command_it_is_given_and_v1s_own_line_when_it_is_given_none', async () => {
  const [given, own] = ['given', 'own'].map(name => path.join(tmpDir(`pin-verdicts-${name}`), 'fixture'));
  for (const dir of [given, own]) cpSync(FIXTURE, dir, { recursive: true });
  const [a, b] = [ioOf(), ioOf()];
  assert.equal(await main(['verdicts', '--fixture', given], { io: a.io, command: 'node pinned-smoke.mjs --out <dir>' }), 0, a.said.err);
  assert.equal(await main(['verdicts', '--fixture', own], { io: b.io }), 0, b.said.err);
  assert.match(readFileSync(path.join(given, 'SMOKE.md'), 'utf8'), /^- Command: `node pinned-smoke\.mjs --out <dir>`$/m);
  assert.equal(readFileSync(path.join(own, 'SMOKE.md'), 'utf8'), readFileSync(path.join(FIXTURE, 'SMOKE.md'), 'utf8'), 'without a command the file is the committed one, byte for byte');
});
