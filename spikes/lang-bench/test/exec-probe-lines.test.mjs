// E1.S8b brief B: the probe's lines — judged purely from what the box and the host said. The facts the box prints are parsed and each line of the plan's probe list is judged on its own, so
// the probe can exit 3 NAMING the first line that failed. A judge that never fails would let a leaking box through (the refuted shape), so every line has a bad input that is found.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ALLOW_LIST } from '../lib/exec-layer/env.mjs';
import { judgeFacts, parseFacts, recordProblems } from '../lib/exec-probe/facts.mjs';

const GOOD = [
  'ns\tnet:[4026532285]', 'cgroup\t0::/user.slice/user-1000.slice/user@1000.service/app.slice/x.scope/prog', 'sentinel\tabsent', 'mnt-c\tabsent', 'cmd-exe\t127', 'user-bus\t1',
  'stderr-bytes\t0', 'lang\tde_DE.UTF-8', 'lc-all\tde_DE.UTF-8', 'locale-de\tyes', 'invariant\tnone', 'cgroup-visible\tno', `env\t${ALLOW_LIST.join(',')}`,
].join('\n');
const without = (name, replacement) => GOOD.split('\n').map(line => (line.startsWith(`${name}\t`) ? replacement : line)).join('\n');
const failed = text => judgeFacts(parseFacts(text)).filter(line => !line.ok).map(line => line.id);

test('Facts_the_box_prints_are_parsed_by_name_and_a_good_box_fails_no_line', () => {
  assert.equal(parseFacts(GOOD).ns, 'net:[4026532285]');
  assert.deepEqual(failed(GOOD), []);
  const ids = judgeFacts(parseFacts(GOOD)).map(line => line.id);
  for (const id of ['home-sentinel', 'mnt-c', 'cmd-exe', 'user-bus', 'bash-c-true-stderr', 'culture-env', 'locale-a', 'invariant-switch', 'env-keys']) assert.ok(ids.includes(id), `${id} is a line`);
});

test('Each_bad_fact_fails_exactly_its_own_line_and_the_first_failed_line_names_the_probe_halt', () => {
  assert.deepEqual(failed(without('sentinel', 'sentinel\treadable')), ['home-sentinel']);
  assert.deepEqual(failed(without('mnt-c', 'mnt-c\tpresent')), ['mnt-c']);
  assert.deepEqual(failed(without('cmd-exe', 'cmd-exe\t0')), ['cmd-exe']);
  assert.deepEqual(failed(without('user-bus', 'user-bus\t0')), ['user-bus']);
  assert.deepEqual(failed(without('stderr-bytes', 'stderr-bytes\t83')), ['bash-c-true-stderr']);
  assert.deepEqual(failed(without('lang', 'lang\tC.UTF-8')), ['culture-env']);
  assert.deepEqual(failed(without('lc-all', 'lc-all\t')), ['culture-env']);
  assert.deepEqual(failed(without('locale-de', 'locale-de\tno')), ['locale-a']);
  assert.deepEqual(failed(without('invariant', 'invariant\tDOTNET_SYSTEM_GLOBALIZATION_INVARIANT')), ['invariant-switch']);
  assert.deepEqual(failed(without('env', `env\t${ALLOW_LIST.join(',')},WSL_INTEROP`)), ['env-keys']);
});

test('A_fact_the_box_did_not_print_is_a_failed_line_and_never_a_pass', () => {
  assert.deepEqual(failed(GOOD.split('\n').filter(line => !line.startsWith('mnt-c')).join('\n')), ['mnt-c']);
  assert.ok(judgeFacts(parseFacts('')).every(line => !line.ok), 'an empty output passes nothing');
});

test('The_probe_record_holds_only_allow_listed_keys', () => {
  const record = { lines: [{ id: 'env-keys', envKeys: [...ALLOW_LIST] }], memoryView: { observations: { limits: [] } } };
  assert.deepEqual(recordProblems(record), []);
});

test('The_record_check_still_finds_a_planted_host_key_wherever_the_record_holds_keys', () => {
  assert.match(recordProblems({ lines: [{ id: 'env-keys', envKeys: [...ALLOW_LIST, 'WSL_INTEROP'] }] }).join('\n'), /WSL_INTEROP/);
  assert.match(recordProblems({ envKeys: ['SECRET_TOKEN'] }).join('\n'), /SECRET_TOKEN/);
  assert.match(recordProblems({ lines: [{ nested: { envKeys: ['HOME_PLANTED'] } }] }).join('\n'), /HOME_PLANTED/, 'at any depth');
});
