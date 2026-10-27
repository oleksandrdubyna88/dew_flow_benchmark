

```ts
test('kimi rows admit flagged, web refuses naming its record', () => {
  for (const cap of ['none', 'disk']) {
    const r = admit('kimi', cap);
    assert.equal(r.admitted, true, `${cap} admitted`);
    assert.equal(r.flag, 'unconfined', `${cap} unconfined`);
    assert.match(r.caveat, /can read this machine/u, `${cap} caveat mentions reading machine`);
  }
  const w = admit('kimi', 'web');
  assert.equal(w.admitted, false, 'web refused');
  assert.match(w.reason, /not been measured/u, 'web reason mentions measurement');
  assert.match(w.reason, /RESULTS_kimi_confinement\.md/u, 'web reason cites confinement record');
});
```