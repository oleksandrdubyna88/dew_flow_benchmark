// How a blob's bytes become the text the scans read. A file with a NUL byte is either UTF-16 (decoded, by its BOM or because every other
// byte is NUL) or binary (read as latin1, so an ASCII token inside it is still found); either way it says `nul: true`, because a text
// scan of such a file is only as good as this guess. A file without a NUL byte is read as UTF-8.
const SAMPLE_BYTES = 512;
const MIN_UTF16_BYTES = 4;
const NUL_SHARE = 0.4;
const OTHER_SHARE = 0.1;

const swapped = bytes => Buffer.from(bytes.subarray(0, bytes.length - (bytes.length % 2))).swap16();

/** 'le' or 'be' when the NULs of the first bytes sit at every other position, else null. */
function utf16Order(buffer) {
  const sample = buffer.subarray(0, SAMPLE_BYTES);
  const pairs = Math.floor(sample.length / 2);
  const nulsAt = start => Array.from({ length: pairs }, (_, i) => sample[start + 2 * i]).filter(b => b === 0).length;
  const [even, odd] = [nulsAt(0) / pairs, nulsAt(1) / pairs];
  if (sample.length < MIN_UTF16_BYTES) return null;
  if (odd >= NUL_SHARE && even <= OTHER_SHARE) return 'le';
  return even >= NUL_SHARE && odd <= OTHER_SHARE ? 'be' : null;
}

function decodeNulBearing(buffer) {
  const [a, b] = buffer;
  if (a === 0xff && b === 0xfe) return buffer.subarray(2).toString('utf16le');
  if (a === 0xfe && b === 0xff) return swapped(buffer.subarray(2)).toString('utf16le');
  const order = utf16Order(buffer);
  if (order === null) return buffer.toString('latin1');
  return (order === 'le' ? buffer.subarray(0, buffer.length - (buffer.length % 2)) : swapped(buffer)).toString('utf16le');
}

/** `{ text, nul }` for the bytes of one blob. */
export function decodeBlob(buffer) {
  return buffer.includes(0) ? { text: decodeNulBearing(buffer), nul: true } : { text: buffer.toString('utf8'), nul: false };
}
