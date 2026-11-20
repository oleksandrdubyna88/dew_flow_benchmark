// F3.S1 (b), PR 5: the pre-registration's tools that touch the disk, the network or the home (scenarios 65, 65a, 62 for the manifest, 78 and 79 of the stories document): the seed verbs, the manifest, the price read and the CLI pin.
// Everything runs in temporary folders with a temporary home; no test reaches the network, and the seed file of the real home is never touched.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { loadPrices } from '../../minigit-bench/lib/prices.mjs';
import { buildMaterial } from '../lib/leak-material.mjs';
import { scanFiles } from '../lib/leak-scan.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { REPO } from '../lib/paths.mjs';
import { PREREG_DIR, STAGE_PREREG_FILES } from '../lib/stage-lists.mjs';
import { cmdSeedCommit, cmdSeedReveal } from '../lib/stage-prereg-commands.mjs';
import { STAGE_SEED_SHAPE, commitStageSeed, requiredFiles, revealStageSeed, stageSeedFile } from '../lib/stage-seed.mjs';
import { seedProblem } from '../lib/stage-schedule.mjs';
import { IDENTITY_NAMES, identityProblems } from '../lib/stage-identity.mjs';
import * as manifest from '../prereg/write-manifest.mjs';
import { pilotPlan } from '../lib/stage-schedule.mjs';
import { STAGE_PILOT } from '../lib/stage-lists.mjs';
import { buildManifest, diskProjection, identityTexts, largestSessionBytes, main as manifestMain } from '../prereg/write-manifest.mjs';
import * as readPrices from '../prereg/read-prices.mjs';
import { PRICE_HEADER, PRICING_URL, buildPrices, displayNameOf, main as pricesMain, parsePricingPage } from '../prereg/read-prices.mjs';
import * as pinCli from '../prereg/pin-cli.mjs';
import { cliBlockText, cliPinProblems, main as pinMain, pinPlan, withCli } from '../prereg/pin-cli.mjs';

const run = async (main, argv, ...rest) => { const lines = []; const code = await main(argv, line => lines.push(line), ...rest); return { code, lines }; };
const sha = text => createHash('sha256').update(text).digest('hex');
const mode = file => statSync(file).mode & 0o777;

/** A repository root with every registered file but the seed's own two (a folder entry with one file), and a home. */
function treeFixture() {
  const root = tmpDir('seed-root');
  const home = tmpDir('seed-home');
  const samples = { [PREREG_DIR]: 'prices-2026-11-19.json', 'spikes/lang-bench/inputs/specs.d': 'runner.json' };
  for (const entry of STAGE_PREREG_FILES) {
    const rel = typeof entry === 'string' ? entry : `${entry.dir}/${samples[entry.dir]}`;
    if (rel.endsWith('seed.sha256')) continue;
    mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    writeFileSync(path.join(root, rel), `registered ${rel}\n`);
  }
  return { root, home };
}
const hashFile = root => path.join(root, PREREG_DIR, 'seed.sha256');
const everyFileUnder = dir => readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => path.join(e.parentPath, e.name));

/** A 16-hex suffix whose seed the schedule refuses (two of its 120 rids are equal), found by search: about one in 2300 seeds. */
function collidingSuffix() {
  for (let i = 0; i < 200_000; i += 1) {
    const suffix = i.toString(16).padStart(16, '0');
    if (seedProblem(`lv2-${suffix}`) !== null) return suffix;
  }
  throw new Error('no colliding seed found in 200000 tries');
}

// ---- 65, 65a: the seed -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_seed_is_in_the_tree_as_a_hash_only_until_it_is_revealed_by_the_seed_verbs', async () => {
  const f = treeFixture();
  assert.equal(stageSeedFile(f.home), path.join(f.home, 'rsd', 'lang-v2', 'seeds', 'stage1.seed'));
  const lines = [];
  assert.equal(cmdSeedCommit({}, { home: f.home, repo: f.root, say: l => lines.push(l) }), 0, lines.join('|'));
  const seed = readFileSync(stageSeedFile(f.home), 'utf8').trim();
  assert.match(seed, STAGE_SEED_SHAPE.regex);
  assert.equal(mode(stageSeedFile(f.home)), 0o600);
  assert.equal(readFileSync(hashFile(f.root), 'utf8').trim(), sha(seed), 'the tree holds the hash');
  assert.equal(existsSync(path.join(f.root, PREREG_DIR, 'seed.txt')), false, 'and no seed');
  assert.ok(everyFileUnder(f.root).every(file => !readFileSync(file, 'utf8').includes(seed)), 'the seed is in no file of the tree');
  assert.ok(!lines.join('\n').includes(seed), 'the verb never prints the seed');
  assert.equal(cmdSeedCommit({}, { home: f.home, repo: f.root, say: () => {} }), 4, 'committed once');
  const revealed = [];
  assert.equal(cmdSeedReveal({}, { home: f.home, repo: f.root, say: l => revealed.push(l) }), 0, revealed.join('|'));
  assert.equal(readFileSync(path.join(f.root, PREREG_DIR, 'seed.txt'), 'utf8').trim(), seed);
  assert.ok(!revealed.join('\n').includes(seed));
  assert.equal(cmdSeedReveal({}, { home: f.home, repo: f.root, say: () => {} }), 4, 'revealed once');
});

test('The_real_draw_path_refuses_a_colliding_seed_and_draws_another_before_anything_is_committed', () => {
  const bad = collidingSuffix();
  const good = 'abcdef0123456789';
  assert.notEqual(seedProblem(`lv2-${bad}`), null);
  assert.equal(seedProblem(`lv2-${good}`), null);
  const f = treeFixture();
  const drawn = [];
  const random = () => { drawn.push(drawn.length === 0 ? bad : good); return drawn.at(-1); };
  const done = commitStageSeed({ root: f.root, home: f.home, random });
  assert.equal(done.ok, true, done.reason);
  assert.deepEqual(drawn, [bad, good], 'the colliding seed was drawn first and the next draw is the one kept');
  assert.equal(readFileSync(stageSeedFile(f.home), 'utf8'), `lv2-${good}\n`);
  assert.equal(readFileSync(hashFile(f.root), 'utf8').trim(), sha(`lv2-${good}`));
  const text = everyFileUnder(f.home).concat(everyFileUnder(f.root)).map(file => readFileSync(file, 'utf8')).join('\n');
  assert.ok(!text.includes(bad), 'nothing of the first seed was ever written');
});

test('The_seed_is_committed_last_after_every_registered_file_and_the_reveal_refuses_a_seed_that_does_not_hash', () => {
  const f = treeFixture();
  const planned = requiredFiles(f.root);
  assert.equal(planned.length, STAGE_PREREG_FILES.length - 1, 'every registered file but the seed hash');
  assert.ok(!planned.some(p => p.endsWith('seed.sha256') || p.endsWith('seed.txt')));
  git_rm(path.join(f.root, PREREG_DIR, 'statement.md'));
  assert.match(commitStageSeed({ root: f.root, home: f.home }).reason, /statement\.md does not exist: the seed is committed last, with every other registered file in place/);
  assert.equal(existsSync(stageSeedFile(f.home)), false, 'nothing was drawn');
  const g = treeFixture();
  rmDir(path.join(g.root, 'spikes/lang-bench/inputs/specs.d'));
  assert.match(commitStageSeed({ root: g.root, home: g.home }).reason, /specs\.d\/<the spec files of specs\.d> does not exist/);
  const h = treeFixture();
  assert.equal(commitStageSeed({ root: h.root, home: h.home }).ok, true);
  writeFileSync(stageSeedFile(h.home), 'lv2-ffffffffffffffff\n');
  assert.match(revealStageSeed({ root: h.root, home: h.home }).reason, /does not hash to the committed seed\.sha256/);
  writeFileSync(stageSeedFile(h.home), 'mg-0123456789abcdef\n');
  assert.match(revealStageSeed({ root: h.root, home: h.home }).reason, /is not lv2- and 16 hex/);
});
test('A_seed_commit_that_was_stopped_between_its_two_writes_is_named_by_the_verb_and_a_second_reveal_says_already_revealed', () => {
  const f = treeFixture();
  const lines = [];
  const io = { home: f.home, repo: f.root, say: l => lines.push(l) };
  assert.equal(cmdSeedCommit({}, io), 0, lines.join('|'));
  const seed = readFileSync(stageSeedFile(f.home), 'utf8');
  rmSync(hashFile(f.root)); // the state of a kill between the two writes
  lines.length = 0;
  assert.equal(cmdSeedCommit({}, io), 4);
  assert.match(lines[0], /that seed was never committed and is not reused; the private seed file at .*stage1\.seed can be removed by the operator, then run seed-commit again/);
  assert.equal(readFileSync(stageSeedFile(f.home), 'utf8'), seed, 'the real home\'s seed file is kept');
  assert.equal(cmdSeedReveal({}, io), 4, 'and a seed that was never committed is not revealed');
  rmSync(stageSeedFile(f.home));
  assert.equal(cmdSeedCommit({}, io), 0, 'the operator removed the file: the retry commits a new seed');
  assert.equal(cmdSeedReveal({}, io), 0);
  lines.length = 0;
  assert.equal(cmdSeedReveal({}, io), 4);
  assert.match(lines[0], /^already revealed: .*seed\.txt holds the committed seed; nothing was written$/);
});

const git_rm = file => rmSync(file);
const rmDir = dir => rmSync(dir, { recursive: true });

// ---- 62: the manifest -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** One text for each of PR 4's identity names (what `identityTexts` gives from a clean read). */
const namesFixture = (over = {}) => ({ ...Object.fromEntries(IDENTITY_NAMES.map(name => [name, sha(name)])), ...over });
/** What `liveIdentities()` answers for every name: a value each. */
const liveFixture = (over = {}) => ({ ...Object.fromEntries(Object.entries(namesFixture()).map(([name, value]) => [name, { value }])), ...over });

/** A series folder of the explore root: its state (a quick one-task series has none of `stage`) and one kept session folder of `size` bytes. */
function seriesFolder(root, name, { stage, rid, size }) {
  mkdirSync(path.join(root, name, 's', rid, 'box'), { recursive: true });
  writeFileSync(path.join(root, name, 's', rid, 'box', 'f'), Buffer.alloc(size));
  writeFileSync(path.join(root, name, 'explore-series.json'), `${JSON.stringify({ seriesId: name, ...(stage === undefined ? { task: '01' } : { stage }) })}\n`);
}

test('The_manifest_is_written_again_to_the_same_bytes_from_the_identities_the_floor_and_the_growth_file', async () => {
  const root = tmpDir('quick-series');
  for (const [series, rid, size] of [['x-20261105-aaaa', 'a1', 100], ['x-20261105-aaaa', 'b2', 700], ['x-20261106-bbbb', 'c3', 300]]) seriesFolder(root, series, { rid, size });
  mkdirSync(path.join(root, 'not-a-series', 's', 'd4', 'box'), { recursive: true });
  writeFileSync(path.join(root, 'not-a-series', 's', 'd4', 'box', 'f'), Buffer.alloc(9000));
  assert.deepEqual(largestSessionBytes(root), { ok: true, value: 700 }, 'the largest kept session folder of the quick run\'s series; a folder that is no series is not counted');
  assert.match(largestSessionBytes(tmpDir('none')).reason, /no kept session folder under/);
  const dir = tmpDir('manifest-out');
  const growthFile = path.join(dir, 'growth.json');
  writeFileSync(growthFile, '{"pairWorstCaseBytes":117395352}\n');
  const out = path.join(dir, 'manifest.json');
  const seams = { liveIdentities: async () => liveFixture(), root, growthFile };
  assert.equal((await run(manifestMain, ['--out', out], seams)).code, 0);
  const first = readFileSync(out, 'utf8');
  const parsed = JSON.parse(first);
  assert.deepEqual(Object.keys(parsed.identities), [...IDENTITY_NAMES].sort(), 'PR 4\'s names, keys in code-point order');
  assert.equal(Object.keys(parsed.identities).length, 41, 'the 41 names of the guard');
  assert.equal(parsed.identities.harness, sha('harness'), 'the text each reader gave, not the reader\'s wrapper');
  assert.equal(parsed.sessionFloorBytes, 700, 'the floor is measured from the quick run\'s series');
  assert.deepEqual(parsed.sessionFloorSeries, ['x-20261105-aaaa', 'x-20261106-bbbb'], 'and the series it was measured over are named');
  assert.equal(parsed.growth.pairWorstCaseBytes, 117395352);
  const reordered = Object.fromEntries(Object.entries(liveFixture()).reverse());
  assert.equal((await run(manifestMain, ['--out', out], { ...seams, liveIdentities: async () => reordered })).code, 0);
  assert.equal(readFileSync(out, 'utf8'), first, 'the same bytes whatever order the identities were read in');
  assert.equal((await run(manifestMain, ['--out', out, '--check'], seams)).code, 0);
  seriesFolder(root, 'x-20261110-cccc', { stage: 1, rid: 'e5', size: 5000 }); // a Stage 1 series of its own appears: it never counts
  assert.equal((await run(manifestMain, ['--out', out, '--check'], seams)).code, 0, 'the check is stable once Stage 1 sessions exist');
  assert.equal((await run(manifestMain, ['--out', out], seams)).code, 0);
  assert.equal(readFileSync(out, 'utf8'), first, 'the same bytes: a Stage 1 series folder does not change the output');
  writeFileSync(out, first.replace('"sessionFloorBytes": 700', '"sessionFloorBytes": 701'));
  assert.equal((await run(manifestMain, ['--out', out, '--check'], seams)).code, 1, 'a typed value fails the check');
  assert.equal((await run(manifestMain, ['--out', out, '--floor-bytes', '700'], seams)).code, 4, 'the floor is measured and no flag types it');
});

test('The_pilots_sessions_and_the_repeat_pair_come_from_the_pilot_plan_and_not_from_typed_numbers', () => {
  const plan = pilotPlan({ pilot: STAGE_PILOT }).value;
  assert.equal(manifest.PILOT_SESSIONS, plan.aa.sessions.length + plan.pairs.reduce((n, pair) => n + pair.sessions.length, 0), 'the A/A\'s twins and the two pairs\' sessions');
  assert.equal(manifest.REPEAT_PAIR, plan.pairs[0].sessions.length, 'a repeat pair is one stack-pair of sessions');
});

test('The_disk_projection_counts_the_66_sessions_every_one_re_run_one_re_run_and_a_repeat_pair', () => {
  assert.deepEqual(diskProjection({ floorBytes: 10 }), {
    floorBytes: 10, dataSessions: 60, pilotSessions: 6, series: { sessions: 66, bytes: 660 }, withEveryRerun: { sessions: 132, bytes: 1320 }, oneRerun: { sessions: 1, bytes: 10 }, repeatPilotPair: { sessions: 2, bytes: 20 },
  });
  for (const bad of [0, -1, 1.5, NaN, undefined, '7']) assert.match(buildManifest({ identities: namesFixture(), floorBytes: bad, growth: {} }).reason, /the floor of a session folder's size is not a positive whole number/, String(bad));
  for (const bad of [null, undefined]) assert.match(buildManifest({ identities: bad, floorBytes: 1, growth: {} }).reason, /liveIdentities\(\) gave no identities/);
});

test('The_manifest_holds_exactly_the_guards_names_and_the_guard_names_an_extra_one_and_a_missing_one', () => {
  const growth = { pairWorstCaseBytes: 1 };
  const [first, ...rest] = IDENTITY_NAMES;
  const without = Object.fromEntries(Object.entries(namesFixture()).filter(([name]) => name !== first));
  const missing = buildManifest({ identities: without, floorBytes: 5, growth });
  assert.match(missing.reason, new RegExp(`not exactly the guard's 41 names \\(missing: ${first}; not in the guard's list: none\\)`));
  const extra = buildManifest({ identities: namesFixture({ invented: 'f'.repeat(64) }), floorBytes: 5, growth });
  assert.match(extra.reason, /not exactly the guard's 41 names \(missing: none; not in the guard's list: invented\)/);
  const both = buildManifest({ identities: { ...without, invented: '1' }, floorBytes: 5, growth });
  assert.match(both.reason, new RegExp(`missing: ${first}; not in the guard's list: invented`));
  const built = buildManifest({ identities: namesFixture(), floorBytes: 5, growth });
  assert.equal(built.ok, true, built.reason);
  assert.deepEqual(identityProblems(built.value, liveFixture()), [], 'the guard reads the manifest as written: every identity equals');
  assert.deepEqual(identityProblems({ identities: { ...built.value.identities, invented: '1' } }, liveFixture()), ["invented is in the manifest but not in the guard's list of identities: the guard never reads it"], 'the guard names an extra name');
  const lacking = identityProblems({ identities: Object.fromEntries(Object.entries(built.value.identities).filter(([name]) => name !== rest[0])) }, liveFixture());
  assert.match(lacking[0], new RegExp(`^${rest[0]} is not in the manifest`), 'and a missing one');
});

test('An_identity_that_cannot_be_read_is_never_frozen_as_text_and_the_manifest_names_each', () => {
  assert.deepEqual(identityTexts(liveFixture()).value, namesFixture());
  const refused = identityTexts(liveFixture({ harness: { unknown: 'git cannot read the tree' }, host: { unknown: 'no cgroup' } }));
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /2 identity\(ies\) cannot be read, so the manifest would freeze less than the guard compares: harness \(git cannot read the tree\); host \(no cgroup\)/);
});

// ---- the price read ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const HEADER = `| ${PRICE_HEADER.join(' | ')} |`;
/** An explore.json that pins the CLI the price fixtures are read for (its `cli.version`: the one source of the version). */
const PRICE_EXPLORE = path.join(tmpDir('prices-explore'), 'explore.json');
writeFileSync(PRICE_EXPLORE, '{"cli":{"version":"2.1.310"}}\n');
const PAGE = [
  '# Pricing', '', HEADER, '|---|---|---|---|---|---|',
  '| Claude Opus 5.5 | $4 / MTok | $5 / MTok | $8 / MTok | $0.20 / MTok | $20 / MTok |', '| [Claude Sonnet 5.5](/docs/x) | $2 / MTok | $2.50 / MTok | $4 / MTok | $0.10 / MTok | $10 / MTok |',
  '| Claude Haiku 4.5 | $1 / MTok | $1.25 / MTok | $2 / MTok | $0.10 / MTok | $5 / MTok |', '| Claude Old 1 (deprecated) | $9 / MTok | n/a | n/a | n/a | n/a |', '',
].join('\n');
const BASE = { models: [{ modelId: 'claude-opus-5-5' }, { modelId: 'claude-haiku-4-5-20251001' }, { modelId: 'claude-sonnet-5-5' }], cliRates: [
  { modelId: 'claude-sonnet-5-5', cliVersion: '2.1.310', cacheRead: 0.1, observed: 'seen' }, { modelId: 'claude-sonnet-5-5', cliVersion: '2.1.289', cacheRead: 0.2, observed: 'older' }] };
const parsed = text => parsePricingPage(text).value;

test('A_model_id_is_named_the_way_the_pricing_page_names_it_and_the_page_is_read_by_its_exact_header', () => {
  for (const [id, name] of [['claude-opus-5-5', 'Claude Opus 5.5'], ['claude-haiku-4-5-20251001', 'Claude Haiku 4.5'], ['claude-fable-5-1', 'Claude Fable 5.1'], ['claude-opus-4-8', 'Claude Opus 4.8']]) assert.equal(displayNameOf(id), name);
  const page = parsed(PAGE);
  assert.deepEqual([...page.prices.keys()], ['Claude Opus 5.5', 'Claude Sonnet 5.5', 'Claude Haiku 4.5'], 'a link and an emphasis are stripped; a row with no dollar amounts is not a priced row');
  assert.deepEqual(page.prices.get('Claude Opus 5.5'), { input: 4, cacheWrite5m: 5, cacheWrite1h: 8, cacheRead: 0.2, output: 20 });
  assert.deepEqual([...page.unreadable], [['Claude Old 1', "Cache Writes is 'n/a', not a price in the form $5 / MTok".replace('Cache Writes', '5m Cache Writes')]], 'the row it could not read is kept with the cell that is not a price');
});

test('A_page_without_the_exact_header_row_is_refused_naming_the_headers_it_found_and_nothing_is_guessed', () => {
  const none = parsePricingPage('no table here');
  assert.equal(none.ok, false);
  assert.match(none.reason, /^the pricing page has no table with the exact header row \| Model \| Base Input Tokens \| 5m Cache Writes \| 1h Cache Writes \| Cache Hits & Refreshes \| Output Tokens \|: it holds no table$/);
  const variants = [['lower case', HEADER.toLowerCase()], ['a column renamed', HEADER.replace('Output Tokens', 'Output')], ['a column missing', '| Model | Base Input Tokens | 5m Cache Writes | 1h Cache Writes | Output Tokens |'], ['a column added', HEADER.replace(' Output Tokens |', ' Output Tokens | Batch |')], ['the columns reordered', '| Model | 5m Cache Writes | Base Input Tokens | 1h Cache Writes | Cache Hits & Refreshes | Output Tokens |']];
  for (const [what, header] of variants) {
    const refused = parsePricingPage(PAGE.replace(HEADER, header).replace('|---|---|---|---|---|---|', header.replace(/[^|]+/g, '---')));
    assert.equal(refused.ok, false, what);
    assert.ok(refused.reason.includes(`it holds the tables ${header}`), `${what}: ${refused.reason}`);
  }
  const noSeparator = parsePricingPage(PAGE.replace('|---|---|---|---|---|---|\n', ''));
  assert.equal(noSeparator.ok, false, 'a header with no separator row under it is no table');
});

test('A_price_cell_is_read_only_in_the_form_dollars_per_MTok_and_a_name_given_twice_with_different_content_is_refused', () => {
  const page = parsed(PAGE.replace('$4 / MTok', '4 USD').replace('| Claude Haiku 4.5 | $1 / MTok', '| Claude Haiku 4.5 | $1.5 per MTok'));
  assert.deepEqual([...page.prices.keys()], ['Claude Sonnet 5.5'], 'a cell in another form leaves its row unread');
  assert.match(page.unreadable.get('Claude Opus 5.5'), /^Base Input Tokens is '4 USD', not a price in the form \$5 \/ MTok$/);
  const refused = buildPrices({ base: BASE, page, day: 'x', cliVersion: '2.1.310' });
  assert.match(refused.reason, /the pricing page cannot price the series' models: claude-opus-5-5 \(Claude Opus 5\.5\): its row cannot be read: Base Input Tokens is '4 USD'.*claude-haiku-4-5-20251001 \(Claude Haiku 4\.5\): its row cannot be read: Base Input Tokens is '\$1\.5 per MTok'.*: a price is never carried over from the older file or guessed/);
  const twice = parsePricingPage(`${PAGE}\n${HEADER}\n|---|---|---|---|---|---|\n| Claude Opus 5.5 | $5 / MTok | $5 / MTok | $8 / MTok | $0.20 / MTok | $20 / MTok |\n`);
  assert.match(twice.reason, /the pricing page gives Claude Opus 5\.5 twice with different content: a price is never chosen between two/);
  const same = parsePricingPage(`${PAGE}\n${HEADER}\n|---|---|---|---|---|---|\n| Claude Opus 5.5 | $4 / MTok | $5 / MTok | $8 / MTok | $0.20 / MTok | $20 / MTok |\n`);
  assert.equal(same.ok, true, 'the same row twice is one row');
  assert.match(parsed(`${HEADER}\n|---|---|---|---|---|---|\n| Claude Opus 5.5 | $4 / MTok |\n`).unreadable.get('Claude Opus 5.5'), /^2 cells, not 6$/);
});

test('The_price_file_has_the_base_files_models_each_from_its_own_row_and_never_an_older_price', () => {
  const built = buildPrices({ base: BASE, page: parsed(PAGE), day: '2026-11-19', cliVersion: '2.1.310' });
  assert.equal(built.ok, true, built.reason);
  assert.deepEqual(built.value.models.map(m => m.modelId), ['claude-opus-5-5', 'claude-haiku-4-5-20251001', 'claude-sonnet-5-5']);
  assert.deepEqual(built.value.models[0], { modelId: 'claude-opus-5-5', input: 4, output: 20, cacheRead: 0.2, cacheWrite5m: 5, cacheWrite1h: 8 });
  assert.deepEqual(built.value.cliRates.map(c => c.cliVersion), ['2.1.310'], 'a CLI rate only where it was observed on the pinned version');
  assert.equal(built.value.readOn, '2026-11-19');
  assert.equal(built.value.source, PRICING_URL);
  assert.equal(loadPrices(built.value).ok, true, 'v1\'s own loader accepts it');
  const gone = buildPrices({ base: { ...BASE, models: [...BASE.models, { modelId: 'claude-fable-5-1' }] }, page: parsed(PAGE), day: '2026-11-19', cliVersion: '2.1.310' });
  assert.match(gone.reason, /the pricing page cannot price the series' models: claude-fable-5-1 \(Claude Fable 5\.1\): no row \(the rows it found: Claude Opus 5\.5, Claude Sonnet 5\.5, Claude Haiku 4\.5, Claude Old 1\): a price is never carried over from the older file or guessed/);
  assert.equal(buildPrices({ base: { ...BASE, models: [] }, page: parsed(PAGE), day: 'x', cliVersion: '2.1.310' }).ok, false, 'a table prices at least one model');
});

test('The_price_read_makes_one_request_to_the_pricing_page_and_none_offline_and_names_its_day', async () => {
  const dir = tmpDir('prices-out');
  const base = path.join(dir, 'base.json');
  writeFileSync(base, JSON.stringify(BASE));
  const calls = [];
  const fetchFn = async (url, options) => { calls.push([url, options?.headers?.accept]); return { ok: true, status: 200, text: async () => PAGE }; };
  const out = path.join(dir, 'prices-2026-11-19.json');
  const ran = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn, clock: () => new Date('2026-11-19T10:00:00Z') });
  assert.equal(ran.code, 0, ran.lines.join('|'));
  assert.deepEqual(calls, [[PRICING_URL, 'text/markdown, text/html']], 'exactly one GET, of the pricing page');
  assert.equal(JSON.parse(readFileSync(out, 'utf8')).readOn, '2026-11-19', 'the day is the clock\'s UTC day');
  const saved = path.join(dir, 'page.md');
  writeFileSync(saved, PAGE);
  const offline = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--from-file', saved, '--out', path.join(dir, 'o.json')], { fetchFn: async () => { throw new Error('the network was used'); }, clock: () => new Date('2026-11-20T10:00:00Z') });
  assert.equal(offline.code, 0, offline.lines.join('|'));
  assert.equal(JSON.parse(readFileSync(path.join(dir, 'o.json'), 'utf8')).readOn, '2026-11-20');
  const checked = await run(pricesMain, ['--check', '--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn: async () => { throw new Error('the network was used'); } });
  assert.equal(checked.code, 0, `the committed file is valid and in the form this tool writes: no network: ${checked.lines.join('|')}`);
  writeFileSync(out, readFileSync(out, 'utf8').replace(/\n$/, ' \n'));
  assert.equal((await run(pricesMain, ['--check', '--explore', PRICE_EXPLORE, '--base', base, '--out', out], {})).code, 1);
  writeFileSync(out, '{"source":"x"}');
  assert.match((await run(pricesMain, ['--check', '--explore', PRICE_EXPLORE, '--base', base, '--out', out], {})).lines.join('|'), /is refused/);
  assert.equal((await run(pricesMain, ['--check', '--explore', PRICE_EXPLORE, '--base', base, '--out', path.join(dir, 'absent.json')], {})).code, 1);
  assert.equal((await run(pricesMain, ['--out', out, '--explore', '/no/such/explore.json'], {})).code, 5, 'an explore.json that cannot be read is a refusal');
  const down = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn: async () => ({ ok: false, status: 503, text: async () => '' }) });
  assert.deepEqual([down.code, down.lines[0]], [5, 'the pricing page answered 503']);
  const broken = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn: async () => { throw new Error('offline'); } });
  assert.deepEqual([broken.code, broken.lines[0]], [5, 'the pricing page cannot be read: offline']);
});

test('A_pricing_page_that_changed_its_header_writes_no_file_and_exits_5_naming_what_it_found', async () => {
  const dir = tmpDir('prices-changed');
  const base = path.join(dir, 'base.json');
  writeFileSync(base, JSON.stringify(BASE));
  const out = path.join(dir, 'prices-2026-11-19.json');
  const changed = PAGE.replace(HEADER, HEADER.replace('Output Tokens', 'Output Tokens (per million)'));
  const fetchFn = async () => ({ ok: true, status: 200, text: async () => changed });
  const ran = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn, clock: () => new Date('2026-11-19T10:00:00Z') });
  assert.equal(ran.code, 5);
  assert.match(ran.lines.join('|'), /^the pricing page has no table with the exact header row .*: it holds the tables \| Model \| Base Input Tokens .* \| Output Tokens \(per million\) \|$/);
  assert.equal(existsSync(out), false, 'no file was written');
  const dayless = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--out', out, '--from-file', path.join(dir, 'absent.md')], {});
  assert.equal(dayless.code, 5, 'a saved page that is not there is a refusal, not a throw');
});

// ---- the price read's named scenarios and its timeout (coai plan round, PR 5, finding P) ----------------------------------------------------------------------------------------------------------

const priceRun = async (fetchFn, seams = {}) => {
  const dir = tmpDir('prices-named');
  const base = path.join(dir, 'base.json');
  writeFileSync(base, JSON.stringify(BASE));
  const out = path.join(dir, 'prices-2026-11-19.json');
  const ran = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn, clock: () => new Date('2026-11-19T10:00:00Z'), ...seams });
  return { ...ran, out };
};
const answering = text => async () => ({ ok: true, status: 200, text: async () => text });

test('A_valid_price_table_is_parsed_into_one_price_file_with_each_models_own_row', async () => {
  const ran = await priceRun(answering(PAGE));
  assert.equal(ran.code, 0, ran.lines.join('|'));
  assert.deepEqual(JSON.parse(readFileSync(ran.out, 'utf8')).models.map(m => [m.modelId, m.input, m.output]), [['claude-opus-5-5', 4, 20], ['claude-haiku-4-5-20251001', 1, 5], ['claude-sonnet-5-5', 2, 10]]);
});

test('A_header_that_is_not_the_exact_one_is_refused_and_no_file_is_written', async () => {
  const ran = await priceRun(answering(PAGE.replace('Cache Hits & Refreshes', 'Cache Hits')));
  assert.equal(ran.code, 5);
  assert.match(ran.lines[0], /^the pricing page has no table with the exact header row/);
  assert.equal(existsSync(ran.out), false);
});

test('A_model_of_the_base_file_with_no_row_on_the_page_is_refused_and_named', async () => {
  const ran = await priceRun(answering(PAGE.replace('| Claude Haiku 4.5 |', '| Claude Haiku 9 |')));
  assert.equal(ran.code, 5);
  assert.match(ran.lines[0], /claude-haiku-4-5-20251001 \(Claude Haiku 4\.5\): no row/);
  assert.equal(existsSync(ran.out), false);
});

test('A_response_that_is_not_200_is_refused_with_its_status_and_no_file_is_written', async () => {
  const ran = await priceRun(async () => ({ ok: false, status: 404, text: async () => 'not found' }));
  assert.deepEqual([ran.code, ran.lines[0], existsSync(ran.out)], [5, 'the pricing page answered 404', false]);
});

test('A_fetch_that_never_answers_ends_by_its_timeout_as_a_refusal_that_names_it_and_the_request_carries_the_signal', async () => {
  const signals = [];
  const never = (url, options) => new Promise((resolve, reject) => { signals.push(options.signal); options.signal.addEventListener('abort', () => reject(options.signal.reason)); });
  const ran = await priceRun(never, { timeoutMs: 20 });
  assert.equal(ran.code, 5);
  assert.equal(ran.lines[0], `the pricing page did not answer within 20 ms: the read timed out (${PRICING_URL})`);
  assert.equal(existsSync(ran.out), false, 'no file is written');
  assert.equal(signals.length, 1);
  assert.equal(readPrices.FETCH_TIMEOUT_MS, 30_000, 'the real read waits 30 s at most');
});

// ---- the page the read fetched is a snapshot, and --check reads the snapshot (coai plan round 2, PR 5, finding R) ------------------------------------------------------------------------------------

const SNAPSHOT_CLOCK = () => new Date('2026-11-19T10:00:00Z');
const never = async () => { throw new Error('the network was used'); };

/** A read of PAGE into a temporary folder: `{ dir, base, out, snapshot, ran }`. */
async function readFixture(page = PAGE, headers = undefined) {
  const dir = tmpDir('prices-snapshot');
  const base = path.join(dir, 'base.json');
  writeFileSync(base, JSON.stringify(BASE));
  const out = path.join(dir, 'prices-2026-11-19.json');
  const fetchFn = async () => ({ ok: true, status: 200, headers, text: async () => page });
  const ran = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn, clock: SNAPSHOT_CLOCK });
  return { dir, base, out, snapshot: path.join(dir, 'prices-2026-11-19.source.md'), ran };
}
const checkRun = f => run(pricesMain, ['--check', '--explore', PRICE_EXPLORE, '--base', f.base, '--out', f.out], { fetchFn: never });

test('The_fetched_page_is_saved_beside_the_price_file_with_its_url_and_time_and_passes_the_leak_tiers', async () => {
  const f = await readFixture();
  assert.equal(f.ran.code, 0, f.ran.lines.join('|'));
  assert.equal(readFileSync(f.snapshot, 'utf8'), PAGE, 'the page as it was received');
  const file = JSON.parse(readFileSync(f.out, 'utf8'));
  assert.deepEqual(file.provenance, { url: PRICING_URL, fetchedAt: '2026-11-19T10:00:00.000Z', snapshot: 'prices-2026-11-19.source.md', snapshotSha256: sha(PAGE), cliVersion: '2.1.310' });
  const dir = tmpDir('leak-case');
  mkdirSync(path.join(dir, 'cases'));
  writeFileSync(path.join(dir, 'cases', 'a.out'), 'xyzzy 11 22 33 44 55 66\n');
  const material = buildMaterial({ tasks: [{ id: '01', heldout: { dir, casesDir: path.join(dir, 'cases') }, shinglesText: 'the trap named quuxify scatters frobnicate' }] }).value;
  const clean = scanFiles({ root: f.dir, files: ['prices-2026-11-19.source.md'], material });
  assert.deepEqual([clean.findings, clean.unreadable], [[], []], 'the public page holds no case id, trap name or case line');
  writeFileSync(f.snapshot, `${PAGE}\nxyzzy 11 22 33 44 55 66\n`);
  assert.equal(scanFiles({ root: f.dir, files: ['prices-2026-11-19.source.md'], material }).findings.length > 0, true, 'the scan does see a case line in a snapshot');
  const html = await readFixture(PAGE, new Headers({ 'content-type': 'text/html; charset=utf-8' }));
  assert.equal(existsSync(path.join(html.dir, 'prices-2026-11-19.source.html')), true, 'the page\'s own extension');
  assert.equal(JSON.parse(readFileSync(html.out, 'utf8')).provenance.snapshot, 'prices-2026-11-19.source.html');
});

test('The_check_regenerates_from_the_snapshot_and_makes_no_network_call', async () => {
  const f = await readFixture();
  const checked = await checkRun(f);
  assert.equal(checked.code, 0, checked.lines.join('|'));
  assert.match(checked.lines[0], /^check ok /);
});

test('An_edited_snapshot_an_edited_price_file_and_a_missing_snapshot_each_fail_the_check_with_a_sentence', async () => {
  const edited = await readFixture();
  writeFileSync(edited.snapshot, PAGE.replace('$4 / MTok', '$3 / MTok'));
  const snap = await checkRun(edited);
  assert.equal(snap.code, 1);
  assert.match(snap.lines[0], /^check: the snapshot .*prices-2026-11-19\.source\.md hashes to [0-9a-f]{12}, the price file's provenance says [0-9a-f]{12}: the page was edited after it was read$/);
  const price = await readFixture();
  const file = JSON.parse(readFileSync(price.out, 'utf8'));
  writeFileSync(price.out, `${JSON.stringify({ ...file, models: file.models.map(m => ({ ...m, input: m.input + 1 })) }, null, 2)}\n`);
  const priced = await checkRun(price);
  assert.equal(priced.code, 1);
  assert.match(priced.lines[0], /is not what the generator would write/);
  const gone = await readFixture();
  rmSync(gone.snapshot);
  const missing = await checkRun(gone);
  assert.equal(missing.code, 1);
  assert.match(missing.lines[0], /^check: the snapshot .*prices-2026-11-19\.source\.md is absent: --check regenerates from it and never fetches, so the page that was read must be committed beside the price file$/);
});

// ---- the price read takes no typed value (Opus review, PR 5, V9) ------------------------------------------------------------------------------------------------------------------------------------------

test('The_cli_version_of_the_price_file_is_the_one_explore_json_pins_and_a_flag_that_differs_is_refused', async () => {
  const f = await readFixture();
  assert.equal(JSON.parse(readFileSync(f.out, 'utf8')).provenance.cliVersion, '2.1.310', 'taken from explore.json\'s cli.version');
  const dir = tmpDir('prices-typed');
  const base = path.join(dir, 'base.json');
  writeFileSync(base, JSON.stringify(BASE));
  const out = path.join(dir, 'prices-2026-11-19.json');
  const same = await run(pricesMain, ['--cli-version', '2.1.310', '--explore', PRICE_EXPLORE, '--base', base, '--out', out], { fetchFn: answering(PAGE), clock: SNAPSHOT_CLOCK });
  assert.equal(same.code, 0, 'a flag that names the pinned version is no harm');
  const typed = await run(pricesMain, ['--cli-version', '9.9.9', '--explore', PRICE_EXPLORE, '--base', base, '--out', path.join(dir, 'other.json')], { fetchFn: answering(PAGE), clock: SNAPSHOT_CLOCK });
  assert.equal(typed.code, 4);
  assert.match(typed.lines[0], /^--cli-version 9\.9\.9 is not the CLI version explore\.json pins \(2\.1\.310\): the version is taken from the pin, never typed$/);
  assert.equal(existsSync(path.join(dir, 'other.json')), false);
  const stale = await readFixture();
  const other = path.join(tmpDir('prices-explore-2'), 'explore.json');
  writeFileSync(other, '{"cli":{"version":"2.1.400"}}\n');
  const checked = await run(pricesMain, ['--check', '--explore', other, '--base', stale.base, '--out', stale.out], { fetchFn: never });
  assert.equal(checked.code, 1);
  assert.match(checked.lines[0], /^check: the price file was read for CLI 2\.1\.310 and explore\.json pins 2\.1\.400$/);
});

test('The_day_of_the_prices_is_the_fetch_date_and_no_day_flag_exists', async () => {
  const f = await readFixture();
  assert.equal(JSON.parse(readFileSync(f.out, 'utf8')).readOn, '2026-11-19', 'the clock\'s UTC day of the fetch');
  const typed = await run(pricesMain, ['--explore', PRICE_EXPLORE, '--base', f.base, '--out', f.out, '--day', '2026-01-01'], { fetchFn: never });
  assert.equal(typed.code, 4, 'a typed day is a usage error');
  assert.match(typed.lines[0], /^usage error: .*--day/);
  const later = await run(pricesMain, ['--check', '--explore', PRICE_EXPLORE, '--base', f.base, '--out', f.out], { fetchFn: never, clock: () => new Date('2027-05-05T00:00:00Z') });
  assert.equal(later.code, 0, 'the check takes the day from the snapshot\'s provenance, never from the clock');
});

// ---- 79: the CLI pin --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const REAL_EXPLORE = readFileSync(path.join(REPO, 'spikes/lang-explore/inputs/explore.json'), 'utf8');

function pinFixture() {
  const home = tmpDir('pin-home');
  const versions = path.join(home, '.local', 'share', 'claude', 'versions');
  mkdirSync(versions, { recursive: true });
  writeFileSync(path.join(versions, '2.1.330'), '#!/bin/sh\necho 2.1.330\n', { mode: 0o755 });
  const explore = path.join(tmpDir('pin-explore'), 'explore.json');
  writeFileSync(explore, REAL_EXPLORE);
  return { home, versions, explore };
}

test('The_cli_block_of_the_real_explore_file_is_replaced_and_every_other_byte_stays', () => {
  const cli = { version: '2.1.330', pathParts: ['rsd', 'lang-v2', 'toolchains', 'claude-2.1.330'], sha256: 'a'.repeat(64), model: 'opus', effort: 'high' };
  const next = withCli(REAL_EXPLORE, cli);
  assert.equal(next.ok, true, next.reason);
  const old = REAL_EXPLORE.match(/"cli": \{[^{}]*\}/)[0];
  assert.equal(next.value, REAL_EXPLORE.replace(old, cliBlockText(cli)), 'only the block changed');
  assert.deepEqual(JSON.parse(next.value).cli, cli);
  assert.equal(cliBlockText(JSON.parse(REAL_EXPLORE).cli), old, 'the layout is the file\'s own: a block written from its own values is the block it has');
  assert.match(withCli('{"a":1}', cli).reason, /has no "cli" block of flat keys/);
});

test('A_stage_session_runs_the_pinned_CLI_copy_by_its_sha256', async () => {
  const f = pinFixture();
  const ran = await run(pinMain, ['--version', '2.1.330', '--explore', f.explore, '--home', f.home]);
  assert.equal(ran.code, 0, ran.lines.join('|'));
  const copy = path.join(f.home, 'rsd', 'lang-v2', 'toolchains', 'claude-2.1.330');
  assert.equal(mode(copy), 0o555, 'read-only and executable');
  const explore = JSON.parse(readFileSync(f.explore, 'utf8'));
  assert.deepEqual(explore.cli, { version: '2.1.330', pathParts: ['rsd', 'lang-v2', 'toolchains', 'claude-2.1.330'], sha256: sha(readFileSync(copy)), model: 'opus', effort: 'high' }, 'version, path and sha256 written; model and effort kept');
  assert.deepEqual(explore.caps, JSON.parse(REAL_EXPLORE).caps, 'nothing else of the file moved');
  assert.deepEqual(cliPinProblems({ explore, home: f.home }), []);
  assert.equal((await run(pinMain, ['--check', '--explore', f.explore, '--home', f.home])).code, 0);
  assert.equal((await run(pinMain, ['--version', '2.1.330', '--explore', f.explore, '--home', f.home])).code, 0, 'pinned again: the copy there is the pinned one');
  chmodSync(copy, 0o755);
  writeFileSync(copy, '#!/bin/sh\necho tampered\n');
  const problems = cliPinProblems({ explore, home: f.home });
  assert.match(problems[0], /^the pinned CLI copy .*claude-2\.1\.330 hashes to [0-9a-f]{12}, the pin says [0-9a-f]{12}$/);
  assert.match(problems[1], /is not mode 0555/);
  const checked = await run(pinMain, ['--check', '--explore', f.explore, '--home', f.home]);
  assert.equal(checked.code, 1);
  chmodSync(copy, 0o755);
  assert.equal((await run(pinMain, ['--version', '2.1.330', '--explore', f.explore, '--home', f.home])).code, 5, 'a copy there that is not the release is refused, never overwritten');
});

test('A_release_that_is_not_there_or_a_version_that_is_not_one_pins_nothing', async () => {
  const f = pinFixture();
  const before = readFileSync(f.explore, 'utf8');
  const absent = await run(pinMain, ['--version', '2.1.999', '--explore', f.explore, '--home', f.home]);
  assert.equal(absent.code, 5);
  assert.match(absent.lines[0], /the release 2\.1\.999 is not at .*versions\/2\.1\.999: the operator's auto-update may have removed it/);
  for (const argv of [[], ['--version', 'latest'], ['--version', '2.1']]) assert.equal((await run(pinMain, [...argv, '--explore', f.explore, '--home', f.home])).code, 4, argv.join(' '));
  assert.equal((await run(pinMain, ['--bogus'])).code, 4);
  assert.equal(readFileSync(f.explore, 'utf8'), before, 'explore.json untouched');
  assert.equal(existsSync(path.join(f.home, 'rsd')), false, 'no copy made');
  assert.equal(pinPlan({ version: '2.1.330', versionsDir: f.versions, home: f.home, existing: { model: 'opus', effort: 'high' } }).ok, true);
});

// ---- the CLI copy is made whole or not at all (coai plan round, PR 5, finding C) ---------------------------------------------------------------------------------------------------------------

const pinArgs = f => ['--version', '2.1.330', '--explore', f.explore, '--home', f.home];
const copyOf = f => path.join(f.home, 'rsd', 'lang-v2', 'toolchains', 'claude-2.1.330');
const inode = file => statSync(file).ino;

test('A_kill_in_the_middle_of_the_copy_leaves_no_truncated_target_and_the_retry_pins_the_release', async () => {
  const f = pinFixture();
  const before = readFileSync(f.explore, 'utf8');
  const killedCopy = plan => { writeFileSync(plan.copy.to, readFileSync(plan.copy.from).subarray(0, 5), { mode: 0o644 }); throw new Error('killed'); };
  const stopped = await run(pinMain, pinArgs(f), { installFn: killedCopy });
  assert.equal(stopped.code, 5);
  assert.match(stopped.lines[0], /^cannot copy .*versions\/2\.1\.330 to .*: killed$/);
  assert.equal(existsSync(copyOf(f)), false, 'a half copy is never at the target\'s name');
  assert.equal(existsSync(pinCli.copyTempPath(copyOf(f))), false, 'the temp file of a copy that failed is removed');
  assert.equal(readFileSync(f.explore, 'utf8'), before, 'explore.json is untouched');
  const stale = pinCli.copyTempPath(copyOf(f)); // what a real kill leaves: the half copy at the temp name, read-only
  writeFileSync(stale, 'half', { mode: 0o444 });
  const retry = await run(pinMain, pinArgs(f));
  assert.equal(retry.code, 0, retry.lines.join('|'));
  assert.equal(sha(readFileSync(copyOf(f))), sha(readFileSync(path.join(f.versions, '2.1.330'))), 'the retry replaced the stale temp and pinned the whole release');
  assert.equal(mode(copyOf(f)), 0o555);
  assert.equal(existsSync(stale), false, 'the temp name is gone after the rename');
});

test('The_same_file_already_pinned_is_reported_as_already_pinned_and_nothing_is_written_again', async () => {
  const f = pinFixture();
  assert.equal((await run(pinMain, pinArgs(f))).code, 0);
  const [copyInode, exploreInode] = [inode(copyOf(f)), inode(f.explore)];
  const again = await run(pinMain, pinArgs(f), { installFn: () => { throw new Error('the copy step ran for a copy that is already pinned'); } });
  assert.equal(again.code, 0, again.lines.join('|'));
  assert.match(again.lines[0], /^already pinned CLI 2\.1\.330: .*claude-2\.1\.330 \(sha256 [0-9a-f]{12}\), explore\.json already holds it$/);
  assert.deepEqual([inode(copyOf(f)), inode(f.explore)], [copyInode, exploreInode], 'neither the copy nor explore.json was written again');
});

test('Another_file_at_the_copys_place_is_refused_naming_both_hashes_and_a_read_only_file_is_never_overwritten', async () => {
  const f = pinFixture();
  mkdirSync(path.dirname(copyOf(f)), { recursive: true });
  writeFileSync(copyOf(f), 'another file\n', { mode: 0o555 });
  const before = readFileSync(f.explore, 'utf8');
  const refused = await run(pinMain, pinArgs(f));
  assert.equal(refused.code, 5);
  const release = sha(readFileSync(path.join(f.versions, '2.1.330')));
  assert.ok(refused.lines[0].includes(`hashes to ${sha('another file\n')}, the release hashes to ${release}`), refused.lines[0]);
  assert.match(refused.lines[0], /a pinned copy is never overwritten/);
  assert.deepEqual([readFileSync(copyOf(f), 'utf8'), mode(copyOf(f))], ['another file\n', 0o555], 'the file there is as it was');
  assert.equal(readFileSync(f.explore, 'utf8'), before);
  assert.equal(existsSync(pinCli.copyTempPath(copyOf(f))), false, 'no temp file is made for a place that is taken');
});

test('The_release_at_its_place_with_a_mode_other_than_0555_is_refused_not_rewritten', async () => {
  const f = pinFixture();
  assert.equal((await run(pinMain, pinArgs(f))).code, 0);
  chmodSync(copyOf(f), 0o755);
  const refused = await run(pinMain, pinArgs(f));
  assert.equal(refused.code, 5);
  assert.match(refused.lines[0], /is the release but its mode is 755, not 555: change it by hand; it is not rewritten/);
  assert.equal(mode(copyOf(f)), 0o755);
});

test('An_explore_file_with_no_cli_block_is_a_refusal_value_and_pins_nothing', async () => {
  const f = pinFixture();
  writeFileSync(f.explore, '{"caps":{"capUsd":50}}\n');
  const refused = await run(pinMain, pinArgs(f));
  assert.equal(refused.code, 5);
  assert.match(refused.lines[0], /explore\.json holds no "cli" block with the model and the effort to keep: nothing was pinned/);
  assert.equal(existsSync(path.join(f.home, 'rsd')), false, 'no copy was made');
});
