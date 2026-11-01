// The session plan (plan D-X2, D-X5, D-X7, T-X2): v2 section 4.5's bare prompt as written, no allowed host, the pilot's caps, the locale, the extra environment, the pinned CLI; `over` takes only v1's seams.
import { realHome } from '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { PLAN_OVER_KEYS } from '../../minigit-bench/lib/bare-plan.mjs';
import { BARE_TOOLS, claudeArgv, settingsFor } from '../../minigit-bench/lib/launch.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { hostLacks } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore, pinnedPaths } from '../lib/inputs.mjs';
import { INPUTS, REPO } from '../lib/paths.mjs';
import { PROMPT_TEMPLATE, STACK_NAMES, buildPrompts, loadPrompts, planForSession, promptText } from '../lib/plan.mjs';
import { stackSentence } from '../lib/workspace.mjs';

const EXPLORE = loadExplore().value;
const sha256 = text => createHash('sha256').update(text).digest('hex');
const layout = layoutPaths('/r', 'abc123');

/** The run context a plan is made from (the same fields `explore.mjs` puts in the loop's context). */
const ctx = over => ({
  claudeBinary: '/x/claude', cliVersion: EXPLORE.cli.version, nodeDir: '/n', tsDir: '/ts', token: 'fake-token', prices: {}, home: '/h', harnessSources: { boxInit: 'a', audit: 'b' },
  caps: EXPLORE.caps, envExtra: EXPLORE.envExtra, locale: EXPLORE.locale, prompts: loadPrompts(), ...over,
});
const item = stack => ({ session: { stack, phase: 'v1', attempt: 1 }, rid: 'abc123', layout });

test('The_prompt_template_is_v2_section_4_5s_bare_prompt_as_written', () => {
  const plan = readFileSync(path.join(REPO, 'todo', 'PLAN_lang_comparison_v2.md'), 'utf8');
  const block = /\*\*Bare:\*\*\n\n```\n([\s\S]*?)\n```/.exec(plan);
  assert.ok(block, 'the Bare block is found in the v2 plan');
  assert.equal(PROMPT_TEMPLATE, block[1]);
});

test('The_registered_prompts_are_what_the_code_computes_and_differ_only_in_the_stack_name_and_sentence', () => {
  const registered = loadPrompts();
  assert.deepEqual(registered, buildPrompts(EXPLORE), 'inputs/prompts.json is stale: regenerate it from the template and the pins');
  for (const [stack, p] of Object.entries(registered.prompts)) {
    assert.equal(p.sha256, sha256(p.text), stack);
    assert.equal(p.text, promptText({ stack, sentence: stackSentence(EXPLORE, stack) }));
  }
  const norm = stack => registered.prompts[stack].text.replace(STACK_NAMES[stack], '<STACK>').replace(registered.prompts[stack].stackSentence, '<SENTENCE>');
  assert.equal(norm('csharp'), norm('node'));
  assert.ok(!registered.prompts.csharp.text.includes('{'), 'no slot is left unfilled');
  assert.equal(registered.templateSha256, sha256(PROMPT_TEMPLATE));
});

test('The_plan_has_no_allowed_host_the_pilots_caps_the_registered_prompt_the_locale_and_the_extra_environment', () => {
  for (const stack of ['csharp', 'node']) {
    const plan = planForSession({ ctx: ctx(), item: item(stack) });
    assert.deepEqual(plan.allow, [], 'sessions run with no host allowed (v2 D17)');
    assert.deepEqual([plan.capUsd, plan.wallMs, plan.stallMs], [50, 75 * 60_000, 45 * 60_000]);
    assert.equal(plan.prompt, loadPrompts().prompts[stack].text);
    assert.equal(plan.locale, 'de_DE.UTF-8');
    assert.deepEqual(plan.envExtra, { DOTNET_gcServer: '0', NODE_OPTIONS: '' });
    assert.equal(plan.arm, 'bare');
    assert.equal(plan.cellId, 'cell-abc123');
    assert.deepEqual(plan.toolchains, ['/ts']);
    assert.ok(!plan.prompt.includes('minigit') && !plan.prompt.includes('SPEC-v1'), 'none of v1\'s task text');
    assert.match(plan.dummy, /^sk-ant-oat01-dummy-/);
  }
  assert.notEqual(planForSession({ ctx: ctx(), item: item('node') }).dummy, planForSession({ ctx: ctx(), item: item('node') }).dummy, 'a fresh dummy credential per session');
});

test('The_agent_is_opus_at_effort_high_with_v1s_tool_list_and_the_cap_of_the_plan', () => {
  const plan = planForSession({ ctx: ctx(), item: item('csharp') });
  const argv = claudeArgv({ layout, cap: plan.capUsd, tools: plan.tools });
  assert.deepEqual(argv.slice(argv.indexOf('--model'), argv.indexOf('--model') + 2), ['--model', 'opus']);
  assert.equal(argv[argv.indexOf('--max-budget-usd') + 1], '50');
  assert.equal(argv[argv.indexOf('--tools') + 1], BARE_TOOLS.join(','));
  assert.equal(settingsFor({ arm: 'bare', layout }).effortLevel, 'high');
});

test('The_pinned_CLI_copy_is_the_registered_binary', t => {
  const file = pinnedPaths(EXPLORE, realHome).claudeBinary; // the copy is a public binary under the real home; the test only hashes it
  if (!existsSync(file)) { hostLacks(t, `${file} is missing`); return; }
  assert.equal(createHash('sha256').update(readFileSync(file)).digest('hex'), EXPLORE.cli.sha256);
});

test('Over_takes_only_the_seams_of_v1_and_refuses_any_other_key_without_naming_its_value', () => {
  for (const key of PLAN_OVER_KEYS.filter(k => k !== 'upstream')) assert.equal(planForSession({ ctx: ctx({ planOver: { [key]: 5 } }), item: item('node') })[key], 5, key);
  for (const key of ['token', 'allow', 'prompt', 'capUsd', 'locale']) {
    assert.throws(() => planForSession({ ctx: ctx({ planOver: { [key]: 'SECRET-VALUE' } }), item: item('node') }), e => e.message.includes(`'${key}'`) && !e.message.includes('SECRET-VALUE'), key);
  }
  assert.throws(() => planForSession({ ctx: ctx({ planOver: { upstream: { host: 'evil.example', port: 1 } } }), item: item('node') }), /loopback/);
  assert.ok(existsSync(path.join(INPUTS, 'prompts.json')));
});
