```ts
test('kimi rows admit flagged, web refuses naming its record', () => {
  const noneResult = admit('kimi', 'none');
  assert.equal(noneResult.admitted, true);
  assert.match(noneResult.caveat, /can read this machine/u);

  const diskResult = admit('kimi', 'disk');
  assert.equal(diskResult.admitted, true);
  assert.match(diskResult.caveat, /can read this machine/u);

  const webResult = admit('kimi', 'web');
  assert.equal(webResult.admitted, false);
  assert.match(webResult.reason, /not been measured/u);
  assert.match(webResult.reason, /RESULTS_kimi_confinement\.md/u);
});
```