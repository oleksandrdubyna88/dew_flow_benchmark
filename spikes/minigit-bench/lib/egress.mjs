// The egress proxy (E2.S2 D-3, D-19): an HTTP CONNECT proxy on a unix socket bound into the agent box. It forwards `CONNECT <host>:443`
// only for a host named exactly in the arm's list (lib/egress-hosts.mjs, the one list), answers 403 to any other CONNECT and 405 to
// every non-CONNECT line (plain HTTP is not proxied), and logs every request. It never sees inside TLS, so the log holds no secret.
//
// The CONNECT host is not the whole decision: a CDN edge routes by the TLS SNI, and a client may send any name to the edge of an
// allowed host. So after the 200 the proxy holds the client's first bytes, reads the whole ClientHello (lib/tls-hello.mjs), and
// pipes only when the SNI equals the CONNECT host (case-insensitive, exact otherwise) and the hello carries no ECH/ESNI extension.
// Anything else closes both sides before the upstream has seen one byte, and the log says why (`tls-refused` + `reason`).
import { chmodSync, rmSync } from 'node:fs';
import net from 'node:net';
import { inspectClientHello } from './tls-hello.mjs';

export const HELLO_TIMEOUT_MS = 10_000;

export const MAX_HEADER_BYTES = 64 * 1024;
const CONNECT_LINE = /^CONNECT ([^\s:/]+):(\d+) HTTP\/1\.[01]$/;
const HEADER_END = '\r\n\r\n';
const STATUS_TEXT = Object.freeze({ 403: 'Forbidden', 405: 'Method Not Allowed' });

/** The decision on a request's first line -> `{ allow: true, host, port } | { allow: false, status: 403 | 405 | 431, why[, close, host, port] }`. */
export function connectDecision(requestLine, allow) {
  if (requestLine.length > MAX_HEADER_BYTES) return { allow: false, status: 431, close: true, why: 'the request line is over 64 KiB' };
  const m = CONNECT_LINE.exec(requestLine);
  if (!m) return { allow: false, status: 405, why: 'not a CONNECT request: plain HTTP is not proxied' };
  const host = m[1];
  const port = Number(m[2]);
  if (port !== 443) return { allow: false, status: 403, host, port, why: `port ${port} is not 443` };
  if (!allow.includes(host)) return { allow: false, status: 403, host, port, why: `${host} is not on the arm's host list` };
  return { allow: true, host, port };
}

const bump = (counts, key) => ({ ...counts, [key]: (counts[key] ?? 0) + 1 });

/** The log read as a summary: allowed and refused hosts with counts, the number of non-CONNECT requests, and the TLS refusals by reason. */
export function egressSummary(lines) {
  return lines.reduce((acc, l) => {
    if (l.verdict === 'allowed') return { ...acc, allowed: bump(acc.allowed, l.host) };
    if (l.verdict === 'refused') return { ...acc, refused: bump(acc.refused, l.host) };
    if (l.verdict === 'nonConnect') return { ...acc, nonConnect: acc.nonConnect + 1 };
    if (l.verdict === 'tls-refused') return { ...acc, tlsRefused: bump(acc.tlsRefused, l.reason) };
    return acc;
  }, { allowed: {}, refused: {}, nonConnect: 0, tlsRefused: {} });
}

const answer = (socket, status) => socket.end(`HTTP/1.1 ${status} ${STATUS_TEXT[status] ?? 'Error'}\r\nConnection: close\r\n\r\n`);

/** The verdict on what the client sent first, once the 200 is out: `{ pipe: true, sni }` or `{ pipe: false, reason, why, sni? }`; `null` while the hello is incomplete. */
export function helloVerdict(buffered, host) {
  const seen = inspectClientHello(buffered);
  if (seen.status === 'incomplete') return null;
  if (seen.status === 'refused') return { pipe: false, reason: seen.reason, why: seen.why };
  if (seen.sni.toLowerCase() !== host) return { pipe: false, reason: 'sni-mismatch', why: `the SNI names a host other than the CONNECT host ${host}`, sni: seen.sni.slice(0, 255) };
  return { pipe: true, sni: seen.sni };
}

/**
 * After the 200 (and before the upstream gets a byte): read the ClientHello, then pipe or close both sides. `rest` is what the
 * client sent behind the CONNECT headers.
 */
function gateAndPipe({ client, upstream, decision, rest, emit, helloTimeoutMs }) {
  let buffered = rest;
  let settled = false;
  const refuse = (reason, why, extra = {}) => {
    if (settled) return;
    settled = true;
    clearTimeout(timer);
    emit({ verdict: 'tls-refused', host: decision.host, port: decision.port, reason, why, ...extra });
    client.destroy();
    upstream.destroy();
  };
  const timer = setTimeout(() => refuse('not-tls', `no ClientHello within ${helloTimeoutMs} ms`), helloTimeoutMs);
  const check = () => {
    if (settled) return;
    const v = helloVerdict(buffered, decision.host);
    if (v === null) return;
    if (!v.pipe) return refuse(v.reason, v.why, v.sni === undefined ? {} : { sni: v.sni });
    settled = true;
    clearTimeout(timer);
    client.off('data', onData);
    emit({ verdict: 'allowed', host: decision.host, port: decision.port, sni: v.sni });
    upstream.write(buffered);
    client.pipe(upstream);
    upstream.pipe(client);
  };
  const onData = chunk => {
    buffered = Buffer.concat([buffered, chunk]);
    check();
  };
  client.on('data', onData);
  client.on('close', () => { clearTimeout(timer); settled = true; });
  check();
}

/** Open the tunnel after an allowed decision: dial, answer 200, then gate on the ClientHello. An upstream error is logged and the client dropped. */
function tunnel({ client, decision, rest, dial, emit, helloTimeoutMs }) {
  const upstream = dial({ host: decision.host, port: decision.port });
  let connected = false;
  upstream.once('connect', () => {
    connected = true;
    client.write('HTTP/1.1 200 Connection Established\r\n\r\n');
    gateAndPipe({ client, upstream, decision, rest, emit, helloTimeoutMs });
  });
  upstream.on('error', e => {
    if (!connected) emit({ verdict: 'upstream-error', host: decision.host, port: decision.port, code: e.code ?? e.message });
    client.destroy();
  });
  client.on('error', () => upstream.destroy());
  client.on('close', () => upstream.destroy());
}

/** Serve one client connection: read the header block (bounded), decide on its first line, then answer or tunnel. */
function serveClient(client, { allow, dial, emit, helloTimeoutMs }) {
  let buffered = Buffer.alloc(0);
  const onData = chunk => {
    buffered = Buffer.concat([buffered, chunk]);
    const end = buffered.indexOf(HEADER_END);
    if (end < 0) {
      if (buffered.length > MAX_HEADER_BYTES) {
        emit({ verdict: 'nonConnect', why: 'the header block is over 64 KiB: the connection was closed' });
        client.destroy();
      }
      return;
    }
    client.off('data', onData);
    const line = buffered.subarray(0, end).toString('latin1').split('\r\n')[0];
    const decision = connectDecision(line, allow);
    if (decision.allow) return tunnel({ client, decision, rest: buffered.subarray(end + HEADER_END.length), dial, emit, helloTimeoutMs });
    emit(decision.status === 403
      ? { verdict: 'refused', host: decision.host, port: decision.port, status: 403, why: decision.why }
      : { verdict: 'nonConnect', line: line.slice(0, 200), why: decision.why });
    return decision.close ? client.destroy() : answer(client, decision.status);
  };
  client.on('data', onData);
  client.on('error', () => {});
}

/**
 * Start the proxy on `socketPath` (mode 600) -> `{ close() }`. `allow` is the arm's exact host list; `log(line)` receives
 * `{ t, verdict, host?, port?, line?, why?, code?, sni?, reason? }`; `dial` is the seam tests use to point an allowed host at a local server.
 */
export async function egressProxy({ socketPath, allow, log, helloTimeoutMs = HELLO_TIMEOUT_MS, dial = ({ host, port }) => net.connect({ host, port }) }) {
  const emit = line => {
    try { log({ t: new Date().toISOString(), ...line }); } catch (e) { process.stderr.write(`egress: the log sink failed: ${e.message}\n`); }
  };
  const clients = new Set();
  const server = net.createServer(client => {
    clients.add(client);
    client.on('close', () => clients.delete(client));
    serveClient(client, { allow, dial, emit, helloTimeoutMs });
  });
  rmSync(socketPath, { force: true });
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(socketPath, resolve); });
  chmodSync(socketPath, 0o600);
  let closed = false;
  return {
    server,
    close: async () => {
      if (closed) return;
      closed = true;
      clients.forEach(c => c.destroy());
      await new Promise(resolve => { server.close(() => resolve()); });
      rmSync(socketPath, { force: true });
    },
  };
}
