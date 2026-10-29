#!/usr/bin/env node
// The two-step blind adjudication of held-out failures, per series directory (outside git: packets hold case text).
//
//   node spikes/minigit-bench/adjudicate.mjs packets  --series <dir>
//   node spikes/minigit-bench/adjudicate.mjs classify --series <dir> --blind <id> --verdict spec-ambiguity|not-ambiguous --reason <text>
//   node spikes/minigit-bench/adjudicate.mjs reveal   --series <dir>
//   node spikes/minigit-bench/adjudicate.mjs class    --series <dir> --blind <id> --class product|harness --reason <text>
//   node spikes/minigit-bench/adjudicate.mjs revised  --series <dir>
//
// Exit codes: 0 done | 1 refused (a missing verdict, an unknown id, packets already exist) | 4 usage | 5 the tool itself failed.
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { classifyAmbiguity, classifyOrigin, packetsFor, revealFor, revisedFor } from './lib/adjudicate.mjs';

const OPTIONS = { series: { type: 'string' }, blind: { type: 'string' }, verdict: { type: 'string' }, class: { type: 'string' }, reason: { type: 'string' } };
const EXIT = Object.freeze({ ok: 0, refused: 1, usage: 4, broke: 5 });

const needs = (o, names) => names.filter(n => !o[n]);

const COMMANDS = Object.freeze({
  packets: { need: ['series'], run: (o, write) => {
    const r = packetsFor({ series: o.series });
    if (!r.ok) return r;
    write('masking rules applied to every packet:');
    r.value.rules.forEach(rule => write(`  ${rule.name} -> ${rule.replacement}`));
    write('  stderr beyond one plain line, or whose first line needed masking -> <runtime output>');
    write('  exit codes other than 0 and 1 -> <abnormal>');
    write('the blind judge reads only <series>/adjudication-packets/; the map and the verdicts are in <series>/adjudication/');
    r.value.blindIds.forEach(id => write(`packet ${id}`));
    r.value.skipped.forEach(s => write(`skipped cell ${s.cell}: ${s.why}`));
    return r;
  } },
  classify: { need: ['series', 'blind', 'verdict', 'reason'], run: o => classifyAmbiguity({ series: o.series, blindId: o.blind, verdict: o.verdict, reason: o.reason }) },
  reveal: { need: ['series'], run: (o, write) => {
    const r = revealFor({ series: o.series });
    if (r.ok) r.value.forEach(e => write(`${e.blindId}  ${e.caseId}  ${e.cell}  arm ${e.arm}  stack ${e.stack}  code ${e.codePath}`));
    return r;
  } },
  class: { need: ['series', 'blind', 'class', 'reason'], run: o => classifyOrigin({ series: o.series, blindId: o.blind, klass: o.class, reason: o.reason }) },
  revised: { need: ['series'], run: (o, write) => {
    const r = revisedFor({ series: o.series });
    if (r.ok) {
      write(`dropped cases (ambiguous in any cell): ${r.value.droppedCases.join(', ') || 'none'}`);
      r.value.cells.forEach(c => write(`${c.cell}: ${c.known ? JSON.stringify(c.totals) : 'unknown'} primary100=${c.primary100}`));
    }
    return r;
  } },
});

/** The whole command; `write` receives each output line. Resolves to the exit code. */
export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  let parsed;
  try {
    parsed = parseArgs({ args: argv, options: OPTIONS, allowPositionals: true });
  } catch (e) {
    if (!e.code?.startsWith('ERR_PARSE_ARGS')) throw e;
    write(`usage error: ${e.message}`);
    return EXIT.usage;
  }
  const command = COMMANDS[parsed.positionals[0]];
  if (parsed.positionals.length !== 1 || !command) { write(`usage error: one command of ${Object.keys(COMMANDS).join(', ')}`); return EXIT.usage; }
  const values = parsed.values.series ? { ...parsed.values, series: path.resolve(parsed.values.series) } : parsed.values; // normalised once, for every command
  const missing = needs(values, command.need);
  if (missing.length > 0) { write(`usage error: ${parsed.positionals[0]} needs --${missing.join(', --')}`); return EXIT.usage; }
  try {
    const r = command.run(values, write);
    if (!r.ok) write(r.reason);
    else if (parsed.positionals[0] !== 'packets' && parsed.positionals[0] !== 'reveal' && parsed.positionals[0] !== 'revised') write('recorded');
    return r.ok ? EXIT.ok : EXIT.refused;
  } catch (e) {
    write(`adjudicate failed: ${e.message}`);
    return EXIT.broke;
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = await main(process.argv.slice(2));
}
