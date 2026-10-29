import assert from 'node:assert/strict';
import { test } from 'node:test';
import { EGRESS_HOSTS, KNOWN_REFUSED } from '../lib/egress-hosts.mjs';

const GITHUB_HOSTS = ['github.com', 'api.github.com', 'codeload.github.com'];

test('The_bare_arm_reaches_the_two_registries_and_nothing_else', () => {
  assert.deepEqual([...EGRESS_HOSTS.bare], ['registry.npmjs.org', 'api.nuget.org']);
});

test('The_process_arm_is_the_bare_list_plus_the_github_hosts', () => {
  assert.deepEqual([...EGRESS_HOSTS.process], [...EGRESS_HOSTS.bare, ...GITHUB_HOSTS]);
});

test('The_bare_arm_has_no_github_host_and_no_arm_lists_raw_githubusercontent', () => {
  for (const host of GITHUB_HOSTS) assert.equal(EGRESS_HOSTS.bare.includes(host), false, host);
  for (const [arm, hosts] of Object.entries(EGRESS_HOSTS)) {
    assert.equal(hosts.includes('raw.githubusercontent.com'), false, arm);
  }
});

test('The_api_reaches_the_cell_through_the_broker_only_so_no_arm_lists_it', () => {
  assert.ok(KNOWN_REFUSED.includes('api.anthropic.com'));
  for (const [arm, hosts] of Object.entries(EGRESS_HOSTS)) {
    for (const refused of KNOWN_REFUSED) assert.equal(hosts.includes(refused), false, `${arm} lists ${refused}`);
  }
});

test('The_lists_are_exact_names_and_cannot_be_changed', () => {
  for (const hosts of [...Object.values(EGRESS_HOSTS), KNOWN_REFUSED]) {
    for (const host of hosts) assert.match(host, /^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$/, host);
    assert.ok(Object.isFrozen(hosts));
  }
  assert.ok(Object.isFrozen(EGRESS_HOSTS));
});
