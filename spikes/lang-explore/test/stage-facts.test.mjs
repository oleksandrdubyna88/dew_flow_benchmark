// What a Stage 1 session stores for the readers after the tag (F3.S1 (a), PR 3, item 11): the argv and binds of the agent box, the cache snapshots' hashes and package list, the environment keys of the agent box and of the scoring box of each
// completed pass with the two heap keys and their values, the culture of both boxes, the sha256 of the scored tree's run.sh and whether the restore put it back, the affinity read-backs and the sha256 of STATEMENT.md. The writers are the real ones
// (`writeSessionFacts`, `commitBuild`, `writeAttempt` over `passBody`); a fixture session is made with them and held against a list of the fields written out here.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { scoringBoxFacts } from '../lib/box-facts.mjs';
import { commitBuild, runShFact } from '../lib/build-commit.mjs';
import { passBody } from '../lib/pass-child.mjs';
import { writeAttempt } from '../lib/score-files.mjs';
import { FACTS_FILE, STORED_FIELDS, storedProblems, writeSessionFacts } from '../lib/stage-facts.mjs';

const MIB = 1024 * 1024;
const LIMIT = 512 * MIB;
const DIRS = { dotnetSdk: '/t/dotnet', node: '/t/node', typescript: '/t/ts' };
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const READ_BY_LATER_TOOLS = ['agent.argv', 'agent.binds', 'agent.envKeys', 'agent.culture', 'cache.csharp', 'cache.node', 'cache.packages', 'workspace.statementSha256', 'launch.affinity', 'scoring.runSh', 'pass.box', 'pass.affinity'];
const RUN_SH = '#!/bin/bash\nexec node dist/main.js\n';

const world = { snapshots: { csharp: { record: { treeSha256: 'c'.repeat(64), packages: ['pkg-a@1'] } }, node: { record: { treeSha256: 'd'.repeat(64), packages: ['pkg-b@2'] } } }, filesFor: () => ({ 'STATEMENT.md': { bytes: Buffer.from('the statement\n') } }) };
const entry = { rid: 'abc123', stack: 'node', run: { mask: '0-5', maskObserved: '0-5' } };

/** A session written by the real writers: argv.json and plan.json as the launch leaves them, the facts, one completed pass and the build record. */
async function fixtureSession({ locale = 'de_DE.UTF-8', frozen = [], stack = 'node' } = {}) {
  const dir = tmpDir('stage-facts');
  const bwrap = ['bwrap', '--ro-bind', '/usr', '/usr', '--bind', '/work', '/w', '--dev', '/dev'];
  writeFileSync(path.join(dir, 'argv.json'), `${JSON.stringify({ bwrap, claude: ['claude'], envKeys: ['HOME', ...(locale ? ['LANG', 'LC_ALL'] : [])] })}\n`);
  writeFileSync(path.join(dir, 'plan.json'), `${JSON.stringify({ locale })}\n`);
  writeSessionFacts({ sessionDir: dir, entry: { ...entry, stack }, world });
  const work = path.join(dir, 'score');
  const built = await commitBuild({
    work, prepare: async dest => { mkdirSync(dest, { recursive: true }); writeFileSync(path.join(dest, 'run.sh'), RUN_SH); return ok({ frozen, drift: [], undecided: [], left: [], identity: 'id', files: {} }); },
    build: async () => ok({ ok: true, exit: 0, timedOut: false, wallMs: 5, tail: '' }),
  });
  assert.equal(built.ok, true, built.reason);
  const body = passBody({ ran: { ok: true, value: { record: { runner: '0.x', cases: [] }, ran: { memoryCertified: true } } }, observedCpus: '0-5', box: scoringBoxFacts({ stack, dirs: DIRS, memoryBytes: LIMIT }) });
  writeAttempt({ work, n: 1, profile: 'low', attempt: 1, body });
  return { dir, work };
}

const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

test('Every_field_the_read_back_the_GO_table_and_the_record_tools_read_is_in_the_sessions_stored_files', async () => {
  assert.deepEqual(STORED_FIELDS.map(f => f.id), READ_BY_LATER_TOOLS, 'the list of the fields is the one a later reader reads');
  const { dir } = await fixtureSession();
  assert.deepEqual(storedProblems(dir), [], 'every field is in a file the harness wrote');
  const facts = readJson(path.join(dir, FACTS_FILE));
  assert.deepEqual(facts.agent.binds.value, [{ host: '/usr', box: '/usr' }, { host: '/work', box: '/w' }], 'the binds of the agent box, parsed from the argv it ran with');
  assert.equal(facts.agent.argv.sha256, sha(readFileSync(path.join(dir, 'argv.json'))));
  assert.deepEqual(facts.cache.packages.value, ['pkg-a@1', 'pkg-b@2']);
  assert.deepEqual([facts.cache.csharp.treeSha256, facts.cache.node.treeSha256], ['c'.repeat(64), 'd'.repeat(64)]);
  assert.equal(facts.workspace.statementSha256.sha256, sha(Buffer.from('the statement\n')));
  assert.deepEqual(facts.launch.affinity, { known: true, mask: '0-5', observed: '0-5' });
  assert.equal(writeSessionFacts({ sessionDir: dir, entry, world }), false, 'written once: a second call leaves the file as it is');
  // each field removed from what the harness wrote is named
  const stripped = (file, key) => { const f = path.join(dir, file); const j = readJson(f); delete j[key]; writeFileSync(f, `${JSON.stringify(j)}\n`); };
  const facts2 = readJson(path.join(dir, FACTS_FILE));
  delete facts2.agent.culture;
  writeFileSync(path.join(dir, FACTS_FILE), JSON.stringify(facts2));
  stripped('score/build.json', 'runSh');
  const pass = path.join(dir, 'score', 'passes', 'p01-low-a1.json');
  const passJson = readJson(pass);
  delete passJson.box;
  writeFileSync(pass, JSON.stringify(passJson));
  assert.deepEqual(storedProblems(dir).map(p => p.field).sort(), ['agent.culture', 'pass.box', 'scoring.runSh']);
});

test('A_session_stores_the_culture_of_both_boxes_and_the_hash_of_the_run_sh_that_started_its_program', async () => {
  const { dir } = await fixtureSession();
  const facts = readJson(path.join(dir, FACTS_FILE));
  assert.deepEqual([facts.agent.culture.LANG, facts.agent.culture.LC_ALL], ['de_DE.UTF-8', 'de_DE.UTF-8'], 'the agent box');
  assert.deepEqual(facts.agent.envKeys.value, ['HOME', 'LANG', 'LC_ALL']);
  const pass = readJson(path.join(dir, 'score', 'passes', 'p01-low-a1.json'));
  assert.deepEqual([pass.box.known, pass.box.LANG, pass.box.LC_ALL], [true, 'de_DE.UTF-8', 'de_DE.UTF-8'], 'the scoring box, each completed pass');
  assert.deepEqual(pass.box.heap, { NODE_OPTIONS: `--max-old-space-size=${Math.floor((LIMIT * 9) / 10 / MIB)}` }, 'Node\'s heap key and its value, derived from the registered memory value');
  assert.ok(pass.box.envKeys.includes('LANG') && pass.box.envKeys.includes('NODE_OPTIONS'));
  assert.equal(pass.observedCpus, '0-5', 'the affinity read back of the pass');
  const csharp = scoringBoxFacts({ stack: 'csharp', dirs: DIRS, memoryBytes: LIMIT });
  assert.deepEqual(csharp.heap, { DOTNET_GCHeapHardLimit: `0x${((BigInt(LIMIT) * 9n) / 10n).toString(16)}` }, '.NET\'s heap key: nine tenths of the limit in hexadecimal');
  const build = readJson(path.join(dir, 'score', 'build.json'));
  assert.deepEqual(build.runSh, { known: true, sha256: sha(RUN_SH), restored: false }, 'the frozen run.sh of the scored tree, and the restore did not have to put it back');
  const restored = await fixtureSession({ frozen: [{ path: 'run.sh', state: 'changed' }] });
  assert.equal(readJson(path.join(restored.dir, 'score', 'build.json')).runSh.restored, true, 'a run.sh the agent changed was put back, and the fact says so');
  assert.deepEqual(runShFact({ tree: tmpDir('stage-no-runsh'), frozen: [] }), { known: false, why: 'the committed tree has no run.sh' });
  const noLocale = await fixtureSession({ locale: '' });
  assert.equal(readJson(path.join(noLocale.dir, FACTS_FILE)).agent.culture.known, false, 'a culture that cannot be taken says why, never a missing key');
  assert.deepEqual(storedProblems(noLocale.dir), [], 'and counts as stored');
  assert.equal(scoringBoxFacts({ stack: 'rust', dirs: DIRS, memoryBytes: LIMIT }).known, false);
});
