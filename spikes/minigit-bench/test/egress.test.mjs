import assert from 'node:assert/strict';
import net from 'node:net';
import path from 'node:path';
import { test } from 'node:test';
import { connectDecision, egressProxy, egressSummary } from '../lib/egress.mjs';
import { EGRESS_HOSTS } from '../lib/egress-hosts.mjs';
import { sleep } from './broker-stub.mjs';
import { tmpDir } from './helpers.mjs';
import { ECH_EXTENSION, ESNI_EXTENSION } from '../lib/tls-hello.mjs';
import { inRecords, realHello, withExtra, withoutSni, withSni } from './tls-hello-bytes.mjs';

const BARE = EGRESS_HOSTS.bare;

test('The_connect_decision_is_exact_and_deny_by_default', () => {
  const rows = [
    ['CONNECT api.nuget.org:443 HTTP/1.1', { allow: true, host: 'api.nuget.org', port: 443 }],
    ['CONNECT api.nuget.org:443 HTTP/1.0', { allow: true, host: 'api.nuget.org', port: 443 }],
    ['CONNECT api.nuget.org:80 HTTP/1.1', { allow: false, status: 403 }],
    ['CONNECT evil.api.nuget.org:443 HTTP/1.1', { allow: false, status: 403 }],
    ['CONNECT api.nuget.org.evil.example:443 HTTP/1.1', { allow: false, status: 403 }],
    ['CONNECT API.NUGET.ORG:443 HTTP/1.1', { allow: false, status: 403 }],
    ['CONNECT api.anthropic.com:443 HTTP/1.1', { allow: false, status: 403 }],
    ['CONNECT registry.npmjs.org:443 HTTP/1.1', { allow: true, host: 'registry.npmjs.org', port: 443 }],
    ['GET http://x/ HTTP/1.1', { allow: false, status: 405 }],
    ['', { allow: false, status: 405 }],
    ['CONNECT nonsense', { allow: false, status: 405 }],
    [`CONNECT ${'a'.repeat(70 * 1024)}:443 HTTP/1.1`, { allow: false, close: true }],
  ];
  for (const [line, expected] of rows) {
    const d = connectDecision(line, BARE);
    for (const [k, v] of Object.entries(expected)) assert.equal(d[k], v, `${line.slice(0, 50)} -> ${k}`);
    if (!d.allow) assert.equal(typeof d.why, 'string');
  }
  assert.equal(connectDecision('CONNECT api.nuget.org:443 HTTP/1.1', []).allow, false, 'an empty list allows nothing');
});

test('No_arm_lists_the_API_host_and_the_lists_come_from_one_module', async () => {
  for (const [arm, hosts] of Object.entries(EGRESS_HOSTS)) assert.equal(hosts.includes('api.anthropic.com'), false, arm);
  const { readFileSync, readdirSync } = await import('node:fs');
  const lib = path.join(import.meta.dirname, '..', 'lib');
  for (const f of ['egress.mjs', 'sandbox.mjs']) {
    let src = '';
    try { src = readFileSync(path.join(lib, f), 'utf8'); } catch { src = null; }
    if (src === null) continue; // sandbox.mjs arrives later in the story; the scan below is over what exists
    assert.equal(/registry\.npmjs\.org|api\.nuget\.org|github\.com/.test(src.replace(/\/\/.*$/gm, '')), false, `${f} defines no host list of its own`);
  }
  const defining = readdirSync(lib).filter(f => f.endsWith('.mjs') && /export const EGRESS_HOSTS\b/.test(readFileSync(path.join(lib, f), 'utf8')));
  assert.deepEqual(defining, ['egress-hosts.mjs']);
});

test('The_summary_counts_allowed_and_refused_hosts_and_non_connect_lines', () => {
  const lines = [
    { verdict: 'allowed', host: 'api.nuget.org', port: 443 }, { verdict: 'allowed', host: 'api.nuget.org', port: 443 },
    { verdict: 'refused', host: 'example.com', port: 443 }, { verdict: 'refused', host: 'api.anthropic.com', port: 443 },
    { verdict: 'nonConnect', line: 'GET http://x/ HTTP/1.1' }, { verdict: 'upstream-error', host: 'api.nuget.org', port: 443, code: 'ECONNREFUSED' },
    { verdict: 'tls-refused', host: 'api.nuget.org', port: 443, reason: 'sni-mismatch' }, { verdict: 'tls-refused', host: 'api.nuget.org', port: 443, reason: 'sni-mismatch' },
    { verdict: 'tls-refused', host: 'registry.npmjs.org', port: 443, reason: 'ech-refused' },
  ];
  assert.deepEqual(egressSummary(lines), { allowed: { 'api.nuget.org': 2 }, refused: { 'example.com': 1, 'api.anthropic.com': 1 }, nonConnect: 1, tlsRefused: { 'sni-mismatch': 2, 'ech-refused': 1 } });
  assert.deepEqual(egressSummary([]), { allowed: {}, refused: {}, nonConnect: 0, tlsRefused: {} });
});

/** A proxy over a short socket with a local echo server standing in for the upstream (the `dial` seam). */
async function proxyWithEcho(allow) {
  const dir = tmpDir('eg');
  const socketPath = path.join(dir, 'p.sock');
  const lines = [];
  const received = [];
  const echo = net.createServer(c => c.on('data', d => { received.push(d); c.write(Buffer.concat([Buffer.from('echo:'), d])); }));
  await new Promise(r => echo.listen(0, '127.0.0.1', r));
  const dialed = [];
  const proxy = await egressProxy({ socketPath, allow, log: l => lines.push(l), dial: ({ host, port }) => { dialed.push({ host, port }); return net.connect({ host: '127.0.0.1', port: echo.address().port }); } });
  return { socketPath, lines, dialed, received, close: async () => { await proxy.close(); echo.close(); echo.closeAllConnections?.(); } };
}

/** Send `text` to the proxy socket; resolves with everything received until the proxy or the test closes. */
function talk(socketPath, text, { then, waitMs = 300 } = {}) {
  return new Promise(resolve => {
    const c = net.connect(socketPath);
    let got = '';
    c.on('data', d => { got += d; });
    c.on('close', () => resolve({ got, closed: true }));
    c.on('error', () => {});
    c.write(text);
    if (then) c.once('data', () => c.write(then));
    setTimeout(() => { const r = { got, closed: c.destroyed }; c.destroy(); resolve(r); }, waitMs);
  });
}

test('The_proxy_connects_only_after_the_decision_and_pipes_bytes_both_ways', async () => {
  const p = await proxyWithEcho(BARE);
  try {
    const ok = await talk(p.socketPath, 'CONNECT api.nuget.org:443 HTTP/1.1\r\nHost: api.nuget.org:443\r\n\r\n', { then: withSni(realHello('node-registry.npmjs.org'), 'api.nuget.org') });
    assert.match(ok.got, /^HTTP\/1\.1 200 Connection Established\r\n\r\necho:/);
    assert.deepEqual(p.dialed, [{ host: 'api.nuget.org', port: 443 }]);
    const refused = await talk(p.socketPath, 'CONNECT example.com:443 HTTP/1.1\r\n\r\n');
    assert.match(refused.got, /^HTTP\/1\.1 403 /);
    const get = await talk(p.socketPath, 'GET http://example.com/ HTTP/1.1\r\nHost: example.com\r\n\r\n');
    assert.match(get.got, /^HTTP\/1\.1 405 /);
    assert.equal(p.dialed.length, 1, 'a refused request never dials');
    assert.deepEqual(p.lines.map(l => [l.verdict, l.host ?? null]), [['allowed', 'api.nuget.org'], ['refused', 'example.com'], ['nonConnect', null]]);
    assert.equal(p.lines[0].sni, 'api.nuget.org');
    assert.ok(p.lines.every(l => typeof l.t === 'string'));
    assert.equal(p.lines[2].line, 'GET http://example.com/ HTTP/1.1');
  } finally {
    await p.close();
  }
});

test('A_header_block_over_64_KiB_closes_the_connection_without_an_answer_and_the_log_says_why', async () => {
  const p = await proxyWithEcho(BARE);
  try {
    const r = await talk(p.socketPath, `CONNECT api.nuget.org:443 HTTP/1.1\r\nX: ${'a'.repeat(70 * 1024)}`, { waitMs: 500 });
    assert.equal(r.got, '');
    assert.equal(r.closed, true);
    assert.equal(p.dialed.length, 0);
    await sleep(50);
    assert.equal(p.lines.at(-1).verdict, 'nonConnect');
    assert.match(p.lines.at(-1).why, /64 KiB/);
  } finally {
    await p.close();
  }
});

test('An_upstream_that_cannot_be_reached_is_logged_and_the_client_is_dropped', async () => {
  const dir = tmpDir('eg-up');
  const socketPath = path.join(dir, 'p.sock');
  const lines = [];
  const proxy = await egressProxy({ socketPath, allow: BARE, log: l => lines.push(l), dial: () => net.connect({ host: '127.0.0.1', port: 1 }) });
  try {
    const r = await talk(socketPath, 'CONNECT api.nuget.org:443 HTTP/1.1\r\n\r\n', { waitMs: 500 });
    assert.equal(r.got.includes('200'), false);
    assert.deepEqual(lines.map(l => l.verdict), ['upstream-error']);
    assert.equal(lines[0].code, 'ECONNREFUSED');
  } finally {
    await proxy.close();
  }
});

test('The_proxy_socket_is_private_and_close_removes_it_and_is_idempotent', async () => {
  const { statSync, existsSync } = await import('node:fs');
  const p = await proxyWithEcho(BARE);
  assert.equal(statSync(p.socketPath).mode & 0o777, 0o600);
  await p.close();
  await p.close();
  assert.equal(existsSync(p.socketPath), false);
});

const CONNECT_NPM = 'CONNECT registry.npmjs.org:443 HTTP/1.1\r\nHost: registry.npmjs.org:443\r\n\r\n';
const npmHello = () => realHello('node-registry.npmjs.org');

/** CONNECT, then send `chunks` after the 200 with `gapMs` between them; resolves with what came back and whether the proxy closed. */
function throughTunnel(socketPath, chunks, { gapMs = 30, waitMs = 500, connect = CONNECT_NPM } = {}) {
  return new Promise(resolve => {
    const c = net.connect(socketPath);
    let got = Buffer.alloc(0);
    c.on('data', d => {
      got = Buffer.concat([got, d]);
      if (got.toString('latin1').includes('\r\n\r\n') && !c.sent) {
        c.sent = true;
        chunks.reduce((p, chunk) => p.then(() => new Promise(r => { c.write(chunk); setTimeout(r, gapMs); })), Promise.resolve());
      }
    });
    c.on('error', () => {});
    c.write(connect);
    setTimeout(() => { const r = { got: got.toString('latin1'), closed: c.destroyed }; c.destroy(); resolve(r); }, waitMs);
  });
}

test('A_client_that_sends_another_server_name_over_an_allowed_tunnel_is_closed_and_the_upstream_gets_no_byte', async () => {
  const p = await proxyWithEcho(BARE);
  try {
    const r = await throughTunnel(p.socketPath, [withSni(npmHello(), 'www.cloudflare.com')]);
    assert.match(r.got, /^HTTP\/1\.1 200 /);
    assert.equal(r.got.includes('echo:'), false, 'nothing came back from the upstream');
    assert.equal(r.closed, true);
    assert.equal(p.received.length, 0, 'the hello was never written to the upstream');
    assert.deepEqual(p.lines.map(l => [l.verdict, l.reason, l.host, l.sni]), [['tls-refused', 'sni-mismatch', 'registry.npmjs.org', 'www.cloudflare.com']]);
    assert.equal(p.lines.some(l => l.verdict === 'allowed'), false, 'the log never calls a mismatched tunnel allowed');
  } finally {
    await p.close();
  }
});

test('The_server_name_must_equal_the_connect_host_apart_from_case_and_nothing_else', async () => {
  const cases = [['REGISTRY.NPMJS.ORG', 'allowed'], ['registry.npmjs.org', 'allowed'], ['registry.npmjs.org.', 'sni-mismatch'], ['api.nuget.org', 'sni-mismatch'], ['registry.npmjs.org.evil.example', 'sni-mismatch'], [' registry.npmjs.org', 'sni-mismatch']];
  for (const [sni, expected] of cases) {
    const p = await proxyWithEcho(BARE);
    try {
      const r = await throughTunnel(p.socketPath, [withSni(npmHello(), sni)]);
      assert.equal(p.lines.at(-1).verdict === 'allowed' ? 'allowed' : p.lines.at(-1).reason, expected, sni);
      assert.equal(r.got.includes('echo:'), expected === 'allowed', sni);
    } finally {
      await p.close();
    }
  }
});

test('A_hello_without_a_name_or_with_ech_or_that_is_not_tls_is_refused_with_its_reason', async () => {
  const rows = [
    ['sni-missing', [withoutSni(npmHello())]],
    ['ech-refused', [withExtra(npmHello(), ECH_EXTENSION)]],
    ['ech-refused', [withExtra(npmHello(), ESNI_EXTENSION)]],
    ['not-tls', [Buffer.from('ping')]],
    ['not-tls', [Buffer.from('GET / HTTP/1.1\r\nHost: x\r\n\r\n')]],
  ];
  for (const [reason, chunks] of rows) {
    const p = await proxyWithEcho(BARE);
    try {
      const r = await throughTunnel(p.socketPath, chunks);
      assert.equal(r.closed, true, reason);
      assert.equal(p.received.length, 0, `${reason}: nothing reached the upstream`);
      assert.deepEqual(p.lines.map(l => [l.verdict, l.reason]), [['tls-refused', reason]]);
    } finally {
      await p.close();
    }
  }
});

test('A_hello_that_arrives_in_pieces_or_in_several_records_is_read_whole_and_forwarded_whole', async () => {
  for (const wire of [npmHello(), inRecords(npmHello(), 3)]) {
    const p = await proxyWithEcho(BARE);
    try {
      const pieces = [wire.subarray(0, 3), wire.subarray(3, 200), wire.subarray(200, 900), wire.subarray(900)];
      const r = await throughTunnel(p.socketPath, pieces, { gapMs: 40, waitMs: 700 });
      assert.deepEqual(p.lines.map(l => l.verdict), ['allowed']);
      assert.equal(Buffer.concat(p.received).equals(wire), true, 'the upstream got exactly the bytes sent, once');
      assert.equal(r.got.includes('echo:'), true);
    } finally {
      await p.close();
    }
  }
});

test('A_client_that_sends_no_hello_is_cut_off_after_the_read_timeout_and_the_log_says_so', async () => {
  const dir = tmpDir('eg-to');
  const socketPath = path.join(dir, 'p.sock');
  const lines = [];
  const echo = net.createServer(c => c.on('data', () => {}));
  await new Promise(r => echo.listen(0, '127.0.0.1', r));
  const proxy = await egressProxy({ socketPath, allow: BARE, log: l => lines.push(l), helloTimeoutMs: 150, dial: () => net.connect({ host: '127.0.0.1', port: echo.address().port }) });
  try {
    const r = await throughTunnel(socketPath, [], { waitMs: 600 });
    assert.equal(r.closed, true);
    assert.deepEqual(lines.map(l => [l.verdict, l.reason]), [['tls-refused', 'not-tls']]);
    assert.match(lines[0].why, /no ClientHello within 150 ms/);
  } finally {
    await proxy.close();
    echo.close();
    echo.closeAllConnections?.();
  }
});

test('Bytes_sent_after_the_hello_in_the_same_chunk_reach_the_upstream_with_it', async () => {
  const p = await proxyWithEcho(BARE);
  try {
    const wire = Buffer.concat([npmHello(), Buffer.from('early-data')]);
    await throughTunnel(p.socketPath, [wire]);
    assert.equal(Buffer.concat(p.received).equals(wire), true);
  } finally {
    await p.close();
  }
});

/** A proxy whose dial connects to `echo` only after `delayMs`; `received` holds what the upstream got. */
async function proxyWithSlowDial(delayMs, over = {}) {
  const dir = tmpDir('eg-slow');
  const socketPath = path.join(dir, 'p.sock');
  const lines = [];
  const received = [];
  const echo = net.createServer(c => c.on('data', d => received.push(d)));
  await new Promise(r => echo.listen(0, '127.0.0.1', r));
  const dialed = [];
  const dial = () => { const s = new net.Socket(); dialed.push(s); const t = setTimeout(() => s.connect(echo.address().port, '127.0.0.1'), delayMs); s.once('close', () => clearTimeout(t)); return s; };
  const proxy = await egressProxy({ socketPath, allow: BARE, log: l => lines.push(l), dial, ...over });
  return { socketPath, lines, received, dialed, close: async () => { await proxy.close(); echo.close(); echo.closeAllConnections?.(); } };
}

test('A_hello_that_arrives_before_the_upstream_connected_is_held_and_forwarded_not_dropped', async () => {
  const p = await proxyWithSlowDial(300);
  try {
    const hello = npmHello();
    const c = net.connect(p.socketPath);
    c.on('error', () => {});
    await new Promise(r => c.on('connect', r));
    c.write('CONNECT registry.npmjs.org:443 HTTP/1.1\r\nHost: registry.npmjs.org:443\r\n\r\n');
    await sleep(60); // the client does not wait for the 200: its hello goes out while the proxy is still dialling
    c.write(hello);
    await sleep(900);
    assert.equal(Buffer.concat(p.received).equals(hello), true, `the upstream got ${Buffer.concat(p.received).length} of ${hello.length} hello bytes`);
    assert.deepEqual(p.lines.map(l => l.verdict), ['allowed']);
    c.destroy();
  } finally {
    await p.close();
  }
});

test('A_client_that_never_finishes_its_header_block_is_cut_off_after_the_header_timeout', async () => {
  const p = await proxyWithSlowDial(0, { headerTimeoutMs: 150 });
  try {
    const r = await talk(p.socketPath, 'CONNECT registry.npmjs.org:443 HTTP/1.1\r\nHost: x\r\n', { waitMs: 700 });
    assert.equal(r.closed, true, 'the proxy closed a connection that never completed its header block');
    assert.equal(p.dialed.length, 0, 'and never dialled');
    assert.deepEqual(p.lines.map(l => l.verdict), ['nonConnect']);
    assert.match(p.lines[0].why, /no complete header block within 150 ms/);
  } finally {
    await p.close();
  }
});

test('An_upstream_that_never_connects_is_cut_off_after_the_dial_timeout_counted_from_the_accept', async () => {
  const p = await proxyWithSlowDial(60_000, { dialTimeoutMs: 200 });
  try {
    const r = await talk(p.socketPath, 'CONNECT registry.npmjs.org:443 HTTP/1.1\r\n\r\n', { waitMs: 800 });
    assert.equal(r.closed, true);
    assert.equal(r.got.includes('200'), false, 'no 200 for a tunnel that never opened');
    assert.deepEqual(p.lines.map(l => [l.verdict, l.code]), [['upstream-error', 'ETIMEDOUT']]);
    assert.equal(p.dialed[0].destroyed, true, 'the half-open upstream socket was destroyed');
  } finally {
    await p.close();
  }
});
