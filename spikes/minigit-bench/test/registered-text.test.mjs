// The registered texts agree with the code (E3b final fix round): what `analysis-contract.md` (section 2 flags, section 9 money and stops), `statement.md` and the header of `amendments.md` say is what the
// runner does. A sentence that drifts from the code is a pre-registration that lies, and it is frozen by the tag, so each claim here is derived from the code and not typed a second time.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { CHECK_STOP_RULES } from '../lib/crosscheck.mjs';
import { AMENDABLE_INPUTS } from '../lib/prereg-guard.mjs';
import { LEDGER_KINDS } from '../lib/spend-gate.mjs';
import { spikeDir } from './helpers.mjs';

const read = name => readFileSync(path.join(spikeDir, 'inputs', name), 'utf8');
const squash = text => text.replace(/\s+/g, ' ');
const section = (text, from, to) => { const start = text.indexOf(from); assert.ok(start >= 0, `the contract has "${from}"`); const end = text.indexOf(to, start); return text.slice(start, end < 0 ? undefined : end); };

// ---- the flags table of section 2 (T3) ---------------------------------------------------------------------------------------------------

/** The files that put a flag on a session or a pair, and the pattern of the flag literals in them: a one-element array of a lowercase literal (`? ['over cap'] : []`) or of a template (`[`broker refused ${n}`]`). */
const EMITTERS = ['bare-outcome.mjs', 'bare-collect.mjs', 'bare-series.mjs', 'bare-records-io.mjs', 'quiet.mjs', 'workspace.mjs', 'cpu-sampler.mjs'];
const SOURCE_FLAG = /\[\s*(?:'([a-z][^'\]]*)'|`([a-z][^`\]]*)`)\s*\]/g;
/** `broker refused ${n}` and `broker refused k` are one flag; so are `T1 breach`, `T<n> breach`, and `package: ${rule}` and `package: <rule id>`. */
const shape = flag => flag.replace(/\$\{[^}]*\}/g, '<x>').replace(/<[^>]*>/g, '<x>').replace(/ k$/, ' <x>').replace(/^T\d /, 'T<x> ');

/** The flags the code emits: every flag literal of the emitting files, the `T<n>` rules of the cross-checks, and the package rules' `package: <rule>`. */
function emittedFlags() {
  const found = new Set();
  for (const file of EMITTERS) {
    for (const m of readFileSync(path.join(spikeDir, 'lib', file), 'utf8').matchAll(SOURCE_FLAG)) found.add(shape(m[1] ?? m[2]));
  }
  for (const rule of CHECK_STOP_RULES) found.add(shape(rule));
  assert.match(readFileSync(path.join(spikeDir, 'lib', 'packages.mjs'), 'utf8'), /`package: \$\{[^}]*\}`/, 'the package rules put `package: <rule>` on a session');
  found.add('package: <x>');
  return found;
}

/** The first column of the flags table of section 2. */
function tableFlags() {
  const text = section(read('analysis-contract.md'), '**Flags, never classes.**', '### 3.');
  const rows = text.split('\n').filter(l => /^\| `/.test(l));
  return rows.map(l => shape(/^\| `([^`]+)`/.exec(l)[1]));
}

const section9 = () => squash(section(read('analysis-contract.md'), '### 9. Money and the stop rules', '### 10.'));

test('The_ceiling_of_section_9_counts_every_bare_spend_and_the_prior_spend_as_spend_gate_does', () => {
  const item = /\*\*Ceiling \(parent §13\):\*\*(.*?)2\. \*\*Bare budget/.exec(section9())?.[1];
  assert.ok(item, 'item 1 of section 9 is the ceiling');
  const named = { aa: /A\/A/, pilot: /pilot/, data: /data/, void: /void/, prior: /prior spend/ };
  assert.deepEqual(Object.keys(named).sort(), LEDGER_KINDS.filter(k => k !== 'rehearsal').sort(), 'every kind of ledger line but the free rehearsal is a spend the ceiling counts (spend-gate.mjs: spendTotals)');
  for (const [kind, words] of Object.entries(named)) assert.match(item, words, `the ceiling names the '${kind}' spend`);
  assert.match(item, /rehearsal is \$0/, 'and says what the rehearsal costs');
  assert.doesNotMatch(item, /settled data \+ void \+ pilot spend/, 'the earlier wording left the A/A out');
});

test('The_statement_names_every_file_that_may_change_after_the_tag_and_how', () => {
  const text = squash(read('statement.md'));
  const frozen = /the exceptions are (.*?)\)\./.exec(text)?.[1];
  assert.ok(frozen, 'the frozen-by-the-tag paragraph lists the exceptions');
  for (const file of ['amendments.md', 'seed.txt', 'crosscheck-policy.json', ...AMENDABLE_INPUTS]) assert.ok(frozen.includes(`\`${file}\``), `the exceptions name ${file}`);
  assert.match(frozen, /through a dated `input:` line/, 'quiet.json and package-flags.json change only through a dated input: line of amendments.md');
  assert.match(frozen, /section 10/);
});

