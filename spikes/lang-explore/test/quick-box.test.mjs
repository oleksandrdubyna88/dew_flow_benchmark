// The sandbox is not weakened by the quick run (plan item 6): the agent's box for a quick task still hides the operator's home, /mnt, /run, /tmp-of-the-host and /var/tmp as empty tmpfs, binds none of them back, has its own network
// namespace, and the only thing the quick run adds is the pinned .NET SDK read-only and the distribution's dotnet hidden. The argv is built, never run.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { agentBoxPrefix, layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { DISTRIBUTION_DOTNET } from '../lib/quick.mjs';
import { loadPrompts, planForSession } from '../lib/plan.mjs';
import { EXPLORE } from './quick-leak-fixture.mjs';

const HOME = '/home/nobody';
const layout = layoutPaths('/series/s', 'abc123');
const ctx = over => ({
  claudeBinary: '/x/claude', cliVersion: EXPLORE.cli.version, nodeDir: '/n', tsDir: '/ts', token: 'fake-token', prices: {}, home: HOME, harnessSources: { boxInit: 'a', audit: 'b' },
  caps: EXPLORE.caps, envExtra: EXPLORE.envExtra, locale: EXPLORE.locale, prompts: loadPrompts(), ...over,
});
const quickCtx = ctx({ dotnetDir: '/dn', hide: DISTRIBUTION_DOTNET });
const plan = stack => planForSession({ ctx: quickCtx, item: { session: { stack, phase: 'v1', attempt: 1 }, rid: 'abc123', layout } });
const prefixOf = p => agentBoxPrefix({ layout, arm: 'bare', toolchains: p.toolchains, claudeBinary: p.claudeBinary, nodeDir: p.nodeDir, hide: p.hide, operatorHome: HOME, exists: () => true });

/** The destinations (the argument after the flag) of every `flag` in the argv. */
const destinations = (argv, ...flags) => argv.flatMap((a, i) => (flags.includes(a) ? [argv[i + 2] ?? argv[i + 1]] : []));
const tmpfsAt = argv => argv.flatMap((a, i) => (a === '--tmpfs' ? [argv[i + 1]] : []));

for (const stack of ['csharp', 'node']) {
  test(`The_box_of_a_quick_${stack}_session_hides_home_mnt_run_and_var_tmp_and_binds_none_of_them_back`, () => {
    const argv = prefixOf(plan(stack));
    for (const hidden of [HOME, '/mnt', '/run', '/var/tmp']) {
      assert.ok(tmpfsAt(argv).includes(hidden), `${hidden} is an empty tmpfs`);
      const bound = destinations(argv, '--bind', '--ro-bind', '--bind-try', '--ro-bind-try', '--dev-bind');
      assert.deepEqual(bound.filter(d => d === hidden || d.startsWith(`${hidden}/`)), [], `nothing is bound at or under ${hidden}`);
    }
  });

  test(`The_box_of_a_quick_${stack}_session_has_its_own_network_namespace_so_the_broker_is_the_only_way_out`, () => {
    const argv = prefixOf(plan(stack));
    assert.ok(argv.includes('--unshare-net') || argv.includes('--unshare-all'), 'a namespace of its own');
    assert.ok(argv.includes('--unshare-ipc'));
  });

  test(`The_quick_run_adds_only_the_pinned_sdk_read_only_and_hides_the_distributions_dotnet_for_${stack}`, () => {
    const argv = prefixOf(plan(stack));
    const readOnly = destinations(argv, '--ro-bind');
    assert.ok(readOnly.includes('/dn'), 'the pinned SDK is bound read-only');
    for (const dir of DISTRIBUTION_DOTNET) assert.ok(tmpfsAt(argv).includes(dir), `${dir} is hidden`);
    const plain = agentBoxPrefix({ layout, arm: 'bare', toolchains: ['/ts'], claudeBinary: '/x/claude', nodeDir: '/n', operatorHome: HOME, exists: () => true });
    const extra = argv.filter(a => !plain.includes(a));
    assert.deepEqual(extra.filter(a => a.startsWith('/') && !a.startsWith('/dn') && !DISTRIBUTION_DOTNET.includes(a)), [], 'no other path appears in the quick box that the olympiad box lacks');
  });
}

test('A_non_quick_plan_has_no_extra_toolchain_and_hides_nothing_more', () => {
  const p = planForSession({ ctx: ctx({}), item: { session: { stack: 'node', phase: 'v1', attempt: 1 }, rid: 'abc123', layout } });
  assert.equal(p.dotnetDir, undefined);
  assert.equal(p.hide, undefined);
});
