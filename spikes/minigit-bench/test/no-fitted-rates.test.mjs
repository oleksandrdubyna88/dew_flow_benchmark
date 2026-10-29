import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { mkdirSync, writeFileSync } from 'node:fs';
import { findCalls, findNames, importViolations, importedNames, localGraph, sourceFiles, stripComments } from './scan.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

const FORBIDDEN = ['costFromMessages', 'CostMeter', 'summarize', 'parallelism', 'childCosts', 'OUTPUT_FLOOR_PER_MESSAGE'];
// the spec's list, plus initInfo (pure, aliases.mjs), assertRemovable (safe-rm's guard, beside removeUnder) and PINNED_EFFORT (a pure constant:
// the effort block launch.mjs writes into the session's settings, one source with the older spike)
const ALLOWED_FROM_POLICY_AB = ['resultEnvelope', 'initInfo', 'agentTimeline', 'sweepSpans', 'drawOrder', 'rateLimitSignal', 'spendDecision', 'removeUnder', 'assertRemovable', 'killTree', 'canaryTargets', 'findOutsideAccesses', 'FIXED_GIT', 'PINNED_EFFORT'];
const rel = f => path.relative(spikeDir, f).split(path.sep).join('/');
const production = () => sourceFiles(spikeDir, ['test', 'node_modules']);
const policyLib = file => path.resolve(spikeDir, '..', 'policy-ab-quick', 'lib', file);

test('The_spike_never_calls_a_fitted_rate_path', () => {
  const files = production();
  assert.ok(files.length > 10, `the scan sees the spike (${files.length} files)`);
  const hits = files.flatMap(f => findNames(readFileSync(f, 'utf8'), FORBIDDEN, { ownDeclarations: true }).map(h => `${rel(f)}:${h.line} ${h.name}`));
  assert.deepEqual(hits, []);
  const pricingImports = files.filter(f => /(?:from\s*|import\s*\(\s*)['"][^'"]*pricing\.mjs['"]/.test(stripComments(readFileSync(f, 'utf8'))));
  assert.deepEqual(pricingImports.map(rel), []);
});

test('The_fitted_rate_scan_still_finds_its_known_callers', () => {
  const where = (file, name) => findCalls(readFileSync(policyLib(file), 'utf8'), name);
  assert.deepEqual(where('summary.mjs', 'costFromMessages'), [142]);
  assert.deepEqual(where('parallel.mjs', 'costFromMessages'), [90]);
  assert.deepEqual(where('watchdog.mjs', 'costFromMessages'), [27]);
  const names = findNames(readFileSync(policyLib('watchdog.mjs'), 'utf8'), FORBIDDEN).map(h => h.name);
  assert.ok(names.includes('CostMeter') && names.includes('OUTPUT_FLOOR_PER_MESSAGE'), 'the name scan bites too');
});

test('The_scan_ignores_prose_and_finds_code', () => {
  assert.deepEqual(findNames('function summarize(a) { return summarize(a); }', FORBIDDEN, { ownDeclarations: true }), [], 'an own function of the same name is not the fitted path');
  assert.equal(findNames('import { summarize } from "../../policy-ab-quick/lib/summary.mjs"; summarize(x);', FORBIDDEN, { ownDeclarations: true }).length, 1, 'an imported one is');
  assert.deepEqual(findNames('// childCosts is not allowed\n/* parallelism */\nconst s = "a // b"; summarize(x);', FORBIDDEN), [{ name: 'summarize', line: 3 }]);
  assert.deepEqual(findCalls('const x = costFromMessages ( e );\n// costFromMessages(e)', 'costFromMessages'), [1]);
  assert.deepEqual(importedNames("import { a, b as c } from '../../policy-ab-quick/lib/x.mjs';\nimport { z } from './y.mjs';", /policy-ab-quick/), ['a', 'b']);
});

test('The_cost_modules_take_from_the_older_spike_only_the_pure_readers_they_need', () => {
  const root = spikeDir.replace(/[\\/]$/, '');
  const entries = ['lib/session-cost.mjs', 'lib/claude-usage.mjs', 'lib/aliases.mjs', 'lib/crosscheck.mjs'].map(p => path.join(root, p));
  const graph = [...new Set(entries.flatMap(e => localGraph(e, root)))];
  assert.ok(graph.map(rel).includes('lib/prices.mjs'));
  const imported = graph.flatMap(f => importedNames(readFileSync(f, 'utf8'), /policy-ab-quick/));
  assert.ok(imported.length > 0, 'the graph does reach the older spike (resultEnvelope), so this check is not vacuous');
  assert.deepEqual(imported.filter(n => !ALLOWED_FROM_POLICY_AB.includes(n)), []);
});

test('Every_production_file_takes_only_allowed_names_from_the_older_spike', () => {
  const files = production();
  const bad = files.flatMap(f => importViolations(readFileSync(f, 'utf8'), /policy-ab-quick/, ALLOWED_FROM_POLICY_AB).map(v => `${rel(f)}: ${v}`));
  assert.deepEqual(bad, []);
  assert.ok(files.some(f => importedNames(readFileSync(f, 'utf8'), /policy-ab-quick/).length > 0), 'the check is not vacuous');
});

test('A_local_declaration_does_not_mask_an_aliased_import_of_a_fitted_path', () => {
  const masked = "import { summarize as s } from '../../policy-ab-quick/lib/summary.mjs';\nfunction summarize() {}\ns({});";
  assert.ok(findNames(masked, FORBIDDEN, { ownDeclarations: true }).some(h => h.name === 'summarize'), 'the aliased import is still a hit');
  assert.ok(importViolations(masked, /policy-ab-quick/, ALLOWED_FROM_POLICY_AB).some(v => /summarize/.test(v)));
});

test('Namespace_default_dynamic_and_require_forms_of_the_older_spike_are_violations', () => {
  const forms = [
    "import * as s from '../../policy-ab-quick/lib/summary.mjs';",
    "import s from '../../policy-ab-quick/lib/summary.mjs';",
    "const s = await import('../../policy-ab-quick/lib/summary.mjs');",
    "const s = require('../../policy-ab-quick/lib/summary.cjs');",
  ];
  for (const f of forms) assert.equal(importViolations(f, /policy-ab-quick/, ALLOWED_FROM_POLICY_AB).length, 1, f);
  assert.deepEqual(importViolations("import { resultEnvelope } from '../../policy-ab-quick/lib/summary.mjs';", /policy-ab-quick/, ALLOWED_FROM_POLICY_AB), []);
});

test('Js_and_cjs_files_are_scanned_like_mjs', () => {
  const dir = tmpDir('scan');
  mkdirSync(path.join(dir, 'lib'));
  writeFileSync(path.join(dir, 'lib', 'a.cjs'), 'costFromMessages([]);\n');
  writeFileSync(path.join(dir, 'lib', 'b.js'), 'x();\n');
  writeFileSync(path.join(dir, 'lib', 'c.mjs'), 'y();\n');
  writeFileSync(path.join(dir, 'lib', 'd.txt'), 'costFromMessages([]);\n');
  assert.deepEqual(sourceFiles(dir).map(f => path.basename(f)).sort(), ['a.cjs', 'b.js', 'c.mjs']);
});

test('The_scan_root_is_this_spike', () => {
  assert.equal(spikeDir, fileURLToPath(new URL('..', import.meta.url)));
});
