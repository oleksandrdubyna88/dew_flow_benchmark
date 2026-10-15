#!/usr/bin/env node
// The arm-parity probe, before generation 1: prepare one verified tree per arm (prepare-cell --verify-only: rules check, compile,
// adapter, SessionStart, then P's own suite), then require (a) the SAME failing set in both and (b) mounts that differ in exactly
// the story's declared policy files. Writes <probe root>/probe.json; the pilot refuses a resolver-layout series without a passing one.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseCli } from './lib/args.mjs';
import { harnessIdentity } from './lib/harness.mjs';
import { compareProbes, probeSubject } from './lib/probe.mjs';
import { ARM_SHAS } from './lib/consts.mjs';
import { gitlinkAt } from './lib/git.mjs';
import { assertRunsRoot } from './lib/safe-rm.mjs';
import { loadStory, STORY_OPTION } from './lib/story.mjs';
import { mountDirOf, mountFileMap } from './lib/tree.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SPEC = {
  story: STORY_OPTION,
  'probe-root': { type: 'string', help: "where the probe trees go (default: the story's runs root + '-probe'); must not exist yet" },
};

function prepare(args) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [path.join(HERE, 'prepare-cell.mjs'), ...args], { stdio: 'inherit', shell: false });
    child.on('error', reject);
    child.on('close', code => (code === 0 ? resolve() : reject(new Error(`prepare-cell exited ${code}`))));
  });
}

const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8'));

async function main(v) {
  const story = loadStory(v.story);
  if (story.layout !== 'resolver') throw new Error('the arm-parity probe is for the resolver layout');
  const root = assertRunsRoot(v['probe-root'] || `${story.defaultRunsRoot}-probe`);
  if (fs.existsSync(path.join(root, 'probe-A')) || fs.existsSync(path.join(root, 'probe-B'))) throw new Error(`refusing: ${root} already holds a probe`);
  for (const arm of ['A', 'B']) await prepare(['--verify-only', '--arm', arm, '--story', story.id, '--runs-root', root]);
  const suite = arm => readJson(path.join(root, `probe-${arm}`, 'suite.json'));
  const map = arm => mountFileMap(mountDirOf(path.join(root, `probe-${arm}`, 'tree'), story));
  const startOf = arm => readJson(path.join(root, `probe-${arm}`, 'cell.json')).sessionStartProbe;
  const result = compareProbes({ suiteA: suite('A'), suiteB: suite('B'), mapA: await map('A'), mapB: await map('B'), policyFiles: story.policyFiles, sessionStartA: startOf('A'), sessionStartB: startOf('B') });
  const harness = await harnessIdentity();
  const pin = await gitlinkAt(story.repoClone, story.P, story.mount);
  const record = { story: story.id, subject: probeSubject({ story, pin, armShas: ARM_SHAS, harness }), harness, createdUtc: new Date().toISOString(), ...result };
  fs.writeFileSync(path.join(root, 'probe.json'), `${JSON.stringify(record, null, 2)}\n`);
  console.log(result.pass ? `probe PASSED: same failing set (${result.failingSetA.length} of ${result.testsA} tests), mounts differ in [${result.mountDelta.join(', ')}]` : `probe FAILED:\n  ${result.reasons.join('\n  ')}`);
  if (!result.pass) process.exitCode = 1;
}

if (process.argv[1]?.endsWith('probe-series.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node probe-series.mjs --story coai-751 [--probe-root D:/rsd/_s2-probe]');
  if (help) console.log(help);
  else main(values).catch(e => { console.error(e.message); process.exitCode = 1; });
}
