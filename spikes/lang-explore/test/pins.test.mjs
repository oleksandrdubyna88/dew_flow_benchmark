// The hash pins (plan risk 2): the CLI copy, python3, the private notes, the installed TypeScript's integrity and both snapshots' tree hashes are checked before a pair starts; each one that moved is named.
// A temp home is laid out with files that match the pins, then broken one at a time.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore, pinnedPaths } from '../lib/inputs.mjs';
import { underHome } from '../lib/paths.mjs';
import { pinProblems, pinsOk } from '../lib/pins.mjs';

const sha = text => createHash('sha256').update(text).digest('hex');
const INTEGRITY = `sha512-${'Q'.repeat(86)}==`;

/** A home whose files match a copy of the pins, with snapshots whose hashes are the registered ones. */
function laidOut() {
  const home = tmpDir('pins');
  const real = loadExplore().value;
  const explore = JSON.parse(JSON.stringify(real));
  explore.typescript.integrity = INTEGRITY;
  const put = (parts, text) => { const f = underHome(parts, home); mkdirSync(path.dirname(f), { recursive: true }); writeFileSync(f, text); return f; };
  put(explore.cli.pathParts, 'the cli');
  explore.cli.sha256 = sha('the cli');
  put(explore.privateNotes.relativePath.split('/'), 'the notes');
  explore.privateNotes.sha256 = sha('the notes');
  const paths = pinnedPaths(explore, home);
  mkdirSync(paths.tsDir, { recursive: true });
  writeFileSync(path.join(paths.tsDir, 'package-lock.json'), JSON.stringify({ packages: { 'node_modules/typescript': { integrity: INTEGRITY } } }));
  const node = path.join(home, 'snap-node');
  const csharp = path.join(home, 'snap-cs');
  for (const [dir, rel] of [[node, 'npm/x'], [csharp, 'nuget/y']]) { mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true }); writeFileSync(path.join(dir, rel), rel); }
  explore.csharpSnapshot.treeSha256 = treeHash(csharp);
  const snapshots = { node: { dir: node, record: { treeSha256: treeHash(node) } }, csharp: { dir: csharp, record: {} } };
  return { home, explore, snapshots, paths };
}

test('When_every_pin_holds_there_is_no_problem', () => {
  const { home, explore, snapshots } = laidOut();
  assert.deepEqual(pinProblems({ explore, home, snapshots }), []);
  assert.equal(pinsOk({ explore, home, snapshots }).ok, true);
});

test('Each_pin_that_moved_is_named_and_the_refusal_says_a_pin_moves_by_a_dated_amendment', () => {
  const l = laidOut();
  const check = (name, mutate, pattern) => {
    const world = laidOut();
    mutate(world);
    const problems = pinProblems({ explore: world.explore, home: world.home, snapshots: world.snapshots });
    assert.equal(problems.length, 1, `${name}: ${problems.join(' | ')}`);
    assert.match(problems[0], pattern, name);
  };
  check('the CLI copy', w => writeFileSync(w.paths.claudeBinary, 'another cli'), /the pinned CLI copy .* hashes to/);
  check('the CLI copy is gone', w => { w.explore.cli.pathParts = ['nowhere']; }, /the pinned CLI copy .* is missing/);
  check('python3', w => { w.explore.python3.sha256 = '0'.repeat(64); }, /not the pinned python3/);
  check('the private notes', w => writeFileSync(underHome(w.explore.privateNotes.relativePath.split('/'), w.home), 'edited after the pilot was planned'), /the private test notes .* hashes to/);
  check('the TypeScript integrity', w => writeFileSync(path.join(w.paths.tsDir, 'package-lock.json'), JSON.stringify({ packages: { 'node_modules/typescript': { integrity: `sha512-${'Z'.repeat(86)}==` } } })), /installed TypeScript's integrity/);
  check('the node snapshot', w => writeFileSync(path.join(w.snapshots.node.dir, 'npm/x'), 'changed'), /the node snapshot .* no longer hashes/);
  check('the csharp snapshot', w => writeFileSync(path.join(w.snapshots.csharp.dir, 'nuget/y'), 'changed'), /the csharp snapshot .* no longer hashes/);
  const bad = laidOut();
  writeFileSync(bad.paths.claudeBinary, 'x');
  const refusal = pinsOk({ explore: bad.explore, home: bad.home, snapshots: bad.snapshots });
  assert.equal(refusal.ok, false);
  assert.match(refusal.reason, /1 pin\(s\) do not hold: .*dated amendment before pair 1, never between pairs/);
  assert.ok(l.home);
});
