// The three verbs of the clear held-out folder (bench PR 4a): `heldout-open`, `heldout-close`, `heldout-check`, through the command line's `main`. Exit 0 done | 4 a refusal. What they say names tasks, counts and states only:
// never a case text, a case id, a trap name or a key (D41). Invented keys, folders and shares; the real ones are never read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { main } from '../explore.mjs';
import { backupConfigFile } from '../lib/heldout-backup-config.mjs';
import { captureStreams } from './quick-leak-repos-fixture.mjs';
import { IDS, TOKEN, TRAPS, sealedClosed } from './quick-heldout-fixture.mjs';

const seamsOf = (w, over = {}) => ({ home: w.home, frozenFile: w.frozenFile, heldout: { mountOf: w.args.mountOf, retry: w.args.retry, ...over } });
const keyFlags = w => ['--key-file', w.keyFile];
const backupFlags = w => ['--key-backup', w.backupFile, '--share-mount', w.shareMount];

/** One verb, said lines and exit code. */
async function verb(w, argv, over) {
  const said = [];
  const code = await main(argv, { ...seamsOf(w, over), say: line => said.push(line) });
  return { code, said: said.join('\n') };
}

test('The_verbs_open_check_and_close_a_sealed_task_with_exit_0_and_close_twice_is_a_no_op', async () => {
  const w = await sealedClosed();
  const open = await verb(w, ['heldout-open', '--task', '04', ...keyFlags(w)]);
  assert.equal(open.code, 0, open.said);
  assert.match(open.said, /task 04: the clear folder is open, made from the archive/);
  assert.equal(existsSync(w.casesDir), true);
  const again = await verb(w, ['heldout-open', '--task', '04', ...keyFlags(w)]);
  assert.match(again.said, /already open, used as it is/);
  const check = await verb(w, ['heldout-check', '--tasks', '04', ...keyFlags(w), ...backupFlags(w)]);
  assert.equal(check.code, 0, check.said);
  assert.match(check.said, /^task 04: ok; 3 cases, 1 traps; the archive opens with the key; the clear folder is present and equal to the archive, held by the operator; the key backup is verified$/);
  const close = await verb(w, ['heldout-close', '--task', '04', ...backupFlags(w)]);
  assert.equal(close.code, 0, close.said);
  assert.equal(existsSync(w.casesDir), false);
  const second = await verb(w, ['heldout-close', '--task', '04', ...backupFlags(w)]);
  assert.equal(second.code, 0, 'a second close is a no-op');
  assert.match(second.said, /nothing to remove/);
});

test('A_dead_guard_makes_the_open_exit_4_with_one_sentence_and_changes_nothing', async () => {
  const w = await sealedClosed();
  const guard = path.join(w.heldoutDir, '.clear.lock.takeover');
  const body = `${JSON.stringify({ pid: 2 ** 22 - 3, startTime: 'guard-start', at: '2026-11-08T08:30:00.000Z' })}\n`;
  writeFileSync(guard, body);
  const r = await verb(w, ['heldout-open', '--task', '04', ...keyFlags(w)], { isAlive: () => false });
  assert.equal(r.code, 4);
  for (const part of [guard, `pid ${2 ** 22 - 3}`, 'guard-start', '2026-11-08T08:30:00.000Z', 'not running with that start time', `no takeover is in progress; remove ${guard} and run again`]) assert.ok(r.said.includes(part), `${part}: ${r.said}`);
  assert.equal(readFileSync(guard, 'utf8'), body, 'the guard is untouched');
  assert.equal(existsSync(w.casesDir), false, 'no cases/');
  assert.equal(existsSync(path.join(w.heldoutDir, '.clear.lock')), false);
});

test('The_verbs_refuse_with_exit_4_a_missing_or_unknown_task_a_task_of_the_olympiad_and_a_check_with_no_tasks', async () => {
  const w = await sealedClosed();
  for (const argv of [['heldout-open'], ['heldout-open', '--task', '77'], ['heldout-close', '--task', '01'], ['heldout-check'], ['heldout-check', '--tasks', '04,77'], ['heldout-check', '--tasks', '01']]) {
    const r = await verb(w, argv);
    assert.equal(r.code, 4, argv.join(' '));
    assert.ok(r.said.length > 0, `${argv.join(' ')} says why`);
  }
});

test('Heldout_check_says_every_task_it_was_given_and_exits_4_when_any_refuses', async () => {
  const w = await sealedClosed();
  const none = await verb(w, ['heldout-check', '--tasks', '05,04', ...keyFlags(w)]);
  assert.equal(none.code, 4);
  assert.match(none.said, /^task 04: ok; 3 cases/m, 'the good task is said');
  assert.match(none.said, /^task 05: REFUSED: no entry: task 05 is not in /m, 'and the other is refused');
});

test('heldout_check_refuses_a_task_without_an_entry_or_whose_archive_differs', async () => {
  const w = await sealedClosed();
  const noEntry = await verb(w, ['heldout-check', '--tasks', '06', ...keyFlags(w)]);
  assert.equal(noEntry.code, 4);
  assert.match(noEntry.said, /no entry/);
  writeFileSync(path.join(w.heldoutDir, 'suite.enc'), 'bytes that are not the registered archive');
  const bent = await verb(w, ['heldout-check', '--tasks', '04', ...keyFlags(w)]);
  assert.equal(bent.code, 4);
  assert.match(bent.said, /archiveSha256/);
  const good = await sealedClosed();
  const ok = await verb(good, ['heldout-check', '--tasks', '04', ...keyFlags(good)]);
  assert.equal(ok.code, 0, ok.said);
  assert.match(ok.said, /3 cases, 1 traps/, 'the good case: counts only');
  assert.match(ok.said, /the key backup was not checked/, 'no backup named: said, not silently skipped');
});

test('The_backup_and_the_share_come_from_the_flags_or_the_private_file_and_never_from_a_default', async () => {
  const held = async () => { const w = await sealedClosed(); assert.equal((await verb(w, ['heldout-open', '--task', '04', ...keyFlags(w)])).code, 0); return w; };
  const none = await held();
  const refused = await verb(none, ['heldout-close', '--task', '04']);
  assert.equal(refused.code, 4);
  assert.match(refused.said, /no backup file is named: give --key-backup/);
  assert.equal(existsSync(none.casesDir), true, 'no default path: nothing removed');
  const viaFile = await held();
  const file = backupConfigFile(viaFile.home);
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, JSON.stringify({ keyBackup: viaFile.backupFile, shareMount: viaFile.shareMount }), { mode: 0o600 });
  const closed = await verb(viaFile, ['heldout-close', '--task', '04']);
  assert.equal(closed.code, 0, closed.said);
  assert.equal(existsSync(viaFile.casesDir), false, 'the private file names both');
  chmodSync(file, 0o640);
  const loose = await verb(viaFile, ['heldout-close', '--task', '04']);
  assert.equal(loose.code, 4);
  assert.match(loose.said, /mode 600/);
  const flagWins = await verb(viaFile, ['heldout-close', '--task', '04', ...backupFlags(viaFile)]);
  assert.equal(flagWins.code, 0, 'with both flags the file is not read');
  rmSync(file);
});

test('No_output_or_record_of_the_verbs_holds_a_case_text_case_id_or_trap_name', async () => {
  const w = await sealedClosed();
  const key = readKey(w.keyFile).value;
  const other = path.join(tmpDir('q-key-other'), 'heldout.key');
  writeFileSync(other, Buffer.alloc(32, 9), { mode: 0o600 });
  const runs = [];
  const seen = async argv => { const r = await captureStreams(() => main(argv, seamsOf(w))); runs.push([argv.join(' '), r]); return r; };
  assert.equal((await seen(['heldout-open', '--task', '04', ...keyFlags(w)])).value, 0);
  assert.equal((await seen(['heldout-check', '--tasks', '04', ...keyFlags(w), ...backupFlags(w)])).value, 0);
  writeFileSync(path.join(w.casesDir, 'h002.out'), 'changed after sealing\n');
  assert.equal((await seen(['heldout-open', '--task', '04', ...keyFlags(w)])).value, 4, 'the differs refusal (S5)');
  assert.equal((await seen(['heldout-check', '--tasks', '04', ...keyFlags(w)])).value, 4);
  assert.equal((await seen(['heldout-check', '--tasks', '04', '--key-file', other])).value, 4, 'the wrong key (S13)');
  rmSync(w.backupFile);
  assert.equal((await seen(['heldout-check', '--tasks', '04', ...keyFlags(w), ...backupFlags(w)])).value, 4, 'the backup that is gone (S15)');
  const registry = JSON.parse(readFileSync(w.frozenFile, 'utf8'));
  delete registry.tasks['04'];
  writeFileSync(w.frozenFile, JSON.stringify(registry));
  rmSync(w.casesDir, { recursive: true });
  rmSync(path.join(w.heldoutDir, '.open'));
  assert.equal((await seen(['heldout-check', '--tasks', '04', ...keyFlags(w)])).value, 4, 'the interrupted seal (S30)');
  const forbidden = [TOKEN, ...IDS, ...TRAPS, key.toString('hex'), key.toString('base64'), key.toString('latin1')];
  for (const [argv, r] of runs) {
    assert.ok(`${r.stdout}${r.stderr}`.length > 0, `${argv}: a capture of nothing proves nothing`);
    for (const secret of forbidden) assert.equal(`${r.stdout}${r.stderr}`.includes(secret), false, `${argv} says a case text, id, trap or key`);
  }
  const files = [w.frozenFile, path.join(w.heldoutDir, 'suite.enc')].map(f => readFileSync(f, 'latin1')).join('\n');
  for (const secret of [TOKEN, ...IDS, ...TRAPS]) assert.equal(files.includes(secret), false, 'the registry and the archive hold none');
});
