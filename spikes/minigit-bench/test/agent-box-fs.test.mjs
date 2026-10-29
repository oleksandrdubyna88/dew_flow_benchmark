import assert from 'node:assert/strict';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { agentBoxPrefix } from '../lib/sandbox.mjs';
import { runBounded } from '../lib/run.mjs';
import { TS_DIR, boxOptions, envFor, hostHas, newSession, probe, probeArgv } from './box-helpers.mjs';
import { homeDir, sandboxGate } from './helpers.mjs';

test('The_agent_box_has_no_operator_home_and_no_key', async t => {
  if (!sandboxGate(t)) return;
  // a decoy "home" holding a decoy key and a decoy token, told to the box as the operator home (E1's shape)
  const fakeHome = mkdtempSync(path.join(os.homedir(), 'testsToDelete-mgb-ahome-'));
  const dir = path.join(fakeHome, '.config', 'minigit-bench');
  mkdirSync(dir, { recursive: true });
  const files = ['heldout.key', 'claude-oauth-token'].map(f => path.join(dir, f));
  files.forEach(f => writeFileSync(f, 'secret-bytes\n'));
  try {
    assert.equal(readFileSync(files[0], 'utf8'), 'secret-bytes\n', 'control: the key is readable outside the box');
    const layout = newSession();
    const hidden = await probe(layout, `echo "listing=[$(ls -A ${fakeHome})]"; cat ${files.join(' ')} 2>&1`, { over: { operatorHome: fakeHome } });
    assert.equal(hidden.out.includes('secret-bytes'), false);
    assert.match(hidden.out, /listing=\[\]/, 'the decoy home is an empty tmpfs');
    mkdirSync(path.join(fakeHome, 'elsewhere'));
    const open = await probeArgv(agentBoxPrefix(boxOptions(layout, { operatorHome: path.join(fakeHome, 'elsewhere') })), `cat ${files[0]}`, envFor(layout));
    assert.match(open.out, /secret-bytes/, 'control: with another directory as the hidden home the same key IS readable');
    const real = await probe(layout, `ls -A ${os.homedir()}`);
    for (const entry of ['.config', '.ssh', '.claude', '.claude.json', '.bash_history', '.gitconfig']) assert.equal(real.out.split('\n').includes(entry), false, `the real home shows no ${entry}`);
    const realToken = path.join(os.homedir(), '.config', 'minigit-bench', 'claude-oauth-token');
    if (existsSync(realToken)) assert.match((await probe(layout, `test -e ${realToken} && echo VISIBLE || echo ABSENT`)).out, /ABSENT/, 'the operator\'s own token file is absent (control: it exists on the host)');
  } finally {
    rmSync(fakeHome, { recursive: true, force: true });
  }
});

test('The_agent_box_has_no_windows_drive_and_no_interop', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const r = await probe(layout, 'ls -A /mnt | wc -l; echo "interop=[$WSL_INTEROP]"; ls /run/WSL 2>&1 | head -1');
  assert.match(r.out, /^0\n/, '/mnt is empty');
  assert.match(r.out, /interop=\[\]/);
  assert.match(r.out, /No such file|cannot access/, '/run/WSL is absent');
  const whoami = '/mnt/c/Windows/System32/whoami.exe';
  if (!existsSync(whoami)) { t.skip('/mnt/c is absent on this host: there is no Windows interop to refuse'); return; }
  const copy = path.join(layout.work, 'who.exe');
  copyFileSync(whoami, copy);
  const outside = await runBounded(copy, [], { timeoutMs: 20_000, cwd: layout.work });
  if (!outside.ok || !outside.value.stdout.includes('\\')) { t.skip('the copy of whoami.exe prints no user name even outside the box: interop is off on this host'); return; }
  const inside = await probe(layout, `chmod +x ${copy}; ${copy} 2>&1; echo "rc=$?"`);
  assert.equal(inside.out.includes('\\'), false, 'no Windows user name is printed inside');
  assert.match(inside.out, /rc=[1-9]/, 'the exe does not run to success (the interop handler dies on /proc/1/stat)');
});

test('The_agent_box_cannot_see_its_instruments_or_its_partner', async t => {
  if (!sandboxGate(t)) return;
  const root = homeDir('as-pair');
  const layout = newSession({ rid: 'a', root });
  const partner = newSession({ rid: 'b', root });
  const names = ['stream.ndjson', 'egress.ndjson', 'broker.ndjson', 'state.json'];
  names.forEach(n => writeFileSync(path.join(layout.dir, n), 'x'));
  for (const d of ['otel', 'score', 'coai']) { mkdirSync(path.join(layout.dir, d), { recursive: true }); writeFileSync(path.join(layout.dir, d, 'x'), 'x'); }
  writeFileSync(path.join(partner.work, 'x'), 'partner');
  writeFileSync(path.join(layout.work, 'mine'), 'x');
  writeFileSync(layout.files.settings, '{}');
  const scoreTmp = path.join(os.homedir(), 'rsd', 'minigit', 'score-tmp');
  const hiddenPaths = [...names.map(n => path.join(layout.dir, n)), ...['otel', 'score', 'coai'].map(d => path.join(layout.dir, d)), path.join(partner.work, 'x'), scoreTmp];
  const script = hiddenPaths.map(p => `test -e ${p} && echo "SEEN ${p}" || echo "UNSEEN ${p}"`).join('\n');
  const r = await probe(layout, `${script}\ntest -e ${layout.work}/mine && echo "SEEN work"; test -e ${layout.files.settings} && echo "SEEN settings"; touch ${layout.harness}/x 2>/dev/null; echo "harness=$?"; touch ${layout.net}/x 2>/dev/null; echo "net=$?"`);
  assert.equal(r.out.split('\n').filter(l => l.startsWith('SEEN ') && !['SEEN work', 'SEEN settings'].includes(l)).length, 0, `no instrument is visible: ${r.out}`);
  assert.equal(r.out.split('\n').filter(l => l.startsWith('UNSEEN ')).length, hiddenPaths.length);
  assert.match(r.out, /SEEN work/, 'control: box/work is visible');
  assert.match(r.out, /SEEN settings/, 'control: harness/settings.json is visible');
  assert.match(r.out, /harness=[1-9]/);
  assert.match(r.out, /net=[1-9]/);
  for (const p of hiddenPaths.filter(h => h !== scoreTmp)) assert.ok(existsSync(p), `control: ${p} exists on the host`);
});

test('The_bare_box_has_no_gh', async t => {
  if (!sandboxGate(t)) return;
  if (!existsSync('/usr/bin/gh')) { t.skip('/usr/bin/gh is absent on this host: there is nothing to refuse'); return; }
  const layout = newSession();
  const r = await probe(layout, 'gh --version 2>&1; echo "rc=$?"; /usr/bin/gh --version 2>&1; echo "rc=$?"');
  assert.equal((r.out.match(/Permission denied/g) ?? []).length, 2, r.out);
  const outside = await runBounded('/usr/bin/gh', ['--version'], { timeoutMs: 20_000 });
  assert.equal(outside.value.code, 0, 'control: gh runs outside the box');
  const process_ = await probeArgv(agentBoxPrefix(boxOptions(layout, { arm: 'process' })), '/usr/bin/gh --version', envFor(layout));
  assert.match(process_.out, /gh version/, 'control: the process arm\'s box does not block it');
});

test('The_toolchain_is_read_only_and_tsc_is_the_pinned_one', async t => {
  if (!sandboxGate(t)) return;
  if (!existsSync(path.join(TS_DIR, 'node_modules', '.bin', 'tsc'))) { t.skip(`${TS_DIR} is absent: the preflight installs it (npm install --prefix, then chmod -R a-w)`); return; }
  const layout = newSession();
  const r = await probe(layout, `tsc --version; touch ${TS_DIR}/x 2>&1; echo "rc=$?"`, { over: { toolchains: [TS_DIR] }, env: envFor(layout, { tsDir: TS_DIR }) });
  assert.match(r.out, /7\.0\.2/);
  assert.match(r.out, /Read-only file system/);
  assert.equal(existsSync(path.join(TS_DIR, 'x')), false);
  const notBound = await probe(layout, `ls ${TS_DIR} 2>&1 | head -1`);
  assert.match(notBound.out, /No such file/, 'control: unbound, the toolchain is not in the box');
});

test('The_box_can_run_dotnet_and_reports_the_one_pinned_sdk', async t => {
  if (!sandboxGate(t)) return;
  if (!hostHas('dotnet')) { t.skip('dotnet is not on PATH on this host'); return; }
  const layout = newSession();
  const r = await probe(layout, 'dotnet --list-sdks');
  assert.match(r.out, /^10\.0\.113 \[/m);
  assert.equal(r.out.trim().split('\n').length, 1, 'exactly one SDK');
});
