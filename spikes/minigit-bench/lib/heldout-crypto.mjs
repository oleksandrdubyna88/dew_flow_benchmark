// The held-out suite's key and its sealed form (parent D15). The key is a mode-600 file the scorer reads itself, outside every sandbox;
// it is NEVER read from, or written to, an environment variable. Sealed = AES-256-GCM: MAGIC || 12-byte random IV || ciphertext ||
// 16-byte tag, AAD = MAGIC. Every expected failure is a value; nothing here throws past its boundary for a bad key or a bent blob.
import { createCipheriv, createDecipheriv, randomBytes } from 'node:crypto';
import { closeSync, constants, fstatSync, lstatSync, mkdirSync, openSync, readSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from './result.mjs';

export const MAGIC = 'MGHO1\n';
const IV_BYTES = 12;
const TAG_BYTES = 16;
const KEY_BYTES = 32;
const AAD = Buffer.from(MAGIC, 'latin1');

export const defaultKeyPath = () => path.join(os.homedir(), '.config', 'minigit-bench', 'heldout.key');

/** lstat first (a link is refused, never followed), then the owner, the mode and the size; the bytes are read from the same open file. */
export function readKey(file = defaultKeyPath()) {
  let st;
  try {
    st = lstatSync(file);
  } catch (e) {
    if (e.code === 'ENOENT' || e.code === 'ENOTDIR') return fail(`no key file: ${file} does not exist (run: heldout-tool.mjs keygen)`);
    throw e;
  }
  if (!st.isFile()) return fail(`${file} is not a regular file (a link or a directory is refused)`);
  if (st.uid !== process.getuid()) return fail(`${file} is not owned by the current user (uid ${st.uid})`);
  if ((st.mode & 0o077) !== 0) return fail(`${file} has mode ${(st.mode & 0o777).toString(8)}: group and others must have no access (chmod 600)`);
  if (st.size !== KEY_BYTES) return fail(`${file} has size ${st.size}: a key is exactly ${KEY_BYTES} bytes`);
  const dir = lstatSync(path.dirname(file));
  if (!dir.isDirectory() || dir.uid !== process.getuid() || (dir.mode & 0o077) !== 0) {
    return fail(`the directory ${path.dirname(file)} of the key must be a real directory owned by the current user with mode 700 (it is ${(dir.mode & 0o777).toString(8)})`);
  }
  const fd = openSync(file, constants.O_RDONLY | constants.O_NOFOLLOW);
  try {
    const bytes = Buffer.alloc(KEY_BYTES + 1);
    const got = readSync(fd, bytes, 0, bytes.length, 0);
    return got === KEY_BYTES && fstatSync(fd).size === KEY_BYTES ? ok(bytes.subarray(0, KEY_BYTES)) : fail(`${file} changed while it was read`);
  } finally {
    closeSync(fd);
  }
}

/** 32 random bytes, `wx` and mode 600, the directory 700. An existing file is refused, never overwritten. */
export function generateKey(file = defaultKeyPath()) {
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  try {
    writeFileSync(file, randomBytes(KEY_BYTES), { mode: 0o600, flag: 'wx' });
  } catch (e) {
    if (e.code === 'EEXIST') return fail(`${file} already exists: refusing to overwrite a key (move it away on purpose if it must be replaced)`);
    throw e;
  }
  return ok(file);
}

export function seal(plain, key) {
  const iv = randomBytes(IV_BYTES);
  const cipher = createCipheriv('aes-256-gcm', key, iv);
  cipher.setAAD(AAD);
  const body = Buffer.concat([cipher.update(plain), cipher.final()]);
  return Buffer.concat([AAD, iv, body, cipher.getAuthTag()]);
}

/** ok(plaintext) | fail('authentication failed') for any tamper or wrong key | fail('not a sealed suite: ...') for a blob of the wrong shape. */
export function unseal(sealed, key) {
  const minimum = AAD.length + IV_BYTES + TAG_BYTES;
  if (sealed.length < minimum || !sealed.subarray(0, AAD.length).equals(AAD)) return fail('not a sealed suite: wrong magic or too short');
  const iv = sealed.subarray(AAD.length, AAD.length + IV_BYTES);
  const body = sealed.subarray(AAD.length + IV_BYTES, sealed.length - TAG_BYTES);
  const decipher = createDecipheriv('aes-256-gcm', key, iv);
  decipher.setAAD(AAD);
  decipher.setAuthTag(sealed.subarray(sealed.length - TAG_BYTES));
  try {
    return ok(Buffer.concat([decipher.update(body), decipher.final()]));
  } catch (e) {
    if (e.code === 'ERR_CRYPTO_INVALID_TAG' || /unable to authenticate/i.test(e.message)) return fail('authentication failed');
    throw e;
  }
}
