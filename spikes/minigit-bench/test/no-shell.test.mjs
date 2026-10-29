// No spike launcher can reach a shell interpreter (code-scanning alert #8). A scan over every non-test module of both spikes.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { spikeDir, tmpDir } from './helpers.mjs';

const SPIKES = [spikeDir, path.resolve(spikeDir, '..', 'policy-ab-quick')];
const SKIPPED_DIRS = new Set(['test', 'node_modules', '.git']);
const Q = String.raw`['"\x60]`;
const SHELLS = String.raw`(?:sh|bash|dash|zsh|cmd|cmd\.exe)`;

// Each rule matches across lines (`s` flag is not enough for a bounded gap: the gap classes below allow newlines).
export const SHELL_RULES = Object.freeze([
  { rule: 'cmd-exe-literal', re: new RegExp(`${Q}cmd\\.exe${Q}`, 'g') },
  { rule: 'shell-true', re: /\bshell\s*:\s*true\b/g },
  { rule: 'exec-import', re: /import\s*\{[^}]*\bexec(?:Sync)?\b[^}]*\}\s*from\s*['"](?:node:)?child_process['"]/g },
  { rule: 'exec-member', re: /\bchild_process\s*\.\s*exec(?:Sync)?\b|\bcp\s*\.\s*exec(?:Sync)?\s*\(/g },
  { rule: 'shell-dash-c', re: new RegExp(`\\(\\s*${Q}${SHELLS}${Q}\\s*,\\s*\\[\\s*${Q}(?:-c|\\/c|\\/d)${Q}`, 'g') },
]);

const lineOf = (text, index) => text.slice(0, index).split('\n').length;

/** Every shell route in `text`, as `{ rule, line }`, in source order. */
export function shellRoutes(text) {
  return SHELL_RULES.flatMap(({ rule, re }) => [...text.matchAll(re)].map(m => ({ rule, line: lineOf(text, m.index) })))
    .sort((a, b) => a.line - b.line);
}

/** Every non-test `.mjs` under `root`, as absolute paths. */
const modulesUnder = root => readdirSync(root, { withFileTypes: true }).flatMap(e => {
  if (e.isDirectory()) return SKIPPED_DIRS.has(e.name) ? [] : modulesUnder(path.join(root, e.name));
  return e.isFile() && e.name.endsWith('.mjs') ? [path.join(root, e.name)] : [];
});

test('No_spike_launcher_can_reach_a_shell_interpreter', () => {
  const files = SPIKES.flatMap(modulesUnder);
  assert.ok(files.length >= 50, `the scan covers both spikes' modules (${files.length})`);
  const hits = files.flatMap(f => shellRoutes(readFileSync(f, 'utf8')).map(h => `${path.relative(path.resolve(spikeDir, '..', '..'), f)}:${h.line} ${h.rule}`));
  assert.deepEqual(hits, []);
});

test('The_shell_scan_still_finds_a_planted_route', () => {
  const file = path.join(tmpDir('no-shell'), 'planted.mjs');
  writeFileSync(file, "import { spawn } from 'node:child_process';\nspawn('cmd.exe', ['/c', 'x']);\nspawn('x', [], {\n  shell: true });\n");
  const hits = shellRoutes(readFileSync(file, 'utf8'));
  assert.deepEqual(hits.map(h => h.rule).sort(), ['cmd-exe-literal', 'shell-dash-c', 'shell-true']);
  assert.deepEqual(hits.map(h => h.line), [2, 2, 4]);
});

test('The_scan_recognises_each_route_shape_across_lines_and_ignores_safe_code', () => {
  const planted = {
    'exec-import': "import {\n  spawn,\n  execSync,\n} from 'node:child_process';",
    'exec-member': "child_process.exec('x')",
    'shell-dash-c': "spawn(\n  'bash',\n  [\n    '-c', 'x'])",
    'shell-true': 'spawn(a, b, { shell : true })',
    'cmd-exe-literal': 'const exe = "cmd.exe";',
  };
  for (const [rule, text] of Object.entries(planted)) assert.ok(shellRoutes(text).some(h => h.rule === rule), rule);
  const safe = "import { spawn, execFile } from 'node:child_process';\nspawn(exe, args, { shell: false });\nrun('sh', ['script.sh']);";
  assert.deepEqual(shellRoutes(safe), []);
});

test('The_three_spikes_kill_through_one_function', () => {
  const importsSharedKill = file => /import\s*\{[^}]*\bkillTree\b[^}]*\}\s*from\s*'[^']*policy-ab-quick\/lib\/proc\.mjs'/.test(readFileSync(file, 'utf8'));
  assert.equal(importsSharedKill(path.join(spikeDir, 'lib', 'run.mjs')), true, 'lib/run.mjs');
  assert.equal(importsSharedKill(path.join(spikeDir, 'capture', 'capture-run.mjs')), true, 'capture/capture-run.mjs');
  const files = SPIKES.flatMap(modulesUnder);
  const own = path.join(SPIKES[1], 'lib', 'proc.mjs');
  const signalling = files.filter(f => f !== own && /\bkill\(\s*-|\[\s*-[\w.]*pid\b/.test(readFileSync(f, 'utf8')));
  assert.deepEqual(signalling, [], 'a group signal written outside proc.mjs');
  assert.ok(/kill\(target, 'SIGKILL'\)/.test(readFileSync(own, 'utf8')), 'the scan looks at the module that owns the kill');
});
