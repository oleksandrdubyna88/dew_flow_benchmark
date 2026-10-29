#!/usr/bin/env node
// E1.S3 step 0: `run` (the one paid session), `sanitise` (raw capture -> public fixture, fail-closed) and `verdicts` (CAPTURE.md).
//   node capture/capture-session.mjs run --out <dir outside every git tree> [--dry-run]
//   node capture/capture-session.mjs sanitise --from <that dir> --to <fixture dir>
//   node capture/capture-session.mjs verdicts --fixture <fixture dir>
// Exit codes: 0 done - 3 environment (no token, claude missing, bad out dir) - 4 usage - 5 the capture or the sanitise itself failed.
import { writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { crosscheckPolicyFrom } from './policy-input.mjs';
import { capturePlan } from './capture-plan.mjs';
import { outProblems, readToken, tokenStatus } from './capture-env.mjs';
import { findOnPath } from '../lib/find-on-path.mjs';
import { loadCapture, sanitiseCapture, sha256File } from './capture-files.mjs';
import { runCapture } from './capture-run.mjs';
import { computeVerdicts, decisionsOf, renderCapture } from './verdicts.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const DRY_RUN_PORT = 49152;
const OK = 0;
const ENVIRONMENT = 3;
const USAGE = 4;
const FAILED = 5;

const say = (stream, text) => stream.write(`${text}\n`);

function parse(argv, options) {
  try {
    return { ok: true, values: parseArgs({ args: argv, options, strict: true, allowPositionals: false }).values };
  } catch (e) {
    return { ok: false, reason: e.message };
  }
}

function dryRun({ out, env, home, io }) {
  const token = tokenStatus({ home });
  const planned = capturePlan({ out, spikeDir: SPIKE, port: DRY_RUN_PORT, token: '<not read in a dry run>', env });
  if (!planned.ok) { say(io.err, planned.reason); return ENVIRONMENT; }
  const plan = planned.value;
  const shown = { ...plan.env, CLAUDE_CODE_OAUTH_TOKEN: token.present ? '<set, not shown>' : '<absent>', OTEL_EXPORTER_OTLP_ENDPOINT: 'http://127.0.0.1:<port assigned at launch>' };
  const claudeAt = findOnPath('claude', env.PATH);
  say(io.out, JSON.stringify({
    exe: plan.exe, argv: plan.argv, cwd: plan.cwd, env: shown, envKeys: plan.envKeys, stdin: plan.stdin, timeoutMs: plan.timeoutMs,
    files: Object.keys(plan.files), tokenSource: token.present ? token.source : 'none', tokenProblem: token.present ? null : token.why, claudeFound: claudeAt !== null, claudeAt, outProblems: outProblems(out, { home }),
  }, null, 2));
  return OK;
}

async function run({ values, env, io }) {
  const home = os.homedir();
  const out = path.resolve(values.out);
  if (values['dry-run']) return dryRun({ out, env, home, io });
  const token = readToken({ home });
  if (!token.ok) { say(io.err, token.reason); return ENVIRONMENT; }
  const claude = findOnPath('claude', env.PATH);
  if (!claude) { say(io.err, 'the claude executable is not on PATH'); return ENVIRONMENT; }
  const problems = outProblems(out, { home });
  if (problems.length > 0) { problems.forEach(p => say(io.err, p)); return ENVIRONMENT; }
  say(io.out, `capturing into ${out} (progress: ${path.join(out, 'capture.log')}); this is the one paid session and can take minutes`);
  const result = await runCapture({ out, spikeDir: SPIKE, exe: claude, token: token.value, env });
  if (!result.ok) {
    say(io.err, `capture failed: ${result.reason} (artefacts, if any, are in ${out})`);
    say(io.out, `capture status: failed (exit ${FAILED}); see ${path.join(out, 'capture.log')}`);
    return FAILED;
  }
  say(io.out, `captured into ${out}: claude ${result.value.meta.cliVersion}, cost $${result.value.costUsd} (count it as pilot spend), ${result.value.transcripts.length} transcript file(s)`);
  say(io.out, `capture status: ok (exit ${OK})`);
  return OK;
}

function sanitise({ values, env, io }) {
  const token = readToken({ home: os.homedir() });
  const result = sanitiseCapture({ from: path.resolve(values.from), to: path.resolve(values.to), token: token.ok ? token.value : null });
  if (result.ok) { say(io.out, `sanitised ${result.value.files.length} file(s) into ${values.to}`); return OK; }
  say(io.err, result.reason);
  for (const f of result.findings ?? []) say(io.err, `  ${f.file}: ${f.pattern}`);
  return FAILED;
}

function verdicts({ values, io }) {
  const dir = path.resolve(values.fixture);
  const capture = loadCapture(dir);
  if (!capture.ok) { say(io.err, capture.reason); return FAILED; }
  const inputs = crosscheckPolicyFrom(SPIKE, values);
  if (!inputs.ok) { say(io.err, inputs.reason); return ENVIRONMENT; }
  const rows = computeVerdicts(capture.value, inputs.value);
  const command = 'node spikes/minigit-bench/capture/capture-session.mjs run --out ~/rsd/minigit/capture/<UTC stamp>';
  const md = renderCapture({ meta: capture.value.meta, argv: capture.value.argv, command, sanitiserSha256: sha256File(path.join(SPIKE, 'capture', 'sanitise.mjs')), rows });
  writeFileSync(path.join(dir, 'CAPTURE.md'), md);
  const d = decisionsOf(rows);
  say(io.out, `wrote ${path.join(dir, 'CAPTURE.md')}: ${rows.map(r => `${r.id} ${r.verdict}`).join(', ')}`);
  say(io.out, `decisions: join=${d.join} t3OutputSource=${d.t3OutputSource ?? 'undecided'} OTEL_STAMP_MARKS=${d.stampMarks}`);
  const policy = inputs.value.policy;
  if (policy.join !== d.join || policy.t3OutputSource !== d.t3OutputSource) say(io.out, 'the policy file differs from these decisions: change inputs/crosscheck-policy.json in a dated commit');
  return OK;
}

const VERBS = Object.freeze({
  run: { options: { out: { type: 'string' }, 'dry-run': { type: 'boolean' } }, need: ['out'], fn: run },
  sanitise: { options: { from: { type: 'string' }, to: { type: 'string' } }, need: ['from', 'to'], fn: sanitise },
  verdicts: { options: { fixture: { type: 'string' }, prices: { type: 'string' }, policy: { type: 'string' } }, need: ['fixture'], fn: verdicts },
});

/** The CLI as a function: exit code out, nothing thrown for a usage error. */
export async function main(argv, { env = process.env, io = { out: process.stdout, err: process.stderr } } = {}) {
  const [verb, ...rest] = argv;
  const spec = VERBS[verb];
  if (!spec) { say(io.err, `usage: capture-session.mjs ${Object.keys(VERBS).join('|')} ...`); return USAGE; }
  const parsed = parse(rest, spec.options);
  if (!parsed.ok) { say(io.err, parsed.reason); return USAGE; }
  const missing = spec.need.filter(k => !parsed.values[k]);
  if (missing.length > 0) { say(io.err, `${verb} needs ${missing.map(k => `--${k}`).join(' ')}`); return USAGE; }
  return spec.fn({ values: parsed.values, env, io });
}

if (process.argv[1] === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
