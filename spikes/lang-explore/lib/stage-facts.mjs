// What a Stage 1 session STORES for the readers that come after the tag (F3.S1 (a), PR 3, item 11). The read-back, the GO table and the record tools can only read what the frozen harness wrote, so this makes sure it is there, per session and
// per pass, in files of their own that no later step rewrites (each is published once, `publishOnce`):
//   <session>/argv.json         v1's: the argv and the binds of the agent box, its environment keys (read, never rewritten)
//   <session>/stage-facts.json  this module's, written when the unit's records are: the sha256 of argv.json and the binds parsed from it (`{ host, box }`, by v1's `bindPairsOf`), the agent box's environment keys and culture, the cache snapshots' tree hashes and package list,
//                               the sha256 of the workspace's STATEMENT.md and the affinity the launch read back
//   <session>/score/build.json  the build record: the sha256 of the scored tree's `run.sh` and whether the restore had to put it back (`build-commit.mjs`)
//   <session>/score/passes/*    every completed pass: the scoring box's environment keys with the two heap keys and their values and the culture (`box-facts.mjs`), and the affinity read back (`observedCpus`)
// A field that cannot be taken is `{ known: false, why }`, never a missing key. `STORED_FIELDS` names every field a later reader reads; `storedProblems` lists the ones a session's files lack.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { bindPairsOf } from '../../minigit-bench/lib/sandbox.mjs';
import { publishOnce } from './publish-once.mjs';

export const FACTS_FILE = 'stage-facts.json';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const unknown = why => ({ known: false, why });
const readJsonOr = file => { try { return JSON.parse(readFileSync(file, 'utf8')); } catch { return null; } };

/** The agent box: the argv file's hash and binds, the environment keys and the culture it had. */
function agentFacts(sessionDir) {
  const file = path.join(sessionDir, 'argv.json');
  const argv = readJsonOr(file);
  const plan = readJsonOr(path.join(sessionDir, 'plan.json'));
  if (argv === null) return { argv: unknown('argv.json cannot be read'), binds: unknown('argv.json cannot be read'), envKeys: unknown('argv.json cannot be read'), culture: unknown('argv.json cannot be read') };
  const keys = argv.envKeys ?? [];
  const both = ['LANG', 'LC_ALL'].every(k => keys.includes(k)) && typeof plan?.locale === 'string';
  return {
    argv: { known: true, file: 'argv.json', sha256: sha256(readFileSync(file)) }, binds: { known: true, value: bindPairsOf(argv.bwrap ?? []) }, envKeys: { known: true, value: keys },
    culture: both ? { known: true, LANG: plan.locale, LC_ALL: plan.locale, source: 'plan.json locale, which the box\'s environment is built from (`agentEnv`); both keys are in argv.json\'s environment keys' } : unknown('the box environment lacks LANG or LC_ALL, or plan.json names no locale'),
  };
}

const snapshotFact = snap => (snap?.record?.treeSha256 ? { known: true, treeSha256: snap.record.treeSha256 } : unknown('the world holds no registered tree hash for this snapshot'));

const packagesOf = world => [world?.snapshots?.csharp, world?.snapshots?.node].map(snap => snap?.record?.packages);

/** The package list of both snapshots: known only when both records hold one (a partial list is not the list, and no list is not an empty one). */
const packagesFact = world => (packagesOf(world).every(Array.isArray) ? { known: true, value: packagesOf(world).flat() } : unknown('the world holds no package list for one or both snapshots (the record has no `packages`)'));

/** The cache snapshots the session ran against: each tree hash and the package list. */
const cacheFacts = world => ({ csharp: snapshotFact(world?.snapshots?.csharp), node: snapshotFact(world?.snapshots?.node), packages: packagesFact(world) });

const statementFact = (world, stack) => {
  const statement = world?.filesFor?.(stack)?.['STATEMENT.md'];
  return statement === undefined ? unknown('the workspace has no STATEMENT.md') : { known: true, sha256: sha256(statement.bytes) };
};

const affinityFact = entry => {
  if (entry.run?.mask === undefined) return unknown('the run-map entry holds no mask');
  return entry.run.maskObserved === undefined || entry.run.maskObserved === null ? unknown('the affinity read-back never ran: the entry holds the mask but no observed mask') : { known: true, mask: entry.run.mask, observed: entry.run.maskObserved };
};

/** The facts of one session. `entry` is its run-map entry (the read-back of the launch's affinity), `world` the loaded world of its task. */
export function sessionFacts({ sessionDir, entry, world }) {
  return { rid: entry.rid, stack: entry.stack, agent: agentFacts(sessionDir), cache: cacheFacts(world), workspace: { statementSha256: statementFact(world, entry.stack) }, launch: { affinity: affinityFact(entry) } };
}

/** Write the session's facts once; a file that is there is left as it is. -> `true` when this call wrote it. */
export function writeSessionFacts({ sessionDir, entry, world }) {
  const file = path.join(sessionDir, FACTS_FILE);
  if (existsSync(file)) return false;
  publishOnce({ file, text: `${JSON.stringify(sessionFacts({ sessionDir, entry, world }), null, 2)}\n` });
  return true;
}

/** Every field a later reader reads, as `{ id, file, at }`: `file` is relative to the session, `at` the dotted path in it (a `*` file is each completed pass). */
export const STORED_FIELDS = Object.freeze([
  { id: 'agent.argv', file: FACTS_FILE, at: 'agent.argv' },
  { id: 'agent.binds', file: FACTS_FILE, at: 'agent.binds' },
  { id: 'agent.envKeys', file: FACTS_FILE, at: 'agent.envKeys' },
  { id: 'agent.culture', file: FACTS_FILE, at: 'agent.culture' },
  { id: 'cache.csharp', file: FACTS_FILE, at: 'cache.csharp' },
  { id: 'cache.node', file: FACTS_FILE, at: 'cache.node' },
  { id: 'cache.packages', file: FACTS_FILE, at: 'cache.packages' },
  { id: 'workspace.statementSha256', file: FACTS_FILE, at: 'workspace.statementSha256' },
  { id: 'launch.affinity', file: FACTS_FILE, at: 'launch.affinity' },
  { id: 'scoring.runSh', file: 'score/build.json', at: 'runSh' },
  { id: 'pass.box', file: 'score/passes/*', at: 'box' },
  { id: 'pass.affinity', file: 'score/passes/*', at: 'observedCpus' },
]);

const at = (object, dotted) => dotted.split('.').reduce((x, k) => (x === undefined || x === null ? undefined : x[k]), object);

/** The completed passes' files of a session. */
const passFiles = sessionDir => {
  const dir = path.join(sessionDir, 'score', 'passes');
  return existsSync(dir) ? readdirSync(dir).filter(n => n.endsWith('.json')).map(n => path.join(dir, n)).filter(f => readJsonOr(f)?.kind === 'completed') : [];
};

const problemsOf = (field, files) => (files.length === 0 ? [{ field: field.id, why: `${field.file} is absent` }] : files.flatMap(f => (at(readJsonOr(f), field.at) === undefined ? [{ field: field.id, why: `${path.basename(f)} lacks ${field.at}` }] : [])));

const filesOf = (sessionDir, field) => (field.file.endsWith('*') ? passFiles(sessionDir) : [path.join(sessionDir, field.file)].filter(existsSync));

/** The fields of `STORED_FIELDS` a session's files lack (a present key counts, `{ known: false, why }` included): `[{ field, why }]`; empty when everything a later reader reads is there. A session with no completed pass is asked for none of the pass fields. */
export function storedProblems(sessionDir) {
  const asked = STORED_FIELDS.filter(f => !f.file.endsWith('*') || passFiles(sessionDir).length > 0);
  return asked.flatMap(field => problemsOf(field, filesOf(sessionDir, field)));
}
