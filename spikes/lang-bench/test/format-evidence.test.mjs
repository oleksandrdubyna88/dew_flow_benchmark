// Fix batch 5 (E1.S9): the direct evidence that the frozen comparison protects the protected parts of the REAL draft. The full sweep (three changes to every line of the draft: delete
// the line, append ` zz` to it, insert an empty line after it; 3,435 candidates, about two minutes) passed in a protected part only for the `Gate verdict` value, the 14 free 2.1 rows
// and one extra line feed at the end of the file. This guard runs a sample of the lines (every 53rd, plus the lines that matter) and asserts the same: a change to a protected line is
// refused, . It needs the real draft (LANG_V2_TASK_REPO); the cases are asserted by position in the file, never retyped.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isProtected, partsOf, readClassed } from '../lib/format-class-table.mjs';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { realDraft } from './support/format-fixtures.mjs';

const STEP = 53;
const CHANGES = {
  delete: (lines, at) => lines.filter((_, index) => index !== at),
  alter: (lines, at) => lines.map((text, index) => (index === at ? `${text} zz` : text)),
  blank: (lines, at) => [...lines.slice(0, at + 1), '', ...lines.slice(at + 1)],
};

test('A_change_to_a_protected_line_of_the_real_draft_is_refused_and_the_passes_are_the_ones_the_rule_names', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const lines = draft.bytes.toString('utf8').split('\n');
  const read = readClassed(draft.bytes);
  const parts = partsOf(read.format, read.rows);
  const classOf = line => parts.findLast(part => part.no <= line)?.cls ?? 'common';
  const inTable = no => parts.findLast(part => part.no <= no)?.key === '2.1';
  const forced = [lines.findIndex(text => text.startsWith('Gate verdict:')), lines.findIndex(text => text.startsWith('| F-REQ |')), lines.findIndex(text => text.startsWith('| F-PORT |')), lines.length - 2, lines.length - 1];
  const sample = [...new Set([...lines.keys()].filter(index => index % STEP === 0).concat(forced))].sort((a, b) => a - b);
  const passes = [];
  for (const at of sample) {
    for (const [name, change] of Object.entries(CHANGES)) {
      const result = checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(change(lines, at).join('\n')), reference: draft.recorded });
      if (result.exit === 0 && isProtected(classOf(at + 1))) passes.push({ name, at, text: lines[at] });
    }
  }
  for (const pass of passes) {
    const gate = pass.name === 'alter' && pass.text.startsWith('Gate verdict:');
    const row = pass.name !== 'blank' && /^\| F-/.test(pass.text) && inTable(pass.at + 1);
    const endOfFile = pass.name === 'blank' && pass.at >= lines.length - 2;
    assert.ok(gate || row || endOfFile, `${pass.name} at line ${pass.at + 1} passed in a protected part: ${pass.text.slice(0, 80)}`);
  }
  const names = (index, change) => passes.some(pass => pass.at === index && pass.name === change);
  assert.ok(names(forced[0], 'alter'), 'the Gate verdict value may change (the scan can see a pass)');
  assert.ok(names(forced[2], 'alter') && names(forced[2], 'delete'), 'a free 2.1 row may change or go (the scan can see a pass)');
  assert.ok(names(forced[4], 'blank') || names(forced[3], 'blank'), 'one extra line feed at the end passes (the scan can see a pass)');
  assert.ok(!['delete', 'alter', 'blank'].some(change => names(forced[1], change)), 'a protected 2.1 row never passes');
  assert.ok(sample.length >= 20, 'the sample covers the file');
});
