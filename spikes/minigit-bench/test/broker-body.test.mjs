// The broker's body check: the broker signs what it forwards, so it reads the JSON body first and refuses a request that makes the API reach the
// network on the agent's behalf (a server tool, a connector, a container, a URL source). Pure unit tests here; the wire tests are in the
// second half of this file (a stub upstream sees nothing of a refused request).
import assert from 'node:assert/strict';
import net from 'node:net';
import { test } from 'node:test';
import { MAX_BODY_BYTES, bodyDecision, encodingDecision } from '../lib/broker-body.mjs';
import { sleep, startBroker, stubUpstream, viaSocket } from './broker-stub.mjs';

const decide = value => bodyDecision(Buffer.from(typeof value === 'string' ? value : JSON.stringify(value), 'utf8'));
const CUSTOM = { name: 'Bash', description: 'run', input_schema: { type: 'object', properties: {} } };

test('The_body_check_allows_the_clients_own_tools_and_plain_content', () => {
  const allowed = [
    {},
    { model: 'claude-opus-5-5', max_tokens: 10, messages: [{ role: 'user', content: 'hi' }] },
    { tools: [] },
    { tools: [CUSTOM, { ...CUSTOM, type: 'custom' }] },
    { messages: [{ role: 'user', content: [{ type: 'image', source: { type: 'base64', media_type: 'image/png', data: 'AAAA' } }, { type: 'text', text: 'container mcp_servers web_search' }] }] },
    { system: [{ type: 'text', text: 'x' }], tool_choice: { type: 'auto' }, thinking: { type: 'enabled', budget_tokens: 1024 }, metadata: { user_id: 'u' } },
  ];
  for (const body of allowed) assert.deepEqual(decide(body), { allow: true }, JSON.stringify(body).slice(0, 80));
});

test('The_body_check_refuses_every_server_tool_type_a_connector_a_container_and_a_url_source', () => {
  const server = ['web_search_20250305', 'web_fetch_20250910', 'code_execution_20250522', 'computer_20250124', 'bash_20250124', 'text_editor_20250728', 'memory_20250818', 'tool_search_tool_regex_20251119', 'anything_new_2027'];
  for (const type of server) {
    const d = decide({ tools: [CUSTOM, { type, name: 'x' }] });
    assert.equal(d.allow, false, type);
    assert.match(d.offending, new RegExp(`tools\\[1\\]\\.type=${type}`), 'names the tool index and the type');
  }
  const rows = {
    'mcp_servers': { mcp_servers: [{ type: 'url', url: 'https://evil.example/mcp', name: 'x' }] },
    'container': { container: 'container_abc' },
    'a null type': { tools: [{ type: null, name: 'x' }] },
    'a tool that is not an object': { tools: ['web_search'] },
    'tools that is not a list': { tools: { type: 'web_search_20250305' } },
    'an image url source': { messages: [{ role: 'user', content: [{ type: 'image', source: { type: 'url', url: 'https://evil.example/?d=secret' } }] }] },
    'a document url source nested in a tool result': { messages: [{ role: 'user', content: [{ type: 'tool_result', tool_use_id: 't', content: [{ type: 'document', source: { type: 'url', url: 'https://evil.example/x.pdf' } }] }] }] },
    'a url source in the system prompt': { system: [{ type: 'image', source: { type: 'url', url: 'https://evil.example/x' } }] },
  };
  for (const [name, body] of Object.entries(rows)) {
    const d = decide(body);
    assert.equal(d.allow, false, name);
    assert.equal(typeof d.why, 'string');
    assert.equal(JSON.stringify(d).includes('evil.example'), false, `${name}: the refusal never carries the body's values`);
  }
});

test('The_body_check_refuses_a_body_two_parsers_could_read_differently', () => {
  const rows = {
    'not JSON': '{"tools": [',
    'JSON that is not an object': '[]',
    'an empty body': '',
    'a duplicate key (the last wins here, the first may win there)': '{"tools":[{"type":"web_search_20250305","name":"x"}],"tools":[]}',
    'a duplicate key written with an escape': '{"tools":[],"t\\u006fols":[{"type":"bash_20250124"}]}',
    'a duplicate key in a nested object': '{"messages":[{"role":"user","role":"assistant","content":"x"}]}',
  };
  for (const [name, text] of Object.entries(rows)) assert.equal(decide(text).allow, false, name);
  assert.equal(bodyDecision(Buffer.from([0x7b, 0x22, 0xff, 0x22, 0x3a, 0x31, 0x7d])).allow, false, 'bytes that are not UTF-8');
  assert.equal(decide('{"a":{"x":1},"b":{"x":2},"c":[{"x":1},{"x":1}]}').allow, true, 'the same key in different objects is not a duplicate');
  assert.equal(decide('{"a":"tools","b":"tools"}').allow, true, 'equal values are not duplicate keys');
});

test('A_body_with_a_content_encoding_cannot_be_inspected_and_is_refused', () => {
  assert.deepEqual(encodingDecision({}), { allow: true });
  assert.deepEqual(encodingDecision({ 'content-encoding': 'identity' }), { allow: true });
  for (const enc of ['gzip', 'br', 'deflate', 'zstd', 'gzip, identity']) assert.equal(encodingDecision({ 'content-encoding': enc }).allow, false, enc);
  assert.equal(MAX_BODY_BYTES, 8 * 1024 * 1024);
});

const OK = { id: 'msg_2', model: 'claude-sonnet-5-5', usage: { input_tokens: 10, output_tokens: 20 } };
const ok = (_q, res) => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(JSON.stringify(OK)); };
const SECRET = 'BODY-MARKER-9f3a';

test('A_request_with_a_web_search_tool_is_answered_403_with_reason_body_and_nothing_is_forwarded_signed_or_reserved', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    const body = JSON.stringify({ model: 'claude-opus-5-5', max_tokens: 5, messages: [{ role: 'user', content: SECRET }], tools: [{ type: 'web_search_20250305', name: 'web_search', max_uses: 5 }] });
    for (const path of ['/v1/messages?beta=true', '/v1/messages/count_tokens']) {
      const r = await viaSocket(b.socketPath, { path, body });
      assert.equal(r.status, 403, path);
      assert.equal(r.body.includes(SECRET), false, 'the answer does not echo the body');
    }
    assert.equal(stub.seen.length, 0, 'the upstream saw nothing');
    const refused = b.lines.filter(l => l.event === 'refused');
    assert.equal(refused.length, 2);
    assert.equal(refused[0].reason, 'body');
    assert.match(refused[0].offending, /tools\[0\]\.type=web_search_20250305/);
    assert.equal(JSON.stringify(b.lines).includes(SECRET), false, 'a log line never carries the body');
    assert.deepEqual([b.broker.summary().inFlight, b.broker.summary().reservedUsd, b.broker.summary().spentUsd], [0, 0, 0], 'a refused body held no reservation');
    assert.deepEqual(b.broker.summary().refusedByReason, { cap: 0, path: 0, body: 2, busy: 0 });
  } finally {
    await b.close();
    await stub.close();
  }
});

test('The_exact_bytes_read_are_the_bytes_forwarded_with_their_own_length', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub });
  try {
    const body = '{ "model":"claude-opus-5-5" ,\n "messages":[{"role":"user","content":"héllo ☃  "}],  "tools":[{"name":"Bash","type":"custom","input_schema":{}}] }';
    assert.equal((await viaSocket(b.socketPath, { body })).status, 200);
    assert.equal(stub.seen[0].body, body, 'byte for byte, whitespace and all');
    assert.equal(stub.seen[0].headers['content-length'], String(Buffer.byteLength(body)));
    assert.equal('transfer-encoding' in stub.seen[0].headers, false);
  } finally {
    await b.close();
    await stub.close();
  }
});

test('An_oversize_body_and_an_encoded_body_and_a_body_that_is_not_json_are_refused_with_reason_body', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub, capUsd: 10 });
  try {
    const big = `{"a":"${'x'.repeat(MAX_BODY_BYTES)}"}`;
    const huge = await viaSocket(b.socketPath, { body: big });
    assert.equal(huge.status, 413);
    assert.equal((await viaSocket(b.socketPath, { body: '{}', headers: { 'content-encoding': 'gzip' } })).status, 403);
    assert.equal((await viaSocket(b.socketPath, { body: 'not json' })).status, 403);
    assert.equal(stub.seen.length, 0);
    assert.deepEqual(b.lines.filter(l => l.event === 'refused').map(l => l.reason), ['body', 'body', 'body']);
    assert.equal(b.broker.summary().inFlight, 0);
  } finally {
    await b.close();
    await stub.close();
  }
});

/** A client that sends the head of a POST declaring `declared` body bytes and then only `sent` of them: it holds its body open until `finish()`. */
function slowBody(socketPath, declared, sent) {
  const c = net.connect(socketPath);
  let answer = '';
  c.on('data', d => { answer += d.toString('latin1'); });
  c.on('error', () => {});
  const body = `{"pad":"${'x'.repeat(declared - 10)}"}`; // valid JSON of exactly `declared` bytes
  c.write(`POST /v1/messages HTTP/1.1\r\nHost: x\r\nContent-Type: application/json\r\nContent-Length: ${declared}\r\n\r\n${body.slice(0, sent)}`);
  return { finish: () => c.write(body.slice(sent)), close: () => c.destroy(), answer: () => answer };
}

test('The_bytes_buffered_across_concurrent_requests_are_bounded_and_one_over_the_bound_is_answered_503_busy_with_nothing_reserved', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub, capUsd: 10, maxBufferedBytes: 1000 });
  try {
    const first = slowBody(b.socketPath, 800, 300); // declares 800 and holds its body open: 800 bytes of the 1000 are spoken for
    await sleep(100);
    const second = await viaSocket(b.socketPath, { body: JSON.stringify({ pad: 'y'.repeat(300) }) });
    assert.equal(second.status, 503, 'a second body that does not fit is refused at once, not queued');
    assert.match(second.body, /busy/);
    assert.equal(stub.seen.length, 0, 'nothing was forwarded');
    const refused = b.lines.filter(l => l.event === 'refused');
    assert.deepEqual(refused.map(l => l.reason), ['busy']);
    assert.deepEqual(b.broker.summary().refusedByReason, { cap: 0, path: 0, body: 0, busy: 1 });
    assert.deepEqual([b.broker.summary().inFlight, b.broker.summary().reservedUsd], [0, 0], 'a busy refusal holds no reservation (and the first request holds none yet)');
    // a small one still fits beside the first
    assert.equal((await viaSocket(b.socketPath, { body: '{}' })).status, 200, 'the bound is on bytes, not on requests');
    // the first request completes and is forwarded; its bytes are released when it has been handed to the upstream
    first.finish();
    await sleep(300);
    assert.equal(stub.seen.length, 2);
    assert.match(first.answer(), /^HTTP\/1\.1 200/);
    assert.equal((await viaSocket(b.socketPath, { body: JSON.stringify({ pad: 'z'.repeat(900) }) })).status, 200, 'with the first one released a 900-byte body fits');
    // a client that walks away mid-body gives its bytes back
    const gone = slowBody(b.socketPath, 900, 100);
    await sleep(100);
    assert.equal((await viaSocket(b.socketPath, { body: JSON.stringify({ pad: 'w'.repeat(300) }) })).status, 503, 'while it holds 900');
    gone.close();
    await sleep(150);
    assert.equal((await viaSocket(b.socketPath, { body: JSON.stringify({ pad: 'w'.repeat(300) }) })).status, 200, 'after it left the bytes are free again');
  } finally {
    await b.close();
    await stub.close();
  }
});

test('A_body_with_no_declared_length_counts_the_bytes_it_has_sent_and_is_refused_when_they_pass_the_bound', async () => {
  const stub = await stubUpstream(ok);
  const b = await startBroker({ stub, capUsd: 10, maxBufferedBytes: 1000 });
  try {
    const answer = await new Promise(resolve => {
      const c = net.connect(b.socketPath);
      let got = '';
      c.on('data', d => { got += d.toString('latin1'); });
      c.on('error', () => {});
      c.on('close', () => resolve(got));
      c.write('POST /v1/messages HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\n\r\n');
      const chunk = 'x'.repeat(400);
      for (let i = 0; i < 4; i += 1) c.write(`${chunk.length.toString(16)}\r\n${chunk}\r\n`);
      setTimeout(() => c.destroy(), 600);
    });
    assert.match(answer, /^HTTP\/1\.1 503/, answer.slice(0, 40));
    assert.equal(stub.seen.length, 0);
    assert.equal(b.lines.filter(l => l.event === 'refused').at(-1).reason, 'busy');
  } finally {
    await b.close();
    await stub.close();
  }
});
