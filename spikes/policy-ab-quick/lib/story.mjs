// A story = one merged change re-run from its pre-implementation spec. Everything that differs between stories
// lives in stories/<id>/story.json; everything else (arms, hooks, scoring) is shared.
import fs from 'node:fs';
import path from 'node:path';
import { SPIKE_DIR } from './consts.mjs';
import { assertRunsRoot } from './safe-rm.mjs';

export const DEFAULT_STORY = 'creds-176';
export const LAYOUTS = Object.freeze(['native', 'resolver']);
const REQUIRED = ['repoClone', 'P', 'S', 'mount', 'layout', 'planFile', 'inputs', 'srcDir', 'struckFrom', 'defaultRunsRoot', 'prompt', 'canarySlug'];
const SHA = /^[0-9a-f]{40}$/;

/** Pure: a parsed story.json -> the validated story with paths resolved against the spike folder. Throws on any gap. */
export function parseStory(id, json, spikeDir = SPIKE_DIR) {
  const missing = REQUIRED.filter(k => json?.[k] === undefined || json[k] === '');
  if (missing.length > 0) throw new Error(`story ${id}: missing ${missing.join(', ')}`);
  if (!LAYOUTS.includes(json.layout)) throw new Error(`story ${id}: layout must be one of ${LAYOUTS.join('|')}, got ${json.layout}`);
  for (const k of ['P', 'S']) if (!SHA.test(json[k])) throw new Error(`story ${id}: ${k} is not a 40-hex sha`);
  if (json.layout === 'resolver' && !(Array.isArray(json.policyFiles) && json.policyFiles.length > 0)) throw new Error(`story ${id}: the resolver layout declares its policyFiles`);
  const story = {
    scrubLabel: '<repo>', parallelHint: false, policyFiles: [], ...json, id,
    inputsDir: path.resolve(spikeDir, json.inputs), promptFile: path.resolve(spikeDir, json.prompt),
  };
  return Object.freeze({ ...story, runMapFile: path.join(spikeDir, id === DEFAULT_STORY ? 'run-map.json' : `run-map.${id}.json`) });
}

export function loadStory(id = DEFAULT_STORY, spikeDir = SPIKE_DIR) {
  if (!/^[a-z0-9][a-z0-9-]*$/.test(id)) throw new Error(`bad story id: ${id}`);
  const file = path.join(spikeDir, 'stories', id, 'story.json');
  if (!fs.existsSync(file)) throw new Error(`no story ${id}: ${file} does not exist`);
  return parseStory(id, JSON.parse(fs.readFileSync(file, 'utf8')), spikeDir);
}

/** The option every script shares. */
export const STORY_OPTION = Object.freeze({ type: 'string', default: DEFAULT_STORY, help: 'story id under stories/ (default creds-176)' });

/** Pure: the instruction file a cell's tree gets the rule through, relative to the mount, for one overlay rule. */
export const overlayTarget = (story, rule) => `${story.mount}/${rule}`;

/** The runs root a script uses: --runs-root when given, else the story's own default. */
export const runsRootOf = (values, story) => assertRunsRoot(values['runs-root'] || story.defaultRunsRoot);
