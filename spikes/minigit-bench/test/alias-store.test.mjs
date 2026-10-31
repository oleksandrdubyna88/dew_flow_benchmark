// The alias baseline of a series (D12): the first session sets it, a new alias extends it, a drift never rewrites it.
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { checkAliases } from '../lib/aliases.mjs';
import { aliasStore } from '../lib/alias-store.mjs';
import { tmpDir } from './helpers.mjs';

const rec = (aliases, internal = []) => ({ ok: true, value: { aliases, internal, fallbacks: [], unresolved: [] } });

test('The_first_session_sets_the_baseline_a_new_alias_extends_it_and_a_drift_never_rewrites_it', () => {
  const file = path.join(tmpDir('alias'), 'aliases.json');
  const store = aliasStore(file);
  assert.equal(store.get(), null);
  const first = rec({ opus: 'claude-opus-a' }, ['claude-haiku-x']);
  store.update(first, { verdict: 'ok', extend: {} });
  assert.deepEqual(store.get(), { aliases: { opus: 'claude-opus-a' }, internal: ['claude-haiku-x'] });
  const second = rec({ opus: 'claude-opus-a', sonnet: 'claude-sonnet-b' }, ['claude-haiku-x']);
  const check = checkAliases(store.get(), second.value);
  assert.equal(check.verdict, 'ok');
  store.update(second, check);
  assert.deepEqual(store.get().aliases, { opus: 'claude-opus-a', sonnet: 'claude-sonnet-b' }, 'a first use extends');
  const drift = rec({ opus: 'claude-opus-NEW' }, ['claude-haiku-x']);
  const stop = checkAliases(store.get(), drift.value);
  assert.equal(stop.verdict, 'stop');
  store.update(drift, stop);
  assert.equal(store.get().aliases.opus, 'claude-opus-a', 'a drift is a stop, not an update');
  assert.deepEqual(aliasStore(file).get(), store.get(), 'what was written is what a restart reads');
});

test('A_session_whose_alias_record_failed_changes_nothing_and_writes_nothing', () => {
  const file = path.join(tmpDir('alias'), 'aliases.json');
  const store = aliasStore(file);
  store.update({ ok: false, reason: 'two ids' }, { verdict: 'stop', extend: {} });
  assert.equal(store.get(), null);
  assert.equal(existsSync(file), false);
});
