// A session is charged once PER SERIES AND RID. A new series reuses the registered seed, so its rids equal an earlier series' rids, and `init` copies the earlier series' spend lines into the new ledger tagged `fromSeries`: such a
// copy is the earlier series' charge, never this series' own (series b82e, pair 1: USD 0.9569538 of its own data sessions was never charged because the copies held the same rids).
import './fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdtempSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { appendChargeOnce, appendLedger, readLedger } from '../lib/ledger.mjs';
import { spendTotals } from '../lib/spend-gate.mjs';

const ledgerFile = () => path.join(mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-mgb-ledger-')), 'ledger.ndjson');
const copy = (rid, usd, fromSeries = 's-20261030-bf89') => ({ kind: 'data', usd, what: 'completed', rid, t: '2026-10-30T10:00:00.000Z', fromSeries });
const own = (rid, usd) => ({ kind: 'data', usd, what: 'completed', rid, t: '2026-10-30T20:00:00.000Z' });

test('A_series_charges_its_own_session_once_though_its_ledger_holds_an_earlier_series_line_for_the_same_rid', () => {
  const file = ledgerFile();
  appendLedger(file, copy('x1', 0.9));
  assert.equal(appendChargeOnce(file, own('x1', 0.95)), true, 'the copy is the earlier series charge');
  assert.deepEqual(readLedger(file).map(l => [l.rid, l.usd, l.fromSeries]), [['x1', 0.9, 's-20261030-bf89'], ['x1', 0.95, undefined]]);
  assert.equal(spendTotals(readLedger(file)).bareUsd, 0.9 + 0.95, 'both are spend: the $40 and the $500 see all of it');
});

test('A_second_charge_of_the_own_rid_is_skipped', () => {
  const file = ledgerFile();
  appendLedger(file, copy('x1', 0.9));
  assert.equal(appendChargeOnce(file, own('x1', 0.95)), true);
  assert.equal(appendChargeOnce(file, own('x1', 0.95)), false, 'a resume that settles the session again');
  assert.equal(appendChargeOnce(file, own('x1', 1.5)), false, 'whatever the figure');
  assert.deepEqual(readLedger(file).filter(l => l.fromSeries === undefined).map(l => l.rid), ['x1']);
});

test('A_copy_from_two_earlier_series_and_an_own_charge_of_another_rid_do_not_hide_each_other', () => {
  const file = ledgerFile();
  appendLedger(file, copy('x1', 1, 's-20261029-aaaa'));
  appendLedger(file, copy('x1', 2, 's-20261030-bf89'));
  appendLedger(file, { event: 'reclassify', rid: 'x2', from: 'void', to: 'completed', t: 't' });
  assert.equal(appendChargeOnce(file, own('x2', 3)), true, 'an event line is no charge');
  assert.equal(appendChargeOnce(file, own('x1', 4)), true);
  assert.equal(appendChargeOnce(file, own('x1', 4)), false);
  assert.equal(readLedger(file).length, 5);
});
