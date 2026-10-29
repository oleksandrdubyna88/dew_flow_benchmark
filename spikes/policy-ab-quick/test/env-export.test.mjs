import assert from 'node:assert/strict';
import test from 'node:test';
import { DROP, FIXED_GIT, KEEP, buildChildEnv, scrubEnv } from '../lib/env.mjs';

const parent = { PATH: '/usr/bin:/bin', HOME: '/home/u', CLAUDECODE: '1', ANTHROPIC_MODEL: 'x', ANTHROPIC_API_KEY: 'k', CLAUDE_CONFIG_DIR: '/c',
  GH_TOKEN: 't', OTEL_X: '1', GIT_AUTHOR_NAME: 'someone', GIT_TERMINAL_PROMPT: '1' };

test('The_env_export_changes_no_child_env', () => {
  // The literal block buildChildEnv carried before the exports existed: the composed result must equal it.
  const before = {
    ...Object.fromEntries(Object.entries(parent).filter(([k]) => KEEP.has(k) || !DROP.some(re => re.test(k)))),
    PATH: '/usr/bin:/bin',
    CLAUDE_CODE_DISABLE_BACKGROUND_TASKS: '1',
    CLAUDE_CODE_ENTRYPOINT: 'claude-vscode',
    GIT_CONFIG_GLOBAL: 'E',
    GIT_CONFIG_NOSYSTEM: '1',
    GIT_TERMINAL_PROMPT: '0',
    GIT_AUTHOR_NAME: 'bench', GIT_AUTHOR_EMAIL: 'bench@invalid',
    GIT_COMMITTER_NAME: 'bench', GIT_COMMITTER_EMAIL: 'bench@invalid',
  };
  const env = buildChildEnv(parent, { emptyGitConfig: 'E', hasGh: () => false });
  assert.deepEqual(env, before);
  assert.deepEqual(Object.keys(env), Object.keys(before), 'same key order too');
  for (const [k, v] of Object.entries(FIXED_GIT)) assert.equal(env[k], v, k);
});

test('The_exports_are_the_pieces_the_new_spike_composes_and_cannot_be_changed', () => {
  const scrubbed = scrubEnv(parent);
  assert.deepEqual(Object.keys(scrubbed).sort(), ['ANTHROPIC_API_KEY', 'CLAUDE_CONFIG_DIR', 'GIT_AUTHOR_NAME', 'GIT_TERMINAL_PROMPT', 'HOME', 'PATH']);
  assert.ok(Object.isFrozen(FIXED_GIT) && Object.isFrozen(DROP));
  assert.equal('GIT_CONFIG_GLOBAL' in FIXED_GIT, false, 'GIT_CONFIG_GLOBAL stays a parameter');
  assert.equal(parent.CLAUDECODE, '1', 'the input is not mutated');
});
