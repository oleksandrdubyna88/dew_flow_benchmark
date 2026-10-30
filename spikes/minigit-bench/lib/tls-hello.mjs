// The ClientHello reader behind the egress proxy's SNI gate (E2.S2 D-19): a pure parser over the first bytes a client sends through
// a CONNECT tunnel. It answers one of three things and never throws: `incomplete` (read more), `ok` (the server name, as sent) or
// `refused` with one of the four reasons the egress log records: `not-tls`, `sni-missing`, `ech-refused`. (`sni-mismatch` is the
// caller's: only it knows the CONNECT host.)
//
// Why it exists: the CONNECT line names the host, but TLS names it again, in the SNI, and a CDN edge routes by the SNI it is sent.
// A client can CONNECT registry.npmjs.org and send any other name to that edge. And with Encrypted Client Hello the real name is
// inside an encrypted extension, so no SNI check can mean anything: a hello that carries ECH (0xfe0d) or the legacy ESNI (0xffce)
// is refused whatever its outer name says.
export const HELLO_MAX_BYTES = 16 * 1024;
export const ECH_EXTENSION = 0xfe0d;
export const ESNI_EXTENSION = 0xffce;

const RECORD_HEADER = 5;
const HANDSHAKE_HEADER = 4;
const HANDSHAKE = 0x16;
const CLIENT_HELLO = 0x01;
const SERVER_NAME = 0;
const HOST_NAME = 0;
const MAX_RECORD = 16 * 1024 + 256;

const refuse = (reason, why) => ({ status: 'refused', reason, why });
const NOT_TLS = why => refuse('not-tls', why);
const INCOMPLETE = Object.freeze({ status: 'incomplete' });

/** The handshake bytes carried by the records at the front of `buf`: `{ bytes }`, `{ short: true }` (more to read) or a refusal. */
function handshakeBytes(buf) {
  const pieces = [];
  let at = 0;
  let have = 0;
  let need = null;
  while (need === null || have < need) {
    const left = buf.length - at;
    if (left >= 1 && buf[at] !== HANDSHAKE) return { refusal: NOT_TLS('the first bytes are not a TLS handshake record') };
    if ((left >= 2 && buf[at + 1] !== 3) || (left >= 3 && buf[at + 2] > 4)) return { refusal: NOT_TLS('the record version is not SSL 3.0 / TLS 1.x') };
    if (left < RECORD_HEADER) return { short: true };
    const len = buf.readUInt16BE(at + 3);
    if (len === 0 || len > MAX_RECORD) return { refusal: NOT_TLS(`a record length of ${len} is not valid`) };
    const take = Math.min(len, buf.length - at - RECORD_HEADER);
    pieces.push(buf.subarray(at + RECORD_HEADER, at + RECORD_HEADER + take));
    have += take;
    if (need === null && have >= HANDSHAKE_HEADER) {
      const first = Buffer.concat(pieces);
      if (first[0] !== CLIENT_HELLO) return { refusal: NOT_TLS('the handshake message is not a ClientHello') };
      need = HANDSHAKE_HEADER + first.readUIntBE(1, 3);
      if (need > HELLO_MAX_BYTES) return { refusal: NOT_TLS(`the ClientHello is over ${HELLO_MAX_BYTES} bytes`) };
    }
    if (take < len) return { short: true };
    at += RECORD_HEADER + len;
  }
  return { bytes: Buffer.concat(pieces).subarray(0, need) };
}

/** A cursor over `bytes` that reports an overrun as `null` instead of reading past the end. */
function reader(bytes) {
  let at = 0;
  const take = n => {
    if (at + n > bytes.length) return null;
    at += n;
    return bytes.subarray(at - n, at);
  };
  return {
    take,
    skip: n => take(n) !== null,
    u8: () => take(1)?.[0] ?? null,
    u16: () => take(2)?.readUInt16BE(0) ?? null,
    vec8: () => { const n = take(1)?.[0]; return n === undefined ? null : take(n); },
    vec16: () => { const n = take(2)?.readUInt16BE(0); return n === undefined ? null : take(n); },
    done: () => at === bytes.length,
  };
}

/** The `server_name` extension body -> the one host name, or a refusal. */
function hostNameOf(body) {
  const list = reader(body).vec16();
  if (list === null) return { refusal: NOT_TLS('the server_name extension is cut short') };
  const names = [];
  const entries = reader(list);
  while (!entries.done()) {
    const type = entries.u8();
    const name = entries.vec16();
    if (type === null || name === null) return { refusal: NOT_TLS('a server_name entry is cut short') };
    // RFC 6066: one entry per name type, and host_name is the only type defined. A type this proxy does not read could be read by the server, so any other type is a refusal
    if (type !== HOST_NAME) return { refusal: NOT_TLS(`the server_name list holds an entry of type ${type}: only one host_name entry is read the same way by every server`) };
    names.push(name.toString('latin1'));
  }
  if (names.length !== 1) return { refusal: names.length === 0 ? refuse('sni-missing', 'the server_name extension names no host') : NOT_TLS('the server_name extension names several hosts') };
  return { sni: names[0] };
}

/** The extension rows of a hello body -> `Map<type, body>`, or a refusal (a cut-short or repeated extension is not a hello a server reads one way). */
function extensionsOf(body) {
  const r = reader(body);
  if (!r.skip(2 + 32) || r.vec8() === null || r.vec16() === null || r.vec8() === null) return { refusal: NOT_TLS('the ClientHello is cut short before its extensions') };
  const block = r.vec16();
  if (block === null || !r.done()) return { refusal: NOT_TLS('the ClientHello extensions do not fit their length') };
  const found = new Map();
  const rows = reader(block);
  while (!rows.done()) {
    const type = rows.u16();
    const data = rows.vec16();
    if (type === null || data === null) return { refusal: NOT_TLS('an extension is cut short') };
    if (found.has(type)) return { refusal: NOT_TLS(`the extension ${type} appears twice`) };
    found.set(type, data);
  }
  return { found };
}

/**
 * Inspect the bytes read so far from a client -> `{ status: 'incomplete' } | { status: 'ok', sni, ech, esni } | { status: 'refused', reason, why }`.
 * The hello may span records and chunks; it is complete once its handshake message (at most 16 KiB) is whole.
 */
export function inspectClientHello(buf) {
  const hs = handshakeBytes(buf);
  if (hs.refusal) return { status: 'refused', ...hs.refusal };
  if (hs.short) return INCOMPLETE;
  const ext = extensionsOf(hs.bytes.subarray(HANDSHAKE_HEADER));
  if (ext.refusal) return { status: 'refused', ...ext.refusal };
  const ech = ext.found.has(ECH_EXTENSION);
  const esni = ext.found.has(ESNI_EXTENSION);
  if (ech || esni) return refuse('ech-refused', `the ClientHello carries ${ech ? 'encrypted_client_hello (0xfe0d)' : 'the legacy encrypted_server_name (0xffce)'}: the real server name is hidden`);
  if (!ext.found.has(SERVER_NAME)) return refuse('sni-missing', 'the ClientHello has no server_name extension');
  const named = hostNameOf(ext.found.get(SERVER_NAME));
  return named.refusal ? { status: 'refused', ...named.refusal } : { status: 'ok', sni: named.sni, ech, esni };
}
