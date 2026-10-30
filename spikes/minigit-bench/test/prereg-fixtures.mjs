// Shared fixtures of the pre-registration tests: a real temporary repository with every registered file, one tagged commit and a bare origin that holds both, and the two
// registered files the consistency check reads (the SDK a C# prompt names against the pin of stacks.json).
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { PREREG_FILES } from '../lib/prereg-guard.mjs';
import { tmpDir } from './helpers.mjs';

export const TAG = 'prereg/minigit-bare-20261030';
export const INPUTS = 'spikes/minigit-bench/inputs';
export const SEED = 'mg-0123456789abcdef';
export const sha256 = text => createHash('sha256').update(text).digest('hex');
/** The two registered files the guard reads for its consistency check; every other registered file is a placeholder text. */
export const stacksText = sdk => `${JSON.stringify({ pins: { claude: '2.1.310', dotnetSdk: sdk } })}\n`;
export const promptsText = sdk => `${JSON.stringify({ prompts: { csharp: { v1: { text: `Implement it using C#. Use C# 14 on .NET 10 (SDK ${sdk} is installed).` }, v2: { text: `Modify it. Use C# 14 on .NET 10 (SDK ${sdk} is installed).` } }, node: { v1: { text: 'Implement it using TypeScript.' }, v2: { text: 'Modify it.' } } } })}\n`;

export function git(cwd, ...args) {
  const r = spawnSync('git', args, { cwd, encoding: 'utf8', env: { ...process.env, GIT_AUTHOR_NAME: 't', GIT_AUTHOR_EMAIL: 't@t', GIT_COMMITTER_NAME: 't', GIT_COMMITTER_EMAIL: 't@t' } });
  assert.equal(r.status, 0, `git ${args.join(' ')}: ${r.stderr}`);
  return r.stdout.trim();
}
export const write = (repo, rel, text) => { mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true }); writeFileSync(path.join(repo, rel), text); };
export const commit = (repo, msg) => { git(repo, 'add', '-A'); git(repo, 'commit', '-q', '-m', msg); return git(repo, 'rev-parse', 'HEAD'); };

/** A repo with every registered file, harness code, one commit, an annotated tag on it and a bare origin holding both. */
export function fixture({ push = true, sdk = '10.0.114', pin = sdk } = {}) {
  const remote = tmpDir('remote');
  git(remote, 'init', '-q', '--bare');
  const repo = tmpDir('repo');
  git(repo, 'init', '-q', '-b', 'main');
  git(repo, 'remote', 'add', 'origin', remote);
  for (const f of new Set([...PREREG_FILES, 'stacks.json'])) write(repo, `${INPUTS}/${f}`, f === 'seed.sha256' ? `${sha256(SEED)}\n` : f === 'crosscheck-policy.json' ? '{"t1":"stop","t2":"otel-incomplete","t3":"investigate","t5":"investigate","join":"request_id","t3OutputSource":"transcript","decidedOn":"2026-10-29","reason":"a"}\n' : f === 'amendments.md' ? '# Dated amendments\n' : f === 'stacks.json' ? stacksText(pin) : f === 'prompts-bare.json' ? promptsText(sdk) : `registered ${f}\n`);
  for (const d of ['lib', 'harness', 'capture', 'prereg']) write(repo, `spikes/minigit-bench/${d}/x.mjs`, `// ${d}\n`);
  write(repo, 'spikes/minigit-bench/top.mjs', '// top\n');
  for (const d of ['lib', 'hooks']) write(repo, `spikes/policy-ab-quick/${d}/y.mjs`, `// ${d}\n`);
  const sha = commit(repo, 'prereg');
  git(repo, 'tag', '-a', TAG, '-m', 'prereg', sha);
  if (push) { git(repo, 'push', '-q', 'origin', 'main', TAG); }
  return { repo, remote, sha, runMap: { preregTag: TAG, preregCommit: sha, harnessIdentity: null, harnessAccepted: [] } };
}
