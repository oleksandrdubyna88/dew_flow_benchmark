// How a blob's bytes become the text the scans read: UTF-16 is decoded (BOM or not), anything else with a NUL byte is read as latin1
// and reported as NUL-bearing, so no file hides text from the scans by its encoding.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { decodeBlob } from '../lib/push-decode.mjs';

const LE = text => Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from(text, 'utf16le')]);
const BE = text => Buffer.concat([Buffer.from([0xfe, 0xff]), Buffer.from(text, 'utf16le').swap16()]);

test('A_plain_utf8_file_is_text_with_no_nul', () => {
  assert.deepEqual(decodeBlob(Buffer.from('plain text\n')), { text: 'plain text\n', nul: false });
});

test('A_utf16_file_with_a_bom_is_decoded_in_either_byte_order_and_reported_as_nul_bearing', () => {
  assert.deepEqual(decodeBlob(LE('hello there\n')), { text: 'hello there\n', nul: true });
  assert.deepEqual(decodeBlob(BE('hello there\n')), { text: 'hello there\n', nul: true });
});

test('A_utf16_file_without_a_bom_is_decoded_when_every_other_byte_is_nul', () => {
  assert.equal(decodeBlob(Buffer.from('some words in sixteen bit units\n', 'utf16le')).text, 'some words in sixteen bit units\n');
  assert.equal(decodeBlob(Buffer.from('some words in sixteen bit units\n', 'utf16le').swap16()).text, 'some words in sixteen bit units\n');
});

test('Other_binary_content_is_read_as_latin1_so_an_ascii_token_inside_it_is_still_found', () => {
  const bytes = Buffer.concat([Buffer.from([0, 1, 2, 0xff, 0]), Buffer.from('token-in-a-binary'), Buffer.from([0, 0, 9])]);
  const r = decodeBlob(bytes);
  assert.equal(r.nul, true);
  assert.ok(r.text.includes('token-in-a-binary'));
});

test('A_file_whose_bytes_are_not_valid_utf8_but_hold_no_nul_is_not_reported_as_nul_bearing', () => {
  assert.equal(decodeBlob(Buffer.from([0x63, 0x61, 0x66, 0xe9, 0x0a])).nul, false);
});
