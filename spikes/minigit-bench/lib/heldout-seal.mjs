// Sealing the clear suite directory and reading the sealed one back: the committed files are suite.enc, suite.enc.sha256, manifest.json
// (and calibration.json, written by `calibrate`), plus one line in inputs/hashes.txt. This module knows the suite's SHAPE, not its cases.
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { packDir } from './heldout-archive.mjs';
import { readRegistry } from './heldout-calibrate.mjs';
import { readKey, seal, unseal } from './heldout-crypto.mjs';
import { parseShingles, parseTokens } from './heldout-leak.mjs';
import { readFileOrNull } from './inputs.mjs';
import { fail, ok } from './result.mjs';

export const HELDOUT_DIR = fileURLToPath(new URL('../heldout/', import.meta.url));
export const HASHES_FILE = fileURLToPath(new URL('../inputs/hashes.txt', import.meta.url));
export const sha256Hex = bytes => createHash('sha256').update(bytes).digest('hex');
const HASHES_PATH = 'heldout/suite.enc';

/** What the clear source directory must hold before it may be sealed; every missing piece is named. */
export function validateSource(src) {
  const registry = readRegistry(src);
  if (!registry.ok) return registry;
  const need = ['runner.py', 'refs/a/minigit', 'refs/b/minigit', 'leak-tokens.txt', 'leak-shingles.txt', ...registry.value.mutants.map(m => `mutants/${m.id}.json`)];
  const missing = need.filter(rel => !existsSync(path.join(src, rel)));
  if (missing.length > 0) return fail(`the source ${src} lacks: ${missing.join(', ')}`);
  const tokens = parseTokens(readFileSync(path.join(src, 'leak-tokens.txt'), 'utf8'));
  if (!tokens.ok) return tokens;
  const shingles = parseShingles(readFileSync(path.join(src, 'leak-shingles.txt'), 'utf8'));
  return shingles.ok ? registry : shingles;
}

export const countsOf = registry => ({
  n1: registry.cases.filter(c => c.scored && c.group === 'H1').length,
  n2: registry.cases.filter(c => c.scored && c.group === 'H2').length,
  m: registry.mutants.length,
});

/** Pure: the hashes.txt text with the `heldout/suite.enc` line replaced (or appended when it is not there yet). */
export function withHashesLine(text, sha) {
  const line = `${sha}  ${HASHES_PATH}`;
  const lines = text.split('\n').filter(l => l.trim() !== '' && !l.endsWith(`  ${HASHES_PATH}`));
  return `${[...lines, line].join('\n')}\n`;
}

/** Seal `src` with the key and write the committed files under `outDir`. ok(manifest) | fail (key trouble carries `env: true`). */
export function sealSuite({ src, keyFile, outDir = HELDOUT_DIR, hashesFile = HASHES_FILE, today = new Date().toISOString().slice(0, 10) }) {
  const valid = validateSource(src);
  if (!valid.ok) return valid;
  const key = readKey(keyFile);
  if (!key.ok) return { ...key, env: true };
  const packed = packDir(src);
  if (!packed.ok) return packed;
  const sealed = seal(packed.value, key.value);
  const back = unseal(sealed, key.value);
  if (!back.ok || !back.value.equals(packed.value)) return fail('the sealed blob does not round-trip: nothing was written');
  const manifest = { format: 1, plaintextSha256: sha256Hex(packed.value), ...countsOf(valid.value), sealedOn: today };
  mkdirSync(outDir, { recursive: true });
  writeFileSync(path.join(outDir, 'suite.enc'), sealed, { mode: 0o644 });
  writeFileSync(path.join(outDir, 'suite.enc.sha256'), `${sha256Hex(sealed)}  suite.enc\n`);
  writeFileSync(path.join(outDir, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  writeFileSync(hashesFile, withHashesLine(existsSync(hashesFile) ? readFileSync(hashesFile, 'utf8') : '', sha256Hex(sealed)));
  return ok(manifest);
}

/** The sealed files as committed: ok({ sealed, manifest }) when suite.enc matches its .sha256 line; otherwise a fail naming the file. */
export function readSealedFiles(outDir = HELDOUT_DIR) {
  const enc = readFileOrNull(path.join(outDir, 'suite.enc'));
  const line = readFileOrNull(path.join(outDir, 'suite.enc.sha256'));
  const manifest = readFileOrNull(path.join(outDir, 'manifest.json'));
  const absent = [['suite.enc', enc], ['suite.enc.sha256', line], ['manifest.json', manifest]].filter(([, v]) => v === null).map(([n]) => n);
  if (absent.length > 0) return fail(`${outDir} lacks ${absent.join(', ')}`);
  if (!line.toString('utf8').startsWith(`${sha256Hex(enc)}  suite.enc`)) return fail('suite.enc does not match suite.enc.sha256');
  return ok({ sealed: enc, manifest: JSON.parse(manifest.toString('utf8')) });
}
