// The pre-registration's generated files (E3b.S3): quoted text is verbatim from a commit of origin/main, derived files equal their script's output, the seed is a hash only.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { PREREG_FILES } from '../lib/prereg-guard.mjs';
import { buildPrompts, extractPrompts } from '../prereg/extract-prompts.mjs';
import { QUOTES, MARKER, extractQuotes, keptTail, renderQuotes } from '../prereg/extract-contract.mjs';
import { extractPrediction, extractWeakenings, renderPrediction } from '../prereg/extract-prediction.mjs';
import { heldoutPrereg } from '../prereg/heldout-prereg.mjs';
import { spendPrior } from '../prereg/spend-prior.mjs';
import { unwrap } from '../prereg/blocks.mjs';
import { fullSha, readAtCommit } from '../prereg/parent.mjs';
import { keyGate, spikeDir, tmpDir } from './helpers.mjs';
import { spawnSync } from 'node:child_process';

const REPO = path.resolve(spikeDir, '..', '..');
const INPUTS = path.join(spikeDir, 'inputs');
const read = name => readFileSync(path.join(INPUTS, name), 'utf8');
// The source commit is the one the committed files NAME (all three must agree): regenerating from a later main commit changes the files and needs no change here.
const PARENT_COMMIT = /at `([0-9a-f]{40})`/.exec(readFileSync(path.join(INPUTS, 'prediction.md'), 'utf8'))[1];
const ELISION = /\[…[^\]]*…\]/g;

const parent = async () => {
  const r = await readAtCommit({ repo: REPO, commit: PARENT_COMMIT });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};

/** Every fragment of `text` that is not an elision marker: `[]` of the ones NOT found in `source` (unwrapped on both sides). */
const notFound = (text, source) => text.split(/\n\s*\n/).flatMap(item => item.split(ELISION)).map(f => unwrap(f.replace(/^\s*\d+\.\s+/, '').replace(/^[\s;.]+|[\s;]+$/g, ''))).filter(f => f.length > 3 && !unwrap(source).includes(f));

test('The_prediction_is_verbatim_from_a_main_commit', async () => {
  const text = await parent();
  const file = read('prediction.md');
  const header = /at `([0-9a-f]{40})`/.exec(file)?.[1];
  assert.ok(header, 'the header names the commit in full');
  assert.equal((await readAtCommit({ repo: REPO, commit: header })).ok, true, 'that commit is an ancestor of origin/main');
  assert.equal(header, (await fullSha({ repo: REPO, commit: PARENT_COMMIT })).value);
  const quoted = file.slice(file.indexOf('> *(The ranges'), file.indexOf('## What the bare-first deviation weakens')).replace(/^> /gm, '');
  assert.deepEqual(notFound(quoted, text), [], 'every non-elided fragment is in the parent at that commit');
  assert.ok(quoted.match(ELISION).length >= 2, 'the process clauses are elided and marked');
  assert.match(quoted, /1\. Bare cost: C# > TS/);
  assert.deepEqual(notFound(quoted.replace('median C#/TS 1.15–1.5×', 'median C#/TS 9.99×'), text).length, 1, 'companion: the check does see a changed word');
});

test('The_committed_prediction_is_what_the_script_writes', async () => {
  const p = extractPrediction(await parent());
  const w = extractWeakenings(readFileSync(path.join(REPO, 'todo', 'PLAN_minigit_stack_bench_E3_bare.md'), 'utf8'));
  assert.equal(p.ok && w.ok, true, p.reason ?? w.reason);
  assert.equal(read('prediction.md'), renderPrediction({ prediction: p.value, weakenings: w.value, commit: (await fullSha({ repo: REPO, commit: PARENT_COMMIT })).value }));
});

test('The_analysis_contract_quotes_are_verbatim_from_a_main_commit', async () => {
  const text = await parent();
  const file = read('analysis-contract.md');
  const header = /at `([0-9a-f]{40})`/.exec(file)?.[1];
  assert.equal(header, (await fullSha({ repo: REPO, commit: PARENT_COMMIT })).value);
  const blocks = [...file.matchAll(/<!-- quote: (.+?) \(parent section ([\d.]+)\) -->\n([\s\S]*?)\n<!-- \/quote -->/g)];
  assert.deepEqual(blocks.map(b => b[1]), QUOTES.map(q => q.name), 'every registered quote is there, in order');
  for (const [, name, , body] of blocks) assert.deepEqual(notFound(body.replace(/^> ?/gm, ''), text), [], `${name} is verbatim`);
  const altered = blocks[0][3].replace('the cell', 'the session');
  assert.equal(notFound(altered.replace(/^> ?/gm, ''), text).length, 1, 'companion: the check does see a changed word');
});

test('The_committed_quote_half_is_what_the_script_writes_and_the_tail_is_kept', async () => {
  const quotes = extractQuotes(await parent());
  assert.equal(quotes.ok, true, quotes.reason);
  const sha = (await fullSha({ repo: REPO, commit: PARENT_COMMIT })).value;
  const rendered = renderQuotes({ quotes: quotes.value, commit: sha });
  const file = read('analysis-contract.md');
  assert.ok(file.startsWith(rendered), 'the half above the marker is the script\'s output, nothing typed');
  assert.equal(keptTail(`${rendered}the operator\'s own words\n`), 'the operator\'s own words\n', 'what follows the marker survives a regeneration');
  assert.equal(keptTail('no marker here'), '');
  assert.ok(rendered.includes(MARKER));
  assert.match(rendered, /process-only/, 'delivered is marked process-only');
  assert.equal(extractQuotes('nothing here').ok, false);
});

// The registered extensions of parent §11.1 (E3b.S3): the three classes the plan's scenario names. Each must be a list item of the operationalizations that carries the
// marker and a reason, and the outcome table's † rows must be exactly those items: a class marked in one place and not the other is the defect this test catches.
const EXTENSION_MARKER = '**registered extension of §11.1.**';
const EXTENSIONS = ['stop: auth', 'void: harness (interrupted | disposed)', 'void: upstream'];

/** The operationalizations' extension items: `- \`<class>\` — **registered extension of §11.1.** Reason: <text>` (the item may wrap) -> `[{ cls, reason }]`. */
const extensionItems = tail => tail.split(/\n(?=- )/).flatMap(item => {
  const m = /^- `([^`]+)` — (.+)$/s.exec(item.split(/\n\s*\n/)[0]);
  if (!m || !m[2].startsWith(EXTENSION_MARKER)) return [];
  const reason = /Reason: (.+)$/s.exec(m[2])?.[1] ?? '';
  return [{ cls: m[1], reason: unwrap(reason) }];
});

/** The classes of the outcome table's rows marked †, with the table's `\|` unescaped. */
const daggerRows = tail => tail.split('\n').filter(l => /^\| \d+ \|/.test(l) && l.includes('†'))
  .flatMap(l => [...l.replaceAll('\\|', '|').matchAll(/`([^`]+)` †/g)].map(m => m[1]));

test('The_extensions_are_marked_as_registered', () => {
  const tail = keptTail(read('analysis-contract.md'));
  const items = extensionItems(tail);
  assert.deepEqual(items.map(i => i.cls).sort(), [...EXTENSIONS].sort(), 'each extension is an item under the registered-extension marker, and nothing else is');
  for (const { cls, reason } of items) assert.ok(reason.length >= 40, `${cls} carries a reason: '${reason}'`);
  assert.deepEqual([...daggerRows(tail)].sort(), [...EXTENSIONS].sort(), 'the outcome table marks exactly the registered extensions with †');
  // companions: the parser does see an item, and an item without the marker or without a reason is not counted as registered
  const known = `- \`void: upstream\` — ${EXTENSION_MARKER} Reason: the provider, not the stack, ended the session after the CLI's own retries.`;
  assert.deepEqual(extensionItems(known).map(i => i.cls), ['void: upstream']);
  assert.deepEqual(extensionItems(known.replace(EXTENSION_MARKER, 'an extension.')), []);
  assert.equal(extensionItems(known.replace(/Reason: .+$/, ''))[0].reason, '');
  assert.deepEqual(daggerRows('| 9 | a final 5xx | `void: upstream` † | no |'), ['void: upstream']);
});

test('The_heldout_prereg_is_derived_not_typed', () => {
  const derived = heldoutPrereg();
  assert.equal(derived.ok, true, derived.reason);
  assert.deepEqual(JSON.parse(read('heldout-prereg.json')), derived.value);
  assert.deepEqual(Object.keys(derived.value).sort(), ['archiveSha256', 'calibration', 'm', 'n1', 'n2', 'note', 'plaintextSha256', 'sealedOn'], 'counts and hashes only');
});

test('The_registered_prompts_are_what_the_script_derives_from_the_parent', async () => {
  const extracted = extractPrompts(await parent());
  assert.equal(extracted.ok, true, extracted.reason);
  const sha = (await fullSha({ repo: REPO, commit: PARENT_COMMIT })).value;
  assert.deepEqual(JSON.parse(read('prompts-bare.json')), buildPrompts({ extracted: extracted.value, commit: sha }));
  assert.equal(extractPrompts('no prompts').ok, false);
});

test('The_seed_is_committed_as_a_hash_only', () => {
  const inputsFiles = dir => readdirSync(dir, { withFileTypes: true }).flatMap(e => (e.isDirectory() ? inputsFiles(path.join(dir, e.name)) : [path.join(dir, e.name)]));
  const files = inputsFiles(INPUTS);
  assert.equal(files.some(f => path.basename(f) === 'seed.txt'), false, 'the seed is revealed AFTER the tag');
  const seeds = files.filter(f => /mg-[0-9a-f]{16}/.test(readFileSync(f, 'utf8')));
  assert.deepEqual(seeds.map(f => path.basename(f)), [], 'no seed-shaped string in any registered input');
  const hash = files.find(f => path.basename(f) === 'seed.sha256');
  if (hash) assert.match(readFileSync(hash, 'utf8').trim(), /^[0-9a-f]{64}$/);
  // companion: the scan does see a seed in a file
  const dir = tmpDir('inputs');
  const planted = path.join(dir, 'x.md');
  spawnSync('sh', ['-c', `printf 'seed mg-0123456789abcdef' > ${planted}`]);
  assert.equal(inputsFiles(dir).filter(f => /mg-[0-9a-f]{16}/.test(readFileSync(f, 'utf8'))).length, 1);
});

test('The_guard_list_is_the_committed_list', () => {
  const plan = readFileSync(path.join(REPO, 'todo', 'PLAN_minigit_stack_bench_E3_bare.md'), 'utf8');
  const table = plan.slice(plan.indexOf('| File | Content |'), plan.indexOf('**`prediction.md`, the text**'));
  const names = table.split('\n').slice(2).filter(l => l.startsWith('|')).flatMap(l => [...l.split('|')[1].matchAll(/`([^`]+)`/g)].map(m => m[1]));
  assert.ok(names.length >= 10, 'the scan still finds the table rows');
  assert.deepEqual([...names].sort(), [...PREREG_FILES].sort());
});

test('The_prereg_names_no_heldout_case', t => {
  if (!keyGate(t)) return;
  const r = spawnSync(process.execPath, [path.join(spikeDir, 'heldout-tool.mjs'), 'leakcheck', '--range', 'origin/main..HEAD'], { cwd: REPO, encoding: 'utf8', timeout: 300_000 });
  assert.equal(r.status, 0, `leakcheck exit ${r.status}: ${r.stdout.slice(-300)}`);
});

test('The_prior_spend_is_read_from_its_evidence_and_counts_every_paid_instrument_session', () => {
  const derived = spendPrior();
  assert.equal(derived.ok, true, derived.reason);
  assert.deepEqual(JSON.parse(read('spend-prior.json')), derived.value);
  assert.equal(derived.value.length, 7, 'capture, design run, broker proof, the first smoke, the three re-smokes counted once each (the committed fixture is the last)');
  const total = derived.value.reduce((a, e) => a + e.usd, 0);
  assert.ok(total > 0.8 && total < 0.85, `the instrument work before E3b: ${total}`);
  for (const e of derived.value) {
    assert.match(e.date, /^\d{4}-\d{2}-\d{2}$/);
    assert.ok(e.usd > 0 && e.what && e.evidence, JSON.stringify(e));
    assert.equal(existsSync(path.join(REPO, e.evidence.split(' (')[0])), true, `${e.evidence} exists`);
  }
});

test('The_three_quoting_files_name_the_same_main_commit', () => {
  const named = {
    prediction: /at `([0-9a-f]{40})`/.exec(read('prediction.md'))?.[1], contract: /at `([0-9a-f]{40})`/.exec(read('analysis-contract.md'))?.[1], prompts: JSON.parse(read('prompts-bare.json')).source.commit,
  };
  assert.ok(named.prediction && named.contract && named.prompts, JSON.stringify(named));
  assert.equal(new Set(Object.values(named)).size, 1, `one commit for every quote: ${JSON.stringify(named)}`);
});
