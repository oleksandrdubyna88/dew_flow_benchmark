#!/usr/bin/env node
// Read a finished cell's stream + audit log and write <cell>/summary.json.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { DEFAULT_RUNS_ROOT } from './lib/consts.mjs';
import { parseNdjson, summarize } from './lib/summary.mjs';

const SPEC = {
  cell: { type: 'string', help: 'A1 | B | A2' },
  'runs-root': { type: 'string', default: DEFAULT_RUNS_ROOT, help: 'runs root' },
  'canary-tokens': { type: 'string', help: 'canaries.json manifest, or a text file with one token per line' },
};

const readIf = file => (fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '');

/** Tokens from a canaries manifest ({planted:[{token}]}) or a plain one-per-line file. */
export function readTokens(file) {
  if (!file) return [];
  const text = readIf(file);
  try {
    const json = JSON.parse(text);
    return (json.planted ?? json.entries ?? []).map(e => e.token).filter(Boolean);
  } catch {
    return text.split('\n').map(l => l.trim()).filter(Boolean);
  }
}

function main(v) {
  const cellDir = path.join(path.resolve(v['runs-root']), v.cell);
  const stream = readIf(path.join(cellDir, 'stream.ndjson'));
  const auditText = readIf(path.join(cellDir, 'policy-audit.ndjson')) || readIf(path.join(cellDir, 'tree', '.git', 'policy-audit.ndjson'));
  const cellJson = JSON.parse(readIf(path.join(cellDir, 'cell.json')) || '{}');
  const summary = summarize({
    events: parseNdjson(stream), audit: parseNdjson(auditText), cellJson,
    treeDir: path.join(cellDir, 'tree'), tokens: readTokens(v['canary-tokens']),
    rawTexts: [{ source: 'stream', text: stream }, { source: 'audit', text: auditText }, { source: 'stderr', text: readIf(path.join(cellDir, 'stderr.txt')) }],
  });
  const run = JSON.parse(readIf(path.join(cellDir, 'run.json')) || '{}');
  fs.writeFileSync(path.join(cellDir, 'summary.json'), `${JSON.stringify({ ...summary, run }, null, 2)}\n`);
  console.log(`summary written: ${path.join(cellDir, 'summary.json')}`);
}

if (process.argv[1]?.endsWith('summarize-cell.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node summarize-cell.mjs --cell <A1|B|A2> [--runs-root dir] [--canary-tokens file]');
  if (help) console.log(help);
  else {
    requireValues(values, ['cell']);
    main(values);
  }
}
