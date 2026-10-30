// ClientHello bytes for the SNI-gate tests: the real hellos captured from node's TLS and curl (fixtures/tls/*.hex), and the three
// edits a real hello needs to become the hostile shapes (an extra extension, a removed one, a re-fragmentation into records).
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { spikeDir } from './helpers.mjs';

/** A captured ClientHello (the whole TLS record) by fixture name, e.g. `node-registry.npmjs.org`. */
export const realHello = name => Buffer.from(readFileSync(path.join(spikeDir, 'test', 'fixtures', 'tls', `${name}.hello.hex`), 'utf8').replace(/\s+/g, ''), 'hex');

const u16 = n => Buffer.from([n >> 8, n & 0xff]);
const u24 = n => Buffer.from([n >> 16, (n >> 8) & 0xff, n & 0xff]);

/** The handshake message (header and body) of a single-record hello. */
const handshakeOf = hello => hello.subarray(5);

/** Offsets inside a handshake message: where the extensions block starts (its 2-byte length) and ends. */
function extensionsSpan(hs) {
  let at = 4 + 2 + 32;
  at += 1 + hs[at];
  at += 2 + hs.readUInt16BE(at);
  at += 1 + hs[at];
  return { lenAt: at, start: at + 2, end: at + 2 + hs.readUInt16BE(at) };
}

/** The extensions of a real hello as `{ type, bytes }` rows (the bytes include the 4-byte extension header). */
export function extensionsOf(hello) {
  const hs = handshakeOf(hello);
  const { start, end } = extensionsSpan(hs);
  const rows = [];
  for (let at = start; at < end; at += 4 + hs.readUInt16BE(at + 2)) rows.push({ type: hs.readUInt16BE(at), bytes: hs.subarray(at, at + 4 + hs.readUInt16BE(at + 2)) });
  return rows;
}

/** The same hello with its extensions replaced by `rows`, every length rewritten. */
export function withExtensions(hello, rows) {
  const hs = handshakeOf(hello);
  const { lenAt } = extensionsSpan(hs);
  const block = Buffer.concat(rows.map(r => r.bytes));
  const body = Buffer.concat([hs.subarray(4, lenAt), u16(block.length), block]);
  return Buffer.concat([hello.subarray(0, 3), u16(4 + body.length), hs.subarray(0, 1), u24(body.length), body]);
}

/** An extension row of `type` carrying `data`. */
export const extension = (type, data = Buffer.alloc(0)) => ({ type, bytes: Buffer.concat([u16(type), u16(data.length), data]) });

/** A hello with an extra extension appended (ECH 0xfe0d, legacy ESNI 0xffce). */
export const withExtra = (hello, type, data = Buffer.from([1, 2, 3])) => withExtensions(hello, [...extensionsOf(hello), extension(type, data)]);

/** A hello without its `server_name` extension. */
export const withoutSni = hello => withExtensions(hello, extensionsOf(hello).filter(r => r.type !== 0));

/** The same handshake message cut into `parts` TLS records of near-equal size. */
export function inRecords(hello, parts) {
  const hs = handshakeOf(hello);
  const size = Math.ceil(hs.length / parts);
  const records = [];
  for (let at = 0; at < hs.length; at += size) {
    const piece = hs.subarray(at, at + size);
    records.push(Buffer.concat([hello.subarray(0, 3), u16(piece.length), piece]));
  }
  return Buffer.concat(records);
}

/** A `server_name` extension row naming `names` (host_name entries). */
export function sniExtension(...names) {
  const entries = Buffer.concat(names.map(n => Buffer.concat([Buffer.from([0]), u16(n.length), Buffer.from(n, 'latin1')])));
  return extension(0, Buffer.concat([u16(entries.length), entries]));
}

/** A hello whose server_name extension is replaced by `sniRow` (or one naming `name`). */
export const withSni = (hello, name) => withExtensions(hello, extensionsOf(hello).map(r => (r.type === 0 ? sniExtension(name) : r)));
