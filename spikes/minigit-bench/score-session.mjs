#!/usr/bin/env node
// Scores one finished session and writes <session>/score/score.json.
//
//   node spikes/minigit-bench/score-session.mjs --session <dir> --source <dir> --arm process|bare --stack csharp|node --phase v1|v2
//        --toolchain <dir>... [--nuget-cache <dir>] [--npm-cache <dir>] [--inputs-repo <url|path>]
//
// Exit codes (the repository's CLI contract): 0 scored (whatever the score) | 3 environment (resumable): bwrap not on PATH, a toolchain
// or cache directory absent, the scoring box cannot start, another scoring of the session in progress, the pinned inputs unreachable or
// not matching hashes.txt | 4 configuration: a usage error, a missing --source | 5 nothing produced: the scorer itself failed.
import './lib/lower-priority.mjs';
import { existsSync, mkdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { findOnPath } from './lib/bwrap.mjs';
import { fetchInputs } from './lib/fetch-inputs.mjs';
import { defaultDeps } from './lib/score-deps.mjs';
import { runScoring, scoringPlan } from './lib/score-pipeline.mjs';

const OPTIONS = {
  session: { type: 'string' }, source: { type: 'string' }, arm: { type: 'string' }, stack: { type: 'string' }, phase: { type: 'string' },
  toolchain: { type: 'string', multiple: true }, 'nuget-cache': { type: 'string' }, 'npm-cache': { type: 'string' }, 'inputs-repo': { type: 'string' },
};

/** argv -> ok(options) | fail(usage message) */
export function parseCli(argv) {
  let parsed;
  try {
    parsed = parseArgs({ args: argv, options: OPTIONS, allowPositionals: false }).values;
  } catch (e) {
    if (e.code?.startsWith('ERR_PARSE_ARGS')) return { ok: false, reason: e.message };
    throw e;
  }
  const missing = ['session', 'source', 'arm', 'stack', 'phase'].filter(k => !parsed[k]);
  if (missing.length > 0) return { ok: false, reason: `missing --${missing.join(', --')}` };
  if (!parsed.toolchain?.length) return { ok: false, reason: 'at least one --toolchain <dir> is required' };
  const resolve = p => (p ? path.resolve(p) : undefined);
  return { ok: true, value: { ...parsed, toolchain: parsed.toolchain.map(p => path.resolve(p)), 'nuget-cache': resolve(parsed['nuget-cache']), 'npm-cache': resolve(parsed['npm-cache']) } };
}

const EXIT = Object.freeze({ environment: 3, configuration: 4 });

/** The environment this command needs, or the first thing missing: bwrap on PATH (as spawn finds it), toolchains, caches. */
function missingEnvironment(o) {
  const absent = [...o.toolchain, o['nuget-cache'], o['npm-cache']].filter(p => p && !existsSync(p));
  if (findOnPath('bwrap') === null) return 'bwrap is not on PATH';
  return absent.length > 0 ? `missing ${absent.join(', ')}` : null;
}

/** The whole command; `write` receives each output line. Resolves to the exit code. */
export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  const cli = parseCli(argv);
  if (!cli.ok) { write(`usage error: ${cli.reason}`); return 4; }
  const o = cli.value;
  const planned = scoringPlan({ arm: o.arm, stack: o.stack, phase: o.phase, sourceDir: path.resolve(o.source), sessionDir: path.resolve(o.session),
    nugetCache: o['nuget-cache'], npmCache: o['npm-cache'] });
  if (!planned.ok) { write(`usage error: ${planned.reason}`); return 4; }
  if (!statSync(planned.value.sourceDir, { throwIfNoEntry: false })?.isDirectory()) { write(`usage error: --source ${planned.value.sourceDir} is not a directory`); return 4; }
  const missing = missingEnvironment(o);
  if (missing) { write(`environment: ${missing}`); return 3; }
  const fetched = await fetchInputs({ repoUrl: o['inputs-repo'] });
  if (!fetched.ok) { write(`environment: ${fetched.reason}`); return 3; }
  let scored;
  try {
    scored = await runScoring(planned.value, defaultDeps({ inputs: fetched.value, toolchainDirs: o.toolchain }));
  } catch (e) {
    write(`scorer failed: ${e.message}`);
    return 5;
  }
  if (!scored.ok) { write(`${EXIT[scored.kind] === 3 ? 'environment' : 'scorer failed'}: ${scored.reason}`); return EXIT[scored.kind] ?? 5; }
  scored.value.trace.forEach(write);
  const out = path.join(planned.value.scoreDir, 'score.json');
  mkdirSync(planned.value.scoreDir, { recursive: true });
  writeFileSync(out, `${JSON.stringify(scored.value, null, 2)}\n`);
  write(`score written: ${out}`);
  return 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = await main(process.argv.slice(2));
}
