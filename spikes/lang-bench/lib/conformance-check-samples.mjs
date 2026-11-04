// Checks 7 to 9 (E1.S8): the samples folder against section 6, the forms CP1's F11 names, and the `env` lines against the execution layer's own lists. The runner's parse of each pair is
// started by conformance.mjs (conformance-runner.mjs) and joins check 7's findings.
import { ALLOW_LIST, CASE_KEYS } from './exec-layer/env.mjs';
import { decodeCell, showable } from './conformance-shown.mjs';

const SAMPLE_ID = /^s\d\d$/;

const firstDifference = (a, b) => {
  const at = [...a].findIndex((byte, index) => byte !== b[index]);
  return at === -1 ? Math.min(a.length, b.length) : at;
};

/** The names the folder must hold for the samples section 6 shows. */
const expectedNames = ids => ids.flatMap(id => [`${id}.in`, `${id}.out`]);

function folderProblems(shown, folder) {
  const problems = [...folder.odd.map(text => `samples/: ${text}`)];
  const ids = shown.ids;
  if (ids.length === 0) problems.push('section 6 shows no sample');
  const want = expectedNames(ids.map((_, index) => `s${String(index + 1).padStart(2, '0')}`));
  if (ids.join() !== ids.map((_, index) => `s${String(index + 1).padStart(2, '0')}`).join()) problems.push(`section 6 shows the samples ${ids.join(', ')}; they must be s01 … s${String(ids.length).padStart(2, '0')} without a gap`);
  for (const name of want) if (!folder.names.includes(name)) problems.push(`samples/${name} is missing: section 6 shows it`);
  for (const name of folder.names) if (!want.includes(name)) problems.push(`samples/${name} is not one of the sample pairs section 6 shows (samples/ holds the pairs s01 … s${String(ids.length).padStart(2, '0')} and nothing else)`);
  return problems;
}

function cellProblems(row, label, cell, bytes, file) {
  const decoded = decodeCell(cell ?? '');
  const where = `line ${row.no}: sample ${row.id} ${label}`;
  if (decoded.error !== undefined) return [`${where}: the cell ${decoded.error}`];
  if (bytes === undefined) return [];
  if (decoded.size !== undefined) {
    if (decoded.file !== file) return [`${where}: the cell names ${decoded.file}, and this cell is for ${file}`];
    if (decoded.size !== bytes.length) return [`${where}: the cell says ${decoded.size} bytes, and samples/${file} holds ${bytes.length}`];
    return showable(bytes) ? [`${where}: samples/${file} can be shown as bytes (FORMAT 3.4); the size form is only for what cannot`] : [];
  }
  if (decoded.bytes.equals(bytes)) return [];
  return [`${where}: the cell shows ${decoded.bytes.length} bytes, samples/${file} holds ${bytes.length}; first difference at byte ${firstDifference(decoded.bytes, bytes) + 1}`];
}

function olympiadProblems(shown, folder) {
  return shown.rows.flatMap(row => [
    ...cellProblems(row, 'standard input', row.input, folder.files.get(`${row.id}.in`), `${row.id}.in`),
    ...cellProblems(row, 'expected standard output', row.output, folder.files.get(`${row.id}.out`), `${row.id}.out`),
  ]);
}

function blockProblems(shown, folder) {
  return shown.rows.flatMap(row => row.blocks.slice(0, 2).flatMap((block, index) => {
    const file = `${row.id}.${index === 0 ? 'in' : 'out'}`;
    const heading = block.before.trim();
    const bytes = folder.files.get(file);
    const problems = heading === `\`samples/${file}\`` ? [] : [`line ${block.openNo}: the block of sample ${row.id} must be headed by the line \`samples/${file}\`; the line above it is "${heading}"`];
    if (bytes !== undefined && !bytes.equals(Buffer.from(block.content, 'utf8'))) problems.push(`line ${block.openNo}: the block of samples/${file} shows ${Buffer.byteLength(block.content)} bytes, the file holds ${bytes.length}; first difference at byte ${firstDifference(Buffer.from(block.content, 'utf8'), bytes) + 1}`);
    return problems;
  }));
}

/** Check 7 without the runner: the folder holds exactly the pairs section 6 shows, and the cells or blocks equal the files' bytes. */
export function sampleFindings(ctx) {
  const { shown, folder, kind } = ctx;
  return [...shown.problems, ...folderProblems(shown, folder), ...(kind === 'olympiad' ? olympiadProblems(shown, folder) : blockProblems(shown, folder))];
}

/** Statements of a script or transcript, each with its line number: blank and comment lines dropped. */
export function statementsOf(bytes) {
  return bytes.toString('utf8').split('\n').map((text, index) => ({ no: index + 1, text: text.trim() })).filter(line => line.text !== '' && !line.text.startsWith('#'));
}

const ENTRY_WORDS = new Set(['ready', 'recv', 'reply', 'quiet', 'closed', 'exit', 'repeat', 'unordered', 'end', 'header', 'body']);

function transcriptProblems(id, bytes, hasJson) {
  const statements = statementsOf(bytes);
  const firstCheck = statements.findIndex(line => /^check\s/.test(line.text));
  const problems = firstCheck === -1 ? [] : statements.slice(firstCheck).filter(line => ENTRY_WORDS.has(line.text.split(/\s+/)[0])).map(line => `sample ${id}.out line ${line.no}: an entry after a check; a check stands only after the last entry`);
  const json = statements.find(line => /^body json\b/.test(line.text) || /^recv\s+\S+\s+json\b/.test(line.text));
  return json !== undefined && !hasJson ? [...problems, `sample ${id}.out line ${json.no}: a JSON comparison, and no requirement carries F-JSON`] : problems;
}

function targetProblems(id, bytes) {
  return statementsOf(bytes).filter(line => /^request\s+\S+\s+[A-Z]+\s/.test(line.text) && !/^request\s+\S+\s+[A-Z]+\s+"\//.test(line.text))
    .map(line => `sample ${id}.in line ${line.no}: the target of a request begins with "/"`);
}

/** Check 8 (CP1 F11): a `check` only after the last transcript entry, a request target that begins with `/`, sample ids exactly `s<two digits>`; a JSON comparison only where F-JSON stands. */
export function formFindings(ctx) {
  const findings = ctx.shown.ids.filter(id => !SAMPLE_ID.test(id)).map(id => `sample id "${id}" in section 6 is not s<two digits>`);
  findings.push(...ctx.folder.names.filter(name => /\.(?:in|out)$/.test(name) && !SAMPLE_ID.test(name.replace(/\.(?:in|out)$/, ''))).map(name => `samples/${name}: the id is not s<two digits>`));
  if (ctx.kind === 'olympiad') return findings;
  const hasJson = (ctx.found.get('F-JSON')?.hits.length ?? 0) > 0;
  for (const [id, bytes] of ctx.scenarios) findings.push(...targetProblems(id, bytes));
  for (const [id, bytes] of ctx.transcripts) findings.push(...transcriptProblems(id, bytes, hasJson));
  return findings;
}

const lower = text => text.toLowerCase();

/** The keys of an `env` line the execution layer would refuse: a base key other than `TZ` in any spelling of its case, or a per-case heap key. The lists are the layer's (env.mjs). */
export function refusedEnvKey(key, { allowList = ALLOW_LIST, caseKeys = CASE_KEYS } = {}) {
  return (allowList.some(base => lower(base) === lower(key)) && key !== 'TZ') || caseKeys.some(heap => lower(heap) === lower(key));
}

/** Check 9: the `env` lines of every daemon and web sample. */
export function envFindings(ctx) {
  if (ctx.kind === 'olympiad') return [];
  const lists = ctx.envLists ?? {};
  return [...ctx.scenarios].flatMap(([id, bytes]) => statementsOf(bytes)
    .map(line => ({ line, key: /^env\s+(\S+)/.exec(line.text)?.[1] }))
    .filter(found => found.key !== undefined && refusedEnvKey(found.key, lists))
    .map(found => `sample ${id}.in line ${found.line.no}: the env line sets ${found.key}, which the test machine's environment does not allow a scenario to set`));
}
