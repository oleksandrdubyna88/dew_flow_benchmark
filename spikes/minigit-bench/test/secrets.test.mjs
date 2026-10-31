import assert from 'node:assert/strict';
import { chmodSync, mkdirSync, readFileSync, renameSync, symlinkSync, writeFileSync } from 'node:fs';
import os from 'node:os';
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

// N18: a test run must never reach the operator's real secret files. The guard looks at the HOME it is given and acts before any file is looked at, so these cases name a file
// that does not exist: an absent file is a plain failure when there is no guard, and a thrown refusal when there is one, and neither reads a secret.
const realHome = os.userInfo().homedir;
const NOT_A_SECRET = ['.config', 'minigit-bench', 'testsToDelete-no-such-secret'];

test('The_reader_refuses_the_operators_real_home_when_it_is_called_under_the_test_runner', () => {
  assert.throws(() => readSecretFile({ home: realHome, parts: NOT_A_SECRET, label: 'token' }), /test runner.*real home|real home.*test runner/s);
  assert.throws(() => readSecretFile({ home: `${realHome}${path.sep}.`, parts: NOT_A_SECRET }), /test runner/, 'the same directory spelled another way');
  const link = path.join(tmpDir('home-link'), 'h');
  symlinkSync(realHome, link);
  assert.throws(() => readSecretFile({ home: link, parts: NOT_A_SECRET }), /test runner/, 'a link to the real home is the real home');
  assert.equal(readSecretFile({ home: tmpDir('another-home'), parts: NOT_A_SECRET }).ok, false, 'control: any other home is a plain failure, not a refusal');
});

test('The_refusal_names_no_secret_and_tells_the_test_how_to_opt_in', () => {
  try {
    readSecretFile({ home: realHome, parts: NOT_A_SECRET, label: 'token' });
    assert.fail('the reader did not refuse');
  } catch (e) {
    assert.match(e.message, /allowRealSecretUnderTest/);
    assert.equal(e.message.includes(realHome + path.sep + '.config'), false, 'the refusal does not name the secret file');
  }
});
