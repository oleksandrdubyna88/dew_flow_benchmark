// The dotnet hide list (E2 amendment 2026-11-19, D-9): on a host that holds several SDKs, a box on the distribution's dotnet shows only the pin's version folders. Pure: an invented listing, no folder.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { DISTRIBUTION_DOTNET, isSdkVersion, otherDotnetVersions } from '../lib/dotnet-hide.mjs';
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

test('A_pin_is_a_version_as_a_WHOLE_string_x_y_z_with_at_most_a_prerelease_suffix', () => {
  const host = fakeDirs(twoSdkHost());
  // the refuted reading: a pattern anchored only at the start took `10.0.114typo` for the pin 10.0.114's major and built a box that hid SDK 10.0.114 itself
  for (const pin of ['10.0.114typo', '10.0.114 ', ' 10.0.114', 'v10.0.114', '10.0.114.1x', '10.0.114-', '10.0', '10.0.114\n11.0.100']) {
    assert.equal(isSdkVersion(pin), false, JSON.stringify(pin));
    assert.throws(() => otherDotnetVersions({ pin, ...host }), /is not an SDK version/, JSON.stringify(pin));
  }
  for (const pin of ['10.0.114', '11.0.100-preview.6', '11.0.100-rc.1', '11.0.100-rc.1.25451.107']) {
    assert.equal(isSdkVersion(pin), true, pin);
    assert.doesNotThrow(() => otherDotnetVersions({ pin, ...host }), pin);
  }
  assert.deepEqual(otherDotnetVersions({ pin: '11.0.100-preview.6', ...fakeDirs([...twoSdkHost(), `${ROOT}/sdk/11.0.100-preview.6`]) }).filter(p => p.startsWith(`${ROOT}/sdk/`)), [`${ROOT}/sdk/10.0.114`, `${ROOT}/sdk/11.0.100`], 'a prerelease pin keeps its own SDK folder and hides the others');
});

test('A_folder_whose_name_only_starts_like_a_version_is_no_version_and_is_left_alone', () => {
  assert.deepEqual(hideOf([...oneSdkHost(), `${ROOT}/sdk/11.0.100typo`, `${ROOT}/templates/11.0.0.bak`]), []);
});

/** Every kind of folder the list walks, as a link on the two-SDK host: a version folder it would hide, and a framework or pack folder it would look into. */
const LINKS = [
  `${ROOT}/sdk/${SDK_11.sdk}`, `${ROOT}/shared/Microsoft.NETCore.App/${SDK_11.runtime}`, `${ROOT}/packs/Microsoft.NETCore.App.Ref/${SDK_11.runtime}`, `${ROOT}/templates/${SDK_11.templates}`,
  `${ROOT}/sdk-manifests/${SDK_11.bands[1]}`, `${ROOT}/shared/Microsoft.NETCore.App`, `${ROOT}/packs/Microsoft.NETCore.App.Ref`,
];

for (const link of LINKS) {
  test(`A_link_at_${link.slice(ROOT.length + 1).replace(/\W/g, '_')}_is_refused_naming_its_path_and_nothing_is_listed`, () => {
    const host = fakeDirs(twoSdkHost(), { links: [link] });
    assert.equal(host.listDir(link.slice(0, link.lastIndexOf('/'))).find(e => e.name === link.slice(link.lastIndexOf('/') + 1)).link, true, 'the invented listing reports the link, as the real one does');
    assert.throws(() => otherDotnetVersions({ pin: PIN, ...host }), e => e.message.startsWith(`${link} is a symbolic link`) && /cannot be hidden/.test(e.message), link);
  });
}

test('A_link_the_list_would_not_mount_over_is_no_refusal_the_pins_own_folder_and_a_name_that_is_no_version', () => {
  const dirs = twoSdkHost();
  const host = fakeDirs(dirs, { links: [`${ROOT}/sdk/${PIN}`, `${ROOT}/sdk/current`, `${ROOT}/shared/Microsoft.NETCore.App/10.0.14`] });
  assert.deepEqual([...otherDotnetVersions({ pin: PIN, ...host })].sort(), otherVersionDirs().sort(), 'the same list as with no link: the pin\'s own versions are never mounted over, and the muxer takes no folder named `current` for an SDK');
});

test('The_roots_are_one_list_and_the_quick_profile_reads_the_same_one', () => {
  assert.equal(QUICK_ROOTS, DISTRIBUTION_DOTNET, 'lang-explore re-exports this list, it does not keep a second one');
  assert.ok(DISTRIBUTION_DOTNET.includes(ROOT) && Object.isFrozen(DISTRIBUTION_DOTNET));
});
