// The ground of the tests that plug PR 5 into PR 3's and PR 4's seams (F3.S1 (b), PR 5): the temporary repository of `stage-prereg-fixture.mjs` made complete, so that the real pre-registration guard, run over it with a bare origin that
// holds a fake tag, finds a frozen record (PR 4's invented world over an invented task repository), a manifest, the registered prompts, a schedule file with its pilot, the price file and the policy. Nothing here is a real input.
// `omit` leaves one registered file out, `manifestHarness` writes another harness identity into the manifest, `prompts` and `schedule` replace what the file holds. Not a test file.
import { copyFileSync, mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { commit } from '../../minigit-bench/test/prereg-fixtures.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { REPO } from '../lib/paths.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { PREREG_DIR, STAGE_PILOT } from '../lib/stage-lists.mjs';
import { STAGE_PREREG_CONFIG } from '../lib/stage-prereg.mjs';
import { stagePrompts } from '../lib/stage-prompts.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';
import { world, writeRoot, put } from './stage-guard-fixture.mjs';
import { stageFixture, write } from './stage-prereg-fixture.mjs';

/** The v1 files a series pins when its price file and policy are not the pre-registration's: copied beside the fixture so that a series made over the fixture pins bytes the real checkout holds too. */
const V1_FILES = Object.freeze(['spikes/minigit-bench/inputs/prices-2026-10-29.json', 'spikes/minigit-bench/inputs/crosscheck-policy.json']);

/** The prompts file as the engine sends them: the template and, per task and stack, the text and its sha256. */
export function promptsFile() {
  const sent = stagePrompts(quickExplore(loadExplore().value, '/home/nobody'));
  const stacks = Object.fromEntries(Object.entries(sent.prompts).map(([stack, p]) => [stack, { text: p.text, sha256: p.sha256 }]));
  return { template: sent.template, templateSha256: sent.templateSha256, tasks: Object.fromEntries(FREEZE_TASKS.map(id => [id, stacks])) };
}

const schedule = () => ({ stage: 1, pilot: { aa: { ...STAGE_PILOT.aa, twins: ['a', 'b'] }, daemon: STAGE_PILOT.daemon, web: STAGE_PILOT.web } });

/** What the pre-registration's own files hold, over the harness identity of the repository as it is now. A file in `omit` is removed (it is then absent at the tag); every `[path, text]` of `add` is written. */
async function registerFiles(repo, { omit, add, manifestHarness, prompts, schedule: scheduleOver }) {
  write(repo, 'docs/rig.txt', 'the harness folders are committed before the manifest names their identity\n');
  commit(repo, 'the harness folders');
  const identity = (await harnessIdentity(repo, STAGE_PREREG_CONFIG)).value;
  const files = { 'manifest.json': { identities: { harness: manifestHarness ?? identity } }, 'schedule.json': scheduleOver ?? schedule(), 'prompts-lang.json': prompts ?? promptsFile() };
  for (const [name, value] of Object.entries(files)) put(path.join(repo, PREREG_DIR, name), value);
  for (const name of omit) rmSync(path.join(repo, PREREG_DIR, name));
  for (const [rel, text] of add) write(repo, rel, text);
}

/** The v1 files a series pins by default, copied from the real checkout (same bytes) into the fixture. */
function copyV1Files(repo) {
  for (const rel of V1_FILES) {
    mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true });
    copyFileSync(path.join(REPO, rel), path.join(repo, rel));
  }
}

/**
 * The complete repository: `{ f, w }`, `f` the fixture of `stage-prereg-fixture.mjs` (the working tree is on the series' branch, cut at the tag; the origin holds `main` and the tag) and `w` PR 4's invented world, whose record the repository
 * holds at the tag. An omitted file is absent at the tag's commit too.
 */
export async function preregRig(t, { omit = [], add = [], manifestHarness, prompts, schedule: scheduleOver } = {}) {
  const w = await world(t);
  const f = await stageFixture({ extra: async repo => {
    writeRoot(repo, w.c);
    copyV1Files(repo);
    await registerFiles(repo, { omit, add, manifestHarness, prompts, schedule: scheduleOver });
  } });
  return { f, w };
}
