// Which version of this harness made (or is about to use) a probe: the commit, the tree id of this folder, and whether the folder has
// uncommitted changes. The tree id is what the pilot's probe gate compares - an unrelated commit elsewhere does not void a probe, any change
// to the harness does.
import path from 'node:path';
import { REPO_ROOT, SPIKE_DIR } from './consts.mjs';
import { git, gitEnv } from './git.mjs';

export async function harnessIdentity() {
  const env = gitEnv();
  const rel = path.relative(REPO_ROOT, SPIKE_DIR).split(path.sep).join('/');
  const text = async args => (await git(REPO_ROOT, args, env)).toString('utf8').trim();
  return {
    commit: await text(['rev-parse', 'HEAD']),
    spikeTree: await text(['rev-parse', `HEAD:${rel}`]),
    dirty: (await text(['status', '--porcelain', '--', rel])) !== '',
  };
}
