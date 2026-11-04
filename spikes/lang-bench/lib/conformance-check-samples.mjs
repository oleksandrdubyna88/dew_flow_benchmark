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

const idAt = index => `s${String(index + 1).padStart(2, '0')}`;
const lastId = shown => idAt(shown.ids.length - 1);

const oddProblems = folder => folder.odd.map(text => `samples/: ${text}`);
const emptyProblems = shown => (shown.ids.length === 0 ? ['section 6 shows no sample'] : []);

function gapProblems(shown) {
  const want = shown.ids.map((_, index) => idAt(index));
  return shown.ids.join() === want.join() ? [] : [`section 6 shows the samples ${shown.ids.join(', ')}; they must be s01 … ${lastId(shown)} without a gap`];
}

const missingProblems = (want, folder) => want.filter(name => !folder.names.includes(name)).map(name => `samples/${name} is missing: section 6 shows it`);
const extraProblems = (want, folder, shown) => folder.names.filter(name => !want.includes(name))
  .map(name => `samples/${name} is not one of the sample pairs section 6 shows (samples/ holds the pairs s01 … ${lastId(shown)} and nothing else)`);

function folderProblems(shown, folder) {
  const want = expectedNames(shown.ids.map((_, index) => idAt(index)));
  return [...oddProblems(folder), ...emptyProblems(shown), ...gapProblems(shown), ...missingProblems(want, folder), ...extraProblems(want, folder, shown)];
}

/** A cell in the size form (`<size> bytes, see samples/<id>.<in|out>`): it names its own file, says the file's size, and only stands for bytes that cannot be shown. */
function sizeProblems(where, decoded, bytes, file) {
  if (decoded.file !== file) return [`${where}: the cell names ${decoded.file}, and this cell is for ${file}`];
  if (decoded.size !== bytes.length) return [`${where}: the cell says ${decoded.size} bytes, and samples/${file} holds ${bytes.length}`];
  return showable(bytes) ? [`${where}: samples/${file} can be shown as bytes (FORMAT 3.4); the size form is only for what cannot`] : [];
}

const byteProblems = (where, shownBytes, bytes, file) => (shownBytes.equals(bytes) ? [] : [`${where}: the cell shows ${shownBytes.length} bytes, samples/${file} holds ${bytes.length}; first difference at byte ${firstDifference(shownBytes, bytes) + 1}`]);

function cellProblems(row, label, cell, bytes, file) {
  const decoded = decodeCell(cell ?? '');
  const where = `line ${row.no}: sample ${row.id} ${label}`;
  if (decoded.error !== undefined) return [`${where}: the cell ${decoded.error}`];
  if (bytes === undefined) return [];
  return decoded.size !== undefined ? sizeProblems(where, decoded, bytes, file) : byteProblems(where, decoded.bytes, bytes, file);
}

function olympiadProblems(shown, folder) {
  return shown.rows.flatMap(row => [
    ...cellProblems(row, 'standard input', row.input, folder.files.get(`${row.id}.in`), `${row.id}.in`),
    ...cellProblems(row, 'expected standard output', row.output, folder.files.get(`${row.id}.out`), `${row.id}.out`),
  ]);
}

const headingProblems = (row, block, file) => (block.before.trim() === `\`samples/${file}\`` ? [] : [`line ${block.openNo}: the block of sample ${row.id} must be headed by the line \`samples/${file}\`; the line above it is "${block.before.trim()}"`]);

function contentProblems(block, bytes, file) {
  const shownBytes = Buffer.from(block.content, 'utf8');
  if (bytes === undefined || bytes.equals(shownBytes)) return [];
  return [`line ${block.openNo}: the block of samples/${file} shows ${shownBytes.length} bytes, the file holds ${bytes.length}; first difference at byte ${firstDifference(shownBytes, bytes) + 1}`];
}

function oneBlockProblems(row, block, index, folder) {
  const file = `${row.id}.${index === 0 ? 'in' : 'out'}`;
  return [...headingProblems(row, block, file), ...contentProblems(block, folder.files.get(file), file)];
}

const blockProblems = (shown, folder) => shown.rows.flatMap(row => row.blocks.slice(0, 2).flatMap((block, index) => oneBlockProblems(row, block, index, folder)));

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

const isCheck = line => /^check\s/.test(line.text);
const isEntry = line => ENTRY_WORDS.has(line.text.split(/\s+/)[0]);
const isJsonEntry = line => /^body json\b/.test(line.text) || /^recv\s+\S+\s+json\b/.test(line.text);

function afterCheckProblems(id, statements) {
  const first = statements.findIndex(isCheck);
  const after = first === -1 ? [] : statements.slice(first).filter(isEntry);
  return after.map(line => `sample ${id}.out line ${line.no}: an entry after a check; a check stands only after the last entry`);
}

function jsonProblems(id, statements, hasJson) {
  const json = statements.find(isJsonEntry);
  return json !== undefined && !hasJson ? [`sample ${id}.out line ${json.no}: a JSON comparison, and no requirement carries F-JSON`] : [];
}

function transcriptProblems(id, bytes, hasJson) {
  const statements = statementsOf(bytes);
  return [...afterCheckProblems(id, statements), ...jsonProblems(id, statements, hasJson)];
}

const isRequest = line => /^request\s+\S+\s+[A-Z]+\s/.test(line.text);
const hasSlashTarget = line => /^request\s+\S+\s+[A-Z]+\s+"\//.test(line.text);

const targetProblems = (id, bytes) => statementsOf(bytes).filter(line => isRequest(line) && !hasSlashTarget(line))
  .map(line => `sample ${id}.in line ${line.no}: the target of a request begins with "/"`);

const stripSuffix = name => name.replace(/\.(?:in|out)$/, '');
const isPairFile = name => /\.(?:in|out)$/.test(name);

function idProblems(ctx) {
  return [
    ...ctx.shown.ids.filter(id => !SAMPLE_ID.test(id)).map(id => `sample id "${id}" in section 6 is not s<two digits>`),
    ...ctx.folder.names.filter(name => isPairFile(name) && !SAMPLE_ID.test(stripSuffix(name))).map(name => `samples/${name}: the id is not s<two digits>`),
  ];
}

function scenarioFormProblems(ctx) {
  const hasJson = (ctx.found.get('F-JSON')?.hits.length ?? 0) > 0;
  return [
    ...[...ctx.scenarios].flatMap(([id, bytes]) => targetProblems(id, bytes)),
    ...[...ctx.transcripts].flatMap(([id, bytes]) => transcriptProblems(id, bytes, hasJson)),
  ];
}

/** Check 8 (CP1 F11): a `check` only after the last transcript entry, a request target that begins with `/`, sample ids exactly `s<two digits>`; a JSON comparison only where F-JSON stands. */
export function formFindings(ctx) {
  return ctx.kind === 'olympiad' ? idProblems(ctx) : [...idProblems(ctx), ...scenarioFormProblems(ctx)];
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
