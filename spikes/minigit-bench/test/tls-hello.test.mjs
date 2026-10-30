import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ECH_EXTENSION, ESNI_EXTENSION, HELLO_MAX_BYTES, inspectClientHello } from '../lib/tls-hello.mjs';
import { extension, extensionsOf, inRecords, realHello, sniExtension, withExtensions, withExtra, withoutSni, withSni } from './tls-hello-bytes.mjs';

const firstRecordOf = records => records.subarray(0, 5 + records.readUInt16BE(3));
const REAL = ['node-registry.npmjs.org', 'curl-registry.npmjs.org', 'curl-www.cloudflare.com'];
const sniOf = name => name.replace(/^(node|curl)-/, '');

test('The_real_hellos_of_node_and_curl_give_their_server_name', () => {
  for (const name of REAL) {
    const r = inspectClientHello(realHello(name));
    assert.deepEqual(r, { status: 'ok', sni: sniOf(name), ech: false, esni: false }, name);
  }
});

test('A_hello_cut_at_any_byte_is_incomplete_and_never_a_verdict', () => {
  const hello = realHello('node-registry.npmjs.org');
  for (let n = 0; n < hello.length; n += 7) assert.equal(inspectClientHello(hello.subarray(0, n)).status, 'incomplete', `${n} bytes`);
  assert.equal(inspectClientHello(hello.subarray(0, hello.length - 1)).status, 'incomplete');
  assert.equal(inspectClientHello(hello).status, 'ok');
});

test('A_hello_split_over_several_records_is_read_whole_and_bytes_after_it_do_not_matter', () => {
  const hello = realHello('curl-registry.npmjs.org');
  for (const parts of [2, 3, 7]) {
    const split = inRecords(hello, parts);
    assert.equal(inspectClientHello(split).sni, 'registry.npmjs.org', `${parts} records`);
    assert.equal(inspectClientHello(split.subarray(0, split.length - 3)).status, 'incomplete', `${parts} records, last one short`);
  }
  assert.equal(inspectClientHello(Buffer.concat([hello, Buffer.from('trailing application bytes')])).sni, 'registry.npmjs.org');
});

test('A_hello_without_a_server_name_is_refused_as_sni_missing', () => {
  assert.deepEqual(inspectClientHello(realHello('node-no-sni')), { status: 'refused', reason: 'sni-missing', why: 'the ClientHello has no server_name extension' });
  const stripped = withoutSni(realHello('node-registry.npmjs.org'));
  assert.equal(inspectClientHello(stripped).reason, 'sni-missing');
  assert.equal(extensionsOf(stripped).some(r => r.type === 0), false, 'the fixture really lacks the extension');
});

test('A_hello_with_the_encrypted_client_hello_or_the_legacy_esni_extension_is_refused_as_ech_refused', () => {
  const base = realHello('node-registry.npmjs.org');
  for (const type of [ECH_EXTENSION, ESNI_EXTENSION]) {
    const r = inspectClientHello(withExtra(base, type));
    assert.equal(r.status, 'refused', type.toString(16));
    assert.equal(r.reason, 'ech-refused');
  }
  assert.equal(ECH_EXTENSION, 0xfe0d);
  assert.equal(ESNI_EXTENSION, 0xffce);
});

test('The_ech_check_comes_before_the_server_name_check_because_an_outer_name_proves_nothing', () => {
  const hidden = withExtra(realHello('node-registry.npmjs.org'), ECH_EXTENSION);
  assert.equal(inspectClientHello(hidden).reason, 'ech-refused');
  assert.equal(inspectClientHello(withExtra(realHello('node-no-sni'), ECH_EXTENSION)).reason, 'ech-refused');
});

test('Anything_that_is_not_a_tls_handshake_record_holding_a_hello_is_refused_as_not_tls', () => {
  const hello = realHello('node-registry.npmjs.org');
  const rows = {
    'an HTTP request': Buffer.from('GET / HTTP/1.1\r\nHost: registry.npmjs.org\r\n\r\n'),
    'four bytes of text, which is already too many to be a record header start': Buffer.from('ping'),
    'one byte that is not a handshake record': Buffer.from([0x47]),
    'an SSH banner': Buffer.from('SSH-2.0-OpenSSH_9.6\r\n'),
    'zeroes': Buffer.alloc(64),
    'an application-data record': Buffer.concat([Buffer.from([0x17, 3, 3]), hello.subarray(3)]),
    'a record version of 0x0200': Buffer.concat([Buffer.from([0x16, 2, 0]), hello.subarray(3)]),
    'a record over 16 KiB': Buffer.concat([Buffer.from([0x16, 3, 1, 0x50, 0x00]), Buffer.alloc(20480)]),
    'a handshake that is not a ClientHello': Buffer.concat([hello.subarray(0, 5), Buffer.from([2]), hello.subarray(6)]),
    'a hello over the cap': Buffer.concat([hello.subarray(0, 5), Buffer.from([1, 0xff, 0xff, 0xff]), hello.subarray(9)]),
    'a second record that is not a handshake': Buffer.concat([firstRecordOf(inRecords(hello, 2)), Buffer.from([0x17, 3, 3, 0, 4, 1, 2, 3, 4])]),
  };
  for (const [name, bytes] of Object.entries(rows)) {
    const r = inspectClientHello(bytes);
    assert.equal(r.status, 'refused', name);
    assert.equal(r.reason, 'not-tls', name);
  }
  assert.ok(HELLO_MAX_BYTES <= 16 * 1024);
});

test('A_hello_whose_inner_lengths_lie_is_refused_as_not_tls_and_never_read_past_its_end', () => {
  const hello = realHello('node-registry.npmjs.org');
  const body = hello.subarray(5);
  const lies = {
    'a session id longer than the message': Buffer.concat([hello.subarray(0, 5 + 4 + 2 + 32), Buffer.from([0xff]), hello.subarray(5 + 4 + 2 + 32 + 1)]),
    'an extension longer than the block': withExtensions(hello, [{ type: 1, bytes: Buffer.from([0, 1, 0xff, 0xff, 0]) }]),
    'a body of one byte': Buffer.concat([Buffer.from([0x16, 3, 1, 0, 5, 1, 0, 0, 1, 0])]),
    'a server_name list longer than its extension': withExtensions(hello, [extension(0, Buffer.from([0x00, 0x50, 0, 0, 3, 0x61]))]),
  };
  assert.ok(body.length > 100);
  for (const [name, bytes] of Object.entries(lies)) assert.equal(inspectClientHello(bytes).reason, 'not-tls', name);
});

test('Two_server_name_extensions_or_two_names_in_one_are_refused_because_the_server_could_pick_either', () => {
  const base = realHello('node-registry.npmjs.org');
  const twice = withExtensions(base, [...extensionsOf(base), sniExtension('www.cloudflare.com')]);
  assert.equal(inspectClientHello(twice).reason, 'not-tls');
  const twoNames = withExtensions(base, extensionsOf(base).map(r => (r.type === 0 ? sniExtension('registry.npmjs.org', 'www.cloudflare.com') : r)));
  assert.equal(inspectClientHello(twoNames).reason, 'not-tls');
});

test('A_server_name_list_holds_one_host_name_entry_and_nothing_else_so_a_type_the_proxy_ignores_cannot_hide_a_name', () => {
  const base = realHello('node-registry.npmjs.org');
  const u16 = n => Buffer.from([n >> 8, n & 0xff]);
  const entry = (type, name) => Buffer.concat([Buffer.from([type]), u16(name.length), Buffer.from(name, 'latin1')]);
  const row = (...entries) => { const list = Buffer.concat(entries); return extension(0, Buffer.concat([u16(list.length), list])); };
  const hello = r => withExtensions(base, extensionsOf(base).map(x => (x.type === 0 ? r : x)));
  // the reviewer's probe: an entry of another name type FIRST, then the host_name the proxy checks. A server that reads the first entry sees `evil.example`.
  const mixed = inspectClientHello(hello(row(entry(1, 'evil.example'), entry(0, 'registry.npmjs.org'))));
  assert.equal(mixed.status, 'refused', JSON.stringify(mixed));
  assert.equal(mixed.reason, 'not-tls');
  assert.match(mixed.why, /server_name/);
  const rows = {
    'a host_name then another type': row(entry(0, 'registry.npmjs.org'), entry(7, 'evil.example')),
    'only an entry of another type': row(entry(1, 'registry.npmjs.org')),
    'two host_names': row(entry(0, 'registry.npmjs.org'), entry(0, 'registry.npmjs.org')),
  };
  for (const [name, r] of Object.entries(rows)) assert.equal(inspectClientHello(hello(r)).status, 'refused', name);
  assert.deepEqual(inspectClientHello(hello(row(entry(0, 'registry.npmjs.org')))), { status: 'ok', sni: 'registry.npmjs.org', ech: false, esni: false }, 'control: the one host_name entry passes');
});

test('The_name_is_returned_as_sent_so_the_caller_compares_it_exactly', () => {
  const base = realHello('node-registry.npmjs.org');
  for (const name of ['REGISTRY.npmjs.org', 'registry.npmjs.org.', 'registry.npmjs.org\0.evil.example']) {
    assert.equal(inspectClientHello(withSni(base, name)).sni, name);
  }
});
