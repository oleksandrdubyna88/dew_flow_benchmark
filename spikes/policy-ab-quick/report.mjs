#!/usr/bin/env node
// Combine each cell's summary.json + score.json into research/policy-ab/quick-pilot-data.json.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli } from './lib/args.mjs';
import { ARM_SHAS, REPO_ROOT } from './lib/consts.mjs';
import { readRunMap } from './lib/runmap.mjs';
import { ownSuiteVsReference } from './lib/ownsuite.mjs';
import { machineScrubber, scrubStrings } from './lib/scrub.mjs';
import { loadStory, runsRootOf, STORY_OPTION } from './lib/story.mjs';

const SPEC = {
  story: STORY_OPTION,
  'runs-root': { type: 'string', help: "runs root (default: the story's defaultRunsRoot)" },
  out: { type: 'string', help: 'output file (default research/bench/quick-pilot-data.json; other stories: research/bench/policy-ab-<story>-data.json)' },
};
const CELLS = ['c1', 'c2', 'c3', 'c4', 'c5', 'c6', 'c7', 'c8', 'c9', 'reference'];

const readJson = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null);

/** Pure: one cell's record from its parts. */
export function cellRecord(name, summary, score, cellJson, mapEntry) {
  return { cell: name, arm: mapEntry?.arm ?? null, armSha: mapEntry?.armSha ?? null, overlay: cellJson?.overlay ?? null, summary, score };
}

function main(v) {
  const story = loadStory(v.story);
  const root = runsRootOf(v, story);
  const runMap = readRunMap(story.runMapFile);
  const reference = readJson(path.join(root, 'reference', 'score.json'))?.ownSuite;
  const cells = CELLS.map(name => {
    const dir = path.join(root, name);
    const record = cellRecord(name, readJson(path.join(dir, 'summary.json')), readJson(path.join(dir, 'score.json')), readJson(path.join(dir, 'cell.json')), runMap.cells?.[name]);
    return name === 'reference' || !record.score ? record : { ...record, ownSuiteVsReference: ownSuiteVsReference(record.score.ownSuite, reference) };
  }).filter(c => c.summary || c.score);
  const data = scrubStrings({ generatedUtc: new Date().toISOString(), seed: runMap.seed ?? null, order: runMap.order ?? null, blocks: runMap.blocks ?? null, story: { id: story.id, repo: story.repo, pr: story.pr, P: story.P, S: story.S, layout: story.layout }, arms: ARM_SHAS, cells }, machineScrubber({ runsRoot: root, creds: story.repoClone, label: story.scrubLabel }));
  const out = v.out ? path.resolve(v.out) : path.join(REPO_ROOT, 'research', 'bench', story.id === 'creds-176' ? 'quick-pilot-data.json' : `policy-ab-${story.id}-data.json`);
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, `${JSON.stringify(data, null, 2)}\n`);
  console.log(`wrote ${out} (${cells.length} cells)`);
}

if (process.argv[1]?.endsWith('report.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node report.mjs [--story creds-176] [--runs-root dir] [--out file]');
  if (help) console.log(help);
  else main(values);
}
