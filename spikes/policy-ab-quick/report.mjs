#!/usr/bin/env node
// Combine each cell's summary.json + score.json into research/policy-ab/quick-pilot-data.json.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseCli } from './lib/args.mjs';
import { ARM_SHAS, DEFAULT_RUNS_ROOT, P, REPO_ROOT, S } from './lib/consts.mjs';
import { pathVariants, scrub } from './lib/scrub.mjs';

const SPEC = {
  'runs-root': { type: 'string', default: DEFAULT_RUNS_ROOT, help: 'runs root' },
  out: { type: 'string', help: 'output file (default research/policy-ab/quick-pilot-data.json)' },
};
const CELLS = ['A1', 'B', 'A2', 'reference'];

const readJson = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null);

/** Pure: one cell's record from its parts. */
export function cellRecord(name, summary, score, cellJson) {
  return { cell: name, arm: cellJson?.arm ?? null, armSha: cellJson?.armSha ?? null, overlay: cellJson?.overlay ?? null, summary, score };
}

function replacementsFor(root) {
  const rootName = path.basename(root);
  return [...pathVariants(root).map(v => [v, `<${rootName}>`]), ...pathVariants(os.homedir()).map(v => [v, '<home>'])];
}

function main(v) {
  const root = path.resolve(v['runs-root']);
  const cells = CELLS.map(name => {
    const dir = path.join(root, name);
    return cellRecord(name, readJson(path.join(dir, 'summary.json')), readJson(path.join(dir, 'score.json')), readJson(path.join(dir, 'cell.json')));
  }).filter(c => c.summary || c.score);
  const data = scrub({ generatedUtc: new Date().toISOString(), story: { repo: 'dew_flow_creds_for_devs', pr: 176, P, S }, arms: ARM_SHAS, cells }, replacementsFor(root));
  const out = v.out ? path.resolve(v.out) : path.join(REPO_ROOT, 'research', 'policy-ab', 'quick-pilot-data.json');
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, `${JSON.stringify(data, null, 2)}\n`);
  console.log(`wrote ${out} (${cells.length} cells)`);
}

if (process.argv[1]?.endsWith('report.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node report.mjs [--runs-root dir] [--out file]');
  if (help) console.log(help);
  else main(values);
}
