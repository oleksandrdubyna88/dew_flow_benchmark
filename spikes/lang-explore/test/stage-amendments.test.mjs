// The dated lines of Stage 1 (F3.S1 (a), PR 3, item 7): `lib/stage-amendments.mjs` reads the registered `amendments.md`. It parses four kinds of its own (the owner's go for the pilot, the owner's go for rounds, the one `caps` line,
// the pilot's GO line) and hands every other line to v1's `parseAmendments`. A malformed line of its kinds is reported and never counts. The module is pure over the text; its one edge reads the file (absent is no line).
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { parseAmendments } from '../../minigit-bench/lib/amendments.mjs';
import { STAGE_ROUNDS } from '../lib/stage-schedule.mjs';
import { capsOf, homePathProblems, ownerGoLine, ownerGoProblem, parseStageAmendments, pilotGoProblem, readStageAmendments, readbackLinesOf, stageHandVerdictsFor } from '../lib/stage-amendments.mjs';

const GO_PILOT = '2026-11-20 | owner-go | pilot | the owner, 2026-11-20: run the pilot';
const GO_ROUNDS = '2026-11-21 | owner-go | rounds:1-2 | the owner, 2026-11-21: rounds one and two';
const CAPS = '2026-11-22 | caps | wallMs:5400000 | stallMs:3000000 | the wall of the A/A twins, G-18';
const GO_LINE = '2026-11-22 | pilot | GO | verdicts:0123456789ab | the team lead';
const VERDICT = '2026-11-23 | G-1 | holds | abc123 | ~/rsd/lang-v2/x-1/readback.md | the judge';
const text = (...lines) => `# the amendments\n\nsome prose that is no line\n\n${lines.join('\n')}\n`;

test('The_four_lines_of_Stage_1_are_parsed_and_every_other_line_goes_to_v1s_parser_unchanged', () => {
  const body = text(GO_PILOT, GO_ROUNDS, CAPS, GO_LINE, VERDICT, '2026-11-24 | instrument | harness:0123456789ab | the accepted identity, G-3');
  const got = parseStageAmendments(body);
  assert.deepEqual(got.ownerGo.map(l => [l.date, l.scope]), [['2026-11-20', 'pilot'], ['2026-11-21', { from: 1, to: 2 }]]);
  assert.deepEqual(got.ownerGo.map(l => l.words), ['the owner, 2026-11-20: run the pilot', 'the owner, 2026-11-21: rounds one and two']);
  assert.deepEqual(got.caps.map(l => [l.date, l.wallMs, l.stallMs]), [['2026-11-22', 5_400_000, 3_000_000]]);
  assert.deepEqual(got.pilotGo.map(l => [l.date, l.verdicts12]), [['2026-11-22', '0123456789ab']]);
  assert.deepEqual(got.malformed, []);
  assert.deepEqual(got.v1, parseAmendments(text(VERDICT, '2026-11-24 | instrument | harness:0123456789ab | the accepted identity, G-3')), 'v1 sees the other lines and only them, and v1\'s parser is not changed');
  assert.equal(got.v1.malformed.length, 0, 'v1 never sees one of the four lines, which it would call malformed');
});

test('A_malformed_go_line_is_reported_and_never_counts', () => {
  const bad = [
    ['2026-11-20 | owner-go | pilot |', 'the owner\'s words'],
    ['2026-11-20 | owner-go | pilot', 'the owner\'s words'],
    ['2026-11-20 | owner-go | rounds:3-2 | words', 'rounds'],
    ['2026-11-20 | owner-go | rounds:0-2 | words', 'rounds'],
    [`2026-11-20 | owner-go | rounds:1-${STAGE_ROUNDS + 1} | words`, 'rounds'],
    ['2026-11-20 | owner-go | rounds:x | words', 'rounds'],
    ['2026-11-20 | owner-go | everything | words', 'scope'],
    ['2026-02-30 | owner-go | pilot | words', 'date'],
    ['2026-11-20 | caps | wallMs:5400000 | stallMs:3000000 |', 'reason'],
    ['2026-11-20 | caps | wallMs:0 | stallMs:3000000 | reason', 'wallMs'],
    ['2026-11-20 | caps | wallMs:abc | stallMs:3000000 | reason', 'wallMs'],
    ['2026-11-20 | pilot | NO GO | verdicts:0123456789ab | who', 'GO'],
    ['2026-11-20 | pilot | GO | verdicts:xyz | who', 'verdicts'],
    ['2026-11-20 | pilot | GO | verdicts:0123456789ab |', 'who'],
  ];
  for (const [line, mentions] of bad) {
    const got = parseStageAmendments(text(line));
    assert.deepEqual([got.ownerGo, got.caps, got.pilotGo], [[], [], []], `${line}: counts for nothing`);
    assert.equal(got.malformed.length, 1, `${line}: reported once`);
    assert.equal(got.malformed[0].line, line);
    assert.ok(got.malformed[0].why.includes(mentions), `${line}: the sentence says what is wrong (${got.malformed[0].why})`);
  }
  assert.equal(ownerGoProblem({ lines: parseStageAmendments(text(bad[0][0])), scope: { pilot: true } }) !== null, true, 'a malformed line opens nothing');
});

test('An_owner_go_for_rounds_1_to_2_does_not_cover_round_3_and_one_for_the_pilot_covers_no_round', () => {
  const lines = parseStageAmendments(text(GO_PILOT, GO_ROUNDS));
  assert.equal(ownerGoProblem({ lines, scope: { pilot: true } }), null);
  assert.equal(ownerGoProblem({ lines, scope: { round: 1 } }), null);
  assert.equal(ownerGoProblem({ lines, scope: { round: 2 } }), null);
  assert.match(ownerGoProblem({ lines, scope: { round: 3 } }), /round 3.*no owner-go line covers/);
  const pilotOnly = parseStageAmendments(text(GO_PILOT));
  assert.match(ownerGoProblem({ lines: pilotOnly, scope: { round: 1 } }), /round 1.*no owner-go line covers/);
  const roundsOnly = parseStageAmendments(text(GO_ROUNDS));
  assert.match(ownerGoProblem({ lines: roundsOnly, scope: { pilot: true } }), /the pilot.*no owner-go line covers/);
  assert.match(ownerGoProblem({ lines: parseStageAmendments(text()), scope: { round: 1 } }), /no owner-go line covers round 1/);
  assert.match(ownerGoProblem({ lines, scope: { round: 6 } }), /round 6.*the rounds are 1 to 5/);
  assert.match(ownerGoProblem({ lines, scope: {} }), /scope/);
});

test('The_first_covering_go_line_is_the_one_a_launch_names', () => {
  const lines = parseStageAmendments(text('2026-11-25 | owner-go | rounds:1-5 | the owner, again', GO_ROUNDS));
  assert.equal(ownerGoLine({ lines, scope: { round: 2 } }).value.line, GO_ROUNDS, 'the earliest date covers');
  assert.equal(ownerGoLine({ lines, scope: { round: 5 } }).value.date, '2026-11-25');
  assert.equal(ownerGoLine({ lines, scope: { round: 6 } }).ok, false);
});

test('A_GO_line_that_names_another_verdicts_hash_than_the_one_it_is_checked_against_is_reported', () => {
  const lines = parseStageAmendments(text(GO_LINE));
  assert.equal(pilotGoProblem({ lines }), null, 'with no hash to check against, a dated GO line is a GO (the harness never reads the table\'s file)');
  assert.equal(pilotGoProblem({ lines, verdicts12: '0123456789ab' }), null);
  assert.match(pilotGoProblem({ lines, verdicts12: 'ffffffffffff' }), /names verdicts:0123456789ab.*ffffffffffff/);
  assert.match(pilotGoProblem({ lines: parseStageAmendments(text()) }), /no dated .pilot \| GO. line/);
});

test('The_caps_line_gives_the_wall_and_the_stall_once_and_a_second_line_is_reported', () => {
  const one = parseStageAmendments(text(CAPS));
  assert.deepEqual(capsOf(one), { wallMs: 5_400_000, stallMs: 3_000_000, date: '2026-11-22' });
  assert.equal(capsOf(parseStageAmendments(text())), null);
  const two = parseStageAmendments(text(CAPS, '2026-11-23 | caps | wallMs:5500000 | stallMs:3100000 | again, G-18'));
  assert.equal(two.caps.length, 2);
  assert.match(capsOf(two).problem, /one .caps. line/);
});

test('The_read_back_lines_and_the_hand_verdicts_are_the_other_lines_through_v1s_reader', () => {
  const body = text(VERDICT, '2026-11-24 | G-1 | refuted | def456 | ~/e.md | the judge', '2026-11-24 | G-5 | holds | abc123 | ~/e.md | the judge');
  assert.deepEqual(readbackLinesOf(body).map(l => [l.g, l.verdict, l.rid, l.date]), [['G-1', 'holds', 'abc123', '2026-11-23'], ['G-1', 'refuted', 'def456', '2026-11-24'], ['G-5', 'holds', 'abc123', '2026-11-24']]);
  assert.deepEqual(stageHandVerdictsFor(body, 'def456').map(v => v.verdict), ['refuted']);
  assert.deepEqual(stageHandVerdictsFor(body, 'zzz999'), []);
  assert.deepEqual(readbackLinesOf(''), []);
});

test('An_evidence_path_with_the_home_written_out_is_reported_and_the_tilde_form_is_not', () => {
  const home = '/home/someone';
  const body = text(VERDICT, `2026-11-23 | G-1 | holds | abc124 | ${home}/rsd/lang-v2/x/readback.md | the judge`);
  assert.deepEqual(homePathProblems(body, home).map(p => p.line.split(' | ')[3]), ['abc124']);
  assert.deepEqual(homePathProblems(text(VERDICT), home), []);
});

test('The_registered_file_is_read_and_an_absent_file_is_no_line', () => {
  const dir = mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-lex-amend-'));
  try {
    const file = path.join(dir, 'amendments.md');
    assert.equal(readStageAmendments(file).ok, true);
    assert.equal(readStageAmendments(file).value, '', 'absent: no line');
    writeFileSync(file, text(GO_PILOT));
    assert.equal(parseStageAmendments(readStageAmendments(file).value).ownerGo.length, 1);
    assert.equal(readStageAmendments(dir).ok, false, 'a folder in its place is unreadable, never an empty file');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
