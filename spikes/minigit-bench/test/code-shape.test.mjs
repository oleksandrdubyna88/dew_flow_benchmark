// The shape rule of common.coding-style on the runner's command path (E3b coai-1): a function of `bare-runner.mjs` has fewer than 50 lines and a cyclomatic complexity of at most 4.
// The scanner itself is checked on a sample first: a measure that cannot tell a decision from a word in a message proves nothing.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { codeOnly, functionShapes } from './code-shape.mjs';
import { spikeDir } from './helpers.mjs';

/**
 * The files E3b owns: the runner and everything the bare arm added around it. Each is measured whole, with no exemption. A file joins the list in the commit that splits its last function.
 * Files NOT listed are owned by E1 or E2 (the counters, the broker, the egress proxy, the box, the launch, the smoke, the held-out suite) and are left as they are.
 */
const E3B_FILES = [
  'bare-runner.mjs',
  'lib/bare-config.mjs', 'lib/bare-collect.mjs', 'lib/bare-exit.mjs', 'lib/bare-gates.mjs', 'lib/bare-init.mjs', 'lib/bare-limits.mjs', 'lib/bare-outcome.mjs', 'lib/bare-phase.mjs', 'lib/bare-pins.mjs', 'lib/bare-plan.mjs',
  'lib/bare-reclass.mjs', 'lib/bare-record.mjs', 'lib/bare-records-io.mjs', 'lib/bare-resume.mjs', 'lib/bare-schedule.mjs', 'lib/bare-series.mjs', 'lib/bare-settle.mjs',
  'lib/run-map.mjs', 'lib/series-lock.mjs', 'lib/spend-gate.mjs', 'lib/ledger.mjs', 'lib/amendments.mjs', 'lib/alias-store.mjs', 'lib/seed.mjs', 'lib/prereg-guard.mjs', 'lib/prereg-consistency.mjs',
  'lib/contamination-links.mjs', 'lib/contamination-reach.mjs',
  'lib/aa-check.mjs', 'lib/aa-facts.mjs', 'lib/aa-rules.mjs', 'lib/aa-rules-twins.mjs', 'lib/aa-smoke.mjs', 'lib/aa-verdicts.mjs',
  'lib/quiet.mjs', 'lib/cpu-sampler.mjs', 'lib/cache-snapshot.mjs', 'lib/packages.mjs', 'lib/workspace.mjs', 'lib/loc.mjs', 'lib/canary-plan.mjs', 'lib/rehearsal.mjs', 'lib/stub-cli.mjs', 'lib/stub-upstream.mjs', 'lib/score-run.mjs', 'lib/stream-calls.mjs',
  'prereg/blocks.mjs', 'prereg/cli.mjs', 'prereg/extract-contract.mjs', 'prereg/extract-prediction.mjs', 'prereg/extract-prompts.mjs', 'prereg/heldout-prereg.mjs', 'prereg/parent.mjs', 'prereg/spend-prior.mjs',
];

test('The_shape_scanner_counts_decisions_in_code_and_not_words_in_messages', () => {
  const sample = [
    'function a(x) {',
    '  // if for while && || ? in a comment',
    '  const m = `if ${x ? "a" : `b ${x && 1}`} while`;',
    "  const q = 'if && || ?';",
    '  if (x && x.y) return x?.z ?? 1;',
    '  const r = /a(b)?[?/]c/.test(x) ? 1 / 2 : 0;',
    '  return m + q;',
    '}',
    'export async function b() { return 1; }',
  ].join('\n');
  assert.doesNotMatch(codeOnly(sample), /comment|while`|'if/, 'comments and string contents are blanked');
  assert.deepEqual(functionShapes(sample), [{ name: 'a', lines: 8, complexity: 1 + 1 + 1 + 1 + 1 + 1 }, { name: 'b', lines: 1, complexity: 1 }], 'a template expression ?: and &&, the if and its && and a ternary count; ?. and ?? and a ? inside a regex literal do not, and a division is not a regex');
});

for (const file of E3B_FILES) {
  test(`Every_function_of_${file.replace(/\W/g, '_')}_has_under_50_lines_and_a_complexity_of_at_most_4`, () => {
    const shapes = functionShapes(readFileSync(path.join(spikeDir, file), 'utf8'));
    assert.ok(shapes.length > 0, `${file} has top-level functions`);
    assert.deepEqual(shapes.filter(s => s.lines >= 50 || s.complexity > 4), [], 'split the function: one helper per step');
  });
}

/**
 * Every other source file of the spike, owned by E1 or E2 and left as it is. With E3B_FILES it is the ownership boundary: a source
 * file in neither list fails the test below, so a new E3b module cannot escape the shape rule by not being listed (coai, 2026-10-30).
 */
const OWNED_ELSEWHERE = [
  'adjudicate.mjs', 'heldout-tool.mjs', 'run-tests.mjs', 'score-session.mjs', 'smoke-session.mjs', 'harness/box-init.mjs',
  'capture/capture-env.mjs', 'capture/capture-files.mjs', 'capture/capture-plan.mjs', 'capture/capture-run.mjs', 'capture/capture-session.mjs',
  'capture/otlp-sink.mjs', 'capture/policy-input.mjs', 'capture/sanitise.mjs', 'capture/stamp-hook.mjs', 'capture/verdict-rules.mjs', 'capture/verdicts.mjs',
  'lib/adjudicate-mask.mjs', 'lib/adjudicate.mjs', 'lib/agent-files.mjs', 'lib/aliases.mjs', 'lib/broker-body.mjs', 'lib/broker-routes.mjs', 'lib/broker.mjs',
  'lib/budget.mjs', 'lib/bwrap.mjs', 'lib/categories.mjs', 'lib/classify.mjs', 'lib/claude-usage.mjs', 'lib/coai-usage.mjs', 'lib/contamination-refs.mjs',
  'lib/contamination.mjs', 'lib/crosscheck.mjs', 'lib/disposers.mjs', 'lib/egress-hosts.mjs', 'lib/egress.mjs', 'lib/fetch-inputs.mjs', 'lib/find-on-path.mjs',
  'lib/git-guard.mjs', 'lib/heldout-archive.mjs', 'lib/heldout-calibrate.mjs', 'lib/heldout-crypto.mjs', 'lib/heldout-leak.mjs', 'lib/heldout-pipeline.mjs',
  'lib/heldout-run.mjs', 'lib/heldout-seal.mjs', 'lib/host-checks.mjs', 'lib/http-body.mjs', 'lib/inputs.mjs', 'lib/intervals.mjs', 'lib/janitor.mjs',
  'lib/launch.mjs', 'lib/limits.mjs', 'lib/link-class.mjs', 'lib/lower-priority.mjs', 'lib/otel-decode.mjs', 'lib/otel-receiver.mjs', 'lib/preflight.mjs',
  'lib/prices.mjs', 'lib/proc-sampler.mjs', 'lib/readback.mjs', 'lib/render.mjs', 'lib/result.mjs', 'lib/run.mjs', 'lib/sandbox.mjs', 'lib/score-deps.mjs',
  'lib/score-pipeline.mjs', 'lib/score-scripts.mjs', 'lib/score-steps.mjs', 'lib/secrets.mjs', 'lib/session-cost.mjs', 'lib/session-launch.mjs',
  'lib/session-scan.mjs', 'lib/session-state.mjs', 'lib/smoke-load.mjs', 'lib/smoke-run.mjs', 'lib/smoke-verdicts.mjs', 'lib/spans.mjs', 'lib/stream-tap.mjs',
  'lib/timeline.mjs', 'lib/tls-hello.mjs', 'lib/token-scan.mjs', 'lib/token-txt.mjs', 'lib/transcripts.mjs', 'lib/visible.mjs', 'lib/watchdogs.mjs',
];

test('Every_source_file_of_the_spike_is_either_E3b_owned_and_measured_or_named_as_owned_elsewhere', () => {
  const sources = ['.', 'lib', 'prereg', 'capture', 'harness']
    .flatMap(dir => readdirSync(path.join(spikeDir, dir)).filter(f => f.endsWith('.mjs')).map(f => (dir === '.' ? f : `${dir}/${f}`)));
  const listed = new Set([...E3B_FILES, ...OWNED_ELSEWHERE]);
  assert.deepEqual(sources.filter(f => !listed.has(f)).sort(), [], 'a new source file joins E3B_FILES (measured) or OWNED_ELSEWHERE (named, with its owner)');
  assert.deepEqual(E3B_FILES.filter(f => OWNED_ELSEWHERE.includes(f)), [], 'no file is in both lists');
});
