// The two implementations meet here: policy-ab-quick's audit hook WRITES the record, E1's span builder READS it.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { auditRecord } from '../../policy-ab-quick/hooks/audit.mjs';
import { BACKGROUND, BUILD_TEST } from '../lib/categories.mjs';
import { spansFrom } from '../lib/spans.mjs';

const noSha = () => null;
const T0 = Date.parse('2026-10-29T10:00:00.000Z');
const iso = ms => new Date(T0 + ms).toISOString();
const payload = (event, id, command, extra = {}, input = {}) =>
  ({ hook_event_name: event, tool_name: 'Bash', tool_use_id: id, session_id: 's1', agent_id: 'ag1', tool_input: { command, ...input }, ...extra });

/** The records the hook writes for one Bash call: Pre at `startMs`, Post (with the CLI's duration) at `endMs`. */
const pair = (id, command, startMs, endMs, input) => [
  auditRecord(payload('PreToolUse', id, command, {}, input), iso(startMs), noSha),
  auditRecord(payload('PostToolUse', id, command, { duration_ms: endMs - startMs }, input), iso(endMs), noSha),
];

test('Audit_records_become_spans_in_E1s_builder', () => {
  const audit = [...pair('toolu_build', 'dotnet build', 1000, 5321), ...pair('toolu_bg', 'npm run watch', 6000, 6040, { run_in_background: true })];
  const r = spansFrom({ audit });
  const build = r.spans.filter(s => s.category === BUILD_TEST);
  assert.equal(build.length, 1);
  assert.equal(build[0].toolUseId, 'toolu_build');
  assert.equal(build[0].endMs - build[0].startMs, 4321, 'the exact ms between the two ts');
  assert.equal(build[0].agentId, 'ag1');
  const background = r.spans.filter(s => s.category === BACKGROUND);
  assert.equal(background.length, 1);
  assert.equal(background[0].toolUseId, 'toolu_bg');
  assert.ok(r.notes.some(n => /1 background Bash launch/.test(n)), r.notes.join('|'));
  assert.deepEqual(r.unpaired, []);
});

test('A_record_without_its_tool_use_id_is_reported_unpaired_never_dropped', () => {
  const [pre] = pair('x', 'dotnet build', 0, 10);
  const r = spansFrom({ audit: [{ ...pre, tool_use_id: null }] });
  assert.deepEqual(r.spans, []);
  assert.deepEqual(r.unpaired, [{ kind: 'tool', id: null, missing: 'tool_use_id' }]);
});
