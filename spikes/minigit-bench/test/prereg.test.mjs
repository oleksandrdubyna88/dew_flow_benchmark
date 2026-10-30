// The pre-registration's generated files (E3b.S3): quoted text is verbatim from a commit of origin/main, derived files equal their script's output, the seed is a hash only.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { PREREG_FILES } from '../lib/prereg-guard.mjs';
import { buildPrompts, extractPrompts } from '../prereg/extract-prompts.mjs';
import { QUOTES, MARKER, extractQuotes, keptTail, renderQuotes } from '../prereg/extract-contract.mjs';
import { extractPrediction, extractWeakenings, renderPrediction } from '../prereg/extract-prediction.mjs';
import { heldoutPrereg } from '../prereg/heldout-prereg.mjs';
import { unwrap } from '../prereg/blocks.mjs';
import { fullSha, readAtCommit } from '../prereg/parent.mjs';
import { keyGate, spikeDir, tmpDir } from './helpers.mjs';
import { spawnSync } from 'node:child_process';

const REPO = path.resolve(spikeDir, '..', '..');
const INPUTS = path.join(spikeDir, 'inputs');
const read = name => readFileSync(path.join(INPUTS, name), 'utf8');
const PARENT_COMMIT = '85c54b7';
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
