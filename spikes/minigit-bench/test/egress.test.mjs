import assert from 'node:assert/strict';
import net from 'node:net';
import path from 'node:path';
import { test } from 'node:test';
import { connectDecision, egressProxy, egressSummary } from '../lib/egress.mjs';
import { EGRESS_HOSTS } from '../lib/egress-hosts.mjs';
import { sleep } from './broker-stub.mjs';
import { tmpDir } from './helpers.mjs';

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
  ];
  assert.deepEqual(egressSummary(lines), { allowed: { 'api.nuget.org': 2 }, refused: { 'example.com': 1, 'api.anthropic.com': 1 }, nonConnect: 1 });
  assert.deepEqual(egressSummary([]), { allowed: {}, refused: {}, nonConnect: 0 });
});

/** A proxy over a short socket with a local echo server standing in for the upstream (the `dial` seam). */
async function proxyWithEcho(allow) {
  const dir = tmpDir('eg');
  const socketPath = path.join(dir, 'p.sock');
  const lines = [];
  const echo = net.createServer(c => c.on('data', d => c.write(`echo:${d}`)));
  await new Promise(r => echo.listen(0, '127.0.0.1', r));
  const dialed = [];
  const proxy = await egressProxy({ socketPath, allow, log: l => lines.push(l), dial: ({ host, port }) => { dialed.push({ host, port }); return net.connect({ host: '127.0.0.1', port: echo.address().port }); } });
  return { socketPath, lines, dialed, close: async () => { await proxy.close(); echo.close(); echo.closeAllConnections?.(); } };
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
    const ok = await talk(p.socketPath, 'CONNECT api.nuget.org:443 HTTP/1.1\r\nHost: api.nuget.org:443\r\n\r\n', { then: 'ping' });
    assert.match(ok.got, /^HTTP\/1\.1 200 Connection Established\r\n\r\necho:ping$/);
    assert.deepEqual(p.dialed, [{ host: 'api.nuget.org', port: 443 }]);
    const refused = await talk(p.socketPath, 'CONNECT example.com:443 HTTP/1.1\r\n\r\n');
    assert.match(refused.got, /^HTTP\/1\.1 403 /);
    const get = await talk(p.socketPath, 'GET http://example.com/ HTTP/1.1\r\nHost: example.com\r\n\r\n');
    assert.match(get.got, /^HTTP\/1\.1 405 /);
    assert.equal(p.dialed.length, 1, 'a refused request never dials');
    assert.deepEqual(p.lines.map(l => [l.verdict, l.host ?? null]), [['allowed', 'api.nuget.org'], ['refused', 'example.com'], ['nonConnect', null]]);
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
