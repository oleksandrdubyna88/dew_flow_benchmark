// The scans over what a push carries, as a flat list of tagged findings: `{ check, commit, path, line, what, atTip }`. Nothing here
// mutates anything it is given; the check composition (push-check.mjs) groups the list by the check's name.
//
// What reads what: the leak and secret scans read every commit and every message (history is published); every other rule reads the
// files as they stand at the tip (a run base carries the tip tree, never the history), so a finding of those is kept only when the blob
// is still at the tip. A file's path is scanned once, like text.
import { attributionSection, outsideAttribution } from './attribution.mjs';
import { decodeBlob } from './push-decode.mjs';
import { findAidHits } from './push-needles.mjs';
import { findOwnSecrets, findSecrets, maskSecrets } from './push-secrets.mjs';
import { findVisibleRules, GROUP_FAMILY } from './push-visible.mjs';

export const COMMIT_MESSAGE = '(commit message)';
export const SUBMODULE = '160000';
export const NUL_REASON = 'file holds a NUL byte (binary or UTF-16): the text scans cannot be trusted on it';
/** The checks that read every commit; the others read the tip. */
export const HISTORY_CHECKS = Object.freeze(new Set(['leakcheck', 'secrets']));

/** The findings of one text, by check name. `file` decides which placement rules apply; `body` is a PR or review body (links allowed). */
export function scanText(file, text, inputs, { body = false } = {}) {
  const readme = file === 'README.md';
  const visible = findVisibleRules(file, readme ? outsideAttribution(text) : text, { known: inputs.known, assumeVisible: body, links: !body });
  return {
    leakcheck: findAidHits(file, text, inputs.aids, { mode: inputs.aidMode }),
    secrets: [...findSecrets(file, text), ...findOwnSecrets(file, text, inputs.ownSecrets ?? [])],
    [readme ? 'attribution' : 'run-base-text']: visible.filter(f => f.group !== GROUP_FAMILY),
    'family-names': visible.filter(f => f.group === GROUP_FAMILY),
  };
}

/** `{ check: [finding] }` as a flat list, each finding tagged with its check and the commit. */
export const flatten = (byName, commit) => Object.entries(byName).flatMap(([check, findings]) => findings.map(f => ({ check, commit, ...f })));

/** A file's PATH is text too; a finding names the path as the position, a secret in it masked, and has no line. */
function scanPath(file, inputs) {
  const shown = maskSecrets(file);
  return Object.fromEntries(Object.entries(scanText(file, file, inputs)).map(([check, findings]) => [check, findings.map(f => ({ ...f, path: shown, line: null }))]));
}

/** For each key the index of its first occurrence in `items`. */
const firstIndexBy = (items, keyOf) => new Map([...items].reverse().map((item, i) => [keyOf(item), items.length - 1 - i]));

/** The entries to read: the first of each (path, blob), no submodule pointers, each told whether its blob is the tip's and whether it is its path's first. */
export function scanEntries(files, tip) {
  const byBlob = firstIndexBy(files, f => `${f.path}\0${f.oid}`);
  const byPath = firstIndexBy(files, f => f.path);
  return files
    .map((f, i) => ({ ...f, atTip: tip.get(f.path)?.oid === f.oid, firstBlob: byBlob.get(`${f.path}\0${f.oid}`) === i, firstOfPath: byPath.get(f.path) === i }))
    .filter(e => e.firstBlob && e.mode !== SUBMODULE);
}

/** Tagged findings of one entry's decoded blob (none when `bytes` is undefined: the blob is already published), its path (once) and, where asked, its NUL byte. */
export function entryFindings(entry, bytes, inputs, { refuseNul }) {
  const { commit, path } = entry;
  const { text, nul } = bytes === undefined ? { text: null, nul: false } : decodeBlob(bytes);
  const refusal = nul && refuseNul ? [{ check: 'tree-shape', commit, path, line: null, what: NUL_REASON }] : [];
  return [
    ...(text === null ? [] : flatten(scanText(path, text, inputs), commit)),
    ...(entry.firstOfPath ? flatten(scanPath(path, inputs), commit) : []),
    ...refusal,
  ].map(f => ({ ...f, atTip: entry.atTip }));
}

/** Tagged findings of the commit messages (never at the tip: only the history checks keep them). */
export const messageFindings = (messages, inputs) => messages.flatMap(({ commit, text }) => flatten(scanText(COMMIT_MESSAGE, text, inputs), commit).map(f => ({ ...f, atTip: false })));

const isMessageFamily = f => f.check === 'family-names' && f.path === COMMIT_MESSAGE;

/** Only what the check reads: a history check keeps everything, a family name in a commit message too (public history), any other keeps what is at the tip. */
export const keptFindings = findings => findings.filter(f => f.atTip !== false || HISTORY_CHECKS.has(f.check) || isMessageFamily(f));

/** Is there a README structure to check? It is when a known-task text is in play or the README already has the heading. */
const attributionNeeded = (text, addedPaths, carriesKnownText) => text !== null && (carriesKnownText(addedPaths) || /^## Attribution\s*$/m.test(text));

/** README structure findings: the attribution section present once and closing the file, when it is needed. */
export function attributionFindings(text, addedPaths, carriesKnownText) {
  if (!attributionNeeded(text, addedPaths, carriesKnownText)) return [];
  const section = attributionSection(text);
  return section.ok ? [] : [{ path: 'README.md', line: null, what: section.reason }];
}
