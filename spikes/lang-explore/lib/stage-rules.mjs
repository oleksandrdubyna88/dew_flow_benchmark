// The two rule files of the tools outside the harness identity (F3.S1 (b), PR 5; stories document section 3, condition 1): `pilot-rules.json` (the GO table and the read-back, as data) and `adjudication-rules.json` (the blind adjudication,
// as data). The contract's own part names every GO row and every adjudication rule with one marker line each (`<!-- go-row: G-1 -->`, `<!-- adjudication-rule: packets-sessions -->`), and the two files must hold exactly those ids, each
// row and rule complete: so a tool written after the tag has nothing to decide, and a row or a rule the contract names and the file lacks (or the reverse) is a refusal. Pure.
export const GO_ROW_MARKER = /^<!-- go-row: (G-\d+) -->$/;
export const ADJUDICATION_RULE_MARKER = /^<!-- adjudication-rule: ([a-z0-9]+(?:-[a-z0-9]+)*) -->$/;

/** What a GO row of `pilot-rules.json` must say (stories document section 6, PR 7 item 1): the sessions it is asked of, where its evidence is stored, whether it is computed or by hand, its threshold, what missing evidence gives, whether `not exercised` may pass and with which evidence, and whether a refutation stops. */
export const ROW_KEYS = Object.freeze(['id', 'askedOf', 'evidence', 'mode', 'threshold', 'whenMissing', 'notExercised', 'stopsWhenRefuted']);
/** What an adjudication rule of `adjudication-rules.json` must say: its id and its words. */
export const RULE_KEYS = Object.freeze(['id', 'text']);

/** The ids the contract's own part names: `{ rows, rules }`, in the order of the text. */
export function contractIds(contractText) {
  const lines = String(contractText ?? '').split('\n').map(l => l.trim());
  const idsOf = marker => lines.map(l => marker.exec(l)?.[1]).filter(Boolean);
  return { rows: idsOf(GO_ROW_MARKER), rules: idsOf(ADJUDICATION_RULE_MARKER) };
}

const duplicates = ids => ids.filter((id, i) => ids.indexOf(id) !== i);
const missingKeys = (entry, keys) => keys.filter(key => entry?.[key] === undefined || entry[key] === null || entry[key] === '');

/** The sentences for one list of entries against the ids the contract names: a duplicate, an id the contract does not name, an id it names that the file lacks, and an entry that leaves one of its keys open. */
function listProblems({ what, named, entries, keys }) {
  const ids = entries.map(e => e?.id);
  const dups = duplicates(ids).map(id => `${what} ${id} is in the file twice`);
  const extra = ids.filter(id => !named.includes(id)).map(id => `${what} ${id} is in the file and the contract does not name it`);
  const lacking = named.filter(id => !ids.includes(id)).map(id => `${what} ${id} is named by the contract and the file lacks it`);
  const open = entries.flatMap(e => (missingKeys(e, keys).length === 0 ? [] : [`${what} ${e?.id ?? '(no id)'} leaves open: ${missingKeys(e, keys).join(', ')}`]));
  return [...dups, ...extra, ...lacking, ...open];
}

/** The sentences where the two rule files are not exactly the rows and rules the contract names, each complete. `[]` when they are. */
export function ruleFileProblems({ contractText, pilotRules, adjudicationRules }) {
  const named = contractIds(contractText);
  const rows = Array.isArray(pilotRules?.rows) ? listProblems({ what: 'row', named: named.rows, entries: pilotRules.rows, keys: ROW_KEYS }) : ['pilot-rules.json holds no `rows` list'];
  const rules = Array.isArray(adjudicationRules?.rules) ? listProblems({ what: 'rule', named: named.rules, entries: adjudicationRules.rules, keys: RULE_KEYS }) : ['adjudication-rules.json holds no `rules` list'];
  return [...rows, ...rules];
}
