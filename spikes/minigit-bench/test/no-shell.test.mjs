// No spike launcher can reach a shell interpreter (code-scanning alert #8). A scan over every non-test module of both spikes.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { spikeDir, tmpDir } from './helpers.mjs';
import { ALLOWED_CHILD_PROCESS, shellRoutes } from './shell-routes.mjs';

const SPIKES = [spikeDir, path.resolve(spikeDir, '..', 'policy-ab-quick'), path.resolve(spikeDir, '..', 'lang-explore')];
const SKIPPED_DIRS = new Set(['test', 'node_modules', '.git']);
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

test('The_scan_covers_the_exploratory_pilots_spike_as_well', () => {
  const pilotModule = path.resolve(spikeDir, '..', 'lang-explore', 'lib', 'paths.mjs');
  assert.ok(existsSync(pilotModule), 'the pilot module the scan must find exists');
  assert.ok(SPIKES.flatMap(modulesUnder).includes(pilotModule), 'a module of spikes/lang-explore is among the scanned files, so a shell route written there fails the scan above');
});

test('The_shell_scan_still_finds_a_planted_route', () => {
  const file = path.join(tmpDir('no-shell'), 'planted.mjs');
  writeFileSync(file, "import { spawn } from 'node:child_process';\nspawn('cmd.exe', ['/c', 'x']);\nspawn('x', [], {\n  shell: true });\n");
  const hits = shellRoutes(readFileSync(file, 'utf8'));
  assert.deepEqual(hits.map(h => h.rule).sort(), ['cmd-exe-literal', 'shell-key', 'shell-program-flag']);
  assert.deepEqual(hits.map(h => h.line), [2, 2, 4]);
});

// Every shape the reviewer found the first scan blind to (E2.S1 review), plus the routes the first scan did catch.
const PLANTED = Object.freeze({
  'namespace import': ["import * as cpx from 'node:child_process';\ncpx.execSync('rm -rf x');", ['child-process-whole-module']],
  'default import': ["import childProc from 'child_process';\nchildProc.exec(cmd);", ['child-process-whole-module']],
  'default and named': ["import cp, { execSync } from 'node:child_process';", ['child-process-whole-module', 'child-process-name']],
  'require destructure': ["const { execSync } = require('child_process');", ['child-process-name']],
  'require whole': ["const cp = require('child_process');", ['child-process-whole-module']],
  'dynamic import destructure': ["const { exec } = await import('node:child_process');", ['child-process-name']],
  'dynamic import whole': ["const cp = await import('node:child_process');", ['child-process-whole-module']],
  'named as': ["import { exec as run } from 'node:child_process';", ['child-process-name']],
  're-export': ["export { execSync } from 'node:child_process';", ['child-process-name']],
  'export star': ["export * from 'node:child_process';", ['child-process-whole-module']],
  'bracket member': ["import * as child_process from 'node:child_process';\nchild_process['exec'](cmd);", ['child-process-whole-module']],
  'multi-line import': ["import {\n  spawn,\n  execSync,\n} from 'node:child_process';", ['child-process-name']],
  'bin sh': ["spawn('/bin/sh', ['-c', cmd]);", ['shell-program-flag']],
  'bash -lc': ["spawn('bash', ['-lc', cmd]);", ['shell-program-flag']],
  'ksh': ["spawn('ksh', ['-c', cmd]);", ['shell-program-flag']],
  'pwsh -Command': ["spawn('pwsh', ['-Command', cmd]);", ['shell-program-flag']],
  'powershell.exe -c': ["spawn('powershell.exe', ['-c', cmd]);", ['shell-program-flag']],
  'windows path cmd': ["spawn('C:\\\\Windows\\\\System32\\\\cmd.exe', ['/c', cmd]);", ['shell-program-flag']],
  'cmd.exe literal': ["const exe = \"cmd.exe\";", ['cmd-exe-literal']],
  'shell constant': ["const SH = 'sh';\nspawn(SH, ['-c', cmd]);", ['shell-program-flag']],
  'execFileSync sh': ["execFileSync('sh', ['-c', `git ${x}`]);", ['shell-program-flag']],
  'spawnSync bash -c': ["spawnSync('bash', ['-c', 'x'])", ['shell-program-flag']],
  'shell true': ['spawn(a, b, { shell : true })', ['shell-key']],
  'shell conditional': ["spawn(x, a, { shell: process.platform === 'win32' });", ['shell-key']],
  'shell string': ["spawn(x, a, { shell: '/bin/bash' });", ['shell-key']],
  'shell quoted key': ["spawn(x, a, { 'shell': true });", ['shell-key']],
  'shell shorthand': ['const shell = true;\nspawn(x, a, { cwd, shell });', ['shell-key']],
  'shell on its own line': ['spawn(x, a, {\n  shell:\n    true });', ['shell-key']],
  execa: ["import { execa } from 'execa';", ['shell-package']],
  shelljs: ["const sh = require('shelljs');", ['shell-package']],
});
const SAFE = Object.freeze({
  'regex exec': 'const r = /x/.exec(s); regex.exec(t);',
  'argv launchers': "import { spawn, spawnSync, execFile, execFileSync } from 'node:child_process';",
  'shell false': 'spawn(a, b, { shell: false, cwd });',
  'a table keyed shell': "const OPTIONS = { shell: ['-o', '+o'], git: ['-c'] };",
  'a script run by a shell program': "spawn('bash', ['script.sh', '-x']);",
  'git -c': "spawn('git', ['-c', 'core.autocrlf=false', 'add']);",
  'node -c': 'spawn(process.execPath, ["-c", file]);',
  'a comment': "// spawn('bash', ['-c', x]); import * as cp from 'node:child_process'; shell: true\n/* cmd.exe */",
});

test('The_scan_recognises_every_planted_route_shape', () => {
  for (const [name, [text, rules]] of Object.entries(PLANTED)) assert.deepEqual(shellRoutes(text).map(h => h.rule), rules, name);
});

test('The_scan_leaves_safe_code_and_prose_alone', () => {
  for (const [name, text] of Object.entries(SAFE)) assert.deepEqual(shellRoutes(text), [], name);
  assert.deepEqual([...ALLOWED_CHILD_PROCESS], ['spawn', 'spawnSync', 'execFile', 'execFileSync']);
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
