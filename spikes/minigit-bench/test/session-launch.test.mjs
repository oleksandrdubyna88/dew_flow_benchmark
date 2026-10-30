import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import { PassThrough } from 'node:stream';
import { test } from 'node:test';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { EGRESS_HOSTS } from '../lib/egress-hosts.mjs';
import { cellProcesses, endCell, endSurvivors, realProcApi } from '../lib/janitor.mjs';
import { holdsPaidWork, launchSession } from '../lib/session-launch.mjs';
import { scanSessionDir } from '../lib/session-scan.mjs';
import { dummyToken, sessionLayout } from '../lib/sandbox.mjs';
import { apiAttrs, otlpPayload } from './synth.mjs';
import { fakeToken, prices, sseAnswer, sseBody, chunksOf, stubUpstream } from './broker-stub.mjs';
import { newSession, nodeDir, sleep } from './box-helpers.mjs';
import { sandboxGate, spikeDir, tmpDir } from './helpers.mjs';

const AUDIT = path.join(spikeDir, '..', 'policy-ab-quick', 'hooks', 'audit.mjs');
const BOX_INIT = path.join(spikeDir, 'harness', 'box-init.mjs');
const rand = () => Math.random().toString(36).slice(2, 9);

/** The plan of a launch over `layout`; `over` varies it. The real token is a FAKE here, everywhere. */
function planFor(layout, over = {}) {
  const real = fakeToken();
  return {
    layout, arm: 'bare', cellId: `cell-${rand()}`, prompt: 'Reply OK', capUsd: 0.2, wallMs: 60_000, stallMs: 30_000, tickMs: 150, sampleMs: 100, settleMs: 200, graceMs: 1500,
    claudeBinary: process.execPath, cliVersion: '2.1.310', nodeDir, tsDir: null, toolchains: [], token: real, dummy: dummyToken(rand()), prices: prices(),
    allow: EGRESS_HOSTS.bare, harnessSources: { boxInit: BOX_INIT, audit: AUDIT }, ...over,
  };
}

// ---- a stub `claude`: a script that does what the real one does at its edges ----------------------------------------------------

/** The init event as 2.1.310 reports it for this box; the stub fills the box-specific fields from its own environment and argv. */
const STUB = otlpBody => `#!/usr/bin/env node
const fs = require('node:fs'); const path = require('node:path'); const http = require('node:http');
const argv = process.argv.slice(2);
const tools = argv[argv.indexOf('--tools') + 1].split(',').filter(t => t !== 'TodoWrite').map(t => (t === 'Agent' ? 'Task' : t));
const say = o => process.stdout.write(JSON.stringify(o) + '\\n');
const post = (url, body, headers) => new Promise((resolve, reject) => {
  const u = new URL(url);
  const req = http.request({ host: u.hostname, port: u.port, path: u.pathname, method: 'POST', headers: { 'content-type': 'application/json', ...headers } }, res => { const c = []; res.on('data', d => c.push(d)); res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(c).toString() })); });
  req.on('error', reject); req.end(body);
});
(async () => {
  say({ type: 'system', subtype: 'init', cwd: process.cwd(), session_id: 'stub-session', tools, mcp_servers: [], model: 'claude-sonnet-5-5', permissionMode: 'default', apiKeySource: 'none', claude_code_version: '2.1.310', memory_paths: { auto: process.env.CLAUDE_CONFIG_DIR + '/projects/-p/memory/' } });
  if (process.env.STUB_SLEEP_FOREVER === '1') { setInterval(() => {}, 1000); return; }
  if (process.env.STUB_ABANDON === '1') { // send the request and leave without reading the answer: the upstream keeps streaming to a client that is gone
    const u = new URL(process.env.ANTHROPIC_BASE_URL);
    const req = http.request({ host: u.hostname, port: u.port, path: '/v1/messages?beta=true', method: 'POST', headers: { 'content-type': 'application/json', authorization: 'Bearer ' + process.env.CLAUDE_CODE_OAUTH_TOKEN } });
    req.on('error', () => {}); req.end('{"model":"claude-sonnet-5-5","stream":true}');
    setTimeout(() => process.exit(0), 150); return;
  }
  const r = await post(process.env.ANTHROPIC_BASE_URL + '/v1/messages?beta=true', '{"model":"claude-sonnet-5-5","stream":true}', { authorization: 'Bearer ' + process.env.CLAUDE_CODE_OAUTH_TOKEN, 'anthropic-beta': 'oauth-2025-04-20' });
  await post(process.env.OTEL_EXPORTER_OTLP_ENDPOINT + '/v1/logs', ${JSON.stringify(otlpBody)}, {});
  fs.mkdirSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p', { recursive: true });
  fs.writeFileSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p/stub-session.jsonl', '{"type":"assistant"}\\n');
  await new Promise(r2 => setTimeout(r2, Number(process.env.STUB_HOLD_MS || 0)));
  say({ type: 'assistant', message: { id: 'm1', model: 'claude-sonnet-5-5', usage: { input_tokens: 2, output_tokens: 4 } } });
  say({ type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.009232, result: 'status ' + r.status });
})();
`;

function stubClaude(dir, name = 'claude-stub') {
  const file = path.join(dir, name);
  const otlp = JSON.stringify(otlpPayload([{ name: 'api_request', tsMs: Date.now(), attrs: apiAttrs({ request_id: 'req_stub_1' }) }]));
  writeFileSync(file, STUB(otlp), { mode: 0o755 });
  return file;
}

/** A launch over a real box, a stub claude and a stub upstream behind the broker. `env` is added to the box's environment through envExtra. */
async function realLaunch(t, { holdMs = 0, over = {}, signal } = {}) {
  const layout = newSession();
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  t.after(() => stub.close());
  const plan = planFor(layout, { claudeBinary: stubClaude(tmpDir('stub')), upstream: stub.upstream, envExtra: { STUB_HOLD_MS: String(holdMs) }, ...over });
  return { layout, stub, plan, result: await launchSession(plan, {}, { signal }) };
}

test('The_launch_stamps_every_line_and_collects_what_E1_reads', async t => {
  if (!sandboxGate(t)) return;
  const { layout, stub, plan, result } = await realLaunch(t);
  assert.equal(result.ok, true, result.reason);
  const { meta } = result.value;
  const raw = readFileSync(layout.files.stream, 'utf8').split('\n').filter(Boolean);
  const stamped = readFileSync(layout.files.streamStamped, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  assert.deepEqual(stamped.map(s => s.line), raw, 'one { t, line } per raw line');
  assert.ok(stamped.every((s, i) => i === 0 || s.t >= stamped[i - 1].t) && stamped.at(-1).t > stamped[0].t, 't increases');
  assert.equal(stub.seen[0].headers.authorization, `Bearer ${plan.token}`, 'the request reached the upstream signed with the real token');
  assert.notEqual(stub.seen[0].headers.authorization, `Bearer ${plan.dummy}`);
  const otel = readFileSync(path.join(layout.otel, 'otel.ndjson'), 'utf8');
  assert.match(otel, /api_request/, 'the OTel body arrived through the socket');
  assert.ok(readdirSync(layout.transcripts, { recursive: true }).some(f => String(f).endsWith('.jsonl')), 'transcripts copied');
  const argvJson = readFileSync(layout.files.argv, 'utf8');
  assert.equal(argvJson.includes(plan.token) || argvJson.includes(plan.dummy), false, 'argv.json carries env KEYS only');
  assert.ok(JSON.parse(argvJson).envKeys.includes('CLAUDE_CODE_OAUTH_TOKEN'));
  assert.match(JSON.parse(argvJson).settingsSha256, /^[0-9a-f]{64}$/);
  for (const f of ['egress', 'broker', 'meta', 'readback']) assert.ok(existsSync(layout.files[f]), f);
  assert.equal(JSON.parse(readFileSync(layout.files.state, 'utf8')).phase, 'closed');
  assert.equal(meta.outcome.label, 'ok');
  assert.equal(meta.broker.spentUsd, 0.009232);
  assert.deepEqual(meta.broker.unpricedModels, [], 'the cell record lists every model that was charged at the dearest rates: none here');
  assert.equal(meta.janitorEnded, 0);
  assert.equal(meta.stopped, null);
  assert.equal(meta.stoppedAt, null, 'nothing stopped it: no stop time');
  assert.deepEqual(meta.cleanup, []);
  assert.deepEqual(readdirSync(layout.net), [], 'the sockets are removed');
  await assert.rejects(new Promise((resolve, reject) => net.connect(layout.sockets.otel).once('connect', resolve).once('error', reject)), /ENOENT|ECONNREFUSED/);
  assert.equal(cellProcesses(plan.cellId, realProcApi).pids.length, 0, 'no process carries the cell marker');
  assert.deepEqual(scanSessionDir(layout.dir, { real: plan.token, dummy: plan.dummy }).hits, [], 'the real token is in no file of the session dir');
});

test('The_real_token_is_on_no_host_process_listing_or_environ_while_a_session_runs', async t => {
  if (!sandboxGate(t)) return;
  const { result } = await realLaunch(t, { holdMs: 1200 });
  assert.equal(result.ok, true, result.reason);
  const s = result.value.meta.samplers;
  assert.ok(s.samples >= 3, `at least 3 samples (${s.samples})`);
  assert.equal(s.realInCmdline, 0);
  assert.equal(s.realInEnviron, 0);
  assert.ok(s.dummyInEnviron >= 1, 'control: the sampler does see the dummy in the box\'s environment');
});

test('A_startup_failure_leaves_no_listener_and_no_empty_dir', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const plan = planFor(layout, { claudeBinary: path.join(tmpDir('nobin'), 'does-not-exist') });
  let receiverPort = null;
  const result = await launchSession(plan, { startReceiver: async o => { const r = await (await import('../lib/otel-receiver.mjs')).startReceiver(o); receiverPort = r.port; return r; } });
  assert.equal(result.ok, false);
  assert.equal(result.exit, 5);
  assert.match(result.reason, /does not exist/);
  assert.ok(receiverPort > 0);
  await assert.rejects(new Promise((resolve, reject) => net.connect(receiverPort, '127.0.0.1').once('connect', resolve).once('error', reject)), /ECONNREFUSED/, 'the receiver port is gone');
  for (const sock of Object.values(layout.sockets)) assert.equal(existsSync(sock), false, sock);
  assert.equal(existsSync(layout.dir), false, 'the dir holds nothing paid and is removed');
});

test('A_cancelled_launch_stops_the_cell_and_closes_everything', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const stub = await stubUpstream((_q, res) => res.end());
  t.after(() => stub.close());
  const plan = planFor(layout, { claudeBinary: stubClaude(tmpDir('stub-c')), upstream: stub.upstream, envExtra: { STUB_SLEEP_FOREVER: '1' } });
  const ac = new AbortController();
  const spy = [];
  const deps = { endCell: async a => { spy.push(a.cellId); return endCell(a); } };
  setTimeout(() => ac.abort(), 1500);
  const result = await launchSession(plan, deps, { signal: ac.signal });
  assert.equal(result.ok, true, result.reason);
  assert.deepEqual(spy, [plan.cellId], 'endCell ran once');
  assert.equal(result.value.meta.stopped, 'cancelled');
  assert.equal(cellProcesses(plan.cellId, realProcApi).pids.length, 0);
  assert.deepEqual(readdirSync(layout.net), []);
  assert.equal(JSON.parse(readFileSync(layout.files.state, 'utf8')).phase, 'closed');
  assert.equal(existsSync(layout.dir), true, 'the dir is kept: a stream line was written');
  assert.match(readFileSync(layout.files.stream, 'utf8'), /"subtype":"init"/);
});

// ---- every exit path, with the edges faked ------------------------------------------------------------------------------------

function fakeChild() {
  const c = new EventEmitter();
  Object.assign(c, { pid: 2 ** 22 - 7, stdout: new PassThrough(), stderr: new PassThrough(), stdin: new PassThrough() });
  c.finish = (code = 0, signal = null) => { if (c.done) return; c.done = true; c.stdout.end(); setImmediate(() => c.emit('close', code, signal)); };
  return c;
}
const INIT = over => ({
  type: 'system', subtype: 'init', cwd: null, session_id: 's', tools: ['Task', 'Bash', 'Edit', 'Glob', 'Grep', 'Read', 'Write'], mcp_servers: [], model: 'claude-sonnet-5-5', permissionMode: 'default',
  apiKeySource: 'none', claude_code_version: '2.1.310', memory_paths: { auto: null }, ...over,
});
const RESULT = { type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.01 };

/** Fake deps. Each server pushes its name to `order` when closed; `script(child, layout)` plays the box. */
function fakeDeps(order, layout, { script, throwOn = {}, closeThrows = null, drain = async () => ({ drained: true, inFlight: 0, reservedUsd: 0 }), unsettled = null } = {}) {
  const count = unsettled?.count ?? 0;
  const seal = () => ({ unsettled: unsettled ?? { count: 0, reservedUsd: 0 }, lowerBound: count > 0, summary: { spentUsd: unsettled?.reservedUsd ?? 0, reservedUsd: unsettled?.reservedUsd ?? 0, upstreamRequests: 0, refused: 0, overCap: false, overshootUsd: 0 } });
  const server = (name, extra = {}) => ({ ...extra, close: async () => { order.push(name); if (closeThrows === name) throw new Error(`${name} close failed`); } });
  const child = fakeChild();
  const spawned = [];
  return {
    spawned, child,
    deps: {
      startReceiver: async () => { if (throwOn.receiver) throw new Error('listen EADDRINUSE: receiver'); return server('receiver', { port: 1, misconfigured: 0, undecodable: 0, liveSum: () => { if (throwOn.liveSum) throw new Error('liveSum threw'); return { usd: 0, requests: 0, lastAt: 0 }; } }); },
      brokerServer: async () => server('broker', { drain, seal, summary: () => ({ spentUsd: 0, reservedUsd: 0, upstreamRequests: 0, refused: 0, overCap: false, overshootUsd: 0 }) }),
      egressProxy: async () => { if (throwOn.proxy) throw new Error('listen EADDRINUSE: proxy socket'); return server('proxy'); },
      spawnBox: (argv, env) => { spawned.push({ argv, env }); setImmediate(() => script(child, layout)); return child; },
      endCell: async a => { order.push('endCell'); child.finish(143, null); return { cellId: a.cellId }; },
      endSurvivors: async () => 0, killTree: () => {}, procApi: { pids: () => [], read: () => { throw Object.assign(new Error('x'), { code: 'ENOENT' }); }, signal: () => {} },
      sleep: async () => {},
    },
  };
}
const goodInit = layout => INIT({ cwd: layout.work, memory_paths: { auto: `${layout.config}/projects/-p/memory/` } });
const say = (child, ...evs) => evs.forEach(e => child.stdout.write(`${JSON.stringify(e)}\n`));

test('Every_exit_path_runs_every_disposer_once', async () => {
  const CREATED = ['receiver', 'broker', 'proxy'];
  const paths = [
    ['success', { script: (c, l) => { say(c, goodInit(l), RESULT); c.finish(0); } }, {}, ['proxy', 'broker', 'receiver'], 0],
    ['a read-back mismatch', { script: (c, l) => say(c, { ...goodInit(l), claude_code_version: '9.9.9' }) }, {}, ['proxy', 'broker', 'receiver'], 1],
    ['the wall watchdog', { script: (c, l) => say(c, goodInit(l)) }, { wallMs: 120 }, ['proxy', 'broker', 'receiver'], 1],
    ['a receiver whose listen throws', { script: () => {}, throwOn: { receiver: true } }, {}, [], 0],
    ['a proxy socket already taken', { script: () => {}, throwOn: { proxy: true } }, {}, ['broker', 'receiver'], 0],
    ['an aborted signal mid-run', { script: (c, l) => say(c, goodInit(l)) }, { abort: 150 }, ['proxy', 'broker', 'receiver'], 1],
    ['a dep that throws inside the loop', { script: (c, l) => { say(c, goodInit(l)); setTimeout(() => { say(c, RESULT); c.finish(0); }, 400); }, throwOn: { liveSum: true } }, {}, ['proxy', 'broker', 'receiver'], 0],
  ];
  for (const [name, spec, planOver, expectedClosed, endCells] of paths) {
    const layout = newSession();
    const order = [];
    const fake = fakeDeps(order, layout, spec);
    const ac = new AbortController();
    if (planOver.abort) setTimeout(() => ac.abort(), planOver.abort);
    const result = await launchSession(planFor(layout, { claudeBinary: process.execPath, wallMs: planOver.wallMs ?? 60_000, settleMs: 0, tickMs: 40 }), fake.deps, { signal: ac.signal });
    assert.deepEqual(order.filter(o => CREATED.includes(o)), expectedClosed, `${name}: each created server closed once, LIFO`);
    assert.equal(order.filter(o => o === 'endCell').length, endCells, `${name}: endCell`);
    assert.equal(Object.values(layout.sockets).some(s => existsSync(s)), false, `${name}: sockets gone`);
    const state = existsSync(layout.files.state) ? JSON.parse(readFileSync(layout.files.state, 'utf8')) : null;
    if (result.ok) {
      assert.ok(['closed'].includes(state.phase), `${name}: phase ${state.phase}`);
      assert.deepEqual(result.value.meta.cleanup, [], name);
    } else {
      assert.equal(result.exit, 5, name);
    }
  }
});

test('The_collection_drains_the_broker_before_it_writes_broker_json_and_records_what_stayed_unsettled', async () => {
  const layout = newSession();
  const seen = [];
  const drain = async ms => { seen.push({ ms, brokerJsonYet: existsSync(layout.files.broker) }); return { drained: false, inFlight: 2, reservedUsd: 0.1 }; };
  const order = [];
  const fake = fakeDeps(order, layout, { script: (c, l) => { say(c, goodInit(l), RESULT); c.finish(0); }, drain, unsettled: { count: 2, reservedUsd: 0.1 } });
  const result = await launchSession(planFor(layout, { settleMs: 0, drainMs: 777 }), fake.deps);
  assert.equal(result.ok, true, result.reason);
  assert.deepEqual(seen, [{ ms: 777, brokerJsonYet: false }], 'drained once, with the plan\'s ceiling, before broker.json existed');
  assert.deepEqual(result.value.meta.unsettledAtClose, { count: 2, reservedUsd: 0.1 });
  assert.deepEqual(JSON.parse(readFileSync(layout.files.broker, 'utf8')).unsettledAtClose, { count: 2, reservedUsd: 0.1 });
  assert.equal(JSON.parse(readFileSync(layout.files.broker, 'utf8')).lowerBound, true, 'B with an unsettled request is a lower bound');
  assert.equal(result.value.meta.broker.lowerBound, true);
  assert.equal(order.includes('broker'), true, 'and the broker was closed after (the disposers run after the collection)');
});

test('A_request_the_cli_abandoned_is_settled_before_the_meta_is_written', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 4), 250)); // about 750 ms of stream after the client is gone
  t.after(() => stub.close());
  const plan = planFor(layout, { claudeBinary: stubClaude(tmpDir('stub-ab')), upstream: stub.upstream, envExtra: { STUB_ABANDON: '1' }, settleMs: 50 });
  const result = await launchSession(plan, {});
  assert.equal(result.ok, true, result.reason);
  const { meta } = result.value;
  assert.equal(meta.broker.spentUsd, 0.009232, 'the abandoned request was drained and priced, not left as 0 spent');
  assert.deepEqual(meta.unsettledAtClose, { count: 0, reservedUsd: 0 });
  assert.equal(JSON.parse(readFileSync(layout.files.broker, 'utf8')).unsettled, 0);
});

test('A_request_still_streaming_at_the_drain_limit_is_reported_as_a_lower_bound_and_nothing_is_written_to_the_ledger_after_the_meta', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const marks = {};
  const stub = await stubUpstream(async (_q, res) => { // ~3 s of stream for a client that is gone; the drain limit is 100 ms
    res.on('close', () => { marks.closedAt = Date.now(); });
    res.writeHead(200, { 'content-type': 'text/event-stream' });
    for (const c of chunksOf(sseBody(), 6)) { if (res.destroyed) return; res.write(c); await sleep(500); }
    res.end();
  });
  t.after(() => stub.close());
  const plan = planFor(layout, { claudeBinary: stubClaude(tmpDir('stub-lb')), upstream: stub.upstream, envExtra: { STUB_ABANDON: '1' }, settleMs: 50, drainMs: 100 });
  const result = await launchSession(plan, {});
  assert.equal(result.ok, true, result.reason);
  const { meta } = result.value;
  assert.deepEqual(meta.unsettledAtClose, { count: 1, reservedUsd: 0.05 });
  assert.equal(meta.broker.lowerBound, true);
  assert.equal(meta.broker.spentUsd, 0.05, 'B is the settled spend (0) plus the unsettled reservation');
  const linesAfter = readFileSync(layout.files.brokerLog, 'utf8').split('\n').filter(Boolean).length;
  const brokerJson = JSON.parse(readFileSync(layout.files.broker, 'utf8'));
  assert.equal(brokerJson.lowerBound, true);
  await sleep(1200);
  assert.equal(readFileSync(layout.files.brokerLog, 'utf8').split('\n').filter(Boolean).length, linesAfter, 'no ledger line was written after the meta');
  assert.ok(marks.closedAt !== undefined, 'the upstream request was destroyed at the seal: its 3 s stream was not finished 1.2 s after the meta');
});

test('A_disposer_that_throws_does_not_stop_the_next_and_its_failure_is_in_meta_cleanup', async () => {
  const layout = newSession();
  const order = [];
  const fake = fakeDeps(order, layout, { script: (c, l) => { say(c, goodInit(l), RESULT); c.finish(0); }, closeThrows: 'broker' });
  const result = await launchSession(planFor(layout, { settleMs: 0 }), fake.deps);
  assert.equal(result.ok, true, result.reason);
  assert.deepEqual(order, ['proxy', 'broker', 'receiver'], 'the receiver still closed after the broker\'s failure');
  assert.deepEqual(result.value.meta.cleanup, [{ name: 'broker', error: 'broker close failed' }]);
});

test('A_read_back_mismatch_stops_the_session_through_endCell_and_names_the_field', async () => {
  const layout = newSession();
  const order = [];
  const fake = fakeDeps(order, layout, { script: (c, l) => say(c, { ...goodInit(l), mcp_servers: [{ name: 'x' }] }) });
  const result = await launchSession(planFor(layout, { settleMs: 0 }), fake.deps);
  assert.equal(result.ok, true, result.reason);
  assert.equal(result.value.meta.stopped, 'readback');
  assert.ok(Date.parse(result.value.meta.stoppedAt) >= Date.parse(result.value.meta.startedAt), 'the stop time is in meta, as an ISO instant');
  assert.equal(result.value.meta.outcome.label, 'void: harness');
  const rb = JSON.parse(readFileSync(layout.files.readback, 'utf8'));
  assert.equal(rb.ok, false);
  assert.ok(rb.mismatches.some(m => m.field === 'mcp_servers'));
  assert.equal(JSON.stringify(fake.spawned[0].env).includes('plan'), false);
});

test('The_spawn_env_has_the_dummy_and_never_the_real_token_and_the_argv_has_neither', async () => {
  const layout = newSession();
  const fake = fakeDeps([], layout, { script: (c, l) => { say(c, goodInit(l), RESULT); c.finish(0); } });
  const plan = planFor(layout, { settleMs: 0 });
  const result = await launchSession(plan, fake.deps);
  assert.equal(result.ok, true, result.reason);
  const { argv, env } = fake.spawned[0];
  assert.equal(env.CLAUDE_CODE_OAUTH_TOKEN, plan.dummy);
  assert.equal(JSON.stringify(env).includes(plan.token), false);
  assert.equal(argv.join('\0').includes(plan.token) || argv.join('\0').includes(plan.dummy), false);
  assert.equal(argv.includes('--setenv'), false);
});

test('A_directory_is_paid_work_once_the_broker_forwarded_a_request_even_before_any_stream_byte', () => {
  const made = sessionLayout(tmpDir('claimed'), 'aaaaaa');
  assert.equal(made.ok, true, made.reason);
  const { layout } = { layout: made.value };
  assert.equal(holdsPaidWork(layout), false, 'a claimed layout with nothing in it is unpaid');
  writeFileSync(layout.files.brokerLog, `${JSON.stringify({ seq: 1, event: 'refused', reason: 'path' })}\n`);
  assert.equal(holdsPaidWork(layout), false, 'a refusal never reached the provider: nothing was paid');
  writeFileSync(layout.files.brokerLog, `${JSON.stringify({ seq: 1, event: 'refused', reason: 'path' })}\n${JSON.stringify({ seq: 2, event: 'forwarding' })}\n`);
  assert.equal(holdsPaidWork(layout), true, 'a forwarded request is paid (or reserved) whether or not a stream line was written');
});
