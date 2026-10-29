#!/usr/bin/env node
// Scores one finished session and writes <session>/score/score.json.
//
//   node spikes/minigit-bench/score-session.mjs --session <dir> --source <dir> --arm process|bare --stack csharp|node --phase v1|v2
//        --toolchain <dir>... [--cache <dir>...] [--inputs-repo <url|path>]
//
// Exit codes (the repository's CLI contract): 0 scored (whatever the score) | 3 environment: bwrap missing, a toolchain absent, the
// pinned inputs unreachable or not matching hashes.txt (resumable) | 4 configuration: a usage error | 5 nothing produced: the scorer failed.
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { BWRAP_PATH } from './lib/bwrap.mjs';
import { fetchInputs } from './lib/fetch-inputs.mjs';
import { defaultDeps } from './lib/score-deps.mjs';
import { runScoring, scoringPlan } from './lib/score-pipeline.mjs';

const OPTIONS = {
  session: { type: 'string' }, source: { type: 'string' }, arm: { type: 'string' }, stack: { type: 'string' }, phase: { type: 'string' },
  toolchain: { type: 'string', multiple: true }, cache: { type: 'string', multiple: true }, 'inputs-repo': { type: 'string' },
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
  return { ok: true, value: { ...parsed, toolchain: parsed.toolchain.map(p => path.resolve(p)), cache: (parsed.cache ?? []).map(p => path.resolve(p)) } };
}

/** The whole command; `write` receives each output line. Resolves to the exit code. */
export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  const cli = parseCli(argv);
  if (!cli.ok) { write(`usage error: ${cli.reason}`); return 4; }
  const o = cli.value;
  const planned = scoringPlan({ arm: o.arm, stack: o.stack, phase: o.phase, sourceDir: path.resolve(o.source), sessionDir: path.resolve(o.session) });
  if (!planned.ok) { write(`usage error: ${planned.reason}`); return 4; }
  const absent = [BWRAP_PATH, ...o.toolchain].filter(p => !existsSync(p));
  if (absent.length > 0) { write(`environment: missing ${absent.join(', ')}`); return 3; }
  const fetched = await fetchInputs({ repoUrl: o['inputs-repo'] });
  if (!fetched.ok) { write(`environment: ${fetched.reason}`); return 3; }
  let scored;
  try {
    scored = await runScoring(planned.value, defaultDeps({ inputs: fetched.value, toolchainDirs: o.toolchain, cacheDirs: o.cache }));
  } catch (e) {
    write(`scorer failed: ${e.message}`);
    return 5;
  }
  if (!scored.ok) { write(`scorer failed: ${scored.reason}`); return 5; }
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
