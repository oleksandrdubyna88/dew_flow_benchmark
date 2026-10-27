#!/usr/bin/env node
// Step 2 of a Q arm: the local model fills the FILL regions a Sonnet skeleton left, and nothing else.
//   node fill.mjs <arm> <test|prod>
// A region is, in any tracked or new file of the arm's clone:
//     // QWEN-FILL-BEGIN <id> <test|prod>
//     // instructions written by the skeleton author
//     <placeholder body>
//     // QWEN-FILL-END <id>
// Its context pack is vllm/work/<arm>/regions/<id>.md. The answer replaces ONLY the lines between the markers (the rail):
// an answer that holds a marker, is empty or leaves unbalanced braces is rejected and asked once more; a second rejection
// leaves the placeholder in place and is recorded. Up to 4 regions go to vLLM at once (its --max-num-seqs). Then the
// arm builds, the test classes of the filled files run, and the result goes to results/<arm>/fill-<kind>.json.
// Re-runnable: regions already filled (results/<arm>/fill-state.json) are skipped.
import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync, readdirSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';

const [arm, kind] = process.argv.slice(2);
if (!arm || !['test', 'prod'].includes(kind)) { console.error('usage: node fill.mjs <arm> <test|prod>'); process.exit(1); }
const E = process.env.COAI_EXP_ROOT ?? '/home/jinx/git/coai-exp';
const BASE = process.env.COAI_EXP_BASE ?? 'b4e2ea96'; // the start commit; settable only for the harness's own test
const DIR = `${E}/arm-${arm}`;
const OUT = `${E}/results/${arm}`;
const WORK = `${E}/vllm/work/${arm}`;
const STATE = `${OUT}/fill-state.json`;
// the filler: the local model (default) or, for the H arms, Haiku through the same interface
const ASK = process.env.COAI_EXP_FILLER === 'haiku' ? `${E}/vllm/ask-haiku.mjs` : `${E}/vllm/ask.mjs`;
const BEGIN = /^(\s*)\/\/ QWEN-FILL-BEGIN (\S+) (test|prod)\s*$/;
const END = (id) => new RegExp(`^\\s*// QWEN-FILL-END ${id.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s*$`);
mkdirSync(`${WORK}/prompts`, { recursive: true });
mkdirSync(OUT, { recursive: true });

const git = (...a) => execFileSync('git', ['-C', DIR, ...a], { encoding: 'utf8' });
const state = existsSync(STATE) ? JSON.parse(readFileSync(STATE, 'utf8')) : { filled: {} };
const save = () => writeFileSync(STATE, JSON.stringify(state, null, 2));

// every file the arm changed or added since the start commit, and every new untracked one
const files = [...new Set([
  ...git('diff', '--name-only', BASE).split('\n'),
  ...git('ls-files', '--others', '--exclude-standard').split('\n'),
].filter((f) => f && /\.(cs|ts|mjs)$/.test(f) && existsSync(join(DIR, f))))];

function regionsIn(file) {
  const lines = readFileSync(join(DIR, file), 'utf8').split('\n');
  const found = [];
  for (let i = 0; i < lines.length; i++) {
    const m = lines[i].match(BEGIN);
    if (!m) continue;
    const end = lines.findIndex((l, j) => j > i && END(m[2]).test(l));
    if (end < 0) { found.push({ file, id: m[2], kind: m[3], broken: 'no END marker' }); continue; }
    found.push({ file, id: m[2], kind: m[3], indent: m[1], begin: i, end, body: lines.slice(i + 1, end) });
  }
  return found;
}
const all = files.flatMap(regionsIn);
const mine = all.filter((r) => r.kind === kind && !state.filled[r.id]);
console.log(`${all.length} regions in ${files.length} files; ${mine.length} ${kind} regions to fill now`);

function promptFor(r) {
  const lines = readFileSync(join(DIR, r.file), 'utf8').split('\n');
  const shown = lines.map((l, i) => (i === r.begin ? `${l}   <<< REGION ${r.id} STARTS AFTER THIS LINE` : i === r.end ? `${l}   <<< REGION ${r.id} ENDS BEFORE THIS LINE` : l)).join('\n');
  const instructions = r.body.filter((l) => l.trim().startsWith('//')).map((l) => l.trim().replace(/^\/\/\s?/, '')).join('\n');
  const packFile = `${WORK}/regions/${r.id}.md`;
  const pack = existsSync(packFile) ? readFileSync(packFile, 'utf8') : '(no context pack was written for this region)';
  const lang = r.file.endsWith('.cs') ? 'csharp' : 'typescript';
  return `You write the body of ONE region in an existing ${lang === 'csharp' ? 'C#' : 'TypeScript'} file. The surrounding code, the
signatures and the types are fixed and correct: do not change them.

Return ONLY the code that goes between the two marker lines of region ${r.id} — no marker lines, no method signature, no
explanation — as one \`\`\`${lang} code block. Use only names that appear in the file or in the context below; do not invent
APIs, properties or overloads. Keep the file's code style and indentation.

## What the region must do (the author's instructions)
${instructions || '(none)'}

## The exact existing types and members you may use (context pack)
${pack}

## The whole file (the region is marked with <<<)
\`\`\`${lang}
${shown}
\`\`\`
`;
}

function extract(answer) {
  const blocks = [...answer.matchAll(/```[a-zA-Z]*\n([\s\S]*?)```/g)].map((m) => m[1]);
  return (blocks.length ? blocks.sort((a, b) => b.length - a.length)[0] : answer).replace(/\s+$/, '');
}
function railProblem(code) {
  if (!code.trim()) return 'empty answer';
  if (/QWEN-FILL-(BEGIN|END)/.test(code)) return 'the answer contains a marker';
  const stripped = code.replace(/"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`|\/\/.*$/gm, '');
  const balance = (open, close) => (stripped.split(open).length - 1) - (stripped.split(close).length - 1);
  if (balance('{', '}') !== 0) return `unbalanced braces (${balance('{', '}')})`;
  if (balance('(', ')') !== 0) return `unbalanced parentheses (${balance('(', ')')})`;
  return null;
}

function splice(r, code) { // the rail: only the lines strictly between the markers change
  const path = join(DIR, r.file);
  const lines = readFileSync(path, 'utf8').split('\n');
  const begin = lines.findIndex((l) => { const m = l.match(BEGIN); return m && m[2] === r.id; });
  const end = lines.findIndex((l, j) => j > begin && END(r.id).test(l));
  const body = code.split('\n');
  const minIndent = Math.min(...body.filter((l) => l.trim()).map((l) => l.match(/^\s*/)[0].length));
  const pad = minIndent < r.indent.length ? r.indent.slice(minIndent) : '';
  lines.splice(begin + 1, end - begin - 1, ...body.map((l) => (l.trim() ? pad + l : l)));
  writeFileSync(path, lines.join('\n'));
}

function ask(promptFiles) {
  const run = spawnSync('node', [ASK, '--no-think', ...promptFiles], {
    encoding: 'utf8', env: { ...process.env, COAI_EXP_VLLM_LOG: `${E}/vllm/requests-${arm}.jsonl` } });
  process.stdout.write(run.stdout); process.stderr.write(run.stderr);
}

const report = { arm, kind, started_utc: new Date().toISOString(), regions: [] };
for (let round = 1; round <= 2 && mine.some((r) => !r.done); round++) {
  const todo = mine.filter((r) => !r.done);
  const promptFiles = todo.map((r) => {
    const f = `${WORK}/prompts/${r.id}.r${round}.md`;
    writeFileSync(f, promptFor(r) + (round === 2 ? `\n\nYour previous answer was rejected: ${r.problem}. Return only the region's code.\n` : ''));
    return f;
  });
  ask(promptFiles);
  for (const [i, r] of todo.entries()) {
    const ans = promptFiles[i].replace(/\.md$/, '.answer.md');
    const code = existsSync(ans) ? extract(readFileSync(ans, 'utf8')) : '';
    r.problem = existsSync(ans) ? railProblem(code) : 'no answer';
    r.rounds = round;
    if (!r.problem) {
      splice(r, code);
      r.done = true; r.lines = code.split('\n').length;
      state.filled[r.id] = { file: r.file, kind, lines: r.lines, rounds: round, code };
      save();
    }
  }
}
for (const r of all.filter((x) => x.broken)) report.regions.push({ id: r.id, file: r.file, rejected: r.broken });
for (const r of mine) report.regions.push({ id: r.id, file: r.file, filled: !!r.done, rounds: r.rounds, lines: r.lines ?? 0, rejected: r.done ? null : r.problem });

// build, then the test classes of every file that holds a region of this kind
const lp = `${DIR}/.agents/conventions/tools/low-priority.mjs`;
const build = process.env.COAI_EXP_FILL_NO_BUILD === '1' ? { status: 0, stdout: '', stderr: '' } : spawnSync('node', [lp, 'dotnet', 'build', 'dew_flow_connect_other_ais.slnx', '-c', 'Debug', '-m:4', '--disable-build-servers'],
  { cwd: DIR, encoding: 'utf8', maxBuffer: 1 << 28 });
const errors = [...new Set((build.stdout + build.stderr).match(/error CS\d+:[^[]*/g) ?? [])];
report.build = { ok: build.status === 0, compileErrors: errors.length, errors: errors.slice(0, 40) };
const classes = [...new Set(all.filter((r) => r.kind === 'test' && r.file.startsWith('src_mcp/tests/')).map((r) => basename(r.file, '.cs')))];
if (build.status === 0 && classes.length) {
  const args = classes.flatMap((c) => ['--filter-class', `CoaiMcp.Tests.${c}`]);
  spawnSync('node', [lp, './src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests', ...args, '--results-directory', OUT,
    '--report-xunit-ctrf', '--report-xunit-ctrf-filename', `fill-${kind}.ctrf.json`], { cwd: DIR, encoding: 'utf8', maxBuffer: 1 << 28 });
  const ctrf = existsSync(`${OUT}/fill-${kind}.ctrf.json`) ? JSON.parse(readFileSync(`${OUT}/fill-${kind}.ctrf.json`, 'utf8')).results : null;
  report.tests = ctrf ? { summary: ctrf.summary, failed: ctrf.tests.filter((t) => t.status === 'failed').map((t) => ({ name: t.name.replace('CoaiMcp.Tests.', ''), message: (t.message ?? '').slice(0, 300) })) } : null;
}
// commit what the fill changed, file by file (never git add -A)
const changed = [...new Set(mine.filter((r) => r.done).map((r) => r.file))];
if (changed.length) {
  git('add', '--', ...changed);
  git('commit', '-q', '-m', `wip(${kind === 'test' ? 'tests' : 'code'}): fill the ${kind} regions of the skeleton`);
}
report.head = git('rev-parse', '--short', 'HEAD').trim();
report.ended_utc = new Date().toISOString();
writeFileSync(`${OUT}/fill-${kind}.json`, JSON.stringify(report, null, 2));
const filled = report.regions.filter((r) => r.filled).length;
console.log(`fill ${kind}: ${filled}/${mine.length} filled, rejected ${mine.length - filled}; build ${report.build.ok ? 'ok' : 'FAILED'} (${errors.length} compile errors); ` +
  (report.tests ? `tests ${report.tests.summary.passed} passed, ${report.tests.summary.failed} failed` : 'tests not run'));
