// What this proves, and what it does not. The freeze procedure ((e) steps 4 to 7: T1 merged as C, the workspaces proven on a scratch W″, then the tag, its local check and the push) is a MANUAL order that the team lead follows; no tool enforces it.
// The `procedure` below is that order written as a test helper over the real tools (`check-workspace --base`, `workspace-diff`, `check-local-tag`) and a fake push seam. The test shows that the tools CATCH a planted fault in the generated
// workspaces (a wrong byte in one stack's copy), so that a procedure that stops on their exit status never reaches the push; it does not show that the team lead stops. The leak check on the scratch W″ is the real `leakcheck`'s, run by hand in
// the dry run and at step 6; it is not simulated here. Synthetic fixtures only (test/support/freeze-fixtures.mjs); the remote is a local bare repository.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { diffAllTasks } from '../lib/workspace-diff.mjs';
import { annotate, freezeAndMerge, makeFreezeRepo, pushTag, TAG, workspaceFiles, workspacesBranch } from './support/freeze-fixtures.mjs';
import { git } from './support/push-fixtures.mjs';

const remoteTags = f => git(f.repo, 'ls-remote', '--tags', 'origin');

/**
 * Steps 4 to 7 of (e) over the tools: T1 merged as C; the scratch W″ cut from C with the twelve workspaces; `check-workspace --base C` and `workspace-diff` over W″; and only then the tag, its
 * local check and the push. `files` are the generated workspaces (a planted fault goes there), `push()` is the fake push seam. -> `{ exit, pushed }`.
 */
async function procedure(f, { files = workspaceFiles(), push }) {
  const { c } = freezeAndMerge(f);
  const scratch = git(f.repo, 'rev-parse', workspacesBranch(f, { tag: 'scratch', from: c, files }));
  const proofs = [
    async () => (await f.run('check-workspace', ['--base', c, '--commit', scratch])).exit,
    () => (diffAllTasks({ root: f.repo }).ok ? 0 : 1),
  ];
  for (const proof of proofs) if ((await proof()) !== 0) return { exit: 1, pushed: false };
  const message = annotate(f, { at: c });
  if ((await f.run('check-local-tag', ['--tag', TAG, '--commit', c, '--message-file', message])).exit !== 0) return { exit: 1, pushed: false };
  push();
  return { exit: 0, pushed: true };
}

test('A_workspace_proof_that_fails_on_the_scratch_W_stops_before_the_tag_is_pushed', async t => {
  const wrongByte = workspaceFiles({ 'node/05-key-value-server/samples/s01.in': '4\n' });
  const planted = makeFreezeRepo(t);
  let plantedCalls = 0;
  const result = await procedure(planted, { files: wrongByte, push: () => { plantedCalls += 1; } });
  assert.notEqual(result.exit, 0, 'a planted wrong byte in one generated workspace');
  assert.equal(plantedCalls, 0, 'the push seam is never called');
  assert.equal(remoteTags(planted), '', 'no tag exists on the bare remote');
  const f = makeFreezeRepo(t);
  let calls = 0;
  const clean = await procedure(f, { push: () => { calls += 1; pushTag(f); } });
  assert.equal(clean.exit, 0, 'the control, a clean proof');
  assert.equal(calls, 1, 'reaches the push');
  assert.ok(remoteTags(f).includes(`refs/tags/${TAG}`), 'and the tag is on the remote');
});
