// The note counts the four verdicts per stack and the finality of the sessions from the records and the run-map alone (section 2.2 P14): a number is derived, never typed.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { INPUTS } from '../lib/paths.mjs';
import { renderNote } from '../lib/note.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { cpcBoth, exploreSection, pairRecordOf } from '../lib/records.mjs';
import { scoreShape } from '../lib/score.mjs';
import { VOID_UNSCORED } from '../lib/series.mjs';
import { MIB, recordOf, resultOf } from './support.mjs';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const PINS = JSON.parse(readFileSync(path.join(INPUTS, 'explore.json'), 'utf8'));
const TASK = resolveTask(loadExplore().value, '05').value;

/** A session whose score carries the verdict of its ten passes. `verdict: null` is a session scored the old way (no passes). */
function session({ pair, stack, attempt = 1, verdict, void: isVoid = false }) {
  const score = scoreShape({ result: resultOf(), traps: [] });
  const scored = verdict === null ? score : { ...score, explore: { ...score.explore, verdict, scoringState: verdict === 'unscored: environment' ? 'exhausted' : 'decided', passes: 10 } };
  const base = recordOf({ score: scored });
  const cls = isVoid ? VOID_UNSCORED : 'completed';
  const record = { ...base, class: cls, data: !isVoid, ids: { ...base.ids, stack, pair, attempt, label: attempt === 1 ? 'paired' : 'ran unpaired' } };
  const withExplore = { ...record, explore: isVoid ? { scored: { known: false, why: 'the session was not scored' } } : exploreSection({ score: scored, audit: [] }) };
  return { entry: { rid: `r${pair}${stack[0]}${attempt}`, pair, stack, attempt, kind: 'pilot', mask: '0-5', label: record.ids.label, status: isVoid ? 'ran' : 'scored', class: cls, data: !isVoid, run: { startSkewMs: 10, spawnStampMs: 1, flags: [] } }, record: withExplore };
}

function inputFor(sessions) {
  const pairs = [...new Set(sessions.map(s => s.entry.pair))].map(pair => pairRecordOf({ pair, entries: sessions.filter(s => s.entry.pair === pair).map(s => s.entry), recordOf: rid => sessions.find(s => s.entry.rid === rid)?.record ?? null }));
  const cpc = Object.fromEntries(['csharp', 'node'].map(stack => [stack, cpcBoth(sessions.filter(s => s.entry.stack === stack).map(s => ({ record: s.record, accepted: s.record.explore.accepted?.value === true })))]));
  return { date: '2026-11-08', task: TASK, seriesId: 'x-20261108-ab12', rehearsal: false, subject: { branch: 'b', sha: 'a'.repeat(40), harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: 'b'.repeat(12), pilotIdentity: 'c'.repeat(40) }, pins: PINS, limits: { timeMs: 2000, memoryBytes: 512 * MIB, buildTimeoutMs: 600000 }, sessions, pairs, cpc };
}

const SESSIONS = [
  session({ pair: 1, stack: 'csharp', verdict: 'accepted' }), session({ pair: 1, stack: 'node', verdict: 'accepted' }),
  session({ pair: 2, stack: 'csharp', verdict: 'rejected' }), session({ pair: 2, stack: 'node', verdict: 'accepted' }),
  session({ pair: 3, stack: 'csharp', verdict: 'intermittently incorrect' }), session({ pair: 3, stack: 'node', verdict: 'unscored: environment', void: true }),
  session({ pair: 3, stack: 'node', attempt: 2, verdict: 'unscored: environment', void: true }),
  session({ pair: 4, stack: 'csharp', verdict: 'unscored: environment', void: true }),
];

test('The_note_counts_accepted_rejected_intermittently_incorrect_and_unscored_environment_per_stack_from_the_records_alone', () => {
  const text = renderNote(inputFor(SESSIONS));
  const section = text.split('\n## ').find(s => s.startsWith('Scoring verdicts'));
  assert.ok(section, 'the note has the scoring section');
  assert.match(section, /\| stack \| accepted \| rejected \| intermittently incorrect \| unscored: environment \|/);
  assert.match(section, /\| C# \| 1 \| 1 \| 1 \| 1 \|/);
  assert.match(section, /\| TypeScript \| 2 \| 0 \| 0 \| 2 \|/, 'a session left unscored has no score: it is counted from the run-map class, once per session');
  assert.match(section, /- C#: 3 scored \(final\), 1 void that the series may re-run once, 0 void with its re-run used/);
  assert.match(section, /- TypeScript: 2 scored \(final\), 0 void that the series may re-run once, 1 void with its re-run used/);
  assert.ok(section.includes('ten scoring passes'), 'the section says what a verdict is');
});

test('A_note_of_sessions_scored_the_old_way_has_no_scoring_section_so_its_text_is_what_it_was', () => {
  const old = [session({ pair: 1, stack: 'csharp', verdict: null }), session({ pair: 1, stack: 'node', verdict: null })];
  assert.equal(renderNote(inputFor(old)).includes('Scoring verdicts'), false);
});
