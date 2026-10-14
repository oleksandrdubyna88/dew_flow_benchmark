#!/usr/bin/env node
// canaries.mjs plant|unplant --manifest <runs-root>/canaries.json
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { plant, unplant } from './lib/canaries.mjs';

const SPEC = { manifest: { type: 'string', help: 'manifest path (written before planting)' } };
const [verb, ...rest] = process.argv.slice(2);
const { help, values } = parseCli(rest, SPEC, 'node canaries.mjs <plant|unplant> --manifest <runs-root>/canaries.json');
if (help || !['plant', 'unplant'].includes(verb)) console.log(help || 'usage: node canaries.mjs <plant|unplant> --manifest <file> (--help)');
else {
  requireValues(values, ['manifest']);
  const manifest = path.resolve(values.manifest);
  const rows = verb === 'plant' ? plant(manifest) : unplant(manifest);
  for (const r of rows) console.log(`${r.status}\t${r.path}`);
}
