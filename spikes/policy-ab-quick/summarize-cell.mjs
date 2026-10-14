#!/usr/bin/env node
// Read a finished cell's stream + audit log and write <cell>/summary.json.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { DEFAULT_RUNS_ROOT, INPUTS_DIR } from './lib/consts.mjs';
import { readRunMap } from './lib/runmap.mjs';
import { parseNdjson, summarize } from './lib/summary.mjs';

const SPEC = {
  cell: { type: 'string', help: 'c1 | c2 | c3' },
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
  const read = name => readIf(path.join(cellDir, name));
  const auditText = read('audit.ndjson') || readIf(path.join(cellDir, 'tree', '.git', 'audit.ndjson'));
  const cellJson = JSON.parse(read('cell.json') || '{}');
  const run = JSON.parse(read('run.json') || '{}');
  const reference = JSON.parse(readIf(path.join(INPUTS_DIR, 'reference-only-tokens.json')) || '{}');
  const summary = summarize({
    events: parseNdjson(read('stream.ndjson')), audit: parseNdjson(auditText), cellJson,
    treeDir: path.join(cellDir, 'tree'), canaryTokens: readTokens(v['canary-tokens']),
    reference: { identifiers: reference.identifiers ?? [], phrases: reference.phrases ?? [] },
    excludedFiles: run.excludedFiles ?? [],
    texts: { stream: read('stream.ndjson'), audit: auditText, stderr: read('stderr.txt'), candidateDiff: read('candidate.diff'), excludedDiff: read('excluded.diff') },
  });
  const arm = readRunMap().cells?.[v.cell]?.arm ?? null;
  fs.writeFileSync(path.join(cellDir, 'summary.json'), `${JSON.stringify({ ...summary, arm, run }, null, 2)}\n`);
  console.log(`summary written: ${path.join(cellDir, 'summary.json')}`);
}

if (process.argv[1]?.endsWith('summarize-cell.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node summarize-cell.mjs --cell <c1|c2|c3> [--runs-root dir] [--canary-tokens file]');
  if (help) console.log(help);
  else {
    requireValues(values, ['cell']);
    main(values);
  }
}
