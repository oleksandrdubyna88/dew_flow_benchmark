// The egress proxy (E2.S2 D-3, D-19): an HTTP CONNECT proxy on a unix socket bound into the agent box. It forwards `CONNECT <host>:443`
// only for a host named exactly in the arm's list (lib/egress-hosts.mjs, the one list), answers 403 to any other CONNECT and 405 to
// every non-CONNECT line (plain HTTP is not proxied), and logs every request. It never sees inside TLS, so the log holds no secret.
import { chmodSync, rmSync } from 'node:fs';
import net from 'node:net';

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

/** The log read as a summary: allowed and refused hosts with counts, and the number of non-CONNECT requests. */
export function egressSummary(lines) {
  return lines.reduce((acc, l) => {
    if (l.verdict === 'allowed') return { ...acc, allowed: bump(acc.allowed, l.host) };
    if (l.verdict === 'refused') return { ...acc, refused: bump(acc.refused, l.host) };
    if (l.verdict === 'nonConnect') return { ...acc, nonConnect: acc.nonConnect + 1 };
    return acc;
  }, { allowed: {}, refused: {}, nonConnect: 0 });
}

const answer = (socket, status) => socket.end(`HTTP/1.1 ${status} ${STATUS_TEXT[status] ?? 'Error'}\r\nConnection: close\r\n\r\n`);

/** Open the tunnel after an allowed decision: dial, answer 200, pipe both ways. An upstream error is logged and the client dropped. */
function tunnel({ client, decision, rest, dial, emit }) {
  const upstream = dial({ host: decision.host, port: decision.port });
  let connected = false;
  upstream.once('connect', () => {
    connected = true;
    emit({ verdict: 'allowed', host: decision.host, port: decision.port });
    client.write('HTTP/1.1 200 Connection Established\r\n\r\n');
    if (rest.length > 0) upstream.write(rest);
    client.pipe(upstream);
    upstream.pipe(client);
  });
  upstream.on('error', e => {
    if (!connected) emit({ verdict: 'upstream-error', host: decision.host, port: decision.port, code: e.code ?? e.message });
    client.destroy();
  });
  client.on('error', () => upstream.destroy());
  client.on('close', () => upstream.destroy());
}

/** Serve one client connection: read the header block (bounded), decide on its first line, then answer or tunnel. */
function serveClient(client, { allow, dial, emit }) {
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
    if (decision.allow) return tunnel({ client, decision, rest: buffered.subarray(end + HEADER_END.length), dial, emit });
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
 * `{ t, verdict, host?, port?, line?, why?, code? }`; `dial` is the seam tests use to point an allowed host at a local server.
 */
export async function egressProxy({ socketPath, allow, log, dial = ({ host, port }) => net.connect({ host, port }) }) {
  const emit = line => {
    try { log({ t: new Date().toISOString(), ...line }); } catch (e) { process.stderr.write(`egress: the log sink failed: ${e.message}\n`); }
  };
  const clients = new Set();
  const server = net.createServer(client => {
    clients.add(client);
    client.on('close', () => clients.delete(client));
    serveClient(client, { allow, dial, emit });
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
