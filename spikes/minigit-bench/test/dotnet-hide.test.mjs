// The dotnet hide list (E2 amendment 2026-11-19, D-9): on a host that holds several SDKs, a box on the distribution's dotnet shows only the pin's version folders. Pure: an invented listing, no folder.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { DISTRIBUTION_DOTNET, otherDotnetVersions } from '../lib/dotnet-hide.mjs';
import { DISTRIBUTION_DOTNET as QUICK_ROOTS } from '../../lang-explore/lib/quick.mjs';
import { PIN, ROOT, SDK_11, fakeDirs, oneSdkHost, otherVersionDirs, twoSdkHost } from './dotnet-fixture.mjs';

const hideOf = (dirs, over = {}) => otherDotnetVersions({ pin: PIN, ...fakeDirs(dirs), ...over });

test('On_a_host_with_two_sdks_the_list_is_exactly_the_other_sdk_and_the_other_major_of_shared_packs_templates_and_manifests', () => {
  const hidden = hideOf(twoSdkHost());
  assert.deepEqual([...hidden].sort(), otherVersionDirs().sort());
  assert.ok(hidden.includes(`${ROOT}/sdk/${SDK_11.sdk}`), 'the other SDK');
  assert.ok(hidden.includes(`${ROOT}/sdk-manifests/11.0.100-preview.6`), 'a preview band of the other major is another major too');
  assert.equal(hidden.some(p => p.includes('/10.')), false, 'nothing of the pin\'s major is hidden: not its runtime 10.0.14, nor the 10.0.15 templates and AOT pack');
  assert.ok(Object.isFrozen(hidden));
});

test('The_host_resolver_under_host_fxr_is_never_listed', () => {
  const dirs = twoSdkHost();
  assert.ok(dirs.includes(`${ROOT}/host/fxr/${SDK_11.runtime}`), 'the fixture holds the other resolver, so its absence from the list is a decision and not an empty fixture');
  assert.deepEqual(hideOf(dirs).filter(p => p.startsWith(`${ROOT}/host`)), []);
});

test('A_host_that_holds_only_the_pin_hides_nothing', () => {
  assert.deepEqual(hideOf(oneSdkHost()), []);
});

test('A_root_that_does_not_exist_gives_nothing_and_is_never_listed', () => {
  const asked = [];
  const fake = fakeDirs(twoSdkHost('/usr/share/dotnet'));
  const hidden = otherDotnetVersions({ pin: PIN, exists: fake.exists, listDir: dir => { asked.push(dir); return fake.listDir(dir); } });
  assert.deepEqual([...hidden].sort(), otherVersionDirs('/usr/share/dotnet').sort(), 'the root that exists is read');
  assert.deepEqual(asked.filter(d => d.startsWith(ROOT)), [], `${ROOT} is not on this invented host: it is not listed`);
  assert.deepEqual(otherDotnetVersions({ pin: PIN, exists: () => false, listDir: () => { throw new Error('a host with no root is not listed'); } }), []);
});

test('Another_sdk_of_the_pins_own_major_is_hidden_and_its_runtime_is_not', () => {
  const dirs = [...oneSdkHost(), `${ROOT}/sdk/10.0.113`, `${ROOT}/shared/Microsoft.NETCore.App/10.0.13`];
  assert.deepEqual(hideOf(dirs), [`${ROOT}/sdk/10.0.113`], 'sdk/<v> for every v that is not the pin; shared by major only');
});

test('A_folder_that_is_not_a_version_is_left_alone', () => {
  const dirs = [...oneSdkHost(), `${ROOT}/sdk/NuGetFallbackFolder`, `${ROOT}/packs/Some.Pack/tools`];
  assert.deepEqual(hideOf(dirs), []);
});

test('A_pin_that_is_not_a_version_is_refused_naming_the_shape', () => {
  for (const pin of [undefined, null, '', 'latest', '10', 10]) {
    assert.throws(() => otherDotnetVersions({ pin, ...fakeDirs(twoSdkHost()) }), /dotnetPin .* is not an SDK version .*10\.0\.114/, String(pin));
  }
});

test('The_roots_are_one_list_and_the_quick_profile_reads_the_same_one', () => {
  assert.equal(QUICK_ROOTS, DISTRIBUTION_DOTNET, 'lang-explore re-exports this list, it does not keep a second one');
  assert.ok(DISTRIBUTION_DOTNET.includes(ROOT) && Object.isFrozen(DISTRIBUTION_DOTNET));
});
