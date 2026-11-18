// The order of the freeze procedure (F2.S2 (e), step 6 before step 7): nothing irreversible before the workspaces are proven on the scratch W″. The procedure is written here as the sequence of the team lead's steps over
// the tools of this repository (the freeze verbs, `workspace-diff`) and two seams: a fake `leakcheck` and a fake push. A failed proof must end the sequence before the push seam is called; a clean one must reach it.
// Synthetic fixtures only (test/support/freeze-fixtures.mjs); the remote is a local bare repository and the tag push a fake seam over a real push into it.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { diffAllTasks } from '../lib/workspace-diff.mjs';
import { annotate, freezeAndMerge, makeFreezeRepo, pushTag, TAG, workspaceFiles, workspacesBranch } from './support/freeze-fixtures.mjs';
import { git } from './support/push-fixtures.mjs';

const remoteTags = f => git(f.repo, 'ls-remote', '--tags', 'origin');

/**
 * Steps 4 to 7 of (e) over the tools: T1 merged as C; the scratch W″ cut from C with the twelve workspaces; `check-workspace --base C` and `workspace-diff` and the fake leak check over W″; and only then the tag, its
 * local check and the push. `files` are the generated workspaces (a planted fault goes there), `leakcheck(commit)` answers the fake scan (`{ exit }`), `push()` is the fake push seam. -> `{ exit, pushed }`.
 */
async function procedure(f, { files = workspaceFiles(), leakcheck = () => ({ exit: 0 }), push }) {
  const { c } = freezeAndMerge(f);
  const scratch = git(f.repo, 'rev-parse', workspacesBranch(f, { tag: 'scratch', from: c, files }));
  const proofs = [
    async () => (await f.run('check-workspace', ['--base', c, '--commit', scratch])).exit,
    () => diffAllTasks({ root: f.repo }).ok ? 0 : 1,
    () => leakcheck(scratch).exit,
  ];
  for (const proof of proofs) if ((await proof()) !== 0) return { exit: 1, pushed: false };
  const message = annotate(f, { at: c });
  if ((await f.run('check-local-tag', ['--tag', TAG, '--commit', c, '--message-file', message])).exit !== 0) return { exit: 1, pushed: false };
  push();
  return { exit: 0, pushed: true };
}

test('A_workspace_proof_that_fails_on_the_scratch_W_stops_before_the_tag_is_pushed', async t => {
  const wrongByte = workspaceFiles({ 'node/05-key-value-server/samples/s01.in': '4\n' });
  const needle = { leakcheck: commit => ({ exit: commit.length === 40 ? 1 : 0 }) };
  for (const [what, options] of [['a planted wrong byte in one generated workspace', { files: wrongByte }], ['a planted needle seen by the fake leak check on W″', needle]]) {
    const f = makeFreezeRepo(t);
    let calls = 0;
    const result = await procedure(f, { ...options, push: () => { calls += 1; } });
    assert.notEqual(result.exit, 0, what);
    assert.equal(calls, 0, `${what}: the push seam is never called`);
    assert.equal(remoteTags(f), '', `${what}: no tag exists on the bare remote`);
  }
  const f = makeFreezeRepo(t);
  let calls = 0;
  const clean = await procedure(f, { push: () => { calls += 1; pushTag(f); } });
  assert.equal(clean.exit, 0, 'the control, a clean proof');
  assert.equal(calls, 1, 'reaches the push');
  assert.ok(remoteTags(f).includes(`refs/tags/${TAG}`), 'and the tag is on the remote');
});
