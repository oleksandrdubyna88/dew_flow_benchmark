import assert from 'node:assert/strict';
import { test } from 'node:test';
import { aliasRecord, checkAliases } from '../lib/aliases.mjs';
import { REFUSAL_FALLBACK, TWO_CHILDREN, mapEvents, readEvents } from './synth.mjs';

const real = readEvents(TWO_CHILDREN);
const record = events => {
  const r = aliasRecord({ events });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};
const baseline = record(real);

/** Rename one model id everywhere a stream names it for a child: launch result, message, and (optionally) the envelope line. */
function renameChild(events, from, to, { alias, usageToo = true } = {}) {
  return mapEvents(events, e => {
    if (e.tool_use_result?.resolvedModel === from) e.tool_use_result.resolvedModel = to;
    if (e.message?.model === from && e.parent_tool_use_id) e.message.model = to;
    if (alias && e.type === 'assistant') {
      for (const b of e.message.content ?? []) if (b.type === 'tool_use' && b.name === 'Agent' && b.input.model === 'sonnet') b.input.model = alias;
    }
    if (usageToo && e.type === 'result' && e.modelUsage[from]) { e.modelUsage[to] = e.modelUsage[from]; delete e.modelUsage[from]; }
    return e;
  });
}

test('The_baseline_of_the_real_stream_maps_each_alias_to_its_exact_id', () => {
  assert.deepEqual(baseline.aliases, { opus: 'claude-opus-5-5', sonnet: 'claude-sonnet-5-5' });
  assert.deepEqual(baseline.internal, ['claude-haiku-4-5-20251001']);
  assert.deepEqual(baseline.fallbacks, []);
  assert.deepEqual(baseline.unresolved, []);
});

test('A_session_resolving_an_alias_it_used_to_a_new_id_stops_the_series', () => {
  const current = record(renameChild(real, 'claude-sonnet-5-5', 'claude-sonnet-5-6', { usageToo: false }));
  const verdict = checkAliases(baseline, current);
  assert.equal(verdict.verdict, 'stop');
  assert.match(verdict.reasons[0], /sonnet/);
  assert.match(verdict.reasons[0], /claude-sonnet-5-5/);
  assert.match(verdict.reasons[0], /claude-sonnet-5-6/);
});

test('A_session_that_launched_no_sonnet_child_is_not_drift', () => {
  const dropSonnet = mapEvents(real, e => {
    if (e.type === 'result') delete e.modelUsage['claude-sonnet-5-5'];
    return e;
  });
  const events = dropSonnet.filter(e => !(e.message?.model === 'claude-sonnet-5-5') && e.tool_use_result?.resolvedModel !== 'claude-sonnet-5-5');
  const current = record(events);
  assert.equal(current.aliases.sonnet, undefined, 'the fixture really lost its Sonnet child');
  assert.deepEqual(checkAliases(baseline, current), { verdict: 'ok', reasons: [], extend: {} });
});

test('A_first_use_of_an_alias_extends_the_baseline_and_does_not_stop', () => {
  const current = record(renameChild(real, 'claude-sonnet-5-5', 'claude-fable-5-1', { alias: 'fable' }));
  assert.equal(current.aliases.fable, 'claude-fable-5-1');
  const noFable = { ...baseline, aliases: { opus: 'claude-opus-5-5' } };
  const verdict = checkAliases(noFable, current);
  assert.equal(verdict.verdict, 'ok');
  assert.deepEqual(verdict.extend, { fable: 'claude-fable-5-1' });
  assert.deepEqual(noFable.aliases, { opus: 'claude-opus-5-5' }, 'the baseline is not mutated');
});

test('A_new_internal_id_stops', () => {
  const events = mapEvents(real, e => {
    if (e.type === 'result') { e.modelUsage['claude-haiku-5-5'] = e.modelUsage['claude-haiku-4-5-20251001']; delete e.modelUsage['claude-haiku-4-5-20251001']; }
    return e;
  });
  const current = record(events);
  assert.deepEqual(current.internal, ['claude-haiku-5-5']);
  const verdict = checkAliases(baseline, current);
  assert.equal(verdict.verdict, 'stop');
  assert.match(verdict.reasons[0], /claude-haiku-5-5/);
});

test('A_model_refusal_fallback_flags_and_does_not_stop', () => {
  const current = record(readEvents(REFUSAL_FALLBACK));
  assert.deepEqual(current.fallbacks[0], { parent: null, original: 'claude-opus-5-5', fallback: 'claude-opus-4-8', category: 'cyber' });
  assert.deepEqual(current.internal, [], 'the fallback model is the fallback, not an internal line');
  const verdict = checkAliases(baseline, current);
  assert.equal(verdict.verdict, 'flag');
  assert.match(verdict.reasons[0], /claude-opus-5-5 -> claude-opus-4-8/);
});

test('Two_children_on_one_alias_resolving_to_two_ids_is_a_failure_value', () => {
  let n = 0;
  const events = mapEvents(real, e => {
    if (e.tool_use_result?.resolvedModel === 'claude-opus-5-5') { n += 1; e.tool_use_result.resolvedModel = 'claude-opus-5-6'; }
    return e;
  });
  assert.ok(n > 0, 'the fixture has an opus child');
  const r = aliasRecord({ events });
  assert.equal(r.ok, false);
  assert.match(r.reason, /'opus'.*claude-opus-5-5.*claude-opus-5-6/);
});

test('A_launch_that_resolved_nothing_is_listed_not_mapped', () => {
  const events = mapEvents(real, e => {
    if (e.tool_use_result?.resolvedModel === 'claude-sonnet-5-5') delete e.tool_use_result.resolvedModel;
    return e;
  });
  const r = record(events);
  assert.equal(r.aliases.sonnet, undefined);
  assert.equal(r.unresolved.length, 1);
  assert.match(r.unresolved[0].why, /no resolvedModel/);
});

test('A_stream_without_an_init_event_has_no_main_alias', () => {
  const r = aliasRecord({ events: real.filter(e => !(e.type === 'system' && e.subtype === 'init')) });
  assert.equal(r.ok, true);
  assert.equal(r.value.aliases.opus, 'claude-opus-5-5', 'the opus child still resolves the alias');
});
