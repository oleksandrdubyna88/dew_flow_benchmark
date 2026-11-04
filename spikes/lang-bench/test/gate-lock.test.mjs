// E1.S4: the D40 gate lock (`lib/gate-lock.mjs`): one exclusive file, the owner's record in it, refused while its owner may be alive, removed by the next taker only when its owner is proven gone.
// Every lock here is in an owned folder: the tests never meet the real `~/.cache/lang-v2/gate.lock`.
import assert from 'node:assert/strict';
import { readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { thisOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { takeGateLock } from '../lib/gate-lock.mjs';
import { exitedPid, recordOf } from './support/owners.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const lockIn = t => path.join(ownedFolder(t, 'gl'), 'sub', 'gate.lock');

test('A_held_gate_lock_refuses_naming_its_owner_and_never_waits', t => {
  const file = lockIn(t);
  const first = takeGateLock({ file });
  assert.equal(first.ok, true, first.reason);
  assert.equal(first.value.text, recordOf({}), 'the lock holds this run\'s owner record');
  assert.equal(readFileSync(file, 'utf8'), first.value.text);
  assert.equal(statSync(file).mode & 0o777, 0o600, 'only the owner reads or writes it');
  assert.equal(statSync(path.dirname(file)).mode & 0o777, 0o700, 'its folder is the owner\'s alone');
  const started = Date.now();
  const second = takeGateLock({ file });
  assert.equal(second.ok, false);
  assert.equal(second.held, true);
  assert.equal(second.reason, `gate lock held by ${first.value.text.trim()}`, 'the refusal names the owner text');
  assert.ok(Date.now() - started < 1000, 'a held lock is refused at once, never waited for');
  assert.equal(readFileSync(file, 'utf8'), first.value.text, 'and left as it was');
  const elsewhere = recordOf({ host: 'another-host@linux' });
  writeFileSync(file, elsewhere);
  // fix batch 1 (L4): an `unknown` owner (another host here) is not "held by": it says the state is unknown and names the manual step
  const unknown = takeGateLock({ file }).reason;
  assert.match(unknown, /owner state unknown/, 'a lock of another host is never judged: refused as unknown');
  assert.match(unknown, new RegExp(`remove ${file.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} by hand if no gate run is going`));
  assert.equal(readFileSync(file, 'utf8'), elsewhere);
});

test('A_gate_lock_whose_owner_is_gone_is_removed_by_the_next_taker_naming_it', t => {
  const file = lockIn(t);
  const stale = recordOf({ pid: exitedPid() });
  const said = [];
  assert.equal(takeGateLock({ file }).ok, true);
  writeFileSync(file, stale);
  const taken = takeGateLock({ file, say: line => said.push(line) });
  assert.equal(taken.ok, true, taken.reason);
  assert.deepEqual(said, [`removed a gate lock whose owner is gone: ${stale.trim()}`]);
  assert.equal(readFileSync(file, 'utf8'), taken.value.text, 'the new lock is the taker\'s');
  assert.equal(takeGateLock({ file }).ok, false, 'and it holds: the same file now refuses the next one');
});

test('A_lock_that_is_replaced_while_its_owner_is_judged_is_not_removed', t => {
  const file = lockIn(t);
  const stale = recordOf({ pid: exitedPid() });
  const successor = recordOf({ host: 'another-host@linux' });
  assert.equal(takeGateLock({ file }).ok, true);
  writeFileSync(file, stale);
  // the probe runs between the first read and the removal; a successor takes the lock there
  const probe = pid => { writeFileSync(file, successor); return { state: pid === thisOwner().pid ? 'live' : 'dead' }; };
  const taken = takeGateLock({ file, probe });
  assert.equal(taken.ok, false, 'the text changed since it was judged: not the lock that was judged');
  assert.equal(readFileSync(file, 'utf8'), successor, 'the successor\'s lock is untouched');
});

test('An_unreadable_gate_lock_is_refused_and_left_in_place', t => {
  const file = lockIn(t);
  assert.equal(takeGateLock({ file }).ok, true);
  for (const text of ['', 'not an owner record\n', `${recordOf({})}${recordOf({})}`, `${'x'.repeat(600)}\n`]) {
    writeFileSync(file, text);
    const refused = takeGateLock({ file });
    assert.equal(refused.ok, false, JSON.stringify(text.slice(0, 20)));
    assert.match(refused.reason, /gate lock .*unreadable/, 'it says the lock cannot be read');
    assert.equal(refused.held, true);
    assert.equal(readFileSync(file, 'utf8'), text, 'and leaves it in place for a person to look at');
  }
});

test('Releasing_removes_only_the_lock_this_taker_wrote', t => {
  const file = lockIn(t);
  const mine = takeGateLock({ file });
  assert.equal(mine.ok, true);
  const other = recordOf({ host: 'another-host@linux' });
  writeFileSync(file, other);
  mine.value.release();
  assert.equal(readFileSync(file, 'utf8'), other, 'a lock another taker put there since is not ours to remove');
  writeFileSync(file, mine.value.text);
  mine.value.release();
  assert.throws(() => statSync(file), { code: 'ENOENT' }, 'the lock this taker wrote is removed');
  mine.value.release();
  assert.equal(takeGateLock({ file }).ok, true, 'and the next taker gets it');
});
