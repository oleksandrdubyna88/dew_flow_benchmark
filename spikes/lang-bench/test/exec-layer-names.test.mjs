// S4 (coai 4): the owned marker of a child of a run is built through the SAME naming helper as the run's other names, never by `replace` on the holder's name.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { childMarkerOf, HOLDER_WHAT, holderNameOf, unitPrefixOf } from '../lib/exec-layer/names.mjs';
import { markerOf } from '../lib/exec-probe/lines.mjs';
import { owned } from './support/owned.mjs';

test('A_childs_marker_is_the_owned_prefix_of_its_kind_and_the_runs_id_and_does_not_depend_on_the_holders_name', () => {
  assert.equal(childMarkerOf(owned, 'probedriver', 'AbC123'), `${owned.ownedPrefix('probedriver')}AbC123`);
  assert.equal(holderNameOf(owned, 'AbC123'), childMarkerOf(owned, HOLDER_WHAT, 'AbC123'), 'the holder name and the markers come from one helper');
  assert.ok(unitPrefixOf(owned, 'AbC123').endsWith('-AbC123'));
  const run = { owned, id: 'AbC123', holderName: 'a-name-with-no-lhold-segment-at-all' };
  assert.equal(markerOf(run, 'probeserver'), `${owned.ownedPrefix('probeserver')}AbC123`, 'built from the id, not by replacing a segment of the holder name');
  assert.match(markerOf(run, 'probedriver'), /^testsToDelete-[uw]\d+-bench-probedriver-AbC123$/);
});
