import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { CAPTURE_PROMPT, capturePlan } from '../capture/capture-plan.mjs';
import { findOnPath, outProblems, readToken, tokenStatus } from '../capture/capture-env.mjs';
import { runCapture } from '../capture/capture-run.mjs';
import { sanitiseCapture } from '../capture/capture-files.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';
import { parseLines } from './synth.mjs';

const SESSION = path.join(spikeDir, 'capture', 'capture-session.mjs');
const TOKEN = 'sk-ant-oat01-SENTINELSENTINELSENTINEL0123456789';
const OUT = '/tmp/mgb-dry/cap-out';
const plan = (over = {}) => {
  const p = capturePlan({ out: OUT, spikeDir: '/repo/spikes/minigit-bench', port: 43111, token: TOKEN, env: { PATH: '/usr/bin:/bin', HOME: '/home/op', ANTHROPIC_API_KEY: 'must-not-pass', OTEL_SERVICE_NAME: 'leak', ...over.env }, ...over });
  assert.equal(p.ok, true, p.reason);
  return p.value;
};

test('The_dry_run_assembles_the_exact_argv_the_spec_prescribes', () => {
  const p = plan();
  assert.equal(p.exe, 'claude');
  assert.deepEqual(p.argv, [
    '-p', '--model', 'opus', '--output-format', 'stream-json', '--verbose', '--forward-subagent-text', '--include-hook-events',
    '--setting-sources', 'project', '--settings', `${OUT}/settings.json`, '--strict-mcp-config', '--mcp-config', `${OUT}/mcp.json`,
    '--max-budget-usd', '1.5', '--tools', 'Agent,Bash,Read,Glob',
  ]);
  assert.equal(p.argv.includes('--no-session-persistence'), false, 'session persistence stays ON: the transcripts are what is captured');
  assert.equal(p.cwd, `${OUT}/tree`);
  assert.equal(p.timeoutMs, 600000);
});

test('The_dry_run_assembles_the_exact_env_and_passes_nothing_else', () => {
  const p = plan();
  assert.deepEqual(p.env, {
    PATH: '/usr/bin:/bin', HOME: '/home/op', LANG: 'C.UTF-8', CLAUDE_CODE_OAUTH_TOKEN: TOKEN, CLAUDE_CONFIG_DIR: `${OUT}/config`,
    CLAUDE_CODE_DISABLE_BACKGROUND_TASKS: '1', DISABLE_AUTOUPDATER: '1', CLAUDE_CODE_ENABLE_TELEMETRY: '1', OTEL_LOGS_EXPORTER: 'otlp',
    OTEL_METRICS_EXPORTER: 'otlp', OTEL_EXPORTER_OTLP_PROTOCOL: 'http/json', OTEL_EXPORTER_OTLP_ENDPOINT: 'http://127.0.0.1:43111',
    OTEL_LOGS_EXPORT_INTERVAL: '1000', OTEL_METRIC_EXPORT_INTERVAL: '1000', OTEL_LOG_TOOL_DETAILS: '1', OTEL_LOG_USER_PROMPTS: '0',
  });
  assert.equal('ANTHROPIC_API_KEY' in p.env, false);
  assert.equal('OTEL_SERVICE_NAME' in p.env, false, 'the parent OTEL_* never passes');
  assert.deepEqual(p.envKeys, Object.keys(p.env).sort());
});

test('The_token_travels_in_the_env_only_never_on_argv_or_in_the_prompt_or_the_files', () => {
  const p = plan();
  const everything = JSON.stringify({ argv: p.argv, stdin: p.stdin, files: p.files });
  assert.equal(everything.includes('SENTINEL'), false);
  assert.equal(p.stdin, CAPTURE_PROMPT);
});

test('The_settings_file_is_the_one_the_spec_writes_and_its_hooks_call_the_stamp_hook', () => {
  const p = plan();
  const hook = { hooks: [{ type: 'command', command: `node /repo/spikes/minigit-bench/capture/stamp-hook.mjs ${OUT}/hooks.ndjson` }] };
  assert.deepEqual(JSON.parse(p.files['settings.json']), {
    effortLevel: 'high', modelSettings: { opus: { effortLevel: 'high' }, sonnet: { effortLevel: 'high' }, fable: { effortLevel: 'high' } },
    hooks: { PreToolUse: [hook], PostToolUse: [hook] },
    permissions: { allow: ['Bash(ls)', 'Bash(ls:*)', 'Bash(echo capture-ok)', 'Read', 'Glob', 'Agent'] },
  });
  assert.deepEqual(JSON.parse(p.files['mcp.json']), { mcpServers: {} });
  assert.deepEqual(Object.keys(p.files).sort(), ['mcp.json', 'settings.json', 'tree/a.txt', 'tree/b.txt']);
});

test('The_allow_list_covers_what_the_prompt_runs_and_holds_no_broad_entry', () => {
  const allow = JSON.parse(plan().files['settings.json']).permissions.allow;
  assert.ok(!allow.includes('Bash'), 'no bare Bash');
  assert.ok(allow.every(a => !/^Bash\(\*\)$|^Bash\(\*:\*\)$/.test(a)), 'no Bash(*)');
  assert.ok(allow.filter(a => a.startsWith('Bash(')).every(a => /^Bash\((ls|ls:\*|echo capture-ok)\)$/.test(a)));
  assert.ok(CAPTURE_PROMPT.includes('`ls`') && CAPTURE_PROMPT.includes('echo capture-ok'), 'the entries match the prompt');
});

test('A_path_with_a_space_is_quoted_in_the_hook_command', () => {
  const p = plan({ out: '/tmp/a b/out' });
  assert.match(JSON.parse(p.files['settings.json']).hooks.PreToolUse[0].hooks[0].command, /^node \S+stamp-hook\.mjs '\/tmp\/a b\/out\/hooks\.ndjson'$/);
});

test('The_prompt_asks_for_two_children_on_two_models_in_one_message_and_one_main_tool_call', () => {
  for (const part of ['ONE message', 'model: "sonnet"', 'model: "fable"', 'general-purpose', '`ls`', 'echo capture-ok', 'DONE']) assert.ok(CAPTURE_PROMPT.includes(part), part);
});

test('A_plan_without_a_path_or_a_port_is_refused_as_a_value', () => {
  assert.equal(capturePlan({ out: OUT, spikeDir: '/s', port: 1, token: TOKEN, env: {} }).ok, false);
  assert.equal(capturePlan({ out: 'relative/out', spikeDir: '/s', port: 1, token: TOKEN, env: { PATH: '/bin', HOME: '/h' } }).ok, false);
  assert.equal(capturePlan({ out: OUT, spikeDir: '/s', port: 0, token: TOKEN, env: { PATH: '/bin', HOME: '/h' } }).ok, false);
});

// ---- the environment checks ---------------------------------------------------------------------------------------------------

const homeWithToken = (mode, text = `${TOKEN}\n`) => {
  const home = tmpDir('home');
  const dir = path.join(home, '.config', 'minigit-bench');
  mkdirSync(dir, { recursive: true });
  const file = path.join(dir, 'claude-oauth-token');
  writeFileSync(file, text);
  chmodSync(file, mode);
  return home;
};

test('The_token_comes_from_the_env_first_then_from_a_mode_600_file', () => {
  assert.deepEqual(readToken({ env: { CLAUDE_CODE_OAUTH_TOKEN: ' from-env ' }, home: homeWithToken(0o600) }), { ok: true, value: 'from-env', source: 'env' });
  const fromFile = readToken({ env: {}, home: homeWithToken(0o600) });
  assert.deepEqual([fromFile.ok, fromFile.value, fromFile.source], [true, TOKEN, 'file']);
});

test('The_token_status_is_known_without_reading_the_token', () => {
  assert.deepEqual(tokenStatus({ env: { CLAUDE_CODE_OAUTH_TOKEN: 'x' }, home: tmpDir('h') }), { present: true, source: 'env' });
  assert.deepEqual(tokenStatus({ env: {}, home: homeWithToken(0o600) }), { present: true, source: 'file' });
  const wide = tokenStatus({ env: {}, home: homeWithToken(0o644) });
  assert.equal(wide.present, false);
  assert.match(wide.why, /mode/);
  assert.equal(tokenStatus({ env: {}, home: tmpDir('h2') }).present, false);
  assert.equal(JSON.stringify([wide, tokenStatus({ env: {}, home: homeWithToken(0o600) })]).includes('SENTINEL'), false);
});

test('A_token_file_that_others_can_read_or_that_is_empty_or_absent_is_refused_without_echoing_it', () => {
  for (const [home, why] of [[homeWithToken(0o644), /mode/], [homeWithToken(0o600, '  \n'), /empty/], [tmpDir('home-none'), /no token/]]) {
    const r = readToken({ env: {}, home });
    assert.equal(r.ok, false);
    assert.match(r.reason, why);
    assert.equal(r.reason.includes('SENTINEL'), false);
  }
});

test('The_out_dir_must_be_absolute_outside_every_git_tree_outside_the_heldout_sources_and_new', () => {
  const home = tmpDir('home-out');
  assert.deepEqual(outProblems(path.join(os.tmpdir(), 'mgb-out-fresh'), { home }), []);
  assert.match(outProblems(path.join(spikeDir, 'out'), { home })[0], /git/);
  assert.match(outProblems('relative', { home })[0], /absolute/);
  assert.match(outProblems(path.join(home, 'rsd', 'minigit', 'heldout-src', 'cap'), { home }).join(), /heldout-src/);
  const used = tmpDir('used');
  writeFileSync(path.join(used, 'x'), '');
  assert.match(outProblems(used, { home })[0], /not empty/);
});

test('A_binary_is_found_on_PATH_only_when_it_is_executable', () => {
  const bin = tmpDir('bin');
  writeFileSync(path.join(bin, 'claude'), '#!/bin/sh\n');
  assert.equal(findOnPath('claude', bin), null, 'not executable yet');
  chmodSync(path.join(bin, 'claude'), 0o755);
  assert.equal(findOnPath('claude', `/nonexistent:${bin}`), path.join(bin, 'claude'));
  assert.equal(findOnPath('claude', ''), null);
});

// ---- the verbs through the CLI ------------------------------------------------------------------------------------------------

const cli = (args, env = {}) => spawnSync(process.execPath, [SESSION, ...args], { encoding: 'utf8', timeout: 30000, env: { PATH: '/nonexistent', HOME: tmpDir('cli-home'), ...env } });

test('The_dry_run_verb_prints_the_plan_with_the_token_redacted_and_launches_nothing', () => {
  const out = path.join(tmpDir('dry'), 'out');
  const r = cli(['run', '--dry-run', '--out', out], { CLAUDE_CODE_OAUTH_TOKEN: TOKEN });
  assert.equal(r.status, 0, r.stderr);
  assert.equal((r.stdout + r.stderr).includes('SENTINEL'), false);
  const printed = JSON.parse(r.stdout);
  assert.equal(printed.env.CLAUDE_CODE_OAUTH_TOKEN, '<set, not shown>');
  assert.equal(printed.argv[0], '-p');
  assert.equal(printed.claudeFound, false, 'PATH had no claude: the dry run says so and still exits 0');
  assert.equal(existsSync(out), false, 'a dry run creates nothing');
});

test('The_dry_run_verb_works_without_a_token_and_says_so', () => {
  const r = cli(['run', '--dry-run', '--out', path.join(tmpDir('dry2'), 'o')]);
  assert.equal(r.status, 0, r.stderr);
  const printed = JSON.parse(r.stdout);
  assert.equal(printed.tokenSource, 'none');
  assert.equal(printed.env.CLAUDE_CODE_OAUTH_TOKEN, '<absent>');
});

test('A_real_run_without_a_token_exits_3_and_names_where_to_put_one', () => {
  const r = cli(['run', '--out', path.join(tmpDir('notok'), 'o')]);
  assert.equal(r.status, 3);
  assert.match(r.stderr, /claude-oauth-token/);
});

test('A_run_with_the_out_dir_inside_a_git_tree_exits_3_and_usage_errors_exit_4', () => {
  assert.equal(cli(['run', '--out', path.join(spikeDir, 'cap')], { CLAUDE_CODE_OAUTH_TOKEN: TOKEN }).status, 3);
  assert.equal(cli(['run']).status, 4);
  assert.equal(cli(['frobnicate']).status, 4);
  assert.equal(cli(['sanitise', '--from', '/x']).status, 4);
  assert.equal(cli(['verdicts']).status, 4);
});

// ---- run end to end against a STUB executable (plumbing only: the stub prints placeholder data into a temp dir, never into a fixture) ----

const STUB = `#!/usr/bin/env node
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
const mode = '__MODE__';
let prompt = '';
for await (const c of process.stdin) prompt += c;
const emit = o => process.stdout.write(JSON.stringify(o) + '\\n');
const tools = mode === 'noagent' ? ['Bash'] : ['Agent', 'Bash', 'Read', 'Glob'];
emit({ type: 'system', subtype: 'init', session_id: 'stub-session', claude_code_version: '0.0.0-stub', model: 'stub-model', tools, cwd: process.cwd() });
if (mode === 'sleep' || mode === 'noagent') await new Promise(r => setTimeout(r, 60000));
const proj = path.join(process.env.CLAUDE_CONFIG_DIR, 'projects', '-stub-tree');
mkdirSync(path.join(proj, 'stub-session', 'subagents'), { recursive: true });
writeFileSync(path.join(proj, 'stub-session.jsonl'), JSON.stringify({ type: 'assistant', STUB: true, cwd: process.cwd() }) + '\\n');
writeFileSync(path.join(proj, 'stub-session', 'subagents', 'agent-stub.jsonl'), JSON.stringify({ type: 'assistant', STUB: true }) + '\\n');
const attr = (key, stringValue) => ({ key, value: { stringValue } });
const body = JSON.stringify({ resourceLogs: [{ scopeLogs: [{ logRecords: [{ attributes: [attr('user.email', 'stub-person@example.org'), attr('user.id', 'STUBUSERID123'), attr('session.id', 'stub-session')] }] }] }] });
await fetch(process.env.OTEL_EXPORTER_OTLP_ENDPOINT + '/v1/logs', { method: 'POST', headers: { 'content-type': 'application/json' }, body });
if (mode === 'leak') emit({ type: 'assistant', leaked: process.env.CLAUDE_CODE_OAUTH_TOKEN });
emit({ type: 'result', subtype: 'success', total_cost_usd: 0.0123, session_id: 'stub-session', prompt_chars: prompt.length });
`;

/** The environment is an allow-list, so the stub's mode is baked into its text. */
function stubExe(mode = 'ok') {
  const dir = tmpDir('stub');
  const file = path.join(dir, 'claude');
  writeFileSync(file, STUB.replace('__MODE__', mode));
  chmodSync(file, 0o755);
  return file;
}

const walkRel = (dir, base = dir) => readdirSync(dir).flatMap(n => {
  const f = path.join(dir, n);
  return statSync(f).isDirectory() ? walkRel(f, base) : [path.relative(base, f)];
}).sort();

async function stubRun(mode = 'ok', extra = {}) {
  const out = path.join(tmpDir('run'), 'out');
  const r = await runCapture({ out, spikeDir, exe: stubExe(mode), token: TOKEN, env: { PATH: process.env.PATH, HOME: os.homedir() }, settleMs: 50, ...extra });
  return { out, r };
}

test('A_run_against_a_stub_writes_every_artefact_the_spec_lists_and_copies_the_transcripts_by_walking', async () => {
  const { out, r } = await stubRun('ok');
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.costUsd, 0.0123);
  const files = walkRel(out);
  for (const f of ['stream.ndjson', 'stderr.txt', 'otlp-raw.ndjson', 'argv.json', 'meta.json', 'capture.log', 'settings.json', 'mcp.json', 'tree/a.txt', 'tree/b.txt',
    'transcripts/-stub-tree/stub-session.jsonl', 'transcripts/-stub-tree/stub-session/subagents/agent-stub.jsonl']) assert.ok(files.includes(f), `${f} missing from ${files.join(', ')}`);
  const meta = JSON.parse(readFileSync(path.join(out, 'meta.json'), 'utf8'));
  assert.deepEqual([meta.cliVersion, meta.sessionId, meta.exitCode], ['0.0.0-stub', 'stub-session', 0]);
  assert.ok(Number.isInteger(meta.wallMs) && /^\d{4}-\d{2}-\d{2}/.test(meta.date));
  assert.equal(parseLines(readFileSync(path.join(out, 'otlp-raw.ndjson'), 'utf8')).length, 1, 'the stub posted through the real sink');
  assert.equal(parseLines(readFileSync(path.join(out, 'stream.ndjson'), 'utf8')).at(-1).type, 'result');
});

test('The_recorded_argv_and_env_hold_keys_never_values', async () => {
  const { out } = await stubRun('ok');
  const argvJson = JSON.parse(readFileSync(path.join(out, 'argv.json'), 'utf8'));
  assert.equal(argvJson.argv[0], '-p');
  assert.ok(argvJson.envKeys.includes('CLAUDE_CODE_OAUTH_TOKEN'));
  const all = walkRel(out).map(f => readFileSync(path.join(out, f), 'utf8')).join('\n');
  assert.equal(all.includes('SENTINEL'), false, 'the token value is in no file of the capture');
});

test('A_stream_whose_init_lacks_the_agent_tool_stops_the_capture_and_says_so', async () => {
  const { r } = await stubRun('noagent');
  assert.equal(r.ok, false);
  assert.equal(r.exit, 5);
  assert.match(r.reason, /init.*tools.*Agent/);
});

test('A_capture_past_its_wall_limit_is_killed_with_its_process_tree_and_fails_named', async () => {
  const started = Date.now();
  const { r } = await stubRun('sleep', { timeoutMs: 700 });
  assert.equal(r.ok, false);
  assert.match(r.reason, /time limit/);
  assert.ok(Date.now() - started < 20000);
});

test('A_missing_executable_is_a_failure_value_not_a_throw', async () => {
  const { r } = await stubRun('ok', { exe: '/nonexistent/claude' });
  assert.equal(r.ok, false);
  assert.match(r.reason, /claude/);
});

// ---- sanitise --------------------------------------------------------------------------------------------------------------------

test('Sanitise_copies_a_stub_capture_clean_and_a_second_run_into_the_same_target_is_refused', async () => {
  const { out } = await stubRun('ok');
  const to = path.join(tmpDir('fixture'), 'claude-stub-capture');
  const r = sanitiseCapture({ from: out, to, token: TOKEN });
  assert.equal(r.ok, true, JSON.stringify(r));
  const files = walkRel(to);
  assert.deepEqual(files.filter(f => !f.startsWith('transcripts/')).sort(), ['argv.json', 'meta.json', 'otlp-raw.ndjson', 'stderr.txt', 'stream.ndjson']);
  const text = files.map(f => readFileSync(path.join(to, f), 'utf8')).join('\n');
  for (const secret of [os.homedir(), os.userInfo().username === 'user' ? 'no-user-check' : os.userInfo().username, out, 'stub-person@example.org', 'STUBUSERID123']) assert.equal(text.includes(secret), false, secret);
  assert.ok(text.includes('stub-session'), 'session.id is kept: it is the join key');
  assert.ok(text.includes('user@example.invalid') && text.includes('redacted'));
  assert.ok(text.includes('/cap/tree'));
  assert.ok(files.every(f => !f.includes(path.basename(path.dirname(out)))), 'no raw out component in a path');
  assert.equal(sanitiseCapture({ from: out, to, token: TOKEN }).ok, false);
});

test('Sanitise_fails_closed_when_the_token_value_is_in_the_capture_and_leaves_nothing_behind', async () => {
  const { out } = await stubRun('leak');
  const parent = tmpDir('fixture-leak');
  const to = path.join(parent, 'claude-stub-capture');
  const r = sanitiseCapture({ from: out, to, token: TOKEN });
  assert.equal(r.ok, false);
  assert.ok(r.findings.some(f => f.pattern === 'the token value' || f.pattern.startsWith('credential shape')));
  assert.equal(JSON.stringify(r).includes('SENTINEL'), false, 'the failure does not repeat the token');
  assert.deepEqual(readdirSync(parent), [], 'no fixture directory and no staging directory is left');
});
