import assert from 'node:assert/strict';
import { chmodSync, mkdirSync, readFileSync, renameSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { TOKEN_FILE_PARTS, readToken, tokenStatus } from '../capture/capture-env.mjs';
import { PAT_FILE_PARTS, readSecretFile, secretStatus } from '../lib/secrets.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

const SECRET = 'sk-ant-fake-SENTINEL-0123456789';
const PARTS = ['.config', 'minigit-bench', 'a-secret'];

/** A home holding the secret file with the given modes. */
function homeWith({ mode = 0o600, text = `${SECRET}\n`, dirMode = 0o700 } = {}) {
  const home = tmpDir('secret-home');
  const dir = path.join(home, ...PARTS.slice(0, -1));
  mkdirSync(dir, { recursive: true });
  chmodSync(dir, dirMode);
  const file = path.join(dir, PARTS.at(-1));
  writeFileSync(file, text);
  chmodSync(file, mode);
  return { home, dir, file };
}

test('The_secrets_reader_is_the_captures', () => {
  const good = homeWith();
  const read = readSecretFile({ home: good.home, parts: PARTS });
  assert.deepEqual([read.ok, read.value, read.source], [true, SECRET, 'file']);
  assert.deepEqual(secretStatus({ home: good.home, parts: PARTS }), { present: true, source: 'file' });

  const refusals = [
    [homeWith({ mode: 0o644 }), /mode 644.*600/],
    [homeWith({ dirMode: 0o755 }), /directory.*700/],
    [homeWith({ text: '  \n' }), /empty/],
    [{ home: tmpDir('secret-none') }, /no secret/],
  ];
  for (const [{ home }, why] of refusals) {
    const r = readSecretFile({ home, parts: PARTS });
    assert.equal(r.ok, false, String(why));
    assert.match(r.reason, why);
    assert.equal(r.reason.includes('SENTINEL'), false, 'a reason never echoes the secret');
    if (!/empty/.test(String(why))) assert.equal(secretStatus({ home, parts: PARTS }).present, false, 'status refuses what the reader refuses, except an empty value it does not read');
  }

  const linked = homeWith();
  const real = path.join(linked.dir, 'real');
  renameSync(linked.file, real);
  symlinkSync(real, linked.file);
  assert.match(readSecretFile({ home: linked.home, parts: PARTS }).reason, /regular file/, 'a link is refused');

  const prior = process.env.CLAUDE_CODE_OAUTH_TOKEN;
  process.env.CLAUDE_CODE_OAUTH_TOKEN = 'from-the-environment';
  try {
    const none = readSecretFile({ home: tmpDir('secret-env-only'), parts: PARTS });
    assert.equal(none.ok, false, 'an environment variable has no mode to check, so it is not a secret');
    assert.equal(none.reason.includes('from-the-environment'), false);
  } finally {
    if (prior === undefined) delete process.env.CLAUDE_CODE_OAUTH_TOKEN; else process.env.CLAUDE_CODE_OAUTH_TOKEN = prior;
  }
});

test('The_capture_reads_its_token_through_the_shared_reader_and_the_pat_has_its_own_path', () => {
  const src = readFileSync(path.join(spikeDir, 'capture', 'capture-env.mjs'), 'utf8');
  assert.match(src, /from '\.\.\/lib\/secrets\.mjs'/, 'capture-env.mjs imports the reader');
  assert.equal(/lstatSync|statSync/.test(src.replace(/function insideGitTree[\s\S]*$/, '')), false, 'the mode checks live in secrets.mjs only');
  assert.deepEqual(TOKEN_FILE_PARTS, ['.config', 'minigit-bench', 'claude-oauth-token']);
  assert.deepEqual(PAT_FILE_PARTS, ['.config', 'minigit-bench', 'github-pat']);
  const home = homeWith();
  const dir = path.join(home.home, '.config', 'minigit-bench');
  writeFileSync(path.join(dir, 'claude-oauth-token'), `${SECRET}\n`, { mode: 0o600 });
  assert.equal(readToken({ home: home.home }).value, SECRET);
  assert.equal(tokenStatus({ home: home.home }).present, true);
});
