import assert from 'node:assert/strict';
import { test } from 'node:test';
import { brokerView, forwardHeaders, parseSseUsage } from '../lib/broker.mjs';
import { chunksOf, sleep, sseAnswer, sseBody, startBroker, stubUpstream, viaSocket } from './broker-stub.mjs';

const json = (res, status, body, headers = {}) => { res.writeHead(status, { 'content-type': 'application/json', ...headers }); res.end(JSON.stringify(body)); };
const OK_JSON = { id: 'msg_2', model: 'claude-sonnet-5-5', usage: { input_tokens: 10, output_tokens: 20 } };

test('The_broker_signs_with_the_real_token_and_forwards_the_open_list', async () => {
  const stub = await stubUpstream((_q, res) => json(res, 200, OK_JSON));
  const b = await startBroker({ stub });
  try {
    const dummy = 'sk-ant-oat01-dummy-abc123';
    const sent = {
      authorization: `Bearer ${dummy}`, 'x-api-key': 'x', 'anthropic-beta': 'claude-code-20250219,oauth-2025-04-20', 'anthropic-version': '2023-06-01',
      'x-claude-code-session-id': 'sess-1', 'accept-encoding': 'gzip', host: '127.0.0.1:8787',
    };
    const r = await viaSocket(b.socketPath, { headers: sent });
    assert.equal(r.status, 200);
    const seen = stub.seen[0].headers;
    assert.equal(seen.authorization, `Bearer ${b.token}`, 'signed with the real token');
    assert.equal('x-api-key' in seen, false);
    assert.equal(seen['anthropic-beta'], sent['anthropic-beta'], 'byte-equal: the OAuth capability travels in it');
    assert.equal(seen['anthropic-version'], '2023-06-01');
    assert.equal(seen['x-claude-code-session-id'], 'sess-1');
    assert.equal('accept-encoding' in seen, false, 'dropped so the usage parser reads plain SSE');
    assert.equal(seen.host, `127.0.0.1:${stub.upstream.port}`, 'host rewritten to the upstream');
    assert.equal(stub.seen[0].url, '/v1/messages?beta=true', 'method and path verbatim');
    const dump = JSON.stringify(b.lines);
    assert.equal(dump.includes(b.token) || dump.includes(dummy), false, 'a log line names headers and never a value');
    assert.ok(b.lines.find(l => l.event === 'forwarding').headerNames.includes('anthropic-beta'));
  } finally {
    await b.close();
    await stub.close();
  }
});

test('The_broker_streams_and_prices_an_sse_response', async () => {
  const writes = [];
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 5), 50, writes));
  const b = await startBroker({ stub });
  try {
    let firstAt = 0;
    const r = await viaSocket(b.socketPath, { onFirstChunk: t => { firstAt = t; } });
    assert.equal(r.status, 200);
    assert.equal(r.body, sseBody(), 'the client received the stream intact');
    assert.ok(firstAt < writes.at(-1), `streaming, not buffering: the first chunk (${firstAt}) arrived before the last was sent (${writes.at(-1)})`);
    const settled = b.lines.find(l => l.event === 'settled');
    assert.equal(settled.usage.input_tokens, 2);
    assert.equal(settled.usage.output_tokens, 4, 'the message_delta output overrides the message_start placeholder');
    assert.equal(settled.usage.cache_creation.ephemeral_1h_input_tokens, 2297);
    assert.equal(settled.chargedUsd, 0.009232);
    assert.equal(settled.model, 'claude-sonnet-5-5');
    assert.equal(settled.clientClosedEarly, false);
    assert.equal(b.broker.summary().spentUsd, 0.009232);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_json_response_is_priced_from_its_usage_block', async () => {
  const stub = await stubUpstream((_q, res) => json(res, 200, OK_JSON));
  const b = await startBroker({ stub });
  try {
    await viaSocket(b.socketPath);
    assert.equal(b.lines.find(l => l.event === 'settled').chargedUsd, (10 * 2 + 20 * 10) / 1e6);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('The_broker_follows_no_redirect', async () => {
  const stub = await stubUpstream((_q, res) => { res.writeHead(302, { location: 'https://evil.example/' }); res.end(); });
  const b = await startBroker({ stub });
  try {
    const r = await viaSocket(b.socketPath);
    assert.equal(r.status, 502);
    assert.equal(stub.seen.length, 1, 'the stub saw one request: the redirect was not followed');
    assert.ok(b.lines.some(l => l.event === 'redirect-refused'));
    assert.equal(JSON.stringify(b.lines).includes('evil.example'), true, 'the refused location is logged (it is not a secret)');
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_direct_request_at_zero_budget_leaves_the_upstream_count_unchanged', async () => {
  const stub = await stubUpstream((_q, res) => json(res, 200, OK_JSON));
  const b = await startBroker({ stub, capUsd: 0 });
  try {
    const r1 = await viaSocket(b.socketPath, { body: '{"model":"claude-sonnet-4-5","max_tokens":5,"messages":[{"role":"user","content":"hi"}]}' });
    const r2 = await viaSocket(b.socketPath, { path: '/v1/messages' });
    for (const r of [r1, r2]) {
      assert.equal(r.status, 429);
      const body = JSON.parse(r.body);
      assert.equal(body.type, 'error');
      assert.equal(body.error.type, 'rate_limit_error');
      assert.match(body.error.message, /budget/);
    }
    assert.equal(stub.seen.length, 0, 'the upstream saw nothing');
    assert.deepEqual({ forwarded: brokerView(b.lines).forwarded, refused: brokerView(b.lines).refused }, { forwarded: 0, refused: 2 });
    assert.equal(b.broker.summary().upstreamRequests, 0);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('Three_concurrent_requests_hold_three_reservations', async () => {
  const release = [];
  const stub = await stubUpstream((_q, res) => release.push(() => json(res, 200, OK_JSON)));
  const b = await startBroker({ stub, capUsd: 0.12, reserveUsd: 0.05 });
  try {
    const first = [viaSocket(b.socketPath), viaSocket(b.socketPath)];
    await sleep(150);
    assert.equal(release.length, 2, 'two admitted and held');
    const third = await viaSocket(b.socketPath);
    assert.equal(third.status, 429, 'the third is refused while two are in flight');
    release.splice(0).forEach(f => f());
    await Promise.all(first);
    await sleep(50);
    const fourth = viaSocket(b.socketPath);
    await sleep(150);
    assert.equal(release.length, 1, 'after both settled (each below the reserve) a fourth is admitted');
    release.splice(0).forEach(f => f());
    assert.equal((await fourth).status, 200);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_client_that_disconnects_is_still_charged', async () => {
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 6), 80));
  const b = await startBroker({ stub });
  try {
    await viaSocket(b.socketPath, { abortAfterFirstChunk: true });
    for (let i = 0; i < 60 && !b.lines.some(l => l.event === 'settled'); i += 1) await sleep(50);
    const settled = b.lines.find(l => l.event === 'settled');
    assert.ok(settled, 'the broker read the upstream to its end and settled');
    assert.equal(settled.clientClosedEarly, true);
    assert.equal(settled.chargedUsd, 0.009232, 'the body was read to its end, so it is priced in full');
    assert.equal(stub.seen.length, 1);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('Nothing_parsed_is_charged_the_reservation_never_zero', async () => {
  const stub = await stubUpstream((_q, res) => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(''); });
  const b = await startBroker({ stub, reserveUsd: 0.05 });
  try {
    await viaSocket(b.socketPath);
    const settled = b.lines.find(l => l.event === 'settled');
    assert.equal(settled.chargedUsd, 0.05);
    assert.equal(settled.priced.known, false);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('An_upstream_that_fails_answers_502_and_settles_the_reservation', async () => {
  const stub = await stubUpstream((_q, res) => res.destroy());
  const b = await startBroker({ stub });
  try {
    const r = await viaSocket(b.socketPath);
    assert.equal(r.status, 502);
    assert.ok(b.lines.some(l => l.event === 'upstream-error'));
    assert.equal(b.broker.summary().inFlight, 0, 'nothing is left reserved');
    assert.equal(b.broker.summary().spentUsd, 0.05);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('The_socket_is_private_and_close_removes_it', async () => {
  const stub = await stubUpstream((_q, res) => json(res, 200, OK_JSON));
  const b = await startBroker({ stub });
  const { statSync, existsSync } = await import('node:fs');
  assert.equal(statSync(b.socketPath).mode & 0o777, 0o600);
  await b.close();
  await b.close();
  assert.equal(existsSync(b.socketPath), false, 'closed, the socket is gone; closing twice is a no-op');
  await stub.close();
});

test('forwardHeaders_drops_the_closed_list_and_rewrites_the_host', () => {
  const out = forwardHeaders({ authorization: 'a', 'x-api-key': 'k', host: 'h', connection: 'c', 'content-length': '1', 'transfer-encoding': 't', 'accept-encoding': 'g', 'anthropic-beta': 'b', 'x-new-header': 'n' }, 'api.anthropic.com');
  assert.deepEqual(out, { 'anthropic-beta': 'b', 'x-new-header': 'n', host: 'api.anthropic.com' });
});

test('The_sse_usage_feeder_handles_events_split_across_chunks_and_crlf', () => {
  const f = parseSseUsage();
  const body = sseBody().replaceAll('\n', '\r\n');
  for (let i = 0; i < body.length; i += 7) f.feed(Buffer.from(body.slice(i, i + 7)));
  assert.equal(f.result().model, 'claude-sonnet-5-5');
  assert.equal(f.result().usage.output_tokens, 4);
  assert.deepEqual(parseSseUsage().result(), { model: null, usage: null });
  const junk = parseSseUsage();
  junk.feed('data: {not json}\n\ndata: [DONE]\n\n');
  assert.deepEqual(junk.result(), { model: null, usage: null });
});

test('brokerView_reads_the_ledger_lines', () => {
  const lines = [
    { event: 'forwarding', seq: 1 }, { event: 'settled', seq: 1, model: 'm', usage: { a: 1 }, chargedUsd: 0.1, status: 200, clientClosedEarly: false },
    { event: 'refused', seq: 2 }, { event: 'forwarding', seq: 3 }, { event: 'summary' },
  ];
  const v = brokerView(lines);
  assert.equal(v.forwarded, 2);
  assert.equal(v.refused, 1);
  assert.equal(v.spentUsd, 0.1);
  assert.equal(v.unsettled, 1, 'seq 3 never settled');
  assert.deepEqual(v.settled, [{ seq: 1, model: 'm', usage: { a: 1 }, chargedUsd: 0.1, status: 200, clientClosedEarly: false }]);
  assert.deepEqual(brokerView([]), { present: false, forwarded: 0, refused: 0, pathRefused: 0, unsettled: 0, spentUsd: 0, settled: [] });
});
