// Every identity the pre-registration freezes, and the guard's comparison with the manifest (F3.S1 (a), PR 4; stories document section 5, brief item 1: `liveIdentities()` and `identityProblems(manifest, live)`). The list is `READERS`
// (`stage-identity-readers.mjs`): this module owns the LIST and the comparison; the manifest is PR 5's file, `manifest.identities.<name>` a text each. A difference is a sentence naming the identity with both short hashes; an identity that
// cannot be read, or that the manifest lacks or holds beyond the list, is a sentence too. The harness identity alone is accepted when a dated `instrument | harness:<12 hex>` line names the live one and no data session has left `planned`
// (the caller reads the line and passes `harnessAccepted`, the 12 hex of what it accepts); for every other identity there is no acceptance.
import path from 'node:path';
import { loadExplore } from './inputs.mjs';
import { POLICY_FREE_FIELDS, READERS } from './stage-identity-readers.mjs';
import { RECORD_PATHS, readRecord } from './stage-record.mjs';

export { POLICY_FREE_FIELDS };
export const IDENTITY_NAMES = Object.freeze(READERS.map(([name]) => name));
const SHORT = 12;
const short = value => String(value).slice(0, SHORT);

/** One identity, read: `{ value }` or `{ unknown: why }` (a reader that throws is a unit of its own: the others are still read). */
async function readOne({ name, read, ctx }) {
  try {
    return { value: String(await (ctx.readers?.[name] ?? read)(ctx)) };
  } catch (e) {
    return { unknown: e.message };
  }
}

const exploreOf = ({ repo, explore }) => {
  if (explore !== undefined) return explore;
  const loaded = loadExplore(path.join(repo, 'spikes/lang-explore/inputs/explore.json'), path.join(repo, RECORD_PATHS.memoryLimit));
  return loaded.ok ? loaded.value : null;
};

/**
 * `{ [name]: { value } | { unknown } }` for every identity of the list, each read from the place that owns it. `{ repo, home, taskRepo, record, explore, readers }`: `readers` replaces a reader by name (PR 5 hands in `harness`); a missing
 * `record` is read from `repo`; `explore` defaults to `explore.json` of `repo`. Reads files, the task repository's blobs and, for the two snapshots, about a second of hashing.
 */
export async function liveIdentities({ repo, home, taskRepo, record, explore, readers = {} }) {
  const ctx = { repo, home, taskRepo, record: record ?? readRecord(repo).record, explore: exploreOf({ repo, explore }), readers };
  const entries = await Promise.all(READERS.map(async ([name, read]) => [name, await readOne({ name, read, ctx })]));
  return Object.fromEntries(entries);
}

const unreadable = now => now === undefined || now.value === undefined;
const unreadSentence = ({ name, now }) => `${name} cannot be read on this host: ${now?.unknown ?? 'no reader answered'}`;
const isAccepted = ({ name, now, harnessAccepted }) => name === 'harness' && harnessAccepted.includes(short(now.value));
const isRegistered = args => args.held === args.now.value || isAccepted(args);

/** The sentence for one identity, or null when it equals the manifest's (or is the harness identity a dated line accepts). */
function problemOf(args) {
  if (unreadable(args.now)) return unreadSentence(args);
  if (args.held === undefined) return `${args.name} is not in the manifest: nothing is registered to compare ${short(args.now.value)} with`;
  return isRegistered(args) ? null : `${args.name} differs: the manifest has ${short(args.held)}, this checkout has ${short(args.now.value)}`;
}

/**
 * `[sentence]` for every identity that differs from the manifest (`manifest.identities`), with both short hashes; empty when every identity is the registered one. `live` is `liveIdentities`'s answer, `harnessAccepted` the 12 hex of the
 * harness identities a dated instrument line accepts (none for any other identity). A manifest that holds an identity the guard's list does not is named: the manifest cannot grow what the guard never reads.
 */
export function identityProblems(manifest, live, { harnessAccepted = [] } = {}) {
  const held = manifest?.identities;
  if (held === null || typeof held !== 'object') return ['the pre-registration manifest holds no identities: what the series is frozen under cannot be compared'];
  const own = IDENTITY_NAMES.map(name => problemOf({ name, held: held[name], now: live[name], harnessAccepted })).filter(Boolean);
  const beyond = Object.keys(held).filter(name => !IDENTITY_NAMES.includes(name)).map(name => `${name} is in the manifest but not in the guard's list of identities: the guard never reads it`);
  return [...own, ...beyond];
}
