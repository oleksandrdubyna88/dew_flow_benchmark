import assert from 'node:assert/strict';
import os from 'node:os';
import { test } from 'node:test';
import { hiddenPaths, isolation, scoreBoxArgv, tmpfs } from '../lib/bwrap.mjs';

const HOME = '/home/someone';

test('The_hide_list_is_the_home_and_the_four_host_prefixes_and_a_missing_host_dir_is_left_alone', () => {
  assert.deepEqual(hiddenPaths({ operatorHome: HOME, exists: () => true }), [HOME, '/mnt', '/run', '/tmp', '/var/tmp']);
  assert.deepEqual(hiddenPaths({ operatorHome: HOME, exists: () => false }), [HOME], 'the home is always hidden; the others only when the host has them');
  assert.deepEqual(hiddenPaths({ operatorHome: HOME, exists: p => p !== '/var/tmp' }), [HOME, '/mnt', '/run', '/tmp']);
  assert.equal(Object.isFrozen(hiddenPaths({ operatorHome: HOME, exists: () => true })), true);
});

test('The_scoring_box_hides_exactly_what_hiddenPaths_names', () => {
  const opts = { copyDir: '/w/copy', home: '/w/home', tmp: '/w/tmp', operatorHome: HOME, exists: () => true };
  const argv = scoreBoxArgv(opts);
  for (const p of hiddenPaths(opts)) {
    assert.ok(argv.some((a, i) => a === '--tmpfs' && argv[i + 1] === p), `${p} is hidden`);
  }
  assert.deepEqual(tmpfs('/mnt'), ['--tmpfs', '/mnt']);
  assert.deepEqual(isolation(), ['--unshare-net', '--unshare-pid', '--die-with-parent', '--new-session']);
  assert.equal(os.homedir().length > 0, true);
});
