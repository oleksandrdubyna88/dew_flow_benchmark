#!/usr/bin/env node
// Combine each cell's summary.json + score.json into research/policy-ab/quick-pilot-data.json.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli } from './lib/args.mjs';
import { ARM_SHAS, REPO_ROOT } from './lib/consts.mjs';
import { readRunMap } from './lib/runmap.mjs';
import { allVoid, outcomeOfCell, reportFindings, voidSpend } from './lib/findings.mjs';
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
  return { cell: name, arm: mapEntry?.arm ?? null, armSha: mapEntry?.armSha ?? null, outcome: outcomeOfCell(mapEntry, summary), attempts: mapEntry?.attempts ?? [], crash: mapEntry?.crash ?? null, overlay: mapEntry?.private?.overlay ?? cellJson?.overlay ?? null, summary, score };
}

/** Pure (readers injected): the whole report object - cells with their outcome, the series stop, and the findings. */
export function assembleReport({ story, runMap, root, read = readJson, now = new Date().toISOString() }) {
  const reference = read(path.join(root, 'reference', 'score.json'))?.ownSuite;
  const cells = CELLS.map(name => {
    const dir = path.join(root, name);
    const record = cellRecord(name, read(path.join(dir, 'summary.json')), read(path.join(dir, 'score.json')), read(path.join(dir, 'cell.json')), runMap.cells?.[name]);
    return name === 'reference' || !record.score ? record : { ...record, ownSuiteVsReference: ownSuiteVsReference(record.score.ownSuite, reference) };
  }).filter(c => c.summary || c.score || c.outcome);
  const stop = runMap.stop ?? null;
  return {
    generatedUtc: now, seed: runMap.seed ?? null, order: runMap.order ?? null, blocks: runMap.blocks ?? null, stop,
    story: { id: story.id, repo: story.repo, pr: story.pr, P: story.P, S: story.S, layout: story.layout }, arms: ARM_SHAS, cells,
    voidSpendUsd: voidSpend(cells), resumes: runMap.resumes ?? [],
    findings: reportFindings({ cells, stop }),
  };
}

/**
 * Where the report goes. A report whose every cell is void is not a result: it never overwrites research/bench/<story>-data.json (the file a
 * real run produces); it is written into the runs root instead.
 */
export function reportTarget({ out, story, report, root }) {
  if (out) return path.resolve(out);
  if (allVoid(report.cells)) return path.join(root, 'report-void.json');
  return path.join(REPO_ROOT, 'research', 'bench', story.id === 'creds-176' ? 'quick-pilot-data.json' : `policy-ab-${story.id}-data.json`);
}

function main(v) {
  const story = loadStory(v.story);
  const root = runsRootOf(v, story);
  const runMap = readRunMap(story.runMapFile);
  const report = assembleReport({ story, runMap, root });
  const cells = report.cells;
  const data = scrubStrings(report, machineScrubber({ runsRoot: root, creds: story.repoClone, label: story.scrubLabel }));
  const out = reportTarget({ out: v.out, story, report, root });
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, `${JSON.stringify(data, null, 2)}\n`);
  console.log(`wrote ${out} (${cells.length} cells)`);
}

if (process.argv[1]?.endsWith('report.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node report.mjs [--story creds-176] [--runs-root dir] [--out file]');
  if (help) console.log(help);
  else main(values);
}
