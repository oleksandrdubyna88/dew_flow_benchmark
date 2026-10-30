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
    assert.deepEqual(b.lines.filter(l => l.event === 'refused').map(l => l.reason), ['cap', 'cap'], 'a budget refusal says so');
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

test('The_reservation_grows_to_the_largest_charge_settled_so_far_and_does_not_read_max_tokens', async () => {
  const big = { id: 'm', model: 'claude-sonnet-5-5', usage: { input_tokens: 0, output_tokens: 7000 } }; // 0.07
  const release = [];
  let answerBig = true;
  const stub = await stubUpstream((_q, res) => { if (answerBig) { answerBig = false; json(res, 200, big); } else release.push(() => json(res, 200, OK_JSON)); });
  const b = await startBroker({ stub, capUsd: 0.2, reserveUsd: 0.05 });
  try {
    const huge = JSON.stringify({ model: 'claude-opus-5-5', max_tokens: 128000, messages: [] });
    assert.equal((await viaSocket(b.socketPath, { body: huge })).status, 200);
    assert.equal(b.lines.find(l => l.event === 'forwarding').reserveUsd, 0.05, 'a 128k max_tokens does not raise the first reservation');
    const held = viaSocket(b.socketPath, { body: huge });
    await sleep(150);
    assert.equal(release.length, 1, 'the second request is admitted: spent 0.07 + its reservation 0.07 = 0.14');
    assert.deepEqual(b.lines.filter(l => l.event === 'forwarding').map(l => l.reserveUsd), [0.05, 0.07], 'it reserved what the dearest settled request cost');
    assert.equal((await viaSocket(b.socketPath, { body: huge })).status, 429, 'a third in flight would be 0.07 + 0.07 + 0.07 over the 0.2 cap');
    release.splice(0).forEach(f => f());
    await held;
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

test('An_upstream_that_drops_the_connection_after_the_request_was_sent_answers_502_and_keeps_the_reservation_as_the_charge', async () => {
  // the request reached the upstream whole, so the API may have started billing it: the reservation stands (it is not a dial failure)
  const stub = await stubUpstream((_q, res) => res.destroy());
  const b = await startBroker({ stub });
  try {
    const r = await viaSocket(b.socketPath);
    assert.equal(r.status, 502);
    assert.ok(b.lines.some(l => l.event === 'upstream-error'));
    assert.equal(b.broker.summary().inFlight, 0, 'nothing is left reserved');
    assert.equal(b.broker.summary().spentUsd, 0.05);
    assert.equal(b.lines.find(l => l.event === 'settled').priced.known, false);
  } finally {
    await b.close();
    await stub.close();
  }
});

/** An SSE `message_start` event carrying `usage` for `model`. */
const messageStart = (model, usage) => `event: message_start\ndata: ${JSON.stringify({ type: 'message_start', message: { model, usage } })}\n\n`;

test('A_stream_cut_after_message_start_is_charged_the_usage_already_parsed_never_less_than_the_reservation', async () => {
  // the reviewer's probe: 200k Sonnet input tokens announced, then the connection is reset. The API bills what it read: 200k x the input rate
  // is far above the 0.05 reservation, and throwing the parsed usage away undercounted the cell.
  const stub = await stubUpstream((_q, res) => { res.writeHead(200, { 'content-type': 'text/event-stream' }); res.write(messageStart('claude-sonnet-5-5', { input_tokens: 200000, output_tokens: 1 })); setTimeout(() => res.socket.destroy(), 60); });
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    await viaSocket(b.socketPath).catch(() => {});
    await b.broker.drain(3000);
    const settled = b.lines.filter(l => l.event === 'settled');
    assert.equal(settled.length, 1);
    assert.equal(settled[0].priced.known, true, 'priced from the usage already parsed');
    assert.ok(settled[0].chargedUsd >= 0.4, `charged ${settled[0].chargedUsd}: 200k Sonnet input tokens are at least 0.40`);
    assert.equal(b.broker.summary().spentUsd, settled[0].chargedUsd);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_stream_cut_after_a_small_message_start_is_charged_at_least_the_reservation_and_a_cut_with_an_unknown_model_at_the_dearest_rates', async () => {
  const cut = model => async () => {
    const stub = await stubUpstream((_q, res) => { res.writeHead(200, { 'content-type': 'text/event-stream' }); res.write(messageStart(model, { input_tokens: 10, output_tokens: 1 })); setTimeout(() => res.socket.destroy(), 60); });
    const b = await startBroker({ stub, capUsd: 10 });
    try {
      await viaSocket(b.socketPath).catch(() => {});
      await b.broker.drain(3000);
      return b.lines.find(l => l.event === 'settled');
    } finally {
      await b.close();
      await stub.close();
    }
  };
  const small = await cut('claude-sonnet-5-5')();
  assert.equal(small.priced.known, true);
  assert.equal(small.chargedUsd, 0.05, 'ten tokens cost a fraction of a cent: the reservation is the floor');
  const unknown = await cut('claude-not-in-the-table-9')();
  assert.equal(unknown.priced.known, false);
  assert.equal(unknown.priced.why, 'unpriced model: dearest known rates');
  assert.ok(unknown.chargedUsd >= 0.05);
});

test('An_upstream_that_cannot_be_dialled_answers_502_and_charges_nothing_because_no_byte_was_sent', async () => {
  const stub = await stubUpstream(ok => ok);
  await stub.close();
  const b = await startBroker({ stub: { ...stub, upstream: { host: '127.0.0.1', port: 1, tls: false } } });
  try {
    for (let i = 0; i < 6; i += 1) assert.equal((await viaSocket(b.socketPath)).status, 502);
    assert.equal(b.broker.summary().spentUsd, 0, 'six refused dials cost nothing');
    assert.equal(b.broker.summary().inFlight, 0);
    assert.equal(b.broker.summary().reservedUsd, 0);
    assert.equal(b.broker.summary().refused, 0, 'and the cap was never used up');
    const settled = b.lines.filter(l => l.event === 'settled');
    assert.equal(settled.length, 6);
    assert.deepEqual(settled[0].priced, { known: true, usd: 0, why: 'no byte was sent upstream' });
  } finally {
    await b.close();
  }
});

test('Four_upstream_529s_do_not_use_the_cap_and_the_fifth_request_is_still_forwarded', async () => {
  const overloaded = { type: 'error', error: { type: 'overloaded_error', message: 'Overloaded' } };
  const stub = await stubUpstream((_q, res) => json(res, 529, overloaded));
  const b = await startBroker({ stub, capUsd: 0.2, reserveUsd: 0.05 });
  try {
    for (let i = 0; i < 5; i += 1) assert.equal((await viaSocket(b.socketPath)).status, 529, `request ${i + 1} was forwarded and answered 529`);
    await sleep(50);
    assert.equal(b.broker.summary().spentUsd, 0, 'an error the API refused to bill is charged nothing');
    assert.equal(b.broker.summary().refused, 0);
    assert.equal(stub.seen.length, 5);
    assert.deepEqual(b.lines.find(l => l.event === 'settled').priced, { known: true, usd: 0, why: 'upstream error' });
    assert.equal(b.lines.find(l => l.event === 'settled').status, 529);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('An_error_status_without_a_json_error_body_and_a_2xx_with_nothing_parsed_keep_the_reservation', async () => {
  const bodies = [
    [500, 'text/html', '<html>bad gateway</html>'],
    [500, 'application/json', '{"message":"no type field"}'],
    [200, 'application/json', '{"type":"error","error":{"type":"x"}}'],
    [200, 'text/event-stream', 'event: ping\ndata: {"type":"ping"}\n\n'],
  ];
  for (const [status, type, body] of bodies) {
    const stub = await stubUpstream((_q, res) => { res.writeHead(status, { 'content-type': type }); res.end(body); });
    const b = await startBroker({ stub });
    try {
      await viaSocket(b.socketPath);
      assert.equal(b.broker.summary().spentUsd, 0.05, `${status} ${type} ${body.slice(0, 20)}`);
    } finally {
      await b.close();
      await stub.close();
    }
  }
});

test('Drain_waits_for_a_request_the_client_walked_away_from_until_the_broker_has_read_the_upstream_to_its_end', async () => {
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 6), 120));
  const b = await startBroker({ stub });
  try {
    await viaSocket(b.socketPath, { abortAfterFirstChunk: true });
    assert.equal(b.broker.summary().inFlight, 1, 'the client is gone and the upstream is still streaming: the request is in flight');
    assert.equal(b.lines.some(l => l.event === 'settled'), false, 'not settled yet: a collector that wrote broker.json now would record 0 spent');
    const drained = await b.broker.drain(5000);
    assert.deepEqual([drained.drained, drained.inFlight, drained.reservedUsd], [true, 0, 0]);
    assert.equal(b.broker.summary().spentUsd, 0.009232, 'the stream was read to its end and priced in full');
    assert.equal(b.lines.find(l => l.event === 'settled').clientClosedEarly, true);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('Drain_returns_at_once_when_nothing_is_in_flight_and_at_the_ceiling_when_something_never_ends', async () => {
  const stub = await stubUpstream((_q, res) => { res.writeHead(200, { 'content-type': 'text/event-stream' }); res.write('event: ping\ndata: {"type":"ping"}\n\n'); });
  const b = await startBroker({ stub });
  try {
    const idle = Date.now();
    assert.equal((await b.broker.drain(5000)).drained, true);
    assert.ok(Date.now() - idle < 500, 'an idle broker drains immediately');
    viaSocket(b.socketPath, { abortAfterFirstChunk: true }).catch(() => {});
    await sleep(150);
    const started = Date.now();
    const stuck = await b.broker.drain(300);
    assert.equal(stuck.drained, false);
    assert.deepEqual([stuck.inFlight, stuck.reservedUsd], [1, 0.05], 'the unsettled request and its reservation are reported');
    assert.ok(Date.now() - started >= 290 && Date.now() - started < 2000, `it waited the ceiling and no longer (${Date.now() - started} ms)`);
  } finally {
    await b.close();
    await stub.close();
  }
});

/** A stub answer that streams slowly and records when its response is closed (by the broker destroying the request, or by its own end). */
const slowStream = (marks, gapMs = 400) => async (_q, res) => {
  res.on('close', () => { marks.closedAt = Date.now(); });
  res.writeHead(200, { 'content-type': 'text/event-stream' });
  for (const c of chunksOf(sseBody(), 6)) { if (res.destroyed) return; res.write(c); await sleep(gapMs); }
  res.end();
};

test('Sealing_after_the_drain_limit_reports_settled_plus_reservations_as_a_lower_bound_destroys_the_upstream_request_and_writes_no_ledger_line_after', async () => {
  const marks = {};
  const stub = await stubUpstream(slowStream(marks));
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    await viaSocket(b.socketPath, { abortAfterFirstChunk: true });
    const drained = await b.broker.drain(250);
    assert.equal(drained.drained, false, 'the limit passed with the request unsettled');
    const sealedAt = Date.now();
    const sealed = b.broker.seal();
    assert.deepEqual(sealed.unsettled, { count: 1, reservedUsd: 0.05 });
    assert.equal(sealed.lowerBound, true);
    assert.equal(sealed.summary.spentUsd, 0.05, 'B is the settled spend (0) plus the unsettled reservation');
    const written = b.lines.length;
    assert.equal(b.lines.at(-1).event, 'summary', 'the summary is the last line the ledger writes');
    await sleep(900);
    assert.ok(marks.closedAt !== undefined && marks.closedAt - sealedAt < 300, 'the upstream request was destroyed at the seal, not read to its end');
    assert.equal(b.lines.length, written, 'no ledger line after the seal: the late settlement is silent');
    assert.deepEqual(b.broker.seal(), sealed, 'sealing twice is the same answer');
  } finally {
    await b.close();
    await stub.close();
  }
});

test('Close_destroys_an_in_flight_upstream_request_and_a_clean_seal_is_not_a_lower_bound', async () => {
  const marks = {};
  const stub = await stubUpstream(slowStream(marks, 500));
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    await viaSocket(b.socketPath, { abortAfterFirstChunk: true });
    const closedAt = Date.now();
    await b.close();
    await sleep(300);
    assert.ok(marks.closedAt !== undefined && marks.closedAt - closedAt < 300, 'closing the broker ended the upstream request at once');
    assert.equal(b.lines.some(l => l.event === 'settled' && Date.parse(l.t) > closedAt), false, 'and no settlement line was written after the close');
  } finally {
    await stub.close();
  }
  const idle = await stubUpstream((_q, res) => json(res, 200, OK_JSON));
  const clean = await startBroker({ stub: idle });
  try {
    await viaSocket(clean.socketPath);
    const sealed = clean.broker.seal();
    assert.deepEqual([sealed.lowerBound, sealed.unsettled], [false, { count: 0, reservedUsd: 0 }]);
    assert.equal(sealed.summary.spentUsd, clean.broker.summary().spentUsd, 'with nothing unsettled B is the settled spend');
  } finally {
    await clean.close();
    await idle.close();
  }
});

test('A_response_from_a_model_the_table_does_not_know_is_charged_at_the_dearest_rates_and_the_cell_record_says_so', async () => {
  const unknown = { id: 'm', model: 'claude-unknown-9', usage: { input_tokens: 1000, output_tokens: 1000 } }; // dearest: 1000 x 10 + 1000 x 50 = 0.06
  const stub = await stubUpstream((_q, res) => json(res, 200, unknown));
  const b = await startBroker({ stub, capUsd: 1 });
  try {
    await viaSocket(b.socketPath);
    const settled = b.lines.find(l => l.event === 'settled');
    assert.equal(settled.chargedUsd, 0.06);
    assert.deepEqual(settled.priced, { known: false, usd: 0.06, model: 'claude-unknown-9', why: 'unpriced model: dearest known rates' });
    const view = brokerView(b.lines);
    assert.deepEqual(view.unpricedModels, ['claude-unknown-9'], 'the cell record names every model that was not priced');
    assert.equal(view.settled[0].unpriced, true);
    assert.deepEqual(brokerView([]).unpricedModels, []);
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

test('forwardHeaders_drops_every_hop_by_hop_header_and_every_name_the_connection_header_lists', () => {
  const hop = { upgrade: 'h2c', te: 'trailers', 'keep-alive': 'timeout=5', 'proxy-connection': 'keep-alive', trailer: 'x', 'http2-settings': 'AAMAAABk', connection: 'close' };
  const out = forwardHeaders({ ...hop, 'anthropic-beta': 'b', 'x-private': 'p', 'X-Other': 'o', 'x-kept': 'k' }, 'h');
  assert.deepEqual(out, { 'anthropic-beta': 'b', 'x-private': 'p', 'X-Other': 'o', 'x-kept': 'k', host: 'h' }, 'no hop header: control, the end-to-end ones stay');
  const named = forwardHeaders({ connection: 'Keep-Alive, X-Private , x-other', 'x-private': 'p', 'X-Other': 'o', 'x-kept': 'k', 'anthropic-beta': 'b' }, 'h');
  assert.deepEqual(named, { 'x-kept': 'k', 'anthropic-beta': 'b', host: 'h' }, 'a header the Connection header names is hop-by-hop for that message: dropped, whatever its case');
});

test('An_upgrade_request_reaches_the_upstream_without_the_upgrade_headers', async () => {
  const stub = await stubUpstream((_q, res) => json(res, 200, OK_JSON));
  const b = await startBroker({ stub });
  try {
    const r = await viaSocket(b.socketPath, { headers: { connection: 'Upgrade, HTTP2-Settings, x-private', upgrade: 'h2c', 'http2-settings': 'AAMAAABk', te: 'trailers', 'x-private': 'p' } });
    assert.equal(r.status, 200);
    const seen = stub.seen[0].headers;
    for (const name of ['upgrade', 'http2-settings', 'te', 'x-private', 'keep-alive', 'proxy-connection', 'trailer']) assert.equal(name in seen, false, `${name} was not forwarded`);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_count_tokens_answer_is_free_and_the_ledger_says_why_while_a_messages_answer_with_the_same_body_is_not', async () => {
  const stub = await stubUpstream((_q, res) => json(res, 200, { input_tokens: 1234 }));
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    assert.equal((await viaSocket(b.socketPath, { path: '/v1/messages/count_tokens?beta=true' })).status, 200);
    const line = b.lines.find(l => l.event === 'settled');
    assert.equal(line.chargedUsd, 0, 'count_tokens is not billed');
    assert.deepEqual(line.priced, { known: true, usd: 0, why: 'count_tokens is free' });
    assert.equal(b.broker.summary().spentUsd, 0);
    assert.equal(b.broker.summary().reservedUsd, 0);
    // control: the same answer from /v1/messages carries no usage and keeps the reservation
    assert.equal((await viaSocket(b.socketPath, { path: '/v1/messages' })).status, 200);
    assert.equal(b.broker.summary().spentUsd, 0.05);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_client_that_resets_mid_write_cannot_crash_the_broker_and_the_request_is_still_charged', async () => {
  const stub = await stubUpstream(async (_q, res) => { res.writeHead(200, { 'content-type': 'text/event-stream' }); for (const c of chunksOf(sseBody(), 6)) { res.write(c); await sleep(40); } res.end(); });
  const b = await startBroker({ stub, capUsd: 10 });
  const crashes = [];
  const onCrash = e => crashes.push(e.message);
  process.on('uncaughtException', onCrash);
  try {
    // Node emits `error` on a ServerResponse written after its socket was destroyed but before it closed: forced here on every response, a window
    // the broker's own `close` flag does not cover
    b.broker.server.on('request', (_req, res) => { setTimeout(() => res.emit('error', Object.assign(new Error('write EPIPE'), { code: 'EPIPE' })), 70); });
    await viaSocket(b.socketPath, { abortAfterFirstChunk: true });
    await viaSocket(b.socketPath, { abortAfterFirstChunk: true });
    const drained = await b.broker.drain(3000);
    assert.equal(drained.drained, true, 'both requests were read to their end');
    assert.deepEqual(crashes, [], 'an error event on the response is handled, not thrown');
    assert.equal(b.lines.filter(l => l.event === 'settled').length, 2, 'and both were still settled');
    assert.equal(b.broker.summary().spentUsd, 0.018464, 'priced in full, as before');
  } finally {
    process.off('uncaughtException', onCrash);
    await b.close();
    await stub.close();
  }
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
    { event: 'refused', seq: 2, reason: 'cap' }, { event: 'refused', seq: 4, reason: 'path' }, { event: 'refused', seq: 5, reason: 'path' }, { event: 'forwarding', seq: 3 }, { event: 'summary' },
  ];
  const v = brokerView(lines);
  assert.equal(v.forwarded, 2);
  assert.equal(v.refused, 3);
  assert.deepEqual(v.refusedByReason, { cap: 1, path: 2, body: 0 });
  assert.equal(v.spentUsd, 0.1);
  assert.equal(v.unsettled, 1, 'seq 3 never settled');
  assert.deepEqual(v.settled, [{ seq: 1, model: 'm', usage: { a: 1 }, chargedUsd: 0.1, status: 200, clientClosedEarly: false }]);
  assert.deepEqual(brokerView([]), { present: false, forwarded: 0, refused: 0, refusedByReason: { cap: 0, path: 0, body: 0 }, unpricedModels: [], unsettled: 0, spentUsd: 0, settled: [] });
});
