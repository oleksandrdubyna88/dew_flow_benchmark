// "Network was used only by `prepare`" (plan DoD): no module of the pilot but `lib/prepare.mjs` runs npm, names a registry, imports a networking module or runs a git verb that talks to a remote. A scan,
// with its companion: the scan still finds all of that in `prepare.mjs`, so a pass is not an empty scan.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { SPIKE } from '../lib/paths.mjs';
import { stripComments } from '../../minigit-bench/test/scan.mjs';

const NETWORK = [
  [/registry\.npmjs|registry\.yarnpkg|api\.nuget\.org|api\.anthropic\.com/, 'a registry or API host'],
  [/\brunBounded\(\s*'npm'|spawn\w*\(\s*'npm'|'npm',\s*\[/, 'npm run as a process'],
  [/from\s+'node:(?:http|https|http2|net|dns|tls|dgram)'/, 'a networking module'],
  [/\bfetch\(|\bXMLHttpRequest\b|\bcurl\b|\bwget\b/, 'an HTTP client'],
  [/'(?:clone|fetch|pull|push|ls-remote)'/, 'a git verb that talks to a remote'],
];
const modules = () => [path.join(SPIKE, 'explore.mjs'), ...readdirSync(path.join(SPIKE, 'lib')).filter(f => f.endsWith('.mjs')).map(f => path.join(SPIKE, 'lib', f))];
const hits = file => NETWORK.filter(([re]) => re.test(stripComments(readFileSync(file, 'utf8')))).map(([, what]) => what);

test('No_module_of_the_pilot_but_prepare_reaches_the_network', () => {
  const offenders = modules().filter(f => path.basename(f) !== 'prepare.mjs').map(f => [path.relative(SPIKE, f), hits(f)]).filter(([, h]) => h.length > 0);
  assert.deepEqual(offenders, []);
});

test('The_scan_still_finds_what_it_looks_for_in_prepare_so_the_pass_above_is_not_an_empty_scan', () => {
  const found = hits(path.join(SPIKE, 'lib', 'prepare.mjs'));
  assert.ok(found.includes('npm run as a process'), found.join(', '));
  assert.ok(modules().length >= 15);
});
