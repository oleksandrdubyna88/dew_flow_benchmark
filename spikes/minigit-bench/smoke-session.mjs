#!/usr/bin/env node
// E2.S2 step 7: the bare-session smoke, its sanitising and its verdicts.
//   node smoke-session.mjs --out <dir outside every git tree> [--dry-run]     the one paid session (cap $0.20)
//   node smoke-session.mjs sanitise --from <that dir> --to <fixture dir>      fail-closed copy for the repository
//   node smoke-session.mjs verdicts --fixture <fixture dir>                   writes SMOKE.md (computed, never typed)
// Exit codes: 0 done - 3 environment (no token, preflight failed, bad out dir) - 4 usage - 5 the session or the sanitise failed - 130/143 cancelled.
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { outProblems } from './capture/capture-env.mjs';
import { sanitiseCapture } from './capture/capture-files.mjs';
import { crosscheckPolicyFrom } from './capture/policy-input.mjs';
import { fail, ok } from './lib/result.mjs';
import { SPIKE, runSmoke, smokeInputs } from './lib/smoke-run.mjs';
import { loadSmoke, stageSmoke } from './lib/smoke-load.mjs';
import { computeSmokeVerdicts, renderSmoke } from './lib/smoke-verdicts.mjs';
import { readSecretFile } from './lib/secrets.mjs';
import { TOKEN_FILE_PARTS } from './capture/capture-env.mjs';

const OK = 0;
const ENVIRONMENT = 3;
const USAGE = 4;
const FAILED = 5;
const say = (stream, text) => stream.write(`${text}\n`);

function parse(argv, options) {
  try {
    return ok(parseArgs({ args: argv, options, strict: true, allowPositionals: false }).values);
  } catch (e) {
    return fail(e.message);
  }
}

async function run({ values, io }) {
  const home = os.homedir();
  const out = path.resolve(values.out);
  const problems = outProblems(out, { home });
  if (problems.length > 0) { problems.forEach(p => say(io.err, p)); return ENVIRONMENT; }
  const inputs = smokeInputs({ home });
  if (!inputs.ok) { say(io.err, inputs.reason); return ENVIRONMENT; }
  if (values['dry-run']) {
    say(io.out, JSON.stringify({ out, claude: inputs.value.claudeBinary, pins: inputs.value.stacks.pins, tsDir: inputs.value.tsDir, capUsd: 0.2, token: 'present, mode checked, not shown' }, null, 2));
    return OK;
  }
  const ac = new AbortController();
  let signalled = null;
  for (const [name, code] of [['SIGINT', 130], ['SIGTERM', 143]]) process.once(name, () => { signalled = code; ac.abort(); });
  const r = await runSmoke({ out, inputs: inputs.value, home, signal: ac.signal, say: t => say(io.out, t) });
  if (!r.ok) { say(io.err, r.reason); return r.exit ?? FAILED; }
  const { result, layout } = r.value;
  if (!result.ok) { say(io.err, `the session failed: ${result.reason} (artefacts, if any, are in ${layout.dir})`); return FAILED; }
  const m = result.value.meta;
  say(io.out, `smoke ${m.outcome.label}: envelope $${m.envelopeCostUsd}, broker $${m.broker.spentUsd}, wall ${m.wallMs} ms, stopped: ${m.stopped ?? 'no'}; artefacts in ${layout.dir}`);
  if (signalled !== null) return signalled;
  return m.outcome.ok ? OK : FAILED;
}

function sanitise({ values, io }) {
  const staging = mkdtempSync(path.join(path.resolve(values.from), '..', '.smoke-stage-'));
  try {
    const staged = stageSmoke(path.resolve(values.from), staging);
    if (!staged.ok) { say(io.err, staged.reason); return FAILED; }
    const token = readSecretFile({ home: os.homedir(), parts: TOKEN_FILE_PARTS, label: 'token' });
    const result = sanitiseCapture({ from: staging, to: path.resolve(values.to), token: token.ok ? token.value : null, optional: staged.value.filter(f => !f.startsWith('transcripts')) });
    if (result.ok) { say(io.out, `sanitised ${result.value.files.length} file(s) into ${values.to}`); return OK; }
    say(io.err, result.reason);
    for (const f of result.findings ?? []) say(io.err, `  ${f.file}: ${f.pattern}`);
    return FAILED;
  } finally {
    rmSync(staging, { recursive: true, force: true });
  }
}

function verdicts({ values, io }) {
  const dir = path.resolve(values.fixture);
  const smoke = loadSmoke(dir);
  if (!smoke.ok) { say(io.err, smoke.reason); return FAILED; }
  const inputs = crosscheckPolicyFrom(SPIKE, values);
  if (!inputs.ok) { say(io.err, inputs.reason); return ENVIRONMENT; }
  const rows = computeSmokeVerdicts(smoke.value, inputs.value);
  const command = 'node spikes/minigit-bench/smoke-session.mjs --out ~/rsd/minigit/smoke/<UTC stamp>';
  writeFileSync(path.join(dir, 'SMOKE.md'), renderSmoke({ meta: smoke.value.meta, rows, command }));
  say(io.out, `wrote ${path.join(dir, 'SMOKE.md')}: ${rows.map(r => `${r.id} ${r.verdict}`).join(', ')}`);
  return OK;
}

const VERBS = Object.freeze({
  run: { options: { out: { type: 'string' }, 'dry-run': { type: 'boolean' } }, need: ['out'], fn: run },
  sanitise: { options: { from: { type: 'string' }, to: { type: 'string' } }, need: ['from', 'to'], fn: sanitise },
  verdicts: { options: { fixture: { type: 'string' }, prices: { type: 'string' }, policy: { type: 'string' } }, need: ['fixture'], fn: verdicts },
});

/** The CLI as a function: exit code out, nothing thrown for a usage error. A bare `--out ...` is the `run` verb. */
export async function main(argv, { io = { out: process.stdout, err: process.stderr } } = {}) {
  const [first, ...rest] = argv;
  const verb = VERBS[first] ? first : 'run';
  const spec = VERBS[verb];
  const parsed = parse(VERBS[first] ? rest : argv, spec.options);
  if (!parsed.ok) { say(io.err, `${parsed.reason}\nusage: smoke-session.mjs [run] --out <dir> [--dry-run] | sanitise --from <dir> --to <dir> | verdicts --fixture <dir>`); return USAGE; }
  const missing = spec.need.filter(k => !parsed.value[k]);
  if (missing.length > 0) { say(io.err, `${verb} needs ${missing.map(k => `--${k}`).join(' ')}`); return USAGE; }
  return spec.fn({ values: parsed.value, io });
}

if (process.argv[1] === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
