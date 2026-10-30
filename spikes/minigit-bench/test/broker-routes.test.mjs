import assert from 'node:assert/strict';
import net from 'node:net';
import { test } from 'node:test';
import { ALLOWED_ROUTES, routeDecision } from '../lib/broker-routes.mjs';
import { brokerView } from '../lib/broker.mjs';
import { sleep, startBroker, stubUpstream, viaSocket } from './broker-stub.mjs';

const OK = { id: 'msg_2', model: 'claude-sonnet-5-5', usage: { input_tokens: 10, output_tokens: 20 } };
const ok = (_q, res) => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(JSON.stringify(OK)); };

test('The_route_decision_allows_exactly_two_post_paths_with_or_without_a_query', () => {
  for (const url of ['/v1/messages', '/v1/messages?beta=true', '/v1/messages/count_tokens', '/v1/messages/count_tokens?beta=true&x=1']) {
    assert.deepEqual(routeDecision('POST', url), { allow: true }, url);
  }
  assert.deepEqual(ALLOWED_ROUTES, [{ method: 'POST', path: '/v1/messages' }, { method: 'POST', path: '/v1/messages/count_tokens' }]);
});

test('The_route_decision_refuses_every_other_method_path_and_form', () => {
  const rows = [
    ['GET', '/v1/messages'], ['DELETE', '/v1/messages'], ['PUT', '/v1/messages'], ['OPTIONS', '/v1/messages'], ['post', '/v1/messages'],
    ['POST', '/api/oauth/profile'], ['POST', '/v1/files'], ['POST', '/v1/messages/batches'], ['POST', '/v1/messages/'], ['POST', '/v1/messages/count_tokens/'],
    ['POST', '/v1/messagesx'], ['POST', '/V1/messages'], ['POST', '/v1/messages;x=1'], ['POST', '/v1/messages#frag'],
    ['POST', 'http://api.anthropic.com/v1/messages'], ['POST', 'http://evil.example/v1/messages'], ['POST', '//evil.example/v1/messages'], ['POST', 'v1/messages'], ['POST', '*'], ['POST', ''],
    ['POST', '/v1/../v1/messages'], ['POST', '/v1/messages/../files'], ['POST', '/v1/./messages'], ['POST', '/v1/%2e%2e/messages'], ['POST', '/v1%2fmessages'], ['POST', '/v1/messages%2fcount_tokens'], ['POST', '/v1/messages%00'],
    ['POST', '/v1\\messages'], ['POST', '/v1/messages?a=b#c'], ['POST', '/v1/messages?a b'], ['POST', '/v1/messages?é'], ['CONNECT', 'api.anthropic.com:443'],
  ];
  for (const [method, url] of rows) {
    const d = routeDecision(method, url);
    assert.equal(d.allow, false, `${method} ${url}`);
    assert.equal(typeof d.why, 'string');
  }
});

/** The raw request line over the broker's socket, answered status line only. */
const raw = (socketPath, text) => new Promise(resolve => {
  const c = net.connect(socketPath);
  let out = '';
  c.on('data', d => { out += d.toString('latin1'); });
  c.on('close', () => resolve(out.split('\r\n')[0]));
  c.on('error', e => resolve(`ERR ${e.code}`));
  c.write(Buffer.from(text, 'latin1'));
  setTimeout(() => c.end(), 400);
});

test('A_request_off_the_list_is_answered_403_logged_as_path_refused_and_never_forwarded_or_signed', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    for (const [method, p] of [['GET', '/api/oauth/profile'], ['DELETE', '/v1/files/file_x'], ['POST', '/v1/files'], ['POST', '/v1/messages/batches'], ['GET', '/v1/messages']]) {
      const r = await viaSocket(b.socketPath, { method, path: p, body: method === 'GET' ? '' : '{}' });
      assert.equal(r.status, 403, `${method} ${p}`);
      assert.equal(JSON.parse(r.body).type, 'error');
    }
    assert.match(await raw(b.socketPath, 'GET http://evil.example/steal HTTP/1.1\r\nHost: evil.example\r\n\r\n'), /^HTTP\/1\.1 403 /, 'absolute-form');
    assert.match(await raw(b.socketPath, 'POST http://api.anthropic.com/v1/messages HTTP/1.1\r\nHost: x\r\nContent-Length: 2\r\n\r\n{}'), /^HTTP\/1\.1 403 /, 'absolute-form POST of an allowed path');
    await sleep(100);
    assert.equal(stub.seen.length, 0, 'nothing reached the upstream, so nothing was signed');
    const refused = b.lines.filter(l => l.event === 'path-refused');
    assert.equal(refused.length, 7);
    assert.ok(refused.every(l => typeof l.method === 'string' && typeof l.path === 'string' && Array.isArray(l.headerNames)));
    assert.equal(b.lines.some(l => l.event === 'forwarding'), false);
    assert.equal(b.broker.summary().upstreamRequests, 0);
    assert.equal(b.broker.summary().pathRefused, 7);
    assert.equal(b.broker.summary().reservedUsd, 0, 'a path refusal holds no reservation');
    assert.equal(brokerView(b.lines).pathRefused, 7);
    assert.equal(JSON.stringify(b.lines).includes(b.token), false);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('The_two_allowed_paths_are_forwarded_verbatim_with_their_query', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    for (const p of ['/v1/messages?beta=true', '/v1/messages/count_tokens?beta=true', '/v1/messages']) assert.equal((await viaSocket(b.socketPath, { path: p })).status, 200, p);
    assert.deepEqual(stub.seen.map(s => `${s.method} ${s.url}`), ['POST /v1/messages?beta=true', 'POST /v1/messages/count_tokens?beta=true', 'POST /v1/messages']);
    assert.equal(b.lines.filter(l => l.event === 'path-refused').length, 0);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_refused_path_at_zero_budget_is_a_path_refusal_not_a_budget_one', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub, capUsd: 0 });
  try {
    assert.equal((await viaSocket(b.socketPath, { method: 'GET', path: '/api/oauth/profile', body: '' })).status, 403);
    assert.equal((await viaSocket(b.socketPath)).status, 429);
    assert.deepEqual(b.lines.filter(l => l.event === 'path-refused' || l.event === 'refused').map(l => l.event), ['path-refused', 'refused']);
  } finally {
    await b.close();
    await stub.close();
  }
});
